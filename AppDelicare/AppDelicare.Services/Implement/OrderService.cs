using AutoMapper;
using Dapper;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Data.Share;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Globalization;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using AppDelicare.Services.Interface;
using Serilog;
using System.Net;

namespace AppDelicare.Services.Implement
{
    public partial class OrderService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper, IClock clock
        , IOptions<BusinessRulesOptions> rules, IDocumentNumberService documentNumberService, IPricePolicyService pricePolicyService
        , IMenuAssignmentService menuAssignmentService, IApprovalRequestService approvalRequestService
        , IAuditChangeService auditChangeService) : IOrderService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;
        private readonly IClock _clock = clock;
        private readonly BusinessRulesOptions _rules = rules.Value;
        private readonly IDocumentNumberService _documentNumberService = documentNumberService;
        private readonly IPricePolicyService _pricePolicyService = pricePolicyService;
        private readonly IMenuAssignmentService _menuAssignmentService = menuAssignmentService;
        private readonly IApprovalRequestService _approvalRequestService = approvalRequestService;

        public async Task<IResults<OrderViewModel>> GetAll(OrderSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@CustomerId", request.CustomerId == Guid.Empty ? null : request.CustomerId);
                dynamicParameters.Add("@ContractId", request.ContractId);
                dynamicParameters.Add("@DeliveryLocationId", request.DeliveryLocationId);
                dynamicParameters.Add("@OrderNumber", request.OrderNumber);
                dynamicParameters.Add("@OrderDate", request.OrderDate);
                dynamicParameters.Add("@DeliveryDate", request.DeliveryDate);
                dynamicParameters.Add("@Status", request.Status);
                dynamicParameters.Add("@TotalQuantity", request.TotalQuantity);
                dynamicParameters.Add("@TotalAmount", request.TotalAmount);
                dynamicParameters.Add("@FromMealDate", request.FromMealDate?.Date);
                dynamicParameters.Add("@ToMealDate", request.ToMealDate?.Date);
                //dynamicParameters.Add("@Note", request.Note);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<OrderViewModel>("uspOrder_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<OrderViewModel>
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

        public IResults<Order> GetAllData()
        {
            try
            {
                var list = _unitOfWork.OrderRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<Order>
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

        public async Task<IResult<bool>> Save(OrderRequest request, string typeSave)
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

        /// <summary>T13: tạo đơn chỉ qua <c>Order/Create</c> (sinh số PH-, giá, thực đơn, giờ chốt) – CRUD Insert không ghi.</summary>
        private Task<string> Insert(OrderRequest request, string typeSave)
            => Task.FromResult(ConstantHelper.Message.OrderInsertBlocked);

        /// <summary>T13: CRUD Update chỉ sửa ghi chú / ngày giao của đơn Nháp; trạng thái, số, khách, ngày ăn, tổng tiền chỉ đổi qua action nghiệp vụ.</summary>
        private async Task<string> Update(OrderRequest request, string typeSave)
        {
            var exist = _unitOfWork.OrderRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            if (exist.Status != OrderStatus.Draft)
            {
                return InvalidStatus(exist, "sửa đơn", OrderStatus.Draft);
            }
            exist.Note = request.Note;
            if (request.DeliveryDate != default)
            {
                exist.DeliveryDate = request.DeliveryDate;
            }
            _unitOfWork.OrderRepository.Update(exist);
            await _unitOfWork.OrderRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        /// <summary>T13: chỉ xóa (mềm) đơn Nháp, kèm các dòng; đơn đã gửi trở đi dùng Cancel.</summary>
        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            var delete = _unitOfWork.OrderRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            if (delete.Status != OrderStatus.Draft)
            {
                return InvalidStatus(delete, "xóa đơn", OrderStatus.Draft);
            }
            // T07: ReferenceGuard (quan hệ logic, truy vấn có tham số); dòng của chính đơn xóa cùng đơn.
            if (await _auditChangeService.IsReferencedAsync(nameof(Order), (Guid)id, nameof(OrderLine)))
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            foreach (var line in _unitOfWork.OrderLineRepository.GetAll().Where(l => l.OrderId == delete.Id && !l.IsDeleted).ToList())
            {
                _unitOfWork.OrderLineRepository.Update(line);
            }
            _unitOfWork.OrderRepository.Update(delete);
            await _unitOfWork.OrderRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<Order>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.OrderRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<Order>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<Order>
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

        // ============================================================ T13 – đơn hàng (spec 6.6, BUSINESS_RULES §2)
        private const string ActiveStatus = "Active";

        private static string InvalidStatus(Order order, string action, params string[] expected)
            => string.Format(ConstantHelper.Message.StatusActionInvalid, action, "đơn", string.Join(" / ", expected.Select(OrderStatus.Label)), OrderStatus.Label(order.Status));

        /// <summary>
        /// Giờ chốt đơn của ngày ăn D = ngày D-1 lúc <c>BusinessRules:OrderCutoffHourPreviousDay</c> giờ (mặc định 20:00), giờ Việt Nam (IClock).
        /// Từ giờ chốt trở đi (≥) là "sau giờ chốt" (PROPOSED – BA/PO CONFIRM).
        /// </summary>
        public DateTime CutoffOf(DateTime mealDate) => mealDate.Date.AddDays(-1).AddHours(_rules.OrderCutoffHourPreviousDay);

        private bool IsAfterCutoff(DateTime mealDate) => _clock.Now >= CutoffOf(mealDate);

        private string AfterCutoff(DateTime mealDate, string action)
            => string.Format(ConstantHelper.Message.OrderAfterCutoff, CutoffOf(mealDate).ToString("HH:mm dd/MM/yyyy"), mealDate.ToString("dd/MM/yyyy"), action);

        private Task<Contract?> ActiveContractAsync(Guid customerId, DateTime mealDate, Guid? contractId)
        {
            DateTime day = mealDate.Date;
            return _unitOfWork.ContractRepository.GetAll().AsNoTracking()
                // BE2.2: hợp đồng Chấm dứt vẫn nhận đơn tới hết ngày chấm dứt (EffectiveTo) – PROPOSED D-51.
                .Where(c => !c.IsDeleted && c.CustomerId == customerId && (c.Status == ActiveStatus || c.Status == ContractStatus.Terminated)
                    && c.EffectiveFrom <= day && (c.EffectiveTo == null || c.EffectiveTo >= day)
                    && (contractId == null || c.Id == contractId))
                .OrderByDescending(c => c.EffectiveFrom).FirstOrDefaultAsync();
        }

        /// <summary>Ca mặc định theo loại suất: BusinessRules:MealTypeDefaultShift (MealType → Shift.Code).</summary>
        private async Task<Shift?> DefaultShiftAsync(string? mealType)
        {
            if (mealType == null || !_rules.MealTypeDefaultShift.TryGetValue(mealType, out var code))
            {
                return null;
            }
            return await _unitOfWork.ShiftRepository.GetAll().AsNoTracking().FirstOrDefaultAsync(s => s.Code == code && !s.IsDeleted);
        }

        /// <summary>Yêu cầu duyệt đang chờ của đơn (lệch suất ORDER_DEVIATION hoặc sửa sau giờ chốt ORDER_CHANGE_AFTER_CUTOFF – BE2.2).</summary>
        private async Task<ApprovalRequest?> PendingDeviationAsync(Guid orderId)
            => await _unitOfWork.ApprovalRequestRepository.GetAll().AsNoTracking()
                .Where(a => !a.IsDeleted && OrderStatus.ApprovalBusinessTypes.Contains(a.BusinessType) && a.BusinessId == orderId && ApprovalRequestService.OpenStatuses.Contains(a.RequestStatus))
                .OrderByDescending(a => a.RequestedAt).FirstOrDefaultAsync();

        /// <summary>Spec 6.6 new-template: hợp đồng Active tại ngày ăn (không có → lỗi), ContractMeal × điểm giao, ca mặc định, đơn giá, thực đơn.</summary>
        public async Task<IResult<OrderTemplateModel>> GetNewTemplate(Guid customerId, DateTime mealDate)
        {
            try
            {
                var customer = await _unitOfWork.CustomerRepository.GetAll().AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId && !c.IsDeleted);
                if (customer == null)
                {
                    return Result<OrderTemplateModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                DateTime day = mealDate.Date;
                var contract = await ActiveContractAsync(customerId, day, null);
                if (contract == null)
                {
                    return Result<OrderTemplateModel>.Fail(string.Format(ConstantHelper.Message.NoActiveContract, day.ToString("dd/MM/yyyy")));
                }
                var meals = await _unitOfWork.ContractMealRepository.GetAll().AsNoTracking()
                    .Where(m => m.ContractId == contract.Id && !m.IsDeleted && (m.Status == null || m.Status == ActiveStatus)).ToListAsync();
                var locations = await _unitOfWork.CustomerDeliveryLocationRepository.GetAll().AsNoTracking()
                    .Where(l => l.CustomerId == customerId && !l.IsDeleted && (l.Status == null || l.Status == ActiveStatus))
                    .OrderBy(l => l.Code).ThenBy(l => l.Name).ToListAsync();
                var existing = await _unitOfWork.OrderRepository.GetAll().AsNoTracking()
                    .Where(o => !o.IsDeleted && o.CustomerId == customerId && o.MealDate == day && o.Status != OrderStatus.Cancelled).ToListAsync();
                var model = new OrderTemplateModel
                {
                    CustomerId = customerId, CustomerName = customer.Name, MealDate = day, ContractId = contract.Id,
                    ContractNumber = contract.ContractNumber ?? contract.ContractNo, ContractName = contract.Name,
                    CutoffAt = CutoffOf(day), IsAfterCutoff = IsAfterCutoff(day),
                };
                var shifts = new Dictionary<string, Shift?>();
                var prices = new Dictionary<string, PriceResolveModel?>();
                foreach (var meal in meals.Where(m => m.MealType != null))
                {
                    shifts[meal.MealType!] = await DefaultShiftAsync(meal.MealType);
                    var price = await _pricePolicyService.Resolve(new PriceResolveRequest { CustomerId = customerId, ContractId = contract.Id, MealType = meal.MealType, Date = day });
                    prices[meal.MealType!] = price.ResponseCode == (int)HttpStatusCode.OK ? price.Data : null;
                }
                // BE2.2 (spec 6.6 new-template, Catalog 92): số gợi ý = dự báo Đã công bố mới nhất (ForecastRules.ForLocation).
                var forecasts = await ForecastRules.LatestPublishedAsync(_unitOfWork.DbContext, day, day, [customerId]);
                var locationIds = locations.Select(l => l.Id).ToList();
                foreach (var location in locations)
                {
                    var order = existing.FirstOrDefault(o => o.DeliveryLocationId == location.Id);
                    var item = new OrderTemplateLocationModel
                    {
                        DeliveryLocationId = location.Id, LocationCode = location.Code, LocationName = location.Name,
                        ExistingOrderId = order?.Id, ExistingOrderNumber = order?.OrderNumber ?? order?.OrderNo,
                    };
                    foreach (var meal in meals)
                    {
                        var shift = meal.MealType != null ? shifts.GetValueOrDefault(meal.MealType) : null;
                        var price = meal.MealType != null ? prices.GetValueOrDefault(meal.MealType) : null;
                        var menu = await _menuAssignmentService.ResolveAsync(customerId, location.Id, meal.MealType, day);
                        var options = await MenuOptionRules.OptionsAsync(_unitOfWork.DbContext, menu?.MenuId, meal.MealType, day);
                        item.ListLine.Add(new OrderTemplateLineModel
                        {
                            ContractMealId = meal.Id, MealType = meal.MealType, MealName = meal.MealName, ServiceType = meal.ServiceType,
                            ShiftId = shift?.Id, ShiftCode = shift?.Code, ShiftName = shift?.Name,
                            UnitPrice = price?.UnitPrice, PriceSource = price?.Source, MenuId = menu?.MenuId, MenuCode = menu?.MenuCode,
                            SuggestedQuantity = ForecastRules.ForLocation(forecasts, customerId, location.Id, locationIds, meal.MealType, shift?.Id) ?? 0m,
                            ListOption = options,
                        });
                    }
                    model.ListLocation.Add(item);
                }
                return Result<OrderTemplateModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetNewTemplate: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>
        /// Spec 6.6 create (1 transaction): kiểm giờ chốt, hợp đồng Active, điểm giao của khách, không trùng đơn (khách, điểm, ngày);
        /// số <c>PH-YYMMDD-NNNN</c> (BUSINESS_RULES §11, ghi cả OrderNumber và OrderNo), Status = Draft, OrderDate = bây giờ;
        /// mỗi dòng: MealType theo ContractMeal, ca mặc định, MenuId theo phân công, UnitPrice theo tra giá, LineAmount (+ Amount cũ);
        /// bỏ dòng số lượng 0; tổng TotalQuantity / TotalAmount.
        /// </summary>
        public async Task<IResult<OrderDetailModel>> Create(OrderCreateRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var warnings = new List<string>();
                var (order, lines, error) = await BuildOrderAsync(request, warnings);
                if (error != null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<OrderDetailModel>.Fail(error);
                }
                string number = await _documentNumberService.NextAsync(DocumentKind.Order, _clock.Today);
                order!.OrderNumber = number;
                order.OrderNo = number;
                _unitOfWork.OrderRepository.Add(order);
                foreach (var line in lines!)
                {
                    _unitOfWork.OrderLineRepository.Add(line);
                }
                await _unitOfWork.OrderRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                string message = ConstantHelper.Message.SaveSuccess;
                bool afterCutoff = IsAfterCutoff(order.MealDate);
                if (afterCutoff)
                {
                    // BE2.2 (ORDER_CHANGE_AFTER_CUTOFF): thêm đơn sau giờ chốt – đơn Nháp + yêu cầu duyệt; duyệt → Đã chốt, từ chối / hủy → Hủy.
                    var (approval, approvalError) = await _approvalRequestService.CreateAsync(OrderStatus.ChangeAfterCutoffBusinessType, order.Id,
                        JsonConvert.SerializeObject(new AfterCutoffProposal { Kind = AfterCutoffProposal.Create, Reason = request.AfterCutoffReason!.Trim() }));
                    if (approvalError != null)
                    {
                        _unitOfWork.RollbackTransaction();
                        return Result<OrderDetailModel>.Fail(approvalError);
                    }
                    message = string.Format(ConstantHelper.Message.OrderCreateAfterCutoffRequested, number, approval!.RequestNo);
                }
                _unitOfWork.CommitTransaction();
                var detail = await DetailAsync(order.Id);
                detail!.ListWarning.AddRange(warnings);
                detail.ApprovalRequested = afterCutoff;
                return Result<OrderDetailModel>.Success(detail, message);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Create: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        private async Task<(Order? Order, List<OrderLine>? Lines, string? Error)> BuildOrderAsync(OrderCreateRequest request, List<string> warnings)
        {
            DateTime day = request.MealDate.Date;
            if (IsAfterCutoff(day) && string.IsNullOrWhiteSpace(request.AfterCutoffReason))
            {
                return (null, null, AfterCutoff(day, "tạo đơn"));
            }
            if (IsAfterCutoff(day) && request.AfterCutoffReason!.Trim().Length < 3)
            {
                return (null, null, ConstantHelper.Message.OrderChangeReasonRequired);
            }
            if (!await _unitOfWork.CustomerRepository.GetAll().AnyAsync(c => c.Id == request.CustomerId && !c.IsDeleted))
            {
                return (null, null, string.Format(ConstantHelper.Message.NotExist, "Khách hàng").Trim());
            }
            if (!await _unitOfWork.CustomerDeliveryLocationRepository.GetAll().AnyAsync(l => l.Id == request.DeliveryLocationId && l.CustomerId == request.CustomerId && !l.IsDeleted))
            {
                return (null, null, ConstantHelper.Message.MenuAssignmentLocationMismatch);
            }
            var contract = await ActiveContractAsync(request.CustomerId, day, request.ContractId);
            if (contract == null)
            {
                return (null, null, request.ContractId == null
                    ? string.Format(ConstantHelper.Message.NoActiveContract, day.ToString("dd/MM/yyyy"))
                    : string.Format(ConstantHelper.Message.ContractNotActiveOnDate, day.ToString("dd/MM/yyyy")));
            }
            var duplicate = await _unitOfWork.OrderRepository.GetAll().AsNoTracking()
                .FirstOrDefaultAsync(o => !o.IsDeleted && o.CustomerId == request.CustomerId && o.DeliveryLocationId == request.DeliveryLocationId
                    && o.MealDate == day && o.Status != OrderStatus.Cancelled);
            if (duplicate != null)
            {
                return (null, null, string.Format(ConstantHelper.Message.OrderDuplicate, duplicate.OrderNumber ?? duplicate.OrderNo));
            }
            var input = request.ListLine ?? [];
            if (input.Any(l => l.Quantity < 0))
            {
                return (null, null, ConstantHelper.Message.OrderQuantityNegative);
            }
            if (input.Any(l => l.DeliveryLocationId != null && l.DeliveryLocationId != request.DeliveryLocationId))
            {
                return (null, null, ConstantHelper.Message.OrderLineLocationMismatch);
            }
            input = input.Where(l => l.Quantity > 0).ToList();
            if (input.Count == 0)
            {
                return (null, null, ConstantHelper.Message.OrderNeedsLine);
            }
            var mealIds = input.Select(l => l.ContractMealId).Distinct().ToList();
            var meals = await _unitOfWork.ContractMealRepository.GetAll().AsNoTracking()
                .Where(m => mealIds.Contains(m.Id) && m.ContractId == contract.Id && !m.IsDeleted).ToDictionaryAsync(m => m.Id);
            if (meals.Count != mealIds.Count)
            {
                return (null, null, ConstantHelper.Message.OrderContractMealInvalid);
            }
            var order = new Order
            {
                Id = Guid.NewGuid(), CustomerId = request.CustomerId, ContractId = contract.Id, DeliveryLocationId = request.DeliveryLocationId,
                OrderDate = _clock.Now, MealDate = day, DeliveryDate = (request.DeliveryDate ?? day).Date, Status = OrderStatus.Draft, Note = request.Note,
            };
            var lines = new List<OrderLine>();
            foreach (var l in input)
            {
                var meal = meals[l.ContractMealId];
                Guid? shiftId = l.ShiftId ?? (await DefaultShiftAsync(meal.MealType))?.Id;
                if (shiftId == null)
                {
                    return (null, null, string.Format(ConstantHelper.Message.OrderShiftNotFound, meal.MealType));
                }
                var menu = await _menuAssignmentService.ResolveAsync(request.CustomerId, request.DeliveryLocationId, meal.MealType, day);
                if (menu == null)
                {
                    warnings.Add($"{ConstantHelper.Message.MenuNotAssigned} ({meal.MealType})");
                }
                var (optionNo, optionError) = await OptionOfLineAsync(l.MenuOptionNo, menu?.MenuId, meal.MealType, day);
                if (optionError != null)
                {
                    return (null, null, optionError);
                }
                // ORD-OPT-1.3: không trùng (loại suất của hợp đồng, ca, lựa chọn) trong một đơn.
                if (lines.Any(x => x.ContractMealId == meal.Id && x.ShiftId == shiftId && x.MenuOptionNo == optionNo))
                {
                    return (null, null, ConstantHelper.Message.OrderLineDuplicate);
                }
                var price = await _pricePolicyService.Resolve(new PriceResolveRequest { CustomerId = request.CustomerId, ContractId = contract.Id, MealType = meal.MealType, Date = day });
                if (price.ResponseCode != (int)HttpStatusCode.OK || price.Data == null)
                {
                    return (null, null, $"{price.ResponseMessage} ({meal.MealType})");
                }
                decimal amount = Math.Round(l.Quantity * price.Data.UnitPrice, 2);
                lines.Add(new OrderLine
                {
                    Id = Guid.NewGuid(), OrderId = order.Id, ContractMealId = meal.Id, MealType = meal.MealType, ShiftId = shiftId, MenuId = menu?.MenuId,
                    MenuOptionNo = optionNo, Quantity = l.Quantity, UnitPrice = price.Data.UnitPrice, LineAmount = amount, Amount = amount, Note = l.Note,
                });
            }
            order.TotalQuantity = lines.Sum(x => x.Quantity);
            order.TotalAmount = lines.Sum(x => x.LineAmount);
            return (order, lines, null);
        }

        /// <summary>
        /// ORD-OPT-1.3 (D-74 c): lựa chọn của dòng đơn. Có gửi → phải 1–9 và có trong thực đơn áp dụng (loại suất, thứ của ngày ăn);
        /// không gửi → thực đơn chỉ 1 lựa chọn thì = lựa chọn đó, còn lại NULL (dòng không theo lựa chọn – GAP-MRP-03).
        /// </summary>
        private async Task<(int? Option, string? Error)> OptionOfLineAsync(int? requested, Guid? menuId, string? mealType, DateTime day)
        {
            var options = await MenuOptionRules.OptionsAsync(_unitOfWork.DbContext, menuId, mealType, day);
            if (requested == null)
            {
                return (options.Count == 1 ? options[0].OptionNo : null, null);
            }
            if (menuId == null)
            {
                return (null, string.Format(ConstantHelper.Message.OrderOptionNoMenu, mealType, day.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)));
            }
            if (!options.Any(o => o.OptionNo == requested))
            {
                return (null, string.Format(ConstantHelper.Message.OrderOptionNotInMenu, requested, mealType, day.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                    options.Count == 0 ? "không có" : string.Join(", ", options.Select(o => o.OptionNo))));
            }
            return (requested, null);
        }

        /// <summary>
        /// Ghi số suất mới cho các dòng (đã kiểm tra): tính lại LineAmount / Amount và tổng đơn; số lượng 0 = xóa mềm dòng.
        /// Chạy trong transaction của người gọi (UpdateLines hoặc duyệt ORDER_DEVIATION). Trả lỗi hoặc null.
        /// </summary>
        public async Task<string?> ApplyLinesAsync(Guid orderId, IReadOnlyCollection<OrderLineQuantityRequest> changes)
        {
            var order = _unitOfWork.OrderRepository.GetSingle(o => o.Id == orderId && !o.IsDeleted);
            if (order == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            var lines = _unitOfWork.OrderLineRepository.GetAll().Where(l => l.OrderId == orderId && !l.IsDeleted).ToList();
            foreach (var change in changes)
            {
                if (lines.All(l => l.Id != change.Id))
                {
                    return string.Format(ConstantHelper.Message.OrderLineNotInOrder, change.Id);
                }
                if (change.Quantity < 0)
                {
                    return ConstantHelper.Message.OrderQuantityNegative;
                }
            }
            var removed = new List<OrderLine>();
            foreach (var line in lines)
            {
                var change = changes.FirstOrDefault(c => c.Id == line.Id);
                if (change == null)
                {
                    continue;
                }
                if (change.Quantity == 0)
                {
                    removed.Add(line);
                    continue;
                }
                line.Quantity = change.Quantity;
                line.LineAmount = Math.Round(change.Quantity * (line.UnitPrice ?? 0m), 2);
                line.Amount = line.LineAmount;
                _unitOfWork.OrderLineRepository.Update(line);
            }
            var remaining = lines.Except(removed).ToList();
            if (remaining.Count == 0)
            {
                return ConstantHelper.Message.OrderNeedsLine;
            }
            order.TotalQuantity = remaining.Sum(l => l.Quantity);
            order.TotalAmount = remaining.Sum(l => l.LineAmount);
            _unitOfWork.OrderRepository.Update(order);
            await _unitOfWork.OrderRepository.CommitAsync(ConstantHelper.TypeSave.Update);
            if (removed.Count > 0)
            {
                // CommitAsync(Delete) xóa mềm mọi bản ghi đang sửa → chỉ để các dòng bị bỏ ở trạng thái sửa.
                foreach (var line in removed)
                {
                    _unitOfWork.OrderLineRepository.Update(line);
                }
                await _unitOfWork.OrderLineRepository.CommitAsync(ConstantHelper.TypeSave.Delete);
            }
            return null;
        }

        /// <summary>Nội dung được duyệt của ORDER_DEVIATION: trạng thái + số suất hiện tại từng dòng (sửa sau khi gửi → không duyệt được).</summary>
        public async Task<string?> BuildDeviationStateAsync(Guid orderId)
        {
            var order = await _unitOfWork.OrderRepository.GetAll().AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted);
            if (order == null)
            {
                return null;
            }
            var lines = await _unitOfWork.OrderLineRepository.GetAll().AsNoTracking().Where(l => l.OrderId == orderId && !l.IsDeleted)
                .OrderBy(l => l.Id).Select(l => new { l.Id, l.Quantity }).ToListAsync();
            return JsonConvert.SerializeObject(new { order.Id, order.Status, Lines = lines.Select(l => new { l.Id, Quantity = l.Quantity.ToString("0.######", CultureInfo.InvariantCulture) }) });
        }

        /// <summary>
        /// Spec 6.6 lines: sửa số suất (Draft / Submitted / Confirmed, trước giờ chốt). Đơn Confirmed: lệch của dòng bất kỳ
        /// |mới − cũ| / cũ × 100 &gt; <c>BusinessRules:PortionDeviationPctNeedApproval</c> (30%) → không ghi, tạo yêu cầu duyệt ORDER_DEVIATION
        /// kèm đề xuất; ≤ ngưỡng → ghi ngay (PROPOSED – BA/PO CONFIRM). Số lượng 0 = bỏ dòng.
        /// </summary>
        public async Task<IResult<OrderDetailModel>> UpdateLines(OrderUpdateLinesRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var order = _unitOfWork.OrderRepository.GetSingle(o => o.Id == request.Id && !o.IsDeleted);
                string? error = order == null ? ConstantHelper.Message.RecordNotFoundMessage
                    : order.Status is not (OrderStatus.Draft or OrderStatus.Submitted or OrderStatus.Confirmed)
                        ? InvalidStatus(order, "sửa số suất", OrderStatus.Draft, OrderStatus.Submitted, OrderStatus.Confirmed)
                    : IsAfterCutoff(order.MealDate) ? AfterCutoff(order.MealDate, "sửa số suất")
                    : (request.ListLine ?? []).Count == 0 ? ConstantHelper.Message.OrderNeedsLine
                    : null;
                if (error == null && await PendingDeviationAsync(order!.Id) is { } pending)
                {
                    error = string.Format(ConstantHelper.Message.OrderApprovalPending, pending.RequestNo);
                }
                if (error != null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<OrderDetailModel>.Fail(error);
                }
                var changes = request.ListLine!.GroupBy(l => l.Id).Select(g => g.Last()).ToList();
                var current = await _unitOfWork.OrderLineRepository.GetAll().AsNoTracking().Where(l => l.OrderId == order!.Id && !l.IsDeleted).ToDictionaryAsync(l => l.Id, l => l.Quantity);
                var notInOrder = changes.FirstOrDefault(c => !current.ContainsKey(c.Id));
                error = notInOrder != null ? string.Format(ConstantHelper.Message.OrderLineNotInOrder, notInOrder.Id)
                    : changes.Any(c => c.Quantity < 0) ? ConstantHelper.Message.OrderQuantityNegative
                    : null;
                if (error != null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<OrderDetailModel>.Fail(error);
                }
                decimal maxDeviation = changes.Select(c => current[c.Id] == 0 ? (c.Quantity == 0 ? 0m : 100m) : Math.Abs(c.Quantity - current[c.Id]) / current[c.Id] * 100m)
                    .DefaultIfEmpty(0m).Max();
                maxDeviation = Math.Round(maxDeviation, 2);
                if (!string.IsNullOrWhiteSpace(request.Note))
                {
                    order!.Note = string.IsNullOrWhiteSpace(order.Note) ? request.Note.Trim() : $"{order.Note}\n{request.Note.Trim()}";
                }
                // BIZ-2.1 (D-77 – B1): trước giờ chốt sửa số KHÔNG đi duyệt nữa (bỏ ORDER_DEVIATION) – lệch so trung bình 4 tuần cùng thứ của điểm giao > ngưỡng
                // chỉ cảnh báo + ghi Note. Yêu cầu ORDER_DEVIATION đang chờ (tạo trước BIZ-2) vẫn xử lý như cũ.
                var projected = current.ToDictionary(kv => kv.Key, kv => kv.Value);
                foreach (var c in changes)
                {
                    projected[c.Id] = c.Quantity;
                }
                string? historyWarning = await HistoryDeviationWarningAsync(order!, projected.Values.Sum());
                if (historyWarning != null)
                {
                    order!.Note = string.IsNullOrWhiteSpace(order.Note) ? "[LECH_TB_4_TUAN] " + historyWarning : $"{order.Note}\n[LECH_TB_4_TUAN] {historyWarning}";
                }
                _unitOfWork.OrderRepository.Update(order);
                error = await ApplyLinesAsync(order.Id, changes);
                if (error != null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<OrderDetailModel>.Fail(error);
                }
                _unitOfWork.CommitTransaction();
                var detail = await DetailAsync(order.Id);
                detail!.MaxDeviationPercent = maxDeviation;
                if (historyWarning != null)
                {
                    detail.ListWarning.Add(historyWarning);
                }
                return Result<OrderDetailModel>.Success(detail, historyWarning == null ? ConstantHelper.Message.SaveSuccess : $"{ConstantHelper.Message.SaveSuccess} – {historyWarning}");
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " UpdateLines: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>Đổi trạng thái 1 đơn trong 1 transaction: trạng thái nguồn, giờ chốt (nếu cần), điều kiện riêng; ghi chú nối vào Note.</summary>
        private async Task<IResult<OrderDetailModel>> ChangeStatus(Guid id, string[] from, string to, string action, bool blockAfterCutoff,
            string? note, Func<Order, Task<string?>>? check)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var order = _unitOfWork.OrderRepository.GetSingle(o => o.Id == id && !o.IsDeleted);
                string? error = order == null ? ConstantHelper.Message.RecordNotFoundMessage
                    : !from.Contains(order.Status) ? InvalidStatus(order, action, from)
                    : blockAfterCutoff && IsAfterCutoff(order.MealDate) ? AfterCutoff(order.MealDate, action)
                    : check != null ? await check(order) : null;
                if (!string.IsNullOrEmpty(error))
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<OrderDetailModel>.Fail(error);
                }
                order!.Status = to;
                if (to == OrderStatus.Locked)
                {
                    await FixPricesAtLockAsync(order); // BIZ-1.5: chốt tay từng đơn cũng cố định giá
                }
                if (!string.IsNullOrWhiteSpace(note))
                {
                    order.Note = string.IsNullOrWhiteSpace(order.Note) ? note.Trim() : $"{order.Note}\n{note.Trim()}";
                }
                _unitOfWork.OrderRepository.Update(order);
                await _unitOfWork.OrderRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                return Result<OrderDetailModel>.Success(await DetailAsync(order.Id), ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ChangeStatus: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>
        /// BIZ-2.1 (D-77 – B1): Σ suất mới so trung bình suất thực nhận của <b>cùng điểm giao</b> trong 4 (ForecastLookbackWeeks) ngày cùng thứ trước ngày ăn
        /// (tuần không có đơn không tính – như dự báo gợi ý FC-1). Lệch &gt; <c>PortionDeviationPctNeedApproval</c> (30%) → câu cảnh báo; không chặn.
        /// </summary>
        private async Task<string?> HistoryDeviationWarningAsync(Order order, decimal newTotal)
        {
            if (order.DeliveryLocationId is not { } location)
            {
                return null;
            }
            int weeks = Math.Max(1, _rules.ForecastLookbackWeeks);
            var dates = Enumerable.Range(1, weeks).Select(i => order.MealDate.Date.AddDays(-7 * i)).ToList();
            var actual = await ForecastService.ActualAsync(_unitOfWork.DbContext, dates, [order.CustomerId]);
            var perDay = actual.Where(a => a.Key.LocationId == location).GroupBy(a => a.Key.Date).Select(g => g.Sum(x => x.Value)).ToList();
            if (perDay.Count == 0)
            {
                return null;
            }
            decimal avg = perDay.Average();
            if (avg <= 0)
            {
                return null;
            }
            decimal pct = Math.Round(Math.Abs(newTotal - avg) / avg * 100m, 1, MidpointRounding.AwayFromZero);
            return pct > _rules.PortionDeviationPctNeedApproval
                ? string.Format(ConstantHelper.Message.OrderHistoryDeviationWarning, newTotal.ToString("0.##", CultureInfo.InvariantCulture), pct.ToString("0.#", CultureInfo.InvariantCulture),
                    Math.Round(avg, 1).ToString("0.#", CultureInfo.InvariantCulture), _rules.PortionDeviationPctNeedApproval.ToString("0.##", CultureInfo.InvariantCulture))
                : null;
        }

        /// <summary>Spec 6.6 submit: Draft → Submitted, trước giờ chốt; ≥ 1 dòng, mọi dòng có MealType, ShiftId, Quantity &gt; 0.</summary>
        public Task<IResult<OrderDetailModel>> Submit(OrderActionRequest request)
            => ChangeStatus(request.Id, [OrderStatus.Draft], OrderStatus.Submitted, "gửi đơn", true, request.Note, async order =>
            {
                var lines = await _unitOfWork.OrderLineRepository.GetAll().AsNoTracking().Where(l => l.OrderId == order.Id && !l.IsDeleted).ToListAsync();
                return lines.Count == 0 ? ConstantHelper.Message.OrderNeedsLine
                    : lines.Any(l => string.IsNullOrEmpty(l.MealType) || l.ShiftId == null || l.Quantity <= 0) ? ConstantHelper.Message.OrderLineIncomplete
                    : null;
            });

        /// <summary>
        /// Spec 6.6 confirm (Catalog 96): Submitted → Confirmed, trước giờ chốt. BE2.2: lệch dự báo |đặt − dự báo| / dự báo × 100 &gt;
        /// <c>BusinessRules:ForecastVariancePctWarning</c> (10%) → cảnh báo (không chặn) + ghi nhận vào Note đơn dạng <c>[LECH_DU_BAO] …</c> (AuditChange tự ghi lần sửa đơn).
        /// Kết quả trả <c>forecastQuantity</c> / <c>forecastVariancePercent</c> / <c>forecastVarianceExceeded</c>.
        /// </summary>
        public async Task<IResult<OrderDetailModel>> Confirm(OrderActionRequest request)
        {
            string? warning = null, extraWarning = null;
            var current = await _unitOfWork.OrderRepository.GetAll().AsNoTracking().FirstOrDefaultAsync(o => o.Id == request.Id && !o.IsDeleted);
            if (current != null && current.Status is OrderStatus.Submitted or OrderStatus.Draft)
            {
                var lines = await _unitOfWork.OrderLineRepository.GetAll().AsNoTracking().Where(l => l.OrderId == current.Id && !l.IsDeleted).ToListAsync();
                string? historyWarning = await HistoryDeviationWarningAsync(current, lines.Where(l => l.Quantity > 0).Sum(l => l.Quantity));
                if (historyWarning != null)
                {
                    request.Note = string.IsNullOrWhiteSpace(request.Note) ? "[LECH_TB_4_TUAN] " + historyWarning : $"{request.Note.Trim()}\n[LECH_TB_4_TUAN] {historyWarning}";
                    extraWarning = historyWarning;
                }
                var forecast = await ForecastOfAsync(current, lines);
                if (forecast.Variance is { } v && Math.Abs(v) > _rules.ForecastVariancePctWarning)
                {
                    warning = string.Format(ConstantHelper.Message.ForecastVarianceWarning, v.ToString("+0.##;-0.##", CultureInfo.InvariantCulture),
                        forecast.Ordered.ToString("0.##", CultureInfo.InvariantCulture), forecast.Total!.Value.ToString("0.##", CultureInfo.InvariantCulture),
                        _rules.ForecastVariancePctWarning.ToString("0.##", CultureInfo.InvariantCulture));
                }
            }
            string? note = warning == null ? request.Note : string.IsNullOrWhiteSpace(request.Note) ? "[LECH_DU_BAO] " + warning : $"{request.Note.Trim()}\n[LECH_DU_BAO] {warning}";
            // BIZ-2.1 (D-77 – B2): đơn nhân viên nhập Nháp → Xác nhận thẳng (bỏ bước Gửi); Đã gửi chỉ còn cho đơn khách tự đăng ký.
            var result = await ChangeStatus(request.Id, [OrderStatus.Draft, OrderStatus.Submitted], OrderStatus.Confirmed, "xác nhận đơn", true, note, async order =>
            {
                if (order.Status != OrderStatus.Draft)
                {
                    return null;
                }
                var lines = await _unitOfWork.OrderLineRepository.GetAll().AsNoTracking().Where(l => l.OrderId == order.Id && !l.IsDeleted).ToListAsync();
                return lines.Count == 0 ? ConstantHelper.Message.OrderNeedsLine
                    : lines.Any(l => string.IsNullOrEmpty(l.MealType) || l.ShiftId == null || l.Quantity <= 0) ? ConstantHelper.Message.OrderLineIncomplete
                    : null;
            });
            if (result.ResponseCode == (int)HttpStatusCode.OK && result.Data != null)
            {
                foreach (var w in new[] { warning, extraWarning }.Where(w => w != null))
                {
                    result.ResponseMessage = $"{result.ResponseMessage} – {w}";
                    result.Data.ListWarning.Add(w!);
                }
            }
            return result;
        }

        /// <summary>
        /// BE2.2: dự báo của đơn = Σ dự báo từng dòng (<see cref="ForecastRules.ForLocation"/> theo điểm giao / loại suất / ca); lệch tính trên các dòng có dự báo
        /// (PROPOSED – D-51). Không có dự báo → Total / Variance null.
        /// </summary>
        private async Task<(decimal? Total, decimal Ordered, decimal? Variance, Dictionary<Guid, decimal?> ByLine)> ForecastOfAsync(Order order, IReadOnlyCollection<OrderLine> lines)
        {
            var rows = await ForecastRules.LatestPublishedAsync(_unitOfWork.DbContext, order.MealDate, order.MealDate, [order.CustomerId]);
            var byLine = new Dictionary<Guid, decimal?>();
            if (rows.Count == 0)
            {
                return (null, 0m, null, byLine);
            }
            var locationIds = await _unitOfWork.CustomerDeliveryLocationRepository.GetAll().AsNoTracking()
                .Where(l => l.CustomerId == order.CustomerId && !l.IsDeleted && (l.Status == null || l.Status == ActiveStatus))
                .OrderBy(l => l.Code).ThenBy(l => l.Name).Select(l => l.Id).ToListAsync();
            // ORD-OPT-1: bữa tách nhiều dòng lựa chọn → dự báo của (loại suất, ca) tính MỘT lần, so với Σ suất các lựa chọn; ghi ở dòng đầu nhóm.
            decimal total = 0m, ordered = 0m;
            bool any = false;
            foreach (var group in lines.GroupBy(l => (Meal: (l.MealType ?? string.Empty).Trim().ToUpperInvariant(), l.ShiftId)))
            {
                var first = group.OrderBy(l => l.MenuOptionNo ?? 0).ThenBy(l => l.CreatedDate).First();
                decimal? forecast = order.DeliveryLocationId is { } lid ? ForecastRules.ForLocation(rows, order.CustomerId, lid, locationIds, first.MealType, first.ShiftId) : null;
                foreach (var line in group)
                {
                    byLine[line.Id] = line.Id == first.Id ? forecast : null;
                }
                if (forecast != null)
                {
                    any = true;
                    total += forecast.Value;
                    ordered += group.Sum(l => l.Quantity);
                }
            }
            if (!any)
            {
                return (null, 0m, null, byLine);
            }
            return (total, ordered, ForecastRules.VariancePercent(ordered, total), byLine);
        }

        /// <summary>Spec 6.6 lock: Confirmed → Locked (bấm tay, được bấm bất kỳ lúc nào – thay job 20h cho từng đơn).</summary>
        public Task<IResult<OrderDetailModel>> Lock(OrderActionRequest request)
            => ChangeStatus(request.Id, [OrderStatus.Confirmed], OrderStatus.Locked, "chốt đơn", false, request.Note, null);

        /// <summary>
        /// Spec 6.6 cancel: Draft / Submitted / Confirmed, lý do bắt buộc (ghi Note "Hủy: …"). Đơn Confirmed sau giờ chốt không hủy được
        /// (bếp đã tính suất – PROPOSED); Draft / Submitted hủy được cả sau giờ chốt.
        /// </summary>
        public Task<IResult<OrderDetailModel>> Cancel(OrderCancelRequest request)
        {
            string? reason = request.Reason?.Trim();
            if (string.IsNullOrEmpty(reason) || reason.Length < 3)
            {
                return Task.FromResult<IResult<OrderDetailModel>>(Result<OrderDetailModel>.Fail(ConstantHelper.Message.OrderCancelReasonRequired));
            }
            return ChangeStatus(request.Id, [OrderStatus.Draft, OrderStatus.Submitted, OrderStatus.Confirmed], OrderStatus.Cancelled, "hủy đơn", false,
                "Hủy: " + reason, order => Task.FromResult(order.Status == OrderStatus.Confirmed && IsAfterCutoff(order.MealDate) ? AfterCutoff(order.MealDate, "hủy đơn đã xác nhận") : null));
        }

        /// <summary>
        /// BIZ-1.5 (D-77 e): CỐ ĐỊNH GIÁ lúc chốt – tra chính sách giá tại ngày ăn (PricePolicy/Resolve) cho từng dòng, ghi đơn giá + thành tiền vào dòng đơn,
        /// tính lại tổng đơn. Sau đó đối soát / giá vốn / bảng kê dùng giá đã ghi (không tra lại) – chính sách giá duyệt sau giờ chốt không đổi đơn đã chốt.
        /// Dòng không tra được giá (chưa có chính sách) giữ giá đang có. Đơn bù (Note có [DON_BU:) giữ nguyên giá.
        /// </summary>
        private async Task FixPricesAtLockAsync(Order order)
        {
            if (order.Note?.Contains("[DON_BU:", StringComparison.Ordinal) == true)
            {
                return;
            }
            DateTime day = order.MealDate.Date;
            var lines = _unitOfWork.OrderLineRepository.GetAll().Where(l => l.OrderId == order.Id && !l.IsDeleted).ToList();
            var cache = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in lines)
            {
                string key = (line.MealType ?? string.Empty).Trim();
                if (!cache.TryGetValue(key, out var unitPrice))
                {
                    var price = await _pricePolicyService.Resolve(new PriceResolveRequest { CustomerId = order.CustomerId, ContractId = order.ContractId, MealType = line.MealType, Date = day });
                    unitPrice = price.ResponseCode == (int)HttpStatusCode.OK && price.Data != null ? price.Data.UnitPrice : null;
                    cache[key] = unitPrice;
                }
                if (unitPrice is not { } p)
                {
                    continue;
                }
                decimal amount = Math.Round(line.Quantity * p, 2);
                if (line.UnitPrice == p && line.LineAmount == amount)
                {
                    continue;
                }
                line.UnitPrice = p;
                line.LineAmount = amount;
                line.Amount = amount;
                _unitOfWork.OrderLineRepository.Update(line);
            }
            order.TotalAmount = lines.Sum(l => l.LineAmount);
        }

        /// <summary>
        /// Nút "Chốt đơn" theo ngày ăn (thay job 20:00 trong Pilot): chỉ khi đã tới giờ chốt; mọi đơn Confirmed của ngày → Locked.
        /// Đơn Draft / Submitted không khóa – trả danh sách để điều phối xử lý tay.
        /// </summary>
        public async Task<IResult<OrderLockSummaryModel>> LockByMealDate(OrderLockByMealDateRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                DateTime day = request.MealDate.Date;
                var summary = new OrderLockSummaryModel { MealDate = day, CutoffAt = CutoffOf(day) };
                if (!IsAfterCutoff(day))
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<OrderLockSummaryModel>.Fail(string.Format(ConstantHelper.Message.OrderCutoffNotReached, summary.CutoffAt.ToString("HH:mm dd/MM/yyyy"), day.ToString("dd/MM/yyyy")), summary);
                }
                var orders = _unitOfWork.OrderRepository.GetAll().Where(o => !o.IsDeleted && o.MealDate == day
                    && (o.Status == OrderStatus.Draft || o.Status == OrderStatus.Submitted || o.Status == OrderStatus.Confirmed)).ToList();
                foreach (var order in orders.OrderBy(o => o.OrderNumber))
                {
                    string number = order.OrderNumber ?? order.OrderNo ?? order.Id.ToString();
                    if (order.Status != OrderStatus.Confirmed)
                    {
                        summary.ListNotConfirmedNumber.Add(number);
                        continue;
                    }
                    if (await PendingDeviationAsync(order.Id) != null)
                    {
                        summary.ListPendingApprovalNumber.Add(number);
                    }
                    order.Status = OrderStatus.Locked;
                    await FixPricesAtLockAsync(order);
                    _unitOfWork.OrderRepository.Update(order);
                    summary.ListLockedNumber.Add(number);
                }
                summary.LockedCount = summary.ListLockedNumber.Count;
                await _unitOfWork.OrderRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                return Result<OrderLockSummaryModel>.Success(summary, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " LockByMealDate: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>Thứ tự 8 cột của bảng đơn (STATUS_CATALOG Order.Status).</summary>
        private static readonly string[] BoardStatuses =
            [OrderStatus.Draft, OrderStatus.Submitted, OrderStatus.Confirmed, OrderStatus.Locked, OrderStatus.InProduction, OrderStatus.Delivered, OrderStatus.Reconciled, OrderStatus.Cancelled];

        /// <summary>
        /// BE2.1 (spec 6.6 GET orders view=board, Catalog 90): bảng đơn theo 8 trạng thái + đếm theo trạng thái. Lọc ngày ăn (1 ngày hoặc khoảng), trạng thái, khách,
        /// chi nhánh của khách (<c>Customer.BranchId</c>), tìm theo số đơn / tên khách. Không lọc ngày → 30 ngày gần nhất tới 7 ngày tới (tránh tải hết – PROPOSED).
        /// </summary>
        public async Task<IResult<OrderBoardModel>> GetBoard(OrderBoardRequest request)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var query = db.Order.AsNoTracking().Where(o => !o.IsDeleted);
                if (request.MealDate is { } day)
                {
                    DateTime d = day.Date, next = d.AddDays(1);
                    query = query.Where(o => o.MealDate >= d && o.MealDate < next);
                }
                else
                {
                    DateTime from = (request.MealDateFrom ?? _clock.Today.AddDays(-30)).Date;
                    DateTime to = (request.MealDateTo ?? _clock.Today.AddDays(7)).Date.AddDays(1);
                    query = query.Where(o => o.MealDate >= from && o.MealDate < to);
                }
                if (!string.IsNullOrWhiteSpace(request.Status))
                {
                    query = query.Where(o => o.Status == request.Status);
                }
                if (request.CustomerId is { } cid && cid != Guid.Empty)
                {
                    query = query.Where(o => o.CustomerId == cid);
                }
                if (request.BranchId is { } bid && bid != Guid.Empty)
                {
                    var customerIds = db.Customer.Where(c => c.BranchId == bid).Select(c => c.Id);
                    query = query.Where(o => customerIds.Contains(o.CustomerId));
                }
                var orders = await query.ToListAsync();
                var ids = orders.Select(o => o.CustomerId).Distinct().ToList();
                var customers = await db.Customer.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.DisplayName ?? c.Name);
                if (!string.IsNullOrWhiteSpace(request.SearchText))
                {
                    string q = request.SearchText.Trim();
                    orders = [.. orders.Where(o => (o.OrderNumber ?? o.OrderNo ?? string.Empty).Contains(q, StringComparison.OrdinalIgnoreCase)
                        || (customers.GetValueOrDefault(o.CustomerId) ?? string.Empty).Contains(q, StringComparison.OrdinalIgnoreCase))];
                }
                var locationIds = orders.Where(o => o.DeliveryLocationId != null).Select(o => o.DeliveryLocationId!.Value).Distinct().ToList();
                var locations = await db.CustomerDeliveryLocation.AsNoTracking().Where(l => locationIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.Name);
                var items = orders.Select(o => new OrderBoardItemModel
                {
                    Id = o.Id,
                    OrderNumber = o.OrderNumber ?? o.OrderNo,
                    CustomerId = o.CustomerId,
                    CustomerName = customers.GetValueOrDefault(o.CustomerId),
                    DeliveryLocationId = o.DeliveryLocationId,
                    LocationName = o.DeliveryLocationId is { } lid ? locations.GetValueOrDefault(lid) : null,
                    MealDate = o.MealDate,
                    DeliveryDate = o.DeliveryDate,
                    TotalQuantity = o.TotalQuantity,
                    TotalAmount = o.TotalAmount,
                    Status = o.Status,
                }).OrderBy(x => x.MealDate).ThenBy(x => x.OrderNumber, StringComparer.Ordinal).ToList();
                var columns = BoardStatuses.Concat(items.Select(i => i.Status ?? string.Empty).Where(s => !BoardStatuses.Contains(s)).Distinct())
                    .Select(status =>
                    {
                        var mine = items.Where(i => (i.Status ?? string.Empty) == status).ToList();
                        return new OrderBoardColumnModel
                        {
                            Status = status,
                            StatusName = OrderStatus.Label(status),
                            Count = mine.Count,
                            TotalQuantity = mine.Sum(i => i.TotalQuantity ?? 0m),
                            ListItem = mine,
                        };
                    }).ToList();
                return Result<OrderBoardModel>.Success(new OrderBoardModel
                {
                    ListColumn = columns,
                    StatusCounts = columns.ToDictionary(c => c.Status, c => c.Count),
                    TotalRecords = items.Count,
                }, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetBoard: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Chi tiết đơn (spec 6.6 GET {id}) – dòng kèm tên suất / ca / thực đơn, thao tác được phép, yêu cầu lệch suất đang chờ.</summary>
        public async Task<IResult<OrderDetailModel>> GetDetail(Guid id)
        {
            try
            {
                var detail = await DetailAsync(id);
                return detail == null
                    ? Result<OrderDetailModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage)
                    : Result<OrderDetailModel>.Success(detail, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetDetail: ", ex.ToString()));
                throw;
            }
        }

        private async Task<OrderDetailModel?> DetailAsync(Guid id)
        {
            var order = await _unitOfWork.OrderRepository.GetAll().AsNoTracking().FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted);
            if (order == null)
            {
                return null;
            }
            var lines = await _unitOfWork.OrderLineRepository.GetAll().AsNoTracking().Where(l => l.OrderId == id && !l.IsDeleted).ToListAsync();
            var mealIds = lines.Where(l => l.ContractMealId != null).Select(l => l.ContractMealId!.Value).Distinct().ToList();
            var shiftIds = lines.Where(l => l.ShiftId != null).Select(l => l.ShiftId!.Value).Distinct().ToList();
            var menuIds = lines.Where(l => l.MenuId != null).Select(l => l.MenuId!.Value).Distinct().ToList();
            var meals = await _unitOfWork.ContractMealRepository.GetAll().AsNoTracking().Where(m => mealIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.MealName);
            var shifts = await _unitOfWork.ShiftRepository.GetAll().AsNoTracking().Where(s => shiftIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name);
            var menus = await _unitOfWork.MenuRepository.GetAll().AsNoTracking().Where(m => menuIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Code);
            var customer = await _unitOfWork.CustomerRepository.GetAll().AsNoTracking().Where(c => c.Id == order.CustomerId).Select(c => c.Name).FirstOrDefaultAsync();
            var location = order.DeliveryLocationId is { } lid
                ? await _unitOfWork.CustomerDeliveryLocationRepository.GetAll().AsNoTracking().Where(l => l.Id == lid).Select(l => l.Name).FirstOrDefaultAsync()
                : null;
            var contract = order.ContractId is { } cid
                ? await _unitOfWork.ContractRepository.GetAll().AsNoTracking().Where(c => c.Id == cid).Select(c => c.ContractNumber ?? c.ContractNo).FirstOrDefaultAsync()
                : null;
            var pending = await PendingDeviationAsync(id);
            bool afterCutoff = IsAfterCutoff(order.MealDate);
            var actions = new List<string>();
            switch (order.Status)
            {
                case OrderStatus.Draft:
                    if (!afterCutoff) actions.AddRange(["updateLines", "confirm"]);
                    actions.Add("cancel");
                    break;
                case OrderStatus.Submitted:
                    if (!afterCutoff) actions.AddRange(["updateLines", "confirm"]);
                    actions.Add("cancel");
                    break;
                case OrderStatus.Confirmed:
                    if (!afterCutoff && pending == null) actions.Add("updateLines");
                    actions.Add("lock");
                    if (!afterCutoff) actions.Add("cancel");
                    break;
            }
            // BE2.2: sau giờ chốt, đơn Nháp / Đã gửi / Đã xác nhận / Đã chốt xin sửa qua duyệt ORDER_CHANGE_AFTER_CUTOFF.
            if (afterCutoff && pending == null && AfterCutoffChangeStatuses.Contains(order.Status))
            {
                actions.Add("requestChange");
            }
            var forecast = await ForecastOfAsync(order, lines);
            var optionMap = await MenuOptionRules.ForMenusAsync(_unitOfWork.DbContext, menuIds, order.MealDate);
            return new OrderDetailModel
            {
                Order = order, StatusName = OrderStatus.Label(order.Status), CustomerName = customer, LocationName = location, ContractNumber = contract,
                CutoffAt = CutoffOf(order.MealDate), IsAfterCutoff = afterCutoff, AllowedActions = actions,
                PendingApprovalId = pending?.Id, PendingApprovalNo = pending?.RequestNo, PendingApprovalType = pending?.BusinessType,
                ForecastQuantity = forecast.Total, ForecastVariancePercent = forecast.Variance,
                ForecastVarianceExceeded = forecast.Variance is { } v && Math.Abs(v) > _rules.ForecastVariancePctWarning,
                ListLine = lines.OrderBy(l => MenuService.MealRank(l.MealType)).ThenBy(l => l.MenuOptionNo ?? 0).ThenBy(l => l.CreatedDate).Select(l => new OrderLineDetailModel
                {
                    Id = l.Id, ContractMealId = l.ContractMealId, MealType = l.MealType, MealName = l.ContractMealId is { } m ? meals.GetValueOrDefault(m) : null,
                    ShiftId = l.ShiftId, ShiftName = l.ShiftId is { } s ? shifts.GetValueOrDefault(s) : null,
                    MenuId = l.MenuId, MenuCode = l.MenuId is { } mn ? menus.GetValueOrDefault(mn) : null,
                    Quantity = l.Quantity, UnitPrice = l.UnitPrice, LineAmount = l.LineAmount, Note = l.Note,
                    ForecastQuantity = forecast.ByLine.GetValueOrDefault(l.Id),
                    MenuOptionNo = l.MenuOptionNo,
                    OptionDishText = l.MenuOptionNo is { } no ? MenuOptionRules.Of(optionMap, l.MenuId, l.MealType).FirstOrDefault(o => o.OptionNo == no)?.DishText : null,
                }).ToList(),
            };
        }

        // ============================================================ BE2.2 – thêm / sửa đơn sau giờ chốt qua duyệt (ORDER_CHANGE_AFTER_CUTOFF, BUSINESS_RULES §2)
        /// <summary>Trạng thái đơn được xin sửa sau giờ chốt (Nháp / Đã gửi = xin nhận muộn; Đã xác nhận / Đã chốt = xin đổi số suất).</summary>
        private static readonly string[] AfterCutoffChangeStatuses = [OrderStatus.Draft, OrderStatus.Submitted, OrderStatus.Confirmed, OrderStatus.Locked];

        /// <summary>Đề xuất lưu kèm yêu cầu duyệt (PayloadJson.proposal).</summary>
        private sealed class AfterCutoffProposal
        {
            public const string Create = "CREATE";
            public const string Change = "CHANGE";
            public string Kind { get; set; } = Change;
            public string? Reason { get; set; }
            public List<OrderLineQuantityRequest> Lines { get; set; } = [];
            public List<OrderCreateLineRequest> NewLines { get; set; } = [];
        }

        /// <summary>Kiểm 1 dòng thêm mới: suất thuộc hợp đồng của đơn, ca (mặc định theo loại suất), không trùng (suất, ca) với dòng đang có, tra được giá.</summary>
        private async Task<(OrderLine? Line, string? Error)> BuildNewLineAsync(Order order, OrderCreateLineRequest input, IReadOnlyCollection<OrderLine> existing)
        {
            if (input.Quantity <= 0)
            {
                return (null, ConstantHelper.Message.OrderQuantityNegative);
            }
            var meal = await _unitOfWork.ContractMealRepository.GetAll().AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == input.ContractMealId && m.ContractId == order.ContractId && !m.IsDeleted);
            if (meal == null)
            {
                return (null, ConstantHelper.Message.OrderContractMealInvalid);
            }
            Guid? shiftId = input.ShiftId ?? (await DefaultShiftAsync(meal.MealType))?.Id;
            if (shiftId == null)
            {
                return (null, string.Format(ConstantHelper.Message.OrderShiftNotFound, meal.MealType));
            }
            var menu = order.DeliveryLocationId is { } lid ? await _menuAssignmentService.ResolveAsync(order.CustomerId, lid, meal.MealType, order.MealDate.Date) : null;
            var (optionNo, optionError) = await OptionOfLineAsync(input.MenuOptionNo, menu?.MenuId, meal.MealType, order.MealDate.Date);
            if (optionError != null)
            {
                return (null, optionError);
            }
            if (existing.Any(l => l.ContractMealId == meal.Id && l.ShiftId == shiftId && l.MenuOptionNo == optionNo))
            {
                return (null, ConstantHelper.Message.OrderLineDuplicate);
            }
            var price = await _pricePolicyService.Resolve(new PriceResolveRequest { CustomerId = order.CustomerId, ContractId = order.ContractId, MealType = meal.MealType, Date = order.MealDate.Date });
            if (price.ResponseCode != (int)HttpStatusCode.OK || price.Data == null)
            {
                return (null, $"{price.ResponseMessage} ({meal.MealType})");
            }
            decimal amount = Math.Round(input.Quantity * price.Data.UnitPrice, 2);
            return (new OrderLine
            {
                Id = Guid.NewGuid(), OrderId = order.Id, ContractMealId = meal.Id, MealType = meal.MealType, ShiftId = shiftId, MenuId = menu?.MenuId,
                MenuOptionNo = optionNo, Quantity = input.Quantity, UnitPrice = price.Data.UnitPrice, LineAmount = amount, Amount = amount, Note = input.Note,
            }, null);
        }

        /// <summary>
        /// BE2.2 (BUSINESS_RULES §2): xin sửa đơn sau giờ chốt – chỉ khi đã tới giờ chốt; đơn Nháp / Đã gửi / Đã xác nhận / Đã chốt; lý do bắt buộc; không có yêu cầu duyệt khác
        /// đang chờ. Không ghi gì vào đơn: tạo ApprovalRequest ORDER_CHANGE_AFTER_CUTOFF (quyền mục <c>#/wf-inbox?type=crm-order</c>) kèm đề xuất (số suất mới / dòng mới).
        /// Đơn Đã xác nhận / Đã chốt phải có ít nhất 1 thay đổi; đơn Nháp / Đã gửi được gửi không đổi số (= xin nhận đơn muộn). Duyệt → ghi số, đơn → Đã chốt (PROPOSED – D-51).
        /// </summary>
        public async Task<IResult<OrderDetailModel>> RequestChange(OrderChangeAfterCutoffRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var order = await _unitOfWork.OrderRepository.GetAll().AsNoTracking().FirstOrDefaultAsync(o => o.Id == request.Id && !o.IsDeleted);
                string? reason = request.Reason?.Trim();
                string? error = order == null ? ConstantHelper.Message.RecordNotFoundMessage
                    : !AfterCutoffChangeStatuses.Contains(order.Status) ? string.Format(ConstantHelper.Message.OrderChangeStatusInvalid, order.OrderNumber ?? order.OrderNo, OrderStatus.Label(order.Status))
                    : !IsAfterCutoff(order.MealDate) ? string.Format(ConstantHelper.Message.OrderChangeBeforeCutoff, CutoffOf(order.MealDate).ToString("HH:mm dd/MM/yyyy", CultureInfo.InvariantCulture))
                    : string.IsNullOrEmpty(reason) || reason.Length < 3 ? ConstantHelper.Message.OrderChangeReasonRequired
                    : null;
                if (error == null && await PendingDeviationAsync(order!.Id) is { } pending)
                {
                    error = string.Format(ConstantHelper.Message.OrderApprovalPending, pending.RequestNo);
                }
                if (error != null)
                {
                    return Fail(error);
                }
                var current = await _unitOfWork.OrderLineRepository.GetAll().AsNoTracking().Where(l => l.OrderId == order!.Id && !l.IsDeleted).ToListAsync();
                var changes = (request.ListLine ?? []).GroupBy(l => l.Id).Select(g => g.Last()).ToList();
                var notInOrder = changes.FirstOrDefault(c => current.All(l => l.Id != c.Id));
                if (notInOrder != null)
                {
                    return Fail(string.Format(ConstantHelper.Message.OrderLineNotInOrder, notInOrder.Id));
                }
                if (changes.Any(c => c.Quantity < 0))
                {
                    return Fail(ConstantHelper.Message.OrderQuantityNegative);
                }
                changes = [.. changes.Where(c => current.First(l => l.Id == c.Id).Quantity != c.Quantity)];
                var newLines = new List<OrderCreateLineRequest>();
                var probe = current.ToList();
                foreach (var input in request.ListNewLine ?? [])
                {
                    var (line, lineError) = await BuildNewLineAsync(order!, input, probe);
                    if (lineError != null)
                    {
                        return Fail(lineError);
                    }
                    probe.Add(line!);
                    newLines.Add(new OrderCreateLineRequest { ContractMealId = input.ContractMealId, ShiftId = line!.ShiftId, Quantity = input.Quantity, Note = input.Note });
                }
                bool mustChange = order!.Status is OrderStatus.Confirmed or OrderStatus.Locked;
                if (mustChange && changes.Count == 0 && newLines.Count == 0)
                {
                    return Fail(ConstantHelper.Message.OrderChangeNothing);
                }
                decimal remaining = current.Sum(l => changes.FirstOrDefault(c => c.Id == l.Id)?.Quantity ?? l.Quantity) + newLines.Sum(l => l.Quantity);
                if (remaining <= 0)
                {
                    return Fail(ConstantHelper.Message.OrderNeedsLine);
                }
                var proposal = new AfterCutoffProposal { Kind = AfterCutoffProposal.Change, Reason = reason, Lines = changes, NewLines = newLines };
                var (approval, approvalError) = await _approvalRequestService.CreateAsync(OrderStatus.ChangeAfterCutoffBusinessType, order.Id, JsonConvert.SerializeObject(proposal));
                if (approvalError != null)
                {
                    return Fail(approvalError);
                }
                _unitOfWork.CommitTransaction();
                var detail = (await DetailAsync(order.Id))!;
                detail.ApprovalRequested = true;
                return Result<OrderDetailModel>.Success(detail, string.Format(ConstantHelper.Message.OrderChangeRequested, approval!.RequestNo));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " RequestChange: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }

            IResult<OrderDetailModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                return Result<OrderDetailModel>.Fail(message);
            }
        }

        /// <summary>
        /// Nội dung được duyệt của ORDER_CHANGE_AFTER_CUTOFF: giai đoạn + số suất từng dòng. Đã xác nhận ↔ Đã chốt coi là cùng giai đoạn (nút chốt 20h chạy trong lúc
        /// chờ duyệt không làm hỏng yêu cầu); sửa số / hủy / vào sản xuất sau khi gửi → không duyệt được.
        /// </summary>
        public async Task<string?> BuildChangeAfterCutoffStateAsync(Guid orderId)
        {
            var order = await _unitOfWork.OrderRepository.GetAll().AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted);
            if (order == null)
            {
                return null;
            }
            var lines = await _unitOfWork.OrderLineRepository.GetAll().AsNoTracking().Where(l => l.OrderId == orderId && !l.IsDeleted)
                .OrderBy(l => l.Id).Select(l => new { l.Id, l.Quantity }).ToListAsync();
            string phase = order.Status is OrderStatus.Confirmed or OrderStatus.Locked ? "CONFIRMED_OR_LOCKED" : order.Status ?? string.Empty;
            return JsonConvert.SerializeObject(new { order.Id, Phase = phase, Lines = lines.Select(l => new { l.Id, Quantity = l.Quantity.ToString("0.######", CultureInfo.InvariantCulture) }) });
        }

        /// <summary>
        /// Nội bộ (duyệt ORDER_CHANGE_AFTER_CUTOFF, trong transaction của engine): thêm dòng mới (giá / thực đơn tra lại lúc duyệt), ghi số suất mới + tính lại tiền,
        /// đơn → Đã chốt; ghi chú "[SAU_GIO_CHOT] …" (kế hoạch sản xuất / MRP của ngày đã lập → nhắc tính bổ sung). Thêm đơn (CREATE) → Đã chốt.
        /// </summary>
        public async Task<string?> ApplyChangeAfterCutoffAsync(Guid orderId, string? proposalJson, string? note)
        {
            var order = _unitOfWork.OrderRepository.GetSingle(o => o.Id == orderId && !o.IsDeleted);
            if (order == null || !AfterCutoffChangeStatuses.Contains(order.Status))
            {
                return string.Format(ConstantHelper.Message.OrderChangeStatusInvalid, order?.OrderNumber ?? order?.OrderNo, OrderStatus.Label(order?.Status));
            }
            var proposal = string.IsNullOrEmpty(proposalJson) ? new AfterCutoffProposal() : JsonConvert.DeserializeObject<AfterCutoffProposal>(proposalJson) ?? new AfterCutoffProposal();
            if (proposal.NewLines.Count != 0)
            {
                var existing = _unitOfWork.OrderLineRepository.GetAll().Where(l => l.OrderId == orderId && !l.IsDeleted).ToList();
                foreach (var input in proposal.NewLines)
                {
                    var (line, error) = await BuildNewLineAsync(order, input, existing);
                    if (error != null)
                    {
                        return error;
                    }
                    _unitOfWork.OrderLineRepository.Add(line!);
                    existing.Add(line!);
                }
                await _unitOfWork.OrderLineRepository.CommitAsync(ConstantHelper.TypeSave.Update);
            }
            string? applyError = await ApplyLinesAsync(orderId, proposal.Lines);
            if (applyError != null)
            {
                return applyError;
            }
            order.Status = OrderStatus.Locked;
            await FixPricesAtLockAsync(order);
            string text = proposal.Kind == AfterCutoffProposal.Create ? "Duyệt thêm đơn sau giờ chốt" : "Duyệt sửa đơn sau giờ chốt";
            text = $"[SAU_GIO_CHOT] {text}: {proposal.Reason}" + (string.IsNullOrWhiteSpace(note) ? string.Empty : $" – {note.Trim()}");
            DateTime day = order.MealDate.Date, next = day.AddDays(1);
            var plan = await _unitOfWork.DbContext.ProductionPlan.AsNoTracking().Where(p => !p.IsDeleted && p.ProductionDate >= day && p.ProductionDate < next).FirstOrDefaultAsync();
            if (plan != null)
            {
                text += $" (kế hoạch {plan.PlanNumber ?? plan.PlanNo} đã lập – tính nhu cầu NL bổ sung: MaterialRequirement/CalculateTopUp)";
            }
            order.Note = string.IsNullOrWhiteSpace(order.Note) ? text : $"{order.Note}\n{text}";
            _unitOfWork.OrderRepository.Update(order);
            return null;
        }

        /// <summary>Nội bộ (từ chối / hủy ORDER_CHANGE_AFTER_CUTOFF): sửa đơn → không đổi số (ghi chú); thêm đơn (đơn Nháp tạo sau giờ chốt) → Hủy.</summary>
        public async Task<string?> RejectChangeAfterCutoffAsync(Guid orderId, string note)
        {
            var order = _unitOfWork.OrderRepository.GetSingle(o => o.Id == orderId && !o.IsDeleted);
            if (order == null)
            {
                return null;
            }
            var approval = await _unitOfWork.DbContext.ApprovalRequest.AsNoTracking()
                .Where(a => !a.IsDeleted && a.BusinessType == OrderStatus.ChangeAfterCutoffBusinessType && a.BusinessId == orderId && ApprovalRequestService.OpenStatuses.Contains(a.RequestStatus))
                .OrderByDescending(a => a.RequestedAt).FirstOrDefaultAsync();
            string? proposalJson = approval == null ? null : ApprovalRequestService.UnwrapPayload(approval.PayloadJson).Proposal;
            var proposal = string.IsNullOrEmpty(proposalJson) ? null : JsonConvert.DeserializeObject<AfterCutoffProposal>(proposalJson);
            bool created = proposal?.Kind == AfterCutoffProposal.Create && order.Status == OrderStatus.Draft;
            string text = created ? $"[SAU_GIO_CHOT] Từ chối thêm đơn sau giờ chốt: {note}" : $"[SAU_GIO_CHOT] Từ chối sửa đơn sau giờ chốt: {note}";
            if (created)
            {
                order.Status = OrderStatus.Cancelled;
            }
            order.Note = string.IsNullOrWhiteSpace(order.Note) ? text : $"{order.Note}\n{text}";
            _unitOfWork.OrderRepository.Update(order);
            return null;
        }
    }
}
