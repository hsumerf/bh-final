using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;

namespace BrailleUrdu
{
    public class BhPrintDialog : Form
    {
        private readonly CanvasPanel _canvas;

        private ListBox       _printerList;
        private RadioButton   _rbCurrent;
        private RadioButton   _rbAll;
        private RadioButton   _rbRange;
        private NumericUpDown _from;
        private NumericUpDown _to;

        public BhPrintDialog(CanvasPanel canvas)
        {
            _canvas = canvas;
            BuildUI();
            LoadPrinters();
        }

        private void BuildUI()
        {
            Text            = "Print";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition   = FormStartPosition.CenterParent;
            ClientSize      = new Size(636, 390);
            MaximizeBox     = false;
            MinimizeBox     = false;
            ShowInTaskbar   = false;
            BackColor       = Color.FromArgb(242, 242, 242);

            // ── Left: printer list ────────────────────────────────────────────
            var leftBox = new Panel
            {
                Location    = new Point(16, 16),
                Size        = new Size(214, 320),
                BackColor   = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            _printerList = new ListBox
            {
                Location    = new Point(0, 0),
                Size        = new Size(212, 318),
                BorderStyle = BorderStyle.None,
                Font        = new Font("Segoe UI", 9.5f),
                DrawMode    = DrawMode.OwnerDrawFixed,
                ItemHeight  = 30,
                BackColor   = Color.White
            };
            _printerList.DrawItem += OnDrawPrinterItem;
            leftBox.Controls.Add(_printerList);

            // ── Right: Page Range ─────────────────────────────────────────────
            var grpRange = new GroupBox
            {
                Text      = "Page Range",
                Location  = new Point(248, 16),
                Size      = new Size(372, 140),
                Font      = new Font("Segoe UI", 9f),
                BackColor = Color.FromArgb(242, 242, 242)
            };

            _rbCurrent = Radio("Current Page", new Point(16, 26), true);
            _rbAll     = Radio("All Pages",    new Point(16, 56));
            _rbRange   = Radio("Range",        new Point(16, 86));

            _from = Spinner(new Point(92, 85));
            _to   = Spinner(new Point(180, 85));

            var dash = new Label
            {
                Text      = "–",
                Location  = new Point(154, 88),
                Size      = new Size(22, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                Font      = new Font("Segoe UI", 9f)
            };

            _from.Enabled = false;
            _to.Enabled   = false;

            _rbRange.CheckedChanged += (s, e) =>
            {
                _from.Enabled = _rbRange.Checked;
                _to.Enabled   = _rbRange.Checked;
                if (_rbRange.Checked)
                {
                    _from.Maximum = _to.Maximum = Document.Pages.Count;
                    _to.Value     = Math.Min(_to.Value, Document.Pages.Count);
                }
            };

            grpRange.Controls.AddRange(new Control[] {
                _rbCurrent, _rbAll, _rbRange, _from, dash, _to
            });

            // ── Right: Copies ─────────────────────────────────────────────────
            var grpCopies = new GroupBox
            {
                Text      = "Copies",
                Location  = new Point(248, 172),
                Size      = new Size(372, 72),
                Font      = new Font("Segoe UI", 9f),
                BackColor = Color.FromArgb(242, 242, 242)
            };

            grpCopies.Controls.Add(new Label
            {
                Text      = "Number of Copies",
                Location  = new Point(16, 28),
                Size      = new Size(155, 22),
                Font      = new Font("Segoe UI", 9f),
                TextAlign = ContentAlignment.MiddleLeft
            });
            grpCopies.Controls.Add(new Label
            {
                Text      = "1",
                Location  = new Point(175, 28),
                Size      = new Size(30, 22),
                Font      = new Font("Segoe UI", 9f),
                TextAlign = ContentAlignment.MiddleLeft
            });

            // ── Buttons ───────────────────────────────────────────────────────
            var btnCancel = new Button
            {
                Text         = "Cancel",
                Location     = new Point(456, 350),
                Size         = new Size(82, 28),
                FlatStyle    = FlatStyle.System,
                DialogResult = DialogResult.Cancel
            };

            var btnPrint = new Button
            {
                Text      = "Print",
                Location  = new Point(546, 350),
                Size      = new Size(82, 28),
                FlatStyle = FlatStyle.System
            };
            btnPrint.Click += OnPrintClick;

            CancelButton = btnCancel;

            Controls.AddRange(new Control[] {
                leftBox, grpRange, grpCopies, btnCancel, btnPrint
            });
        }

        private static RadioButton Radio(string text, Point loc, bool isChecked = false)
        {
            return new RadioButton
            {
                Text     = text,
                Location = loc,
                Size     = new Size(200, 24),
                Checked  = isChecked,
                Font     = new Font("Segoe UI", 9f)
            };
        }

        private static NumericUpDown Spinner(Point loc)
        {
            return new NumericUpDown
            {
                Location  = loc,
                Size      = new Size(58, 22),
                Minimum   = 1,
                Maximum   = 9999,
                Value     = 1,
                Font      = new Font("Segoe UI", 9f),
                TextAlign = HorizontalAlignment.Center
            };
        }

        private void OnDrawPrinterItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            using (var bg = new SolidBrush(sel ? Color.FromArgb(200, 225, 255) : Color.White))
                e.Graphics.FillRectangle(bg, e.Bounds);
            e.Graphics.DrawString(
                _printerList.Items[e.Index].ToString(),
                _printerList.Font, Brushes.Black,
                new PointF(e.Bounds.X + 12, e.Bounds.Y + 7));
        }

        private void LoadPrinters()
        {
            foreach (string p in PrinterSettings.InstalledPrinters)
                _printerList.Items.Add(p);
            if (_printerList.Items.Count > 0)
                _printerList.SelectedIndex = 0;
        }

        private void OnPrintClick(object sender, EventArgs e)
        {
            if (_printerList.SelectedItem == null)
            {
                MessageBox.Show("Please select a printer.", "Print",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string printerName = _printerList.SelectedItem.ToString();
            List<DocumentPage> pageList;

            if (_rbCurrent.Checked)
            {
                pageList = new List<DocumentPage> { Document.CurrentPage };
            }
            else if (_rbAll.Checked)
            {
                pageList = new List<DocumentPage>(Document.Pages);
            }
            else
            {
                int from = (int)_from.Value - 1;
                int to   = Math.Min((int)_to.Value - 1, Document.Pages.Count - 1);
                if (from > to)
                {
                    MessageBox.Show("Invalid page range.", "Print",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                pageList = new List<DocumentPage>();
                for (int i = from; i <= to; i++)
                    pageList.Add(Document.Pages[i]);
            }

            int pageIdx = 0;
            var doc     = new PrintDocument();
            doc.PrinterSettings.PrinterName = printerName;

            // A4: 210 mm × 297 mm → 827 × 1169 hundredths-of-inch
            doc.DefaultPageSettings.PaperSize = new PaperSize("A4", 827, 1169);
            doc.DefaultPageSettings.Margins   = new Margins(0, 0, 0, 0);

            doc.PrintPage += (ps, pe) =>
            {
                _canvas.RenderPageToPrinter(pe.Graphics, pageList[pageIdx]);
                pageIdx++;
                pe.HasMorePages = pageIdx < pageList.Count;
            };

            try
            {
                doc.Print();
                MessageBox.Show("Document sent to printer.", "Print",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Print failed: " + ex.Message, "Print",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
