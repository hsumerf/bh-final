using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace BrailleUrdu
{
    public class PrintTextBox : UserControl
    {
        private const int HANDLE_SIZE = 8;
        private const int PAD         = 4;

        // ── Language / display properties ─────────────────────────────────────
        private string _fontFamily = LanguageInfo.FontFor(Document.Language);
        private float  _fontSizePt = 12f;
        private bool   _isRtl      = LanguageInfo.RtlFor(Document.Language);

        public string FontFamily
        {
            get => _fontFamily;
            set { _fontFamily = value ?? "Segoe UI"; Invalidate(); }
        }

        public float FontSizePt
        {
            get => _fontSizePt;
            set { _fontSizePt = Math.Max(1f, value); Invalidate(); }
        }

        public bool IsRightToLeft
        {
            get => _isRtl;
            set { _isRtl = value; RightToLeft = value ? RightToLeft.Yes : RightToLeft.No; Invalidate(); }
        }

        // ── Content & cursor ─────────────────────────────────────────────────
        private string _text         = "";
        private int    _cursorPos    = 0;
        private string _inputPending = ""; // multi-char input buffer (e.g. "\" waiting for "z")

        public string DisplayText
        {
            get => _text;
            set { _text = value ?? ""; _cursorPos = _text.Length; Invalidate(); }
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

        // ── Construction ──────────────────────────────────────────────────────
        public PrintTextBox()
        {
            SetStyle(
                ControlStyles.Selectable                   |
                ControlStyles.UserPaint                    |
                ControlStyles.AllPaintingInWmPaint         |
                ControlStyles.OptimizedDoubleBuffer        |
                ControlStyles.SupportsTransparentBackColor, true);

            ResizeRedraw  = true;
            BackColor     = Color.Transparent;
            TabStop       = true;
            Size          = new Size(200, 36);
            MinimumSize   = new Size(60, 22);
            RightToLeft   = _isRtl ? RightToLeft.Yes : RightToLeft.No;
            ImeMode       = ImeMode.On;

            _caretTimer.Tick += (s, e) => { _caretVisible = !_caretVisible; Invalidate(); };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _caretTimer.Dispose();
            base.Dispose(disposing);
        }

        // ── Mode helpers ──────────────────────────────────────────────────────
        private void EnterTextMode(int index = -1)
        {
            _textEditMode = true;
            _cursorPos    = index < 0 ? _text.Length : Math.Min(index, _text.Length);
            _caretVisible = true;
            _caretTimer.Start();
            Invalidate();
        }

        private void ExitTextMode()
        {
            FlushInputPending();
            _textEditMode = false;
            _caretTimer.Stop();
            _caretVisible = false;
            Invalidate();
        }

        private void FlushInputPending()
        {
            if (string.IsNullOrEmpty(_inputPending)) return;
            string flushed = Document.PrintMap?.Flush(ref _inputPending) ?? _inputPending;
            _inputPending = "";
            if (flushed.Length == 0) return;
            _text = _text.Substring(0, _cursorPos) + flushed + _text.Substring(_cursorPos);
            _cursorPos += flushed.Length;
            _caretVisible = true;
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
        private Font MakeFont() => new Font(_fontFamily, _fontSizePt, GraphicsUnit.Point);

        private StringFormat MakeFmt()
        {
            var fmt = new StringFormat(StringFormat.GenericTypographic);
            fmt.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
            if (_isRtl)
                fmt.FormatFlags |= StringFormatFlags.DirectionRightToLeft;
            return fmt;
        }

        // ── Paint ─────────────────────────────────────────────────────────────
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            using (var font = MakeFont())
            {
                DrawText(g, font);

                if (Focused)
                {
                    if (_textEditMode && _caretVisible) DrawCaret(g, font);

                    using (var pen = new Pen(Color.DodgerBlue, 1.5f))
                        g.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);

                    foreach (var h in new[] {
                        ResizeHandle.TopCenter,
                        ResizeHandle.MiddleLeft, ResizeHandle.MiddleRight,
                        ResizeHandle.BottomCenter })
                    {
                        var r = GetHandleRect(h);
                        g.FillRectangle(Brushes.White, r);
                        g.DrawRectangle(Pens.DodgerBlue, r);
                    }
                }
                else if (_text.Length == 0)
                {
                    // Dashed border so an empty unfocused box stays locatable
                    using (var pen = new Pen(Color.FromArgb(160, 160, 160), 1f) { DashStyle = DashStyle.Dash })
                        g.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
                }
            }
        }

        private void DrawText(Graphics g, Font font)
        {
            if (_text.Length == 0) return;
            using (var fmt   = MakeFmt())
            using (var brush = new SolidBrush(Color.Black))
                g.DrawString(_text, font, brush,
                    new RectangleF(PAD, PAD, Width - PAD * 2, Height - PAD * 2), fmt);
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

            float lh           = font.GetHeight(g);
            float beforeWidth  = before.Length > 0
                ? g.MeasureString(before, font, PointF.Empty, MakeFmt()).Width
                : 0f;
            float x = _isRtl
                ? (Width - PAD) - beforeWidth
                : PAD + beforeWidth;
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
            ResizeHandle.TopCenter,
            ResizeHandle.MiddleLeft, ResizeHandle.MiddleRight,
            ResizeHandle.BottomCenter
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
            _dragging        = !_resizing;
            Capture          = true;

            if (_activeHandle == ResizeHandle.None && (e.Clicks >= 2 || _textEditMode))
                EnterTextMode(TextIndexAt(e.Location));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

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
                Location = new Point(
                    Math.Max(0, _startLocation.X + dx),
                    Math.Max(0, _startLocation.Y + dy));
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
            }

            SetBounds(nx, ny, nw, nh);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _dragging = _resizing = false;
            _activeHandle = ResizeHandle.None;
            Capture = false;
        }

        // ── Keyboard ──────────────────────────────────────────────────────────
        protected override bool IsInputKey(Keys keyData) => true;

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);
            char c = e.KeyChar;
            if (c == (char)Keys.Back || c == (char)27 || c == '\r' || c == '\n') return;

            if (!_textEditMode) EnterTextMode();

            string output = Document.PrintMap != null
                ? Document.PrintMap.Convert(ref _inputPending, c)
                : c.ToString();

            if (output == null) { e.Handled = true; return; } // still buffering multi-char

            _text      = _text.Substring(0, _cursorPos) + output + _text.Substring(_cursorPos);
            _cursorPos += output.Length;
            _caretVisible = true;
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
                    else if (_cursorPos < _text.Length)
                    {
                        _text = _text.Substring(0, _cursorPos) + _text.Substring(_cursorPos + 1);
                        _caretVisible = true; Invalidate();
                    }
                    e.Handled = true;
                    break;

                case Keys.Back:
                    if (_textEditMode && _cursorPos > 0)
                    {
                        _text = _text.Substring(0, _cursorPos - 1) + _text.Substring(_cursorPos);
                        _cursorPos--; _caretVisible = true; Invalidate();
                    }
                    e.Handled = true;
                    break;

                case Keys.Enter:
                    FlushInputPending();
                    if (!_textEditMode) EnterTextMode();
                    _text = _text.Substring(0, _cursorPos) + '\n' + _text.Substring(_cursorPos);
                    _cursorPos++;
                    _caretVisible = true; Invalidate();
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
                    { _cursorPos--; _caretVisible = true; Invalidate(); }
                    e.Handled = true;
                    break;

                case Keys.Right:
                    FlushInputPending();
                    if (_textEditMode && _cursorPos < _text.Length)
                    { _cursorPos++; _caretVisible = true; Invalidate(); }
                    e.Handled = true;
                    break;

                case Keys.Home:
                    if (_textEditMode)
                    {
                        GetLineCol(_cursorPos, out int hl, out _);
                        _cursorPos = LineColToIndex(hl, 0);
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
                            _caretVisible = true; Invalidate();
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
            _textEditMode = false;
            _caretVisible = false;
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            ExitTextMode();
        }
    }
}
