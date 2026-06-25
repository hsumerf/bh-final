using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BrailleUrdu
{
    public class PageNumberBox : UserControl
    {
        private const int PAD = 6;

        private static float PxPerMm => 96f / 25.4f * 1.10f;
        private static int   CellPx  => (int)(DocumentPage.CELL_WIDTH_MM  * PxPerMm);
        private static int   LinePx  => (int)(DocumentPage.LINE_HEIGHT_MM * PxPerMm);

        private string _displayText  = "#";
        private string _brailleText  = "⠼";   // braille placeholder on master page

        private bool  _dragging;
        private Point _mouseDownScreen;
        private Point _startLocation;
        private Dictionary<Control, Point> _groupStartLocations;

        public bool   IsSelected      { get; set; }
        public bool   IsBraille       { get; set; }
        public string DisplayText     => _displayText;
        public string BrailleDisplayText => _brailleText;

        public PageNumberBox()
        {
            SetStyle(
                ControlStyles.UserPaint              |
                ControlStyles.AllPaintingInWmPaint   |
                ControlStyles.Selectable             |
                ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, false);
            BackColor   = Color.Transparent;
            TabStop     = true;
            MinimumSize = new Size(24, 22);
            Size        = new Size(24, 22);
        }

        // Called by CanvasPanel.PageChanged() each time the active page changes.
        // pageIndex < 0 means we are viewing a master page → show placeholder.
        public void UpdateNumber(int pageIndex)
        {
            _displayText = pageIndex < 0 ? "#" : (pageIndex + 1).ToString();
            _brailleText = PageNumToBraille(_displayText);
            FitSize();
            Invalidate();
        }

        // Stateless helper used by the render pipeline so it doesn't depend on cached _brailleText.
        public string GetBrailleForPage(int pageIdx)
            => PageNumToBraille(pageIdx < 0 ? "#" : (pageIdx + 1).ToString());

        // Converts a page-number string to its braille Unicode representation.
        // "#" (master-page placeholder) → number indicator ⠼ alone.
        private static string PageNumToBraille(string numStr)
        {
            if (numStr == "#") return "⠼⠁"; // placeholder: indicator + digit 1 so width matches real pages
            var sb = new System.Text.StringBuilder();
            sb.Append('⠼'); // number indicator — must precede braille digits
            foreach (char c in numStr)
            {
                string cell = BrailleMapper.ToBraille(c);
                if (!string.IsNullOrEmpty(cell)) sb.Append(cell);
            }
            return sb.ToString();
        }

        private void FitSize()
        {
            if (!IsHandleCreated) return;
            try
            {
                if (IsBraille)
                {
                    int cells = Math.Max(1, _brailleText.Length);
                    Size = new Size(
                        Math.Max(MinimumSize.Width,  PAD * 2 + cells * CellPx),
                        Math.Max(MinimumSize.Height, PAD + LinePx));
                }
                else
                {
                    using (var font = MakeFont())
                    using (var g    = CreateGraphics())
                    {
                        var sz = g.MeasureString(_displayText, font);
                        Size = new Size(
                            Math.Max(MinimumSize.Width,  (int)sz.Width  + PAD * 2),
                            Math.Max(MinimumSize.Height, (int)sz.Height + PAD));
                    }
                }
            }
            catch { }
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            if (Parent != null) FitSize();
        }

        private static Font MakeFont() =>
            new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);

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

            if (IsBraille)
                DrawBrailleDots(g);
            else
                DrawText(g);

            if (Focused)
            {
                using (var pen = new Pen(Color.DodgerBlue, 2f))
                    g.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
            }
            else if (IsSelected)
            {
                using (var pen = new Pen(Color.DodgerBlue, 1.5f) { DashStyle = DashStyle.Dash })
                    g.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
            }
            else
            {
                using (var pen = new Pen(Color.FromArgb(160, 160, 160), 1f) { DashStyle = DashStyle.Dot })
                    g.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
            }
        }

        private void DrawText(Graphics g)
        {
            using (var font  = MakeFont())
            using (var brush = new SolidBrush(Color.Black))
            {
                var fmt = new StringFormat
                {
                    Alignment     = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString(_displayText, font, brush,
                    new RectangleF(0, 0, Width, Height), fmt);
            }
        }

        private void DrawBrailleDots(Graphics g)
        {
            if (string.IsNullOrEmpty(_brailleText)) return;

            float dotSpacePx = DocumentPage.DOT_SPACING_MM * PxPerMm;
            float dotRad     = Math.Max(1.5f, dotSpacePx * 0.27f);
            float cellW      = CellPx;
            float lineH      = LinePx;
            float dotOffsetX = (cellW - dotSpacePx)      / 2f;
            float dotOffsetY = (lineH  - 2 * dotSpacePx) / 2f;
            int   col        = 0;

            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var brush = new SolidBrush(Color.FromArgb(34, 139, 34)))
            {
                foreach (char c in _brailleText)
                {
                    if ((int)c < 0x2800 || (int)c > 0x28FF) { col++; continue; }

                    float ox = PAD + col * cellW;
                    float oy = PAD;
                    int   bits = (int)c - 0x2800;

                    for (int b = 0; b < 8; b++)
                    {
                        if ((bits & (1 << b)) == 0) continue;
                        int   dcol = b < 6 ? b / 3 : b - 6;
                        int   drow = b < 6 ? b % 3 : 3;
                        float cx   = ox + dotOffsetX + dcol * dotSpacePx;
                        float cy   = oy + dotOffsetY + drow * dotSpacePx;
                        g.FillEllipse(brush, cx - dotRad, cy - dotRad, dotRad * 2, dotRad * 2);
                    }
                    col++;
                }
            }
            g.SmoothingMode = SmoothingMode.None;
        }

        // ── Mouse (drag only — no resize) ─────────────────────────────────────
        protected override void OnMouseDown(MouseEventArgs e)
        {
            _mouseDownScreen = PointToScreen(e.Location);
            base.OnMouseDown(e);
            Focus();
            _startLocation   = Location;
            _dragging        = true;
            Capture          = true;

            var canvas = Parent as CanvasPanel;
            if (canvas != null && IsSelected && canvas.SelectedControls.Count > 1)
            {
                _groupStartLocations = new Dictionary<Control, Point>();
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
        protected override bool IsInputKey(Keys keyData) =>
            keyData == Keys.Delete || keyData == Keys.Back;

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back)
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
