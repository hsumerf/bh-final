namespace BrailleUrdu
{
    // Delegates to the active language spec; falls back to ASCII braille shorthand
    // so the BrailleTextBox always works with a standard keyboard (a→⠁, b→⠃, …).
    public static class BrailleMapper
    {
        // Braille capital indicator (U+2820 = ⠠)
        private const string CAP = "⠠";

        public static string ToBraille(char c)
        {
            if (char.IsUpper(c))
            {
                // Capital letter = capital indicator + lowercase braille cell
                string lower = Raw(char.ToLower(c));
                return lower.Length > 0 ? CAP + lower : "";
            }
            return Raw(c);
        }

        private static string Raw(char c)
        {
            var spec = Document.Spec?.ToBraille(c) ?? "";
            if (spec.Length > 0) return spec;
            return LanguageSpec.ShorthandToBraille(c);
        }

        // Converts a Unicode braille string back to typed text using the active language spec.
        public static string FromBraille(string brailleText)
            => Document.Spec?.FromBraille(brailleText) ?? brailleText ?? "";
    }
}
