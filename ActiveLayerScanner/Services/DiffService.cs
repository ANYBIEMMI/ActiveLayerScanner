using System;
using System.Collections.Generic;
using System.Text;

namespace ActiveLayerScanner.Services
{
    public enum DiffKind { Same, Added, Removed }

    public class DiffLine
    {
        public DiffKind Kind { get; set; }
        public string Text { get; set; }
    }

    public static class DiffService
    {
        /// <summary>Line-based LCS diff between two texts (JSON is pretty-printed first).</summary>
        public static List<DiffLine> Diff(string baseText, string activeText)
        {
            var a = Lines(Pretty(baseText));
            var b = Lines(Pretty(activeText));
            var lcs = new int[a.Length + 1, b.Length + 1];
            for (var i = a.Length - 1; i >= 0; i--)
                for (var j = b.Length - 1; j >= 0; j--)
                    lcs[i, j] = a[i] == b[j] ? lcs[i + 1, j + 1] + 1 : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);

            var res = new List<DiffLine>();
            int x = 0, y = 0;
            while (x < a.Length && y < b.Length)
            {
                if (a[x] == b[y]) { res.Add(new DiffLine { Kind = DiffKind.Same, Text = a[x] }); x++; y++; }
                else if (lcs[x + 1, y] >= lcs[x, y + 1]) res.Add(new DiffLine { Kind = DiffKind.Removed, Text = a[x++] });
                else res.Add(new DiffLine { Kind = DiffKind.Added, Text = b[y++] });
            }
            while (x < a.Length) res.Add(new DiffLine { Kind = DiffKind.Removed, Text = a[x++] });
            while (y < b.Length) res.Add(new DiffLine { Kind = DiffKind.Added, Text = b[y++] });
            return res;
        }

        private static string[] Lines(string s) => (s ?? string.Empty).Replace("\r", "").Split('\n');

        // Minimal JSON pretty printer (no external dependency); non-JSON text is returned as-is.
        private static string Pretty(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var t = s.TrimStart();
            if (t[0] != '{' && t[0] != '[') return s;
            if (s.Contains("\n")) return s;
            var sb = new StringBuilder();
            var indent = 0;
            var inStr = false;
            for (var i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if (inStr)
                {
                    sb.Append(c);
                    if (c == '\\' && i + 1 < s.Length) sb.Append(s[++i]);
                    else if (c == '"') inStr = false;
                    continue;
                }
                switch (c)
                {
                    case '"': inStr = true; sb.Append(c); break;
                    case '{': case '[': sb.Append(c).Append('\n').Append(' ', ++indent * 2); break;
                    case '}': case ']': sb.Append('\n').Append(' ', Math.Max(--indent, 0) * 2).Append(c); break;
                    case ',': sb.Append(c).Append('\n').Append(' ', indent * 2); break;
                    case ':': sb.Append(": "); break;
                    case ' ': case '\t': case '\r': case '\n': break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }
    }
}
