using System;
using System.Windows.Forms;

namespace BrailleUrdu
{
    public class Navigation : MenuStrip
    {
        public event EventHandler NewDocumentClicked;
        public event EventHandler DocumentSetupClicked;
        public event EventHandler StackModeChanged;
        public event EventHandler PrintClicked;
        public event EventHandler EmbossClicked;
        public event EventHandler OpenClicked;
        public event EventHandler SaveClicked;
        public event EventHandler SaveAsClicked;
        public event EventHandler ExportClicked;
        public event EventHandler UndoClicked;
        public event EventHandler RedoClicked;
        public event EventHandler CutClicked;
        public event EventHandler CopyClicked;
        public event EventHandler PasteClicked;
        public event EventHandler DeleteClicked;
        public event EventHandler DuplicateClicked;
        public event EventHandler BringFrontClicked;
        public event EventHandler BringForwardClicked;
        public event EventHandler SendBackClicked;
        public event EventHandler SendBackwardClicked;
        public event EventHandler BrailleFindClicked;
        public event EventHandler BrailleReplaceClicked;
        public event EventHandler InsertPageNumberClicked;
        public event EventHandler TextFindClicked;
        public event EventHandler TextReplaceClicked;
        public event EventHandler DictionaryClicked;
        public event EventHandler TranslatorClicked;

        private ToolStripMenuItem _stackModeItem;
        public bool IsStackMode => _stackModeItem?.Checked ?? false;

        public Navigation()
        {
            BuildMenu();
        }

        private void BuildMenu()
        {
            // ── Top-level items ──────────────────────────────────────────────
            var file       = new ToolStripMenuItem("&File");
            var edit       = new ToolStripMenuItem("&Edit");
            var layout     = new ToolStripMenuItem("&Layout");
            var text       = new ToolStripMenuItem("&Text");
            var braille    = new ToolStripMenuItem("&Braille");
            var translator = new ToolStripMenuItem("&Translator");
            var view       = new ToolStripMenuItem("&View");
            var help       = new ToolStripMenuItem("&Help");

            this.Items.AddRange(new ToolStripItem[] {
                file, edit, layout, text, braille, translator, view, help
            });

            // ── File ─────────────────────────────────────────────────────────
            file.DropDownItems.AddRange(new ToolStripItem[] {
                NewDocItem(),
                new ToolStripSeparator(),
                OpenItem(),
                Item("Open Cloud Document..."),
                new ToolStripSeparator(),
                Item("Import"),
                new ToolStripSeparator(),
                SaveItem(),
                SaveAsItem(),
                Item("Save On Cloud..."),
                new ToolStripSeparator(),
                ExportItem(),
                new ToolStripSeparator(),
                PrintItem(),
                EmbossItem(),
                new ToolStripSeparator(),
                Item("Animals Book Urdu.epd"),
                new ToolStripSeparator(),
                Exit()
            });

            // ── Edit ─────────────────────────────────────────────────────────
            edit.DropDownItems.AddRange(new ToolStripItem[] {
                EditItem("Undo",      Keys.Control | Keys.Z,              () => UndoClicked),
                EditItem("Redo",      Keys.Control | Keys.Y,              () => RedoClicked),
                new ToolStripSeparator(),
                EditItem("Cut",       Keys.Control | Keys.X,              () => CutClicked),
                EditItem("Copy",      Keys.Control | Keys.C,              () => CopyClicked),
                EditItem("Paste",     Keys.Control | Keys.V,              () => PasteClicked),
                EditItem("Delete",    Keys.Delete,                         () => DeleteClicked),
                new ToolStripSeparator(),
                EditItem("Duplicate", Keys.Control | Keys.D,              () => DuplicateClicked),
                new ToolStripSeparator(),
                EditItem("Bring Front",   Keys.Control | Keys.Shift | Keys.OemCloseBrackets, () => BringFrontClicked),
                EditItem("Bring Forward", Keys.Control | Keys.OemCloseBrackets,              () => BringForwardClicked),
                new ToolStripSeparator(),
                EditItem("Send Back",     Keys.Control | Keys.Shift | Keys.OemOpenBrackets,  () => SendBackClicked),
                EditItem("Send Backward", Keys.Control | Keys.OemOpenBrackets,               () => SendBackwardClicked),
                new ToolStripSeparator(),
                Item("Spacer..."),
                new ToolStripSeparator(),
                Item("Preferences...")
            });

            // ── Layout ───────────────────────────────────────────────────────
            layout.DropDownItems.AddRange(new ToolStripItem[] {
                DocumentSetupItem(),
                new ToolStripSeparator(),
                StackModeItem()
            });

            // ── Text ─────────────────────────────────────────────────────────
            var insertSymbol   = Item("Insert Symbol");
            var pageNumberItem = new ToolStripMenuItem("Page Number");
            pageNumberItem.Click += (s, e) => InsertPageNumberClicked?.Invoke(this, EventArgs.Empty);
            insertSymbol.DropDownItems.Add(pageNumberItem);

            text.DropDownItems.AddRange(new ToolStripItem[] {
                TextFindItem(),
                TextReplaceItem(),
                new ToolStripSeparator(),
                insertSymbol
            });

            // ── Braille ──────────────────────────────────────────────────────
            braille.DropDownItems.AddRange(new ToolStripItem[] {
                BrailleFindItem(),
                BrailleReplaceItem()
            });

            // ── Translator ────────────────────────────────────────────────────
            translator.DropDownItems.AddRange(new ToolStripItem[] {
                DictionaryItem(),
                new ToolStripSeparator(),
                TranslatorItem()
            });

            // ── View ─────────────────────────────────────────────────────────
            view.DropDownItems.Add(Item("Comments..."));

            // ── Help ─────────────────────────────────────────────────────────
            help.DropDownItems.Add(Item("Update"));
        }

