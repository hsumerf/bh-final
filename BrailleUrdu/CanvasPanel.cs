using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Windows.Forms;

namespace BrailleUrdu
{
    public class CanvasPanel : Panel
    {
        private const float MM_PER_INCH  = 25.4f;
        private const float SCREEN_DPI   = 96f;
        private const int   PAGE_PADDING = 30;

        // Controls keyed by the page they were placed on
        private readonly Dictionary<DocumentPage, List<Control>> _pageControls
            = new Dictionary<DocumentPage, List<Control>>();

        private float  _zoom     = 1.10f;
        private string _viewMode = "Braille & Print"; // "Braille & Print" | "Braille" | "Print"

        public float Zoom
        {
            get => _zoom;
            set
            {
                _zoom = Math.Max(0.25f, Math.Min(4f, value));
                UpdateScrollSize();
                Invalidate();
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED — composites all children in one buffer
                return cp;
            }
        }

        public CanvasPanel()
        {
            SetStyle(ControlStyles.Selectable, true);
            DoubleBuffered = true;
            BackColor      = Color.FromArgb(185, 185, 185);
            Dock           = DockStyle.Fill;
            AutoScroll     = true;
            TabStop        = true;
            UpdateScrollSize();
        }

        // ── Coordinate helpers ────────────────────────────────────────────────

        private float MmToPx(float mm) => mm * (SCREEN_DPI / MM_PER_INCH) * _zoom;

        public float PageWidthPx  => MmToPx(DocumentPage.WIDTH_MM);
        public float PageHeightPx => MmToPx(DocumentPage.HEIGHT_MM);

        private PointF PageOrigin() => new PointF(
            Math.Max(PAGE_PADDING, (ClientSize.Width - PageWidthPx) / 2f),
            PAGE_PADDING);

        public PointF PageOriginPx => PageOrigin();

        private void UpdateScrollSize()
        {
            AutoScrollMinSize = new Size(
                (int)(PageWidthPx  + PAGE_PADDING * 2),
                (int)(PageHeightPx + PAGE_PADDING * 2));
        }

        // ── Painting ──────────────────────────────────────────────────────────

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode   = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // Apply scroll offset so content pans with scrollbars
            g.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);

            var  origin = PageOrigin();
            float px = origin.X;
            float py = origin.Y;
            float pw = PageWidthPx;
            float ph = PageHeightPx;

            // Drop shadow
            using (var shadow = new SolidBrush(Color.FromArgb(55, 0, 0, 0)))
                g.FillRectangle(shadow, px + 5, py + 5, pw, ph);

            // Page surface
            g.FillRectangle(Brushes.White, px, py, pw, ph);

            // Margin guides (pink lines — same visual as screenshot)
            DrawMarginGuides(g, px, py, pw, ph);

            // Page border
            using (var border = new Pen(Color.FromArgb(190, 190, 190), 1))
                g.DrawRectangle(border, px, py, pw, ph);

