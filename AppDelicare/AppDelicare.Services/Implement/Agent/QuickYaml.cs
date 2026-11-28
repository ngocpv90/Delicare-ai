using System.Globalization;
using System.Text;

namespace AppDelicare.Services.Implement.Agent
{
    /// <summary>
    /// AI-25 (D-143): bộ đọc YAML TỐI GIẢN cho <c>app-delicare-agents/&lt;MÃ&gt;/quick.yaml</c> và <c>ban-tin.yaml</c> – Quyết định tự chọn QĐ-AI251-1 (không thêm thư viện).
    /// Hỗ trợ đúng tập cú pháp các tệp dùng: map khối (thụt lề 2 khoảng), danh sách khối "- key: v", map một dòng <c>{ a: b, c: "d" }</c>, danh sách một dòng <c>[a, "b"]</c>,
    /// chuỗi có / không nháy, ghi chú "#". Kết quả: Dictionary&lt;string, object?&gt; / List&lt;object?&gt; / string. Cú pháp ngoài tập này → <see cref="FormatException"/> (test đọc đủ mọi tệp).
    /// </summary>
    public static class QuickYaml
    {
        private sealed record Line(int Indent, string Text, int No);

        public static object? Parse(string text)
        {
            var lines = new List<Line>();
            int no = 0;
            foreach (string raw in text.Replace("\r\n", "\n").Split('\n'))
            {
                no++;
                string s = StripComment(raw).TrimEnd();
                if (s.Trim().Length == 0)
                {
                    continue;
                }
                int ind = s.Length - s.TrimStart(' ').Length;
                lines.Add(new Line(ind, s.Trim(), no));
            }
            int i = 0;
            return lines.Count == 0 ? null : Block(lines, ref i, lines[0].Indent);
        }

