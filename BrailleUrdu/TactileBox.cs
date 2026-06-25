using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BrailleUrdu
{
    public class TactileBox : UserControl
    {
        private const int PAD = 2;

        private static float PxPerMm => 96f / 25.4f * 1.10f;

        private bool[,] _dots = new bool[0, 0];
        private int     _cols, _rows;

        private bool  _dragging;
        private Point _mouseDownScreen, _startLocation;
        private System.Collections.Generic.Dictionary<Control, Point> _groupStartLocations;

        public bool IsSelected { get; set; }

        public bool[,] DotGrid
        {
            get => _dots;
            set
            {
                _dots = value ?? new bool[0, 0];
                _cols = _dots.GetLength(0);
                _rows = _dots.GetLength(1);
                if (_cols > 0 && _rows > 0)
                {
                    float sp = DocumentPage.DOT_SPACING_MM * PxPerMm;
                    Size = new Size((int)(_cols * sp) + PAD * 2,
                                   (int)(_rows * sp) + PAD * 2);
                }
                Invalidate();
            }
        }

        public TactileBox()
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
            Size         = new Size(120, 120);
            MinimumSize  = new Size(40, 40);
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

            if (_cols > 0 && _rows > 0)
            {
                float spX = (float)(Width  - PAD * 2) / _cols;
                float spY = (float)(Height - PAD * 2) / _rows;
                float r   = Math.Max(1.5f, DocumentPage.DOT_SPACING_MM * PxPerMm * 0.27f);

                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(Color.FromArgb(34, 139, 34)))
                {
                    for (int c = 0; c < _cols; c++)
                    for (int row = 0; row < _rows; row++)
                    {
                        if (!_dots[c, row]) continue;
                        float cx = PAD + c   * spX + spX * 0.5f;
                        float cy = PAD + row * spY + spY * 0.5f;
                        g.FillEllipse(brush, cx - r, cy - r, r * 2, r * 2);
                    }
                }
                g.SmoothingMode = SmoothingMode.None;
            }

            if (Focused)
            {
                using (var pen = new Pen(Color.DodgerBlue, 1.5f))
                    g.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
            }
            else if (IsSelected)
            {
                using (var pen = new Pen(Color.DodgerBlue, 1.5f) { DashStyle = DashStyle.Dash })
                    g.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
            }
            else if (_cols == 0)
            {
                using (var pen = new Pen(Color.FromArgb(160, 160, 160), 1f) { DashStyle = DashStyle.Dash })
                    g.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
            }
        }

        // ── Mouse (drag only — no resize) ────────────────────────────────────

        protected override void OnMouseDown(MouseEventArgs e)
        {
            // Capture screen coords BEFORE base.OnMouseDown: WinForms fires focus
            // internally which can trigger AutoScroll and physically move the HWND,
            // making a post-move PointToScreen give the wrong drag anchor.
            _mouseDownScreen = PointToScreen(e.Location);
            base.OnMouseDown(e);
            Focus();
            _startLocation   = Location; // captured after any focus-induced scroll
            _dragging        = true;
            Capture          = true;

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

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_dragging) return;
            var screen = PointToScreen(e.Location);
            int dx = screen.X - _mouseDownScreen.X;
            int dy = screen.Y - _mouseDownScreen.Y;

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
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _dragging            = false;
            _groupStartLocations = null;
            Capture              = false;
        }

        // ── Keyboard ──────────────────────────────────────────────────────────

        protected override bool IsInputKey(Keys keyData) => true;

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Delete)
            {
                BeginInvoke((Action)(() => { Parent?.Controls.Remove(this); Dispose(); }));
                e.Handled = true;
            }
        }

        protected override void OnGotFocus(EventArgs e)  { base.OnGotFocus(e);  Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    }
}