            // Elements
            RenderElements(g, px, py);
        }

        private void DrawMarginGuides(Graphics g, float px, float py, float pw, float ph)
        {
            var page = Document.CurrentPage;
            using (var pen = new Pen(Color.FromArgb(230, 160, 175), 1))
            {
                float ml = MmToPx(page.MarginLeft);
                float mr = MmToPx(page.MarginRight);
                float mt = MmToPx(page.MarginTop);
                float mb = MmToPx(page.MarginBottom);

                g.DrawLine(pen, px + ml,      py,      px + ml,      py + ph); // left
                g.DrawLine(pen, px + pw - mr, py,      px + pw - mr, py + ph); // right
                g.DrawLine(pen, px,           py + mt, px + pw,      py + mt); // top
                g.DrawLine(pen, px,           py + ph - mb, px + pw, py + ph - mb); // bottom
            }
        }

        // ── Element rendering ─────────────────────────────────────────────────

        private void RenderElements(Graphics g, float pageX, float pageY)
        {
            var page = Document.CurrentPage;
            if (page == null) return;

            foreach (var el in page.Elements)
            {
                float ex = pageX + MmToPx(el.X);
                float ey = pageY + MmToPx(el.Y);
                float ew = MmToPx(el.Width);
                float eh = MmToPx(el.Height);

                switch (el)
                {
                    case BrailleTextElement braille:
                        DrawBrailleText(g, braille, ex, ey, ew, eh);
                        break;
                    case PrintTextElement print:
                        DrawPrintText(g, print, ex, ey, ew, eh);
                        break;
                    case ImageElement img:
                        DrawImage(g, img, ex, ey, ew, eh);
                        break;
                    case TactileGraphicElement tact:
                        DrawTactileGraphic(g, tact, ex, ey, ew, eh);
                        break;
                }

                // Selection outline
                if (el.Selected)
                {
                    using (var sel = new Pen(Color.DodgerBlue, 1.5f) { DashStyle = DashStyle.Dash })
                        g.DrawRectangle(sel, ex, ey, ew, eh);
                }
            }
        }

        private void DrawBrailleText(Graphics g, BrailleTextElement el, float x, float y, float w, float h)
        {
            float ptSize = MmToPx(DocumentPage.LINE_HEIGHT_MM) * 0.72f;
            using (var font  = new Font("SimBraille", Math.Max(1f, ptSize), GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(Color.Black))
                g.DrawString(el.BrailleText, font, brush, new RectangleF(x, y, w, h));
        }

        private void DrawPrintText(Graphics g, PrintTextElement el, float x, float y, float w, float h)
        {
            float ptSize = el.FontSize * _zoom;
            using (var font   = new Font(el.FontName, Math.Max(1f, ptSize), GraphicsUnit.Pixel))
            using (var brush  = new SolidBrush(Color.Black))
            {
                var fmt = new StringFormat();
                if (el.RightToLeft)
                    fmt.FormatFlags = StringFormatFlags.DirectionRightToLeft;
                g.DrawString(el.Text, font, brush, new RectangleF(x, y, w, h), fmt);
            }
        }

        private void DrawImage(Graphics g, ImageElement el, float x, float y, float w, float h)
        {
            if (el.Bitmap != null)
                g.DrawImage(el.Bitmap, x, y, w, h);
        }

        private void DrawTactileGraphic(Graphics g, TactileGraphicElement el, float x, float y, float w, float h)
        {
            if (el.DotGrid == null) return;

            float spacingPx = MmToPx(DocumentPage.DOT_SPACING_MM);
            float r         = spacingPx * 0.35f;
            int   cols      = el.DotGrid.GetLength(0);
            int   rows      = el.DotGrid.GetLength(1);

            using (var brush = new SolidBrush(Color.Black))
            {
                for (int row = 0; row < rows; row++)
                for (int col = 0; col < cols; col++)
                {
                    if (!el.DotGrid[col, row]) continue;
                    float cx = x + col * spacingPx;
                    float cy = y + row * spacingPx;
                    g.FillEllipse(brush, cx - r, cy - r, r * 2, r * 2);
                }
            }
        }

        // Fires whenever the user selects a different element (null = canvas clicked, nothing selected)
        public event Action<Control> SelectionChanged;

        // ── Page-control registry ─────────────────────────────────────────────

        private void RegisterControl(Control ctrl)
        {
            var page = Document.CurrentPage;
            if (!_pageControls.ContainsKey(page))
                _pageControls[page] = new List<Control>();
            _pageControls[page].Add(ctrl);

            ctrl.GotFocus += (s, e) => SelectionChanged?.Invoke(ctrl);

            // Keep registry clean when a control is deleted by the user
            ctrl.Disposed += (s, e) =>
            {
                if (_pageControls.TryGetValue(page, out var list))
                    list.Remove(ctrl);
            };
        }

        // Called whenever the active page changes (add / remove / select / master switch)
        public void PageChanged()
        {
            var  current     = Document.CurrentPage;
            bool isOnMaster  = Document.IsOnMasterPage;
            int  pageIdx     = Document.CurrentPageIndex; // ≥ 0 when on a regular page

            foreach (var kvp in _pageControls)
            {
                var  page         = kvp.Key;
                bool isMasterOdd  = page == Document.MasterOdd;
                bool isMasterEven = page == Document.MasterEven;

                bool shouldShow, shouldEnable;

                if (isOnMaster)
                {
                    // On a master page: show only that master's own controls, editable
                    shouldShow   = page == current;
                    shouldEnable = shouldShow;
                }
                else
                {
                    if (page == current)
                    {
                        shouldShow = shouldEnable = true;
                    }
                    else if (isMasterOdd && pageIdx % 2 == 0)
                    {
                        // MasterOdd overlays pages 1, 3, 5… (0-indexed 0, 2, 4…)
                        shouldShow   = true;
                        shouldEnable = false;
                    }
                    else if (isMasterEven && pageIdx % 2 == 1)
                    {
                        // MasterEven overlays pages 2, 4, 6… (0-indexed 1, 3, 5…)
                        shouldShow   = true;
                        shouldEnable = false;
                    }
                    else
                    {
                        shouldShow = shouldEnable = false;
                    }
                }

                foreach (var ctrl in kvp.Value)
                {
                    // Apply view-mode filter on top of page visibility
                    bool show = shouldShow;
                    if (show && _viewMode != "Braille & Print")
                    {
                        bool isBrailleCtrl = ctrl is BrailleTextBox;
                        bool isPrintCtrl   = ctrl is PrintTextBox || ctrl is ImageBox;
                        if (_viewMode == "Braille" && !isBrailleCtrl) show = false;
                        if (_viewMode == "Print"   && !isPrintCtrl)   show = false;
                    }

                    ctrl.Visible = show;
                    ctrl.Enabled = show && shouldEnable;
                    if (show && !shouldEnable)
                        ctrl.SendToBack();
                }
            }

            // Dispose controls whose regular page was deleted — never touch master pages
            var dead = new List<DocumentPage>();
            foreach (var page in _pageControls.Keys)
                if (page != Document.MasterOdd
                    && page != Document.MasterEven
                    && !Document.Pages.Contains(page))
                    dead.Add(page);

            foreach (var page in dead)
            {
                var snapshot = new List<Control>(_pageControls[page]);
                foreach (var ctrl in snapshot)
                {
                    Controls.Remove(ctrl);
                    ctrl.Dispose();
                }
                _pageControls.Remove(page);
            }

            UpdateScrollSize();
            Invalidate();
        }

        public void SetViewMode(string mode)
        {
            _viewMode = mode;
            PageChanged();
        }

        // ── Tools ─────────────────────────────────────────────────────────────

        public void ActivateTextTool()
        {
            var origin = PageOrigin();
            var page   = Document.CurrentPage;
            var loc    = new Point(
                (int)(origin.X + MmToPx(page.MarginLeft)),
                (int)(origin.Y + MmToPx(page.MarginTop)));
            var box = new PrintTextBox { Location = loc };
            RegisterControl(box);
            Controls.Add(box);
            box.BringToFront();
            box.Focus();
        }

        public void ActivateImageTool()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title  = "Select Image";
                dlg.Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tiff|All Files|*.*";
                if (dlg.ShowDialog() != DialogResult.OK) return;

                Image img;
                try   { img = Image.FromFile(dlg.FileName); }
                catch { MessageBox.Show("Could not load image file.", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

                var origin = PageOrigin();
                var page   = Document.CurrentPage;
                var loc    = new Point(
                    (int)(origin.X + MmToPx(page.MarginLeft)),
                    (int)(origin.Y + MmToPx(page.MarginTop)));

                var box = new ImageBox(img) { Location = loc };
                RegisterControl(box);
                Controls.Add(box);
                box.BringToFront();
                box.Focus();
            }
        }

        public void ActivateBrailleTool()
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in "abcde")
                sb.Append(BrailleMapper.ToBraille(c));

            var origin = PageOrigin();
            var page   = Document.CurrentPage;
            var loc    = new Point(
                (int)(origin.X + MmToPx(page.MarginLeft)),
                (int)(origin.Y + MmToPx(page.MarginTop)));

            var box = new BrailleTextBox
            {
                Location    = loc,
                BrailleText = sb.ToString()
            };
            RegisterControl(box);
            Controls.Add(box);
            box.BringToFront();
            box.Focus();
        }

        // ── Print rendering ───────────────────────────────────────────────────

        public void RenderPageToPrinter(Graphics g, DocumentPage page)
        {
            // Work in mm so font point-sizes and positions stay physically correct
            g.PageUnit = System.Drawing.GraphicsUnit.Millimeter;

            float screenPxPerMm = SCREEN_DPI / MM_PER_INCH * _zoom;
            float originX       = Math.Max(PAGE_PADDING, (ClientSize.Width - PageWidthPx) / 2f);
            float originY       = PAGE_PADDING;

            if (!_pageControls.TryGetValue(page, out var controls)) return;

            foreach (var ctrl in controls)
            {
                float mmX = (ctrl.Location.X - originX) / screenPxPerMm;
                float mmY = (ctrl.Location.Y - originY) / screenPxPerMm;
                float mmW = ctrl.Width  / screenPxPerMm;
                float mmH = ctrl.Height / screenPxPerMm;

                if (ctrl is PrintTextBox ptb)
                {
                    if (string.IsNullOrEmpty(ptb.DisplayText)) continue;
                    using (var font  = new Font(ptb.FontFamily, ptb.FontSizePt, GraphicsUnit.Point))
                    using (var brush = new SolidBrush(Color.Black))
                    {
                        var fmt = new StringFormat(StringFormat.GenericTypographic);
                        if (ptb.IsRightToLeft)
                            fmt.FormatFlags |= StringFormatFlags.DirectionRightToLeft;
                        g.DrawString(ptb.DisplayText, font, brush,
                            new RectangleF(mmX, mmY, mmW, mmH), fmt);
                    }
                }
                else if (ctrl is ImageBox ib && ib.SourceImage != null)
                {
                    g.DrawImage(ib.SourceImage, mmX, mmY, mmW, mmH);
                }
            }
        }

        // ── Emboss coordinate export ──────────────────────────────────────────

        public string BuildDotCoordinatesForPages(IEnumerable<DocumentPage> pages)
        {
            var   dots    = new List<string>();
            float pxPerMm = SCREEN_DPI / MM_PER_INCH * _zoom;
            float originX = Math.Max(PAGE_PADDING, (ClientSize.Width - PageWidthPx) / 2f);
            float originY = PAGE_PADDING;

            foreach (var page in pages)
            {
                if (!_pageControls.TryGetValue(page, out var controls)) continue;

                foreach (var ctrl in controls)
                {
                    if (!(ctrl is BrailleTextBox btb)) continue;

                    float baseXmm = (ctrl.Location.X - originX) / pxPerMm;
                    float baseYmm = (ctrl.Location.Y - originY) / pxPerMm;

                    CollectBrailleDots(dots, btb.BrailleText, baseXmm, baseYmm,
                        ctrl.Width / pxPerMm);
                }
            }

            if (dots.Count == 0) return string.Empty;
            return string.Join("\r\n", dots) + ";";
        }

        private static void CollectBrailleDots(List<string> dots,
            string text, float baseX, float baseY, float boxWidthMm)
        {
            float cellW = DocumentPage.CELL_WIDTH_MM;
            float lineH = DocumentPage.LINE_HEIGHT_MM;
            float dotS  = DocumentPage.DOT_SPACING_MM;
            int   col   = 0, row = 0;

            foreach (char c in text)
            {
                if ((int)c < 0x2800 || (int)c > 0x28FF) { col++; continue; }

                float ox = baseX + col * cellW;
                if (ox + cellW > baseX + boxWidthMm) { row++; col = 0; ox = baseX; }
                float oy = baseY + row * lineH;

                int bits = (int)c - 0x2800;
                for (int b = 0; b < 8; b++)
                {
                    if ((bits & (1 << b)) == 0) continue;
                    int dcol = b < 6 ? b / 3 : b - 6;
                    int drow = b < 6 ? b % 3 : 3;
                    float dotX = ox + dcol * dotS;
                    float dotY = oy + drow * dotS;
                    dots.Add(string.Format("{0:F2}:{1:F2}", dotX, dotY));
                }
                col++;
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            SelectionChanged?.Invoke(null); // nothing selected
        }

        // ── Resize ────────────────────────────────────────────────────────────

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateScrollSize();
            Invalidate();
        }
    }
}
