using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BrailleUrdu
{
    public class ImageBox : UserControl
    {
        private const int H = 6; // resize handle size

        public Image SourceImage { get; }
        public bool  IsSelected  { get; set; }

        private bool  _selected;
        private bool  _dragging;
        private bool  _resizing;
        private Point _dragStart;
        private Point _origLocation;
        private Size  _origSize;
        private int   _resizeHandle; // 0=none  1=TL 2=TR 3=BL 4=BR
        private System.Collections.Generic.Dictionary<Control, Point> _groupStartLocations;

        public ImageBox(Image image)
        {
            SourceImage = image;
            Width       = 200;
            Height      = 150;
            _selected   = true;

            SetStyle(ControlStyles.UserPaint
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, false);
            BackColor = Color.Transparent;
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
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

            g.DrawImage(SourceImage, 0, 0, Width, Height);

            g.SmoothingMode = SmoothingMode.None;
            if (_selected)
            {
                using (var pen = new Pen(Color.DodgerBlue, 1.5f))
                    g.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);

                foreach (var r in GetHandles())
                {
                    g.FillRectangle(Brushes.White, r);
                    g.DrawRectangle(Pens.DodgerBlue, r);
                }
            }
            else if (IsSelected)
            {
                using (var pen = new Pen(Color.DodgerBlue, 1.5f) { DashStyle = DashStyle.Dash })
                    g.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
            }
        }

        // ── Resize handles (4 corners) ────────────────────────────────────────

        private Rectangle[] GetHandles() => new[]
        {
            new Rectangle(0,         0,          H, H), // TL
            new Rectangle(Width - H, 0,          H, H), // TR
            new Rectangle(0,         Height - H, H, H), // BL
            new Rectangle(Width - H, Height - H, H, H), // BR
        };

        private int HitHandle(Point p)
        {
            var handles = GetHandles();
            for (int i = 0; i < handles.Length; i++)
                if (handles[i].Contains(p)) return i + 1;
            return 0;
        }

        // ── Mouse ─────────────────────────────────────────────────────────────

        protected override void OnMouseDown(MouseEventArgs e)
        {
            _dragStart    = PointToScreen(e.Location);
            base.OnMouseDown(e);
            _selected     = true;
            _origLocation = Location;
            _origSize     = Size;
            _resizeHandle = HitHandle(e.Location);
            _resizing     = _resizeHandle != 0;
            _dragging     = !_resizing;
            Focus();
            Invalidate();

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
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (!_dragging && !_resizing)
            {
                int h = HitHandle(e.Location);
                Cursor = h == 1 || h == 4 ? Cursors.SizeNWSE
                       : h == 2 || h == 3 ? Cursors.SizeNESW
                       : Cursors.SizeAll;
                return;
            }

            var cur = PointToScreen(e.Location);
            int dx  = cur.X - _dragStart.X;
            int dy  = cur.Y - _dragStart.Y;

            if (_dragging)
            {
                if (_groupStartLocations != null)
                {
                    var canvasSc = Parent as ScrollableControl;
                    int minX2    = canvasSc?.AutoScrollPosition.X ?? 0;
                    int minY2    = canvasSc?.AutoScrollPosition.Y ?? 0;
                    foreach (var kvp in _groupStartLocations)
                        kvp.Key.Location = new Point(
                            Math.Max(minX2, kvp.Value.X + dx),
                            Math.Max(minY2, kvp.Value.Y + dy));
                }
                else
                {
                    Location = new Point(_origLocation.X + dx, _origLocation.Y + dy);
                }
                return;
            }

            int nx = _origLocation.X, ny = _origLocation.Y;
            int nw = _origSize.Width,  nh = _origSize.Height;

            switch (_resizeHandle)
            {
                case 1: nx += dx; ny += dy; nw -= dx; nh -= dy; break; // TL
                case 2:           ny += dy; nw += dx; nh -= dy; break; // TR
                case 3: nx += dx;           nw -= dx; nh += dy; break; // BL
                case 4:                     nw += dx; nh += dy; break; // BR
            }

            if (nw < 20) nw = 20;
            if (nh < 20) nh = 20;
            SetBounds(nx, ny, nw, nh);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _dragging            = _resizing = false;
            _groupStartLocations = null;
        }

        // ── Focus / keyboard ──────────────────────────────────────────────────

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            _selected = false;
            Invalidate();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Delete)
                BeginInvoke((Action)DeleteSelf);
            e.Handled = true;
        }

        private void DeleteSelf()
        {
            var parent = Parent;
            parent?.Controls.Remove(this);
            Dispose();
        }

        protected override bool IsInputKey(Keys keyData) => true;

        protected override void Dispose(bool disposing)
        {
            if (disposing) SourceImage?.Dispose();
            base.Dispose(disposing);
        }
    }
}
