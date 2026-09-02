using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Windows.Forms;

namespace BrailleUrdu
{
    // Two-panel translator: text on the left, braille on the right.
    // Typing in either panel syncs the other in real time.
    public class TranslatorDialog : Form
    {
        private readonly LanguageSpec  _spec;
        private readonly RichTextBox   _textBox;
        private readonly BrailleGridBox _brailleBox;

        public TranslatorDialog(string langCode)
        {
            _spec = LanguageSpec.Load(langCode);

            Text            = "Translator — " + LanguageInfo.DisplayName(langCode);
            ClientSize      = new Size(820, 520);
            MinimumSize     = new Size(620, 400);
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition   = FormStartPosition.CenterParent;
            MaximizeBox     = true;
            MinimizeBox     = true;
            ShowInTaskbar   = true;
            Font            = new Font("Segoe UI", 9f);

            var lblText = new Label
            {
                Text     = LanguageInfo.DisplayName(langCode),
                Font     = new Font("Segoe UI", 9f, FontStyle.Bold),
                Location = new Point(16, 12),
                AutoSize = true
            };
            var lblBraille = new Label
            {
                Text     = "Braille",
                Font     = new Font("Segoe UI", 9f, FontStyle.Bold),
                AutoSize = true
            };

            _textBox = new RichTextBox
            {
                Multiline     = true,
                ScrollBars    = RichTextBoxScrollBars.Vertical,
                Font        = new Font(LanguageInfo.FontFor(langCode), 12f),
                RightToLeft = LanguageInfo.RtlFor(langCode) ? RightToLeft.Yes : RightToLeft.No,
                Anchor      = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            _brailleBox = new BrailleGridBox
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right
            };

            // Vertical divider
            var divider = new Panel
            {
                BackColor = SystemColors.ControlDark,
                Anchor    = AnchorStyles.Top | AnchorStyles.Bottom
            };

            _textBox.KeyPress    += OnTextKeyPress;
            _brailleBox.KeyPress += OnBrailleKeyPress;

            var btnClear = new Button
            {
                Text   = "Clear",
                Size   = new Size(80, 28),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            btnClear.Click += (s, e) =>
            {
                _textBox.Clear();
                _brailleBox.Clear();
            };

            var btnTranslate = new Button
            {
                Text   = "Translate",
                Size   = new Size(90, 28),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnTranslate.Click += (s, e) =>
            {
                if (_textBox.Focused || (!_brailleBox.Focused))
                {
                    _brailleBox.Text = ToBraille(_textBox.Text);
                }
                else
                {
                    _textBox.Text = _spec.FromBraille(_brailleBox.Text);
                }
            };

            var btnClose = new Button
            {
                Text   = "Close",
                Size   = new Size(80, 28),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnClose.Click += (s, e) => Close();

            Controls.AddRange(new Control[] { lblText, lblBraille, divider, _textBox, _brailleBox, btnClear, btnTranslate, btnClose });
            CancelButton = btnClose;

            Resize += (s, e) => DoLayout(lblBraille, divider, btnClear, btnTranslate, btnClose);
            DoLayout(lblBraille, divider, btnClear, btnTranslate, btnClose);
        }

        private void DoLayout(Label lblBraille, Panel divider, Button btnClear, Button btnTranslate, Button btnClose)
        {
            int mid    = ClientSize.Width / 2;
            int top    = 36;
            int bottom = ClientSize.Height - 50;
            int boxH   = Math.Max(bottom - top, 50);

            _textBox.SetBounds(16, top, mid - 24, boxH);
            divider.SetBounds(mid - 4, top, 2, boxH);
            _brailleBox.SetBounds(mid + 4, top, ClientSize.Width - mid - 20, boxH);

            lblBraille.Location    = new Point(mid + 4, 12);
            int btnY               = ClientSize.Height - 42;
            btnClear.Location      = new Point(16, btnY);
            btnClose.Location      = new Point(ClientSize.Width - 96, btnY);
            btnTranslate.Location  = new Point(btnClose.Left - btnTranslate.Width - 8, btnY);
        }

        // ── Text box key intercept (English keyboard → language characters) ──

        private void OnTextKeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar < ' ') return;  // pass Backspace, Enter, Ctrl-chars through
            if (e.KeyChar >= '0' && e.KeyChar <= '9')
            {
                // Map to the language's own digit character (Arabic-Indic for RTL langs)
                _textBox.SelectedText = DigitForLang(e.KeyChar);
                e.Handled = true;
                return;
            }
            string mapped = _spec.TypedKeyFor(e.KeyChar);
            if (mapped.Length > 0)
            {
                _textBox.SelectedText = mapped;
                e.Handled = true;
            }
            // no mapping found → let the keystroke through unchanged
        }

        // Digits 1-9 use shorthands a-i; 0 uses j — consistent across Urdu/Arabic/Sindhi specs.
        private string DigitForLang(char ascii)
        {
            if (_spec.LangCode == "en") return ascii.ToString();
            char sh = ascii == '0' ? 'j' : (char)('a' + (ascii - '1'));
            string mapped = _spec.DigitKeyFor(sh);
            return mapped.Length > 0 ? mapped : ascii.ToString();
        }

        // ── Braille box key intercept (ASCII shorthand → braille glyphs) ─────

        private void OnBrailleKeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar < ' ') return;  // pass Backspace, Enter, Ctrl-chars through
            string br = LanguageSpec.ShorthandToBraille(e.KeyChar);
            if (br.Length > 0)
            {
                if (e.KeyChar >= '0' && e.KeyChar <= '9')
                    br = "⠼" + br;
                _brailleBox.SelectedText = br;
            }
            e.Handled = true;
        }

