using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace AppDelicare.Tests.Menu
{
    /// <summary>
    /// FIX-1.4 (D-92) + D-93: menu dự phòng của FE (<c>app-delicare-client/.../data/catalog.js</c> – <c>menuOrder</c>, <c>menuGroups</c>, <c>grp</c>)
    /// phải khớp bố cục menu của DB (<c>DB/scripts/D79_menu_regroup.sql</c>) – cả hai sinh từ cùng nguồn <c>Seeds.Menu5Groups</c> (CodeGen --layers security).
    /// Lệch (sửa tay một bên, quên chạy CodeGen) → test đỏ.
    /// </summary>
    public class CatalogMenuTests
    {
        private static string RepoPath(params string[] parts)
        {
            string? dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "CLAUDE.md")))
            {
                dir = Path.GetDirectoryName(dir);
            }
            return Path.Combine([dir!, .. parts]);
        }

        private sealed record Row(string Link, int Level, string Parent, int Order, string Name, string? Active);

        /// <summary>Bố cục sidebar suy từ script DB: nhóm cấp 1 theo thứ tự (bỏ nhóm Phê duyệt – D-93 c), mục cấp 2 đang bật (bỏ quyền vào bot, Hộp phê duyệt cũ), Phê duyệt dưới Bảng điều hành.</summary>
        private static List<(string Name, List<string> Items)> ExpectedFromScript()
        {
            string sql = File.ReadAllText(RepoPath("DB", "scripts", "D79_menu_regroup.sql"));
            var rx = new Regex(@"^\s*\(N'(?<link>[^']+)',\s*(?<lvl>\d),\s*(?:N'[^']*'|NULL),\s*-?\d+,\s*-?\d+,\s*N'(?:[^']|'')*',\s*(?:N'(?<parent>[^']*)'|NULL),\s*(?<ord>-?\d+),\s*N'(?<name>(?:[^']|'')*)',\s*N'[^']*',\s*(?<act>NULL|\d)\)", RegexOptions.Multiline);
            var rows = rx.Matches(sql).Select(m => new Row(m.Groups["link"].Value, int.Parse(m.Groups["lvl"].Value), m.Groups["parent"].Value,
                int.Parse(m.Groups["ord"].Value), m.Groups["name"].Value.Replace("''", "'"), m.Groups["act"].Value == "NULL" ? null : m.Groups["act"].Value)).ToList();
            rows.Should().NotBeEmpty("script D79_menu_regroup.sql phải có bảng @P");
            var result = new List<(string, List<string>)>();
            foreach (var g in rows.Where(r => r.Level == 1 && r.Link != "#/group/approve").OrderBy(r => r.Order))
            {
                var items = rows.Where(r => r.Level == 2 && r.Parent == g.Link && r.Active != "0").OrderBy(r => r.Order)
                    .Select(r => r.Link[2..]).Where(i => i != "wf-inbox" && !i.StartsWith("bot-", StringComparison.Ordinal) && !i.StartsWith("knowledge?", StringComparison.Ordinal)).ToList(); // AI-6: mục quyền thư mục kho không lên sidebar
                if (g.Link == "#/group/01")
                {
                    items.Add("wf-inbox"); // UI-6.28 (D-109): Phê duyệt cuối nhóm Điều hành
                }
                result.Add((g.Name, items));
            }
            return result;
        }

        private static JsonElement Catalog()
        {
            string js = File.ReadAllText(RepoPath("app-delicare-client", "src", "assets", "delicare", "js", "data", "catalog.js"));
            int s = js.IndexOf("window.DC_CATALOG=", StringComparison.Ordinal) + "window.DC_CATALOG=".Length;
            int e = js.LastIndexOf('}');
            return JsonDocument.Parse(js[s..(e + 1)]).RootElement.Clone();
        }

        [Fact]
        public void Fallback_menu_groups_match_db_menu_script()
        {
            var expected = ExpectedFromScript();
            var cat = Catalog();
            var order = cat.GetProperty("menuOrder").EnumerateArray().Select(x => x.GetString()!).ToList();
            order.Should().Equal(expected.Select(g => g.Name), "menuOrder của catalog.js = thứ tự nhóm MENU-5 (D-79 e, D-93)");
            var groups = cat.GetProperty("menuGroups").EnumerateArray().Select(g => (Name: g.GetProperty("name").GetString()!,
                Items: g.GetProperty("items").EnumerateArray().Select(i => i.GetString()!).ToList())).ToList();
            groups.Select(g => g.Name).Should().Equal(expected.Select(g => g.Name));
            foreach (var (name, items) in expected)
            {
                groups.Single(g => g.Name == name).Items.Should().Equal(items, $"mục của nhóm \"{name}\"");
            }
        }

        [Fact]
        public void Catalog_has_no_legacy_groups_and_approval_sits_under_dashboard()
        {
            var cat = Catalog();
            var order = cat.GetProperty("menuOrder").EnumerateArray().Select(x => x.GetString()!).ToList();
            order.Should().NotContain(["Mua hàng & Kho", "Sản xuất & Giá thành", "Tích hợp & Chỉ số", "Database Architecture", "Phê duyệt"]);
            order.Should().StartWith(["Việc của tôi", "Điều hành"]);
            order.Should().ContainInOrder("Mua hàng", "Kho");
            var exec = cat.GetProperty("menuGroups").EnumerateArray().Single(g => g.GetProperty("name").GetString() == "Điều hành")
                .GetProperty("items").EnumerateArray().Select(i => i.GetString()).ToList();
            exec.Should().Equal(["dash-exec", "kpi-daily", "cost-meal", "ai-agents", "ai-agent-log", "ai-flow", "ai-tasks", "ai-schedule", "ai-cost", "wf-inbox"],
                "D-109 (UI-6.28): Bảng điều hành · Chỉ số kinh doanh · Giá vốn & lãi gộp · Phê duyệt; AI-21 (D-138 a): Trung tâm Agent + 5 mục tab ẩn (sidebar FE đặt Trung tâm Agent dưới Phê duyệt)");

            // grp của từng màn trong catalog.funcs đúng nhóm mới (breadcrumb / tìm kiếm chức năng dùng grp).
            var groupOf = cat.GetProperty("menuGroups").EnumerateArray()
                .SelectMany(g => g.GetProperty("items").EnumerateArray().Select(i => (Id: i.GetString()!, Grp: g.GetProperty("name").GetString()!)))
                .ToDictionary(x => x.Id, x => x.Grp);
            foreach (var f in cat.GetProperty("funcs").EnumerateArray())
            {
                string id = f.GetProperty("id").GetString()!;
                if (groupOf.TryGetValue(id, out string? grp))
                {
                    f.GetProperty("grp").GetString().Should().Be(grp, $"màn {id}");
                }
            }
        }
    }
}
