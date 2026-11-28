using System.Data;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Memory;

namespace AppDelicare.Services.Implement
{
    public sealed class ConfigCheckRowModel
    {
        public string Kind { get; set; } = string.Empty;
        public string Item { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
    }

    public sealed class ConfigCheckModel
    {
        /// <summary>false = DB không phải SQL Server (vd test InMemory) – không kiểm được.</summary>
        public bool Supported { get; set; }
        public int Count { get; set; }
        public DateTime CheckedAt { get; set; }
        /// <summary>Tối đa 50 dòng lệch đầu tiên.</summary>
        public List<ConfigCheckRowModel> ListRow { get; set; } = [];
    }

    /// <summary>
    /// DEPLOY-SYNC-1.5 (D-134): chạy cùng câu <c>DB/scripts/CHECK_menu_permission.sql</c> (bản nhúng <see cref="ConfigCheckSql"/>, sinh từ CodeGen) trên DB đang dùng –
    /// admin thấy cảnh báo trên web khi menu / quyền / script chuyển đổi lệch bản chuẩn (DB chưa chạy quy trình cập nhật). Chỉ đọc; nhớ kết quả 10 phút.
    /// </summary>
    public interface IConfigCheckService
    {
        Task<IResult<ConfigCheckModel>> Run(bool refresh = false);
    }

    public class ConfigCheckService(IUnitOfWork unitOfWork, IMemoryCache cache) : IConfigCheckService
    {
        private const string CacheKey = "config-check:menu-permission";

        public async Task<IResult<ConfigCheckModel>> Run(bool refresh = false)
        {
            if (!refresh && cache.TryGetValue(CacheKey, out ConfigCheckModel? hit) && hit != null)
            {
                return Result<ConfigCheckModel>.Success(hit, "OK");
            }
            var db = unitOfWork.DbContext.Database;
            var model = new ConfigCheckModel { CheckedAt = DateTime.UtcNow.AddHours(7) };
            if (!db.IsSqlServer())
            {
                return Result<ConfigCheckModel>.Success(model, "DB không phải SQL Server – không kiểm được");
            }
            // Bỏ dòng RAISERROR (dành cho sqlcmd -b trong quy trình cập nhật) – ở web chỉ đọc kết quả.
            string sql = string.Join('\n', ConfigCheckSql.Script.Replace("\r", string.Empty).Split('\n').Where(l => !l.TrimStart().StartsWith("IF @N > 0 RAISERROR", StringComparison.Ordinal)));
            var conn = db.GetDbConnection();
            bool opened = conn.State != ConnectionState.Open;
            if (opened)
            {
                await conn.OpenAsync();
            }
            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.CommandTimeout = 120;
                if (db.CurrentTransaction is { } tx)
                {
                    cmd.Transaction = tx.GetDbTransaction();
                }
                await using var reader = await cmd.ExecuteReaderAsync();
                do
                {
                    if (reader.FieldCount == 3)
                    {
                        while (await reader.ReadAsync())
                        {
                            model.Count++;
                            if (model.ListRow.Count < 50)
                            {
                                model.ListRow.Add(new ConfigCheckRowModel { Kind = reader.GetString(0), Item = reader.GetString(1), Detail = reader.IsDBNull(2) ? string.Empty : reader.GetString(2) });
                            }
                        }
                    }
                }
                while (await reader.NextResultAsync());
            }
            finally
            {
                if (opened)
                {
                    await conn.CloseAsync();
                }
            }
            model.Supported = true;
            cache.Set(CacheKey, model, TimeSpan.FromMinutes(10));
            return Result<ConfigCheckModel>.Success(model, model.Count == 0 ? "Menu / quyền đúng bản chuẩn" : $"{model.Count} dòng lệch bản chuẩn");
        }
    }
}
