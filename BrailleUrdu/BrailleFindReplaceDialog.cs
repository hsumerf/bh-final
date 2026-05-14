using System;
using System.Drawing;
using System.Windows.Forms;

namespace BrailleUrdu
{
    public class BrailleFindReplaceDialog : Form
    {
        private readonly CanvasPanel _canvas;
        private readonly bool        _isReplace;

        private TextBox _tbFind;
        private TextBox _tbReplace;
        private Label   _lblStatus;

        public BrailleFindReplaceDialog(CanvasPanel canvas, bool isReplace)
        {
            _canvas    = canvas;
            _isReplace = isReplace;
            BuildUI();
        }

        private void BuildUI()
        {
            Text            = _isReplace ? "Find & Replace — Braille" : "Find — Braille";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            StartPosition   = FormStartPosition.CenterParent;
            Width           = 400;
            Height          = _isReplace ? 240 : 185;
            Font            = new Font("Segoe UI", 9f);
            BackColor       = Color.FromArgb(245, 245, 245);

            int y = 18;

            // ── Find row ─────────────────────────────────────────────────────
            Controls.Add(new Label { Text = "Find:", Location = new Point(16, y + 3), AutoSize = true });
            _tbFind = new TextBox
            {
                Location = new Point(80, y),
                Size     = new Size(290, 22),
                Font     = new Font("Segoe UI", 9.5f)
            };
            _tbFind.KeyDown += (s, e) => { if (e.KeyCode == Keys.Return) DoFind(); };
            Controls.Add(_tbFind);
            y += 40;

            // ── Replace row (only in replace mode) ───────────────────────────
            if (_isReplace)
            {
                Controls.Add(new Label { Text = "Replace:", Location = new Point(16, y + 3), AutoSize = true });
                _tbReplace = new TextBox
                {
                    Location = new Point(80, y),
                    Size     = new Size(290, 22),
                    Font     = new Font("Segoe UI", 9.5f)
                };
                Controls.Add(_tbReplace);
                y += 40;
            }

            // ── Buttons ───────────────────────────────────────────────────────
            var btnFind = new Button
            {
                Text     = _isReplace ? "Find All" : "Find All",
                Location = new Point(16, y),
                Size     = new Size(100, 28),
                FlatStyle = FlatStyle.System
            };
            btnFind.Click += (s, e) => DoFind();
            Controls.Add(btnFind);

            if (_isReplace)
            {
                var btnReplace = new Button
                {
                    Text      = "Replace All",
                    Location  = new Point(126, y),
                    Size      = new Size(100, 28),
                    FlatStyle = FlatStyle.System
                };
                btnReplace.Click += (s, e) => DoReplaceAll();
                Controls.Add(btnReplace);
            }

            var btnClose = new Button
            {
                Text      = "Close",
                Location  = new Point(270, y),
                Size      = new Size(100, 28),
                FlatStyle = FlatStyle.System
            };
            btnClose.Click += (s, e) => Close();
            Controls.Add(btnClose);
            y += 40;

            // ── Status label ─────────────────────────────────────────────────
            _lblStatus = new Label
            {
                Text      = "",
                Location  = new Point(16, y),
                Size      = new Size(360, 20),
                ForeColor = Color.FromArgb(80, 80, 80),
                Font      = new Font("Segoe UI", 8.5f)
            };
            Controls.Add(_lblStatus);

            AcceptButton = btnFind;
            CancelButton = btnClose;
        }

        // Converts typed text to braille unicode via BrailleMapper.
        // Braille characters (U+2800–U+28FF) are passed through unchanged.
        private static string ConvertToBraille(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            var sb = new System.Text.StringBuilder();
            foreach (char c in input)
            {
                if (c >= '⠀' && c <= '⣿') { sb.Append(c); continue; }
                string b = BrailleMapper.ToBraille(c);
                if (b.Length > 0) sb.Append(b);
            }
            return sb.ToString();
        }

        private void DoFind()
        {
            foreach (var box in _canvas.GetAllBrailleBoxes())
                box.ClearSearchHighlight();

            string pattern = ConvertToBraille(_tbFind.Text);
            if (string.IsNullOrEmpty(pattern))
            {
                _lblStatus.Text = "Enter text to find.";
                return;
            }

            int count = 0;
            foreach (var box in _canvas.GetAllBrailleBoxes())
            {
                if (box.BrailleText.IndexOf(pattern, StringComparison.Ordinal) >= 0)
                {
                    box.SetSearchHighlight(pattern);
                    count++;
                }
            }
            _lblStatus.Text = count == 0
                ? "Not found."
                : string.Format("Highlighted in {0} box(es).", count);
        }

        private void DoReplaceAll()
        {
            string pattern     = ConvertToBraille(_tbFind.Text);
            string replacement = ConvertToBraille(_tbReplace?.Text ?? "");
            if (string.IsNullOrEmpty(pattern))
            {
                _lblStatus.Text = "Enter text to find.";
                return;
            }

            int count = 0;
            foreach (var box in _canvas.GetAllBrailleBoxes())
            {
                if (box.BrailleText.IndexOf(pattern, StringComparison.Ordinal) >= 0)
                {
                    box.BrailleText = box.BrailleText.Replace(pattern, replacement);
                    box.ClearSearchHighlight();
                    count++;
                }
            }
            _lblStatus.Text = count == 0
                ? "Not found."
                : string.Format("Replaced in {0} box(es).", count);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            foreach (var box in _canvas.GetAllBrailleBoxes())
                box.ClearSearchHighlight();
            base.OnFormClosing(e);
        }
    }
}
