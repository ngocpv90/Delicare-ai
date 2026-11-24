using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.HrAccount
{
    /// <summary>
    /// HR-ACC-1.2 (D-82 d): DB/scripts/D82_link_employee_person.sql trên SQL Server thật (ConnectionStrings:TestDb, không có thì DB dev), trong 1 transaction rồi ROLLBACK:
    /// khớp chắc (tên đăng nhập = mã NV) được điền PersonId; trùng SĐT 2 người = nhiều ứng viên, không có = không khớp → không điền; chạy lại lần 2 = 0 việc.
    /// Cần migration 20261123_02 (cột Employee.PersonId) – DB chưa có cột thì Skip.
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class D82LinkScriptSqlTests(ApiFactory factory)
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

        private static async Task<object?> ScalarAsync(SqlConnection cn, SqlTransaction tx, string sql)
        {
            await using var cmd = new SqlCommand(sql, cn, tx) { CommandTimeout = 300 };
            return await cmd.ExecuteScalarAsync();
        }

        private static async Task RunAsync(SqlConnection cn, SqlTransaction tx, string sql)
        {
            await using var cmd = new SqlCommand(sql, cn, tx) { CommandTimeout = 300 };
            await cmd.ExecuteNonQueryAsync();
        }

        [SkippableFact]
        public async Task Script_links_sure_matches_only_and_rerun_changes_nothing()
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);
            string connectionString = factory.Services.GetRequiredService<IConfiguration>().GetConnectionString("MainConnectionString")!;
            await using var cn = new SqlConnection(connectionString);
            await cn.OpenAsync();
            await using (var check = new SqlCommand("SELECT COL_LENGTH(N'dbo.Employee', N'PersonId')", cn))
            {
                Skip.If(await check.ExecuteScalarAsync() is DBNull or null, "DB chưa chạy migration 20261123_02 (Employee.PersonId)");
            }
            await using var tx = (SqlTransaction)await cn.BeginTransactionAsync();
            try
            {
                Guid by = (Guid)(await ScalarAsync(cn, tx, "SELECT TOP 1 Id FROM dbo.[User] WHERE UserName = N'system' AND ISNULL(IsDeleted, 0) = 0"))!;
                Guid p1 = Guid.NewGuid(), p2 = Guid.NewGuid(), p3 = Guid.NewGuid(), e1 = Guid.NewGuid(), e2 = Guid.NewGuid(), e3 = Guid.NewGuid(), u1 = Guid.NewGuid();
                string k = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
                await RunAsync(cn, tx, $@"
SET QUOTED_IDENTIFIER ON;
INSERT INTO dbo.Person (Id, FullName, PhoneNumber, CreatedDate, CreatedBy, IsDeleted) VALUES
  ('{p1}', N'[TEST] Người 1 {k}', NULL, GETDATE(), '{by}', 0),
  ('{p2}', N'[TEST] Người 2 {k}', N'+84 977 {k[..3]} 002', GETDATE(), '{by}', 0),
  ('{p3}', N'[TEST] Người 3 {k}', N'0977.{k[..3]}.002', GETDATE(), '{by}', 0);
INSERT INTO dbo.[User] (Id, UserName, PersonId, IsActive, IsAdmin, IsDefault, CreatedDate, CreatedBy, IsDeleted) VALUES
  ('{u1}', N'hrsql-{k.ToLowerInvariant()}-1', '{p1}', 1, 0, 0, GETDATE(), '{by}', 0);
INSERT INTO dbo.Employee (Id, Code, EmployeeCode, FullName, Phone, Status, CreatedDate, CreatedBy, IsDeleted) VALUES
  ('{e1}', N'HRSQL-{k}-1', N'HRSQL-{k}-1', N'[TEST] NV khớp chắc', NULL, N'Active', GETDATE(), '{by}', 0),
  ('{e2}', N'HRSQL-{k}-2', N'HRSQL-{k}-2', N'[TEST] NV trùng SĐT', N'0977{k[..3]}002', N'Active', GETDATE(), '{by}', 0),
  ('{e3}', N'HRSQL-{k}-3', N'HRSQL-{k}-3', N'[TEST] NV không khớp', N'0911999999', N'Active', GETDATE(), '{by}', 0);");

                string script = File.ReadAllText(RepoPath("DB", "scripts", "D82_link_employee_person.sql")).Replace("$(DryRun)", "0");
                await RunAsync(cn, tx, script);
                async Task<object?> PersonOf(Guid e) => await ScalarAsync(cn, tx, $"SELECT PersonId FROM dbo.Employee WHERE Id = '{e}'");
                (await PersonOf(e1)).Should().Be(p1, "tên đăng nhập = mã NV (không phân biệt hoa thường) → khớp chắc");
                (await PersonOf(e2)).Should().Be(DBNull.Value, "SĐT (chuẩn hóa +84 / 0) khớp 2 người → nhiều ứng viên");
                (await PersonOf(e3)).Should().Be(DBNull.Value, "không khớp");
                string auditSql = $"SELECT COUNT(*) FROM dbo.AuditChange WHERE EntityName = N'Employee' AND IdChange IN ('{e1}', '{e2}', '{e3}') AND Changes LIKE N'%HR-ACC-1.2%'";
                ((int)(await ScalarAsync(cn, tx, auditSql))!).Should().Be(1);

                await RunAsync(cn, tx, script);
                ((int)(await ScalarAsync(cn, tx, auditSql))!).Should().Be(1, "chạy lại lần 2 = 0 việc");
                (await PersonOf(e2)).Should().Be(DBNull.Value);
            }
            finally
            {
                await tx.RollbackAsync();
            }
        }
    }
}
