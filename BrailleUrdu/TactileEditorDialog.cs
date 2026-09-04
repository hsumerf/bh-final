using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BrailleUrdu
{
    public class TactileEditorDialog : Form
    {
        private readonly bool[,] _dots;
        private readonly int     _cols, _rows;

        private bool[,]  _sourceDots;   // snapshot used for proportional scaling

        private DotGridPanel _gridView;
        private Panel        _scroll;
        private TrackBar     _sizeSlider;
        private Label        _sizePctLabel;
        private Label        _lblSize;
        private bool         _fitting;
        private Panel        _sliderRow;

        public bool[,] ResultGrid => TrimDots();

        public event Action<bool[,]> ConfirmClicked;

        public TactileEditorDialog()
        {
            // Full embossable area at braille dot spacing
            var page = Document.CurrentPage;
            float ew = DocumentPage.WIDTH_MM  - page.MarginLeft - page.MarginRight;
            float eh = DocumentPage.HEIGHT_MM - page.MarginTop  - page.MarginBottom;
            _cols = Math.Max(4, (int)(ew / DocumentPage.DOT_SPACING_MM));
            _rows = Math.Max(4, (int)(eh / DocumentPage.DOT_SPACING_MM));
            _dots = new bool[_cols, _rows];

            BuildUI();
        }

        private bool[,] TrimDots()
        {
            int minC = _cols, maxC = -1, minR = _rows, maxR = -1;
            for (int c = 0; c < _cols; c++)
            for (int r = 0; r < _rows; r++)
            {
                if (!_dots[c, r]) continue;
                if (c < minC) minC = c;
                if (c > maxC) maxC = c;
                if (r < minR) minR = r;
                if (r > maxR) maxR = r;
            }

            if (maxC < 0) return _dots; // nothing drawn — return full grid as-is

            int nc = maxC - minC + 1;
            int nr = maxR - minR + 1;
            var out_ = new bool[nc, nr];
            for (int c = 0; c < nc; c++)
            for (int r = 0; r < nr; r++)
                out_[c, r] = _dots[minC + c, minR + r];
            return out_;
        }

        // Scale _sourceDots proportionally into _dots at pct% of the grid,
        // centred, maintaining the source's own aspect ratio.
        private void ApplyScale(int pct)
        {
            if (_sourceDots == null) return;

            // Bounding box of the source pattern
            int srcMinC = _cols, srcMaxC = -1, srcMinR = _rows, srcMaxR = -1;
            for (int c = 0; c < _cols; c++)
            for (int r = 0; r < _rows; r++)
            {
                if (!_sourceDots[c, r]) continue;
                if (c < srcMinC) srcMinC = c;
                if (c > srcMaxC) srcMaxC = c;
                if (r < srcMinR) srcMinR = r;
                if (r > srcMaxR) srcMaxR = r;
            }
            if (srcMaxC < 0) return; // source empty

            int   srcW      = srcMaxC - srcMinC + 1;
            int   srcH      = srcMaxR - srcMinR + 1;
            float srcAspect = (float)srcW / srcH;

            // Target bounding box inside the grid
            int   tgtW      = Math.Max(1, _cols * pct / 100);
            int   tgtH      = Math.Max(1, _rows * pct / 100);
            float tgtAspect = (float)tgtW / tgtH;

            int drawW, drawH;
            if (srcAspect >= tgtAspect) { drawW = tgtW; drawH = Math.Max(1, (int)(tgtW / srcAspect)); }
            else                        { drawH = tgtH; drawW = Math.Max(1, (int)(tgtH * srcAspect)); }

            int offC = (_cols - drawW) / 2;
            int offR = (_rows - drawH) / 2;

            // Clear grid, then paint scaled pattern
            for (int c = 0; c < _cols; c++)
            for (int r = 0; r < _rows; r++)
                _dots[c, r] = false;

            for (int dr = 0; dr < drawH; dr++)
            for (int dc = 0; dc < drawW; dc++)
            {
                // Downscaling: left-edge formula — range always covers srcMinC/srcMinR.
                // Upscaling:   centred formula  — symmetric, so left≡right, top≡bottom.
                int sc0, sc1;
                if (drawW <= srcW)
                {
                    sc0 = srcMinC + dc * srcW / drawW;
                    sc1 = Math.Max(sc0 + 1, srcMinC + (dc + 1) * srcW / drawW);
                }
                else
                {
                    sc0 = srcMinC + (2 * dc * srcW + srcW) / (2 * drawW);
                    sc1 = sc0 + 1;
                }
                int sr0, sr1;
                if (drawH <= srcH)
                {
                    sr0 = srcMinR + dr * srcH / drawH;
                    sr1 = Math.Max(sr0 + 1, srcMinR + (dr + 1) * srcH / drawH);
                }
                else
                {
                    sr0 = srcMinR + (2 * dr * srcH + srcH) / (2 * drawH);
                    sr1 = sr0 + 1;
                }
                bool any = false;
                for (int sr = sr0; sr < Math.Min(_rows, sr1) && !any; sr++)
                for (int sc = sc0; sc < Math.Min(_cols, sc1) && !any; sc++)
                    any = _sourceDots[sc, sr];
                _dots[offC + dc, offR + dr] = any;
            }

            _gridView?.Invalidate();
        }

        private static bool[,] CopyDots(bool[,] src, int cols, int rows)
        {
            var copy = new bool[cols, rows];
            for (int c = 0; c < cols; c++)
            for (int r = 0; r < rows; r++)
                copy[c, r] = src[c, r];
            return copy;
        }

        private void BuildUI()
        {
            Text            = "Tactile Graphic Editor";
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition   = FormStartPosition.CenterParent;
            Size            = new Size(880, 680);
            MinimumSize     = new Size(600, 480);
            BackColor       = Color.FromArgb(242, 242, 242);
            ShowInTaskbar   = true;
            MaximizeBox     = true;
            MinimizeBox     = true;

            // ── Tool strip (left 80 px) ────────────────────────────────────────
            var tools = new Panel
            {
                Width     = 80,
                Dock      = DockStyle.Left,
                BackColor = Color.FromArgb(230, 230, 230)
            };

            var btnDraw   = MakeToolBtn("Draw",   "Pen — click or drag to set dots");
            var btnImport = MakeToolBtn("Import", "Import image and convert to dot pattern");
            var btnEraser = MakeToolBtn("Eraser", "Eraser — click or drag to clear dots");
            btnDraw.Location   = new Point(10, 12);
            btnImport.Location = new Point(10, 62);
            btnEraser.Location = new Point(10, 112);

            // Pen icon on Draw button
            btnDraw.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(Color.FromArgb(60, 60, 60), 2f))
                {
                    g.DrawLine(pen, 28, 10, 18, 26);
                    g.DrawLine(pen, 28, 10, 32, 14);
                    g.DrawLine(pen, 32, 14, 18, 26);
                    g.FillPolygon(new SolidBrush(Color.FromArgb(60, 60, 60)),
                        new[] { new Point(18, 26), new Point(22, 22), new Point(16, 29) });
                }
            };

            // Arrow-down icon on Import button
            btnImport.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(Color.FromArgb(60, 60, 60), 2f))
                {
                    g.DrawLine(pen, 25, 10, 25, 22);
                    g.DrawLine(pen, 19, 17, 25, 24);
                    g.DrawLine(pen, 31, 17, 25, 24);
                    g.DrawLine(pen, 17, 26, 33, 26);
                }
            };

            // Eraser icon on Eraser button
            btnEraser.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen   = new Pen(Color.FromArgb(60, 60, 60), 1.5f))
                using (var fill  = new SolidBrush(Color.FromArgb(230, 180, 180)))
                using (var fill2 = new SolidBrush(Color.FromArgb(200, 200, 200)))
                {
                    // Eraser body (parallelogram)
                    var body = new[] {
                        new Point(14, 24), new Point(34, 24),
                        new Point(38, 16), new Point(18, 16) };
                    g.FillPolygon(fill, body);
                    g.DrawPolygon(pen, body);
                    // Pink left face
                    var face = new[] {
                        new Point(14, 24), new Point(18, 16),
                        new Point(18, 13), new Point(14, 21) };
                    g.FillPolygon(fill2, face);
                    // Base line
                    g.DrawLine(pen, 12, 25, 36, 25);
                }
            };

            // Highlight Draw as default active
            btnDraw.BackColor = Color.White;

            btnDraw.Click += (s, e) =>
            {
                _gridView.EraseMode  = false;
                btnDraw.BackColor    = Color.White;
                btnEraser.BackColor  = Color.FromArgb(230, 230, 230);
                btnImport.BackColor  = Color.FromArgb(230, 230, 230);
            };
            btnImport.Click += (s, e) => DoImport();
            btnEraser.Click += (s, e) =>
            {
                _gridView.EraseMode  = true;
                btnEraser.BackColor  = Color.White;
                btnDraw.BackColor    = Color.FromArgb(230, 230, 230);
                btnImport.BackColor  = Color.FromArgb(230, 230, 230);
            };

            tools.Controls.AddRange(new Control[] { btnDraw, btnImport, btnEraser });

            // ── Scrollable grid area ───────────────────────────────────────────
            _scroll = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.FromArgb(185, 185, 185),
                Padding   = new Padding(10)
            };

            _gridView = new DotGridPanel(_dots, _cols, _rows);
            _gridView.Location  = new Point(10, 10);
            _gridView.OnUserDrew = () =>
            {
                _sourceDots = CopyDots(_dots, _cols, _rows);
                if (_sizeSlider != null) _sizeSlider.Value = 100;
            };
            _scroll.Controls.Add(_gridView);

            // ── Bottom bar (buttons only) ──────────────────────────────────────
            var bottom = new Panel
            {
                Height    = 46,
                Dock      = DockStyle.Bottom,
                BackColor = Color.FromArgb(242, 242, 242)
            };

            bottom.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(210, 210, 210)))
                    e.Graphics.DrawLine(pen, 0, 0, bottom.Width, 0);
            };

            var btnCancel = new Button
            {
                Text      = "Cancel",
                Size      = new Size(90, 28),
                FlatStyle = FlatStyle.System
            };
            btnCancel.Click += (s, e) => Close();

            var btnOK = new Button
            {
                Text      = "Confirm",
                Size      = new Size(90, 28),
                FlatStyle = FlatStyle.System
            };
            btnOK.Click += (s, e) => { ConfirmClicked?.Invoke(ResultGrid); Close(); };

            bottom.Resize += (s, e) =>
            {
                btnCancel.Location = new Point(bottom.Width - 196, 9);
                btnOK.Location     = new Point(bottom.Width -  98, 9);
            };

            bottom.Controls.AddRange(new Control[] { btnCancel, btnOK });

            // ── Size slider — lives in the grey zone to the RIGHT of the grid ──
            // Row height matches the TrackBar so all three controls share one baseline.
            const int ROW_H    = 24;   // == TrackBar height
            const int LBL_W    = 44;   // "Size:" label width
            const int SLIDER_W = 220;
            const int PCT_W    = 42;   // "100%" label width

            _lblSize = new Label
            {
                Text      = "Size:",
                Size      = new Size(LBL_W, ROW_H),
                TextAlign = ContentAlignment.MiddleLeft,
                Font      = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(40, 40, 40),
                Location  = new Point(0, 0)
            };

            _sizeSlider = new TrackBar
            {
                Minimum     = 10,
                Maximum     = 100,
                Value       = 100,
                TickStyle   = TickStyle.None,
                SmallChange = 5,
                LargeChange = 10,
                Size        = new Size(SLIDER_W, ROW_H),
                BackColor   = Color.FromArgb(185, 185, 185),
                Location    = new Point(LBL_W + 2, 0)
            };

            _sizePctLabel = new Label
            {
                Text      = "100%",
                Size      = new Size(PCT_W, ROW_H),
                TextAlign = ContentAlignment.MiddleLeft,
                Font      = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(40, 40, 40),
                Location  = new Point(LBL_W + 2 + SLIDER_W + 2, 0)
            };

            _sizeSlider.ValueChanged += (s, e) =>
            {
                _sizePctLabel.Text = _sizeSlider.Value + "%";
                ApplyScale(_sizeSlider.Value);
            };

            _sliderRow = new Panel
            {
                Size      = new Size(LBL_W + 2 + SLIDER_W + 2 + PCT_W, ROW_H),
                BackColor = Color.FromArgb(185, 185, 185)
            };
            _sliderRow.Controls.AddRange(new Control[] { _lblSize, _sizeSlider, _sizePctLabel });

            // Positioning of _sliderRow is done in FitGrid so it tracks the grid's right edge
            _scroll.Controls.Add(_sliderRow);

            Controls.Add(_scroll);
            Controls.Add(tools);
            Controls.Add(bottom);
        }

        // Recompute dot size to fill available space (called on Load + Resize)
        private void FitGrid()
        {
            if (_gridView == null || _fitting) return;
            int aw = ClientSize.Width  - 80 - 20;
            int ah = ClientSize.Height - 46 - 20;
            if (aw <= 0 || ah <= 0) return;
            int dpx = Math.Max(3, Math.Min(14,
                (int)Math.Min((double)aw / _cols, (double)ah / _rows)));
            _gridView.SetDotPx(dpx);
            _gridView.Size = new Size(_cols * dpx + 1, _rows * dpx + 1);

            // Snap form height so there is no grey gap below the grid
            int idealClientH = _gridView.Height + 20 + 46;
            if (ClientSize.Height != idealClientH)
            {
                _fitting = true;
                ClientSize = new Size(ClientSize.Width, idealClientH);
                _fitting   = false;
            }

            // Place slider row at the bottom of the grey zone to the right of the grid
            if (_sliderRow != null)
            {
                int rx = _gridView.Right + 14;
                _sliderRow.Location = new Point(rx, _gridView.Bottom - _sliderRow.Height);
            }
        }

        protected override void OnLoad(EventArgs e)   { base.OnLoad(e);   FitGrid(); }
        protected override void OnResize(EventArgs e) { base.OnResize(e); FitGrid(); }

        // ── Image import ──────────────────────────────────────────────────────

        private void DoImport()
        {
            using (var dlg = new OpenFileDialog
            {
                Title  = "Import Image",
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tiff|All Files|*.*"
            })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                try
                {
                    using (var src = System.Drawing.Image.FromFile(dlg.FileName))
                        ConvertToDots(src);
                    _gridView.Invalidate();
                }
                catch
                {
                    MessageBox.Show("Could not load the image.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void ConvertToDots(System.Drawing.Image src)
        {
            // Scale proportionally so the image is not stretched to fit the grid.
            // Dots are equally spaced in both axes, so the grid's pixel aspect ratio
            // equals _cols : _rows. A square source must map to equal dot counts.
            float srcAspect  = (float)src.Width / src.Height;
            float gridAspect = (float)_cols / _rows;

            int drawW, drawH, offX, offY;
            if (srcAspect > gridAspect)
            {
                drawW = _cols;
                drawH = Math.Max(1, (int)(_cols / srcAspect));
                offX  = 0;
                offY  = (_rows - drawH) / 2;
            }
            else
            {
                drawH = _rows;
                drawW = Math.Max(1, (int)(_rows * srcAspect));
                offX  = (_cols - drawW) / 2;
                offY  = 0;
            }

            // Render at 4× resolution so stroke edges land on many sub-pixels.
            // Binarise, thin with Zhang-Suen (any stroke → 1-pixel skeleton),
            // then area-OR sample back.  Result: every line fires exactly one dot
            // regardless of stroke thickness, and all sides are consistent.
            const int S = 4;
            int bmpW = _cols * S, bmpH = _rows * S;

            using (var bmp = new System.Drawing.Bitmap(bmpW, bmpH))
            {
                using (var g = System.Drawing.Graphics.FromImage(bmp))
                {
                    g.Clear(System.Drawing.Color.White);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(src, offX * S, offY * S, drawW * S, drawH * S);
                }

                var rect  = new Rectangle(0, 0, bmpW, bmpH);
                var bdata = bmp.LockBits(rect,
                    System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                int    stride = bdata.Stride;
                byte[] raw    = new byte[stride * bmpH];
                System.Runtime.InteropServices.Marshal.Copy(bdata.Scan0, raw, 0, raw.Length);
                bmp.UnlockBits(bdata);

                bool[,] img = new bool[bmpW, bmpH];
                for (int y = 0; y < bmpH; y++)
                for (int x = 0; x < bmpW; x++)
                {
                    int   i   = y * stride + x * 4;
                    float lum = 0.299f * raw[i + 2] + 0.587f * raw[i + 1] + 0.114f * raw[i];
                    img[x, y] = lum < 128f;
                }

                // Flood-fill from the border to identify exterior (background) pixels.
                var exterior = new bool[bmpW, bmpH];
                var q = new System.Collections.Generic.Queue<int>();
                void tryEnq(int ex, int ey)
                {
                    if (ex >= 0 && ex < bmpW && ey >= 0 && ey < bmpH
                        && !img[ex, ey] && !exterior[ex, ey])
                    { exterior[ex, ey] = true; q.Enqueue(ey * bmpW + ex); }
                }
                for (int x = 0; x < bmpW; x++) { tryEnq(x, 0); tryEnq(x, bmpH - 1); }
                for (int y = 0; y < bmpH; y++) { tryEnq(0, y); tryEnq(bmpW - 1, y); }
                while (q.Count > 0)
                {
                    int idx = q.Dequeue(), qx = idx % bmpW, qy = idx / bmpW;
                    tryEnq(qx - 1, qy); tryEnq(qx + 1, qy);
                    tryEnq(qx, qy - 1); tryEnq(qx, qy + 1);
                }

                // Outer contour: dark pixels adjacent to exterior — 1-pixel-wide,
                // fires only the outermost layer regardless of stroke thickness.
                var contour = new bool[bmpW, bmpH];
                for (int y = 0; y < bmpH; y++)
                for (int x = 0; x < bmpW; x++)
                {
                    if (!img[x, y]) continue;
                    if ((x > 0 && exterior[x - 1, y]) || (x < bmpW - 1 && exterior[x + 1, y]) ||
                        (y > 0 && exterior[x, y - 1]) || (y < bmpH - 1 && exterior[x, y + 1]))
                        contour[x, y] = true;
                }

                // Compute centroid of outer contour to test for circles.
                double sumCX = 0, sumCY = 0;
                int cCnt = 0;
                for (int py = 0; py < bmpH; py++)
                for (int px = 0; px < bmpW; px++)
                {
                    if (!contour[px, py]) continue;
                    sumCX += px; sumCY += py; cCnt++;
                }

                for (int row = 0; row < _rows; row++)
                for (int col = 0; col < _cols; col++)
                    _dots[col, row] = false;

                if (cCnt > 0)
                {
                    double cx4 = sumCX / cCnt, cy4 = sumCY / cCnt;
                    double sumR = 0, sumR2 = 0;
                    for (int py = 0; py < bmpH; py++)
                    for (int px = 0; px < bmpW; px++)
                    {
                        if (!contour[px, py]) continue;
                        double dx = px - cx4, dy = py - cy4;
                        double rr = Math.Sqrt(dx * dx + dy * dy);
                        sumR += rr; sumR2 += rr * rr;
                    }
                    double avgR = sumR / cCnt;
                    double varR = Math.Max(0, sumR2 / cCnt - avgR * avgR);
                    // Coefficient of variation < 5% → shape is circular.
                    bool isCircle = cCnt > 20 && avgR > 0 && Math.Sqrt(varR) / avgR < 0.05;

                    if (isCircle)
                    {
                        // Bresenham midpoint circle — mathematically perfect single-dot ring.
                        int gcx = (int)Math.Round(cx4 / S);
                        int gcy = (int)Math.Round(cy4 / S);
                        int gr  = (int)Math.Round(avgR / S);
                        void PutDot(int c, int rr2)
                        {
                            if ((uint)c < (uint)_cols && (uint)rr2 < (uint)_rows)
                                _dots[c, rr2] = true;
                        }
                        int bx = 0, by = gr, bd = 3 - 2 * gr;
                        while (bx <= by)
                        {
                            PutDot(gcx + bx, gcy + by); PutDot(gcx - bx, gcy + by);
                            PutDot(gcx + bx, gcy - by); PutDot(gcx - bx, gcy - by);
                            PutDot(gcx + by, gcy + bx); PutDot(gcx - by, gcy + bx);
                            PutDot(gcx + by, gcy - bx); PutDot(gcx - by, gcy - bx);
                            if (bd < 0) bd += 4 * bx + 6;
                            else { bd += 4 * (bx - by) + 10; by--; }
                            bx++;
                        }
                    }
                    else
                    {
                        // Non-circular: nearest-centre sample — formula (2p+1)/(2S).
                        for (int py = 0; py < bmpH; py++)
                        for (int px = 0; px < bmpW; px++)
                        {
                            if (!contour[px, py]) continue;
                            int dc = (2 * px + 1) / (2 * S);
                            int dr = (2 * py + 1) / (2 * S);
                            if ((uint)dc < (uint)_cols && (uint)dr < (uint)_rows)
                                _dots[dc, dr] = true;
                        }
                    }
                }
            }

            _sourceDots = CopyDots(_dots, _cols, _rows);
            if (_sizeSlider != null && _sizeSlider.Value != 100)
                _sizeSlider.Value = 100;
        }

        private static void ZhangSuenThin(bool[,] img, int w, int h)
        {
            var  mark    = new bool[w, h];
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int y = 1; y < h - 1; y++)
                for (int x = 1; x < w - 1; x++)
                {
                    if (!img[x, y]) continue;
                    bool p2=img[x,y-1],p3=img[x+1,y-1],p4=img[x+1,y],p5=img[x+1,y+1],
                         p6=img[x,y+1],p7=img[x-1,y+1],p8=img[x-1,y],p9=img[x-1,y-1];
                    int b=(p2?1:0)+(p3?1:0)+(p4?1:0)+(p5?1:0)+(p6?1:0)+(p7?1:0)+(p8?1:0)+(p9?1:0);
                    if (b < 2 || b > 6) continue;
                    int a=(!p2&&p3?1:0)+(!p3&&p4?1:0)+(!p4&&p5?1:0)+(!p5&&p6?1:0)
                         +(!p6&&p7?1:0)+(!p7&&p8?1:0)+(!p8&&p9?1:0)+(!p9&&p2?1:0);
                    if (a != 1) continue;
                    if (p2&&p4&&p6) continue;
                    if (p4&&p6&&p8) continue;
                    mark[x, y] = true; changed = true;
                }
                for (int y=1;y<h-1;y++) for (int x=1;x<w-1;x++)
                    if (mark[x,y]) { img[x,y]=false; mark[x,y]=false; }

                for (int y = 1; y < h - 1; y++)
                for (int x = 1; x < w - 1; x++)
                {
                    if (!img[x, y]) continue;
                    bool p2=img[x,y-1],p3=img[x+1,y-1],p4=img[x+1,y],p5=img[x+1,y+1],
                         p6=img[x,y+1],p7=img[x-1,y+1],p8=img[x-1,y],p9=img[x-1,y-1];
                    int b=(p2?1:0)+(p3?1:0)+(p4?1:0)+(p5?1:0)+(p6?1:0)+(p7?1:0)+(p8?1:0)+(p9?1:0);
                    if (b < 2 || b > 6) continue;
                    int a=(!p2&&p3?1:0)+(!p3&&p4?1:0)+(!p4&&p5?1:0)+(!p5&&p6?1:0)
                         +(!p6&&p7?1:0)+(!p7&&p8?1:0)+(!p8&&p9?1:0)+(!p9&&p2?1:0);
                    if (a != 1) continue;
                    if (p2&&p4&&p8) continue;
                    if (p2&&p6&&p8) continue;
                    mark[x, y] = true; changed = true;
                }
                for (int y=1;y<h-1;y++) for (int x=1;x<w-1;x++)
                    if (mark[x,y]) { img[x,y]=false; mark[x,y]=false; }
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static Button MakeToolBtn(string text, string tip)
        {
            var btn = new Button
            {
                Text      = text,
                Size      = new Size(60, 44),
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 7.5f),
                BackColor = Color.FromArgb(230, 230, 230),
                Cursor    = Cursors.Hand,
                TextAlign = ContentAlignment.BottomCenter
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            new ToolTip().SetToolTip(btn, tip);
            return btn;
        }

        // ── Inner dot-grid panel ──────────────────────────────────────────────

        private sealed class DotGridPanel : Control
        {
            private readonly bool[,] _dots;
            private readonly int     _cols, _rows;
            private          int     _dotPx = 6;

            private bool _drawing;
            private bool _drawOn; // true = set dot, false = clear dot
            private bool _didDraw;

            public Action OnUserDrew;

            private bool _eraseMode;
            public bool EraseMode
            {
                get => _eraseMode;
                set { _eraseMode = value; Cursor = value ? Cursors.Default : Cursors.Cross; }
            }

            public DotGridPanel(bool[,] dots, int cols, int rows)
            {
                _dots = dots; _cols = cols; _rows = rows;
                SetStyle(
                    ControlStyles.UserPaint        |
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer, true);
                Cursor = Cursors.Cross;
            }

            public void SetDotPx(int px) { _dotPx = px; Invalidate(); }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(Color.White);
                g.SmoothingMode = SmoothingMode.AntiAlias;

                int   dp = _dotPx;
                float r  = Math.Max(1.3f, dp * 0.36f);
                float rg = Math.Max(0.8f, dp * 0.18f);

                for (int row = 0; row < _rows; row++)
                for (int col = 0; col < _cols; col++)
                {
                    float cx = col * dp + dp * 0.5f;
                    float cy = row * dp + dp * 0.5f;
                    if (_dots[col, row])
                    {
                        g.FillEllipse(Brushes.Black, cx - r, cy - r, r * 2, r * 2);
                    }
                    else
                    {
                        using (var b = new SolidBrush(Color.FromArgb(195, 195, 195)))
                            g.FillEllipse(b, cx - rg, cy - rg, rg * 2, rg * 2);
                    }
                }
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                if (e.Button != MouseButtons.Left) return;
                int col = e.X / _dotPx, row = e.Y / _dotPx;
                if (col < 0 || col >= _cols || row < 0 || row >= _rows) return;
                _drawOn  = EraseMode ? false : !_dots[col, row];
                _drawing = true;
                _didDraw = false;
                Capture  = true;
                _dots[col, row] = _drawOn;
                Invalidate();
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                if (!_drawing) return;
                int col = e.X / _dotPx, row = e.Y / _dotPx;
                if (col < 0 || col >= _cols || row < 0 || row >= _rows) return;
                if (_dots[col, row] == _drawOn) return;
                _dots[col, row] = _drawOn;
                _didDraw = true;
                Invalidate();
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                bool wasDrew = _drawing && _didDraw;
                _drawing = false;
                _didDraw = false;
                Capture  = false;
                if (wasDrew) OnUserDrew?.Invoke();
            }
        }
    }
}
