using AppDelicare.Data.Connection;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Model.Request.Custom;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6.34 (D-115): "chỉ giao khi đủ toàn bộ thực đơn" – trạng thái từng ô điểm giao (đơn) × món của ngày.
    /// Món sẵn sàng chia khi mọi mẻ của món × ca đã Đạt / Đã đóng gói (mẻ Không đạt chặn trừ khi đã có mẻ thay thế lập sau; mẻ nấu thêm chưa đạt → món chờ).
    /// Ô đã chia = có lệnh đóng gói (tự sinh theo mẻ) của đơn × món đã ghi kết quả và không còn lệnh chưa ghi. Liên kết qua OrderId – không thêm cột.
    /// </summary>
    public static class PackingReadiness
    {
        public static readonly string[] BoardOrderStatus = [OrderStatus.Locked, OrderStatus.InProduction, OrderStatus.Delivered, OrderStatus.Reconciled];

        public sealed record DishState(int Passed, int Total, bool Ready);
        public sealed record Cell(Guid OrderId, Guid ShiftId, Guid DishId, decimal Quantity, string Status, decimal Damaged, List<Guid> OpenPackingIds);

        public static async Task<Dictionary<(Guid Dish, Guid Shift), DishState>> DishStatesAsync(DatabaseContext db, DateTime date)
        {
            DateTime day = date.Date;
            var batches = await (from b in db.ProductionBatch.AsNoTracking()
                                 join o in db.ProductionOrder.AsNoTracking() on b.ProductionOrderId equals o.Id
                                 join p in db.ProductionPlan.AsNoTracking() on o.ProductionPlanId equals p.Id
                                 where !b.IsDeleted && !o.IsDeleted && !p.IsDeleted && p.ProductionDate == day && b.Status != ProductionBatchStatus.Cancelled
                                 select new { b.DishId, o.ShiftId, b.Status, b.StartDate, b.QcStatus }).ToListAsync();
            var result = new Dictionary<(Guid, Guid), DishState>();
            foreach (var g in batches.Where(b => b.ShiftId != null).GroupBy(b => (b.DishId, b.ShiftId!.Value)))
            {
                var list = g.Select(b => (b.Status, b.StartDate)).ToList();
                // Mẻ Không đạt chỉ chặn khi chưa có mẻ thay thế (không Không đạt) lập cùng lúc hoặc sau nó.
                bool Replaced((string? Status, DateTime StartDate) b) => list.Any(x => x.Status != ProductionBatchStatus.Failed && x.StartDate >= b.StartDate);
                bool Blocking((string? Status, DateTime StartDate) b) => b.Status == ProductionBatchStatus.Failed ? !Replaced(b)
                    : !(b.Status == ProductionBatchStatus.Passed || b.Status == ProductionBatchStatus.Packed);
                int passed = list.Count(b => b.Status is ProductionBatchStatus.Passed or ProductionBatchStatus.Packed);
                int total = list.Count(b => b.Status != ProductionBatchStatus.Failed || !Replaced(b));
                bool ready = passed > 0 && !list.Any(Blocking);
                result[g.Key] = new DishState(passed, total, ready);
            }
            return result;
        }

        /// <summary>Ô điểm giao × món của ngày (đơn đã chốt trở đi); <paramref name="orderIds"/> trống = mọi đơn của ngày.</summary>
        public static async Task<List<Cell>> CellsAsync(DatabaseContext db, BusinessRulesOptions rules, DateTime date, IReadOnlyCollection<Guid>? orderIds = null)
        {
            var (demands, _, _, _) = await ProductionRules.DishDemandsAsync(db, date.Date, BoardOrderStatus, rules);
            if (orderIds != null)
            {
                var set = orderIds.ToHashSet();
                demands = [.. demands.Where(d => set.Contains(d.OrderId))];
            }
            var states = await DishStatesAsync(db, date);
            var ids = demands.Select(d => d.OrderId).Distinct().ToList();
            var packs = await (from p in db.PackingOrder.AsNoTracking()
                               join b in db.ProductionBatch.AsNoTracking() on p.ProductionBatchId equals b.Id
                               where !p.IsDeleted && p.OrderId != null && ids.Contains(p.OrderId.Value) && p.Status != PackingOrderStatus.Cancelled
                               select new { p.Id, OrderId = p.OrderId!.Value, b.DishId, p.Status, Qty = p.PlannedQty ?? 0m }).ToListAsync();
            var packIds = packs.Select(p => p.Id).ToList();
            var damaged = (await db.PackingResult.AsNoTracking().Where(r => !r.IsDeleted && packIds.Contains(r.PackingOrderId))
                .Select(r => new { r.PackingOrderId, D = (r.DamagedQty ?? 0m) + r.RejectedQuantity }).ToListAsync())
                .GroupBy(r => r.PackingOrderId).ToDictionary(g => g.Key, g => g.Sum(x => x.D));
            var cells = new List<Cell>();
            foreach (var g in demands.GroupBy(d => (d.OrderId, d.ShiftId, d.DishId)))
            {
                decimal qty = g.Sum(d => d.Quantity);
                if (qty <= 0)
                {
                    continue;
                }
                var mine = packs.Where(p => p.OrderId == g.Key.OrderId && p.DishId == g.Key.DishId).ToList();
                var open = mine.Where(p => p.Status != PackingOrderStatus.Packed).Select(p => p.Id).ToList();
                bool ready = states.TryGetValue((g.Key.DishId, g.Key.ShiftId), out var st) && st.Ready;
                decimal dmg = mine.Sum(p => damaged.GetValueOrDefault(p.Id));
                // Ô đã chia đủ phần (lệnh đã ghi hết, số đã chia ≥ số cần) giữ "đã chia" kể cả khi món có mẻ nấu thêm; thiếu phần → chờ mẻ mới.
                bool allDone = mine.Count != 0 && open.Count == 0;
                bool enough = mine.Sum(p => p.Qty) >= qty;
                string status = allDone && (ready || enough) ? (dmg > 0 ? "DAMAGED" : "PACKED") : ready && open.Count != 0 ? "READY" : "WAITING";
                cells.Add(new Cell(g.Key.OrderId, g.Key.ShiftId, g.Key.DishId, qty, status, dmg, open));
            }
            return cells;
        }

        /// <summary>Đơn → món còn thiếu (chưa chia) – rỗng = điểm giao đủ thực đơn.</summary>
        public static async Task<Dictionary<Guid, List<string>>> MissingByOrderAsync(DatabaseContext db, BusinessRulesOptions rules, DateTime date, IReadOnlyCollection<Guid> orderIds)
        {
            var cells = await CellsAsync(db, rules, date, orderIds);
            var dishIds = cells.Select(c => c.DishId).Distinct().ToList();
            var names = await db.Dish.AsNoTracking().Where(d => dishIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name ?? d.Code);
            var result = orderIds.ToDictionary(o => o, _ => new List<string>());
            foreach (var c in cells.Where(c => c.Status is not ("PACKED" or "DAMAGED")))
            {
                result[c.OrderId].Add(names.GetValueOrDefault(c.DishId) ?? "?");
            }
            return result;
        }
    }

    public partial class PackingOrderService
    {
        public async Task<IResult<PackingBoardModel>> GetBoard(DateTime? date, Guid? shiftId)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                DateTime day = (date ?? _clock.Today).Date;
                var all = await PackingReadiness.CellsAsync(db, _rules, day);
                var shiftIds = all.Select(c => c.ShiftId).Distinct().ToList();
                var shifts = await db.Shift.AsNoTracking().Where(s => shiftIds.Contains(s.Id)).OrderBy(s => s.StartTime).ToListAsync();
                Guid? sid = shiftId is { } x && shifts.Any(s => s.Id == x) ? x : shifts.FirstOrDefault(s => all.Any(c => c.ShiftId == s.Id && c.Status != "PACKED" && c.Status != "DAMAGED"))?.Id ?? shifts.FirstOrDefault()?.Id;
                var cells = all.Where(c => c.ShiftId == sid).ToList();
                var states = await PackingReadiness.DishStatesAsync(db, day);
                var dishIds = cells.Select(c => c.DishId).Distinct().ToList();
                var dishes = await db.Dish.AsNoTracking().Where(d => dishIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name ?? d.Code);
                var orderIds = cells.Select(c => c.OrderId).Distinct().ToList();
                var orders = await db.Order.AsNoTracking().Where(o => orderIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id);
                var custIds = orders.Values.Select(o => o.CustomerId).Distinct().ToList();
                var custs = await db.Customer.AsNoTracking().Where(c => custIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.DisplayName ?? c.Name);
                var locIds = orders.Values.Where(o => o.DeliveryLocationId != null).Select(o => o.DeliveryLocationId!.Value).Distinct().ToList();
                var locs = await db.CustomerDeliveryLocation.AsNoTracking().Where(l => locIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.Name);
                var dos = await db.DeliveryOrder.AsNoTracking().Where(d => !d.IsDeleted && orderIds.Contains(d.OrderId)).Select(d => new { d.OrderId, d.DeliveryRouteId }).ToListAsync();
                var routeIds = dos.Where(d => d.DeliveryRouteId != null).Select(d => d.DeliveryRouteId!.Value).Distinct().ToList();
                var routes = await db.DeliveryRoute.AsNoTracking().Where(r => routeIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Name ?? r.RouteNo ?? r.Code);
                var model = new PackingBoardModel
                {
                    Date = day, ShiftId = sid, ShiftName = shifts.FirstOrDefault(s => s.Id == sid)?.Name,
                    ListShift = [.. shifts.Select(s => new PackingBoardShiftModel { Id = s.Id, Name = s.Name ?? s.Code })],
                };
                foreach (var d in dishIds.OrderBy(d => dishes.GetValueOrDefault(d)))
                {
                    var stt = sid is { } s2 && states.TryGetValue((d, s2), out var v) ? v : new PackingReadiness.DishState(0, 0, false);
                    model.ListDish.Add(new PackingBoardDishModel
                    {
                        DishId = d, DishName = dishes.GetValueOrDefault(d), BatchesPassed = stt.Passed, BatchesTotal = stt.Total, Ready = stt.Ready,
                        AllPacked = cells.Where(c => c.DishId == d).All(c => c.Status is "PACKED" or "DAMAGED"),
                    });
                }
                foreach (var o in orderIds)
                {
                    orders.TryGetValue(o, out var ord);
                    var route = dos.FirstOrDefault(x => x.OrderId == o)?.DeliveryRouteId;
                    var mine = cells.Where(c => c.OrderId == o).ToList();
                    model.ListRow.Add(new PackingBoardRowModel
                    {
                        OrderId = o, OrderNumber = ord?.OrderNumber ?? ord?.OrderNo, CustomerName = ord == null ? null : custs.GetValueOrDefault(ord.CustomerId),
                        LocationName = ord?.DeliveryLocationId is { } l ? locs.GetValueOrDefault(l) : null, DeliveryRouteId = route, RouteName = route is { } r ? routes.GetValueOrDefault(r) : null,
                        DishesTotal = mine.Count, DishesDone = mine.Count(c => c.Status is "PACKED" or "DAMAGED"),
                        ListCell = [.. model.ListDish.Select(d => mine.FirstOrDefault(c => c.DishId == d.DishId) is { } c
                            ? new PackingBoardCellModel { DishId = d.DishId, Status = c.Status, Quantity = c.Quantity, Damaged = c.Damaged }
                            : new PackingBoardCellModel { DishId = d.DishId, Status = "NA" })],
                    });
                }
                foreach (var row in model.ListRow)
                {
                    row.Complete = row.DishesTotal > 0 && row.DishesDone == row.DishesTotal;
                }
                model.ListRow = [.. model.ListRow.OrderBy(r => r.RouteName == null).ThenBy(r => r.RouteName).ThenBy(r => r.CustomerName).ThenBy(r => r.LocationName)];
                model.DishesReady = model.ListDish.Count(d => d.Ready);
                model.PointsTotal = model.ListRow.Count;
                model.PointsComplete = model.ListRow.Count(r => r.Complete);
                model.PortionsTotal = cells.Sum(c => c.Quantity);
                model.PortionsPacked = cells.Where(c => c.Status is "PACKED" or "DAMAGED").Sum(c => c.Quantity);
                model.DamagedTotal = cells.Sum(c => c.Damaged);
                return Result<PackingBoardModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetBoard: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>
        /// Ghi kết quả chia cho nhiều ô trong một giao dịch: mỗi ô ghi mọi lệnh theo mẻ chưa ghi của đơn × món (mặc định đủ số; hỏng / thiếu ghi vào lệnh lớn nhất).
        /// Món chưa đủ mẻ → lỗi; ô đã chia → bỏ qua (không nhân đôi).
        /// </summary>
        public async Task<IResult<PackingResultManyModel>> RecordResultMany(PackingResultManyRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            var touched = new HashSet<Guid>();
            try
            {
                var actor = _currentActor.RequireActor();
                var db = _unitOfWork.DbContext;
                DateTime day = request.Date.Date;
                var inputs = request.ListCell.Where(c => c.OrderId != Guid.Empty && c.DishId != Guid.Empty).GroupBy(c => (c.OrderId, c.DishId)).Select(g => g.First()).ToList();
                if (inputs.Count == 0)
                {
                    return Fail(ConstantHelper.Message.PackingManyEmpty);
                }
                var cells = await PackingReadiness.CellsAsync(db, _rules, day, [.. inputs.Select(i => i.OrderId).Distinct()]);
                var dishIds = inputs.Select(i => i.DishId).Distinct().ToList();
                var dishNames = await db.Dish.AsNoTracking().Where(d => dishIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name ?? d.Code);
                var model = new PackingResultManyModel();
                foreach (var input in inputs)
                {
                    var cell = cells.FirstOrDefault(c => c.OrderId == input.OrderId && c.DishId == input.DishId && (request.ShiftId == null || c.ShiftId == request.ShiftId));
                    if (cell == null || cell.Status is "PACKED" or "DAMAGED")
                    {
                        continue;
                    }
                    if (cell.Status == "WAITING")
                    {
                        return Fail(string.Format(ConstantHelper.Message.PackingDishNotReady, dishNames.GetValueOrDefault(input.DishId)));
                    }
                    decimal damaged = Math.Max(0, input.Damaged ?? 0m);
                    var packs = cell.OpenPackingIds.Select(id => db.PackingOrder.AsNoTracking().First(p => p.Id == id)).OrderByDescending(p => p.PlannedQty ?? 0m).ToList();
                    foreach (var p in packs)
                    {
                        var tracked = _unitOfWork.PackingOrderRepository.GetSingle(x => x.Id == p.Id && !x.IsDeleted)!;
                        decimal planned = tracked.PlannedQty ?? 0m, d = Math.Min(damaged, planned);
                        damaged -= d;
                        var warnings = new List<string>();
                        string? error = await RecordResultCoreAsync(tracked, new PackingOrderResultRequest
                        {
                            Id = tracked.Id, PackedQuantity = planned - d, DamagedQty = d, RejectedQuantity = 0, Note = d > 0 ? input.Reason : null,
                        }, actor, warnings);
                        if (error != null)
                        {
                            return Fail(error);
                        }
                        _unitOfWork.PackingOrderRepository.Update(tracked);
                        await _unitOfWork.PackingOrderRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                        db.ChangeTracker.Clear();
                        touched.Add(tracked.ProductionBatchId);
                        model.Orders++;
                        model.ListWarning.AddRange(warnings);
                    }
                    model.Cells++;
                }
                _unitOfWork.CommitTransaction();
                foreach (var batchId in touched)
                {
                    if (await ProductionAutoClose.AfterBatchAsync(_services, _rules, db, batchId) is { } closeNote)
                    {
                        model.ListWarning.Add(closeNote);
                    }
                }
                return Result<PackingResultManyModel>.Success(model, string.Format(ConstantHelper.Message.PackingManySaved, model.Cells));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " RecordResultMany: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                throw;
            }

            IResult<PackingResultManyModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                return Result<PackingResultManyModel>.Fail(message);
            }
        }
    }
}