        private string ToBraille(string text)
        {
            var sb = new StringBuilder();
            foreach (char c in text)
            {
                if (c == '\r') continue;
                if (c == '\n') { sb.Append('\n'); continue; }
                string br = _spec.ToBraille(c);
                if (br.Length == 0 && char.IsUpper(c))
                {
                    br = _spec.ToBraille(char.ToLower(c));
                    if (br.Length > 0) sb.Append('⠠');
                }
                if (br.Length > 0)
                {
                    if (char.IsDigit(c)) sb.Append('⠼');
                    sb.Append(br);
                }
                else sb.Append("⠀");
            }
            return sb.ToString();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        // ── Braille dot-grid display ──────────────────────────────────────────

        private sealed class BrailleGridBox : ScrollableControl
        {
            private string _text  = "";
            private int    _caret;

            // Mouse selection (indices into the flat braille char sequence)
            private int  _selAnchor = -1;
            private int  _selFocus  = -1;
            private bool _selecting;

            // Caret blink
            private readonly Timer _caretTimer  = new Timer { Interval = 530 };
            private bool           _caretVisible = false;

            // Local undo / redo
            private struct Snap { public string Text; public int Caret; }
            private readonly Stack<Snap> _undoStack = new Stack<Snap>();
            private readonly Stack<Snap> _redoStack = new Stack<Snap>();

            private const float RAISED   = 4f;
            private const float UNRAISED = 1.5f;
            private const float H_STEP   = 6f;
            private const float V_STEP   = 6f;
            private const float CELL_GAP = 4f;
            private const float LINE_GAP = 4f;
            private const float PAD      = 6f;

            private float CellW => H_STEP + RAISED;
            private float CellH => 2f * V_STEP + RAISED;

            private bool HasSel => _selAnchor >= 0 && _selAnchor != _selFocus;
            private int  SelMin => Math.Min(_selAnchor, _selFocus);
            private int  SelMax => Math.Max(_selAnchor, _selFocus);

            public new string Text
            {
                get => _text;
                set { _text = value ?? ""; _caret = _text.Length; ClearSel(); _undoStack.Clear(); _redoStack.Clear(); Relayout(); Invalidate(); }
            }

            public string SelectedText
            {
                get => "";
                set
                {
                    if (string.IsNullOrEmpty(value)) return;
                    PushUndo();
                    if (HasSel) DeleteSelection();
                    _text  = _text.Insert(_caret, value);
                    _caret = Math.Min(_caret + value.Length, _text.Length);
                    Relayout(); Invalidate();
                }
            }

            public new void Clear() { Text = ""; }

            public BrailleGridBox()
            {
                SetStyle(ControlStyles.UserPaint            |
                         ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.Selectable, true);
                AutoScroll = true;
                BackColor  = Color.White;
                Cursor     = Cursors.IBeam;
                TabStop    = true;

                _caretTimer.Tick += (s, ev) => { _caretVisible = !_caretVisible; Invalidate(); };

                var menu     = new ContextMenuStrip();
                var copyItem = new ToolStripMenuItem("Copy");
                copyItem.Click += (s, ev) => CopySelection();
                menu.Items.Add(copyItem);
                ContextMenuStrip = menu;
            }

            protected override void OnGotFocus(EventArgs e)
            {
                base.OnGotFocus(e);
                _caretVisible = true;
                _caretTimer.Start();
                Invalidate();
            }

            protected override void OnLostFocus(EventArgs e)
            {
                base.OnLostFocus(e);
                _caretTimer.Stop();
                _caretVisible = false;
                Invalidate();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing) _caretTimer.Dispose();
                base.Dispose(disposing);
            }

            // ── Undo / redo ───────────────────────────────────────────────────

            private void PushUndo()
            {
                _undoStack.Push(new Snap { Text = _text, Caret = _caret });
                _redoStack.Clear();
            }

            // ── Selection helpers ─────────────────────────────────────────────

            private void ClearSel() { _selAnchor = -1; _selFocus = -1; }

            private void DeleteSelection()
            {
                int min = Math.Max(0, SelMin);
                int max = Math.Min(SelMax, _text.Length);
                if (max <= min) return;
                _text  = _text.Substring(0, min) + _text.Substring(max);
                _caret = min;
                ClearSel();
            }

            private void CopySelection()
            {
                string flat = FlatBraille();
                if (flat.Length == 0) return;
                string toCopy = HasSel
                    ? flat.Substring(Math.Max(0, SelMin), Math.Min(SelMax, flat.Length) - Math.Max(0, SelMin))
                    : flat;
                if (toCopy.Length > 0) Clipboard.SetText(toCopy);
            }

            private string FlatBraille()
            {
                var sb = new StringBuilder();
                foreach (string line in WrapLines()) sb.Append(line);
                return sb.ToString();
            }

            // ── Hit test ──────────────────────────────────────────────────────

            private int HitTest(int lx, int ly)
            {
                var lines = WrapLines();
                float y = PAD;
                int ci = 0;
                for (int li = 0; li < lines.Count; li++)
                {
                    bool lastLine = li == lines.Count - 1;
                    if (ly < y + CellH + LINE_GAP || lastLine)
                    {
                        int col = Math.Max(0, (int)((lx - PAD) / (CellW + CELL_GAP)));
                        return Math.Min(ci + col, ci + lines[li].Length);
                    }
                    ci += lines[li].Length;
                    y  += CellH + LINE_GAP;
                }
                return 0;
            }

            // ── Mouse ─────────────────────────────────────────────────────────

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                Focus();
                if (e.Button == MouseButtons.Left)
                {
                    int idx    = HitTest(e.X - AutoScrollPosition.X, e.Y - AutoScrollPosition.Y);
                    _caret     = idx;
                    _selAnchor = idx;
                    _selFocus  = idx;
                    _selecting = true;
                    Capture    = true;
                    _caretVisible = true;
                    Invalidate();
                }
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                if (!_selecting) return;
                _selFocus = HitTest(e.X - AutoScrollPosition.X, e.Y - AutoScrollPosition.Y);
                _caret    = _selFocus;
                Invalidate();
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                _selecting = false;
                Capture    = false;
            }

