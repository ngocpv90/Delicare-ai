using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace AppDelicare.Tests.Schema
{
    /// <summary>
    /// 2026-11-18 (anh yêu cầu): cấu trúc bảng quản lý bằng MIGRATION – DB/migrations/*.sql là nguồn duy nhất, chạy theo thứ tự tên file ở mọi môi trường
    /// (deploy/db/Migrations.ps1 + deploy/db/migrations.sh). Test giữ quy ước: tên file, loại khai báo, đúng 1 baseline đứng đầu và KHỚP TỪNG KÝ TỰ với DDL chuẩn
    /// của PO (docs/reference – test SchemaMappingTests so model EF với file đó), tên extended property ≤ 128 ký tự, migration mới chạy lại được
    /// và không xóa bảng / cột / dữ liệu, 2 bộ chạy cùng quy tắc.
    /// </summary>
    public class MigrationFilesTests
    {
        private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        private static readonly string Dir = Path.Combine(Root, "DB", "migrations");
        private static readonly Regex Name = new(@"^\d{8}_[a-z0-9]+(_[a-z0-9]+)*\.sql$");
        private static readonly string[] Modes = ["baseline", "legacy-upgrade", "normal"];

        private static List<string> Files() => [.. Directory.GetFiles(Dir, "*.sql").Select(Path.GetFileName).Cast<string>().OrderBy(x => x, StringComparer.Ordinal)];

        private static string Mode(string file)
        {
            foreach (var line in File.ReadLines(Path.Combine(Dir, file)).Take(5))
            {
                var m = Regex.Match(line, @"^--\s*delicare-migration:\s*([a-z-]+)");
                if (m.Success)
                {
                    return m.Groups[1].Value;
                }
            }
            return "normal";
        }

        [Fact]
        public void File_names_modes_and_order_follow_the_convention()
        {
            var files = Files();
            files.Should().NotBeEmpty();
            files.Should().OnlyContain(f => Name.IsMatch(f), "tên file: YYYYMMDD_<nn>_<tên>.sql – chữ thường, số, gạch dưới");
            files.Select(Mode).Should().OnlyContain(m => Modes.Contains(m));
            files.Where(f => Mode(f) == "baseline").Should().ContainSingle("đúng 1 migration baseline");
            Mode(files[0]).Should().Be("baseline", "baseline phải chạy đầu tiên (đứng đầu theo thứ tự tên)");
            files.Should().OnlyContain(f => ("DelicareMigration:" + f).Length <= 128, "tên extended property tối đa 128 ký tự");
        }

        [Fact]
        public void Baseline_is_exactly_the_po_reference_ddl()
        {
            string baseline = File.ReadAllText(Path.Combine(Dir, Files().Single(f => Mode(f) == "baseline")), Encoding.UTF8);
            string reference = File.ReadAllText(Path.Combine(Root, "docs", "reference", "20261113_structure_db_appdelicare.sql"), Encoding.UTF8);
            // Phần đầu baseline chỉ là chú thích "--"; sau đó là NGUYÊN VĂN DDL của PO.
            int start = baseline.IndexOf("USE [", StringComparison.Ordinal);
            start.Should().BeGreaterThan(0);
            baseline[..start].Split('\n').Where(l => l.Trim().Length > 0).Should().OnlyContain(l => l.TrimStart().StartsWith("--"));
            baseline[start..].Should().Be(reference, "baseline = DDL chuẩn 124 bảng, không sửa – thay đổi cấu trúc là file migration mới");
            Regex.Matches(baseline, @"(?m)^CREATE TABLE \[dbo\]\.\[").Count.Should().Be(124);
        }

        [Fact]
        public void Later_migrations_are_rerunnable_and_never_drop_structure_or_data()
        {
            foreach (var f in Files().Where(f => Mode(f) == "normal"))
            {
                string sql = Regex.Replace(File.ReadAllText(Path.Combine(Dir, f)), @"--[^\n]*", string.Empty);
                sql.Should().MatchRegex(@"(?is)IF\s+(NOT\s+)?EXISTS|OBJECT_ID\s*\(|COL_LENGTH\s*\(|INDEXPROPERTY|CREATE\s+OR\s+ALTER", $"{f}: phải chạy lại được (kiểm tồn tại trước khi tạo / thêm)");
                sql.Should().NotMatchRegex(@"(?i)\bDROP\s+(TABLE|COLUMN)\b|\bTRUNCATE\s+TABLE\b|\bDELETE\s+FROM\b", $"{f}: không xóa bảng / cột / dữ liệu trong migration (cần anh duyệt riêng)");
            }
        }

        [Fact]
        public void Bash_and_powershell_runners_share_the_same_rules()
        {
            string sh = File.ReadAllText(Path.Combine(Root, "deploy", "db", "migrations.sh"));
            string ps = File.ReadAllText(Path.Combine(Root, "deploy", "db", "Migrations.ps1"));
            foreach (var token in new[] { "DelicareMigration:", "delicare-migration:", "baseline", "legacy-upgrade", "ADOPT", "RUN", "sha256:", "sysdiagrams" })
            {
                sh.Should().Contain(token);
                ps.Should().Contain(token);
            }
            // Không còn đường tạo bảng nào ngoài migration.
            foreach (var script in new[] { Path.Combine("deploy", "db", "Deploy-Database.ps1"), Path.Combine("deploy", "docker", "db-init.sh"), Path.Combine("deploy", "db", "migrate.sh") })
            {
                File.ReadAllText(Path.Combine(Root, script)).Should().NotContain("docs/reference/20261113_structure_db_appdelicare.sql", $"{script}: cấu trúc chỉ lấy từ DB/migrations");
            }
        }
    }
}
