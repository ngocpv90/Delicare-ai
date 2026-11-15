using System.Text;
using System.Text.RegularExpressions;

namespace AppDelicare.CodeGen;

/// <summary>
/// T45B.5: sinh <c>AppDelicare.Model/Helper/StatusCatalog.Generated.cs</c> từ <c>docs/reference/STATUS_CATALOG.md</c>
/// (bảng | cột | mã | nghĩa | nguồn). Mã và nhãn tách theo "→", "/", ";", ",", "⇄" – giữ thứ tự luồng.
/// </summary>
public static partial class StatusCatalogGen
{
    public sealed record Entry(string Table, string Column, IReadOnlyList<string> Codes, IReadOnlyList<string> Labels, string Source);

    public static List<Entry> Load(string root)
    {
        var result = new List<Entry>();
        foreach (string line in File.ReadLines(Path.Combine(root, "docs", "reference", "STATUS_CATALOG.md")))
        {
            var cells = line.Split('|').Select(c => c.Trim()).ToArray();
            // | Bảng | Cột | Giá trị | Nghĩa | Nguồn |  → 7 phần tử khi tách theo '|'
            if (cells.Length < 7 || cells[1] is "Bảng" || cells[1].StartsWith("---", StringComparison.Ordinal) || cells[1].Length == 0)
            {
                continue;
            }
            var codes = Split(cells[3]);
            var labels = Split(cells[4]);
            if (labels.Count != codes.Count)
            {
                // Nhãn có thể chứa "/" trong 1 nhãn (vd. "Hủy/hao hụt") → thử không tách theo "/".
                labels = cells[4].Split(['→', '⇄', ';', ',']).Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
            }
            if (codes.Count == 0)
            {
                continue;
            }
            result.Add(new Entry(cells[1], cells[2], codes, labels.Count == codes.Count ? labels : codes, cells[5]));
        }
        return result;
    }

    private static List<string> Split(string raw)
        => Separator().Split(raw).Select(x => x.Trim().Trim('`')).Where(x => x.Length > 0).Distinct().ToList();

    public static string Code(List<Entry> entries)
    {
        static string S(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        var sb = new StringBuilder();
        sb.AppendLine(Templates.Header.Replace("docs/reference/20261113_structure_db_appdelicare.sql", "docs/reference/STATUS_CATALOG.md"));
        sb.AppendLine("namespace AppDelicare.Model.Helper");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>Danh mục trạng thái (STATUS_CATALOG.md – PROPOSED tới khi BA chốt). Khóa = \"Bảng.Cột\".</summary>");
        sb.AppendLine("    public static class StatusCatalog");
        sb.AppendLine("    {");
        sb.AppendLine("        public sealed record Item(string Table, string Column, string[] Codes, string[] Labels, string Source)");
        sb.AppendLine("        {");
        sb.AppendLine("            public string Key => Table + \".\" + Column;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        public static readonly IReadOnlyList<Item> All =");
        sb.AppendLine("        [");
        foreach (var e in entries)
        {
            sb.AppendLine($"            new({S(e.Table)}, {S(e.Column)}, [{string.Join(", ", e.Codes.Select(S))}], [{string.Join(", ", e.Labels.Select(S))}], {S(e.Source)}),");
        }
        sb.AppendLine("        ];");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    [GeneratedRegex(@"\s*(?:→|⇄|/|;|,)\s*")]
    private static partial Regex Separator();
}
