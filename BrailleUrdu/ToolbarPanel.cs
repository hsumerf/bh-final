using System;
using System.Drawing;
using System.Windows.Forms;

namespace BrailleUrdu
{
    public class ToolbarPanel : Panel
    {
        public event EventHandler      BrailleToolClicked;
        public event EventHandler      TextToolClicked;
        public event EventHandler      ImageToolClicked;
        public event EventHandler      TactileToolClicked;
        public event Action<string>    ViewModeChanged;

        public ToolbarPanel()
        {
            Height    = 46;
            Dock      = DockStyle.Top;
            BackColor = Color.FromArgb(245, 245, 245);
            Build();
        }

        private void Build()
        {
            // Bottom border drawn on paint
            Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(210, 210, 210), 1))
                    e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
            };

            var btnText    = MakeButton("T",  "Text Tool");
            var btnImage   = MakeButton("",   "Image Tool");
            var btnBraille = MakeButton("B",  "Braille Tool");
            var btnTactile = MakeButton("",   "Tactile Graphic Tool");

            btnText.Click    += (s, e) => TextToolClicked?.Invoke(this, EventArgs.Empty);
            btnImage.Click   += (s, e) => ImageToolClicked?.Invoke(this, EventArgs.Empty);
            btnBraille.Click += (s, e) => BrailleToolClicked?.Invoke(this, EventArgs.Empty);
            btnTactile.Click += (s, e) => TactileToolClicked?.Invoke(this, EventArgs.Empty);

            // Draw a simple landscape icon on the image button
            btnImage.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var pen = new Pen(Color.FromArgb(80, 80, 80), 1.5f))
                    g.DrawRectangle(pen, 9, 11, 22, 18);
                g.FillEllipse(new System.Drawing.SolidBrush(Color.FromArgb(210, 155, 20)), 12, 14, 6, 6);
                var pts = new[] {
                    new Point(10, 28), new Point(17, 19), new Point(22, 24),
                    new Point(26, 20), new Point(31, 28)
                };
                g.FillPolygon(new System.Drawing.SolidBrush(Color.FromArgb(70, 130, 70)), pts);
            };

            // Draw a 4×4 dot-grid icon on the tactile button
            btnTactile.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                int[] filled = { 5, 6, 9, 10 }; // indices in a 4×4 grid that are "raised"
                for (int i = 0; i < 16; i++)
                {
                    int col = i % 4, row = i / 4;
                    float cx = 10 + col * 6f;
                    float cy = 11 + row * 6f;
                    bool on = System.Array.IndexOf(filled, i) >= 0;
                    float r = on ? 2.5f : 1.3f;
                    var color = on ? Color.FromArgb(50, 50, 50) : Color.FromArgb(180, 180, 180);
                    g.FillEllipse(new System.Drawing.SolidBrush(color), cx - r, cy - r, r * 2, r * 2);
                }
            };

            // Wrap four buttons in a container so we can center it as one unit
            var group = new Panel
            {
                Width  = 184,  // 4 × 40px + 3 × 8px gap
                Height = 40,
                Anchor = AnchorStyles.None
            };

            btnText.Location    = new Point(0,   0);
            btnImage.Location   = new Point(48,  0);
            btnBraille.Location = new Point(96,  0);
            btnTactile.Location = new Point(144, 0);
            group.Controls.Add(btnText);
            group.Controls.Add(btnImage);
            group.Controls.Add(btnBraille);
            group.Controls.Add(btnTactile);

            Controls.Add(group);

            // ── View mode dropdown (right-aligned) ────────────────────────────
            var lblView = new Label
            {
                Text      = "View:",
                AutoSize  = true,
                Font      = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(80, 80, 80)
            };

            var cbView = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width         = 90,
                Font          = new Font("Segoe UI", 9f),
                Cursor        = Cursors.Hand
            };
            cbView.Items.AddRange(new object[] { "Braille & Print", "Braille", "Print" });
            cbView.SelectedIndex = 0;
            cbView.SelectedIndexChanged += (s, e) =>
                ViewModeChanged?.Invoke(cbView.SelectedItem?.ToString() ?? "Both");

            // Right-side container — stays anchored to the right edge
            var right = new Panel
            {
                Width  = 150,
                Height = 40,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            right.Controls.Add(lblView);
            right.Controls.Add(cbView);

            Controls.Add(right);

            // Re-center tool-button group; keep right panel near the right edge
            Resize += (s, e) =>
            {
                group.Location = new Point(
                    (Width  - group.Width)  / 2,
                    (Height - group.Height) / 2);

                right.Location = new Point(
                    Width - right.Width - 8,
                    (Height - right.Height) / 2);

                // Lay out label and combobox vertically centred inside the right panel
                lblView.Location = new Point(0,  (right.Height - lblView.Height) / 2 + 1);
                cbView.Location  = new Point(lblView.Right + 4,
                                             (right.Height - cbView.Height) / 2);
            };
        }

        private static Button MakeButton(string text, string tip)
        {
            var btn = new Button
            {
                Text      = text,
                Width     = 40,
                Height    = 40,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 12f, FontStyle.Bold),
                BackColor = Color.White,
                Cursor    = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            new ToolTip().SetToolTip(btn, tip);
            return btn;
        }
    }
}
