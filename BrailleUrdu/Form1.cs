using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace BrailleUrdu
{
    public partial class Form1 : Form
    {
        private string         _currentFilePath;
        private bool           _isDirty;
        private Button         _btnCollapsePages;
        private Button         _btnCollapseProps;
        private readonly Document _myDocument = new Document();
        private readonly string   _fileToLoad;

        // Default constructor — shows Document Setup dialog on first load.
        public Form1() : this(null) { }

        // Opens an existing file directly without showing Document Setup.
        public Form1(string fileToLoad)
        {
            _fileToLoad = fileToLoad;
            Document.SetCurrent(_myDocument); // must be before InitializeComponent
            InitializeComponent();
            canvasPanel.DocumentChanged += (s, e) => _isDirty = true;
            SetupCollapseButtons();
            Shown += (s, e) => RepositionCollapseButtons();
        }

        private void SetupCollapseButtons()
        {
            _btnCollapsePages = MakeCollapseBtn("◄");
            _btnCollapseProps = MakeCollapseBtn("►");

            _btnCollapsePages.Click += (s, e) =>
            {
                pagesPanel.ToggleCollapse();
                _btnCollapsePages.Text = pagesPanel.IsCollapsed ? "►" : "◄";
                RepositionCollapseButtons();
            };
            _btnCollapseProps.Click += (s, e) =>
            {
                propertiesPanel.ToggleCollapse();
                _btnCollapseProps.Text = propertiesPanel.IsCollapsed ? "◄" : "►";
                RepositionCollapseButtons();
            };

            Controls.Add(_btnCollapsePages);
            Controls.Add(_btnCollapseProps);
            _btnCollapsePages.BringToFront();
            _btnCollapseProps.BringToFront();
        }

        private static Button MakeCollapseBtn(string text)
        {
            var btn = new Button
            {
                Size      = new Size(22, 22),
                FlatStyle = FlatStyle.Flat,
                Text      = text,
                Font      = new Font("Segoe UI", 7.5f),
                BackColor = Color.FromArgb(215, 215, 215),
                Cursor    = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(175, 175, 175);
            btn.FlatAppearance.BorderSize  = 1;
            return btn;
        }

        private void RepositionCollapseButtons()
        {
            int y = pagesPanel.Top + 55;
            _btnCollapsePages.Location = new Point(pagesPanel.Right - 11, y);
            _btnCollapseProps.Location = new Point(propertiesPanel.Left - 11, y);
            _btnCollapsePages.BringToFront();
            _btnCollapseProps.BringToFront();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_btnCollapsePages != null)
                RepositionCollapseButtons();
        }

        // When a textbox is actively being edited, bypass the Edit menu shortcuts
        // so Ctrl+C/X/V/A operate on the text content rather than the whole control.
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Control | Keys.A:
                case Keys.Control | Keys.C:
                case Keys.Control | Keys.X:
                case Keys.Control | Keys.V:
                case Keys.Delete:
                    var ctrl = ActiveControl;
                    while (ctrl is ContainerControl cc && cc.ActiveControl != null)
                        ctrl = cc.ActiveControl;
                    if ((ctrl is BrailleTextBox btb && btb.IsTextEditing) ||
                        (ctrl is PrintTextBox   ptb && ptb.IsTextEditing))
                        return false; // skip menu shortcut; key falls through to OnKeyDown
                    break;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (_fileToLoad != null)
            {
                try
                {
                    DocumentSerializer.Load(_fileToLoad, canvasPanel, pagesPanel);
                    _currentFilePath = _fileToLoad;
                    _isDirty         = false;
                    UpdateTitle();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Could not open file:\n" + ex.Message, "Open Failed",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                if (new DocumentSetupDialog(canvasPanel, pagesPanel).ShowDialog(this) != DialogResult.OK)
                    Close(); // OnFormClosed will call Application.Exit() if this was the last window
            }
        }

        // Re-activate this window's document whenever the window gains focus.
        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            Document.SetCurrent(_myDocument);
            canvasPanel.PageChanged();
            pagesPanel.RebuildThumbnails();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!PromptSaveIfDirty())
                e.Cancel = true;
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            if (Application.OpenForms.Count == 0)
                Application.Exit();
        }

        // ── Unsaved-changes guard ─────────────────────────────────────────────
        // Returns true if the caller may proceed (saved, or user chose Discard).
        // Returns false if the user clicked Cancel or save failed.

        private bool PromptSaveIfDirty()
        {
            if (!_isDirty) return true;

            var r = MessageBox.Show(
                "You have unsaved changes. Do you want to save them before continuing?",
                "Unsaved Changes",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Warning);

            if (r == DialogResult.Cancel) return false;
            if (r == DialogResult.Yes)    return TrySave();
            return true; // Discard
        }

        // ── Title bar ─────────────────────────────────────────────────────────

        private void UpdateTitle()
        {
            Text = "BH Braille Designer" +
                   (_currentFilePath != null ? " — " + Path.GetFileName(_currentFilePath) : "");
        }

        // ── New document ──────────────────────────────────────────────────────

        internal void OnNewDocument()
        {
            new Form1().Show();
        }

        // ── Document Setup ────────────────────────────────────────────────────

        internal void OnDocumentSetup()
        {
            if (new DocumentSetupDialog(canvasPanel, pagesPanel).ShowDialog(this) == DialogResult.OK)
                _isDirty = true;
        }

        // ── Open ──────────────────────────────────────────────────────────────

        internal void OnOpen()
        {
            using (var dlg = new OpenFileDialog
            {
                Title  = "Open Document",
                Filter = "BH Braille Document (*.epd)|*.epd|All Files (*.*)|*.*"
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                new Form1(dlg.FileName).Show();
            }
        }

        // ── Save / Save As ────────────────────────────────────────────────────

        internal void OnSave()   => TrySave();
        internal void OnSaveAs() => TrySaveAs();

        private bool TrySave()
        {
            if (_currentFilePath == null) return TrySaveAs();
            try
            {
                DocumentSerializer.Save(_currentFilePath, canvasPanel);
                _isDirty = false;
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not save file:\n" + ex.Message, "Save Failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private bool TrySaveAs()
        {
            using (var dlg = new SaveFileDialog
            {
                Title      = "Save Document As",
                Filter     = "BH Braille Document (*.epd)|*.epd|All Files (*.*)|*.*",
                DefaultExt = "epd",
                FileName   = _currentFilePath != null
                             ? Path.GetFileName(_currentFilePath) : "Untitled.epd"
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return false;
                try
                {
                    DocumentSerializer.Save(dlg.FileName, canvasPanel);
                    _currentFilePath = dlg.FileName;
                    _isDirty         = false;
                    UpdateTitle();
                    return true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Could not save file:\n" + ex.Message, "Save Failed",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
            }
        }

        // ── Text Find / Replace ───────────────────────────────────────────────

        internal void OnTextFind()    => new TextFindReplaceDialog(canvasPanel, false).ShowDialog(this);
        internal void OnTextReplace() => new TextFindReplaceDialog(canvasPanel, true).ShowDialog(this);

        // ── Insert Page Number ────────────────────────────────────────────────

        private void OnInsertPageNumber()
        {
            using (var dlg = new PageNumberTypeDialog())
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                canvasPanel.InsertPageNumber(dlg.IsBraille);
            }
        }

        // ── Braille Find / Replace ────────────────────────────────────────────

        internal void OnBrailleFind()    => new BrailleFindReplaceDialog(canvasPanel, false).ShowDialog(this);
        internal void OnBrailleReplace() => new BrailleFindReplaceDialog(canvasPanel, true).ShowDialog(this);

        // ── Export ────────────────────────────────────────────────────────────

        internal void OnExport()
        {
            using (var dlg = new SaveFileDialog
            {
                Title       = "Export Document",
                Filter      = "PDF Document (*.pdf)|*.pdf|PNG Image (*.png)|*.png|All Files (*.*)|*.*",
                DefaultExt  = "pdf",
                FilterIndex = 1,
                FileName    = _currentFilePath != null
                              ? Path.GetFileNameWithoutExtension(_currentFilePath) : "export"
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    if (dlg.FilterIndex == 1) // PDF
                    {
                        DocumentSerializer.ExportPdf(dlg.FileName, canvasPanel);
                        if (WindowState == FormWindowState.Minimized)
                            WindowState = FormWindowState.Normal;
                        Activate();
                        MessageBox.Show("PDF exported successfully.", "Export Complete",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else // PNG
                    {
                        DocumentSerializer.ExportPng(dlg.FileName, canvasPanel);
                        string msg = Document.Pages.Count == 1
                            ? "Page exported successfully."
                            : Document.Pages.Count + " pages exported successfully.";
                        MessageBox.Show(msg, "Export Complete",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Could not export:\n" + ex.Message, "Export Failed",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }

    // Small dialog asking whether to insert the page number as Text or Braille.
    internal class PageNumberTypeDialog : Form
    {
        public bool IsBraille { get; private set; }

        internal PageNumberTypeDialog()
        {
            Text            = "Insert Page Number";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition   = FormStartPosition.CenterParent;
            ClientSize      = new Size(300, 120);
            MaximizeBox     = false;
            MinimizeBox     = false;
            ShowInTaskbar   = false;
            BackColor       = Color.FromArgb(242, 242, 242);

            Controls.Add(new Label
            {
                Text     = "Insert page number as:",
                Location = new Point(16, 18),
                AutoSize = true,
                Font     = new Font("Segoe UI", 9.5f)
            });

            var btnText = new Button
            {
                Text      = "Text",
                Location  = new Point(16, 52),
                Size      = new Size(120, 34),
                FlatStyle = FlatStyle.System,
                Font      = new Font("Segoe UI", 9.5f)
            };
            btnText.Click += (s, e) => { IsBraille = false; DialogResult = DialogResult.OK; Close(); };

            var btnBraille = new Button
            {
                Text      = "Braille",
                Location  = new Point(148, 52),
                Size      = new Size(120, 34),
                FlatStyle = FlatStyle.System,
                Font      = new Font("Segoe UI", 9.5f)
            };
            btnBraille.Click += (s, e) => { IsBraille = true; DialogResult = DialogResult.OK; Close(); };

            Controls.AddRange(new Control[] { btnText, btnBraille });
            CancelButton = btnText; // Escape defaults to Text (no change)
        }
    }
}
