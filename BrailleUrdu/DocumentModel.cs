using System.Collections.Generic;
using System.Drawing;

namespace BrailleUrdu
{
    // Per-window document state.  Each Form1 instance owns one Document; the
    // static properties delegate to whichever window is currently active.
    public class Document
    {
        // ── Ambient context ───────────────────────────────────────────────────
        private static Document _current = new Document();

        public static void SetCurrent(Document doc) { if (doc != null) _current = doc; }

        // ── Static passthrough accessors — all existing call-sites unchanged ──
        public static List<DocumentPage> Pages          => _current._pages;
        public static int CurrentPageIndex
        {
            get => _current._currentPageIndex;
            set => _current._currentPageIndex = value;
        }
        public static DocumentPage MasterOdd  => _current._masterOdd;
        public static DocumentPage MasterEven => _current._masterEven;
        public static bool IsOnMasterPage     => _current._currentPageIndex < 0;
        public static DocumentPage CurrentPage =>
            _current._currentPageIndex == -1 ? _current._masterOdd  :
            _current._currentPageIndex == -2 ? _current._masterEven :
            _current._pages[_current._currentPageIndex];

        public static LanguageSpec Spec { get; } = LanguageSpec.Load("en");

        public static void AddPage()
        {
            _current._pages.Add(new DocumentPage());
        }

        public static void RemovePage(int index)
        {
            var p = _current._pages;
            if (p.Count <= 1 || index < 0 || index >= p.Count) return;
            p.RemoveAt(index);
            if (_current._currentPageIndex >= p.Count)
                _current._currentPageIndex = p.Count - 1;
        }

        // ── Per-instance data ─────────────────────────────────────────────────
        private readonly List<DocumentPage> _pages            = new List<DocumentPage> { new DocumentPage() };
        private          int                _currentPageIndex = 0;
        private readonly DocumentPage       _masterOdd        = new DocumentPage();
        private readonly DocumentPage       _masterEven       = new DocumentPage();
    }

    // Per-language display name, print font, and text direction
    public static class LanguageInfo
    {
        private struct LangMeta { public string Name, Font; public bool Rtl; }

        private static readonly Dictionary<string, LangMeta> _meta =
            new Dictionary<string, LangMeta>
            {
                ["en"] = new LangMeta { Name = "English", Font = "Calibri",                  Rtl = false },
                ["ur"] = new LangMeta { Name = "Urdu",    Font = "Jameel Noori Nastaleeq",   Rtl = true  },
                ["ar"] = new LangMeta { Name = "Arabic",  Font = "Arabic Typesetting",        Rtl = true  },
                ["si"] = new LangMeta { Name = "Sindhi",  Font = "Jameel Noori Nastaleeq",   Rtl = true  },
            };

        public static string DisplayName(string code) =>
            _meta.TryGetValue(code, out var m) ? m.Name : code;

        public static string FontFor(string code) =>
            _meta.TryGetValue(code, out var m) ? m.Font : "Calibri";

        public static bool RtlFor(string code) =>
            _meta.TryGetValue(code, out var m) && m.Rtl;

        // All supported language codes in display order
        public static readonly string[] Codes = { "en", "ur", "ar", "si" };
    }

    // ── Page ─────────────────────────────────────────────────────────────────

    public class DocumentPage
    {
        // Page dimensions in mm — mutable so Document Setup can change them
        public static float WIDTH_MM  { get; set; } = 210f;
        public static float HEIGHT_MM { get; set; } = 297f;

        // Braille embosser constants (must match RawHelper escape sequence values)
        public const float DOT_SPACING_MM = 2.5f;
        public const float CELL_WIDTH_MM  = 6.0f;
        public const float LINE_HEIGHT_MM = 7.5f;

        // Margins = 6 dots from each edge: 6 × DOT_SPACING_MM = 6 × 2.5 = 15 mm
        public float MarginLeft   { get; set; } = 6 * DOT_SPACING_MM;  // 15 mm
        public float MarginRight  { get; set; } = 6 * DOT_SPACING_MM;  // 15 mm
        public float MarginTop    { get; set; } = 6 * DOT_SPACING_MM;  // 15 mm
        public float MarginBottom { get; set; } = 6 * DOT_SPACING_MM;  // 15 mm

        public List<PageElement> Elements { get; } = new List<PageElement>();
    }

    // ── Element base ─────────────────────────────────────────────────────────

    public abstract class PageElement
    {
        public float X        { get; set; }  // mm from page left
        public float Y        { get; set; }  // mm from page top
        public float Width    { get; set; }  // mm
        public float Height   { get; set; }  // mm
        public bool  Selected { get; set; }
    }

    // ── Braille text ──────────────────────────────────────────────────────────
    // Rendered with SimBraille font; position/size drive embosser dot coordinates.

    public class BrailleTextElement : PageElement
    {
        public string BrailleText { get; set; } = "";
    }

    // ── Print text (multi-language) ───────────────────────────────────────────
    // Supports LTR (English) and RTL (Urdu, Arabic) scripts.

    public class PrintTextElement : PageElement
    {
        public string Text        { get; set; } = "";
        public string FontName    { get; set; } = "Calibri";
        public float  FontSize    { get; set; } = 12f;
        public string Language    { get; set; } = "en";   // en, ur, ar, ...
        public bool   RightToLeft { get; set; } = false;
    }

    // ── Raster image ─────────────────────────────────────────────────────────

    public class ImageElement : PageElement
    {
        public Image Bitmap { get; set; }
    }

    // ── Braille tactile graphic ───────────────────────────────────────────────
    // DotGrid[col, row] == true means the dot at that grid position is raised.
    // Physical spacing between dots is DOT_SPACING_MM (2.5 mm).

    public class TactileGraphicElement : PageElement
    {
        public bool[,] DotGrid { get; set; } = new bool[0, 0];
    }
}