        // Creates a plain menu item with optional shortcut.
        private static ToolStripMenuItem Item(string text, Keys shortcut = Keys.None)
        {
            var item = new ToolStripMenuItem(text);
            if (shortcut != Keys.None)
                item.ShortcutKeys = shortcut;
            return item;
        }

        private ToolStripMenuItem NewDocItem()
        {
            var item = new ToolStripMenuItem("New");
            item.ShortcutKeys = Keys.Control | Keys.N;
            item.Click += (s, e) => NewDocumentClicked?.Invoke(this, EventArgs.Empty);
            return item;
        }

        private ToolStripMenuItem DocumentSetupItem()
        {
            var item = new ToolStripMenuItem("Document Setup...");
            item.Click += (s, e) => DocumentSetupClicked?.Invoke(this, EventArgs.Empty);
            return item;
        }

        private ToolStripMenuItem PrintItem()
        {
            var item = new ToolStripMenuItem("Print...");
            item.ShortcutKeys = Keys.Control | Keys.P;
            item.Click += (s, e) => PrintClicked?.Invoke(this, EventArgs.Empty);
            return item;
        }

        private ToolStripMenuItem EmbossItem()
        {
            var item = new ToolStripMenuItem("Emboss...");
            item.Click += (s, e) => EmbossClicked?.Invoke(this, EventArgs.Empty);
            return item;
        }

        // Factory for Edit-menu items that fire a named event.
        // The Func<EventHandler> getter is evaluated at click time so the
        // lambda captures 'this' correctly without needing a named field.
        private ToolStripMenuItem EditItem(string text, Keys shortcut,
                                           Func<EventHandler> getEvent)
        {
            var item = new ToolStripMenuItem(text);
            if (shortcut != Keys.None) item.ShortcutKeys = shortcut;
            item.Click += (s, e) => getEvent()?.Invoke(this, EventArgs.Empty);
            return item;
        }

        private ToolStripMenuItem OpenItem()
        {
            var item = new ToolStripMenuItem("Open...");
            item.ShortcutKeys = Keys.Control | Keys.O;
            item.Click += (s, e) => OpenClicked?.Invoke(this, EventArgs.Empty);
            return item;
        }

        private ToolStripMenuItem SaveItem()
        {
            var item = new ToolStripMenuItem("Save");
            item.ShortcutKeys = Keys.Control | Keys.S;
            item.Click += (s, e) => SaveClicked?.Invoke(this, EventArgs.Empty);
            return item;
        }

        private ToolStripMenuItem SaveAsItem()
        {
            var item = new ToolStripMenuItem("Save As...");
            item.ShortcutKeys = Keys.Control | Keys.Shift | Keys.S;
            item.Click += (s, e) => SaveAsClicked?.Invoke(this, EventArgs.Empty);
            return item;
        }

        private ToolStripMenuItem ExportItem()
        {
            var item = new ToolStripMenuItem("Export...");
            item.Click += (s, e) => ExportClicked?.Invoke(this, EventArgs.Empty);
            return item;
        }

        private ToolStripMenuItem StackModeItem()
        {
            _stackModeItem              = new ToolStripMenuItem("Stack Mode");
            _stackModeItem.CheckOnClick = true;
            _stackModeItem.Click       += (s, e) => StackModeChanged?.Invoke(this, EventArgs.Empty);
            return _stackModeItem;
        }

        private ToolStripMenuItem TextFindItem()
        {
            var item = new ToolStripMenuItem("Find...");
            item.ShortcutKeys = Keys.Control | Keys.F;
            item.Click += (s, e) => TextFindClicked?.Invoke(this, EventArgs.Empty);
            return item;
        }

        private ToolStripMenuItem TextReplaceItem()
        {
            var item = new ToolStripMenuItem("Replace...");
            item.ShortcutKeys = Keys.Control | Keys.R;
            item.Click += (s, e) => TextReplaceClicked?.Invoke(this, EventArgs.Empty);
            return item;
        }

        private ToolStripMenuItem BrailleFindItem()
        {
            var item = new ToolStripMenuItem("Find...");
            item.ShortcutKeys = Keys.Control | Keys.Shift | Keys.F;
            item.Click += (s, e) => BrailleFindClicked?.Invoke(this, EventArgs.Empty);
            return item;
        }

        private ToolStripMenuItem BrailleReplaceItem()
        {
            var item = new ToolStripMenuItem("Replace...");
            item.ShortcutKeys = Keys.Control | Keys.Shift | Keys.R;
            item.Click += (s, e) => BrailleReplaceClicked?.Invoke(this, EventArgs.Empty);
            return item;
        }

        // Creates the Exit item wired to Application.Exit.
        private static ToolStripMenuItem Exit()
        {
            var item = new ToolStripMenuItem("Exit");
            item.Click += (s, e) => Application.Exit();
            return item;
        }

        private ToolStripMenuItem DictionaryItem()
        {
            var item = new ToolStripMenuItem("Dictionary...");
            item.Click += (s, e) => DictionaryClicked?.Invoke(this, EventArgs.Empty);
            return item;
        }

        private ToolStripMenuItem TranslatorItem()
        {
            var item = new ToolStripMenuItem("Translator...");
            item.Click += (s, e) => TranslatorClicked?.Invoke(this, EventArgs.Empty);
            return item;
        }
    }
}
