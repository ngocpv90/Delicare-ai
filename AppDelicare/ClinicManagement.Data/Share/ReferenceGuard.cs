using System.Reflection;
using AppDelicare.Data.Connection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AppDelicare.Data.Share
{
    /// <summary>Một nơi đang tham chiếu tới bản ghi.</summary>
    public sealed record ReferenceUsage(string Table, string Column, int Count);

    /// <summary>
    /// T07: kiểm tra bản ghi có đang được tham chiếu (theo quan hệ logic – DB không có FK vật lý) trước khi xóa mềm.
    /// Thay cho SQL động ghép chuỗi qua <c>uspSystem_ExecuteQuery</c> (CODE_AUDIT A10).
    /// </summary>
    public interface IReferenceGuard
    {
        /// <param name="tableOrClass">Tên bảng thật (vd. <c>Dishe</c>) hoặc tên class C# (vd. <c>Dish</c>).</param>
        Task<IReadOnlyList<ReferenceUsage>> FindUsagesAsync(string tableOrClass, Guid id, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Đếm bản ghi chưa xóa mềm ở các bảng nguồn trong <see cref="ReferenceMap.Incoming"/> bằng LINQ (truy vấn có tham số).
    /// Tên bảng / cột chỉ lấy từ map sinh từ tài liệu, không bao giờ từ input người dùng.
    /// </summary>
    public sealed class ReferenceGuard(DatabaseContext db) : IReferenceGuard
    {
        private static readonly MethodInfo CountMethod =
            typeof(ReferenceGuard).GetMethod(nameof(CountAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

        public async Task<IReadOnlyList<ReferenceUsage>> FindUsagesAsync(string tableOrClass, Guid id, CancellationToken cancellationToken = default)
        {
            string table = ReferenceMap.TableOfClass.TryGetValue(tableOrClass, out var t) ? t : tableOrClass;
            if (!ReferenceMap.Incoming.TryGetValue(table, out var sources))
            {
                return [];
            }

            var usages = new List<ReferenceUsage>();
            foreach (var (sourceTable, column) in sources)
            {
                IEntityType? entity = db.Model.GetEntityTypes().FirstOrDefault(e => string.Equals(e.GetTableName(), sourceTable, StringComparison.OrdinalIgnoreCase));
                if (entity == null)
                {
                    continue;
                }
                var store = StoreObjectIdentifier.Table(sourceTable, entity.GetSchema());
                IProperty? property = entity.GetProperties().FirstOrDefault(p => p.GetColumnName(store) == column);
                if (property == null || (Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType) != typeof(Guid))
                {
                    continue;
                }
                bool hasIsDeleted = entity.FindProperty("IsDeleted") != null;
                var task = (Task<int>)CountMethod.MakeGenericMethod(entity.ClrType)
                    .Invoke(null, [db, property.Name, id, hasIsDeleted, cancellationToken])!;
                int count = await task;
                if (count > 0)
                {
                    usages.Add(new ReferenceUsage(sourceTable, column, count));
                }
            }
            return usages;
        }

        private static Task<int> CountAsync<T>(DatabaseContext db, string property, Guid id, bool hasIsDeleted, CancellationToken cancellationToken)
            where T : class
        {
            IQueryable<T> query = db.Set<T>().AsNoTracking().Where(e => EF.Property<Guid?>(e, property) == id);
            if (hasIsDeleted)
            {
                query = query.Where(e => !EF.Property<bool>(e, "IsDeleted"));
            }
            return query.CountAsync(cancellationToken);
        }
    }
}
