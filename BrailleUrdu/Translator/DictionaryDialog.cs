using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace BrailleUrdu
{
    // Shows all character-to-braille mappings from a language's .spec file.
    // The Braille column is editable; typing produces braille glyphs via BrailleMapper.
    // Saving writes changes back to the spec file on disk.
    public class DictionaryDialog : Form
    {
        private const int COL_CHAR    = 0;
        private const int COL_BRAILLE = 1;

        private readonly string      _langCode;
        private readonly LanguageSpec _spec;
        private readonly DataGridView _grid;
        private readonly Label        _lblStatus;
        private readonly Font         _brailleFont;

        private TextBox _lastBrailleEditor;

        public DictionaryDialog(string langCode)
        {
            _langCode    = langCode;
            _spec        = LanguageSpec.Load(langCode);
            _brailleFont = new Font("SimBraille", 11f);

            Text            = "Dictionary — " + LanguageInfo.DisplayName(langCode);
            ClientSize      = new Size(620, 560);
            MinimumSize     = new Size(500, 420);
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition   = FormStartPosition.CenterParent;
            MaximizeBox     = true;
            MinimizeBox     = false;
            ShowInTaskbar   = false;
            Font            = new Font("Segoe UI", 9f);

            // ── Title label ───────────────────────────────────────────────────
            var lblTitle = new Label
            {
                Text     = "Dictionary — " + LanguageInfo.DisplayName(langCode),
                Font     = new Font("Segoe UI", 10f, FontStyle.Bold),
                Location = new Point(16, 12),
                AutoSize = true
            };

            // ── Grid ──────────────────────────────────────────────────────────
            _grid = new DataGridView
            {
                Location              = new Point(16, 40),
                Size                  = new Size(ClientSize.Width - 32, ClientSize.Height - 100),
                Anchor                = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect           = false,
                AutoSizeRowsMode      = DataGridViewAutoSizeRowsMode.None,
                BackgroundColor       = Color.White,
                BorderStyle           = BorderStyle.FixedSingle,
                RowHeadersVisible     = true,
                RowHeadersWidth       = 42,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                EditMode              = DataGridViewEditMode.EditOnEnter,  // single click to edit
            };
            _grid.RowTemplate.Height = 26;

            var colChar = new DataGridViewTextBoxColumn
            {
                HeaderText = "Character",
                Width      = 200,
                ReadOnly   = false,   // per-row override set in LoadGrid for existing entries
                SortMode   = DataGridViewColumnSortMode.NotSortable
            };
            var colBraille = new DataGridViewTextBoxColumn
            {
                HeaderText = "Braille",
                Width      = 300,
                SortMode   = DataGridViewColumnSortMode.NotSortable
            };
            colBraille.DefaultCellStyle.Font = _brailleFont;

            _grid.Columns.AddRange(new DataGridViewColumn[] { colChar, colBraille });
            _grid.EditingControlShowing += OnEditingControlShowing;
            _grid.RowPostPaint += (s, e) =>
            {
                var rect = new Rectangle(e.RowBounds.Left + 2, e.RowBounds.Top,
                                         _grid.RowHeadersWidth - 4, e.RowBounds.Height);
                TextRenderer.DrawText(e.Graphics, (e.RowIndex + 1).ToString(),
                                      _grid.Font, rect,
                                      SystemColors.ControlDarkDark,
                                      TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            };

            // ── Buttons ───────────────────────────────────────────────────────
            var btnAddRow = new Button
            {
                Text   = "+ Add Row",
                Size   = new Size(90, 28),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            btnAddRow.Click += OnAddRowClick;

            var btnDeleteRow = new Button
            {
                Text   = "- Delete Row",
                Size   = new Size(90, 28),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            btnDeleteRow.Click += OnDeleteRowClick;

            var btnSave = new Button
            {
                Text   = "Save",
                Size   = new Size(90, 28),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnSave.Click += OnSaveClick;

            var btnClose = new Button
            {
                Text         = "Close",
                Size         = new Size(90, 28),
                DialogResult = DialogResult.Cancel,
                Anchor       = AnchorStyles.Bottom | AnchorStyles.Right
            };

            // ── Status label ──────────────────────────────────────────────────
            _lblStatus = new Label
            {
                AutoSize  = true,
                ForeColor = Color.Gray,
                Font      = new Font("Segoe UI", 8.5f),
                Anchor    = AnchorStyles.Bottom | AnchorStyles.Left
            };

            Controls.AddRange(new Control[] { lblTitle, _grid, btnAddRow, btnDeleteRow, btnSave, btnClose, _lblStatus });
            CancelButton = btnClose;

            // Position bottom controls after ClientSize is set
            Resize += (s, e) => LayoutBottomControls(btnAddRow, btnDeleteRow, btnSave, btnClose);
            LayoutBottomControls(btnAddRow, btnDeleteRow, btnSave, btnClose);

            LoadGrid();
        }

        private void LayoutBottomControls(Button btnAddRow, Button btnDeleteRow, Button btnSave, Button btnClose)
        {
            int y = ClientSize.Height - 42;
            btnAddRow.Location    = new Point(16, y);
            btnDeleteRow.Location = new Point(116, y);
            btnClose.Location     = new Point(ClientSize.Width - 106, y);
            btnSave.Location      = new Point(ClientSize.Width - 202, y);
            _lblStatus.Location   = new Point(216, y + 6);
        }

        private void LoadGrid()
        {
            _grid.Rows.Clear();
            int count = 0;
            foreach (var entry in _spec.Entries)
            {
                if (!entry.IsData) continue;
                string brailleUnicode = LanguageSpec.ShorthandToUnicode(entry.Shorthand);
                int rowIdx = _grid.Rows.Add(entry.TypedKey, brailleUnicode);
                _grid.Rows[rowIdx].Tag = entry.Mode;
                count++;
            }
            _lblStatus.Text = count + " mappings loaded.";
        }

        // ── Braille key intercept ─────────────────────────────────────────────

        private void OnEditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            // Detach from the previous editing control (DataGridView reuses the same instance)
            if (_lastBrailleEditor != null)
            {
                _lastBrailleEditor.KeyPress -= OnBrailleCellKeyPress;
                _lastBrailleEditor = null;
            }

            if (_grid.CurrentCell?.ColumnIndex == COL_BRAILLE)
            {
                var tb = e.Control as TextBox;
                if (tb == null) return;
                e.CellStyle.Font = _brailleFont;
                tb.Font          = _brailleFont;
                tb.KeyPress     += OnBrailleCellKeyPress;
                _lastBrailleEditor = tb;
            }
        }

        private void OnBrailleCellKeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar < ' ') return;  // pass Backspace, Enter, Ctrl+chars through

            string br = BrailleMapper.ToBraille(e.KeyChar);
            if (br.Length > 0)
            {
                var tb  = (TextBox)sender;
                int sel = tb.SelectionStart;
                int len = tb.SelectionLength;
                tb.Text           = tb.Text.Remove(sel, len).Insert(sel, br);
                tb.SelectionStart = sel + br.Length;
            }
            e.Handled = true;
        }

        // ── Add Row ───────────────────────────────────────────────────────────

        private void OnAddRowClick(object sender, EventArgs e)
        {
            _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            _grid.EndEdit();

            int rowIdx = _grid.Rows.Add("", "");
            _grid.Rows[rowIdx].Tag = "a";  // new entries default to mode 'a' (always)
            // Both cells intentionally left writable (no ReadOnly override for new rows)

            _grid.ClearSelection();
            _grid.CurrentCell = _grid.Rows[rowIdx].Cells[COL_CHAR];
            _grid.Rows[rowIdx].Selected = true;
            _grid.FirstDisplayedScrollingRowIndex = rowIdx;
            _grid.BeginEdit(true);

            _lblStatus.Text = "New row added. Fill in Character and Braille, then click Save.";
        }

        private void OnDeleteRowClick(object sender, EventArgs e)
        {
            if (_grid.CurrentRow == null) return;
            _grid.EndEdit();
            int idx = _grid.CurrentRow.Index;
            _grid.Rows.RemoveAt(idx);
            _lblStatus.Text = "Row deleted. Click Save to apply.";
        }

        // ── Save ──────────────────────────────────────────────────────────────

        private void OnSaveClick(object sender, EventArgs e)
        {
            _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            _grid.EndEdit();

            var newEntries = new List<LanguageSpec.RawLine>();

            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                string typedKey       = (_grid.Rows[i].Cells[COL_CHAR].Value    as string ?? "");
                string brailleUnicode = (_grid.Rows[i].Cells[COL_BRAILLE].Value as string ?? "");
                string mode           = (_grid.Rows[i].Tag as string ?? "a");
                if (string.IsNullOrEmpty(mode)) mode = "a";

                if (string.IsNullOrEmpty(typedKey) && string.IsNullOrEmpty(brailleUnicode))
                    continue;  // skip entirely empty rows

                if (string.IsNullOrEmpty(typedKey) || string.IsNullOrEmpty(brailleUnicode))
                {
                    _lblStatus.Text = "Row " + (i + 1) + " is incomplete.";
                    MessageBox.Show(
                        "Row " + (i + 1) + " has an incomplete mapping.\n" +
                        "Both Character and Braille must be filled in.",
                        "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string shorthand = LanguageSpec.UnicodeToShorthand(brailleUnicode);
                if (string.IsNullOrEmpty(shorthand))
                {
                    _lblStatus.Text = "Row " + (i + 1) + ": braille cannot be converted to shorthand.";
                    MessageBox.Show(
                        "Row " + (i + 1) + ": the braille in this row cannot be saved.\n" +
                        "Tip: type standard letter/digit keys in the Braille column to enter braille.",
                        "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                newEntries.Add(new LanguageSpec.RawLine
                {
                    IsData    = true,
                    Shorthand = shorthand,
                    TypedKey  = typedKey,
                    Mode      = mode,
                    Raw       = shorthand + "≡" + typedKey + "≡" + mode
                });
            }

            try
            {
                _spec.SaveEntries(newEntries);
                _lblStatus.Text = "Saved successfully.";
                MessageBox.Show(
                    "Spec file saved.\nChanges will take effect the next time this language is loaded.",
                    "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not save:\n" + ex.Message,
                    "Save Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _brailleFont?.Dispose();
            base.Dispose(disposing);
        }
    }
}
