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
        private const int PAD         = 8;

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
        private bool _textEditMode = false;

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

        // ── Construction ──────────────────────────────────────────────────────
        public BrailleTextBox()
        {
            SetStyle(
                ControlStyles.Selectable                   |
                ControlStyles.UserPaint                    |
                ControlStyles.AllPaintingInWmPaint         |
                ControlStyles.OptimizedDoubleBuffer        |
                ControlStyles.SupportsTransparentBackColor, true);

            ResizeRedraw = true;
            BackColor    = Color.Transparent;
            TabStop      = true;
            MinimumSize  = new Size(CellPx + PAD * 2, LinePx + PAD);
            Size         = new Size(CellPx + PAD * 2, LinePx + PAD);

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

            int col = 0, rows = 1;
            foreach (char c in _text)
            {
                float ox = PAD + col * (float)cellW;
                if (ox + cellW > newWidth - PAD) { rows++; col = 0; }
                col++;
            }
            int newHeight = PAD + rows * lineH;

            if (Width != newWidth || Height != newHeight)
                SetBounds(Left, Top, newWidth, newHeight);
            else
                Invalidate();
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

        // ── Paint ─────────────────────────────────────────────────────────────
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            DrawSearchHighlight(g);
            DrawBrailleSelection(g);
            DrawBrailleDots(g);

            if (Focused)
            {
                if (_textEditMode && _caretVisible) DrawCaret(g);

                using (var pen = new Pen(Color.DodgerBlue, 2f))
                    g.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);

                foreach (var h in new[] {
                    ResizeHandle.TopLeft,    ResizeHandle.TopCenter,    ResizeHandle.TopRight,
                    ResizeHandle.MiddleLeft,                            ResizeHandle.MiddleRight,
                    ResizeHandle.BottomLeft, ResizeHandle.BottomCenter, ResizeHandle.BottomRight })
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
        private void DrawSearchHighlight(Graphics g)
        {
            if (string.IsNullOrEmpty(_searchHighlight)) return;
            int    searchLen = _searchHighlight.Length;
            float  cellW     = CellPx;
            float  lineH     = LinePx;

            using (var brush = new SolidBrush(Color.FromArgb(180, 255, 210, 0)))
            {
                int pos = 0;
                while (pos <= _text.Length - searchLen)
                {
                    int match = _text.IndexOf(_searchHighlight, pos, StringComparison.Ordinal);
                    if (match < 0) break;

                    int col = 0, row = 0;
                    for (int i = 0; i < _text.Length; i++)
                    {
                        float ox = PAD + col * cellW;
                        if (ox + cellW > Width - PAD) { row++; col = 0; ox = PAD; }
                        if (i >= match && i < match + searchLen)
                            g.FillRectangle(brush, ox, PAD + row * lineH, cellW, lineH);
                        col++;
                    }
                    pos = match + searchLen;
                }
            }
        }

        // ── Selection highlight ───────────────────────────────────────────────
        private void DrawBrailleSelection(Graphics g)
        {
            if (!HasSelection || !_textEditMode) return;
            int start = SelStart, end = SelEnd;
            float cellW = CellPx, lineH = LinePx;
            using (var brush = new SolidBrush(Color.FromArgb(80, 51, 153, 255)))
            {
                int col = 0, row = 0;
                for (int i = 0; i < _text.Length; i++)
                {
                    float ox = PAD + col * cellW;
                    if (ox + cellW > Width - PAD) { row++; col = 0; ox = PAD; }
                    if (i >= start && i < end)
                        g.FillRectangle(brush, ox, PAD + row * lineH, cellW, lineH);
                    col++;
                }
            }
        }

        // ── Dot rendering ─────────────────────────────────────────────────────
        private void DrawBrailleDots(Graphics g)
        {
            float dotSpacePx = DocumentPage.DOT_SPACING_MM * PxPerMm;
            float dotRad     = Math.Max(2f, dotSpacePx * 0.38f);
            float cellW      = CellPx;
            float lineH      = LinePx;
            int col = 0, row = 0;

            using (var brush = new SolidBrush(Color.Black))
            {
                foreach (char c in _text)
                {
                    if ((int)c < 0x2800 || (int)c > 0x28FF) { col++; continue; }

                    float ox = PAD + col * cellW;
                    if (ox + cellW > Width - PAD) { row++; col = 0; ox = PAD; }
                    float oy = PAD + row * lineH;

                    int bits = (int)c - 0x2800;
                    for (int b = 0; b < 8; b++)
                    {
                        if ((bits & (1 << b)) == 0) continue;
                        int dcol = b < 6 ? b / 3 : b - 6;
                        int drow = b < 6 ? b % 3 : 3;
                        float cx = ox + dcol * dotSpacePx;
                        float cy = oy + drow * dotSpacePx;
                        g.FillEllipse(brush, cx - dotRad, cy - dotRad, dotRad * 2, dotRad * 2);
                    }
                    col++;
                }
            }
        }

        // ── Caret ─────────────────────────────────────────────────────────────
        private void DrawCaret(Graphics g)
        {
            var p = CursorScreenPos();
            using (var pen = new Pen(Color.Black, 1.5f))
                g.DrawLine(pen, p.X, p.Y + 1, p.X, p.Y + LinePx - 2);
        }

        private PointF CursorScreenPos()
        {
            float cellW = CellPx;
            int col = 0, row = 0;

            for (int i = 0; i <= _text.Length; i++)
            {
                float ox = PAD + col * cellW;
                if (ox + cellW > Width - PAD) { row++; col = 0; }

                if (i == _cursorPos)
                    return new PointF(PAD + col * cellW, PAD + row * LinePx);

                if (i < _text.Length) col++;
            }
            return new PointF(PAD, PAD);
        }

        private int TextIndexAt(Point p)
        {
            float cellW = CellPx, lineH = LinePx;
            int col = 0, row = 0;
            int best = _text.Length;
            float bestDist = float.MaxValue;

            for (int i = 0; i <= _text.Length; i++)
            {
                float ox = PAD + col * cellW;
                if (ox + cellW > Width - PAD) { row++; col = 0; }

                float cx = PAD + col * cellW;
                float cy = PAD + row * lineH + lineH * 0.5f;
                float dx = p.X - cx, dy = p.Y - cy;
                float d2 = dx * dx + dy * dy;
                if (d2 < bestDist) { bestDist = d2; best = i; }

                if (i < _text.Length) col++;
            }
            return best;
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
            ResizeHandle.TopLeft,    ResizeHandle.TopCenter,    ResizeHandle.TopRight,
            ResizeHandle.MiddleLeft,                            ResizeHandle.MiddleRight,
            ResizeHandle.BottomLeft, ResizeHandle.BottomCenter, ResizeHandle.BottomRight
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
                case ResizeHandle.TopCenter:
                case ResizeHandle.BottomCenter: return Cursors.SizeNS;
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
            base.OnMouseDown(e);
            Focus();
            _mouseDownScreen = PointToScreen(e.Location);
            _startLocation   = Location;
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
                if (e.Clicks >= 2 || _textEditMode)
                {
                    int idx = TextIndexAt(e.Location);
                    if (_textEditMode && (ModifierKeys & Keys.Shift) != 0)
                    { _cursorPos = idx; _caretVisible = true; Invalidate(); }
                    else
                        EnterTextMode(idx);
                }
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
                if (_groupStartLocations != null)
                {
                    foreach (var kvp in _groupStartLocations)
                        kvp.Key.Location = new Point(
                            Math.Max(0, kvp.Value.X + dx),
                            Math.Max(0, kvp.Value.Y + dy));
                }
                else
                {
                    Location = new Point(
                        Math.Max(0, _startLocation.X + dx),
                        Math.Max(0, _startLocation.Y + dy));
                }
                return;
            }

            int nx = _startLocation.X, ny = _startLocation.Y;
            int nw = _startSize.Width,  nh = _startSize.Height;
            int mw = MinimumSize.Width,  mh = MinimumSize.Height;

            switch (_activeHandle)
            {
                case ResizeHandle.TopCenter:
                    nh = Math.Max(mh, _startSize.Height - dy);
                    ny = _startLocation.Y + (_startSize.Height - nh);
                    break;
                case ResizeHandle.BottomCenter:
                    nh = Math.Max(mh, _startSize.Height + dy);
                    break;
                case ResizeHandle.MiddleLeft:
                    nw = Math.Max(mw, _startSize.Width - dx);
                    nx = _startLocation.X + (_startSize.Width - nw);
                    break;
                case ResizeHandle.MiddleRight:
                    nw = Math.Max(mw, _startSize.Width + dx);
                    break;
                case ResizeHandle.TopLeft:
                    nw = Math.Max(mw, _startSize.Width - dx);
                    nx = _startLocation.X + (_startSize.Width - nw);
                    nh = Math.Max(mh, _startSize.Height - dy);
                    ny = _startLocation.Y + (_startSize.Height - nh);
                    break;
                case ResizeHandle.TopRight:
                    nw = Math.Max(mw, _startSize.Width + dx);
                    nh = Math.Max(mh, _startSize.Height - dy);
                    ny = _startLocation.Y + (_startSize.Height - nh);
                    break;
                case ResizeHandle.BottomLeft:
                    nw = Math.Max(mw, _startSize.Width - dx);
                    nx = _startLocation.X + (_startSize.Width - nw);
                    nh = Math.Max(mh, _startSize.Height + dy);
                    break;
                case ResizeHandle.BottomRight:
                    nw = Math.Max(mw, _startSize.Width + dx);
                    nh = Math.Max(mh, _startSize.Height + dy);
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
                ApplyTextChange(
                    _text.Substring(0, _cursorPos) + braille + _text.Substring(_cursorPos),
                    _cursorPos + braille.Length);
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
                    if (_textEditMode && _cursorPos > 0)
                    { _cursorPos--; if (!e.Shift) _selectionAnchor = _cursorPos; _caretVisible = true; Invalidate(); }
                    e.Handled = true;
                    break;

                case Keys.Right:
                    if (_textEditMode && _cursorPos < _text.Length)
                    { _cursorPos++; if (!e.Shift) _selectionAnchor = _cursorPos; _caretVisible = true; Invalidate(); }
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
