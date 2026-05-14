using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BrailleUrdu
{
    public class LineBox : UserControl
    {
        public enum LineDirection { Horizontal, Vertical }

        private const int HANDLE_SIZE = 10;

        // ── Properties ────────────────────────────────────────────────────────
        private LineDirection _direction     = LineDirection.Horizontal;
        private Color         _lineColor     = Color.Black;
        private int           _lineThickness = 1;

        public LineDirection Direction
        {
            get => _direction;
            set { _direction = value; Invalidate(); }
        }

        public Color LineColor
        {
            get => _lineColor;
            set { _lineColor = value; Invalidate(); }
        }

        public int LineThickness
        {
            get => _lineThickness;
            set { _lineThickness = Math.Max(1, value); Invalidate(); }
        }

        // ── Drag / resize state ───────────────────────────────────────────────
        private bool         _dragging;
        private bool         _resizing;
        private Point        _mouseDownScreen;
        private Point        _startLocation;
        private Size         _startSize;
        private ResizeHandle _activeHandle = ResizeHandle.None;

        // ── Construction ──────────────────────────────────────────────────────
        public LineBox()
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
            MinimumSize  = new Size(HANDLE_SIZE * 3, HANDLE_SIZE * 3);
        }

        // ── Paint ─────────────────────────────────────────────────────────────
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (var pen = new Pen(_lineColor, _lineThickness))
            {
                if (_direction == LineDirection.Horizontal)
                    g.DrawLine(pen, 0, Height / 2f, Width, Height / 2f);
                else
                    g.DrawLine(pen, Width / 2f, 0, Width / 2f, Height);
            }

            if (Focused)
            {
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
        }

        // ── Handle geometry ───────────────────────────────────────────────────
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

        private static readonly ResizeHandle[] _handles =
        {
            ResizeHandle.TopLeft,    ResizeHandle.TopCenter,    ResizeHandle.TopRight,
            ResizeHandle.MiddleLeft,                            ResizeHandle.MiddleRight,
            ResizeHandle.BottomLeft, ResizeHandle.BottomCenter, ResizeHandle.BottomRight
        };

        private ResizeHandle HitTest(Point p)
        {
            foreach (var h in _handles)
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
            _dragging        = !_resizing;
            Capture          = true;
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
            _activeHandle = ResizeHandle.None;
            Capture = false;
        }

        // ── Keyboard ──────────────────────────────────────────────────────────
        protected override bool IsInputKey(Keys keyData) => true;

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Delete || e.KeyCode == Keys.Escape)
            {
                BeginInvoke((Action)(() => { Parent?.Controls.Remove(this); Dispose(); }));
                e.Handled = true;
            }
        }

        // ── Focus ─────────────────────────────────────────────────────────────
        protected override void OnGotFocus(EventArgs e)  { base.OnGotFocus(e);  Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    }
}
