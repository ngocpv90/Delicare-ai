using AutoMapper;
using AppDelicare.Data.Connection;
using Dapper;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;
using Microsoft.EntityFrameworkCore;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using AppDelicare.Services.Interface;
using Serilog;
using System.Net;

namespace AppDelicare.Services.Implement
{
    public class MenuAssignmentService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService) : IMenuAssignmentService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;

        public async Task<IResults<MenuAssignmentViewModel>> GetAll(MenuAssignmentSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@CustomerId", request.CustomerId == Guid.Empty ? null : request.CustomerId);
                dynamicParameters.Add("@ContractId", request.ContractId);
                dynamicParameters.Add("@MenuId", request.MenuId == Guid.Empty ? null : request.MenuId);
                dynamicParameters.Add("@DeliveryLocationId", request.DeliveryLocationId);
                dynamicParameters.Add("@EffectiveFrom", request.EffectiveFrom);
                dynamicParameters.Add("@EffectiveTo", request.EffectiveTo);
                dynamicParameters.Add("@Status", request.Status);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<MenuAssignmentViewModel>("uspMenuAssignment_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<MenuAssignmentViewModel>
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

        public IResults<MenuAssignment> GetAllData()
        {
            try
            {
                var list = _unitOfWork.MenuAssignmentRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<MenuAssignment>
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

        public async Task<IResult<bool>> Save(MenuAssignmentRequest request, string typeSave)
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

        private async Task<string> Insert(MenuAssignmentRequest request, string typeSave)
        {
            // T13 (spec 6.3): kiểm tra trước khi ghi.
            var invalid = await CheckRules(request);
            if (!string.IsNullOrEmpty(invalid))
            {
                return invalid;
            }
            var insert = _mapper.Map<MenuAssignment>(request);
            insert.Status ??= ActiveStatus;
            _unitOfWork.MenuAssignmentRepository.Add(insert);
            await _unitOfWork.MenuAssignmentRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Update(MenuAssignmentRequest request, string typeSave)
        {
            var exist = _unitOfWork.MenuAssignmentRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // T13 (spec 6.3): kiểm tra trước khi ghi.
            var invalid = await CheckRules(request);
            if (!string.IsNullOrEmpty(invalid))
            {
                return invalid;
            }
            var update = _mapper.Map(request, exist);
            _unitOfWork.MenuAssignmentRepository.Update(update);
            await _unitOfWork.MenuAssignmentRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // T07: ReferenceGuard (quan hệ logic, truy vấn có tham số) thay SQL động uspSystem_ExecuteQuery (A10).
            if (await _auditChangeService.IsReferencedAsync(nameof(MenuAssignment), (Guid)id))
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            var delete = _unitOfWork.MenuAssignmentRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            _unitOfWork.MenuAssignmentRepository.Update(delete);
            await _unitOfWork.MenuAssignmentRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<MenuAssignment>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.MenuAssignmentRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<MenuAssignment>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<MenuAssignment>
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

        // ============================================================ T13 – phân công thực đơn (spec 6.3)
        private const string ActiveStatus = "Active";

        /// <summary>
        /// Spec 6.3: hiệu lực từ ≤ đến; khách / thực đơn tồn tại; hợp đồng (nếu có) thuộc đúng khách và có suất <c>MealType</c>;
        /// điểm giao (nếu có) thuộc khách; không chồng hiệu lực với phân công Active khác cùng (khách, điểm giao, loại suất).
        /// </summary>
        private async Task<string> CheckRules(MenuAssignmentRequest request)
        {
            if (request.EffectiveTo != null && request.EffectiveTo.Value.Date < request.EffectiveFrom.Date)
            {
                return ConstantHelper.Message.EffectiveRangeInvalid;
            }
            if (!await _unitOfWork.CustomerRepository.GetAll().AnyAsync(c => c.Id == request.CustomerId && !c.IsDeleted))
            {
                return string.Format(ConstantHelper.Message.NotExist, "Khách hàng").Trim();
            }
            if (!await _unitOfWork.MenuRepository.GetAll().AnyAsync(m => m.Id == request.MenuId && !m.IsDeleted))
            {
                return string.Format(ConstantHelper.Message.NotExist, "Thực đơn").Trim();
            }
            if (request.ContractId is { } contractId)
            {
                var contract = await _unitOfWork.ContractRepository.GetAll().AsNoTracking().FirstOrDefaultAsync(c => c.Id == contractId && !c.IsDeleted);
                if (contract == null || contract.CustomerId != request.CustomerId)
                {
                    return ConstantHelper.Message.MenuAssignmentContractMismatch;
                }
                if (!string.IsNullOrEmpty(request.MealType)
                    && !await _unitOfWork.ContractMealRepository.GetAll().AnyAsync(m => m.ContractId == contractId && !m.IsDeleted && m.MealType == request.MealType))
                {
                    return string.Format(ConstantHelper.Message.MenuAssignmentMealTypeNotInContract, request.MealType);
                }
            }
            if (request.DeliveryLocationId is { } locationId
                && !await _unitOfWork.CustomerDeliveryLocationRepository.GetAll().AnyAsync(l => l.Id == locationId && l.CustomerId == request.CustomerId && !l.IsDeleted))
            {
                return ConstantHelper.Message.MenuAssignmentLocationMismatch;
            }
            if (request.Status != null && request.Status != ActiveStatus)
            {
                return string.Empty;
            }
            DateTime from = request.EffectiveFrom.Date;
            DateTime to = request.EffectiveTo?.Date ?? new DateTime(9999, 12, 31);
            bool overlap = await _unitOfWork.MenuAssignmentRepository.GetAll().AnyAsync(a => !a.IsDeleted && a.Id != request.Id
                && (a.Status == null || a.Status == ActiveStatus)
                && a.CustomerId == request.CustomerId && a.DeliveryLocationId == request.DeliveryLocationId && a.MealType == request.MealType
                && (a.EffectiveFrom == null || a.EffectiveFrom <= to) && (a.EffectiveTo == null || a.EffectiveTo >= from));
            return overlap ? ConstantHelper.Message.MenuAssignmentOverlap : string.Empty;
        }

        public async Task<IResult<MenuResolveModel>> Resolve(MenuResolveRequest request)
        {
            try
            {
                var found = await ResolveAsync(request.CustomerId, request.DeliveryLocationId, request.MealType, request.Date);
                return found == null
                    ? Result<MenuResolveModel>.Fail(ConstantHelper.Message.MenuNotAssigned)
                    : Result<MenuResolveModel>.Success(found, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Resolve: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>
        /// Spec 6.3: phân công Active hiệu lực tại ngày, đúng loại suất (dòng không ghi loại suất áp cho mọi loại – ưu tiên thấp hơn);
        /// cấp điểm giao trước, rồi cấp khách (DeliveryLocationId trống). Chỉ thực đơn Approved / Published (PROPOSED – BA/PO CONFIRM).
        /// ORD-OPT-1.2 (D-74 f): không có phân công → <b>thực đơn chung</b> (<c>MenuType = COMMON</c>) Đã công bố, hiệu lực tại ngày ăn, có món cho loại suất (nếu có loại suất).
        /// </summary>
        public Task<MenuResolveModel?> ResolveAsync(Guid customerId, Guid? deliveryLocationId, string? mealType, DateTime date)
            => ResolveAsync(_unitOfWork.DbContext, customerId, deliveryLocationId, mealType, date);

        /// <summary>BIZ-1.3: bản dùng chung (tính nhu cầu nhiều ngày lấy thực đơn cho dự báo) – cùng quy tắc như trên.</summary>
        public static async Task<MenuResolveModel?> ResolveAsync(DatabaseContext db, Guid customerId, Guid? deliveryLocationId, string? mealType, DateTime date)
        {
            DateTime day = date.Date;
            var rows = await (from a in db.MenuAssignment.AsNoTracking()
                              join m in db.Menu.AsNoTracking() on a.MenuId equals m.Id
                              where !a.IsDeleted && !m.IsDeleted && a.CustomerId == customerId
                                  && (a.Status == null || a.Status == ActiveStatus)
                                  && (m.Status == MenuStatus.Approved || m.Status == MenuStatus.Published)
                                  && (a.EffectiveFrom == null || a.EffectiveFrom <= day) && (a.EffectiveTo == null || a.EffectiveTo >= day)
                                  && (a.MealType == null || a.MealType == mealType)
                                  && (a.DeliveryLocationId == null || a.DeliveryLocationId == deliveryLocationId)
                              select new { a.Id, a.DeliveryLocationId, a.MealType, a.EffectiveFrom, MenuId = m.Id, m.Code, m.Name }).ToListAsync();
            var best = rows
                .OrderByDescending(r => r.DeliveryLocationId != null)
                .ThenByDescending(r => r.MealType != null)
                .ThenByDescending(r => r.EffectiveFrom)
                .FirstOrDefault();
            if (best != null)
            {
                return new MenuResolveModel
                {
                    MenuId = best.MenuId, MenuCode = best.Code, MenuName = best.Name, MenuAssignmentId = best.Id,
                    Level = best.DeliveryLocationId != null ? "LOCATION" : "CUSTOMER",
                };
            }
            var common = await CommonMenuAsync(db, mealType, day);
            return common == null ? null : new MenuResolveModel { MenuId = common.Id, MenuCode = common.Code, MenuName = common.Name, Level = "COMMON" };
        }

        /// <summary>ORD-OPT-1.2 (D-74 f): thực đơn chung Đã công bố hiệu lực tại ngày (mới nhất trước); có loại suất thì phải có món cho loại suất đó.</summary>
        public static async Task<Menu?> CommonMenuAsync(DatabaseContext db, string? mealType, DateTime day)
        {
            var menus = await db.Menu.AsNoTracking()
                .Where(m => !m.IsDeleted && m.MenuType == MenuScope.Common && m.Status == MenuStatus.Published
                    && (m.EffectiveFrom == null || m.EffectiveFrom <= day) && (m.EffectiveTo == null || m.EffectiveTo >= day))
                .OrderByDescending(m => m.EffectiveFrom).ToListAsync();
            if (string.IsNullOrWhiteSpace(mealType) || menus.Count == 0)
            {
                return menus.FirstOrDefault();
            }
            var ids = menus.Select(m => m.Id).ToList();
            var withMeal = await db.MenuLine.AsNoTracking().Where(l => ids.Contains(l.MenuId) && !l.IsDeleted && l.MealType == mealType).Select(l => l.MenuId).Distinct().ToListAsync();
            return menus.FirstOrDefault(m => withMeal.Contains(m.Id));
        }
    }
}
