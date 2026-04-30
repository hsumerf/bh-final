using System;
using System.Windows.Forms;

namespace BrailleUrdu
{
    public class Navigation : MenuStrip
    {
        public event EventHandler NewDocumentClicked;
        public event EventHandler DocumentSetupClicked;
        public event EventHandler PrintClicked;
        public event EventHandler EmbossClicked;

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
            var navigation = new ToolStripMenuItem("&Navigation");
            var view       = new ToolStripMenuItem("&View");
            var help       = new ToolStripMenuItem("&Help");

            this.Items.AddRange(new ToolStripItem[] {
                file, edit, layout, text, braille, navigation, view, help
            });

            // ── File ─────────────────────────────────────────────────────────
            file.DropDownItems.AddRange(new ToolStripItem[] {
                NewDocItem(),
                new ToolStripSeparator(),
                Item("Open...",                Keys.Control | Keys.O),
                Item("Open Cloud Document..."),
                new ToolStripSeparator(),
                Item("Import"),
                new ToolStripSeparator(),
                Item("Save",                   Keys.Control | Keys.S),
                Item("Save As...",             Keys.Control | Keys.Shift | Keys.S),
                Item("Save On Cloud..."),
                new ToolStripSeparator(),
                Item("Export..."),
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
                Item("Undo",          Keys.Control | Keys.Z),
                new ToolStripSeparator(),
                Item("Cut",           Keys.Control | Keys.X),
                Item("Copy",          Keys.Control | Keys.C),
                Item("Paste",         Keys.Control | Keys.V),
                Item("Delete",        Keys.Delete),
                new ToolStripSeparator(),
                Item("Duplicate",     Keys.Control | Keys.D),
                new ToolStripSeparator(),
                Item("Bring Front",   Keys.Control | Keys.Shift | Keys.OemCloseBrackets),
                Item("Bring Forward", Keys.Control | Keys.OemCloseBrackets),
                new ToolStripSeparator(),
                Item("Send Back",     Keys.Control | Keys.Shift | Keys.OemOpenBrackets),
                Item("Send Backward", Keys.Control | Keys.OemOpenBrackets),
                new ToolStripSeparator(),
                Item("Spacer..."),
                new ToolStripSeparator(),
                Item("Preferences...")
            });

            // ── Layout ───────────────────────────────────────────────────────
            layout.DropDownItems.AddRange(new ToolStripItem[] {
                DocumentSetupItem(),
                new ToolStripSeparator(),
                Item("Stack Mode")
            });

            // ── Text ─────────────────────────────────────────────────────────
            var insertSymbol = Item("Insert Symbol");
            insertSymbol.DropDownItems.Add(Item("Page Number"));

            text.DropDownItems.AddRange(new ToolStripItem[] {
                Item("Find...",    Keys.Control | Keys.F),
                Item("Replace...", Keys.Control | Keys.R),
                new ToolStripSeparator(),
                insertSymbol
            });

            // ── Braille ──────────────────────────────────────────────────────
            braille.DropDownItems.AddRange(new ToolStripItem[] {
                Item("Find...",  Keys.Control | Keys.Shift | Keys.F),
                Item("Replace",  Keys.Control | Keys.Shift | Keys.R)
            });

            // ── Navigation (reserved for future items) ───────────────────────

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

        // Creates the Exit item wired to Application.Exit.
        private static ToolStripMenuItem Exit()
        {
            var item = new ToolStripMenuItem("Exit");
            item.Click += (s, e) => Application.Exit();
            return item;
        }
    }
}
