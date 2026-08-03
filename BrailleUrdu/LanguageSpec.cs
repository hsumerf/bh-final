using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace BrailleUrdu
{
    // Loads ProgramData/ln/<code>/<code>.spec and converts typed keys to Unicode braille.
    // Spec line format:  brailleShorthand ≡ inputKey(s) ≡ mode
    //   mode: a=always  s=start-of-word  p=end-of-word  ('a' takes priority)
    public class LanguageSpec
    {
        // One parsed line from a .spec file.
        public struct RawLine
        {
            public bool   IsData;     // false for blank/comment/header lines
            public string Shorthand;  // ASCII shorthand, e.g. "a" or "al,lh"
            public string TypedKey;   // keyboard input string, e.g. "a" or "ا"
            public string Mode;       // "a" / "s" / "p"
            public string Raw;        // original line text (preserved verbatim for non-data lines)
        }

        // Single-char braille shorthand → Unicode braille cell
        private static readonly Dictionary<char, char> _sh = new Dictionary<char, char>
        {
            ['a']='⠁',['b']='⠃',['c']='⠉',['d']='⠙',['e']='⠑',
            ['f']='⠋',['g']='⠛',['h']='⠓',['i']='⠊',['j']='⠚',
            ['k']='⠅',['l']='⠇',['m']='⠍',['n']='⠝',['o']='⠕',
            ['p']='⠏',['q']='⠟',['r']='⠗',['s']='⠎',['t']='⠞',
            ['u']='⠥',['v']='⠧',['w']='⠺',['x']='⠭',['y']='⠽',
            ['z']='⠵',[' ']='⠀',
            ['1']='⠂',['2']='⠆',['3']='⠒',['4']='⠲',['5']='⠢',
            ['6']='⠖',['7']='⠶',['8']='⠦',['9']='⠔',['0']='⠴',
            ['@']='⠈',['.']='⠄',[',']='⠂',[';']='⠆',['-']='⠤',
            ['"']='⠐',['#']='⠼',['_']='⠸',['<']='⠣',['>']='⠜',
            ['/']='⠌',['\\']='⠘',['*']='⠡',['&']='⠯',['?']='⠹',
            ['!']='⠮',['%']='⠩',['$']='⠫',['^']='⠬',['=']='⠿',
            ['+']='⠬',['(']='⠷',[')']='⠾',['[']='⠪',[']']='⠻',
            ['\'']='⠄',['`']='⠄',
        };

        // Reverse: Unicode braille cell → ASCII shorthand char (first-seen wins for duplicates)
        private static readonly Dictionary<char, char> _shReverse = BuildShReverse();

        private static Dictionary<char, char> BuildShReverse()
        {
            var d = new Dictionary<char, char>();
            foreach (var kvp in _sh)
                if (!d.ContainsKey(kvp.Value))
                    d[kvp.Value] = kvp.Key;
            return d;
        }

        // input key(s) → Unicode braille string;  first mode='a' entry wins over later 'a' and all 's'/'p'
        private readonly Dictionary<string, string> _map        = new Dictionary<string, string>();
        private readonly HashSet<string>             _alwaysKeys = new HashSet<string>();

        // single shorthand char → typed key string (e.g. 'a' → "ا" for Urdu)
        private readonly Dictionary<char, string> _shToKey = new Dictionary<char, string>();

        // single shorthand char → typed key, but only when the typed key is a digit character
        private readonly Dictionary<char, string> _shToDigit = new Dictionary<char, string>();

        // Returns the typed key that corresponds to a single shorthand char, or "" if not found.
        public string TypedKeyFor(char sh) =>
            _shToKey.TryGetValue(sh, out var k) ? k : "";

        // Returns the digit typed key for a shorthand char, or "" if not a digit mapping.
        public string DigitKeyFor(char sh) =>
            _shToDigit.TryGetValue(sh, out var k) ? k : "";

        // braille string → input key(s), built lazily from _map
        private Dictionary<string, string> _reverseMap;

        // All lines from the loaded spec file (data rows and preserved comment/header rows).
        private readonly List<RawLine> _entries = new List<RawLine>();
        public IReadOnlyList<RawLine> Entries => _entries;

        public string LangCode { get; private set; }

        private LanguageSpec() { }

        // Full path to the spec file for a given language code.
        public static string GetSpecPath(string langCode) =>
            Path.Combine(
                Path.GetDirectoryName(Application.ExecutablePath) ?? "",
                "ProgramData", "ln", langCode, langCode + ".spec");

        public static LanguageSpec Load(string langCode)
        {
            var spec = new LanguageSpec();
            spec.LangCode = langCode;
            string path = GetSpecPath(langCode);

            if (!File.Exists(path)) return spec;

            foreach (var raw in File.ReadAllLines(path, Encoding.UTF8))
            {
                // Blank lines — preserve but not a data entry
                if (string.IsNullOrEmpty(raw))
                {
                    spec._entries.Add(new RawLine { Raw = raw });
                    continue;
                }

                var parts = raw.Split(new[] { "≡" }, StringSplitOptions.None);

                // Header / comment lines (< 3 parts, or either key field empty)
                if (parts.Length < 3 ||
                    string.IsNullOrEmpty(parts[0]) ||
                    string.IsNullOrEmpty(parts[1]))
                {
                    spec._entries.Add(new RawLine { Raw = raw });
                    continue;
                }

                string sh  = parts[0];  // braille shorthand
                string key = parts[1];  // typed input char(s)
                string mod = parts[2];  // a / s / p

                // Single-char shorthand → typed key (first-seen wins; used for phonetic text input)
                if (sh.Length == 1 && !spec._shToKey.ContainsKey(sh[0]))
                    spec._shToKey[sh[0]] = key;

                // Separate digit-only map: only stored when the typed key is a digit character
                if (sh.Length == 1 && key.Length == 1 && char.IsDigit(key[0])
                    && !spec._shToDigit.ContainsKey(sh[0]))
                    spec._shToDigit[sh[0]] = key;

                // Record as a data entry
                spec._entries.Add(new RawLine
                {
                    IsData    = true,
                    Shorthand = sh,
                    TypedKey  = key,
                    Mode      = mod,
                    Raw       = raw
                });

                string braille = ShorthandToUnicode(sh);
                if (string.IsNullOrEmpty(braille)) continue;

                // First 'a' entry wins; 'a' can promote an existing 's'/'p' entry but not another 'a'
                bool existingIsAlways = spec._alwaysKeys.Contains(key);
                if (!spec._map.ContainsKey(key) || (mod == "a" && !existingIsAlways))
                {
                    spec._map[key] = braille;
                    if (mod == "a") spec._alwaysKeys.Add(key);
                }
            }

            return spec;
        }

        // Writes edited data entries back to the spec file, preserving comment/header lines at the top.
        public void SaveEntries(IList<RawLine> newDataEntries)
        {
            var lines = new List<string>();

            // Comment and header lines preserved verbatim at the top
            foreach (var e in _entries)
                if (!e.IsData) lines.Add(e.Raw);

            // All data rows (possibly edited)
            foreach (var e in newDataEntries)
                lines.Add(e.Shorthand + "≡" + e.TypedKey + "≡" + e.Mode);

            File.WriteAllLines(GetSpecPath(LangCode), lines, Encoding.UTF8);

            // Refresh _entries to match the saved state
            _entries.Clear();
            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line)) { _entries.Add(new RawLine { Raw = line }); continue; }
                var parts = line.Split(new[] { "≡" }, StringSplitOptions.None);
                if (parts.Length < 3 || string.IsNullOrEmpty(parts[0]) || string.IsNullOrEmpty(parts[1]))
                    _entries.Add(new RawLine { Raw = line });
                else
                    _entries.Add(new RawLine
                    {
                        IsData    = true,
                        Shorthand = parts[0],
                        TypedKey  = parts[1],
                        Mode      = parts[2],
                        Raw       = line
                    });
            }
        }

        // Returns the Unicode braille string for a single typed character, or "" if not mapped.
        public string ToBraille(char c)
        {
            return _map.TryGetValue(c.ToString(), out var b) ? b : "";
        }

        // Converts a Unicode braille string back to the typed input representation.
        // ⠠ + cell → uppercase letter; other cells looked up in the reverse map.
        public string FromBraille(string brailleText)
        {
            if (string.IsNullOrEmpty(brailleText)) return "";
            if (_reverseMap == null)
            {
                _reverseMap = new Dictionary<string, string>();
                foreach (var kvp in _map)
                    if (!_reverseMap.ContainsKey(kvp.Value))
                        _reverseMap[kvp.Value] = kvp.Key;
            }

            var sb = new StringBuilder();
            int i  = 0;
            while (i < brailleText.Length)
            {
                char c = brailleText[i];

                // Capital indicator ⠠ + next cell → uppercase letter
                if (c == '⠠' && i + 1 < brailleText.Length)
                {
                    string cell = brailleText[i + 1].ToString();
                    if (_reverseMap.TryGetValue(cell, out string lk)
                        && lk.Length == 1 && char.IsLower(lk[0]))
                    {
                        sb.Append(char.ToUpper(lk[0]));
                        i += 2;
                        continue;
                    }
                }

                // Non-braille (space, newline, etc.) — pass through
                if (c < '⠀' || c > '⣿') { sb.Append(c); i++; continue; }

                // Greedy longest-match on braille cells
                bool found = false;
                for (int len = Math.Min(4, brailleText.Length - i); len >= 1; len--)
                {
                    string seq = brailleText.Substring(i, len);
                    if (_reverseMap.TryGetValue(seq, out string mapped))
                    { sb.Append(mapped); i += len; found = true; break; }
                }
                if (!found) { sb.Append(c); i++; }
            }
            return sb.ToString();
        }

        // Converts a single braille shorthand char (a-z, digits, symbols) to its Unicode braille cell.
        public static string ShorthandToBraille(char c)
        {
            return _sh.TryGetValue(c, out char cell) ? cell.ToString() : "";
        }

        // Converts an ASCII shorthand string to a Unicode braille string.
        public static string ShorthandToUnicode(string sh)
        {
            var sb = new StringBuilder(sh.Length);
            foreach (char c in sh)
                if (_sh.TryGetValue(c, out char u))
                    sb.Append(u);
            return sb.ToString();
        }

        // Converts a Unicode braille string back to an ASCII shorthand string for saving to a spec file.
        public static string UnicodeToShorthand(string brailleUnicode)
        {
            if (string.IsNullOrEmpty(brailleUnicode)) return "";
            var sb = new StringBuilder();
            foreach (char c in brailleUnicode)
                if (_shReverse.TryGetValue(c, out char sh))
                    sb.Append(sh);
            return sb.ToString();
        }
    }
}
