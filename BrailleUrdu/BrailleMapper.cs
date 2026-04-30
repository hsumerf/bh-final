namespace BrailleUrdu
{
    // Delegates to the active language spec; falls back to ASCII braille shorthand
    // so the BrailleTextBox always works with a standard keyboard (a→⠁, b→⠃, …).
    public static class BrailleMapper
    {
        public static string ToBraille(char c)
        {
            var spec = Document.Spec?.ToBraille(c) ?? "";
            if (spec.Length > 0) return spec;
            return LanguageSpec.ShorthandToBraille(c);
        }
    }
}
