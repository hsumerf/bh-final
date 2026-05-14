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

        private DotGridPanel _gridView;
        private Panel        _scroll;

        public bool[,] ResultGrid => TrimDots();

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

        private void BuildUI()
        {
            Text            = "Tactile Graphic Editor";
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition   = FormStartPosition.CenterParent;
            Size            = new Size(880, 680);
            MinimumSize     = new Size(600, 480);
            BackColor       = Color.FromArgb(242, 242, 242);

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
                Dock       = DockStyle.Fill,
                AutoScroll = true,
                BackColor  = Color.FromArgb(185, 185, 185),
                Padding    = new Padding(10)
            };

            _gridView = new DotGridPanel(_dots, _cols, _rows);
            _gridView.Location = new Point(10, 10);
            _scroll.Controls.Add(_gridView);

            // ── Bottom bar ─────────────────────────────────────────────────────
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
                Text         = "Cancel",
                Size         = new Size(90, 28),
                FlatStyle    = FlatStyle.System,
                DialogResult = DialogResult.Cancel
            };
            var btnOK = new Button
            {
                Text         = "Confirm",
                Size         = new Size(90, 28),
                FlatStyle    = FlatStyle.System,
                DialogResult = DialogResult.OK
            };

            bottom.Resize += (s, e) =>
            {
                btnCancel.Location = new Point(bottom.Width - 196, 9);
                btnOK.Location     = new Point(bottom.Width -  98, 9);
            };

            bottom.Controls.AddRange(new Control[] { btnCancel, btnOK });
            AcceptButton = btnOK;
            CancelButton = btnCancel;

            Controls.Add(_scroll);
            Controls.Add(tools);
            Controls.Add(bottom);
        }

        // Recompute dot size to fill available space (called on Load + Resize)
        private void FitGrid()
        {
            if (_gridView == null) return;
            int aw = ClientSize.Width  - 80 - 20;
            int ah = ClientSize.Height - 46 - 20;
            if (aw <= 0 || ah <= 0) return;
            int dpx = Math.Max(3, Math.Min(14,
                (int)Math.Min((double)aw / _cols, (double)ah / _rows)));
            _gridView.SetDotPx(dpx);
            _gridView.Size = new Size(_cols * dpx + 1, _rows * dpx + 1);
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
            // Resize to grid dimensions, then threshold luminance to get dot pattern
            using (var bmp = new System.Drawing.Bitmap(_cols, _rows))
            {
                using (var g = System.Drawing.Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(src, 0, 0, _cols, _rows);
                }
                for (int row = 0; row < _rows; row++)
                for (int col = 0; col < _cols; col++)
                {
                    var px  = bmp.GetPixel(col, row);
                    // Standard luminance formula; dark pixels → raised dot
                    float lum = 0.299f * px.R + 0.587f * px.G + 0.114f * px.B;
                    _dots[col, row] = lum < 128f;
                }
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
                Invalidate();
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                _drawing = false;
                Capture  = false;
            }
        }
    }
}
