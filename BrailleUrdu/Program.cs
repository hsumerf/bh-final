using System;
using System.Drawing;
using System.Windows.Forms;

namespace BrailleUrdu
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (!ActivationManager.IsActivated())
            {
                bool hadKey = ActivationManager.HasStoredActivation();
                bool sent   = ActivationManager.SendRequest();

                using (var dlg = new ActivationDialog(sent, hadKey))
                    if (dlg.ShowDialog() != DialogResult.OK) return;
            }

            Application.Run(new Form1());
        }
    }

    internal class ActivationDialog : Form
    {
        private readonly TextBox _tb;

        internal ActivationDialog(bool emailSent, bool keyExpiredOrInvalid)
        {
            Text            = "BH Braille Designer – Activation";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition   = FormStartPosition.CenterScreen;
            ClientSize      = new Size(420, 230);
            MaximizeBox     = false;
            MinimizeBox     = false;
            ShowInTaskbar   = false;
            BackColor       = Color.FromArgb(242, 242, 242);

            string heading = keyExpiredOrInvalid
                ? "Your activation key is no longer valid.\r\n"
                + "This happens when your 365-day license expires or a new build is installed.\r\n"
                + "A new request has been sent automatically."
                : "This device has not been activated yet.\r\n"
                + "An activation request has been sent to the developer.";

            string footer = emailSent
                ? "\r\nYou will receive your key by email. Enter it below and click Activate."
                : "\r\nThe request could not be sent automatically.\r\n"
                + "Please contact the developer to obtain your activation key.";

            var lblStatus = new Label
            {
                Text     = heading + footer,
                Location = new Point(16, 16),
                Size     = new Size(388, 90),
                Font     = new Font("Segoe UI", 9f)
            };

            var lblKey = new Label
            {
                Text     = "Activation Key:",
                Location = new Point(16, 116),
                AutoSize = true,
                Font     = new Font("Segoe UI", 9f)
            };

            _tb = new TextBox
            {
                Location        = new Point(16, 136),
                Size            = new Size(388, 26),
                Font            = new Font("Segoe UI", 10.5f),
                CharacterCasing = CharacterCasing.Upper
            };

            var btnActivate = new Button
            {
                Text      = "Activate",
                Location  = new Point(212, 184),
                Size      = new Size(96, 28),
                FlatStyle = FlatStyle.System
            };

            var btnExit = new Button
            {
                Text         = "Exit",
                Location     = new Point(316, 184),
                Size         = new Size(88, 28),
                FlatStyle    = FlatStyle.System,
                DialogResult = DialogResult.Cancel
            };

            btnActivate.Click += (s, e) =>
            {
                if (ActivationManager.TryActivate(_tb.Text))
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    MessageBox.Show(
                        "Invalid activation key. Please check and try again.",
                        "Activation Failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    _tb.SelectAll();
                    _tb.Focus();
                }
            };

            AcceptButton = btnActivate;
            CancelButton = btnExit;

            Controls.AddRange(new Control[] { lblStatus, lblKey, _tb, btnActivate, btnExit });
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _tb.Focus();
        }
    }
}
