using AppDelicare.Model.Entity;
using AppDelicare.Services.Implement;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text.Json.Nodes;

namespace AppDelicare.Tests.Security
{
    /// <summary>
    /// DEPLOY-SYNC-1.5 (D-134): CHECK menu / quyền. (1) Trên DB dev, trong transaction rồi ROLLBACK: chạy cấu hình hội tụ (Z_Menu_sync + D79_permission_matrix) → CHECK 0 dòng;
    /// đổi tay 1 tên mục + 1 ô quyền → CHECK bắt đúng 2 dòng. Bản nhúng API (ConfigCheckSql) trùng file DB/scripts. (2) API ConfigCheck/Get: admin 200, người thường 403.
    /// Không có DB dev thì Skip phần (1).
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class ConfigCheckSqlTests(ApiFactory factory)
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

        private static string Check => string.Join('\n', ConfigCheckSql.Script.Replace("\r", string.Empty).Split('\n').Where(l => !l.TrimStart().StartsWith("IF @N > 0 RAISERROR", StringComparison.Ordinal)));

        private static async Task<List<string>> RunCheckAsync(SqlConnection cn, SqlTransaction tx)
        {
            var rows = new List<string>();
            await using var cmd = new SqlCommand(Check, cn, tx) { CommandTimeout = 300 };
            await using var r = await cmd.ExecuteReaderAsync();
            do
            {
                if (r.FieldCount == 3)
                {
                    while (await r.ReadAsync())
                    {
                        rows.Add($"{r.GetString(0)} | {r.GetString(1)} | {(r.IsDBNull(2) ? string.Empty : r.GetString(2))}");
                    }
                }
            }
            while (await r.NextResultAsync());
            return rows;
        }

        [Fact]
        public void Embedded_check_equals_db_script()
            => ConfigCheckSql.Script.Replace("\r", string.Empty).Trim().Should().Be(File.ReadAllText(RepoPath("DB", "scripts", "CHECK_menu_permission.sql")).Replace("\r", string.Empty).Trim());

        [SkippableFact]
        public async Task Check_is_zero_after_convergent_config_and_catches_drift()
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);
            string connectionString = factory.Services.GetRequiredService<IConfiguration>().GetConnectionString("MainConnectionString")!;
            await using var cn = new SqlConnection(connectionString);
            await cn.OpenAsync();
            await using var tx = (SqlTransaction)await cn.BeginTransactionAsync();
            try
            {
                async Task Run(string sql)
                {
                    await using var cmd = new SqlCommand(sql, cn, tx) { CommandTimeout = 300 };
                    await cmd.ExecuteNonQueryAsync();
                }
                // Ghi nhận giả các script chuyển đổi (DB dev đã ADOPT – trong transaction phòng DB thử chưa có).
                foreach (string f in File.ReadAllLines(RepoPath("deploy", "db", "once.order")).Select(l => l.Split('#')[0].Trim()).Where(l => l.Length > 0).Select(Path.GetFileName))
                {
                    await Run($"IF NOT EXISTS (SELECT 1 FROM sys.extended_properties WHERE class = 0 AND name = N'DelicareOnce:{f}') EXEC sys.sp_addextendedproperty @name = N'DelicareOnce:{f}', @value = N'TEST';");
                }
                await Run(File.ReadAllText(RepoPath("DB", "seed", "Z_Menu_sync.sql")).Replace("BEGIN TRANSACTION;", string.Empty).Replace("COMMIT TRANSACTION;", string.Empty));
                var perm = File.ReadAllText(RepoPath("DB", "scripts", "D79_permission_matrix.sql")).Replace("$(DryRun)", "0");
                await Run(perm.Replace("BEGIN TRANSACTION;", string.Empty).Replace("COMMIT TRANSACTION;", string.Empty).Replace("ROLLBACK TRANSACTION;", string.Empty));
                (await RunCheckAsync(cn, tx)).Should().BeEmpty("sau cấu hình hội tụ CHECK = 0");

                await Run("UPDATE ObjectMenu SET Name = N'Tên sai D-134' WHERE MetaLink = N'#/scm-gr' AND ISNULL(IsDeleted, 0) = 0;");
                await Run("""
                    UPDATE r SET AllowDelete = 1 FROM UserRole r JOIN UserGroup g ON g.Id = r.UserGroupId JOIN ObjectMenu m ON m.Id = r.ObjectMenuId
                    WHERE ISNULL(NULLIF(g.Code, N''), g.Name) = N'OPS-WH' AND m.MetaLink = N'#/scm-gr' AND ISNULL(r.IsDeleted, 0) = 0;
                    """);
                var drift = await RunCheckAsync(cn, tx);
                drift.Should().HaveCount(2, string.Join("; ", drift));
                drift.Should().Contain(x => x.Contains("#/scm-gr") && x.Contains("Tên sai D-134")).And.Contain(x => x.Contains("OPS-WH × #/scm-gr"));
            }
            finally
            {
                await tx.RollbackAsync();
            }
        }

        [Fact]
        public async Task Api_is_admin_only_and_reports_unsupported_on_in_memory()
        {
            using var f = new InMemoryApiFactory();
            Guid plain = Guid.NewGuid();
            await f.SeedAsync(db =>
            {
                db.User.Add(new User { Id = plain, UserName = "uat.khong-quyen-sync", PassWord = "!NO-LOGIN!", IsActive = true, AccountType = "INTERNAL" });
                return Task.CompletedTask;
            });
            using var admin = f.CreateAuthorizedClient();
            var res = JsonNode.Parse(await (await admin.GetAsync("/api/ConfigCheck/Get")).Content.ReadAsStringAsync());
            res!["data"]!["supported"]!.GetValue<bool>().Should().BeFalse();
            using var user = f.CreateAuthorizedClient(plain);
            (await user.GetAsync("/api/ConfigCheck/Get")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }
}
