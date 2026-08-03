using System.Drawing;
using System.Windows.Forms;

namespace BrailleUrdu
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.navigation      = new Navigation();
            this.pagesPanel      = new PagesPanel(() => this.canvasPanel.PageChanged());
            this.toolbarPanel    = new ToolbarPanel();
            this.canvasPanel      = new CanvasPanel();
            this.pagesPanel.SetCanvas(this.canvasPanel);
            this.centerContainer  = new Panel { Dock = DockStyle.Fill };
            this.propertiesPanel  = new PropertiesPanel(this.canvasPanel);

            this.SuspendLayout();

            // ── Center container: toolbar on top, canvas fills the rest ───────
            // (add Fill first; Top docks first because it is added last)
            this.navigation.NewDocumentClicked   += (s, e) => this.OnNewDocument();
            this.navigation.DocumentSetupClicked += (s, e) => this.OnDocumentSetup();
            this.navigation.PrintClicked         += (s, e) => new BhPrintDialog(this.canvasPanel).ShowDialog(this);
            this.navigation.EmbossClicked        += (s, e) => new EmbossDialog(this.canvasPanel).ShowDialog(this);
            this.navigation.OpenClicked          += (s, e) => this.OnOpen();
            this.navigation.SaveClicked          += (s, e) => this.OnSave();
            this.navigation.SaveAsClicked        += (s, e) => this.OnSaveAs();
            this.navigation.ExportClicked        += (s, e) => this.OnExport();
            this.navigation.UndoClicked              += (s, e) => this.canvasPanel.EditUndo();
            this.navigation.BrailleFindClicked       += (s, e) => this.OnBrailleFind();
            this.navigation.BrailleReplaceClicked    += (s, e) => this.OnBrailleReplace();
            this.navigation.InsertPageNumberClicked  += (s, e) => this.OnInsertPageNumber();
            this.navigation.TextFindClicked          += (s, e) => this.OnTextFind();
            this.navigation.TextReplaceClicked       += (s, e) => this.OnTextReplace();
            this.navigation.DictionaryClicked        += (s, e) => this.OnDictionary();
            this.navigation.TranslatorClicked        += (s, e) => this.OnTranslator();
            this.navigation.CutClicked           += (s, e) => this.canvasPanel.EditCut();
            this.navigation.CopyClicked          += (s, e) => this.canvasPanel.EditCopy();
            this.navigation.PasteClicked         += (s, e) => this.canvasPanel.EditPaste();
            this.navigation.DeleteClicked        += (s, e) => this.canvasPanel.EditDelete();
            this.navigation.DuplicateClicked     += (s, e) => this.canvasPanel.EditDuplicate();
            this.navigation.StackModeChanged     += (s, e) => this.canvasPanel.StackMode = this.navigation.IsStackMode;
            this.toolbarPanel.TextToolClicked    += (s, e)    => this.canvasPanel.ActivateTextTool();
            this.toolbarPanel.LineToolClicked    += (s, e)    => this.canvasPanel.ActivateLineTool();
            this.toolbarPanel.ImageToolClicked   += (s, e)    => this.canvasPanel.ActivateImageTool();
            this.toolbarPanel.BrailleToolClicked  += (s, e)    => this.canvasPanel.ActivateBrailleTool();
            this.toolbarPanel.TactileToolClicked  += (s, e)    => this.canvasPanel.ActivateTactileTool();
            this.toolbarPanel.TableToolClicked    += (s, e)    => this.canvasPanel.ActivateTableTool();
            this.toolbarPanel.ViewModeChanged    += (mode)    => this.canvasPanel.SetViewMode(mode);

            this.centerContainer.Controls.Add(this.canvasPanel);
            this.centerContainer.Controls.Add(this.toolbarPanel);

            // ── Form controls (add Fill first, then Right/Left, then Top) ─────
            //   navigation      → Top   (menu bar,        never scrolls)
            //   pagesPanel      → Left  (page list,        never scrolls)
            //   propertiesPanel → Right (alignment + transform, never scrolls)
            //   centerContainer → Fill  (toolbar + canvas, canvas scrolls)
            this.Controls.Add(this.centerContainer);
            this.Controls.Add(this.propertiesPanel);
            this.Controls.Add(this.pagesPanel);
            this.Controls.Add(this.navigation);

            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode       = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize          = new System.Drawing.Size(800, 450);
            this.MainMenuStrip       = this.navigation;
            this.Name                = "Form1";
            this.Text                = "BH Braille Designer";
            this.WindowState         = System.Windows.Forms.FormWindowState.Maximized;

            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private Navigation  navigation;
        private PagesPanel  pagesPanel;
        private ToolbarPanel toolbarPanel;
        private CanvasPanel canvasPanel;
        private Panel            centerContainer;
        private PropertiesPanel  propertiesPanel;
    }
}
