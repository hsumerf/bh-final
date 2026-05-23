using System;
using System.Drawing;
using System.Windows.Forms;

namespace BrailleUrdu
{
    public class TextFindReplaceDialog : Form
    {
        private readonly CanvasPanel _canvas;
        private readonly bool        _isReplace;

        private TextBox _tbFind;
        private TextBox _tbReplace;
        private Label   _lblStatus;

        public TextFindReplaceDialog(CanvasPanel canvas, bool isReplace)
        {
            _canvas    = canvas;
            _isReplace = isReplace;
            BuildUI();
        }

        private void BuildUI()
        {
            Text            = _isReplace ? "Find & Replace — Text" : "Find — Text";
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
            _tbFind = MakeLanguageInputBox(new Point(80, y), new Size(290, 24));
            _tbFind.KeyDown += (s, e) => { if (e.KeyCode == Keys.Return) DoFind(); };
            Controls.Add(_tbFind);
            y += 40;

            // ── Replace row (only in replace mode) ───────────────────────────
            if (_isReplace)
            {
                Controls.Add(new Label { Text = "Replace:", Location = new Point(16, y + 3), AutoSize = true });
                _tbReplace = MakeLanguageInputBox(new Point(80, y), new Size(290, 24));
                Controls.Add(_tbReplace);
                y += 40;
            }

            // ── Buttons ───────────────────────────────────────────────────────
            var btnFind = new Button
            {
                Text      = "Find All",
                Location  = new Point(16, y),
                Size      = new Size(100, 28),
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

        // Creates a TextBox wired to the current document language:
        // correct font, RTL direction, and PrintInputMap key conversion.
        private static TextBox MakeLanguageInputBox(Point loc, Size sz)
        {
            bool   isRtl      = LanguageInfo.RtlFor(Document.Language);
            string fontFamily = LanguageInfo.FontFor(Document.Language);

            var tb = new TextBox
            {
                Location    = loc,
                Size        = sz,
                Font        = new Font(fontFamily, 12f),
                RightToLeft = isRtl ? RightToLeft.Yes : RightToLeft.No,
                TextAlign   = isRtl ? HorizontalAlignment.Right : HorizontalAlignment.Left
            };

            // pendingRef[0] holds the multi-char accumulation buffer for PrintInputMap
            var pendingRef = new[] { "" };

            tb.KeyPress += (s, e) =>
            {
                if (e.KeyChar < ' ') return; // let Backspace, Enter, Ctrl+keys through
                string output = Document.PrintMap.Convert(ref pendingRef[0], e.KeyChar);
                if (output == null) { e.Handled = true; return; } // still accumulating

                int sel = tb.SelectionStart;
                int len = tb.SelectionLength;
                tb.Text           = tb.Text.Remove(sel, len).Insert(sel, output);
                tb.SelectionStart = sel + output.Length;
                e.Handled = true;
            };

            tb.LostFocus += (s, e) =>
            {
                if (string.IsNullOrEmpty(pendingRef[0])) return;
                string flushed = Document.PrintMap.Flush(ref pendingRef[0]);
                if (flushed.Length == 0) return;
                int sel = tb.SelectionStart;
                tb.Text           = tb.Text.Insert(sel, flushed);
                tb.SelectionStart = sel + flushed.Length;
            };

            return tb;
        }

        private void DoFind()
        {
            foreach (var box in _canvas.GetAllPrintBoxes())
                box.ClearSearchHighlight();

            string pattern = _tbFind.Text;
            if (string.IsNullOrEmpty(pattern))
            {
                _lblStatus.Text = "Enter text to find.";
                return;
            }

            int count = 0;
            foreach (var box in _canvas.GetAllPrintBoxes())
            {
                if (box.DisplayText.IndexOf(pattern, StringComparison.Ordinal) >= 0)
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
            string pattern     = _tbFind.Text;
            string replacement = _tbReplace?.Text ?? "";
            if (string.IsNullOrEmpty(pattern))
            {
                _lblStatus.Text = "Enter text to find.";
                return;
            }

            int count = 0;
            foreach (var box in _canvas.GetAllPrintBoxes())
            {
                if (box.DisplayText.IndexOf(pattern, StringComparison.Ordinal) >= 0)
                {
                    box.DisplayText = box.DisplayText.Replace(pattern, replacement);
                    box.ClearSearchHighlight();
                    count++;
                }
            }
            _lblStatus.Text = count == 0
                ? "Not found."
                : string.Format("Replaced in {0} box(es).", count);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _tbFind.Focus();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            foreach (var box in _canvas.GetAllPrintBoxes())
                box.ClearSearchHighlight();
            base.OnFormClosing(e);
        }
    }
}
