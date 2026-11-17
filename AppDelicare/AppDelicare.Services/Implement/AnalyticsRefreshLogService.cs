#nullable enable
using AutoMapper;
using Dapper;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using AppDelicare.Services.Interface;
using AppDelicare.Services.Jobs;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Net;

namespace AppDelicare.Services.Implement
{
    public class AnalyticsRefreshLogService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper, IReferenceGuard referenceGuard
        , IClock clock, Microsoft.Extensions.Options.IOptions<AppDelicare.Model.Option.BusinessRulesOptions> rules) : IAnalyticsRefreshLogService
    {
        private readonly IClock _clock = clock;
        private readonly AppDelicare.Model.Option.ChartRulesOptions _charts = rules.Value.Charts;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IReferenceGuard _referenceGuard = referenceGuard;

        public async Task<IResults<AnalyticsRefreshLogViewModel>> GetAll(AnalyticsRefreshLogSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@RunId", request.RunId);
                dynamicParameters.Add("@RunStatus", request.RunStatus);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<AnalyticsRefreshLogViewModel>("uspAnalyticsRefreshLog_GetAll", dynamicParameters)).ToList();
                int i = 0;
                list.ForEach(x =>
                {
                    i++;
                    x.STT = (request.Page - 1) * request.PageSize + i;
                });
                return new Results<AnalyticsRefreshLogViewModel>
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

        public IResults<AnalyticsRefreshLog> GetAllData()
        {
            try
            {
                var list = _unitOfWork.AnalyticsRefreshLogRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<AnalyticsRefreshLog>
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

        public async Task<IResult<bool>> Save(AnalyticsRefreshLogRequest request, string typeSave)
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

        private async Task<string> Insert(AnalyticsRefreshLogRequest request, string typeSave)
        {
            var insert = _mapper.Map<AnalyticsRefreshLog>(request);
            var entity = insert;
            _unitOfWork.AnalyticsRefreshLogRepository.Add(insert);
            await _unitOfWork.AnalyticsRefreshLogRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Update(AnalyticsRefreshLogRequest request, string typeSave)
        {
            var exist = _unitOfWork.AnalyticsRefreshLogRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            var entity = _mapper.Map(request, exist);
            _unitOfWork.AnalyticsRefreshLogRepository.Update(entity);
            await _unitOfWork.AnalyticsRefreshLogRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // T07: bản ghi đang được tham chiếu (quan hệ logic, truy vấn có tham số) → không cho xóa.
            if ((await _referenceGuard.FindUsagesAsync("AnalyticsRefreshLog", (Guid)id)).Count > 0)
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            var delete = _unitOfWork.AnalyticsRefreshLogRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            _unitOfWork.AnalyticsRefreshLogRepository.Update(delete);
            await _unitOfWork.AnalyticsRefreshLogRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<AnalyticsRefreshLog>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.AnalyticsRefreshLogRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<AnalyticsRefreshLog>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<AnalyticsRefreshLog>
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
        /// BE3.4 (Catalog 225, spec 11.7 refresh-logs): nhật ký <c>AnalyticsRefreshLog</c> (nút KpiSnapshot/Refresh + job T23). Lọc theo giờ bắt đầu trong [from, to]
        /// (trống = 30 ngày gần nhất, tối đa 366 ngày – BusinessRules:Charts), mã job, trạng thái; mới nhất trước. Trigger / ngày nghiệp vụ tách từ SourceWatermark.
        /// </summary>
        public async Task<IResults<AnalyticsRefreshLogItemModel>> GetList(AnalyticsRefreshLogListRequest request)
        {
            try
            {
                string? error = ChartRules.DayRange(request.From, request.To, _clock.Today, _charts.DefaultRangeDays, _charts.MaxRangeDays, out DateTime from, out DateTime to);
                if (error != null)
                {
                    return Results<AnalyticsRefreshLogItemModel>.Fail(error);
                }
                int page = Math.Max(1, request.Page);
                int pageSize = Math.Clamp(request.PageSize, 1, 500);
                DateTime end = to.AddDays(1);
                var db = _unitOfWork.DbContext;
                var query = db.AnalyticsRefreshLog.AsNoTracking().Where(x => !x.IsDeleted && x.StartedAt >= from && x.StartedAt < end);
                if (!string.IsNullOrWhiteSpace(request.JobCode))
                {
                    string code = request.JobCode.Trim();
                    query = query.Where(x => x.JobCode == code);
                }
                if (!string.IsNullOrWhiteSpace(request.Status))
                {
                    string status = request.Status.Trim();
                    query = query.Where(x => x.RunStatus == status);
                }
                int total = await query.CountAsync();
                var rows = await query.OrderByDescending(x => x.StartedAt).ThenByDescending(x => x.CreatedDate)
                    .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
                var userIds = rows.Where(x => x.CreatedBy != null).Select(x => x.CreatedBy!.Value).Distinct().ToList();
                var users = await db.User.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.UserName);
                int i = 0;
                var list = rows.Select(x =>
                {
                    string[] mark = (x.SourceWatermark ?? string.Empty).Split('|');
                    i++;
                    return new AnalyticsRefreshLogItemModel
                    {
                        STT = (page - 1) * pageSize + i,
                        Id = x.Id,
                        JobCode = x.JobCode,
                        RunId = x.RunId,
                        StartedAt = x.StartedAt,
                        CompletedAt = x.CompletedAt,
                        DurationSeconds = x.CompletedAt is { } c ? Math.Round((c - x.StartedAt).TotalSeconds, 0) : null,
                        RunStatus = x.RunStatus,
                        RunStatusName = RunStatusLabel(x.RunStatus),
                        RowsProcessed = x.RowsProcessed,
                        SourceWatermark = x.SourceWatermark,
                        Trigger = mark.Length > 1 ? mark[0] : null,
                        TargetDate = mark.Length > 1 && DateTime.TryParseExact(mark[1], "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None, out DateTime d) ? d : null,
                        Message = x.ErrorMessage,
                        CreatedBy = x.CreatedBy,
                        CreatedByName = x.CreatedBy is { } u ? users.GetValueOrDefault(u) : null,
                    };
                }).ToList();
                return new Results<AnalyticsRefreshLogItemModel>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    ListData = list,
                    TotalRecords = total,
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetList: ", ex.ToString()));
                throw;
            }
        }

        public static string RunStatusLabel(string? status) => status switch
        {
            JobRunStatus.Running => "Đang chạy",
            JobRunStatus.Succeeded => "Thành công",
            JobRunStatus.Failed => "Lỗi",
            JobRunStatus.Skipped => "Bỏ qua",
            JobRunStatus.Locked => "Đang chạy ở nơi khác",
            _ => status ?? string.Empty,
        };
    }
}
