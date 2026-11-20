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
    public class IngredientService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService, IDocumentNumberService documentNumberService) : IIngredientService
    {
        private readonly IDocumentNumberService _documentNumberService = documentNumberService;
        /// <summary>MA-1.3 (D-63): mã vừa cấp khi thêm mới – báo kèm trong thông báo thành công.</summary>
        private string? _createdCode;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;

        public async Task<IResults<IngredientViewModel>> GetAll(IngredientSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<IngredientViewModel>("uspIngredient_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<IngredientViewModel>
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

        public IResults<Ingredient> GetAllData()
        {
            try
            {
                var list = _unitOfWork.IngredientRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<Ingredient>
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

        public async Task<IResult<bool>> Save(IngredientRequest request, string typeSave)
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
                        : _createdCode != null ? string.Format(ConstantHelper.Message.SaveSuccessWithCode, "nguyên liệu", _createdCode)
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

        private async Task<string> Insert(IngredientRequest request, string typeSave)
        {
            // T45R: luật nghiệp vụ (spec) – trước khi ghi.
            var invalid = await CheckRules(request);
            if (!string.IsNullOrEmpty(invalid))
            {
                return invalid;
            }
            var insert = _mapper.Map<Ingredient>(request);
            // MA-1.3 (D-63): mã danh mục máy chủ tự sinh (…), bỏ qua giá trị gửi lên; không đổi sau khi cấp.
            insert.Code = await _documentNumberService.NextCodeAsync(DocumentKind.Ingredient);
            _createdCode = insert.Code;
            _unitOfWork.IngredientRepository.Add(insert);
            await _unitOfWork.IngredientRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Update(IngredientRequest request, string typeSave)
        {
            // T45R: luật nghiệp vụ (spec) – trước khi ghi.
            var invalid = await CheckRules(request);
            if (!string.IsNullOrEmpty(invalid))
            {
                return invalid;
            }
            var exist = _unitOfWork.IngredientRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // MA-1.3 (D-63): mã không đổi sau khi cấp – bỏ qua giá trị gửi lên.
            request.Code = exist.Code;
            var update = _mapper.Map(request, exist);
            _unitOfWork.IngredientRepository.Update(update);
            await _unitOfWork.IngredientRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // T07: ReferenceGuard (quan hệ logic, truy vấn có tham số) thay SQL động uspSystem_ExecuteQuery (A10).
            if (await _auditChangeService.IsReferencedAsync(nameof(Ingredient), (Guid)id))
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            var delete = _unitOfWork.IngredientRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            _unitOfWork.IngredientRepository.Update(delete);
            await _unitOfWork.IngredientRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<Ingredient>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.IngredientRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<Ingredient>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<Ingredient>
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

        /// <summary>T45R (spec 4.4): YieldPercent ∈ (0, 100]; StandardWastePercent ∈ [0, 100); ShelfLifeDays &gt; 0 khi quản lý hạn.</summary>
        private Task<string> CheckRules(IngredientRequest request)
        {
            if (request.YieldPercent is { } y && (y <= 0 || y > 100))
            {
                return Task.FromResult(ConstantHelper.Message.YieldPercentRange);
            }
            if (request.StandardWastePercent is { } w && (w < 0 || w >= 100))
            {
                return Task.FromResult(ConstantHelper.Message.StandardWasteRange);
            }
            if (request.IsExpiryManaged && !(request.ShelfLifeDays > 0))
            {
                return Task.FromResult(ConstantHelper.Message.ShelfLifeRequired);
            }
            return Task.FromResult(string.Empty);
        }

        /// <summary>T45R (spec 4.4): tồn khả dụng của nguyên liệu (tổng các kho) và cờ dưới mức tối thiểu.</summary>
        public async Task<IResult<IngredientStockModel>> GetStock(Guid id)
        {
            try
            {
                var ingredient = await _unitOfWork.IngredientRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (ingredient == null)
                {
                    return Result<IngredientStockModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                var balances = await _unitOfWork.InventoryBalanceRepository.GetAll().AsNoTracking()
                    .Where(b => b.IngredientId == id && !b.IsDeleted)
                    .GroupBy(b => b.WarehouseId).Select(g => new IngredientStockByWarehouse { WarehouseId = g.Key, AvailableQuantity = g.Sum(b => b.AvailableQuantity) })
                    .ToListAsync();
                decimal available = balances.Sum(b => b.AvailableQuantity);
                return Result<IngredientStockModel>.Success(new IngredientStockModel
                {
                    IngredientId = id,
                    AvailableQuantity = available,
                    MinimumStock = ingredient.MinimumStock,
                    BelowMinimum = ingredient.MinimumStock is { } min && available < min,
                    ListWarehouse = balances,
                }, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetStock: ", ex.ToString()));
                throw;
            }
        }
    }
}
