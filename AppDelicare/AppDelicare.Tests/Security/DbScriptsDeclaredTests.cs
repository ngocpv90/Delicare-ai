using FluentAssertions;

namespace AppDelicare.Tests.Security
{
    /// <summary>
    /// DEPLOY-SYNC-1.4 (D-134 e): mọi file DB/scripts/*.sql nằm ở ĐÚNG MỘT danh sách – deploy/db/scripts.order (cấu hình hội tụ), deploy/db/once.order
    /// (chuyển đổi 1 lần) hoặc deploy/db/scripts.merged (đã gộp / chỉ báo cáo / REAL-1 / công cụ kiểm). Thiếu → máy FE / server test không bao giờ chạy → test đỏ.
    /// Seed mới trong DB/seed cũng phải có trong scripts.order (trừ dữ liệu demo ghi rõ ở scripts.merged).
    /// </summary>
    public class DbScriptsDeclaredTests
    {
        private static string Repo()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "deploy", "db", "scripts.order")))
            {
                dir = dir.Parent;
            }
            return dir?.FullName ?? throw new DirectoryNotFoundException("deploy/db/scripts.order");
        }

        private static List<string> Entries(string root, string file)
            => [.. File.ReadAllLines(Path.Combine(root, "deploy", "db", file)).Select(l => l.Split('#')[0].Trim()).Where(l => l.Length > 0)];

        [Fact]
        public void Every_db_script_is_declared_exactly_once()
        {
            string root = Repo();
            var order = Entries(root, "scripts.order");
            var once = Entries(root, "once.order");
            var merged = Entries(root, "scripts.merged");
            foreach (string rel in order.Concat(once).Concat(merged))
            {
                File.Exists(Path.Combine(root, rel)).Should().BeTrue($"{rel} được khai báo nhưng không có file");
            }
            var scripts = Directory.EnumerateFiles(Path.Combine(root, "DB", "scripts"), "*.sql").Select(f => "DB/scripts/" + Path.GetFileName(f)).ToList();
            var problems = scripts.Select(s => (s, n: (order.Contains(s) ? 1 : 0) + (once.Contains(s) ? 1 : 0) + (merged.Contains(s) ? 1 : 0)))
                .Where(x => x.n != 1).Select(x => $"{x.s}: khai báo {x.n} lần").ToList();
            problems.Should().BeEmpty("mỗi script DB/scripts phải nằm ở đúng một trong scripts.order / once.order / scripts.merged (D-134)");

            var seeds = Directory.EnumerateFiles(Path.Combine(root, "DB", "seed"), "*.sql").Select(f => "DB/seed/" + Path.GetFileName(f)).ToList();
            seeds.Where(s => !order.Contains(s) && !merged.Contains(s)).Should().BeEmpty("seed mới phải có trong scripts.order (hoặc ghi lý do ở scripts.merged)");
        }

        [Fact]
        public void Convergent_config_runs_last_in_seed_group_and_check_is_generated()
        {
            string root = Repo();
            var order = Entries(root, "scripts.order");
            int sync = order.IndexOf("DB/seed/Z_Menu_sync.sql"), matrix = order.IndexOf("DB/scripts/D79_permission_matrix.sql"), agent = order.IndexOf("DB/seed/Agent_seed.sql");
            sync.Should().BeGreaterThan(order.FindLastIndex(l => l.StartsWith("DB/seed/ObjectMenu", StringComparison.Ordinal)), "menu hội tụ chạy sau mọi seed menu");
            matrix.Should().Be(sync + 1, "ma trận quyền ngay sau menu");
            agent.Should().Be(matrix + 1, "quyền agent theo vai trò sau ma trận");
            File.ReadAllText(Path.Combine(root, "DB", "scripts", "CHECK_menu_permission.sql")).Should().Contain("RAISERROR").And.Contain("DelicareOnce:");
            File.ReadAllText(Path.Combine(root, "DB", "scripts", "D79_permission_matrix.sql")).Should().Contain("NULLIF(g.Code", "nhóm khớp theo Code (UI-6.43)");
        }
    }
}
