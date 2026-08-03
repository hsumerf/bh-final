using System;
using System.Drawing;
using System.Windows.Forms;

namespace BrailleUrdu
{
    // Small modal popup that lets the user pick a language before opening the Dictionary.
    public class LanguageSelectorDialog : Form
    {
        public string SelectedLangCode { get; private set; } = "en";

        private readonly RadioButton[] _radios;

        public LanguageSelectorDialog()
        {
            Text            = "Select Language";
            ClientSize      = new Size(280, 210);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition   = FormStartPosition.CenterParent;
            MaximizeBox     = false;
            MinimizeBox     = false;
            ShowInTaskbar   = false;
            Font            = new Font("Segoe UI", 9.5f);

            var codes = LanguageInfo.Codes;   // { "en", "ur", "ar", "si" }
            _radios   = new RadioButton[codes.Length];

            var label = new Label
            {
                Text      = "Select a language to edit:",
                Location  = new Point(20, 18),
                AutoSize  = true,
                Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };
            Controls.Add(label);

            for (int i = 0; i < codes.Length; i++)
            {
                _radios[i] = new RadioButton
                {
                    Text     = LanguageInfo.DisplayName(codes[i]),
                    Tag      = codes[i],
                    Location = new Point(36, 48 + i * 28),
                    AutoSize = true,
                    Checked  = (i == 0)
                };
                Controls.Add(_radios[i]);
            }

            var btnOk = new Button
            {
                Text         = "OK",
                DialogResult = DialogResult.OK,
                Size         = new Size(80, 28),
                Location     = new Point(ClientSize.Width - 180, ClientSize.Height - 44)
            };
            btnOk.Click += OnOkClick;

            var btnCancel = new Button
            {
                Text         = "Cancel",
                DialogResult = DialogResult.Cancel,
                Size         = new Size(80, 28),
                Location     = new Point(ClientSize.Width - 92, ClientSize.Height - 44)
            };

            Controls.AddRange(new Control[] { btnOk, btnCancel });
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        private void OnOkClick(object sender, EventArgs e)
        {
            var codes = LanguageInfo.Codes;
            for (int i = 0; i < _radios.Length; i++)
                if (_radios[i].Checked) { SelectedLangCode = codes[i]; break; }
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
