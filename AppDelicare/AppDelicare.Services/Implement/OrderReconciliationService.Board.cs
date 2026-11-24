using System.Net;
using System.Text.Json;
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
    /// UI-6.8 (D-86 b, c): màn Đối soát – bảng xác nhận ngay trên trang (thay 2 hộp thoại), xác nhận / chốt theo lô.
    /// Đọc theo lô (đơn, dòng, đơn giao, POD, đối soát) – không gọi từng đơn. Ghi dùng lại <see cref="Reconcile"/> / <see cref="Close"/> (cùng luật, mỗi đơn một transaction).
    /// </summary>
    public partial class OrderReconciliationService
    {
        public const string TabPending = "PENDING", TabConfirmed = "CONFIRMED", TabVariance = "VARIANCE", TabClosed = "CLOSED";
        public const string SourcePod = "POD", SourceBot = "BOT", SourceWeb = "WEB", SourceAuto = "AUTO";
        /// <summary>Ghi chú đối soát do bot điểm phát ("Đã phục vụ đủ" / nhập số).</summary>
        public const string BotRemark = "Telegram";
        private const int ChainComputeLimit = 400;

        public static string SourceOf(string? remark, bool hasReconciliation)
            => !hasReconciliation ? SourcePod
                : IsAutoRemark(remark) ? SourceAuto
                : remark != null && (remark.StartsWith(BotRemark, StringComparison.OrdinalIgnoreCase) || remark.Contains("Bot", StringComparison.OrdinalIgnoreCase)) ? SourceBot
                : SourceWeb;

        public async Task<IResult<ReconBoardModel>> GetBoard(ReconBoardRequest request)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                DateTime to = (request.To ?? _clock.Now).Date, start = (request.From ?? to.AddDays(-6)).Date;
                if (start > to)
                {
                    (start, to) = (to, start);
                }
                DateTime end = to.AddDays(1);
                var orders = await (from o in db.Order.AsNoTracking()
                                    join c in db.Customer.AsNoTracking() on o.CustomerId equals c.Id
                                    where !o.IsDeleted && o.MealDate >= start && o.MealDate < end
                                          && (o.Status == OrderStatus.Delivered || o.Status == OrderStatus.Reconciled)
                                          && (request.CustomerId == null || o.CustomerId == request.CustomerId)
                                          && (request.BranchId == null || c.BranchId == request.BranchId)
                                    select new { o, CustomerName = c.DisplayName ?? c.Name }).ToListAsync();
                var ids = orders.Select(x => x.o.Id).ToList();
                var lines = await db.OrderLine.AsNoTracking().Where(l => ids.Contains(l.OrderId) && !l.IsDeleted).ToListAsync();
                var dos = await db.DeliveryOrder.AsNoTracking().Where(d => ids.Contains(d.OrderId) && !d.IsDeleted).Select(d => new { d.Id, d.OrderId }).ToListAsync();
                var doIds = dos.Select(d => d.Id).ToList();
                var pods = await db.ProofOfDelivery.AsNoTracking().Where(p => doIds.Contains(p.DeliveryOrderId) && !p.IsDeleted).ToListAsync();
                var recs = await db.OrderReconciliation.AsNoTracking().Where(r => ids.Contains(r.OrderId) && !r.IsDeleted).ToListAsync();
                var locIds = orders.Where(x => x.o.DeliveryLocationId != null).Select(x => x.o.DeliveryLocationId!.Value).Distinct().ToList();
                var locs = await db.CustomerDeliveryLocation.AsNoTracking().Where(l => locIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.Name);
                var model = new ReconBoardModel
                {
                    From = start, To = to,
                    ListFaultReason = DeliveryCode.FaultReasons.Select(kv => new ReconFaultReasonModel { Code = kv.Key, Name = kv.Value }).ToList(),
                };
                decimal produced = 0m, packed = 0m;
                int computed = 0;
                foreach (var x in orders.OrderBy(x => x.o.MealDate).ThenBy(x => x.CustomerName).ThenBy(x => x.o.OrderNumber))
                {
                    var o = x.o;
                    var ol = lines.Where(l => l.OrderId == o.Id).ToList();
                    var doSet = dos.Where(d => d.OrderId == o.Id).Select(d => d.Id).ToHashSet();
                    var op = pods.Where(p => doSet.Contains(p.DeliveryOrderId)).ToList();
                    var rec = recs.Where(r => r.OrderId == o.Id).OrderByDescending(r => r.ModifiedDate ?? r.CreatedDate).FirstOrDefault();
                    decimal ordered = DeliveryRules.OrderQuantity(o, ol);
                    decimal delivered = op.Sum(p => p.DeliveredQuantity ?? p.DeliveredQty ?? 0m), accepted = op.Sum(p => p.AcceptedQuantity ?? p.AcceptedQty ?? 0m);
                    decimal amount = o.TotalAmount ?? ol.Sum(l => l.LineAmount);
                    ReconcileNote? note = null;
                    if (rec?.Note is { Length: > 0 } json)
                    {
                        try
                        {
                            note = JsonSerializer.Deserialize<ReconcileNote>(json, NoteJson);
                        }
                        catch (JsonException)
                        {
                            note = null;
                        }
                    }
                    decimal fault = note?.Fault ?? 0m;
                    bool closed = rec?.Status == ReconciliationStatus.Closed;
                    bool confirmed = rec != null && !IsAutoRemark(rec.Remark);
                    var row = new ReconBoardRowModel
                    {
                        OrderId = o.Id, OrderNumber = o.OrderNumber ?? o.OrderNo, OrderStatus = o.Status, MealDate = o.MealDate.Date,
                        CustomerId = o.CustomerId, CustomerName = x.CustomerName,
                        LocationName = o.DeliveryLocationId is { } lid ? locs.GetValueOrDefault(lid) : null,
                        Ordered = ordered, Delivered = delivered, Accepted = accepted,
                        Served = rec?.AcceptedQuantity, Returned = note?.Returned, Wasted = note?.Wasted, Fault = note?.Fault, FaultReason = note?.FaultReason,
                        Billed = note?.BillingQuantity ?? Math.Max(0m, accepted - fault),
                        Amount = rec?.FinalAmount ?? (ordered > 0 ? Math.Round(amount * Math.Max(0m, accepted - fault) / ordered, 0) : 0m),
                        UnitAmount = ordered > 0 ? Math.Round(amount / ordered, 2) : 0m,
                        ReconciliationId = rec?.Id, ReconciliationStatus = rec?.Status,
                        Source = SourceOf(rec?.Remark, rec != null), Confirmed = confirmed, HasPod = op.Count != 0,
                        ListLine = ol.Count > 1 && ol.Any(l => l.MenuOptionNo != null)
                            ? ol.OrderBy(l => MenuService.MealRank(l.MealType)).ThenBy(l => l.MenuOptionNo ?? 0).Select(l => new ReconBoardLineModel
                            {
                                OrderLineId = l.Id, MealType = l.MealType, MenuOptionNo = l.MenuOptionNo, Quantity = l.Quantity, UnitPrice = l.UnitPrice,
                                Served = note?.Lines?.FirstOrDefault(n => n.Id == l.Id)?.Served,
                            }).ToList()
                            : [],
                    };
                    row.Tab = closed ? TabClosed : accepted != ordered || fault > 0 ? TabVariance : confirmed ? TabConfirmed : TabPending;
                    model.ListRow.Add(row);
                    model.Ordered += ordered;
                    model.Delivered += delivered;
                    model.Accepted += accepted;
                    if (closed)
                    {
                        model.ClosedCount++;
                        model.ClosedAmount += rec!.FinalAmount ?? 0m;
                    }
                    else
                    {
                        model.PendingCount++;
                        if (row.Tab == TabVariance)
                        {
                            model.VarianceCount++;
                        }
                        if (row.Tab == TabPending && row.HasPod && o.Status == OrderStatus.Delivered)
                        {
                            model.MatchedPendingCount++;
                        }
                        if (confirmed)
                        {
                            model.ConfirmedOpenCount++;
                        }
                    }
                    decimal? p = rec?.ProducedQty, k = rec?.PackedQty;
                    if (p == null && k == null && computed < ChainComputeLimit)
                    {
                        (p, k) = await ReconciliationChain.ComputeAsync(db, o.Id);
                        computed++;
                    }
                    produced += p ?? 0m;
                    packed += k ?? 0m;
                }
                model.AcceptedPct = model.Ordered > 0 ? Math.Round(model.Accepted / model.Ordered * 100m, 1) : null;
                model.ListChain = [model.Ordered, produced, packed, model.Delivered, model.Accepted];
                for (DateTime d = start; d < end; d = d.AddDays(1))
                {
                    var day = model.ListRow.Where(r => r.MealDate == d).ToList();
                    model.ListDay.Add(new ReconciliationChartPointModel { Date = d, Ordered = day.Sum(r => r.Ordered), Delivered = day.Sum(r => r.Delivered), Accepted = day.Sum(r => r.Accepted) });
                }
                return Result<ReconBoardModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetBoard: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Xác nhận nhiều dòng: mỗi dòng = <see cref="Reconcile"/> (phục vụ trống = khách nhận). Dòng lỗi báo lại, không chặn dòng khác.</summary>
        public async Task<IResult<ReconBatchResultModel>> ReconcileMany(ReconcileManyRequest request)
        {
            try
            {
                var items = (request.ListItem ?? []).GroupBy(i => i.OrderId).Select(g => g.Last()).ToList();
                if (items.Count == 0)
                {
                    return Result<ReconBatchResultModel>.Fail(ConstantHelper.Message.ReconNothingSelected);
                }
                var result = new ReconBatchResultModel();
                foreach (var item in items)
                {
                    decimal served = item.ServedQuantity ?? await AcceptedOfAsync(item.OrderId);
                    var r = await Reconcile(new OrderReconcileRequest
                    {
                        OrderId = item.OrderId, ServedQuantity = served, ReturnedQuantity = item.ReturnedQuantity, WastedQuantity = item.WastedQuantity,
                        DelicareFaultQuantity = item.DelicareFaultQuantity, FaultReason = item.FaultReason,
                        Remark = string.IsNullOrWhiteSpace(item.Remark) ? ConstantHelper.Message.ReconWebRemark : item.Remark,
                        ListLine = (item.ListLine ?? []).Select(l => new ReconcileLineRequest { OrderLineId = l.OrderLineId, ServedQuantity = l.ServedQuantity }).ToList(),
                    });
                    if (r.ResponseCode == (int)HttpStatusCode.OK)
                    {
                        result.Ok++;
                    }
                    else
                    {
                        result.Failed++;
                        result.ListError.Add(new ReconBatchErrorModel { Id = item.OrderId, Message = r.ResponseMessage });
                    }
                }
                return Result<ReconBatchResultModel>.Success(result, string.Format(ConstantHelper.Message.ReconManyDone, result.Ok, result.Failed));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ReconcileMany: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Chốt nhiều đối soát: mỗi bản = <see cref="Close"/> (chênh lệch vượt ngưỡng cần ghi chú – dùng ghi chú chung nếu có).</summary>
        public async Task<IResult<ReconBatchResultModel>> CloseMany(ReconCloseManyRequest request)
        {
            try
            {
                var ids = (request.ListReconciliationId ?? []).Distinct().ToList();
                if (ids.Count == 0)
                {
                    return Result<ReconBatchResultModel>.Fail(ConstantHelper.Message.ReconNothingSelected);
                }
                var result = new ReconBatchResultModel();
                foreach (var id in ids)
                {
                    var r = await Close(new ReconciliationCloseRequest { Id = id, Remark = request.Remark });
                    if (r.ResponseCode == (int)HttpStatusCode.OK)
                    {
                        result.Ok++;
                    }
                    else
                    {
                        result.Failed++;
                        result.ListError.Add(new ReconBatchErrorModel { Id = id, Message = r.ResponseMessage });
                    }
                }
                return Result<ReconBatchResultModel>.Success(result, string.Format(ConstantHelper.Message.ReconCloseManyDone, result.Ok, result.Failed));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " CloseMany: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Bot "Đã phục vụ đủ": phục vụ = số khách nhận khi bàn giao, ghi nguồn bot.</summary>
        public async Task<IResult<OrderReconciliationDetailModel>> ConfirmServedFull(Guid orderId)
            => await Reconcile(new OrderReconcileRequest { OrderId = orderId, ServedQuantity = await AcceptedOfAsync(orderId), Remark = BotRemark + " – Đã phục vụ đủ" });

        private async Task<decimal> AcceptedOfAsync(Guid orderId)
        {
            var db = _unitOfWork.DbContext;
            var doIds = await db.DeliveryOrder.AsNoTracking().Where(d => d.OrderId == orderId && !d.IsDeleted).Select(d => d.Id).ToListAsync();
            return await db.ProofOfDelivery.AsNoTracking().Where(p => doIds.Contains(p.DeliveryOrderId) && !p.IsDeleted).SumAsync(p => p.AcceptedQuantity ?? p.AcceptedQty ?? 0m);
        }
    }
}
