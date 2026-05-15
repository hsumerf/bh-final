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
                string localIp = ActivationManager.GetLocalIp();
                string key     = ActivationManager.DeriveKey(localIp);
                bool   sent    = ActivationManager.SendRequest(localIp, key);

                using (var dlg = new ActivationDialog(sent))
                    if (dlg.ShowDialog() != DialogResult.OK) return;
            }

            Application.Run(new Form1());
        }
    }

    internal class ActivationDialog : Form
    {
        private readonly TextBox _tb;

        internal ActivationDialog(bool emailSent)
        {
            Text            = "BH Braille Designer – Activation";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition   = FormStartPosition.CenterScreen;
            ClientSize      = new Size(420, 210);
            MaximizeBox     = false;
            MinimizeBox     = false;
            ShowInTaskbar   = false;
            BackColor       = Color.FromArgb(242, 242, 242);

            string statusMsg = emailSent
                ? "An activation request has been sent.\r\n"
                + "You will receive your activation key by email.\r\n"
                + "Once you have it, enter it below and click Activate."
                : "Could not send activation request automatically.\r\n"
                + "Please contact the developer to obtain your activation key,\r\n"
                + "then enter it below and click Activate.";

            var lblStatus = new Label
            {
                Text      = statusMsg,
                Location  = new Point(16, 16),
                Size      = new Size(388, 66),
                Font      = new Font("Segoe UI", 9f)
            };

            var lblKey = new Label
            {
                Text      = "Activation Key:",
                Location  = new Point(16, 96),
                AutoSize  = true,
                Font      = new Font("Segoe UI", 9f)
            };

            _tb = new TextBox
            {
                Location        = new Point(16, 116),
                Size            = new Size(388, 26),
                Font            = new Font("Segoe UI", 10.5f),
                CharacterCasing = CharacterCasing.Upper
            };

            var btnActivate = new Button
            {
                Text      = "Activate",
                Location  = new Point(212, 162),
                Size      = new Size(96, 28),
                FlatStyle = FlatStyle.System
            };

            var btnExit = new Button
            {
                Text         = "Exit",
                Location     = new Point(316, 162),
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
