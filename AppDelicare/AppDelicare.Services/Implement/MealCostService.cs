using AutoMapper;
using Dapper;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using AppDelicare.Services.Interface;
using Serilog;
using System.Net;
using AppDelicare.Data.Connection;
using AppDelicare.Data.Share;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Model.Request.Custom;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AppDelicare.Services.Implement
{
    public class MealCostService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService, IClock clock, ICurrentActor currentActor, IOptions<BusinessRulesOptions> rules) : IMealCostService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;
        private readonly IClock _clock = clock;
        private readonly ICurrentActor _currentActor = currentActor;
        private readonly BusinessRulesOptions _rules = rules.Value;

        public async Task<IResults<MealCostViewModel>> GetAll(MealCostSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@OrderId", request.OrderId);
                dynamicParameters.Add("@OrderLineId", request.OrderLineId);
                dynamicParameters.Add("@DishId", request.DishId);
                //dynamicParameters.Add("@AcceptedMealQuantity", request.AcceptedMealQuantity);
                //dynamicParameters.Add("@TotalActualCost", request.TotalActualCost);
                //dynamicParameters.Add("@CostPerMeal", request.CostPerMeal);
                //dynamicParameters.Add("@Revenue", request.Revenue);
                //dynamicParameters.Add("@GrossMargin", request.GrossMargin);
                dynamicParameters.Add("@CalculatedAt", request.CalculatedAt);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<MealCostViewModel>("uspMealCost_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<MealCostViewModel>
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

        public IResults<MealCost> GetAllData()
        {
            try
            {
                var list = _unitOfWork.MealCostRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<MealCost>
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

        public async Task<IResult<bool>> Save(MealCostRequest request, string typeSave)
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

        /// <summary>BE2.4: Giá vốn suất chỉ sinh / cập nhật qua nghiệp vụ (MealCost/Calculate) – CRUD Insert không ghi.</summary>
        private Task<string> Insert(MealCostRequest request, string typeSave)
            => Task.FromResult(string.Format(ConstantHelper.Message.ProductionCrudBlocked, "Giá vốn suất", "MealCost/Calculate"));

        /// <summary>BE2.4: Giá vốn suất chỉ sinh / cập nhật qua nghiệp vụ (MealCost/Calculate) – CRUD Update không ghi.</summary>
        private Task<string> Update(MealCostRequest request, string typeSave)
            => Task.FromResult(string.Format(ConstantHelper.Message.ProductionCrudBlocked, "Giá vốn suất", "MealCost/Calculate"));

        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // T07: ReferenceGuard (quan hệ logic, truy vấn có tham số) thay SQL động uspSystem_ExecuteQuery (A10).
            if (await _auditChangeService.IsReferencedAsync(nameof(MealCost), (Guid)id))
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            var delete = _unitOfWork.MealCostRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            _unitOfWork.MealCostRepository.Update(delete);
            await _unitOfWork.MealCostRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<MealCost>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.MealCostRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<MealCost>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<MealCost>
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

        /// <summary>
        /// BE2.4 (spec 8.5 meal-costs/calculate, Catalog 154) – giá vốn suất của 1 đơn đã giao / đối soát, 1 transaction (MealCost 1 dòng / (dòng đơn, món), cập nhật tại chỗ):
        /// <list type="number">
        /// <item>Suất nhận: OrderReconciliation.AcceptedQuantity (đã đối soát) → Σ POD AcceptedQuantity của đơn giao → suất đặt (cảnh báo); chia cho dòng đơn theo tỉ lệ suất đặt.</item>
        /// <item>Phần món theo dòng đơn = cùng công thức MRP / phát hành (dòng đơn × thực đơn, chia lựa chọn + dự phòng, đổi món đã duyệt).</item>
        /// <item>Giá thành 1 phần món của (ngày, ca, món) = Σ giá thành thực tế (ActualCost) các lệnh SX của món / ca ÷ Σ phần món của mọi đơn trong ngày / ca
        /// (phân bổ hết giá thành ngày theo tỉ lệ phần – spec "phân bổ ActualCost của ngày theo tỷ lệ suất"); lệnh chưa tính giá → giá thành chuẩn (cảnh báo).</item>
        /// <item>TotalActualCost = phần món × giá / phần; CostPerMeal (dòng đơn) = Σ món ÷ suất nhận; Revenue = LineAmount của dòng đơn chia cho món theo tỉ lệ giá vốn;
        /// GrossMargin = Revenue − TotalActualCost.</item>
        /// </list>
        /// PROPOSED – BA/PO CONFIRM (D-53 g).
        /// </summary>
        public async Task<IResult<MealCostResultModel>> Calculate(MealCostCalculateRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                _currentActor.RequireActor();
                var db = _unitOfWork.DbContext;
                var order = await db.Order.AsNoTracking().FirstOrDefaultAsync(o => o.Id == request.OrderId && !o.IsDeleted);
                if (order == null)
                {
                    return Fail(ConstantHelper.Message.MealCostOrderNotFound);
                }
                if (order.Status is not (OrderStatus.Delivered or OrderStatus.Reconciled))
                {
                    return Fail(string.Format(ConstantHelper.Message.StatusActionInvalid, "tính giá vốn suất", "đơn " + (order.OrderNumber ?? order.OrderNo),
                        "Đã giao / Đã đối soát", order.Status));
                }
                var warnings = new List<string>();
                var lines = await db.OrderLine.AsNoTracking().Where(l => l.OrderId == order.Id && !l.IsDeleted && l.Quantity > 0).ToListAsync();
                decimal ordered = lines.Sum(l => l.Quantity);
                var (accepted, source) = await AcceptedMealsAsync(db, order, ordered, warnings);
                DateTime day = order.MealDate.Date;
                var (demands, _, _, _) = await ProductionRules.DishDemandsAsync(db, day,
                    [OrderStatus.Locked, OrderStatus.InProduction, OrderStatus.Delivered, OrderStatus.Reconciled], _rules);
                var mine = demands.Where(d => d.OrderId == order.Id).ToList();
                if (mine.Count == 0)
                {
                    warnings.Add(ConstantHelper.Message.MealCostNoDemand);
                }
                // Giá / phần theo (món, ca).
                DateTime next = day.AddDays(1);
                var planIds = await db.ProductionPlan.AsNoTracking().Where(p => !p.IsDeleted && p.ProductionDate >= day && p.ProductionDate < next).Select(p => p.Id).ToListAsync();
                var productionOrders = await db.ProductionOrder.AsNoTracking()
                    .Where(o => !o.IsDeleted && planIds.Contains(o.ProductionPlanId) && o.Status != ProductionOrderStatus.Cancelled).ToListAsync();
                var stored = await CostingRules.StoredAsync(db, [.. productionOrders.Select(o => o.Id)]);
                var shifts = await db.Shift.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Code);
                var dishIds = mine.Select(d => d.DishId).Distinct().ToList();
                var dishes = await db.Dish.AsNoTracking().Where(d => dishIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name);
                var price = new Dictionary<(Guid Dish, Guid Shift), (decimal? PerPortion, string Source)>();
                foreach (var key in mine.Select(d => (d.DishId, d.ShiftId)).Distinct())
                {
                    var pos = productionOrders.Where(o => o.DishId == key.DishId && o.ShiftId == key.ShiftId).ToList();
                    var costed = pos.Where(o => stored.ContainsKey(o.Id)).ToList();
                    decimal demand = demands.Where(d => d.DishId == key.DishId && d.ShiftId == key.ShiftId).Sum(d => d.Quantity);
                    if (costed.Count != 0 && demand > 0)
                    {
                        price[key] = (costed.Sum(o => stored[o.Id].TotalCost) / demand, "ACTUAL");
                        continue;
                    }
                    decimal? standard = await CostingRules.StandardPerPortionAsync(db, key.DishId);
                    string dishName = dishes.GetValueOrDefault(key.DishId) ?? key.DishId.ToString();
                    warnings.Add(standard != null
                        ? string.Format(ConstantHelper.Message.MealCostUseStandard, dishName, shifts.GetValueOrDefault(key.ShiftId))
                        : string.Format(ConstantHelper.Message.MealCostNoCost, dishName));
                    price[key] = (standard, standard != null ? "STANDARD" : "NONE");
                }
                var existing = _unitOfWork.MealCostRepository.GetAll().Where(m => m.OrderId == order.Id && !m.IsDeleted).ToList();
                DateTime now = _clock.Now;
                // Món không còn trong đơn (đổi món / sửa thực đơn) → xóa mềm dòng giá vốn cũ trước (CommitAsync Delete đánh dấu mọi bản ghi đang sửa).
                var targets = mine.Select(d => (d.OrderLineId, d.DishId)).ToHashSet();
                var stale = existing.Where(m => m.OrderLineId == null || !targets.Contains((m.OrderLineId.Value, m.DishId))).ToList();
                if (stale.Count != 0)
                {
                    foreach (var m in stale)
                    {
                        db.Update(m);
                    }
                    await _unitOfWork.MealCostRepository.CommitAsync(ConstantHelper.TypeSave.Delete);
                    existing = [.. existing.Except(stale)];
                }
                foreach (var line in lines)
                {
                    var lineDemands = mine.Where(d => d.OrderLineId == line.Id).GroupBy(d => (d.DishId, d.ShiftId)).ToList();
                    decimal lineAccepted = ordered > 0 ? Math.Round(accepted * line.Quantity / ordered, 4) : 0m;
                    decimal lineRevenue = line.LineAmount != 0 ? line.LineAmount : line.Amount ?? (line.UnitPrice ?? 0m) * line.Quantity;
                    var costs = lineDemands.Select(g =>
                    {
                        decimal portions = g.Sum(d => d.Quantity);
                        var (perPortion, _) = price[g.Key];
                        return (g.Key, Portions: portions, Cost: Math.Round(portions * (perPortion ?? 0m), 4));
                    }).ToList();
                    decimal lineCost = costs.Sum(c => c.Cost);
                    decimal lineShare = costs.Sum(c => c.Portions);
                    foreach (var c in costs)
                    {
                        decimal share = lineCost > 0 ? c.Cost / lineCost : lineShare > 0 ? c.Portions / lineShare : 0m;
                        decimal revenue = Math.Round(lineRevenue * share, 4);
                        var row = existing.FirstOrDefault(m => m.OrderLineId == line.Id && m.DishId == c.Key.DishId);
                        if (row == null)
                        {
                            row = new MealCost { Id = Guid.NewGuid(), OrderId = order.Id, OrderLineId = line.Id, DishId = c.Key.DishId };
                            _unitOfWork.MealCostRepository.Add(row);
                        }
                        else
                        {
                            _unitOfWork.MealCostRepository.Update(row);
                        }
                        row.MealQty = c.Portions;
                        row.AcceptedMealQuantity = lineAccepted;
                        row.TotalActualCost = c.Cost;
                        row.TotalCost = Math.Round(c.Cost, 2);
                        row.CostPerMeal = lineAccepted > 0 ? Math.Round(c.Cost / lineAccepted, 2) : null;
                        row.Revenue = revenue;
                        row.GrossMargin = revenue - c.Cost;
                        row.CalculatedAt = now;
                    }
                }
                await _unitOfWork.MealCostRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                var result = await BuildAsync(db, order, accepted, source, price.ToDictionary(p => p.Key, p => p.Value.Source));
                result.ListWarning.InsertRange(0, warnings.Distinct());
                return Result<MealCostResultModel>.Success(result, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Calculate: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }

            IResult<MealCostResultModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                return Result<MealCostResultModel>.Fail(message);
            }
        }

        /// <summary>Suất nhận của đơn: đối soát → POD → suất đặt.</summary>
        private static async Task<(decimal Accepted, string Source)> AcceptedMealsAsync(DatabaseContext db, Order order, decimal ordered, List<string> warnings)
        {
            var rec = await db.OrderReconciliation.AsNoTracking().Where(r => !r.IsDeleted && r.OrderId == order.Id)
                .OrderByDescending(r => r.ReconciliationDate).ThenByDescending(r => r.CreatedDate).FirstOrDefaultAsync();
            if (rec != null && (rec.AcceptedQuantity > 0 || order.Status == OrderStatus.Reconciled))
            {
                return (rec.AcceptedQuantity, "RECONCILIATION");
            }
            var deliveryIds = await db.DeliveryOrder.AsNoTracking().Where(d => !d.IsDeleted && d.OrderId == order.Id).Select(d => d.Id).ToListAsync();
            var pods = (await db.ProofOfDelivery.AsNoTracking().Where(p => !p.IsDeleted && deliveryIds.Contains(p.DeliveryOrderId)).ToListAsync())
                .GroupBy(p => p.DeliveryOrderId).Select(g => g.OrderByDescending(p => p.ConfirmedAt).First()).ToList();
            if (pods.Count != 0)
            {
                warnings.Add(ConstantHelper.Message.MealCostNotReconciled);
                return (pods.Sum(p => p.AcceptedQuantity ?? p.AcceptedQty ?? 0m), "POD");
            }
            warnings.Add(ConstantHelper.Message.MealCostNoPod);
            return (ordered, "ORDERED");
        }

        /// <summary>Kết quả giá vốn suất của đơn từ các dòng MealCost đã ghi.</summary>
        private async Task<MealCostResultModel> BuildAsync(DatabaseContext db, Order order, decimal? accepted, string? source,
            IReadOnlyDictionary<(Guid Dish, Guid Shift), string>? sources)
        {
            var rows = await db.MealCost.AsNoTracking().Where(m => !m.IsDeleted && m.OrderId == order.Id).ToListAsync();
            var lines = await db.OrderLine.AsNoTracking().Where(l => l.OrderId == order.Id && !l.IsDeleted && l.Quantity > 0).ToListAsync();
            var dishIds = rows.Select(r => r.DishId).Distinct().ToList();
            var dishes = await db.Dish.AsNoTracking().Where(d => dishIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name);
            var shifts = await db.Shift.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Code);
            var fallbackShift = await db.Shift.AsNoTracking().Where(s => s.Code == _rules.ProductionDefaultShiftCode && !s.IsDeleted).Select(s => (Guid?)s.Id).FirstOrDefaultAsync();
            var result = new MealCostResultModel
            {
                OrderId = order.Id,
                OrderNumber = order.OrderNumber ?? order.OrderNo,
                MealDate = order.MealDate,
                OrderStatus = order.Status,
                OrderedMeals = lines.Sum(l => l.Quantity),
            };
            foreach (var line in lines)
            {
                var mine = rows.Where(r => r.OrderLineId == line.Id).ToList();
                Guid shiftId = line.ShiftId ?? fallbackShift ?? Guid.Empty;
                decimal lineAccepted = mine.FirstOrDefault()?.AcceptedMealQuantity
                    ?? (accepted is { } a && result.OrderedMeals > 0 ? Math.Round(a * line.Quantity / result.OrderedMeals, 4) : 0m);
                decimal cost = mine.Sum(r => r.TotalActualCost);
                decimal revenue = mine.Sum(r => r.Revenue ?? 0m);
                result.ListLine.Add(new MealCostLineModel
                {
                    OrderLineId = line.Id,
                    MealType = line.MealType,
                    ShiftCode = line.ShiftId != null ? shifts.GetValueOrDefault(line.ShiftId.Value) : null,
                    OrderedMeals = line.Quantity,
                    AcceptedMeals = lineAccepted,
                    TotalActualCost = cost,
                    CostPerMeal = lineAccepted > 0 ? Math.Round(cost / lineAccepted, 2) : null,
                    Revenue = revenue,
                    GrossMargin = revenue - cost,
                    GrossMarginPercent = revenue != 0 ? Math.Round((revenue - cost) / revenue * 100m, 2) : null,
                    ListDish = [.. mine.Select(r => new MealCostDishModel
                    {
                        Id = r.Id,
                        DishId = r.DishId,
                        DishName = dishes.GetValueOrDefault(r.DishId),
                        Portions = r.MealQty ?? 0m,
                        TotalActualCost = r.TotalActualCost,
                        CostPerPortion = r.MealQty is > 0 ? Math.Round(r.TotalActualCost / r.MealQty.Value, 4) : null,
                        CostSource = sources?.GetValueOrDefault((r.DishId, shiftId)) ?? "STORED",
                        Revenue = r.Revenue,
                        GrossMargin = r.GrossMargin,
                    }).OrderBy(d => d.DishName)],
                });
            }
            result.AcceptedMeals = accepted ?? result.ListLine.Sum(l => l.AcceptedMeals);
            result.AcceptedSource = source ?? "STORED";
            result.TotalActualCost = result.ListLine.Sum(l => l.TotalActualCost);
            result.CostPerMeal = result.AcceptedMeals > 0 ? Math.Round(result.TotalActualCost / result.AcceptedMeals, 2) : null;
            result.Revenue = result.ListLine.Sum(l => l.Revenue);
            result.GrossMargin = result.Revenue - result.TotalActualCost;
            result.GrossMarginPercent = result.Revenue != 0 ? Math.Round(result.GrossMargin / result.Revenue * 100m, 2) : null;
            return result;
        }

        /// <summary>BE2.4: giá vốn suất đã tính – theo đơn hoặc ngày ăn (từ / đến).</summary>
        public async Task<IResults<MealCostResultModel>> GetList(MealCostListRequest request)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                // Rà soát BE: không lọc đơn → bắt buộc khoảng ngày ăn (mặc định 31 ngày gần nhất, tối đa 92 ngày) – trước đây tải mọi đơn
                // có giá vốn rồi tính lại từng đơn (5+ truy vấn / đơn).
                if (request.OrderId == null)
                {
                    DateTime to = (request.To ?? _clock.Today).Date;
                    DateTime from = (request.From ?? to.AddDays(-30)).Date;
                    if (from > to || (to - from).TotalDays > 91)
                    {
                        return new Results<MealCostResultModel>
                        {
                            ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                            ResponseMessage = "Khoảng ngày ăn không hợp lệ: Từ ngày ≤ Đến ngày, tối đa 92 ngày (hoặc lọc theo đơn – OrderId).",
                        };
                    }
                    request.From = from;
                    request.To = to;
                }
                var orderIds = await db.MealCost.AsNoTracking().Where(m => !m.IsDeleted && (request.OrderId == null || m.OrderId == request.OrderId))
                    .Select(m => m.OrderId).Distinct().ToListAsync();
                var orders = await db.Order.AsNoTracking()
                    .Where(o => orderIds.Contains(o.Id) && (request.From == null || o.MealDate >= request.From.Value.Date)
                        && (request.To == null || o.MealDate < request.To.Value.Date.AddDays(1)))
                    .OrderBy(o => o.MealDate).ThenBy(o => o.OrderNumber).ToListAsync();
                var list = new List<MealCostResultModel>();
                foreach (var order in orders)
                {
                    list.Add(await BuildAsync(db, order, null, null, null));
                }
                return new Results<MealCostResultModel>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    ListData = list,
                    TotalRecords = list.Count,
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetList: ", ex.ToString()));
                throw;
            }
        }
    }
}
