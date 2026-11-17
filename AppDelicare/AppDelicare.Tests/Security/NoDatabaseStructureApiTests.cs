using System.Reflection;
using System.Text.RegularExpressions;
using AppDelicare.WebApi.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace AppDelicare.Tests.Security
{
    /// <summary>
    /// BE3.7 – D-58 (anh chốt 2026-11-17): KHÔNG có API tra cứu cấu trúc database (DB Explorer / GAP – Catalog 226–230, 233, 234 = Không làm).
    /// Chặn tái phát: (1) không controller / action nào mang tên kiểu tra cứu cấu trúc; (2) mã nguồn WebApi / Services / Data không đọc
    /// <c>INFORMATION_SCHEMA</c> / <c>sys.tables</c>… (catalog view của SQL Server).
    /// </summary>
    public class NoDatabaseStructureApiTests
    {
        private static readonly Regex StructureName = new("(Explorer|Schema|DbTable|TableList|Columns?$|GetTables|GetRelations|GetGaps|Gap$|SampleRows)", RegexOptions.IgnoreCase);

        [Fact]
        public void No_controller_or_action_exposes_database_structure()
        {
            var controllers = typeof(BaseController).Assembly.GetTypes()
                .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract).ToList();
            controllers.Should().NotBeEmpty();
            var offenders = new List<string>();
            foreach (var controller in controllers)
            {
                string name = controller.Name.Replace("Controller", string.Empty);
                if (StructureName.IsMatch(name))
                {
                    offenders.Add(name);
                }
                foreach (var method in controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
                {
                    var routes = method.GetCustomAttributes<HttpMethodAttribute>().Select(a => a.Template ?? method.Name);
                    offenders.AddRange(routes.Where(r => StructureName.IsMatch(r)).Select(r => $"{name}/{r}"));
                }
            }
            offenders.Should().BeEmpty("D-58: không API tra cứu cấu trúc database");
        }

        [Fact]
        public void Backend_source_does_not_read_sql_server_catalog_views()
        {
            string? dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "AppDelicare.sln")))
            {
                dir = Path.GetDirectoryName(dir);
            }
            dir.Should().NotBeNull("chạy test trong repo (tìm AppDelicare.sln)");
            var pattern = new Regex(@"INFORMATION_SCHEMA|\bsys\.(tables|columns|objects|foreign_keys|indexes|schemas|all_columns)\b|sp_columns|sp_help\b", RegexOptions.IgnoreCase);
            var offenders = new[] { "AppDelicare.WebApi", "AppDelicare.Services", "AppDelicare.Data", "AppDelicare.Model", "AppDelicare.Job" }
                .Select(p => Path.Combine(dir!, p))
                .Where(Directory.Exists)
                .SelectMany(p => Directory.EnumerateFiles(p, "*.cs", SearchOption.AllDirectories))
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                .Where(f => pattern.IsMatch(File.ReadAllText(f)))
                .Select(f => Path.GetRelativePath(dir!, f))
                .ToList();
            offenders.Should().BeEmpty("D-58: không đọc catalog view của SQL Server để trả cấu trúc ra ngoài");
        }
    }
}
