// Sinh bởi tools/AppDelicare.CodeGen (T05), nay là file của dev (T45R: thêm luật (Code, VersionNo)) – CodeGen không ghi đè.
#nullable enable
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Dapper;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
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
    public partial class FoodSafetyChecklistService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper, IReferenceGuard referenceGuard
        , Microsoft.Extensions.Options.IOptions<AppDelicare.Model.Option.BusinessRulesOptions> rules, AppDelicare.Data.Share.IClock clock) : IFoodSafetyChecklistService
    {
        private readonly AppDelicare.Model.Option.BusinessRulesOptions _rules = rules.Value; // UI-6.33: bộ hệ thống
        private readonly AppDelicare.Data.Share.IClock _clock = clock;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IReferenceGuard _referenceGuard = referenceGuard;

        public async Task<IResults<FoodSafetyChecklistViewModel>> GetAll(FoodSafetyChecklistSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@Status", request.Status);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<FoodSafetyChecklistViewModel>("uspFoodSafetyChecklist_GetAll", dynamicParameters)).ToList();
                int i = 0;
                list.ForEach(x =>
                {
                    i++;
                    x.STT = (request.Page - 1) * request.PageSize + i;
                });
                return new Results<FoodSafetyChecklistViewModel>
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

        public IResults<FoodSafetyChecklist> GetAllData()
        {
            try
            {
                var list = _unitOfWork.FoodSafetyChecklistRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<FoodSafetyChecklist>
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

        public async Task<IResult<bool>> Save(FoodSafetyChecklistRequest request, string typeSave)
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
          break;                    default:
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

        private async Task<string> Insert(FoodSafetyChecklistRequest request, string typeSave)
        {
            // T45R: luật nghiệp vụ (spec) – trước khi ghi.
            var invalid = await CheckRules(request);
            if (!string.IsNullOrEmpty(invalid))
            {
                return invalid;
            }
            // UI-6.33 (D-114): bộ mới luôn phiên bản 1 (phiên bản sau qua Tạo phiên bản mới); mã trùng bộ khác → thêm hậu tố.
            request.VersionNo = 1;
            string baseCode = (request.Code ?? string.Empty).Trim().ToUpperInvariant();
            string code = baseCode;
            for (int n = 2; _unitOfWork.FoodSafetyChecklistRepository.GetSingle(x => x.Code == code && !x.IsDeleted) != null; n++)
            {
                code = baseCode + "_" + n;
            }
            request.Code = code;
            var insert = _mapper.Map<FoodSafetyChecklist>(request);
            insert.VersionNo = 1;
            if (request.Status == null) insert.Status = "Active";
            var entity = insert;
            _unitOfWork.FoodSafetyChecklistRepository.Add(insert);
            await _unitOfWork.FoodSafetyChecklistRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Update(FoodSafetyChecklistRequest request, string typeSave)
        {
            // T45R: luật nghiệp vụ (spec) – trước khi ghi.
            var invalid = await CheckRules(request);
            if (!string.IsNullOrEmpty(invalid))
            {
                return invalid;
            }
            var exist = _unitOfWork.FoodSafetyChecklistRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // UI-6.33 (D-114): bộ hệ thống không đổi mã / không ngừng dùng; phiên bản không sửa tay.
            if (FoodSafetyRules.IsSystem(exist.Code, _rules) && (!string.Equals(request.Code?.Trim(), exist.Code, StringComparison.OrdinalIgnoreCase)
                || string.Equals(request.Status, "Inactive", StringComparison.OrdinalIgnoreCase)))
            {
                return string.Format(ConstantHelper.Message.ChecklistSystemCodeLocked, exist.Name ?? exist.Code);
            }
            request.VersionNo = exist.VersionNo;
            if (_unitOfWork.FoodSafetyChecklistRepository.GetSingle(x => x.Id != request.Id && x.Code == request.Code && x.VersionNo == request.VersionNo && !x.IsDeleted) != null)
            {
                return string.Format(ConstantHelper.Message.ChecklistVersionDuplicate, request.Name ?? request.Code, request.VersionNo);
            }
            var entity = _mapper.Map(request, exist);
            _unitOfWork.FoodSafetyChecklistRepository.Update(entity);
            await _unitOfWork.FoodSafetyChecklistRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // T07: bản ghi đang được tham chiếu (quan hệ logic, truy vấn có tham số) → không cho xóa.
            if ((await _referenceGuard.FindUsagesAsync("FoodSafetyChecklist", (Guid)id)).Count > 0)
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            var delete = _unitOfWork.FoodSafetyChecklistRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            if (FoodSafetyRules.IsSystem(delete.Code, _rules))
            {
                return string.Format(ConstantHelper.Message.ChecklistSystemCodeLocked, delete.Name ?? delete.Code);
            }
            _unitOfWork.FoodSafetyChecklistRepository.Update(delete);
            await _unitOfWork.FoodSafetyChecklistRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<FoodSafetyChecklist>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.FoodSafetyChecklistRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<FoodSafetyChecklist>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<FoodSafetyChecklist>
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

        /// <summary>T45R (spec 5.5, UNIQUE của DDL): (Code, VersionNo) không trùng.</summary>
        private async Task<string> CheckRules(FoodSafetyChecklistRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.Code) && await _unitOfWork.FoodSafetyChecklistRepository.GetAll()
                .AnyAsync(x => x.Code == request.Code && x.VersionNo == request.VersionNo && x.Id != request.Id && !x.IsDeleted))
            {
                return string.Format(ConstantHelper.Message.ChecklistVersionExist, request.Code, request.VersionNo);
            }
            return string.Empty;
        }
    }
}
