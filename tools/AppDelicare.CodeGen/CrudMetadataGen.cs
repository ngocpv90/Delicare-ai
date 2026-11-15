using System.Text;

namespace AppDelicare.CodeGen;

/// <summary>
/// T45B: sinh <c>AppDelicare.Application/Common/Crud/TableMetadata.Generated.cs</c> – metadata từng bảng cho bộ CRUD v1 generic
/// (cột, kiểu, bắt buộc, độ dài, deprecated, UNIQUE, quan hệ logic, cột nhãn / tìm kiếm). Nguồn: DDL + DEPRECATED_COLUMNS + LOGICAL_RELATIONS.
/// </summary>
public static class CrudMetadataGen
{
    private static string S(string? v) => v == null ? "null" : "\"" + v.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    public static string Generate(Dictionary<string, Table> tables, Metadata meta)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Templates.Header);
        sb.AppendLine("#nullable enable");
        sb.AppendLine("namespace AppDelicare.Application.Common.Crud");
        sb.AppendLine("{");
        sb.AppendLine("    public static partial class TableMetadata");
        sb.AppendLine("    {");
        sb.AppendLine("        public static readonly IReadOnlyDictionary<string, TableInfo> All = new Dictionary<string, TableInfo>(StringComparer.OrdinalIgnoreCase)");
        sb.AppendLine("        {");
        foreach (var t in tables.Values.OrderBy(t => t.Name, StringComparer.Ordinal))
        {
            string cls = Metadata.ClassOf(t.Name);
            var columns = new List<string>();
            foreach (var c in t.Columns)
            {
                bool isBase = Metadata.BaseColumns.Contains(c.Name, StringComparer.OrdinalIgnoreCase);
                bool deprecated = meta.IsDeprecated(t, c.Name) || c.Name == "DeleteddBy";
                bool required = !c.Nullable && c.Default == null && !isBase;
                string? target = meta.RelationTarget(t, c.Name);
                string? replacedBy = meta.Deprecated.TryGetValue(t.Name, out var dep) && dep.TryGetValue(c.Name, out var nw) ? Templates.PropertyName(cls, nw) : null;
                columns.Add($"new ColumnInfo({S(c.Name)}, {S(Templates.PropertyName(cls, c.Name))}, {S(c.SqlType)}, {(c.Length is > 0 ? c.Length.ToString() : "null")}, {(required ? "true" : "false")}, {(c.Default != null ? "true" : "false")}, {(isBase ? "true" : "false")}, {(deprecated ? "true" : "false")}, {S(target)}, {S(replacedBy)})");
            }
            string uniques = string.Join(", ", t.Uniques.Select(u => "new[] { " + string.Join(", ", u.Columns.Select(S)) + " }"));
            string? label = Metadata.LabelColumn(t);
            var search = meta.BusinessColumns(t)
                .Where(c => c.IsString && (c.Name is "Code" or "Name" or "FullName" or "DisplayName" or "Title" or "UserName"
                    || c.Name.EndsWith("Number", StringComparison.Ordinal) || c.Name.EndsWith("No", StringComparison.Ordinal)
                    || c.Name.EndsWith("Code", StringComparison.Ordinal) || c.Name.EndsWith("Name", StringComparison.Ordinal)))
                .Select(c => S(Templates.PropertyName(cls, c.Name)));
            sb.AppendLine($"            [{S(t.Name)}] = new TableInfo({S(t.Name)}, {S(cls)}, {S(label)},");
            sb.AppendLine($"                [{string.Join(", ", search)}],");
            sb.AppendLine($"                [{uniques}],");
            sb.AppendLine("                [");
            foreach (string col in columns)
            {
                sb.AppendLine($"                    {col},");
            }
            sb.AppendLine("                ]),");
        }
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }
}
