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

            if (!PromptPassword()) return;

            Application.Run(new Form1());
        }

        private static bool PromptPassword()
        {
            using (var dlg = new PasswordDialog())
                return dlg.ShowDialog() == DialogResult.OK;
        }
    }

    internal class PasswordDialog : Form
    {
        private readonly TextBox _tb;

        internal PasswordDialog()
        {
            Text            = "BH Braille Designer";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition   = FormStartPosition.CenterScreen;
            ClientSize      = new Size(320, 140);
            MaximizeBox     = false;
            MinimizeBox     = false;
            ShowInTaskbar   = false;
            BackColor       = Color.FromArgb(242, 242, 242);

            var lbl = new Label
            {
                Text      = "Enter password to continue:",
                Location  = new Point(16, 20),
                Size      = new Size(288, 20),
                Font      = new Font("Segoe UI", 9.5f)
            };

            _tb = new TextBox
            {
                Location        = new Point(16, 48),
                Size            = new Size(288, 24),
                Font            = new Font("Segoe UI", 10f),
                PasswordChar    = '●',
                UseSystemPasswordChar = false
            };

            var btnOK = new Button
            {
                Text      = "OK",
                Location  = new Point(136, 90),
                Size      = new Size(80, 28),
                FlatStyle = FlatStyle.System
            };
            var btnCancel = new Button
            {
                Text         = "Cancel",
                Location     = new Point(224, 90),
                Size         = new Size(80, 28),
                FlatStyle    = FlatStyle.System,
                DialogResult = DialogResult.Cancel
            };

            btnOK.Click += (s, e) =>
            {
                if (_tb.Text == "B01t@yHur00f")
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    MessageBox.Show("Incorrect password.", "Access Denied",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _tb.Clear();
                    _tb.Focus();
                }
            };

            AcceptButton = btnOK;
            CancelButton = btnCancel;

            Controls.AddRange(new Control[] { lbl, _tb, btnOK, btnCancel });
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _tb.Focus();
        }
    }
}
