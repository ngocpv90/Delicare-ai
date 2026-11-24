using AppDelicare.Data.Share;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
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
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;

namespace AppDelicare.Services.Implement
{
    public class PricePolicyService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService, IClock clock, IDocumentNumberService documentNumberService) : IPricePolicyService
    {
        private readonly IDocumentNumberService _documentNumberService = documentNumberService;
        /// <summary>MA-1.3 (D-63): mã vừa cấp khi thêm mới – báo kèm trong thông báo thành công.</summary>
        private string? _createdCode;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;

        public async Task<IResults<PricePolicyViewModel>> GetAll(PricePolicySearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@CustomerId", request.CustomerId);
                dynamicParameters.Add("@ContractId", request.ContractId);
                dynamicParameters.Add("@Code", request.Code);
                dynamicParameters.Add("@Name", request.Name);
                //dynamicParameters.Add("@UnitPrice", request.UnitPrice);
                dynamicParameters.Add("@CurrencyCode", request.CurrencyCode);
                dynamicParameters.Add("@EffectiveFrom", request.EffectiveFrom);
                dynamicParameters.Add("@EffectiveTo", request.EffectiveTo);
                dynamicParameters.Add("@Status", request.Status);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<PricePolicyViewModel>("uspPricePolicy_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<PricePolicyViewModel>
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

        public IResults<PricePolicy> GetAllData()
        {
            try
            {
                var list = _unitOfWork.PricePolicyRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<PricePolicy>
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

        public async Task<IResult<bool>> Save(PricePolicyRequest request, string typeSave)
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
                    ResponseMessage = typeSave == ConstantHelper.TypeSave.Delete ? ConstantHelper.Message.DeleteSuccess
                        : _createdCode != null ? string.Format(ConstantHelper.Message.SaveSuccessWithCode, "bảng giá", _createdCode)
                        : ConstantHelper.Message.SaveSuccess,
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

        private async Task<string> Insert(PricePolicyRequest request, string typeSave)
        {
            // T45R: luật nghiệp vụ (spec) – trước khi ghi.
            var invalid = await CheckRules(request);
            if (!string.IsNullOrEmpty(invalid))
            {
                return invalid;
            }
            var insert = _mapper.Map<PricePolicy>(request);
            // MA-1.3 (D-63): mã danh mục máy chủ tự sinh (…), bỏ qua giá trị gửi lên; không đổi sau khi cấp.
            insert.Code = await _documentNumberService.NextCodeAsync(DocumentKind.PricePolicy);
            _createdCode = insert.Code;
            _unitOfWork.PricePolicyRepository.Add(insert);
            await _unitOfWork.PricePolicyRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Update(PricePolicyRequest request, string typeSave)
        {
            // T45R: luật nghiệp vụ (spec) – trước khi ghi.
            var invalid = await CheckRules(request);
            if (!string.IsNullOrEmpty(invalid))
            {
                return invalid;
            }
            var exist = _unitOfWork.PricePolicyRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // MA-1.3 (D-63): mã không đổi sau khi cấp – bỏ qua giá trị gửi lên.
            request.Code = exist.Code;
            var update = _mapper.Map(request, exist);
            _unitOfWork.PricePolicyRepository.Update(update);
            await _unitOfWork.PricePolicyRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // T07: ReferenceGuard (quan hệ logic, truy vấn có tham số) thay SQL động uspSystem_ExecuteQuery (A10).
            if (await _auditChangeService.IsReferencedAsync(nameof(PricePolicy), (Guid)id))
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            var delete = _unitOfWork.PricePolicyRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            _unitOfWork.PricePolicyRepository.Update(delete);
            await _unitOfWork.PricePolicyRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<PricePolicy>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.PricePolicyRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<PricePolicy>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<PricePolicy>
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

        /// <summary>T45R (spec 5.4): đơn giá &gt; 0; chính sách đang áp dụng không chồng hiệu lực cùng khách / hợp đồng / loại suất.</summary>
        private async Task<string> CheckRules(PricePolicyRequest request)
        {
            if (request.UnitPrice <= 0)
            {
                return ConstantHelper.Message.PriceMustBePositive;
            }
            if (request.Status != null && request.Status != ActiveStatus)
            {
                return string.Empty;
            }
            DateTime from = request.EffectiveFrom;
            DateTime to = request.EffectiveTo ?? DateTime.MaxValue;
            bool overlap = await _unitOfWork.PricePolicyRepository.GetAll().AnyAsync(p => !p.IsDeleted && p.Id != request.Id
                && (p.Status == null || p.Status == ActiveStatus)
                && p.CustomerId == request.CustomerId && p.ContractId == request.ContractId && p.MealType == request.MealType
                && (p.EffectiveFrom == null || p.EffectiveFrom <= to) && (p.EffectiveTo == null || p.EffectiveTo >= from));
            return overlap ? ConstantHelper.Message.PriceOverlap : string.Empty;
        }

        private const string ActiveStatus = "Active";

        /// <summary>
        /// T45R (spec 5.4): tra đơn giá áp dụng – thứ tự: chính sách theo hợp đồng → theo khách → ContractMeal.
        /// Ưu tiên dòng đúng loại suất, rồi hiệu lực mới nhất. Không có → ResponseCode 400.
        /// </summary>
        public async Task<IResult<PriceResolveModel>> Resolve(PriceResolveRequest request)
        {
            try
            {
                DateTime day = (request.Date ?? clock.Today /* Rà soát BE: giờ nghiệp vụ VN */).Date;
                IQueryable<PricePolicy> Effective() => _unitOfWork.PricePolicyRepository.GetAll().AsNoTracking().Where(p => !p.IsDeleted
                    && (p.Status == null || p.Status == ActiveStatus)
                    && (p.EffectiveFrom == null || p.EffectiveFrom <= day) && (p.EffectiveTo == null || p.EffectiveTo >= day)
                    && (request.MealType == null || p.MealType == request.MealType || p.MealType == null) && p.UnitPrice > 0);
                if (request.ContractId is { } contractId)
                {
                    var byContract = await Effective().Where(p => p.ContractId == contractId)
                        .OrderByDescending(p => p.MealType != null).ThenByDescending(p => p.EffectiveFrom).FirstOrDefaultAsync();
                    if (byContract != null)
                    {
                        return Result<PriceResolveModel>.Success(new PriceResolveModel { UnitPrice = byContract.UnitPrice, Source = "PricePolicy:Contract", SourceId = byContract.Id }, ConstantHelper.Message.GetAllSuccessfully);
                    }
                }
                // UI-6.4 (D-83): bỏ cấp theo khách – chỉ còn đọc bản cũ chưa chuyển sang hợp đồng (khách 0 / > 1 hợp đồng hiệu lực – chờ anh quyết), có cảnh báo.
                var byCustomer = await Effective().Where(p => p.CustomerId == request.CustomerId && p.ContractId == null)
                    .OrderByDescending(p => p.MealType != null).ThenByDescending(p => p.EffectiveFrom).FirstOrDefaultAsync();
                if (byCustomer != null)
                {
                    Log.Warning("[PricePolicy] Tra giá theo khách cũ (chưa chuyển sang phụ lục hợp đồng – D-83): khách {CustomerId}, chính sách {Code}", request.CustomerId, byCustomer.Code);
                    return Result<PriceResolveModel>.Success(new PriceResolveModel { UnitPrice = byCustomer.UnitPrice, Source = "PricePolicy:Customer", SourceId = byCustomer.Id }, ConstantHelper.Message.GetAllSuccessfully);
                }
                if (request.ContractId is { } cid)
                {
                    var meal = await _unitOfWork.ContractMealRepository.GetAll().AsNoTracking()
                        .Where(m => !m.IsDeleted && m.ContractId == cid && (request.MealType == null || m.MealType == request.MealType) && m.UnitPrice > 0)
                        .FirstOrDefaultAsync();
                    if (meal != null)
                    {
                        return Result<PriceResolveModel>.Success(new PriceResolveModel { UnitPrice = meal.UnitPrice!.Value, Source = "ContractMeal", SourceId = meal.Id }, ConstantHelper.Message.GetAllSuccessfully);
                    }
                }
                return Result<PriceResolveModel>.Fail(ConstantHelper.Message.PriceNotFound);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Resolve: ", ex.ToString()));
                throw;
            }
        }
    }
}
