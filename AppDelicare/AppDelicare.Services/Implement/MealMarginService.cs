using System.Globalization;
using AppDelicare.Data.Connection;
using AppDelicare.Data.Share;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;

namespace AppDelicare.Services.Interface
{
    /// <summary>UI-6.27 (D-108): báo cáo Giá vốn & lãi gộp + tính lại giá vốn suất đang tạm tính theo giá chuẩn.</summary>
    public interface IMealMarginService
    {
        Task<IResult<MealMarginModel>> GetMargin(MealMarginRequest request);
        Task<int> RecalcProvisionalAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
    }
}

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6.27 (D-108): "Giá vốn & lãi gộp" – tổng hợp từ MealCost (doanh thu, giá vốn, suất nhận theo dòng đơn), ActualCost (thực tế / phần theo món),
    /// StandardCost (chuẩn / phần), MaterialConsumptionVariance (nguyên nhân chênh). Đơn "tạm tính" = có món mà lệnh SX của ngày ăn chưa có giá thành thực tế.
    /// Không thêm bảng / cột. Nhân công / chung = 0 (chưa có định mức) → nhãn "Lãi sau nguyên liệu".
    /// </summary>
    public class MealMarginService(IUnitOfWork unitOfWork, IClock clock, IOptions<BusinessRulesOptions> rules, IMealCostService mealCostService) : Interface.IMealMarginService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly BusinessRulesOptions _rules = rules.Value;
        private readonly IMealCostService _mealCostService = mealCostService;

        private sealed record OrderAgg(Guid OrderId, DateTime Date, Guid CustomerId, Guid? ContractId, bool Fault, decimal Meals, decimal Revenue, decimal Cost, bool Provisional);

        /// <summary>Đơn có món chưa có giá thành thực tế (lệnh SX của ngày ăn cùng món chưa có ActualCost) – giá vốn đang tạm tính theo giá chuẩn.</summary>
        public static async Task<HashSet<Guid>> ProvisionalOrderIdsAsync(DatabaseContext db, IReadOnlyCollection<(Guid OrderId, DateTime Date, Guid DishId)> rows)
        {
            var result = new HashSet<Guid>();
            if (rows.Count == 0)
            {
                return result;
            }
            DateTime dFrom = rows.Min(r => r.Date).Date, dTo = rows.Max(r => r.Date).Date.AddDays(1);
            var actual = (await (from o in db.ProductionOrder.AsNoTracking()
                                 join p in db.ProductionPlan.AsNoTracking() on o.ProductionPlanId equals p.Id
                                 where !o.IsDeleted && !p.IsDeleted && p.ProductionDate >= dFrom && p.ProductionDate < dTo
                                       && db.ActualCost.Any(c => !c.IsDeleted && c.ProductionOrderId == o.Id)
                                 select new { p.ProductionDate, o.DishId }).ToListAsync())
                .Select(x => (x.ProductionDate.Date, x.DishId)).ToHashSet();
            foreach (var r in rows.Where(r => !actual.Contains((r.Date.Date, r.DishId))))
            {
                result.Add(r.OrderId);
            }
            return result;
        }

        private static (DateTime From, DateTime To, string Month) Period(string? month, DateTime today)
        {
            DateTime first = DateTime.TryParseExact(month ?? string.Empty, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var m) ? m : new DateTime(today.Year, today.Month, 1);
            DateTime last = first.AddMonths(1).AddDays(-1);
            if (last > today && first <= today)
            {
                last = today;
            }
            return (first, last, first.ToString("yyyy-MM", CultureInfo.InvariantCulture));
        }

        private async Task<List<OrderAgg>> AggregateAsync(DatabaseContext db, DateTime from, DateTime to, Guid? customerId, Guid? contractId)
        {
            DateTime end = to.AddDays(1);
            var orders = await db.Order.AsNoTracking()
                .Where(o => !o.IsDeleted && o.MealDate >= from && o.MealDate < end && (customerId == null || o.CustomerId == customerId) && (contractId == null || o.ContractId == contractId)
                            && db.MealCost.Any(m => !m.IsDeleted && m.OrderId == o.Id))
                .Select(o => new { o.Id, o.MealDate, o.CustomerId, o.ContractId, o.Note, o.TotalAmount }).ToListAsync();
            var ids = orders.Select(o => o.Id).ToList();
            var rows = await db.MealCost.AsNoTracking().Where(m => !m.IsDeleted && ids.Contains(m.OrderId))
                .Select(m => new { m.OrderId, m.OrderLineId, m.DishId, m.AcceptedMealQuantity, m.TotalActualCost, m.Revenue }).ToListAsync();
            var byOrder = rows.GroupBy(r => r.OrderId).ToDictionary(g => g.Key, g => g.ToList());
            var dates = orders.ToDictionary(o => o.Id, o => o.MealDate.Date);
            var provisional = await ProvisionalOrderIdsAsync(db, [.. rows.Select(r => (r.OrderId, dates[r.OrderId], r.DishId))]);
            return [.. orders.Select(o =>
            {
                var mine = byOrder.GetValueOrDefault(o.Id) ?? [];
                decimal meals = mine.GroupBy(r => r.OrderLineId).Sum(g => g.First().AcceptedMealQuantity);
                bool fault = o.Note?.Contains("[DON_BU:", StringComparison.Ordinal) == true && (o.TotalAmount ?? 0m) == 0m;
                return new OrderAgg(o.Id, o.MealDate.Date, o.CustomerId, o.ContractId, fault, meals, mine.Sum(r => r.Revenue ?? 0m), mine.Sum(r => r.TotalActualCost), provisional.Contains(o.Id));
            })];
        }

        private static decimal? Pct(decimal num, decimal den) => den != 0 ? Math.Round(num / den * 100m, 1) : null;
        private static decimal? Per(decimal num, decimal den) => den > 0 ? Math.Round(num / den, 0) : null;

        public async Task<IResult<MealMarginModel>> GetMargin(MealMarginRequest request)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var (from, to, month) = Period(request.Month, _clock.Today.Date);
                string group = (request.GroupBy ?? "customer").Trim().ToLowerInvariant();
                var aggs = await AggregateAsync(db, from, to, request.CustomerId, request.ContractId);
                var sale = aggs.Where(a => !a.Fault).ToList();
                var model = new MealMarginModel
                {
                    Month = month, From = from, To = to, GroupBy = group, ThresholdPct = _rules.KpiIngredientToRevenueMaxPct,
                    LaborIncluded = _rules.RecipeLaborCostPerServing > 0 || _rules.RecipeOverheadCostPerServing > 0 || _rules.ProductionLaborCostPerHour > 0,
                };
                decimal meals = sale.Sum(a => a.Meals), rev = sale.Sum(a => a.Revenue), cost = sale.Sum(a => a.Cost);
                var prevFrom = from.AddMonths(-1);
                var prev = (await AggregateAsync(db, prevFrom, from.AddDays(-1), request.CustomerId, request.ContractId)).Where(a => !a.Fault).ToList();
                model.Summary = new MealMarginSummaryModel
                {
                    OrderCount = aggs.Count, Meals = meals, Revenue = rev, Cost = cost, Margin = rev - cost, RatioPct = Pct(cost, rev), CostPerMeal = Per(cost, meals),
                    PrevCostPerMeal = Per(prev.Sum(a => a.Cost), prev.Sum(a => a.Meals)),
                    FaultCost = aggs.Where(a => a.Fault).Sum(a => a.Cost), FaultMeals = aggs.Where(a => a.Fault).Sum(a => a.Meals),
                    ProvisionalOrders = aggs.Count(a => a.Provisional), ProvisionalMealsPct = Pct(aggs.Where(a => a.Provisional).Sum(a => a.Meals), aggs.Sum(a => a.Meals)),
                };
                model.ListDay = [.. aggs.GroupBy(a => a.Date).OrderBy(g => g.Key).Select(g =>
                {
                    var s = g.Where(a => !a.Fault).ToList();
                    return new MealMarginDayModel { Date = g.Key, OrderCount = g.Count(), Meals = s.Sum(a => a.Meals), Revenue = s.Sum(a => a.Revenue), Cost = s.Sum(a => a.Cost),
                        RatioPct = Pct(s.Sum(a => a.Cost), s.Sum(a => a.Revenue)), ProvisionalOrders = g.Count(a => a.Provisional) };
                })];
                if (group == "dish")
                {
                    model.ListDish = await DishesAsync(db, from, to, aggs);
                }
                else if (group != "day")
                {
                    var cusIds = aggs.Select(a => a.CustomerId).Distinct().ToList();
                    var conIds = aggs.Where(a => a.ContractId != null).Select(a => a.ContractId!.Value).Distinct().ToList();
                    var cus = await db.Customer.AsNoTracking().Where(c => cusIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.DisplayName ?? c.Name ?? c.Code);
                    var con = await db.Contract.AsNoTracking().Where(c => conIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.ContractNumber ?? c.ContractNo);
                    model.ListRow = [.. aggs.GroupBy(a => (a.CustomerId, a.ContractId)).Select(g =>
                    {
                        var s = g.Where(a => !a.Fault).ToList();
                        decimal m2 = s.Sum(a => a.Meals), r2 = s.Sum(a => a.Revenue), c2 = s.Sum(a => a.Cost);
                        return new MealMarginRowModel
                        {
                            CustomerId = g.Key.CustomerId, ContractId = g.Key.ContractId, Name = cus.GetValueOrDefault(g.Key.CustomerId),
                            Sub = g.Key.ContractId is { } k ? "HĐ " + con.GetValueOrDefault(k) : "Không gắn hợp đồng", OrderCount = g.Count(), Meals = m2, Revenue = r2, Cost = c2, Margin = r2 - c2,
                            PricePerMeal = Per(r2, m2), CostPerMeal = Per(c2, m2), RatioPct = Pct(c2, r2), FaultCost = g.Where(a => a.Fault).Sum(a => a.Cost), ProvisionalOrders = g.Count(a => a.Provisional),
                        };
                    }).OrderByDescending(r => r.RatioPct ?? -1m).ThenBy(r => r.Name)];
                }
                return Result<MealMarginModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetMargin: ", ex.ToString()));
                throw;
            }
        }

        private async Task<List<MealMarginDishModel>> DishesAsync(DatabaseContext db, DateTime dFrom, DateTime dTo, List<OrderAgg> aggs)
        {
            var orderIds = aggs.Select(a => a.OrderId).ToList();
            var portions = await db.MealCost.AsNoTracking().Where(m => !m.IsDeleted && orderIds.Contains(m.OrderId))
                .GroupBy(m => m.DishId).Select(g => new { g.Key, Q = g.Sum(m => m.MealQty ?? 0m) }).ToListAsync();
            DateTime end = dTo.AddDays(1);
            var prod = await (from o in db.ProductionOrder.AsNoTracking()
                              join p in db.ProductionPlan.AsNoTracking() on o.ProductionPlanId equals p.Id
                              where !o.IsDeleted && !p.IsDeleted && p.ProductionDate >= dFrom && p.ProductionDate < end && o.Status != ProductionOrderStatus.Cancelled
                              select new { o.Id, o.DishId, o.ActualQty, PlanId = p.Id, p.ProductionDate }).ToListAsync();
            var prodIds = prod.Select(o => o.Id).ToList();
            var costs = (await db.ActualCost.AsNoTracking().Where(c => !c.IsDeleted && c.ProductionOrderId != null && prodIds.Contains(c.ProductionOrderId.Value))
                .GroupBy(c => c.ProductionOrderId!.Value).Select(g => new { g.Key, T = g.Sum(c => c.TotalCost ?? 0m) }).ToListAsync()).ToDictionary(x => x.Key, x => x.T);
            var variances = await (from v in db.MaterialConsumptionVariance.AsNoTracking()
                                   join i in db.Ingredient.AsNoTracking() on v.IngredientId equals i.Id
                                   where !v.IsDeleted && prodIds.Contains(v.ProductionOrderId) && v.VariancePercent != null
                                   select new { v.ProductionOrderId, Name = i.Name ?? i.Code, v.VariancePercent, v.VarianceCost }).ToListAsync();
            var rejected = await db.ProductionBatch.AsNoTracking().Where(b => !b.IsDeleted && prodIds.Contains(b.ProductionOrderId) && b.Status == ProductionBatchStatus.Failed)
                .Select(b => b.ProductionOrderId).ToListAsync();
            var dishIds = portions.Select(p => p.Key).Union(prod.Select(p => p.DishId)).Distinct().ToList();
            var names = await db.Dish.AsNoTracking().Where(d => dishIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name ?? d.Code);
            var list = new List<MealMarginDishModel>();
            foreach (Guid dishId in dishIds)
            {
                var mine = prod.Where(o => o.DishId == dishId && costs.ContainsKey(o.Id)).ToList();
                decimal qty = mine.Sum(o => o.ActualQty ?? 0m), total = mine.Sum(o => costs[o.Id]);
                decimal? actual = qty > 0 ? Math.Round(total / qty, 0) : null;
                decimal? standard = await CostingRules.StandardPerPortionAsync(db, dishId);
                decimal portion = portions.FirstOrDefault(p => p.Key == dishId)?.Q ?? prod.Where(o => o.DishId == dishId).Sum(o => o.ActualQty ?? 0m);
                var mineIds = mine.Select(o => o.Id).ToHashSet();
                var over = variances.Where(v => mineIds.Contains(v.ProductionOrderId) && Math.Abs(v.VariancePercent!.Value) > _rules.ConsumptionVariancePctWarning)
                    .GroupBy(v => v.Name).OrderByDescending(g => g.Sum(v => Math.Abs(v.VarianceCost ?? 0m))).FirstOrDefault();
                int rej = rejected.Count(id => prod.Any(o => o.Id == id && o.DishId == dishId));
                var causes = new List<string>();
                if (over != null)
                {
                    causes.Add($"{over.Key} vượt định mức {ProductionRules.Num(Math.Round(over.Average(v => v.VariancePercent!.Value), 1))}%");
                }
                if (rej > 0)
                {
                    causes.Add($"{rej} mẻ QC loại");
                }
                list.Add(new MealMarginDishModel
                {
                    DishId = dishId, DishName = names.GetValueOrDefault(dishId), Portions = portion, StandardPerPortion = standard is { } st ? Math.Round(st, 0) : null, ActualPerPortion = actual,
                    VariancePct = standard is > 0 && actual != null ? Math.Round((actual.Value - standard.Value) / standard.Value * 100m, 1) : null,
                    VarianceAmount = standard != null && actual != null ? Math.Round((actual.Value - standard.Value) * qty, 0) : null,
                    MainCause = causes.Count == 0 ? null : string.Join(" · ", causes),
                    LastPlanId = prod.Where(o => o.DishId == dishId).OrderByDescending(o => o.ProductionDate).Select(o => (Guid?)o.PlanId).FirstOrDefault(),
                });
            }
            return [.. list.OrderByDescending(d => Math.Abs(d.VariancePct ?? 0m)).ThenBy(d => d.DishName)];
        }

        /// <summary>Tính lại giá vốn suất các đơn đã đối soát trong khoảng còn tạm tính theo giá chuẩn (MealCost/Calculate cập nhật tại chỗ – idempotent).</summary>
        public async Task<int> RecalcProvisionalAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
        {
            var db = _unitOfWork.DbContext;
            var aggs = await AggregateAsync(db, from.Date, to.Date, null, null);
            var ids = aggs.Where(a => a.Provisional).Select(a => a.OrderId).ToList();
            var reconciled = await db.Order.AsNoTracking().Where(o => ids.Contains(o.Id) && o.Status == OrderStatus.Reconciled).Select(o => o.Id).ToListAsync(cancellationToken);
            int n = 0;
            foreach (Guid id in reconciled)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var r = await _mealCostService.Calculate(new MealCostCalculateRequest { OrderId = id });
                if (r.ResponseCode == 200)
                {
                    n++;
                }
                db.ChangeTracker.Clear();
            }
            return n;
        }
    }
}
