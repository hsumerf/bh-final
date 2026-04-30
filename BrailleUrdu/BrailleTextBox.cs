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

        private const int HANDLE_SIZE = 8;
        private const int PAD         = 2;

        // ── Content & cursor ─────────────────────────────────────────────────
        private string _text      = "";
        private int    _cursorPos = 0;

        public string BrailleText
        {
            get => _text;
            set { _text = value ?? ""; _cursorPos = _text.Length; Invalidate(); }
        }

        // Two modes: object-selected (handles, no cursor) vs text-edit (cursor, typing).
        // Single-click → object mode. Double-click / any printable key → text-edit mode.
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
            Size         = new Size(CellPx * 10, LinePx);
            MinimumSize  = new Size(CellPx, LinePx);

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
            _textEditMode = true;
            _cursorPos    = cursorIndex < 0 ? _text.Length : cursorIndex;
            _caretVisible = true;
            _caretTimer.Start();
            Invalidate();
        }

        private void ExitTextMode()
        {
            _textEditMode = false;
            _caretTimer.Stop();
            _caretVisible = false;
            Invalidate();
        }

        private void DeleteSelf()
        {
            // Defer until the current event stack unwinds — disposing inside
            // an event handler for this same control crashes WinForms silently.
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

            DrawBrailleDots(g);

            if (Focused)
            {
                if (_textEditMode && _caretVisible) DrawCaret(g);

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
                using (var pen = new Pen(Color.FromArgb(160, 160, 160), 1f) { DashStyle = DashStyle.Dash })
                    g.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
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
            ResizeHandle.TopCenter,
            ResizeHandle.MiddleLeft, ResizeHandle.MiddleRight,
            ResizeHandle.BottomCenter
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

            if (_activeHandle == ResizeHandle.None)
            {
                if (e.Clicks >= 2 || _textEditMode)
                    EnterTextMode(TextIndexAt(e.Location));
                // single click: object-selected mode — cursor stays hidden
            }
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
            if (e.KeyChar == (char)Keys.Back || e.KeyChar == (char)27) return; // handled in OnKeyDown

            // Any printable character auto-enters text-edit mode
            if (!_textEditMode) EnterTextMode();

            string braille = BrailleMapper.ToBraille(e.KeyChar);
            if (!string.IsNullOrEmpty(braille))
            {
                _text      = _text.Substring(0, _cursorPos) + braille + _text.Substring(_cursorPos);
                _cursorPos += braille.Length;
                _caretVisible = true;
                Invalidate();
            }
            e.Handled = true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.KeyCode)
            {
                // ── Object-mode actions ───────────────────────────────────────
                case Keys.Delete:
                    if (!_textEditMode) { DeleteSelf(); }
                    else if (_cursorPos < _text.Length)
                    {
                        _text = _text.Substring(0, _cursorPos) + _text.Substring(_cursorPos + 1);
                        _caretVisible = true;
                        Invalidate();
                    }
                    e.Handled = true;
                    break;

                case Keys.Back:
                    if (_textEditMode && _cursorPos > 0)
                    {
                        _text = _text.Substring(0, _cursorPos - 1) + _text.Substring(_cursorPos);
                        _cursorPos--;
                        _caretVisible = true;
                        Invalidate();
                    }
                    e.Handled = true;
                    break;

                case Keys.Escape:
                    if (_textEditMode) ExitTextMode();   // text → object mode
                    else Parent?.Focus();                // object → deselect
                    e.Handled = true;
                    break;

                // ── Text-mode navigation ──────────────────────────────────────
                case Keys.Left:
                    if (_textEditMode && _cursorPos > 0)
                    { _cursorPos--; _caretVisible = true; Invalidate(); }
                    e.Handled = true;
                    break;

                case Keys.Right:
                    if (_textEditMode && _cursorPos < _text.Length)
                    { _cursorPos++; _caretVisible = true; Invalidate(); }
                    e.Handled = true;
                    break;

                case Keys.Home:
                    if (_textEditMode) { _cursorPos = 0; _caretVisible = true; Invalidate(); }
                    e.Handled = true;
                    break;

                case Keys.End:
                    if (_textEditMode) { _cursorPos = _text.Length; _caretVisible = true; Invalidate(); }
                    e.Handled = true;
                    break;
            }
        }

        // ── Focus ─────────────────────────────────────────────────────────────
        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            // Enter object-selected mode; typing will upgrade to text-edit mode
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

    public enum ResizeHandle
    {
        None,
        TopLeft, TopCenter, TopRight,
        MiddleLeft, MiddleRight,
        BottomLeft, BottomCenter, BottomRight
    }
}