        private static string StripComment(string s)
        {
            bool sq = false, dq = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '"' && !sq) dq = !dq;
                else if (c == '\'' && !dq) sq = !sq;
                else if (c == '#' && !sq && !dq && (i == 0 || char.IsWhiteSpace(s[i - 1]))) return s[..i];
            }
            return s;
        }

        private static object? Block(List<Line> lines, ref int i, int indent)
        {
            return lines[i].Text.StartsWith("- ", StringComparison.Ordinal) || lines[i].Text == "-" ? List(lines, ref i, indent) : Map(lines, ref i, indent, null);
        }

        private static List<object?> List(List<Line> lines, ref int i, int indent)
        {
            var list = new List<object?>();
            while (i < lines.Count && lines[i].Indent == indent && (lines[i].Text.StartsWith("- ", StringComparison.Ordinal) || lines[i].Text == "-"))
            {
                string rest = lines[i].Text.Length > 1 ? lines[i].Text[2..].Trim() : string.Empty;
                if (rest.StartsWith('{') || rest.StartsWith('[') || (rest.Length > 0 && KeyOf(rest) == null))
                {
                    list.Add(Scalar(rest, lines[i].No));
                    i++;
                    continue;
                }
                // "- key: v" → map; các khóa tiếp theo thụt lề = indent + 2.
                int itemIndent = indent + 2;
                var first = new Line(itemIndent, rest, lines[i].No);
                lines[i] = first;
                list.Add(Map(lines, ref i, itemIndent, null));
            }
            return list;
        }

        private static string? KeyOf(string s)
        {
            int c = s.IndexOf(':');
            if (c <= 0 || s[0] == '"' || s[0] == '\'')
            {
                return null;
            }
            string k = s[..c];
            return (c == s.Length - 1 || s[c + 1] == ' ') && k.All(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-' or '.') ? k : null;
        }

        private static Dictionary<string, object?> Map(List<Line> lines, ref int i, int indent, Dictionary<string, object?>? into)
        {
            var map = into ?? new Dictionary<string, object?>(StringComparer.Ordinal);
            while (i < lines.Count && lines[i].Indent == indent && !lines[i].Text.StartsWith("- ", StringComparison.Ordinal))
            {
                var ln = lines[i];
                string key = KeyOf(ln.Text) ?? throw new FormatException($"Dòng {ln.No}: thiếu 'khóa:' – «{ln.Text}»");
                string val = ln.Text[(key.Length + 1)..].Trim();
                i++;
                if (val.Length > 0)
                {
                    map[key] = Scalar(val, ln.No);
                }
                else if (i < lines.Count && lines[i].Indent > indent)
                {
                    map[key] = Block(lines, ref i, lines[i].Indent);
                }
                else if (i < lines.Count && lines[i].Indent == indent && lines[i].Text.StartsWith("- ", StringComparison.Ordinal))
                {
                    map[key] = List(lines, ref i, indent); // danh sách cùng cột với khóa cha
                }
                else
                {
                    map[key] = null;
                }
            }
            if (i < lines.Count && lines[i].Indent > indent)
            {
                throw new FormatException($"Dòng {lines[i].No}: thụt lề không đúng – «{lines[i].Text}»");
            }
            return map;
        }

        private static object? Scalar(string s, int no)
        {
            int p = 0;
            var v = Value(s, ref p, no, top: true);
            if (p < s.Length && s[p..].Trim().Length > 0)
            {
                throw new FormatException($"Dòng {no}: thừa «{s[p..]}»");
            }
            return v;
        }

        private static void Ws(string s, ref int p)
        {
            while (p < s.Length && char.IsWhiteSpace(s[p])) p++;
        }

        private static object? Value(string s, ref int p, int no, bool top)
        {
            Ws(s, ref p);
            if (p >= s.Length) return null;
            char c = s[p];
            if (c == '{')
            {
                var map = new Dictionary<string, object?>(StringComparer.Ordinal);
                p++;
                while (true)
                {
                    Ws(s, ref p);
                    if (p < s.Length && s[p] == '}') { p++; return map; }
                    int k0 = p;
                    while (p < s.Length && s[p] != ':') p++;
                    if (p >= s.Length) throw new FormatException($"Dòng {no}: map thiếu ':'");
                    string key = s[k0..p].Trim().Trim('"', '\'');
                    p++;
                    map[key] = Value(s, ref p, no, top: false);
                    Ws(s, ref p);
                    if (p < s.Length && s[p] == ',') { p++; continue; }
                    if (p < s.Length && s[p] == '}') { p++; return map; }
                    throw new FormatException($"Dòng {no}: map thiếu '}}'");
                }
            }
            if (c == '[')
            {
                var list = new List<object?>();
                p++;
                while (true)
                {
                    Ws(s, ref p);
                    if (p < s.Length && s[p] == ']') { p++; return list; }
                    list.Add(Value(s, ref p, no, top: false));
                    Ws(s, ref p);
                    if (p < s.Length && s[p] == ',') { p++; continue; }
                    if (p < s.Length && s[p] == ']') { p++; return list; }
                    throw new FormatException($"Dòng {no}: danh sách thiếu ']'");
                }
            }
            if (c == '"' || c == '\'')
            {
                var sb = new StringBuilder();
                p++;
                while (p < s.Length && s[p] != c)
                {
                    if (s[p] == '\\' && c == '"' && p + 1 < s.Length) { p++; sb.Append(s[p] == 'n' ? '\n' : s[p]); }
                    else sb.Append(s[p]);
                    p++;
                }
                if (p >= s.Length) throw new FormatException($"Dòng {no}: chuỗi thiếu dấu nháy đóng");
                p++;
                return sb.ToString();
            }
            int st = p;
            while (p < s.Length && (top || (s[p] != ',' && s[p] != '}' && s[p] != ']'))) p++;
            return s[st..p].Trim();
        }

        // ---------- truy cập tiện dụng ----------
        public static string? Str(this Dictionary<string, object?> m, string key) => m.TryGetValue(key, out var v) ? v as string : null;

        public static Dictionary<string, object?>? Obj(this Dictionary<string, object?> m, string key) => m.TryGetValue(key, out var v) ? v as Dictionary<string, object?> : null;

        public static List<object?> Arr(this Dictionary<string, object?> m, string key) => m.TryGetValue(key, out var v) && v is List<object?> l ? l : [];

        public static int? Int(this Dictionary<string, object?> m, string key) => int.TryParse(m.Str(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : null;

        public static bool Bool(this Dictionary<string, object?> m, string key) => string.Equals(m.Str(key), "true", StringComparison.OrdinalIgnoreCase);
    }
}
