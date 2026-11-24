using System.Globalization;
using System.Net;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// ORD-OPT-1.4 / 1.5 (D-74 g, h): đăng ký suất tuần theo lựa chọn – lưới (buổi × lựa chọn) × 7 ngày cho 1 điểm giao; mỗi ngày = 1 đơn.
    /// Lưu / xác nhận đi qua đúng các nghiệp vụ đơn đang có (Create / UpdateLines / Submit / Confirm) – cùng kiểm tra giờ chốt, duyệt lệch, cảnh báo lệch dự báo.
    /// Phiếu đăng ký Excel mang mã ẩn để tải lên đọc lại đúng ô.
    /// </summary>
    public partial class OrderService
    {
        public const string WeekCreated = "CREATED", WeekUpdated = "UPDATED", WeekUnchanged = "UNCHANGED", WeekSkipped = "SKIPPED",
            WeekError = "ERROR", WeekApproval = "APPROVAL";

        /// <summary>Đánh dấu phiếu đăng ký (ô A1 ẩn): <c>DELICARE_SLIP|khách|điểm giao|thứ Hai yyyy-MM-dd</c>.</summary>
        public const string SlipMarker = "DELICARE_SLIP";

        public static DateTime MondayOf(DateTime date)
        {
            DateTime day = date.Date;
            return day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
        }

        private static string Day(DateTime d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        private sealed record WeekKey(Guid ContractMealId, Guid? ShiftId, int? Option);

        public async Task<IResult<OrderWeekModel>> GetWeek(OrderWeekRequest request)
        {
            try
            {
                var (model, error) = await BuildWeekAsync(request.CustomerId, request.DeliveryLocationId, request.WeekStart);
                if (model != null)
                {
                    await FillAveragesAsync(model);
                }
                return error != null ? Result<OrderWeekModel>.Fail(error) : Result<OrderWeekModel>.Success(model!, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetWeek: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>UI-6.6: TB 4 tuần cùng thứ (chỉ tuần có đơn) theo (buổi, lựa chọn) của điểm giao – cảnh báo lệch, không chặn.</summary>
        private async Task FillAveragesAsync(OrderWeekModel model)
        {
            DateTime since = model.WeekStart.AddDays(-28);
            var lines = await (from o in _unitOfWork.DbContext.Order.AsNoTracking()
                               join l in _unitOfWork.DbContext.OrderLine.AsNoTracking() on o.Id equals l.OrderId
                               where !o.IsDeleted && !l.IsDeleted && o.DeliveryLocationId == model.DeliveryLocationId && o.Status != OrderStatus.Cancelled
                                     && o.MealDate >= since && o.MealDate < model.WeekStart
                               select new { o.MealDate, l.MealType, l.MenuOptionNo, l.Quantity }).ToListAsync();
            foreach (var row in model.ListRow)
            {
                foreach (var cell in row.ListCell)
                {
                    var perWeek = lines.Where(x => x.MealDate.DayOfWeek == cell.MealDate.DayOfWeek && x.MenuOptionNo == row.MenuOptionNo
                            && string.Equals(x.MealType, row.MealType, StringComparison.OrdinalIgnoreCase))
                        .GroupBy(x => x.MealDate.Date).Select(g => g.Sum(x => x.Quantity)).ToList();
                    cell.Average4Weeks = perWeek.Count == 0 ? null : Math.Round(perWeek.Average(), 0);
                }
            }
        }

        /// <summary>
        /// Lưới tuần: hợp đồng Active từng ngày → suất hợp đồng × ca mặc định × lựa chọn của thực đơn áp dụng (điểm giao → khách → thực đơn chung);
        /// đơn đã có của ngày (khách, điểm giao) điền số theo (suất hợp đồng, ca, lựa chọn). Ngày khóa: quá giờ chốt / đơn đã chốt trở đi / đang chờ duyệt / không có hợp đồng.
        /// </summary>
        private async Task<(OrderWeekModel? Model, string? Error)> BuildWeekAsync(Guid customerId, Guid locationId, DateTime anyDay)
        {
            var db = _unitOfWork.DbContext;
            var customer = await db.Customer.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId && !c.IsDeleted);
            if (customer == null)
            {
                return (null, string.Format(ConstantHelper.Message.NotExist, "Khách hàng").Trim());
            }
            var location = await db.CustomerDeliveryLocation.AsNoTracking().FirstOrDefaultAsync(l => l.Id == locationId && l.CustomerId == customerId && !l.IsDeleted);
            if (location == null)
            {
                return (null, ConstantHelper.Message.MenuAssignmentLocationMismatch);
            }
            DateTime monday = MondayOf(anyDay), sunday = monday.AddDays(6);
            var model = new OrderWeekModel
            {
                CustomerId = customerId, CustomerName = customer.DisplayName ?? customer.Name, DeliveryLocationId = locationId,
                LocationCode = location.Code, LocationName = location.Name, WeekStart = monday, WeekEnd = sunday, Deadline = CutoffOf(monday),
            };
            DateTime next = sunday.AddDays(1);
            var orders = await db.Order.AsNoTracking()
                .Where(o => !o.IsDeleted && o.CustomerId == customerId && o.DeliveryLocationId == locationId && o.MealDate >= monday && o.MealDate < next && o.Status != OrderStatus.Cancelled)
                .ToListAsync();
            var orderIds = orders.Select(o => o.Id).ToList();
            var orderLines = await db.OrderLine.AsNoTracking().Where(l => orderIds.Contains(l.OrderId) && !l.IsDeleted).ToListAsync();
            var mealIdsOfLines = orderLines.Where(l => l.ContractMealId != null).Select(l => l.ContractMealId!.Value).Distinct().ToList();
            var mealsById = await db.ContractMeal.AsNoTracking().Where(m => mealIdsOfLines.Contains(m.Id)).ToDictionaryAsync(m => m.Id);
            var shiftNames = await db.Shift.AsNoTracking().Where(s => !s.IsDeleted).ToDictionaryAsync(s => s.Id, s => s.Name);
            var rows = new Dictionary<WeekKey, OrderWeekRowModel>();
            var defaultShift = new Dictionary<string, Shift?>(StringComparer.OrdinalIgnoreCase);

            OrderWeekRowModel RowOf(WeekKey key, ContractMeal meal)
            {
                if (!rows.TryGetValue(key, out var row))
                {
                    row = new OrderWeekRowModel
                    {
                        ContractMealId = meal.Id, MealType = meal.MealType, MealName = meal.MealName, ShiftId = key.ShiftId,
                        ShiftName = key.ShiftId is { } s ? shiftNames.GetValueOrDefault(s) : null, MenuOptionNo = key.Option,
                        ListCell = Enumerable.Range(0, 7).Select(i => new OrderWeekCellModel { MealDate = monday.AddDays(i) }).ToList(),
                    };
                    rows[key] = row;
                }
                return row;
            }

            for (int i = 0; i < 7; i++)
            {
                DateTime date = monday.AddDays(i);
                var order = orders.FirstOrDefault(o => o.MealDate.Date == date);
                var day = new OrderWeekDayModel
                {
                    MealDate = date, DayOfWeek = MenuService.MenuDayOf(date), CutoffAt = CutoffOf(date),
                    OrderId = order?.Id, OrderNumber = order?.OrderNumber ?? order?.OrderNo, Status = order?.Status, StatusName = order == null ? null : OrderStatus.Label(order.Status),
                    TotalQuantity = order?.TotalQuantity ?? 0m, TotalAmount = order?.TotalAmount ?? 0m,
                };
                var pending = order == null ? null : await PendingDeviationAsync(order.Id);
                day.LockReason = order != null && order.Status is not (OrderStatus.Draft or OrderStatus.Submitted or OrderStatus.Confirmed)
                        ? string.Format(ConstantHelper.Message.OrderWeekDayLockedStatus, day.OrderNumber, day.StatusName)
                    : IsAfterCutoff(date) ? string.Format(ConstantHelper.Message.OrderWeekDayLockedCutoff, CutoffOf(date).ToString("HH:mm dd/MM", CultureInfo.InvariantCulture))
                    : pending != null ? string.Format(ConstantHelper.Message.OrderApprovalPending, pending.RequestNo)
                    : null;
                var contract = await ActiveContractAsync(customerId, date, null);
                if (contract == null)
                {
                    day.LockReason ??= string.Format(ConstantHelper.Message.NoActiveContract, Day(date));
                }
                else
                {
                    model.ContractNumber ??= contract.ContractNumber ?? contract.ContractNo;
                    var meals = await db.ContractMeal.AsNoTracking()
                        .Where(m => m.ContractId == contract.Id && !m.IsDeleted && (m.Status == null || m.Status == ActiveStatus) && m.MealType != null).ToListAsync();
                    foreach (var meal in meals)
                    {
                        mealsById.TryAdd(meal.Id, meal);
                        if (!defaultShift.TryGetValue(meal.MealType!, out var shift))
                        {
                            shift = await DefaultShiftAsync(meal.MealType);
                            defaultShift[meal.MealType!] = shift;
                        }
                        var price = await _pricePolicyService.Resolve(new PriceResolveRequest { CustomerId = customerId, ContractId = contract.Id, MealType = meal.MealType, Date = date });
                        decimal? unitPrice = price.ResponseCode == (int)HttpStatusCode.OK ? price.Data?.UnitPrice : null;
                        if (model.ListPrice.All(p => !string.Equals(p.MealType, meal.MealType, StringComparison.OrdinalIgnoreCase)))
                        {
                            model.ListPrice.Add(new OrderWeekPriceModel { MealType = meal.MealType, MealName = meal.MealName, UnitPrice = unitPrice });
                        }
                        var menu = await _menuAssignmentService.ResolveAsync(customerId, locationId, meal.MealType, date);
                        var options = await MenuOptionRules.OptionsAsync(db, menu?.MenuId, meal.MealType, date);
                        if (options.Count == 0)
                        {
                            // Chưa có thực đơn / thực đơn không có món cho loại suất → 1 ô không theo lựa chọn (như tạo đơn hiện nay).
                            var cell = RowOf(new WeekKey(meal.Id, shift?.Id, null), meal).ListCell[i];
                            cell.Available = true;
                            cell.UnitPrice = unitPrice;
                            cell.MenuCode = menu?.MenuCode;
                            cell.MenuLevel = menu?.Level;
                            continue;
                        }
                        foreach (var option in options)
                        {
                            var cell = RowOf(new WeekKey(meal.Id, shift?.Id, option.OptionNo), meal).ListCell[i];
                            cell.Available = true;
                            cell.DishText = option.DishText;
                            cell.UnitPrice = unitPrice;
                            cell.MenuCode = menu!.MenuCode;
                            cell.MenuLevel = menu.Level;
                        }
                    }
                }
                if (order != null)
                {
                    foreach (var line in orderLines.Where(l => l.OrderId == order.Id && l.ContractMealId != null))
                    {
                        if (!mealsById.TryGetValue(line.ContractMealId!.Value, out var meal))
                        {
                            continue;
                        }
                        var cell = RowOf(new WeekKey(meal.Id, line.ShiftId, line.MenuOptionNo), meal).ListCell[i];
                        cell.Available = true;
                        cell.Quantity += line.Quantity;
                        cell.OrderLineId = line.Id;
                        cell.UnitPrice ??= line.UnitPrice;
                    }
                }
                day.Locked = day.LockReason != null;
                foreach (var row in rows.Values)
                {
                    row.ListCell[i].Locked = day.Locked;
                }
                model.ListDay.Add(day);
            }
            // Hàng tạo sau (ngày sau) cũng phải khóa đúng các ngày trước.
            foreach (var row in rows.Values)
            {
                for (int i = 0; i < 7; i++)
                {
                    row.ListCell[i].Locked = model.ListDay[i].Locked;
                }
            }
            model.ListRow = rows.Values.OrderBy(r => MenuService.MealRank(r.MealType)).ThenBy(r => r.MealName).ThenBy(r => r.ShiftName)
                .ThenBy(r => r.MenuOptionNo ?? 0).ToList();
            if (model.ListRow.Count == 0)
            {
                model.ListWarning.Add(string.Format(ConstantHelper.Message.NoActiveContract, $"{Day(monday)} – {Day(sunday)}"));
            }
            return (model, null);
        }

        /// <summary>
        /// ORD-OPT-1.4: lưu nháp / xác nhận cả tuần – từng ngày độc lập: chưa có đơn → Create (dòng số &gt; 0); có đơn sửa được → thêm dòng mới + UpdateLines
        /// (0 = bỏ dòng; đơn Đã xác nhận lệch &gt; ngưỡng → gửi duyệt như sửa đơn thường). Ngày khóa bỏ qua và báo lại. Confirm → Nháp: gửi + xác nhận; Đã gửi: xác nhận.
        /// </summary>
        public async Task<IResult<OrderWeekSaveModel>> SaveWeek(OrderWeekSaveRequest request)
        {
            try
            {
                DateTime monday = MondayOf(request.WeekStart), next = monday.AddDays(7);
                var cells = request.ListCell ?? [];
                if (cells.Any(c => c.MealDate.Date < monday || c.MealDate.Date >= next))
                {
                    return Result<OrderWeekSaveModel>.Fail(string.Format(ConstantHelper.Message.OrderWeekCellOutside, Day(monday), Day(next.AddDays(-1))));
                }
                if (cells.Any(c => c.Quantity < 0))
                {
                    return Result<OrderWeekSaveModel>.Fail(ConstantHelper.Message.OrderQuantityNegative);
                }
                var (week, error) = await BuildWeekAsync(request.CustomerId, request.DeliveryLocationId, monday);
                if (error != null)
                {
                    return Result<OrderWeekSaveModel>.Fail(error);
                }
                var result = new OrderWeekSaveModel();
                foreach (var day in week!.ListDay)
                {
                    var dayCells = cells.Where(c => c.MealDate.Date == day.MealDate)
                        .GroupBy(c => new WeekKey(c.ContractMealId, c.ShiftId, c.MenuOptionNo)).Select(g => g.Last()).ToList();
                    if (dayCells.Count == 0 && !(request.Confirm && day.OrderId != null))
                    {
                        continue;
                    }
                    var item = new OrderWeekSaveDayModel { MealDate = day.MealDate, OrderId = day.OrderId, OrderNumber = day.OrderNumber, Status = day.Status, StatusName = day.StatusName };
                    if (day.Locked)
                    {
                        if (day.OrderId != null || dayCells.Any(c => c.Quantity > 0))
                        {
                            item.Action = WeekSkipped;
                            item.Message = day.LockReason;
                            result.Skipped++;
                            result.ListDay.Add(item);
                        }
                        continue;
                    }
                    if (day.OrderId == null)
                    {
                        var input = dayCells.Where(c => c.Quantity > 0).ToList();
                        if (input.Count == 0)
                        {
                            continue;
                        }
                        var created = await Create(new OrderCreateRequest
                        {
                            CustomerId = request.CustomerId, DeliveryLocationId = request.DeliveryLocationId, MealDate = day.MealDate, Note = request.Note,
                            ListLine = input.Select(c => new OrderCreateLineRequest { ContractMealId = c.ContractMealId, ShiftId = c.ShiftId, MenuOptionNo = c.MenuOptionNo, Quantity = c.Quantity }).ToList(),
                        });
                        if (created.ResponseCode != (int)HttpStatusCode.OK || created.Data?.Order == null)
                        {
                            item.Action = WeekError;
                            item.Message = created.ResponseMessage;
                            result.ListDay.Add(item);
                            continue;
                        }
                        item.Action = WeekCreated;
                        item.OrderId = created.Data.Order.Id;
                        item.OrderNumber = created.Data.Order.OrderNumber ?? created.Data.Order.OrderNo;
                        item.Status = created.Data.Order.Status;
                        result.Created++;
                        result.ListWarning.AddRange(created.Data.ListWarning.Select(w => $"{Day(day.MealDate)}: {w}"));
                    }
                    else
                    {
                        var (action, message) = await SaveWeekDayAsync(day.OrderId.Value, dayCells);
                        item.Action = action;
                        item.Message = message;
                        if (action == WeekUpdated)
                        {
                            result.Updated++;
                        }
                        if (action is WeekError or WeekApproval)
                        {
                            result.ListDay.Add(item);
                            continue;
                        }
                    }
                    if (request.Confirm && item.OrderId is { } orderId)
                    {
                        string? status = await _unitOfWork.OrderRepository.GetAll().AsNoTracking().Where(o => o.Id == orderId).Select(o => o.Status).FirstOrDefaultAsync();
                        if (status == OrderStatus.Draft)
                        {
                            var submitted = await Submit(new OrderActionRequest { Id = orderId });
                            if (submitted.ResponseCode != (int)HttpStatusCode.OK)
                            {
                                item.Message = submitted.ResponseMessage;
                            }
                            else
                            {
                                status = OrderStatus.Submitted;
                            }
                        }
                        if (status == OrderStatus.Submitted)
                        {
                            var confirmed = await Confirm(new OrderActionRequest { Id = orderId });
                            if (confirmed.ResponseCode == (int)HttpStatusCode.OK)
                            {
                                status = OrderStatus.Confirmed;
                                result.Confirmed++;
                                result.ListWarning.AddRange((confirmed.Data?.ListWarning ?? []).Select(w => $"{Day(day.MealDate)}: {w}"));
                            }
                            else
                            {
                                item.Message = confirmed.ResponseMessage;
                            }
                        }
                        item.Status = status;
                    }
                    item.Status ??= day.Status;
                    item.StatusName = OrderStatus.Label(item.Status);
                    result.ListDay.Add(item);
                }
                (result.Week, _) = await BuildWeekAsync(request.CustomerId, request.DeliveryLocationId, monday);
                string summary = string.Format(ConstantHelper.Message.OrderWeekSaved, result.Created, result.Updated, result.Confirmed, result.Skipped);
                return Result<OrderWeekSaveModel>.Success(result, summary);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " SaveWeek: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Ngày đã có đơn: thêm dòng (lựa chọn) mới rồi sửa số dòng đã có qua UpdateLines (cùng kiểm tra / duyệt lệch).</summary>
        private async Task<(string Action, string? Message)> SaveWeekDayAsync(Guid orderId, List<OrderWeekCellRequest> cells)
        {
            var current = await _unitOfWork.OrderLineRepository.GetAll().AsNoTracking().Where(l => l.OrderId == orderId && !l.IsDeleted).ToListAsync();
            var changes = new List<OrderLineQuantityRequest>();
            var added = new List<OrderWeekCellRequest>();
            foreach (var cell in cells)
            {
                var line = current.FirstOrDefault(l => l.ContractMealId == cell.ContractMealId && l.MenuOptionNo == cell.MenuOptionNo
                    && (cell.ShiftId == null || l.ShiftId == cell.ShiftId));
                if (line == null)
                {
                    if (cell.Quantity > 0)
                    {
                        added.Add(cell);
                    }
                }
                else if (line.Quantity != cell.Quantity)
                {
                    changes.Add(new OrderLineQuantityRequest { Id = line.Id, Quantity = cell.Quantity });
                }
            }
            if (added.Count == 0 && changes.Count == 0)
            {
                return (WeekUnchanged, null);
            }
            if (added.Count != 0)
            {
                using var tran = await _unitOfWork.OpenTransaction();
                try
                {
                    var order = _unitOfWork.OrderRepository.GetSingle(o => o.Id == orderId && !o.IsDeleted);
                    var existing = _unitOfWork.OrderLineRepository.GetAll().Where(l => l.OrderId == orderId && !l.IsDeleted).ToList();
                    foreach (var cell in added)
                    {
                        var (line, error) = await BuildNewLineAsync(order!, new OrderCreateLineRequest
                        {
                            ContractMealId = cell.ContractMealId, ShiftId = cell.ShiftId, MenuOptionNo = cell.MenuOptionNo, Quantity = cell.Quantity,
                        }, existing);
                        if (error != null)
                        {
                            _unitOfWork.RollbackTransaction();
                            _unitOfWork.DbContext.ChangeTracker.Clear();
                            return (WeekError, error);
                        }
                        _unitOfWork.OrderLineRepository.Add(line!);
                        existing.Add(line!);
                    }
                    await _unitOfWork.OrderLineRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                    string? totalsError = await ApplyLinesAsync(orderId, []);
                    if (totalsError != null)
                    {
                        _unitOfWork.RollbackTransaction();
                        _unitOfWork.DbContext.ChangeTracker.Clear();
                        return (WeekError, totalsError);
                    }
                    _unitOfWork.CommitTransaction();
                }
                catch
                {
                    _unitOfWork.RollbackTransaction();
                    _unitOfWork.DbContext.ChangeTracker.Clear();
                    throw;
                }
            }
            if (changes.Count != 0)
            {
                var updated = await UpdateLines(new OrderUpdateLinesRequest { Id = orderId, ListLine = changes });
                if (updated.ResponseCode != (int)HttpStatusCode.OK)
                {
                    return (WeekError, updated.ResponseMessage);
                }
                if (updated.Data?.ApprovalRequested == true)
                {
                    return (WeekApproval, updated.ResponseMessage);
                }
            }
            return (WeekUpdated, null);
        }

        /// <summary>ORD-OPT-1.4: số tuần trước (cùng thứ, suất hợp đồng, ca, lựa chọn) làm gợi ý cho tuần này – chỉ trả về, không lưu; ô khóa / không có lựa chọn bỏ qua.</summary>
        public async Task<IResult<OrderWeekModel>> CopyPreviousWeek(OrderWeekRequest request)
        {
            try
            {
                DateTime monday = MondayOf(request.WeekStart);
                var (week, error) = await BuildWeekAsync(request.CustomerId, request.DeliveryLocationId, monday);
                if (error != null)
                {
                    return Result<OrderWeekModel>.Fail(error);
                }
                var (previous, _) = await BuildWeekAsync(request.CustomerId, request.DeliveryLocationId, monday.AddDays(-7));
                int copied = 0;
                foreach (var row in week!.ListRow)
                {
                    var source = previous?.ListRow.FirstOrDefault(r => r.ContractMealId == row.ContractMealId && r.ShiftId == row.ShiftId && r.MenuOptionNo == row.MenuOptionNo)
                        ?? previous?.ListRow.FirstOrDefault(r => string.Equals(r.MealType, row.MealType, StringComparison.OrdinalIgnoreCase) && r.ShiftId == row.ShiftId && r.MenuOptionNo == row.MenuOptionNo);
                    for (int i = 0; i < 7; i++)
                    {
                        var cell = row.ListCell[i];
                        if (cell.Locked || !cell.Available)
                        {
                            continue;
                        }
                        cell.Quantity = source?.ListCell[i].Quantity ?? 0m;
                        copied += cell.Quantity > 0 ? 1 : 0;
                    }
                }
                if (previous == null || copied == 0)
                {
                    week.ListWarning.Add(ConstantHelper.Message.OrderWeekNothingToCopy);
                }
                return Result<OrderWeekModel>.Success(week, string.Format(ConstantHelper.Message.OrderWeekCopied, copied));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " CopyPreviousWeek: ", ex.ToString()));
                throw;
            }
        }

        // ============================================================ ORD-OPT-1.5 – phiếu đăng ký suất (Excel có mã ẩn)

        /// <summary>
        /// D-74 h: "Phiếu đăng ký suất" của khách + điểm giao + tuần (từ mẫu Thực đơn gửi khách): mỗi (buổi, lựa chọn) 2 hàng – món trong set / ô Số suất (tô vàng) dưới từng ngày.
        /// Mã ẩn: ô A1 (<see cref="SlipMarker"/>|khách|điểm|thứ Hai), hàng 2 ẩn = ngày yyyy-MM-dd từng cột, cột J ẩn = suất hợp đồng|ca|lựa chọn của hàng Số suất.
        /// </summary>
        public async Task<IResult<byte[]>> ExportWeekSlip(OrderWeekRequest request)
        {
            try
            {
                var (week, error) = await BuildWeekAsync(request.CustomerId, request.DeliveryLocationId, request.WeekStart);
                if (error != null)
                {
                    return Result<byte[]>.Fail(error);
                }
                if (week!.ListRow.Count == 0)
                {
                    return Result<byte[]>.Fail(week.ListWarning.FirstOrDefault() ?? ConstantHelper.Message.OrderNeedsLine);
                }
                var company = await _unitOfWork.DbContext.Company.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.CreatedDate).FirstOrDefaultAsync();
                const string brand = "#91AF42";
                const int firstDayCol = 3, codeCol = 10;
                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Phieu dang ky");
                ws.Cell(1, 1).Value = $"{SlipMarker}|{week.CustomerId:D}|{week.DeliveryLocationId:D}|{week.WeekStart:yyyy-MM-dd}";
                for (int i = 0; i < 7; i++)
                {
                    ws.Cell(2, firstDayCol + i).Value = week.ListDay[i].MealDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                }
                ws.Row(1).Hide();
                ws.Row(2).Hide();
                ws.Cell(3, 1).Value = company?.Name ?? "DELICARE";
                ws.Cell(3, 1).Style.Font.Bold = true;
                ws.Range(5, 1, 5, 9).Merge().Value = "PHIẾU ĐĂNG KÝ SUẤT ĂN";
                ws.Range(5, 1, 5, 9).Style.Font.SetBold().Font.SetFontSize(14).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                ws.Cell(6, 1).Value = $"Khách: {week.CustomerName} · Điểm giao: {week.LocationName} · Tuần {Day(week.WeekStart)} – {Day(week.WeekEnd)}";
                ws.Cell(7, 1).Value = $"Hạn gửi lại: {week.Deadline:HH:mm dd/MM/yyyy} · Ghi số suất vào ô tô vàng (trống / 0 = không đặt). Không sửa / xóa hàng, cột.";
                int header = 9;
                ws.Cell(header, 1).Value = "Buổi";
                ws.Cell(header, 2).Value = "Lựa chọn";
                for (int i = 0; i < 7; i++)
                {
                    ws.Cell(header, firstDayCol + i).Value = $"{MenuService.DayLabel(week.ListDay[i].DayOfWeek)} {week.ListDay[i].MealDate:dd/MM}";
                }
                ws.Range(header, 1, header, 9).Style.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(XLColor.FromHtml(brand))
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center).Alignment.SetWrapText(true);
                int r = header + 1;
                foreach (var row in week.ListRow)
                {
                    ws.Range(r, 1, r + 1, 1).Merge().Value = MenuService.MealLabel(row.MealType) + (string.IsNullOrWhiteSpace(row.ShiftName) ? string.Empty : $" ({row.ShiftName})");
                    ws.Range(r, 2, r + 1, 2).Merge().Value = row.MenuOptionNo is { } no ? $"Lựa chọn {no}" : "–";
                    ws.Cell(r + 1, codeCol).Value = $"{row.ContractMealId:D}|{row.ShiftId?.ToString("D") ?? string.Empty}|{row.MenuOptionNo?.ToString(CultureInfo.InvariantCulture) ?? string.Empty}";
                    for (int i = 0; i < 7; i++)
                    {
                        var cell = row.ListCell[i];
                        ws.Cell(r, firstDayCol + i).Value = cell.Available ? cell.DishText ?? string.Empty : "–";
                        var qty = ws.Cell(r + 1, firstDayCol + i);
                        if (cell.Available && !cell.Locked)
                        {
                            if (cell.Quantity > 0)
                            {
                                qty.Value = cell.Quantity;
                            }
                            qty.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#FFF5CC"));
                        }
                        else
                        {
                            if (cell.Locked && cell.Quantity > 0)
                            {
                                qty.Value = cell.Quantity;
                            }
                            qty.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#EEEEEE"));
                        }
                        qty.Style.NumberFormat.Format = "#,##0";
                    }
                    ws.Range(r, 1, r + 1, 2).Style.Font.SetBold().Alignment.SetVertical(XLAlignmentVerticalValues.Center);
                    ws.Range(r, firstDayCol, r, 9).Style.Alignment.SetWrapText(true).Alignment.SetVertical(XLAlignmentVerticalValues.Top).Font.SetFontSize(9);
                    r += 2;
                }
                ws.Range(header, 1, r - 1, 9).Style.Border.SetInsideBorder(XLBorderStyleValues.Thin).Border.SetOutsideBorder(XLBorderStyleValues.Thin);
                ws.Column(1).Width = 14;
                ws.Column(2).Width = 11;
                for (int i = 0; i < 7; i++)
                {
                    ws.Column(firstDayCol + i).Width = 20;
                }
                ws.Column(codeCol).Hide();
                ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
                ws.PageSetup.FitToPages(1, 0);
                using var stream = new MemoryStream();
                wb.SaveAs(stream);
                return Result<byte[]>.Success(stream.ToArray(), ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ExportWeekSlip: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>
        /// D-74 h: đọc phiếu đăng ký Excel (mẫu <see cref="ExportWeekSlip"/>) vào lưới – chưa lưu. Sai mẫu / khác khách, điểm giao, tuần → lỗi;
        /// ô lựa chọn không còn trong thực đơn / ngày đã khóa / số không hợp lệ → dòng lỗi, bỏ ô đó.
        /// </summary>
        public async Task<IResult<OrderWeekSlipModel>> ImportWeekSlip(Guid customerId, Guid locationId, DateTime weekStart, Stream file)
        {
            try
            {
                DateTime monday = MondayOf(weekStart);
                var (week, error) = await BuildWeekAsync(customerId, locationId, monday);
                if (error != null)
                {
                    return Result<OrderWeekSlipModel>.Fail(error);
                }
                XLWorkbook wb;
                try
                {
                    wb = new XLWorkbook(file);
                }
                catch (Exception)
                {
                    return Result<OrderWeekSlipModel>.Fail(ConstantHelper.Message.OrderSlipInvalid);
                }
                using (wb)
                {
                    var ws = wb.Worksheets.FirstOrDefault();
                    string[] marker = (ws?.Cell(1, 1).GetString() ?? string.Empty).Split('|');
                    if (ws == null || marker.Length != 4 || marker[0] != SlipMarker)
                    {
                        return Result<OrderWeekSlipModel>.Fail(ConstantHelper.Message.OrderSlipInvalid);
                    }
                    if (!Guid.TryParse(marker[1], out var slipCustomer) || slipCustomer != customerId || !Guid.TryParse(marker[2], out var slipLocation) || slipLocation != locationId
                        || !DateTime.TryParseExact(marker[3], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var slipWeek) || slipWeek.Date != monday)
                    {
                        return Result<OrderWeekSlipModel>.Fail(string.Format(ConstantHelper.Message.OrderSlipOtherWeek, week!.CustomerName, week.LocationName, Day(monday)));
                    }
                    const int firstDayCol = 3, codeCol = 10;
                    var model = new OrderWeekSlipModel { CustomerId = customerId, DeliveryLocationId = locationId, WeekStart = monday };
                    var dates = new DateTime?[7];
                    for (int i = 0; i < 7; i++)
                    {
                        dates[i] = DateTime.TryParseExact(ws.Cell(2, firstDayCol + i).GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d.Date : null;
                    }
                    int lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
                    for (int r = 3; r <= lastRow; r++)
                    {
                        string code = ws.Cell(r, codeCol).GetString().Trim();
                        if (code.Length == 0)
                        {
                            continue;
                        }
                        string[] parts = code.Split('|');
                        if (parts.Length != 3 || !Guid.TryParse(parts[0], out var mealId))
                        {
                            model.ListError.Add(string.Format(ConstantHelper.Message.OrderSlipRowInvalid, r));
                            continue;
                        }
                        Guid? shiftId = Guid.TryParse(parts[1], out var s) ? s : null;
                        int? option = int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var o) ? o : null;
                        var row = week!.ListRow.FirstOrDefault(x => x.ContractMealId == mealId && x.ShiftId == shiftId && x.MenuOptionNo == option);
                        string label = $"{MenuService.MealLabel(ws.Cell(r - 1, 1).GetString())} {(option is { } n ? "lựa chọn " + n : string.Empty)}".Trim();
                        for (int i = 0; i < 7; i++)
                        {
                            var cell = ws.Cell(r, firstDayCol + i);
                            if (cell.IsEmpty() || dates[i] is not { } date)
                            {
                                continue;
                            }
                            if (!cell.TryGetValue(out decimal qty) || qty < 0)
                            {
                                model.ListError.Add(string.Format(ConstantHelper.Message.OrderSlipQuantityInvalid, cell.Address.ToStringRelative(), cell.GetString()));
                                continue;
                            }
                            var target = row?.ListCell.FirstOrDefault(c => c.MealDate == date);
                            if (target == null || !target.Available)
                            {
                                if (qty > 0)
                                {
                                    model.ListError.Add(string.Format(ConstantHelper.Message.OrderSlipOptionGone, cell.Address.ToStringRelative(), label, Day(date)));
                                }
                                continue;
                            }
                            if (target.Locked)
                            {
                                if (qty != target.Quantity)
                                {
                                    model.ListError.Add(string.Format(ConstantHelper.Message.OrderSlipDayLocked, cell.Address.ToStringRelative(), Day(date)));
                                }
                                continue;
                            }
                            model.ListCell.Add(new OrderWeekSlipCellModel { MealDate = date, ContractMealId = mealId, ShiftId = shiftId, MenuOptionNo = option, Quantity = qty });
                        }
                    }
                    return Result<OrderWeekSlipModel>.Success(model, string.Format(ConstantHelper.Message.OrderSlipRead, model.ListCell.Count, model.ListError.Count));
                }
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ImportWeekSlip: ", ex.ToString()));
                throw;
            }
        }
    }
}