            // ── Keyboard ──────────────────────────────────────────────────────

            protected override bool IsInputKey(Keys keyData)
            {
                switch (keyData & Keys.KeyCode)
                {
                    case Keys.Left:
                    case Keys.Right:
                    case Keys.Home:
                    case Keys.End:
                    case Keys.Back:
                    case Keys.Delete:
                        return true;
                }
                return base.IsInputKey(keyData);
            }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                base.OnKeyDown(e);
                if (e.Control && e.KeyCode == Keys.Z)
                {
                    if (_undoStack.Count > 0)
                    {
                        _redoStack.Push(new Snap { Text = _text, Caret = _caret });
                        var snap = _undoStack.Pop();
                        _text = snap.Text; _caret = snap.Caret;
                        ClearSel(); Relayout(); Invalidate();
                    }
                    e.Handled = true;
                }
                else if (e.Control && e.KeyCode == Keys.Y)
                {
                    if (_redoStack.Count > 0)
                    {
                        _undoStack.Push(new Snap { Text = _text, Caret = _caret });
                        var snap = _redoStack.Pop();
                        _text = snap.Text; _caret = snap.Caret;
                        ClearSel(); Relayout(); Invalidate();
                    }
                    e.Handled = true;
                }
                else if (e.Control && e.KeyCode == Keys.C)
                {
                    CopySelection(); e.Handled = true;
                }
                else if (e.Control && e.KeyCode == Keys.A)
                {
                    _selAnchor = 0; _selFocus = FlatBraille().Length;
                    Invalidate(); e.Handled = true;
                }
                else if (e.KeyCode == Keys.Left)
                {
                    if (_caret > 0) _caret--;
                    ClearSel(); _caretVisible = true; Invalidate(); e.Handled = true;
                }
                else if (e.KeyCode == Keys.Right)
                {
                    if (_caret < FlatBraille().Length) _caret++;
                    ClearSel(); _caretVisible = true; Invalidate(); e.Handled = true;
                }
                else if (e.KeyCode == Keys.Home)
                {
                    _caret = 0;
                    ClearSel(); _caretVisible = true; Invalidate(); e.Handled = true;
                }
                else if (e.KeyCode == Keys.End)
                {
                    _caret = FlatBraille().Length;
                    ClearSel(); _caretVisible = true; Invalidate(); e.Handled = true;
                }
                else if (e.KeyCode == Keys.Back || e.KeyCode == Keys.Delete)
                {
                    bool canAct = HasSel ||
                                  (e.KeyCode == Keys.Back   && _caret > 0) ||
                                  (e.KeyCode == Keys.Delete && _caret < _text.Length);
                    if (canAct)
                    {
                        PushUndo();
                        if (HasSel) DeleteSelection();
                        else if (e.KeyCode == Keys.Back)   _text = _text.Remove(--_caret, 1);
                        else                               _text = _text.Remove(_caret, 1);
                        ClearSel(); Relayout(); Invalidate();
                    }
                    e.Handled = true;
                }
            }

            // ── Layout ────────────────────────────────────────────────────────

            protected override void OnResize(EventArgs e) { base.OnResize(e); Relayout(); Invalidate(); }

            private void Relayout()
            {
                int lines = WrapLines().Count;
                AutoScrollMinSize = new Size(0, (int)(PAD * 2 + lines * (CellH + LINE_GAP)));
            }

            private List<string> WrapLines()
            {
                float avail = Math.Max(CellW + CELL_GAP, ClientSize.Width - PAD * 2);
                int   cpl   = Math.Max(1, (int)(avail / (CellW + CELL_GAP)));
                var   lines = new List<string>();
                var   sb    = new StringBuilder();
                foreach (char c in _text)
                {
                    if (c == '\n') { lines.Add(sb.ToString()); sb.Clear(); continue; }
                    if (c < '⠀' || c > '⣿') continue;
                    sb.Append(c);
                    if (sb.Length >= cpl) { lines.Add(sb.ToString()); sb.Clear(); }
                }
                if (sb.Length > 0 || lines.Count == 0) lines.Add(sb.ToString());
                return lines;
            }

            // ── Paint ─────────────────────────────────────────────────────────

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);

                int selMin = HasSel ? SelMin : -1;
                int selMax = HasSel ? SelMax : -1;

                var lines = WrapLines();

                using (var rb  = new SolidBrush(Color.FromArgb(35, 35, 35)))
                using (var ub  = new SolidBrush(Color.FromArgb(130, 130, 130)))
                using (var sb2 = new SolidBrush(Color.FromArgb(180, 210, 240)))
                using (var srb = new SolidBrush(Color.FromArgb(20, 60, 140)))
                using (var sub = new SolidBrush(Color.FromArgb(100, 140, 200)))
                {
                    float y = PAD;
                    int   ci = 0;
                    foreach (string line in lines)
                    {
                        float x = PAD;
                        foreach (char bc in line)
                        {
                            bool sel  = ci >= selMin && ci < selMax;
                            var  dotR = sel ? srb : rb;
                            var  dotU = sel ? sub : ub;

                            if (sel)
                                g.FillRectangle(sb2, x - 1, y - 1, CellW + 2, CellH + 2);

                            int bits = bc - '⠀';
                            for (int col = 0; col < 2; col++)
                            for (int row = 0; row < 3; row++)
                            {
                                int   bit = col == 0 ? row : row + 3;
                                bool  up  = (bits & (1 << bit)) != 0;
                                float cx  = x + col * H_STEP + RAISED / 2f;
                                float cy  = y + row * V_STEP + RAISED / 2f;
                                if (up)
                                    g.FillEllipse(dotR, cx - RAISED / 2f,   cy - RAISED / 2f,   RAISED,   RAISED);
                                else
                                    g.FillEllipse(dotU, cx - UNRAISED / 2f, cy - UNRAISED / 2f, UNRAISED, UNRAISED);
                            }
                            x += CellW + CELL_GAP;
                            ci++;
                        }
                        y += CellH + LINE_GAP;
                    }
                }

                // Draw caret (blinking vertical bar)
                if (_caretVisible && Focused)
                {
                    float cy = PAD, cx = PAD;
                    int ci2 = 0;
                    for (int li = 0; li < lines.Count; li++)
                    {
                        int lineLen = lines[li].Length;
                        if (ci2 + lineLen >= _caret || li == lines.Count - 1)
                        {
                            int col = Math.Min(_caret - ci2, lineLen);
                            cx = PAD + col * (CellW + CELL_GAP) - 1;
                            cy = PAD + li  * (CellH + LINE_GAP);
                            break;
                        }
                        ci2 += lineLen;
                    }
                    using (var pen = new Pen(Color.Black, 1.5f))
                        g.DrawLine(pen, cx, cy, cx, cy + CellH);
                }
            }
        }
    }
}
