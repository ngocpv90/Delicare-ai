using System.Text.RegularExpressions;
using AppDelicare.Data.Connection;
using AppDelicare.Data.Share;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AppDelicare.Tests.Schema
{
    /// <summary>
    /// T05 – model EF khớp DDL chuẩn 124 bảng (docs/reference/20261113_structure_db_appdelicare.sql):
    /// mọi DbSet map tới bảng có thật, mọi property map tới cột có thật, và ngược lại mọi bảng / cột đều có trong model.
    /// DDL chuẩn + cột do migration <c>normal</c> trong <c>DB/migrations</c> thêm (<c>ALTER TABLE [dbo].[X] ADD [Cột] [kiểu]</c> – DB-MIGRATIONS m1,
    /// vd ORD-OPT-1.1 <c>OrderLine.MenuOptionNo</c>); docs/reference giữ nguyên.
    /// Dựng model bằng provider SQL Server nhưng KHÔNG kết nối DB.
    /// </summary>
    public partial class SchemaMappingTests
    {
        private static readonly Lazy<Dictionary<string, Dictionary<string, string>>> Ddl = new(LoadDdl);

        private static Dictionary<string, Dictionary<string, string>> LoadDdl()
        {
            string? dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "CLAUDE.md")))
            {
                dir = Path.GetDirectoryName(dir);
            }
            dir.Should().NotBeNull("test phải chạy bên trong repo để đọc DDL");
            string path = Path.Combine(dir!, "docs", "reference", "20261113_structure_db_appdelicare.sql");

            var tables = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string>? current = null;
            foreach (string line in File.ReadLines(path))
            {
                var create = CreateTable().Match(line);
                if (create.Success)
                {
                    current = new Dictionary<string, string>(StringComparer.Ordinal);
                    tables[create.Groups[1].Value] = current;
                    continue;
                }
                if (current == null)
                {
                    continue;
                }
                if (line.TrimStart().StartsWith("CONSTRAINT", StringComparison.Ordinal) || line.StartsWith(')'))
                {
                    current = null;
                    continue;
                }
                var col = ColumnLine().Match(line);
                if (col.Success)
                {
                    current[col.Groups[1].Value] = col.Groups[2].Value.ToLowerInvariant();
                }
            }
            // Cột do migration normal thêm (DB-MIGRATIONS m1: DB/migrations là nguồn cấu trúc).
            foreach (string file in Directory.GetFiles(Path.Combine(dir!, "DB", "migrations"), "*.sql").Order(StringComparer.Ordinal))
            {
                string sql = File.ReadAllText(file);
                if (!Regex.IsMatch(sql, @"^--\s*delicare-migration:\s*normal", RegexOptions.Multiline))
                {
                    continue;
                }
                foreach (Match add in AddColumn().Matches(sql))
                {
                    if (tables.TryGetValue(add.Groups[1].Value, out var cols))
                    {
                        cols[add.Groups[2].Value] = add.Groups[3].Value.ToLowerInvariant();
                    }
                }
            }
            return tables;
        }

        [GeneratedRegex(@"ALTER\s+TABLE\s+\[dbo\]\.\[(\w+)\]\s+ADD\s+\[(\w+)\]\s+\[(\w+)\]", RegexOptions.IgnoreCase)]
        private static partial Regex AddColumn();

        private static IModel BuildModel()
        {
            var options = new DbContextOptionsBuilder<DatabaseContext>()
                .UseSqlServer("Server=model-only;Database=none;Trusted_Connection=True;")
                .Options;
            using var db = new DatabaseContext(options, new CurrentActor(new HttpContextAccessor()));
            return db.Model;
        }

        [Fact]
        public void Ddl_has_124_tables()
        {
            Ddl.Value.Should().HaveCount(124);
        }

        [Fact]
        public void Every_entity_maps_to_an_existing_table_and_every_property_to_an_existing_column()
        {
            var problems = new List<string>();
            foreach (var entity in BuildModel().GetEntityTypes())
            {
                string? table = entity.GetTableName();
                if (table == null || !Ddl.Value.TryGetValue(table, out var columns))
                {
                    problems.Add($"{entity.ClrType.Name} → bảng '{table}' không có trong DDL");
                    continue;
                }
                var store = StoreObjectIdentifier.Table(table, entity.GetSchema());
                foreach (var property in entity.GetProperties())
                {
                    string? column = property.GetColumnName(store);
                    if (column == null || !columns.ContainsKey(column))
                    {
                        problems.Add($"{entity.ClrType.Name}.{property.Name} → cột '{table}.{column}' không có trong DDL");
                    }
                }
            }
            problems.Should().BeEmpty();
        }

        [Fact]
        public void Every_ddl_table_and_column_is_mapped()
        {
            var model = BuildModel();
            var mapped = model.GetEntityTypes().Where(e => e.GetTableName() != null)
                .ToDictionary(e => e.GetTableName()!, e => e, StringComparer.OrdinalIgnoreCase);
            var problems = new List<string>();
            foreach (var (table, columns) in Ddl.Value)
            {
                if (!mapped.TryGetValue(table, out var entity))
                {
                    problems.Add($"Bảng '{table}' chưa có entity / DbSet");
                    continue;
                }
                var store = StoreObjectIdentifier.Table(table, entity.GetSchema());
                var mappedColumns = entity.GetProperties().Select(p => p.GetColumnName(store)).ToHashSet(StringComparer.Ordinal);
                problems.AddRange(columns.Keys.Where(c => !mappedColumns.Contains(c)).Select(c => $"Cột '{table}.{c}' chưa có property"));
            }
            problems.Should().BeEmpty();
        }

        [Fact]
        public void Column_types_are_compatible_with_clr_types()
        {
            var problems = new List<string>();
            foreach (var entity in BuildModel().GetEntityTypes())
            {
                string table = entity.GetTableName()!;
                var store = StoreObjectIdentifier.Table(table, entity.GetSchema());
                foreach (var property in entity.GetProperties())
                {
                    string sqlType = Ddl.Value[table][property.GetColumnName(store)!];
                    Type clr = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                    bool ok = sqlType switch
                    {
                        "uniqueidentifier" => clr == typeof(Guid),
                        "nvarchar" or "varchar" or "nchar" or "char" or "sysname" => clr == typeof(string),
                        "int" => clr == typeof(int),
                        "bigint" => clr == typeof(long),
                        "bit" => clr == typeof(bool),
                        "decimal" or "numeric" => clr == typeof(decimal),
                        "date" or "datetime" or "datetime2" => clr == typeof(DateTime),
                        "time" => clr == typeof(TimeSpan),
                        _ => true,
                    };
                    if (!ok)
                    {
                        problems.Add($"{entity.ClrType.Name}.{property.Name}: SQL {sqlType} ↔ C# {clr.Name}");
                    }
                }
            }
            problems.Should().BeEmpty();
        }

        [Fact]
        public void Legacy_table_names_keep_their_real_names()
        {
            var tables = BuildModel().GetEntityTypes().Select(e => e.GetTableName()).ToHashSet(StringComparer.Ordinal);
            tables.Should().Contain(["Dishe", "DishCategorie", "ProductionBatche", "CleaningBatche", "ProofOfDeliverie",
                "IngredientCategorie", "InventoryPolicie", "PricePolicie", "QCInspection", "QCInspectionLine"]);
        }

        [GeneratedRegex(@"^CREATE TABLE \[dbo\]\.\[([^\]]+)\]\($")]
        private static partial Regex CreateTable();

        [GeneratedRegex(@"^\s+\[([^\]]+)\]\s+\[([^\]]+)\]")]
        private static partial Regex ColumnLine();
    }
}
