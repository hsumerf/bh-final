using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace BrailleUrdu
{
    public class PrintTextBox : UserControl
    {
        private const int HANDLE_SIZE = 10;
        private const int PAD         = 4;

        // ── Static defaults ───────────────────────────────────────────────────
        public static string    DefaultFontFamily = "Calibri";
        public static float     DefaultFontSizePt = 12f;
        public static FontStyle DefaultFontStyle  = FontStyle.Regular;
        public static Color     DefaultTextColor  = Color.Black;

        // ── Language / display ────────────────────────────────────────────────
        private string    _fontFamily;
        private float     _fontSizePt;
        private FontStyle _fontStyle;
        private bool      _isRtl;

        // ── Style ─────────────────────────────────────────────────────────────
        private Color           _textColor = Color.Black;
        private StringAlignment _hAlign    = StringAlignment.Near;
        private StringAlignment _vAlign    = StringAlignment.Near;

        // ── Border ────────────────────────────────────────────────────────────
        private Color _borderColor  = Color.Black;
        private int   _borderWidth  = 1;
        private bool  _borderTop, _borderBottom, _borderLeft, _borderRight;

        // ── Background ────────────────────────────────────────────────────────
        private Color _fillColor       = Color.White;
        private bool  _fillTransparent = true;

        // ── Public properties ─────────────────────────────────────────────────

        public string FontFamily
        {
            get => _fontFamily;
            set { _fontFamily = value ?? "Segoe UI"; FitSize(); Invalidate(); }
        }

        public float FontSizePt
        {
            get => _fontSizePt;
            set { _fontSizePt = Math.Max(1f, value); FitSize(); Invalidate(); }
        }

        public FontStyle TextFontStyle
        {
            get => _fontStyle;
            set { _fontStyle = value; FitSize(); Invalidate(); }
        }

        public bool IsRightToLeft
        {
            get => _isRtl;
            set { _isRtl = value; RightToLeft = value ? RightToLeft.Yes : RightToLeft.No; Invalidate(); }
        }

        public Color TextColor
        {
            get => _textColor;
            set { _textColor = value; Invalidate(); }
        }

        public StringAlignment HTextAlign
        {
            get => _hAlign;
            set { _hAlign = value; Invalidate(); }
        }

        public StringAlignment VTextAlign
        {
            get => _vAlign;
            set { _vAlign = value; Invalidate(); }
        }

        public Color BorderColor
        {
            get => _borderColor;
            set { _borderColor = value; Invalidate(); }
        }

        public int BorderWidth
        {
            get => _borderWidth;
            set { _borderWidth = Math.Max(0, value); Invalidate(); }
        }

        public bool BorderTop    { get => _borderTop;    set { _borderTop    = value; Invalidate(); } }
        public bool BorderBottom { get => _borderBottom; set { _borderBottom = value; Invalidate(); } }
        public bool BorderLeft   { get => _borderLeft;   set { _borderLeft   = value; Invalidate(); } }
        public bool BorderRight  { get => _borderRight;  set { _borderRight  = value; Invalidate(); } }

        public Color FillColor
        {
            get => _fillColor;
            set { _fillColor = value; Invalidate(); }
        }

        public bool FillTransparent
        {
            get => _fillTransparent;
            set { _fillTransparent = value; Invalidate(); }
        }

        // ── Content & cursor ─────────────────────────────────────────────────
        private string _text            = "";
        private int    _cursorPos       = 0;
        private int    _selectionAnchor = 0;
        private string _inputPending    = "";

        public string DisplayText
        {
            get => _text;
            set { _text = value ?? ""; _cursorPos = _text.Length; _selectionAnchor = _cursorPos; FitSize(); Invalidate(); }
        }

        public bool IsSelected     { get; set; }
        public bool IsTextEditing => _textEditMode;

        private int    SelStart     => Math.Min(_selectionAnchor, _cursorPos);
        private int    SelEnd       => Math.Max(_selectionAnchor, _cursorPos);
        private bool   HasSelection => _selectionAnchor != _cursorPos;
        private string SelectedText => _text.Substring(SelStart, SelEnd - SelStart);

        // ── Mode ──────────────────────────────────────────────────────────────
        private bool _textEditMode = false;

        private readonly Timer _caretTimer   = new Timer { Interval = 530 };
        private bool           _caretVisible = false;

        // ── Drag / resize ─────────────────────────────────────────────────────
        private bool         _dragging;
        private bool         _resizing;
        private Point        _mouseDownScreen;
        private Point        _startLocation;
        private Size         _startSize;
        private ResizeHandle _activeHandle = ResizeHandle.None;
        private System.Collections.Generic.Dictionary<Control, Point> _groupStartLocations;

        // ── Construction ──────────────────────────────────────────────────────
        public PrintTextBox()
        {
            _fontFamily = DefaultFontFamily;
            _fontSizePt = DefaultFontSizePt;
            _fontStyle  = DefaultFontStyle;
            _textColor  = DefaultTextColor;
            _isRtl      = LanguageInfo.RtlFor(Document.Language);

            SetStyle(
                ControlStyles.Selectable                   |
                ControlStyles.UserPaint                    |
                ControlStyles.AllPaintingInWmPaint         |
                ControlStyles.OptimizedDoubleBuffer        |
                ControlStyles.SupportsTransparentBackColor, true);

            ResizeRedraw = true;
            BackColor    = Color.Transparent;
            TabStop      = true;
            MinimumSize  = new Size(60, 22);
            RightToLeft  = _isRtl ? RightToLeft.Yes : RightToLeft.No;
            ImeMode      = ImeMode.On;

            Width = MinimumSize.Width;
            FitSize();

            _caretTimer.Tick += (s, e) => { _caretVisible = !_caretVisible; Invalidate(); };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _caretTimer.Dispose();
            base.Dispose(disposing);
        }

        // ── Auto-size (grows right to margin, then wraps down) ────────────────
        private void FitSize()
        {
            if (!IsHandleCreated)
            {
                try
                {
                    using (var font = MakeFont())
                    {
                        int lines = Math.Max(1, _text.Split('\n').Length);
                        int newH  = Math.Max(MinimumSize.Height,
                            (int)Math.Ceiling(font.GetHeight(96f) * lines) + PAD * 2 + 2);
                        if (Height != newH) Height = newH;
                    }
                }
                catch { }
                return;
            }
            try
            {
                using (var font = MakeFont())
                using (var g    = CreateGraphics())
                {
                    int maxWidth;
                    var canvas = Parent as CanvasPanel;
                    if (canvas != null)
                        maxWidth = Math.Max(MinimumSize.Width,
                            (int)canvas.MarginBoundsPx.Right - Left);
                    else
                        maxWidth = int.MaxValue;

                    // Width of the widest explicit line (no wrapping)
                    float maxLineW = 0f;
                    if (_text.Length > 0)
                    {
                        var noWrap = new StringFormat
                            { FormatFlags = StringFormatFlags.NoWrap };
                        foreach (var ln in _text.Split('\n'))
                        {
                            if (ln.Length == 0) continue;
                            float w = g.MeasureString(ln, font, PointF.Empty, noWrap).Width;
                            if (w > maxLineW) maxLineW = w;
                        }
                    }
                    int contentW = (int)Math.Ceiling(maxLineW) + PAD * 2 + 4;
                    int newWidth = maxWidth == int.MaxValue
                        ? Math.Max(MinimumSize.Width, contentW)
                        : Math.Max(MinimumSize.Width, Math.Min(contentW, maxWidth));

                    // Height with soft wrapping at newWidth
                    float measuredH;
                    if (_text.Length > 0)
                    {
                        var wrapFmt = new StringFormat(StringFormat.GenericTypographic);
                        measuredH = g.MeasureString(_text, font,
                            newWidth - PAD * 2, wrapFmt).Height;
                    }
                    else
                    {
                        measuredH = font.GetHeight(g);
                    }
                    int newHeight = Math.Max(MinimumSize.Height,
                        (int)Math.Ceiling(measuredH) + PAD * 2 + 2);

                    if (Width != newWidth || Height != newHeight)
                        SetBounds(Left, Top, newWidth, newHeight);
                    else
                        Invalidate();
                }
            }
            catch { }
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            if (Parent != null) FitSize();
        }

        // ── Mode helpers ──────────────────────────────────────────────────────
        private void EnterTextMode(int index = -1)
        {
            _textEditMode    = true;
            _cursorPos       = index < 0 ? _text.Length : Math.Min(index, _text.Length);
            _selectionAnchor = _cursorPos;
            _caretVisible    = true;
            _caretTimer.Start();
            Invalidate();
        }

        private void ExitTextMode()
        {
            FlushInputPending();
            _textEditMode    = false;
            _selectionAnchor = _cursorPos;
            _caretTimer.Stop();
            _caretVisible    = false;
            Invalidate();
        }

        private void FlushInputPending()
        {
            if (string.IsNullOrEmpty(_inputPending)) return;
            string flushed = Document.PrintMap?.Flush(ref _inputPending) ?? _inputPending;
            _inputPending = "";
            if (flushed.Length == 0) return;
            _text = _text.Substring(0, _cursorPos) + flushed + _text.Substring(_cursorPos);
            _cursorPos      += flushed.Length;
            _selectionAnchor = _cursorPos;
            _caretVisible    = true;
            FitSize();
            Invalidate();
        }

        private void DeleteSelection()
        {
            int s = SelStart, end = SelEnd;
            _text            = _text.Substring(0, s) + _text.Substring(end);
            _cursorPos       = s;
            _selectionAnchor = s;
            _caretVisible    = true;
            FitSize();
            Invalidate();
        }

        private void DeleteSelf()
        {
            BeginInvoke((Action)(() =>
            {
                Parent?.Controls.Remove(this);
                Dispose();
            }));
        }

        // ── Font / format helpers ─────────────────────────────────────────────
        private Font MakeFont() => new Font(_fontFamily, _fontSizePt, _fontStyle, GraphicsUnit.Point);

        private StringFormat MakeFmt()
        {
            var fmt = new StringFormat(StringFormat.GenericTypographic);
            fmt.FormatFlags  |= StringFormatFlags.MeasureTrailingSpaces;
            fmt.Alignment     = _hAlign;
            fmt.LineAlignment = _vAlign;
            if (_isRtl) fmt.FormatFlags |= StringFormatFlags.DirectionRightToLeft;
            return fmt;
        }

        // ── Paint ─────────────────────────────────────────────────────────────
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            if (!_fillTransparent)
                using (var b = new SolidBrush(_fillColor))
                    g.FillRectangle(b, 0, 0, Width, Height);

            using (var font = MakeFont())
            {
                DrawTextSelection(g, font);
                DrawText(g, font);

                if (Focused)
                {
                    if (_textEditMode && _caretVisible) DrawCaret(g, font);

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

            DrawBorders(g);
        }

        private void DrawText(Graphics g, Font font)
        {
            if (_text.Length == 0) return;
            using (var fmt   = MakeFmt())
            using (var brush = new SolidBrush(_textColor))
                g.DrawString(_text, font, brush,
                    new RectangleF(PAD, PAD, Width - PAD * 2, Height - PAD * 2), fmt);
        }

        // ── Selection highlight ───────────────────────────────────────────────
        private void DrawTextSelection(Graphics g, Font font)
        {
            if (!HasSelection || !_textEditMode) return;
            int selStart = SelStart, selEnd = SelEnd;
            float lh = font.GetHeight(g);
            string[] lines = _text.Split('\n');

            using (var selBrush = new SolidBrush(Color.FromArgb(100, 51, 153, 255)))
            using (var fmt      = MakeFmt())
            {
                int idx = 0;
                for (int li = 0; li < lines.Length; li++)
                {
                    string ln        = lines[li];
                    int    lineStart = idx;
                    int    lineEnd   = idx + ln.Length;
                    float  y         = PAD + li * lh;

                    int  cs              = Math.Max(selStart, lineStart) - lineStart;
                    int  ce              = Math.Min(selEnd,   lineEnd)   - lineStart;
                    bool newlineSel      = selEnd > lineEnd && li < lines.Length - 1;

                    if (cs < ce || newlineSel)
                    {
                        float x1 = cs > 0
                            ? g.MeasureString(ln.Substring(0, cs), font, PointF.Empty, fmt).Width
                            : 0f;
                        float x2 = ce > 0
                            ? g.MeasureString(ln.Substring(0, ce), font, PointF.Empty, fmt).Width
                            : 0f;
                        if (newlineSel) x2 = Math.Max(x2, x1 + 6f);

                        float rx = _isRtl ? Width - PAD - x2 : PAD + x1;
                        float rw = Math.Max(2f, x2 - x1);
                        g.FillRectangle(selBrush, rx, y, rw, lh);
                    }

                    idx += ln.Length + 1;
                }
            }
        }

        private void DrawBorders(Graphics g)
        {
            if (_borderWidth <= 0) return;
            using (var pen = new Pen(_borderColor, _borderWidth))
            {
                float half = _borderWidth / 2f;
                if (_borderTop)    g.DrawLine(pen, 0,          half,          Width,      half);
                if (_borderBottom) g.DrawLine(pen, 0,          Height - half, Width,      Height - half);
                if (_borderLeft)   g.DrawLine(pen, half,       0,             half,       Height);
                if (_borderRight)  g.DrawLine(pen, Width-half, 0,             Width-half, Height);
            }
        }

        // ── Caret ─────────────────────────────────────────────────────────────
        private void DrawCaret(Graphics g, Font font)
        {
            var   pos = CursorScreenPos(g, font);
            float lh  = font.GetHeight(g);
            using (var pen = new Pen(Color.Black, 1.5f))
                g.DrawLine(pen, pos.X, pos.Y + 1f, pos.X, pos.Y + lh - 1f);
        }

        private PointF CursorScreenPos(Graphics g, Font font)
        {
            GetLineCol(_cursorPos, out int line, out int col);
            string[] lines  = _text.Split('\n');
            string   ln     = line < lines.Length ? lines[line] : "";
            string   before = col <= ln.Length ? ln.Substring(0, col) : ln;

            float lh          = font.GetHeight(g);
            float beforeWidth = before.Length > 0
                ? g.MeasureString(before, font, PointF.Empty, MakeFmt()).Width
                : 0f;
            float x = _isRtl ? (Width - PAD) - beforeWidth : PAD + beforeWidth;
            return new PointF(x, PAD + line * lh);
        }

        private int TextIndexAt(Point p)
        {
            using (var font = MakeFont())
            using (var g    = CreateGraphics())
            using (var fmt  = MakeFmt())
            {
                float    lh    = font.GetHeight(g);
                string[] lines = _text.Split('\n');
                int      li    = Math.Max(0, Math.Min((int)((p.Y - PAD) / lh), lines.Length - 1));

                int baseIdx = 0;
                for (int i = 0; i < li; i++) baseIdx += lines[i].Length + 1;

                string ln      = lines[li];
                float  best    = float.MaxValue;
                int    bestCol = ln.Length;

                for (int col = 0; col <= ln.Length; col++)
                {
                    float w  = col > 0
                        ? g.MeasureString(ln.Substring(0, col), font, PointF.Empty, fmt).Width
                        : 0f;
                    float cx = _isRtl ? (Width - PAD) - w : PAD + w;
                    float d  = Math.Abs(p.X - cx);
                    if (d < best) { best = d; bestCol = col; }
                }

                return Math.Min(baseIdx + bestCol, _text.Length);
            }
        }

        // ── Multi-line helpers ────────────────────────────────────────────────
        private void GetLineCol(int idx, out int line, out int col)
        {
            line = 0; col = 0;
            for (int i = 0; i < idx && i < _text.Length; i++)
            {
                if (_text[i] == '\n') { line++; col = 0; }
                else col++;
            }
        }

        private int LineColToIndex(int line, int col)
        {
            int idx = 0, cur = 0;
            while (cur < line && idx < _text.Length)
            {
                if (_text[idx] == '\n') cur++;
                idx++;
            }
            return Math.Min(idx + col, _text.Length);
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

        private static readonly ResizeHandle[] _resizeHandles =
        {
            ResizeHandle.TopLeft,    ResizeHandle.TopCenter,    ResizeHandle.TopRight,
            ResizeHandle.MiddleLeft,                            ResizeHandle.MiddleRight,
            ResizeHandle.BottomLeft, ResizeHandle.BottomCenter, ResizeHandle.BottomRight
        };

        private ResizeHandle HitTest(Point p)
        {
            foreach (var h in _resizeHandles)
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

            if (_activeHandle == ResizeHandle.None && (e.Clicks >= 2 || _textEditMode))
            {
                int idx = TextIndexAt(e.Location);
                if (_textEditMode && (ModifierKeys & Keys.Shift) != 0)
                { _cursorPos = idx; _caretVisible = true; Invalidate(); }
                else
                    EnterTextMode(idx);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            // Text-mode drag extends selection
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
                case ResizeHandle.BottomCenter: break; // height is auto-managed
                case ResizeHandle.MiddleLeft:
                    nw = Math.Max(mw, _startSize.Width - dx);
                    nx = _startLocation.X + (_startSize.Width - nw);
                    break;
                case ResizeHandle.MiddleRight:
                    nw = Math.Max(mw, _startSize.Width + dx);
                    break;
                case ResizeHandle.TopLeft:
                case ResizeHandle.BottomLeft:
                    nw = Math.Max(mw, _startSize.Width - dx);
                    nx = _startLocation.X + (_startSize.Width - nw);
                    break;
                case ResizeHandle.TopRight:
                case ResizeHandle.BottomRight:
                    nw = Math.Max(mw, _startSize.Width + dx);
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
            char c = e.KeyChar;
            if (c < ' ') return; // skip Backspace, Escape, Enter, Ctrl+A/C/V/X and all other control chars

            if (!_textEditMode) EnterTextMode();

            if (HasSelection) DeleteSelection();

            string output = (!char.IsDigit(c) && Document.PrintMap != null)
                ? Document.PrintMap.Convert(ref _inputPending, c)
                : c.ToString();

            if (output == null) { e.Handled = true; return; }

            _text            = _text.Substring(0, _cursorPos) + output + _text.Substring(_cursorPos);
            _cursorPos      += output.Length;
            _selectionAnchor = _cursorPos;
            _caretVisible    = true;
            FitSize();
            Invalidate();
            e.Handled = true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.KeyCode)
            {
                case Keys.Delete:
                    if (!_textEditMode) DeleteSelf();
                    else if (HasSelection) DeleteSelection();
                    else if (_cursorPos < _text.Length)
                    {
                        _text = _text.Substring(0, _cursorPos) + _text.Substring(_cursorPos + 1);
                        _caretVisible = true; FitSize(); Invalidate();
                    }
                    e.Handled = true;
                    break;

                case Keys.Back:
                    if (_textEditMode)
                    {
                        if (HasSelection) DeleteSelection();
                        else if (_cursorPos > 0)
                        {
                            _text = _text.Substring(0, _cursorPos - 1) + _text.Substring(_cursorPos);
                            _cursorPos--; _selectionAnchor = _cursorPos;
                            _caretVisible = true; FitSize(); Invalidate();
                        }
                    }
                    e.Handled = true;
                    break;

                case Keys.Enter:
                    FlushInputPending();
                    if (!_textEditMode) EnterTextMode();
                    if (HasSelection) DeleteSelection();
                    _text = _text.Substring(0, _cursorPos) + '\n' + _text.Substring(_cursorPos);
                    _cursorPos++; _selectionAnchor = _cursorPos;
                    _caretVisible = true; FitSize(); Invalidate();
                    e.Handled = true;
                    break;

                case Keys.Escape:
                    if (_textEditMode) ExitTextMode();
                    else Parent?.Focus();
                    e.Handled = true;
                    break;

                case Keys.Left:
                    FlushInputPending();
                    if (_textEditMode && _cursorPos > 0)
                    { _cursorPos--; if (!e.Shift) _selectionAnchor = _cursorPos; _caretVisible = true; Invalidate(); }
                    e.Handled = true;
                    break;

                case Keys.Right:
                    FlushInputPending();
                    if (_textEditMode && _cursorPos < _text.Length)
                    { _cursorPos++; if (!e.Shift) _selectionAnchor = _cursorPos; _caretVisible = true; Invalidate(); }
                    e.Handled = true;
                    break;

                case Keys.Home:
                    if (_textEditMode)
                    {
                        GetLineCol(_cursorPos, out int hl, out _);
                        _cursorPos = LineColToIndex(hl, 0);
                        if (!e.Shift) _selectionAnchor = _cursorPos;
                        _caretVisible = true; Invalidate();
                    }
                    e.Handled = true;
                    break;

                case Keys.End:
                    if (_textEditMode)
                    {
                        GetLineCol(_cursorPos, out int el, out _);
                        string[] lns = _text.Split('\n');
                        _cursorPos = LineColToIndex(el, lns[Math.Min(el, lns.Length - 1)].Length);
                        if (!e.Shift) _selectionAnchor = _cursorPos;
                        _caretVisible = true; Invalidate();
                    }
                    e.Handled = true;
                    break;

                case Keys.Up:
                    if (_textEditMode)
                    {
                        GetLineCol(_cursorPos, out int ul, out int uc);
                        if (ul > 0)
                        {
                            string[] lns = _text.Split('\n');
                            _cursorPos = LineColToIndex(ul - 1, Math.Min(uc, lns[ul - 1].Length));
                            if (!e.Shift) _selectionAnchor = _cursorPos;
                            _caretVisible = true; Invalidate();
                        }
                    }
                    e.Handled = true;
                    break;

                case Keys.Down:
                    if (_textEditMode)
                    {
                        GetLineCol(_cursorPos, out int dl, out int dc);
                        string[] lns = _text.Split('\n');
                        if (dl < lns.Length - 1)
                        {
                            _cursorPos = LineColToIndex(dl + 1, Math.Min(dc, lns[dl + 1].Length));
                            if (!e.Shift) _selectionAnchor = _cursorPos;
                            _caretVisible = true; Invalidate();
                        }
                    }
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
                            _text            = _text.Substring(0, _cursorPos) + clip + _text.Substring(_cursorPos);
                            _cursorPos      += clip.Length;
                            _selectionAnchor = _cursorPos;
                            _caretVisible    = true; FitSize(); Invalidate();
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
}
