using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace BrailleUrdu
{
    // Loads ProgramData/ln/<code>/<code>_r.spec and converts typed keyboard keys to
    // the target language's Unicode characters for display in PrintTextBox.
    // Spec line format:  outputChar(s) ≡ typedKey(s) ≡ mode
    public class PrintInputMap
    {
        private readonly Dictionary<string, string> _map      = new Dictionary<string, string>();
        private readonly HashSet<string>             _prefixes = new HashSet<string>();
        private readonly HashSet<string>             _alwaysKeys = new HashSet<string>(); // keys with mode='a'

        private PrintInputMap() { }

        public static PrintInputMap Load(string langCode)
        {
            var m = new PrintInputMap();
            string path = Path.Combine(
                Path.GetDirectoryName(Application.ExecutablePath) ?? "",
                "ProgramData", "ln", langCode, langCode + "_r.spec");

            if (!File.Exists(path)) return m;

            foreach (var raw in File.ReadAllLines(path, Encoding.UTF8))
            {
                if (string.IsNullOrEmpty(raw)) continue;
                var parts = raw.Split(new[] { "≡" }, System.StringSplitOptions.None);
                if (parts.Length < 3) continue;

                string output = parts[0];
                string key    = parts[1];
                string mode   = parts[2];

                if (string.IsNullOrEmpty(key)) continue;

                // First mode='a' entry wins; 'a' can promote an existing 's'/'p' entry
                bool existingIsAlways = m._alwaysKeys.Contains(key);
                if (!m._map.ContainsKey(key) || (mode == "a" && !existingIsAlways))
                {
                    m._map[key] = output;
                    if (mode == "a") m._alwaysKeys.Add(key);
                }

                // Register all proper prefixes of multi-char keys (e.g. "\" for "\z")
                if (key.Length > 1)
                    for (int i = 1; i < key.Length; i++)
                        m._prefixes.Add(key.Substring(0, i));
            }

            return m;
        }

        // Process one typed character against the pending buffer.
        // Returns the string to insert, or null if still buffering.
        // pending is updated in-place by the caller.
        public string Convert(ref string pending, char c)
        {
            string attempt = pending + c;

            // Full match that is not itself a prefix of something longer
            if (_map.ContainsKey(attempt) && !_prefixes.Contains(attempt))
            {
                pending = "";
                return _map[attempt];
            }

            // It's a prefix — accumulate and wait
            if (_prefixes.Contains(attempt))
            {
                pending = attempt;
                return null;
            }

            // No match and not a prefix: flush pending first, then retry c alone
            if (pending.Length > 0)
            {
                string flushed = _map.TryGetValue(pending, out var pv) ? pv : pending;
                pending = "";
                string rest = Convert(ref pending, c);
                return flushed + (rest ?? "");
            }

            // Single char with no pending — map or pass through
            pending = "";
            return _map.TryGetValue(c.ToString(), out var v) ? v : c.ToString();
        }

        // Flush any buffered pending chars (call on cursor move, focus loss, Enter, etc.)
        public string Flush(ref string pending)
        {
            if (string.IsNullOrEmpty(pending)) return "";
            string result = _map.TryGetValue(pending, out var v) ? v : pending;
            pending = "";
            return result;
        }
    }
}
