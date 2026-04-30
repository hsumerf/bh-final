using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;

namespace BrailleUrdu
{
    public class EmbossDialog : Form
    {
        private readonly CanvasPanel _canvas;

        private ListBox       _printerList;
        private RadioButton   _rbCurrent;
        private RadioButton   _rbAll;
        private RadioButton   _rbRange;
        private NumericUpDown _from;
        private NumericUpDown _to;

        public EmbossDialog(CanvasPanel canvas)
        {
            _canvas = canvas;
            BuildUI();
            LoadPrinters();
        }

        private void BuildUI()
        {
            Text            = "Emboss";
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

            var btnEmboss = new Button
            {
                Text      = "Emboss",
                Location  = new Point(546, 350),
                Size      = new Size(82, 28),
                FlatStyle = FlatStyle.System
            };
            btnEmboss.Click += OnEmbossClick;

            CancelButton = btnCancel;

            Controls.AddRange(new Control[] {
                leftBox, grpRange, grpCopies, btnCancel, btnEmboss
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

        private void OnEmbossClick(object sender, EventArgs e)
        {
            if (_printerList.SelectedItem == null)
            {
                MessageBox.Show("Please select a printer.", "Emboss",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string printer = _printerList.SelectedItem.ToString();
            IEnumerable<DocumentPage> pages;

            if (_rbCurrent.Checked)
            {
                pages = new[] { Document.CurrentPage };
            }
            else if (_rbAll.Checked)
            {
                pages = Document.Pages;
            }
            else
            {
                int from = (int)_from.Value - 1;
                int to   = Math.Min((int)_to.Value - 1, Document.Pages.Count - 1);
                if (from > to)
                {
                    MessageBox.Show("Invalid page range.", "Emboss",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                var list = new List<DocumentPage>();
                for (int i = from; i <= to; i++)
                    list.Add(Document.Pages[i]);
                pages = list;
            }

            string dotCoords = _canvas.BuildDotCoordinatesForPages(pages);
            if (string.IsNullOrEmpty(dotCoords))
            {
                MessageBox.Show("No braille content found on the selected page(s).", "Emboss",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // ESC = char 27; built at runtime so no invisible byte appears in source
            string esc   = ((char)27).ToString();
            string final = esc + "DBT0,TD0DP1,CH46,LP27,BI0,TM0,PN0,LS100,MC1;"
                         + esc + "FOR0.00:0.00,WX250.00,HY300.00;"
                         + dotCoords;

            try
            {
                RawHelper.SendStringToPrinter(printer, final);
                MessageBox.Show("Document sent to embosser.", "Emboss",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Emboss failed: " + ex.Message, "Emboss",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}


