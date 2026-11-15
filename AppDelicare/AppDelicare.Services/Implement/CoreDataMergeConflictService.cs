using Dapper;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using AppDelicare.Services.Interface;
using Serilog;
using System.Net;

namespace AppDelicare.Services.Implement
{
    /// <summary>T45R: xung đột chuẩn hóa dữ liệu lõi (CoreDataMergeConflict, Id bigint – spec 3.6). Viết tay theo mẫu WarehouseService.</summary>
    public class CoreDataMergeConflictService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IClock clock) : ICoreDataMergeConflictService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IClock _clock = clock;

        public async Task<IResults<CoreDataMergeConflictViewModel>> GetAll(CoreDataMergeConflictSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@IsResolved", request.IsResolved);
                dynamicParameters.Add("@EntityName", request.EntityName);
                dynamicParameters.Add("@MigrationCode", request.MigrationCode);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<CoreDataMergeConflictViewModel>("uspCoreDataMergeConflict_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<CoreDataMergeConflictViewModel>
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

        public async Task<IResult<CoreDataMergeConflict>> GetById(long id)
        {
            try
            {
                var data = await _unitOfWork.CoreDataMergeConflictRepository.GetSingleAsync(x => x.Id == id);
                return data == null
                    ? Result<CoreDataMergeConflict>.Fail(ConstantHelper.Message.RecordNotFoundMessage)
                    : Result<CoreDataMergeConflict>.Success(data, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetById: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<bool>> Resolve(CoreDataMergeConflictResolveRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                string? note = request.ResolutionNote?.Trim();
                if (string.IsNullOrEmpty(note) || note.Length > 1000)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<bool>.Fail(ConstantHelper.Message.ResolutionNoteRequired, false);
                }
                var exist = _unitOfWork.CoreDataMergeConflictRepository.GetSingle(x => x.Id == request.Id);
                if (exist == null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<bool>.Fail(ConstantHelper.Message.RecordNotFoundMessage, false);
                }
                if (exist.ResolvedAt != null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<bool>.Fail(ConstantHelper.Message.AlreadyResolved, false);
                }
                exist.ResolvedAt = _clock.Now;
                exist.ResolutionNote = note;
                _unitOfWork.CoreDataMergeConflictRepository.Update(exist);
                await _unitOfWork.CoreDataMergeConflictRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                return Result<bool>.Success(true, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Resolve: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }
    }
}
