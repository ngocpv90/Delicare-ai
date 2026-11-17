using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// Rà soát BE (2026-11-17): khóa ứng dụng SQL Server (<c>sp_getapplock</c>, giữ tới hết transaction) chống 2 request chạy chồng
    /// trên cùng chứng từ / cùng dòng tồn – không thêm bảng / cột (rowversion) theo luật DB. Phải gọi TRONG transaction, TRƯỚC khi đọc
    /// trạng thái / số tồn: request thứ 2 chờ request 1 commit rồi mới đọc → thấy trạng thái mới và bị chặn như bình thường.
    /// DB không quan hệ (InMemory – test) → bỏ qua.
    /// </summary>
    public static class DbLocks
    {
        /// <summary>Khóa theo chứng từ: <paramref name="kind"/> = loại (vd. MaterialIssue), <paramref name="id"/> = Id chứng từ.</summary>
        public static Task DocumentAsync(DbContext db, string kind, Guid? id, CancellationToken ct = default)
            => AcquireAsync(db, $"Doc:{kind}:{id:N}", ct);

        /// <summary>Khóa theo dòng tồn (kho, vị trí, nguyên liệu, lô) – cộng / trừ tồn lần lượt, không ghi đè lẫn nhau.</summary>
        public static Task BalanceAsync(DbContext db, Guid warehouseId, Guid? locationId, Guid ingredientId, Guid? lotId, CancellationToken ct = default)
            => AcquireAsync(db, $"Inv:{warehouseId:N}:{locationId:N}:{ingredientId:N}:{lotId:N}", ct);

        public static async Task AcquireAsync(DbContext db, string resource, CancellationToken ct = default)
        {
            if (!db.Database.IsRelational())
            {
                return;
            }
            if (db.Database.CurrentTransaction == null)
            {
                throw new InvalidOperationException("DbLocks: phải mở transaction trước khi lấy khóa " + resource);
            }
            await db.Database.ExecuteSqlRawAsync("""
                DECLARE @r INT;
                EXEC @r = sp_getapplock @Resource = @res, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
                IF @r < 0 THROW 50012, N'Chứng từ / tồn kho đang được người khác xử lý – vui lòng thử lại.', 1;
                """, [new SqlParameter("@res", resource)], ct);
        }
    }
}
