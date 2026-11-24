using AppDelicare.Data.Connection;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6.26 (D-107, GAP-PRD-10): dây chuyền nấu của từng món = dây chuyền của nhóm món (<c>Dishe.DishCategoryId</c> → <c>DishCategorie.ProductionLineId</c>);
    /// nhóm trống → nhóm cha (<c>ParentId</c>, tối đa 5 cấp). Dây chuyền đã xóa / Ngừng hoạt động → coi như chưa gán + cảnh báo.
    /// Dùng chung cho Ra lệnh kế hoạch, tự lập sau chốt (qua Release) và bổ sung đơn chốt muộn. Không có dây chuyền → 1 mẻ, thời lượng mặc định (cách cũ).
    /// </summary>
    public static class ProductionLineRules
    {
        public const string Inactive = "Inactive";

        public sealed record DishLine(ProductionLine? Line, string? CategoryName, string? Warning);

        public static bool IsActive(ProductionLine? line) => line != null && !line.IsDeleted && !string.Equals(line.Status, Inactive, StringComparison.OrdinalIgnoreCase);

        public static async Task<Dictionary<Guid, DishLine>> ForDishesAsync(DatabaseContext db, IReadOnlyCollection<Guid> dishIds)
        {
            var result = new Dictionary<Guid, DishLine>();
            if (dishIds.Count == 0)
            {
                return result;
            }
            var ids = dishIds.Distinct().ToList();
            var dishes = await db.Dish.AsNoTracking().Where(d => ids.Contains(d.Id)).Select(d => new { d.Id, d.Name, d.Code, d.DishCategoryId }).ToListAsync();
            var cats = await db.DishCategory.AsNoTracking().Where(c => !c.IsDeleted).Select(c => new { c.Id, c.Name, c.ParentId, c.ProductionLineId }).ToDictionaryAsync(c => c.Id);
            var lineIds = cats.Values.Where(c => c.ProductionLineId != null).Select(c => c.ProductionLineId!.Value).Distinct().ToList();
            var lines = await db.ProductionLine.AsNoTracking().Where(l => lineIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id);
            foreach (var d in dishes)
            {
                string dishName = d.Name ?? d.Code ?? d.Id.ToString();
                if (d.DishCategoryId is not { } catId || !cats.TryGetValue(catId, out var cat))
                {
                    result[d.Id] = new DishLine(null, null, string.Format(ConstantHelper.Message.DishNoCategoryOneBatch, dishName));
                    continue;
                }
                var c = cat;
                Guid? lineId = null;
                for (int depth = 0; depth < 5 && c != null; depth++)
                {
                    if (c.ProductionLineId is { } l)
                    {
                        lineId = l;
                        break;
                    }
                    c = c.ParentId is { } p && cats.TryGetValue(p, out var parent) ? parent : null;
                }
                var line = lineId is { } x ? lines.GetValueOrDefault(x) : null;
                if (lineId != null && !IsActive(line))
                {
                    result[d.Id] = new DishLine(null, cat.Name, string.Format(ConstantHelper.Message.DishLineInactive, dishName, cat.Name, line?.Name ?? line?.Code ?? string.Empty));
                    continue;
                }
                result[d.Id] = line == null
                    ? new DishLine(null, cat.Name, string.Format(ConstantHelper.Message.DishNoLineOneBatch, dishName, cat.Name))
                    : new DishLine(line, cat.Name, null);
            }
            return result;
        }

        /// <summary>Phút / mẻ của dây chuyền (làm tròn lên), không có → mặc định cấu hình.</summary>
        public static int Minutes(ProductionLine? line, int defaultMinutes) => line?.MinutesPerBatch is > 0 ? (int)Math.Ceiling(line.MinutesPerBatch.Value) : defaultMinutes;
    }
}
