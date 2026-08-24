using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace BrailleUrdu
{
    // Two-panel translator: text on the left, braille on the right.
    // Typing in either panel syncs the other in real time.
    public class TranslatorDialog : Form
    {
        private readonly LanguageSpec _spec;
        private readonly RichTextBox  _textBox;
        private readonly RichTextBox  _brailleBox;
        private readonly Font         _brailleFont;
        private bool _updating;

        public TranslatorDialog(string langCode)
        {
            _spec        = LanguageSpec.Load(langCode);
            _brailleFont = new Font("SimBraille", 13f);

            Text            = "Translator — " + LanguageInfo.DisplayName(langCode);
            ClientSize      = new Size(820, 520);
            MinimumSize     = new Size(620, 400);
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition   = FormStartPosition.CenterParent;
            MaximizeBox     = true;
            MinimizeBox     = true;
            ShowInTaskbar   = true;
            Font            = new Font("Segoe UI", 9f);

            var lblText = new Label
            {
                Text     = LanguageInfo.DisplayName(langCode),
                Font     = new Font("Segoe UI", 9f, FontStyle.Bold),
                Location = new Point(16, 12),
                AutoSize = true
            };
            var lblBraille = new Label
            {
                Text     = "Braille",
                Font     = new Font("Segoe UI", 9f, FontStyle.Bold),
                AutoSize = true
            };

            _textBox = new RichTextBox
            {
                Multiline     = true,
                ScrollBars    = RichTextBoxScrollBars.Vertical,
                Font        = new Font(LanguageInfo.FontFor(langCode), 12f),
                RightToLeft = LanguageInfo.RtlFor(langCode) ? RightToLeft.Yes : RightToLeft.No,
                Anchor      = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            _brailleBox = new RichTextBox
            {
                Multiline     = true,
                ScrollBars    = RichTextBoxScrollBars.Vertical,
                Font   = _brailleFont,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right
            };

            // Vertical divider
            var divider = new Panel
            {
                BackColor = SystemColors.ControlDark,
                Anchor    = AnchorStyles.Top | AnchorStyles.Bottom
            };

            _textBox.KeyPress       += OnTextKeyPress;
            _textBox.TextChanged    += OnTextBoxChanged;
            _brailleBox.KeyPress    += OnBrailleKeyPress;
            _brailleBox.TextChanged += OnBrailleBoxChanged;

            var btnClear = new Button
            {
                Text   = "Clear",
                Size   = new Size(80, 28),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            btnClear.Click += (s, e) =>
            {
                _updating = true;
                try { _textBox.Clear(); _brailleBox.Clear(); }
                finally { _updating = false; }
            };

            var btnClose = new Button
            {
                Text         = "Close",
                Size         = new Size(80, 28),
                DialogResult = DialogResult.Cancel,
                Anchor       = AnchorStyles.Bottom | AnchorStyles.Right
            };

            Controls.AddRange(new Control[] { lblText, lblBraille, divider, _textBox, _brailleBox, btnClear, btnClose });
            CancelButton = btnClose;

            Resize += (s, e) => DoLayout(lblBraille, divider, btnClear, btnClose);
            DoLayout(lblBraille, divider, btnClear, btnClose);
        }

        private void DoLayout(Label lblBraille, Panel divider, Button btnClear, Button btnClose)
        {
            int mid    = ClientSize.Width / 2;
            int top    = 36;
            int bottom = ClientSize.Height - 50;
            int boxH   = Math.Max(bottom - top, 50);

            _textBox.SetBounds(16, top, mid - 24, boxH);
            divider.SetBounds(mid - 4, top, 2, boxH);
            _brailleBox.SetBounds(mid + 4, top, ClientSize.Width - mid - 20, boxH);

            lblBraille.Location = new Point(mid + 4, 12);
            btnClear.Location   = new Point(16, ClientSize.Height - 42);
            btnClose.Location   = new Point(ClientSize.Width - 96, ClientSize.Height - 42);
        }

        // ── Text box key intercept (English keyboard → language characters) ──

        private void OnTextKeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar < ' ') return;  // pass Backspace, Enter, Ctrl-chars through
            if (e.KeyChar >= '0' && e.KeyChar <= '9')
            {
                // Map to the language's own digit character (Arabic-Indic for RTL langs)
                _textBox.SelectedText = DigitForLang(e.KeyChar);
                e.Handled = true;
                return;
            }
            string mapped = _spec.TypedKeyFor(e.KeyChar);
            if (mapped.Length > 0)
            {
                _textBox.SelectedText = mapped;
                e.Handled = true;
            }
            // no mapping found → let the keystroke through unchanged
        }

        // Digits 1-9 use shorthands a-i; 0 uses j — consistent across Urdu/Arabic/Sindhi specs.
        private string DigitForLang(char ascii)
        {
            if (_spec.LangCode == "en") return ascii.ToString();
            char sh = ascii == '0' ? 'j' : (char)('a' + (ascii - '1'));
            string mapped = _spec.DigitKeyFor(sh);
            return mapped.Length > 0 ? mapped : ascii.ToString();
        }

        // ── Braille box key intercept (ASCII shorthand → braille glyphs) ─────

        private void OnBrailleKeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar < ' ') return;  // pass Backspace, Enter, Ctrl-chars through
            string br = LanguageSpec.ShorthandToBraille(e.KeyChar);
            if (br.Length > 0)
            {
                if (e.KeyChar >= '0' && e.KeyChar <= '9')
                    br = "⠼" + br;
                _brailleBox.SelectedText = br;
            }
            e.Handled = true;
        }

        // ── Bidirectional sync ────────────────────────────────────────────────

        private void OnTextBoxChanged(object sender, EventArgs e)
        {
            if (_updating || _brailleBox.Focused) return;
            _updating = true;
            try
            {
                int sel = _brailleBox.SelectionStart;
                _brailleBox.Text = ToBraille(_textBox.Text);
                _brailleBox.SelectionStart = Math.Min(sel, _brailleBox.TextLength);
            }
            finally { _updating = false; }
        }

        private void OnBrailleBoxChanged(object sender, EventArgs e)
        {
            if (_updating || _textBox.Focused) return;
            _updating = true;
            try
            {
                int sel = _textBox.SelectionStart;
                _textBox.Text = _spec.FromBraille(_brailleBox.Text);
                _textBox.SelectionStart = Math.Min(sel, _textBox.TextLength);
            }
            finally { _updating = false; }
        }

        private string ToBraille(string text)
        {
            var sb = new StringBuilder();
            foreach (char c in text)
            {
                if (c == '\r') continue;
                if (c == '\n') { sb.Append('\n'); continue; }
                string br = _spec.ToBraille(c);
                if (br.Length > 0)
                {
                    if (char.IsDigit(c)) sb.Append('⠼');
                    sb.Append(br);
                }
                else sb.Append("⠀");
            }
            return sb.ToString();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _brailleFont?.Dispose();
            base.Dispose(disposing);
        }
    }
}
