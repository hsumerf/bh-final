using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BrailleUrdu
{
    public class BrailleTextBox : UserControl
    {
        // ── Layout constants ──────────────────────────────────────────────────
        private static float PxPerMm => 96f / 25.4f * 1.10f;
        private static int   CellPx  => (int)(DocumentPage.CELL_WIDTH_MM  * PxPerMm);
        private static int   LinePx  => (int)(DocumentPage.LINE_HEIGHT_MM * PxPerMm);

        private const int HANDLE_SIZE = 10;
        private const int PAD         = 12;

        // ── Content & cursor ─────────────────────────────────────────────────
        private string _text            = "";
        private int    _cursorPos       = 0;
        private int    _selectionAnchor = 0;

        public string BrailleText
        {
            get => _text;
            set { _text = value ?? ""; _cursorPos = _text.Length; _selectionAnchor = _cursorPos; FitSize(); }
        }

        public event EventHandler BrailleTextChanged;

        public bool IsSelected     { get; set; }
        public bool IsTextEditing => _textEditMode;

        private int    SelStart     => Math.Min(_selectionAnchor, _cursorPos);
        private int    SelEnd       => Math.Max(_selectionAnchor, _cursorPos);
        private bool   HasSelection => _selectionAnchor != _cursorPos;
        private string SelectedText => _text.Substring(SelStart, SelEnd - SelStart);

        // ── Search highlight ──────────────────────────────────────────────────
        private string _searchHighlight;

        public void SetSearchHighlight(string braillePattern)
        {
            _searchHighlight = braillePattern;
            Invalidate();
        }

        public void ClearSearchHighlight()
        {
            _searchHighlight = null;
            Invalidate();
        }

        // ── Mode ──────────────────────────────────────────────────────────────
        private bool _textEditMode  = false;
        private bool _justGotFocus  = false; // true from OnGotFocus until first OnMouseDown

        private readonly Timer _caretTimer   = new Timer { Interval = 530 };
        private bool           _caretVisible = false;

        // ── Drag / resize state ───────────────────────────────────────────────
        private bool         _dragging;
        private bool         _resizing;
        private Point        _mouseDownScreen;
        private Point        _startLocation;
        private Size         _startSize;
        private ResizeHandle _activeHandle = ResizeHandle.None;
        private System.Collections.Generic.Dictionary<Control, Point> _groupStartLocations;
        private int          _pendingClickIdx  = -1; // deferred single-click text entry
        private int          _ownClickCount    = 0;  // our own click counter (e.Clicks unreliable >2)
        private int          _lastClickTick    = 0;
        private Point        _lastClickScreen  = Point.Empty;

        // ── Construction ──────────────────────────────────────────────────────
        public BrailleTextBox()
        {
            SetStyle(
                ControlStyles.Selectable                   |
                ControlStyles.UserPaint                    |
                ControlStyles.AllPaintingInWmPaint         |
                ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, false);

            ResizeRedraw = true;
            BackColor    = Color.Transparent;
            TabStop      = true;
            MinimumSize  = new Size(CellPx + PAD * 2, LinePx);
            Size         = new Size(CellPx + PAD * 2, LinePx);

            _caretTimer.Tick += (s, e) => { _caretVisible = !_caretVisible; Invalidate(); };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _caretTimer.Dispose();
            base.Dispose(disposing);
        }

        // ── Text-edit mode helpers ────────────────────────────────────────────
        private void EnterTextMode(int cursorIndex = -1)
        {
            _textEditMode    = true;
            _cursorPos       = cursorIndex < 0 ? _text.Length : cursorIndex;
            _selectionAnchor = _cursorPos;
            _caretVisible    = true;
            _caretTimer.Start();
            Invalidate();
        }

        private void ExitTextMode()
        {
            _textEditMode    = false;
            _selectionAnchor = _cursorPos;
            _caretTimer.Stop();
            _caretVisible    = false;
            Invalidate();
        }

        private void ApplyTextChange(string newText, int newCursor)
        {
            _text            = newText;
            _cursorPos       = newCursor;
            _selectionAnchor = newCursor;
            _caretVisible    = true;
            BrailleTextChanged?.Invoke(this, EventArgs.Empty);
            FitSize();
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            if (Parent != null) FitSize();
        }

        private void FitSize()
        {
            int cellW = CellPx;
            int lineH = LinePx;

            int maxWidth;
            var canvas = Parent as CanvasPanel;
            if (canvas != null)
                maxWidth = Math.Max(MinimumSize.Width, (int)canvas.MarginBoundsPx.Right - Left);
            else
                maxWidth = PAD * 2 + Math.Max(1, _text.Length) * cellW;

            int singleLineW = PAD * 2 + _text.Length * cellW;
            int newWidth    = Math.Max(MinimumSize.Width, Math.Min(singleLineW, maxWidth));

            var layout = BuildLayout(newWidth);
            int newHeight = (layout[_text.Length].Y + 1) * lineH;

            if (Width != newWidth || Height != newHeight)
                SetBounds(Left, Top, newWidth, newHeight);
            else
                Invalidate();
        }

        // Returns (col, row) for each character in _text plus one extra entry for the
        // after-last-char cursor. Words (non-space runs) wrap as a unit; individual
        // characters within an overlong word hard-wrap character by character.
        private Point[] BuildLayout(int width)
        {
            float cellW = CellPx;
            float availW = width - PAD * 2;
            var pos = new Point[_text.Length + 1];
            int col = 0, row = 0;
            int i = 0;

            while (i < _text.Length)
            {
                if (_text[i] == '⠀') // braille space — word boundary
                {
                    if (col > 0 && (col + 1) * cellW > availW) { row++; col = 0; }
                    pos[i] = new Point(col, row);
                    col++;
                    i++;
                }
                else
                {
                    int wEnd = i;
                    while (wEnd < _text.Length && _text[wEnd] != '⠀') wEnd++;
                    int wLen = wEnd - i;

                    if (col > 0 && (col + wLen) * cellW > availW) { row++; col = 0; }

                    for (int j = i; j < wEnd; j++)
                    {
                        if (col > 0 && (col + 1) * cellW > availW) { row++; col = 0; }
                        pos[j] = new Point(col, row);
                        col++;
                    }
                    i = wEnd;
                }
            }
            pos[_text.Length] = new Point(col, row);
            return pos;
        }

        private int ComputeHeight(int width)
        {
            var layout = BuildLayout(width);
            return (layout[_text.Length].Y + 1) * LinePx;
        }

        private void DeleteSelection()
        {
            int s = SelStart, end = SelEnd;
            ApplyTextChange(_text.Substring(0, s) + _text.Substring(end), s);
        }

        private void DeleteSelf()
        {
            BeginInvoke((Action)(() =>
            {
                Parent?.Controls.Remove(this);
                Dispose();
            }));
        }

        // ── Transparency ──────────────────────────────────────────────────────
        protected override CreateParams CreateParams
        {
            get { var cp = base.CreateParams; cp.ExStyle |= 0x20; return cp; }
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == 0x84 && m.Result == (IntPtr)(-1)) m.Result = (IntPtr)1;
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        // ── Paint ─────────────────────────────────────────────────────────────
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.None;

            var layout = BuildLayout(Width);
            DrawSearchHighlight(g, layout);
            DrawBrailleSelection(g, layout);
            DrawBrailleDots(g, layout);

            if (Focused)
            {
                if (_textEditMode && _caretVisible) DrawCaret(g, layout);

                using (var pen = new Pen(Color.DodgerBlue, 2f))
                    g.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);

                foreach (var h in new[] {
                    ResizeHandle.TopLeft,    ResizeHandle.TopRight,
                    ResizeHandle.MiddleLeft, ResizeHandle.MiddleRight,
                    ResizeHandle.BottomLeft, ResizeHandle.BottomRight })
                {
                    var r = GetHandleRect(h);
                    g.FillRectangle(Brushes.DodgerBlue, r);
                    using (var pen = new Pen(Color.White, 1f))
                        g.DrawRectangle(pen, r);
                }
            }
            else if (IsSelected)
            {
                using (var pen = new Pen(Color.DodgerBlue, 1.5f) { DashStyle = DashStyle.Dash })
                    g.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
            }
            else if (_text.Length == 0)
            {
                using (var pen = new Pen(Color.FromArgb(160, 160, 160), 1f) { DashStyle = DashStyle.Dash })
                    g.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
            }
        }

        // ── Search highlight ──────────────────────────────────────────────────
        private void DrawSearchHighlight(Graphics g, Point[] layout)
        {
            if (string.IsNullOrEmpty(_searchHighlight)) return;
            int   searchLen = _searchHighlight.Length;
            float cellW     = CellPx;
            float lineH     = LinePx;

            using (var brush = new SolidBrush(Color.FromArgb(180, 255, 210, 0)))
            {
                int pos = 0;
                while (pos <= _text.Length - searchLen)
                {
                    int match = _text.IndexOf(_searchHighlight, pos, StringComparison.Ordinal);
                    if (match < 0) break;

                    for (int i = match; i < match + searchLen; i++)
                    {
                        float ox = PAD + layout[i].X * cellW;
                        float oy = layout[i].Y * lineH;
                        g.FillRectangle(brush, ox, oy, cellW, lineH);
                    }
                    pos = match + searchLen;
                }
            }
        }

        // ── Selection highlight ───────────────────────────────────────────────
        private void DrawBrailleSelection(Graphics g, Point[] layout)
        {
            if (!HasSelection || !_textEditMode) return;
            int start = SelStart, end = SelEnd;
            float cellW = CellPx, lineH = LinePx;
            using (var brush = new SolidBrush(Color.FromArgb(80, 51, 153, 255)))
            {
                for (int i = start; i < end; i++)
                {
                    float ox = PAD + layout[i].X * cellW;
                    float oy = layout[i].Y * lineH;
                    g.FillRectangle(brush, ox, oy, cellW, lineH);
                }
            }
        }

        // ── Dot rendering ─────────────────────────────────────────────────────
        private void DrawBrailleDots(Graphics g, Point[] layout)
        {
            float dotSpacePx = DocumentPage.DOT_SPACING_MM * PxPerMm;
            float dotRad     = Math.Max(1.5f, dotSpacePx * 0.27f);
            float cellW      = CellPx;
            float lineH      = LinePx;
            // Center the dot grid within the cell rectangle so dots sit fully
            // inside the selection/highlight rectangle [ox, ox+cellW] x [oy, oy+lineH].
            float dotOffsetX = (cellW - dotSpacePx)       / 2f;
            float dotOffsetY = (lineH  - 2 * dotSpacePx)  / 2f;

            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var brush = new SolidBrush(Color.FromArgb(34, 139, 34)))
            {
                for (int i = 0; i < _text.Length; i++)
                {
                    char c = _text[i];
                    if ((int)c < 0x2800 || (int)c > 0x28FF) continue;

                    float ox = PAD + layout[i].X * cellW;
                    float oy = layout[i].Y * lineH;

                    int bits = (int)c - 0x2800;
                    for (int b = 0; b < 8; b++)
                    {
                        if ((bits & (1 << b)) == 0) continue;
                        int dcol = b < 6 ? b / 3 : b - 6;
                        int drow = b < 6 ? b % 3 : 3;
                        float cx = ox + dotOffsetX + dcol * dotSpacePx;
                        float cy = oy + dotOffsetY + drow * dotSpacePx;
                        g.FillEllipse(brush, cx - dotRad, cy - dotRad, dotRad * 2, dotRad * 2);
                    }
                }
            }
            g.SmoothingMode = SmoothingMode.None;
        }

        // ── Caret ─────────────────────────────────────────────────────────────
        private void DrawCaret(Graphics g, Point[] layout)
        {
            var p = CursorScreenPos(layout);
            using (var pen = new Pen(Color.Black, 1.5f))
                g.DrawLine(pen, p.X, p.Y + 1, p.X, p.Y + LinePx - 2);
        }

        private PointF CursorScreenPos(Point[] layout)
        {
            int idx = Math.Min(_cursorPos, _text.Length);
            return new PointF(PAD + layout[idx].X * (float)CellPx, layout[idx].Y * (float)LinePx);
        }

        private int TextIndexAt(Point p)
        {
            var layout = BuildLayout(Width);
            float cellW = CellPx, lineH = LinePx;
            int best = _text.Length;
            float bestDist = float.MaxValue;

            for (int i = 0; i <= _text.Length; i++)
            {
                float cx = PAD + layout[i].X * cellW + cellW * 0.5f;
                float cy = layout[i].Y * lineH + lineH * 0.5f;
                float dx = p.X - cx, dy = p.Y - cy;
                float d2 = dx * dx + dy * dy;
                if (d2 < bestDist) { bestDist = d2; best = i; }
            }
            return best;
        }

        // ── Word / line selection helpers ─────────────────────────────────────
        private void SelectWordAt(int idx)
        {
            if (_text.Length == 0) return;
            if (idx >= _text.Length) idx = _text.Length - 1;

            // Braille space U+2800 ⠀ is the word separator
            int start = idx;
            while (start > 0 && _text[start - 1] != '⠀')
                start--;

            int end = idx;
            while (end < _text.Length && _text[end] != '⠀')
                end++;

            if (start < end)
            {
                _selectionAnchor = start;
                _cursorPos       = end;
                _caretVisible    = true;
                Invalidate();
            }
        }

        private void SelectLineAt(int idx)
        {
            if (_text.Length == 0) return;
            var layout = BuildLayout(Width);

            int safeIdx   = Math.Max(0, Math.Min(idx, _text.Length - 1));
            int targetRow = layout[safeIdx].Y;

            int start = _text.Length, end = -1;
            for (int i = 0; i < _text.Length; i++)
            {
                if (layout[i].Y == targetRow)
                {
                    if (i < start) start = i;
                    if (i > end)   end   = i;
                }
            }

            if (end >= 0)
            {
                _selectionAnchor = start;
                _cursorPos       = end + 1;
                _caretVisible    = true;
                Invalidate();
            }
        }

        private void SelectAll()
        {
            if (_text.Length == 0) return;
            _selectionAnchor = 0;
            _cursorPos       = _text.Length;
            _caretVisible    = true;
            Invalidate();
        }

        // ── Resize handle geometry ────────────────────────────────────────────
        private Rectangle GetHandleRect(ResizeHandle h)
        {
            int w = Width, ht = Height, s = HANDLE_SIZE, half = s / 2;
            switch (h)
            {
                case ResizeHandle.TopLeft:      return new Rectangle(0,          0,          s, s);
                case ResizeHandle.TopCenter:    return new Rectangle(w/2 - half, 0,          s, s);
                case ResizeHandle.TopRight:     return new Rectangle(w - s,      0,          s, s);
                case ResizeHandle.MiddleLeft:   return new Rectangle(0,          ht/2-half,  s, s);
                case ResizeHandle.MiddleRight:  return new Rectangle(w - s,      ht/2-half,  s, s);
                case ResizeHandle.BottomLeft:   return new Rectangle(0,          ht - s,     s, s);
                case ResizeHandle.BottomCenter: return new Rectangle(w/2 - half, ht - s,     s, s);
                case ResizeHandle.BottomRight:  return new Rectangle(w - s,      ht - s,     s, s);
                default: return Rectangle.Empty;
            }
        }

        private static readonly ResizeHandle[] _activeHandles =
        {
            ResizeHandle.TopLeft,    ResizeHandle.TopRight,
            ResizeHandle.MiddleLeft, ResizeHandle.MiddleRight,
            ResizeHandle.BottomLeft, ResizeHandle.BottomRight
        };

        private ResizeHandle HitTest(Point p)
        {
            foreach (var h in _activeHandles)
                if (GetHandleRect(h).Contains(p)) return h;
            return ResizeHandle.None;
        }

        private static Cursor CursorFor(ResizeHandle h)
        {
            switch (h)
            {
                case ResizeHandle.MiddleLeft:
                case ResizeHandle.MiddleRight:  return Cursors.SizeWE;
                case ResizeHandle.TopLeft:
                case ResizeHandle.BottomRight:  return Cursors.SizeNWSE;
                case ResizeHandle.TopRight:
                case ResizeHandle.BottomLeft:   return Cursors.SizeNESW;
                default:                        return Cursors.SizeAll;
            }
        }

        // ── Mouse ─────────────────────────────────────────────────────────────
        protected override void OnMouseDown(MouseEventArgs e)
        {
            // Capture screen coords BEFORE base.OnMouseDown: WinForms fires focus
            // internally which can trigger AutoScroll and physically move the HWND,
            // making a post-move PointToScreen give the wrong drag anchor.
            _mouseDownScreen = PointToScreen(e.Location);
            base.OnMouseDown(e);
            // WM_SETFOCUS fires before WM_LBUTTONDOWN, so OnGotFocus (and the canvas GotFocus
            // handler that sets IsSelected) has already run by the time we get here.
            // _justGotFocus tells us whether this is the very click that gained focus.
            bool wasInTextMode = _textEditMode;
            bool justGotFocus  = _justGotFocus;
            _justGotFocus      = false; // consume — next click on this already-focused box won't see it
            Focus();

            // e.Clicks from WinForms is unreliable beyond 2 — track rapid consecutive clicks ourselves.
            {
                int   nowTick = Environment.TickCount;
                Point scr     = _mouseDownScreen; // reuse pre-focus-scroll capture
                Size  dblSz   = SystemInformation.DoubleClickSize;
                bool  same    = Math.Abs(scr.X - _lastClickScreen.X) <= dblSz.Width / 2 &&
                                Math.Abs(scr.Y - _lastClickScreen.Y) <= dblSz.Height / 2;
                bool  inTime  = unchecked(nowTick - _lastClickTick) <= SystemInformation.DoubleClickTime;
                _ownClickCount   = (same && inTime) ? _ownClickCount + 1 : 1;
                _lastClickTick   = nowTick;
                _lastClickScreen = scr;
            }
            _startLocation   = Location; // captured after any focus-induced scroll
            _startSize       = Size;
            _activeHandle    = HitTest(e.Location);
            _resizing        = _activeHandle != ResizeHandle.None;
            _dragging        = !_resizing && !_textEditMode;
            Capture          = true;

            if (_dragging)
            {
                var canvas = Parent as CanvasPanel;
                if (canvas != null && IsSelected && canvas.SelectedControls.Count > 1)
                {
                    _groupStartLocations = new System.Collections.Generic.Dictionary<Control, Point>();
                    foreach (var c in canvas.SelectedControls)
                        _groupStartLocations[c] = c.Location;
                }
                else
                    _groupStartLocations = null;
            }

            if (_activeHandle == ResizeHandle.None)
            {
                int idx = TextIndexAt(e.Location);

                if (wasInTextMode)
                {
                    // Already editing: cursor / word / all by click count.
                    _pendingClickIdx = -1;
                    if ((ModifierKeys & Keys.Shift) != 0)
                    { _cursorPos = idx; _caretVisible = true; Invalidate(); }
                    else if (_ownClickCount >= 3) { SelectAll(); }
                    else if (_ownClickCount == 2) { SelectWordAt(idx); }
                    else                          { EnterTextMode(idx); }
                }
                else if (!justGotFocus)
                {
                    if (_ownClickCount >= 3)
                    {
                        // Triple-click before text mode was established: enter and select all.
                        EnterTextMode(idx);
                        SelectAll();
                    }
                    else
                    {
                        // Box was already focused/selected; this click places the cursor.
                        // Defer to mouse-up so the user can still drag the box.
                        _pendingClickIdx = idx;
                    }
                }
                // else justGotFocus: this click is what selected the box — no cursor yet.
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            // In text-edit mode, left-button drag extends selection
            if (_textEditMode && e.Button == MouseButtons.Left && !_resizing)
            {
                _cursorPos = TextIndexAt(e.Location);
                _caretVisible = true;
                Invalidate();
                return;
            }

            if (!_dragging && !_resizing)
            {
                Cursor = CursorFor(HitTest(e.Location));
                return;
            }

            var screen = PointToScreen(e.Location);
            int dx = screen.X - _mouseDownScreen.X;
            int dy = screen.Y - _mouseDownScreen.Y;

            if (_dragging)
            {
                var canvas = Parent as ScrollableControl;
                int minX   = canvas?.AutoScrollPosition.X ?? 0;
                int minY   = canvas?.AutoScrollPosition.Y ?? 0;

                if (_groupStartLocations != null)
                {
                    foreach (var kvp in _groupStartLocations)
                        kvp.Key.Location = new Point(
                            Math.Max(minX, kvp.Value.X + dx),
                            Math.Max(minY, kvp.Value.Y + dy));
                }
                else
                {
                    Location = new Point(
                        Math.Max(minX, _startLocation.X + dx),
                        Math.Max(minY, _startLocation.Y + dy));
                }
                return;
            }

            int nx = _startLocation.X, ny = _startLocation.Y;
            int nw = _startSize.Width,  nh = _startSize.Height;
            int mw = MinimumSize.Width,  mh = MinimumSize.Height;

            switch (_activeHandle)
            {
                case ResizeHandle.MiddleLeft:
                    nw = Math.Max(mw, _startSize.Width - dx);
                    nx = _startLocation.X + (_startSize.Width - nw);
                    nh = ComputeHeight(nw);
                    break;
                case ResizeHandle.MiddleRight:
                    nw = Math.Max(mw, _startSize.Width + dx);
                    nh = ComputeHeight(nw);
                    break;
                case ResizeHandle.TopLeft:
                    nw = Math.Max(mw, _startSize.Width - dx);
                    nx = _startLocation.X + (_startSize.Width - nw);
                    nh = ComputeHeight(nw);
                    ny = _startLocation.Y + (_startSize.Height - nh);
                    break;
                case ResizeHandle.TopRight:
                    nw = Math.Max(mw, _startSize.Width + dx);
                    nh = ComputeHeight(nw);
                    ny = _startLocation.Y + (_startSize.Height - nh);
                    break;
                case ResizeHandle.BottomLeft:
                    nw = Math.Max(mw, _startSize.Width - dx);
                    nx = _startLocation.X + (_startSize.Width - nw);
                    nh = ComputeHeight(nw);
                    break;
                case ResizeHandle.BottomRight:
                    nw = Math.Max(mw, _startSize.Width + dx);
                    nh = ComputeHeight(nw);
                    break;
            }

            SetBounds(nx, ny, nw, nh);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _dragging = _resizing = false;
            _activeHandle        = ResizeHandle.None;
            _groupStartLocations = null;
            Capture              = false;

            if (_pendingClickIdx >= 0)
            {
                var ds  = SystemInformation.DragSize;
                var scr = PointToScreen(e.Location);
                if (Math.Abs(scr.X - _mouseDownScreen.X) <= ds.Width &&
                    Math.Abs(scr.Y - _mouseDownScreen.Y) <= ds.Height)
                    EnterTextMode(_pendingClickIdx);
                _pendingClickIdx = -1;
            }
        }

        // ── Keyboard ──────────────────────────────────────────────────────────
        protected override bool IsInputKey(Keys keyData) => true;

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);
            if (e.KeyChar < ' ') return; // skip Backspace, Escape, Ctrl+A/C/V/X and all other control chars

            if (!_textEditMode) EnterTextMode();

            if (HasSelection) DeleteSelection();

            string braille = BrailleMapper.ToBraille(e.KeyChar);
            if (!string.IsNullOrEmpty(braille))
            {
                if (e.KeyChar >= '0' && e.KeyChar <= '9')
                    braille = "⠼" + braille;
                ApplyTextChange(
                    _text.Substring(0, _cursorPos) + braille + _text.Substring(_cursorPos),
                    _cursorPos + braille.Length);
            }
            e.Handled = true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.KeyCode)
            {
                case Keys.Delete:
                    if (!_textEditMode) { DeleteSelf(); }
                    else if (HasSelection) DeleteSelection();
                    else if (_cursorPos < _text.Length)
                        ApplyTextChange(
                            _text.Substring(0, _cursorPos) + _text.Substring(_cursorPos + 1),
                            _cursorPos);
                    e.Handled = true;
                    break;

                case Keys.Back:
                    if (_textEditMode)
                    {
                        if (HasSelection) DeleteSelection();
                        else if (_cursorPos > 0)
                        {
                            // Delete capital-indicator pair as one unit
                            int del = 1;
                            if (_cursorPos >= 2 && _text[_cursorPos - 1] != '⠠'
                                                && _text[_cursorPos - 2] == '⠠')
                                del = 2;
                            ApplyTextChange(
                                _text.Substring(0, _cursorPos - del) + _text.Substring(_cursorPos),
                                _cursorPos - del);
                        }
                    }
                    e.Handled = true;
                    break;

                case Keys.Escape:
                    if (_textEditMode) ExitTextMode();
                    else Parent?.Focus();
                    e.Handled = true;
                    break;

                case Keys.Left:
                    if (_textEditMode)
                    {
                        if (HasSelection && !e.Shift)
                        { _cursorPos = SelStart; _selectionAnchor = _cursorPos; _caretVisible = true; Invalidate(); }
                        else if (_cursorPos > 0)
                        { _cursorPos--; if (!e.Shift) _selectionAnchor = _cursorPos; _caretVisible = true; Invalidate(); }
                    }
                    e.Handled = true;
                    break;

                case Keys.Right:
                    if (_textEditMode)
                    {
                        if (HasSelection && !e.Shift)
                        { _cursorPos = SelEnd; _selectionAnchor = _cursorPos; _caretVisible = true; Invalidate(); }
                        else if (_cursorPos < _text.Length)
                        { _cursorPos++; if (!e.Shift) _selectionAnchor = _cursorPos; _caretVisible = true; Invalidate(); }
                    }
                    e.Handled = true;
                    break;

                case Keys.Home:
                    if (_textEditMode)
                    { _cursorPos = 0; if (!e.Shift) _selectionAnchor = _cursorPos; _caretVisible = true; Invalidate(); }
                    e.Handled = true;
                    break;

                case Keys.End:
                    if (_textEditMode)
                    { _cursorPos = _text.Length; if (!e.Shift) _selectionAnchor = _cursorPos; _caretVisible = true; Invalidate(); }
                    e.Handled = true;
                    break;

                case Keys.A:
                    if (e.Control && _textEditMode)
                    { _selectionAnchor = 0; _cursorPos = _text.Length; _caretVisible = true; Invalidate(); }
                    e.Handled = true;
                    break;

                case Keys.C:
                    if (e.Control && _textEditMode && HasSelection)
                        Clipboard.SetText(SelectedText);
                    e.Handled = true;
                    break;

                case Keys.X:
                    if (e.Control && _textEditMode && HasSelection)
                    { Clipboard.SetText(SelectedText); DeleteSelection(); }
                    e.Handled = true;
                    break;

                case Keys.V:
                    if (e.Control && _textEditMode)
                    {
                        string clip = Clipboard.GetText();
                        if (!string.IsNullOrEmpty(clip))
                        {
                            if (HasSelection) DeleteSelection();
                            var sb = new System.Text.StringBuilder();
                            foreach (char c in clip)
                            {
                                // Paste braille unicode directly; convert anything else
                                if (c >= '⠀' && c <= '⣿') { sb.Append(c); continue; }
                                string b = BrailleMapper.ToBraille(c);
                                if (b.Length > 0) sb.Append(b);
                            }
                            string ins = sb.ToString();
                            if (ins.Length > 0)
                                ApplyTextChange(
                                    _text.Substring(0, _cursorPos) + ins + _text.Substring(_cursorPos),
                                    _cursorPos + ins.Length);
                        }
                    }
                    e.Handled = true;
                    break;
            }
        }

        // ── Focus ─────────────────────────────────────────────────────────────
        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            _justGotFocus    = true;
            _textEditMode    = false;
            _selectionAnchor = _cursorPos;
            _caretVisible    = false;
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            ExitTextMode();
        }
    }

    public enum ResizeHandle
    {
        None,
        TopLeft, TopCenter, TopRight,
        MiddleLeft, MiddleRight,
        BottomLeft, BottomCenter, BottomRight
    }
}
