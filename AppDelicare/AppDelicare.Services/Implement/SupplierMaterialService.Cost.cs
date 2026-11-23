using AppDelicare.Data.Share;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// BIZ-3.3 (D-77 – C10): thêm / sửa / đổi ưu tiên / xóa dòng bảng giá NCC → tự tính lại giá thành chuẩn các món có công thức khóa đang dùng nguyên liệu đó
    /// (<c>StandardCost/Recalculate</c> theo món), rồi kiểm FC % các thực đơn Đã duyệt / Đã công bố còn hiệu lực hoặc sắp áp dụng có các món này:
    /// FC % = giá vốn / giá bán – giá bán = đơn giá thấp nhất của loại suất trong hợp đồng hiệu lực (PROPOSED: thực đơn chung không gắn khách).
    /// Vượt <c>KpiIngredientToRevenueMaxPct</c> → bot Kế hoạch + COO. Lỗi bước tự động không làm hỏng việc lưu bảng giá.
    /// </summary>
    public partial class SupplierMaterialService
    {
        private async Task<string?> AfterPriceChangeAsync(Guid ingredientId)
        {
            if (!_rules.StandardCostAutoOnSupplierPrice || ingredientId == Guid.Empty)
            {
                return null;
            }
            try
            {
                var db = _unitOfWork.DbContext;
                var dishIds = await (from r in db.Recipe.AsNoTracking()
                                     join l in db.RecipeLine.AsNoTracking() on r.Id equals l.RecipeId
                                     where !r.IsDeleted && r.IsActive && r.Status == RecipeStatus.Locked && !l.IsDeleted && l.IngredientId == ingredientId
                                     select r.DishId).Distinct().ToListAsync();
                if (dishIds.Count == 0)
                {
                    return null;
                }
                var recalculated = await _services.GetRequiredService<IStandardCostService>().Recalculate(new StandardCostRecalculateRequest { ListDishId = string.Join(",", dishIds) });
                int count = recalculated.Data;
                var over = await FoodCostOverAsync(dishIds);
                if (over.Count != 0)
                {
                    var users = new HashSet<Guid>(await RecipientResolver.UsersWithMenuOnlyAsync(db, "#/menu-menu", true));
                    users.UnionWith(await RecipientResolver.UsersInGroupsAsync(db, ["COO"]));
                    string name = await db.Ingredient.AsNoTracking().Where(i => i.Id == ingredientId).Select(i => i.Name).FirstOrDefaultAsync() ?? string.Empty;
                    await BusinessNotifier.SafeEnqueueAsync(_notifications, new NotificationRequest
                    {
                        Template = NotificationCatalog.FoodCostOver, UserIds = [.. users],
                        Data = new { title = $"🔴 Giá NCC {name} đổi – {over.Count} thực đơn vượt FC {_rules.KpiIngredientToRevenueMaxPct:0.#}%", lines = over.Take(10).ToList() },
                        IdempotencyKey = $"{NotificationCatalog.FoodCostOver}:{ingredientId:N}:{_clock.Now:yyyyMMddHHmm}",
                    });
                }
                return string.Format(ConstantHelper.Message.StandardCostAutoRecalculated, count) + (over.Count == 0 ? string.Empty : " – " + string.Format(ConstantHelper.Message.FoodCostOverWarning, string.Join("; ", over.Take(5))));
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[SupplierMaterialService] tự tính lại giá thành sau đổi giá NCC {Ingredient}", ingredientId);
                return ConstantHelper.Message.StandardCostAutoFailed;
            }
        }

        /// <summary>Thực đơn Đã duyệt / Đã công bố còn hiệu lực hoặc sắp áp dụng có món trong <paramref name="dishIds"/> mà FC % (theo loại suất, lựa chọn, thứ) vượt ngưỡng.</summary>
        private async Task<List<string>> FoodCostOverAsync(List<Guid> dishIds)
        {
            var db = _unitOfWork.DbContext;
            DateTime today = _clock.Today;
            var menus = await (from m in db.Menu.AsNoTracking()
                               join l in db.MenuLine.AsNoTracking() on m.Id equals l.MenuId
                               where !m.IsDeleted && !l.IsDeleted && dishIds.Contains(l.DishId) && (m.Status == MenuStatus.Published || m.Status == MenuStatus.Approved)
                               select new { m.Id, m.Code, m.EffectiveFrom, m.EffectiveTo, l.MealType }).Distinct().ToListAsync();
            menus = [.. menus.Where(m => (m.EffectiveTo ?? (m.EffectiveFrom ?? today).AddDays(6)).Date >= today)];
            if (menus.Count == 0)
            {
                return [];
            }
            var prices = await (from cm in db.ContractMeal.AsNoTracking()
                                join c in db.Contract.AsNoTracking() on cm.ContractId equals c.Id
                                where !cm.IsDeleted && !c.IsDeleted && c.Status == ContractStatus.Active && cm.UnitPrice > 0 && cm.MealType != null
                                select new { cm.MealType, cm.UnitPrice }).ToListAsync();
            var minPrice = prices.GroupBy(p => p.MealType!.Trim().ToUpperInvariant()).ToDictionary(g => g.Key, g => g.Min(x => x.UnitPrice!.Value));
            var menuService = _services.GetRequiredService<IMenuService>();
            var result = new List<string>();
            foreach (var g in menus.GroupBy(m => (m.Id, m.Code, Meal: (m.MealType ?? string.Empty).Trim().ToUpperInvariant())))
            {
                if (!minPrice.TryGetValue(g.Key.Meal, out var price))
                {
                    continue;
                }
                var calendar = (await menuService.GetCalendar(g.Key.Id, g.Key.Meal, price, null, null, today)).Data;
                var worst = calendar?.ListTotal.Where(t => t.FoodCostOverTarget).OrderByDescending(t => t.FoodCostPercent).FirstOrDefault();
                if (worst != null)
                {
                    result.Add($"{g.Key.Code} {g.Key.Meal} LC{worst.OptionNo} thứ {(worst.DayOfWeek == 7 ? "CN" : (worst.DayOfWeek + 1).ToString())}: FC {worst.FoodCostPercent:0.#}%");
                }
            }
            return result;
        }
    }
}
