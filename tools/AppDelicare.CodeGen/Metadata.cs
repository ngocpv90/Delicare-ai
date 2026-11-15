using System.Globalization;
using System.Text.RegularExpressions;

namespace AppDelicare.CodeGen;

/// <summary>Thông tin ngoài DDL: tên class, cột deprecated, quan hệ logic, sổ chỉ ghi thêm.</summary>
public sealed partial class Metadata
{
    /// <summary>CODE_AUDIT mục C – tên bảng legacy → tên class.</summary>
    public static readonly IReadOnlyDictionary<string, string> ClassNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Dishe"] = "Dish",
        ["DishCategorie"] = "DishCategory",
        ["IngredientCategorie"] = "IngredientCategory",
        ["InventoryPolicie"] = "InventoryPolicy",
        ["PricePolicie"] = "PricePolicy",
        ["ProductionBatche"] = "ProductionBatch",
        ["CleaningBatche"] = "CleaningBatch",
        ["ProofOfDeliverie"] = "ProofOfDelivery",
        ["QCInspection"] = "QcInspection",
        ["QCInspectionLine"] = "QcInspectionLine",
    };

    /// <summary>CLAUDE.md 3.3 – sổ chỉ ghi thêm: không sinh Update / Delete.</summary>
    public static readonly HashSet<string> AppendOnly = new(StringComparer.OrdinalIgnoreCase)
    {
        "InventoryTransaction", "PackagingLedger", "TemperatureLog", "AuditChange", "IntegrationLog",
        "ApprovalDecision", "AttendanceEvent", "DeliveryEvent", "ComplaintHistory",
    };

    /// <summary>Cột của BaseEntity (không sinh lại trong entity con).</summary>
    public static readonly string[] BaseColumns =
        ["Id", "CreatedDate", "CreatedBy", "ModifiedDate", "ModifiedBy", "DeletedDate", "DeleteddBy", "DeletedBy", "IsDeleted"];

    /// <summary>Bảng không theo khuôn BaseEntity (Id bigint, không có cột audit).</summary>
    public static bool IsStandalone(Table t) => t.Find("Id")?.SqlType != "uniqueidentifier" || !t.Has("IsDeleted") || !t.Has("CreatedDate");

    public static bool IsLegacyReadOnly(Table t) => t.Name.EndsWith("_Legacy_20261113", StringComparison.OrdinalIgnoreCase);

    public static string ClassOf(string table) => ClassNames.TryGetValue(table, out var c) ? c : table;

    /// <summary>(bảng → cột cũ → cột mới). DeleteddBy xử lý ở BaseEntity nên không nằm ở đây.</summary>
    public Dictionary<string, Dictionary<string, string>> Deprecated { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>(bảng → cột → bảng đích).</summary>
    public Dictionary<string, Dictionary<string, string>> Relations { get; } = new(StringComparer.OrdinalIgnoreCase);

    public static Metadata Load(string repoRoot)
    {
        var meta = new Metadata();
        string dep = Path.Combine(repoRoot, "docs", "reference", "DEPRECATED_COLUMNS.md");
        foreach (string line in File.ReadLines(dep))
        {
            var m = Row3().Match(line);
            if (!m.Success || m.Groups[1].Value is "Bảng" || m.Groups[1].Value.StartsWith("---", StringComparison.Ordinal))
            {
                continue;
            }
            string table = m.Groups[1].Value.Trim(), oldCol = m.Groups[2].Value.Trim(), newCol = m.Groups[3].Value.Trim();
            if (!meta.Deprecated.TryGetValue(table, out var map))
            {
                meta.Deprecated[table] = map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            map[oldCol] = newCol;
        }

        string rel = Path.Combine(repoRoot, "docs", "reference", "LOGICAL_RELATIONS.md");
        foreach (string line in File.ReadLines(rel))
        {
            var m = RelationRow().Match(line);
            if (!m.Success)
            {
                continue;
            }
            string table = m.Groups[1].Value.Trim(), column = m.Groups[2].Value.Trim(), target = m.Groups[3].Value.Trim();
            if (!meta.Relations.TryGetValue(table, out var map))
            {
                meta.Relations[table] = map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            map[column] = target;
        }
        return meta;
    }

    public bool IsDeprecated(Table t, string column)
        => Deprecated.TryGetValue(t.Name, out var map) && map.ContainsKey(column);

    /// <summary>Cột "nghiệp vụ" (không thuộc BaseEntity, không deprecated).</summary>
    public IEnumerable<Column> BusinessColumns(Table t)
        => t.Columns.Where(c => !BaseColumns.Contains(c.Name, StringComparer.OrdinalIgnoreCase) && !IsDeprecated(t, c.Name));

    public string? RelationTarget(Table t, string column)
        => Relations.TryGetValue(t.Name, out var map) && map.TryGetValue(column, out var target) ? target : null;

    /// <summary>Cột nhãn của bảng đích cho dropdown / JOIN (Spec 2.2): DisplayName → Name → FullName → số chứng từ → Code.</summary>
    public static string? LabelColumn(Table target)
    {
        foreach (string c in new[] { "DisplayName", "Name", "FullName" })
        {
            if (target.Find(c) is { IsString: true })
            {
                return c;
            }
        }
        var number = target.Columns.FirstOrDefault(c => c.IsString && (c.Name.EndsWith("Number", StringComparison.Ordinal) || c.Name.EndsWith("No", StringComparison.Ordinal)));
        if (number != null)
        {
            return number.Name;
        }
        return target.Find("Code") is { IsString: true } ? "Code" : null;
    }

    public static string Camel(string name) => name.Length == 0 ? name : char.ToLower(name[0], CultureInfo.InvariantCulture) + name[1..];

    [GeneratedRegex(@"^\|\s*([A-Za-z_][A-Za-z0-9_]*)\s*\|\s*([A-Za-z_][A-Za-z0-9_]*)\s*\|\s*([A-Za-z_][A-Za-z0-9_]*)\s*\|\s*$")]
    private static partial Regex Row3();

    [GeneratedRegex(@"^\|\s*([A-Za-z_][A-Za-z0-9_]*)\s*\|\s*([A-Za-z_][A-Za-z0-9_]*)\s*\|\s*→\s*([A-Za-z_][A-Za-z0-9_]*)\s*\|\s*Id\s*\|")]
    private static partial Regex RelationRow();
}
