using AppDelicare.Data.Connection;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// BIZ-1.3 (D-77 c, gộp FC-1.4 – anh chốt 2026-11-23): kế hoạch mua theo NGÀY CẦN HÀNG cho D+1 → D+N (N = PurchaseHorizonDays, mặc định 7).
    /// <list type="bullet">
    /// <item>Nguồn từng ngày, theo (khách, điểm giao, loại suất): đơn <b>Đã chốt</b> → đăng ký / đơn <b>Đã xác nhận</b> (cả Đã gửi) → dự báo <b>Đã công bố</b>
    ///   (thực đơn theo phân công / thực đơn chung – chia đều lựa chọn như dòng đơn cũ, GAP-MRP-03).</item>
    /// <item>Mỗi ngày tính như MRP chính thức (công thức, hao hụt 2 tầng, tồn xuất được tại ngày, hàng PO sắp về ≤ ngày, tồn an toàn) rồi trừ
    ///   <b>lũy kế</b>: thiếu lũy kế = max(0, Σ nhu cầu tới ngày + tồn an toàn − tồn xuất được − hàng sắp về − đề nghị mua đang mở có ngày cần ≤ ngày);
    ///   phải mua cho ngày = thiếu lũy kế − thiếu lũy kế lớn nhất các ngày trước.</item>
    /// <item>Ngày phải đặt = ngày cần − lead time NCC ưu tiên − PurchaseOrderLeadBufferDays (1).</item>
    /// </list>
    /// Chỉ tính khi xem, không lưu. Kế hoạch SX / tính nhu cầu chính thức (Calculate, 20:00 tính bổ sung) vẫn chỉ đơn Đã chốt (A9).
    /// </summary>
    public partial class MaterialRequirementService
    {
        private sealed class HorizonRun
        {
            public MaterialHorizonModel Model { get; } = new();
            public Dictionary<Guid, MaterialHorizonItemModel> Items { get; } = [];
        }

        public async Task<IResult<MaterialHorizonModel>> GetHorizon(MaterialHorizonRequest request)
        {
            try
            {
                var (run, error) = await HorizonAsync(request);
                return error != null ? Result<MaterialHorizonModel>.Fail(error) : Result<MaterialHorizonModel>.Success(run!.Model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetHorizon: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<MaterialHorizonModel>> CreateHorizonPurchaseRequest(MaterialHorizonPurchaseRequest request)
        {
            try
            {
                var (run, error) = await HorizonAsync(request);
                if (error != null)
                {
                    return Result<MaterialHorizonModel>.Fail(error);
                }
                DateTime today = _clock.Today;
                var pick = request.ListIngredientId is { Count: > 0 } ids ? ids.ToHashSet() : null;
                var lines = run!.Model.ListItem.Where(i => pick == null || pick.Contains(i.IngredientId))
                    .SelectMany(i => i.ListDay.Where(c => c.Shortage > 0 && (!request.OnlyDue || (c.OrderByDate ?? c.Date) <= today)).Select(c => (Item: i, Cell: c)))
                    .OrderBy(x => x.Cell.Date).ThenBy(x => x.Item.IngredientName).ToList();
                if (lines.Count == 0)
                {
                    return Result<MaterialHorizonModel>.Fail(request.OnlyDue ? ConstantHelper.Message.MrpHorizonNothingDue : ConstantHelper.Message.MrpHorizonNothingToOrder);
                }
                using var tran = await _unitOfWork.OpenTransaction();
                try
                {
                    string number = await _documentNumberService.NextAsync(DocumentKind.PurchaseRequest, today);
                    var pr = new PurchaseRequest
                    {
                        Id = Guid.NewGuid(), RequestNumber = number, RequestNo = number, RequestDate = _clock.Now, RequiredDate = lines[0].Cell.Date,
                        Status = PurchaseRequestStatus.Draft,
                        Note = $"Kế hoạch mua theo ngày cần hàng {Day(run.Model.FromDate)} – {Day(run.Model.ToDate)}"
                            + (request.OnlyDue ? " – phần phải đặt tới hôm nay" : string.Empty) + (string.IsNullOrWhiteSpace(request.Note) ? string.Empty : " – " + request.Note.Trim()),
                    };
                    _unitOfWork.PurchaseRequestRepository.Add(pr);
                    foreach (var (item, cell) in lines)
                    {
                        _unitOfWork.PurchaseRequestLineRepository.Add(new PurchaseRequestLine
                        {
                            Id = Guid.NewGuid(), PurchaseRequestId = pr.Id, IngredientId = item.IngredientId, Quantity = Ceil4(cell.Shortage),
                            UnitId = item.UnitId, UomId = item.UnitId, RequiredDate = cell.Date,
                            Note = (item.SupplierName == null ? ConstantHelper.Message.MrpNoSupplier : $"NCC gợi ý: {item.SupplierName}")
                                + (cell.OrderByDate is { } by ? $" – đặt trước {Day(by)}" : string.Empty),
                        });
                    }
                    await _unitOfWork.PurchaseRequestRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                    _unitOfWork.CommitTransaction();
                }
                catch
                {
                    _unitOfWork.RollbackTransaction();
                    throw;
                }
                var (after, _) = await HorizonAsync(request);
                return Result<MaterialHorizonModel>.Success(after!.Model, string.Format(ConstantHelper.Message.MrpHorizonPrCreated, lines.Count));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " CreateHorizonPurchaseRequest: ", ex.ToString()));
                throw;
            }
        }

        private async Task<(HorizonRun? Run, string? Error)> HorizonAsync(MaterialHorizonRequest request)
        {
            int days = request.Days ?? _rules.PurchaseHorizonDays;
            if (days is < 1 or > 31)
            {
                return (null, ConstantHelper.Message.MrpHorizonDaysRange);
            }
            if (await ValidateScopeAsync(request.OrganizationUnitId, request.WarehouseId) is { } scopeError)
            {
                return (null, scopeError);
            }
            var db = _unitOfWork.DbContext;
            DateTime today = _clock.Today, from = (request.FromDate ?? today.AddDays(1)).Date, to = from.AddDays(days - 1);
            var run = new HorizonRun();
            var m = run.Model;
            m.FromDate = from;
            m.ToDate = to;
            m.Today = today;

            // Đề nghị mua đang mở (chưa lập PO, chưa hủy / từ chối): coi như hàng sẽ về đúng ngày cần – tránh đề nghị trùng.
            var openStatuses = new[] { PurchaseRequestStatus.Draft, PurchaseRequestStatus.PendingApproval, PurchaseRequestStatus.Approved };
            var openPr = await (from l in db.PurchaseRequestLine.AsNoTracking()
                                join p in db.PurchaseRequest.AsNoTracking() on l.PurchaseRequestId equals p.Id
                                where !l.IsDeleted && !p.IsDeleted && p.Status != null && openStatuses.Contains(p.Status) && l.Quantity > 0
                                select new { l.IngredientId, l.Quantity, Unit = l.UnitId != Guid.Empty ? l.UnitId : l.UomId, Date = l.RequiredDate ?? p.RequiredDate ?? p.RequestDate })
                .ToListAsync();

            var cumRequired = new Dictionary<Guid, decimal>();
            var maxShort = new Dictionary<Guid, decimal>();
            var seen = new Dictionary<Guid, Guid>();
            for (DateTime d = from; d <= to; d = d.AddDays(1))
            {
                var (lines, labels, dayModel) = await HorizonDemandAsync(db, d);
                m.ListDay.Add(dayModel);
                if (lines.Count == 0 && seen.Count == 0)
                {
                    continue;
                }
                var (c, error) = await ComputeAsync(d, request.OrganizationUnitId, request.WarehouseId, seen, null, (lines, labels));
                if (c == null)
                {
                    if (error != null)
                    {
                        dayModel.ListWarning.Add(error);
                    }
                    continue;
                }
                dayModel.ListWarning.AddRange(c.Warnings.Distinct());
                foreach (var item in c.Items.Values)
                {
                    seen[item.IngredientId] = item.UnitId;
                    var ingredient = c.Ingredients.GetValueOrDefault(item.IngredientId);
                    if (!run.Items.TryGetValue(item.IngredientId, out var row))
                    {
                        row = new MaterialHorizonItemModel
                        {
                            IngredientId = item.IngredientId, IngredientCode = ingredient?.Code, IngredientName = ingredient?.Name, UnitId = item.UnitId,
                            UnitName = c.UnitNames.GetValueOrDefault(item.UnitId), AvailableQuantity = Round4(item.Available),
                        };
                        run.Items[item.IngredientId] = row;
                    }
                    row.SafetyStock = Round4(item.Safety);
                    if (item.BestSupplier != null)
                    {
                        row.SupplierName = item.BestSupplier.Name;
                        row.LeadTimeDays = item.Best?.LeadTimeDays;
                    }
                    decimal requested = 0m;
                    foreach (var l in openPr.Where(x => x.IngredientId == item.IngredientId && x.Date.Date <= d))
                    {
                        requested += await c.Converter.ConvertAsync(l.Quantity, l.Unit, item.UnitId) ?? 0m;
                    }
                    decimal cum = cumRequired.GetValueOrDefault(item.IngredientId) + item.Required;
                    cumRequired[item.IngredientId] = cum;
                    decimal shortNow = Math.Max(0m, cum + item.Safety - item.Available - item.Incoming - requested);
                    decimal prev = maxShort.GetValueOrDefault(item.IngredientId);
                    decimal inc = Round4(Math.Max(0m, shortNow - prev));
                    maxShort[item.IngredientId] = Math.Max(prev, shortNow);
                    int lead = Math.Max(0, item.Best?.LeadTimeDays ?? 0);
                    row.ListDay.Add(new MaterialHorizonCellModel
                    {
                        Date = d, Required = Round4(item.Required), Shortage = inc,
                        OrderByDate = inc > 0 ? d.AddDays(-lead - Math.Max(0, _rules.PurchaseOrderLeadBufferDays)) : null,
                    });
                }
            }
            foreach (var row in run.Items.Values)
            {
                row.TotalRequired = Round4(row.ListDay.Sum(x => x.Required));
                row.TotalShortage = Round4(row.ListDay.Sum(x => x.Shortage));
                var first = row.ListDay.Where(x => x.Shortage > 0).OrderBy(x => x.Date).FirstOrDefault();
                row.FirstShortageDate = first?.Date;
                row.OrderByDate = row.ListDay.Where(x => x.OrderByDate != null).Select(x => x.OrderByDate).Min();
                (row.OrderStatus, row.OrderStatusName) = row.OrderByDate is not { } by ? ("NONE", "Đủ hàng")
                    : by < today ? ("LATE", $"Trễ – lẽ ra đặt {Day(by)}")
                    : by == today ? ("TODAY", "Phải đặt hôm nay")
                    : ("UPCOMING", $"Đặt trước ngày {Day(by)}");
            }
            m.ListItem = [.. run.Items.Values.Where(r => r.TotalRequired > 0 || r.TotalShortage > 0)
                .OrderBy(r => r.OrderStatus switch { "LATE" => 0, "TODAY" => 1, "UPCOMING" => 2, _ => 3 }).ThenBy(r => r.OrderByDate).ThenBy(r => r.IngredientName)];
            m.DueTodayCount = m.ListItem.Count(r => r.OrderStatus == "TODAY");
            m.LateCount = m.ListItem.Count(r => r.OrderStatus == "LATE");
            if (m.ListDay.All(x => x.Source == "NONE"))
            {
                m.ListWarning.Add(string.Format(ConstantHelper.Message.MrpHorizonNoDemand, Day(from), Day(to)));
            }
            return (run, null);
        }

        /// <summary>Nhu cầu 1 ngày: dòng đơn chốt / đăng ký, dự báo đã công bố quy ra dòng (thực đơn theo phân công) – theo (khách, điểm giao, loại suất).</summary>
        private async Task<(List<OrderLine> Lines, Dictionary<Guid, string> Labels, MaterialHorizonDayModel Day)> HorizonDemandAsync(DatabaseContext db, DateTime d)
        {
            var day = new MaterialHorizonDayModel { Date = d };
            DateTime next = d.AddDays(1);
            var statuses = new[] { OrderStatus.Locked, OrderStatus.Confirmed, OrderStatus.Submitted };
            var orders = await db.Order.AsNoTracking().Where(o => !o.IsDeleted && o.MealDate >= d && o.MealDate < next && o.Status != null && statuses.Contains(o.Status)).ToListAsync();
            var orderIds = orders.Select(o => o.Id).ToList();
            var orderLines = await db.OrderLine.AsNoTracking().Where(l => orderIds.Contains(l.OrderId) && !l.IsDeleted && l.Quantity > 0).ToListAsync();
            var byId = orders.ToDictionary(o => o.Id);
            static string Meal(string? s) => (s ?? string.Empty).Trim().ToUpperInvariant();
            var lines = new List<OrderLine>();
            var labels = orders.ToDictionary(o => o.Id, o => o.OrderNumber ?? o.OrderNo ?? o.Id.ToString("N")[..8]);
            var covered = new HashSet<(Guid, Guid?, string)>();
            foreach (var g in orderLines.GroupBy(l => (byId[l.OrderId].CustomerId, byId[l.OrderId].DeliveryLocationId, Meal(l.MealType))))
            {
                covered.Add(g.Key);
                var locked = g.Where(l => byId[l.OrderId].Status == OrderStatus.Locked).ToList();
                if (locked.Count != 0)
                {
                    lines.AddRange(locked);
                    day.LockedMeals += locked.Sum(l => l.Quantity);
                }
                else
                {
                    lines.AddRange(g);
                    day.ConfirmedMeals += g.Sum(l => l.Quantity);
                }
            }
            var forecasts = (await db.Forecast.AsNoTracking().Where(f => !f.IsDeleted && f.ForecastDate >= d && f.ForecastDate < next && f.Status == ForecastStatus.Published).ToListAsync())
                .GroupBy(f => (f.CustomerId, f.DeliveryLocationId, Meal(f.MealType))).Select(g => g.OrderByDescending(f => f.VersionNo).First())
                .Where(f => !covered.Contains((f.CustomerId, f.DeliveryLocationId, Meal(f.MealType)))).ToList();
            if (forecasts.Count != 0)
            {
                var customerIds = forecasts.Select(f => f.CustomerId).Distinct().ToList();
                var customers = await db.Customer.AsNoTracking().Where(x => customerIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Code ?? x.Name);
                foreach (var f in forecasts)
                {
                    decimal qty = f.ForecastQuantity > 0 ? f.ForecastQuantity : f.Quantity;
                    if (qty <= 0)
                    {
                        continue;
                    }
                    string who = customers.GetValueOrDefault(f.CustomerId) ?? f.CustomerId.ToString("N")[..8];
                    var menu = await MenuAssignmentService.ResolveAsync(db, f.CustomerId, f.DeliveryLocationId, f.MealType, d);
                    if (menu == null)
                    {
                        day.ListWarning.Add(string.Format(ConstantHelper.Message.MrpHorizonForecastNoMenu, who, f.MealType, Day(d)));
                        continue;
                    }
                    Guid pseudo = Guid.NewGuid();
                    labels[pseudo] = "Dự báo " + who;
                    lines.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = pseudo, MenuId = menu.MenuId, MealType = f.MealType, ShiftId = f.ShiftId, Quantity = qty });
                    day.ForecastMeals += qty;
                }
            }
            int kinds = (day.LockedMeals > 0 ? 1 : 0) + (day.ConfirmedMeals > 0 ? 1 : 0) + (day.ForecastMeals > 0 ? 1 : 0);
            (day.Source, day.SourceName) = kinds == 0 ? ("NONE", "Chưa có đơn / đăng ký / dự báo")
                : kinds > 1 ? ("MIXED", "Kết hợp")
                : day.LockedMeals > 0 ? ("LOCKED", "Đơn đã chốt")
                : day.ConfirmedMeals > 0 ? ("CONFIRMED", "Đăng ký / đơn đã xác nhận")
                : ("FORECAST", "Dự báo đã công bố");
            return (lines, labels, day);
        }
    }
}
