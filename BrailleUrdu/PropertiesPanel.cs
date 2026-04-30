using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BrailleUrdu
{
    public class PropertiesPanel : Panel
    {
        private readonly CanvasPanel _canvas;
        private Control _target;
        private bool    _updating;

        private TextBox _tbX, _tbY, _tbW, _tbH;

        public PropertiesPanel(CanvasPanel canvas)
        {
            _canvas   = canvas;
            Width     = 260;
            Dock      = DockStyle.Right;
            BackColor = Color.FromArgb(240, 240, 240);

            Build();
            canvas.SelectionChanged += OnSelectionChanged;
        }

        // ── UI construction ───────────────────────────────────────────────────

        private void Build()
        {
            // ── Alignment row ─────────────────────────────────────────────────
            var alignRow = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 50,
                BackColor = Color.FromArgb(235, 235, 235)
            };
            alignRow.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.Silver, 1))
                    e.Graphics.DrawLine(pen, 0, alignRow.Height - 1, alignRow.Width, alignRow.Height - 1);
            };

            string[] tips = {
                "Align Left", "Center Horizontally", "Align Right",
                "Align Top",  "Center Vertically",   "Align Bottom"
            };
            string[] modes = { "left", "centerH", "right", "top", "centerV", "bottom" };

            for (int i = 0; i < 6; i++)
            {
                int   idx     = i;
                int   groupX  = i < 3 ? 10 + i * 32 : 10 + i * 32 + 10; // 10px gap between groups
                var   btn     = new Button
                {
                    Width     = 28,
                    Height    = 28,
                    Location  = new Point(groupX, 11),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.White,
                    Cursor    = Cursors.Hand
                };
                btn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
                new ToolTip().SetToolTip(btn, tips[i]);
                var mode = modes[i];
                btn.Click += (s, e) => AlignSelected(mode);
                btn.Paint += (s, e) => DrawAlignIcon(e.Graphics, idx, btn.Width, btn.Height);
                alignRow.Controls.Add(btn);
            }

            // ── Transform section ─────────────────────────────────────────────
            var transformArea = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 108,
                BackColor = Color.Transparent
            };

            var lblTitle = new Label
            {
                Text      = "Transform",
                Location  = new Point(12, 12),
                AutoSize  = true,
                Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(90, 90, 90)
            };

            var lblX = FieldLabel("X", new Point(12, 46));
            _tbX     = FieldBox(new Point(28, 42));

            var lblY = FieldLabel("Y", new Point(140, 46));
            _tbY     = FieldBox(new Point(156, 42));

            var lblW = FieldLabel("W", new Point(12, 76));
            _tbW     = FieldBox(new Point(28, 72));

            var lblH = FieldLabel("H", new Point(140, 76));
            _tbH     = FieldBox(new Point(156, 72));

            foreach (var tb in new[] { _tbX, _tbY, _tbW, _tbH })
            {
                tb.KeyDown   += (s, e) => { if (e.KeyCode == Keys.Return) ApplyTransform(); };
                tb.LostFocus += (s, e) => ApplyTransform();
            }

            transformArea.Controls.AddRange(new Control[] {
                lblTitle,
                lblX, _tbX, lblY, _tbY,
                lblW, _tbW, lblH, _tbH
            });

            // Dock order: add Fill anchor first, then Top panels (last-added docks first)
            Controls.Add(transformArea);
            Controls.Add(alignRow);
        }

        private static Label FieldLabel(string text, Point loc) => new Label
        {
            Text      = text,
            Location  = loc,
            Size      = new Size(14, 22),
            Font      = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(120, 120, 120),
            TextAlign = ContentAlignment.MiddleLeft
        };

        private static TextBox FieldBox(Point loc) => new TextBox
        {
            Location  = loc,
            Size      = new Size(82, 22),
            Font      = new Font("Segoe UI", 9f),
            Text      = "",
            TextAlign = HorizontalAlignment.Right,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(175, 65, 95)
        };

        // ── Selection handling ────────────────────────────────────────────────

        private void OnSelectionChanged(Control ctrl)
        {
            if (_target != null)
            {
                _target.LocationChanged -= OnTargetTransformed;
                _target.SizeChanged     -= OnTargetTransformed;
            }
            _target = ctrl;
            if (_target != null)
            {
                _target.LocationChanged += OnTargetTransformed;
                _target.SizeChanged     += OnTargetTransformed;
            }
            RefreshFields();
        }

        private void OnTargetTransformed(object sender, EventArgs e) => RefreshFields();

        private void RefreshFields()
        {
            if (_updating) return;
            _updating = true;
            try
            {
                if (_target == null || _target.IsDisposed)
                {
                    _tbX.Text = _tbY.Text = _tbW.Text = _tbH.Text = "";
                    return;
                }
                var origin = _canvas.PageOriginPx;
                _tbX.Text = ((int)(_target.Left  - origin.X)).ToString();
                _tbY.Text = ((int)(_target.Top   - origin.Y)).ToString();
                _tbW.Text = _target.Width.ToString();
                _tbH.Text = _target.Height.ToString();
            }
            finally { _updating = false; }
        }

        private void ApplyTransform()
        {
            if (_updating || _target == null || _target.IsDisposed) return;
            if (!int.TryParse(_tbX.Text, out int x) ||
                !int.TryParse(_tbY.Text, out int y) ||
                !int.TryParse(_tbW.Text, out int w) ||
                !int.TryParse(_tbH.Text, out int h)) return;

            _updating = true;
            try
            {
                var origin = _canvas.PageOriginPx;
                _target.Location = new Point((int)origin.X + x, (int)origin.Y + y);
                if (w >= 10 && h >= 10)
                    _target.Size = new Size(w, h);
            }
            finally { _updating = false; }
        }

        // ── Alignment ─────────────────────────────────────────────────────────

        private void AlignSelected(string mode)
        {
            if (_target == null || _target.IsDisposed) return;

            var   origin = _canvas.PageOriginPx;
            float pw     = _canvas.PageWidthPx;
            float ph     = _canvas.PageHeightPx;
            int   ox     = (int)origin.X;
            int   oy     = (int)origin.Y;

            int x = _target.Left, y = _target.Top;

            switch (mode)
            {
                case "left":    x = ox;                                       break;
                case "centerH": x = ox + (int)((pw - _target.Width)  / 2f);  break;
                case "right":   x = ox + (int)pw  - _target.Width;            break;
                case "top":     y = oy;                                       break;
                case "centerV": y = oy + (int)((ph - _target.Height) / 2f);  break;
                case "bottom":  y = oy + (int)ph - _target.Height;            break;
            }

            _target.Location = new Point(x, y);
        }

        // ── Alignment icon drawing ────────────────────────────────────────────
        //  0=AlignLeft  1=CenterH  2=AlignRight
        //  3=AlignTop   4=CenterV  5=AlignBottom

        private static void DrawAlignIcon(Graphics g, int idx, int bw, int bh)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int cx = bw / 2, cy = bh / 2;

            using (var lp  = new Pen(Color.FromArgb(90, 90, 90), 2f))
            using (var rb  = new SolidBrush(Color.FromArgb(130, 175, 215)))
            {
                switch (idx)
                {
                    case 0: // Align Left — vertical guide on left
                        g.DrawLine(lp, 4, 4, 4, bh - 4);
                        g.FillRectangle(rb, 7,  7,  13, 5);
                        g.FillRectangle(rb, 7,  16, 17, 5);
                        break;
                    case 1: // Center Horizontally — vertical guide in center
                        g.DrawLine(lp, cx, 4, cx, bh - 4);
                        g.FillRectangle(rb, cx - 7,  7,  14, 5);
                        g.FillRectangle(rb, cx - 9,  16, 18, 5);
                        break;
                    case 2: // Align Right — vertical guide on right
                        g.DrawLine(lp, bw - 4, 4, bw - 4, bh - 4);
                        g.FillRectangle(rb, bw - 20, 7,  13, 5);
                        g.FillRectangle(rb, bw - 24, 16, 17, 5);
                        break;
                    case 3: // Align Top — horizontal guide on top
                        g.DrawLine(lp, 4, 4, bw - 4, 4);
                        g.FillRectangle(rb, 7,  7,  5, 13);
                        g.FillRectangle(rb, 16, 7,  5, 17);
                        break;
                    case 4: // Center Vertically — horizontal guide in center
                        g.DrawLine(lp, 4, cy, bw - 4, cy);
                        g.FillRectangle(rb, 7,  cy - 7,  5, 14);
                        g.FillRectangle(rb, 16, cy - 9,  5, 18);
                        break;
                    case 5: // Align Bottom — horizontal guide on bottom
                        g.DrawLine(lp, 4, bh - 4, bw - 4, bh - 4);
                        g.FillRectangle(rb, 7,  bh - 20, 5, 13);
                        g.FillRectangle(rb, 16, bh - 24, 5, 17);
                        break;
                }
            }
        }
    }
}
