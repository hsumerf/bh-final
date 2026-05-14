using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace BrailleUrdu
{
    public partial class Form1 : Form
    {
        private string _currentFilePath;
        private bool   _isDirty;
        private Button _btnCollapsePages;
        private Button _btnCollapseProps;

        public Form1()
        {
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
            if (new DocumentSetupDialog(canvasPanel, pagesPanel).ShowDialog(this) != DialogResult.OK)
                Application.Exit();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!PromptSaveIfDirty())
                e.Cancel = true;
            base.OnFormClosing(e);
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
            if (!PromptSaveIfDirty()) return;

            canvasPanel.ClearAll();
            Document.Pages.Clear();
            Document.Pages.Add(new DocumentPage());
            Document.CurrentPageIndex = 0;
            canvasPanel.PageChanged();
            pagesPanel.RebuildThumbnails();
            _currentFilePath = null;
            _isDirty         = false;
            UpdateTitle();
            new DocumentSetupDialog(canvasPanel, pagesPanel).ShowDialog(this);
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
            if (!PromptSaveIfDirty()) return;

            using (var dlg = new OpenFileDialog
            {
                Title  = "Open Document",
                Filter = "BH Braille Document (*.epd)|*.epd|All Files (*.*)|*.*"
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    DocumentSerializer.Load(dlg.FileName, canvasPanel, pagesPanel);
                    _currentFilePath = dlg.FileName;
                    _isDirty         = false;
                    UpdateTitle();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Could not open file:\n" + ex.Message, "Open Failed",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
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

        // ── Braille Find / Replace ────────────────────────────────────────────

        internal void OnBrailleFind()    => new BrailleFindReplaceDialog(canvasPanel, false).ShowDialog(this);
        internal void OnBrailleReplace() => new BrailleFindReplaceDialog(canvasPanel, true).ShowDialog(this);

        // ── Export ────────────────────────────────────────────────────────────

        internal void OnExport()
        {
            using (var dlg = new SaveFileDialog
            {
                Title      = "Export as PNG",
                Filter     = "PNG Image (*.png)|*.png|All Files (*.*)|*.*",
                DefaultExt = "png",
                FileName   = _currentFilePath != null
                             ? Path.GetFileNameWithoutExtension(_currentFilePath) : "export"
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    DocumentSerializer.ExportPng(dlg.FileName, canvasPanel);
                    string msg = Document.Pages.Count == 1
                        ? "Page exported successfully."
                        : Document.Pages.Count + " pages exported successfully.";
                    MessageBox.Show(msg, "Export Complete",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Could not export:\n" + ex.Message, "Export Failed",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
