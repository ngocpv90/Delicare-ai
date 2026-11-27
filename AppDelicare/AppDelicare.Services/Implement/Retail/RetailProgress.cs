using AppDelicare.Data.Connection;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement.Retail
{
    /// <summary>
    /// RETAIL-2 (D-136 – PROPOSED – BA/PO CONFIRM): tiến độ + nguồn gốc của đơn khách lẻ cho khách xem.
    /// Đơn lẻ KHÔNG gắn lệnh SX / mẻ (bếp nấu chung theo ngày) → suy theo NGÀY GIAO + BỮA:
    /// lệnh SX của ngày (kế hoạch SX ngày đó hoặc ngày bắt đầu dự kiến) → mẻ của các món trong thực đơn chung (COMMON) của ngày + bữa
    /// (không khớp món nào → mọi mẻ của ngày). Bước: Nhận đơn → Xác nhận → Sơ chế (đã có phiếu xuất NL / lệnh SX đang chạy) → Nấu (mẻ đang nấu)
    /// → Đóng gói (mọi mẻ đã nấu xong: chờ QC / đạt / đã đóng gói) → Giao hàng (đơn Đang giao hoặc chuyến đã gán đang chạy) → Đã giao.
    /// Chỉ trả thông tin chung (món, số mẻ, lô, NCC, nhiệt độ) – không lộ PO, giá vốn, khách / điểm giao khác.
    /// </summary>
    public static class RetailProgress
    {
        public const string StepReceived = "RECEIVED";
        public const string StepConfirmed = "CONFIRMED";
        public const string StepPrep = "PREP";
        public const string StepCooking = "COOKING";
        public const string StepPacking = "PACKING";
        public const string StepDelivering = "DELIVERING";
        public const string StepDelivered = "DELIVERED";
        public const string StepCancelled = "CANCELLED";

        public const string Done = "done";
        public const string Current = "current";
        public const string Pending = "pending";
        public const string CancelledState = "cancelled";

        private static readonly (string Code, string Label)[] Steps =
        [
            (StepReceived, "Nhận đơn"), (StepConfirmed, "Xác nhận"), (StepPrep, "Sơ chế"), (StepCooking, "Nấu"),
            (StepPacking, "Đóng gói"), (StepDelivering, "Giao hàng"), (StepDelivered, "Đã giao"),
        ];

        /// <summary>Nhãn "đang ở bước nào" cho danh sách đơn.</summary>
        public static string StageLabel(string code) => code switch
        {
            StepReceived => "Chờ xác nhận",
            StepConfirmed => "Đã xác nhận – chờ bếp",
            StepPrep => "Đang sơ chế",
            StepCooking => "Đang nấu",
            StepPacking => "Đang đóng gói",
            StepDelivering => "Đang giao",
            StepDelivered => "Đã giao",
            StepCancelled => "Đã hủy",
            _ => code,
        };

        /// <summary>Tình trạng bếp của 1 ngày + bữa.</summary>
        public sealed class Kitchen
        {
            public List<ProductionBatch> Batches { get; } = [];
            public List<Guid> ProductionOrderIds { get; } = [];
            public DateTime? PrepAt { get; set; }
            public bool PrepStarted { get; set; }
            public DateTime? CookAt { get; set; }
            public bool Cooking { get; set; }
            public bool Cooked { get; set; }
            public DateTime? CookedAt { get; set; }
            public bool AllPacked { get; set; }
            /// <summary>Mẻ lọc theo món của thực đơn ngày + bữa (false = không khớp món, lấy mọi mẻ của ngày).</summary>
            public bool MatchedMenu { get; set; }
        }

        private static readonly string[] CookedStatuses = [ProductionBatchStatus.QcPending, ProductionBatchStatus.Passed, ProductionBatchStatus.Failed, ProductionBatchStatus.Packed];

        /// <summary>Món trong thực đơn CHUNG đã công bố của ngày + bữa (bữa so theo nhãn ReferenceLists:MealType).</summary>
        public static async Task<HashSet<Guid>> MenuDishesAsync(DatabaseContext db, DateTime date, string? meal, ReferenceListsOptions lists)
        {
            DateTime day = date.Date;
            int dow = day.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)day.DayOfWeek;
            var menuIds = await db.Menu.AsNoTracking()
                .Where(m => !m.IsDeleted && m.Status == MenuStatus.Published && m.MenuType == MenuScope.Common
                    && (m.EffectiveFrom == null || m.EffectiveFrom <= day) && (m.EffectiveTo == null || m.EffectiveTo >= day))
                .OrderByDescending(m => m.EffectiveFrom).ThenByDescending(m => m.VersionNo).Select(m => m.Id).Take(1).ToListAsync();
            if (menuIds.Count == 0)
            {
                return [];
            }
            var lines = await db.MenuLine.AsNoTracking().Where(l => menuIds.Contains(l.MenuId) && !l.IsDeleted && l.DayOfWeek == dow)
                .Select(l => new { l.DishId, l.MealType }).ToListAsync();
            return lines.Where(l => meal == null || string.Equals(RetailRules.ResolveMeal(l.MealType, lists) ?? l.MealType, meal, StringComparison.OrdinalIgnoreCase))
                .Select(l => l.DishId).ToHashSet();
        }

        public static async Task<Kitchen> KitchenAsync(DatabaseContext db, DateTime date, string? meal, ReferenceListsOptions lists)
        {
            DateTime day = date.Date, next = day.AddDays(1);
            var k = new Kitchen();
            var planIds = await db.ProductionPlan.AsNoTracking().Where(p => !p.IsDeleted && p.ProductionDate >= day && p.ProductionDate < next).Select(p => p.Id).ToListAsync();
            var orders = await db.ProductionOrder.AsNoTracking()
                .Where(o => !o.IsDeleted && o.Status != ProductionOrderStatus.Cancelled
                    && (planIds.Contains(o.ProductionPlanId) || (o.PlannedStartDate >= day && o.PlannedStartDate < next)))
                .ToListAsync();
            if (orders.Count == 0)
            {
                return k;
            }
            var dishes = await MenuDishesAsync(db, day, meal, lists);
            var matched = orders.Where(o => dishes.Contains(o.DishId)).ToList();
            k.MatchedMenu = matched.Count > 0;
            var used = k.MatchedMenu ? matched : orders;
            k.ProductionOrderIds.AddRange(used.Select(o => o.Id));
            var ids = k.ProductionOrderIds;
            k.Batches.AddRange(await db.ProductionBatch.AsNoTracking()
                .Where(b => !b.IsDeleted && ids.Contains(b.ProductionOrderId) && b.Status != ProductionBatchStatus.Cancelled)
                .OrderBy(b => b.StartTime ?? b.StartDate).ToListAsync());
            var issueDates = await db.MaterialIssue.AsNoTracking()
                .Where(i => !i.IsDeleted && ids.Contains(i.ProductionOrderId) && i.Status != MaterialIssueStatus.Cancelled)
                .Select(i => i.IssueDate).ToListAsync();
            k.PrepStarted = issueDates.Count > 0 || used.Any(o => o.Status == ProductionOrderStatus.InProgress) || k.Batches.Any(b => b.Status != ProductionBatchStatus.Planned);
            k.PrepAt = issueDates.Count > 0 ? issueDates.Min() : used.Where(o => o.ActualStartDate != null).Select(o => o.ActualStartDate).Min();
            var started = k.Batches.Where(b => b.Status != ProductionBatchStatus.Planned).ToList();
            k.Cooking = started.Count > 0;
            k.CookAt = started.Select(b => b.StartTime ?? b.StartDate).Where(t => t != null).Min();
            k.Cooked = k.Batches.Count > 0 && k.Batches.All(b => CookedStatuses.Contains(b.Status));
            k.CookedAt = k.Cooked ? k.Batches.Select(b => b.EndTime ?? b.EndDate).Where(t => t != null).Max() : null;
            k.AllPacked = k.Batches.Count > 0 && k.Batches.All(b => b.Status == ProductionBatchStatus.Packed);
            return k;
        }

        /// <summary>Các bước + bước hiện tại. <paramref name="kitchen"/> null = không tra bếp (đơn chưa xác nhận / đã xong).</summary>
        public static (List<RetailProgressStepModel> Steps, string Current) Build(RetailOrder o, Kitchen? kitchen, DeliveryRoute? route)
        {
            var list = new List<RetailProgressStepModel>();
            if (o.OrderStatus == RetailOrderStatus.Cancelled)
            {
                list.Add(Step(StepReceived, Done, o.CreatedDate));
                if (o.ConfirmedAt != null)
                {
                    list.Add(Step(StepConfirmed, Done, o.ConfirmedAt));
                }
                list.Add(new RetailProgressStepModel { Code = StepCancelled, Label = "Đã hủy", State = CancelledState, At = o.CancelledAt, Note = o.CancelReason });
                return (list, StepCancelled);
            }
            string current = o.OrderStatus switch
            {
                RetailOrderStatus.New => StepReceived,
                RetailOrderStatus.Delivering => StepDelivering,
                RetailOrderStatus.Completed => StepDelivered,
                _ => StepConfirmed,
            };
            if (current == StepConfirmed && kitchen != null)
            {
                current = kitchen.Cooked ? StepPacking : kitchen.Cooking ? StepCooking : kitchen.PrepStarted ? StepPrep : StepConfirmed;
            }
            if (current is StepConfirmed or StepPrep or StepCooking or StepPacking && route?.Status == DeliveryRouteStatus.InTransit)
            {
                current = StepDelivering; // chuyến đã gán đã xuất phát – nhân viên chưa kịp chuyển trạng thái đơn
            }
            int at = Array.FindIndex(Steps, s => s.Code == current);
            for (int i = 0; i < Steps.Length; i++)
            {
                string state = i < at ? Done : i == at ? (current == StepDelivered ? Done : Current) : Pending;
                DateTime? time = Steps[i].Code switch
                {
                    StepReceived => o.CreatedDate,
                    StepConfirmed => o.ConfirmedAt,
                    StepPrep => kitchen?.PrepAt,
                    StepCooking => kitchen?.CookAt,
                    StepPacking => kitchen?.CookedAt,
                    StepDelivering => o.DeliveringAt ?? route?.ActualStart,
                    StepDelivered => o.CompletedAt ?? route?.ActualEnd,
                    _ => null,
                };
                var step = Step(Steps[i].Code, state, state == Pending ? null : time);
                if (Steps[i].Code == StepPacking && state != Pending && kitchen?.AllPacked == true)
                {
                    step.Note = "Đã đóng gói xong";
                }
                list.Add(step);
            }
            return (list, current);
        }

        private static RetailProgressStepModel Step(string code, string state, DateTime? at)
            => new() { Code = code, Label = Array.Find(Steps, s => s.Code == code).Label ?? code, State = state, At = at };

        /// <summary>Thông tin chuyến cho khách: xe, tài xế, giờ, số điểm đã giao, sự kiện chung của chuyến + vị trí gần nhất.</summary>
        public static async Task<RetailRouteModel?> RouteAsync(DatabaseContext db, DeliveryRoute? r)
        {
            if (r == null)
            {
                return null;
            }
            string? driver = r.DriverId == null ? null : await db.Employee.AsNoTracking().Where(e => e.Id == r.DriverId).Select(e => e.FullName).FirstOrDefaultAsync();
            string? plate = r.VehicleId == null ? null : await db.Vehicle.AsNoTracking().Where(v => v.Id == r.VehicleId).Select(v => v.LicensePlate ?? v.Code).FirstOrDefaultAsync();
            var stops = await db.DeliveryStop.AsNoTracking().Where(s => !s.IsDeleted && s.DeliveryRouteId == r.Id).Select(s => s.StopStatus).ToListAsync();
            var events = await db.DeliveryEvent.AsNoTracking().Where(e => !e.IsDeleted && e.DeliveryRouteId == r.Id).OrderBy(e => e.EventAt).ToListAsync();
            var last = events.LastOrDefault(e => e.Latitude != null && e.Longitude != null);
            return new RetailRouteModel
            {
                RouteNumber = r.RouteNo ?? r.Code ?? string.Empty,
                Status = r.Status ?? string.Empty,
                StatusLabel = DeliveryRouteStatus.Label(r.Status),
                DriverName = driver,
                LicensePlate = plate,
                PlannedStart = r.PlannedStart, PlannedEnd = r.PlannedEnd, ActualStart = r.ActualStart, ActualEnd = r.ActualEnd,
                StopCount = stops.Count,
                StopDoneCount = stops.Count(s => s is DeliveryStopStatus.Delivered or DeliveryStopStatus.Failed),
                LastLatitude = last?.Latitude, LastLongitude = last?.Longitude, LastPositionAt = last?.EventAt,
                // Chỉ sự kiện chung của chuyến (xếp hàng, xuất phát, nhiệt độ lúc xuất phát, vị trí) – sự kiện tại điểm của khách khác không hiện.
                ListEvent = events.Where(e => e.DeliveryStopId == null || e.EventType == DeliveryCode.EventPosition)
                    .Select(e => new RetailRouteEventModel { Label = DeliveryCode.EventLabel(e.EventType), At = e.EventAt, TemperatureC = e.TemperatureC }).ToList(),
            };
        }

        /// <summary>Chi tiết đơn cho khách (không tính địa chỉ – lớp gọi tự quyết có trả hay không).</summary>
        public static async Task<RetailMyOrderDetailModel> DetailAsync(DatabaseContext db, RetailOrder o, ReferenceListsOptions lists)
        {
            var lines = await db.RetailOrderLine.AsNoTracking().Where(l => l.RetailOrderId == o.Id && !l.IsDeleted).OrderBy(l => l.CreatedDate).ToListAsync();
            var route = o.DeliveryRouteId == null ? null : await db.DeliveryRoute.AsNoTracking().FirstOrDefaultAsync(r => r.Id == o.DeliveryRouteId && !r.IsDeleted);
            var kitchen = o.OrderStatus == RetailOrderStatus.Confirmed || o.OrderStatus == RetailOrderStatus.Delivering || o.OrderStatus == RetailOrderStatus.Completed
                ? await KitchenAsync(db, o.DeliveryDate, o.MealType, lists) : null;
            var (steps, current) = Build(o, o.OrderStatus == RetailOrderStatus.Confirmed ? kitchen : WithTimesOnly(kitchen), route);
            return new RetailMyOrderDetailModel
            {
                OrderNumber = o.OrderNumber ?? string.Empty, OrderStatus = o.OrderStatus ?? string.Empty, DeliveryDate = RetailRules.Iso(o.DeliveryDate),
                MealType = o.MealType ?? string.Empty, CustomerName = o.CustomerName ?? string.Empty, TotalAmount = o.TotalAmount,
                PaymentStatus = o.PaymentStatus ?? RetailCodes.Unpaid, CancelReason = o.CancelReason,
                Lines = lines.Select(l => new RetailTrackLineModel { ProductName = l.ProductName ?? string.Empty, Quantity = l.Quantity, Unit = l.Unit ?? string.Empty, LineAmount = l.LineAmount }).ToList(),
                CreatedDate = o.CreatedDate,
                StageCode = current, StageLabel = StageLabel(current), ListStep = steps,
                Route = await RouteAsync(db, route),
                HasTrace = kitchen != null && kitchen.Batches.Count > 0,
            };
        }

        /// <summary>Đơn đã qua bếp: chỉ lấy mốc giờ của bếp, không để bếp quyết bước hiện tại.</summary>
        private static Kitchen? WithTimesOnly(Kitchen? k)
        {
            if (k == null)
            {
                return null;
            }
            var copy = new Kitchen { PrepAt = k.PrepAt, CookAt = k.CookAt, CookedAt = k.CookedAt, AllPacked = k.AllPacked, MatchedMenu = k.MatchedMenu };
            return copy;
        }

        /// <summary>Bước hiện tại cho danh sách nhiều đơn: tra bếp 1 lần / (ngày, bữa) của các đơn đã xác nhận.</summary>
        public static async Task<Dictionary<Guid, string>> StagesAsync(DatabaseContext db, IReadOnlyCollection<RetailOrder> orders, ReferenceListsOptions lists)
        {
            var kitchens = new Dictionary<(DateTime, string), Kitchen>();
            foreach (var key in orders.Where(o => o.OrderStatus == RetailOrderStatus.Confirmed).Select(o => (o.DeliveryDate.Date, o.MealType ?? string.Empty)).Distinct())
            {
                kitchens[key] = await KitchenAsync(db, key.Item1, key.Item2, lists);
            }
            var routeIds = orders.Where(o => o.DeliveryRouteId != null).Select(o => o.DeliveryRouteId!.Value).Distinct().ToList();
            var routes = await db.DeliveryRoute.AsNoTracking().Where(r => routeIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id);
            return orders.ToDictionary(o => o.Id, o => Build(o, kitchens.GetValueOrDefault((o.DeliveryDate.Date, o.MealType ?? string.Empty)),
                o.DeliveryRouteId is { } rid ? routes.GetValueOrDefault(rid) : null).Current);
        }

        /// <summary>Nguồn gốc: mẻ nấu (món, giờ nấu, QC, nhiệt độ lõi) + nguyên liệu theo lô (NCC, ngày nhận, hạn dùng, kết quả kiểm tra).</summary>
        public static async Task<RetailTraceModel> TraceAsync(DatabaseContext db, RetailOrder o, ReferenceListsOptions lists)
        {
            var k = await KitchenAsync(db, o.DeliveryDate, o.MealType, lists);
            var model = new RetailTraceModel
            {
                OrderNumber = o.OrderNumber ?? string.Empty, DeliveryDate = RetailRules.Iso(o.DeliveryDate), MealType = o.MealType ?? string.Empty,
                Inferred = true,
                Note = k.Batches.Count == 0 ? string.Format(RetailMessage.TraceNotReady, o.DeliveryDate.ToString("dd/MM/yyyy"))
                    : k.MatchedMenu ? "Suất ăn của đơn được nấu chung theo thực đơn ngày " + o.DeliveryDate.ToString("dd/MM/yyyy") + " – bữa " + o.MealType + "."
                    : "Các mẻ nấu của nhà máy ngày " + o.DeliveryDate.ToString("dd/MM/yyyy") + ".",
            };
            if (k.Batches.Count == 0)
            {
                return model;
            }
            var dishIds = k.Batches.Select(b => b.DishId).Distinct().ToList();
            var dishes = await db.Dish.AsNoTracking().Where(d => dishIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name);
            var batchIds = k.Batches.Select(b => b.Id).ToList();
            var temps = await db.TemperatureLog.AsNoTracking().Where(t => !t.IsDeleted && t.ProductionBatchId != null && batchIds.Contains(t.ProductionBatchId.Value))
                .OrderBy(t => t.MeasuredAt).ToListAsync();
            var issues = await db.MaterialIssue.AsNoTracking()
                .Where(i => !i.IsDeleted && k.ProductionOrderIds.Contains(i.ProductionOrderId) && i.Status != MaterialIssueStatus.Cancelled).ToListAsync();
            var issueIds = issues.Select(i => i.Id).ToList();
            var issueLines = await db.MaterialIssueLine.AsNoTracking().Where(l => !l.IsDeleted && issueIds.Contains(l.MaterialIssueId)).ToListAsync();
            var lotIds = issueLines.Select(l => l.MaterialLotId ?? l.LotId).Where(x => x != null).Select(x => x!.Value).Distinct().ToList();
            var lots = await db.MaterialLot.AsNoTracking().Where(l => lotIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id);
            var ingIds = issueLines.Select(l => l.IngredientId).Concat(lots.Values.Select(l => l.IngredientId)).Distinct().ToList();
            var ingredients = await db.Ingredient.AsNoTracking().Where(i => ingIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Name);
            // NCC: của lô, không có thì của phiếu nhập chứa lô.
            var grLineIds = lots.Values.Where(l => l.SupplierId == null && l.GoodsReceiptLineId != null).Select(l => l.GoodsReceiptLineId!.Value).ToList();
            var grSupplier = await (from gl in db.GoodsReceiptLine.AsNoTracking()
                                    join gr in db.GoodsReceipt.AsNoTracking() on gl.GoodsReceiptId equals gr.Id
                                    where grLineIds.Contains(gl.Id)
                                    select new { gl.Id, gr.SupplierId }).ToDictionaryAsync(x => x.Id, x => (Guid?)x.SupplierId);
            Guid? SupplierOf(MaterialLot l) => l.SupplierId ?? (l.GoodsReceiptLineId is { } g ? grSupplier.GetValueOrDefault(g) : null);
            var supIds = lots.Values.Select(SupplierOf).Where(x => x != null).Select(x => x!.Value).Distinct().ToList();
            var suppliers = await db.Supplier.AsNoTracking().Where(s => supIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name);
            // Khâu "Lưu mẫu thực phẩm" (quy trình ATTP): lưu mẫu theo món × ca (D-106) – lệnh SX đã có mẫu thì mọi mẻ của lệnh coi là đã lưu.
            var samples = await ProductionRules.OrderSamplesAsync(db, [.. k.Batches.Select(b => b.ProductionOrderId).Distinct()]);

            foreach (var b in k.Batches)
            {
                string number = b.BatchNumber ?? b.BatchNo ?? string.Empty;
                // Phiếu xuất của mẻ: phiếu của lệnh SX ghi số mẻ trong Note (GAP-PRD-04); lệnh chỉ có 1 mẻ → mọi phiếu của lệnh.
                bool single = k.Batches.Count(x => x.ProductionOrderId == b.ProductionOrderId) == 1;
                var mine = issues.Where(i => i.ProductionOrderId == b.ProductionOrderId && (single || (number.Length > 0 && i.Note != null && i.Note.Contains(number)))).Select(i => i.Id).ToHashSet();
                var item = new RetailTraceBatchModel
                {
                    DishName = dishes.GetValueOrDefault(b.DishId) ?? string.Empty,
                    BatchNumber = number,
                    StatusLabel = ProductionBatchStatus.Label(b.Status),
                    QcLabel = string.IsNullOrWhiteSpace(b.QcStatus) ? "Chưa kiểm tra" : BatchQcStatus.Label(b.QcStatus),
                    CookStart = b.StartTime ?? b.StartDate,
                    CookEnd = b.EndTime ?? b.EndDate,
                    SampleRetainedAt = samples.TryGetValue(b.ProductionOrderId, out var sample) ? sample.At : null,
                    ListTemperature = temps.Where(t => t.ProductionBatchId == b.Id).Select(t => new RetailTraceTemperatureModel
                    {
                        Label = t.MeasurementType == ProductionCode.CoreMeasurement ? "Nhiệt độ lõi món" : "Nhiệt độ",
                        TemperatureC = t.TemperatureC, MeasuredAt = t.MeasuredAt, IsWithinRange = t.IsWithinRange,
                    }).ToList(),
                };
                foreach (var g in issueLines.Where(l => mine.Contains(l.MaterialIssueId)).GroupBy(l => (l.MaterialLotId ?? l.LotId, l.IngredientId)))
                {
                    var lot = g.Key.Item1 is { } lid ? lots.GetValueOrDefault(lid) : null;
                    Guid? sup = lot == null ? null : SupplierOf(lot);
                    item.ListIngredient.Add(new RetailTraceIngredientModel
                    {
                        IngredientName = ingredients.GetValueOrDefault(lot?.IngredientId ?? g.Key.IngredientId) ?? string.Empty,
                        LotNumber = lot?.LotNumber ?? lot?.LotNo,
                        SupplierName = sup is { } s ? suppliers.GetValueOrDefault(s) : null,
                        ReceivedDate = lot?.ReceivedDate,
                        ExpiryDate = lot?.ExpiryDate,
                        QcLabel = LotQcLabel(lot?.QcStatus, lot != null),
                    });
                }
                item.ListIngredient = item.ListIngredient.OrderBy(i => i.IngredientName, StringComparer.CurrentCulture).ToList();
                model.ListBatch.Add(item);
            }
            model.ListSupplier = suppliers.Values.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n!).Distinct().OrderBy(n => n, StringComparer.CurrentCulture).ToList();
            return model;
        }

        private static string LotQcLabel(string? qc, bool hasLot) => (qc ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            LotQcStatus.Pass or LotQcStatus.Dat => "Đạt kiểm tra đầu vào",
            LotQcStatus.DatMotPhan => "Đạt một phần",
            LotQcStatus.Hold => "Tạm giữ",
            LotQcStatus.Reject or LotQcStatus.KhongDat => "Không đạt",
            _ => hasLot ? "Đã nhận hàng" : "Chưa gắn lô",
        };

        /// <summary>Chuyến giao cùng ngày (chưa hủy) để nhân viên gán cho đơn lẻ.</summary>
        public static async Task<List<RetailRouteOptionModel>> RouteOptionsAsync(DatabaseContext db, DateTime date)
        {
            DateTime day = date.Date, next = day.AddDays(1);
            var routes = await db.DeliveryRoute.AsNoTracking().Where(r => !r.IsDeleted && r.DeliveryDate >= day && r.DeliveryDate < next && r.Status != "Cancelled")
                .OrderBy(r => r.PlannedStart).ThenBy(r => r.RouteNo).ToListAsync();
            var vehicleIds = routes.Where(r => r.VehicleId != null).Select(r => r.VehicleId!.Value).Distinct().ToList();
            var plates = await db.Vehicle.AsNoTracking().Where(v => vehicleIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, v => v.LicensePlate ?? v.Code);
            return routes.Select(r => new RetailRouteOptionModel
            {
                Id = r.Id,
                Status = r.Status ?? string.Empty,
                Label = string.Join(" · ", new[] { r.RouteNo ?? r.Code, r.Name, r.VehicleId is { } v ? plates.GetValueOrDefault(v) : null, r.PlannedStart?.ToString("HH:mm"), DeliveryRouteStatus.Label(r.Status) }
                    .Where(x => !string.IsNullOrWhiteSpace(x))),
            }).ToList();
        }
    }
}
