using System;
using System.Windows.Forms;

namespace BrailleUrdu
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            // Require language + page setup before the editor is usable
            if (new DocumentSetupDialog(canvasPanel, pagesPanel).ShowDialog(this) != DialogResult.OK)
                Application.Exit();
        }

        internal void OnNewDocument()
        {
            // Clear all page content
            Document.Pages.Clear();
            Document.Pages.Add(new DocumentPage());
            Document.CurrentPageIndex = 0;
            canvasPanel.PageChanged();
            pagesPanel.RebuildThumbnails();
            // Show setup; if cancelled the blank document stays open
            new DocumentSetupDialog(canvasPanel, pagesPanel).ShowDialog(this);
        }
    }
}
