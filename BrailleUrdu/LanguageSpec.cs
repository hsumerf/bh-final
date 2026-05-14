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

        // input key(s) → Unicode braille string;  first mode='a' entry wins over later 'a' and all 's'/'p'
        private readonly Dictionary<string, string> _map        = new Dictionary<string, string>();
        private readonly HashSet<string>             _alwaysKeys = new HashSet<string>();

        // braille string → input key(s), built lazily from _map
        private Dictionary<string, string> _reverseMap;

        private LanguageSpec() { }

        public static LanguageSpec Load(string langCode)
        {
            var spec = new LanguageSpec();
            string path = Path.Combine(
                Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath) ?? "",
                "ProgramData", "ln", langCode, langCode + ".spec");

            if (!File.Exists(path)) return spec;

            foreach (var raw in File.ReadAllLines(path, Encoding.UTF8))
            {
                if (string.IsNullOrEmpty(raw)) continue;
                var parts = raw.Split(new[] { "≡" }, System.StringSplitOptions.None); // ≡
                if (parts.Length < 3) continue;   // section-header lines

                string sh  = parts[0];  // braille shorthand
                string key = parts[1];  // typed input char(s)
                string mod = parts[2];  // a / s / p

                if (string.IsNullOrEmpty(sh) || string.IsNullOrEmpty(key)) continue;

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

        private static string ShorthandToUnicode(string sh)
        {
            var sb = new StringBuilder(sh.Length);
            foreach (char c in sh)
                if (_sh.TryGetValue(c, out char u))
                    sb.Append(u);
            return sb.ToString();
        }
    }
}
