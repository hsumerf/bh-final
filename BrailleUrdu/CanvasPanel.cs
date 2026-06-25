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

        private MarginGuideOverlay _overlay;

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

        // ── Stack mode ────────────────────────────────────────────────────────
        public bool StackMode { get; set; } = false;

        // ── Multi-selection & rubber-band ────────────────────────────────────
        private readonly List<Control> _selectedControls = new List<Control>();
        public IReadOnlyList<Control> SelectedControls => _selectedControls;
        private bool  _rubberBanding;
        private Point _rubberStart;
        private Point _rubberEnd;

        // Tracks the page origin X so controls can be shifted when the window is resized
        private float _lastOriginX;

        // ── Edit state ────────────────────────────────────────────────────────
        private Control            _focused;
        private readonly List<string> _undoStack = new List<string>();
        private string             _clipboard;
        private const int          MAX_UNDO = 50;

        public CanvasPanel()
        {
            SetStyle(ControlStyles.Selectable, true);
            DoubleBuffered = true;
            BackColor      = Color.FromArgb(185, 185, 185);
            Dock           = DockStyle.Fill;
            AutoScroll     = true;
            TabStop        = true;
            UpdateScrollSize();
            SelectionChanged += ctrl => _focused = ctrl;
            _lastOriginX = PageOrigin().X;

            _overlay = new MarginGuideOverlay(this);
            Controls.Add(_overlay);
            ControlAdded += (s, ev) => { if (ev.Control != _overlay) _overlay?.BringToFront(); };
        }

        // ── Coordinate helpers ────────────────────────────────────────────────

        private float MmToPx(float mm) => mm * (SCREEN_DPI / MM_PER_INCH) * _zoom;

        public float PageWidthPx  => MmToPx(DocumentPage.WIDTH_MM);
        public float PageHeightPx => MmToPx(DocumentPage.HEIGHT_MM);

        private PointF PageOrigin() => new PointF(
            Math.Max(PAGE_PADDING, (ClientSize.Width - PageWidthPx) / 2f),
            PAGE_PADDING);

        public PointF PageOriginPx => PageOrigin();

        public RectangleF MarginBoundsPx
        {
            get {
                var page = Document.CurrentPage;
                var o    = PageOriginPx;
                return new RectangleF(
                    o.X + MmToPx(page.MarginLeft),
                    o.Y + MmToPx(page.MarginTop),
                    PageWidthPx  - MmToPx(page.MarginLeft) - MmToPx(page.MarginRight),
                    PageHeightPx - MmToPx(page.MarginTop)  - MmToPx(page.MarginBottom));
            }
        }

        private void UpdateScrollSize()
        {
            AutoScrollMinSize = new Size(
                (int)(PageWidthPx  + PAGE_PADDING * 2),
                (int)(PageHeightPx + PAGE_PADDING * 2));
        }

        // Prevent WinForms from auto-scrolling the canvas when a child control
        // gains focus (e.g. clicking a large TactileBox would jump the scroll).
        protected override Point ScrollToControl(Control activeControl)
            => DisplayRectangle.Location;

        protected override void OnScroll(ScrollEventArgs se)
        {
            base.OnScroll(se);
            _overlay?.Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            _overlay?.Invalidate();
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

            // Page border
            using (var border = new Pen(Color.FromArgb(190, 190, 190), 1))
                g.DrawRectangle(border, px, py, pw, ph);

            // Elements
            RenderElements(g, px, py);

            // Rubber-band selection rectangle
            if (_rubberBanding)
            {
                var r = ClientToContent(MakeClientRect(_rubberStart, _rubberEnd));
                if (r.Width > 2 || r.Height > 2)
                {
                    using (var fill = new SolidBrush(Color.FromArgb(40, 51, 153, 255)))
                        g.FillRectangle(fill, r);
                    using (var pen = new Pen(Color.DodgerBlue, 1f) { DashStyle = DashStyle.Dash })
                        g.DrawRectangle(pen, r);
                }
            }
        }

        private void DrawMarginGuides(Graphics g, float px, float py, float pw, float ph)
        {
            var page = Document.CurrentPage;
            using (var pen = new Pen(Color.Red, 2f))
            {
                float ml = MmToPx(page.MarginLeft);
                float mr = MmToPx(page.MarginRight);
                float mt = MmToPx(page.MarginTop);
                float mb = MmToPx(page.MarginBottom);

                g.DrawLine(pen, px + ml,      py,           px + ml,      py + ph); // left
                g.DrawLine(pen, px + pw - mr, py,           px + pw - mr, py + ph); // right
                g.DrawLine(pen, px,           py + mt,      px + pw,      py + mt); // top
                g.DrawLine(pen, px,           py + ph - mb, px + pw,      py + ph - mb); // bottom
            }
        }

        internal void PaintMarginOverlay(Graphics g)
        {
            if (Document.CurrentPage == null) return;
            g.SmoothingMode   = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            var origin = PageOrigin();
            DrawMarginGuides(g, origin.X, origin.Y, PageWidthPx, PageHeightPx);
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

        // ── Selection helpers ─────────────────────────────────────────────────

        private void ClearSelection()
        {
            foreach (var c in _selectedControls)
                SetSelected(c, false);
            _selectedControls.Clear();
        }

        private static void SetSelected(Control ctrl, bool selected)
        {
            if      (ctrl is BrailleTextBox b)   b.IsSelected   = selected;
            else if (ctrl is PrintTextBox p)      p.IsSelected   = selected;
            else if (ctrl is PageNumberBox pnb)   pnb.IsSelected = selected;
            else if (ctrl is LineBox lb)          lb.IsSelected  = selected;
            else if (ctrl is TableBox tab)        tab.IsSelected = selected;
            else if (ctrl is ImageBox ib)         ib.IsSelected  = selected;
            else if (ctrl is TactileBox tb)       tb.IsSelected  = selected;
            ctrl.Invalidate();
        }

        private static Rectangle MakeClientRect(Point a, Point b) => new Rectangle(
            Math.Min(a.X, b.X), Math.Min(a.Y, b.Y),
            Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

        private Rectangle ClientToContent(Rectangle r) => new Rectangle(
            r.X - AutoScrollPosition.X, r.Y - AutoScrollPosition.Y, r.Width, r.Height);

        // Fires whenever the user selects a different element (null = canvas clicked, nothing selected)
        public event Action<Control> SelectionChanged;

        // Fires whenever the document content changes (add, move, resize, edit, delete, undo)
        public event EventHandler DocumentChanged;

        // ── Page-control registry ─────────────────────────────────────────────

        private void RegisterControl(Control ctrl)
        {
            var page = Document.CurrentPage;
            if (!_pageControls.ContainsKey(page))
                _pageControls[page] = new List<Control>();
            _pageControls[page].Add(ctrl);

            ctrl.GotFocus += (s, e) =>
            {
                if (!_selectedControls.Contains(ctrl))
                {
                    ClearSelection();
                    if (!ctrl.IsDisposed) { _selectedControls.Add(ctrl); SetSelected(ctrl, true); }
                    SelectionChanged?.Invoke(ctrl);
                }
            };
            ctrl.LocationChanged += (s, e) => DocumentChanged?.Invoke(this, EventArgs.Empty);
            ctrl.SizeChanged     += (s, e) => DocumentChanged?.Invoke(this, EventArgs.Empty);

            ctrl.Disposed += (s, e) =>
            {
                if (_pageControls.TryGetValue(page, out var list))
                    list.Remove(ctrl);
            };
        }

        // Called whenever the active page changes (add / remove / select / master switch)
        public void PageChanged()
        {
            AutoScrollPosition = Point.Empty;

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
                        bool isBrailleCtrl = ctrl is BrailleTextBox || ctrl is TactileBox
                                             || (ctrl is PageNumberBox pnbB && pnbB.IsBraille);
                        bool isPrintCtrl   = ctrl is PrintTextBox  || ctrl is ImageBox
                                             || ctrl is LineBox || ctrl is TableBox
                                             || (ctrl is PageNumberBox pnbP && !pnbP.IsBraille);
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

            // Update page number displays on visible master overlays
            foreach (var kvp in _pageControls)
                foreach (var ctrl in kvp.Value)
                    if (ctrl is PageNumberBox pnb && ctrl.Visible)
                        pnb.UpdateNumber(isOnMaster ? -1 : pageIdx);

            ClearSelection();
            UpdateScrollSize();
            _overlay?.BringToFront();
            Invalidate();
            _overlay?.Invalidate();
        }

        public void SetViewMode(string mode)
        {
            _viewMode = mode;
            PageChanged();
        }

        // Returns false when the current view mode should suppress this control from rendering.
        // Mirrors the same filter applied to ctrl.Visible in PageChanged().
        private bool ShouldRender(Control ctrl)
        {
            if (_viewMode == "Braille & Print") return true;
            bool isBraille = ctrl is BrailleTextBox || ctrl is TactileBox
                             || (ctrl is PageNumberBox pnbB && pnbB.IsBraille);
            bool isPrint   = ctrl is PrintTextBox   || ctrl is ImageBox
                             || ctrl is LineBox || ctrl is TableBox
                             || (ctrl is PageNumberBox pnbP && !pnbP.IsBraille);
            if (_viewMode == "Braille") return isBraille;
            if (_viewMode == "Print")   return isPrint;
            return true;
        }

        // ── Tools ─────────────────────────────────────────────────────────────

        // When StackMode is on, returns a location directly below the lowest
        // PrintTextBox or BrailleTextBox on the current page; falls back to
        // the margin top-left when no such box exists yet.
        private Point StackLocation()
        {
            var origin = PageOrigin();
            var page   = Document.CurrentPage;
            int defaultX = (int)(origin.X + MmToPx(page.MarginLeft));
            int defaultY = (int)(origin.Y + MmToPx(page.MarginTop));

            if (!_pageControls.TryGetValue(page, out var controls))
                return new Point(defaultX, defaultY);

            Control lowest  = null;
            int     maxBot  = int.MinValue;
            foreach (var ctrl in controls)
            {
                if (!(ctrl is PrintTextBox || ctrl is BrailleTextBox)) continue;
                int bot = ctrl.Bottom;
                if (bot > maxBot) { maxBot = bot; lowest = ctrl; }
            }

            return lowest != null
                ? new Point(lowest.Left, lowest.Bottom)
                : new Point(defaultX, defaultY);
        }

        public void ActivateTextTool()
        {
            var origin = PageOrigin();
            var page   = Document.CurrentPage;
            var loc    = StackMode
                ? StackLocation()
                : new Point(
                    (int)(origin.X + MmToPx(page.MarginLeft)),
                    (int)(origin.Y + MmToPx(page.MarginTop)));
            var box = new PrintTextBox { Location = loc };
            RegisterControl(box);
            Controls.Add(box);
            box.BringToFront();
            box.Focus();
            DocumentChanged?.Invoke(this, EventArgs.Empty);
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
                DocumentChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void ActivateBrailleTool()
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in "abcde")
                sb.Append(BrailleMapper.ToBraille(c));

            var origin = PageOrigin();
            var page   = Document.CurrentPage;
            var loc    = StackMode
                ? StackLocation()
                : new Point(
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
            DocumentChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ActivateLineTool()
        {
            var origin = PageOrigin();
            var page   = Document.CurrentPage;
            int x = (int)(origin.X + MmToPx(page.MarginLeft));
            int y = (int)(origin.Y + MmToPx(page.MarginTop));
            int w = (int)(PageWidthPx - MmToPx(page.MarginLeft) - MmToPx(page.MarginRight));

            var box = new LineBox { Location = new Point(x, y), Size = new Size(w, 20) };
            RegisterControl(box);
            Controls.Add(box);
            box.BringToFront();
            box.Focus();
            DocumentChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ActivateTableTool()
        {
            var origin = PageOrigin();
            var page   = Document.CurrentPage;
            int x = (int)(origin.X + MmToPx(page.MarginLeft));
            int y = (int)(origin.Y + MmToPx(page.MarginTop));
            int w = (int)(PageWidthPx - MmToPx(page.MarginLeft) - MmToPx(page.MarginRight));

            var box = new TableBox
            {
                Location = new Point(x, y),
                Size     = new Size(w, 80),
                RowSpec  = "1-1",
                ColSpec  = "1-1"
            };
            RegisterControl(box);
            Controls.Add(box);
            box.BringToFront();
            box.Focus();
            DocumentChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ActivateTactileTool()
        {
            using (var dlg = new TactileEditorDialog())
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;

                var origin = PageOrigin();
                var page   = Document.CurrentPage;
                var loc    = new Point(
                    (int)(origin.X + MmToPx(page.MarginLeft)),
                    (int)(origin.Y + MmToPx(page.MarginTop)));

                var box = new TactileBox { Location = loc, DotGrid = dlg.ResultGrid };
                RegisterControl(box);
                Controls.Add(box);
                box.BringToFront();
                box.Focus();
                DocumentChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void InsertPageNumber(bool braille)
        {
            var origin = PageOrigin();

            foreach (var masterPage in new[] { Document.MasterOdd, Document.MasterEven })
            {
                if (_pageControls.TryGetValue(masterPage, out var existing) &&
                    existing.Exists(c => c is PageNumberBox pnbEx && pnbEx.IsBraille == braille))
                    continue; // already has one of this type

                var pnb = new PageNumberBox { IsBraille = braille };
                int x   = (int)(origin.X + MmToPx(15f) - pnb.Width);
                int y   = (int)(origin.Y + MmToPx(15f) - pnb.Height);
                pnb.Location = new Point(Math.Max(0, x), Math.Max(0, y));

                var pg = masterPage; // explicit capture for closures
                if (!_pageControls.ContainsKey(pg))
                    _pageControls[pg] = new List<Control>();
                _pageControls[pg].Add(pnb);

                pnb.GotFocus += (s, e) =>
                {
                    if (!_selectedControls.Contains(pnb))
                    {
                        ClearSelection();
                        if (!pnb.IsDisposed) { _selectedControls.Add(pnb); SetSelected(pnb, true); }
                        SelectionChanged?.Invoke(pnb);
                    }
                };
                pnb.LocationChanged += (s, e) => DocumentChanged?.Invoke(this, EventArgs.Empty);
                pnb.SizeChanged     += (s, e) => DocumentChanged?.Invoke(this, EventArgs.Empty);
                pnb.Disposed        += (s, e) =>
                {
                    if (_pageControls.TryGetValue(pg, out var list))
                        list.Remove(pnb);
                };

                Controls.Add(pnb);
            }

            PageChanged();
            DocumentChanged?.Invoke(this, EventArgs.Empty);
        }

        // ── Print rendering ───────────────────────────────────────────────────

        public void RenderPageToPrinter(Graphics g, DocumentPage page)
        {
            // Work in mm so positions and font sizes are physically correct on paper
            g.PageUnit       = System.Drawing.GraphicsUnit.Millimeter;
            g.SmoothingMode  = SmoothingMode.AntiAlias;

            float screenPxPerMm = SCREEN_DPI / MM_PER_INCH * _zoom;
            float originX       = Math.Max(PAGE_PADDING, (ClientSize.Width - PageWidthPx) / 2f);
            float originY       = PAGE_PADDING;

            if (!_pageControls.TryGetValue(page, out var controls)) return;

            int pageIdx  = Document.Pages.IndexOf(page);
            var master   = (pageIdx % 2 == 0) ? Document.MasterOdd : Document.MasterEven;
            var other    = (pageIdx % 2 == 0) ? Document.MasterEven : Document.MasterOdd;
            var allCtrls = new System.Collections.Generic.List<Control>(controls);
            if (_pageControls.TryGetValue(master, out var masterCtrls))
                allCtrls.AddRange(masterCtrls);
            // If the primary master has no page number of a given type, pull it from the other master.
            // Handles documents saved before page numbers were mirrored to both masters.
            if (_pageControls.TryGetValue(other, out var otherCtrls))
                foreach (var c in otherCtrls)
                {
                    if (!(c is PageNumberBox pnbFb)) continue;
                    bool have = false;
                    foreach (var ec in allCtrls)
                        if (ec is PageNumberBox ep && ep.IsBraille == pnbFb.IsBraille) { have = true; break; }
                    if (!have) allCtrls.Add(c);
                }

            // AutoScroll only repositions controls that have a Win32 HWND.
            // Invisible controls without handles stay at logical positions.
            int scrollDX = AutoScrollPosition.X;
            int scrollDY = AutoScrollPosition.Y;

            foreach (var ctrl in allCtrls)
            {
                if (!ShouldRender(ctrl)) continue;

                int   sdx = ctrl.IsHandleCreated ? scrollDX : 0;
                int   sdy = ctrl.IsHandleCreated ? scrollDY : 0;
                float mmX = ((ctrl.Location.X - sdx) - originX) / screenPxPerMm;
                float mmY = ((ctrl.Location.Y - sdy) - originY) / screenPxPerMm;
                float mmW = ctrl.Width  / screenPxPerMm;
                float mmH = ctrl.Height / screenPxPerMm;
                var   rc  = new RectangleF(mmX, mmY, mmW, mmH);

                if (ctrl is PrintTextBox ptb)
                {
                    if (!ptb.FillTransparent)
                        using (var b = new SolidBrush(ptb.FillColor))
                            g.FillRectangle(b, rc);

                    if (!string.IsNullOrEmpty(ptb.DisplayText))
                        using (var font  = new Font(ptb.FontFamily, ptb.FontSizePt,
                                                    ptb.TextFontStyle, GraphicsUnit.Point))
                        using (var brush = new SolidBrush(ptb.TextColor))
                        {
                            var fmt = new StringFormat
                            {
                                Alignment     = ptb.HTextAlign,
                                LineAlignment = ptb.VTextAlign
                            };
                            if (ptb.IsRightToLeft)
                                fmt.FormatFlags |= StringFormatFlags.DirectionRightToLeft;
                            g.DrawString(ptb.DisplayText, font, brush, rc, fmt);
                        }

                    if (ptb.BorderWidth > 0)
                        using (var pen = new Pen(ptb.BorderColor,
                                                 ptb.BorderWidth / screenPxPerMm))
                        {
                            if (ptb.BorderTop)
                                g.DrawLine(pen, rc.Left,  rc.Top,    rc.Right, rc.Top);
                            if (ptb.BorderBottom)
                                g.DrawLine(pen, rc.Left,  rc.Bottom, rc.Right, rc.Bottom);
                            if (ptb.BorderLeft)
                                g.DrawLine(pen, rc.Left,  rc.Top,    rc.Left,  rc.Bottom);
                            if (ptb.BorderRight)
                                g.DrawLine(pen, rc.Right, rc.Top,    rc.Right, rc.Bottom);
                        }
                }
                else if (ctrl is BrailleTextBox btb && !string.IsNullOrEmpty(btb.BrailleText))
                {
                    // SimBraille: size the em-height to one braille line (with same 0.72 scale as bitmap renderer)
                    float brailleMm = DocumentPage.LINE_HEIGHT_MM * 0.72f;
                    using (var font  = new Font("SimBraille", brailleMm, GraphicsUnit.Millimeter))
                    using (var brush = new SolidBrush(Color.Black))
                        g.DrawString(btb.BrailleText, font, brush, rc);
                }
                else if (ctrl is ImageBox ib && ib.SourceImage != null)
                {
                    g.DrawImage(ib.SourceImage, rc);
                }
                else if (ctrl is TactileBox tb && tb.DotGrid != null)
                {
                    int   cols   = tb.DotGrid.GetLength(0);
                    int   rows   = tb.DotGrid.GetLength(1);
                    float dotS   = DocumentPage.DOT_SPACING_MM;
                    float radius = dotS * 0.35f;
                    using (var brush = new SolidBrush(Color.Black))
                        for (int r = 0; r < rows; r++)
                        for (int c = 0; c < cols; c++)
                            if (tb.DotGrid[c, r])
                                g.FillEllipse(brush,
                                    mmX + c * dotS - radius,
                                    mmY + r * dotS - radius,
                                    radius * 2, radius * 2);
                }
                else if (ctrl is LineBox lb)
                {
                    float thick = Math.Max(0.1f, lb.LineThickness / screenPxPerMm);
                    using (var pen = new Pen(lb.LineColor, thick))
                    {
                        if (lb.Direction == LineBox.LineDirection.Horizontal)
                            g.DrawLine(pen, mmX, mmY + mmH / 2f, mmX + mmW, mmY + mmH / 2f);
                        else
                            g.DrawLine(pen, mmX + mmW / 2f, mmY, mmX + mmW / 2f, mmY + mmH);
                    }
                }
                else if (ctrl is TableBox tab)
                {
                    float thick = Math.Max(0.1f, tab.LineThickness / screenPxPerMm);
                    using (var pen = new Pen(tab.LineColor, thick))
                    {
                        g.DrawRectangle(pen, mmX, mmY, mmW, mmH);

                        var rows   = tab.RowSizes;
                        float sumR = 0; foreach (var v in rows) sumR += v;
                        float yOff = 0;
                        for (int i = 0; i < rows.Length - 1; i++)
                        {
                            yOff += rows[i] / sumR * mmH;
                            g.DrawLine(pen, mmX, mmY + yOff, mmX + mmW, mmY + yOff);
                        }

                        var cols   = tab.ColSizes;
                        float sumC = 0; foreach (var v in cols) sumC += v;
                        float xOff = 0;
                        for (int i = 0; i < cols.Length - 1; i++)
                        {
                            xOff += cols[i] / sumC * mmW;
                            g.DrawLine(pen, mmX + xOff, mmY, mmX + xOff, mmY + mmH);
                        }
                    }
                }
                else if (ctrl is PageNumberBox pnb)
                {
                    if (pnb.IsBraille)
                    {
                        string bt = pnb.GetBrailleForPage(pageIdx);
                        if (!string.IsNullOrEmpty(bt))
                        {
                            float brailleMm = DocumentPage.LINE_HEIGHT_MM * 0.72f;
                            using (var font  = new Font("SimBraille", brailleMm, GraphicsUnit.Millimeter))
                            using (var brush = new SolidBrush(Color.Black))
                                g.DrawString(bt, font, brush, rc);
                        }
                    }
                    else
                    {
                        using (var font  = new Font("Segoe UI", 9.5f, GraphicsUnit.Point))
                        using (var brush = new SolidBrush(Color.Black))
                        {
                            var fmt = new StringFormat
                            {
                                Alignment     = StringAlignment.Center,
                                LineAlignment = StringAlignment.Center
                            };
                            g.DrawString((pageIdx + 1).ToString(), font, brush, rc, fmt);
                        }
                    }
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
                    float baseXmm = (ctrl.Location.X - originX) / pxPerMm;
                    float baseYmm = (ctrl.Location.Y - originY) / pxPerMm;

                    if (ctrl is BrailleTextBox btb)
                    {
                        CollectBrailleDots(dots, btb.BrailleText, baseXmm, baseYmm,
                            ctrl.Width / pxPerMm);
                    }
                    else if (ctrl is TactileBox tb && tb.DotGrid != null)
                    {
                        CollectTactileDots(dots, tb.DotGrid, baseXmm, baseYmm,
                            ctrl.Width / pxPerMm, ctrl.Height / pxPerMm);
                    }
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

        private static void CollectTactileDots(List<string> dots,
            bool[,] grid, float baseX, float baseY, float boxWidthMm, float boxHeightMm)
        {
            int   cols = grid.GetLength(0);
            int   rows = grid.GetLength(1);
            float dotS = DocumentPage.DOT_SPACING_MM;
            // Scale grid dots to physical box size
            float stepX = cols > 1 ? boxWidthMm  / (cols - 1) : dotS;
            float stepY = rows > 1 ? boxHeightMm / (rows - 1) : dotS;

            for (int c = 0; c < cols; c++)
            for (int r = 0; r < rows; r++)
            {
                if (!grid[c, r]) continue;
                dots.Add(string.Format("{0:F2}:{1:F2}",
                    baseX + c * stepX,
                    baseY + r * stepY));
            }
        }

        // ── Edit operations ───────────────────────────────────────────────────

        private void PushUndo()
        {
            _undoStack.Add(DocumentSerializer.SnapshotPage(Document.CurrentPage, this));
            if (_undoStack.Count > MAX_UNDO) _undoStack.RemoveAt(0);
            DocumentChanged?.Invoke(this, EventArgs.Empty);
        }

        public void EditUndo()
        {
            if (_undoStack.Count == 0) return;
            string snap = _undoStack[_undoStack.Count - 1];
            _undoStack.RemoveAt(_undoStack.Count - 1);
            DocumentSerializer.RestorePageSnapshot(snap, Document.CurrentPage, this);
            _focused = null;
            SelectionChanged?.Invoke(null);
            DocumentChanged?.Invoke(this, EventArgs.Empty);
        }

        // Returns all selected controls when a multi-selection exists,
        // otherwise the single focused control, otherwise an empty list.
        private List<Control> SelectedTargets()
        {
            if (_selectedControls.Count > 0)
                return new List<Control>(_selectedControls);
            if (_focused != null && !_focused.IsDisposed)
                return new List<Control> { _focused };
            return new List<Control>();
        }

        public void EditDelete()
        {
            var targets = SelectedTargets();
            if (targets.Count == 0) return;
            PushUndo();
            _selectedControls.Clear();
            _focused = null;
            SelectionChanged?.Invoke(null);
            foreach (var ctrl in targets) { Controls.Remove(ctrl); ctrl.Dispose(); }
        }

        public void EditCut()
        {
            var targets = SelectedTargets();
            if (targets.Count == 0) return;
            _clipboard = DocumentSerializer.SerializeControls(targets, this);
            PushUndo();
            _selectedControls.Clear();
            _focused = null;
            SelectionChanged?.Invoke(null);
            foreach (var ctrl in targets) { Controls.Remove(ctrl); ctrl.Dispose(); }
        }

        public void EditCopy()
        {
            var targets = SelectedTargets();
            if (targets.Count == 0) return;
            _clipboard = DocumentSerializer.SerializeControls(targets, this);
        }

        public void EditPaste()
        {
            if (string.IsNullOrEmpty(_clipboard)) return;
            PushUndo();
            var ctrls     = DocumentSerializer.DeserializeControls(_clipboard, this);
            Control last  = null;
            foreach (var ctrl in ctrls)
            {
                ctrl.Location = new Point(ctrl.Location.X + 20, ctrl.Location.Y + 20);
                RegisterControl(ctrl);
                Controls.Add(ctrl);
                ctrl.BringToFront();
                last = ctrl;
            }
            if (last != null) last.Focus();
        }

        public void EditDuplicate()
        {
            var targets = SelectedTargets();
            if (targets.Count == 0) return;
            PushUndo();
            string xml    = DocumentSerializer.SerializeControls(targets, this);
            var    ctrls  = DocumentSerializer.DeserializeControls(xml, this);
            Control last  = null;
            foreach (var ctrl in ctrls)
            {
                ctrl.Location = new Point(ctrl.Location.X + 20, ctrl.Location.Y + 20);
                RegisterControl(ctrl);
                Controls.Add(ctrl);
                ctrl.BringToFront();
                last = ctrl;
            }
            if (last != null) last.Focus();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            ClearSelection();
            SelectionChanged?.Invoke(null);
            _rubberStart   = e.Location;
            _rubberEnd     = e.Location;
            _rubberBanding = true;
            Capture        = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_rubberBanding) return;
            _rubberEnd = e.Location;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!_rubberBanding) return;
            _rubberBanding = false;
            Capture        = false;
            var rubber = MakeClientRect(_rubberStart, e.Location);
            if (rubber.Width > 4 || rubber.Height > 4)
            {
                var content = ClientToContent(rubber);
                foreach (Control ctrl in Controls)
                {
                    if (!ctrl.Visible || !ctrl.Enabled) continue;
                    if (!(ctrl is BrailleTextBox || ctrl is PrintTextBox || ctrl is PageNumberBox
                          || ctrl is LineBox || ctrl is TableBox || ctrl is ImageBox || ctrl is TactileBox)) continue;
                    if (!content.IntersectsWith(ctrl.Bounds)) continue;
                    SetSelected(ctrl, true);
                    if (!_selectedControls.Contains(ctrl)) _selectedControls.Add(ctrl);
                }
                if (_selectedControls.Count == 1)
                    SelectionChanged?.Invoke(_selectedControls[0]);
                else if (_selectedControls.Count > 1)
                    SelectionChanged?.Invoke(null);
            }
            Invalidate();
        }

        // ── Serialization helpers ─────────────────────────────────────────────

        // px per mm at the current zoom and screen DPI
        public float PxPerMm => MmToPx(1f);

        // Returns a snapshot of the controls registered for a given page.
        public System.Collections.Generic.IEnumerable<System.Windows.Forms.Control>
            GetControlsForPage(DocumentPage page)
        {
            return _pageControls.TryGetValue(page, out var list)
                ? list.ToArray()
                : new System.Windows.Forms.Control[0];
        }

        // Registers and adds a control for a specific page (used when loading a file).
        public void LoadControlForPage(DocumentPage page,
                                       System.Windows.Forms.Control ctrl)
        {
            if (!_pageControls.ContainsKey(page))
                _pageControls[page] = new List<System.Windows.Forms.Control>();
            _pageControls[page].Add(ctrl);

            ctrl.GotFocus += (s, e) =>
            {
                if (!_selectedControls.Contains(ctrl))
                {
                    ClearSelection();
                    if (!ctrl.IsDisposed) { _selectedControls.Add(ctrl); SetSelected(ctrl, true); }
                    SelectionChanged?.Invoke(ctrl);
                }
            };
            ctrl.LocationChanged += (s, e) => DocumentChanged?.Invoke(this, EventArgs.Empty);
            ctrl.SizeChanged     += (s, e) => DocumentChanged?.Invoke(this, EventArgs.Empty);
            ctrl.Disposed        += (s, e) =>
            {
                if (_pageControls.TryGetValue(page, out var list))
                    list.Remove(ctrl);
            };

            ctrl.Visible = false; // PageChanged() sets correct visibility
            Controls.Add(ctrl);
        }

        // Returns every BrailleTextBox across all pages (regular + masters).
        public System.Collections.Generic.IEnumerable<BrailleTextBox> GetAllBrailleBoxes()
        {
            var result = new List<BrailleTextBox>();
            foreach (var page in Document.Pages)
                if (_pageControls.TryGetValue(page, out var ctrls))
                    foreach (var c in ctrls)
                        if (c is BrailleTextBox b) result.Add(b);
            foreach (var master in new[] { Document.MasterOdd, Document.MasterEven })
                if (master != null && _pageControls.TryGetValue(master, out var ctrls))
                    foreach (var c in ctrls)
                        if (c is BrailleTextBox b) result.Add(b);
            return result;
        }

        // Returns every PrintTextBox across all pages (regular + masters).
        public System.Collections.Generic.IEnumerable<PrintTextBox> GetAllPrintBoxes()
        {
            var result = new List<PrintTextBox>();
            foreach (var page in Document.Pages)
                if (_pageControls.TryGetValue(page, out var ctrls))
                    foreach (var c in ctrls)
                        if (c is PrintTextBox p) result.Add(p);
            foreach (var master in new[] { Document.MasterOdd, Document.MasterEven })
                if (master != null && _pageControls.TryGetValue(master, out var ctrls))
                    foreach (var c in ctrls)
                        if (c is PrintTextBox p) result.Add(p);
            return result;
        }

        // Removes and disposes all managed controls (called before loading a new file).
        public void ClearAll()
        {
            var all = new List<System.Windows.Forms.Control>();
            foreach (var kvp in _pageControls)
                all.AddRange(kvp.Value);
            _pageControls.Clear();
            foreach (var ctrl in all)
            {
                if (ctrl.Parent == this) Controls.Remove(ctrl);
                if (!ctrl.IsDisposed) ctrl.Dispose();
            }
            _selectedControls.Clear();
            _lastOriginX = PageOrigin().X;
        }

        // Renders a page's controls to a new Bitmap (page size at current zoom).
        public System.Drawing.Bitmap RenderPageToBitmap(DocumentPage page)
        {
            int pw  = Math.Max(1, (int)Math.Ceiling(PageWidthPx));
            int ph  = Math.Max(1, (int)Math.Ceiling(PageHeightPx));
            var bmp = new System.Drawing.Bitmap(pw, ph);

            _pageControls.TryGetValue(page, out var pageControls);
            int pageIdx  = Document.Pages.IndexOf(page);
            var master   = (pageIdx % 2 == 0) ? Document.MasterOdd : Document.MasterEven;
            var other    = (pageIdx % 2 == 0) ? Document.MasterEven : Document.MasterOdd;
            var allCtrls = new System.Collections.Generic.List<Control>(
                pageControls ?? new System.Collections.Generic.List<Control>());
            if (_pageControls.TryGetValue(master, out var masterCtrls2)) allCtrls.AddRange(masterCtrls2);
            if (_pageControls.TryGetValue(other, out var otherCtrls2))
                foreach (var c in otherCtrls2)
                {
                    if (!(c is PageNumberBox pnbFb)) continue;
                    bool have = false;
                    foreach (var ec in allCtrls)
                        if (ec is PageNumberBox ep && ep.IsBraille == pnbFb.IsBraille) { have = true; break; }
                    if (!have) allCtrls.Add(c);
                }

            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.Clear(System.Drawing.Color.White);
                g.SmoothingMode = SmoothingMode.AntiAlias;

                var   origin  = PageOrigin();
                float pxPerMm = MmToPx(1f);

                // AutoScroll only physically repositions controls that have a Win32 HWND.
                // Invisible controls that have never been shown have no handle and stay
                // at their logical positions, so only subtract the scroll offset for
                // handle-bearing controls.
                int scrollDX = AutoScrollPosition.X;
                int scrollDY = AutoScrollPosition.Y;

                foreach (var ctrl in allCtrls)
                {
                    if (!ShouldRender(ctrl)) continue;

                    int   sdx = ctrl.IsHandleCreated ? scrollDX : 0;
                    int   sdy = ctrl.IsHandleCreated ? scrollDY : 0;
                    float x  = (ctrl.Location.X - sdx) - origin.X;
                    float y  = (ctrl.Location.Y - sdy) - origin.Y;
                    var   rc = new System.Drawing.RectangleF(x, y, ctrl.Width, ctrl.Height);

                    if (ctrl is PrintTextBox ptb)
                    {
                        if (!ptb.FillTransparent)
                            using (var b = new System.Drawing.SolidBrush(ptb.FillColor))
                                g.FillRectangle(b, rc);

                        if (!string.IsNullOrEmpty(ptb.DisplayText))
                        {
                            using (var font  = new System.Drawing.Font(ptb.FontFamily,
                                                   ptb.FontSizePt, ptb.TextFontStyle,
                                                   System.Drawing.GraphicsUnit.Point))
                            using (var brush = new System.Drawing.SolidBrush(ptb.TextColor))
                            {
                                var fmt = new System.Drawing.StringFormat
                                {
                                    Alignment     = ptb.HTextAlign,
                                    LineAlignment = ptb.VTextAlign
                                };
                                if (ptb.IsRightToLeft)
                                    fmt.FormatFlags |= System.Drawing.StringFormatFlags.DirectionRightToLeft;
                                g.DrawString(ptb.DisplayText, font, brush, rc, fmt);
                            }
                        }

                        if (ptb.BorderWidth > 0)
                            using (var pen = new System.Drawing.Pen(ptb.BorderColor, ptb.BorderWidth))
                            {
                                if (ptb.BorderTop)
                                    g.DrawLine(pen, rc.Left, rc.Top,    rc.Right, rc.Top);
                                if (ptb.BorderBottom)
                                    g.DrawLine(pen, rc.Left, rc.Bottom, rc.Right, rc.Bottom);
                                if (ptb.BorderLeft)
                                    g.DrawLine(pen, rc.Left, rc.Top,    rc.Left,  rc.Bottom);
                                if (ptb.BorderRight)
                                    g.DrawLine(pen, rc.Right, rc.Top,   rc.Right, rc.Bottom);
                            }
                    }
                    else if (ctrl is BrailleTextBox btb && !string.IsNullOrEmpty(btb.BrailleText))
                    {
                        float ptSize = MmToPx(DocumentPage.LINE_HEIGHT_MM) * 0.72f;
                        using (var font  = new System.Drawing.Font("SimBraille",
                                               Math.Max(1f, ptSize),
                                               System.Drawing.GraphicsUnit.Pixel))
                        using (var brush = new System.Drawing.SolidBrush(System.Drawing.Color.Black))
                            g.DrawString(btb.BrailleText, font, brush, rc);
                    }
                    else if (ctrl is ImageBox ib && ib.SourceImage != null)
                    {
                        g.DrawImage(ib.SourceImage, rc);
                    }
                    else if (ctrl is TactileBox tb && tb.DotGrid != null)
                    {
                        int   cols   = tb.DotGrid.GetLength(0);
                        int   rows   = tb.DotGrid.GetLength(1);
                        float sp     = DocumentPage.DOT_SPACING_MM * pxPerMm;
                        float radius = sp * 0.35f;
                        using (var brush = new System.Drawing.SolidBrush(System.Drawing.Color.Black))
                            for (int row = 0; row < rows; row++)
                            for (int col = 0; col < cols; col++)
                                if (tb.DotGrid[col, row])
                                    g.FillEllipse(brush,
                                        x + col * sp - radius, y + row * sp - radius,
                                        radius * 2, radius * 2);
                    }
                    else if (ctrl is LineBox lb)
                    {
                        float thick = System.Math.Max(1f, lb.LineThickness);
                        using (var pen = new System.Drawing.Pen(lb.LineColor, thick))
                        {
                            if (lb.Direction == LineBox.LineDirection.Horizontal)
                                g.DrawLine(pen, x, y + rc.Height / 2f, x + rc.Width, y + rc.Height / 2f);
                            else
                                g.DrawLine(pen, x + rc.Width / 2f, y, x + rc.Width / 2f, y + rc.Height);
                        }
                    }
                    else if (ctrl is TableBox tab)
                    {
                        float thick = System.Math.Max(1f, tab.LineThickness);
                        using (var pen = new System.Drawing.Pen(tab.LineColor, thick))
                        {
                            g.DrawRectangle(pen, x, y, rc.Width, rc.Height);

                            var rowSizes = tab.RowSizes;
                            float sumR = 0; foreach (var v in rowSizes) sumR += v;
                            float yOff = 0;
                            for (int i = 0; i < rowSizes.Length - 1; i++)
                            {
                                yOff += rowSizes[i] / sumR * rc.Height;
                                g.DrawLine(pen, x, y + yOff, x + rc.Width, y + yOff);
                            }

                            var colSizes = tab.ColSizes;
                            float sumC = 0; foreach (var v in colSizes) sumC += v;
                            float xOff = 0;
                            for (int i = 0; i < colSizes.Length - 1; i++)
                            {
                                xOff += colSizes[i] / sumC * rc.Width;
                                g.DrawLine(pen, x + xOff, y, x + xOff, y + rc.Height);
                            }
                        }
                    }
                    else if (ctrl is PageNumberBox pnb)
                    {
                        if (pnb.IsBraille)
                        {
                            string bt = pnb.GetBrailleForPage(pageIdx);
                            if (!string.IsNullOrEmpty(bt))
                            {
                                float braillePx = DocumentPage.LINE_HEIGHT_MM * (96f / 25.4f) * 0.72f;
                                using (var font  = new System.Drawing.Font("SimBraille", braillePx,
                                                       System.Drawing.GraphicsUnit.Pixel))
                                using (var brush = new System.Drawing.SolidBrush(System.Drawing.Color.Black))
                                    g.DrawString(bt, font, brush, rc);
                            }
                        }
                        else
                        {
                            string num = pageIdx >= 0 ? (pageIdx + 1).ToString() : "#";
                            using (var font  = new System.Drawing.Font("Segoe UI", 9.5f,
                                                   System.Drawing.GraphicsUnit.Point))
                            using (var brush = new System.Drawing.SolidBrush(System.Drawing.Color.Black))
                            {
                                var fmt = new System.Drawing.StringFormat
                                {
                                    Alignment     = System.Drawing.StringAlignment.Center,
                                    LineAlignment = System.Drawing.StringAlignment.Center
                                };
                                g.DrawString(num, font, brush, rc, fmt);
                            }
                        }
                    }
                }
            }

            return bmp;
        }

        // ── Resize ────────────────────────────────────────────────────────────

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            float newOriginX = PageOrigin().X;
            float dx         = newOriginX - _lastOriginX;
            if (Math.Abs(dx) > 0.5f)
            {
                foreach (var kvp in _pageControls)
                    foreach (var ctrl in kvp.Value)
                        ctrl.Location = new Point((int)(ctrl.Location.X + dx), ctrl.Location.Y);
            }
            _lastOriginX = newOriginX;
            UpdateScrollSize();
            Invalidate();
        }

        // ── Margin guide overlay ──────────────────────────────────────────────
        // Transparent control that always sits at the top of the Z-order so the
        // red margin lines are painted OVER every content HWND (images, text
        // boxes, tactile boxes, etc.).

        private sealed class MarginGuideOverlay : Control
        {
            private readonly CanvasPanel _owner;

            public MarginGuideOverlay(CanvasPanel owner)
            {
                _owner  = owner;
                Dock    = DockStyle.Fill;
                TabStop = false;
                SetStyle(ControlStyles.UserPaint              |
                         ControlStyles.AllPaintingInWmPaint   |
                         ControlStyles.SupportsTransparentBackColor, true);
                SetStyle(ControlStyles.OptimizedDoubleBuffer, false);
                BackColor = Color.Transparent; // must follow SupportsTransparentBackColor
            }

            protected override CreateParams CreateParams
            {
                get
                {
                    var cp = base.CreateParams;
                    cp.ExStyle |= 0x20; // WS_EX_TRANSPARENT — paint after siblings, see through them
                    return cp;
                }
            }

            // Return HTTRANSPARENT so all mouse events fall through to controls below.
            protected override void WndProc(ref Message m)
            {
                if (m.Msg == 0x84) { m.Result = (IntPtr)(-1); return; } // WM_NCHITTEST
                base.WndProc(ref m);
            }

            protected override void OnPaintBackground(PaintEventArgs e) { }

            protected override void OnPaint(PaintEventArgs e)
            {
                _owner.PaintMarginOverlay(e.Graphics);
            }
        }
    }
}
