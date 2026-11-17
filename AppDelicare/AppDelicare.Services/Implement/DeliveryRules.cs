using System.Globalization;
using AppDelicare.Data.Connection;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Services.Interface;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// T19 (Pilot P8) – quy tắc dùng chung của giao vận (lớp tĩnh, không đăng ký DI, dùng DbContext / transaction của service gọi):
    /// nhiệt độ giao (DeliveryEvent TEMP_CHECK + TemperatureLog DELIVERY, sổ chỉ ghi thêm), điều kiện xuất phát (mẻ đã đóng gói, đã lưu mẫu, nhiệt độ ≥ 60 °C),
    /// chi tiết chuyến, tạo phản ánh PA-, mẫu in Biên bản giao hàng.
    /// </summary>
    public static class DeliveryRules
    {
        public static string Num(decimal value) => ProductionRules.Num(value);

        public static string RouteNumber(DeliveryRoute route) => route.RouteNo ?? route.Code ?? route.Id.ToString("N")[..8];

        public static string StopName(DeliveryStop stop, string? locationName) => $"#{stop.SequenceNo} {locationName}".Trim();

        public static decimal OrderQuantity(Order order, IEnumerable<OrderLine> lines)
            => order.TotalQuantity is > 0 ? order.TotalQuantity.Value : lines.Where(l => !l.IsDeleted && l.OrderId == order.Id).Sum(l => l.Quantity);

        public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        // ============================================================ BE2.1 – số suất của đơn giao (GAP-DLV-08), suất còn lại của đơn hàng

        /// <summary>Số suất ghi trong tag <c>[SUAT:n]</c> ở đầu DeliveryOrder.Note (null = không có tag – đơn giao T19 lấy từ điểm dừng).</summary>
        public static decimal? TaggedMeals(string? note)
        {
            if (string.IsNullOrEmpty(note) || !note.StartsWith(DeliveryCode.MealTagPrefix, StringComparison.Ordinal))
            {
                return null;
            }
            int end = note.IndexOf(']');
            return end > DeliveryCode.MealTagPrefix.Length
                && decimal.TryParse(note[DeliveryCode.MealTagPrefix.Length..end], NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value) ? value : null;
        }

        /// <summary>Ghi / thay tag số suất ở đầu ghi chú, giữ phần chữ phía sau.</summary>
        public static string WithMeals(string? note, decimal meals)
        {
            string rest = note ?? string.Empty;
            if (TaggedMeals(rest) != null)
            {
                rest = rest[(rest.IndexOf(']') + 1)..].TrimStart();
            }
            string tag = DeliveryCode.MealTagPrefix + Num(meals) + "]";
            return string.IsNullOrEmpty(rest) ? tag : tag + " " + rest;
        }

        /// <summary>Ghi chú đơn giao khi gán chuyến: <c>[SUAT:n] Chuyến CX-… – &lt;ghi chú cũ bỏ phần "Chuyến …" cũ&gt;</c>.</summary>
        public static string RouteNote(string routeNo, string? note, decimal meals)
        {
            string? rest = NoteText(note);
            if (rest != null && rest.StartsWith("Chuyến ", StringComparison.Ordinal))
            {
                int cut = rest.IndexOf(" – ", StringComparison.Ordinal);
                rest = cut < 0 ? null : Clean(rest[(cut + 3)..]);
            }
            return WithMeals(rest == null ? "Chuyến " + routeNo : "Chuyến " + routeNo + " – " + rest, meals);
        }

        /// <summary>Ghi chú không có tag số suất (hiển thị).</summary>
        public static string? NoteText(string? note)
            => TaggedMeals(note) == null ? note : Clean(note![(note.IndexOf(']') + 1)..]);

        /// <summary>Số suất của từng đơn giao: điểm dừng còn hiệu lực (PlannedMealQty) → tag trong Note → 0.</summary>
        public static async Task<Dictionary<Guid, decimal>> DeliveryOrderMealsAsync(DatabaseContext db, IReadOnlyCollection<DeliveryOrder> deliveryOrders)
        {
            var ids = deliveryOrders.Select(d => d.Id).ToList();
            var stops = await db.DeliveryStop.AsNoTracking().Where(s => !s.IsDeleted && s.DeliveryOrderId != null && ids.Contains(s.DeliveryOrderId.Value))
                .Select(s => new { DeliveryOrderId = s.DeliveryOrderId!.Value, s.PlannedMealQty }).ToListAsync();
            return deliveryOrders.ToDictionary(d => d.Id,
                d => stops.FirstOrDefault(s => s.DeliveryOrderId == d.Id)?.PlannedMealQty ?? TaggedMeals(d.Note) ?? 0m);
        }

        /// <summary>Suất đã nằm trong đơn giao còn hiệu lực (Kế hoạch / Đang giao / Đã giao) của từng đơn hàng; <paramref name="exceptId"/> = bỏ qua 1 đơn giao.</summary>
        public static async Task<Dictionary<Guid, decimal>> AllocatedMealsAsync(DatabaseContext db, IReadOnlyCollection<Guid> orderIds, Guid? exceptId = null)
        {
            var active = await db.DeliveryOrder.AsNoTracking()
                .Where(d => !d.IsDeleted && orderIds.Contains(d.OrderId) && DeliveryOrderStatus.Active.Contains(d.Status!) && d.Id != exceptId).ToListAsync();
            var meals = await DeliveryOrderMealsAsync(db, active);
            return active.GroupBy(d => d.OrderId).ToDictionary(g => g.Key, g => g.Sum(d => meals[d.Id]));
        }

        /// <summary>Suất của đơn hàng chưa lập đơn giao còn hiệu lực = tổng suất đơn − Σ suất đơn giao còn hiệu lực.</summary>
        public static async Task<Dictionary<Guid, decimal>> RemainingMealsAsync(DatabaseContext db, IReadOnlyCollection<Order> orders, Guid? exceptDeliveryOrderId = null)
        {
            var orderIds = orders.Select(o => o.Id).ToList();
            var lines = await db.OrderLine.AsNoTracking().Where(l => !l.IsDeleted && orderIds.Contains(l.OrderId)).ToListAsync();
            var allocated = await AllocatedMealsAsync(db, orderIds, exceptDeliveryOrderId);
            return orders.ToDictionary(o => o.Id, o => OrderQuantity(o, lines) - allocated.GetValueOrDefault(o.Id));
        }

        /// <summary>Đơn giao đã có dòng xếp hàng Loaded (đã lên xe).</summary>
        public static Task<bool> IsLoadedAsync(DatabaseContext db, Guid deliveryOrderId)
            => db.DeliveryLoading.AsNoTracking().AnyAsync(l => !l.IsDeleted && l.DeliveryOrderId == deliveryOrderId && l.Status == DeliveryLoadingStatus.Loaded);

        /// <summary>
        /// Đánh số lại điểm dừng của 1 chuyến – ràng buộc UNIQUE (DeliveryRouteId, SequenceNo) tính cả dòng đã xóa mềm (DDL UQ_DeliveryStop_Route_Sequence):
        /// bước 1 đổi các điểm liên quan sang số âm tạm (nhỏ hơn mọi số đang có), lưu; bước 2 gán số cuối, lưu. Điểm bị bỏ (<paramref name="removed"/>) giữ số âm.
        /// <paramref name="ordered"/> = các điểm còn hiệu lực theo thứ tự mới (đánh 1..n) – hoặc <paramref name="finalNumbers"/> nếu có số cụ thể.
        /// </summary>
        public static async Task ResequenceAsync(IUnitOfWork uow, Guid routeId, IReadOnlyList<DeliveryStop> ordered, IReadOnlyCollection<DeliveryStop>? removed = null,
            IReadOnlyDictionary<Guid, int>? finalNumbers = null)
        {
            var db = uow.DbContext;
            int min = Math.Min(0, await db.DeliveryStop.Where(s => s.DeliveryRouteId == routeId).Select(s => (int?)s.SequenceNo).MinAsync() ?? 0);
            var all = ordered.Concat(removed ?? []).ToList();
            int temp = min - 1;
            foreach (var stop in all)
            {
                stop.SequenceNo = temp--;
                uow.DeliveryStopRepository.Update(stop);
            }
            await uow.DeliveryStopRepository.CommitAsync(ConstantHelper.TypeSave.Update);
            for (int i = 0; i < ordered.Count; i++)
            {
                ordered[i].SequenceNo = finalNumbers != null && finalNumbers.TryGetValue(ordered[i].Id, out int n) ? n : i + 1;
                uow.DeliveryStopRepository.Update(ordered[i]);
            }
            await uow.DeliveryStopRepository.CommitAsync(ConstantHelper.TypeSave.Update);
        }

        /// <summary>Số âm chưa dùng cho điểm dừng sắp xóa mềm (giải phóng số thứ tự – xem <see cref="ResequenceAsync"/>).</summary>
        public static async Task<int> FreeNegativeSequenceAsync(DatabaseContext db, Guid routeId)
            => Math.Min(0, await db.DeliveryStop.Where(s => s.DeliveryRouteId == routeId).Select(s => (int?)s.SequenceNo).MinAsync() ?? 0) - 1;

        /// <summary>Cập nhật / tạo dòng thực hiện giao (DeliveryExecution) của đơn giao theo bước (spec 10.3). Không lưu.</summary>
        public static void SyncExecution(IUnitOfWork uow, DeliveryOrder deliveryOrder, Guid? routeId, string status, DateTime at, string? remark = null,
            string? driverName = null, string? vehicleCode = null)
        {
            var execution = uow.DeliveryExecutionRepository.GetAll().Where(e => e.DeliveryOrderId == deliveryOrder.Id && !e.IsDeleted)
                .OrderByDescending(e => e.CreatedDate).FirstOrDefault();
            if (execution == null)
            {
                execution = new DeliveryExecution { Id = Guid.NewGuid(), DeliveryOrderId = deliveryOrder.Id };
                uow.DeliveryExecutionRepository.Add(execution);
            }
            else
            {
                uow.DeliveryExecutionRepository.Update(execution);
            }
            execution.DeliveryRouteId = routeId ?? execution.DeliveryRouteId;
            execution.Status = status;
            execution.DriverName = driverName ?? execution.DriverName;
            execution.VehicleCode = vehicleCode ?? execution.VehicleCode;
            switch (status)
            {
                case DeliveryExecutionStatus.Dispatched:
                    execution.DispatchedAt = at;
                    execution.DepartedAt = at;
                    break;
                case DeliveryExecutionStatus.Arrived:
                    execution.ArrivedAt = at;
                    break;
                case DeliveryExecutionStatus.Delivered:
                    execution.DeliveredAt = at;
                    execution.ArrivedAt ??= at;
                    break;
            }
            if (remark != null)
            {
                execution.Remark = remark.Length > 2000 ? remark[..2000] : remark;
            }
        }

        /// <summary>
        /// Khu vực giao (spec 10.2 plan "byArea") – CustomerDeliveryLocation / Customer không có cột khu vực (GAP-DLV-09, PROPOSED – BA/PO CONFIRM):
        /// <c>Customer.AreaId</c> nếu có; không có → quận / huyện / thị xã / thành phố tách từ địa chỉ điểm giao (phần cuối có tiền tố đó); không có → phần cuối địa chỉ;
        /// trống → "(chưa rõ khu vực)".
        /// </summary>
        public static string AreaOf(CustomerDeliveryLocation? location, Customer? customer)
        {
            if (customer?.AreaId is { } areaId && areaId != Guid.Empty)
            {
                return "AREA:" + areaId.ToString("N")[..8];
            }
            var parts = (location?.Address ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            string[] districtPrefixes = ["quận", "q.", "huyện", "h.", "thị xã", "tx."];
            string[] cityPrefixes = ["thành phố", "tp.", "tp "];
            string? area = parts.LastOrDefault(p => districtPrefixes.Any(x => p.StartsWith(x, StringComparison.OrdinalIgnoreCase)))
                ?? parts.LastOrDefault(p => cityPrefixes.Any(x => p.StartsWith(x, StringComparison.OrdinalIgnoreCase)))
                ?? parts.LastOrDefault();
            return string.IsNullOrWhiteSpace(area) ? "(chưa rõ khu vực)" : area.Trim();
        }

        /// <summary>Lần đo nhiệt độ gần nhất: cấp chuyến (stopId null = xuất phát) hoặc cấp điểm dừng (bàn giao).</summary>
        public static Task<DeliveryEvent?> LatestTemperatureAsync(DatabaseContext db, Guid routeId, Guid? stopId)
            => db.DeliveryEvent.AsNoTracking()
                .Where(e => !e.IsDeleted && e.DeliveryRouteId == routeId && e.EventType == DeliveryCode.EventTemperature && e.TemperatureC != null
                    && (stopId == null ? e.DeliveryStopId == null : e.DeliveryStopId == stopId))
                .OrderByDescending(e => e.EventAt).ThenByDescending(e => e.CreatedDate).ThenBy(e => e.TemperatureC) // cùng giây (cột datetime2(0)) → lấy lần đo thấp nhất (an toàn)
                .FirstOrDefaultAsync();

        /// <summary>
        /// Ghi 1 lần đo nhiệt độ giao (BUSINESS_RULES §7): <c>TemperatureLog</c> MeasurementType DELIVERY (MinAllowedC = DeliveryTempMinC, IsWithinRange ở server)
        /// + <c>DeliveryEvent</c> TEMP_CHECK (chuyến / điểm / đơn giao, ExternalEventId = "TemperatureLog:&lt;Id&gt;" – GAP-DLV-01). Dưới ngưỡng bắt buộc hành động khắc phục.
        /// Trả lỗi nghiệp vụ hoặc null; <paramref name="within"/> = đạt ngưỡng.
        /// </summary>
        public static string? AddTemperature(IUnitOfWork uow, BusinessRulesOptions rules, ActorInfo actor, DateTime at, Guid routeId, Guid? stopId, Guid? deliveryOrderId,
            decimal temperature, string? corrective, string? deviceCode, string? evidenceUrl, List<string> warnings, out bool within)
        {
            decimal min = rules.DeliveryTempMinC;
            within = temperature >= min;
            corrective = Clean(corrective);
            if (!within && corrective == null)
            {
                return string.Format(ConstantHelper.Message.DeliveryTempCorrectiveRequired, Num(temperature), Num(min));
            }
            var log = new TemperatureLog
            {
                Id = Guid.NewGuid(),
                MeasurementType = DeliveryCode.DeliveryMeasurement,
                TemperatureC = temperature,
                MinAllowedC = min,
                IsWithinRange = within,
                MeasuredAt = at,
                DeviceCode = Clean(deviceCode),
                SourceChannel = DeliveryCode.SourceManual,
                RecordedBy = actor.UserId,
                CorrectiveAction = corrective,
                EvidenceUrl = Clean(evidenceUrl),
            };
            uow.TemperatureLogRepository.Add(log);
            uow.DeliveryEventRepository.Add(new DeliveryEvent
            {
                Id = Guid.NewGuid(),
                DeliveryRouteId = routeId,
                DeliveryStopId = stopId,
                DeliveryOrderId = deliveryOrderId,
                EventType = DeliveryCode.EventTemperature,
                EventAt = at,
                TemperatureC = temperature,
                PhotoUrl = Clean(evidenceUrl),
                Description = (stopId == null ? "Nhiệt độ xuất phát " : "Nhiệt độ bàn giao ") + Num(temperature) + " °C"
                    + (within ? string.Empty : " (dưới ngưỡng " + Num(min) + " °C) – khắc phục: " + corrective),
                RecordedBy = actor.UserId,
                SourceChannel = DeliveryCode.SourceManual,
                ExternalEventId = DeliveryCode.TemperatureLogRef + log.Id,
            });
            if (!within)
            {
                warnings.Add(string.Format(ConstantHelper.Message.DeliveryTempBelowWarning, Num(temperature), Num(min)));
            }
            return null;
        }

        public static void AddEvent(IUnitOfWork uow, ActorInfo actor, DateTime at, Guid routeId, Guid? stopId, Guid? deliveryOrderId, string type, string? description,
            decimal? latitude = null, decimal? longitude = null, string? photoUrl = null)
            => uow.DeliveryEventRepository.Add(new DeliveryEvent
            {
                Id = Guid.NewGuid(),
                DeliveryRouteId = routeId,
                DeliveryStopId = stopId,
                DeliveryOrderId = deliveryOrderId,
                EventType = type,
                EventAt = at,
                Latitude = latitude,
                Longitude = longitude,
                PhotoUrl = Clean(photoUrl),
                Description = Clean(description),
                RecordedBy = actor.UserId,
                SourceChannel = DeliveryCode.SourceManual,
            });

        /// <summary>Ca của các đơn trên chuyến (OrderLine.ShiftId) – chuyến không có cột ca (GAP-DLV-04).</summary>
        public static async Task<List<Guid>> RouteShiftIdsAsync(DatabaseContext db, Guid routeId, BusinessRulesOptions rules)
        {
            var orderIds = await db.DeliveryOrder.AsNoTracking().Where(d => !d.IsDeleted && d.DeliveryRouteId == routeId).Select(d => d.OrderId).ToListAsync();
            return await OrderShiftIdsAsync(db, orderIds, rules);
        }

        /// <summary>
        /// Ca của các đơn (OrderLine.ShiftId). T17.1: dòng chưa gán ca thuộc ca mặc định <c>ProductionDefaultShiftCode</c> (cùng quy tắc ProductionPlan/Release);
        /// không có ca mặc định → bỏ qua (danh sách rỗng = mọi mẻ của ngày như T19).
        /// </summary>
        public static async Task<List<Guid>> OrderShiftIdsAsync(DatabaseContext db, IReadOnlyCollection<Guid> orderIds, BusinessRulesOptions rules)
        {
            var shiftIds = await db.OrderLine.AsNoTracking().Where(l => !l.IsDeleted && orderIds.Contains(l.OrderId))
                .Select(l => l.ShiftId).Distinct().ToListAsync();
            var result = shiftIds.Where(s => s != null).Select(s => s!.Value).ToList();
            if (shiftIds.Contains(null))
            {
                var fallback = await db.Shift.AsNoTracking().Where(s => s.Code == rules.ProductionDefaultShiftCode && !s.IsDeleted).Select(s => (Guid?)s.Id).FirstOrDefaultAsync();
                if (fallback != null && !result.Contains(fallback.Value))
                {
                    result.Add(fallback.Value);
                }
            }
            return result;
        }

        /// <summary>
        /// Mẻ nấu phục vụ chuyến: mẻ của lệnh SX thuộc kế hoạch ngày sản xuất = ngày giao, lệnh SX cùng ca với các đơn trên chuyến
        /// (đơn không gán ca / lệnh không có ca → lấy mọi mẻ của ngày). Đánh dấu đã lưu mẫu (SAMPLE_RETAINED).
        /// </summary>
        public static async Task<List<DeliveryBatchBriefModel>> BatchesAsync(DatabaseContext db, DateTime deliveryDate, IReadOnlyCollection<Guid> shiftIds)
        {
            DateTime date = deliveryDate.Date, next = date.AddDays(1);
            var planIds = await db.ProductionPlan.AsNoTracking().Where(p => !p.IsDeleted && p.ProductionDate >= date && p.ProductionDate < next).Select(p => p.Id).ToListAsync();
            var orders = await db.ProductionOrder.AsNoTracking().Where(o => !o.IsDeleted && planIds.Contains(o.ProductionPlanId) && o.Status != ProductionOrderStatus.Cancelled)
                .Select(o => new { o.Id, o.ShiftId }).ToListAsync();
            var orderIds = orders.Where(o => shiftIds.Count == 0 || o.ShiftId == null || shiftIds.Contains(o.ShiftId.Value)).Select(o => o.Id).ToList();
            var batches = await db.ProductionBatch.AsNoTracking().Where(b => !b.IsDeleted && orderIds.Contains(b.ProductionOrderId)).ToListAsync();
            if (batches.Count == 0)
            {
                return [];
            }
            var batchIds = batches.Select(b => b.Id).ToList();
            var itemIds = await db.FoodSafetyChecklistItem.AsNoTracking().Where(i => i.ItemCode == ProductionCode.SampleRetainedItem).Select(i => i.Id).ToListAsync();
            var sampled = await db.FoodSafetyChecklistResult.AsNoTracking()
                .Where(r => !r.IsDeleted && r.ProductionBatchId != null && batchIds.Contains(r.ProductionBatchId.Value) && itemIds.Contains(r.ChecklistItemId))
                .Select(r => r.ProductionBatchId!.Value).Distinct().ToListAsync();
            var dishIds = batches.Select(b => b.DishId).Distinct().ToList();
            var dishes = await db.Dish.AsNoTracking().Where(d => dishIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name);
            return [.. batches.Select(b => new DeliveryBatchBriefModel
            {
                Id = b.Id,
                BatchNumber = b.BatchNumber ?? b.BatchNo,
                DishName = dishes.GetValueOrDefault(b.DishId),
                Status = b.Status,
                StatusName = ProductionBatchStatus.Label(b.Status),
                SampleRetained = sampled.Contains(b.Id),
            }).OrderBy(b => b.BatchNumber, StringComparer.Ordinal)];
        }

        /// <summary>
        /// Điều kiện sản xuất trước khi xuất chuyến (PROPOSED – GAP-DLV-03; lưu mẫu: anh chốt 2026-11-16):
        /// có mẻ của ngày / ca, mọi mẻ đã Đóng gói / Không đạt (<c>DeliveryRequirePackedBatches</c>), mọi mẻ không bị loại đã lưu mẫu (<c>DeliveryRequireSampleRetained</c>).
        /// </summary>
        public static List<string> ProductionBlockers(List<DeliveryBatchBriefModel> batches, BusinessRulesOptions rules)
        {
            var blockers = new List<string>();
            if (rules.DeliveryRequirePackedBatches)
            {
                if (batches.Count == 0)
                {
                    blockers.Add(ConstantHelper.Message.BatchesNone);
                }
                var open = batches.Where(b => !ProductionBatchStatus.Closed.Contains(b.Status)).ToList();
                if (open.Count != 0)
                {
                    blockers.Add(string.Format(ConstantHelper.Message.BatchesNotPacked,
                        string.Join(", ", open.Select(b => $"{b.BatchNumber} ({b.StatusName})"))));
                }
            }
            if (rules.DeliveryRequireSampleRetained)
            {
                var noSample = batches.Where(b => b.Status != ProductionBatchStatus.Failed && !b.SampleRetained).ToList();
                if (noSample.Count != 0)
                {
                    blockers.Add(string.Format(ConstantHelper.Message.BatchesNoSample, string.Join(", ", noSample.Select(b => b.BatchNumber))));
                }
            }
            return blockers;
        }

        /// <summary>Điều kiện xuất phát đầy đủ: sản xuất + nhiệt độ xuất phát gần nhất ≥ DeliveryTempMinC.</summary>
        public static async Task<List<string>> DispatchBlockersAsync(DatabaseContext db, DeliveryRoute route, BusinessRulesOptions rules)
        {
            var batches = await BatchesAsync(db, route.DeliveryDate, await RouteShiftIdsAsync(db, route.Id, rules));
            var blockers = ProductionBlockers(batches, rules);
            if (rules.DeliveryRequireLoading)
            {
                // BE2.1 (spec 10.2 dispatch): mọi đơn giao còn hiệu lực trên chuyến phải đã xếp lên xe (DeliveryLoading Loaded).
                var stopOrderIds = await db.DeliveryStop.AsNoTracking().Where(s => !s.IsDeleted && s.DeliveryRouteId == route.Id && s.DeliveryOrderId != null)
                    .Select(s => s.DeliveryOrderId!.Value).ToListAsync();
                var open = await db.DeliveryOrder.AsNoTracking()
                    .Where(d => !d.IsDeleted && stopOrderIds.Contains(d.Id) && d.Status == DeliveryOrderStatus.Planned).ToListAsync();
                var openIds = open.Select(d => d.Id).ToList();
                var loaded = await db.DeliveryLoading.AsNoTracking()
                    .Where(l => !l.IsDeleted && openIds.Contains(l.DeliveryOrderId) && l.Status == DeliveryLoadingStatus.Loaded).Select(l => l.DeliveryOrderId).ToListAsync();
                var notLoaded = open.Where(d => !loaded.Contains(d.Id)).Select(d => d.DeliveryNumber ?? d.DeliveryNo ?? d.Id.ToString("N")[..8]).OrderBy(x => x, StringComparer.Ordinal).ToList();
                if (notLoaded.Count != 0)
                {
                    blockers.Add(string.Format(ConstantHelper.Message.RouteNotLoaded, string.Join(", ", notLoaded)));
                }
            }
            var temp = await LatestTemperatureAsync(db, route.Id, null);
            if (temp == null)
            {
                blockers.Add(string.Format(ConstantHelper.Message.DepartureTempMissing, Num(rules.DeliveryTempMinC)));
            }
            else if (temp.TemperatureC < rules.DeliveryTempMinC)
            {
                blockers.Add(string.Format(ConstantHelper.Message.DepartureTempBelow, Num(temp.TemperatureC!.Value), Num(rules.DeliveryTempMinC)));
            }
            return blockers;
        }

        /// <summary>Hạn xử lý phản ánh = lúc nhận + số giờ theo mức độ (BusinessRules:ComplaintSlaHours, không phân biệt hoa / thường; không có → MEDIUM).</summary>
        public static DateTime SlaDue(DateTime receivedAt, string severity, BusinessRulesOptions rules)
        {
            var hours = new Dictionary<string, int>(rules.ComplaintSlaHours, StringComparer.OrdinalIgnoreCase);
            int h = hours.TryGetValue(severity, out int v) ? v : hours.GetValueOrDefault("MEDIUM", 24);
            return receivedAt.AddHours(h);
        }

        /// <summary>
        /// Tạo phản ánh <c>PA-yyMMdd-NNNN</c> (T11, trong transaction đang mở) + lịch sử CREATED. Không lưu (service gọi CommitAsync).
        /// </summary>
        public static async Task<CustomerComplaint> NewComplaintAsync(IUnitOfWork uow, IDocumentNumberService numbers, BusinessRulesOptions rules, ActorInfo actor,
            DateTime receivedAt, Guid customerId, Guid? orderId, Guid? deliveryOrderId, Guid? routeId, string? sourceChannel, string? categoryCode, string severity, string description)
        {
            var complaint = new CustomerComplaint
            {
                Id = Guid.NewGuid(),
                ComplaintNo = await numbers.NextAsync(DocumentKind.Complaint, receivedAt),
                CustomerId = customerId,
                OrderId = orderId,
                DeliveryOrderId = deliveryOrderId,
                DeliveryRouteId = routeId,
                SourceChannel = Clean(sourceChannel)?.ToUpperInvariant(),
                CategoryCode = Clean(categoryCode)?.ToUpperInvariant(),
                Severity = severity,
                Description = description,
                ReceivedAt = receivedAt,
                SlaDueAt = SlaDue(receivedAt, severity, rules),
                ComplaintStatus = ComplaintStatus.Open,
            };
            uow.CustomerComplaintRepository.Add(complaint);
            AddHistory(uow, actor, receivedAt, complaint.Id, DeliveryCode.ComplaintCreated, null, ComplaintStatus.Open, description.Length > 2000 ? description[..2000] : description);
            return complaint;
        }

        public static void AddHistory(IUnitOfWork uow, ActorInfo actor, DateTime at, Guid complaintId, string eventType, string? oldStatus, string? newStatus, string? comment)
            => uow.ComplaintHistoryRepository.Add(new ComplaintHistory
            {
                Id = Guid.NewGuid(),
                ComplaintId = complaintId,
                EventType = eventType,
                OldStatus = oldStatus,
                NewStatus = newStatus,
                Comment = Clean(comment),
                EventAt = at,
                ActorId = actor.UserId,
            });

        /// <summary>Chi tiết chuyến (xe, tài xế, điểm dừng + POD, sự kiện, mẻ của ngày / ca, điều kiện xuất phát, thao tác được phép).</summary>
        public static async Task<DeliveryRouteDetailModel?> RouteDetailAsync(DatabaseContext db, Guid routeId, BusinessRulesOptions rules)
        {
            var route = await db.DeliveryRoute.AsNoTracking().FirstOrDefaultAsync(r => r.Id == routeId && !r.IsDeleted);
            if (route == null)
            {
                return null;
            }
            return (await RouteDetailsAsync(db, [route], rules, withBatches: true)).Single();
        }

        public static async Task<List<DeliveryRouteDetailModel>> RouteDetailsAsync(DatabaseContext db, List<DeliveryRoute> routes, BusinessRulesOptions rules, bool withBatches)
        {
            var routeIds = routes.Select(r => r.Id).ToList();
            var stops = await db.DeliveryStop.AsNoTracking().Where(s => !s.IsDeleted && routeIds.Contains(s.DeliveryRouteId)).ToListAsync();
            var deliveryOrderIds = stops.Where(s => s.DeliveryOrderId != null).Select(s => s.DeliveryOrderId!.Value).ToList();
            var deliveryOrders = await db.DeliveryOrder.AsNoTracking().Where(d => deliveryOrderIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id);
            var orderIds = deliveryOrders.Values.Select(d => d.OrderId).Distinct().ToList();
            var orders = await db.Order.AsNoTracking().Where(o => orderIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id);
            var customerIds = stops.Where(s => s.CustomerId != null).Select(s => s.CustomerId!.Value).Distinct().ToList();
            var customers = await db.Customer.AsNoTracking().Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.DisplayName ?? c.Name);
            var locationIds = stops.Where(s => s.DeliveryLocationId != null).Select(s => s.DeliveryLocationId!.Value).Distinct().ToList();
            var locations = await db.CustomerDeliveryLocation.AsNoTracking().Where(l => locationIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id);
            var pods = await db.ProofOfDelivery.AsNoTracking().Where(p => !p.IsDeleted && deliveryOrderIds.Contains(p.DeliveryOrderId)).ToListAsync();
            var events = await db.DeliveryEvent.AsNoTracking().Where(e => !e.IsDeleted && routeIds.Contains(e.DeliveryRouteId)).OrderBy(e => e.EventAt).ThenBy(e => e.CreatedDate).ToListAsync();
            var vehicleIds = routes.Where(r => r.VehicleId != null).Select(r => r.VehicleId!.Value).ToList();
            var vehicles = await db.Vehicle.AsNoTracking().Where(v => vehicleIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id);
            var driverIds = routes.Where(r => r.DriverId != null).Select(r => r.DriverId!.Value).ToList();
            var drivers = await db.Employee.AsNoTracking().Where(e => driverIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.FullName);
            // BE2.1: xếp hàng lên xe + trạng thái thực hiện giao theo đơn giao.
            var loadings = await db.DeliveryLoading.AsNoTracking()
                .Where(l => !l.IsDeleted && deliveryOrderIds.Contains(l.DeliveryOrderId) && l.Status == DeliveryLoadingStatus.Loaded).ToListAsync();
            var executions = await db.DeliveryExecution.AsNoTracking().Where(e => !e.IsDeleted && deliveryOrderIds.Contains(e.DeliveryOrderId)).ToListAsync();

            var result = new List<DeliveryRouteDetailModel>();
            foreach (var route in routes)
            {
                var vehicle = route.VehicleId is { } vid ? vehicles.GetValueOrDefault(vid) : null;
                var routeEvents = events.Where(e => e.DeliveryRouteId == route.Id).ToList();
                var departure = routeEvents.Where(e => e.EventType == DeliveryCode.EventTemperature && e.DeliveryStopId == null && e.TemperatureC != null).LastOrDefault();
                var detail = new DeliveryRouteDetailModel
                {
                    Route = route,
                    StatusName = DeliveryRouteStatus.Label(route.Status),
                    VehicleCode = vehicle?.Code,
                    LicensePlate = vehicle?.LicensePlate,
                    Capacity = vehicle?.Capacity,
                    DriverName = route.DriverId is { } did ? drivers.GetValueOrDefault(did) : null,
                    DeliveryTempMinC = rules.DeliveryTempMinC,
                    DepartureTemperatureC = departure?.TemperatureC,
                    ListEvent = routeEvents,
                };
                foreach (var stop in stops.Where(s => s.DeliveryRouteId == route.Id).OrderBy(s => s.SequenceNo))
                {
                    var deliveryOrder = stop.DeliveryOrderId is { } doid ? deliveryOrders.GetValueOrDefault(doid) : null;
                    var order = deliveryOrder == null ? null : orders.GetValueOrDefault(deliveryOrder.OrderId);
                    var location = stop.DeliveryLocationId is { } lid ? locations.GetValueOrDefault(lid) : null;
                    var handover = routeEvents.Where(e => e.EventType == DeliveryCode.EventTemperature && e.DeliveryStopId == stop.Id && e.TemperatureC != null).LastOrDefault();
                    bool open = route.Status == DeliveryRouteStatus.InTransit && !DeliveryStopStatus.Closed.Contains(stop.StopStatus);
                    detail.ListStop.Add(new DeliveryStopDetailModel
                    {
                        Id = stop.Id,
                        SequenceNo = stop.SequenceNo,
                        StopStatus = stop.StopStatus,
                        StatusName = DeliveryStopStatus.Label(stop.StopStatus),
                        CustomerId = stop.CustomerId,
                        CustomerName = stop.CustomerId is { } cid ? customers.GetValueOrDefault(cid) : null,
                        DeliveryLocationId = stop.DeliveryLocationId,
                        LocationName = location?.Name,
                        Address = location?.Address,
                        OrderId = order?.Id,
                        OrderNumber = order?.OrderNumber ?? order?.OrderNo,
                        DeliveryOrderId = deliveryOrder?.Id,
                        DeliveryNumber = deliveryOrder?.DeliveryNumber ?? deliveryOrder?.DeliveryNo,
                        PlannedArrival = stop.PlannedArrival,
                        ActualArrival = stop.ActualArrival,
                        PlannedMealQty = stop.PlannedMealQty,
                        DeliveredMealQty = stop.DeliveredMealQty,
                        ShortageQty = stop.ShortageQty,
                        HandoverTemperatureC = handover?.TemperatureC,
                        Pod = deliveryOrder == null ? null : pods.Where(p => p.DeliveryOrderId == deliveryOrder.Id).OrderByDescending(p => p.ConfirmedAt).FirstOrDefault(),
                        LoadedQty = deliveryOrder == null ? null : loadings.Where(l => l.DeliveryOrderId == deliveryOrder.Id).OrderByDescending(l => l.LoadingTime).FirstOrDefault()?.LoadedQty,
                        ExecutionStatus = deliveryOrder == null ? null
                            : executions.Where(e => e.DeliveryOrderId == deliveryOrder.Id).OrderByDescending(e => e.CreatedDate).FirstOrDefault()?.Status ?? DeliveryExecutionStatus.Ready,
                        Note = stop.Note,
                        AllowedActions = !open ? []
                            : stop.StopStatus == DeliveryStopStatus.Planned ? ["arrive", "recordTemperature", "deliver", "fail"]
                            : ["recordTemperature", "deliver", "fail"],
                    });
                }
                detail.LoadMeals = detail.ListStop.Sum(s => s.PlannedMealQty ?? 0m);
                detail.LoadPercent = vehicle?.Capacity is > 0 ? Math.Round(detail.LoadMeals / vehicle.Capacity.Value * 100m, 2, MidpointRounding.AwayFromZero) : null;
                bool planning = route.Status is DeliveryRouteStatus.Planned or DeliveryRouteStatus.Loading;
                if (withBatches)
                {
                    var shiftIds = await RouteShiftIdsAsync(db, route.Id, rules);
                    detail.ListBatch = await BatchesAsync(db, route.DeliveryDate, shiftIds);
                    if (planning)
                    {
                        detail.ListBlocker = await DispatchBlockersAsync(db, route, rules);
                    }
                }
                // BE2.1: thêm load (xếp hàng lên xe), updateStops (đổi thứ tự điểm). Bắt buộc xếp hàng → Kế hoạch không có dispatch.
                detail.AllowedActions = route.Status switch
                {
                    DeliveryRouteStatus.Planned => rules.DeliveryRequireLoading
                        ? ["recordTemperature", "load", "updateStops", "update", "delete", "exportDeliveryNote"]
                        : ["recordTemperature", "load", "dispatch", "updateStops", "update", "delete", "exportDeliveryNote"],
                    DeliveryRouteStatus.Loading => ["recordTemperature", "load", "dispatch", "exportDeliveryNote"],
                    DeliveryRouteStatus.InTransit => ["exportDeliveryNote"],
                    _ => ["exportDeliveryNote"],
                };
                result.Add(detail);
            }
            return result;
        }

        /// <summary>
        /// Mẫu in "Biên bản giao hàng / phiếu chuyến" (.xlsx): tên pháp nhân, số chuyến, ngày, xe, tài xế, nhiệt độ xuất phát; bảng điểm dừng (khách, điểm giao, địa chỉ, đơn,
        /// suất kế hoạch, giờ dự kiến, nhiệt độ bàn giao, suất giao / nhận, người nhận, ký nhận). Bố cục PROPOSED (chưa có mẫu thật – GAP-DLV-07).
        /// </summary>
        public static byte[] BuildDeliveryNote(string? companyName, DeliveryRouteDetailModel detail)
        {
            const string brand = "#91AF42";
            const int lastCol = 12;
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Bien ban giao hang");
            ws.Cell(1, 1).Value = companyName ?? "DELICARE";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Range(3, 1, 3, lastCol).Merge().Value = "BIÊN BẢN GIAO HÀNG – PHIẾU CHUYẾN";
            ws.Range(3, 1, 3, lastCol).Style.Font.SetBold().Font.SetFontSize(16).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            var route = detail.Route;
            ws.Range(4, 1, 4, lastCol).Merge().Value = $"Chuyến {RouteNumber(route)} – ngày {route.DeliveryDate:dd/MM/yyyy}";
            ws.Range(4, 1, 4, lastCol).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center).Font.SetItalic();
            ws.Cell(5, 1).Value = "Xe:";
            ws.Cell(5, 2).Value = string.Join(" – ", new[] { detail.VehicleCode, detail.LicensePlate }.Where(x => !string.IsNullOrEmpty(x)));
            ws.Cell(5, 5).Value = "Tài xế:";
            ws.Cell(5, 6).Value = detail.DriverName ?? string.Empty;
            ws.Cell(5, 9).Value = "Nhiệt độ xuất phát (°C):";
            if (detail.DepartureTemperatureC != null)
            {
                ws.Cell(5, 11).Value = detail.DepartureTemperatureC.Value;
            }
            string[] heads = ["STT", "Khách hàng", "Điểm giao", "Địa chỉ", "Đơn hàng", "Suất kế hoạch", "Giờ dự kiến", "Nhiệt độ bàn giao (°C)", "Suất giao", "Suất nhận", "Người nhận", "Ký nhận"];
            int head = 7;
            for (int i = 0; i < heads.Length; i++)
            {
                ws.Cell(head, i + 1).Value = heads[i];
            }
            ws.Range(head, 1, head, lastCol).Style.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(XLColor.FromHtml(brand))
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center).Alignment.SetWrapText(true);
            int row = head;
            foreach (var stop in detail.ListStop)
            {
                row++;
                ws.Cell(row, 1).Value = stop.SequenceNo;
                ws.Cell(row, 2).Value = stop.CustomerName ?? string.Empty;
                ws.Cell(row, 3).Value = stop.LocationName ?? string.Empty;
                ws.Cell(row, 4).Value = stop.Address ?? string.Empty;
                ws.Cell(row, 5).Value = stop.OrderNumber ?? string.Empty;
                ws.Cell(row, 6).Value = stop.PlannedMealQty ?? 0m;
                ws.Cell(row, 7).Value = stop.PlannedArrival?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? string.Empty;
                if (stop.HandoverTemperatureC != null)
                {
                    ws.Cell(row, 8).Value = stop.HandoverTemperatureC.Value;
                }
                if (stop.Pod != null)
                {
                    ws.Cell(row, 9).Value = stop.Pod.DeliveredQuantity ?? 0m;
                    ws.Cell(row, 10).Value = stop.Pod.AcceptedQuantity ?? 0m;
                    ws.Cell(row, 11).Value = stop.Pod.ReceiverName ?? string.Empty;
                }
            }
            row++;
            ws.Cell(row, 5).Value = "Tổng";
            ws.Cell(row, 5).Style.Font.Bold = true;
            ws.Cell(row, 6).Value = detail.LoadMeals;
            ws.Range(head, 1, row, lastCol).Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin).Border.SetInsideBorder(XLBorderStyleValues.Thin);
            row += 2;
            ws.Cell(row, 2).Value = "Điều phối";
            ws.Cell(row, 6).Value = "Tài xế";
            ws.Cell(row, 10).Value = "Đại diện khách hàng";
            ws.Range(row, 1, row, lastCol).Style.Font.Bold = true;
            ws.Columns(1, lastCol).AdjustToContents(8.0, 40.0);
            using var stream = new MemoryStream();
            wb.SaveAs(stream);
            return stream.ToArray();
        }
    }
}
