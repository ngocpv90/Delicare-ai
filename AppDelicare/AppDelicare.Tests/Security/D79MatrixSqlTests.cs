using System.Text.RegularExpressions;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Security
{
    /// <summary>
    /// MENU-5.6 (D-79): trên DB dev, trong 1 transaction rồi ROLLBACK (không để lại dữ liệu): seed → D79_menu_regroup.sql → D79_permission_matrix.sql (chạy thật) →
    /// quyền của 19 nhóm chuẩn trên mọi mục cấp 2 đang bật KHỚP ma trận sinh từ Seeds.Menu5.cs; chạy lại 2 script không đổi gì; kiểm tay các ô đại diện của bảng 5.1.
    /// Không có DB dev thì Skip.
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class D79MatrixSqlTests(ApiFactory factory)
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

        private static async Task RunAsync(SqlConnection cn, SqlTransaction tx, string sql)
        {
            await using var cmd = new SqlCommand(sql, cn, tx) { CommandTimeout = 300 };
            await cmd.ExecuteNonQueryAsync();
        }

        private static string Script(string file) => File.ReadAllText(RepoPath("DB", "scripts", file)).Replace("$(DryRun)", "0");

        [SkippableFact]
        public async Task Scripts_apply_the_screen_matrix_for_19_roles_and_rerun_changes_nothing()
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);
            string connectionString = factory.Services.GetRequiredService<IConfiguration>().GetConnectionString("MainConnectionString")!;
            await using var cn = new SqlConnection(connectionString);
            await cn.OpenAsync();
            await using var tx = (SqlTransaction)await cn.BeginTransactionAsync();
            try
            {
                foreach (string seed in new[] { "ObjectMenu_seed.sql", "UserRole_seed.sql" })
                {
                    await RunAsync(cn, tx, await File.ReadAllTextAsync(RepoPath("DB", "seed", seed)));
                }
                await RunAsync(cn, tx, Script("D79_menu_regroup.sql"));
                await RunAsync(cn, tx, Script("D79_permission_matrix.sql"));

                async Task<int> CountAsync(string sql)
                {
                    await using var cmd = new SqlCommand(sql, cn, tx);
                    return Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }
                const string auditSql = "SELECT COUNT(*) FROM AuditChange WHERE EntityName IN ('UserRole', 'ObjectMenu')";
                int audits = await CountAsync(auditSql);
                await RunAsync(cn, tx, Script("D79_menu_regroup.sql"));
                await RunAsync(cn, tx, Script("D79_permission_matrix.sql"));
                (await CountAsync(auditSql)).Should().Be(audits, "chạy lại 2 script không đổi gì");

                // Ma trận đích = các dòng @Allow trong script sinh (nguồn Seeds.Menu5.cs).
                var expected = Regex.Matches(File.ReadAllText(RepoPath("DB", "scripts", "D79_permission_matrix.sql")),
                        @"\(N'([A-Z-]+)', N'(#/[^']+)', ([01]), ([01]), ([01]), ([01]), ([01]), ([01]), ([01])\)")
                    .ToDictionary(m => (m.Groups[1].Value, m.Groups[2].Value), m => string.Concat(Enumerable.Range(3, 7).Select(i => m.Groups[i].Value)));
                expected.Should().NotBeEmpty();
                var roles = expected.Keys.Select(k => k.Item1).Distinct().ToList();
                roles.Should().HaveCount(19);

                var actual = new Dictionary<(string, string), string>();
                await using (var cmd = new SqlCommand("""
                    SELECT g.Name, m.MetaLink,
                           CONCAT(MAX(CAST(r.AllowAccess AS INT)), MAX(CAST(r.AllowAdd AS INT)), MAX(CAST(r.AllowEdit AS INT)), MAX(CAST(r.AllowDelete AS INT)),
                                  MAX(CAST(r.AllowPrint AS INT)), MAX(CAST(r.AllowExport AS INT)), MAX(CAST(r.AllowImport AS INT)))
                    FROM UserRole r JOIN UserGroup g ON g.Id = r.UserGroupId AND g.IsDeleted = 0
                    JOIN ObjectMenu m ON m.Id = r.ObjectMenuId AND m.IsDeleted = 0 AND m.IsActive = 1 AND m.LevelShow = 2
                    WHERE r.IsDeleted = 0 AND m.MetaLink NOT IN ('#/bot-van-hanh', '#/bot-dieu-hanh')
                    GROUP BY g.Name, m.MetaLink
                    """, cn, tx))
                await using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        if (roles.Contains(reader.GetString(0)))
                        {
                            actual[(reader.GetString(0), reader.GetString(1))] = reader.GetString(2);
                        }
                    }
                }
                var wrong = actual.Where(kv => kv.Value != expected.GetValueOrDefault(kv.Key, "0000000")).Select(kv => $"{kv.Key.Item1} × {kv.Key.Item2}: {kv.Value} (đích {expected.GetValueOrDefault(kv.Key, "0000000")})").ToList();
                wrong.Should().BeEmpty("quyền 19 nhóm sau khi chạy script phải khớp ma trận MENU-5.1");
                expected.Where(kv => kv.Value[0] == '1').Select(kv => kv.Key).Where(k => !actual.ContainsKey(k))
                    .Where(k => k.Item2 is not ("#/crm-price-policy" or "#/menu-assignment" or "#/menu-change-request" or "#/crm-location" or "#/scm-ingredient-cat" or "#/menu-dish-cat" or "#/scm-uom-conversion")).Should().BeEmpty("mọi mục được cấp trong ma trận (đang bật) đều có dòng quyền – #/menu-assignment (UI-6.12, D-90), #/crm-price-policy (UI-6.4, D-83), #/menu-change-request (UI-6.13, D-91), #/crm-location (UI-6.16, D-97) ẩn nhưng giữ quyền");

                // Kiểm tay các ô đại diện của bảng ROADMAP MENU-5.1 (độc lập với bộ sinh). Cờ: Xem Thêm Sửa Xóa In Xuất Nhập.
                string F(string role, string link) => actual.GetValueOrDefault((role, link), "0000000");
                F("OPS-POS", "#/crm-contract").Should().Be("0000000", "POS không vào hợp đồng");
                F("OPS-POS", "#/crm-reconciliation").Should().Be("1110000");
                F("SAL-CS", "#/log-route").Should().Be("0000000", "CSKH không sửa chuyến");
                F("SAL-CS", "#/crm-order").Should().Be("1000000", "CSKH chỉ xem đơn → không tạo đơn bù");
                F("SAL-CS", "#/log-complaint").Should().Be("1110000");
                F("QA", "#/scm-po").Should().Be("0000000", "QA không lập PO");
                F("QA", "#/wf-inbox?type=prod-batch").Should().Be("1010000", "QA Giữ / Loại mẻ");
                F("QA", "#/wf-inbox?type=scm-lot").Should().Be("1010000");
                F("QA", "#/wf-inbox?type=qc-inspection").Should().Be("1010000");
                F("OPS-KIT", "#/prod-batch").Should().Be("1110000");
                F("OPS-KIT", "#/wf-inbox?type=prod-batch").Should().Be("0000000", "bếp không Đạt mẻ tay");
                F("OPS-KIT", "#/wf-inbox?type=qc-inspection").Should().Be("0000000", "bếp không kết luận QC");
                F("OPS-KIT", "#/cost-meal").Should().Be("0000000", "giá vốn không lộ cho bếp");
                F("OPS-WH", "#/scm-lot").Should().Be("1110110");
                F("OPS-WH", "#/wf-inbox?type=scm-lot").Should().Be("0000000", "kho không thả lô");
                F("OPS-WH", "#/scm-po").Should().Be("1000000", "thủ kho chỉ xem đơn mua");
                F("OPS-SCM", "#/scm-gr").Should().Be("1000000", "người mua không ghi sổ nhập");
                F("OPS-SCM", "#/wf-inbox?type=scm-pr-l1").Should().Be("0000000", "người lập ĐN mua không duyệt cấp 1");
                F("OPS-SCM-HEAD", "#/wf-inbox?type=scm-pr-l1").Should().Be("1010000");
                F("OPS-SCM-HEAD", "#/scm-gr").Should().Be("1110110");
                F("SAL-CON", "#/crm-contract").Should().Be("1111111");
                F("SAL-CON", "#/wf-inbox?type=crm-order").Should().Be("0000000", "SAL-CON không còn duyệt sửa đơn sau chốt");
                F("SAL-HEAD", "#/wf-inbox?type=crm-order").Should().Be("1010000");
                F("SAL-HEAD", "#/crm-order").Should().Be("1111111");
                F("COO", "#/wf-inbox?type=crm-order").Should().Be("1010000");
                F("CEO", "#/wf-inbox?type=crm-contract").Should().Be("1010000");
                F("CEO", "#/wf-inbox?type=menu-recipe").Should().Be("0000000", "CEO không duyệt việc vận hành");
                F("CEO", "#/crm-billing").Should().Be("1000110");
                F("CEO", "#/wf-inbox?type=wf-inbox").Should().Be("1000000", "Tất cả yêu cầu duyệt: chỉ xem");
                F("FIN-ACC", "#/wf-inbox").Should().Be("0000000", "Hộp phê duyệt chung chỉ CEO / CEO-PMO / COO / FIN-IT");
                F("FIN-IT", "#/wf-inbox").Should().Be("1000000");
                F("FIN-ACC", "#/cost-meal").Should().Be("1000110");
                F("FIN-HR", "#/adm-position").Should().Be("1111001", "FIN-HR quản Chức vụ");
                F("FIN-HR", "#/wf-inbox?type=hr-leave").Should().Be("1010000");
                F("FIN-IT", "#/adm-job").Should().Be("1111111");
                F("FIN-IT", "#/int-bot").Should().Be("1110110", "giữ quyền D-71");
                F("OPS-LOG", "#/crm-price-policy").Should().Be("0000000", "giá bán không lộ cho giao vận");
                foreach (string role in roles)
                {
                    F(role, "#/hr-my").Should().Be("1000000", role + " vào Việc của tôi");
                }

                // Menu: 14 nhóm theo thứ tự, Việc của tôi đầu, Phê duyệt thứ 2, Kho / Giá thành có mục.
                await using var order = new SqlCommand("""
                    SELECT STRING_AGG(MetaLink, ',') WITHIN GROUP (ORDER BY OrderShow) FROM ObjectMenu
                    WHERE IsDeleted = 0 AND IsActive = 1 AND LevelShow = 1 AND (MetaLink LIKE '#/group/%')
                    """, cn, tx);
                (await order.ExecuteScalarAsync() as string).Should().Be("#/group/00,#/group/approve,#/group/01,#/group/02,#/group/03,#/group/04,#/group/04b,#/group/05,#/group/05b,#/group/06,#/group/07,#/group/08,#/group/10,#/group/09");
                (await CountAsync("SELECT COUNT(*) FROM ObjectMenu c JOIN ObjectMenu p ON p.Id = c.ParentId WHERE p.MetaLink = '#/group/04b' AND c.IsActive = 1 AND c.IsDeleted = 0")).Should().Be(12);
                (await CountAsync("SELECT COUNT(*) FROM ObjectMenu WHERE MetaLink IN ('#/crm-contact', '#/wf-inbox?type=scm-pr', '#/crm-price-policy', '#/menu-assignment') AND IsActive = 1 AND IsDeleted = 0")).Should().Be(0);
                (await CountAsync("SELECT COUNT(*) FROM ObjectMenu WHERE MetaLink = '#/wf-inbox?type=crm-price-policy' AND IsActive = 1 AND IsDeleted = 0")).Should().Be(1, "UI-6.4: quyền duyệt phụ lục điều chỉnh giá");
            }
            finally
            {
                await tx.RollbackAsync();
            }
        }
    }
}
