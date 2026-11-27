using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Interface;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;

namespace AppDelicare.Services.Implement.Retail
{
    /// <summary>RETAIL-1: đơn khách lẻ – màn nội bộ (docs/frontend-handoff/RETAIL-1.md mục 5.2). Người thao tác = ICurrentActor.</summary>
    public class RetailOrderService(IUnitOfWork unitOfWork, IClock clock, ICurrentActor currentActor, IOptions<BusinessRulesOptions> rules,
        IOptions<ReferenceListsOptions> referenceLists, IDocumentNumberService documentNumbers) : IRetailOrderService
    {
        private const int MaxPageSize = 1000;
        private readonly RetailRulesOptions _rules = rules.Value.Retail;
        private readonly ReferenceListsOptions _lists = referenceLists.Value;

        // ------------------------------------------------------------------ Đọc
        public async Task<IResult<RetailOrderListModel>> GetList(RetailOrderListRequest request)
        {
            try
            {
                var baseQuery = Filter(request, withStatus: false);
                var statuses = SplitStatuses(request.OrderStatus);
                var query = statuses.Count > 0 ? baseQuery.Where(o => statuses.Contains(o.OrderStatus!)) : baseQuery;
                int page = Math.Max(1, request.Page), size = Math.Clamp(request.PageSize, 1, MaxPageSize);
                int total = await query.CountAsync();
                var orders = await query.OrderBy(o => o.OrderStatus == RetailOrderStatus.New ? 0 : 1)
                    .ThenByDescending(o => o.CreatedDate).Skip((page - 1) * size).Take(size).ToListAsync();
                var counts = await baseQuery.GroupBy(o => o.OrderStatus).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
                DateTime today = clock.Today;
                var db = unitOfWork.DbContext;
                var todays = db.RetailOrder.AsNoTracking().Where(o => !o.IsDeleted && o.DeliveryDate == today);
                return Result<RetailOrderListModel>.Success(new RetailOrderListModel
                {
                    ListItem = await ItemsAsync(orders),
                    TotalRecords = total,
                    StatusCounts = RetailOrderStatus.All.ToDictionary(s => s, s => counts.FirstOrDefault(c => c.Key == s)?.Count ?? 0),
                    TodayCount = await todays.CountAsync(o => o.OrderStatus != RetailOrderStatus.Cancelled),
                    TodayAmount = await todays.Where(o => o.OrderStatus == RetailOrderStatus.Completed).SumAsync(o => (decimal?)o.TotalAmount) ?? 0,
                }, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetList: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<RetailOrderDetailModel>> GetDetail(Guid id)
        {
            try
            {
                var db = unitOfWork.DbContext;
                var o = await db.RetailOrder.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
                if (o == null)
                {
                    return Result<RetailOrderDetailModel>.Fail(RetailMessage.OrderNotFound);
                }
                var lines = await db.RetailOrderLine.AsNoTracking().Where(l => l.RetailOrderId == id && !l.IsDeleted).OrderBy(l => l.CreatedDate).ToListAsync();
                var progress = await RetailProgress.DetailAsync(db, o, _lists);
                return Result<RetailOrderDetailModel>.Success(new RetailOrderDetailModel
                {
                    Order = Item(o, lines),
                    Lines = lines.Select(l => new RetailOrderLineItemModel
                    {
                        Id = l.Id, RetailProductId = l.RetailProductId, ProductName = l.ProductName ?? string.Empty, Unit = l.Unit ?? string.Empty,
                        Quantity = l.Quantity, UnitPrice = l.UnitPrice, LineAmount = l.LineAmount,
                    }).ToList(),
                    AllowedTransitions = [.. RetailOrderStatus.AllowedFrom(o.OrderStatus)],
                    DeliveryRouteId = o.DeliveryRouteId,
                    Route = progress.Route,
                    ListRouteOption = await RetailProgress.RouteOptionsAsync(db, o.DeliveryDate),
                    StageLabel = progress.StageLabel,
                    ListStep = progress.ListStep,
                    HasAccount = o.RetailCustomerId != null,
                }, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetDetail: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Tổng hợp cho bếp: đơn Đã xác nhận + Đang giao của ngày nhận, cộng số lượng theo món × bữa.</summary>
        public async Task<IResult<RetailPrepSummaryModel>> GetPrepSummary(string? date)
        {
            try
            {
                DateTime day = RetailRules.ParseDate(date) ?? clock.Today.AddDays(1);
                var db = unitOfWork.DbContext;
                var rows = await (from o in db.RetailOrder.AsNoTracking()
                                  join l in db.RetailOrderLine.AsNoTracking() on o.Id equals l.RetailOrderId
                                  where !o.IsDeleted && !l.IsDeleted && o.DeliveryDate == day
                                      && (o.OrderStatus == RetailOrderStatus.Confirmed || o.OrderStatus == RetailOrderStatus.Delivering)
                                  select new { o.Id, o.MealType, l.ProductName, l.Unit, l.Quantity }).ToListAsync();
                var order = RetailRules.Meals(_lists).Select(m => m.Label).ToList();
                var meals = rows.GroupBy(r => r.MealType ?? string.Empty)
                    .OrderBy(g => order.IndexOf(g.Key) is var i && i >= 0 ? i : int.MaxValue)
                    .Select(g => new RetailPrepMealModel
                    {
                        MealType = g.Key,
                        OrderCount = g.Select(r => r.Id).Distinct().Count(),
                        Items = g.GroupBy(r => (r.ProductName ?? string.Empty, r.Unit ?? string.Empty))
                            .Select(x => new RetailPrepItemModel { ProductName = x.Key.Item1, Unit = x.Key.Item2, Quantity = x.Sum(r => r.Quantity) })
                            .OrderByDescending(x => x.Quantity).ThenBy(x => x.ProductName).ToList(),
                    }).ToList();
                return Result<RetailPrepSummaryModel>.Success(new RetailPrepSummaryModel { Date = RetailRules.Iso(day), Meals = meals }, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetPrepSummary: ", ex.ToString()));
                throw;
            }
        }

        // ------------------------------------------------------------------ Ghi
        /// <summary>Nhân viên lập đơn khi khách gọi / nhắn Zalo → trạng thái Đã xác nhận (đã nói chuyện với khách).</summary>
        public async Task<IResult<RetailCreatedModel>> Create(RetailOrderCreateRequest request)
        {
            using var tran = await unitOfWork.OpenTransaction();
            try
            {
                var actor = currentActor.RequireActor();
                string? channel = RetailCodes.StaffChannels.FirstOrDefault(c => string.Equals(c, request.Channel?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (channel == null)
                {
                    return Fail<RetailCreatedModel>(RetailMessage.ChannelInvalid);
                }
                string? name = RetailRules.Clean(request.CustomerName, 200);
                if (name == null || name.Length < 2)
                {
                    return Fail<RetailCreatedModel>(RetailMessage.NameRequired);
                }
                string? phone = RetailRules.NormalizePhone(request.PhoneNumber);
                if (phone == null)
                {
                    return Fail<RetailCreatedModel>(RetailMessage.PhoneInvalid);
                }
                DateTime now = clock.Now;
                var (head, error) = CheckHead(request.Address, request.DeliveryDate, request.MealType, request.ShippingFee, request.PaymentMethod, now, null);
                if (head == null)
                {
                    return Fail<RetailCreatedModel>(error!);
                }
                var h = head.Value;
                var (lines, lineError) = await RetailRules.PriceLinesAsync(unitOfWork.DbContext, request.Lines, h.Meal, _rules, _lists);
                if (lineError != null)
                {
                    return Fail<RetailCreatedModel>(lineError);
                }
                decimal sub = lines.Sum(l => l.LineAmount);
                var entity = new RetailOrder
                {
                    Id = Guid.NewGuid(),
                    OrderNumber = await documentNumbers.NextAsync(DocumentKind.RetailOrder, now),
                    Channel = channel, CustomerName = name, PhoneNumber = phone, Address = h.Address, DeliveryDate = h.Date, MealType = h.Meal,
                    Note = RetailRules.Clean(request.Note, 1000),
                    OrderStatus = RetailOrderStatus.Confirmed, ConfirmedAt = now, ConfirmedBy = actor.UserId,
                    SubTotal = sub, ShippingFee = h.Fee, TotalAmount = sub + h.Fee,
                    PaymentMethod = h.Payment ?? RetailCodes.PayCod, PaymentStatus = RetailCodes.Unpaid,
                };
                unitOfWork.RetailOrderRepository.Add(entity);
                foreach (var l in lines)
                {
                    unitOfWork.RetailOrderLineRepository.Add(RetailRules.ToEntity(l, entity.Id));
                }
                await unitOfWork.RetailOrderRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                unitOfWork.CommitTransaction();
                return Result<RetailCreatedModel>.Success(new RetailCreatedModel { Id = entity.Id, OrderNumber = entity.OrderNumber! }, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Create: ", ex.ToString()));
                unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>Sửa đơn Mới / Đã xác nhận: dòng món (giá tính lại theo giá hiện hành), địa chỉ, ngày, bữa, phí giao, ghi chú.</summary>
        public async Task<IResult<bool>> UpdateLines(RetailOrderUpdateLinesRequest request)
        {
            using var tran = await unitOfWork.OpenTransaction();
            try
            {
                currentActor.RequireActor();
                var db = unitOfWork.DbContext;
                var o = await db.RetailOrder.FirstOrDefaultAsync(x => x.Id == request.Id && !x.IsDeleted);
                if (o == null)
                {
                    return Fail<bool>(RetailMessage.OrderNotFound);
                }
                if (!RetailOrderStatus.Editable.Contains(o.OrderStatus))
                {
                    return Fail<bool>(string.Format(RetailMessage.NotEditable, RetailOrderStatus.Label(o.OrderStatus)));
                }
                var (head, error) = CheckHead(request.Address, request.DeliveryDate, request.MealType, request.ShippingFee ?? o.ShippingFee,
                    request.PaymentMethod ?? o.PaymentMethod, clock.Now, o.DeliveryDate);
                if (head == null)
                {
                    return Fail<bool>(error!);
                }
                var h = head.Value;
                var old = await db.RetailOrderLine.Where(l => l.RetailOrderId == o.Id && !l.IsDeleted).ToListAsync();
                var (lines, lineError) = await RetailRules.PriceLinesAsync(db, request.Lines, h.Meal, _rules, _lists, old.Select(l => l.RetailProductId).ToHashSet());
                if (lineError != null)
                {
                    return Fail<bool>(lineError);
                }
                // Dòng cũ xóa mềm, ghi dòng mới (giá chụp lại) – AuditChange giữ bản trước.
                foreach (var l in old)
                {
                    l.IsDeleted = true;
                    unitOfWork.RetailOrderLineRepository.Update(l);
                }
                await unitOfWork.RetailOrderLineRepository.CommitAsync(ConstantHelper.TypeSave.Delete);
                foreach (var l in lines)
                {
                    unitOfWork.RetailOrderLineRepository.Add(RetailRules.ToEntity(l, o.Id));
                }
                o.Address = h.Address;
                o.DeliveryDate = h.Date;
                o.MealType = h.Meal;
                o.Note = RetailRules.Clean(request.Note, 1000);
                o.ShippingFee = h.Fee;
                o.PaymentMethod = h.Payment ?? o.PaymentMethod;
                o.SubTotal = lines.Sum(l => l.LineAmount);
                o.TotalAmount = o.SubTotal + o.ShippingFee;
                unitOfWork.RetailOrderRepository.Update(o);
                await unitOfWork.RetailOrderRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                unitOfWork.CommitTransaction();
                return Result<bool>.Success(true, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " UpdateLines: ", ex.ToString()));
                unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task<IResult<bool>> Transition(RetailOrderTransitionRequest request)
        {
            using var tran = await unitOfWork.OpenTransaction();
            try
            {
                var actor = currentActor.RequireActor();
                var db = unitOfWork.DbContext;
                var o = await db.RetailOrder.FirstOrDefaultAsync(x => x.Id == request.Id && !x.IsDeleted);
                if (o == null)
                {
                    return Fail<bool>(RetailMessage.OrderNotFound);
                }
                string? to = RetailOrderStatus.All.FirstOrDefault(s => string.Equals(s, request.To?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (to == null || to == RetailOrderStatus.New)
                {
                    return Fail<bool>(RetailMessage.TargetInvalid);
                }
                if (!RetailOrderStatus.CanMove(o.OrderStatus, to))
                {
                    return Fail<bool>(string.Format(RetailMessage.TransitionInvalid, RetailOrderStatus.Label(o.OrderStatus), RetailOrderStatus.Label(to)));
                }
                DateTime now = clock.Now;
                switch (to)
                {
                    case RetailOrderStatus.Confirmed:
                        if (request.ShippingFee is { } fee)
                        {
                            if (fee < 0)
                            {
                                return Fail<bool>(RetailMessage.ShippingFeeInvalid);
                            }
                            o.ShippingFee = fee;
                            o.TotalAmount = o.SubTotal + fee;
                        }
                        if (!string.IsNullOrWhiteSpace(request.PaymentMethod))
                        {
                            string? pay = RetailCodes.PaymentMethods.FirstOrDefault(p => string.Equals(p, request.PaymentMethod.Trim(), StringComparison.OrdinalIgnoreCase));
                            if (pay == null)
                            {
                                return Fail<bool>(RetailMessage.PaymentInvalid);
                            }
                            o.PaymentMethod = pay;
                        }
                        o.ConfirmedAt = now;
                        o.ConfirmedBy = actor.UserId;
                        break;
                    case RetailOrderStatus.Delivering:
                        o.DeliveringAt = now;
                        if (request.DeliveryRouteId is { } rid)
                        {
                            if (await RouteErrorAsync(db, o, rid) is { } routeError)
                            {
                                return Fail<bool>(routeError);
                            }
                            o.DeliveryRouteId = rid;
                        }
                        break;
                    case RetailOrderStatus.Completed:
                        o.CompletedAt = now;
                        o.PaymentStatus = RetailCodes.Paid;
                        break;
                    case RetailOrderStatus.Cancelled:
                        string? reason = RetailRules.Clean(request.Reason, 500);
                        if (reason == null || reason.Length < 3)
                        {
                            return Fail<bool>(RetailMessage.CancelReasonRequired);
                        }
                        o.CancelledAt = now;
                        o.CancelReason = reason;
                        break;
                }
                o.OrderStatus = to;
                unitOfWork.RetailOrderRepository.Update(o);
                await unitOfWork.RetailOrderRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                unitOfWork.CommitTransaction();
                return Result<bool>.Success(true, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Transition: ", ex.ToString()));
                unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task<IResult<FileContentModel>> Export(RetailOrderListRequest request)
        {
            try
            {
                var statuses = SplitStatuses(request.OrderStatus);
                var query = Filter(request, withStatus: false);
                if (statuses.Count > 0)
                {
                    query = query.Where(o => statuses.Contains(o.OrderStatus!));
                }
                var orders = await query.OrderBy(o => o.DeliveryDate).ThenBy(o => o.OrderNumber).Take(20000).ToListAsync();
                var items = await ItemsAsync(orders);
                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Đơn khách lẻ");
                string[] head = ["STT", "Số đơn", "Ngày đặt", "Kênh", "Khách hàng", "Điện thoại", "Địa chỉ giao", "Ngày nhận", "Bữa", "Món",
                    "Tạm tính", "Phí giao", "Tổng tiền", "Thanh toán", "Đã thanh toán", "Trạng thái", "Lý do hủy", "Ghi chú"];
                for (int c = 0; c < head.Length; c++)
                {
                    ws.Cell(1, c + 1).Value = head[c];
                }
                ws.Row(1).Style.Font.Bold = true;
                int r = 2;
                foreach (var o in items)
                {
                    object?[] v = [r - 1, o.OrderNumber, o.CreatedDate, ChannelLabel(o.Channel), o.CustomerName, o.PhoneNumber, o.Address, o.DeliveryDate, o.MealType,
                        o.LineSummary, o.SubTotal, o.ShippingFee, o.TotalAmount, o.PaymentMethod == RetailCodes.PayBank ? "Chuyển khoản" : "Tiền mặt khi nhận",
                        o.PaymentStatus == RetailCodes.Paid ? "Đã thanh toán" : "Chưa thanh toán", RetailOrderStatus.Label(o.OrderStatus), o.CancelReason, o.Note];
                    for (int c = 0; c < v.Length; c++)
                    {
                        ws.Cell(r, c + 1).Value = v[c] switch
                        {
                            null => Blank.Value,
                            DateTime d => d,
                            decimal m => m,
                            int i => i,
                            var x => x.ToString(),
                        };
                    }
                    r++;
                }
                ws.Column(3).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
                ws.Column(8).Style.DateFormat.Format = "dd/MM/yyyy";
                foreach (int c in new[] { 11, 12, 13 })
                {
                    ws.Column(c).Style.NumberFormat.Format = "#,##0";
                }
                ws.SheetView.FreezeRows(1);
                ws.Columns().AdjustToContents(1, Math.Min(r, 200), 8, 60);
                using var ms = new MemoryStream();
                wb.SaveAs(ms);
                return Result<FileContentModel>.Success(new FileContentModel
                {
                    Content = ms.ToArray(), ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    FileName = $"Don_khach_le_{clock.Now:yyyyMMdd_HHmm}.xlsx",
                }, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Export: ", ex.ToString()));
                throw;
            }
        }

        // ------------------------------------------------------------------ Phụ trợ
        private IQueryable<RetailOrder> Filter(RetailOrderListRequest r, bool withStatus)
        {
            var q = unitOfWork.DbContext.RetailOrder.AsNoTracking().Where(o => !o.IsDeleted);
            string? text = RetailRules.Clean(r.SearchText, 100);
            if (text != null)
            {
                string digits = new(text.Where(char.IsDigit).ToArray());
                q = q.Where(o => o.OrderNumber!.Contains(text) || o.CustomerName!.Contains(text) || (digits.Length >= 3 && o.PhoneNumber!.Contains(digits)));
            }
            if (!string.IsNullOrWhiteSpace(r.Channel))
            {
                string ch = r.Channel.Trim().ToUpperInvariant();
                q = q.Where(o => o.Channel == ch);
            }
            if (RetailRules.ParseDate(r.FromDate) is { } from)
            {
                q = q.Where(o => o.DeliveryDate >= from);
            }
            if (RetailRules.ParseDate(r.ToDate) is { } to)
            {
                q = q.Where(o => o.DeliveryDate <= to);
            }
            if (withStatus && SplitStatuses(r.OrderStatus) is { Count: > 0 } st)
            {
                q = q.Where(o => st.Contains(o.OrderStatus!));
            }
            return q;
        }

        private static List<string> SplitStatuses(string? s)
            => (s ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => RetailOrderStatus.All.FirstOrDefault(a => string.Equals(a, x, StringComparison.OrdinalIgnoreCase)))
                .Where(x => x != null).Select(x => x!).Distinct().ToList();

        private async Task<List<RetailOrderItemModel>> ItemsAsync(List<RetailOrder> orders)
        {
            var ids = orders.Select(o => o.Id).ToList();
            var lines = ids.Count == 0 ? [] : await unitOfWork.DbContext.RetailOrderLine.AsNoTracking()
                .Where(l => ids.Contains(l.RetailOrderId) && !l.IsDeleted).OrderBy(l => l.CreatedDate).ToListAsync();
            var byOrder = lines.ToLookup(l => l.RetailOrderId);
            return orders.Select(o => Item(o, byOrder[o.Id])).ToList();
        }

        private static RetailOrderItemModel Item(RetailOrder o, IEnumerable<RetailOrderLine> lines) => new()
        {
            Id = o.Id, OrderNumber = o.OrderNumber ?? string.Empty, Channel = o.Channel ?? string.Empty, CustomerName = o.CustomerName ?? string.Empty,
            PhoneNumber = o.PhoneNumber ?? string.Empty, Address = o.Address ?? string.Empty, DeliveryDate = o.DeliveryDate, MealType = o.MealType ?? string.Empty,
            OrderStatus = o.OrderStatus ?? string.Empty, SubTotal = o.SubTotal, ShippingFee = o.ShippingFee, TotalAmount = o.TotalAmount,
            PaymentMethod = o.PaymentMethod, PaymentStatus = o.PaymentStatus ?? RetailCodes.Unpaid, Note = o.Note,
            LineSummary = string.Join("; ", lines.Select(l => $"{l.ProductName} × {l.Quantity:0.##}")),
            CreatedDate = o.CreatedDate, ConfirmedAt = o.ConfirmedAt, CancelReason = o.CancelReason,
        };

        private static string ChannelLabel(string? c) => c switch
        {
            RetailCodes.ChannelWeb => "Trang web", RetailCodes.ChannelPhone => "Điện thoại", RetailCodes.ChannelZalo => "Zalo", _ => c ?? string.Empty,
        };

        /// <summary>
        /// Kiểm phần đầu đơn (địa chỉ, ngày, bữa, phí, thanh toán). Sửa đơn: chỉ kiểm giờ chốt khi ĐỔI ngày nhận
        /// (<paramref name="currentDate"/> = ngày đang có) – sửa địa chỉ / phí của đơn ngày mai sau giờ chốt vẫn được.
        /// </summary>
        private ((string Address, DateTime Date, string Meal, decimal Fee, string? Payment)? Head, string? Error) CheckHead(
            string? address, string? date, string? meal, decimal? fee, string? payment, DateTime now, DateTime? currentDate)
        {
            string? addr = RetailRules.Clean(address, 500);
            if (addr == null || addr.Length < 5)
            {
                return (null, RetailMessage.AddressRequired);
            }
            if (RetailRules.ParseDate(date) is not { } d)
            {
                return (null, RetailMessage.DateInvalid);
            }
            if ((currentDate == null || currentDate.Value.Date != d) && RetailRules.CheckDate(d, now, _rules) is { } dateError)
            {
                return (null, dateError);
            }
            string? m = RetailRules.ResolveMeal(meal, _lists);
            if (m == null)
            {
                return (null, string.Format(RetailMessage.MealInvalid, string.Join(", ", RetailRules.Meals(_lists).Select(x => x.Label))));
            }
            decimal f = fee ?? 0;
            if (f < 0)
            {
                return (null, RetailMessage.ShippingFeeInvalid);
            }
            string? pay = null;
            if (!string.IsNullOrWhiteSpace(payment))
            {
                pay = RetailCodes.PaymentMethods.FirstOrDefault(p => string.Equals(p, payment.Trim(), StringComparison.OrdinalIgnoreCase));
                if (pay == null)
                {
                    return (null, RetailMessage.PaymentInvalid);
                }
            }
            return ((addr, d, m, f, pay), null);
        }

        /// <summary>RETAIL-2: gán / bỏ chuyến giao (đơn chưa xong / chưa hủy). Khách thấy xe, tài xế, giờ xuất phát, vị trí gần nhất của chuyến.</summary>
        public async Task<IResult<bool>> AssignRoute(RetailOrderAssignRouteRequest request)
        {
            using var tran = await unitOfWork.OpenTransaction();
            try
            {
                currentActor.RequireActor();
                var db = unitOfWork.DbContext;
                var o = await db.RetailOrder.FirstOrDefaultAsync(x => x.Id == request.Id && !x.IsDeleted);
                if (o == null)
                {
                    return Fail<bool>(RetailMessage.OrderNotFound);
                }
                if (o.OrderStatus is RetailOrderStatus.Completed or RetailOrderStatus.Cancelled)
                {
                    return Fail<bool>(string.Format(RetailMessage.TransitionInvalid, RetailOrderStatus.Label(o.OrderStatus), "Gán chuyến giao"));
                }
                if (request.DeliveryRouteId is { } rid && await RouteErrorAsync(db, o, rid) is { } routeError)
                {
                    return Fail<bool>(routeError);
                }
                o.DeliveryRouteId = request.DeliveryRouteId;
                unitOfWork.RetailOrderRepository.Update(o);
                await unitOfWork.RetailOrderRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                unitOfWork.CommitTransaction();
                return Result<bool>.Success(true, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " AssignRoute: ", ex.ToString()));
                unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>Chuyến phải có, chưa hủy và cùng ngày nhận hàng của đơn.</summary>
        private static async Task<string?> RouteErrorAsync(AppDelicare.Data.Connection.DatabaseContext db, RetailOrder o, Guid routeId)
        {
            var r = await db.DeliveryRoute.AsNoTracking().FirstOrDefaultAsync(x => x.Id == routeId && !x.IsDeleted && x.Status != "Cancelled");
            if (r == null)
            {
                return RetailMessage.RouteNotFound;
            }
            return r.DeliveryDate.Date == o.DeliveryDate.Date ? null
                : string.Format(RetailMessage.RouteDateMismatch, r.RouteNo ?? r.Code, o.DeliveryDate.ToString("dd/MM/yyyy"));
        }

        /// <summary>Lỗi nghiệp vụ TRONG transaction (Create / UpdateLines / Transition): rollback để UnitOfWork không còn giữ transaction mở.</summary>
        private Result<T> Fail<T>(string message)
        {
            unitOfWork.RollbackTransaction();
            return Result<T>.Fail(message);
        }
    }
}
