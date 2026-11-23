using AutoMapper;
using Dapper;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Model.Request;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using System.Net;
using System.Text.Json;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// T19 (spec 6.7, BUSINESS_RULES §8b, Pilot P8) – đối soát suất: đơn Đã giao → Reconcile (suất thực tế phục vụ do khách xác nhận; Open → Khớp / Chênh lệch, ghi lại được tới khi đóng)
    /// → Close (→ Đóng, đơn hàng → Đã đối soát). CRUD cũ không ghi (số liệu chỉ tính qua Reconcile).
    /// </summary>
    public class OrderReconciliationService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService, IClock clock, ICurrentActor currentActor, IOptions<BusinessRulesOptions> rules) : IOrderReconciliationService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;
        private readonly IClock _clock = clock;
        private readonly ICurrentActor _currentActor = currentActor;
        private readonly BusinessRulesOptions _rules = rules.Value;

        /// <summary>Phần không có cột riêng (GAP-DLV-05) lưu dạng JSON trong OrderReconciliation.Note.</summary>
        private sealed class ReconcileNote
        {
            public decimal PodAccepted { get; set; }
            public decimal? Returned { get; set; }
            public decimal? Wasted { get; set; }
            /// <summary>BIZ-1.4: suất trả lại / hủy do lỗi Delicare + mã lý do.</summary>
            public decimal? Fault { get; set; }
            public string? FaultReason { get; set; }
            public string? BillingBasis { get; set; }
            public decimal BillingQuantity { get; set; }
            public decimal OrderAmount { get; set; }
            /// <summary>ORD-OPT-1.7: suất phục vụ theo dòng đơn (lựa chọn) – null = ghi theo tổng.</summary>
            public List<ReconcileNoteLine>? Lines { get; set; }
        }

        private sealed class ReconcileNoteLine
        {
            public Guid Id { get; set; }
            public decimal Served { get; set; }
        }

        private static readonly JsonSerializerOptions NoteJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public async Task<IResults<OrderReconciliationViewModel>> GetAll(OrderReconciliationSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@OrderId", request.OrderId == Guid.Empty ? null : request.OrderId);
                dynamicParameters.Add("@ProofOfDeliveryId", request.ProofOfDeliveryId);
                dynamicParameters.Add("@ReconciliationDate", request.ReconciliationDate);
                //dynamicParameters.Add("@OrderedQuantity", request.OrderedQuantity);
                //dynamicParameters.Add("@DeliveredQuantity", request.DeliveredQuantity);
                //dynamicParameters.Add("@AcceptedQuantity", request.AcceptedQuantity);
                //dynamicParameters.Add("@FinalAmount", request.FinalAmount);
                dynamicParameters.Add("@Status", request.Status);
                //dynamicParameters.Add("@Note", request.Note);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<OrderReconciliationViewModel>("uspOrderReconciliation_GetAll", dynamicParameters)).ToList();
                // Bản đối soát lập trước 2026-11-22 chưa lưu suất SX / đóng gói → tính khi xem (không ghi lại).
                foreach (var x in list.Where(x => x.ProducedQty == null && x.PackedQty == null))
                {
                    (x.ProducedQty, x.PackedQty) = await ReconciliationChain.ComputeAsync(_unitOfWork.DbContext, x.OrderId);
                }
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<OrderReconciliationViewModel>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    ListData = list,
                    TotalRecords = list.FirstOrDefault()?.TotalRow ?? 0
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetAll: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResults<OrderReconciliation>> GetAllData()
        {
            try
            {
                var list = await _unitOfWork.DbContext.OrderReconciliation.AsNoTracking().Where(x => !x.IsDeleted).ToListAsync();
                // Bản đối soát lập trước 2026-11-22 chưa lưu suất SX / đóng gói → tính khi xem (AsNoTracking – không ghi lại).
                foreach (var x in list.Where(x => x.ProducedQty == null && x.PackedQty == null))
                {
                    (x.ProducedQty, x.PackedQty) = await ReconciliationChain.ComputeAsync(_unitOfWork.DbContext, x.OrderId);
                }
                return new Results<OrderReconciliation>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    ListData = list,
                    TotalRecords = list.Count
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetAllData: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<bool>> Save(OrderReconciliationRequest request, string typeSave)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var result = string.Empty;
                switch (typeSave)
                {
                    case ConstantHelper.TypeSave.Insert:
                        result = await Insert(request, typeSave);
                        break;
                    case ConstantHelper.TypeSave.Update:
                        result = await Update(request, typeSave);
                        break;
                    case ConstantHelper.TypeSave.Delete:
                        result = await Delete(request.Id, typeSave);
                        break;
                    default:
                        result = "Loại thao tác lưu dữ liệu không hợp lệ.";
                        break;
                }
                _unitOfWork.CommitTransaction();
                if (!string.IsNullOrEmpty(result))
                {
                    return new Result<bool>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = result,
                        Data = false
                    };
                }
                return new Result<bool>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = typeSave == ConstantHelper.TypeSave.Delete ? ConstantHelper.Message.DeleteSuccess : ConstantHelper.Message.SaveSuccess,
                    Data = true
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Save: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>T19: đối soát chỉ tạo / tính qua OrderReconciliation/Reconcile – CRUD cũ không ghi.</summary>
        private static Task<string> Blocked()
            => Task.FromResult(string.Format(ConstantHelper.Message.DeliveryCrudBlocked, "Đối soát", "OrderReconciliation/Reconcile / Close"));

        private Task<string> Insert(OrderReconciliationRequest request, string typeSave) => Blocked();

        private Task<string> Update(OrderReconciliationRequest request, string typeSave) => Blocked();

        private Task<string> Delete(Guid? id, string typeSave) => Blocked();

        /// <summary>Số liệu giao nhận của đơn: suất đặt, Σ giao / nhận theo POD, POD đầu tiên, giá trị đơn.</summary>
        private async Task<(decimal Ordered, decimal Delivered, decimal Accepted, List<ProofOfDelivery> Pods, decimal Amount)> FiguresAsync(Order order)
        {
            var db = _unitOfWork.DbContext;
            var lines = await db.OrderLine.AsNoTracking().Where(l => !l.IsDeleted && l.OrderId == order.Id).ToListAsync();
            var deliveryOrderIds = await db.DeliveryOrder.AsNoTracking().Where(d => !d.IsDeleted && d.OrderId == order.Id).Select(d => d.Id).ToListAsync();
            var pods = await db.ProofOfDelivery.AsNoTracking().Where(p => !p.IsDeleted && deliveryOrderIds.Contains(p.DeliveryOrderId)).OrderBy(p => p.ConfirmedAt).ToListAsync();
            decimal amount = order.TotalAmount ?? lines.Sum(l => l.LineAmount);
            return (DeliveryRules.OrderQuantity(order, lines), pods.Sum(p => p.DeliveredQuantity ?? p.DeliveredQty ?? 0m),
                pods.Sum(p => p.AcceptedQuantity ?? p.AcceptedQty ?? 0m), pods, amount);
        }

        /// <summary>
        /// T19 (spec 6.7 reconcile, BUSINESS_RULES §8b): đối soát 1 đơn Đã giao – nhân viên điểm ghi <b>suất thực tế phục vụ</b> (khách xác nhận).
        /// OrderedQuantity = tổng suất đơn; DeliveredQuantity / suất nhận = Σ POD; AcceptedQuantity = suất phục vụ (0 ≤ phục vụ ≤ suất nhận; phục vụ + trả về + hủy ≤ suất nhận);
        /// VarianceQty = phục vụ − đặt; VariancePercent = VarianceQty ÷ đặt × 100 (2 số lẻ); Status = Khớp nếu chênh 0, ngược lại Chênh lệch;
        /// FinalAmount = giá trị đơn × số suất tính tiền ÷ suất đặt (làm tròn đồng) – số suất tính tiền theo <c>BusinessRules:ReconciliationBillingBasis</c>
        /// (mặc định SERVED, PROPOSED – GAP-DLV-05). ProducedQty / PackedQty tính theo <see cref="ReconciliationChain"/> (UAT 2026-11-22). Ghi lại được khi chưa đóng.
        /// </summary>
        public async Task<IResult<OrderReconciliationDetailModel>> Reconcile(OrderReconcileRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                _currentActor.RequireActor();
                var order = await _unitOfWork.DbContext.Order.AsNoTracking().FirstOrDefaultAsync(o => o.Id == request.OrderId && !o.IsDeleted);
                if (order == null)
                {
                    return Fail(ConstantHelper.Message.ReconcileOrderNotFound);
                }
                string orderNo = order.OrderNumber ?? order.OrderNo ?? order.Id.ToString("N")[..8];
                if (order.Status != OrderStatus.Delivered)
                {
                    return Fail(string.Format(ConstantHelper.Message.StatusActionInvalid, "đối soát", "đơn " + orderNo,
                        OrderStatus.Label(OrderStatus.Delivered), OrderStatus.Label(order.Status)));
                }
                var existing = _unitOfWork.OrderReconciliationRepository.GetSingle(r => r.OrderId == order.Id && !r.IsDeleted);
                if (existing?.Status == ReconciliationStatus.Closed)
                {
                    return Fail(string.Format(ConstantHelper.Message.ReconcileClosed, orderNo));
                }
                var (ordered, delivered, podAccepted, pods, amount) = await FiguresAsync(order);
                if (pods.Count == 0)
                {
                    return Fail(string.Format(ConstantHelper.Message.ReconcileNoPod, orderNo));
                }
                // ORD-OPT-1.7 (D-74 i): ghi theo dòng đơn (lựa chọn) → suất phục vụ = Σ dòng.
                var orderLines = await _unitOfWork.DbContext.OrderLine.AsNoTracking().Where(l => l.OrderId == order.Id && !l.IsDeleted).ToListAsync();
                var perLine = (request.ListLine ?? []).GroupBy(l => l.OrderLineId).Select(g => g.Last()).ToList();
                if (perLine.Any(l => orderLines.All(o => o.Id != l.OrderLineId)))
                {
                    return Fail(string.Format(ConstantHelper.Message.OrderLineNotInOrder, perLine.First(l => orderLines.All(o => o.Id != l.OrderLineId)).OrderLineId));
                }
                if (perLine.Any(l => l.ServedQuantity < 0))
                {
                    return Fail(string.Format(ConstantHelper.Message.ReconcileQuantityInvalid, DeliveryRules.Num(podAccepted)));
                }
                decimal served = perLine.Count != 0 ? perLine.Sum(l => l.ServedQuantity) : request.ServedQuantity;
                decimal returned = request.ReturnedQuantity ?? 0m, wasted = request.WastedQuantity ?? 0m;
                if (served < 0 || served > podAccepted || returned < 0 || wasted < 0 || served + returned + wasted > podAccepted)
                {
                    return Fail(string.Format(ConstantHelper.Message.ReconcileQuantityInvalid, DeliveryRules.Num(podAccepted)));
                }
                // BIZ-1.4 (D-77 d): suất trả lại / hủy do lỗi Delicare – trừ khỏi số tính tiền; phải có lý do thuộc nhóm lỗi Delicare.
                decimal fault = request.DelicareFaultQuantity ?? 0m;
                string? faultReason = DeliveryRules.Clean(request.FaultReason)?.ToUpperInvariant();
                if (fault < 0 || fault > podAccepted)
                {
                    return Fail(string.Format(ConstantHelper.Message.ReconcileFaultInvalid, DeliveryRules.Num(podAccepted)));
                }
                if (fault > 0 && (faultReason == null || !DeliveryCode.FaultReasons.ContainsKey(faultReason)))
                {
                    return Fail(string.Format(ConstantHelper.Message.ReconcileFaultReasonRequired, string.Join(", ", DeliveryCode.FaultReasons.Values)));
                }
                if (fault == 0)
                {
                    faultReason = null;
                }
                decimal variance = served - ordered;
                decimal percent = ordered > 0 ? Math.Round(variance / ordered * 100m, 2, MidpointRounding.AwayFromZero) : 0m;
                string basis = (_rules.ReconciliationBillingBasis ?? DeliveryCode.BillingReceived).Trim().ToUpperInvariant();
                if (basis is not (DeliveryCode.BillingOrdered or DeliveryCode.BillingDelivered or DeliveryCode.BillingAccepted or DeliveryCode.BillingServed))
                {
                    basis = DeliveryCode.BillingReceived;
                }
                decimal billingQuantity = basis switch
                {
                    DeliveryCode.BillingOrdered => ordered,
                    DeliveryCode.BillingDelivered => delivered,
                    DeliveryCode.BillingAccepted => podAccepted,
                    DeliveryCode.BillingServed => served,
                    _ => Math.Max(0m, podAccepted - fault),
                };
                decimal finalAmount = ordered > 0 ? Math.Round(amount * billingQuantity / ordered, 0, MidpointRounding.AwayFromZero) : 0m;
                if (perLine.Count != 0 && basis == DeliveryCode.BillingServed)
                {
                    // D-74 i: tiền = Σ suất tính tiền từng dòng × giá buổi của dòng.
                    finalAmount = Math.Round(perLine.Sum(l => l.ServedQuantity * (orderLines.First(o => o.Id == l.OrderLineId).UnitPrice ?? 0m)), 0, MidpointRounding.AwayFromZero);
                }

                var reconciliation = existing ?? new OrderReconciliation { Id = Guid.NewGuid(), OrderId = order.Id };
                reconciliation.OrderedQuantity = ordered;
                reconciliation.OrderedQty = ordered;
                reconciliation.DeliveredQuantity = delivered;
                reconciliation.DeliveredQty = delivered;
                (reconciliation.ProducedQty, reconciliation.PackedQty) = await ReconciliationChain.ComputeAsync(_unitOfWork.DbContext, order.Id);
                reconciliation.AcceptedQuantity = served;
                reconciliation.AcceptedQty = served;
                reconciliation.VarianceQty = variance;
                reconciliation.VariancePercent = percent;
                reconciliation.Status = variance == 0 ? ReconciliationStatus.Matched : ReconciliationStatus.Variance;
                reconciliation.ProofOfDeliveryId = pods[0].Id;
                reconciliation.ReconciliationDate = _clock.Now;
                reconciliation.FinalAmount = finalAmount;
                reconciliation.Remark = DeliveryRules.Clean(request.Remark) ?? reconciliation.Remark;
                reconciliation.Note = JsonSerializer.Serialize(new ReconcileNote
                {
                    PodAccepted = podAccepted,
                    Returned = request.ReturnedQuantity,
                    Wasted = request.WastedQuantity,
                    Fault = fault > 0 ? fault : null,
                    FaultReason = faultReason,
                    BillingBasis = basis,
                    BillingQuantity = billingQuantity,
                    OrderAmount = amount,
                    Lines = perLine.Count == 0 ? null : perLine.Select(l => new ReconcileNoteLine { Id = l.OrderLineId, Served = l.ServedQuantity }).ToList(),
                }, NoteJson);
                if (existing == null)
                {
                    _unitOfWork.OrderReconciliationRepository.Add(reconciliation);
                }
                else
                {
                    _unitOfWork.OrderReconciliationRepository.Update(reconciliation);
                }
                await _unitOfWork.OrderReconciliationRepository.CommitAsync(existing == null ? ConstantHelper.TypeSave.Insert : ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                return Result<OrderReconciliationDetailModel>.Success((await DetailAsync(reconciliation.Id))!, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Reconcile: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                throw;
            }

            IResult<OrderReconciliationDetailModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                return Result<OrderReconciliationDetailModel>.Fail(message);
            }
        }

        /// <summary>
        /// T19 (spec 6.7 close): Khớp / Chênh lệch → Đóng; đơn hàng Đã giao → Đã đối soát. |Chênh lệch %| &gt; <c>ReconciliationVarianceRemarkPct</c> (1%) → bắt buộc ghi chú (PROPOSED, GAP-F-11).
        /// </summary>
        public async Task<IResult<OrderReconciliationDetailModel>> Close(ReconciliationCloseRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                _currentActor.RequireActor();
                var reconciliation = _unitOfWork.OrderReconciliationRepository.GetSingle(r => r.Id == request.Id && !r.IsDeleted);
                if (reconciliation == null)
                {
                    return Fail(ConstantHelper.Message.ReconciliationNotFound);
                }
                if (reconciliation.Status is not (ReconciliationStatus.Matched or ReconciliationStatus.Variance))
                {
                    return Fail(string.Format(ConstantHelper.Message.StatusActionInvalid, "đóng đối soát", "bản đối soát",
                        ReconciliationStatus.Label(ReconciliationStatus.Matched) + " / " + ReconciliationStatus.Label(ReconciliationStatus.Variance), ReconciliationStatus.Label(reconciliation.Status)));
                }
                string? remark = DeliveryRules.Clean(request.Remark) ?? DeliveryRules.Clean(reconciliation.Remark);
                decimal percent = Math.Abs(reconciliation.VariancePercent ?? 0m);
                if (percent > _rules.ReconciliationVarianceRemarkPct && remark == null)
                {
                    return Fail(string.Format(ConstantHelper.Message.ReconcileRemarkRequired, DeliveryRules.Num(percent), DeliveryRules.Num(_rules.ReconciliationVarianceRemarkPct)));
                }
                reconciliation.Status = ReconciliationStatus.Closed;
                reconciliation.Remark = remark;
                // Chốt: lấy số sản xuất / đóng gói cuối cùng của đơn.
                (reconciliation.ProducedQty, reconciliation.PackedQty) = await ReconciliationChain.ComputeAsync(_unitOfWork.DbContext, reconciliation.OrderId);
                _unitOfWork.OrderReconciliationRepository.Update(reconciliation);
                var order = _unitOfWork.OrderRepository.GetSingle(o => o.Id == reconciliation.OrderId && !o.IsDeleted);
                if (order != null && order.Status == OrderStatus.Delivered)
                {
                    order.Status = OrderStatus.Reconciled;
                    _unitOfWork.OrderRepository.Update(order);
                }
                await _unitOfWork.OrderReconciliationRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                return Result<OrderReconciliationDetailModel>.Success((await DetailAsync(reconciliation.Id))!, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Close: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                throw;
            }

            IResult<OrderReconciliationDetailModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                return Result<OrderReconciliationDetailModel>.Fail(message);
            }
        }

        /// <summary>T19: chi tiết đối soát (theo Id).</summary>
        public async Task<IResult<OrderReconciliationDetailModel>> GetDetail(Guid id)
        {
            try
            {
                var detail = await DetailAsync(id);
                return detail == null ? Result<OrderReconciliationDetailModel>.Fail(ConstantHelper.Message.ReconciliationNotFound)
                    : Result<OrderReconciliationDetailModel>.Success(detail, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetDetail: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>T19: đối soát của 1 đơn hàng (chưa có → lỗi nghiệp vụ "không tìm thấy").</summary>
        public async Task<IResult<OrderReconciliationDetailModel>> GetByOrder(Guid orderId)
        {
            try
            {
                var id = await _unitOfWork.DbContext.OrderReconciliation.AsNoTracking().Where(r => r.OrderId == orderId && !r.IsDeleted).Select(r => (Guid?)r.Id).FirstOrDefaultAsync();
                return id == null ? Result<OrderReconciliationDetailModel>.Fail(ConstantHelper.Message.ReconciliationNotFound) : await GetDetail(id.Value);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetByOrder: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>
        /// BE3.1 (Catalog 100, spec 6.7 GET reconciliations): đối soát theo <b>ngày ăn</b> của đơn (from / to, mặc định 30 ngày – BusinessRules:Charts),
        /// trạng thái, khách, tìm số đơn / tên khách. <c>chart</c> = Σ suất đặt / giao / nhận theo ngày ăn trên toàn bộ kết quả lọc (không phân trang),
        /// đủ mọi ngày trong khoảng (ngày không có đối soát = 0) để vẽ liền trục. <c>statusCounts</c> tính trước khi lọc trạng thái (chip lọc nhanh).
        /// </summary>
        public async Task<IResult<ReconciliationListModel>> GetList(ReconciliationListRequest request)
        {
            try
            {
                var charts = _rules.Charts;
                string? error = ChartRules.DayRange(request.From, request.To, _clock.Today, charts.DefaultRangeDays, charts.MaxRangeDays, out DateTime from, out DateTime to);
                if (error != null)
                {
                    return Result<ReconciliationListModel>.Fail(error);
                }
                var db = _unitOfWork.DbContext;
                DateTime end = to.AddDays(1);
                var query = from r in db.OrderReconciliation.AsNoTracking()
                            join o in db.Order.AsNoTracking() on r.OrderId equals o.Id
                            join c in db.Customer.AsNoTracking() on o.CustomerId equals c.Id into cj
                            from c in cj.DefaultIfEmpty()
                            where !r.IsDeleted && !o.IsDeleted && o.MealDate >= @from && o.MealDate < end
                            select new { r, o.MealDate, o.CustomerId, OrderNumber = o.OrderNumber ?? o.OrderNo, CustomerName = c != null ? (c.DisplayName ?? c.Name) : null };
                if (request.CustomerId is { } customer && customer != Guid.Empty)
                {
                    query = query.Where(x => x.CustomerId == customer);
                }
                if (!string.IsNullOrWhiteSpace(request.SearchText))
                {
                    string text = request.SearchText.Trim();
                    query = query.Where(x => (x.OrderNumber != null && x.OrderNumber.Contains(text)) || (x.CustomerName != null && x.CustomerName.Contains(text)));
                }
                var counts = await query.GroupBy(x => x.r.Status ?? string.Empty).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
                if (!string.IsNullOrWhiteSpace(request.Status))
                {
                    string status = request.Status.Trim();
                    query = query.Where(x => x.r.Status == status);
                }
                var byDate = await query.GroupBy(x => x.MealDate)
                    .Select(g => new { Date = g.Key, Ordered = g.Sum(x => x.r.OrderedQuantity), Delivered = g.Sum(x => x.r.DeliveredQuantity), Accepted = g.Sum(x => x.r.AcceptedQuantity) })
                    .ToListAsync();
                int total = await query.CountAsync();
                int page = Math.Max(1, request.Page), size = Math.Clamp(request.PageSize, 1, 500);
                var rows = await query.OrderByDescending(x => x.MealDate).ThenBy(x => x.OrderNumber)
                    .Skip((page - 1) * size).Take(size).ToListAsync();
                var model = new ReconciliationListModel
                {
                    From = from,
                    To = to,
                    TotalRecords = total,
                    StatusCounts = counts.ToDictionary(x => x.Key, x => x.Count),
                    ListItem = rows.Select(x => new ReconciliationListItemModel
                    {
                        Id = x.r.Id,
                        OrderId = x.r.OrderId,
                        OrderNumber = x.OrderNumber,
                        MealDate = x.MealDate,
                        CustomerId = x.CustomerId,
                        CustomerName = x.CustomerName,
                        ReconciliationDate = x.r.ReconciliationDate,
                        OrderedQuantity = x.r.OrderedQuantity,
                        DeliveredQuantity = x.r.DeliveredQuantity,
                        AcceptedQuantity = x.r.AcceptedQuantity,
                        VarianceQty = x.r.VarianceQty,
                        VariancePercent = x.r.VariancePercent,
                        FinalAmount = x.r.FinalAmount,
                        Status = x.r.Status,
                        StatusName = ReconciliationStatus.Label(x.r.Status),
                    }).ToList(),
                };
                var points = byDate.GroupBy(x => x.Date.Date).ToDictionary(g => g.Key, g => (Ordered: g.Sum(x => x.Ordered), Delivered: g.Sum(x => x.Delivered), Accepted: g.Sum(x => x.Accepted)));
                for (DateTime d = from; d <= to; d = d.AddDays(1))
                {
                    var p = points.TryGetValue(d, out var v) ? v : (Ordered: 0m, Delivered: 0m, Accepted: 0m);
                    model.Chart.Add(new ReconciliationChartPointModel { Date = d, Ordered = p.Ordered, Delivered = p.Delivered, Accepted = p.Accepted });
                }
                return Result<ReconciliationListModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetList: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>T19: đơn Đã giao chờ đối soát (kể cả đã có bản đối soát chưa đóng) – lọc ngày ăn / khách.</summary>
        public async Task<IResults<ReconciliationPendingModel>> GetPendingOrders(ReconciliationPendingRequest request)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var query = db.Order.AsNoTracking().Where(o => !o.IsDeleted && o.Status == OrderStatus.Delivered);
                if (request.MealDate is { } day)
                {
                    DateTime date = day.Date, next = date.AddDays(1);
                    query = query.Where(o => o.MealDate >= date && o.MealDate < next);
                }
                if (request.CustomerId is { } customerId && customerId != Guid.Empty)
                {
                    query = query.Where(o => o.CustomerId == customerId);
                }
                var orders = await query.OrderBy(o => o.MealDate).ThenBy(o => o.OrderNumber).ToListAsync();
                var orderIds = orders.Select(o => o.Id).ToList();
                var customerIds = orders.Select(o => o.CustomerId).Distinct().ToList();
                var customers = await db.Customer.AsNoTracking().Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.DisplayName ?? c.Name);
                var reconciliations = await db.OrderReconciliation.AsNoTracking().Where(r => !r.IsDeleted && orderIds.Contains(r.OrderId)).ToListAsync();
                var list = new List<ReconciliationPendingModel>();
                foreach (var order in orders)
                {
                    var (ordered, delivered, accepted, _, _) = await FiguresAsync(order);
                    var reconciliation = reconciliations.FirstOrDefault(r => r.OrderId == order.Id);
                    list.Add(new ReconciliationPendingModel
                    {
                        OrderId = order.Id,
                        OrderNumber = order.OrderNumber ?? order.OrderNo,
                        MealDate = order.MealDate,
                        CustomerId = order.CustomerId,
                        CustomerName = customers.GetValueOrDefault(order.CustomerId),
                        OrderStatus = order.Status,
                        OrderedQuantity = ordered,
                        DeliveredQuantity = delivered,
                        PodAcceptedQuantity = accepted,
                        ReconciliationId = reconciliation?.Id,
                        ReconciliationStatus = reconciliation?.Status,
                    });
                }
                return Results<ReconciliationPendingModel>.Success(list, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetPendingOrders: ", ex.ToString()));
                throw;
            }
        }

        private async Task<OrderReconciliationDetailModel?> DetailAsync(Guid id)
        {
            var db = _unitOfWork.DbContext;
            var reconciliation = await db.OrderReconciliation.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
            var order = reconciliation == null ? null : await db.Order.AsNoTracking().FirstOrDefaultAsync(o => o.Id == reconciliation.OrderId);
            if (reconciliation == null || order == null)
            {
                return null;
            }
            var (ordered, delivered, accepted, pods, amount) = await FiguresAsync(order);
            if (reconciliation.ProducedQty == null && reconciliation.PackedQty == null)
            {
                // Bản lập trước 2026-11-22: tính khi xem / in (bản ghi AsNoTracking – không lưu).
                (reconciliation.ProducedQty, reconciliation.PackedQty) = await ReconciliationChain.ComputeAsync(db, order.Id);
            }
            ReconcileNote? note = null;
            try
            {
                note = string.IsNullOrWhiteSpace(reconciliation.Note) ? null : JsonSerializer.Deserialize<ReconcileNote>(reconciliation.Note, NoteJson);
            }
            catch (JsonException)
            {
                // Note không phải JSON của Reconcile (ghi tay cũ) – bỏ qua phần chi tiết.
            }
            var customer = await db.Customer.AsNoTracking().FirstOrDefaultAsync(c => c.Id == order.CustomerId);
            decimal served = reconciliation.AcceptedQuantity;
            // ORD-OPT-1.7: suất theo dòng đơn (loại suất × lựa chọn).
            var orderLines = await db.OrderLine.AsNoTracking().Where(l => l.OrderId == order.Id && !l.IsDeleted).ToListAsync();
            var shiftIds = orderLines.Where(l => l.ShiftId != null).Select(l => l.ShiftId!.Value).Distinct().ToList();
            var shiftNames = await db.Shift.AsNoTracking().Where(s => shiftIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name);
            var optionMap = await MenuOptionRules.ForMenusAsync(db, orderLines.Where(l => l.MenuId != null).Select(l => l.MenuId!.Value), order.MealDate);
            var listLine = orderLines.OrderBy(l => MenuService.MealRank(l.MealType)).ThenBy(l => l.MenuOptionNo ?? 0).Select(l =>
            {
                decimal? lineServed = note?.Lines?.FirstOrDefault(x => x.Id == l.Id)?.Served;
                return new ReconciliationLineModel
                {
                    OrderLineId = l.Id, MealType = l.MealType, ShiftName = l.ShiftId is { } s ? shiftNames.GetValueOrDefault(s) : null, MenuOptionNo = l.MenuOptionNo,
                    OptionDishText = l.MenuOptionNo is { } no ? MenuOptionRules.Of(optionMap, l.MenuId, l.MealType).FirstOrDefault(o => o.OptionNo == no)?.DishText : null,
                    OrderedQuantity = l.Quantity, UnitPrice = l.UnitPrice, ServedQuantity = lineServed,
                    Amount = lineServed is { } q ? Math.Round(q * (l.UnitPrice ?? 0m), 0, MidpointRounding.AwayFromZero) : null,
                };
            }).ToList();
            return new OrderReconciliationDetailModel
            {
                Reconciliation = reconciliation,
                StatusName = ReconciliationStatus.Label(reconciliation.Status),
                OrderNumber = order.OrderNumber ?? order.OrderNo,
                OrderStatus = order.Status,
                MealDate = order.MealDate,
                CustomerId = order.CustomerId,
                CustomerName = customer?.DisplayName ?? customer?.Name,
                OrderTotalAmount = note?.OrderAmount ?? amount,
                OrderedQuantity = reconciliation.OrderedQuantity,
                DeliveredQuantity = reconciliation.DeliveredQuantity,
                PodAcceptedQuantity = note?.PodAccepted ?? accepted,
                ServedQuantity = served,
                ReturnedQuantity = note?.Returned,
                WastedQuantity = note?.Wasted,
                LeftoverQuantity = (note?.PodAccepted ?? accepted) - served,
                BillingBasis = note?.BillingBasis,
                BillingBasisName = note?.BillingBasis == null ? null : DeliveryCode.BillingLabel(note.BillingBasis),
                DelicareFaultQuantity = note?.Fault,
                FaultReason = note?.FaultReason,
                FaultReasonName = note?.FaultReason is { } fr ? DeliveryCode.FaultReasons.GetValueOrDefault(fr, fr) : null,
                BillingQuantity = note?.BillingQuantity ?? served,
                ListPod = pods,
                ListLine = listLine,
                AllowedActions = (reconciliation.Status is ReconciliationStatus.Matched or ReconciliationStatus.Variance) ? ["reconcile", "close"] : [],
            };
        }

        public async Task<IResult<OrderReconciliation>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.OrderReconciliationRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<OrderReconciliation>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<OrderReconciliation>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    Data = data,
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetById: ", ex.ToString()));
                throw;
            }
        }
    }
}
