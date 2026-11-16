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
    public class OrderService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper, IClock clock
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
                dynamicParameters.Add("@CustomerId", request.CustomerId);
                dynamicParameters.Add("@ContractId", request.ContractId);
                dynamicParameters.Add("@DeliveryLocationId", request.DeliveryLocationId);
                dynamicParameters.Add("@OrderNumber", request.OrderNumber);
                dynamicParameters.Add("@OrderDate", request.OrderDate);
                dynamicParameters.Add("@DeliveryDate", request.DeliveryDate);
                dynamicParameters.Add("@Status", request.Status);
                dynamicParameters.Add("@TotalQuantity", request.TotalQuantity);
                dynamicParameters.Add("@TotalAmount", request.TotalAmount);
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
                .Where(c => !c.IsDeleted && c.CustomerId == customerId && c.Status == ActiveStatus
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

        private async Task<ApprovalRequest?> PendingDeviationAsync(Guid orderId)
            => await _unitOfWork.ApprovalRequestRepository.GetAll().AsNoTracking()
                .Where(a => !a.IsDeleted && a.BusinessType == OrderStatus.DeviationBusinessType && a.BusinessId == orderId && a.RequestStatus == "Pending")
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
                        item.ListLine.Add(new OrderTemplateLineModel
                        {
                            ContractMealId = meal.Id, MealType = meal.MealType, MealName = meal.MealName, ServiceType = meal.ServiceType,
                            ShiftId = shift?.Id, ShiftCode = shift?.Code, ShiftName = shift?.Name,
                            UnitPrice = price?.UnitPrice, PriceSource = price?.Source, MenuId = menu?.MenuId, MenuCode = menu?.MenuCode,
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
                string number = await _documentNumberService.NextAsync(DocumentKind.Voucher, _clock.Today);
                order!.OrderNumber = number;
                order.OrderNo = number;
                _unitOfWork.OrderRepository.Add(order);
                foreach (var line in lines!)
                {
                    _unitOfWork.OrderLineRepository.Add(line);
                }
                await _unitOfWork.OrderRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                _unitOfWork.CommitTransaction();
                var detail = await DetailAsync(order.Id);
                detail!.ListWarning.AddRange(warnings);
                return Result<OrderDetailModel>.Success(detail, ConstantHelper.Message.SaveSuccess);
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
            if (IsAfterCutoff(day))
            {
                return (null, null, AfterCutoff(day, "tạo đơn"));
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
                if (lines.Any(x => x.ContractMealId == meal.Id && x.ShiftId == shiftId))
                {
                    return (null, null, ConstantHelper.Message.OrderLineDuplicate);
                }
                var price = await _pricePolicyService.Resolve(new PriceResolveRequest { CustomerId = request.CustomerId, ContractId = contract.Id, MealType = meal.MealType, Date = day });
                if (price.ResponseCode != (int)HttpStatusCode.OK || price.Data == null)
                {
                    return (null, null, $"{price.ResponseMessage} ({meal.MealType})");
                }
                var menu = await _menuAssignmentService.ResolveAsync(request.CustomerId, request.DeliveryLocationId, meal.MealType, day);
                if (menu == null)
                {
                    warnings.Add($"{ConstantHelper.Message.MenuNotAssigned} ({meal.MealType})");
                }
                decimal amount = Math.Round(l.Quantity * price.Data.UnitPrice, 2);
                lines.Add(new OrderLine
                {
                    Id = Guid.NewGuid(), OrderId = order.Id, ContractMealId = meal.Id, MealType = meal.MealType, ShiftId = shiftId, MenuId = menu?.MenuId,
                    Quantity = l.Quantity, UnitPrice = price.Data.UnitPrice, LineAmount = amount, Amount = amount, Note = l.Note,
                });
            }
            order.TotalQuantity = lines.Sum(x => x.Quantity);
            order.TotalAmount = lines.Sum(x => x.LineAmount);
            return (order, lines, null);
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
                    error = string.Format(ConstantHelper.Message.OrderDeviationPending, pending.RequestNo);
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
                if (order!.Status == OrderStatus.Confirmed && maxDeviation > _rules.PortionDeviationPctNeedApproval)
                {
                    if (changes.Count(c => c.Quantity > 0) + current.Keys.Count(k => changes.All(c => c.Id != k)) == 0)
                    {
                        _unitOfWork.RollbackTransaction();
                        return Result<OrderDetailModel>.Fail(ConstantHelper.Message.OrderNeedsLine);
                    }
                    var (approval, approvalError) = await _approvalRequestService.CreateAsync(OrderStatus.DeviationBusinessType, order.Id,
                        JsonConvert.SerializeObject(changes.Select(c => new { c.Id, c.Quantity })));
                    if (approvalError != null)
                    {
                        _unitOfWork.RollbackTransaction();
                        return Result<OrderDetailModel>.Fail(approvalError);
                    }
                    _unitOfWork.OrderRepository.Update(order);
                    await _unitOfWork.OrderRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                    _unitOfWork.CommitTransaction();
                    var pendingDetail = await DetailAsync(order.Id);
                    pendingDetail!.ApprovalRequested = true;
                    pendingDetail.MaxDeviationPercent = maxDeviation;
                    return Result<OrderDetailModel>.Success(pendingDetail,
                        string.Format(ConstantHelper.Message.OrderDeviationRequested, maxDeviation, _rules.PortionDeviationPctNeedApproval, approval!.RequestNo));
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
                return Result<OrderDetailModel>.Success(detail, ConstantHelper.Message.SaveSuccess);
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

        /// <summary>Spec 6.6 submit: Draft → Submitted, trước giờ chốt; ≥ 1 dòng, mọi dòng có MealType, ShiftId, Quantity &gt; 0.</summary>
        public Task<IResult<OrderDetailModel>> Submit(OrderActionRequest request)
            => ChangeStatus(request.Id, [OrderStatus.Draft], OrderStatus.Submitted, "gửi đơn", true, request.Note, async order =>
            {
                var lines = await _unitOfWork.OrderLineRepository.GetAll().AsNoTracking().Where(l => l.OrderId == order.Id && !l.IsDeleted).ToListAsync();
                return lines.Count == 0 ? ConstantHelper.Message.OrderNeedsLine
                    : lines.Any(l => string.IsNullOrEmpty(l.MealType) || l.ShiftId == null || l.Quantity <= 0) ? ConstantHelper.Message.OrderLineIncomplete
                    : null;
            });

        /// <summary>Spec 6.6 confirm: Submitted → Confirmed, trước giờ chốt. Lệch dự báo &gt; 10%: DEFERRED(PILOT) (chưa dùng Forecast).</summary>
        public Task<IResult<OrderDetailModel>> Confirm(OrderActionRequest request)
            => ChangeStatus(request.Id, [OrderStatus.Submitted], OrderStatus.Confirmed, "xác nhận đơn", true, request.Note, null);

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
                    if (!afterCutoff) actions.AddRange(["updateLines", "submit"]);
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
            return new OrderDetailModel
            {
                Order = order, StatusName = OrderStatus.Label(order.Status), CustomerName = customer, LocationName = location, ContractNumber = contract,
                CutoffAt = CutoffOf(order.MealDate), IsAfterCutoff = afterCutoff, AllowedActions = actions,
                PendingApprovalId = pending?.Id, PendingApprovalNo = pending?.RequestNo,
                ListLine = lines.OrderBy(l => l.MealType).ThenBy(l => l.CreatedDate).Select(l => new OrderLineDetailModel
                {
                    Id = l.Id, ContractMealId = l.ContractMealId, MealType = l.MealType, MealName = l.ContractMealId is { } m ? meals.GetValueOrDefault(m) : null,
                    ShiftId = l.ShiftId, ShiftName = l.ShiftId is { } s ? shifts.GetValueOrDefault(s) : null,
                    MenuId = l.MenuId, MenuCode = l.MenuId is { } mn ? menus.GetValueOrDefault(mn) : null,
                    Quantity = l.Quantity, UnitPrice = l.UnitPrice, LineAmount = l.LineAmount, Note = l.Note,
                }).ToList(),
            };
        }
    }
}
