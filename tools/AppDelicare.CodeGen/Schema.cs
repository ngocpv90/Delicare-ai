using System.Text.RegularExpressions;

namespace AppDelicare.CodeGen;

/// <summary>Một cột trong DDL.</summary>
public sealed record Column(string Name, string SqlType, int? Length, int? Precision, int? Scale, bool Nullable)
{
    public string? Default { get; set; }

    /// <summary>nvarchar(max) / varchar(max): Length = -1.</summary>
    public bool IsMax => Length == -1;

    public bool IsString => SqlType is "nvarchar" or "varchar" or "nchar" or "char" or "sysname" or "ntext" or "text";

    public string ClrType => SqlType switch
    {
        "uniqueidentifier" => "Guid",
        "nvarchar" or "varchar" or "nchar" or "char" or "sysname" or "ntext" or "text" or "xml" => "string",
        "int" => "int",
        "bigint" => "long",
        "smallint" => "short",
        "tinyint" => "byte",
        "bit" => "bool",
        "decimal" or "numeric" or "money" or "smallmoney" => "decimal",
        "float" => "double",
        "real" => "float",
        "date" or "datetime" or "datetime2" or "smalldatetime" => "DateTime",
        "time" => "TimeSpan",
        "datetimeoffset" => "DateTimeOffset",
        "varbinary" or "binary" or "image" or "timestamp" or "rowversion" => "byte[]",
        _ => throw new NotSupportedException($"Kiểu SQL chưa hỗ trợ: {SqlType} (cột {Name})"),
    };

    public bool IsValueType => ClrType is not ("string" or "byte[]");

    /// <summary>Kiểu C# kèm '?' (string luôn nullable theo mẫu code hiện có).</summary>
    public string ClrTypeNullable => ClrType == "string" || ClrType == "byte[]" || Nullable ? ClrType + "?" : ClrType;

    /// <summary>Kiểu khai báo tham số SQL trong SP.</summary>
    public string SqlParamType => SqlType switch
    {
        "nvarchar" or "varchar" or "nchar" or "char" or "sysname" => "NVARCHAR(MAX)",
        "decimal" or "numeric" => $"DECIMAL({Precision ?? 18},{Scale ?? 2})",
        "datetime2" => "DATETIME2",
        "time" => "TIME",
        _ => SqlType.ToUpperInvariant(),
    };

    /// <summary>TypeName cho [Column] khi khác mặc định của EF (datetime, date, datetime2(n)).</summary>
    public string? ColumnTypeName => SqlType switch
    {
        "datetime" => "datetime",
        "date" => "date",
        "smalldatetime" => "smalldatetime",
        "datetime2" when Precision is not null and not 7 => $"datetime2({Precision})",
        _ => null,
    };
}

public sealed record UniqueConstraint(string Name, IReadOnlyList<string> Columns);

/// <summary>Một bảng trong DDL.</summary>
public sealed class Table(string name)
{
    public string Name { get; } = name;
    public List<Column> Columns { get; } = [];
    public List<UniqueConstraint> Uniques { get; } = [];

    public Column? Find(string column) => Columns.FirstOrDefault(c => c.Name.Equals(column, StringComparison.OrdinalIgnoreCase));
    public bool Has(string column) => Find(column) != null;
}

/// <summary>Đọc DDL chuẩn (SSMS script) – <c>CREATE TABLE [dbo].[X](...)</c>, UNIQUE, DEFAULT.</summary>
public static partial class DdlParser
{
    public static Dictionary<string, Table> Parse(string ddlPath)
    {
        var tables = new Dictionary<string, Table>(StringComparer.OrdinalIgnoreCase);
        string[] lines = File.ReadAllLines(ddlPath);
        Table? current = null;
        string? pendingUnique = null;
        List<string>? uniqueCols = null;

        foreach (string raw in lines)
        {
            string line = raw.TrimEnd();
            var create = CreateTable().Match(line);
            if (create.Success)
            {
                current = new Table(create.Groups[1].Value);
                tables[current.Name] = current;
                continue;
            }
            if (current != null)
            {
                var unique = UniqueStart().Match(line);
                if (unique.Success)
                {
                    pendingUnique = unique.Groups[1].Value;
                    uniqueCols = [];
                    continue;
                }
                if (pendingUnique != null)
                {
                    var uc = UniqueColumn().Match(line);
                    if (uc.Success)
                    {
                        uniqueCols!.Add(uc.Groups[1].Value);
                        continue;
                    }
                    if (line.TrimStart().StartsWith(")", StringComparison.Ordinal))
                    {
                        current.Uniques.Add(new UniqueConstraint(pendingUnique, uniqueCols!));
                        pendingUnique = null;
                        uniqueCols = null;
                        continue;
                    }
                }
                if (line.StartsWith(") ON [PRIMARY]", StringComparison.Ordinal) || line == ")" || line.StartsWith("GO", StringComparison.Ordinal))
                {
                    current = null;
                    continue;
                }
                var col = ColumnLine().Match(line);
                if (col.Success && pendingUnique == null)
                {
                    current.Columns.Add(ToColumn(col));
                }
                continue;
            }

            var df = DefaultLine().Match(line);
            if (df.Success && tables.TryGetValue(df.Groups[1].Value, out var t) && t.Find(df.Groups[3].Value) is { } c)
            {
                c.Default = df.Groups[2].Value.Trim();
            }
        }
        return tables;
    }

    private static Column ToColumn(Match m)
    {
        string name = m.Groups[1].Value;
        string type = m.Groups[2].Value.ToLowerInvariant();
        string args = m.Groups[3].Success ? m.Groups[3].Value : string.Empty;
        bool nullable = !m.Groups[4].Value.Contains("NOT NULL", StringComparison.OrdinalIgnoreCase);
        int? length = null, precision = null, scale = null;
        if (args.Length > 0)
        {
            string[] parts = args.Trim('(', ')').Split(',', StringSplitOptions.TrimEntries);
            if (type is "decimal" or "numeric")
            {
                precision = int.Parse(parts[0]);
                scale = parts.Length > 1 ? int.Parse(parts[1]) : 0;
            }
            else if (type is "datetime2" or "time" or "datetimeoffset")
            {
                precision = int.Parse(parts[0]);
            }
            else
            {
                length = parts[0].Equals("max", StringComparison.OrdinalIgnoreCase) ? -1 : int.Parse(parts[0]);
            }
        }
        if (type == "sysname")
        {
            length = 128;
        }
        return new Column(name, type, length, precision, scale, nullable);
    }

    [GeneratedRegex(@"^CREATE TABLE \[dbo\]\.\[([^\]]+)\]\($")]
    private static partial Regex CreateTable();

    [GeneratedRegex(@"^\s+\[([^\]]+)\]\s+\[([^\]]+)\](\([^)]*\))?(.*)$")]
    private static partial Regex ColumnLine();

    [GeneratedRegex(@"CONSTRAINT \[([^\]]+)\] UNIQUE")]
    private static partial Regex UniqueStart();

    [GeneratedRegex(@"^\s+\[([^\]]+)\] (ASC|DESC)")]
    private static partial Regex UniqueColumn();

    [GeneratedRegex(@"^ALTER TABLE \[dbo\]\.\[([^\]]+)\] ADD\s+CONSTRAINT \[[^\]]+\]\s+DEFAULT \((.*)\) FOR \[([^\]]+)\]")]
    private static partial Regex DefaultLine();
}
