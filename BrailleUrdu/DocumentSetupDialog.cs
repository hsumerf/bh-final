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
        private TextBox     _tbWidth;
        private TextBox     _tbHeight;
        private ComboBox    _cbOrientation;
        private RadioButton _rbEn, _rbUr, _rbAr, _rbSi;

        // [widthMm, heightMm] in portrait orientation
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
            ClientSize      = new Size(450, 450);
            MaximizeBox     = false;
            MinimizeBox     = false;
            ShowInTaskbar   = false;
            BackColor       = Color.FromArgb(242, 242, 242);

            // ── Page Size GroupBox ────────────────────────────────────────────
            var grpPage = new GroupBox
            {
                Text      = "Page Size",
                Location  = new Point(16, 16),
                Size      = new Size(416, 220),
                Font      = new Font("Segoe UI", 9f),
                BackColor = Color.FromArgb(242, 242, 242)
            };

            _cbPreset = new ComboBox
            {
                Location      = new Point(12, 30),
                Size          = new Size(386, 24),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = new Font("Segoe UI", 9.5f)
            };
            _cbPreset.Items.Add("A4 (8.3 x 11.7)");
            _cbPreset.Items.Add("Standard (11.5 x 11)");
            _cbPreset.SelectedIndex = 0;
            _cbPreset.SelectedIndexChanged += (s, e) => UpdateDisplay();

            var lblWidth = MakeLabel("Width",  new Point(12, 76));
            _tbWidth = new TextBox
            {
                Location  = new Point(70, 73),
                Size      = new Size(100, 22),
                Font      = new Font("Segoe UI", 9.5f),
                ReadOnly  = true,
                BackColor = Color.White,
                TabStop   = false
            };

            var lblHeight = MakeLabel("Height", new Point(12, 114));
            _tbHeight = new TextBox
            {
                Location  = new Point(70, 111),
                Size      = new Size(100, 22),
                Font      = new Font("Segoe UI", 9.5f),
                ReadOnly  = true,
                BackColor = Color.White,
                TabStop   = false
            };

            var lblOrientation = MakeLabel("Orientation", new Point(226, 76));
            _cbOrientation = new ComboBox
            {
                Location      = new Point(226, 111),
                Size          = new Size(170, 24),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = new Font("Segoe UI", 9.5f)
            };
            _cbOrientation.Items.Add("Portrait");
            _cbOrientation.Items.Add("Landscape");
            _cbOrientation.SelectedIndex = 0;
            _cbOrientation.SelectedIndexChanged += (s, e) => UpdateDisplay();

            grpPage.Controls.AddRange(new Control[] {
                _cbPreset,
                lblWidth,  _tbWidth,
                lblHeight, _tbHeight,
                lblOrientation, _cbOrientation
            });

            // ── Language GroupBox ─────────────────────────────────────────────
            var grpLang = new GroupBox
            {
                Text      = "Language",
                Location  = new Point(16, 248),
                Size      = new Size(416, 130),
                Font      = new Font("Segoe UI", 9f),
                BackColor = Color.FromArgb(242, 242, 242)
            };

            _rbEn = MakeRadio("English", new Point(12, 28));
            _rbUr = MakeRadio("Urdu",    new Point(12, 56));
            _rbAr = MakeRadio("Arabic",  new Point(220, 28));
            _rbSi = MakeRadio("Sindhi",  new Point(220, 56));

            // Show font name as hint
            AddFontHint(grpLang, LanguageInfo.FontFor("en"), new Point(90, 31));
            AddFontHint(grpLang, LanguageInfo.FontFor("ur"), new Point(90, 59));
            AddFontHint(grpLang, LanguageInfo.FontFor("ar"), new Point(298, 31));
            AddFontHint(grpLang, LanguageInfo.FontFor("si"), new Point(298, 59));

            grpLang.Controls.AddRange(new Control[] { _rbEn, _rbUr, _rbAr, _rbSi });

            // ── Buttons ───────────────────────────────────────────────────────
            var btnCancel = new Button
            {
                Text         = "Cancel",
                Location     = new Point(244, 394),
                Size         = new Size(90, 28),
                FlatStyle    = FlatStyle.System,
                DialogResult = DialogResult.Cancel
            };
            var btnOK = new Button
            {
                Text      = "OK",
                Location  = new Point(344, 394),
                Size      = new Size(90, 28),
                FlatStyle = FlatStyle.System
            };
            btnOK.Click += OnOKClick;

            CancelButton = btnCancel;
            Controls.AddRange(new Control[] { grpPage, grpLang, btnCancel, btnOK });
        }

        private static Label MakeLabel(string text, Point loc) => new Label
        {
            Text      = text,
            Location  = loc,
            Size      = new Size(90, 22),
            Font      = new Font("Segoe UI", 9f),
            TextAlign = ContentAlignment.MiddleLeft
        };

        private static RadioButton MakeRadio(string text, Point loc) => new RadioButton
        {
            Text      = text,
            Location  = loc,
            AutoSize  = true,
            Font      = new Font("Segoe UI", 9.5f)
        };

        private static void AddFontHint(Control parent, string fontName, Point loc)
        {
            parent.Controls.Add(new Label
            {
                Text      = "(" + fontName + ")",
                Location  = loc,
                AutoSize  = true,
                Font      = new Font("Segoe UI", 7.5f),
                ForeColor = Color.Gray
            });
        }

        private void InitFromCurrent()
        {
            float w = DocumentPage.WIDTH_MM;
            float h = DocumentPage.HEIGHT_MM;

            float small = Math.Min(w, h);
            float large = Math.Max(w, h);

            bool isA4 = Math.Abs(small - 210f) < 5f && Math.Abs(large - 297f) < 5f;
            _cbPreset.SelectedIndex      = isA4 ? 0 : 1;
            _cbOrientation.SelectedIndex = w > h ? 1 : 0;

            // Pre-select current language
            switch (Document.Language)
            {
                case "ur": _rbUr.Checked = true; break;
                case "ar": _rbAr.Checked = true; break;
                case "si": _rbSi.Checked = true; break;
                default:   _rbEn.Checked = true; break;
            }

            UpdateDisplay();
        }

        private float[] PresetMm() =>
            _cbPreset.SelectedIndex == 0 ? SizeA4 : SizeStandard;

        private void UpdateDisplay()
        {
            float[] sz        = PresetMm();
            bool    landscape = _cbOrientation.SelectedIndex == 1;

            float wMm = landscape ? sz[1] : sz[0];
            float hMm = landscape ? sz[0] : sz[1];

            _tbWidth.Text  = (wMm / 25.4f).ToString("F1");
            _tbHeight.Text = (hMm / 25.4f).ToString("F1");
        }

        private string SelectedLanguageCode()
        {
            if (_rbUr.Checked) return "ur";
            if (_rbAr.Checked) return "ar";
            if (_rbSi.Checked) return "si";
            return "en";
        }

        private void OnOKClick(object sender, EventArgs e)
        {
            float[] sz        = PresetMm();
            bool    landscape = _cbOrientation.SelectedIndex == 1;

            DocumentPage.WIDTH_MM  = landscape ? sz[1] : sz[0];
            DocumentPage.HEIGHT_MM = landscape ? sz[0] : sz[1];

            Document.SetLanguage(SelectedLanguageCode());

            _canvas.PageChanged();
            _pages.RebuildThumbnails();

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
