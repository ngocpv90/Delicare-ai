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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using AppDelicare.Services.Interface;
using Serilog;
using System.Net;

namespace AppDelicare.Services.Implement
{
    /// <summary>BE2.1 (spec 10.1): lệnh đóng gói PH- sinh từ mẻ Đạt QC / lập tay, bắt đầu, ghi kết quả (PackingResult), hủy; mẻ chia hết + đóng gói xong → mẻ Đã đóng gói.</summary>
    public class PackingOrderService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService, IDocumentNumberService documentNumberService, IClock clock, ICurrentActor currentActor
        , IOptions<BusinessRulesOptions> rules) : IPackingOrderService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;
        private readonly IDocumentNumberService _documentNumberService = documentNumberService;
        private readonly IClock _clock = clock;
        private readonly ICurrentActor _currentActor = currentActor;
        private readonly BusinessRulesOptions _rules = rules.Value;

        public async Task<IResults<PackingOrderViewModel>> GetAll(PackingOrderSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@ProductionBatchId", request.ProductionBatchId);
                dynamicParameters.Add("@OrderId", request.OrderId);
                //dynamicParameters.Add("@PackingOrderNumber", request.PackingOrderNumber);
                dynamicParameters.Add("@PackingDate", request.PackingDate);
                dynamicParameters.Add("@Status", request.Status);
                //dynamicParameters.Add("@Note", request.Note);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<PackingOrderViewModel>("uspPackingOrder_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<PackingOrderViewModel>
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

        public IResults<PackingOrder> GetAllData()
        {
            try
            {
                var list = _unitOfWork.PackingOrderRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<PackingOrder>
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

        public async Task<IResult<bool>> Save(PackingOrderRequest request, string typeSave)
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

        /// <summary>BE2.1: Lệnh đóng gói chỉ ghi qua nghiệp vụ (PackingOrder/GenerateFromBatch / Create / Start / RecordResult / Cancel) – CRUD thêm / sửa / xóa không ghi (giữ máy trạng thái, người thao tác, số liệu).</summary>
        private Task<string> Insert(PackingOrderRequest request, string typeSave)
            => Task.FromResult(string.Format(ConstantHelper.Message.DeliveryCrudBlocked, "Lệnh đóng gói", "PackingOrder/GenerateFromBatch / Create / Start / RecordResult / Cancel"));

        private Task<string> Update(PackingOrderRequest request, string typeSave)
            => Task.FromResult(string.Format(ConstantHelper.Message.DeliveryCrudBlocked, "Lệnh đóng gói", "PackingOrder/GenerateFromBatch / Create / Start / RecordResult / Cancel"));

        private Task<string> Delete(Guid? id, string typeSave)
            => Task.FromResult(string.Format(ConstantHelper.Message.DeliveryCrudBlocked, "Lệnh đóng gói", "PackingOrder/GenerateFromBatch / Create / Start / RecordResult / Cancel"));

        // ============================================================ BE2.1 (spec 10.1) – lệnh đóng gói sinh từ mẻ, bắt đầu / kết quả / hủy
        // Mẻ Đạt QC (Passed, QcStatus PASS) → lệnh đóng gói PH- theo đơn (Σ số phần ≤ số đạt của mẻ) → Đang đóng gói → Đã đóng gói (PackingResult);
        // mẻ chia hết + mọi lệnh Đã đóng gói → mẻ Đã đóng gói (như ProductionBatch/Pack, kiểm lại nhiệt độ lõi ≥ 70 °C) → lệnh SX / kế hoạch hoàn thành.

        private static readonly string[] EligibleOrderStatus = [OrderStatus.Locked, OrderStatus.InProduction];

        private static string Number(PackingOrder p) => p.PackingOrderNumber ?? p.PackingNo ?? p.Id.ToString("N")[..8];

        private static string BatchNo(ProductionBatch b) => b.BatchNumber ?? b.BatchNo ?? b.Id.ToString("N")[..8];

        /// <summary>Số phần đạt của mẻ (sản lượng đạt đã ghi).</summary>
        private static decimal Accepted(ProductionBatch b) => b.AcceptedQty ?? b.ActualQuantity ?? b.ActualQty ?? 0m;

        private async Task<decimal> AllocatedAsync(Guid batchId, Guid? exceptId = null)
            => await _unitOfWork.DbContext.PackingOrder.AsNoTracking()
                .Where(p => !p.IsDeleted && p.ProductionBatchId == batchId && p.Id != exceptId && PackingOrderStatus.Active.Contains(p.Status!))
                .SumAsync(p => p.PlannedQty ?? 0m);

        private async Task<List<PackingOrderDetailModel>> DetailsAsync(List<PackingOrder> list)
        {
            var db = _unitOfWork.DbContext;
            var ids = list.Select(p => p.Id).ToList();
            var batchIds = list.Select(p => p.ProductionBatchId).Distinct().ToList();
            var batches = await db.ProductionBatch.AsNoTracking().Where(b => batchIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id);
            var dishIds = batches.Values.Select(b => b.DishId).Distinct().ToList();
            var dishes = await db.Dish.AsNoTracking().Where(d => dishIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name);
            var orderIds = list.Where(p => p.OrderId != null).Select(p => p.OrderId!.Value).Distinct().ToList();
            var orders = await db.Order.AsNoTracking().Where(o => orderIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id);
            var customerIds = orders.Values.Select(o => o.CustomerId).Distinct().ToList();
            var customers = await db.Customer.AsNoTracking().Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.DisplayName ?? c.Name);
            var results = await db.PackingResult.AsNoTracking().Where(r => !r.IsDeleted && ids.Contains(r.PackingOrderId)).ToListAsync();
            return [.. list.Select(p =>
            {
                var batch = batches.GetValueOrDefault(p.ProductionBatchId);
                var order = p.OrderId is { } oid ? orders.GetValueOrDefault(oid) : null;
                return new PackingOrderDetailModel
                {
                    PackingOrder = p,
                    StatusName = PackingOrderStatus.Label(p.Status),
                    BatchNumber = batch == null ? null : BatchNo(batch),
                    BatchStatus = batch?.Status,
                    DishName = batch == null ? null : dishes.GetValueOrDefault(batch.DishId),
                    OrderNumber = order?.OrderNumber ?? order?.OrderNo,
                    CustomerName = order == null ? null : customers.GetValueOrDefault(order.CustomerId),
                    ListResult = [.. results.Where(r => r.PackingOrderId == p.Id)],
                    AllowedActions = p.Status switch
                    {
                        PackingOrderStatus.Planned => ["start", "recordResult", "cancel"],
                        PackingOrderStatus.Packing => ["recordResult"],
                        _ => [],
                    },
                };
            }).OrderBy(x => x.PackingOrder.PackingOrderNumber, StringComparer.Ordinal)];
        }

        private async Task<PackingBatchModel> BatchModelAsync(Guid batchId, List<string> warnings)
        {
            var db = _unitOfWork.DbContext;
            var batch = await db.ProductionBatch.AsNoTracking().FirstAsync(b => b.Id == batchId);
            var list = await db.PackingOrder.AsNoTracking().Where(p => !p.IsDeleted && p.ProductionBatchId == batchId).ToListAsync();
            decimal accepted = Accepted(batch);
            decimal allocated = list.Where(p => PackingOrderStatus.Active.Contains(p.Status)).Sum(p => p.PlannedQty ?? 0m);
            return new PackingBatchModel
            {
                ProductionBatchId = batch.Id,
                BatchNumber = BatchNo(batch),
                BatchStatus = batch.Status,
                AcceptedQuantity = accepted,
                AllocatedQuantity = allocated,
                RemainingQuantity = accepted - allocated,
                ListPackingOrder = await DetailsAsync(list),
                ListWarning = warnings,
            };
        }

        /// <summary>BE2.1 (spec 10.1 GET packing-orders): lệnh đóng gói theo ngày đóng gói / trạng thái / đơn / mẻ (+ mẻ, món, đơn, khách, kết quả, thao tác được phép).</summary>
        public async Task<IResults<PackingOrderDetailModel>> GetList(PackingOrderListRequest request)
        {
            try
            {
                var query = _unitOfWork.DbContext.PackingOrder.AsNoTracking().Where(p => !p.IsDeleted);
                if (request.PackingDate is { } day)
                {
                    DateTime date = day.Date, next = date.AddDays(1);
                    query = query.Where(p => p.PackingDate >= date && p.PackingDate < next);
                }
                if (!string.IsNullOrWhiteSpace(request.Status))
                {
                    query = query.Where(p => p.Status == request.Status);
                }
                if (request.OrderId is { } oid && oid != Guid.Empty)
                {
                    query = query.Where(p => p.OrderId == oid);
                }
                if (request.ProductionBatchId is { } bid && bid != Guid.Empty)
                {
                    query = query.Where(p => p.ProductionBatchId == bid);
                }
                return Results<PackingOrderDetailModel>.Success(await DetailsAsync(await query.ToListAsync()), ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetList: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>BE2.1: lệnh đóng gói của 1 mẻ + số phần đạt / đã chia / còn lại.</summary>
        public async Task<IResult<PackingBatchModel>> GetByBatch(Guid id)
        {
            try
            {
                if (!await _unitOfWork.DbContext.ProductionBatch.AsNoTracking().AnyAsync(b => b.Id == id && !b.IsDeleted))
                {
                    return Result<PackingBatchModel>.Fail(ConstantHelper.Message.BatchNotFound);
                }
                return Result<PackingBatchModel>.Success(await BatchModelAsync(id, []), ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetByBatch: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Mẻ Đạt QC (Passed, QcStatus PASS) + lệnh SX + kiểm ATTP (nhiệt độ lõi gần nhất ≥ 70 °C, đã ghi sản lượng) – hoặc lỗi.</summary>
        private async Task<(ProductionBatch? Batch, ProductionOrder? Order, string? Error)> PassedBatchAsync(Guid batchId)
        {
            var batch = _unitOfWork.ProductionBatchRepository.GetSingle(b => b.Id == batchId && !b.IsDeleted);
            if (batch == null)
            {
                return (null, null, ConstantHelper.Message.BatchNotFound);
            }
            if (batch.Status != ProductionBatchStatus.Passed || !string.Equals(batch.QcStatus, BatchQcStatus.Pass, StringComparison.OrdinalIgnoreCase))
            {
                return (null, null, string.Format(ConstantHelper.Message.PackingBatchNotPassed, BatchNo(batch), ProductionBatchStatus.Label(batch.Status)));
            }
            var order = _unitOfWork.ProductionOrderRepository.GetSingle(o => o.Id == batch.ProductionOrderId && !o.IsDeleted);
            if (order == null)
            {
                return (null, null, ConstantHelper.Message.ProductionOrderNotFound);
            }
            var blockers = await ProductionRules.SafetyBlockersAsync(_unitOfWork.DbContext, batch, _rules, needOutput: true);
            if (blockers.Count != 0)
            {
                return (null, null, string.Format(ConstantHelper.Message.BatchCannotPack, BatchNo(batch), string.Join("; ", blockers)));
            }
            return (batch, order, null);
        }

        private PackingOrder NewPackingOrder(string number, ProductionBatch batch, Order order, decimal qty, DateTime packingDate, string? note)
        {
            var packing = new PackingOrder
            {
                Id = Guid.NewGuid(),
                PackingOrderNumber = number,
                PackingNo = number,
                ProductionBatchId = batch.Id,
                OrderId = order.Id,
                PlannedQty = qty,
                PackingDate = packingDate,
                Status = PackingOrderStatus.Planned,
                Note = string.Join(" – ", new[] { $"Mẻ {BatchNo(batch)} → đơn {order.OrderNumber ?? order.OrderNo}", DeliveryRules.Clean(note) }.Where(x => x != null)),
            };
            _unitOfWork.PackingOrderRepository.Add(packing);
            return packing;
        }

        /// <summary>
        /// BE2.1 (spec 10.1 "lệnh đóng gói sinh từ mẻ"): chia số phần đạt còn lại của mẻ Đạt QC cho các đơn Đã chốt / Đang sản xuất của ngày sản xuất, cùng ca với lệnh SX
        /// của mẻ (dòng đơn chưa gán ca = ca mặc định), theo tỉ lệ suất của món trong đơn (tính như MRP – <see cref="ProductionRules.DishDemandsAsync"/>); không có đơn nào
        /// có món trong thực đơn → chia theo tổng suất của đơn trong ca (cảnh báo). Số phần nguyên chia theo phần dư lớn nhất (tổng đúng bằng số còn lại);
        /// mỗi đơn 1 lệnh <c>PH-</c> Kế hoạch. GAP-PCK-01 (PROPOSED – BA/PO CONFIRM).
        /// </summary>
        public async Task<IResult<PackingBatchModel>> GenerateFromBatch(PackingOrderGenerateRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                _currentActor.RequireActor();
                var db = _unitOfWork.DbContext;
                var (batch, productionOrder, error) = await PassedBatchAsync(request.ProductionBatchId);
                if (batch == null)
                {
                    return Fail(error!);
                }
                decimal remaining = Accepted(batch) - await AllocatedAsync(batch.Id);
                if (remaining <= 0)
                {
                    return Fail(string.Format(ConstantHelper.Message.PackingNothingToAllocate, BatchNo(batch)));
                }
                var plan = await db.ProductionPlan.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productionOrder!.ProductionPlanId);
                DateTime date = (plan?.ProductionDate ?? batch.StartDate).Date;
                var (demands, orders, _, fallback) = await ProductionRules.DishDemandsAsync(db, date, EligibleOrderStatus, _rules);
                Guid? shiftId = productionOrder!.ShiftId;
                var warnings = new List<string>();
                var weights = demands.Where(d => d.DishId == batch.DishId && (shiftId == null || d.ShiftId == shiftId))
                    .GroupBy(d => d.OrderId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
                if (weights.Count == 0)
                {
                    var orderIds = orders.Select(o => o.Id).ToList();
                    var lines = await db.OrderLine.AsNoTracking().Where(l => orderIds.Contains(l.OrderId) && !l.IsDeleted && l.Quantity > 0).ToListAsync();
                    weights = lines.Where(l => shiftId == null || (l.ShiftId ?? fallback?.Id) == shiftId)
                        .GroupBy(l => l.OrderId).ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));
                    if (weights.Count != 0)
                    {
                        warnings.Add("Không tìm thấy món của mẻ trong thực đơn của các đơn – chia theo tổng suất đơn trong ca");
                    }
                }
                if (weights.Count == 0)
                {
                    string shiftCode = shiftId is { } sid ? await db.Shift.AsNoTracking().Where(s => s.Id == sid).Select(s => s.Code).FirstOrDefaultAsync() ?? "?" : "mọi ca";
                    return Fail(string.Format(ConstantHelper.Message.PackingNoOrders, ProductionRules.Day(date), shiftCode));
                }
                var byOrder = orders.ToDictionary(o => o.Id);
                var targets = weights.Where(w => w.Value > 0 && byOrder.ContainsKey(w.Key))
                    .OrderByDescending(w => w.Value).ThenBy(w => byOrder[w.Key].OrderNumber ?? byOrder[w.Key].OrderNo, StringComparer.Ordinal).ToList();
                decimal total = targets.Sum(t => t.Value);
                var shares = Allocate(remaining, targets.Select(t => t.Value / total).ToList());
                DateTime packingDate = request.PackingDate ?? _clock.Now;
                string first = await _documentNumberService.NextAsync(DocumentKind.PackingOrder, packingDate);
                int n = 0;
                for (int i = 0; i < targets.Count; i++)
                {
                    if (shares[i] <= 0)
                    {
                        continue;
                    }
                    NewPackingOrder(ProductionRules.Increment(first, n++), batch, byOrder[targets[i].Key], shares[i], packingDate, request.Note);
                }
                await _unitOfWork.PackingOrderRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                _unitOfWork.CommitTransaction();
                return Result<PackingBatchModel>.Success(await BatchModelAsync(batch.Id, warnings), ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GenerateFromBatch: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                throw;
            }

            IResult<PackingBatchModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                return Result<PackingBatchModel>.Fail(message);
            }
        }

        /// <summary>
        /// Chia <paramref name="total"/> theo tỉ lệ: tổng nguyên → phần nguyên theo phần dư lớn nhất (đơn đứng trước nhận trước khi bằng nhau); tổng lẻ → 4 số lẻ, phần cuối nhận phần còn lại.
        /// </summary>
        public static List<decimal> Allocate(decimal total, IReadOnlyList<decimal> ratios)
        {
            var result = new List<decimal>();
            if (ratios.Count == 0)
            {
                return result;
            }
            if (total == Math.Floor(total))
            {
                var raw = ratios.Select(r => total * r).ToList();
                result = [.. raw.Select(Math.Floor)];
                decimal left = total - result.Sum();
                foreach (int i in raw.Select((v, i) => (Frac: v - Math.Floor(v), i)).OrderByDescending(x => x.Frac).ThenBy(x => x.i).Select(x => x.i))
                {
                    if (left <= 0)
                    {
                        break;
                    }
                    result[i] += 1;
                    left -= 1;
                }
                return result;
            }
            decimal given = 0m;
            for (int i = 0; i < ratios.Count; i++)
            {
                decimal qty = i == ratios.Count - 1 ? total - given : ProductionRules.Round4(total * ratios[i]);
                given += qty;
                result.Add(qty);
            }
            return result;
        }

        /// <summary>
        /// BE2.1 (spec 10.1 POST packing-orders): lập lệnh đóng gói tay – mẻ Đạt QC, đơn Đã chốt / Đang sản xuất, Σ số phần các lệnh còn hiệu lực của mẻ ≤ số phần đạt của mẻ.
        /// Số <c>PH-</c> theo ngày đóng gói, Kế hoạch.
        /// </summary>
        public async Task<IResult<PackingBatchModel>> Create(PackingOrderCreateRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                _currentActor.RequireActor();
                if (request.PlannedQty <= 0)
                {
                    return Fail(ConstantHelper.Message.PackingQuantityInvalid);
                }
                var (batch, _, error) = await PassedBatchAsync(request.ProductionBatchId);
                if (batch == null)
                {
                    return Fail(error!);
                }
                var order = await _unitOfWork.DbContext.Order.AsNoTracking().FirstOrDefaultAsync(o => o.Id == request.OrderId && !o.IsDeleted);
                if (order == null)
                {
                    return Fail(ConstantHelper.Message.ReconcileOrderNotFound);
                }
                if (!EligibleOrderStatus.Contains(order.Status))
                {
                    return Fail(string.Format(ConstantHelper.Message.PackingOrderNotEligible, order.OrderNumber ?? order.OrderNo, OrderStatus.Label(order.Status)));
                }
                decimal allocated = await AllocatedAsync(batch.Id) + request.PlannedQty;
                if (allocated > Accepted(batch))
                {
                    return Fail(string.Format(ConstantHelper.Message.PackingOverAllocated, BatchNo(batch), DeliveryRules.Num(allocated), DeliveryRules.Num(Accepted(batch))));
                }
                DateTime packingDate = request.PackingDate ?? _clock.Now;
                NewPackingOrder(await _documentNumberService.NextAsync(DocumentKind.PackingOrder, packingDate), batch, order, request.PlannedQty, packingDate, request.Note);
                await _unitOfWork.PackingOrderRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                _unitOfWork.CommitTransaction();
                return Result<PackingBatchModel>.Success(await BatchModelAsync(batch.Id, []), ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Create: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                throw;
            }

            IResult<PackingBatchModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                return Result<PackingBatchModel>.Fail(message);
            }
        }

        /// <summary>Khung chung thao tác 1 lệnh đóng gói (1 transaction): nạp lệnh (đang theo dõi), kiểm trạng thái, chạy thân, lưu, commit, trả mẻ + các lệnh.</summary>
        private async Task<IResult<PackingBatchModel>> RunAsync(Guid id, string action, string[] allowed, Func<PackingOrder, ActorInfo, List<string>, Task<string?>> body)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var actor = _currentActor.RequireActor();
                var packing = _unitOfWork.PackingOrderRepository.GetSingle(p => p.Id == id && !p.IsDeleted);
                if (packing == null)
                {
                    return Fail(ConstantHelper.Message.PackingOrderNotFound);
                }
                if (!allowed.Contains(packing.Status))
                {
                    return Fail(string.Format(ConstantHelper.Message.StatusActionInvalid, action, "lệnh đóng gói " + Number(packing),
                        string.Join(" / ", allowed.Select(PackingOrderStatus.Label)), PackingOrderStatus.Label(packing.Status)));
                }
                var warnings = new List<string>();
                string? error = await body(packing, actor, warnings);
                if (error != null)
                {
                    return Fail(error);
                }
                _unitOfWork.PackingOrderRepository.Update(packing);
                await _unitOfWork.PackingOrderRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                return Result<PackingBatchModel>.Success(await BatchModelAsync(packing.ProductionBatchId, warnings), ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ", action, ": ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                throw;
            }

            IResult<PackingBatchModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                return Result<PackingBatchModel>.Fail(message);
            }
        }

        /// <summary>BE2.1 (spec 10.1 start): Kế hoạch → Đang đóng gói (mẻ vẫn phải Đạt QC, nhiệt độ lõi đạt).</summary>
        public Task<IResult<PackingBatchModel>> Start(PackingOrderActionRequest request)
            => RunAsync(request.Id, "bắt đầu đóng gói", [PackingOrderStatus.Planned], async (packing, actor, warnings) =>
            {
                var (batch, _, error) = await PassedBatchAsync(packing.ProductionBatchId);
                if (batch == null)
                {
                    return error;
                }
                packing.Status = PackingOrderStatus.Packing;
                return null;
            });

        /// <summary>BE2.1 (spec 10.1 cancel): chỉ lệnh Kế hoạch, lý do bắt buộc → Hủy; số phần trả về mẻ (chia lại được).</summary>
        public Task<IResult<PackingBatchModel>> Cancel(PackingOrderActionRequest request)
            => RunAsync(request.Id, "hủy lệnh đóng gói", [PackingOrderStatus.Planned], (packing, actor, warnings) =>
            {
                if (string.IsNullOrWhiteSpace(request.Reason))
                {
                    return Task.FromResult<string?>(ConstantHelper.Message.PackingReasonRequired);
                }
                packing.Status = PackingOrderStatus.Cancelled;
                packing.Note = string.Join(" – ", new[] { packing.Note, "Hủy: " + request.Reason.Trim() }.Where(x => !string.IsNullOrEmpty(x)));
                return Task.FromResult<string?>(null);
            });

        /// <summary>
        /// BE2.1 (spec 10.1 result): Kế hoạch / Đang đóng gói → Đã đóng gói. Đóng gói + hỏng + loại = số kế hoạch; mẻ phải Đạt QC và nhiệt độ lõi gần nhất ≥ 70 °C.
        /// Ghi PackingResult (AcceptedQty = số đóng gói, QualityStatus mặc định PASS); hỏng + loại &gt; 0 → MealWasteRecord PACKING_DAMAGE gắn mẻ, WasteQty của mẻ cộng thêm.
        /// Mẻ chia hết (còn lại ≤ 0) và mọi lệnh còn hiệu lực Đã đóng gói → mẻ Đã đóng gói (lệnh SX / kế hoạch hoàn thành như ProductionBatch/Pack).
        /// </summary>
        public Task<IResult<PackingBatchModel>> RecordResult(PackingOrderResultRequest request)
            => RunAsync(request.Id, "ghi kết quả đóng gói", [PackingOrderStatus.Planned, PackingOrderStatus.Packing], async (packing, actor, warnings) =>
            {
                decimal damaged = request.DamagedQty ?? 0m, rejected = request.RejectedQuantity ?? 0m;
                if (request.PackedQuantity < 0 || damaged < 0 || rejected < 0)
                {
                    return ConstantHelper.Message.PackingQuantityInvalid;
                }
                decimal planned = packing.PlannedQty ?? 0m;
                if (request.PackedQuantity + damaged + rejected != planned)
                {
                    return string.Format(ConstantHelper.Message.PackingQuantityMismatch, DeliveryRules.Num(request.PackedQuantity + damaged + rejected), DeliveryRules.Num(planned));
                }
                var (batch, productionOrder, error) = await PassedBatchAsync(packing.ProductionBatchId);
                if (batch == null)
                {
                    return error;
                }
                var db = _unitOfWork.DbContext;
                Guid? outputId = request.ProductionOutputId is { } oid && oid != Guid.Empty ? oid
                    : await db.ProductionOutput.AsNoTracking().Where(o => o.ProductionBatchId == batch.Id && !o.IsDeleted).Select(o => (Guid?)o.Id).FirstOrDefaultAsync();
                DateTime now = _clock.Now;
                _unitOfWork.PackingResultRepository.Add(new PackingResult
                {
                    Id = Guid.NewGuid(),
                    PackingOrderId = packing.Id,
                    ProductionOutputId = outputId,
                    PlannedQty = planned,
                    PackedQuantity = request.PackedQuantity,
                    PackedQty = request.PackedQuantity,
                    DamagedQty = damaged,
                    RejectedQuantity = rejected,
                    RejectedQty = rejected,
                    AcceptedQty = request.PackedQuantity,
                    PackagingType = DeliveryRules.Clean(request.PackagingType),
                    QualityStatus = DeliveryRules.Clean(request.QualityStatus)?.ToUpperInvariant() ?? BatchQcStatus.Pass,
                    Remark = DeliveryRules.Clean(request.Note),
                    Note = $"Ghi lúc {ProductionRules.Time(now)}",
                });
                packing.Status = PackingOrderStatus.Packed;
                if (damaged + rejected > 0)
                {
                    _unitOfWork.MealWasteRecordRepository.Add(new MealWasteRecord
                    {
                        Id = Guid.NewGuid(),
                        ProductionBatchId = batch.Id,
                        ProductionOrderId = productionOrder!.Id,
                        DishId = batch.DishId,
                        ShiftId = productionOrder.ShiftId,
                        WasteType = DeliveryCode.WastePacking,
                        QuantityMeals = damaged + rejected,
                        Reason = $"Đóng gói {Number(packing)}: hỏng {DeliveryRules.Num(damaged)}, loại {DeliveryRules.Num(rejected)}" + (DeliveryRules.Clean(request.Note) is { } n ? " – " + n : string.Empty),
                        OccurredAt = now,
                        RecordedBy = actor.UserId,
                    });
                    batch.WasteQty = (batch.WasteQty ?? 0m) + damaged + rejected;
                    _unitOfWork.ProductionBatchRepository.Update(batch);
                    warnings.Add($"Đã ghi hao hụt {DeliveryRules.Num(damaged + rejected)} suất (PACKING_DAMAGE) cho mẻ {BatchNo(batch)}");
                }
                // Mẻ chia hết + mọi lệnh còn hiệu lực đã đóng gói → mẻ Đã đóng gói.
                var others = await db.PackingOrder.AsNoTracking()
                    .Where(p => !p.IsDeleted && p.ProductionBatchId == batch.Id && p.Id != packing.Id && PackingOrderStatus.Active.Contains(p.Status!)).ToListAsync();
                decimal remaining = Accepted(batch) - planned - others.Sum(p => p.PlannedQty ?? 0m);
                if (others.All(p => p.Status == PackingOrderStatus.Packed))
                {
                    if (remaining <= 0)
                    {
                        batch.Status = ProductionBatchStatus.Packed;
                        _unitOfWork.ProductionBatchRepository.Update(batch);
                        await ProductionRules.CompleteOrderIfDoneAsync(_unitOfWork, now, batch, productionOrder!);
                        warnings.Add($"Mẻ {BatchNo(batch)} đã đóng gói xong");
                    }
                    else
                    {
                        warnings.Add(string.Format(ConstantHelper.Message.PackingLeftoverWarning, BatchNo(batch), DeliveryRules.Num(remaining)));
                    }
                }
                return null;
            });

        public async Task<IResult<PackingOrder>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.PackingOrderRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<PackingOrder>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<PackingOrder>
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
