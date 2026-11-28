using System.Globalization;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement
{
    // ======================================================================= mô hình

    /// <summary>RPT-1 (D-140): 1 thẻ chỉ số – giá trị tháng, tháng trước, cùng kỳ năm trước; Source = EST (Tạm tính Delicare) / AMIS (Chính thức); null = chưa có số.</summary>
    public sealed class PlCardModel
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        /// <summary>VND · PCT · MEAL.</summary>
        public string Unit { get; set; } = "VND";
        public string Source { get; set; } = "EST";
        public decimal? Value { get; set; }
        public decimal? Prev { get; set; }
        public decimal? YearAgo { get; set; }
        /// <summary>true = số tăng là xấu (chi phí, %NVL).</summary>
        public bool LowerIsBetter { get; set; }
        public string? Note { get; set; }
    }

    public sealed class PlRevenueRowModel
    {
        public Guid CustomerId { get; set; }
        public string? CustomerName { get; set; }
        /// <summary>Doanh thu theo bữa (SANG / TRUA / TOI / DEM / KHAC).</summary>
        public Dictionary<string, decimal> ByMeal { get; set; } = [];
        public decimal Meals { get; set; }
        public decimal Total { get; set; }
        /// <summary>Bảng kê tháng chưa được khách xác nhận (ACC-3) – số lấy từ đơn × đơn giá hợp đồng.</summary>
        public bool Unconfirmed { get; set; }
    }

    public sealed class PlGroupModel
    {
        public string Group { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public decimal Prev { get; set; }
        /// <summary>% lũy kế Pareto (tháng đang xem).</summary>
        public decimal CumPct { get; set; }
    }

    public sealed class PlGroupMonthModel
    {
        public string Group { get; set; } = string.Empty;
        /// <summary>12 giá trị theo <see cref="DashboardPlModel.Months"/>.</summary>
        public List<decimal> Values { get; set; } = [];
        public decimal Total { get; set; }
    }

    public sealed class DashboardPlModel
    {
        public string Month { get; set; } = string.Empty;
        public string PrevMonth { get; set; } = string.Empty;
        public string YearAgoMonth { get; set; } = string.Empty;
        /// <summary>true = đã có số AMIS (Chính thức) cho tháng; false = chỉ Tạm tính.</summary>
        public bool HasAmis { get; set; }
        public bool PeriodLocked { get; set; }
        public List<PlCardModel> ListCard { get; set; } = [];
        public List<string> Meals { get; set; } = [];
        public List<PlRevenueRowModel> ListRevenue { get; set; } = [];
        public Dictionary<string, decimal> RevenueByMeal { get; set; } = [];
        public List<PlGroupModel> ListGroup { get; set; } = [];
        /// <summary>12 tháng kết thúc tại tháng đang xem (yyyy-MM).</summary>
        public List<string> Months { get; set; } = [];
        public List<PlGroupMonthModel> ListGroupMonth { get; set; } = [];
        public string? CustomerGroupNote { get; set; }
    }

    /// <summary>
    /// RPT-1.1 (D-140 a, b, c, f, g): tab "Kết quả kinh doanh" của Bảng điều hành – phần Delicare tự tính (lớp <b>Tạm tính</b>):
    /// DT / NVL / %NVL / lãi gộp theo NVL lấy từ <see cref="IMealMarginService"/> (cùng số tab Giá vốn – không tính lần hai); bao bì = phiếu xuất bao bì ACC-6
    /// (MaterialIssue ghi thẻ <c>[XUAT_BB:</c>); NVL theo nhóm nguyên liệu = giao dịch ISSUE × giá lô (không gồm bao bì); DT theo khách × bữa = MealCost × OrderLine.MealType,
    /// cờ "chưa xác nhận" theo bảng kê tháng (ACC-3). Thẻ lớp <b>Chính thức</b> đọc KpiMonthlySnapshot mã <c>PL_AMIS_*</c> (RPT-1.2 nạp) – chưa có → null ("Chờ số AMIS").
    /// Không bảng / cột mới. Khách không có cách gộp nhóm (Customer không có cột cha) → hiện theo khách (GAP-RPT-01 PROPOSED).
    /// </summary>
    public interface IDashboardPlService
    {
        Task<IResult<DashboardPlModel>> GetPl(string? month, Guid? branchId);
    }

    public sealed class DashboardPlService(IUnitOfWork unitOfWork, IClock clock, IMealMarginService margin, IBillingStatementService billing, ICommonService common) : IDashboardPlService
    {
        public const string AmisPrefix = "PL_AMIS_";
        public static readonly string[] MealOrder = ["SANG", "TRUA", "TOI", "DEM", "KHAC"];
        /// <summary>Thẻ chính thức (AMIS) – mã chỉ tiêu, tên theo D-140 f.</summary>
        public static readonly (string Code, string Name, bool Lower)[] AmisCards =
        [
            ("PL_AMIS_GROSS_PROFIT", "Lợi nhuận gộp", false),
            ("PL_AMIS_SELLING_ADMIN", "Chi phí bán hàng & quản lý", true),
            ("PL_AMIS_PROFIT_BEFORE_TAX", "Lợi nhuận trước thuế", false),
        ];

        private static string Ym(DateTime d) => d.ToString("yyyy-MM", CultureInfo.InvariantCulture);

        public async Task<IResult<DashboardPlModel>> GetPl(string? month, Guid? branchId)
        {
            DateTime m0 = DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var mm) ? mm : new DateTime(clock.Today.Year, clock.Today.Month, 1);
            if (branchId == Guid.Empty)
            {
                branchId = null;
            }
            List<Guid>? units = null;
            if (branchId is { } bid)
            {
                units = await common.GetUnitTree(bid);
                if (units == null)
                {
                    return Result<DashboardPlModel>.Fail(ConstantHelper.Message.AlertBranchNotFound);
                }
            }
            DateTime prev = m0.AddMonths(-1), yearAgo = m0.AddMonths(-12);
            async Task<MealMarginSummaryModel?> Margin(DateTime m) => (await margin.GetMargin(new MealMarginRequest { Month = Ym(m), GroupBy = "customer", BranchId = branchId })).Data?.Summary;
            var cur = await Margin(m0);
            var pre = await Margin(prev);
            var ago = await Margin(yearAgo);
            var db = unitOfWork.DbContext;

            // Bao bì + NVL theo nhóm: 13 tháng (12 tháng xem + cùng kỳ) một lần.
            DateTime from = m0.AddMonths(-12), to = m0.AddMonths(1);
            var issues = await IssueRowsAsync(from, to, units);
            decimal Pack(DateTime m) => issues.Where(i => i.Packaging && i.Month == m).Sum(i => i.Cost);
            decimal? Pct(decimal? a, decimal? b) => a is { } x && b is { } y && y != 0 ? Math.Round(x / y * 100m, 1) : null;
            PlCardModel Est(string code, string name, Func<MealMarginSummaryModel?, decimal?> f, string unit = "VND", bool lower = false, string? note = null) => new()
            {
                Code = code, Name = name, Unit = unit, Source = "EST", LowerIsBetter = lower, Note = note, Value = f(cur), Prev = f(pre), YearAgo = f(ago),
            };
            var model = new DashboardPlModel { Month = Ym(m0), PrevMonth = Ym(prev), YearAgoMonth = Ym(yearAgo) };
            model.ListCard.Add(Est("PL_EST_REVENUE", "Doanh thu", s => s?.Revenue));
            model.ListCard.Add(Est("PL_EST_MEALS", "Số suất", s => s?.Meals, "MEAL"));
            model.ListCard.Add(Est("PL_EST_FOOD_COST", "Nguyên vật liệu", s => s?.Cost, lower: true, note: "giá vốn suất – như tab Giá vốn & lãi gộp"));
            model.ListCard.Add(new PlCardModel
            {
                Code = "PL_EST_PACKAGING", Name = "Bao bì", Source = "EST", LowerIsBetter = true, Value = Pack(m0), Prev = Pack(prev), YearAgo = Pack(yearAgo), Note = "phiếu xuất bao bì theo định mức (ACC-6)",
            });
            model.ListCard.Add(Est("PL_EST_FOOD_PCT", "% NVL / doanh thu", s => s?.RatioPct, "PCT", lower: true));
            model.ListCard.Add(Est("PL_EST_FOOD_MARGIN", "Lãi gộp theo NVL", s => s?.Margin, note: "doanh thu − nguyên vật liệu"));

            // Thẻ chính thức (AMIS) – RPT-1.2 nạp KpiMonthlySnapshot PL_AMIS_*; chưa có → null.
            var codes = AmisCards.Select(a => a.Code).ToList();
            var amis = await db.KpiMonthlySnapshot.AsNoTracking()
                .Where(k => k.MetricCode != null && k.MetricCode.StartsWith(AmisPrefix) && (k.SnapshotMonth == m0 || k.SnapshotMonth == prev || k.SnapshotMonth == yearAgo) && k.BranchId == null)
                .Select(k => new { k.MetricCode, k.SnapshotMonth, k.MetricValue, k.MetricVersion }).ToListAsync();
            decimal? A(string code, DateTime m) => amis.Where(k => k.MetricCode == code && k.SnapshotMonth == m).OrderByDescending(k => k.MetricVersion).Select(k => k.MetricValue).FirstOrDefault();
            model.HasAmis = amis.Any(k => k.SnapshotMonth == m0);
            foreach (var (code, name, lower) in AmisCards)
            {
                model.ListCard.Add(new PlCardModel { Code = code, Name = name, Source = "AMIS", LowerIsBetter = lower, Value = A(code, m0), Prev = A(code, prev), YearAgo = A(code, yearAgo), Note = branchId != null ? "số AMIS cả công ty" : null });
            }
            model.PeriodLocked = await AccountingPeriodRules.IsLockedAsync(db, m0);

            // DT theo khách × bữa (MealCost × OrderLine.MealType) + cờ chưa xác nhận theo bảng kê tháng.
            DateTime end = m0.AddMonths(1);
            IQueryable<Guid>? branchCustomers = units == null ? null : db.Customer.AsNoTracking().Where(c => c.BranchId != null && units.Contains(c.BranchId.Value)).Select(c => c.Id);
            var rev = await (from c in db.MealCost.AsNoTracking()
                             join o in db.Order.AsNoTracking() on c.OrderId equals o.Id
                             join l in db.OrderLine.AsNoTracking() on c.OrderLineId equals l.Id into ll
                             from l in ll.DefaultIfEmpty()
                             where !c.IsDeleted && !o.IsDeleted && o.MealDate >= m0 && o.MealDate < end && (branchCustomers == null || branchCustomers.Contains(o.CustomerId))
                             select new { o.CustomerId, MealType = l != null ? l.MealType : null, c.Revenue, Meals = c.AcceptedMealQuantity != 0 ? c.AcceptedMealQuantity : (c.MealQty ?? 0m) }).ToListAsync();
            string MealOf(string? t) => (t ?? string.Empty).Trim().ToUpperInvariant() is var u && MealOrder.Contains(u) && u != "KHAC" ? u : "KHAC";
            var custIds = rev.Select(r => r.CustomerId).Distinct().ToList();
            var names = await db.Customer.AsNoTracking().Where(c => custIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.DisplayName ?? c.Name ?? c.Code);
            var bill = (await billing.GetMonthOverview(new BillingMonthRequest { Month = m0, BranchId = branchId })).Data;
            var confirmed = bill?.ListCustomer.Where(c => c.ConfirmState is BillingConfirmation.StateConfirmed or BillingConfirmation.StateInvoiced).Select(c => c.CustomerId).ToHashSet() ?? [];
            model.ListRevenue = [.. rev.GroupBy(r => r.CustomerId).Select(g => new PlRevenueRowModel
            {
                CustomerId = g.Key, CustomerName = names.GetValueOrDefault(g.Key), ByMeal = g.GroupBy(x => MealOf(x.MealType)).ToDictionary(x => x.Key, x => x.Sum(y => y.Revenue ?? 0m)),
                Meals = g.Sum(x => x.Meals), Total = g.Sum(x => x.Revenue ?? 0m), Unconfirmed = !confirmed.Contains(g.Key),
            }).Where(r => r.Total != 0 || r.Meals != 0).OrderByDescending(r => r.Total)];
            model.RevenueByMeal = MealOrder.ToDictionary(k => k, k => model.ListRevenue.Sum(r => r.ByMeal.GetValueOrDefault(k)));
            model.Meals = [.. MealOrder.Where(k => model.RevenueByMeal[k] != 0)];
            model.CustomerGroupNote = "Hiện theo khách – chưa có cách gộp nhóm khách (GAP-RPT-01 PROPOSED: Customer.ParentCustomerId, chờ anh duyệt).";

            // NVL theo nhóm: tháng đang xem + tháng trước (Pareto), 12 tháng.
            var food = issues.Where(i => !i.Packaging).ToList();
            var groups = food.Where(i => i.Month == m0 || i.Month == prev).GroupBy(i => i.Group).Select(g => new PlGroupModel
            {
                Group = g.Key, Value = g.Where(x => x.Month == m0).Sum(x => x.Cost), Prev = g.Where(x => x.Month == prev).Sum(x => x.Cost),
            }).OrderByDescending(g => g.Value).ThenByDescending(g => g.Prev).ToList();
            decimal total = groups.Sum(g => g.Value), cum = 0;
            foreach (var g in groups)
            {
                cum += g.Value;
                g.CumPct = total == 0 ? 0 : Math.Round(cum / total * 100m, 1);
            }
            model.ListGroup = groups;
            var months = Enumerable.Range(0, 12).Select(i => m0.AddMonths(i - 11)).ToList();
            model.Months = [.. months.Select(Ym)];
            model.ListGroupMonth = [.. food.Where(i => i.Month > yearAgo).GroupBy(i => i.Group).Select(g => new PlGroupMonthModel
            {
                Group = g.Key, Values = [.. months.Select(m => g.Where(x => x.Month == m).Sum(x => x.Cost))], Total = g.Sum(x => x.Cost),
            }).OrderByDescending(g => g.Values[^1]).ThenByDescending(g => g.Total)];
            return Result<DashboardPlModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
        }

        private sealed record IssueRow(DateTime Month, string Group, bool Packaging, decimal Cost);

        /// <summary>Giao dịch xuất kho (ISSUE) theo tháng × nhóm nguyên liệu × bao bì / NVL; giá trị = số xuất × giá lô (kho thuộc chi nhánh nếu lọc).</summary>
        private async Task<List<IssueRow>> IssueRowsAsync(DateTime from, DateTime to, List<Guid>? units)
        {
            var db = unitOfWork.DbContext;
            var q = db.InventoryTransaction.AsNoTracking().Where(t => !t.IsDeleted && t.TransactionType == "ISSUE" && t.TransactionDate >= from && t.TransactionDate < to);
            if (units != null)
            {
                var wh = db.Warehouse.AsNoTracking().Where(w => w.OrganizationUnitId == null || units.Contains(w.OrganizationUnitId.Value)).Select(w => w.Id);
                q = q.Where(t => wh.Contains(t.WarehouseId));
            }
            var rows = await (from t in q
                              join i in db.Ingredient.AsNoTracking() on t.IngredientId equals i.Id
                              join mi in db.MaterialIssue.AsNoTracking() on t.ReferenceId equals mi.Id into mis
                              from mi in mis.DefaultIfEmpty()
                              select new { t.TransactionDate, Qty = t.QuantityOut != 0 ? t.QuantityOut : Math.Abs(t.Quantity), t.UnitCost, Cat = i.IngredientCategoryId ?? i.CategoryId, Note = mi != null ? mi.Note : null }).ToListAsync();
            var cats = await db.IngredientCategory.AsNoTracking().Select(c => new { c.Id, c.Name, c.Code }).ToDictionaryAsync(c => c.Id, c => c.Name ?? c.Code ?? "Khác");
            return [.. rows.Select(r => new IssueRow(new DateTime(r.TransactionDate.Year, r.TransactionDate.Month, 1),
                r.Cat is { } c && cats.TryGetValue(c, out var n) ? n : "Chưa phân nhóm",
                r.Note != null && r.Note.Contains(PackagingNormService.TagPrefix, StringComparison.Ordinal), Math.Round(r.Qty * (r.UnitCost ?? 0m), 0)))];
        }
    }
}
