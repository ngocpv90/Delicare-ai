using AutoMapper;
using Dapper;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Data.Share;
using Microsoft.EntityFrameworkCore;
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
    public class ContractService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper, IClock clock
        , IDocumentNumberService documentNumberService, IApprovalRequestService approvalRequestService
        , IAuditChangeService auditChangeService) : IContractService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;
        private readonly IClock _clock = clock;
        private readonly IDocumentNumberService _documentNumberService = documentNumberService;
        private readonly IApprovalRequestService _approvalRequestService = approvalRequestService;

        public async Task<IResults<ContractViewModel>> GetAll(ContractSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@CustomerId", request.CustomerId == Guid.Empty ? null : request.CustomerId);
                dynamicParameters.Add("@ContractNumber", request.ContractNumber);
                dynamicParameters.Add("@Name", request.Name);
                dynamicParameters.Add("@SignedDate", request.SignedDate);
                dynamicParameters.Add("@EffectiveFrom", request.EffectiveFrom);
                dynamicParameters.Add("@EffectiveTo", request.EffectiveTo);
                //dynamicParameters.Add("@ContractValue", request.ContractValue);
                dynamicParameters.Add("@Status", request.Status);
                //dynamicParameters.Add("@Note", request.Note);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<ContractViewModel>("uspContract_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<ContractViewModel>
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

        public IResults<Contract> GetAllData()
        {
            try
            {
                var list = _unitOfWork.ContractRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<Contract>
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

        public async Task<IResult<bool>> Save(ContractRequest request, string typeSave)
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

        /// <summary>
        /// BE2.2: CRUD Insert tạo hợp đồng <b>Nháp</b> (Status gửi lên bị bỏ qua – chỉ đổi qua Submit → duyệt → Terminate); ContractNumber trống → tự sinh HD-YYMMDD-NNN.
        /// Tạo kèm loại suất trong 1 lần: <c>Contract/Create</c> (JSON).
        /// </summary>
        private async Task<string> Insert(ContractRequest request, string typeSave)
        {
            string? error = await HeaderErrorAsync(request.CustomerId, request.EffectiveFrom, request.EffectiveTo, request.ContractNumber, null);
            if (error != null)
            {
                return error;
            }
            var insert = _mapper.Map<Contract>(request);
            insert.Status = ContractStatus.Draft;
            await AssignNumberAsync(insert, request.ContractNumber);
            _unitOfWork.ContractRepository.Add(insert);
            await _unitOfWork.ContractRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        /// <summary>BE2.2: CRUD Update chỉ cho hợp đồng Nháp; giữ trạng thái và số (MA-1 – số không đổi sau khi cấp).</summary>
        private async Task<string> Update(ContractRequest request, string typeSave)
        {
            var exist = _unitOfWork.ContractRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            if (exist.Status != ContractStatus.Draft)
            {
                return string.Format(ConstantHelper.Message.ContractEditOnlyDraft, ContractStatus.Label(exist.Status));
            }
            string? number = exist.ContractNumber ?? exist.ContractNo; // MA-1 (D-63): số không đổi sau khi cấp
            string? error = await HeaderErrorAsync(request.CustomerId, request.EffectiveFrom, request.EffectiveTo, number, exist.Id);
            if (error != null)
            {
                return error;
            }
            string? status = exist.Status;
            var update = _mapper.Map(request, exist);
            update.Status = status;
            update.ContractNumber = number;
            update.ContractNo = number;
            _unitOfWork.ContractRepository.Update(update);
            await _unitOfWork.ContractRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        /// <summary>BE2.2: chỉ xóa (mềm) hợp đồng Nháp, kèm các loại suất của chính nó.</summary>
        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            var delete = _unitOfWork.ContractRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            if (delete.Status != ContractStatus.Draft)
            {
                return string.Format(ConstantHelper.Message.ContractEditOnlyDraft, ContractStatus.Label(delete.Status));
            }
            // T07: ReferenceGuard (quan hệ logic, truy vấn có tham số) thay SQL động uspSystem_ExecuteQuery (A10); loại suất của chính hợp đồng xóa cùng.
            if (await _auditChangeService.IsReferencedAsync(nameof(Contract), (Guid)id, nameof(ContractMeal)))
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            foreach (var meal in _unitOfWork.ContractMealRepository.GetAll().Where(m => m.ContractId == delete.Id && !m.IsDeleted).ToList())
            {
                _unitOfWork.ContractMealRepository.Update(meal);
            }
            _unitOfWork.ContractRepository.Update(delete);
            await _unitOfWork.ContractRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<Contract>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.ContractRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<Contract>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<Contract>
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

        // ============================================================ BE2.2 – hợp đồng (spec 6.1, Catalog 78–80)
        /// <summary>Đơn hàng còn "mở" (chưa giao / chưa hủy) – chặn chấm dứt hợp đồng nếu ngày ăn sau ngày chấm dứt.</summary>
        private static readonly string[] OpenOrderStatuses = [OrderStatus.Draft, OrderStatus.Submitted, OrderStatus.Confirmed, OrderStatus.Locked, OrderStatus.InProduction];

        private static string InvalidStatus(Contract contract, string action, params string[] expected)
            => string.Format(ConstantHelper.Message.StatusActionInvalid, action, "hợp đồng", string.Join(" / ", expected.Select(ContractStatus.Label)), ContractStatus.Label(contract.Status));

        private static string AppendNote(string? note, string? add)
            => string.IsNullOrWhiteSpace(add) ? note ?? string.Empty : string.IsNullOrWhiteSpace(note) ? add.Trim() : $"{note}\n{add.Trim()}";

        /// <summary>Khách tồn tại, hiệu lực từ ≤ đến, số hợp đồng (nếu có) không trùng hợp đồng khác.</summary>
        private async Task<string?> HeaderErrorAsync(Guid customerId, DateTime effectiveFrom, DateTime? effectiveTo, string? number, Guid? selfId)
        {
            if (!await _unitOfWork.DbContext.Customer.AsNoTracking().AnyAsync(c => c.Id == customerId && !c.IsDeleted))
            {
                return ConstantHelper.Message.ContractCustomerNotFound;
            }
            if (effectiveTo != null && effectiveTo.Value.Date < effectiveFrom.Date)
            {
                return ConstantHelper.Message.ContractDateRangeInvalid;
            }
            if (!string.IsNullOrWhiteSpace(number))
            {
                string n = number.Trim();
                if (await _unitOfWork.DbContext.Contract.AsNoTracking().AnyAsync(c => !c.IsDeleted && c.Id != selfId && (c.ContractNumber == n || c.ContractNo == n)))
                {
                    return string.Format(ConstantHelper.Message.ContractNumberDuplicate, n);
                }
            }
            return null;
        }

        /// <summary>
        /// Số hợp đồng HD-YYMMDD-NNN do máy chủ sinh (MA-1, D-63 c: số gửi lên bị bỏ qua – số hợp đồng giấy của khách ghi ở Ghi chú / Tên);
        /// ghi song song ContractNumber / ContractNo (cột cũ).
        /// </summary>
        private async Task AssignNumberAsync(Contract contract, string? requested)
        {
            _ = requested;
            string number = await _documentNumberService.NextAsync(DocumentKind.Contract, _clock.Today);
            contract.ContractNumber = number;
            contract.ContractNo = number;
        }

        /// <summary>Kiểm loại suất: có ít nhất 1, mỗi MealType 1 lần (không phân biệt hoa thường), đơn giá (nếu có) &gt; 0.</summary>
        private static string? MealsError(IReadOnlyCollection<(string? MealType, decimal? UnitPrice)> meals)
        {
            if (meals.Count == 0)
            {
                return ConstantHelper.Message.ContractNeedsMeal;
            }
            if (meals.Any(m => string.IsNullOrWhiteSpace(m.MealType)))
            {
                return ConstantHelper.Message.ContractMealTypeRequired;
            }
            var duplicate = meals.GroupBy(m => m.MealType!.Trim().ToUpperInvariant()).FirstOrDefault(g => g.Count() > 1);
            if (duplicate != null)
            {
                return string.Format(ConstantHelper.Message.ContractMealTypeDuplicate, duplicate.Key);
            }
            var badPrice = meals.FirstOrDefault(m => m.UnitPrice is <= 0);
            return badPrice.MealType != null ? string.Format(ConstantHelper.Message.ContractMealPriceInvalid, badPrice.MealType) : null;
        }

        private Task<ApprovalRequest?> PendingApprovalAsync(Guid contractId)
            => _unitOfWork.DbContext.ApprovalRequest.AsNoTracking()
                .Where(a => !a.IsDeleted && a.BusinessType == ContractStatus.BusinessType && a.BusinessId == contractId && ApprovalRequestService.OpenStatuses.Contains(a.RequestStatus))
                .OrderByDescending(a => a.RequestedAt).FirstOrDefaultAsync();

        /// <summary>
        /// Spec 6.1 POST contracts (Catalog 78) – 1 transaction: hợp đồng Nháp (số tự sinh HD-YYMMDD-NNN nếu trống) + các ContractMeal (Status Active).
        /// Kiểm: khách tồn tại, EffectiveFrom ≤ EffectiveTo, số không trùng, ≥ 1 loại suất, mỗi MealType 1 lần, đơn giá &gt; 0 nếu có.
        /// </summary>
        public async Task<IResult<ContractDetailModel>> Create(ContractCreateRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var meals = request.ListMeal ?? [];
                string? error = await HeaderErrorAsync(request.CustomerId, request.EffectiveFrom, request.EffectiveTo, request.ContractNumber, null)
                    ?? MealsError([.. meals.Select(m => (m.MealType, m.UnitPrice))]);
                if (error != null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<ContractDetailModel>.Fail(error);
                }
                var contract = new Contract
                {
                    Id = Guid.NewGuid(), CustomerId = request.CustomerId, Name = request.Name?.Trim(), SignedDate = request.SignedDate,
                    EffectiveFrom = request.EffectiveFrom.Date, EffectiveTo = request.EffectiveTo?.Date, StartDate = request.EffectiveFrom.Date, EndDate = request.EffectiveTo?.Date,
                    PaymentTerm = request.PaymentTerm, ContractValue = request.ContractValue, Note = request.Note, Status = ContractStatus.Draft,
                };
                await AssignNumberAsync(contract, request.ContractNumber);
                _unitOfWork.ContractRepository.Add(contract);
                foreach (var m in meals)
                {
                    _unitOfWork.ContractMealRepository.Add(new ContractMeal
                    {
                        Id = Guid.NewGuid(), ContractId = contract.Id, MealType = m.MealType!.Trim(), MealName = m.MealName?.Trim(), ServiceType = m.ServiceType?.Trim(),
                        UnitPrice = m.UnitPrice, UnitId = m.UnitId, Status = ContractStatus.Active,
                    });
                }
                await _unitOfWork.ContractRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                _unitOfWork.CommitTransaction();
                return Result<ContractDetailModel>.Success((await DetailAsync(contract.Id))!, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Create: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>
        /// Spec 6.1 submit (Catalog 79): Nháp → Chờ duyệt + ApprovalRequest CONTRACT qua engine T12 (1 cấp, quyền = mục <c>#/wf-inbox?type=crm-contract</c>).
        /// Điều kiện: ≥ 1 loại suất hợp lệ, hiệu lực từ ≤ đến. BE2.3 chuyển sang nhiều cấp (engine ApprovalStep) – handler không phải đổi.
        /// </summary>
        public async Task<IResult<ContractDetailModel>> Submit(ContractActionRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var contract = _unitOfWork.ContractRepository.GetSingle(c => c.Id == request.Id && !c.IsDeleted);
                var meals = contract == null ? [] : await _unitOfWork.DbContext.ContractMeal.AsNoTracking().Where(m => m.ContractId == contract.Id && !m.IsDeleted).ToListAsync();
                string? error = contract == null ? ConstantHelper.Message.ContractNotFound
                    : contract.Status != ContractStatus.Draft ? InvalidStatus(contract, "gửi duyệt", ContractStatus.Draft)
                    : contract.EffectiveTo != null && contract.EffectiveTo.Value.Date < contract.EffectiveFrom.Date ? ConstantHelper.Message.ContractDateRangeInvalid
                    : MealsError([.. meals.Select(m => (m.MealType, m.UnitPrice))]);
                if (error != null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<ContractDetailModel>.Fail(error);
                }
                // Lưu Chờ duyệt TRƯỚC khi tạo yêu cầu: nội dung được duyệt (hash) đọc từ DB (như PurchaseOrder/Submit).
                contract!.Status = ContractStatus.PendingApproval;
                if (!string.IsNullOrWhiteSpace(request.Note))
                {
                    contract.Note = AppendNote(contract.Note, request.Note);
                }
                _unitOfWork.ContractRepository.Update(contract);
                await _unitOfWork.ContractRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                var (approval, approvalError) = await _approvalRequestService.CreateAsync(ContractStatus.BusinessType, contract.Id);
                if (approvalError != null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<ContractDetailModel>.Fail(approvalError);
                }
                _unitOfWork.CommitTransaction();
                return Result<ContractDetailModel>.Success((await DetailAsync(contract.Id))!, string.Format(ConstantHelper.Message.ContractSubmitted, approval!.RequestNo));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Submit: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>Duyệt / từ chối hợp đồng qua engine T12 (<c>ApprovalRequestService.Decide</c>: quyền mục "Phê duyệt › Hợp đồng suất ăn", hash nội dung, ApprovalDecision).</summary>
        public async Task<IResult<bool>> Decide(ContractActionRequest request, bool approve)
        {
            try
            {
                var pending = await PendingApprovalAsync(request.Id);
                if (pending == null)
                {
                    return Result<bool>.Fail(ConstantHelper.Message.ContractNotPending, false);
                }
                return await _approvalRequestService.Decide(new ApprovalDecideRequest { Id = pending.Id, Approve = approve, Note = request.Note });
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Decide: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>
        /// Spec 6.1 terminate (Catalog 80): chỉ Hiệu lực, lý do bắt buộc; EffectiveTo mới (trống = hôm nay) trong [EffectiveFrom, EffectiveTo cũ];
        /// chặn nếu còn đơn chưa giao (Nháp … Đang SX) của hợp đồng có ngày ăn sau ngày chấm dứt. → Chấm dứt; đơn tới hết ngày chấm dứt vẫn tạo được (PROPOSED – D-51).
        /// </summary>
        public async Task<IResult<ContractDetailModel>> Terminate(ContractTerminateRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var contract = _unitOfWork.ContractRepository.GetSingle(c => c.Id == request.Id && !c.IsDeleted);
                string? reason = request.Reason?.Trim();
                DateTime end = (request.EffectiveTo ?? _clock.Today).Date;
                string? error = contract == null ? ConstantHelper.Message.ContractNotFound
                    : contract.Status != ContractStatus.Active ? InvalidStatus(contract, "chấm dứt", ContractStatus.Active)
                    : string.IsNullOrEmpty(reason) || reason.Length < 3 ? ConstantHelper.Message.ContractTerminateReasonRequired
                    : end < contract.EffectiveFrom.Date || (contract.EffectiveTo != null && end > contract.EffectiveTo.Value.Date)
                        ? string.Format(ConstantHelper.Message.ContractTerminateDateInvalid, contract.EffectiveFrom.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture))
                    : null;
                if (error == null)
                {
                    DateTime after = end.AddDays(1);
                    var open = await _unitOfWork.DbContext.Order.AsNoTracking()
                        .Where(o => !o.IsDeleted && o.ContractId == contract!.Id && o.MealDate >= after && OpenOrderStatuses.Contains(o.Status!))
                        .OrderBy(o => o.MealDate).Select(o => o.OrderNumber ?? o.OrderNo).ToListAsync();
                    if (open.Count != 0)
                    {
                        error = string.Format(ConstantHelper.Message.ContractHasOpenOrders, open.Count, end.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), string.Join(", ", open.Take(5)));
                    }
                }
                if (error != null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<ContractDetailModel>.Fail(error);
                }
                contract!.Status = ContractStatus.Terminated;
                contract.EffectiveTo = end;
                contract.EndDate = end;
                contract.Note = AppendNote(contract.Note, $"Chấm dứt từ {end.AddDays(1):dd/MM/yyyy}: {reason}");
                _unitOfWork.ContractRepository.Update(contract);
                await _unitOfWork.ContractRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                return Result<ContractDetailModel>.Success((await DetailAsync(contract.Id))!, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Terminate: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>
        /// T23 (Catalog 81, spec 6.1 ContractExpiryJob – PROPOSED D-59): hợp đồng <b>Hiệu lực</b> có <c>EffectiveTo</c> &lt; hôm nay → <b>Hết hạn</b>,
        /// ghi chú "Hết hạn ngày dd/MM/yyyy". Chạy lại không đổi gì thêm (chỉ chọn hợp đồng còn Hiệu lực). Không đụng đơn hàng
        /// (đơn sau ngày hết hiệu lực đã bị chặn lúc tạo / sửa theo hiệu lực hợp đồng).
        /// </summary>
        public async Task<IResult<ContractExpireResultModel>> ExpireOverdue(ContractExpireRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                DateTime today = _clock.Today;
                var contracts = await _unitOfWork.DbContext.Contract
                    .Where(c => !c.IsDeleted && c.Status == ContractStatus.Active && c.EffectiveTo != null && c.EffectiveTo < today)
                    .OrderBy(c => c.EffectiveTo).ThenBy(c => c.ContractNumber)
                    .ToListAsync();
                var result = new ContractExpireResultModel
                {
                    Preview = request.Preview,
                    Today = today,
                    ExpiredCount = contracts.Count,
                    ListContractNumber = contracts.Select(c => c.ContractNumber ?? c.ContractNo ?? c.Id.ToString()).ToList(),
                };
                if (request.Preview || contracts.Count == 0)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<ContractExpireResultModel>.Success(result, ConstantHelper.Message.GetAllSuccessfully);
                }
                foreach (var contract in contracts)
                {
                    contract.Status = ContractStatus.Expired;
                    contract.Note = AppendNote(contract.Note, $"Hết hạn ngày {contract.EffectiveTo!.Value:dd/MM/yyyy}");
                    _unitOfWork.ContractRepository.Update(contract);
                }
                await _unitOfWork.ContractRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                return Result<ContractExpireResultModel>.Success(result, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ExpireOverdue: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>Chi tiết hợp đồng (spec 6.1) – loại suất, thao tác được phép, yêu cầu duyệt đang chờ, số đơn chưa giao.</summary>
        public async Task<IResult<ContractDetailModel>> GetDetail(Guid id)
        {
            try
            {
                var detail = await DetailAsync(id);
                return detail == null ? Result<ContractDetailModel>.Fail(ConstantHelper.Message.ContractNotFound)
                    : Result<ContractDetailModel>.Success(detail, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetDetail: ", ex.ToString()));
                throw;
            }
        }

        private async Task<ContractDetailModel?> DetailAsync(Guid id)
        {
            var db = _unitOfWork.DbContext;
            var contract = await db.Contract.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
            if (contract == null)
            {
                return null;
            }
            var pending = await PendingApprovalAsync(id);
            var actions = contract.Status switch
            {
                ContractStatus.Draft => new List<string> { "update", "submit", "delete" },
                ContractStatus.PendingApproval => ["approve", "reject"],
                ContractStatus.Active => ["terminate"],
                _ => [],
            };
            var detail = new ContractDetailModel
            {
                Contract = contract,
                StatusName = ContractStatus.Label(contract.Status),
                CustomerName = await db.Customer.AsNoTracking().Where(c => c.Id == contract.CustomerId).Select(c => c.DisplayName ?? c.Name).FirstOrDefaultAsync(),
                ListMeal = await db.ContractMeal.AsNoTracking().Where(m => m.ContractId == id && !m.IsDeleted).OrderBy(m => m.MealType).ToListAsync(),
                AllowedActions = actions,
                PendingApprovalId = pending?.Id,
                PendingApprovalNo = pending?.RequestNo,
                OpenOrderCount = await db.Order.AsNoTracking().CountAsync(o => !o.IsDeleted && o.ContractId == id && OpenOrderStatuses.Contains(o.Status!)),
            };
            if (contract.Status == ContractStatus.Active && contract.EffectiveTo != null && contract.EffectiveTo.Value.Date < _clock.Today)
            {
                detail.ListWarning.Add($"Hợp đồng đã quá ngày hiệu lực đến {contract.EffectiveTo:dd/MM/yyyy} – không tạo đơn mới được (job chuyển Hết hạn để sau – D-45)");
            }
            return detail;
        }

        /// <summary>Nội dung được duyệt của CONTRACT: trạng thái, khách, số, hiệu lực, giá trị, điều khoản, từng loại suất. Sửa sau khi gửi → không duyệt được.</summary>
        public async Task<string?> BuildApprovalStateAsync(Guid id)
        {
            var db = _unitOfWork.DbContext;
            var c = await db.Contract.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (c == null)
            {
                return null;
            }
            static string? D(decimal? v) => v?.ToString("0.######", CultureInfo.InvariantCulture);
            var meals = await db.ContractMeal.AsNoTracking().Where(m => m.ContractId == id && !m.IsDeleted).OrderBy(m => m.Id).ToListAsync();
            return JsonConvert.SerializeObject(new
            {
                c.Id, c.Status, c.CustomerId, Number = c.ContractNumber ?? c.ContractNo, From = c.EffectiveFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                To = c.EffectiveTo?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Value = D(c.ContractValue), c.PaymentTerm,
                Meals = meals.Select(m => new { m.Id, m.MealType, m.MealName, m.ServiceType, UnitPrice = D(m.UnitPrice), m.UnitId }),
            });
        }

        /// <summary>Tiêu đề hộp duyệt: số hợp đồng – khách – hiệu lực.</summary>
        public async Task<string?> DescribeAsync(Guid id)
        {
            var db = _unitOfWork.DbContext;
            var row = await (from c in db.Contract.AsNoTracking()
                             join k in db.Customer.AsNoTracking() on c.CustomerId equals k.Id
                             where c.Id == id
                             select new { Number = c.ContractNumber ?? c.ContractNo, k.Name, c.EffectiveFrom, c.EffectiveTo }).FirstOrDefaultAsync();
            return row == null ? null
                : $"Hợp đồng {row.Number} – {row.Name} – hiệu lực {row.EffectiveFrom:dd/MM/yyyy}{(row.EffectiveTo != null ? $" → {row.EffectiveTo:dd/MM/yyyy}" : string.Empty)}";
        }

        /// <summary>Engine (handler CONTRACT): duyệt → Hiệu lực (hợp đồng đã quá ngày hiệu lực đến → không duyệt được); loại suất trống trạng thái → Active.</summary>
        public Task<string?> ApproveFromEngineAsync(Guid id, string? note)
        {
            var contract = _unitOfWork.ContractRepository.GetSingle(c => c.Id == id && !c.IsDeleted);
            if (contract == null || contract.Status != ContractStatus.PendingApproval)
            {
                return Task.FromResult<string?>(contract == null ? ConstantHelper.Message.ContractNotFound : InvalidStatus(contract, "duyệt", ContractStatus.PendingApproval));
            }
            if (contract.EffectiveTo != null && contract.EffectiveTo.Value.Date < _clock.Today)
            {
                return Task.FromResult<string?>(string.Format(ConstantHelper.Message.ContractExpiredOnApprove, contract.EffectiveTo.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)));
            }
            contract.Status = ContractStatus.Active;
            if (!string.IsNullOrWhiteSpace(note))
            {
                contract.Note = AppendNote(contract.Note, "Duyệt: " + note);
            }
            _unitOfWork.ContractRepository.Update(contract);
            foreach (var meal in _unitOfWork.ContractMealRepository.GetAll().Where(m => m.ContractId == id && !m.IsDeleted && m.Status == null).ToList())
            {
                meal.Status = ContractStatus.Active;
                _unitOfWork.ContractMealRepository.Update(meal);
            }
            return Task.FromResult<string?>(null);
        }

        /// <summary>Engine (handler CONTRACT): từ chối / người gửi hủy → về Nháp kèm lý do.</summary>
        public Task<string?> RejectFromEngineAsync(Guid id, string note)
        {
            var contract = _unitOfWork.ContractRepository.GetSingle(c => c.Id == id && !c.IsDeleted);
            if (contract == null || contract.Status != ContractStatus.PendingApproval)
            {
                return Task.FromResult<string?>(contract == null ? ConstantHelper.Message.ContractNotFound : InvalidStatus(contract, "từ chối", ContractStatus.PendingApproval));
            }
            contract.Status = ContractStatus.Draft;
            contract.Note = AppendNote(contract.Note, "Từ chối: " + note);
            _unitOfWork.ContractRepository.Update(contract);
            return Task.FromResult<string?>(null);
        }
    }
}
