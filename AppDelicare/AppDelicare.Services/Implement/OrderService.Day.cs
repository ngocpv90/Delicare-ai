using System.Net;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6.15 (D-96): màn "Đơn hàng theo ngày" – theo dõi 1 ngày ăn + xử lý việc phát sinh. Một bảng: khách → điểm giao (cả điểm chưa có đơn, ô dự báo) → đơn
    /// (số theo buổi + lựa chọn, tiến độ, cần chú ý); 4 chỉ số; giờ chốt. Đơn phát sinh nhận biết qua dấu <c>[PHAT_SINH:lý do]</c> trong ghi chú (do
    /// <c>Order/CreateExtra</c> ghi) – đơn cũ không có dấu coi là đơn tuần. Không thêm bảng / cột.
    /// </summary>
    public partial class OrderService
    {
        public const string ExtraMarker = "[PHAT_SINH";
        public static readonly string[] ExtraReasons = ["EVENT", "ADDON", "COMPENSATION", "OTHER"];

        public static string ExtraReasonLabel(string? code) => code switch
        {
            "EVENT" => "Sự kiện / tiệc",
            "ADDON" => "Khách đặt thêm",
            "COMPENSATION" => "Đơn bù do phản ánh",
            _ => "Khác",
        };

        /// <summary>Lý do đơn phát sinh trong ghi chú ("[PHAT_SINH:EVENT] …"); null = đơn tuần.</summary>
        public static string? ExtraReasonOf(string? note)
        {
            if (note == null)
            {
                return null;
            }
            int i = note.IndexOf(ExtraMarker, StringComparison.Ordinal);
            if (i < 0)
            {
                return null;
            }
            int colon = note.IndexOf(':', i), end = note.IndexOf(']', i);
            return colon > i && end > colon ? note[(colon + 1)..end].Trim() : "OTHER";
        }

        private static int ProgressOf(string? status) => status switch
        {
            OrderStatus.Confirmed => 1,
            OrderStatus.Locked => 2,
            OrderStatus.InProduction => 3,
            OrderStatus.Delivered => 4,
            OrderStatus.Reconciled => 5,
            OrderStatus.Cancelled => -1,
            _ => 0,
        };

        public async Task<IResult<OrderDayModel>> GetDay(OrderDayRequest request)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                DateTime day = (request.MealDate ?? _clock.Today.AddDays(1)).Date;
                DateTime now = _clock.Now;
                var model = new OrderDayModel
                {
                    MealDate = day, WeekStart = MondayOf(day), CutoffAt = CutoffOf(day),
                    ReminderSent = now >= day.AddDays(-1).AddHours(18),
                };
                var (points, contracts) = await WeekPointsAsync(day, request.BranchId);
                // WeekPointsAsync lấy hợp đồng giao với [day, day + 7) – chỉ giữ khách có hợp đồng phủ đúng ngày.
                points = points.Where(p => (contracts.GetValueOrDefault(p.CustomerId) ?? []).Any(r => r.From.Date <= day && (r.To == null || r.To.Value.Date >= day))).ToList();
                var customerIds = points.Select(p => p.CustomerId).Distinct().ToList();
                var orders = await db.Order.AsNoTracking()
                    .Where(o => !o.IsDeleted && o.MealDate == day && (request.BranchId == null || db.Customer.Any(c => c.Id == o.CustomerId && c.BranchId == request.BranchId)))
                    .Select(o => new { o.Id, o.CustomerId, o.DeliveryLocationId, o.Status, Qty = o.TotalQuantity ?? 0m, Amount = o.TotalAmount ?? 0m, No = o.OrderNumber ?? o.OrderNo, o.Note, o.CreatedDate })
                    .ToListAsync();
                var orderIds = orders.Select(o => o.Id).ToList();
                var lines = await db.OrderLine.AsNoTracking().Where(l => orderIds.Contains(l.OrderId) && !l.IsDeleted && l.Quantity > 0)
                    .Select(l => new { l.OrderId, l.MealType, l.MenuOptionNo, l.Quantity }).ToListAsync();
                var pending = await db.ApprovalRequest.AsNoTracking()
                    .Where(a => !a.IsDeleted && a.BusinessId != null && orderIds.Contains(a.BusinessId.Value) && (a.RequestStatus == "Pending" || a.RequestStatus == "InProgress"))
                    .Select(a => new { a.Id, a.BusinessId, a.BusinessType }).ToListAsync();
                var forecasts = await db.Forecast.AsNoTracking()
                    .Where(f => !f.IsDeleted && f.Status == ForecastPublished && customerIds.Contains(f.CustomerId) && f.ForecastDate.Date == day)
                    .GroupBy(f => new { f.CustomerId, f.DeliveryLocationId })
                    .Select(g => new { g.Key.CustomerId, g.Key.DeliveryLocationId, Qty = g.Sum(x => x.ForecastQuantity) }).ToListAsync();
                var activeContractIds = await db.Contract.AsNoTracking()
                    .Where(c => !c.IsDeleted && c.Status == ContractStatus.Active && customerIds.Contains(c.CustomerId) && c.EffectiveFrom <= day && (c.EffectiveTo == null || c.EffectiveTo >= day))
                    .Select(c => c.Id).ToListAsync();
                var contractMeals = await db.ContractMeal.AsNoTracking()
                    .Where(m => activeContractIds.Contains(m.ContractId) && !m.IsDeleted && m.MealType != null).Select(m => m.MealType!).Distinct().ToListAsync();
                string[] order = ["SANG", "TRUA", "TOI", "DEM"];
                model.ListMealType = order.Where(m => contractMeals.Contains(m) || lines.Any(l => l.MealType == m)).ToList();
                foreach (var m in model.ListMealType)
                {
                    model.MealTotal[m] = 0m;
                }
                bool locked = now >= model.CutoffAt;
                // Khách có đơn nhưng điểm giao ngoài danh sách (vd. hợp đồng đã hết) – vẫn hiện đơn.
                var extraPoints = orders.Where(o => o.DeliveryLocationId != null && !points.Any(p => p.LocationId == o.DeliveryLocationId)).Select(o => (o.CustomerId, Location: o.DeliveryLocationId!.Value)).Distinct().ToList();
                if (extraPoints.Count > 0)
                {
                    var locIds = extraPoints.Select(x => x.Location).ToList();
                    var cusIds = extraPoints.Select(x => x.CustomerId).Distinct().ToList();
                    var locs = await db.CustomerDeliveryLocation.AsNoTracking().Where(l => locIds.Contains(l.Id)).Select(l => new { l.Id, l.CustomerId, l.Code, l.Name }).ToListAsync();
                    var cus = await db.Customer.AsNoTracking().Where(c => cusIds.Contains(c.Id)).Select(c => new { c.Id, c.Code, Name = c.DisplayName ?? c.Name }).ToDictionaryAsync(c => c.Id);
                    points.AddRange(locs.Where(l => cus.ContainsKey(l.CustomerId)).Select(l => new WeekPoint(l.CustomerId, cus[l.CustomerId].Code, cus[l.CustomerId].Name, l.Id, l.Code, l.Name)));
                }
                foreach (var group in points.GroupBy(p => p.CustomerId))
                {
                    var first = group.First();
                    var customer = new OrderDayCustomerModel { CustomerId = first.CustomerId, CustomerCode = first.CustomerCode, CustomerName = first.CustomerName };
                    foreach (var m in model.ListMealType)
                    {
                        customer.MealTotal[m] = 0m;
                    }
                    foreach (var p in group)
                    {
                        decimal? fc = forecasts.Where(f => f.CustomerId == p.CustomerId && f.DeliveryLocationId == p.LocationId).Select(f => (decimal?)f.Qty).FirstOrDefault();
                        var pointOrders = orders.Where(o => o.DeliveryLocationId == p.LocationId).OrderBy(o => ExtraReasonOf(o.Note) == null ? 0 : 1).ThenBy(o => o.CreatedDate).ToList();
                        customer.PointCount++;
                        model.PointCount++;
                        if (pointOrders.Any(o => o.Status != OrderStatus.Cancelled))
                        {
                            model.PointsWithOrder++;
                        }
                        if (pointOrders.Count == 0)
                        {
                            var row = new OrderDayRowModel { DeliveryLocationId = p.LocationId, LocationCode = p.LocationCode, LocationName = p.LocationName, Forecast = fc, Progress = 0 };
                            if (!locked)
                            {
                                row.ListAttention.Add(new OrderDayAttentionModel { Code = "NO_NUMBER", Label = model.ReminderSent ? "Chưa có số – 18:00 đã nhắc" : "Chưa có số", Action = "INPUT" });
                            }
                            else
                            {
                                row.ListAttention.Add(new OrderDayAttentionModel { Code = "NO_NUMBER", Label = "Không có đơn (đã chốt)", Tone = "info" });
                            }
                            customer.ListRow.Add(row);
                        }
                        bool firstWeekOrder = true;
                        foreach (var o in pointOrders)
                        {
                            string? extra = ExtraReasonOf(o.Note);
                            var row = new OrderDayRowModel
                            {
                                DeliveryLocationId = p.LocationId, LocationCode = p.LocationCode, LocationName = p.LocationName,
                                OrderId = o.Id, OrderNumber = o.No, Source = extra == null ? "WEEK" : "EXTRA", ExtraReason = extra == null ? null : ExtraReasonLabel(extra),
                                Status = o.Status, StatusName = OrderStatus.Label(o.Status), Progress = ProgressOf(o.Status),
                                Total = o.Qty, Amount = o.Amount, Forecast = extra == null && firstWeekOrder ? fc : null,
                            };
                            if (extra == null)
                            {
                                firstWeekOrder = false;
                            }
                            foreach (var mg in lines.Where(l => l.OrderId == o.Id).GroupBy(l => l.MealType ?? string.Empty))
                            {
                                row.ListMeal.Add(new OrderDayMealModel
                                {
                                    MealType = mg.Key, Quantity = mg.Sum(x => x.Quantity),
                                    ListOption = [.. mg.GroupBy(x => x.MenuOptionNo ?? 1).OrderBy(x => x.Key).Select(x => new OrderDayOptionModel { OptionNo = x.Key, Quantity = x.Sum(y => y.Quantity) })],
                                });
                            }
                            if (o.Status == OrderStatus.Cancelled)
                            {
                                model.CancelledCount++;
                                customer.ListRow.Add(row);
                                continue;
                            }
                            foreach (var ml in row.ListMeal)
                            {
                                if (model.MealTotal.ContainsKey(ml.MealType))
                                {
                                    model.MealTotal[ml.MealType] += ml.Quantity;
                                    customer.MealTotal[ml.MealType] += ml.Quantity;
                                }
                            }
                            customer.Total += o.Qty;
                            model.TotalQuantity += o.Qty;
                            model.TotalAmount += o.Amount;
                            if (extra != null)
                            {
                                model.ExtraCount++;
                                row.ListAttention.Add(new OrderDayAttentionModel { Code = "EXTRA", Label = "Đơn phát sinh", Tone = "info" });
                            }
                            var appr = pending.FirstOrDefault(a => a.BusinessId == o.Id);
                            if (appr != null)
                            {
                                row.ListAttention.Add(new OrderDayAttentionModel
                                {
                                    Code = "PENDING_APPROVAL", Action = "APPROVE", ApprovalRequestId = appr.Id,
                                    Label = appr.BusinessType == OrderStatus.DeviationBusinessType ? "Lệch số – chờ duyệt" : appr.BusinessType == OrderStatus.ChangeAfterCutoffBusinessType ? "Sửa sau chốt – chờ duyệt" : "Chờ duyệt",
                                });
                            }
                            else if (o.Status is OrderStatus.Draft or OrderStatus.Submitted)
                            {
                                row.ListAttention.Add(new OrderDayAttentionModel { Code = "UNCONFIRMED", Label = "Chưa xác nhận", Action = locked ? null : "CONFIRM" });
                            }
                            if (row.Forecast is > 0 && extra == null)
                            {
                                decimal pct = Math.Round((o.Qty - row.Forecast.Value) / row.Forecast.Value * 100m, 0);
                                if (Math.Abs(pct) > _rules.ForecastVariancePctWarning && appr == null)
                                {
                                    row.ListAttention.Add(new OrderDayAttentionModel { Code = "DEVIATION", Label = $"Lệch {(pct > 0 ? "+" : "−")}{Math.Abs(pct):0}% dự báo" });
                                }
                            }
                            customer.ListRow.Add(row);
                        }
                    }
                    customer.AttentionCount = customer.ListRow.Count(r => r.ListAttention.Any(a => a.Tone == "warn"));
                    model.AttentionCount += customer.AttentionCount;
                    model.ListCustomer.Add(customer);
                }
                model.CustomerCount = model.ListCustomer.Count(c => c.ListRow.Any(r => r.OrderId != null && r.Status != OrderStatus.Cancelled));
                decimal fcTotal = forecasts.Sum(f => f.Qty);
                model.ForecastTotal = fcTotal > 0 ? fcTotal : null;
                decimal weekQty = model.ListCustomer.SelectMany(c => c.ListRow).Where(r => r.Source == "WEEK" && r.Status != OrderStatus.Cancelled).Sum(r => r.Total);
                model.ForecastDeviationPct = fcTotal > 0 ? Math.Round((weekQty - fcTotal) / fcTotal * 100m, 1) : null;
                var live = orders.Where(o => o.Status != OrderStatus.Cancelled).ToList();
                model.DayState = live.Count > 0 && live.All(o => o.Status is OrderStatus.Delivered or OrderStatus.Reconciled) ? "DELIVERED" : locked ? "LOCKED" : "OPEN";
                return Result<OrderDayModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetDay: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>
        /// "Thêm đơn phát sinh": tạo đơn (cùng luật <c>Order/Create</c>, cho phép điểm đã có đơn tuần – chỉ cảnh báo) + ghi dấu lý do; trước giờ chốt
        /// tự Xác nhận luôn (đơn vào thẳng kế hoạch); sau giờ chốt: lý do làm lý do sửa sau chốt → luồng duyệt hiện có.
        /// </summary>
        public async Task<IResult<OrderDetailModel>> CreateExtra(OrderExtraRequest request)
        {
            try
            {
                string reason = (request.Reason ?? "OTHER").Trim().ToUpperInvariant();
                if (!ExtraReasons.Contains(reason))
                {
                    return Result<OrderDetailModel>.Fail(ConstantHelper.Message.OrderExtraReasonInvalid);
                }
                string text = ExtraReasonLabel(reason) + (string.IsNullOrWhiteSpace(request.Note) ? string.Empty : " – " + request.Note.Trim());
                var created = await CreateCoreAsync(new OrderCreateRequest
                {
                    CustomerId = request.CustomerId, DeliveryLocationId = request.DeliveryLocationId, MealDate = request.MealDate,
                    Note = $"{ExtraMarker}:{reason}] {text}", ListLine = request.ListLine ?? [],
                    AfterCutoffReason = IsAfterCutoff(request.MealDate.Date) ? "Đơn phát sinh: " + text : null,
                }, true);
                if (created.ResponseCode != (int)HttpStatusCode.OK || created.Data?.Order == null || created.Data.ApprovalRequested)
                {
                    return created;
                }
                var warnings = created.Data.ListWarning;
                var confirmed = await Confirm(new OrderActionRequest { Id = created.Data.Order.Id });
                if (confirmed.ResponseCode != (int)HttpStatusCode.OK || confirmed.Data == null)
                {
                    // Không xác nhận được (vd. lệch dự báo cần duyệt) → đơn vẫn tạo, trả về kèm lý do.
                    created.ResponseMessage = confirmed.ResponseMessage;
                    return created;
                }
                confirmed.Data.ListWarning.InsertRange(0, warnings);
                return confirmed;
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " CreateExtra: ", ex.ToString()));
                throw;
            }
        }
    }
}
