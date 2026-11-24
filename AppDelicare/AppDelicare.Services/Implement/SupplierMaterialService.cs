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

namespace AppDelicare.Services.Implement
{
    public partial class SupplierMaterialService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService, IServiceProvider services, Microsoft.Extensions.Options.IOptions<AppDelicare.Model.Option.BusinessRulesOptions> rules
        , AppDelicare.Data.Share.IClock clock, INotificationService notifications) : ISupplierMaterialService
    {
        private readonly IServiceProvider _services = services;
        private readonly AppDelicare.Model.Option.BusinessRulesOptions _rules = rules.Value;
        private readonly AppDelicare.Data.Share.IClock _clock = clock;
        private readonly INotificationService _notifications = notifications;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;

        public async Task<IResults<SupplierMaterialViewModel>> GetAll(SupplierMaterialSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@SupplierId", request.SupplierId == Guid.Empty ? null : request.SupplierId);
                dynamicParameters.Add("@IngredientId", request.IngredientId == Guid.Empty ? null : request.IngredientId);
                dynamicParameters.Add("@SupplierMaterialCode", request.SupplierMaterialCode);
                //dynamicParameters.Add("@UnitPrice", request.UnitPrice);
                //dynamicParameters.Add("@MinimumOrderQuantity", request.MinimumOrderQuantity);
                //dynamicParameters.Add("@LeadTimeDays", request.LeadTimeDays);
                //dynamicParameters.Add("@QualityRequirement", request.QualityRequirement);
                dynamicParameters.Add("@EffectiveFrom", request.EffectiveFrom);
                dynamicParameters.Add("@EffectiveTo", request.EffectiveTo);
                //dynamicParameters.Add("@IsPreferred", request.IsPreferred);
                dynamicParameters.Add("@Status", request.Status);
                dynamicParameters.Add("@EffectiveOn", request.EffectiveOn?.Date); // T45R
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<SupplierMaterialViewModel>("uspSupplierMaterial_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<SupplierMaterialViewModel>
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

        public IResults<SupplierMaterial> GetAllData()
        {
            try
            {
                var list = _unitOfWork.SupplierMaterialRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<SupplierMaterial>
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

        public async Task<IResult<bool>> Save(SupplierMaterialRequest request, string typeSave)
        {
            // BIZ-3.3: nguyên liệu bị ảnh hưởng (xóa → lấy từ dòng cũ) để tính lại giá thành sau khi lưu.
            Guid ingredientId = typeSave == ConstantHelper.TypeSave.Delete
                ? await _unitOfWork.DbContext.SupplierMaterial.AsNoTracking().Where(x => x.Id == request.Id).Select(x => x.IngredientId).FirstOrDefaultAsync()
                : request.IngredientId;
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
                string? costNote = await AfterPriceChangeAsync(ingredientId);
                return new Result<bool>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = (typeSave == ConstantHelper.TypeSave.Delete ? ConstantHelper.Message.DeleteSuccess : ConstantHelper.Message.SaveSuccess) + (_priceNote == null ? string.Empty : " – " + _priceNote) + (costNote == null ? string.Empty : " – " + costNote),
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

        private string Validate(SupplierMaterialRequest request)
        {
            if (_unitOfWork.SupplierRepository.GetSingle(x => x.Id == request.SupplierId && !x.IsDeleted) == null)
            {
                return "Nhà cung cấp không tồn tại.";
            }
            if (_unitOfWork.IngredientRepository.GetSingle(x => x.Id == request.IngredientId && !x.IsDeleted) == null)
            {
                return "Nguyên liệu không tồn tại.";
            }
            if (request.EffectiveFrom.HasValue && request.EffectiveTo.HasValue && request.EffectiveFrom.Value.Date > request.EffectiveTo.Value.Date)
            {
                return "Ngày hiệu lực đến phải sau ngày hiệu lực từ.";
            }
            if ((request.UnitPrice ?? 0) < 0 || (request.MinimumOrderQuantity ?? 0) < 0 || (request.LeadTimeDays ?? 0) < 0)
            {
                return "Đơn giá, số lượng đặt tối thiểu và số ngày giao không được âm.";
            }
            // UI-6.19 (D-100): trùng khoảng hiệu lực kiểm ở PriceRulesAsync (tự kết thúc giá cũ đang mở).
            return string.Empty;
        }

        // Moi nguyen lieu chi co 1 nha cung cap uu tien
        private void ClearOtherPreferred(SupplierMaterialRequest request)
        {
            if (!request.IsPreferred)
            {
                return;
            }
            var others = _unitOfWork.SupplierMaterialRepository
                .FindBy(x => x.IngredientId == request.IngredientId && x.Id != request.Id && x.IsPreferred && !x.IsDeleted).ToList();
            foreach (var item in others)
            {
                item.IsPreferred = false;
                _unitOfWork.SupplierMaterialRepository.Update(item);
            }
        }

        private async Task<string> Insert(SupplierMaterialRequest request, string typeSave)
        {
            // T45R: luật nghiệp vụ (spec) – trước khi ghi.
            var invalid = await CheckRules(request);
            if (!string.IsNullOrEmpty(invalid))
            {
                return invalid;
            }
            invalid = await PriceRulesAsync(request, null);
            if (!string.IsNullOrEmpty(invalid))
            {
                return invalid;
            }
            ClearOtherPreferred(request);
            var insert = _mapper.Map<SupplierMaterial>(request);
            if (insert.Id == Guid.Empty)
            {
                insert.Id = Guid.NewGuid();
            }
            insert.MinimumOrderQty = insert.MinimumOrderQuantity;
            _unitOfWork.SupplierMaterialRepository.Add(insert);
            await _unitOfWork.SupplierMaterialRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Update(SupplierMaterialRequest request, string typeSave)
        {
            var exist = _unitOfWork.SupplierMaterialRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // T45R: luật nghiệp vụ (spec) – trước khi ghi.
            var invalid = await CheckRules(request);
            if (!string.IsNullOrEmpty(invalid))
            {
                return invalid;
            }
            invalid = await PriceRulesAsync(request, exist);
            if (!string.IsNullOrEmpty(invalid))
            {
                return invalid;
            }
            ClearOtherPreferred(request);
            var update = _mapper.Map(request, exist);
            update.MinimumOrderQty = update.MinimumOrderQuantity;
            _unitOfWork.SupplierMaterialRepository.Update(update);
            await _unitOfWork.SupplierMaterialRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // T07: ReferenceGuard (quan hệ logic, truy vấn có tham số) thay SQL động uspSystem_ExecuteQuery (A10).
            if (await _auditChangeService.IsReferencedAsync(nameof(SupplierMaterial), (Guid)id))
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            var delete = _unitOfWork.SupplierMaterialRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            _unitOfWork.SupplierMaterialRepository.Update(delete);
            await _unitOfWork.SupplierMaterialRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<SupplierMaterial>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.SupplierMaterialRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<SupplierMaterial>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<SupplierMaterial>
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
        /// T45R (spec 4.3): kiểm tra luật nghiệp vụ, KHÔNG ghi DB. Việc bỏ ưu tiên các NCC khác (mỗi nguyên liệu chỉ 1 NCC ưu tiên)
        /// làm ở ClearOtherPreferred và được commit chung với lần lưu chính, để validate lỗi thì không đổi dữ liệu.
        /// </summary>
        private Task<string> CheckRules(SupplierMaterialRequest request)
        {
            return Task.FromResult(Validate(request));
        }
    }
}
