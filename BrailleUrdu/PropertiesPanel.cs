using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace BrailleUrdu
{
    public class PropertiesPanel : Panel
    {
        private const int PANEL_W      = 260;
        private const int COLLAPSED_W  = 26;
        private const int PAD_L   = 14;   // left/right padding
        private const int INNER_W = PANEL_W - PAD_L * 2;  // usable width

        private static readonly Color BG        = Color.FromArgb(242, 242, 242);
        private static readonly Color HDR_COLOR = Color.FromArgb(70,  70,  70);
        private static readonly Color LBL_COLOR = Color.FromArgb(130, 130, 130);
        private static readonly Color LINE_COLOR = Color.FromArgb(200, 200, 200);
        private static readonly Color INPUT_BG   = Color.FromArgb(255, 255, 255);

        private Panel  _mainContent;
        private bool   _panelCollapsed;

        private readonly CanvasPanel _canvas;
        private Control        _target;
        private PrintTextBox   _printTarget;
        private BrailleTextBox _brailleTarget;
        private LineBox        _lineTarget;
        private TableBox       _tableTarget;
        private TactileBox     _tactileTarget;
        private bool           _updating;

        // W / H transform row — hidden for TactileBox
        private Label _lblW, _lblH;
        private Panel _wrapW, _wrapH;

        // ── Transform fields ──────────────────────────────────────────────────
        private TextBox _tbX, _tbY, _tbW, _tbH;

        // ── Style panel & its controls ────────────────────────────────────────
        private Panel    _stylePanel;
        private Panel    _braillePanel;
        private Panel    _linePanel;
        private Panel    _tablePanel;
        private Button   _btnTrim;
        private ComboBox _cbLineDir;
        private Button   _btnLineColor;
        private TextBox  _tbLineWidth;
        private TextBox  _tbTableRows;
        private TextBox  _tbTableCols;
        private ComboBox _cbFont, _cbStyle;
        private TextBox  _tbSize, _tbBorderWidth;
        private Button   _btnTextColor, _btnBorderColor, _btnFillColor;
        private Button[] _btnHAlign = new Button[3];
        private Button[] _btnVAlign = new Button[3];
        private CheckBox _chkBorderTop, _chkBorderBottom, _chkBorderLeft, _chkBorderRight;
        private CheckBox _chkTransparent;

        public PropertiesPanel(CanvasPanel canvas)
        {
            _canvas   = canvas;
            Width     = PANEL_W;
            Dock      = DockStyle.Right;
            BackColor = BG;
            Build();
            canvas.SelectionChanged += OnSelectionChanged;
        }

        // ── Build ─────────────────────────────────────────────────────────────

        private void Build()
        {
            // ── Global align buttons (top strip) ──────────────────────────────
            var alignRow = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 48,
                BackColor = Color.FromArgb(232, 232, 232)
            };
            alignRow.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(LINE_COLOR), 0, alignRow.Height - 1, alignRow.Width, alignRow.Height - 1);

            string[] tips  = { "Align Left", "Center Horizontally", "Align Right",
                                "Align Top",  "Center Vertically",   "Align Bottom" };
            string[] modes = { "left", "centerH", "right", "top", "centerV", "bottom" };
            for (int i = 0; i < 6; i++)
            {
                int   idx    = i;
                int   gx     = PAD_L + i * 30 + (i >= 3 ? 8 : 0);
                var   btn    = FlatBtn(new Point(gx, 10), new Size(26, 26));
                var   mode   = modes[i];
                btn.Click   += (s, e) => AlignSelected(mode);
                btn.Paint   += (s, e) => DrawAlignIcon(e.Graphics, idx, 26, 26);
                new ToolTip().SetToolTip(btn, tips[i]);
                alignRow.Controls.Add(btn);
            }

            // ── Transform section ─────────────────────────────────────────────
            var transformArea = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 106,
                BackColor = Color.Transparent,
                Padding   = new Padding(0, 6, 0, 0)
            };

            _tbX = NumBox(); _tbY = NumBox(); _tbW = NumBox(); _tbH = NumBox();
            foreach (var tb in new[] { _tbX, _tbY, _tbW, _tbH })
            {
                tb.KeyDown   += (s, e) => { if (e.KeyCode == Keys.Return) ApplyTransform(); };
                tb.LostFocus += (s, e) => ApplyTransform();
            }

            int col2 = PAD_L + INNER_W / 2 + 4;
            int fw   = INNER_W / 2 - 20;

            _lblW  = MkLabel("W", new Point(PAD_L, 68), false);
            _wrapW = InputWrap(_tbW, new Point(PAD_L + 16, 64), fw);
            _lblH  = MkLabel("H", new Point(col2, 68), false);
            _wrapH = InputWrap(_tbH, new Point(col2 + 16, 64), fw);

            transformArea.Controls.AddRange(new Control[]
            {
                MkLabel("Transform", new Point(PAD_L, 12), true),
                MkLabel("X", new Point(PAD_L, 40), false),
                InputWrap(_tbX, new Point(PAD_L + 16, 36), fw),
                MkLabel("Y", new Point(col2, 40), false),
                InputWrap(_tbY, new Point(col2 + 16, 36), fw),
                _lblW, _wrapW, _lblH, _wrapH
            });

            // ── Style panel (PrintTextBox only) ───────────────────────────────
            _stylePanel = new Panel
            {
                Dock       = DockStyle.Fill,
                AutoScroll = true,
                BackColor  = Color.Transparent,
                Visible    = false
            };
            BuildStylePanel();

            // ── Braille translation panel (BrailleTextBox only) ───────────────
            _braillePanel = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.Transparent,
                Visible   = false
            };
            BuildBraillePanel();

            // ── Line properties panel (LineBox only) ──────────────────────────
            _linePanel = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.Transparent,
                Visible   = false
            };
            BuildLinePanel();

            // ── Table properties panel (TableBox only) ────────────────────────
            _tablePanel = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.Transparent,
                Visible   = false
            };
            BuildTablePanel();

            // Shared Fill container — only one child is visible at a time
            var contentArea = new Panel { Dock = DockStyle.Fill };
            contentArea.Controls.Add(_stylePanel);
            contentArea.Controls.Add(_braillePanel);
            contentArea.Controls.Add(_linePanel);
            contentArea.Controls.Add(_tablePanel);

            _mainContent = new Panel { Dock = DockStyle.Fill };
            _mainContent.Controls.Add(contentArea);
            _mainContent.Controls.Add(transformArea);
            _mainContent.Controls.Add(alignRow);

            Controls.Add(_mainContent);
        }

        public bool IsCollapsed => _panelCollapsed;

        public void ToggleCollapse()
        {
            _panelCollapsed      = !_panelCollapsed;
            _mainContent.Visible = !_panelCollapsed;
            Width                = _panelCollapsed ? COLLAPSED_W : PANEL_W;
        }

        private void BuildStylePanel()
        {
            int y = 12;

            // ── Font and Style ────────────────────────────────────────────────
            _stylePanel.Controls.Add(Rule(y)); y += 24;

            var lnkSave = new LinkLabel
            {
                Text      = "Save as default",
                Location  = new Point(PANEL_W - PAD_L - 90, y + 1),
                Size      = new Size(90, 16),
                Font      = new Font("Segoe UI", 7.5f),
                LinkColor = Color.DodgerBlue,
                TextAlign = ContentAlignment.MiddleRight
            };
            lnkSave.LinkClicked += (s, e) => SaveAsDefault();
            _stylePanel.Controls.Add(MkLabel("Font and Style", new Point(PAD_L, y), true));
            _stylePanel.Controls.Add(lnkSave);
            y += 32;

            _cbFont = new ComboBox
            {
                Location      = new Point(PAD_L, y),
                Size          = new Size(INNER_W, 24),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = new Font("Segoe UI", 9f),
                FlatStyle     = FlatStyle.System
            };
            using (var fc = new InstalledFontCollection())
                foreach (var ff in fc.Families)
                    _cbFont.Items.Add(ff.Name);
            _cbFont.SelectedIndexChanged += (s, e) => ApplyFont();
            _stylePanel.Controls.Add(_cbFont);
            y += 40;

            // Size + Style row
            _tbSize = new TextBox
            {
                Location  = new Point(PAD_L, y + 2),
                Size      = new Size(38, 22),
                Font      = new Font("Segoe UI", 9f),
                TextAlign = HorizontalAlignment.Center,
                BackColor = INPUT_BG
            };
            _tbSize.KeyDown   += (s, e) => { if (e.KeyCode == Keys.Return) ApplyFont(); };
            _tbSize.LostFocus += (s, e) => ApplyFont();
            _stylePanel.Controls.Add(_tbSize);
            _stylePanel.Controls.Add(MkLabel("pt", new Point(PAD_L + 42, y + 5), false));

            _cbStyle = new ComboBox
            {
                Location      = new Point(PAD_L + 58, y),
                Size          = new Size(INNER_W - 58, 24),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = new Font("Segoe UI", 9f),
                FlatStyle     = FlatStyle.System
            };
            _cbStyle.Items.AddRange(new object[] { "Regular", "Bold", "Italic", "Bold Italic" });
            _cbStyle.SelectedIndexChanged += (s, e) => ApplyFont();
            _stylePanel.Controls.Add(_cbStyle);
            y += 46;

            // Color row
            _stylePanel.Controls.Add(MkLabel("Color", new Point(PAD_L, y + 3), false));
            _btnTextColor = Swatch(new Point(PAD_L + 44, y), Color.Black);
            _btnTextColor.Click += (s, e) => PickColor(_btnTextColor, c => _printTarget.TextColor = c);
            _stylePanel.Controls.Add(_btnTextColor);
            y += 44;

            // H-align + V-align buttons
            string[] hTips = { "Align Left", "Center Text", "Align Right" };
            string[] vTips = { "Align Top",  "Middle",      "Align Bottom" };
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                _btnHAlign[i] = FlatBtn(new Point(PAD_L + i * 30, y), new Size(26, 26));
                _btnHAlign[i].Paint += (s, e) => DrawHAlignIcon(e.Graphics, idx, 26, 26);
                _btnHAlign[i].Click += (s, e) => { if (_printTarget != null) { _printTarget.HTextAlign = (StringAlignment)idx; HighlightBtn(_btnHAlign, idx); } };
                new ToolTip().SetToolTip(_btnHAlign[i], hTips[i]);
                _stylePanel.Controls.Add(_btnHAlign[i]);

                _btnVAlign[i] = FlatBtn(new Point(PAD_L + 102 + i * 30, y), new Size(26, 26));
                _btnVAlign[i].Paint += (s, e) => DrawVAlignIcon(e.Graphics, idx, 26, 26);
                _btnVAlign[i].Click += (s, e) => { if (_printTarget != null) { _printTarget.VTextAlign = (StringAlignment)idx; HighlightBtn(_btnVAlign, idx); } };
                new ToolTip().SetToolTip(_btnVAlign[i], vTips[i]);
                _stylePanel.Controls.Add(_btnVAlign[i]);
            }
            y += 52;

            // ── Border ────────────────────────────────────────────────────────
            _stylePanel.Controls.Add(Rule(y)); y += 24;
            _stylePanel.Controls.Add(MkLabel("Border", new Point(PAD_L, y), true)); y += 32;

            _stylePanel.Controls.Add(MkLabel("Color", new Point(PAD_L, y + 3), false));
            _btnBorderColor = Swatch(new Point(PAD_L + 44, y), Color.Black);
            _btnBorderColor.Click += (s, e) => PickColor(_btnBorderColor, c => _printTarget.BorderColor = c);
            _stylePanel.Controls.Add(_btnBorderColor);

            _stylePanel.Controls.Add(MkLabel("Width", new Point(PAD_L + 96, y + 3), false));
            _tbBorderWidth = new TextBox
            {
                Location  = new Point(PAD_L + 136, y + 1),
                Size      = new Size(40, 22),
                Font      = new Font("Segoe UI", 9f),
                TextAlign = HorizontalAlignment.Center,
                BackColor = INPUT_BG
            };
            _tbBorderWidth.KeyDown   += (s, e) => { if (e.KeyCode == Keys.Return) ApplyBorder(); };
            _tbBorderWidth.LostFocus += (s, e) => ApplyBorder();
            _stylePanel.Controls.Add(_tbBorderWidth);
            y += 44;

            int col2 = PAD_L + INNER_W / 2 + 4;
            _chkBorderTop    = MkCheck("Top",    new Point(PAD_L, y));
            _chkBorderBottom = MkCheck("Bottom", new Point(col2,  y));
            _chkBorderTop.CheckedChanged    += (s, e) => { if (_printTarget != null) _printTarget.BorderTop    = _chkBorderTop.Checked; };
            _chkBorderBottom.CheckedChanged += (s, e) => { if (_printTarget != null) _printTarget.BorderBottom = _chkBorderBottom.Checked; };
            _stylePanel.Controls.AddRange(new Control[] { _chkBorderTop, _chkBorderBottom });
            y += 34;

            _chkBorderLeft  = MkCheck("Left",  new Point(PAD_L, y));
            _chkBorderRight = MkCheck("Right",  new Point(col2,  y));
            _chkBorderLeft.CheckedChanged  += (s, e) => { if (_printTarget != null) _printTarget.BorderLeft  = _chkBorderLeft.Checked; };
            _chkBorderRight.CheckedChanged += (s, e) => { if (_printTarget != null) _printTarget.BorderRight = _chkBorderRight.Checked; };
            _stylePanel.Controls.AddRange(new Control[] { _chkBorderLeft, _chkBorderRight });
            y += 48;

            // ── Background ────────────────────────────────────────────────────
            _stylePanel.Controls.Add(Rule(y)); y += 24;
            _stylePanel.Controls.Add(MkLabel("Background", new Point(PAD_L, y), true)); y += 32;

            _stylePanel.Controls.Add(MkLabel("Fill", new Point(PAD_L, y + 3), false));
            _btnFillColor = Swatch(new Point(PAD_L + 30, y), Color.White);
            _btnFillColor.Click += (s, e) => PickColor(_btnFillColor, c => _printTarget.FillColor = c);
            _stylePanel.Controls.Add(_btnFillColor);

            _chkTransparent = new CheckBox
            {
                Text     = "Transparent",
                Location = new Point(PAD_L + 76, y + 1),
                AutoSize = true,
                Font     = new Font("Segoe UI", 8.5f),
                Checked  = true
            };
            _chkTransparent.CheckedChanged += (s, e) =>
            {
                if (_printTarget != null) _printTarget.FillTransparent = _chkTransparent.Checked;
            };
            _stylePanel.Controls.Add(_chkTransparent);
        }

        private void BuildBraillePanel()
        {
            _btnTrim = new Button
            {
                Text      = "Trim extra text",
                Location  = new Point(PAD_L, 12),
                Size      = new Size(INNER_W, 24),
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 8.5f),
                Cursor    = Cursors.Hand,
                Visible   = false,
                BackColor = Color.FromArgb(240, 240, 240)
            };
            _btnTrim.FlatAppearance.BorderColor = LINE_COLOR;
            _btnTrim.Click += (s, e) =>
            {
                if (_brailleTarget != null && !_brailleTarget.IsDisposed)
                    _brailleTarget.Trim();
            };
            _braillePanel.Controls.Add(_btnTrim);
        }

        private void BuildLinePanel()
        {
            int y = 12;
            _linePanel.Controls.Add(Rule(y)); y += 24;
            _linePanel.Controls.Add(MkLabel("Line", new Point(PAD_L, y), true)); y += 32;

            _cbLineDir = new ComboBox
            {
                Location      = new Point(PAD_L, y),
                Size          = new Size(INNER_W, 24),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = new Font("Segoe UI", 9f),
                FlatStyle     = FlatStyle.System
            };
            _cbLineDir.Items.AddRange(new object[] { "Horizontal", "Vertical" });
            _cbLineDir.SelectedIndex = 0;
            _cbLineDir.SelectedIndexChanged += (s, e) =>
            {
                if (_updating || _lineTarget == null || _lineTarget.IsDisposed) return;
                _lineTarget.Direction = _cbLineDir.SelectedIndex == 0
                    ? LineBox.LineDirection.Horizontal
                    : LineBox.LineDirection.Vertical;
            };
            _linePanel.Controls.Add(_cbLineDir);
            y += 44;

            _linePanel.Controls.Add(MkLabel("Color", new Point(PAD_L, y + 3), false));
            _btnLineColor = Swatch(new Point(PAD_L + 44, y), Color.Black);
            _btnLineColor.Click += (s, e) => PickLineColor();
            _linePanel.Controls.Add(_btnLineColor);

            _linePanel.Controls.Add(MkLabel("Width", new Point(PAD_L + 96, y + 3), false));
            _tbLineWidth = new TextBox
            {
                Location  = new Point(PAD_L + 140, y + 1),
                Size      = new Size(40, 22),
                Font      = new Font("Segoe UI", 9f),
                TextAlign = HorizontalAlignment.Center,
                BackColor = INPUT_BG
            };
            _tbLineWidth.KeyDown   += (s, e) => { if (e.KeyCode == Keys.Return) ApplyLineWidth(); };
            _tbLineWidth.LostFocus += (s, e) => ApplyLineWidth();
            _linePanel.Controls.Add(_tbLineWidth);
        }

        private void BuildTablePanel()
        {
            int y = 12;
            _tablePanel.Controls.Add(Rule(y)); y += 24;
            _tablePanel.Controls.Add(MkLabel("Rows && Columns", new Point(PAD_L, y), true)); y += 32;

            // Rows row
            _tablePanel.Controls.Add(MkLabel("Rows", new Point(PAD_L, y + 4), false));
            _tbTableRows = new TextBox
            {
                Location  = new Point(PAD_L + 48, y),
                Size      = new Size(INNER_W - 96, 22),
                Font      = new Font("Segoe UI", 9f),
                BackColor = INPUT_BG
            };
            _tbTableRows.LostFocus += (s, e) => ApplyTableSpec();
            _tbTableRows.KeyDown   += (s, e) => { if (e.KeyCode == Keys.Return) ApplyTableSpec(); };
            _tablePanel.Controls.Add(_tbTableRows);

            var btnRowPlus = new Button
            {
                Text      = "+",
                Location  = new Point(PAD_L + INNER_W - 44, y),
                Size      = new Size(44, 22),
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 10f, FontStyle.Bold),
                BackColor = Color.FromArgb(240, 240, 240),
                Cursor    = Cursors.Hand
            };
            btnRowPlus.FlatAppearance.BorderColor = LINE_COLOR;
            btnRowPlus.Click += (s, e) =>
            {
                if (_tableTarget == null || _tableTarget.IsDisposed) return;
                _tableTarget.RowSpec += "-1";
                RefreshTable();
            };
            _tablePanel.Controls.Add(btnRowPlus);
            y += 44;

            // Columns row
            _tablePanel.Controls.Add(MkLabel("Cols", new Point(PAD_L, y + 4), false));
            _tbTableCols = new TextBox
            {
                Location  = new Point(PAD_L + 48, y),
                Size      = new Size(INNER_W - 96, 22),
                Font      = new Font("Segoe UI", 9f),
                BackColor = INPUT_BG
            };
            _tbTableCols.LostFocus += (s, e) => ApplyTableSpec();
            _tbTableCols.KeyDown   += (s, e) => { if (e.KeyCode == Keys.Return) ApplyTableSpec(); };
            _tablePanel.Controls.Add(_tbTableCols);

            var btnColPlus = new Button
            {
                Text      = "+",
                Location  = new Point(PAD_L + INNER_W - 44, y),
                Size      = new Size(44, 22),
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 10f, FontStyle.Bold),
                BackColor = Color.FromArgb(240, 240, 240),
                Cursor    = Cursors.Hand
            };
            btnColPlus.FlatAppearance.BorderColor = LINE_COLOR;
            btnColPlus.Click += (s, e) =>
            {
                if (_tableTarget == null || _tableTarget.IsDisposed) return;
                _tableTarget.ColSpec += "-1";
                RefreshTable();
            };
            _tablePanel.Controls.Add(btnColPlus);
        }

        // ── Selection ─────────────────────────────────────────────────────────

        private void OnSelectionChanged(Control ctrl)
        {
            if (_target != null)
            {
                _target.LocationChanged -= OnTargetTransformed;
                _target.SizeChanged     -= OnTargetTransformed;
            }
            if (_brailleTarget != null)
                _brailleTarget.BrailleTextChanged -= OnBrailleTextChanged;

            _target        = ctrl;
            _printTarget   = ctrl as PrintTextBox;
            _brailleTarget = ctrl as BrailleTextBox;
            _lineTarget    = ctrl as LineBox;
            _tableTarget   = ctrl as TableBox;
            _tactileTarget = ctrl as TactileBox;

            if (_target != null)
            {
                _target.LocationChanged += OnTargetTransformed;
                _target.SizeChanged     += OnTargetTransformed;
            }
            if (_brailleTarget != null)
                _brailleTarget.BrailleTextChanged += OnBrailleTextChanged;

            _stylePanel  .Visible = _printTarget   != null;
            _braillePanel.Visible = _brailleTarget != null;
            _linePanel   .Visible = _lineTarget    != null;
            _tablePanel  .Visible = _tableTarget   != null;

            bool isTactile = _tactileTarget != null;
            _lblW .Visible = !isTactile;
            _wrapW.Visible = !isTactile;
            _lblH .Visible = !isTactile;
            _wrapH.Visible = !isTactile;

            RefreshFields();
            RefreshStyle();
            RefreshTrimButton();
            RefreshLine();
            RefreshTable();
        }

        private void OnTargetTransformed(object sender, EventArgs e)
        {
            RefreshFields();
            RefreshTrimButton();
        }

        private void RefreshTrimButton()
        {
            if (_btnTrim == null) return;
            _btnTrim.Visible = _brailleTarget != null
                && !_brailleTarget.IsDisposed
                && _brailleTarget.HasOverflow;
        }

        private void RefreshFields()
        {
            if (_updating) return;
            _updating = true;
            try
            {
                if (_target == null || _target.IsDisposed)
                { _tbX.Text = _tbY.Text = _tbW.Text = _tbH.Text = ""; return; }
                var origin = _canvas.PageOriginPx;
                _tbX.Text = ((int)(_target.Left  - origin.X)).ToString();
                _tbY.Text = ((int)(_target.Top   - origin.Y)).ToString();
                _tbW.Text = _target.Width.ToString();
                _tbH.Text = _target.Height.ToString();
            }
            finally { _updating = false; }
        }

        private void RefreshStyle()
        {
            if (_printTarget == null || _printTarget.IsDisposed) return;
            _updating = true;
            try
            {
                int fi = _cbFont.Items.IndexOf(_printTarget.FontFamily);
                if (fi >= 0) _cbFont.SelectedIndex = fi;
                _tbSize.Text = _printTarget.FontSizePt.ToString("F0");
                int si = 0;
                switch (_printTarget.TextFontStyle)
                {
                    case FontStyle.Bold:                    si = 1; break;
                    case FontStyle.Italic:                  si = 2; break;
                    case FontStyle.Bold | FontStyle.Italic: si = 3; break;
                }
                _cbStyle.SelectedIndex = si;
                SetSwatchColor(_btnTextColor, _printTarget.TextColor);
                HighlightBtn(_btnHAlign, (int)_printTarget.HTextAlign);
                HighlightBtn(_btnVAlign, (int)_printTarget.VTextAlign);
                SetSwatchColor(_btnBorderColor, _printTarget.BorderColor);
                _tbBorderWidth.Text      = _printTarget.BorderWidth.ToString();
                _chkBorderTop.Checked    = _printTarget.BorderTop;
                _chkBorderBottom.Checked = _printTarget.BorderBottom;
                _chkBorderLeft.Checked   = _printTarget.BorderLeft;
                _chkBorderRight.Checked  = _printTarget.BorderRight;
                SetSwatchColor(_btnFillColor, _printTarget.FillColor);
                _chkTransparent.Checked  = _printTarget.FillTransparent;
            }
            finally { _updating = false; }
        }

        private void ApplyTransform()
        {
            if (_updating || _target == null || _target.IsDisposed) return;
            if (!int.TryParse(_tbX.Text, out int x) || !int.TryParse(_tbY.Text, out int y) ||
                !int.TryParse(_tbW.Text, out int w) || !int.TryParse(_tbH.Text, out int h)) return;
            _updating = true;
            try
            {
                var origin = _canvas.PageOriginPx;
                _target.Location = new Point((int)origin.X + x, (int)origin.Y + y);
                if (_tactileTarget == null && w >= 10 && h >= 10)
                    _target.Size = new Size(w, h);
            }
            finally { _updating = false; }
        }

        private void ApplyFont()
        {
            if (_updating || _printTarget == null || _printTarget.IsDisposed) return;
            if (_cbFont.SelectedItem is string family)
                _printTarget.FontFamily = family;
            if (float.TryParse(_tbSize.Text, out float sz))
                _printTarget.FontSizePt = sz;
            FontStyle style = FontStyle.Regular;
            switch (_cbStyle.SelectedIndex)
            {
                case 1: style = FontStyle.Bold;                    break;
                case 2: style = FontStyle.Italic;                  break;
                case 3: style = FontStyle.Bold | FontStyle.Italic; break;
            }

            int selStart = _printTarget.SavedSelStart;
            int selEnd   = _printTarget.SavedSelEnd;
            if (selStart < selEnd)
            {
                _printTarget.ApplyStyleToRange(selStart, selEnd, style);
            }
            else
            {
                // No text selection (box selected, or cursor-only): apply to all content
                _printTarget.TextFontStyle = style;
                _printTarget.ApplyStyleToRange(0, _printTarget.DisplayText.Length, style);
            }
        }

        private void ApplyBorder()
        {
            if (_updating || _printTarget == null || _printTarget.IsDisposed) return;
            if (int.TryParse(_tbBorderWidth.Text, out int bw))
                _printTarget.BorderWidth = bw;
        }

        // ── Alignment (snaps to margin) ───────────────────────────────────────

        private void AlignSelected(string mode)
        {
            var multi = _canvas.SelectedControls;
            if (multi.Count == 0 && (_target == null || _target.IsDisposed)) return;

            var mb     = _canvas.MarginBoundsPx;
            var scroll = _canvas.AutoScrollPosition;
            int ml      = (int)mb.Left   + scroll.X;
            int mt      = (int)mb.Top    + scroll.Y;
            int mr      = (int)mb.Right  + scroll.X;
            int mbottom = (int)mb.Bottom + scroll.Y;

            System.Collections.Generic.IEnumerable<Control> targets =
                multi.Count > 0 ? (System.Collections.Generic.IEnumerable<Control>)multi
                                 : new[] { _target };

            foreach (var ctrl in targets)
            {
                if (ctrl == null || ctrl.IsDisposed) continue;
                int x = ctrl.Left, y = ctrl.Top;
                switch (mode)
                {
                    case "left":    x = ml;                                          break;
                    case "centerH": x = ml + ((int)mb.Width  - ctrl.Width)  / 2;    break;
                    case "right":   x = mr - ctrl.Width;                             break;
                    case "top":     y = mt;                                           break;
                    case "centerV": y = mt + ((int)mb.Height - ctrl.Height) / 2;     break;
                    case "bottom":  y = mbottom - ctrl.Height;                       break;
                }
                ctrl.Location = new Point(x, y);
            }
        }

        // BrailleTextBox changed → refresh trim button visibility
        private void OnBrailleTextChanged(object sender, EventArgs e)
        {
            if (_updating) return;
            RefreshTrimButton();
        }

        // ── Line ─────────────────────────────────────────────────────────────

        private void RefreshLine()
        {
            if (_lineTarget == null || _lineTarget.IsDisposed) return;
            _updating = true;
            try
            {
                _cbLineDir.SelectedIndex = _lineTarget.Direction == LineBox.LineDirection.Horizontal ? 0 : 1;
                SetSwatchColor(_btnLineColor, _lineTarget.LineColor);
                _tbLineWidth.Text = _lineTarget.LineThickness.ToString();
            }
            finally { _updating = false; }
        }

        private void PickLineColor()
        {
            if (_lineTarget == null || _lineTarget.IsDisposed) return;
            using (var dlg = new ColorDialog { Color = _btnLineColor.BackColor })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                SetSwatchColor(_btnLineColor, dlg.Color);
                _lineTarget.LineColor = dlg.Color;
            }
        }

        private void ApplyLineWidth()
        {
            if (_updating || _lineTarget == null || _lineTarget.IsDisposed) return;
            if (int.TryParse(_tbLineWidth.Text, out int w))
                _lineTarget.LineThickness = w;
        }

        // ── Table ─────────────────────────────────────────────────────────────

        private void RefreshTable()
        {
            if (_tableTarget == null || _tableTarget.IsDisposed) return;
            _updating = true;
            try
            {
                _tbTableRows.Text = _tableTarget.RowSpec;
                _tbTableCols.Text = _tableTarget.ColSpec;
            }
            finally { _updating = false; }
        }

        private void ApplyTableSpec()
        {
            if (_updating || _tableTarget == null || _tableTarget.IsDisposed) return;
            _tableTarget.RowSpec = _tbTableRows.Text;
            _tableTarget.ColSpec = _tbTableCols.Text;
        }

        // ── Color helpers ─────────────────────────────────────────────────────

        private void PickColor(Button swatch, Action<Color> apply)
        {
            using (var dlg = new ColorDialog { Color = swatch.BackColor })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                if (_printTarget == null || _printTarget.IsDisposed) return;
                SetSwatchColor(swatch, dlg.Color);
                apply(dlg.Color);
            }
        }

        private static void SetSwatchColor(Button btn, Color c)
        {
            btn.BackColor = c;
            btn.FlatAppearance.BorderColor =
                Color.FromArgb(Math.Max(0, c.R - 50), Math.Max(0, c.G - 50), Math.Max(0, c.B - 50));
        }

        private static void HighlightBtn(Button[] btns, int active)
        {
            for (int i = 0; i < btns.Length; i++)
                btns[i].BackColor = i == active
                    ? Color.FromArgb(210, 210, 210)
                    : Color.FromArgb(240, 240, 240);
        }

        private void SaveAsDefault()
        {
            if (_printTarget == null) return;
            PrintTextBox.DefaultFontFamily = _printTarget.FontFamily;
            PrintTextBox.DefaultFontSizePt = _printTarget.FontSizePt;
            PrintTextBox.DefaultFontStyle  = _printTarget.TextFontStyle;
            PrintTextBox.DefaultTextColor  = _printTarget.TextColor;
        }

        // ── Control factory helpers ───────────────────────────────────────────

        private static Label MkLabel(string text, Point loc, bool bold) => new Label
        {
            Text      = text,
            Location  = loc,
            AutoSize  = true,
            Font      = new Font("Segoe UI", bold ? 8.5f : 8f, bold ? FontStyle.Bold : FontStyle.Regular),
            ForeColor = bold ? HDR_COLOR : LBL_COLOR
        };

        private static TextBox NumBox() => new TextBox
        {
            Font      = new Font("Segoe UI", 9f),
            TextAlign = HorizontalAlignment.Right,
            ForeColor = Color.FromArgb(40, 40, 40),
            BackColor = INPUT_BG
        };

        private static Panel InputWrap(TextBox tb, Point loc, int width)
        {
            tb.BorderStyle = BorderStyle.None;
            tb.BackColor   = INPUT_BG;
            tb.Location    = new Point(0, 2);
            tb.Size        = new Size(width, 18);
            var pnl = new Panel { Location = loc, Size = new Size(width, 22), BackColor = INPUT_BG };
            pnl.Controls.Add(tb);
            pnl.Paint += (s, e) =>
            {
                using (var pen = new Pen(LINE_COLOR))
                    e.Graphics.DrawLine(pen, 0, pnl.Height - 1, pnl.Width, pnl.Height - 1);
            };
            return pnl;
        }

        private static Button Swatch(Point loc, Color c)
        {
            var btn = new Button
            {
                Location  = loc,
                Size      = new Size(40, 22),
                FlatStyle = FlatStyle.Flat,
                BackColor = c,
                Text      = ""
            };
            btn.FlatAppearance.BorderSize  = 1;
            btn.FlatAppearance.BorderColor =
                Color.FromArgb(Math.Max(0, c.R - 50), Math.Max(0, c.G - 50), Math.Max(0, c.B - 50));
            return btn;
        }

        private static Button FlatBtn(Point loc, Size size)
        {
            var btn = new Button
            {
                Location  = loc,
                Size      = size,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(240, 240, 240),
                Cursor    = Cursors.Hand,
                Text      = ""
            };
            btn.FlatAppearance.BorderColor = LINE_COLOR;
            btn.FlatAppearance.BorderSize  = 1;
            return btn;
        }

        private static CheckBox MkCheck(string text, Point loc) => new CheckBox
        {
            Text     = text,
            Location = loc,
            AutoSize = true,
            Font     = new Font("Segoe UI", 8.5f)
        };

        private static Label Rule(int y) => new Label
        {
            Location  = new Point(0, y),
            Size      = new Size(PANEL_W, 1),
            BackColor = LINE_COLOR
        };

        // ── Alignment icon painters ───────────────────────────────────────────

        private static void DrawAlignIcon(Graphics g, int idx, int bw, int bh)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int cx = bw / 2, cy = bh / 2;
            using (var lp = new Pen(Color.FromArgb(90, 90, 90), 2f))
            using (var rb = new SolidBrush(Color.FromArgb(120, 160, 210)))
            {
                switch (idx)
                {
                    case 0: g.DrawLine(lp, 3, 3, 3, bh-3); g.FillRectangle(rb, 6,6,12,4); g.FillRectangle(rb, 6,14,16,4); break;
                    case 1: g.DrawLine(lp, cx,3, cx, bh-3); g.FillRectangle(rb, cx-7,6,14,4); g.FillRectangle(rb, cx-9,14,18,4); break;
                    case 2: g.DrawLine(lp, bw-3,3, bw-3, bh-3); g.FillRectangle(rb, bw-18,6,12,4); g.FillRectangle(rb, bw-22,14,16,4); break;
                    case 3: g.DrawLine(lp, 3,3, bw-3,3); g.FillRectangle(rb, 6,6,4,12); g.FillRectangle(rb, 14,6,4,16); break;
                    case 4: g.DrawLine(lp, 3,cy, bw-3,cy); g.FillRectangle(rb, 6,cy-7,4,14); g.FillRectangle(rb, 14,cy-9,4,18); break;
                    case 5: g.DrawLine(lp, 3,bh-3, bw-3,bh-3); g.FillRectangle(rb, 6,bh-18,4,12); g.FillRectangle(rb, 14,bh-22,4,16); break;
                }
            }
        }

        private static void DrawHAlignIcon(Graphics g, int idx, int bw, int bh)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(Color.FromArgb(80, 80, 80)))
            {
                int lh = 3, y1 = 6, y2 = 12, y3 = 18;
                switch (idx)
                {
                    case 0: g.FillRectangle(b, 3, y1, 16, lh); g.FillRectangle(b, 3, y2, 11, lh); g.FillRectangle(b, 3, y3, 14, lh); break;
                    case 1: g.FillRectangle(b, 4, y1, 17, lh); g.FillRectangle(b, 7, y2, 11, lh); g.FillRectangle(b, 5, y3, 15, lh); break;
                    case 2: g.FillRectangle(b, 6, y1, 16, lh); g.FillRectangle(b, 11,y2, 11, lh); g.FillRectangle(b, 8, y3, 14, lh); break;
                }
            }
        }

        private static void DrawVAlignIcon(Graphics g, int idx, int bw, int bh)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var lp = new Pen(Color.FromArgb(80, 80, 80), 1.5f))
            using (var b  = new SolidBrush(Color.FromArgb(150, 150, 150)))
            {
                switch (idx)
                {
                    case 0: g.DrawLine(lp, 3,3, bw-3,3); g.FillRectangle(b, 6,6,4,12); g.FillRectangle(b, 14,6,4,7); break;
                    case 1: int cy=bh/2; g.DrawLine(lp, 3,cy, bw-3,cy); g.FillRectangle(b, 6,cy-7,4,14); g.FillRectangle(b, 14,cy-4,4,8); break;
                    case 2: g.DrawLine(lp, 3,bh-3, bw-3,bh-3); g.FillRectangle(b, 6,bh-18,4,12); g.FillRectangle(b, 14,bh-10,4,7); break;
                }
            }
        }
    }
}
