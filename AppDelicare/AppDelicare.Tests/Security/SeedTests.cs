using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Security
{
    /// <summary>
    /// T07 – seed tổ chức / menu / nhóm quyền chạy 2 lần không nhân bản. Chạy trên DB dev trong 1 transaction rồi ROLLBACK
    /// (không để lại dữ liệu). Không có DB dev thì Skip.
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class SeedTests(ApiFactory factory)
    {
        private static readonly string[] SeedFiles = ["OrganizationUnit_seed.sql", "ObjectMenu_seed.sql", "UserRole_seed.sql", "Shift_seed.sql"];

        private const string CountSql = """
            SELECT (SELECT COUNT(*) FROM Company WHERE IsDeleted = 0),
                   (SELECT COUNT(*) FROM OrganizationUnit WHERE IsDeleted = 0),
                   (SELECT COUNT(*) FROM KitchenWorkstation WHERE IsDeleted = 0),
                   (SELECT COUNT(*) FROM ObjectMenu WHERE IsDeleted = 0),
                   (SELECT COUNT(*) FROM UserGroup WHERE IsDeleted = 0),
                   (SELECT COUNT(*) FROM UserRole WHERE IsDeleted = 0),
                   (SELECT COUNT(*) FROM (SELECT Code FROM OrganizationUnit WHERE IsDeleted = 0 AND Code IS NOT NULL GROUP BY Code HAVING COUNT(*) > 1) d),
                   (SELECT COUNT(*) FROM (SELECT MetaLink FROM ObjectMenu WHERE IsDeleted = 0 AND MetaLink LIKE '#/%' GROUP BY MetaLink HAVING COUNT(*) > 1) d),
                   (SELECT COUNT(*) FROM (SELECT UserGroupId, ObjectMenuId FROM UserRole WHERE IsDeleted = 0 GROUP BY UserGroupId, ObjectMenuId HAVING COUNT(*) > 1) d),
                   (SELECT COUNT(*) FROM Shift WHERE IsDeleted = 0 AND Code IN ('SANG_6H','TRUA','TOI','DEM_23H')),
                   (SELECT COUNT(*) FROM (SELECT Code FROM Shift WHERE IsDeleted = 0 AND Code IS NOT NULL GROUP BY Code HAVING COUNT(*) > 1) d)
            """;

        private static string SeedPath(string file)
        {
            string? dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "CLAUDE.md")))
            {
                dir = Path.GetDirectoryName(dir);
            }
            return Path.Combine(dir!, "DB", "seed", file);
        }

        private static async Task<int[]> CountsAsync(SqlConnection cn, SqlTransaction tx)
        {
            await using var cmd = new SqlCommand(CountSql, cn, tx);
            await using var reader = await cmd.ExecuteReaderAsync();
            await reader.ReadAsync();
            return Enumerable.Range(0, reader.FieldCount).Select(reader.GetInt32).ToArray();
        }

        [SkippableFact]
        public async Task Seeds_are_idempotent_and_build_the_org_tree()
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);
            string connectionString = factory.Services.GetRequiredService<IConfiguration>().GetConnectionString("MainConnectionString")!;
            await using var cn = new SqlConnection(connectionString);
            await cn.OpenAsync();
            await using var tx = (SqlTransaction)await cn.BeginTransactionAsync();
            try
            {
                async Task RunAllAsync()
                {
                    foreach (string file in SeedFiles)
                    {
                        // Seed tự BEGIN/COMMIT: trong transaction ngoài thì COMMIT chỉ giảm @@TRANCOUNT, ROLLBACK ngoài vẫn hủy hết.
                        await using var cmd = new SqlCommand(await File.ReadAllTextAsync(SeedPath(file)), cn, tx) { CommandTimeout = 120 };
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                await RunAllAsync();
                int[] first = await CountsAsync(cn, tx);
                await RunAllAsync();
                int[] second = await CountsAsync(cn, tx);

                second.Should().Equal(first, "chạy seed lần 2 không được thêm dòng");
                first[0].Should().BeGreaterThanOrEqualTo(1, "có Company DLC");
                first[1].Should().BeGreaterThanOrEqualTo(33, "33 đơn vị theo ORG_STRUCTURE");
                first[2].Should().BeGreaterThanOrEqualTo(6, "6 tổ bếp là KitchenWorkstation");
                first[6].Should().Be(0, "không trùng mã đơn vị");
                first[7].Should().Be(0, "không trùng MetaLink menu");
                first[8].Should().Be(0, "không trùng cặp nhóm × menu");
                first[9].Should().Be(4, "4 ca: SANG_6H, TRUA, TOI, DEM_23H (T21.1)");
                first[10].Should().Be(0, "không trùng mã ca");

                await using var tree = new SqlCommand("""
                    SELECT p.Code FROM OrganizationUnit c JOIN OrganizationUnit p ON p.Id = c.ParentId
                    WHERE c.Code = 'OPS-KIT-COOK' AND c.IsDeleted = 0
                    """, cn, tx);
                (await tree.ExecuteScalarAsync()).Should().Be("OPS-KIT");
            }
            finally
            {
                await tx.RollbackAsync();
            }
        }
    }
}
