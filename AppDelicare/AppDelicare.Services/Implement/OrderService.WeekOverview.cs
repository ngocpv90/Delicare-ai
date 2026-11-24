using System.Globalization;
using System.Net;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6.6 (D-85): đăng ký suất tuần theo mẫu chuẩn – tổng quan mọi khách / điểm giao × 7 ngày, xác nhận nhiều điểm một lần,
    /// lấy số dự báo cho ô trống (chia lựa chọn theo tỉ lệ 4 tuần), tải nhiều phiếu một lần (tự nhận điểm qua mã ẩn). Không thêm bảng / cột.
    /// </summary>
    public partial class OrderService
    {
        public const string CellNone = "NONE", CellDraft = "DRAFT", CellSubmitted = "SUBMITTED", CellConfirmed = "CONFIRMED", CellLocked = "LOCKED", CellNoContract = "NOCONTRACT";
        private const string ForecastPublished = "Published";

        private static string CellStatusOf(string? orderStatus) => orderStatus switch
        {
            null => CellNone,
            OrderStatus.Draft => CellDraft,
            OrderStatus.Submitted => CellSubmitted,
            OrderStatus.Confirmed => CellConfirmed,
            _ => CellLocked,
        };

        private DateTime WeekOf(DateTime? any) => MondayOf(any ?? _clock.Now.Date.AddDays(7));

        private sealed record WeekPoint(Guid CustomerId, string? CustomerCode, string? CustomerName, Guid LocationId, string? LocationCode, string? LocationName);

        /// <summary>Khách có hợp đồng Hiệu lực giao với tuần × điểm giao đang hoạt động; kèm khoảng hiệu lực hợp đồng để khóa ngày ngoài hợp đồng.</summary>
        private async Task<(List<WeekPoint> Points, Dictionary<Guid, List<(DateTime From, DateTime? To)>> Contracts)> WeekPointsAsync(DateTime monday, Guid? branchId)
        {
            var db = _unitOfWork.DbContext;
            DateTime end = monday.AddDays(7);
            var contracts = await (from ct in db.Contract.AsNoTracking()
                                   join c in db.Customer.AsNoTracking() on ct.CustomerId equals c.Id
                                   where !ct.IsDeleted && !c.IsDeleted && ct.Status == ContractStatus.Active && ct.EffectiveFrom < end && (ct.EffectiveTo == null || ct.EffectiveTo >= monday)
                                         && (branchId == null || c.BranchId == branchId)
                                   select new { ct.CustomerId, ct.EffectiveFrom, ct.EffectiveTo, c.Code, Name = c.DisplayName ?? c.Name }).ToListAsync();
            var ids = contracts.Select(c => c.CustomerId).Distinct().ToList();
            var locations = await db.CustomerDeliveryLocation.AsNoTracking()
                .Where(l => ids.Contains(l.CustomerId) && !l.IsDeleted && (l.Status == null || l.Status == ActiveStatus))
                .Select(l => new { l.Id, l.CustomerId, l.Code, l.Name }).ToListAsync();
            var names = contracts.GroupBy(c => c.CustomerId).ToDictionary(g => g.Key, g => g.First());
            var points = locations
                .Select(l => new WeekPoint(l.CustomerId, names[l.CustomerId].Code, names[l.CustomerId].Name, l.Id, l.Code, l.Name))
                .OrderBy(p => p.CustomerName, StringComparer.CurrentCulture).ThenBy(p => p.LocationName, StringComparer.CurrentCulture).ToList();
            var ranges = contracts.GroupBy(c => c.CustomerId).ToDictionary(g => g.Key, g => g.Select(c => (c.EffectiveFrom, c.EffectiveTo)).ToList());
            return (points, ranges);
        }

        public async Task<IResult<OrderWeekOverviewModel>> GetWeekOverview(OrderWeekOverviewRequest request)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                DateTime monday = WeekOf(request.WeekStart), end = monday.AddDays(7);
                var model = new OrderWeekOverviewModel { WeekStart = monday, ListDate = Enumerable.Range(0, 7).Select(i => monday.AddDays(i)).ToList(), ListTotal = [.. new decimal[7]] };
                var (points, contracts) = await WeekPointsAsync(monday, request.BranchId);
                var customerIds = points.Select(p => p.CustomerId).Distinct().ToList();
                var orders = await db.Order.AsNoTracking()
                    .Where(o => !o.IsDeleted && customerIds.Contains(o.CustomerId) && o.MealDate >= monday && o.MealDate < end && o.Status != OrderStatus.Cancelled)
                    .Select(o => new { o.Id, o.CustomerId, o.DeliveryLocationId, o.MealDate, o.Status, TotalQuantity = o.TotalQuantity ?? 0m, TotalAmount = o.TotalAmount ?? 0m, No = o.OrderNumber ?? o.OrderNo, o.CreatedDate })
                    .ToListAsync();
                var forecasts = await db.Forecast.AsNoTracking()
                    .Where(f => !f.IsDeleted && f.Status == ForecastPublished && customerIds.Contains(f.CustomerId) && f.ForecastDate >= monday && f.ForecastDate < end)
                    .GroupBy(f => new { f.CustomerId, f.DeliveryLocationId, Day = f.ForecastDate.Date })
                    .Select(g => new { g.Key.CustomerId, g.Key.DeliveryLocationId, g.Key.Day, Qty = g.Sum(x => x.ForecastQuantity) })
                    .ToListAsync();
                decimal regSum = 0m, fcSum = 0m;
                foreach (var group in points.GroupBy(p => p.CustomerId))
                {
                    var first = group.First();
                    var customer = new OrderWeekOverviewCustomerModel { CustomerId = first.CustomerId, CustomerCode = first.CustomerCode, CustomerName = first.CustomerName, ListTotal = [.. new decimal[7]] };
                    var ranges = contracts.GetValueOrDefault(first.CustomerId) ?? [];
                    foreach (var p in group)
                    {
                        var point = new OrderWeekOverviewPointModel { DeliveryLocationId = p.LocationId, LocationCode = p.LocationCode, LocationName = p.LocationName };
                        for (int i = 0; i < 7; i++)
                        {
                            DateTime day = monday.AddDays(i);
                            var order = orders.Where(o => o.DeliveryLocationId == p.LocationId && o.MealDate.Date == day).OrderByDescending(o => o.CreatedDate).FirstOrDefault();
                            decimal? fc = forecasts.Where(f => f.CustomerId == p.CustomerId && f.DeliveryLocationId == p.LocationId && f.Day == day).Select(f => (decimal?)f.Qty).FirstOrDefault();
                            bool inContract = ranges.Any(r => r.From.Date <= day && (r.To == null || r.To.Value.Date >= day));
                            var cell = new OrderWeekOverviewCellModel
                            {
                                Date = day, Forecast = fc, AfterCutoff = IsAfterCutoff(day),
                                Status = order != null ? CellStatusOf(order.Status) : inContract ? CellNone : CellNoContract,
                                Quantity = order?.TotalQuantity, Amount = order?.TotalAmount, OrderId = order?.Id, OrderNumber = order?.No,
                            };
                            point.ListCell.Add(cell);
                            if (order != null)
                            {
                                point.TotalWeek += order.TotalQuantity;
                                customer.ListTotal[i] += order.TotalQuantity;
                                model.ListTotal[i] += order.TotalQuantity;
                                model.TotalAmount += order.TotalAmount;
                                if (fc is > 0)
                                {
                                    regSum += order.TotalQuantity;
                                    fcSum += fc.Value;
                                }
                            }
                        }
                        var states = point.ListCell.Where(c => c.Status is not (CellNone or CellNoContract)).Select(c => c.Status).Distinct().ToList();
                        point.Status = states.Count == 0 ? CellNone : states.Count == 1 ? states[0] : "MIXED";
                        model.PointCount++;
                        if (states.Count == 0)
                        {
                            model.UnregisteredPoints++;
                        }
                        else
                        {
                            model.RegisteredPoints++;
                        }
                        if (point.ListCell.Any(c => c.Status is CellDraft or CellSubmitted && !c.AfterCutoff))
                        {
                            model.PendingPoints++;
                        }
                        customer.ListPoint.Add(point);
                    }
                    model.ListCustomer.Add(customer);
                }
                model.TotalWeek = model.ListTotal.Sum();
                model.ForecastDeviationPct = fcSum > 0 ? Math.Round((regSum - fcSum) / fcSum * 100m, 1) : null;
                DateTime now = _clock.Now;
                model.NextCutoff = Enumerable.Range(0, 8).Select(i => CutoffOf(now.Date.AddDays(i))).First(c => c > now);
                var menus = await db.Menu.AsNoTracking()
                    .Where(m => !m.IsDeleted && m.MenuType == MenuScope.Common && m.Status == MenuStatus.Published
                        && (m.EffectiveFrom == null || m.EffectiveFrom < end) && (m.EffectiveTo == null || m.EffectiveTo >= monday))
                    .OrderByDescending(m => m.EffectiveFrom).Select(m => m.Code).ToListAsync();
                model.ListMenuCode = menus.Where(c => c != null).Select(c => c!).Distinct().Take(5).ToList();
                return Result<OrderWeekOverviewModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetWeekOverview: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Xác nhận nhiều điểm: mọi đơn Nháp / Đã gửi của tuần, chưa quá giờ chốt → Gửi + Xác nhận (cùng luật / cảnh báo như nút Xác nhận). Đơn lỗi báo lại, không chặn đơn khác.</summary>
        public async Task<IResult<OrderWeekConfirmResultModel>> ConfirmWeekPoints(OrderWeekConfirmRequest request)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                DateTime monday = WeekOf(request.WeekStart), end = monday.AddDays(7);
                var locIds = request.ListDeliveryLocationId ?? [];
                var (points, _) = await WeekPointsAsync(monday, request.BranchId);
                var allowed = points.Select(p => p.LocationId).Where(id => locIds.Count == 0 || locIds.Contains(id)).ToHashSet();
                var orders = await db.Order.AsNoTracking()
                    .Where(o => !o.IsDeleted && o.DeliveryLocationId != null && o.MealDate >= monday && o.MealDate < end && (o.Status == OrderStatus.Draft || o.Status == OrderStatus.Submitted))
                    .Select(o => new { o.Id, o.DeliveryLocationId, o.MealDate, o.Status, No = o.OrderNumber ?? o.OrderNo })
                    .ToListAsync();
                orders = orders.Where(o => allowed.Contains(o.DeliveryLocationId!.Value) && !IsAfterCutoff(o.MealDate)).OrderBy(o => o.MealDate).ToList();
                var result = new OrderWeekConfirmResultModel { Points = orders.Select(o => o.DeliveryLocationId).Distinct().Count() };
                if (orders.Count == 0)
                {
                    return Result<OrderWeekConfirmResultModel>.Fail(ConstantHelper.Message.OrderWeekNothingToConfirm);
                }
                foreach (var o in orders)
                {
                    if (o.Status == OrderStatus.Draft)
                    {
                        var submitted = await Submit(new OrderActionRequest { Id = o.Id });
                        if (submitted.ResponseCode != (int)HttpStatusCode.OK)
                        {
                            result.Failed++;
                            result.ListError.Add($"{o.No} ({Day(o.MealDate)}): {submitted.ResponseMessage}");
                            continue;
                        }
                    }
                    var confirmed = await Confirm(new OrderActionRequest { Id = o.Id });
                    if (confirmed.ResponseCode == (int)HttpStatusCode.OK)
                    {
                        result.Confirmed++;
                        result.ListWarning.AddRange((confirmed.Data?.ListWarning ?? []).Select(w => $"{o.No}: {w}"));
                    }
                    else
                    {
                        result.Failed++;
                        result.ListError.Add($"{o.No} ({Day(o.MealDate)}): {confirmed.ResponseMessage}");
                    }
                }
                return Result<OrderWeekConfirmResultModel>.Success(result, string.Format(ConstantHelper.Message.OrderWeekPointsConfirmed, result.Confirmed, result.Points, result.Failed));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ConfirmWeekPoints: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>
        /// "Lấy số dự báo" – chỉ trả về, không lưu: buổi còn trống (mọi lựa chọn = 0, ngày chưa khóa) nhận số dự báo Đã công bố của (khách, điểm, ngày, loại suất),
        /// chia các lựa chọn theo tỉ lệ suất đặt 4 tuần gần nhất cùng thứ (không có lịch sử → dồn vào lựa chọn đầu); phần lẻ vào lựa chọn lớn nhất.
        /// </summary>
        public async Task<IResult<OrderWeekModel>> FillWeekFromForecast(OrderWeekRequest request)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                DateTime monday = MondayOf(request.WeekStart), end = monday.AddDays(7);
                var (week, error) = await BuildWeekAsync(request.CustomerId, request.DeliveryLocationId, monday);
                if (error != null)
                {
                    return Result<OrderWeekModel>.Fail(error);
                }
                var forecasts = await db.Forecast.AsNoTracking()
                    .Where(f => !f.IsDeleted && f.Status == ForecastPublished && f.CustomerId == request.CustomerId && f.DeliveryLocationId == request.DeliveryLocationId
                        && f.ForecastDate >= monday && f.ForecastDate < end)
                    .Select(f => new { Day = f.ForecastDate.Date, f.MealType, f.ForecastQuantity }).ToListAsync();
                DateTime since = monday.AddDays(-28);
                var history = await (from o in db.Order.AsNoTracking()
                                     join l in db.OrderLine.AsNoTracking() on o.Id equals l.OrderId
                                     where !o.IsDeleted && !l.IsDeleted && o.DeliveryLocationId == request.DeliveryLocationId && o.Status != OrderStatus.Cancelled
                                           && o.MealDate >= since && o.MealDate < monday
                                     select new { o.MealDate, l.MealType, l.MenuOptionNo, l.Quantity }).ToListAsync();
                int filled = 0;
                for (int i = 0; i < 7; i++)
                {
                    DateTime day = monday.AddDays(i);
                    foreach (var group in week!.ListRow.GroupBy(r => (r.MealType ?? string.Empty).ToUpperInvariant()))
                    {
                        var rows = group.Where(r => r.ListCell[i].Available && !r.ListCell[i].Locked).ToList();
                        if (rows.Count == 0 || group.Any(r => r.ListCell[i].Quantity > 0))
                        {
                            continue;
                        }
                        decimal fc = Math.Ceiling(forecasts.Where(f => f.Day == day && string.Equals(f.MealType, group.Key, StringComparison.OrdinalIgnoreCase)).Sum(f => f.ForecastQuantity));
                        if (fc <= 0)
                        {
                            continue;
                        }
                        var weights = rows.Select(r => history.Where(h => h.MealDate.DayOfWeek == day.DayOfWeek && string.Equals(h.MealType, group.Key, StringComparison.OrdinalIgnoreCase)
                            && h.MenuOptionNo == r.MenuOptionNo).Sum(h => h.Quantity)).ToList();
                        decimal total = weights.Sum();
                        if (total <= 0)
                        {
                            weights = rows.Select((_, k) => k == 0 ? 1m : 0m).ToList();
                            total = 1m;
                        }
                        decimal given = 0m;
                        for (int k = 0; k < rows.Count; k++)
                        {
                            decimal q = Math.Floor(fc * weights[k] / total);
                            rows[k].ListCell[i].Quantity = q;
                            given += q;
                        }
                        int top = weights.IndexOf(weights.Max());
                        rows[top].ListCell[i].Quantity += fc - given;
                        filled++;
                    }
                }
                if (filled == 0)
                {
                    week!.ListWarning.Add(ConstantHelper.Message.OrderWeekNoForecast);
                }
                return Result<OrderWeekModel>.Success(week!, string.Format(ConstantHelper.Message.OrderWeekForecastFilled, filled));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " FillWeekFromForecast: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Đọc mã ẩn ô A1 của phiếu: khách | điểm giao | thứ Hai.</summary>
        private static (Guid Customer, Guid Location, DateTime Week)? ReadSlipMarker(Stream file)
        {
            try
            {
                using var wb = new XLWorkbook(file);
                string[] m = (wb.Worksheets.FirstOrDefault()?.Cell(1, 1).GetString() ?? string.Empty).Split('|');
                return m.Length == 4 && m[0] == SlipMarker && Guid.TryParse(m[1], out var c) && Guid.TryParse(m[2], out var l)
                    && DateTime.TryParseExact(m[3], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var w) ? (c, l, w.Date) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Tải nhiều phiếu một lần: mỗi phiếu tự nhận khách / điểm giao / tuần qua mã ẩn → đọc như <see cref="ImportWeekSlip"/> → lưu Nháp (<see cref="SaveWeek"/>, Confirm = false).
        /// Phiếu sai mẫu / khác tuần đang mở → báo lỗi phiếu đó, phiếu khác vẫn lưu.
        /// </summary>
        public async Task<IResult<OrderWeekSlipBatchResultModel>> ImportWeekSlips(DateTime? weekStart, IReadOnlyList<(string Name, Stream File)> files)
        {
            try
            {
                DateTime? monday = weekStart == null ? null : MondayOf(weekStart.Value);
                var result = new OrderWeekSlipBatchResultModel { Files = files.Count };
                foreach (var (name, file) in files)
                {
                    var item = new OrderWeekSlipFileResultModel { FileName = name };
                    result.ListFile.Add(item);
                    using var copy = new MemoryStream();
                    await file.CopyToAsync(copy);
                    copy.Position = 0;
                    var marker = ReadSlipMarker(copy);
                    if (marker == null)
                    {
                        item.Message = ConstantHelper.Message.OrderSlipInvalid;
                        result.Failed++;
                        continue;
                    }
                    var (customerId, locationId, week) = marker.Value;
                    item.CustomerId = customerId;
                    item.DeliveryLocationId = locationId;
                    var names = await _unitOfWork.DbContext.CustomerDeliveryLocation.AsNoTracking().Where(l => l.Id == locationId)
                        .Select(l => new { l.Name, Customer = _unitOfWork.DbContext.Customer.Where(c => c.Id == l.CustomerId).Select(c => c.DisplayName ?? c.Name).FirstOrDefault() }).FirstOrDefaultAsync();
                    item.LocationName = names?.Name;
                    item.CustomerName = names?.Customer;
                    if (monday != null && week != monday)
                    {
                        item.Message = string.Format(ConstantHelper.Message.OrderSlipWrongWeek, Day(week), Day(monday.Value));
                        result.Failed++;
                        continue;
                    }
                    copy.Position = 0;
                    var read = await ImportWeekSlip(customerId, locationId, week, copy);
                    if (read.ResponseCode != (int)HttpStatusCode.OK || read.Data == null)
                    {
                        item.Message = read.ResponseMessage;
                        result.Failed++;
                        continue;
                    }
                    item.ListError = read.Data.ListError;
                    item.Cells = read.Data.ListCell.Count;
                    if (item.Cells == 0)
                    {
                        item.Message = read.Data.ListError.Count == 0 ? ConstantHelper.Message.OrderSlipEmpty : read.ResponseMessage;
                        item.Ok = read.Data.ListError.Count == 0;
                        if (!item.Ok)
                        {
                            result.Failed++;
                        }
                        continue;
                    }
                    var saved = await SaveWeek(new OrderWeekSaveRequest
                    {
                        CustomerId = customerId, DeliveryLocationId = locationId, WeekStart = week, Confirm = false, Note = ConstantHelper.Message.OrderSlipNote,
                        ListCell = read.Data.ListCell.Select(c => new OrderWeekCellRequest { MealDate = c.MealDate, ContractMealId = c.ContractMealId, ShiftId = c.ShiftId, MenuOptionNo = c.MenuOptionNo, Quantity = c.Quantity }).ToList(),
                    });
                    if (saved.ResponseCode != (int)HttpStatusCode.OK || saved.Data == null)
                    {
                        item.Message = saved.ResponseMessage;
                        result.Failed++;
                        continue;
                    }
                    item.Created = saved.Data.Created;
                    item.Updated = saved.Data.Updated;
                    item.ListError.AddRange(saved.Data.ListDay.Where(d => d.Action is WeekError or WeekSkipped or WeekApproval && d.Message != null).Select(d => $"{Day(d.MealDate)}: {d.Message}"));
                    item.Message = saved.ResponseMessage;
                    item.Ok = item.Created + item.Updated > 0 || item.ListError.Count == 0;
                    if (item.Ok)
                    {
                        result.Saved++;
                    }
                    else
                    {
                        result.Failed++;
                    }
                }
                return Result<OrderWeekSlipBatchResultModel>.Success(result, string.Format(ConstantHelper.Message.OrderSlipBatchRead, result.Saved, result.Files, result.Failed));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ImportWeekSlips: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>D-85 c: điểm giao (khách hợp đồng hiệu lực tại ngày, điểm đang hoạt động) chưa có đơn (chưa hủy) cho ngày ăn – dùng cho job 18:00 / 20:00.</summary>
        public static async Task<List<(Guid CustomerId, string? CustomerName, Guid? UserAssignId, string? LocationName)>> PointsWithoutOrderAsync(IUnitOfWork unitOfWork, DateTime mealDate)
        {
            var db = unitOfWork.DbContext;
            DateTime day = mealDate.Date, next = day.AddDays(1);
            var customers = await (from ct in db.Contract.AsNoTracking()
                                   join c in db.Customer.AsNoTracking() on ct.CustomerId equals c.Id
                                   where !ct.IsDeleted && !c.IsDeleted && ct.Status == ContractStatus.Active && ct.EffectiveFrom <= day && (ct.EffectiveTo == null || ct.EffectiveTo >= day)
                                   select new { c.Id, Name = c.DisplayName ?? c.Name, c.UserAssignId }).Distinct().ToListAsync();
            var ids = customers.Select(c => c.Id).ToList();
            var locations = await db.CustomerDeliveryLocation.AsNoTracking()
                .Where(l => ids.Contains(l.CustomerId) && !l.IsDeleted && (l.Status == null || l.Status == ActiveStatus)).Select(l => new { l.Id, l.CustomerId, l.Name }).ToListAsync();
            var ordered = (await db.Order.AsNoTracking().Where(o => !o.IsDeleted && o.MealDate >= day && o.MealDate < next && o.Status != OrderStatus.Cancelled && o.DeliveryLocationId != null)
                .Select(o => o.DeliveryLocationId!.Value).Distinct().ToListAsync()).ToHashSet();
            var byId = customers.GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First());
            return locations.Where(l => !ordered.Contains(l.Id))
                .Select(l => (CustomerId: l.CustomerId, CustomerName: byId[l.CustomerId].Name, UserAssignId: byId[l.CustomerId].UserAssignId, LocationName: l.Name))
                .OrderBy(x => x.CustomerName).ThenBy(x => x.LocationName).ToList();
        }
    }
}
