using System;
using System.Drawing;
using System.Windows.Forms;

namespace BrailleUrdu
{
    public class PagesPanel : UserControl
    {
        private readonly Action _onPageChanged;

        private Label            _lblPages;
        private Label            _lblMasterPage;
        private Panel            _underlinePages;
        private Panel            _underlineMaster;
        private FlowLayoutPanel  _content;
        private Button           _btnRemove;
        private Button           _btnAdd;
        private bool             _showMaster;

        public PagesPanel(Action onPageChanged)
        {
            _onPageChanged = onPageChanged;
            Build();
            SetMode(false);
        }

        // ── Build UI ──────────────────────────────────────────────────────────

        private void Build()
        {
            Width     = 215;
            Dock      = DockStyle.Left;
            BackColor = Color.FromArgb(225, 225, 225);

            // ── Header ───────────────────────────────────────────────────────
            var header = new Panel
            {
                Height    = 34,
                Dock      = DockStyle.Top,
                BackColor = Color.FromArgb(235, 235, 235)
            };

            _lblPages = new Label
            {
                Text      = "Pages",
                Dock      = DockStyle.Left,
                Width     = 100,
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor    = Cursors.Hand,
                Font      = new Font("Segoe UI", 9f)
            };
            _lblMasterPage = new Label
            {
                Text      = "Master Page",
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor    = Cursors.Hand,
                Font      = new Font("Segoe UI", 9f)
            };

            // Active-tab underline indicator
            _underlinePages = new Panel
            {
                Height    = 2,
                Dock      = DockStyle.Bottom,
                BackColor = Color.DodgerBlue
            };
            _underlineMaster = new Panel
            {
                Height    = 2,
                Dock      = DockStyle.Bottom,
                BackColor = Color.DodgerBlue,
                Visible   = false
            };

            _lblPages.Controls.Add(_underlinePages);
            _lblMasterPage.Controls.Add(_underlineMaster);

            // Controls added right-to-left because DockStyle.Left fills left-first
            header.Controls.Add(_lblMasterPage);
            header.Controls.Add(_lblPages);

            _lblPages.Click       += (s, e) => SetMode(false);
            _lblMasterPage.Click  += (s, e) => SetMode(true);
            _underlinePages.Click += (s, e) => SetMode(false);
            _underlineMaster.Click+= (s, e) => SetMode(true);

            // ── Scrollable thumbnail area ─────────────────────────────────────
            _content = new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                BackColor     = Color.FromArgb(210, 210, 210),
                AutoScroll    = true,
                Padding       = new Padding(12, 12, 0, 0),
                FlowDirection = FlowDirection.TopDown,
                WrapContents  = false
            };

            // ── Footer ───────────────────────────────────────────────────────
            var footer = new Panel
            {
                Height    = 48,
                Dock      = DockStyle.Bottom,
                BackColor = Color.FromArgb(235, 235, 235)
            };

            _btnRemove = new Button
            {
                Text      = "Remove Page",
                Location  = new Point(5, 10),
                Width     = 97,
                Height    = 28,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.FromArgb(190, 30, 30),
                BackColor = Color.White,
                Font      = new Font("Segoe UI", 8.5f),
                Cursor    = Cursors.Hand
            };
            _btnRemove.FlatAppearance.BorderColor = Color.Silver;

            _btnAdd = new Button
            {
                Text      = "Add Page",
                Location  = new Point(110, 10),
                Width     = 97,
                Height    = 28,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                Font      = new Font("Segoe UI", 8.5f),
                Cursor    = Cursors.Hand
            };
            _btnAdd.FlatAppearance.BorderColor = Color.Silver;

            _btnRemove.Click += OnRemovePage;
            _btnAdd.Click    += OnAddPage;

            footer.Controls.Add(_btnRemove);
            footer.Controls.Add(_btnAdd);

            // Add in reverse dock order: Fill first, then Top and Bottom
            Controls.Add(_content);
            Controls.Add(header);
            Controls.Add(footer);
        }

        // ── Tab switching ─────────────────────────────────────────────────────

        private void SetMode(bool masterMode)
        {
            _showMaster = masterMode;

            _lblPages.ForeColor      = masterMode ? Color.Gray  : Color.Black;
            _lblMasterPage.ForeColor = masterMode ? Color.DodgerBlue : Color.Gray;
            _underlinePages.Visible  = !masterMode;
            _underlineMaster.Visible = masterMode;

            // Add/Remove only applies to regular pages
            _btnAdd.Enabled    = !masterMode;
            _btnRemove.Enabled = !masterMode;

            // Navigate to the appropriate page when switching tabs
            bool navigated = false;
            if (masterMode && !Document.IsOnMasterPage)
            {
                Document.CurrentPageIndex = -1;
                navigated = true;
            }
            else if (!masterMode && Document.IsOnMasterPage)
            {
                Document.CurrentPageIndex = 0;
                navigated = true;
            }

            RebuildThumbnails();
            if (navigated) _onPageChanged?.Invoke();
        }

        // ── Thumbnails ────────────────────────────────────────────────────────

        public void RebuildThumbnails()
        {
            _content.Controls.Clear();

            if (_showMaster)
            {
                _content.Controls.Add(MakeThumbnail("Left",  -1));
                _content.Controls.Add(MakeThumbnail("Right", -2));
            }
            else
            {
                for (int i = 0; i < Document.Pages.Count; i++)
                    _content.Controls.Add(MakeThumbnail((i + 1).ToString(), i));
            }
        }

        private Panel MakeThumbnail(string label, int index)
        {
            bool selected = index == Document.CurrentPageIndex;

            var wrapper = new Panel
            {
                Width     = 185,
                Height    = 132,
                Margin    = new Padding(0, 0, 0, 8),
                BackColor = selected
                    ? Color.FromArgb(190, 220, 255)
                    : Color.FromArgb(210, 210, 210),
                Cursor = Cursors.Hand
            };

            var thumb = new Panel
            {
                Width     = 88,
                Height    = 108,
                Location  = new Point(48, 8),
                BackColor = Color.White
            };
            thumb.Paint += (s, e) =>
            {
                using (var pen = new Pen(
                    selected ? Color.FromArgb(80, 150, 230) : Color.Silver, 1))
                    e.Graphics.DrawRectangle(pen, 0, 0, thumb.Width - 1, thumb.Height - 1);
            };

            var lbl = new Label
            {
                Text      = label,
                Width     = 185,
                Height    = 18,
                Location  = new Point(0, 114),
                TextAlign = ContentAlignment.MiddleCenter,
                Font      = new Font("Segoe UI", 8.5f),
                BackColor = Color.Transparent
            };

            wrapper.Controls.Add(thumb);
            wrapper.Controls.Add(lbl);

            Action select = () => SelectPage(index);
            wrapper.Click += (s, e) => select();
            thumb.Click   += (s, e) => select();
            lbl.Click     += (s, e) => select();

            return wrapper;
        }

        // ── Page management ───────────────────────────────────────────────────

        private void SelectPage(int index)
        {
            Document.CurrentPageIndex = index;
            RebuildThumbnails();
            _onPageChanged?.Invoke();
        }

        private void OnAddPage(object sender, EventArgs e)
        {
            Document.AddPage();
            Document.CurrentPageIndex = Document.Pages.Count - 1;
            RebuildThumbnails();
            _onPageChanged?.Invoke();
        }

        private void OnRemovePage(object sender, EventArgs e)
        {
            Document.RemovePage(Document.CurrentPageIndex);
            RebuildThumbnails();
            _onPageChanged?.Invoke();
        }
    }
}
