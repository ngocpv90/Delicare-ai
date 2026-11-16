using AutoMapper;
using Dapper;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using Microsoft.EntityFrameworkCore;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using AppDelicare.Services.Interface;
using Serilog;
using System.Net;

namespace AppDelicare.Services.Implement
{
    public class MenuLineService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService) : IMenuLineService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;

        public async Task<IResults<MenuLineViewModel>> GetAll(MenuLineSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@MenuId", request.MenuId);
                dynamicParameters.Add("@DishId", request.DishId);
                dynamicParameters.Add("@MealType", request.MealType);
                //dynamicParameters.Add("@DayOfWeek", request.DayOfWeek);
                //dynamicParameters.Add("@SortOrder", request.SortOrder);
                //dynamicParameters.Add("@StandardQuantity", request.StandardQuantity);
                //dynamicParameters.Add("@Note", request.Note);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<MenuLineViewModel>("uspMenuLine_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<MenuLineViewModel>
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

        public IResults<MenuLine> GetAllData()
        {
            try
            {
                var list = _unitOfWork.MenuLineRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<MenuLine>
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

        public async Task<IResult<bool>> Save(MenuLineRequest request, string typeSave)
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
                    ResponseMessage = (typeSave == ConstantHelper.TypeSave.Delete ? ConstantHelper.Message.DeleteSuccess : ConstantHelper.Message.SaveSuccess)
                        // T13: cảnh báo không chặn (món chưa có công thức đang khóa).
                        + (string.IsNullOrEmpty(_warning) ? string.Empty : ". " + _warning),
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

        // ============================================================ T13 – dòng thực đơn tuần (spec 6.2)
        private string? _warning;

        /// <summary>Thực đơn chỉ sửa món khi ở Draft / Approved (spec 6.2). Trả lỗi hoặc rỗng.</summary>
        private async Task<string> CheckMenuEditable(Guid menuId, string action)
        {
            var menu = await _unitOfWork.MenuRepository.GetAll().AsNoTracking().FirstOrDefaultAsync(m => m.Id == menuId && !m.IsDeleted);
            if (menu == null)
            {
                return string.Format(ConstantHelper.Message.NotExist, "Thực đơn").Trim();
            }
            return menu.Status is null or MenuStatus.Draft or MenuStatus.Approved
                ? string.Empty
                : string.Format(ConstantHelper.Message.StatusActionInvalid, action, "thực đơn", "Nháp / Đã duyệt", MenuStatus.Label(menu.Status));
        }

        /// <summary>Spec 6.2: thứ 1–7, lựa chọn 1–9 (SequenceNo – GAP-MENU-01), có loại suất, món tồn tại; món chưa có công thức đang khóa → chỉ cảnh báo.</summary>
        private async Task<string> CheckRules(MenuLineRequest request, string action)
        {
            string error = await CheckMenuEditable(request.MenuId, action);
            if (!string.IsNullOrEmpty(error))
            {
                return error;
            }
            if (request.DayOfWeek is not (>= 1 and <= 7))
            {
                return ConstantHelper.Message.MenuLineDayOfWeekRange;
            }
            if (request.SequenceNo is { } option && option is < 1 or > 9)
            {
                return ConstantHelper.Message.MenuLineOptionRange;
            }
            if (string.IsNullOrWhiteSpace(request.MealType))
            {
                return ConstantHelper.Message.MenuLineMealTypeRequired;
            }
            var dish = await _unitOfWork.DishRepository.GetAll().AsNoTracking().FirstOrDefaultAsync(d => d.Id == request.DishId && !d.IsDeleted);
            if (dish == null)
            {
                return string.Format(ConstantHelper.Message.NotExist, "Món").Trim();
            }
            bool hasLockedRecipe = await _unitOfWork.RecipeRepository.GetAll()
                .AnyAsync(r => r.DishId == dish.Id && !r.IsDeleted && r.IsActive && r.Status == RecipeStatus.Locked);
            _warning = hasLockedRecipe ? null : string.Format(ConstantHelper.Message.MenuDishNoLockedRecipe, dish.Name);
            return string.Empty;
        }

        private async Task<string> Insert(MenuLineRequest request, string typeSave)
        {
            string invalid = await CheckRules(request, "thêm món");
            if (!string.IsNullOrEmpty(invalid))
            {
                return invalid;
            }
            request.SequenceNo ??= 1;
            var insert = _mapper.Map<MenuLine>(request);
            _unitOfWork.MenuLineRepository.Add(insert);
            await _unitOfWork.MenuLineRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Update(MenuLineRequest request, string typeSave)
        {
            var exist = _unitOfWork.MenuLineRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // T13: không chuyển dòng sang thực đơn khác; kiểm tra như thêm mới.
            request.MenuId = exist.MenuId;
            string invalid = await CheckRules(request, "sửa món");
            if (!string.IsNullOrEmpty(invalid))
            {
                return invalid;
            }
            request.SequenceNo ??= exist.SequenceNo ?? 1;
            var update = _mapper.Map(request, exist);
            _unitOfWork.MenuLineRepository.Update(update);
            await _unitOfWork.MenuLineRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // T07: ReferenceGuard (quan hệ logic, truy vấn có tham số) thay SQL động uspSystem_ExecuteQuery (A10).
            if (await _auditChangeService.IsReferencedAsync(nameof(MenuLine), (Guid)id))
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            var delete = _unitOfWork.MenuLineRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // T13: chỉ bỏ món khi thực đơn Draft / Approved.
            string notEditable = await CheckMenuEditable(delete.MenuId, "bỏ món");
            if (!string.IsNullOrEmpty(notEditable))
            {
                return notEditable;
            }
            _unitOfWork.MenuLineRepository.Update(delete);
            await _unitOfWork.MenuLineRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<MenuLine>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.MenuLineRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<MenuLine>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<MenuLine>
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
