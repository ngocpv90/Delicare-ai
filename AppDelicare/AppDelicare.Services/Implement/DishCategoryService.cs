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

namespace AppDelicare.Services.Implement
{
    public partial class DishCategoryService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService, IDocumentNumberService documentNumberService) : IDishCategoryService
    {
        private readonly IDocumentNumberService _documentNumberService = documentNumberService;
        private string? _createdCode;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;

        public async Task<IResults<DishCategoryViewModel>> GetAll(DishCategorySearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@ParentId", request.ParentId);
                dynamicParameters.Add("@Code", request.Code);
                dynamicParameters.Add("@Name", request.Name);
                //dynamicParameters.Add("@Description", request.Description);
                dynamicParameters.Add("@Status", request.Status);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<DishCategoryViewModel>("uspDishCategory_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<DishCategoryViewModel>
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

        public IResults<DishCategory> GetAllData()
        {
            try
            {
                var list = _unitOfWork.DishCategoryRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<DishCategory>
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

        public async Task<IResult<bool>> Save(DishCategoryRequest request, string typeSave)
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
                        : _createdCode != null ? string.Format(ConstantHelper.Message.SaveSuccessWithCode, "nhóm món", _createdCode)
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

        private async Task<string> Insert(DishCategoryRequest request, string typeSave)
        {
            string invalid = await CheckGroupAsync(request, null);
            if (invalid.Length > 0)
            {
                return invalid;
            }
            var insert = _mapper.Map<DishCategory>(request);
            // Anh chốt 2026-11-22: mã nhóm món máy chủ tự sinh (DocumentKind.DishCategory), bỏ qua giá trị gửi lên; không đổi sau khi cấp.
            insert.Code = await _documentNumberService.NextCodeAsync(DocumentKind.DishCategory);
            _createdCode = insert.Code;
            _unitOfWork.DishCategoryRepository.Add(insert);
            await _unitOfWork.DishCategoryRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Update(DishCategoryRequest request, string typeSave)
        {
            var exist = _unitOfWork.DishCategoryRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // Mã do máy chủ cấp – giữ nguyên mã đang có (kể cả mã cũ người dùng đặt trước 2026-11-22).
            request.Code = exist.Code;
            string invalid = await CheckGroupAsync(request, exist);
            if (invalid.Length > 0)
            {
                return invalid;
            }
            var update = _mapper.Map(request, exist);
            _unitOfWork.DishCategoryRepository.Update(update);
            await _unitOfWork.DishCategoryRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // T07: ReferenceGuard (quan hệ logic, truy vấn có tham số) thay SQL động uspSystem_ExecuteQuery (A10).
            if (await _auditChangeService.IsReferencedAsync(nameof(DishCategory), (Guid)id))
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            var delete = _unitOfWork.DishCategoryRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            _unitOfWork.DishCategoryRepository.Update(delete);
            await _unitOfWork.DishCategoryRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<DishCategory>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.DishCategoryRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<DishCategory>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<DishCategory>
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
