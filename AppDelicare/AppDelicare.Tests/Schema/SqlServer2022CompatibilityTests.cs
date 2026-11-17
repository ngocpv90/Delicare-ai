using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace AppDelicare.Tests.Schema
{
    /// <summary>
    /// T48.5 (D-61, anh chốt 2026-11-17): chuẩn là SQL Server 2022 (mức tương thích 160). Mức 160 KHÔNG chặn hết hàm mới của 2025
    /// (một số hàm regex chạy ở mọi mức) → test này là chốt chặn: quét <c>DB/**/*.sql</c>, <c>deploy/db/**/*.sql</c> và chuỗi SQL
    /// trong <c>AppDelicare.*/**/*.cs</c>, báo lỗi khi gặp cú pháp chỉ có từ 2025 (danh sách cấm: CLAUDE.md mục 3.1, quy tắc 0).
    /// Bỏ qua comment và chuỗi '...' của SQL (dữ liệu seed); trong C# chỉ xét nội dung chuỗi (không xét code / comment).
    /// </summary>
    public class SqlServer2022CompatibilityTests
    {
        /// <summary>Ngoại lệ (đường dẫn tương đối từ gốc repo, dấu "/") – mặc định rỗng; thêm phải ghi lý do + anh duyệt.</summary>
        private static readonly HashSet<string> Exceptions = new(StringComparer.OrdinalIgnoreCase);

        private const RegexOptions Opt = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        /// <summary>Từ khóa / hàm cấm: xét trong mọi đoạn SQL (file .sql sau khi bỏ comment + chuỗi; mọi chuỗi C#).</summary>
        private static readonly (string Name, Regex Pattern)[] Always =
        {
            ("REGEXP_*", new Regex(@"\bREGEXP_\w+", Opt)),
            ("JSON_OBJECTAGG / JSON_ARRAYAGG / JSON_CONTAINS", new Regex(@"\bJSON_(OBJECTAGG|ARRAYAGG|CONTAINS)\b", Opt)),
            ("EDIT_DISTANCE*", new Regex(@"\bEDIT_DISTANCE\w*", Opt)),
            ("JARO_WINKLER_*", new Regex(@"\bJARO_WINKLER_\w+", Opt)),
            ("UNISTR", new Regex(@"\bUNISTR\s*\(", Opt)),
            ("BASE64_ENCODE / BASE64_DECODE", new Regex(@"\bBASE64_(ENCODE|DECODE)\b", Opt)),
            ("CURRENT_DATE", new Regex(@"\bCURRENT_DATE\b", Opt)),
            ("AI_GENERATE_*", new Regex(@"\bAI_GENERATE_\w+", Opt)),
            ("VECTOR_*", new Regex(@"\bVECTOR_\w+\s*\(", Opt)),
            ("sp_invoke_external_rest_endpoint", new Regex(@"\bsp_invoke_external_rest_endpoint\b", Opt)),
        };

        /// <summary>Cú pháp dễ trùng chữ thường (PRODUCT, json, ||): chỉ xét trong file .sql và chuỗi C# "giống SQL".</summary>
        private static readonly (string Name, Regex Pattern)[] SqlOnly =
        {
            // PRODUCT(...) là hàm gộp; bỏ qua tên bảng / cột: dbo.Product(, [Product](, INTO Product (…
            ("PRODUCT()", new Regex(@"(?<![\.\[\w])(?<!\b(INTO|FROM|JOIN|UPDATE|TABLE|EXEC|EXECUTE)\s+)\bPRODUCT\s*\(", Opt)),
            ("kiểu json / vector", new Regex(@"(@\w+\s+(AS\s+)?|\bAS\s+|^\s*\w+\s+|,\s*\w+\s+|\(\s*\w+\s+)(json|vector)\b(?!\s*[/\.])", Opt | RegexOptions.Multiline)),
            ("toán tử nối ||", new Regex(@"\|\|", Opt)),
        };

        private static readonly Regex LooksLikeSql = new(@"\b(SELECT|INSERT\s+INTO|UPDATE\s+\w|DELETE\s+FROM|EXEC(UTE)?\s|DECLARE\s+@|CREATE\s|ALTER\s|MERGE\s|WHERE\s)", Opt);

        [Fact]
        public void Sql_scripts_use_only_sql_server_2022_syntax()
        {
            string repo = RepoRoot();
            var files = new[] { "DB", Path.Combine("deploy", "db") }
                .Select(d => Path.Combine(repo, d))
                .Where(Directory.Exists)
                .SelectMany(d => Directory.EnumerateFiles(d, "*.sql", SearchOption.AllDirectories))
                .ToList();
            files.Should().NotBeEmpty("phải tìm thấy script SQL trong DB/ và deploy/db/");
            files.Count(f => f.Contains($"{Path.DirectorySeparatorChar}sp{Path.DirectorySeparatorChar}")).Should().BeGreaterThan(50, "phải quét cả DB/sp");

            var offenders = new List<string>();
            foreach (var file in files)
            {
                string rel = Rel(repo, file);
                if (Exceptions.Contains(rel))
                {
                    continue;
                }
                string code = StripSqlCommentsAndStrings(File.ReadAllText(file));
                offenders.AddRange(Find(code, sqlLike: true).Select(n => $"{rel}: {n}"));
            }
            offenders.Should().BeEmpty("D-61: chỉ dùng cú pháp có trong SQL Server 2022 (CLAUDE.md mục 3.1, quy tắc 0)");
        }

        [Fact]
        public void Inline_sql_in_csharp_uses_only_sql_server_2022_syntax()
        {
            string repo = RepoRoot();
            string self = Path.Combine("AppDelicare", "AppDelicare.Tests", "Schema", nameof(SqlServer2022CompatibilityTests) + ".cs");
            var files = Directory.EnumerateDirectories(Path.Combine(repo, "AppDelicare"), "AppDelicare.*")
                .SelectMany(d => Directory.EnumerateFiles(d, "*.cs", SearchOption.AllDirectories))
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                .Where(f => !f.EndsWith(self, StringComparison.OrdinalIgnoreCase))
                .ToList();
            files.Count.Should().BeGreaterThan(100, "phải quét mã nguồn backend");

            var offenders = new List<string>();
            foreach (var file in files)
            {
                string rel = Rel(repo, file);
                if (Exceptions.Contains(rel))
                {
                    continue;
                }
                foreach (var literal in CSharpStringLiterals(File.ReadAllText(file)))
                {
                    offenders.AddRange(Find(literal, LooksLikeSql.IsMatch(literal)).Select(n => $"{rel}: {n}"));
                }
            }
            offenders.Distinct().Should().BeEmpty("D-61: SQL inline trong C# chỉ dùng cú pháp có trong SQL Server 2022");
        }

        /// <summary>Kiểm chính bộ quét: bắt đúng cú pháp cấm, không báo nhầm comment / dữ liệu / tên bảng.</summary>
        [Theory]
        [InlineData("SELECT * FROM dbo.Dish WHERE REGEXP_LIKE(Name, N'^a')", "REGEXP_*")]
        [InlineData("SELECT JSON_ARRAYAGG(Id) FROM dbo.Dish", "JSON_OBJECTAGG / JSON_ARRAYAGG / JSON_CONTAINS")]
        [InlineData("SELECT EDIT_DISTANCE(a, b)", "EDIT_DISTANCE*")]
        [InlineData("SELECT JARO_WINKLER_SIMILARITY(a, b)", "JARO_WINKLER_*")]
        [InlineData("SELECT UNISTR(N'\\00e9')", "UNISTR")]
        [InlineData("SELECT BASE64_ENCODE(0x01)", "BASE64_ENCODE / BASE64_DECODE")]
        [InlineData("SELECT CURRENT_DATE", "CURRENT_DATE")]
        [InlineData("SELECT PRODUCT(Qty) FROM dbo.Dish", "PRODUCT()")]
        [InlineData("DECLARE @j json = N'{}'", "kiểu json / vector")]
        [InlineData("CREATE TABLE #t (Id int, Doc json NULL)", "kiểu json / vector")]
        [InlineData("SELECT CAST(x AS vector(3))", "kiểu json / vector")]
        [InlineData("SELECT a || b", "toán tử nối ||")]
        [InlineData("SELECT VECTOR_DISTANCE('cosine', a, b)", "VECTOR_*")]
        [InlineData("EXEC sp_invoke_external_rest_endpoint @url = N'x'", "sp_invoke_external_rest_endpoint")]
        [InlineData("SELECT AI_GENERATE_EMBEDDINGS(x)", "AI_GENERATE_*")]
        public void Scanner_detects_forbidden_syntax(string sql, string expected)
        {
            Find(StripSqlCommentsAndStrings(sql), sqlLike: true).Should().Contain(expected);
        }

        [Theory]
        [InlineData("-- REGEXP_LIKE trong comment\nSELECT 1")]
        [InlineData("/* JSON_ARRAYAGG, a || b */ SELECT 1")]
        [InlineData("INSERT INTO dbo.Note (Text) VALUES (N'dùng REGEXP_LIKE || CURRENT_DATE')")]
        [InlineData("INSERT INTO Product (Id) VALUES (1); SELECT * FROM dbo.Product(1); UPDATE [Product](x)")]
        [InlineData("SELECT JSON_VALUE(@x, '$.a'), JSON_OBJECT('a': 1), STRING_AGG(Name, ',') FROM dbo.Dish")]
        [InlineData("DECLARE @json nvarchar(max) = N'{}'; SELECT @json")]
        public void Scanner_ignores_comments_data_and_allowed_syntax(string sql)
        {
            Find(StripSqlCommentsAndStrings(sql), sqlLike: true).Should().BeEmpty();
        }

        [Fact]
        public void Csharp_literal_extraction_skips_code_and_comments()
        {
            const string src = "// REGEXP_LIKE\nvar ok = a || b; /* CURRENT_DATE */ var c = '\"'; var s = @\"SELECT \"\"x\"\" || y\"; var t = \"application/json\";";
            var literals = CSharpStringLiterals(src).ToList();
            literals.Should().HaveCount(2);
            Find(literals[0], LooksLikeSql.IsMatch(literals[0])).Should().Equal("toán tử nối ||");
            Find(literals[1], LooksLikeSql.IsMatch(literals[1])).Should().BeEmpty();
        }

        private static IEnumerable<string> Find(string code, bool sqlLike)
        {
            foreach (var (name, pattern) in Always)
            {
                if (pattern.IsMatch(code))
                {
                    yield return name;
                }
            }
            if (!sqlLike)
            {
                yield break;
            }
            foreach (var (name, pattern) in SqlOnly)
            {
                if (pattern.IsMatch(code))
                {
                    yield return name;
                }
            }
        }

        /// <summary>Bỏ comment (-- và /* */), thay chuỗi '...' và [tên] bằng chỗ trống – giữ xuống dòng.</summary>
        private static string StripSqlCommentsAndStrings(string sql)
        {
            var sb = new StringBuilder(sql.Length);
            int i = 0;
            while (i < sql.Length)
            {
                char c = sql[i];
                if (c == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
                {
                    while (i < sql.Length && sql[i] != '\n') i++;
                }
                else if (c == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
                {
                    int depth = 0;
                    do
                    {
                        if (sql[i] == '/' && i + 1 < sql.Length && sql[i + 1] == '*') { depth++; i += 2; }
                        else if (sql[i] == '*' && i + 1 < sql.Length && sql[i + 1] == '/') { depth--; i += 2; }
                        else { if (sql[i] == '\n') sb.Append('\n'); i++; }
                    } while (depth > 0 && i < sql.Length);
                    sb.Append(' ');
                }
                else if (c == '\'')
                {
                    i++;
                    while (i < sql.Length)
                    {
                        if (sql[i] == '\'' && i + 1 < sql.Length && sql[i + 1] == '\'') { i += 2; continue; }
                        if (sql[i] == '\'') { i++; break; }
                        i++;
                    }
                    sb.Append("''");
                }
                else if (c == '[')
                {
                    while (i < sql.Length && sql[i] != ']') i++;
                    i++;
                    sb.Append("[x]");
                }
                else
                {
                    sb.Append(c);
                    i++;
                }
            }
            return sb.ToString();
        }

        /// <summary>Lấy nội dung các chuỗi C# ("...", @"...", $"...", """...""") – bỏ comment, ký tự '…' và code.</summary>
        private static IEnumerable<string> CSharpStringLiterals(string src)
        {
            int i = 0;
            while (i < src.Length)
            {
                char c = src[i];
                if (c == '/' && i + 1 < src.Length && src[i + 1] == '/')
                {
                    while (i < src.Length && src[i] != '\n') i++;
                }
                else if (c == '/' && i + 1 < src.Length && src[i + 1] == '*')
                {
                    int end = src.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    i = end < 0 ? src.Length : end + 2;
                }
                else if (c == '\'')
                {
                    i++;
                    while (i < src.Length && src[i] != '\'')
                    {
                        i += src[i] == '\\' ? 2 : 1;
                    }
                    i++;
                }
                else if (c == '"')
                {
                    int q = 0;
                    while (i + q < src.Length && src[i + q] == '"') q++;
                    if (q >= 3)
                    {
                        string fence = new('"', q);
                        int end = src.IndexOf(fence, i + q, StringComparison.Ordinal);
                        int stop = end < 0 ? src.Length : end;
                        yield return src.Substring(i + q, stop - i - q);
                        i = end < 0 ? src.Length : end + q;
                        continue;
                    }
                    bool verbatim = (i > 0 && src[i - 1] == '@') || (i > 1 && src[i - 2] == '@' && src[i - 1] == '$') || (i > 1 && src[i - 2] == '$' && src[i - 1] == '@');
                    var sb = new StringBuilder();
                    i++;
                    while (i < src.Length)
                    {
                        if (verbatim && src[i] == '"' && i + 1 < src.Length && src[i + 1] == '"') { sb.Append('"'); i += 2; continue; }
                        if (!verbatim && src[i] == '\\' && i + 1 < src.Length) { sb.Append(src[i + 1]); i += 2; continue; }
                        if (src[i] == '"') { i++; break; }
                        sb.Append(src[i]);
                        i++;
                    }
                    yield return sb.ToString();
                }
                else
                {
                    i++;
                }
            }
        }

        private static string RepoRoot()
        {
            string? dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "AppDelicare.sln")))
            {
                dir = Path.GetDirectoryName(dir);
            }
            dir.Should().NotBeNull("chạy test trong repo (tìm AppDelicare.sln)");
            return Path.GetDirectoryName(dir!)!;
        }

        private static string Rel(string repo, string file) => Path.GetRelativePath(repo, file).Replace('\\', '/');
    }
}
