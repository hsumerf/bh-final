using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace BrailleUrdu
{
    public class PrintTextBox : UserControl
    {
        private const int       HANDLE_SIZE     = 10;
        private const int       PAD             = 8;
        public  const int       HandlePad       = 5; // transparent margin on each side; handles live here
        private const FontStyle SoftBreakStyle  = (FontStyle)0xFF; // sentinel for auto-wrap \n in _charStyle

        // ── Static defaults ───────────────────────────────────────────────────
        public static string    DefaultFontFamily = "Segoe UI";
        public static float     DefaultFontSizePt = 12f;
        public static FontStyle DefaultFontStyle  = FontStyle.Regular;
        public static Color     DefaultTextColor  = Color.Black;

        // ── Language / display ────────────────────────────────────────────────
        private string    _fontFamily;
        private float     _fontSizePt;
        private FontStyle _fontStyle;
        private bool      _isRtl;

        // ── Style ─────────────────────────────────────────────────────────────
        private Color           _textColor = Color.Black;
        private StringAlignment _hAlign    = StringAlignment.Near;
        private StringAlignment _vAlign    = StringAlignment.Center;

        // ── Border ────────────────────────────────────────────────────────────
        private Color _borderColor  = Color.Black;
        private int   _borderWidth  = 1;
        private bool  _borderTop, _borderBottom, _borderLeft, _borderRight;

        // ── Background ────────────────────────────────────────────────────────
        private Color _fillColor       = Color.White;
        private bool  _fillTransparent = true;

        // ── Public properties ─────────────────────────────────────────────────

        public string FontFamily
        {
            get => _fontFamily;
            set { _fontFamily = value ?? "Segoe UI"; FitSize(); Invalidate(); }
        }

        public float FontSizePt
        {
            get => _fontSizePt;
            set { _fontSizePt = Math.Max(1f, value); FitSize(); Invalidate(); }
        }

        public FontStyle TextFontStyle
        {
            get => _fontStyle;
            set { _fontStyle = value; FitSize(); Invalidate(); }
        }

        public bool IsRightToLeft
        {
            get => _isRtl;
            set { _isRtl = value; RightToLeft = value ? RightToLeft.Yes : RightToLeft.No; Invalidate(); }
        }

        public Color TextColor
        {
            get => _textColor;
            set { _textColor = value; Invalidate(); }
        }

        public StringAlignment HTextAlign
        {
            get => _hAlign;
            set { _hAlign = value; Invalidate(); }
        }

        public StringAlignment VTextAlign
        {
            get => _vAlign;
            set { _vAlign = value; Invalidate(); }
        }

        public Color BorderColor
        {
            get => _borderColor;
            set { _borderColor = value; Invalidate(); }
        }

        public int BorderWidth
        {
            get => _borderWidth;
            set { _borderWidth = Math.Max(0, value); Invalidate(); }
        }

        public bool BorderTop    { get => _borderTop;    set { _borderTop    = value; Invalidate(); } }
        public bool BorderBottom { get => _borderBottom; set { _borderBottom = value; Invalidate(); } }
        public bool BorderLeft   { get => _borderLeft;   set { _borderLeft   = value; Invalidate(); } }
        public bool BorderRight  { get => _borderRight;  set { _borderRight  = value; Invalidate(); } }

        public Color FillColor
        {
            get => _fillColor;
            set { _fillColor = value; Invalidate(); }
        }

        public bool FillTransparent
        {
            get => _fillTransparent;
            set { _fillTransparent = value; Invalidate(); }
        }

        // ── Content & cursor ─────────────────────────────────────────────────
        private string _text            = "";
        private int    _cursorPos       = 0;
        private int    _selectionAnchor = 0;
        private string _inputPending    = "";
        private FontStyle[] _charStyle = new FontStyle[0];

        public string DisplayText
        {
            get => _text;
            set { _text = value ?? ""; _charStyle = new FontStyle[0]; _cursorPos = _text.Length; _selectionAnchor = _cursorPos; FitSize(); Invalidate(); }
        }

        public bool IsSelected     { get; set; }
        public bool IsTextEditing => _textEditMode;

        public int SavedSelStart { get; private set; }
        public int SavedSelEnd   { get; private set; }

        private int    SelStart     => Math.Min(_selectionAnchor, _cursorPos);
        private int    SelEnd       => Math.Max(_selectionAnchor, _cursorPos);
        private bool   HasSelection => _selectionAnchor != _cursorPos;
        private string SelectedText => _text.Substring(SelStart, SelEnd - SelStart);

        // ── Search highlight ──────────────────────────────────────────────────
        private string _searchHighlight;

        public void SetSearchHighlight(string pattern)
        {
            _searchHighlight = pattern;
            Invalidate();
        }

        public void ClearSearchHighlight()
        {
            _searchHighlight = null;
            Invalidate();
        }

        // ── Mode ──────────────────────────────────────────────────────────────
        private bool _textEditMode  = false;
        private bool _justGotFocus  = false; // true from OnGotFocus until first OnMouseDown

        private readonly Timer _caretTimer   = new Timer { Interval = 530 };
        private bool           _caretVisible = false;

        // ── Drag / resize ─────────────────────────────────────────────────────
        private bool         _dragging;
        private bool         _resizing;
        private Point        _mouseDownScreen;
        private Point        _startLocation;
        private Size         _startSize;
        private int          _pendingClickIdx  = -1; // deferred single-click text entry
        private int          _ownClickCount    = 0;  // our own click counter (e.Clicks unreliable >2)
        private int          _lastClickTick    = 0;
        private Point        _lastClickScreen  = Point.Empty;
        private ResizeHandle _activeHandle = ResizeHandle.None;
        private System.Collections.Generic.Dictionary<Control, Point> _groupStartLocations;

        // ── Construction ──────────────────────────────────────────────────────
        public PrintTextBox()
        {
            _fontFamily = DefaultFontFamily;
            _fontSizePt = DefaultFontSizePt;
            _fontStyle  = DefaultFontStyle;
            _textColor  = DefaultTextColor;
            _isRtl      = false;

            SetStyle(
                ControlStyles.Selectable                   |
                ControlStyles.UserPaint                    |
                ControlStyles.AllPaintingInWmPaint         |
                ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, false);

            ResizeRedraw = true;
            BackColor    = Color.Transparent;
            TabStop      = true;
            MinimumSize  = new Size(60 + HandlePad * 2, 22 + HandlePad * 2);
            RightToLeft  = _isRtl ? RightToLeft.Yes : RightToLeft.No;
            ImeMode      = ImeMode.On;

            Width = MinimumSize.Width;
            FitSize();

            _caretTimer.Tick += (s, e) => { _caretVisible = !_caretVisible; Invalidate(); };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _caretTimer.Dispose();
            base.Dispose(disposing);
        }

        // ── Auto-size (grows right to margin, then wraps down) ────────────────
        private void FitSize()
        {
            if (!IsHandleCreated)
            {
                try
                {
                    using (var font = MakeFont())
                    {
                        int lines = Math.Max(1, _text.Split('\n').Length);
                        int newH  = Math.Max(MinimumSize.Height,
                            (int)Math.Ceiling(font.GetHeight(96f) * lines) + 2 + HandlePad * 2);
                        if (Height != newH) Height = newH;
                    }
                }
                catch { }
                return;
            }
            try
            {
                using (var font = MakeFont())
                using (var g    = CreateGraphics())
                {
                    int maxWidth;
                    var canvas = Parent as CanvasPanel;
                    if (canvas != null)
                        maxWidth = Math.Max(MinimumSize.Width,
                            (int)canvas.MarginBoundsPx.Right - Left);
                    else
                        maxWidth = int.MaxValue;

                    // Width of the widest explicit line (no wrapping)
                    float maxLineW = 0f;
                    if (_text.Length > 0)
                    {
                        var noWrap = new StringFormat
                            { FormatFlags = StringFormatFlags.NoWrap };
                        foreach (var ln in _text.Split('\n'))
                        {
                            if (ln.Length == 0) continue;
                            float w = g.MeasureString(ln, font, PointF.Empty, noWrap).Width;
                            if (w > maxLineW) maxLineW = w;
                        }
                    }
                    int contentW = (int)Math.Ceiling(maxLineW) + PAD * 2 + 4 + HandlePad * 2;
                    int newWidth = maxWidth == int.MaxValue
                        ? Math.Max(Width, Math.Max(MinimumSize.Width, contentW))
                        : Math.Max(Width, Math.Max(MinimumSize.Width, Math.Min(contentW, maxWidth)));

                    // Height with soft wrapping at newWidth
                    float measuredH;
                    if (_text.Length > 0)
                    {
                        var wrapFmt = new StringFormat(StringFormat.GenericTypographic);
                        measuredH = g.MeasureString(_text, font,
                            newWidth - PAD * 2 - HandlePad * 2, wrapFmt).Height;
                    }
                    else
                    {
                        measuredH = font.GetHeight(g);
                    }
                    int newHeight = Math.Max(MinimumSize.Height,
                        (int)Math.Ceiling(measuredH) + 2 + HandlePad * 2);

                    if (Width != newWidth || Height != newHeight)
                        SetBounds(Left, Top, newWidth, newHeight);
                    else
                        Invalidate();
                }
            }
            catch { }
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            if (Parent != null) FitSize();
        }

        // ── Mode helpers ──────────────────────────────────────────────────────
        private void EnterTextMode(int index = -1)
        {
            _textEditMode    = true;
            _cursorPos       = index < 0 ? _text.Length : Math.Min(index, _text.Length);
            _selectionAnchor = _cursorPos;
            _caretVisible    = true;
            Cursor           = Cursors.IBeam;
            _caretTimer.Start();
            Invalidate();
        }

        private void ExitTextMode()
        {
            FlushInputPending();
            SavedSelStart    = SelStart;
            SavedSelEnd      = SelEnd;
            _textEditMode    = false;
            _selectionAnchor = _cursorPos;
            _caretTimer.Stop();
            _caretVisible    = false;
            Cursor           = Cursors.Default;
            Invalidate();
        }

        private void FlushInputPending()
        {
            if (string.IsNullOrEmpty(_inputPending)) return;
            string flushed = _inputPending;
            _inputPending = "";
            if (flushed.Length == 0) return;
            CharStyleInsert(_cursorPos, flushed.Length);
            _text = _text.Substring(0, _cursorPos) + flushed + _text.Substring(_cursorPos);
            _cursorPos      += flushed.Length;
            _selectionAnchor = _cursorPos;
            _caretVisible    = true;
            FitSize();
            if (AutoWrapIfNeeded()) FitSize();
            Invalidate();
        }

        private void DeleteSelection()
        {
            int s = SelStart, end = SelEnd;
            CharStyleDelete(s, end - s);
            _text            = _text.Substring(0, s) + _text.Substring(end);
            _cursorPos       = s;
            _selectionAnchor = s;
            _caretVisible    = true;
            FitSize();
            Invalidate();
        }

        private void DeleteSelf()
        {
            BeginInvoke((Action)(() =>
            {
                Parent?.Controls.Remove(this);
                Dispose();
            }));
        }

        // ── Font / format helpers ─────────────────────────────────────────────
        private Font MakeFont() => new Font(_fontFamily, _fontSizePt, _fontStyle, GraphicsUnit.Point);

        private StringFormat MakeFmt()
        {
            var fmt = new StringFormat(StringFormat.GenericTypographic);
            fmt.FormatFlags   = StringFormatFlags.NoWrap |
                                StringFormatFlags.MeasureTrailingSpaces;
            fmt.Alignment     = _hAlign;
            fmt.LineAlignment = _vAlign;
            if (_isRtl) fmt.FormatFlags |= StringFormatFlags.DirectionRightToLeft;
            return fmt;
        }

        private float TextLeft  => HandlePad + PAD;
        private float TextRight => HandlePad + PAD;
        private float TextTop   => (float)HandlePad;
        private float TextBot   => (float)HandlePad;

        private float TextStartY(Graphics g, Font font)
        {
            int   lineCount = Math.Max(1, _text.Split('\n').Length);
            float lh        = font.GetHeight(g);
            float totalH    = lh * lineCount;
            float availH    = Height - TextTop - TextBot;
            switch (_vAlign)
            {
                case StringAlignment.Near: return TextTop;
                case StringAlignment.Far:  return TextTop + Math.Max(0f, availH - totalH);
                default:                   return TextTop + Math.Max(0f, availH - totalH) / 2f;
            }
        }

        private int TrWidth(IDeviceContext dc, string text, Font font)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            var flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine |
                        (_isRtl ? TextFormatFlags.RightToLeft : TextFormatFlags.Left);
            return TextRenderer.MeasureText(dc, text, font,
                new Size(int.MaxValue, int.MaxValue), flags).Width;
        }

        // X-coordinate of a character whose preceding text has pixel-width `beforeWidth`,
        // in the current alignment and text-direction context.
        private float CharX(Graphics g, Font font, string ln, float beforeWidth)
        {
            if (_isRtl)
                return Width - TextRight - beforeWidth;

            if (_hAlign == StringAlignment.Far)
            {
                float lineW = TrWidth(g, ln, font);
                return Width - TextRight - lineW + beforeWidth;
            }

            return TextLeft + beforeWidth; // Near or Center
        }

        // ── Transparency ──────────────────────────────────────────────────────
        protected override CreateParams CreateParams
        {
            get { var cp = base.CreateParams; cp.ExStyle |= 0x20; return cp; }
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == 0x84 && m.Result == (IntPtr)(-1)) m.Result = (IntPtr)1;
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        // ── Paint ─────────────────────────────────────────────────────────────
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode     = SmoothingMode.None;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            if (!_fillTransparent)
                using (var b = new SolidBrush(_fillColor))
                    g.FillRectangle(b, HandlePad, HandlePad, Width - HandlePad * 2, Height - HandlePad * 2);

            using (var font = MakeFont())
            {
                DrawSearchHighlight(g, font);
                DrawTextSelection(g, font);
                DrawText(g, font);

                if (Focused)
                {
                    if (_textEditMode && _caretVisible) DrawCaret(g, font);

                    using (var pen = new Pen(Color.DodgerBlue, 2f))
                        g.DrawRectangle(pen, HandlePad, HandlePad,
                            Width - HandlePad * 2 - 1, Height - HandlePad * 2 - 1);

                    foreach (var h in new[] {
                        ResizeHandle.TopLeft,    ResizeHandle.TopRight,
                        ResizeHandle.MiddleLeft, ResizeHandle.MiddleRight,
                        ResizeHandle.BottomLeft, ResizeHandle.BottomRight })
                    {
                        var r = GetHandleRect(h);
                        g.FillRectangle(Brushes.DodgerBlue, r);
                        using (var pen = new Pen(Color.White, 1f))
                            g.DrawRectangle(pen, r);
                    }
                }
                else if (IsSelected)
                {
                    using (var pen = new Pen(Color.DodgerBlue, 1.5f) { DashStyle = DashStyle.Dash })
                        g.DrawRectangle(pen, HandlePad, HandlePad,
                            Width - HandlePad * 2 - 1, Height - HandlePad * 2 - 1);
                }
                else if (_text.Length == 0)
                {
                    using (var pen = new Pen(Color.FromArgb(160, 160, 160), 1f) { DashStyle = DashStyle.Dash })
                        g.DrawRectangle(pen, HandlePad, HandlePad,
                            Width - HandlePad * 2 - 1, Height - HandlePad * 2 - 1);
                }
            }

            DrawBorders(g);
        }

        private void DrawText(Graphics g, Font font)
        {
            if (_text.Length == 0)
            {
                using (var brush = new SolidBrush(Color.FromArgb(160, 160, 160)))
                {
                    var fmt = new StringFormat(StringFormat.GenericTypographic)
                        { FormatFlags = StringFormatFlags.NoWrap };
                    g.DrawString("TextField", font, brush,
                        new PointF(TextLeft, TextTop), fmt);
                }
                return;
            }
            EnsureCharStyle();

            float    lh     = font.GetHeight(g);
            float    startY = TextStartY(g, font);
            float    availW = Width - TextLeft - TextRight;
            string[] lns    = _text.Split('\n');

            // Check whether all non-newline chars share a single style
            FontStyle fs0 = _fontStyle;
            for (int i = 0; i < _charStyle.Length; i++)
                if (_text[i] != '\n') { fs0 = _charStyle[i]; break; }
            bool singleStyle = true;
            for (int i = 1; i < _charStyle.Length; i++)
                if (_text[i] != '\n' && _charStyle[i] != fs0) { singleStyle = false; break; }

            using (var noWrap = new StringFormat(StringFormat.GenericTypographic))
            using (var brush  = new SolidBrush(_textColor))
            {
                noWrap.FormatFlags = StringFormatFlags.NoWrap |
                                     StringFormatFlags.MeasureTrailingSpaces;
                if (_isRtl) noWrap.FormatFlags |= StringFormatFlags.DirectionRightToLeft;

                if (singleStyle)
                {
                    Font sf = fs0 != _fontStyle
                        ? new Font(_fontFamily, _fontSizePt, fs0, GraphicsUnit.Point)
                        : null;
                    try
                    {
                        Font df = sf ?? font;
                        for (int li = 0; li < lns.Length; li++)
                        {
                            string ln    = lns[li];
                            float  y     = startY + li * lh;
                            float  lineW = TrWidth(g, ln, df);
                            float  x;
                            if (_isRtl)
                                x = Width - TextRight - lineW;
                            else switch (_hAlign)
                            {
                                case StringAlignment.Far:    x = TextLeft + availW - lineW; break;
                                case StringAlignment.Center: x = TextLeft + (availW - lineW) / 2f; break;
                                default:                     x = TextLeft; break;
                            }
                            if (ln.Length > 0)
                                g.DrawString(ln, df, brush, new PointF(x, y), noWrap);
                        }
                    }
                    finally { sf?.Dispose(); }
                    return;
                }

                // Mixed-style: draw run by run per line
                int charIdx = 0;
                for (int li = 0; li < lns.Length; li++)
                {
                    string ln    = lns[li];
                    float  y     = startY + li * lh;
                    float  lineW = MeasureLineWidth(g, ln, charIdx);
                    float  x;
                    if (_isRtl)
                        x = Width - TextRight - lineW;
                    else switch (_hAlign)
                    {
                        case StringAlignment.Far:    x = TextLeft + availW - lineW; break;
                        case StringAlignment.Center: x = TextLeft + (availW - lineW) / 2f; break;
                        default:                     x = TextLeft; break;
                    }

                    int runIdx = charIdx;
                    while (runIdx < charIdx + ln.Length)
                    {
                        FontStyle rs     = runIdx < _charStyle.Length ? _charStyle[runIdx] : _fontStyle;
                        int       runEnd = runIdx + 1;
                        while (runEnd < charIdx + ln.Length &&
                               runEnd < _charStyle.Length && _charStyle[runEnd] == rs) runEnd++;
                        string seg = ln.Substring(runIdx - charIdx, runEnd - runIdx);
                        using (var rf = new Font(_fontFamily, _fontSizePt, rs, GraphicsUnit.Point))
                        {
                            g.DrawString(seg, rf, brush, new PointF(x, y), noWrap);
                            x += TrWidth(g, seg, rf);
                        }
                        runIdx = runEnd;
                    }
                    charIdx += ln.Length + 1;
                }
            }
        }

        private float MeasureLineWidth(Graphics g, string ln, int charIdx)
        {
            float w      = 0f;
            int   runIdx = charIdx;
            while (runIdx < charIdx + ln.Length)
            {
                FontStyle rs     = runIdx < _charStyle.Length ? _charStyle[runIdx] : _fontStyle;
                int       runEnd = runIdx + 1;
                while (runEnd < charIdx + ln.Length &&
                       runEnd < _charStyle.Length && _charStyle[runEnd] == rs) runEnd++;
                string seg = ln.Substring(runIdx - charIdx, runEnd - runIdx);
                using (var rf = new Font(_fontFamily, _fontSizePt, rs, GraphicsUnit.Point))
                    w += TrWidth(g, seg, rf);
                runIdx = runEnd;
            }
            return w;
        }

        // ── Selection highlight ───────────────────────────────────────────────
        private void DrawTextSelection(Graphics g, Font font)
        {
            if (!HasSelection || !_textEditMode) return;
            int selStart = SelStart, selEnd = SelEnd;
            float lh = font.GetHeight(g);
            string[] lines = _text.Split('\n');
            float startY = TextStartY(g, font);

            using (var selBrush = new SolidBrush(Color.FromArgb(100, 51, 153, 255)))
            {
                int idx = 0;
                for (int li = 0; li < lines.Length; li++)
                {
                    string ln        = lines[li];
                    int    lineStart = idx;
                    int    lineEnd   = idx + ln.Length;
                    float  y         = startY + li * lh;

                    int  cs         = Math.Max(selStart, lineStart) - lineStart;
                    int  ce         = Math.Min(selEnd,   lineEnd)   - lineStart;
                    bool newlineSel = selEnd > lineEnd && selStart <= lineEnd && li < lines.Length - 1;

                    if (cs < ce || newlineSel)
                    {
                        float x1 = MeasurePrefixWidth(g, font, ln, idx, cs);
                        float x2 = MeasurePrefixWidth(g, font, ln, idx, ce);
                        if (newlineSel) x2 = Math.Max(x2, x1 + 6f);

                        float rw = Math.Max(2f, x2 - x1);
                        float rx = _isRtl ? CharX(g, font, ln, x2) : CharX(g, font, ln, x1);
                        g.FillRectangle(selBrush, rx, y, rw, lh);
                    }

                    idx += ln.Length + 1;
                }
            }
        }

        private void DrawSearchHighlight(Graphics g, Font font)
        {
            if (string.IsNullOrEmpty(_searchHighlight) || _text.Length == 0) return;
            string   pattern = _searchHighlight;
            float    lh      = font.GetHeight(g);
            string[] lines   = _text.Split('\n');
            float    startY  = TextStartY(g, font);

            using (var brush = new SolidBrush(Color.FromArgb(180, 255, 210, 0)))
            {
                for (int li = 0; li < lines.Length; li++)
                {
                    string ln    = lines[li];
                    float  lineY = startY + li * lh;
                    int    pos   = 0;
                    while (pos <= ln.Length - pattern.Length)
                    {
                        int match = ln.IndexOf(pattern, pos, StringComparison.Ordinal);
                        if (match < 0) break;
                        float x1 = match > 0 ? TrWidth(g, ln.Substring(0, match), font) : 0f;
                        float x2 = TrWidth(g, ln.Substring(0, match + pattern.Length), font);
                        float rw = Math.Max(2f, x2 - x1);
                        float rx = _isRtl ? CharX(g, font, ln, x2) : CharX(g, font, ln, x1);
                        g.FillRectangle(brush, rx, lineY, rw, lh);
                        pos = match + pattern.Length;
                    }
                }
            }
        }

        private void DrawBorders(Graphics g)
        {
            if (_borderWidth <= 0) return;
            using (var pen = new Pen(_borderColor, _borderWidth))
            {
                float half = _borderWidth / 2f;
                float l = HandlePad, r = Width  - HandlePad;
                float t = HandlePad, b = Height - HandlePad;

                // Vertical lines stop at the centerline of adjacent horizontal borders.
                float vy0 = _borderTop    ? t + half : t;
                float vy1 = _borderBottom ? b - half : b;

                // GDI+ renders line endpoints inclusively. The horizontal lines start at l
                // (aligning with the left border's pixel) but must end at r - borderWidth,
                // not r, so they don't extend one pixel past the right border.
                float hx1 = _borderRight ? r - _borderWidth : r;

                if (_borderTop)    g.DrawLine(pen, l,        t + half, hx1,      t + half);
                if (_borderBottom) g.DrawLine(pen, l,        b - half, hx1,      b - half);
                if (_borderLeft)   g.DrawLine(pen, l + half, vy0,      l + half, vy1);
                if (_borderRight)  g.DrawLine(pen, r - half, vy0,      r - half, vy1);
            }
        }

        // ── Caret ─────────────────────────────────────────────────────────────
        private void DrawCaret(Graphics g, Font font)
        {
            var   pos = CursorScreenPos(g, font);
            float lh  = font.GetHeight(g);
            using (var pen = new Pen(Color.Black, 1.5f))
                g.DrawLine(pen, pos.X, pos.Y + 1f, pos.X, pos.Y + lh - 1f);
        }

        private PointF CursorScreenPos(Graphics g, Font font)
        {
            GetLineCol(_cursorPos, out int line, out int col);
            string[] lines = _text.Split('\n');
            string   ln    = line < lines.Length ? lines[line] : "";
            int      prefixLen = Math.Min(col, ln.Length);

            int lineBase = 0;
            for (int i = 0; i < line; i++) lineBase += lines[i].Length + 1;

            float lh          = font.GetHeight(g);
            float startY      = TextStartY(g, font);
            float beforeWidth = MeasurePrefixWidth(g, font, ln, lineBase, prefixLen);
            return new PointF(CharX(g, font, ln, beforeWidth), startY + line * lh);
        }

        private int TextIndexAt(Point p)
        {
            using (var font = MakeFont())
            using (var g    = CreateGraphics())
            {
                float    lh     = font.GetHeight(g);
                float    startY = TextStartY(g, font);
                string[] lines  = _text.Split('\n');
                int      li     = Math.Max(0, Math.Min((int)((p.Y - startY) / lh), lines.Length - 1));

                int baseIdx = 0;
                for (int i = 0; i < li; i++) baseIdx += lines[i].Length + 1;

                string ln      = lines[li];
                float  best    = float.MaxValue;
                int    bestCol = ln.Length;

                for (int col = 0; col <= ln.Length; col++)
                {
                    float w  = MeasurePrefixWidth(g, font, ln, baseIdx, col);
                    float cx = CharX(g, font, ln, w);
                    float d  = Math.Abs(p.X - cx);
                    if (d < best) { best = d; bestCol = col; }
                }

                return Math.Min(baseIdx + bestCol, _text.Length);
            }
        }

        private float MeasurePrefixWidth(Graphics g, Font fallbackFont, string ln,
                                         int lineCharOffset, int col)
        {
            if (col <= 0) return 0f;
            EnsureCharStyle();

            int end = lineCharOffset + col;
            FontStyle fs0 = lineCharOffset < _charStyle.Length ? _charStyle[lineCharOffset] : _fontStyle;
            bool uniform = true;
            for (int i = lineCharOffset + 1; i < end; i++)
            {
                FontStyle fi = i < _charStyle.Length ? _charStyle[i] : _fontStyle;
                if (fi != fs0) { uniform = false; break; }
            }

            if (uniform)
            {
                Font rf = fs0 != _fontStyle
                    ? new Font(_fontFamily, _fontSizePt, fs0, GraphicsUnit.Point)
                    : null;
                try   { return TrWidth(g, ln.Substring(0, col), rf ?? fallbackFont); }
                finally { rf?.Dispose(); }
            }

            float w   = 0f;
            int   idx = lineCharOffset;
            while (idx < end)
            {
                FontStyle rs     = idx < _charStyle.Length ? _charStyle[idx] : _fontStyle;
                int       runEnd = idx + 1;
                while (runEnd < end && runEnd < _charStyle.Length && _charStyle[runEnd] == rs) runEnd++;
                string seg = ln.Substring(idx - lineCharOffset, runEnd - idx);
                using (var rf = new Font(_fontFamily, _fontSizePt, rs, GraphicsUnit.Point))
                    w += TrWidth(g, seg, rf);
                idx = runEnd;
            }
            return w;
        }

        private void EnsureCharStyle()
        {
            if (_charStyle == null || _charStyle.Length != _text.Length)
            {
                var ns   = new FontStyle[_text.Length];
                int copy = _charStyle != null ? Math.Min(_charStyle.Length, ns.Length) : 0;
                for (int i = 0; i < copy; i++) ns[i] = _charStyle[i];
                for (int i = copy; i < ns.Length; i++) ns[i] = _fontStyle;
                _charStyle = ns;
            }
        }

        private void CharStyleInsert(int pos, int count)
        {
            EnsureCharStyle();
            var ns = new FontStyle[_charStyle.Length + count];
            Array.Copy(_charStyle, 0, ns, 0, pos);
            for (int i = pos; i < pos + count; i++) ns[i] = _fontStyle;
            Array.Copy(_charStyle, pos, ns, pos + count, _charStyle.Length - pos);
            _charStyle = ns;
        }

        private void CharStyleDelete(int pos, int count)
        {
            EnsureCharStyle();
            if (count <= 0 || pos >= _charStyle.Length) return;
            count = Math.Min(count, _charStyle.Length - pos);
            var ns = new FontStyle[_charStyle.Length - count];
            Array.Copy(_charStyle, 0, ns, 0, pos);
            if (pos < ns.Length)
                Array.Copy(_charStyle, pos + count, ns, pos, ns.Length - pos);
            _charStyle = ns;
        }

        public void ApplyStyleToRange(int start, int end, FontStyle style)
        {
            if (start >= end) return;
            EnsureCharStyle();
            start = Math.Max(0, start);
            end   = Math.Min(_charStyle.Length, end);
            for (int i = start; i < end; i++)
                if (_charStyle[i] != SoftBreakStyle) // preserve auto-wrap \n markers
                    _charStyle[i] = style;
            Invalidate();
        }

        // Compact style string for serialization: one digit ('0'-'3') per character
        // in _text, using '0' at soft-break \n positions. Empty when all chars use
        // the box default style (the common case).
        public string CharStyleData
        {
            get
            {
                EnsureCharStyle();
                bool anyNonDefault = false;
                for (int i = 0; i < _charStyle.Length; i++)
                    if (_charStyle[i] != SoftBreakStyle && _charStyle[i] != _fontStyle)
                    { anyNonDefault = true; break; }
                if (!anyNonDefault) return "";
                var sb = new System.Text.StringBuilder(_charStyle.Length);
                for (int i = 0; i < _charStyle.Length; i++)
                    sb.Append(_charStyle[i] == SoftBreakStyle ? '0' : (char)('0' + (int)_charStyle[i]));
                return sb.ToString();
            }
            set
            {
                if (string.IsNullOrEmpty(value)) return;
                EnsureCharStyle();
                for (int i = 0; i < _charStyle.Length && i < value.Length; i++)
                    if (_charStyle[i] != SoftBreakStyle && char.IsDigit(value[i]))
                        _charStyle[i] = (FontStyle)(value[i] - '0');
                Invalidate();
            }
        }

        // ── Word wrap ─────────────────────────────────────────────────────────
        // After inserting text, call this to hard-wrap the current line if it
        // exceeds the available box width. Returns true if a \n was inserted.
        private bool AutoWrapIfNeeded()
        {
            float availW = Width - TextLeft - TextRight;
            if (availW <= 1f) return false;

            GetLineCol(_cursorPos, out int lineIdx, out int _unused);
            string[] lines = _text.Split('\n');
            if (lineIdx >= lines.Length) return false;
            string ln = lines[lineIdx];
            if (ln.Length == 0) return false;

            int lineStart = 0;
            for (int i = 0; i < lineIdx; i++) lineStart += lines[i].Length + 1;

            try
            {
                using (var font = MakeFont())
                using (var g    = CreateGraphics())
                {
                    if (MeasurePrefixWidth(g, font, ln, lineStart, ln.Length) <= availW)
                        return false;

                    // Find the last character index that still fits (overflowAt chars fit)
                    int overflowAt = ln.Length;
                    for (int i = 1; i <= ln.Length; i++)
                    {
                        if (MeasurePrefixWidth(g, font, ln, lineStart, i) > availW)
                        {
                            overflowAt = i - 1;
                            break;
                        }
                    }

                    // Walk back to find the last space within the fitting portion
                    int spaceIdx = -1;
                    for (int j = overflowAt - 1; j >= 0; j--)
                    {
                        if (ln[j] == ' ') { spaceIdx = j; break; }
                    }

                    int  insertPos;
                    bool replaceSpace;
                    if (spaceIdx >= 0)
                    {
                        insertPos    = lineStart + spaceIdx;
                        replaceSpace = true;
                    }
                    else
                    {
                        insertPos    = lineStart + Math.Max(1, overflowAt);
                        replaceSpace = false;
                    }

                    // Always insert \n (never replace the space); the space stays as
                    // a trailing char on the previous line so stripping the \n restores the text exactly.
                    int nlPos = (replaceSpace && insertPos < _text.Length && _text[insertPos] == ' ')
                        ? insertPos + 1   // insert after the space
                        : insertPos;      // insert before the overflowing word
                    CharStyleInsert(nlPos, 1);
                    _text = _text.Substring(0, nlPos) + '\n' + _text.Substring(nlPos);
                    if (_cursorPos >= nlPos) _cursorPos++;
                    _charStyle[nlPos] = SoftBreakStyle; // mark as auto-wrap break
                    _selectionAnchor  = _cursorPos;
                    return true;
                }
            }
            catch { return false; }
        }

        // Strip all auto-wrap \n breaks, then re-wrap at the current box width.
        // Called when the box is manually resized so text reflows to the new width.
        private void ReflowText(bool fitAfter = true)
        {
            if (_text.Length == 0) return;
            EnsureCharStyle();

            // Remove every \n that was auto-inserted (identified by SoftBreakStyle sentinel)
            var sb     = new System.Text.StringBuilder(_text.Length);
            var styles = new System.Collections.Generic.List<FontStyle>(_text.Length);
            int cursorAdj = 0, anchorAdj = 0;
            for (int i = 0; i < _text.Length; i++)
            {
                bool isSoft = _text[i] == '\n' &&
                              i < _charStyle.Length &&
                              _charStyle[i] == SoftBreakStyle;
                if (isSoft)
                {
                    if (i < _cursorPos)       cursorAdj++;
                    if (i < _selectionAnchor) anchorAdj++;
                    continue;
                }
                sb.Append(_text[i]);
                styles.Add(i < _charStyle.Length ? _charStyle[i] : _fontStyle);
            }
            _text            = sb.ToString();
            _charStyle       = styles.ToArray();
            _cursorPos       = Math.Min(Math.Max(0, _cursorPos       - cursorAdj), _text.Length);
            _selectionAnchor = Math.Min(Math.Max(0, _selectionAnchor - anchorAdj), _text.Length);

            // Re-apply word wrap for all hard lines at the current width
            float availW = Width - TextLeft - TextRight;
            if (availW > 1f && _text.Length > 0 && IsHandleCreated)
            {
                try
                {
                    using (var font = MakeFont())
                    using (var g    = CreateGraphics())
                    {
                        int safety = 0;
                        int pos    = 0;
                        while (pos < _text.Length && safety++ < 5000)
                        {
                            int nlIdx   = _text.IndexOf('\n', pos);
                            int lineEnd = nlIdx >= 0 ? nlIdx : _text.Length;
                            string ln   = _text.Substring(pos, lineEnd - pos);

                            if (ln.Length == 0 || MeasurePrefixWidth(g, font, ln, pos, ln.Length) <= availW)
                            {
                                if (nlIdx < 0) break;
                                pos = nlIdx + 1;
                                continue;
                            }

                            // Find last fitting char count
                            int overflowAt = ln.Length;
                            for (int i = 1; i <= ln.Length; i++)
                                if (MeasurePrefixWidth(g, font, ln, pos, i) > availW) { overflowAt = i - 1; break; }

                            int spaceIdx = -1;
                            for (int j = overflowAt - 1; j >= 0; j--)
                                if (ln[j] == ' ') { spaceIdx = j; break; }

                            int  insertPos;
                            bool replaceSpace;
                            if (spaceIdx >= 0) { insertPos = pos + spaceIdx; replaceSpace = true; }
                            else               { insertPos = pos + Math.Max(1, overflowAt); replaceSpace = false; }

                            int nlPos = (replaceSpace && insertPos < _text.Length && _text[insertPos] == ' ')
                                ? insertPos + 1
                                : insertPos;
                            CharStyleInsert(nlPos, 1);
                            _text = _text.Substring(0, nlPos) + '\n' + _text.Substring(nlPos);
                            if (_cursorPos       >= nlPos) _cursorPos++;
                            if (_selectionAnchor >= nlPos) _selectionAnchor++;
                            _charStyle[nlPos] = SoftBreakStyle;

                            // Advance past the \n; second half checked on next iteration
                            pos = nlPos + 1;
                        }
                    }
                }
                catch { }
            }

            _cursorPos       = Math.Min(_cursorPos,       _text.Length);
            _selectionAnchor = Math.Min(_selectionAnchor, _text.Length);
            if (fitAfter) FitSize();
            Invalidate();
        }

        // ── Multi-line helpers ────────────────────────────────────────────────
        private void GetLineCol(int idx, out int line, out int col)
        {
            line = 0; col = 0;
            for (int i = 0; i < idx && i < _text.Length; i++)
            {
                if (_text[i] == '\n') { line++; col = 0; }
                else col++;
            }
        }

        private int LineColToIndex(int line, int col)
        {
            int idx = 0, cur = 0;
            while (cur < line && idx < _text.Length)
            {
                if (_text[idx] == '\n') cur++;
                idx++;
            }
            return Math.Min(idx + col, _text.Length);
        }

        // ── Word / line selection helpers ─────────────────────────────────────
        private void SelectWordAt(int idx)
        {
            if (_text.Length == 0) return;
            if (idx >= _text.Length) idx = _text.Length - 1;

            int start = idx;
            while (start > 0 && !char.IsWhiteSpace(_text[start - 1]))
                start--;

            int end = idx;
            while (end < _text.Length && !char.IsWhiteSpace(_text[end]))
                end++;

            if (start < end)
            {
                _selectionAnchor = start;
                _cursorPos       = end;
                _caretVisible    = true;
                Invalidate();
            }
        }

        private void SelectLineAt(int idx)
        {
            int start = idx;
            while (start > 0 && _text[start - 1] != '\n')
                start--;

            int end = idx < _text.Length ? idx : _text.Length;
            while (end < _text.Length && _text[end] != '\n')
                end++;

            _selectionAnchor = start;
            _cursorPos       = end;
            _caretVisible    = true;
            Invalidate();
        }

        private void SelectAll()
        {
            if (_text.Length == 0) return;
            _selectionAnchor = 0;
            _cursorPos       = _text.Length;
            _caretVisible    = true;
            Invalidate();
        }

        // ── Resize handle geometry ────────────────────────────────────────────
        private Rectangle GetHandleRect(ResizeHandle h)
        {
            int w = Width, ht = Height;
            const int cs = 8;  // corner square side
            const int ew = 12; // edge handle long dimension
            int ep = HandlePad; // edge handle short dimension (fills the margin)
            switch (h)
            {
                // Corner squares straddle the content-box border
                case ResizeHandle.TopLeft:      return new Rectangle(ep - cs/2,      ep - cs/2,      cs, cs);
                case ResizeHandle.TopRight:     return new Rectangle(w - ep - cs/2,  ep - cs/2,      cs, cs);
                case ResizeHandle.BottomLeft:   return new Rectangle(ep - cs/2,      ht - ep - cs/2, cs, cs);
                case ResizeHandle.BottomRight:  return new Rectangle(w - ep - cs/2,  ht - ep - cs/2, cs, cs);
                // Edge rectangles live entirely in the transparent margin strip
                case ResizeHandle.MiddleLeft:   return new Rectangle(0,       ht/2 - ew/2, ep, ew);
                case ResizeHandle.MiddleRight:  return new Rectangle(w - ep,  ht/2 - ew/2, ep, ew);
                default: return Rectangle.Empty;
            }
        }

        private static readonly ResizeHandle[] _resizeHandles =
        {
            ResizeHandle.TopLeft,   ResizeHandle.TopRight,
            ResizeHandle.MiddleLeft, ResizeHandle.MiddleRight,
            ResizeHandle.BottomLeft, ResizeHandle.BottomRight
        };

        private ResizeHandle HitTest(Point p)
        {
            foreach (var h in _resizeHandles)
                if (GetHandleRect(h).Contains(p)) return h;
            return ResizeHandle.None;
        }

        private static Cursor CursorFor(ResizeHandle h)
        {
            switch (h)
            {
                case ResizeHandle.TopCenter:
                case ResizeHandle.BottomCenter: return Cursors.SizeNS;
                case ResizeHandle.MiddleLeft:
                case ResizeHandle.MiddleRight:  return Cursors.SizeWE;
                case ResizeHandle.TopLeft:
                case ResizeHandle.BottomRight:  return Cursors.SizeNWSE;
                case ResizeHandle.TopRight:
                case ResizeHandle.BottomLeft:   return Cursors.SizeNESW;
                default:                        return Cursors.SizeAll;
            }
        }

        // ── Mouse ─────────────────────────────────────────────────────────────
        protected override void OnMouseDown(MouseEventArgs e)
        {
            // Capture screen coords BEFORE base.OnMouseDown: WinForms fires focus
            // internally which can trigger AutoScroll and physically move the HWND,
            // making a post-move PointToScreen give the wrong drag anchor.
            _mouseDownScreen = PointToScreen(e.Location);
            base.OnMouseDown(e);
            // WM_SETFOCUS fires before WM_LBUTTONDOWN, so OnGotFocus (and the canvas GotFocus
            // handler that sets IsSelected) has already run by the time we get here.
            // _justGotFocus tells us whether this is the very click that gained focus.
            bool wasInTextMode = _textEditMode;
            bool justGotFocus  = _justGotFocus;
            _justGotFocus      = false; // consume — next click on this already-focused box won't see it
            Focus();

            // e.Clicks from WinForms is unreliable beyond 2 — track rapid consecutive clicks ourselves.
            {
                int   nowTick = Environment.TickCount;
                Point scr     = _mouseDownScreen; // reuse pre-focus-scroll capture
                Size  dblSz   = SystemInformation.DoubleClickSize;
                bool  same    = Math.Abs(scr.X - _lastClickScreen.X) <= dblSz.Width / 2 &&
                                Math.Abs(scr.Y - _lastClickScreen.Y) <= dblSz.Height / 2;
                bool  inTime  = unchecked(nowTick - _lastClickTick) <= SystemInformation.DoubleClickTime;
                _ownClickCount   = (same && inTime) ? _ownClickCount + 1 : 1;
                _lastClickTick   = nowTick;
                _lastClickScreen = scr;
            }

            _startLocation   = Location; // captured after any focus-induced scroll
            _startSize       = Size;
            _activeHandle    = HitTest(e.Location);
            _resizing        = _activeHandle != ResizeHandle.None;
            _dragging        = !_resizing && !_textEditMode;
            Capture          = true;

            if (_dragging)
            {
                var canvas = Parent as CanvasPanel;
                if (canvas != null && IsSelected && canvas.SelectedControls.Count > 1)
                {
                    _groupStartLocations = new System.Collections.Generic.Dictionary<Control, Point>();
                    foreach (var c in canvas.SelectedControls)
                        _groupStartLocations[c] = c.Location;
                }
                else
                    _groupStartLocations = null;
            }

            if (_activeHandle == ResizeHandle.None)
            {
                int idx = TextIndexAt(e.Location);

                if (wasInTextMode)
                {
                    // Already editing: cursor / word / all by click count.
                    _pendingClickIdx = -1;
                    if ((ModifierKeys & Keys.Shift) != 0)
                    { _cursorPos = idx; _caretVisible = true; Invalidate(); }
                    else if (_ownClickCount >= 3) { SelectAll(); }
                    else if (_ownClickCount == 2) { SelectWordAt(idx); }
                    else                          { EnterTextMode(idx); }
                }
                else if (!justGotFocus)
                {
                    if (_ownClickCount >= 3)
                    {
                        // Triple-click before text mode was established: enter and select all.
                        EnterTextMode(idx);
                        SelectAll();
                    }
                    else
                    {
                        // Box was already focused/selected; this click places the cursor.
                        // Defer to mouse-up so the user can still drag the box.
                        _pendingClickIdx = idx;
                    }
                }
                // else justGotFocus: this click is what selected the box — no cursor yet.
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            // Text-mode drag extends selection
            if (_textEditMode && e.Button == MouseButtons.Left && !_resizing)
            {
                _cursorPos = TextIndexAt(e.Location);
                _caretVisible = true;
                Invalidate();
                return;
            }

            if (!_dragging && !_resizing)
            {
                var h = HitTest(e.Location);
                Cursor = (_textEditMode && h == ResizeHandle.None) ? Cursors.IBeam : CursorFor(h);
                return;
            }

            var screen = PointToScreen(e.Location);
            int dx = screen.X - _mouseDownScreen.X;
            int dy = screen.Y - _mouseDownScreen.Y;

            if (_dragging)
            {
                var canvas = Parent as ScrollableControl;
                int minX   = canvas?.AutoScrollPosition.X ?? 0;
                int minY   = canvas?.AutoScrollPosition.Y ?? 0;

                if (_groupStartLocations != null)
                {
                    foreach (var kvp in _groupStartLocations)
                        kvp.Key.Location = new Point(
                            Math.Max(minX, kvp.Value.X + dx),
                            Math.Max(minY, kvp.Value.Y + dy));
                }
                else
                {
                    Location = new Point(
                        Math.Max(minX, _startLocation.X + dx),
                        Math.Max(minY, _startLocation.Y + dy));
                }
                return;
            }

            int nx = _startLocation.X, ny = _startLocation.Y;
            int nw = _startSize.Width,  nh = _startSize.Height;
            int mw = MinimumSize.Width,  mh = MinimumSize.Height;

            switch (_activeHandle)
            {
                case ResizeHandle.TopCenter:
                case ResizeHandle.BottomCenter: break; // height is auto-managed
                case ResizeHandle.MiddleLeft:
                    nw = Math.Max(mw, _startSize.Width - dx);
                    nx = _startLocation.X + (_startSize.Width - nw);
                    break;
                case ResizeHandle.MiddleRight:
                    nw = Math.Max(mw, _startSize.Width + dx);
                    break;
                case ResizeHandle.TopLeft:
                case ResizeHandle.BottomLeft:
                    nw = Math.Max(mw, _startSize.Width - dx);
                    nx = _startLocation.X + (_startSize.Width - nw);
                    break;
                case ResizeHandle.TopRight:
                case ResizeHandle.BottomRight:
                    nw = Math.Max(mw, _startSize.Width + dx);
                    break;
            }

            SetBounds(nx, ny, nw, nh);
            if (nw != _startSize.Width) ReflowText(fitAfter: false);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            bool wasResizingWidth = _resizing && _startSize.Width != Width;
            _dragging = _resizing = false;
            _activeHandle        = ResizeHandle.None;
            _groupStartLocations = null;
            Capture              = false;
            if (wasResizingWidth) ReflowText();

            if (_pendingClickIdx >= 0)
            {
                var ds  = SystemInformation.DragSize;
                var scr = PointToScreen(e.Location);
                if (Math.Abs(scr.X - _mouseDownScreen.X) <= ds.Width &&
                    Math.Abs(scr.Y - _mouseDownScreen.Y) <= ds.Height)
                    EnterTextMode(_pendingClickIdx);
                _pendingClickIdx = -1;
            }
        }

        // ── Keyboard ──────────────────────────────────────────────────────────
        protected override bool IsInputKey(Keys keyData) => true;

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);
            char c = e.KeyChar;
            if (c < ' ') return; // skip Backspace, Escape, Enter, Ctrl+A/C/V/X and all other control chars

            if (!_textEditMode) EnterTextMode();

            if (HasSelection) DeleteSelection();

            string output = c.ToString();

            if (output == null) { e.Handled = true; return; }

            CharStyleInsert(_cursorPos, output.Length);
            _text            = _text.Substring(0, _cursorPos) + output + _text.Substring(_cursorPos);
            _cursorPos      += output.Length;
            _selectionAnchor = _cursorPos;
            _caretVisible    = true;
            FitSize();
            if (AutoWrapIfNeeded()) FitSize();
            Invalidate();
            e.Handled = true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.KeyCode)
            {
                case Keys.Delete:
                    if (!_textEditMode) DeleteSelf();
                    else if (HasSelection) DeleteSelection();
                    else if (_cursorPos < _text.Length)
                    {
                        CharStyleDelete(_cursorPos, 1);
                        _text = _text.Substring(0, _cursorPos) + _text.Substring(_cursorPos + 1);
                        _caretVisible = true; FitSize(); Invalidate();
                    }
                    e.Handled = true;
                    break;

                case Keys.Back:
                    if (_textEditMode)
                    {
                        if (HasSelection) DeleteSelection();
                        else if (_cursorPos > 0)
                        {
                            CharStyleDelete(_cursorPos - 1, 1);
                            _text = _text.Substring(0, _cursorPos - 1) + _text.Substring(_cursorPos);
                            _cursorPos--; _selectionAnchor = _cursorPos;
                            _caretVisible = true; FitSize(); Invalidate();
                        }
                    }
                    e.Handled = true;
                    break;

                case Keys.Enter:
                    FlushInputPending();
                    if (!_textEditMode) EnterTextMode();
                    if (HasSelection) DeleteSelection();
                    CharStyleInsert(_cursorPos, 1);
                    _text = _text.Substring(0, _cursorPos) + '\n' + _text.Substring(_cursorPos);
                    _cursorPos++; _selectionAnchor = _cursorPos;
                    _caretVisible = true; FitSize(); Invalidate();
                    e.Handled = true;
                    break;

                case Keys.Escape:
                    if (_textEditMode) ExitTextMode();
                    else Parent?.Focus();
                    e.Handled = true;
                    break;

                case Keys.Left:
                    FlushInputPending();
                    if (_textEditMode)
                    {
                        if (HasSelection && !e.Shift)
                        { _cursorPos = SelStart; _selectionAnchor = _cursorPos; _caretVisible = true; Invalidate(); }
                        else if (_cursorPos > 0)
                        { _cursorPos--; if (!e.Shift) _selectionAnchor = _cursorPos; _caretVisible = true; Invalidate(); }
                    }
                    e.Handled = true;
                    break;

                case Keys.Right:
                    FlushInputPending();
                    if (_textEditMode)
                    {
                        if (HasSelection && !e.Shift)
                        { _cursorPos = SelEnd; _selectionAnchor = _cursorPos; _caretVisible = true; Invalidate(); }
                        else if (_cursorPos < _text.Length)
                        { _cursorPos++; if (!e.Shift) _selectionAnchor = _cursorPos; _caretVisible = true; Invalidate(); }
                    }
                    e.Handled = true;
                    break;

                case Keys.Home:
                    if (_textEditMode)
                    {
                        GetLineCol(_cursorPos, out int hl, out _);
                        _cursorPos = LineColToIndex(hl, 0);
                        if (!e.Shift) _selectionAnchor = _cursorPos;
                        _caretVisible = true; Invalidate();
                    }
                    e.Handled = true;
                    break;

                case Keys.End:
                    if (_textEditMode)
                    {
                        GetLineCol(_cursorPos, out int el, out _);
                        string[] lns = _text.Split('\n');
                        _cursorPos = LineColToIndex(el, lns[Math.Min(el, lns.Length - 1)].Length);
                        if (!e.Shift) _selectionAnchor = _cursorPos;
                        _caretVisible = true; Invalidate();
                    }
                    e.Handled = true;
                    break;

                case Keys.Up:
                    if (_textEditMode)
                    {
                        GetLineCol(_cursorPos, out int ul, out int uc);
                        if (ul > 0)
                        {
                            string[] lns = _text.Split('\n');
                            _cursorPos = LineColToIndex(ul - 1, Math.Min(uc, lns[ul - 1].Length));
                            if (!e.Shift) _selectionAnchor = _cursorPos;
                            _caretVisible = true; Invalidate();
                        }
                    }
                    e.Handled = true;
                    break;

                case Keys.Down:
                    if (_textEditMode)
                    {
                        GetLineCol(_cursorPos, out int dl, out int dc);
                        string[] lns = _text.Split('\n');
                        if (dl < lns.Length - 1)
                        {
                            _cursorPos = LineColToIndex(dl + 1, Math.Min(dc, lns[dl + 1].Length));
                            if (!e.Shift) _selectionAnchor = _cursorPos;
                            _caretVisible = true; Invalidate();
                        }
                    }
                    e.Handled = true;
                    break;

                case Keys.A:
                    if (e.Control && _textEditMode)
                    { _selectionAnchor = 0; _cursorPos = _text.Length; _caretVisible = true; Invalidate(); }
                    e.Handled = true;
                    break;

                case Keys.C:
                    if (e.Control && _textEditMode && HasSelection)
                        Clipboard.SetText(SelectedText);
                    e.Handled = true;
                    break;

                case Keys.X:
                    if (e.Control && _textEditMode && HasSelection)
                    { Clipboard.SetText(SelectedText); DeleteSelection(); }
                    e.Handled = true;
                    break;

                case Keys.V:
                    if (e.Control && _textEditMode)
                    {
                        string clip = Clipboard.GetText();
                        if (!string.IsNullOrEmpty(clip))
                        {
                            clip = clip.Replace("\r\n", "\n").Replace('\r', '\n');
                            if (HasSelection) DeleteSelection();
                            CharStyleInsert(_cursorPos, clip.Length);
                            _text            = _text.Substring(0, _cursorPos) + clip + _text.Substring(_cursorPos);
                            _cursorPos      += clip.Length;
                            _selectionAnchor = _cursorPos;
                            _caretVisible    = true;
                            ReflowText();
                        }
                    }
                    e.Handled = true;
                    break;
            }
        }

        // ── Focus ─────────────────────────────────────────────────────────────
        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            _justGotFocus    = true;
            _textEditMode    = false;
            _selectionAnchor = _cursorPos;
            _caretVisible    = false;
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            ExitTextMode();
        }
    }
}
