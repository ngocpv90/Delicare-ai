using System.Globalization;
using AppDelicare.Data.Connection;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Services.Interface;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// T17 (Pilot P7) – quy tắc dùng chung của sản xuất theo mẻ (lớp tĩnh, không đăng ký DI, dùng DbContext / transaction của service gọi):
    /// chọn công thức, ảnh chụp định lượng, lịch ngược từ giờ xuất bếp, nguyên liệu của mẻ, trạng thái ATTP của mẻ, mẫu in Phiếu sản xuất.
    /// </summary>
    public static class ProductionRules
    {
        /// <summary>1 dòng định lượng trong ảnh chụp (cùng hình dạng <c>Recipe.SnapshotJson</c> của RecipeService.Lock).</summary>
        public sealed class SnapshotLine
        {
            public Guid IngredientId { get; set; }
            public decimal Quantity { get; set; }
            public Guid UnitId { get; set; }
            public decimal? WastePercent { get; set; }
            /// <summary>BIZ-1.1: % hao hụt chế biến (RecipeLine.ExpectedWasteRate). Ảnh chụp cũ không có → 0.</summary>
            public decimal? ExpectedWasteRate { get; set; }
            public int? SequenceNo { get; set; }
        }

        /// <summary>1 mẻ trong lịch: số phần, giờ bắt đầu / kết thúc kế hoạch.</summary>
        public readonly record struct ScheduledBatch(int Index, decimal Quantity, DateTime Start, DateTime End);

        /// <summary>Suất 1 món của 1 dòng đơn: Quantity = suất tính, Even = phần chưa cộng dự phòng.</summary>
        public readonly record struct LinePortion(Guid DishId, decimal Quantity, decimal Even);

        /// <summary>
        /// ORD-OPT-1.6 (D-74 e) – suất từng món của 1 dòng đơn, MỘT chỗ cho MRP (Calculate / TopUp), kế hoạch SX (DishDemandsAsync), chia ca (SplitByShiftAsync).
        /// <list type="bullet">
        /// <item>Dòng có <c>MenuOptionNo</c> (khách đăng ký theo lựa chọn): chỉ các món của lựa chọn đó (<c>MenuLine.SequenceNo</c>) × số suất dòng × <c>PortionQty</c> (trống = 1) – không chia đều;
        /// dự phòng <c>BusinessRules:MrpOptionQtyBufferPct</c> (mặc định 0).</item>
        /// <item>Dòng cũ NULL (hoặc lựa chọn không còn trong thực đơn → <paramref name="optionMissing"/> = true): cách cũ GAP-MRP-03 – chia đều số suất cho các lựa chọn,
        /// cộng <c>MrpOptionBufferPct</c> khi ≥ 2 lựa chọn (món chung mọi lựa chọn không vượt số suất dòng).</item>
        /// </list>
        /// <paramref name="menuLines"/>: MenuLine của thực đơn đúng thứ trong tuần. Không có món cho loại suất → danh sách rỗng.
        /// </summary>
        public static List<LinePortion> LinePortions(OrderLine line, IEnumerable<MenuLine> menuLines, BusinessRulesOptions rules, out bool optionMissing)
        {
            optionMissing = false;
            var result = new List<LinePortion>();
            if (line.MenuId == null)
            {
                return result;
            }
            var options = menuLines.Where(m => m.MenuId == line.MenuId && !m.IsDeleted
                    && string.Equals((m.MealType ?? string.Empty).Trim(), (line.MealType ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
                .GroupBy(m => m.SequenceNo is > 0 ? m.SequenceNo.Value : 1).ToList();
            if (options.Count == 0)
            {
                return result;
            }
            if (line.MenuOptionNo is int optionNo)
            {
                var option = options.FirstOrDefault(o => o.Key == optionNo);
                if (option != null)
                {
                    decimal rate = Math.Max(0m, rules.MrpOptionQtyBufferPct) / 100m;
                    foreach (var dish in option.GroupBy(m => m.DishId))
                    {
                        // Số phần món / suất = MenuLine.PortionQty (trống / ≤ 0 = 1) – vd bánh mì 2 cái / suất → 2.000 suất LC1 = 4.000 cái.
                        decimal exact = line.Quantity * dish.Sum(m => m.PortionQty is > 0 ? m.PortionQty.Value : 1m);
                        result.Add(new LinePortion(dish.Key, exact * (1 + rate), exact));
                    }
                    return result;
                }
                optionMissing = true;
            }
            decimal bufferRate = Math.Max(0m, rules.MrpOptionBufferPct) / 100m;
            decimal perOption = line.Quantity / options.Count;
            bool buffered = options.Count >= 2 && bufferRate > 0;
            foreach (var dish in options.SelectMany(o => o).GroupBy(m => m.DishId))
            {
                decimal even = perOption * dish.Count();
                decimal qty = buffered ? Math.Max(even, Math.Min(even * (1 + bufferRate), line.Quantity)) : even;
                result.Add(new LinePortion(dish.Key, qty, even));
            }
            return result;
        }

        /// <summary>BE2.1: nhu cầu 1 món của 1 dòng đơn (đã quy ca – dòng chưa gán ca = ca mặc định).</summary>
        public sealed record DishDemand(Guid OrderId, Guid OrderLineId, Guid ShiftId, Guid DishId, decimal Quantity);

        /// <summary>
        /// BE2.1 – suất từng món theo dòng đơn của ngày (cùng công thức MRP T14 / T14.1 và T17.1 SplitByShiftAsync): đơn ở các trạng thái <paramref name="statuses"/>,
        /// dòng đơn × thực đơn (MenuLine cùng loại suất, thứ của ngày), chia đều lựa chọn + dự phòng <c>MrpOptionBufferPct</c> (món chung mọi lựa chọn không vượt số suất dòng);
        /// ca = <c>OrderLine.ShiftId</c>, trống → <c>ProductionDefaultShiftCode</c>. Dòng không có thực đơn / thực đơn không có món cho thứ đó → bỏ qua (trả kèm danh sách dòng bỏ qua).
        /// </summary>
        public static async Task<(List<DishDemand> Demands, List<Order> Orders, List<OrderLine> Skipped, Shift? Fallback)> DishDemandsAsync(
            DatabaseContext db, DateTime date, IReadOnlyCollection<string> statuses, BusinessRulesOptions rules)
        {
            var fallback = await db.Shift.AsNoTracking().FirstOrDefaultAsync(s => s.Code == rules.ProductionDefaultShiftCode && !s.IsDeleted);
            DateTime day = date.Date, next = day.AddDays(1);
            var orders = await db.Order.AsNoTracking().Where(o => !o.IsDeleted && o.MealDate >= day && o.MealDate < next && statuses.Contains(o.Status!)).ToListAsync();
            var orderIds = orders.Select(o => o.Id).ToList();
            var orderLines = await db.OrderLine.AsNoTracking().Where(l => orderIds.Contains(l.OrderId) && !l.IsDeleted && l.Quantity > 0).ToListAsync();
            var menuIds = orderLines.Where(l => l.MenuId != null).Select(l => l.MenuId!.Value).Distinct().ToList();
            int dayOfWeek = MenuService.MenuDayOf(day);
            var menuLines = await db.MenuLine.AsNoTracking().Where(m => menuIds.Contains(m.MenuId) && !m.IsDeleted && m.DayOfWeek == dayOfWeek).ToListAsync();
            var demands = new List<DishDemand>();
            var skipped = new List<OrderLine>();
            foreach (var line in orderLines)
            {
                Guid? shiftId = line.ShiftId ?? fallback?.Id;
                var portions = LinePortions(line, menuLines, rules, out _);
                if (shiftId == null || portions.Count == 0)
                {
                    skipped.Add(line);
                    continue;
                }
                foreach (var p in portions)
                {
                    demands.Add(new DishDemand(line.OrderId, line.Id, shiftId.Value, p.DishId, p.Quantity));
                }
            }
            // BE2.2: đổi món đã duyệt (MenuChangeRequest) áp lên nhu cầu – kế hoạch sinh lại / chia mẻ / truy xuất không mất đổi món.
            var changes = demands.Count == 0 ? [] : await ApprovedMenuChangesAsync(db, day);
            if (changes.Count != 0)
            {
                demands = ApplyMenuChanges(demands, changes);
            }
            return (demands, orders, skipped, fallback);
        }

        /// <summary>BE2.2 (spec 6.4): yêu cầu đổi món Đã duyệt – đổi <see cref="Quantity"/> suất (null = tất cả) món cũ → món mới, trong phạm vi đơn / ca nếu có.</summary>
        public sealed record MenuChange(Guid? OrderId, Guid? ShiftId, Guid OriginalDishId, Guid RequestedDishId, decimal? Quantity);

        /// <summary>BE2.2: yêu cầu đổi món Đã duyệt (MenuChangeRequest.RequestStatus = Approved) của ngày ăn, theo thứ tự duyệt.</summary>
        public static async Task<List<MenuChange>> ApprovedMenuChangesAsync(DatabaseContext db, DateTime date)
        {
            DateTime day = date.Date, next = day.AddDays(1);
            var rows = await db.MenuChangeRequest.AsNoTracking()
                .Where(r => !r.IsDeleted && r.RequestStatus == MenuChangeStatus.Approved && r.MealDate >= day && r.MealDate < next
                    && r.OriginalDishId != null && r.RequestedDishId != null)
                .ToListAsync();
            return [.. rows.OrderBy(r => r.ApprovedAt).ThenBy(r => r.RequestNo, StringComparer.Ordinal)
                .Select(r => new MenuChange(r.OrderId, r.ShiftId, r.OriginalDishId!.Value, r.RequestedDishId!.Value, r.Quantity))];
        }

        /// <summary>
        /// BE2.2 (spec 6.4): áp yêu cầu đổi món đã duyệt lên nhu cầu món theo dòng đơn – lần lượt từng yêu cầu: lấy tối đa <c>Quantity</c> suất của món cũ
        /// (trong đơn / ca của yêu cầu nếu có, theo thứ tự dòng) chuyển sang món mới cùng đơn / dòng / ca. Không đủ suất món cũ → chuyển phần đang có (PROPOSED – D-51).
        /// Dùng chung cho kế hoạch sinh tự động, MRP, chia mẻ đóng gói, truy xuất → đổi món không bị mất khi tính lại.
        /// </summary>
        public static List<DishDemand> ApplyMenuChanges(IReadOnlyList<DishDemand> demands, IReadOnlyCollection<MenuChange> changes)
        {
            var result = demands.ToList();
            foreach (var change in changes)
            {
                decimal remaining = change.Quantity ?? decimal.MaxValue;
                int count = result.Count;
                for (int i = 0; i < count && remaining > 0; i++)
                {
                    var d = result[i];
                    if (d.DishId != change.OriginalDishId || d.Quantity <= 0 || (change.OrderId != null && d.OrderId != change.OrderId)
                        || (change.ShiftId != null && d.ShiftId != change.ShiftId))
                    {
                        continue;
                    }
                    decimal moved = Math.Min(d.Quantity, remaining);
                    remaining -= moved;
                    result[i] = d with { Quantity = d.Quantity - moved };
                    result.Add(d with { DishId = change.RequestedDishId, Quantity = moved });
                }
            }
            return [.. result.Where(d => d.Quantity > 0)];
        }

        /// <summary>
        /// Lệnh SX hoàn thành khi mọi mẻ (kể cả mẻ đang xử lý) đã đóng gói / không đạt (ActualQty = Σ số đạt của mẻ đã đóng gói); kế hoạch Đã phát hành hoàn thành
        /// khi mọi lệnh hoàn thành. Không lưu (service gọi CommitAsync). T17 – chuyển từ ProductionBatchService để PackingOrderService dùng chung (BE2.1).
        /// </summary>
        public static async Task CompleteOrderIfDoneAsync(Data.Interface.IUnitOfWork uow, DateTime now, ProductionBatch current, ProductionOrder order)
        {
            var db = uow.DbContext;
            var others = await db.ProductionBatch.AsNoTracking().Where(b => b.ProductionOrderId == order.Id && !b.IsDeleted && b.Id != current.Id).ToListAsync();
            var all = others.Append(current).ToList();
            if (!all.All(b => ProductionBatchStatus.Closed.Contains(b.Status)))
            {
                return;
            }
            order.Status = ProductionOrderStatus.Completed;
            order.ActualQty = all.Where(b => b.Status == ProductionBatchStatus.Packed).Sum(b => b.AcceptedQty ?? b.ActualQuantity ?? b.ActualQty ?? 0m);
            order.ActualEndDate = now;
            uow.ProductionOrderRepository.Update(order);
            var plan = uow.ProductionPlanRepository.GetSingle(p => p.Id == order.ProductionPlanId && !p.IsDeleted);
            if (plan == null || plan.Status != ProductionPlanStatus.Released)
            {
                return;
            }
            bool otherOrdersDone = !await db.ProductionOrder.AsNoTracking()
                .AnyAsync(o => o.ProductionPlanId == plan.Id && !o.IsDeleted && o.Id != order.Id
                    && o.Status != ProductionOrderStatus.Completed && o.Status != ProductionOrderStatus.Closed && o.Status != ProductionOrderStatus.Cancelled);
            if (otherOrdersDone)
            {
                plan.Status = ProductionPlanStatus.Completed;
                uow.ProductionPlanRepository.Update(plan);
            }
        }

        public static string Day(DateTime date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        public static string Time(DateTime date) => date.ToString("HH:mm dd/MM", CultureInfo.InvariantCulture);
        public static string Num(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);
        public static decimal Round4(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);
        public static decimal Ceil4(decimal value) => Math.Ceiling(value * 10000m) / 10000m;

        /// <summary>
        /// Lịch ngược của 1 món (GAP-PRD-07 – anh chốt 2026-11-16): số mẻ N = ceil(phần ÷ năng lực mẻ) (năng lực trống = 1 mẻ), chia đều – số phần nguyên chia
        /// đều cho N mẻ, các mẻ đầu nhận phần dư (250 / 100 → 84, 83, 83), phần lẻ thập phân (nếu có) cộng vào mẻ cuối;
        /// các mẻ của món chạy nối tiếp, mẻ cuối kết thúc đúng <paramref name="cookEnd"/>; các món khác nhau chạy song song.
        /// </summary>
        public static List<ScheduledBatch> Schedule(decimal quantity, decimal? capacityPerBatch, int minutesPerBatch, DateTime cookEnd)
        {
            var result = new List<ScheduledBatch>();
            if (quantity <= 0)
            {
                return result;
            }
            decimal capacity = capacityPerBatch is > 0 ? capacityPerBatch.Value : quantity;
            int count = (int)Math.Ceiling(quantity / capacity);
            decimal whole = Math.Floor(quantity);
            decimal fraction = quantity - whole;
            decimal size = Math.Floor(whole / count);
            int extra = (int)(whole - size * count);
            for (int k = 1; k <= count; k++)
            {
                decimal qty = size + (k <= extra ? 1 : 0) + (k == count ? fraction : 0);
                DateTime end = cookEnd.AddMinutes(-(double)(minutesPerBatch * (count - k)));
                result.Add(new ScheduledBatch(k, Round4(qty), end.AddMinutes(-minutesPerBatch), end));
            }
            return result;
        }

        /// <summary>Phần của 1 ca trong kế hoạch ngày: ca + số phần từng dòng món (dòng kế hoạch) của ca.</summary>
        public sealed class ShiftPart
        {
            public Shift Shift { get; set; } = null!;
            public List<(ProductionPlanLine Line, decimal Quantity)> Lines { get; set; } = [];
            public decimal Portions => Lines.Sum(l => l.Quantity);
        }

        /// <summary>
        /// T17.1 (anh chốt 2026-11-16 – Pilot chạy 4 ca / ngày, kế hoạch MRP vẫn 1 KH- / ngày): chia số phần của từng dòng món theo ca.
        /// Tỉ lệ = suất của món theo ca, tính như MRP (T14 / T14.1) trên đơn Đã chốt / Đang SX của ngày: dòng đơn × thực đơn (MenuLine cùng loại suất, thứ của ngày),
        /// chia đều lựa chọn + dự phòng <c>MrpOptionBufferPct</c>; ca = <c>OrderLine.ShiftId</c>, dòng chưa gán ca → ca mặc định <c>ProductionDefaultShiftCode</c>.
        /// Số phần dòng kế hoạch (đã duyệt) chia theo tỉ lệ đó (làm tròn 4 số lẻ, ca cuối nhận phần còn lại) – tổng luôn bằng dòng kế hoạch.
        /// Món không có nhu cầu trong đơn (dòng thêm tay) → ca mặc định (PROPOSED – BA/PO CONFIRM). Ca sắp theo giờ bắt đầu rồi mã.
        /// </summary>
        public static async Task<(List<ShiftPart> Parts, Shift? Fallback, List<string> Warnings, string? Error)> SplitByShiftAsync(
            DatabaseContext db, DateTime date, List<ProductionPlanLine> planLines, BusinessRulesOptions rules)
        {
            var warnings = new List<string>();
            var fallback = await db.Shift.AsNoTracking().FirstOrDefaultAsync(s => s.Code == rules.ProductionDefaultShiftCode && !s.IsDeleted);
            DateTime next = date.Date.AddDays(1);
            var orderIds = await db.Order.AsNoTracking()
                .Where(o => !o.IsDeleted && o.MealDate >= date.Date && o.MealDate < next && (o.Status == OrderStatus.Locked || o.Status == OrderStatus.InProduction))
                .Select(o => o.Id).ToListAsync();
            var orderLines = await db.OrderLine.AsNoTracking().Where(l => orderIds.Contains(l.OrderId) && !l.IsDeleted && l.Quantity > 0 && l.MenuId != null).ToListAsync();
            var menuIds = orderLines.Select(l => l.MenuId!.Value).Distinct().ToList();
            int dayOfWeek = MenuService.MenuDayOf(date.Date);
            var menuLines = await db.MenuLine.AsNoTracking().Where(m => menuIds.Contains(m.MenuId) && !m.IsDeleted && m.DayOfWeek == dayOfWeek).ToListAsync();
            // weights[món][ca] = suất của món trong ca (cùng công thức MRP – ProductionRules.LinePortions).
            var weights = new Dictionary<Guid, Dictionary<Guid, decimal>>();
            int unassigned = 0;
            foreach (var line in orderLines)
            {
                Guid? shiftId = line.ShiftId ?? fallback?.Id;
                var portions = LinePortions(line, menuLines, rules, out _);
                if (shiftId == null || portions.Count == 0)
                {
                    continue;
                }
                if (line.ShiftId == null)
                {
                    unassigned++;
                }
                foreach (var p in portions)
                {
                    if (!weights.TryGetValue(p.DishId, out var byShift))
                    {
                        byShift = [];
                        weights[p.DishId] = byShift;
                    }
                    byShift[shiftId.Value] = byShift.GetValueOrDefault(shiftId.Value) + p.Quantity;
                }
            }
            if (unassigned > 0 && fallback != null)
            {
                warnings.Add(string.Format(ConstantHelper.Message.ReleaseLinesWithoutShift, unassigned, fallback.Code));
            }

            var split = new Dictionary<Guid, List<(ProductionPlanLine Line, decimal Quantity)>>();
            foreach (var planLine in planLines.Where(l => l.PlannedQuantity > 0))
            {
                var byShift = weights.GetValueOrDefault(planLine.DishId)?.Where(x => x.Value > 0).OrderBy(x => x.Key).ToList() ?? [];
                if (byShift.Count == 0)
                {
                    if (fallback == null)
                    {
                        return ([], null, warnings, ConstantHelper.Message.ShiftNotFound);
                    }
                    byShift = [new(fallback.Id, 1m)];
                }
                decimal total = byShift.Sum(x => x.Value), given = 0m;
                for (int i = 0; i < byShift.Count; i++)
                {
                    decimal qty = i == byShift.Count - 1 ? planLine.PlannedQuantity - given : Round4(planLine.PlannedQuantity * byShift[i].Value / total);
                    given += qty;
                    if (qty <= 0)
                    {
                        continue;
                    }
                    if (!split.TryGetValue(byShift[i].Key, out var list))
                    {
                        list = [];
                        split[byShift[i].Key] = list;
                    }
                    list.Add((planLine, qty));
                }
            }
            var shiftIds = split.Keys.ToList();
            var shifts = await db.Shift.AsNoTracking().Where(s => shiftIds.Contains(s.Id)).ToListAsync();
            var parts = shifts.OrderBy(s => s.StartTime ?? TimeSpan.MaxValue).ThenBy(s => s.Code)
                .Select(s => new ShiftPart { Shift = s, Lines = split[s.Id] }).ToList();
            return (parts, fallback, warnings, null);
        }

        /// <summary>
        /// Công thức dùng cho món tại ngày sản xuất – cùng thứ tự với MRP (T14): bản Locked / Obsolete đã khóa hiệu lực tại ngày → Locked + IsActive
        /// → Approved mới nhất (cảnh báo). Trả ảnh chụp định lượng: SnapshotJson của bản khóa, không có thì dòng RecipeLine hiện tại.
        /// </summary>
        public static async Task<(Recipe? Recipe, bool Locked, List<SnapshotLine> Lines)> SelectRecipeAsync(DatabaseContext db, Guid dishId, DateTime date)
        {
            var mine = await db.Recipe.AsNoTracking()
                .Where(r => r.DishId == dishId && !r.IsDeleted
                    && (r.Status == RecipeStatus.Locked || r.Status == RecipeStatus.Obsolete || r.Status == RecipeStatus.Approved))
                .ToListAsync();
            var recipe = mine.Where(r => r.Status is RecipeStatus.Locked or RecipeStatus.Obsolete && r.LockedAt != null
                        && (r.EffectiveFrom == null || r.EffectiveFrom.Value.Date <= date) && (r.EffectiveTo == null || r.EffectiveTo.Value.Date >= date))
                    .OrderByDescending(r => r.Status == RecipeStatus.Locked).ThenByDescending(r => r.EffectiveFrom).ThenByDescending(r => r.VersionNo).FirstOrDefault()
                ?? mine.Where(r => r.Status == RecipeStatus.Locked && r.IsActive).OrderByDescending(r => r.VersionNo).FirstOrDefault();
            bool locked = recipe != null;
            recipe ??= mine.Where(r => r.Status == RecipeStatus.Approved).OrderByDescending(r => r.VersionNo).FirstOrDefault();
            if (recipe == null)
            {
                return (null, false, []);
            }
            var lines = locked ? ParseSnapshot(recipe.SnapshotJson) : [];
            if (lines.Count == 0)
            {
                lines = await db.RecipeLine.AsNoTracking().Where(l => l.RecipeId == recipe.Id && !l.IsDeleted)
                    .Select(l => new SnapshotLine { IngredientId = l.IngredientId, Quantity = l.Quantity, UnitId = l.UnitId != Guid.Empty ? l.UnitId : l.UomId, WastePercent = l.WastePercent, ExpectedWasteRate = l.ExpectedWasteRate, SequenceNo = l.SequenceNo })
                    .ToListAsync();
            }
            return (recipe, locked, lines);
        }

        public static List<SnapshotLine> ParseSnapshot(string? json)
        {
            if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith('['))
            {
                return [];
            }
            try
            {
                return JsonConvert.DeserializeObject<List<SnapshotLine>>(json) ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }

        /// <summary>
        /// Giờ xuất bếp của (ngày, ca) – BUSINESS_RULES §6: giờ chốt giao của điểm giao − <c>TransportMinutesBeforeCutoff</c> (60′).
        /// Giờ chốt giao = giờ giao sớm nhất (<c>CustomerDeliveryLocation.DeliveryWindowFrom</c>) của các đơn Đã chốt / Đang SX trong ngày có dòng thuộc ca
        /// (dòng chưa gán ca thuộc ca mặc định <paramref name="fallbackShiftId"/>); không có thì giờ bắt đầu ca (<c>Shift.StartTime</c>) − 60′ – anh chốt 2026-11-16 (GAP-PRD-07).
        /// Nhập tay <paramref name="dispatchTime"/> → dùng luôn.
        /// </summary>
        public static async Task<(DateTime? DispatchAt, string Source)> ResolveDispatchAsync(DatabaseContext db, DateTime date, Shift shift, TimeSpan? dispatchTime, BusinessRulesOptions rules, Guid? fallbackShiftId = null)
        {
            if (dispatchTime != null)
            {
                return (date.Date + dispatchTime.Value, "Nhập tay");
            }
            DateTime next = date.Date.AddDays(1);
            var orders = await db.Order.AsNoTracking()
                .Where(o => !o.IsDeleted && o.MealDate >= date.Date && o.MealDate < next && (o.Status == OrderStatus.Locked || o.Status == OrderStatus.InProduction))
                .Select(o => new { o.Id, o.DeliveryLocationId }).ToListAsync();
            var orderIds = orders.Select(o => o.Id).ToList();
            // T17.1: dòng chưa gán ca thuộc ca mặc định (không còn tính cho mọi ca). Đơn có dòng ở nhiều ca: điểm giao chỉ có 1 khung giờ
            // → khung giờ đó không dùng cho ca nào (về giờ bắt đầu ca) – PROPOSED – BA/PO CONFIRM (GAP-PRD-01).
            var inShift = (await db.OrderLine.AsNoTracking()
                    .Where(l => orderIds.Contains(l.OrderId) && !l.IsDeleted && l.Quantity > 0)
                    .Select(l => new { l.OrderId, l.ShiftId }).ToListAsync())
                .GroupBy(l => l.OrderId)
                .Where(g => g.Select(l => l.ShiftId ?? fallbackShiftId).Distinct().Count() == 1 && (g.First().ShiftId ?? fallbackShiftId) == shift.Id)
                .Select(g => g.Key).ToHashSet();
            var locationIds = orders.Where(o => inShift.Contains(o.Id) && o.DeliveryLocationId != null).Select(o => o.DeliveryLocationId!.Value).Distinct().ToList();
            var windows = await db.CustomerDeliveryLocation.AsNoTracking()
                .Where(l => locationIds.Contains(l.Id) && l.DeliveryWindowFrom != null).Select(l => l.DeliveryWindowFrom!.Value).ToListAsync();
            if (windows.Count != 0)
            {
                return (date.Date + windows.Min() - TimeSpan.FromMinutes(rules.TransportMinutesBeforeCutoff), "Giờ giao sớm nhất của điểm giao");
            }
            if (shift.StartTime != null)
            {
                return (date.Date + shift.StartTime.Value - TimeSpan.FromMinutes(rules.TransportMinutesBeforeCutoff), "Giờ bắt đầu ca");
            }
            return (null, string.Empty);
        }

        /// <summary>Phiếu xuất của mẻ: MaterialIssue của lệnh SX có ghi số mẻ trong Note (không có cột ProductionBatchId – GAP-PRD-04).</summary>
        public static IQueryable<MaterialIssue> IssuesOf(DatabaseContext db, ProductionBatch batch)
        {
            string number = batch.BatchNumber ?? batch.BatchNo ?? batch.Id.ToString();
            return db.MaterialIssue.AsNoTracking().Where(i => !i.IsDeleted && i.ProductionOrderId == batch.ProductionOrderId && i.Note != null && i.Note.Contains(number));
        }

        /// <summary>Nhiệt độ lõi đo gần nhất của mẻ (TemperatureLog CORE – sổ chỉ ghi thêm, lần đo sau thay lần trước).</summary>
        public static Task<TemperatureLog?> LastCoreAsync(DatabaseContext db, Guid batchId)
            => db.TemperatureLog.AsNoTracking()
                .Where(t => !t.IsDeleted && t.ProductionBatchId == batchId && t.MeasurementType == ProductionCode.CoreMeasurement)
                .OrderByDescending(t => t.MeasuredAt).ThenByDescending(t => t.CreatedDate).FirstOrDefaultAsync();

        /// <summary>Mục checklist SAMPLE_RETAINED (ưu tiên bộ PRD_BATCH đang hoạt động).</summary>
        public static async Task<FoodSafetyChecklistItem?> SampleItemAsync(DatabaseContext db)
        {
            var items = await db.FoodSafetyChecklistItem.AsNoTracking().Where(i => !i.IsDeleted && i.ItemCode == ProductionCode.SampleRetainedItem).ToListAsync();
            if (items.Count == 0)
            {
                return null;
            }
            var checklistIds = items.Select(i => i.ChecklistId).ToList();
            var checklists = await db.FoodSafetyChecklist.AsNoTracking().Where(c => checklistIds.Contains(c.Id) && !c.IsDeleted).ToDictionaryAsync(c => c.Id);
            return items.Where(i => checklists.ContainsKey(i.ChecklistId))
                .OrderByDescending(i => checklists[i.ChecklistId].Code == ProductionCode.BatchChecklist)
                .ThenByDescending(i => checklists[i.ChecklistId].Status != "Inactive")
                .ThenByDescending(i => checklists[i.ChecklistId].VersionNo)
                .FirstOrDefault();
        }

        public static async Task<List<FoodSafetyChecklistResult>> SamplesAsync(DatabaseContext db, Guid batchId)
        {
            var itemIds = await db.FoodSafetyChecklistItem.AsNoTracking().Where(i => i.ItemCode == ProductionCode.SampleRetainedItem).Select(i => i.Id).ToListAsync();
            return await db.FoodSafetyChecklistResult.AsNoTracking()
                .Where(r => !r.IsDeleted && r.ProductionBatchId == batchId && itemIds.Contains(r.ChecklistItemId) && r.ResultCode == ProductionCode.ResultPass)
                .OrderBy(r => r.CheckedAt).ToListAsync();
        }

        /// <summary>
        /// Điều kiện ATTP để mẻ đạt QC / đóng gói (BUSINESS_RULES §6): nhiệt độ lõi đo gần nhất ≥ <c>CoreTempMinC</c> (70 °C);
        /// đã lưu mẫu (nếu <c>ProductionRequireSampleBeforeRelease</c>); đã ghi sản lượng (khi <paramref name="needOutput"/>).
        /// </summary>
        public static async Task<List<string>> SafetyBlockersAsync(DatabaseContext db, ProductionBatch batch, BusinessRulesOptions rules, bool needOutput)
        {
            var blockers = new List<string>();
            var core = await LastCoreAsync(db, batch.Id);
            if (core == null)
            {
                blockers.Add(ConstantHelper.Message.CoreTempMissing);
            }
            else if (core.TemperatureC < rules.CoreTempMinC)
            {
                blockers.Add(string.Format(ConstantHelper.Message.CoreTempBelow, Num(core.TemperatureC), Num(rules.CoreTempMinC)));
            }
            if (rules.ProductionRequireSampleBeforeRelease && (await SamplesAsync(db, batch.Id)).Count == 0)
            {
                blockers.Add(ConstantHelper.Message.SampleMissing);
            }
            if (needOutput && !await db.ProductionOutput.AsNoTracking().AnyAsync(o => !o.IsDeleted && o.ProductionBatchId == batch.Id))
            {
                blockers.Add(ConstantHelper.Message.OutputMissing);
            }
            // BE2.5 (spec 9.2, PROPOSED D-54 d): tiêu chí ATTP trọng yếu KHÔNG ĐẠT gắn mẻ (kết quả mới nhất) → chặn tới khi kiểm lại ĐẠT.
            if (rules.FoodSafetyCriticalFailHoldsBatch
                && (await FoodSafetyRules.OpenCriticalFailuresAsync(db, [batch.Id])).TryGetValue(batch.Id, out var critical))
            {
                blockers.Add(string.Format(ConstantHelper.Message.CriticalFailOpen, string.Join(", ", critical)));
            }
            return blockers;
        }

        /// <summary>
        /// Nguyên liệu của mẻ = định lượng (ảnh chụp trong lệnh SX) × phần kế hoạch của mẻ ÷ suất chuẩn công thức (StandardOutputQty, trống = BusinessRules 100)
        /// → lượng mua theo hao hụt 2 tầng (WasteRules – BIZ-1.1) – cùng công thức MRP. Số đã xuất = phiếu xuất Đã xuất của mẻ.
        /// </summary>
        public static async Task<List<ProductionMaterialModel>> MaterialsAsync(DatabaseContext db, ProductionOrder order, ProductionBatch batch, BusinessRulesOptions rules)
        {
            var lines = ParseSnapshot(order.RecipeSnapshotJson);
            var recipe = await db.Recipe.AsNoTracking().FirstOrDefaultAsync(r => r.Id == order.RecipeId);
            decimal servings = recipe?.StandardOutputQty is > 0 ? recipe.StandardOutputQty.Value : rules.RecipeStandardServings;
            decimal portions = batch.PlannedQuantity ?? batch.PlannedQty ?? 0m;
            var ingredientIds = lines.Select(l => l.IngredientId).Distinct().ToList();
            var ingredients = await db.Ingredient.AsNoTracking().Where(i => ingredientIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id);
            var units = await db.UnitMeasure.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Name ?? u.Code);
            var issueIds = await IssuesOf(db, batch).Where(i => i.Status == MaterialIssueStatus.Issued).Select(i => i.Id).ToListAsync();
            var issued = await db.MaterialIssueLine.AsNoTracking().Where(l => issueIds.Contains(l.MaterialIssueId) && !l.IsDeleted).ToListAsync();
            var result = new List<ProductionMaterialModel>();
            foreach (var group in lines.Where(l => l.Quantity > 0).GroupBy(l => (l.IngredientId, l.UnitId)))
            {
                var ingredient = ingredients.GetValueOrDefault(group.Key.IngredientId);
                var first = group.First();
                decimal waste = first.WastePercent ?? ingredient?.StandardWastePercent ?? 0m;
                decimal perServing = group.Sum(l => l.Quantity) / servings;
                var mineIssued = issued.Where(l => l.IngredientId == group.Key.IngredientId).ToList();
                result.Add(new ProductionMaterialModel
                {
                    IngredientId = group.Key.IngredientId,
                    IngredientCode = ingredient?.Code,
                    IngredientName = ingredient?.Name,
                    QuantityPerServing = Round4(perServing),
                    RecipeUnitId = group.Key.UnitId,
                    RecipeUnitName = units.GetValueOrDefault(group.Key.UnitId),
                    WastePercent = waste,
                    RequiredQuantity = Ceil4(group.Sum(l => WasteRules.Gross(l.Quantity * portions / servings, l.WastePercent ?? ingredient?.StandardWastePercent, l.ExpectedWasteRate))),
                    IssuedQuantity = mineIssued.Sum(l => l.Quantity),
                    IssuedUnitName = mineIssued.Count == 0 ? null : units.GetValueOrDefault(mineIssued[0].UnitId),
                });
            }
            return [.. result.OrderBy(m => m.IngredientName)];
        }

        /// <summary>Thông tin tóm tắt lệnh SX (món, công thức, ca, số mẻ).</summary>
        public static async Task<List<ProductionOrderBriefModel>> OrderBriefsAsync(DatabaseContext db, List<ProductionOrder> orders)
        {
            var ids = orders.Select(o => o.Id).ToList();
            var dishIds = orders.Select(o => o.DishId).Distinct().ToList();
            var recipeIds = orders.Select(o => o.RecipeId).Distinct().ToList();
            var dishes = await db.Dish.AsNoTracking().Where(d => dishIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name);
            var recipes = await db.Recipe.AsNoTracking().Where(r => recipeIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id);
            var shifts = await db.Shift.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Code);
            var batchCounts = await db.ProductionBatch.AsNoTracking().Where(b => ids.Contains(b.ProductionOrderId) && !b.IsDeleted)
                .GroupBy(b => b.ProductionOrderId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
            return [.. orders.Select(o => new ProductionOrderBriefModel
            {
                Id = o.Id,
                ProductionOrderNumber = o.ProductionOrderNumber ?? o.ProductionOrderNo,
                DishId = o.DishId,
                DishName = dishes.GetValueOrDefault(o.DishId),
                RecipeId = o.RecipeId,
                RecipeVersion = recipes.GetValueOrDefault(o.RecipeId) is { } r ? r.VersionCode ?? "v" + r.VersionNo.ToString(CultureInfo.InvariantCulture) : null,
                ShiftId = o.ShiftId,
                ShiftCode = o.ShiftId != null ? shifts.GetValueOrDefault(o.ShiftId.Value) : null,
                ProductionLineId = o.ProductionLineId,
                PlannedQuantity = o.PlannedQty,
                ActualQuantity = o.ActualQty,
                PlannedStartDate = o.PlannedStartDate,
                PlannedEndDate = o.EndTime,
                Status = o.Status,
                StatusName = ProductionOrderStatus.Label(o.Status),
                BatchCount = batchCounts.GetValueOrDefault(o.Id),
            }).OrderBy(o => o.PlannedStartDate).ThenBy(o => o.DishName)];
        }

        /// <summary>Thông tin tóm tắt mẻ cho bảng điều độ: giờ kế hoạch / thực tế, nhiệt độ lõi gần nhất, lưu mẫu, đã xuất NL.</summary>
        public static async Task<List<ProductionBatchBriefModel>> BatchBriefsAsync(DatabaseContext db, List<ProductionBatch> batches, BusinessRulesOptions rules)
        {
            var ids = batches.Select(b => b.Id).ToList();
            var orderIds = batches.Select(b => b.ProductionOrderId).Distinct().ToList();
            var orders = await db.ProductionOrder.AsNoTracking().Where(o => orderIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id);
            var dishIds = batches.Select(b => b.DishId).Distinct().ToList();
            var dishes = await db.Dish.AsNoTracking().Where(d => dishIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name);
            var shifts = await db.Shift.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Code);
            var lines = await db.ProductionLine.AsNoTracking().ToDictionaryAsync(l => l.Id, l => l.Name);
            var temps = (await db.TemperatureLog.AsNoTracking()
                    .Where(t => !t.IsDeleted && t.ProductionBatchId != null && ids.Contains(t.ProductionBatchId.Value) && t.MeasurementType == ProductionCode.CoreMeasurement)
                    .ToListAsync())
                .GroupBy(t => t.ProductionBatchId!.Value)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.MeasuredAt).ThenByDescending(t => t.CreatedDate).First());
            var sampleItemIds = await db.FoodSafetyChecklistItem.AsNoTracking().Where(i => i.ItemCode == ProductionCode.SampleRetainedItem).Select(i => i.Id).ToListAsync();
            var sampled = (await db.FoodSafetyChecklistResult.AsNoTracking()
                    .Where(r => !r.IsDeleted && r.ProductionBatchId != null && ids.Contains(r.ProductionBatchId.Value) && sampleItemIds.Contains(r.ChecklistItemId) && r.ResultCode == ProductionCode.ResultPass)
                    .Select(r => r.ProductionBatchId!.Value).ToListAsync()).ToHashSet();
            var issues = await db.MaterialIssue.AsNoTracking()
                .Where(i => !i.IsDeleted && orderIds.Contains(i.ProductionOrderId) && i.Status == MaterialIssueStatus.Issued && i.Note != null)
                .Select(i => new { i.ProductionOrderId, i.Note }).ToListAsync();
            return [.. batches.Select(b =>
            {
                var order = orders.GetValueOrDefault(b.ProductionOrderId);
                var core = temps.GetValueOrDefault(b.Id);
                string number = b.BatchNumber ?? b.BatchNo ?? b.Id.ToString();
                return new ProductionBatchBriefModel
                {
                    Id = b.Id,
                    PriorityLabel = order?.Note?.Contains("[BO_SUNG:", StringComparison.Ordinal) == true
                        ? (order.Note.Contains("Đơn bù", StringComparison.Ordinal) ? "🔴 Đơn bù" : "🔴 Bổ sung sau chốt") : null,
                    BatchNumber = b.BatchNumber ?? b.BatchNo,
                    ProductionOrderId = b.ProductionOrderId,
                    ProductionOrderNumber = order?.ProductionOrderNumber ?? order?.ProductionOrderNo,
                    DishId = b.DishId,
                    DishName = dishes.GetValueOrDefault(b.DishId),
                    ShiftId = order?.ShiftId,
                    ShiftCode = order?.ShiftId != null ? shifts.GetValueOrDefault(order.ShiftId.Value) : null,
                    ProductionLineId = order?.ProductionLineId,
                    ProductionLineName = order?.ProductionLineId != null ? lines.GetValueOrDefault(order.ProductionLineId.Value) : null,
                    PlannedQuantity = b.PlannedQuantity ?? b.PlannedQty ?? 0m,
                    ActualQuantity = b.ActualQuantity ?? b.ActualQty,
                    AcceptedQuantity = b.AcceptedQty,
                    PlannedStart = b.StartDate,
                    PlannedEnd = b.EndDate,
                    ActualStart = b.StartTime,
                    ActualEnd = b.EndTime,
                    Status = b.Status,
                    StatusName = ProductionBatchStatus.Label(b.Status),
                    QcStatus = b.QcStatus ?? b.QualityStatus,
                    LastCoreTemperatureC = core?.TemperatureC,
                    CoreTemperatureOk = core == null ? null : core.TemperatureC >= rules.CoreTempMinC,
                    SampleRetained = sampled.Contains(b.Id),
                    MaterialIssued = issues.Any(i => i.ProductionOrderId == b.ProductionOrderId && i.Note!.Contains(number)),
                };
            }).OrderBy(b => b.PlannedStart).ThenBy(b => b.BatchNumber)];
        }

        /// <summary>1 nhóm in trên Phiếu sản xuất: 1 mẻ + nguyên liệu.</summary>
        public sealed class SheetBatch
        {
            public string? ShiftName { get; set; }
            public string? DishName { get; set; }
            public string? BatchNumber { get; set; }
            public decimal PlannedQuantity { get; set; }
            public decimal? ActualQuantity { get; set; }
            public DateTime PlannedStart { get; set; }
            public DateTime? PlannedEnd { get; set; }
            public List<ProductionMaterialModel> Materials { get; set; } = [];
        }

        /// <summary>
        /// T17 – mẫu in "Phiếu sản xuất" (.xlsx, ClosedXML) theo bố cục file thật <c>Menu/Phieu san xuat.xlsx</c> (BUSINESS_DOCS.md – chỉ lấy cấu trúc tiêu đề,
        /// không chép dữ liệu): tên pháp nhân, "Số suất order" / "Số suất thực tế", PHIẾU SẢN XUẤT, Thứ – ngày; bảng Buổi | Tên món | Tên nguyên liệu | ĐVT |
        /// Số lượng (Định mức / SL cần / Thực xuất / Xuất thêm / Lý do) | SC ký nhận | Sơ chế (SL sau sơ chế / Tiêu hao / Trả về) | Bếp ký nhận | Ghi chú.
        /// Định mức theo đơn vị công thức (cột ĐVT), thực xuất theo đơn vị tồn kho (ghi chú nếu khác).
        /// </summary>
        public static byte[] BuildProductionSheet(string? companyName, DateTime productionDate, string subtitle, List<SheetBatch> batches)
        {
            const string brand = "#91AF42";
            const int lastCol = 15;
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Phieu san xuat");
            ws.Cell(1, 1).Value = companyName ?? "DELICARE";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 11).Value = "Số suất order:";
            ws.Cell(1, 13).Value = batches.Sum(b => b.PlannedQuantity);
            ws.Cell(2, 11).Value = "Số suất thực tế:";
            if (batches.Any(b => b.ActualQuantity != null))
            {
                ws.Cell(2, 13).Value = batches.Sum(b => b.ActualQuantity ?? 0m);
            }
            ws.Range(3, 1, 3, lastCol).Merge().Value = "PHIẾU SẢN XUẤT";
            ws.Range(3, 1, 3, lastCol).Style.Font.SetBold().Font.SetFontSize(16).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            int dow = MenuService.MenuDayOf(productionDate);
            string thu = dow == 7 ? "Chủ nhật" : "Thứ " + (dow + 1).ToString(CultureInfo.InvariantCulture);
            ws.Range(4, 1, 4, lastCol).Merge().Value = $"{thu}, ngày {productionDate:dd} tháng {productionDate:MM} năm {productionDate:yyyy} – {subtitle}";
            ws.Range(4, 1, 4, lastCol).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center).Font.SetItalic();

            int head = 6;
            ws.Range(head, 1, head + 1, 1).Merge().Value = "Buổi";
            ws.Range(head, 2, head + 1, 2).Merge().Value = "Tên món";
            ws.Range(head, 3, head + 1, 3).Merge().Value = "Tên nguyên liệu";
            ws.Range(head, 4, head + 1, 4).Merge().Value = "ĐVT";
            ws.Range(head, 5, head, 9).Merge().Value = "Số lượng";
            ws.Cell(head + 1, 5).Value = "Định mức\n(/suất)";
            ws.Cell(head + 1, 6).Value = "SL cần";
            ws.Cell(head + 1, 7).Value = "Thực xuất";
            ws.Cell(head + 1, 8).Value = "Xuất thêm";
            ws.Cell(head + 1, 9).Value = "Lý do";
            ws.Range(head, 10, head + 1, 10).Merge().Value = "SC ký nhận";
            ws.Range(head, 11, head, 13).Merge().Value = "Sơ chế";
            ws.Cell(head + 1, 11).Value = "Số lượng\nsau sơ chế";
            ws.Cell(head + 1, 12).Value = "Tiêu hao";
            ws.Cell(head + 1, 13).Value = "Trả về";
            ws.Range(head, 14, head + 1, 14).Merge().Value = "Bếp ký nhận";
            ws.Range(head, 15, head + 1, 15).Merge().Value = "Ghi chú";
            ws.Range(head, 1, head + 1, lastCol).Style.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(XLColor.FromHtml(brand))
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center).Alignment.SetVertical(XLAlignmentVerticalValues.Center).Alignment.SetWrapText(true);

            int row = head + 1;
            foreach (var batch in batches)
            {
                int start = row + 1;
                var materials = batch.Materials.Count == 0 ? [new ProductionMaterialModel()] : batch.Materials;
                foreach (var m in materials)
                {
                    row++;
                    ws.Cell(row, 3).Value = m.IngredientName ?? string.Empty;
                    ws.Cell(row, 4).Value = m.RecipeUnitName ?? string.Empty;
                    if (m.IngredientId != Guid.Empty)
                    {
                        ws.Cell(row, 5).Value = m.QuantityPerServing;
                        ws.Cell(row, 6).Value = m.RequiredQuantity;
                        if (m.IssuedQuantity != 0)
                        {
                            ws.Cell(row, 7).Value = m.IssuedQuantity;
                        }
                        if (m.IssuedUnitName != null && m.IssuedUnitName != m.RecipeUnitName)
                        {
                            ws.Cell(row, 15).Value = $"Thực xuất theo {m.IssuedUnitName}";
                        }
                    }
                }
                ws.Range(start, 1, row, 1).Merge().Value = batch.ShiftName ?? string.Empty;
                ws.Range(start, 2, row, 2).Merge().Value = $"{batch.DishName}\n{batch.BatchNumber} – {Num(batch.PlannedQuantity)} phần\n"
                    + $"{batch.PlannedStart:HH:mm}" + (batch.PlannedEnd != null ? $"–{batch.PlannedEnd:HH:mm}" : string.Empty);
                ws.Range(start, 1, row, 2).Style.Alignment.SetVertical(XLAlignmentVerticalValues.Top).Alignment.SetWrapText(true);
            }
            ws.Range(head, 1, row, lastCol).Style.Border.SetInsideBorder(XLBorderStyleValues.Thin).Border.SetOutsideBorder(XLBorderStyleValues.Thin);
            if (row > head + 1)
            {
                ws.Range(head + 2, 5, row, 8).Style.NumberFormat.Format = "#,##0.####";
            }
            row += 2;
            ws.Range(row, 1, row, 4).Merge().Value = "Người lập phiếu";
            ws.Range(row, 6, row, 9).Merge().Value = "Thủ kho";
            ws.Range(row, 11, row, 15).Merge().Value = "Bếp trưởng";
            ws.Range(row, 1, row, lastCol).Style.Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            ws.Column(1).Width = 10;
            ws.Column(2).Width = 26;
            ws.Column(3).Width = 28;
            ws.Column(4).Width = 8;
            for (int c = 5; c <= 14; c++)
            {
                ws.Column(c).Width = 11;
            }
            ws.Column(9).Width = 16;
            ws.Column(15).Width = 24;
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.FitToPages(1, 0);
            using var stream = new MemoryStream();
            wb.SaveAs(stream);
            return stream.ToArray();
        }

        /// <summary>Dữ liệu in Phiếu sản xuất cho các mẻ (theo giờ bắt đầu): ca, món, số mẻ, nguyên liệu cần / đã xuất.</summary>
        public static async Task<List<SheetBatch>> SheetBatchesAsync(DatabaseContext db, List<ProductionOrder> orders, List<ProductionBatch> batches, BusinessRulesOptions rules)
        {
            var shifts = await db.Shift.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name ?? s.Code);
            var dishIds = batches.Select(b => b.DishId).Distinct().ToList();
            var dishes = await db.Dish.AsNoTracking().Where(d => dishIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name);
            var result = new List<SheetBatch>();
            foreach (var batch in batches.OrderBy(b => b.StartDate).ThenBy(b => b.BatchNumber))
            {
                var order = orders.First(o => o.Id == batch.ProductionOrderId);
                result.Add(new SheetBatch
                {
                    ShiftName = order.ShiftId != null ? shifts.GetValueOrDefault(order.ShiftId.Value) : null,
                    DishName = dishes.GetValueOrDefault(batch.DishId),
                    BatchNumber = batch.BatchNumber ?? batch.BatchNo,
                    PlannedQuantity = batch.PlannedQuantity ?? batch.PlannedQty ?? 0m,
                    ActualQuantity = batch.ActualQuantity ?? batch.ActualQty,
                    PlannedStart = batch.StartDate,
                    PlannedEnd = batch.EndDate,
                    Materials = await MaterialsAsync(db, order, batch, rules),
                });
            }
            return result;
        }

        /// <summary>Cộng số thứ tự vào mã đã sinh (NN / NNNN cuối mã) – sinh nhiều mã trong cùng transaction (khóa sp_getapplock giữ tới khi commit).</summary>
        public static string Increment(string number, int add)
        {
            int dash = number.LastIndexOf('-');
            string tail = number[(dash + 1)..];
            int n = int.Parse(tail, CultureInfo.InvariantCulture) + add;
            return number[..(dash + 1)] + n.ToString(new string('0', tail.Length), CultureInfo.InvariantCulture);
        }

        /// <summary>Đơn vị tồn kho của nguyên liệu tại kho: đơn vị dòng tồn đang có, không có thì đơn vị cơ sở.</summary>
        public static async Task<Guid?> StockUnitAsync(DatabaseContext db, Guid warehouseId, Ingredient ingredient)
        {
            var unit = await db.InventoryBalance.AsNoTracking()
                .Where(b => !b.IsDeleted && b.WarehouseId == warehouseId && b.IngredientId == ingredient.Id && b.OnHandQuantity > 0)
                .OrderByDescending(b => b.AvailableQuantity).Select(b => (Guid?)b.UnitId).FirstOrDefaultAsync();
            return unit ?? (ingredient.BaseUnitId is { } b && b != Guid.Empty ? b : ingredient.BaseUomId is { } o && o != Guid.Empty ? o : null);
        }

        /// <summary>Quy đổi định lượng sang đơn vị tồn (UomConversionService); cùng đơn vị → giữ nguyên.</summary>
        public static async Task<decimal?> ToUnitAsync(IUomConversionService conversion, decimal quantity, Guid from, Guid to, DateTime on)
            => from == to ? quantity : await conversion.ConvertAsync(quantity, from, to, on);
    }
}
