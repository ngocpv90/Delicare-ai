using System.Collections.Concurrent;
using AppDelicare.Data.Connection;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AppDelicare.Services.Jobs
{
    /// <summary>1 lần chạy ghi vào <c>AnalyticsRefreshLog</c> (bảng có sẵn).</summary>
    public sealed class AnalyticsRunEntry
    {
        public Guid RunId { get; set; }
        public string JobCode { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string RunStatus { get; set; } = string.Empty;
        public long? RowsProcessed { get; set; }
        public string? SourceWatermark { get; set; }
        /// <summary>Thông báo / lỗi → cột ErrorMessage (nvarchar(2000), cắt bớt).</summary>
        public string? Message { get; set; }
    }

    /// <summary>
    /// T23 + BE3.4: khóa chống chạy trùng + nhật ký <c>AnalyticsRefreshLog</c> dùng chung cho <see cref="JobRunService"/> (job hẹn giờ / Job/Run)
    /// và nút làm mới KPI (<c>KpiSnapshot/Refresh</c>):
    /// <list type="bullet">
    /// <item>SQL Server: <c>sp_getapplock</c> theo mã job (khóa phiên trên kết nối riêng, không chờ – nhiều instance / AppDelicare.Job cũng không chạy trùng);
    /// log ghi bằng kết nối riêng (không nằm trong transaction tính toán → lỗi vẫn còn dòng Failed).</item>
    /// <item>EF InMemory (test): khóa trong tiến trình + log qua DbContext.</item>
    /// </list>
    /// Khóa tên <c>DELICARE_JOB_&lt;mã&gt;</c> → job KPI_DAILY của AppDelicare.Job và nút bấm tay KPI_DAILY không chạy cùng lúc.
    /// </summary>
    public sealed class AnalyticsRunGate(IConfiguration configuration, IClock clock, DatabaseContext? db = null)
    {
        public const string LockPrefix = "DELICARE_JOB_";
        private const int MaxMessage = 2000;
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> MemoryLocks = new(StringComparer.OrdinalIgnoreCase);

        private bool UseSql => db == null || db.Database.IsRelational();

        private string ConnectionString => configuration.GetConnectionString(ConstantHelper.MainConnectionString)
            ?? throw new InvalidOperationException("Thiếu ConnectionStrings:MainConnectionString.");

        /// <summary>Lấy khóa theo mã job (không chờ). null = đang có lần chạy khác. Dispose = nhả khóa.</summary>
        public async Task<IAsyncDisposable?> TryAcquireAsync(string jobCode, CancellationToken cancellationToken = default)
        {
            string resource = LockPrefix + jobCode;
            if (!UseSql)
            {
                var gate = MemoryLocks.GetOrAdd(resource, _ => new SemaphoreSlim(1, 1));
                return await gate.WaitAsync(0, cancellationToken) ? new MemoryLock(gate) : null;
            }
            var connection = new SqlConnection(ConnectionString);
            try
            {
                await connection.OpenAsync(cancellationToken);
                int result = await connection.ExecuteScalarAsync<int>(
                    "DECLARE @r int; EXEC @r = sp_getapplock @Resource = @Resource, @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 0; SELECT @r;",
                    new { Resource = resource });
                if (result < 0)
                {
                    await connection.DisposeAsync();
                    return null;
                }
                return new SqlLock(connection, resource);
            }
            catch
            {
                await connection.DisposeAsync();
                throw;
            }
        }

        public async Task InsertLogAsync(AnalyticsRunEntry run, Guid actorId)
        {
            if (!UseSql)
            {
                db!.AnalyticsRefreshLog.Add(new AnalyticsRefreshLog
                {
                    Id = run.RunId,
                    JobCode = run.JobCode,
                    RunId = run.RunId,
                    StartedAt = run.StartedAt,
                    CompletedAt = run.CompletedAt,
                    RunStatus = run.RunStatus,
                    RowsProcessed = run.RowsProcessed,
                    SourceWatermark = run.SourceWatermark,
                    ErrorMessage = Truncate(run.Message),
                });
                await db.SaveChangesAsync(ConstantHelper.TypeSave.Insert);
                return;
            }
            await using var connection = new SqlConnection(ConnectionString);
            await connection.ExecuteAsync(
                "INSERT INTO dbo.AnalyticsRefreshLog (Id, JobCode, RunId, StartedAt, CompletedAt, RunStatus, RowsProcessed, SourceWatermark, ErrorMessage, CreatedDate, CreatedBy, IsDeleted)"
                + " VALUES (@Id, @JobCode, @RunId, @StartedAt, @CompletedAt, @RunStatus, @RowsProcessed, @SourceWatermark, @ErrorMessage, @CreatedDate, @CreatedBy, 0);",
                new
                {
                    Id = run.RunId,
                    run.JobCode,
                    run.RunId,
                    run.StartedAt,
                    run.CompletedAt,
                    run.RunStatus,
                    run.RowsProcessed,
                    run.SourceWatermark,
                    ErrorMessage = Truncate(run.Message),
                    CreatedDate = clock.Now,
                    CreatedBy = actorId,
                });
        }

        public async Task UpdateLogAsync(AnalyticsRunEntry run, Guid actorId)
        {
            if (!UseSql)
            {
                // Bỏ mọi thay đổi còn dở của lần tính (lỗi giữa chừng) rồi mới cập nhật dòng log.
                db!.ChangeTracker.Clear();
                var row = await db.AnalyticsRefreshLog.FirstAsync(x => x.Id == run.RunId);
                row.CompletedAt = run.CompletedAt;
                row.RunStatus = run.RunStatus;
                row.RowsProcessed = run.RowsProcessed;
                row.SourceWatermark = run.SourceWatermark;
                row.ErrorMessage = Truncate(run.Message);
                await db.SaveChangesAsync(ConstantHelper.TypeSave.Update);
                return;
            }
            await using var connection = new SqlConnection(ConnectionString);
            await connection.ExecuteAsync(
                "UPDATE dbo.AnalyticsRefreshLog SET CompletedAt = @CompletedAt, RunStatus = @RunStatus, RowsProcessed = @RowsProcessed, SourceWatermark = @SourceWatermark,"
                + " ErrorMessage = @ErrorMessage, ModifiedDate = @ModifiedDate, ModifiedBy = @ModifiedBy WHERE Id = @Id;",
                new
                {
                    Id = run.RunId,
                    run.CompletedAt,
                    run.RunStatus,
                    run.RowsProcessed,
                    run.SourceWatermark,
                    ErrorMessage = Truncate(run.Message),
                    ModifiedDate = clock.Now,
                    ModifiedBy = actorId,
                });
        }

        private static string? Truncate(string? text) => text is { Length: > MaxMessage } ? text[..MaxMessage] : text;

        private sealed class SqlLock(SqlConnection connection, string resource) : IAsyncDisposable
        {
            public async ValueTask DisposeAsync()
            {
                try
                {
                    await connection.ExecuteAsync("EXEC sp_releaseapplock @Resource = @Resource, @LockOwner = 'Session';", new { Resource = resource });
                }
                finally
                {
                    await connection.DisposeAsync();
                }
            }
        }

        private sealed class MemoryLock(SemaphoreSlim gate) : IAsyncDisposable
        {
            private int _released;

            public ValueTask DisposeAsync()
            {
                if (Interlocked.Exchange(ref _released, 1) == 0)
                {
                    gate.Release();
                }
                return ValueTask.CompletedTask;
            }
        }
    }
}
