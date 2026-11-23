using AppDelicare.Data.Connection;
using AppDelicare.Model.Model;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// ORD-OPT-1 (D-74 a): lựa chọn của thực đơn = set món – <c>MenuLine.SequenceNo</c> (trống / ≤ 0 = 1) của (thực đơn, loại suất, thứ của ngày ăn).
    /// Dùng chung cho mẫu tạo đơn, kiểm dòng đơn, chi tiết đơn, đăng ký suất tuần, phiếu đăng ký.
    /// </summary>
    public static class MenuOptionRules
    {
        public static int OptionOf(int? sequenceNo) => sequenceNo is > 0 ? sequenceNo.Value : 1;

        /// <summary>Lựa chọn (kèm món, theo SortOrder) của nhiều thực đơn cho 1 ngày: khóa = (MenuId, MealType chữ hoa).</summary>
        public static async Task<Dictionary<(Guid MenuId, string MealType), List<MenuOptionModel>>> ForMenusAsync(DatabaseContext db, IEnumerable<Guid> menuIds, DateTime day)
        {
            var ids = menuIds.Distinct().ToList();
            var result = new Dictionary<(Guid, string), List<MenuOptionModel>>();
            if (ids.Count == 0)
            {
                return result;
            }
            int dayOfWeek = MenuService.MenuDayOf(day.Date);
            var rows = await (from l in db.MenuLine.AsNoTracking()
                              join d in db.Dish.AsNoTracking() on l.DishId equals d.Id
                              where ids.Contains(l.MenuId) && !l.IsDeleted && l.DayOfWeek == dayOfWeek
                              select new { l.MenuId, l.MealType, l.SequenceNo, l.SortOrder, l.DishId, d.Code, d.Name }).ToListAsync();
            foreach (var g in rows.Where(r => r.MealType != null).GroupBy(r => (r.MenuId, MealType: r.MealType!.Trim().ToUpperInvariant())))
            {
                result[g.Key] = g.GroupBy(r => OptionOf(r.SequenceNo)).OrderBy(o => o.Key).Select(o =>
                {
                    var dishes = o.OrderBy(r => r.SortOrder).ThenBy(r => r.Name)
                        .Select(r => new MenuOptionDishModel { DishId = r.DishId, DishCode = r.Code, DishName = r.Name }).ToList();
                    return new MenuOptionModel { OptionNo = o.Key, ListDish = dishes, DishText = string.Join(" · ", dishes.Select(d => d.DishName)) };
                }).ToList();
            }
            return result;
        }

        /// <summary>Lựa chọn của 1 thực đơn / loại suất / ngày (rỗng khi chưa có thực đơn hoặc không có món).</summary>
        public static async Task<List<MenuOptionModel>> OptionsAsync(DatabaseContext db, Guid? menuId, string? mealType, DateTime day)
        {
            if (menuId == null || string.IsNullOrWhiteSpace(mealType))
            {
                return [];
            }
            var all = await ForMenusAsync(db, [menuId.Value], day);
            return all.GetValueOrDefault((menuId.Value, mealType.Trim().ToUpperInvariant())) ?? [];
        }

        public static List<MenuOptionModel> Of(Dictionary<(Guid MenuId, string MealType), List<MenuOptionModel>> map, Guid? menuId, string? mealType)
            => menuId == null || string.IsNullOrWhiteSpace(mealType) ? [] : map.GetValueOrDefault((menuId.Value, mealType.Trim().ToUpperInvariant())) ?? [];
    }
}
