#nullable enable
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Serilog;
using System.Net;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// Dashboard (controller không gắn bảng). BE3.5 (Catalog 231, 232 – spec 12.1, 12.2, D-57, D-58): dashboard điều hành đọc thẳng bảng giao dịch
    /// (cache 60 giây theo (ngày, ca, chi nhánh) – cảnh báo theo người lấy riêng từ Common/GetAlerts) và hành trình 1 đơn hàng qua các chặng.
    /// Chỉ đọc; không trả tên bảng / cột. Quy tắc chưa xác nhận: PROPOSED – BA/PO CONFIRM (D-57 ad–ai).
    /// </summary>
    public class DashboardService(IUnitOfWork unitOfWork, IClock clock, IOptions<BusinessRulesOptions> rules, IMemoryCache cache,
        ICommonService commonService, IPermissionService permissionService, ICurrentActor currentActor) : IDashboardService
    {
        public const string ScreenDashboard = "dash-exec";
        public const string ScreenFlow = "flow-e2e";
        public const string ScreenOrder = "crm-order";

        /// <summary>4 chỉ tiêu KPI trên dashboard (spec 12.1).</summary>
        public static readonly string[] SnapshotMetrics = ["PROD_ACHIEVEMENT", "DEL_ONTIME", "INV_ACCURACY", "FIN_FOOD_COST"];

        /// <summary>Mã chặng hành trình đơn (spec 12.2).</summary>
        public static class Stage
        {
            public const string Order = "ORDER";
            public const string ProductionPlan = "PRODUCTION_PLAN";
            public const string Production = "PRODUCTION";
            public const string Packing = "PACKING";
            public const string Delivery = "DELIVERY";
            public const string Pod = "POD";
            public const string Reconciliation = "RECONCILIATION";
            public const string Complaint = "COMPLAINT";
        }

        /// <summary>Trạng thái chung của 1 chặng (chứng từ giữ trạng thái thật của nó) – PROPOSED (D-57 ah).</summary>
        public static class StageStatus
        {
            public const string NotStarted = "NotStarted";
            public const string InProgress = "InProgress";
            public const string Done = "Done";
            public const string Issue = "Issue";
            public const string Cancelled = "Cancelled";
            public const string None = "None";

            public static string Label(string status) => status switch
            {
                NotStarted => "Chưa bắt đầu",
                InProgress => "Đang thực hiện",
                Done => "Hoàn tất",
                Issue => "Có sự cố",
                Cancelled => "Đã hủy",
                None => "Không có",
                _ => status,
            };
        }

        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly DashboardRulesOptions _rules = rules.Value.Dashboard;
        private readonly IMemoryCache _cache = cache;
        private readonly ICommonService _commonService = commonService;
        private readonly IPermissionService _permissionService = permissionService;
        private readonly ICurrentActor _currentActor = currentActor;

        public static string CacheKey(DateTime date, Guid? shiftId, Guid? branchId)
            => $"dash-exec:{date:yyyyMMdd}:{shiftId?.ToString("N") ?? "all"}:{branchId?.ToString("N") ?? "all"}";

        // ============================================================ Catalog 231 – dashboard điều hành

        /// <summary>
        /// Catalog 231 (spec 12.1): mọi khối của dashboard điều hành cho ngày <paramref name="date"/> (trống = hôm nay), lọc ca / chi nhánh (gồm đơn vị con).
        /// Phần số liệu chung cache <c>BusinessRules:Dashboard:CacheSeconds</c> theo (ngày, ca, chi nhánh); <c>alerts</c> = Common/GetAlerts của người đang đăng nhập
        /// (cache riêng theo người) → người dùng không thấy cảnh báo của nhau. Ca / chi nhánh không có → 400.
        /// </summary>
        public async Task<IResult<DashboardExecutiveModel>> GetExecutive(DateTime? date, Guid? shiftId, Guid? branchId)
        {
            try
            {
                DateTime day = (date ?? _clock.Today).Date;
                if (day.Year < 2000 || day.Year > 2100)
                {
                    return Result<DashboardExecutiveModel>.Fail(ConstantHelper.Message.DashboardDateInvalid);
                }
                if (shiftId == Guid.Empty)
                {
                    shiftId = null;
                }
                if (branchId == Guid.Empty)
                {
                    branchId = null;
                }
                string key = CacheKey(day, shiftId, branchId);
                if (!(_rules.CacheSeconds > 0 && _cache.TryGetValue(key, out DashboardExecutiveModel? shared) && shared != null))
                {
                    var built = await BuildAsync(day, shiftId, branchId);
                    if (built.Error != null)
                    {
                        return Result<DashboardExecutiveModel>.Fail(built.Error);
                    }
                    shared = built.Model!;
                    if (_rules.CacheSeconds > 0)
                    {
                        _cache.Set(key, shared, TimeSpan.FromSeconds(_rules.CacheSeconds));
                    }
                }
                var alerts = await _commonService.GetAlerts(branchId);
                return Result<DashboardExecutiveModel>.Success(WithAlerts(shared, alerts.ListData ?? []), ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetExecutive: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Bản sao nông của phần chung (đối tượng trong cache không bị sửa) + cảnh báo của người đang xem.</summary>
        private static DashboardExecutiveModel WithAlerts(DashboardExecutiveModel shared, List<AlertModel> alerts) => new()
        {
            Date = shared.Date,
            ShiftId = shared.ShiftId,
            BranchId = shared.BranchId,
            GeneratedAt = shared.GeneratedAt,
            Kpi = shared.Kpi,
            Chart = shared.Chart,
            ShiftProgress = shared.ShiftProgress,
            KpiSnapshot = shared.KpiSnapshot,
            Orders = shared.Orders,
            OrdersTotal = shared.OrdersTotal,
            Alerts = alerts,
        };

        private sealed record BuildResult(DashboardExecutiveModel? Model, string? Error);

        private async Task<BuildResult> BuildAsync(DateTime day, Guid? shiftId, Guid? branchId)
        {
            var db = _unitOfWork.DbContext;
            DateTime next = day.AddDays(1);
            if (shiftId != null && !await db.Shift.AsNoTracking().AnyAsync(s => s.Id == shiftId && !s.IsDeleted))
            {
                return new BuildResult(null, ConstantHelper.Message.DashboardShiftNotFound);
            }
            List<Guid>? units = null;
            if (branchId is { } bid)
            {
                units = await _commonService.GetUnitTree(bid);
                if (units == null)
                {
                    return new BuildResult(null, ConstantHelper.Message.AlertBranchNotFound);
                }
            }
            var shared = await _commonService.GetSharedCounts(branchId) ?? new SharedAlertCountsModel();
            var model = new DashboardExecutiveModel { Date = day, ShiftId = shiftId, BranchId = branchId, GeneratedAt = _clock.Now };

            // Khách thuộc chi nhánh (gồm đơn vị con) – như Order/GetBoard lọc Customer.BranchId (PROPOSED – D-57 ae).
            IQueryable<Guid>? customerIds = units == null ? null
                : db.Customer.AsNoTracking().Where(c => c.BranchId != null && units.Contains(c.BranchId.Value)).Select(c => c.Id);

            // ---- Đơn hàng của ngày ăn (+ ca: đơn có dòng thuộc ca).
            var orderQuery = db.Order.AsNoTracking().Where(o => !o.IsDeleted && o.MealDate >= day && o.MealDate < next);
            if (customerIds != null)
            {
                orderQuery = orderQuery.Where(o => customerIds.Contains(o.CustomerId));
            }
            if (shiftId != null)
            {
                orderQuery = orderQuery.Where(o => db.OrderLine.Any(l => !l.IsDeleted && l.OrderId == o.Id && l.ShiftId == shiftId));
            }
            var orders = await orderQuery.ToListAsync();
            model.Kpi.Orders = new DashboardOrderCountModel
            {
                Total = orders.Count,
                ByStatus = orders.GroupBy(o => o.Status ?? string.Empty).ToDictionary(g => g.Key, g => g.Count()),
                ListStatus = [.. orders.GroupBy(o => o.Status ?? string.Empty)
                    .Select(g => new DashboardStatusCountModel { Status = g.Key, StatusName = OrderStatus.Label(g.Key), Count = g.Count() })
                    .OrderBy(s => OrderStatusRank(s.Status))],
            };

            // ---- Biểu đồ 7 ngày: kế hoạch = Σ dòng đơn (≠ Nháp / Hủy) theo ngày ăn; nhận = Σ POD theo ngày giao.
            int days = Math.Clamp(_rules.ChartDays, 1, 31);
            DateTime start = day.AddDays(-(days - 1));
            var lineQuery = from l in db.OrderLine.AsNoTracking()
                            join o in db.Order.AsNoTracking() on l.OrderId equals o.Id
                            where !l.IsDeleted && !o.IsDeleted && o.MealDate >= start && o.MealDate < next
                                && o.Status != OrderStatus.Draft && o.Status != OrderStatus.Cancelled
                            select new { o.CustomerId, o.MealDate, l.ShiftId, l.Quantity };
            if (customerIds != null)
            {
                lineQuery = lineQuery.Where(x => customerIds.Contains(x.CustomerId));
            }
            if (shiftId != null)
            {
                lineQuery = lineQuery.Where(x => x.ShiftId == shiftId);
            }
            var planned = (await lineQuery.Select(x => new { x.MealDate, x.Quantity }).ToListAsync())
                .GroupBy(x => x.MealDate.Date).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
            var podQuery = from p in db.ProofOfDelivery.AsNoTracking()
                           join d in db.DeliveryOrder.AsNoTracking() on p.DeliveryOrderId equals d.Id
                           where !p.IsDeleted && !d.IsDeleted && d.DeliveryDate >= start && d.DeliveryDate < next
                           select new { d.CustomerId, d.DeliveryDate, Accepted = p.AcceptedQuantity ?? p.AcceptedQty ?? 0m };
            if (customerIds != null)
            {
                podQuery = podQuery.Where(x => customerIds.Contains(x.CustomerId));
            }
            var accepted = (await podQuery.Select(x => new { x.DeliveryDate, x.Accepted }).ToListAsync())
                .GroupBy(x => x.DeliveryDate.Date).ToDictionary(g => g.Key, g => g.Sum(x => x.Accepted));
            model.Chart.Meals7d = [.. Enumerable.Range(0, days).Select(i => start.AddDays(i)).Select(d => new DashboardMealPointModel
            {
                Date = d,
                Planned = planned.GetValueOrDefault(d),
                Accepted = accepted.GetValueOrDefault(d),
            })];
            model.Kpi.PlannedMeals = planned.GetValueOrDefault(day);

            // ---- Sản xuất: mẻ (≠ Hủy) của lệnh SX thuộc kế hoạch có ngày SX = ngày (như ProductionBatch/GetBoard), lọc ca theo lệnh SX.
            var planQuery = db.ProductionPlan.AsNoTracking().Where(p => !p.IsDeleted && p.ProductionDate >= day && p.ProductionDate < next);
            if (units != null)
            {
                planQuery = planQuery.Where(p => p.OrganizationUnitId == null || units.Contains(p.OrganizationUnitId.Value));
            }
            var planIds = await planQuery.Select(p => p.Id).ToListAsync();
            var productionOrders = await db.ProductionOrder.AsNoTracking()
                .Where(o => !o.IsDeleted && planIds.Contains(o.ProductionPlanId) && (shiftId == null || o.ShiftId == shiftId))
                .Select(o => new { o.Id, o.ShiftId }).ToListAsync();
            var poShift = productionOrders.ToDictionary(o => o.Id, o => o.ShiftId);
            var poIds = productionOrders.Select(o => o.Id).ToList();
            var batches = await db.ProductionBatch.AsNoTracking()
                .Where(b => !b.IsDeleted && poIds.Contains(b.ProductionOrderId) && b.Status != ProductionBatchStatus.Cancelled).ToListAsync();
            model.Kpi.ProductionProgress = Progress(batches);
            var shifts = await db.Shift.AsNoTracking().Where(s => !s.IsDeleted && (s.Status == null || s.Status != "Inactive") && (shiftId == null || s.Id == shiftId))
                .OrderBy(s => s.StartTime).ToListAsync();
            model.ShiftProgress = [.. shifts.Select(s =>
            {
                var p = Progress([.. batches.Where(b => poShift.GetValueOrDefault(b.ProductionOrderId) == s.Id)]);
                return new DashboardShiftProgressModel
                {
                    ShiftId = s.Id, Code = s.Code, Name = s.Name, BatchCount = p.BatchCount, Percent = p.Percent, DonePortions = p.DonePortions, PlannedPortions = p.PlannedPortions,
                };
            })];

            // ---- QC: mẻ trong ngày QC HOLD / REJECT + lô còn tồn đang tạm giữ (thay "dòng QC đầu vào HOLD" – D-57 r).
            model.Kpi.QcDetail = new DashboardQcDetailModel
            {
                BatchHold = batches.Count(b => b.QcStatus == BatchQcStatus.Hold),
                BatchReject = batches.Count(b => b.QcStatus == BatchQcStatus.Reject),
                LotOnHold = shared.LotOnHold,
            };
            model.Kpi.QcHoldReject = model.Kpi.QcDetail.BatchHold + model.Kpi.QcDetail.BatchReject + model.Kpi.QcDetail.LotOnHold;
            model.Kpi.StockAlerts = shared.StockBelowMin;
            model.Kpi.OpenComplaints = shared.OpenComplaints;

            // ---- Giao hàng: đơn giao (≠ Hủy) có ngày giao = ngày; đã giao = trạng thái thực hiện DELIVERED (như DeliveryExecution/GetBoard).
            var doQuery = db.DeliveryOrder.AsNoTracking().Where(d => !d.IsDeleted && d.DeliveryDate >= day && d.DeliveryDate < next && d.Status != DeliveryOrderStatus.Cancelled);
            if (customerIds != null)
            {
                doQuery = doQuery.Where(d => customerIds.Contains(d.CustomerId));
            }
            var dayDeliveries = await doQuery.ToListAsync();
            var dayStatuses = await ExecutionStatusesAsync(dayDeliveries);
            model.Kpi.Delivery = new DashboardDeliveryModel
            {
                Total = dayDeliveries.Count,
                Delivered = dayStatuses.Values.Count(s => s == DeliveryExecutionStatus.Delivered),
                ByStatus = dayStatuses.Values.GroupBy(s => s).ToDictionary(g => g.Key, g => g.Count()),
            };

            // ---- Nhân sự: ca được phân của ngày (lọc ca; chi nhánh theo đơn vị của nhân viên).
            var saQuery = db.ShiftAssignment.AsNoTracking().Where(a => !a.IsDeleted && a.WorkDate >= day && a.WorkDate < next && (shiftId == null || a.ShiftId == shiftId));
            if (units != null)
            {
                saQuery = saQuery.Where(a => db.Employee.Any(e => e.Id == a.EmployeeId && e.OrganizationUnitId != null && units.Contains(e.OrganizationUnitId.Value)));
            }
            var saStatuses = await saQuery.Select(a => a.AssignmentStatus).ToListAsync();
            model.Kpi.Staffing = new DashboardStaffingModel
            {
                Assigned = saStatuses.Count,
                CheckedIn = saStatuses.Count(s => s is ShiftAssignmentStatus.CheckedIn or ShiftAssignmentStatus.Completed),
            };

            // ---- KPI snapshot của ngày (BE3.4 ghi; chưa có → value / target null).
            model.KpiSnapshot = await SnapshotAsync(day, branchId);

            // ---- N đơn đầu (thứ tự như Order/GetBoard: số đơn) + trạng thái giao.
            var top = orders.OrderBy(o => o.MealDate).ThenBy(o => o.OrderNumber ?? o.OrderNo, StringComparer.Ordinal).Take(Math.Max(0, _rules.OrdersTake)).ToList();
            var topIds = top.Select(o => o.Id).ToList();
            var topDeliveries = await db.DeliveryOrder.AsNoTracking()
                .Where(d => !d.IsDeleted && topIds.Contains(d.OrderId) && d.Status != DeliveryOrderStatus.Cancelled).ToListAsync();
            var topStatuses = await ExecutionStatusesAsync(topDeliveries);
            var customerNames = await CustomerNamesAsync(top.Select(o => o.CustomerId));
            model.OrdersTotal = orders.Count;
            model.Orders = [.. top.Select(o => new DashboardOrderModel
            {
                Id = o.Id,
                OrderNumber = o.OrderNumber ?? o.OrderNo,
                CustomerId = o.CustomerId,
                CustomerName = customerNames.GetValueOrDefault(o.CustomerId),
                TotalQuantity = o.TotalQuantity,
                Status = o.Status,
                StatusName = OrderStatus.Label(o.Status),
                Route = "#/" + ScreenOrder,
                ListDelivery = [.. topDeliveries.Where(d => d.OrderId == o.Id).OrderBy(d => d.DeliveryNumber ?? d.DeliveryNo, StringComparer.Ordinal).Select(d => new DashboardDeliveryStatusModel
                {
                    DeliveryOrderId = d.Id,
                    DeliveryNumber = d.DeliveryNumber ?? d.DeliveryNo,
                    Status = topStatuses[d.Id],
                    StatusName = DeliveryExecutionStatus.Label(topStatuses[d.Id]),
                })],
            })];
            return new BuildResult(model, null);
        }

        private static readonly string[] OrderStatusOrder =
            [OrderStatus.Draft, OrderStatus.Submitted, OrderStatus.Confirmed, OrderStatus.Locked, OrderStatus.InProduction, OrderStatus.Delivered, OrderStatus.Reconciled, OrderStatus.Cancelled];

        private static int OrderStatusRank(string status)
        {
            int i = Array.IndexOf(OrderStatusOrder, status);
            return i < 0 ? 99 : i;
        }

        /// <summary>Tiến độ: Σ số đạt (mẻ Đạt / Đã đóng gói: AcceptedQty, không có thì ActualQuantity) ÷ Σ số kế hoạch – cùng công thức ProductionBatch/GetBoard.</summary>
        private static DashboardProductionProgressModel Progress(List<ProductionBatch> batches)
        {
            decimal plannedPortions = batches.Sum(b => b.PlannedQuantity ?? b.PlannedQty ?? 0m);
            decimal done = batches.Where(b => b.Status is ProductionBatchStatus.Passed or ProductionBatchStatus.Packed)
                .Sum(b => b.AcceptedQty ?? b.ActualQuantity ?? b.ActualQty ?? 0m);
            return new DashboardProductionProgressModel
            {
                BatchCount = batches.Count,
                PlannedPortions = plannedPortions,
                DonePortions = done,
                Percent = plannedPortions > 0 ? Math.Round(done / plannedPortions * 100m, 1, MidpointRounding.AwayFromZero) : null,
            };
        }

        /// <summary>Trạng thái thực hiện giao của từng đơn giao (DeliveryRules.ExecutionStatus – cùng quy tắc DeliveryExecution/GetBoard).</summary>
        private async Task<Dictionary<Guid, string>> ExecutionStatusesAsync(List<DeliveryOrder> deliveries)
        {
            var db = _unitOfWork.DbContext;
            var ids = deliveries.Select(d => d.Id).ToList();
            if (ids.Count == 0)
            {
                return [];
            }
            var stops = await db.DeliveryStop.AsNoTracking().Where(s => !s.IsDeleted && s.DeliveryOrderId != null && ids.Contains(s.DeliveryOrderId.Value)).ToListAsync();
            var executions = await db.DeliveryExecution.AsNoTracking().Where(e => !e.IsDeleted && ids.Contains(e.DeliveryOrderId)).ToListAsync();
            return deliveries.ToDictionary(d => d.Id, d => DeliveryRules.ExecutionStatus(d,
                stops.FirstOrDefault(s => s.DeliveryOrderId == d.Id),
                executions.Where(e => e.DeliveryOrderId == d.Id).OrderByDescending(e => e.CreatedDate).FirstOrDefault()));
        }

        private async Task<Dictionary<Guid, string?>> CustomerNamesAsync(IEnumerable<Guid> ids)
        {
            var list = ids.Distinct().ToList();
            return await _unitOfWork.DbContext.Customer.AsNoTracking().Where(c => list.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.DisplayName ?? c.Name);
        }

        /// <summary>
        /// 4 chỉ tiêu từ KpiDailySnapshot của ngày: không chọn chi nhánh → dòng toàn công ty (BranchId trống); chọn → đúng chi nhánh đó. Mỗi chỉ tiêu lấy MetricVersion
        /// cao nhất. Tên / đơn vị từ KpiMetricDefinition (bản hiệu lực tại ngày, VersionNo cao nhất). Chiều chỉ tiêu: thẻ <c>[TARGET &lt;= x]</c> trong mô tả công thức
        /// = càng thấp càng tốt, <c>[TARGET &gt;= x]</c> = càng cao càng tốt; không có thẻ → mã chứa COST / WASTE / REJECT / EXPIRY / LOSS là càng thấp càng tốt (PROPOSED – D-57 ag).
        /// </summary>
        private async Task<List<DashboardKpiSnapshotModel>> SnapshotAsync(DateTime day, Guid? branchId)
        {
            var db = _unitOfWork.DbContext;
            DateTime next = day.AddDays(1);
            var rows = await db.KpiDailySnapshot.AsNoTracking()
                .Where(k => !k.IsDeleted && k.SnapshotDate >= day && k.SnapshotDate < next && k.MetricCode != null && SnapshotMetrics.Contains(k.MetricCode)
                    && (branchId == null ? k.BranchId == null : k.BranchId == branchId))
                .ToListAsync();
            var definitions = (await db.KpiMetricDefinition.AsNoTracking()
                    .Where(d => !d.IsDeleted && d.MetricCode != null && SnapshotMetrics.Contains(d.MetricCode)).ToListAsync())
                .GroupBy(d => d.MetricCode!)
                .ToDictionary(g => g.Key, g => g.Where(d => d.EffectiveFrom <= day && (d.EffectiveTo == null || d.EffectiveTo >= day)).OrderByDescending(d => d.VersionNo).FirstOrDefault()
                    ?? g.OrderByDescending(d => d.VersionNo).First());
            return [.. SnapshotMetrics.Select(code =>
            {
                var row = rows.Where(r => r.MetricCode == code).OrderByDescending(r => r.MetricVersion).ThenByDescending(r => r.CalculatedAt).FirstOrDefault();
                var def = definitions.GetValueOrDefault(code);
                bool lower = LowerIsBetter(code, def?.FormulaDescription);
                decimal? value = row?.MetricValue, target = row?.TargetValue;
                return new DashboardKpiSnapshotModel
                {
                    MetricCode = code,
                    Name = def?.MetricName,
                    UnitCode = def?.UnitCode,
                    Value = value,
                    Target = target,
                    LowerIsBetter = lower,
                    Met = value is { } v && target is { } t ? (lower ? v <= t : v >= t) : null,
                    DataQualityStatus = row?.DataQualityStatus,
                    CalculatedAt = row?.CalculatedAt,
                };
            })];
        }

        private static readonly string[] LowerBetterKeywords = ["COST", "WASTE", "REJECT", "EXPIRY", "LOSS"];

        /// <summary>Chiều chỉ tiêu: true = càng thấp càng tốt (xem <see cref="SnapshotAsync"/>).</summary>
        public static bool LowerIsBetter(string code, string? formulaDescription)
        {
            string text = (formulaDescription ?? string.Empty).Replace(" ", string.Empty);
            if (text.Contains("[TARGET<=", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (text.Contains("[TARGET>=", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return LowerBetterKeywords.Any(k => code.Contains(k, StringComparison.OrdinalIgnoreCase));
        }

        // ============================================================ Catalog 232 – hành trình đơn hàng

        /// <summary>
        /// Catalog 232 (spec 12.2): trạng thái từng chặng của 1 đơn hàng – Đơn hàng → Kế hoạch SX → Lệnh SX / mẻ nấu → Đóng gói → Đơn giao / chuyến → POD → Đối soát → Phản ánh.
        /// Liên kết (viết cứng, PROPOSED – D-57 ah): kế hoạch = KH- có ngày SX = ngày ăn của đơn (đơn vị trống hoặc thuộc chi nhánh của khách – kế hoạch gộp theo ngày);
        /// lệnh SX = lệnh của kế hoạch đó thuộc ca của dòng đơn; mẻ = mẻ của lệnh; đóng gói / đơn giao / đối soát gắn thẳng đơn; POD theo đơn giao; phản ánh gắn đơn hoặc đơn giao.
        /// Quyền: Xem 1 trong các màn <c>#/flow-e2e</c>, <c>#/dash-exec</c>, <c>#/crm-order</c> (không có → 403). Chứng từ của chặng chỉ trả khi được Xem màn của chứng từ.
        /// Chỉ tên màn hình + số chứng từ (D-58).
        /// </summary>
        public async Task<IResult<OrderJourneyModel>> GetOrderJourney(Guid orderId)
        {
            try
            {
                var actor = _currentActor.RequireActor();
                var perms = await _permissionService.GetAsync(actor.UserId);
                bool Can(string screen) => _permissionService.IsAllowed(perms, screen, PermissionAction.View);
                if (!Can(ScreenFlow) && !Can(ScreenDashboard) && !Can(ScreenOrder))
                {
                    return new Result<OrderJourneyModel> { ResponseCode = CommonAccess.Forbidden, ResponseMessage = ConstantHelper.Message.JourneyNoRight };
                }
                if (orderId == Guid.Empty)
                {
                    return Result<OrderJourneyModel>.Fail(ConstantHelper.Message.JourneyOrderRequired);
                }
                var db = _unitOfWork.DbContext;
                var order = await db.Order.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted);
                if (order == null)
                {
                    return Result<OrderJourneyModel>.Fail(ConstantHelper.Message.JourneyOrderNotFound);
                }
                int take = Math.Max(1, _rules.JourneyMaxDocuments);
                var customer = await db.Customer.AsNoTracking().FirstOrDefaultAsync(c => c.Id == order.CustomerId);
                var contract = order.ContractId is { } cid ? await db.Contract.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cid) : null;
                bool cancelled = order.Status == OrderStatus.Cancelled;
                string orderNumber = order.OrderNumber ?? order.OrderNo ?? order.Id.ToString("N")[..8];
                var model = new OrderJourneyModel
                {
                    OrderId = order.Id,
                    OrderNumber = order.OrderNumber ?? order.OrderNo,
                    CustomerName = customer?.DisplayName ?? customer?.Name,
                    ContractNumber = contract?.ContractNumber ?? contract?.ContractNo,
                    MealDate = order.MealDate,
                    Status = order.Status,
                    StatusName = OrderStatus.Label(order.Status),
                };
                void Add(string stage, string title, string screen, string status, DateTime? at, List<JourneyDocumentModel> docs, string? note = null)
                {
                    if (cancelled && status == StageStatus.NotStarted)
                    {
                        status = StageStatus.Cancelled;
                    }
                    var visible = docs.Where(d => Can(d.Route[2..])).ToList();
                    model.Stages.Add(new JourneyStageModel
                    {
                        Stage = stage,
                        Title = title,
                        Route = "#/" + screen,
                        Status = status,
                        StatusName = StageStatus.Label(status),
                        At = at,
                        Done = status is StageStatus.Done or StageStatus.None,
                        DocumentCount = docs.Count,
                        CanView = Can(screen) || visible.Count != 0,
                        Note = note,
                        Documents = [.. visible.Take(take)],
                    });
                }
                static JourneyDocumentModel Doc(Guid id, string? number, string? status, string? statusName, DateTime? at, string screen)
                    => new() { Id = id, Number = number, Status = status, StatusName = statusName, At = at, Route = "#/" + screen };

                // 1. Đơn hàng.
                string orderStage = cancelled ? StageStatus.Cancelled
                    : order.Status is OrderStatus.Draft or OrderStatus.Submitted ? StageStatus.InProgress : StageStatus.Done;
                Add(Stage.Order, "Đơn hàng", ScreenOrder, orderStage, order.OrderDate,
                    [Doc(order.Id, orderNumber, order.Status, OrderStatus.Label(order.Status), order.OrderDate, ScreenOrder)]);

                // 2. Kế hoạch sản xuất (gộp theo ngày ăn).
                DateTime day = order.MealDate.Date, next = day.AddDays(1);
                List<Guid>? units = customer?.BranchId is { } branch ? await _commonService.GetUnitTree(branch) : null;
                var plans = await db.ProductionPlan.AsNoTracking().Where(p => !p.IsDeleted && p.ProductionDate >= day && p.ProductionDate < next).ToListAsync();
                plans = [.. plans.Where(p => p.OrganizationUnitId == null || units == null || units.Contains(p.OrganizationUnitId.Value))
                    .OrderBy(p => p.PlanNumber ?? p.PlanNo, StringComparer.Ordinal)];
                string planStage = plans.Count == 0 ? StageStatus.NotStarted
                    : plans.All(p => p.Status is ProductionPlanStatus.Released or ProductionPlanStatus.Completed) ? StageStatus.Done : StageStatus.InProgress;
                Add(Stage.ProductionPlan, "Kế hoạch sản xuất", "prod-plan", planStage, plans.Count == 0 ? null : plans.Max(p => p.ModifiedDate ?? p.CreatedDate),
                    [.. plans.Select(p => Doc(p.Id, p.PlanNumber ?? p.PlanNo, p.Status, ProductionPlanStatus.Label(p.Status), p.PlanDate, "prod-plan"))],
                    plans.Count == 0 ? null : ConstantHelper.Message.JourneySharedProduction);

                // 3. Lệnh sản xuất + mẻ nấu (lệnh thuộc ca của dòng đơn; dòng không có ca → mọi lệnh của kế hoạch).
                var lineShifts = await db.OrderLine.AsNoTracking().Where(l => !l.IsDeleted && l.OrderId == order.Id && l.ShiftId != null)
                    .Select(l => l.ShiftId!.Value).Distinct().ToListAsync();
                var planIds = plans.Select(p => p.Id).ToList();
                var pos = await db.ProductionOrder.AsNoTracking()
                    .Where(o => !o.IsDeleted && planIds.Contains(o.ProductionPlanId) && (lineShifts.Count == 0 || o.ShiftId == null || lineShifts.Contains(o.ShiftId.Value))).ToListAsync();
                var poIds = pos.Select(o => o.Id).ToList();
                var batches = await db.ProductionBatch.AsNoTracking().Where(b => !b.IsDeleted && poIds.Contains(b.ProductionOrderId)).ToListAsync();
                var activeBatches = batches.Where(b => b.Status != ProductionBatchStatus.Cancelled).ToList();
                var activePos = pos.Where(o => o.Status != ProductionOrderStatus.Cancelled).ToList();
                string prodStage = activePos.Count == 0 && activeBatches.Count == 0 ? StageStatus.NotStarted
                    : activeBatches.Count != 0 && activeBatches.All(b => b.Status == ProductionBatchStatus.Failed) ? StageStatus.Issue
                    : activeBatches.Count != 0 && activeBatches.All(b => ProductionBatchStatus.Closed.Contains(b.Status)) ? StageStatus.Done
                    : StageStatus.InProgress;
                int failed = activeBatches.Count(b => b.Status == ProductionBatchStatus.Failed);
                var prodDocs = pos.OrderBy(o => o.ProductionOrderNumber ?? o.ProductionOrderNo, StringComparer.Ordinal)
                    .Select(o => Doc(o.Id, o.ProductionOrderNumber ?? o.ProductionOrderNo, o.Status, ProductionOrderStatus.Label(o.Status),
                        o.ActualEndDate ?? o.ActualStartDate ?? o.PlannedStartDate, "prod-order"))
                    .Concat(batches.OrderBy(b => b.BatchNumber ?? b.BatchNo, StringComparer.Ordinal)
                        .Select(b => Doc(b.Id, b.BatchNumber ?? b.BatchNo, b.Status, ProductionBatchStatus.Label(b.Status), b.EndTime ?? b.StartTime, "prod-batch")))
                    .ToList();
                DateTime? prodAt = batches.Select(b => b.EndTime ?? b.StartTime).Concat(pos.Select(o => o.ActualEndDate ?? o.ActualStartDate)).Where(x => x != null).Max();
                Add(Stage.Production, "Sản xuất (lệnh SX / mẻ nấu)", "prod-batch", prodStage, prodAt, prodDocs,
                    failed > 0 ? $"{failed} mẻ không đạt QC" : null);

                // 4. Đóng gói (lệnh đóng gói gắn đơn, ≠ Hủy).
                var packings = await db.PackingOrder.AsNoTracking().Where(p => !p.IsDeleted && p.OrderId == order.Id && p.Status != PackingOrderStatus.Cancelled).ToListAsync();
                string packStage = packings.Count == 0 ? StageStatus.NotStarted
                    : packings.All(p => p.Status == PackingOrderStatus.Packed) ? StageStatus.Done : StageStatus.InProgress;
                Add(Stage.Packing, "Đóng gói", "log-packing-order", packStage, packings.Count == 0 ? null : packings.Max(p => p.PackingDate),
                    [.. packings.OrderBy(p => p.PackingOrderNumber ?? p.PackingNo, StringComparer.Ordinal)
                        .Select(p => Doc(p.Id, p.PackingOrderNumber ?? p.PackingNo, p.Status, PackingOrderStatus.Label(p.Status), p.PackingDate, "log-packing-order"))]);

                // 5. Giao hàng: đơn giao (≠ Hủy) + chuyến xe; trạng thái thực hiện như DeliveryExecution/GetBoard.
                var allDeliveries = await db.DeliveryOrder.AsNoTracking().Where(d => !d.IsDeleted && d.OrderId == order.Id).ToListAsync();
                var deliveries = allDeliveries.Where(d => d.Status != DeliveryOrderStatus.Cancelled)
                    .OrderBy(d => d.DeliveryNumber ?? d.DeliveryNo, StringComparer.Ordinal).ToList();
                var statuses = await ExecutionStatusesAsync(deliveries);
                var doIds = deliveries.Select(d => d.Id).ToList();
                var executions = await db.DeliveryExecution.AsNoTracking().Where(e => !e.IsDeleted && doIds.Contains(e.DeliveryOrderId)).ToListAsync();
                var routeIds = (await db.DeliveryStop.AsNoTracking().Where(s => !s.IsDeleted && s.DeliveryOrderId != null && doIds.Contains(s.DeliveryOrderId.Value))
                        .Select(s => s.DeliveryRouteId).ToListAsync())
                    .Concat(deliveries.Where(d => d.DeliveryRouteId != null).Select(d => d.DeliveryRouteId!.Value)).Distinct().ToList();
                var routes = await db.DeliveryRoute.AsNoTracking().Where(r => routeIds.Contains(r.Id) && !r.IsDeleted).ToListAsync();
                var live = deliveries.Where(d => statuses[d.Id] != DeliveryExecutionStatus.Failed).ToList();
                int failedDeliveries = deliveries.Count - live.Count;
                string deliveryStage = deliveries.Count == 0 ? StageStatus.NotStarted
                    : live.Count == 0 ? StageStatus.Issue
                    : live.All(d => statuses[d.Id] == DeliveryExecutionStatus.Delivered) ? StageStatus.Done
                    : StageStatus.InProgress;
                DateTime? deliveredAt = executions.Select(e => e.DeliveredAt).Concat(deliveries.Select(d => d.ActualDeliveryTime)).Where(x => x != null).Max();
                var deliveryDocs = deliveries.Select(d => Doc(d.Id, d.DeliveryNumber ?? d.DeliveryNo, statuses[d.Id], DeliveryExecutionStatus.Label(statuses[d.Id]),
                        d.ActualDeliveryTime ?? d.PlannedDeliveryTime, "log-route")) /* UI-6.34: Đơn giao hàng gộp vào Điều phối */
                    .Concat(routes.OrderBy(r => DeliveryRules.RouteNumber(r), StringComparer.Ordinal)
                        .Select(r => Doc(r.Id, DeliveryRules.RouteNumber(r), r.Status, DeliveryRouteStatus.Label(r.Status), r.ActualEnd ?? r.ActualStart ?? r.DeliveryDate, "log-route")))
                    .ToList();
                Add(Stage.Delivery, "Giao hàng (đơn giao / chuyến xe)", "log-route", deliveryStage, deliveredAt, deliveryDocs,
                    failedDeliveries > 0 ? $"{failedDeliveries} đơn giao không thành công" : null);

                // 6. Xác nhận giao (POD) theo đơn giao – số chứng từ = số đơn giao (POD không có số riêng).
                var pods = await db.ProofOfDelivery.AsNoTracking().Where(p => !p.IsDeleted && doIds.Contains(p.DeliveryOrderId)).ToListAsync();
                var delivered = live.Where(d => statuses[d.Id] == DeliveryExecutionStatus.Delivered).ToList();
                string podStage = pods.Count == 0 ? StageStatus.NotStarted
                    : delivered.Count != 0 && delivered.Count == live.Count && delivered.All(d => pods.Any(p => p.DeliveryOrderId == d.Id)) ? StageStatus.Done
                    : StageStatus.InProgress;
                var doNumbers = deliveries.ToDictionary(d => d.Id, d => d.DeliveryNumber ?? d.DeliveryNo);
                Add(Stage.Pod, "Xác nhận giao nhận (POD)", "log-execution", podStage, pods.Count == 0 ? null : pods.Max(p => p.ConfirmedAt),
                    [.. pods.OrderBy(p => p.ConfirmedAt).Select(p => Doc(p.Id, doNumbers.GetValueOrDefault(p.DeliveryOrderId), null,
                        $"Nhận {DeliveryRules.Num(p.AcceptedQuantity ?? p.AcceptedQty ?? 0m)} / giao {DeliveryRules.Num(p.DeliveredQuantity ?? p.DeliveredQty ?? 0m)} suất",
                        p.ConfirmedAt, "log-execution"))]);

                // 7. Đối soát – số chứng từ = số đơn (đối soát không có số riêng).
                var recs = await db.OrderReconciliation.AsNoTracking().Where(r => !r.IsDeleted && r.OrderId == order.Id).OrderBy(r => r.ReconciliationDate).ToListAsync();
                string recStage = recs.Count == 0 ? StageStatus.NotStarted
                    : recs.Any(r => r.Status is ReconciliationStatus.Matched or ReconciliationStatus.Variance or ReconciliationStatus.Closed) ? StageStatus.Done : StageStatus.InProgress;
                Add(Stage.Reconciliation, "Đối soát", "crm-reconciliation", recStage, recs.Count == 0 ? null : recs.Max(r => r.ModifiedDate ?? r.ReconciliationDate),
                    [.. recs.Select(r => Doc(r.Id, orderNumber, r.Status, ReconciliationStatus.Label(r.Status), r.ReconciliationDate, "crm-reconciliation"))]);

                // 8. Phản ánh (gắn đơn hoặc đơn giao của đơn) – không có = chặng "Không có" (coi như xong).
                var allDoIds = allDeliveries.Select(d => d.Id).ToList();
                var complaints = await db.CustomerComplaint.AsNoTracking()
                    .Where(c => !c.IsDeleted && (c.OrderId == order.Id || (c.DeliveryOrderId != null && allDoIds.Contains(c.DeliveryOrderId.Value))))
                    .OrderBy(c => c.ReceivedAt).ToListAsync();
                string complaintStage = complaints.Count == 0 ? StageStatus.None
                    : complaints.All(c => ComplaintStatus.Done.Contains(c.ComplaintStatus)) ? StageStatus.Done : StageStatus.Issue;
                Add(Stage.Complaint, "Phản ánh khách hàng", "log-complaint", complaintStage,
                    complaints.Count == 0 ? null : complaints.Max(c => c.ResolvedAt ?? c.ReceivedAt),
                    [.. complaints.Select(c => Doc(c.Id, c.ComplaintNo, c.ComplaintStatus, ComplaintStatus.Label(c.ComplaintStatus), c.ResolvedAt ?? c.ReceivedAt, "log-complaint"))],
                    complaints.Count == 0 ? ConstantHelper.Message.JourneyNoComplaint : null);

                model.CurrentStage = cancelled ? null : model.Stages.FirstOrDefault(s => !s.Done)?.Stage;
                return Result<OrderJourneyModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetOrderJourney: ", ex.ToString()));
                throw;
            }
        }

        // ============================================================ mẫu cũ (giữ nguyên – controller cũ đã comment)

        public IResult<DashboardModel> GetCount()
        {
            try
            {
                DashboardModel data = new();
                return new Result<DashboardModel>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    Data = data,
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetCount: ", ex.ToString()));
                throw;
            }
        }

        public IResult<DashboardModel> GetListChartOptionsBar()
        {
            try
            {
                DashboardModel data = new();
                return new Result<DashboardModel>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    Data = data,
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetListChartOptionsBar: ", ex.ToString()));
                throw;
            }
        }

        public IResult<DashboardModel> GetListChartOptionsPolarArea()
        {
            try
            {
                DashboardModel data = new();
                return new Result<DashboardModel>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    Data = data,
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetListChartOptionsPolarArea: ", ex.ToString()));
                throw;
            }
        }
    }
}
