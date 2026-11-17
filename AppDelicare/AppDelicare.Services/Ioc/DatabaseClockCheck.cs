using AppDelicare.Data.Share;
using Dapper;
using Microsoft.Data.SqlClient;

namespace AppDelicare.Services.Ioc
{
    /// <summary>
    /// T48.4 (D-60): giờ của SQL Server phải trùng giờ VN của ứng dụng (IClock) – nhiều cột mặc định <c>sysdatetime()</c> và SP so ngày theo giờ DB.
    /// SQL Server trong container cần <c>TZ=Asia/Ho_Chi_Minh</c>. Lệch quá <see cref="MaxDrift"/> → /health báo Degraded, Job ghi cảnh báo lúc khởi động.
    /// </summary>
    public static class DatabaseClockCheck
    {
        public static readonly TimeSpan MaxDrift = TimeSpan.FromMinutes(5);

        /// <summary>Giờ DB (<c>SYSDATETIME()</c>) − giờ VN của IClock.</summary>
        public static async Task<TimeSpan> GetDriftAsync(string connectionString, IClock clock, CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(connectionString);
            DateTime dbNow = await connection.ExecuteScalarAsync<DateTime>(new CommandDefinition("SELECT SYSDATETIME();", cancellationToken: cancellationToken));
            return dbNow - clock.Now;
        }

        public static string Describe(TimeSpan drift)
            => $"Giờ SQL Server lệch giờ VN của ứng dụng {drift.TotalMinutes:+0.#;-0.#;0} phút – đặt TZ=Asia/Ho_Chi_Minh cho SQL Server (deploy/README.md, D-60).";
    }
}
