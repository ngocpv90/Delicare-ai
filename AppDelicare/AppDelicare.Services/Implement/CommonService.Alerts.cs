#nullable enable
using System.Globalization;
using System.Text;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Services.Interface;
using AppDelicare.Model.Request.Custom;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// BE3.3 (Catalog 16, 17, 18 – D-57 h, i, D-58): chuông cảnh báo (spec 12.1), tìm kiếm toàn cục, tab "Liên quan". Chỉ đọc.
    /// Ngưỡng / giới hạn: <c>BusinessRules:Alerts</c> (PROPOSED – BA/PO CONFIRM, D-57 p–u).
    /// </summary>
    public partial class CommonService
    {
        public const string SeverityHigh = "HIGH";
        public const string SeverityMedium = "MEDIUM";
        public const string SeverityLow = "LOW";

        /// <summary>Mã cảnh báo (cố định – FE dùng làm khóa).</summary>
        public static class AlertCode
        {
            public const string StockBelowMin = "STOCK_BELOW_MIN";
            public const string ApprovalPending = "APPROVAL_PENDING";
            public const string QcIncomingPending = "QC_INCOMING_PENDING";
            public const string LotOnHold = "LOT_ON_HOLD";
            public const string TemperatureOutOfRange = "TEMPERATURE_OUT_OF_RANGE";
            public const string ComplaintOverdue = "COMPLAINT_OVERDUE";
            public const string ComplaintOpen = "COMPLAINT_OPEN";
            public const string OutboundFailed = "OUTBOUND_FAILED";
            public const string LotExpiring = "LOT_EXPIRING";
            // UI-6.46 (D-125 c): cảnh báo mới của các đợt UI-6.
            public const string AccountResignedOpen = "ACCOUNT_RESIGNED_OPEN";
            public const string ShiftShort = "SHIFT_SHORT";
            public const string CleanTraysShort = "CLEAN_TRAYS_SHORT";
            public const string RouteNotReady = "ROUTE_NOT_READY";
        }

        /// <summary>Màn hình (screen-id) chịu quyền Xem + route của từng cảnh báo. Cảnh báo duyệt không cần quyền màn (hộp duyệt của chính người dùng).</summary>
        public static readonly IReadOnlyDictionary<string, string> AlertScreens = new Dictionary<string, string>
        {
            [AlertCode.StockBelowMin] = "scm-inv-balance",
            [AlertCode.ApprovalPending] = "wf-inbox",
            [AlertCode.QcIncomingPending] = "qc-inspection",
            [AlertCode.LotOnHold] = "scm-inv-balance", // UI-6.29: tab Theo lô
            [AlertCode.TemperatureOutOfRange] = "qc-temperature",
            [AlertCode.ComplaintOverdue] = "log-complaint",
            [AlertCode.ComplaintOpen] = "log-complaint",
            [AlertCode.OutboundFailed] = "int-outbound",
            [AlertCode.LotExpiring] = "scm-inv-balance", // UI-6.29: tab Theo lô
            [AlertCode.AccountResignedOpen] = "adm-user", // UI-6.42
            [AlertCode.ShiftShort] = "hr-shift", // UI-6.37
            [AlertCode.CleanTraysShort] = "qc-cleaning", // UI-6.32
            [AlertCode.RouteNotReady] = "log-route", // UI-6.34
        };

        private const string OutboundFailedStatus = "Failed";
        private const string InactiveStatus = "Inactive";

        private static string CacheKeyOfAlerts(Guid userId, Guid? branchId) => $"common-alerts:{userId:N}:{branchId?.ToString("N") ?? "all"}";

        /// <summary>
        /// Catalog 18: cảnh báo cho chuông (chỉ cảnh báo có số &gt; 0, chỉ màn người dùng có quyền Xem; duyệt = hộp duyệt của chính người đó).
        /// branchId: lọc theo đơn vị (chi nhánh + đơn vị con) qua kho (<c>Warehouse.OrganizationUnitId</c>) / khách (<c>Customer.BranchId</c>);
        /// dữ liệu không xác định được đơn vị vẫn tính (PROPOSED – D-57 q). Cache <c>CacheSeconds</c> theo (người, chi nhánh).
        /// </summary>
        public async Task<IResults<AlertModel>> GetAlerts(Guid? branchId)
        {
            try
            {
                var actor = _currentActor.RequireActor();
                if (branchId == Guid.Empty)
                {
                    branchId = null;
                }
                string cacheKey = CacheKeyOfAlerts(actor.UserId, branchId);
                if (_alertRules.CacheSeconds > 0 && _cache.TryGetValue(cacheKey, out List<AlertModel>? cached) && cached != null)
                {
                    return Results<AlertModel>.Success(cached, ConstantHelper.Message.GetAllSuccessfully);
                }
                var db = _unitOfWork.DbContext;
                List<Guid>? units = null;
                if (branchId is { } bid)
                {
                    units = await UnitTreeAsync(bid);
                    if (units == null)
                    {
                        return Results<AlertModel>.Fail(ConstantHelper.Message.AlertBranchNotFound);
                    }
                }
                // Kho thuộc đơn vị KHÁC chi nhánh đang chọn (kho chưa gắn đơn vị vẫn tính).
                List<Guid>? otherWarehouses = units == null ? null : await db.Warehouse.AsNoTracking()
                    .Where(w => w.OrganizationUnitId != null && !units.Contains(w.OrganizationUnitId.Value)).Select(w => w.Id).ToListAsync();
                var perms = await _permissionService.GetAsync(actor.UserId);
                bool Can(string code) => _permissionService.IsAllowed(perms, AlertScreens[code], PermissionAction.View);
                DateTime now = _clock.Now, today = _clock.Today;
                var list = new List<AlertModel>();
                void Add(string code, string severity, int count, string title, string detail)
                {
                    if (count > 0)
                    {
                        list.Add(new AlertModel { Code = code, Severity = severity, Title = title, Detail = detail, Route = "#/" + AlertScreens[code], Count = count });
                    }
                }

                // 1. Nguyên liệu dưới tồn tối thiểu (spec 12.1: Σ AvailableQuantity < InventoryPolicie.MinimumStock).
                if (Can(AlertCode.StockBelowMin))
                {
                    int n = await CountStockBelowMinAsync(otherWarehouses);
                    Add(AlertCode.StockBelowMin, SeverityHigh, n, ConstantHelper.Message.AlertStockBelowMinTitle, string.Format(ConstantHelper.Message.AlertStockBelowMinDetail, n));
                }

                // 2. Yêu cầu duyệt Pending / InProgress người đăng nhập duyệt được ở cấp đang chờ – dùng lại đúng quy tắc của hộp duyệt (ApprovalRequest/GetMyPending).
                var pending = await _approvalRequestService.GetMyPending(null);
                int pendingCount = pending.ListData?.Count ?? 0;
                int pendingOverdue = pending.ListData?.Count(x => x.IsOverdue) ?? 0;
                Add(AlertCode.ApprovalPending, pendingOverdue > 0 ? SeverityHigh : SeverityMedium, pendingCount, ConstantHelper.Message.AlertApprovalTitle,
                    string.Format(ConstantHelper.Message.AlertApprovalDetail, pendingCount, pendingOverdue));

                // 3. QC đầu vào chưa kết luận: phiếu nhập Chờ QC (kết luận DAT / DAT_MOT_PHAN / KHONG_DAT – HOLD được quy về DAT_MOT_PHAN khi kết luận, T15).
                if (Can(AlertCode.QcIncomingPending))
                {
                    var receipts = db.GoodsReceipt.AsNoTracking().Where(g => !g.IsDeleted && g.Status == GoodsReceiptStatus.QcPending);
                    if (otherWarehouses != null)
                    {
                        receipts = receipts.Where(g => !otherWarehouses.Contains(g.WarehouseId));
                    }
                    var receiptIds = await receipts.Select(g => g.Id).ToListAsync();
                    int lines = receiptIds.Count == 0 ? 0 : await db.GoodsReceiptLine.AsNoTracking().CountAsync(l => !l.IsDeleted && receiptIds.Contains(l.GoodsReceiptId));
                    Add(AlertCode.QcIncomingPending, SeverityMedium, receiptIds.Count, ConstantHelper.Message.AlertQcPendingTitle,
                        string.Format(ConstantHelper.Message.AlertQcPendingDetail, receiptIds.Count, lines));
                }

                // 4 + 7. Lô còn tồn (tồn > 0 ở kho thuộc chi nhánh): đang tạm giữ / QC HOLD; Khả dụng hết hạn trong N ngày.
                var stock = db.InventoryBalance.AsNoTracking().Where(b => !b.IsDeleted && b.OnHandQuantity > 0);
                if (otherWarehouses != null)
                {
                    stock = stock.Where(b => !otherWarehouses.Contains(b.WarehouseId));
                }
                if (Can(AlertCode.LotOnHold))
                {
                    int n = await CountLotOnHoldAsync(otherWarehouses);
                    Add(AlertCode.LotOnHold, SeverityMedium, n, ConstantHelper.Message.AlertLotOnHoldTitle, string.Format(ConstantHelper.Message.AlertLotOnHoldDetail, n));
                }
                if (Can(AlertCode.LotExpiring))
                {
                    int days = Math.Max(0, _alertRules.LotExpiryDays);
                    DateTime until = today.AddDays(days + 1);
                    int n = await db.MaterialLot.AsNoTracking()
                        .Where(l => !l.IsDeleted && l.Status == MaterialLotStatus.Available && l.ExpiryDate != null && l.ExpiryDate >= today && l.ExpiryDate < until
                            && stock.Any(b => b.MaterialLotId == l.Id))
                        .CountAsync();
                    Add(AlertCode.LotExpiring, SeverityMedium, n, ConstantHelper.Message.AlertLotExpiringTitle, string.Format(ConstantHelper.Message.AlertLotExpiringDetail, n, days));
                }

                // 5. Nhiệt độ ngoài ngưỡng trong N giờ (mọi nguồn: tay, IoT – SourceChannel IOT, mẻ nấu, xe giao).
                if (Can(AlertCode.TemperatureOutOfRange))
                {
                    int hours = Math.Max(1, _alertRules.TemperatureHours);
                    DateTime since = now.AddHours(-hours);
                    var logs = db.TemperatureLog.AsNoTracking().Where(t => !t.IsDeleted && t.IsWithinRange == false && t.MeasuredAt >= since);
                    if (otherWarehouses != null)
                    {
                        var otherZones = await db.StorageZone.AsNoTracking().Where(z => otherWarehouses.Contains(z.WarehouseId)).Select(z => z.Id).ToListAsync();
                        var otherLocations = await db.StorageLocation.AsNoTracking().Where(z => otherWarehouses.Contains(z.WarehouseId)).Select(z => z.Id).ToListAsync();
                        logs = logs.Where(t => !(t.StorageZoneId != null && otherZones.Contains(t.StorageZoneId.Value))
                            && !(t.StorageLocationId != null && otherLocations.Contains(t.StorageLocationId.Value)));
                    }
                    int n = await logs.CountAsync();
                    Add(AlertCode.TemperatureOutOfRange, SeverityHigh, n, ConstantHelper.Message.AlertTemperatureTitle, string.Format(ConstantHelper.Message.AlertTemperatureDetail, n, hours));
                }

                // 6. Phản ánh đang mở (Mới / Đang xử lý): quá hạn SLA (như CustomerComplaint/GetList isOverdue) và chưa quá hạn – 2 cảnh báo không trùng nhau.
                if (Can(AlertCode.ComplaintOpen))
                {
                    var open = OpenComplaints(units);
                    int overdue = await open.CountAsync(c => c.SlaDueAt != null && c.SlaDueAt < now);
                    int total = await open.CountAsync();
                    Add(AlertCode.ComplaintOverdue, SeverityHigh, overdue, ConstantHelper.Message.AlertComplaintOverdueTitle, string.Format(ConstantHelper.Message.AlertComplaintOverdueDetail, overdue));
                    Add(AlertCode.ComplaintOpen, SeverityLow, total - overdue, ConstantHelper.Message.AlertComplaintOpenTitle, string.Format(ConstantHelper.Message.AlertComplaintOpenDetail, total - overdue));
                }

                // 8. Tin nhắn gửi ra ngoài lỗi (không gắn chi nhánh).
                if (Can(AlertCode.OutboundFailed))
                {
                    int n = await db.OutboundMessage.AsNoTracking().CountAsync(m => !m.IsDeleted && m.MessageStatus == OutboundFailedStatus);
                    Add(AlertCode.OutboundFailed, SeverityMedium, n, ConstantHelper.Message.AlertOutboundFailedTitle, string.Format(ConstantHelper.Message.AlertOutboundFailedDetail, n));
                }

                await NewAlertsAsync(Can, Add, branchId, now, today);

                list = [.. list.OrderBy(a => SeverityRank(a.Severity)).ThenBy(a => a.Code, StringComparer.Ordinal)];
                if (_alertRules.CacheSeconds > 0)
                {
                    _cache.Set(cacheKey, list, TimeSpan.FromSeconds(_alertRules.CacheSeconds));
                }
                return Results<AlertModel>.Success(list, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetAlerts: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>
        /// UI-6.46 (D-125 c): tài khoản nhân viên đã nghỉ còn mở (Tài khoản) · ca thiếu người 7 ngày tới (Phân ca) · khay sạch thiếu cho ngày mai (Vệ sinh khay hộp) ·
        /// chuyến sắp xuất bến (≤ DispatchShortWarnMinutes) chưa đủ hàng (Điều phối giao hàng). Dùng lại số liệu của màn gốc; một cảnh báo lỗi không làm hỏng chuông.
        /// </summary>
        private async Task NewAlertsAsync(Func<string, bool> can, Action<string, string, int, string, string> add, Guid? branchId, DateTime now, DateTime today)
        {
            var db = _unitOfWork.DbContext;
            async Task Safe(string code, Func<Task> run)
            {
                if (!can(code))
                {
                    return;
                }
                try
                {
                    await run();
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[CommonService] GetAlerts {Code}: bỏ qua", code);
                }
            }
            await Safe(AlertCode.AccountResignedOpen, async () =>
            {
                var open = await (from u in db.User.AsNoTracking()
                                  join e in db.Employee.AsNoTracking() on u.PersonId equals e.PersonId
                                  where !u.IsDeleted && u.IsActive && !e.IsDeleted && u.PersonId != null
                                  select new { u.Id, e.Status }).ToListAsync();
                int n = open.GroupBy(x => x.Id).Count(g => g.All(x => !EmployeeStatus.IsWorking(x.Status)));
                add(AlertCode.AccountResignedOpen, SeverityHigh, n, "Tài khoản của người đã nghỉ việc còn mở", $"{n} tài khoản cần khóa");
            });
            await Safe(AlertCode.ShiftShort, async () =>
            {
                var grid = (await _services.GetRequiredService<IShiftAssignmentService>().GetGrid(new ShiftGridRequest { FromDate = today, Days = 7, OrganizationUnitId = branchId })).Data;
                string from = today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                int n = grid?.UnitTotals.Count(t => t.Short && string.CompareOrdinal(t.Day, from) >= 0) ?? 0;
                add(AlertCode.ShiftShort, SeverityMedium, n, "Ca thiếu người", $"{n} ca trong 7 ngày tới chưa đủ định biên");
            });
            await Safe(AlertCode.CleanTraysShort, async () =>
            {
                var stock = (await _services.GetRequiredService<ICleaningBatchService>().GetCleanStock(today)).Data;
                var shortTypes = stock?.ListType.Where(t => t.Shortage > 0).ToList() ?? [];
                add(AlertCode.CleanTraysShort, SeverityMedium, shortTypes.Count, "Khay sạch thiếu cho ngày mai",
                    string.Join("; ", shortTypes.Select(t => $"{t.Label ?? t.PackagingType}: thiếu {t.Shortage:#,##0}")));
            });
            await Safe(AlertCode.RouteNotReady, async () =>
            {
                var board = (await _services.GetRequiredService<IDeliveryRouteService>().GetDispatchBoard(today)).Data;
                int n = board?.ListRoute.Count(r => (r.Status == DeliveryRouteStatus.Planned || r.Status == DeliveryRouteStatus.Loading) && r.ListPoint.Count != 0 && !r.CanLoad
                    && r.PlannedStart is { } st && st > now && st <= now.AddMinutes(_bizRules.DispatchShortWarnMinutes)) ?? 0;
                add(AlertCode.RouteNotReady, SeverityHigh, n, "Chuyến sắp xuất bến chưa đủ hàng", $"{n} chuyến trong {_bizRules.DispatchShortWarnMinutes} phút tới chưa đủ thực đơn");
            });
        }

        private static int SeverityRank(string severity) => severity switch { SeverityHigh => 0, SeverityMedium => 1, _ => 2 };

        /// <summary>
        /// BE3.5 (Catalog 231 – dashboard điều hành): số liệu dùng chung với chuông cảnh báo, KHÔNG lọc theo quyền / không cache theo người (dashboard tự cache):
        /// NL dưới tồn tối thiểu, lô còn tồn đang tạm giữ / QC HOLD, phản ánh Mới / Đang xử lý – cùng quy tắc + lọc chi nhánh (đơn vị con) như <see cref="GetAlerts"/>.
        /// Chi nhánh không có → null.
        /// </summary>
        public async Task<SharedAlertCountsModel?> GetSharedCounts(Guid? branchId)
        {
            try
            {
                List<Guid>? units = null;
                if (branchId is { } bid && bid != Guid.Empty)
                {
                    units = await UnitTreeAsync(bid);
                    if (units == null)
                    {
                        return null;
                    }
                }
                List<Guid>? otherWarehouses = units == null ? null : await _unitOfWork.DbContext.Warehouse.AsNoTracking()
                    .Where(w => w.OrganizationUnitId != null && !units.Contains(w.OrganizationUnitId.Value)).Select(w => w.Id).ToListAsync();
                return new SharedAlertCountsModel
                {
                    StockBelowMin = await CountStockBelowMinAsync(otherWarehouses),
                    LotOnHold = await CountLotOnHoldAsync(otherWarehouses),
                    OpenComplaints = await OpenComplaints(units).CountAsync(),
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetSharedCounts: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Lô còn tồn (OnHand &gt; 0 ở kho thuộc chi nhánh) có QcStatus = HOLD hoặc Status = OnHold (PROPOSED – D-57 r).</summary>
        private Task<int> CountLotOnHoldAsync(List<Guid>? otherWarehouses)
        {
            var db = _unitOfWork.DbContext;
            var stock = db.InventoryBalance.AsNoTracking().Where(b => !b.IsDeleted && b.OnHandQuantity > 0);
            if (otherWarehouses != null)
            {
                stock = stock.Where(b => !otherWarehouses.Contains(b.WarehouseId));
            }
            return db.MaterialLot.AsNoTracking()
                .Where(l => !l.IsDeleted && (l.QcStatus == LotQcStatus.Hold || l.Status == MaterialLotStatus.OnHold) && stock.Any(b => b.MaterialLotId == l.Id))
                .CountAsync();
        }

        /// <summary>Phản ánh Mới / Đang xử lý; lọc chi nhánh qua <c>Customer.BranchId</c> (khách chưa gắn chi nhánh vẫn tính – D-57 q).</summary>
        private IQueryable<Model.Entity.CustomerComplaint> OpenComplaints(List<Guid>? units)
        {
            var db = _unitOfWork.DbContext;
            var open = db.CustomerComplaint.AsNoTracking().Where(c => !c.IsDeleted && (c.ComplaintStatus == ComplaintStatus.Open || c.ComplaintStatus == ComplaintStatus.InProgress));
            if (units != null)
            {
                open = open.Where(c => !db.Customer.Any(cu => cu.Id == c.CustomerId && cu.BranchId != null && !units.Contains(cu.BranchId.Value)));
            }
            return open;
        }

        /// <summary>BE3.5: đơn vị + đơn vị con (dashboard lọc chi nhánh như chuông). Không có → null.</summary>
        public Task<List<Guid>?> GetUnitTree(Guid unitId) => UnitTreeAsync(unitId);

        /// <summary>Đơn vị + mọi đơn vị con (theo ParentId). Không có → null.</summary>
        private async Task<List<Guid>?> UnitTreeAsync(Guid unitId)
        {
            var all = await _unitOfWork.DbContext.OrganizationUnit.AsNoTracking().Where(u => !u.IsDeleted).Select(u => new { u.Id, u.ParentId }).ToListAsync();
            if (!all.Any(u => u.Id == unitId))
            {
                return null;
            }
            var children = all.Where(u => u.ParentId != null).ToLookup(u => u.ParentId!.Value, u => u.Id);
            var result = new List<Guid>();
            var queue = new Queue<Guid>([unitId]);
            var seen = new HashSet<Guid>();
            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                if (!seen.Add(id))
                {
                    continue;
                }
                result.Add(id);
                foreach (var child in children[id])
                {
                    queue.Enqueue(child);
                }
            }
            return result;
        }

        /// <summary>
        /// Số nguyên liệu dưới tồn tối thiểu (PROPOSED – D-57 p): (1) có chính sách tồn (InventoryPolicie, bản mới nhất mỗi kho × nguyên liệu, MinimumStock,
        /// không có thì MinStock cũ): Σ AvailableQuantity của kho đó &lt; mức; (2) nguyên liệu KHÔNG có chính sách ở kho nào nhưng có Ingredient.MinimumStock:
        /// Σ AvailableQuantity mọi kho (thuộc chi nhánh) &lt; mức. Nguyên liệu đã xóa / Inactive không tính.
        /// </summary>
        private async Task<int> CountStockBelowMinAsync(List<Guid>? otherWarehouses)
        {
            var db = _unitOfWork.DbContext;
            var ingredients = await db.Ingredient.AsNoTracking().Where(i => !i.IsDeleted && (i.Status == null || i.Status != InactiveStatus))
                .Select(i => new { i.Id, i.MinimumStock }).ToDictionaryAsync(i => i.Id, i => i.MinimumStock);
            var policies = (await db.InventoryPolicy.AsNoTracking().Where(p => !p.IsDeleted)
                    .Select(p => new { p.WarehouseId, p.IngredientId, Min = p.MinimumStock ?? p.MinStock, p.CreatedDate }).ToListAsync())
                .GroupBy(p => (p.WarehouseId, p.IngredientId)).Select(g => g.OrderByDescending(p => p.CreatedDate).First()).ToList();
            var balanceQuery = db.InventoryBalance.AsNoTracking().Where(b => !b.IsDeleted);
            if (otherWarehouses != null)
            {
                balanceQuery = balanceQuery.Where(b => !otherWarehouses.Contains(b.WarehouseId));
            }
            var balances = (await balanceQuery.GroupBy(b => new { b.WarehouseId, b.IngredientId })
                    .Select(g => new { g.Key.WarehouseId, g.Key.IngredientId, Quantity = g.Sum(b => b.AvailableQuantity) }).ToListAsync())
                .ToDictionary(b => (b.WarehouseId, b.IngredientId), b => b.Quantity);
            var below = new HashSet<Guid>();
            foreach (var p in policies.Where(p => p.Min != null && ingredients.ContainsKey(p.IngredientId)))
            {
                if (otherWarehouses != null && otherWarehouses.Contains(p.WarehouseId))
                {
                    continue;
                }
                if (balances.GetValueOrDefault((p.WarehouseId, p.IngredientId)) < p.Min)
                {
                    below.Add(p.IngredientId);
                }
            }
            var withPolicy = policies.Select(p => p.IngredientId).ToHashSet();
            var totals = balances.GroupBy(b => b.Key.IngredientId).ToDictionary(g => g.Key, g => g.Sum(x => x.Value));
            foreach (var (id, min) in ingredients)
            {
                if (min != null && !withPolicy.Contains(id) && totals.GetValueOrDefault(id) < min)
                {
                    below.Add(id);
                }
            }
            return below.Count;
        }

        /// <summary>
        /// Catalog 17: tìm kiếm toàn cục (từ khóa SearchMinLength…SearchMaxLength ký tự, sai → 400): màn hình = mục menu đang hoạt động người dùng được Xem, tên khớp
        /// (không phân biệt hoa thường / dấu); bản ghi = 8 loại danh sách trắng (<see cref="CommonRelations.SearchTypes"/>) người dùng được Xem, tìm theo mã / tên /
        /// số chứng từ, mỗi loại ≤ SearchMaxPerType. Không trả tên bảng / cột (D-58).
        /// </summary>
        public async Task<IResult<GlobalSearchModel>> GlobalSearch(string? q)
        {
            try
            {
                string text = (q ?? string.Empty).Trim();
                int min = Math.Max(1, _alertRules.SearchMinLength), max = Math.Max(min, _alertRules.SearchMaxLength);
                if (text.Length < min || text.Length > max)
                {
                    return Result<GlobalSearchModel>.Fail(string.Format(ConstantHelper.Message.SearchTextLength, min, max));
                }
                var actor = _currentActor.RequireActor();
                var perms = await _permissionService.GetAsync(actor.UserId);
                var db = _unitOfWork.DbContext;
                var model = new GlobalSearchModel { Q = text };

                string folded = Fold(text);
                var menus = await db.ObjectMenu.AsNoTracking().Where(m => !m.IsDeleted && m.IsActive && m.MetaLink != null && m.Name != null)
                    .Select(m => new { m.Name, m.MetaLink, m.OrderShow }).ToListAsync();
                model.Screens = [.. menus
                    .Where(m => m.MetaLink!.StartsWith("#/", StringComparison.Ordinal) && Fold(m.Name!).Contains(folded, StringComparison.Ordinal))
                    .Where(m => perms.IsAdmin || (perms.Menus.TryGetValue(m.MetaLink!, out var rights) && rights.Access))
                    .GroupBy(m => m.MetaLink!, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
                    .OrderBy(m => Fold(m.Name!).StartsWith(folded, StringComparison.Ordinal) ? 0 : 1).ThenBy(m => m.Name, StringComparer.CurrentCulture)
                    .Take(Math.Max(0, _alertRules.SearchMaxScreens))
                    .Select(m => new SearchScreenModel { Title = MenuTabs.ParentOf.TryGetValue(m.MetaLink!, out var tp) ? $"{tp.ParentName} › {m.Name}" : m.Name!, Route = m.MetaLink! })]; // MENU-TAB: tab hiện "Màn cha › Tab"

                int take = Math.Max(1, _alertRules.SearchMaxPerType);
                foreach (var type in CommonRelations.SearchTypes.Where(t => _permissionService.IsAllowed(perms, t.Resource.ScreenId, PermissionAction.View)))
                {
                    var rows = await type.Query(db, text).OrderBy(r => r.Code).ThenBy(r => r.Id).Take(take).ToListAsync();
                    model.Records.AddRange(rows.Select(r => new SearchRecordModel
                    {
                        Type = type.Resource.Key,
                        TypeTitle = type.Resource.Title,
                        Route = "#/" + type.Resource.ScreenId,
                        Id = r.Id,
                        Code = r.Code,
                        Label = CommonRelations.DefaultLabel(new CommonRow { Name = r.Name, At = r.At }, type.WithDate),
                        Status = r.Status,
                        StatusName = type.Resource.StatusLabel(r.Status),
                    }));
                }
                return Result<GlobalSearchModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GlobalSearch: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Bỏ dấu tiếng Việt + chữ thường (so khớp tên màn hình).</summary>
        public static string Fold(string value)
        {
            var sb = new StringBuilder(value.Length);
            foreach (char c in value.Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c switch { 'đ' => 'd', 'Đ' => 'D', _ => c });
                }
            }
            return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        }

        /// <summary>
        /// Catalog 16: tab "Liên quan" của 1 bản ghi – nhóm theo quan hệ viết cứng (<see cref="CommonRelations.Related"/>), mỗi nhóm = 1 màn hình (tiêu đề + route),
        /// tổng số + tối đa RelatedMaxItems dòng mới nhất. Quyền Xem màn của bản ghi (không có → 403); nhóm không có quyền Xem bị bỏ.
        /// resource ngoài danh sách / bản ghi không có → 400. Không trả tên bảng / cột (D-58).
        /// </summary>
        public async Task<IResults<RelatedGroupModel>> GetRelated(string? resource, Guid id)
        {
            try
            {
                var source = CommonRelations.FindRelatedSource(resource);
                if (source == null)
                {
                    return Results<RelatedGroupModel>.Fail(CommonRegistry.Find(resource) == null
                        ? string.Format(ConstantHelper.Message.CommonResourceNotSupported, resource)
                        : string.Format(ConstantHelper.Message.RelatedResourceNotSupported, resource));
                }
                if (await CommonAccess.DenyAsync(_permissionService, _currentActor, source.ScreenId, PermissionAction.View) is { } deny)
                {
                    return new Results<RelatedGroupModel>([], 0) { ResponseCode = CommonAccess.Forbidden, ResponseMessage = deny };
                }
                var db = _unitOfWork.DbContext;
                if (!await source.ExistsAsync(db, id))
                {
                    return Results<RelatedGroupModel>.Fail(string.Format(ConstantHelper.Message.CommonRecordNotFound, source.Title));
                }
                var perms = await _permissionService.GetAsync(_currentActor.RequireActor().UserId);
                int take = Math.Max(1, _alertRules.RelatedMaxItems);
                var groups = new List<RelatedGroupModel>();
                foreach (var g in CommonRelations.Related[source.Key].Where(g => _permissionService.IsAllowed(perms, g.ScreenId, PermissionAction.View)))
                {
                    var query = g.Query(db, id);
                    int count = await query.CountAsync();
                    var rows = count == 0 ? [] : await query.OrderByDescending(r => r.At).ThenBy(r => r.Id).Take(take).ToListAsync();
                    groups.Add(new RelatedGroupModel
                    {
                        Key = g.Key,
                        Title = g.Title,
                        Route = "#/" + g.ScreenId,
                        Count = count,
                        Items = [.. rows.Select(r => new RelatedItemModel { Id = r.Id, Label = g.Label(r), Status = r.Status, StatusName = CommonRelations.StatusLabel(g.EntityType, g.StatusColumn, r.Status) })],
                    });
                }
                return new Results<RelatedGroupModel>(groups, groups.Count) { ResponseCode = 200, ResponseMessage = ConstantHelper.Message.GetAllSuccessfully };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetRelated: ", ex.ToString()));
                throw;
            }
        }
    }
}
