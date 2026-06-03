using System;
using System.Drawing;
using System.Windows.Forms;

namespace BrailleUrdu
{
    public class DocumentSetupDialog : Form
    {
        private readonly CanvasPanel _canvas;
        private readonly PagesPanel  _pages;

        private ComboBox    _cbPreset;
        private ComboBox    _cbOrientation;

        // Display labels (non-custom modes)
        private Label _lblWValue, _lblHValue;

        // Input boxes (Custom mode only)
        private TextBox _tbCustomW, _tbCustomH;

        private static readonly float[] SizeA4       = { 210f,   297f   };
        private static readonly float[] SizeStandard = { 292.1f, 279.4f };

        public DocumentSetupDialog(CanvasPanel canvas, PagesPanel pages)
        {
            _canvas = canvas;
            _pages  = pages;
            BuildUI();
            InitFromCurrent();
        }

        private void BuildUI()
        {
            Text            = "Document Setup";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition   = FormStartPosition.CenterParent;
            ClientSize      = new Size(460, 310);
            MaximizeBox     = false;
            MinimizeBox     = false;
            ShowInTaskbar   = false;
            BackColor       = Color.FromArgb(242, 242, 242);

            // ── Page Size GroupBox ────────────────────────────────────────────
            var grpPage = new GroupBox
            {
                Text      = "Page Size",
                Location  = new Point(16, 16),
                Size      = new Size(428, 220),
                Font      = new Font("Segoe UI", 9f),
                BackColor = Color.FromArgb(242, 242, 242)
            };

            // Preset dropdown
            _cbPreset = new ComboBox
            {
                Location      = new Point(12, 28),
                Size          = new Size(400, 24),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = new Font("Segoe UI", 9.5f)
            };
            _cbPreset.Items.Add("A4  (210 × 297 mm)");
            _cbPreset.Items.Add("Standard  (292 × 279 mm)");
            _cbPreset.Items.Add("Custom");
            _cbPreset.SelectedIndexChanged += OnPresetChanged;

            // ── Width row ─────────────────────────────────────────────────────
            var lblW = new Label
            {
                Text      = "Width",
                Location  = new Point(12, 76),
                Size      = new Size(54, 22),
                Font      = new Font("Segoe UI", 9f),
                TextAlign = ContentAlignment.MiddleLeft
            };

            // Value label (shown for presets)
            _lblWValue = new Label
            {
                Location    = new Point(68, 73),
                Size        = new Size(78, 22),
                Font        = new Font("Segoe UI", 9.5f),
                TextAlign   = ContentAlignment.MiddleLeft,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor   = Color.White
            };

            // Input textbox (shown for Custom)
            _tbCustomW = new TextBox
            {
                Location = new Point(68, 73),
                Size     = new Size(78, 22),
                Font     = new Font("Segoe UI", 9.5f),
                Visible  = false
            };

            var lblWmm = new Label
            {
                Text      = "mm",
                Location  = new Point(150, 76),
                AutoSize  = true,
                Font      = new Font("Segoe UI", 9f),
                TextAlign = ContentAlignment.MiddleLeft
            };

            // ── Height row ────────────────────────────────────────────────────
            var lblH = new Label
            {
                Text      = "Height",
                Location  = new Point(12, 114),
                Size      = new Size(54, 22),
                Font      = new Font("Segoe UI", 9f),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _lblHValue = new Label
            {
                Location    = new Point(68, 111),
                Size        = new Size(78, 22),
                Font        = new Font("Segoe UI", 9.5f),
                TextAlign   = ContentAlignment.MiddleLeft,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor   = Color.White
            };

            _tbCustomH = new TextBox
            {
                Location = new Point(68, 111),
                Size     = new Size(78, 22),
                Font     = new Font("Segoe UI", 9.5f),
                Visible  = false
            };

            var lblHmm = new Label
            {
                Text      = "mm",
                Location  = new Point(150, 114),
                AutoSize  = true,
                Font      = new Font("Segoe UI", 9f),
                TextAlign = ContentAlignment.MiddleLeft
            };

            // ── Orientation (right column, label + dropdown on same row) ────────
            var lblOri = new Label
            {
                Text      = "Orientation",
                Location  = new Point(230, 79),
                AutoSize  = true,
                Font      = new Font("Segoe UI", 9f),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _cbOrientation = new ComboBox
            {
                Location      = new Point(318, 73),
                Size          = new Size(98, 24),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = new Font("Segoe UI", 9.5f)
            };
            _cbOrientation.Items.Add("Portrait");
            _cbOrientation.Items.Add("Landscape");
            _cbOrientation.SelectedIndex = 0;
            _cbOrientation.SelectedIndexChanged += (s, e) => UpdateDisplay();

            // Custom hint
            var lblHint = new Label
            {
                Text      = "Enter width and height in millimetres.",
                Location  = new Point(12, 158),
                Size      = new Size(400, 18),
                Font      = new Font("Segoe UI", 8f, FontStyle.Italic),
                ForeColor = Color.Gray,
                Visible   = false,
                Name      = "lblHint"
            };

            grpPage.Controls.AddRange(new Control[] {
                _cbPreset,
                lblW,  _lblWValue, _tbCustomW, lblWmm,
                lblH,  _lblHValue, _tbCustomH, lblHmm,
                lblOri, _cbOrientation,
                lblHint
            });

            // ── Buttons ───────────────────────────────────────────────────────
            var btnCancel = new Button
            {
                Text         = "Cancel",
                Location     = new Point(254, 262),
                Size         = new Size(90, 28),
                FlatStyle    = FlatStyle.System,
                DialogResult = DialogResult.Cancel
            };
            var btnOK = new Button
            {
                Text      = "OK",
                Location  = new Point(354, 262),
                Size      = new Size(90, 28),
                FlatStyle = FlatStyle.System
            };
            btnOK.Click += OnOKClick;

            CancelButton = btnCancel;
            Controls.AddRange(new Control[] { grpPage, btnCancel, btnOK });

            // Set initial preset after all controls exist
            _cbPreset.SelectedIndex = 0;
        }

        // ── Preset changed ────────────────────────────────────────────────────

        private void OnPresetChanged(object sender, EventArgs e)
        {
            bool isCustom = _cbPreset.SelectedIndex == 2;

            _lblWValue.Visible = !isCustom;
            _lblHValue.Visible = !isCustom;
            _tbCustomW.Visible = isCustom;
            _tbCustomH.Visible = isCustom;

            _cbOrientation.Enabled = !isCustom;

            foreach (Control c in _cbPreset.Parent.Controls)
                if (c.Name == "lblHint") c.Visible = isCustom;

            UpdateDisplay();
        }

        // ── Init from current document ────────────────────────────────────────

        private void InitFromCurrent()
        {
            float w = DocumentPage.WIDTH_MM;
            float h = DocumentPage.HEIGHT_MM;
            float small = Math.Min(w, h), large = Math.Max(w, h);

            bool matchA4  = Math.Abs(small - 210f)   < 5f && Math.Abs(large - 297f)   < 5f;
            bool matchStd = Math.Abs(small - 279.4f) < 5f && Math.Abs(large - 292.1f) < 5f;

            if (matchA4)
            {
                _cbPreset.SelectedIndex      = 0;
                _cbOrientation.SelectedIndex = w > h ? 1 : 0;
            }
            else if (matchStd)
            {
                _cbPreset.SelectedIndex      = 1;
                _cbOrientation.SelectedIndex = w > h ? 1 : 0;
            }
            else
            {
                _cbPreset.SelectedIndex = 2;
                _tbCustomW.Text = w.ToString("F1");
                _tbCustomH.Text = h.ToString("F1");
            }

            UpdateDisplay();
        }

        // ── Refresh displayed dimensions ──────────────────────────────────────

        private void UpdateDisplay()
        {
            if (_cbPreset == null || _cbPreset.SelectedIndex == 2) return;

            float[] sz        = _cbPreset.SelectedIndex == 0 ? SizeA4 : SizeStandard;
            bool    landscape = _cbOrientation.SelectedIndex == 1;
            float   wMm       = landscape ? sz[1] : sz[0];
            float   hMm       = landscape ? sz[0] : sz[1];

            _lblWValue.Text = wMm.ToString("F1");
            _lblHValue.Text = hMm.ToString("F1");
        }

        // ── OK ────────────────────────────────────────────────────────────────

        private void OnOKClick(object sender, EventArgs e)
        {
            float wMm, hMm;

            if (_cbPreset.SelectedIndex == 2)
            {
                if (!float.TryParse(_tbCustomW.Text, out wMm) || wMm < 50f ||
                    !float.TryParse(_tbCustomH.Text, out hMm) || hMm < 50f)
                {
                    MessageBox.Show("Please enter valid width and height (minimum 50 mm).",
                        "Invalid Dimensions", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            else
            {
                float[] sz        = _cbPreset.SelectedIndex == 0 ? SizeA4 : SizeStandard;
                bool    landscape = _cbOrientation.SelectedIndex == 1;
                wMm = landscape ? sz[1] : sz[0];
                hMm = landscape ? sz[0] : sz[1];
            }

            DocumentPage.WIDTH_MM  = wMm;
            DocumentPage.HEIGHT_MM = hMm;

            _canvas.PageChanged();
            _pages.RebuildThumbnails();

            DialogResult = DialogResult.OK;
            Close();
        }

    }
}
