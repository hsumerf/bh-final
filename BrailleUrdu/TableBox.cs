using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BrailleUrdu
{
    public class TableBox : UserControl
    {
        private const int HANDLE_SIZE = 10;

        // ── Properties ────────────────────────────────────────────────────────
        private int[] _rowSizes = { 1, 1 };
        private int[] _colSizes = { 1, 1 };
        private Color _lineColor = Color.Black;
        private int   _lineThickness = 1;

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

        // RowSpec / ColSpec: dash-separated positive integers, e.g. "1-2-1"
        public string RowSpec
        {
            get => FormatSpec(_rowSizes);
            set { _rowSizes = ParseSpec(value); Invalidate(); }
        }

        public string ColSpec
        {
            get => FormatSpec(_colSizes);
            set { _colSizes = ParseSpec(value); Invalidate(); }
        }

        // ── Drag / resize ─────────────────────────────────────────────────────
        private bool         _dragging;
        private bool         _resizing;
        private Point        _mouseDownScreen;
        private Point        _startLocation;
        private Size         _startSize;
        private ResizeHandle _activeHandle = ResizeHandle.None;

        // ── Construction ──────────────────────────────────────────────────────
        public TableBox()
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
            MinimumSize  = new Size(HANDLE_SIZE * 3, HANDLE_SIZE * 3);
        }

        public int[] RowSizes => _rowSizes;
        public int[] ColSizes => _colSizes;

        // ── Spec helpers ──────────────────────────────────────────────────────
        private static string FormatSpec(int[] sizes) => string.Join("-", sizes);

        private static int[] ParseSpec(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return new[] { 1 };
            var result = new List<int>();
            foreach (var part in s.Split('-'))
                if (int.TryParse(part.Trim(), out int v) && v > 0)
                    result.Add(v);
            return result.Count > 0 ? result.ToArray() : new[] { 1 };
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

            using (var pen = new Pen(_lineColor, _lineThickness))
            {
                // Outer border
                float h = _lineThickness / 2f;
                g.DrawRectangle(pen, h, h, Width - _lineThickness, Height - _lineThickness);

                // Horizontal dividers
                float totalRowUnits = SumOf(_rowSizes);
                float y = 0;
                for (int i = 0; i < _rowSizes.Length - 1; i++)
                {
                    y += _rowSizes[i] / totalRowUnits * Height;
                    g.DrawLine(pen, 0, y, Width, y);
                }

                // Vertical dividers
                float totalColUnits = SumOf(_colSizes);
                float x = 0;
                for (int i = 0; i < _colSizes.Length - 1; i++)
                {
                    x += _colSizes[i] / totalColUnits * Width;
                    g.DrawLine(pen, x, 0, x, Height);
                }
            }

            if (Focused)
            {
                using (var pen = new Pen(Color.DodgerBlue, 2f))
                    g.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);

                foreach (var hnd in new[] {
                    ResizeHandle.TopLeft,    ResizeHandle.TopCenter,    ResizeHandle.TopRight,
                    ResizeHandle.MiddleLeft,                            ResizeHandle.MiddleRight,
                    ResizeHandle.BottomLeft, ResizeHandle.BottomCenter, ResizeHandle.BottomRight })
                {
                    var r = GetHandleRect(hnd);
                    g.FillRectangle(Brushes.DodgerBlue, r);
                    using (var pen = new Pen(Color.White, 1f))
                        g.DrawRectangle(pen, r);
                }
            }
        }

        private static float SumOf(int[] arr)
        {
            float s = 0; foreach (int v in arr) s += v; return s;
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
