using System.Text;

namespace VANAS.Shared.Launchers
{
    /// <summary>
    /// Minimal reader for Valve text KeyValues files (Steam libraryfolders.vdf and appmanifest_*.acf).
    /// Values are either a string or a nested <see cref="Dictionary{TKey, TValue}"/>.
    /// </summary>
    public static class ValveKeyValues
    {
        public static Dictionary<string, object> Parse(string text)
        {
            var pos = 0;
            var root = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            ReadObject(text, ref pos, root);
            return root;
        }

        public static string? GetString(Dictionary<string, object> node, string key) =>
            node.TryGetValue(key, out var v) ? v as string : null;

        public static Dictionary<string, object>? GetObject(Dictionary<string, object> node, string key) =>
            node.TryGetValue(key, out var v) ? v as Dictionary<string, object> : null;

        private static void ReadObject(string s, ref int pos, Dictionary<string, object> into)
        {
            while (true)
            {
                var token = NextToken(s, ref pos, out var isQuoted);
                if (token == null) return;                       // end of input
                if (!isQuoted && token == "}") return;           // end of this object
                if (!isQuoted && token == "{") continue;         // stray brace

                var key = token;
                var next = NextToken(s, ref pos, out var nextQuoted);
                if (next == null) return;

                if (!nextQuoted && next == "{")
                {
                    var child = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    ReadObject(s, ref pos, child);
                    into[key] = child;
                }
                else
                {
                    into[key] = next;
                }
            }
        }

        private static string? NextToken(string s, ref int pos, out bool quoted)
        {
            quoted = false;
            while (pos < s.Length)
            {
                var c = s[pos];
                if (char.IsWhiteSpace(c)) { pos++; continue; }
                if (c == '/' && pos + 1 < s.Length && s[pos + 1] == '/')
                {
                    while (pos < s.Length && s[pos] != '\n') pos++;
                    continue;
                }
                break;
            }
            if (pos >= s.Length) return null;

            if (s[pos] == '{' || s[pos] == '}') return s[pos++].ToString();

            if (s[pos] == '"')
            {
                quoted = true;
                pos++;
                var sb = new StringBuilder();
                while (pos < s.Length && s[pos] != '"')
                {
                    if (s[pos] == '\\' && pos + 1 < s.Length)
                    {
                        pos++;
                        sb.Append(s[pos] switch { 'n' => '\n', 't' => '\t', _ => s[pos] });
                    }
                    else sb.Append(s[pos]);
                    pos++;
                }
                pos++; // closing quote
                return sb.ToString();
            }

            var start = pos;
            while (pos < s.Length && !char.IsWhiteSpace(s[pos]) && s[pos] != '{' && s[pos] != '}') pos++;
            return s[start..pos];
        }
    }
}
