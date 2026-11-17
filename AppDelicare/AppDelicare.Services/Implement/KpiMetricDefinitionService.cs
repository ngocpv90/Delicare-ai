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
using AppDelicare.Model.ViewModels.Custom;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Net;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// Catalog 221 (spec 11.7) – định nghĩa chỉ tiêu KPI, CRUD cũ + <b>phiên bản</b> (BE3.4, D-57 b): đổi công thức / mục tiêu / đơn vị / cách tổng hợp
    /// → tạo phiên bản mới (VersionNo = lớn nhất + 1, kể cả bản đã xóa mềm – UQ_KpiMetricDefinition_Code_Version), đóng EffectiveTo của bản đang dùng,
    /// không sửa bản cũ (số KPI cũ vẫn đối chiếu đúng công thức cũ). Chỉ sửa tại chỗ tên / nhóm / đang dùng / ngày hết hiệu lực của bản mới nhất.
    /// Mục tiêu ghi thành thẻ <c>[TARGET &gt;= x]</c> cuối FormulaDescription (GAP-KPI-01). PROPOSED – BA/PO CONFIRM (D-57 w, x).
    /// </summary>
    public class KpiMetricDefinitionService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper, IReferenceGuard referenceGuard, IClock clock) : IKpiMetricDefinitionService
    {
        private readonly IClock _clock = clock;
        /// <summary>Thông báo thành công riêng của lần lưu (tạo phiên bản mới) – service theo phạm vi request.</summary>
        private string? _savedMessage;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IReferenceGuard _referenceGuard = referenceGuard;

        public async Task<IResults<KpiMetricDefinitionCustomViewModel>> GetAll(KpiMetricDefinitionSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<KpiMetricDefinitionCustomViewModel>("uspKpiMetricDefinition_GetAll", dynamicParameters)).ToList();
                int i = 0;
                DateTime today = _clock.Today;
                list.ForEach(x =>
                {
                    i++;
                    x.STT = (request.Page - 1) * request.PageSize + i;
                    // BE3.4: tách mục tiêu khỏi FormulaDescription (GAP-KPI-01).
                    var (formula, target, direction) = KpiCode.ParseFormula(x.FormulaDescription, x.MetricCode);
                    x.Formula = formula;
                    x.TargetValue = target;
                    x.TargetDirection = direction;
                    x.TargetDirectionName = KpiCode.DirectionLabel(direction);
                    x.IsCurrent = x.IsActive == true && x.EffectiveFrom <= today && (x.EffectiveTo == null || x.EffectiveTo >= today);
                });
                return new Results<KpiMetricDefinitionCustomViewModel>
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

        public IResults<KpiMetricDefinition> GetAllData()
        {
            try
            {
                var list = _unitOfWork.KpiMetricDefinitionRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<KpiMetricDefinition>
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

        public async Task<IResult<bool>> Save(KpiMetricDefinitionCustomRequest request, string typeSave)
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
                    ResponseMessage = _savedMessage ?? (typeSave == ConstantHelper.TypeSave.Delete ? ConstantHelper.Message.DeleteSuccess : ConstantHelper.Message.SaveSuccess),
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

        /// <summary>Mã chuẩn hóa (bỏ khoảng trắng, IN HOA) – công thức tính nhận theo mã.</summary>
        private static string Code(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

        /// <summary>FormulaDescription mới = công thức chữ + thẻ mục tiêu. Mục tiêu trống → lấy thẻ có sẵn trong FormulaDescription gửi lên (nếu có).</summary>
        private static string? Compose(KpiMetricDefinitionCustomRequest request, string code, out string formula)
        {
            var parsed = KpiCode.ParseFormula(request.FormulaDescription, code);
            string? direction = string.IsNullOrWhiteSpace(request.TargetDirection) ? null : KpiCode.NormalizeDirection(request.TargetDirection);
            if (!string.IsNullOrWhiteSpace(request.TargetDirection) && direction == null)
            {
                formula = string.Empty;
                return string.Format(ConstantHelper.Message.KpiDirectionInvalid, request.TargetDirection);
            }
            decimal? target = request.TargetValue ?? parsed.Target;
            formula = KpiCode.ComposeFormula(parsed.Formula, target, direction ?? parsed.Direction);
            return formula.Length > 2000 ? ConstantHelper.Message.KpiFormulaTooLong : null;
        }

        /// <summary>Phiên bản lớn nhất của mã, kể cả bản đã xóa mềm (UNIQUE gồm cả dòng xóa mềm).</summary>
        private int MaxVersion(string code)
            => _unitOfWork.KpiMetricDefinitionRepository.GetAll().Where(x => x.MetricCode == code).Select(x => (int?)x.VersionNo).Max() ?? 0;

        private async Task<string> Insert(KpiMetricDefinitionCustomRequest request, string typeSave)
        {
            string code = Code(request.MetricCode);
            if (_unitOfWork.KpiMetricDefinitionRepository.GetSingle(x => x.MetricCode == code && !x.IsDeleted) != null)
            {
                return string.Format(ConstantHelper.Message.KpiMetricCodeExists, code);
            }
            string? error = Compose(request, code, out string formula);
            if (error != null)
            {
                return error;
            }
            if (request.EffectiveTo != null && request.EffectiveTo.Value.Date < request.EffectiveFrom!.Value.Date)
            {
                return ConstantHelper.Message.ContractDateRangeInvalid;
            }
            int version = MaxVersion(code) + 1;
            if (request.VersionNo is { } wanted && wanted != version)
            {
                // Phiên bản do hệ thống đánh số; gửi số đã dùng → báo trùng (UQ_KpiMetricDefinition_Code_Version).
                if (wanted < version)
                {
                    return string.Format(ConstantHelper.Message.KpiVersionDuplicate, code, wanted);
                }
            }
            var insert = new KpiMetricDefinition
            {
                Id = Guid.NewGuid(),
                MetricCode = code,
                MetricName = request.MetricName?.Trim(),
                DomainCode = request.DomainCode?.Trim().ToUpperInvariant(),
                UnitCode = request.UnitCode?.Trim(),
                FormulaDescription = formula,
                AggregationCode = request.AggregationCode?.Trim().ToUpperInvariant(),
                VersionNo = version,
                EffectiveFrom = request.EffectiveFrom!.Value.Date,
                EffectiveTo = request.EffectiveTo?.Date,
                IsActive = request.IsActive ?? true,
            };
            _unitOfWork.KpiMetricDefinitionRepository.Add(insert);
            await _unitOfWork.KpiMetricDefinitionRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        /// <summary>
        /// Sửa bản mới nhất. Đổi công thức / mục tiêu / đơn vị / cách tổng hợp → phiên bản mới: hiệu lực từ EffectiveFrom gửi lên nếu sau ngày hiệu lực của bản đang dùng,
        /// không thì từ hôm nay (hoặc ngày kế tiếp bản đang dùng); bản đang dùng đóng EffectiveTo = ngày trước đó. Còn lại (tên, nhóm, đang dùng, ngày hết hiệu lực) → sửa tại chỗ.
        /// </summary>
        private async Task<string> Update(KpiMetricDefinitionCustomRequest request, string typeSave)
        {
            var exist = _unitOfWork.KpiMetricDefinitionRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            string code = Code(request.MetricCode);
            if (!string.Equals(code, exist.MetricCode, StringComparison.OrdinalIgnoreCase))
            {
                return ConstantHelper.Message.KpiMetricCodeChange;
            }
            int latest = _unitOfWork.KpiMetricDefinitionRepository.GetAll().Where(x => x.MetricCode == exist.MetricCode && !x.IsDeleted).Max(x => x.VersionNo);
            if (exist.VersionNo != latest)
            {
                return string.Format(ConstantHelper.Message.KpiVersionNotLatest, latest);
            }
            string? error = Compose(request, code, out string formula);
            if (error != null)
            {
                return error;
            }
            string? unit = request.UnitCode?.Trim();
            string? aggregation = request.AggregationCode?.Trim().ToUpperInvariant();
            bool newVersion = !string.Equals(formula, exist.FormulaDescription, StringComparison.Ordinal)
                || !string.Equals(unit ?? string.Empty, exist.UnitCode ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals(aggregation ?? string.Empty, exist.AggregationCode ?? string.Empty, StringComparison.Ordinal);
            if (!newVersion)
            {
                if (request.EffectiveTo != null && request.EffectiveTo.Value.Date < exist.EffectiveFrom)
                {
                    return ConstantHelper.Message.ContractDateRangeInvalid;
                }
                exist.MetricName = request.MetricName?.Trim();
                exist.DomainCode = request.DomainCode?.Trim().ToUpperInvariant();
                exist.IsActive = request.IsActive ?? exist.IsActive;
                exist.EffectiveTo = request.EffectiveTo?.Date;
                _unitOfWork.KpiMetricDefinitionRepository.Update(exist);
                await _unitOfWork.KpiMetricDefinitionRepository.CommitAsync(typeSave);
                return string.Empty;
            }
            DateTime today = _clock.Today;
            DateTime requested = request.EffectiveFrom?.Date ?? today;
            DateTime from = requested > exist.EffectiveFrom ? requested : (today > exist.EffectiveFrom ? today : exist.EffectiveFrom.AddDays(1));
            int version = MaxVersion(exist.MetricCode!) + 1;
            if (_unitOfWork.KpiMetricDefinitionRepository.GetSingle(x => x.MetricCode == exist.MetricCode && x.VersionNo == version) != null)
            {
                return string.Format(ConstantHelper.Message.KpiVersionDuplicate, exist.MetricCode, version);
            }
            // Bản cũ: chỉ đóng hiệu lực (không đổi công thức / mục tiêu / số liệu cũ).
            DateTime closeOn = from.AddDays(-1);
            if (exist.EffectiveTo == null || exist.EffectiveTo > closeOn)
            {
                exist.EffectiveTo = closeOn;
            }
            _unitOfWork.KpiMetricDefinitionRepository.Update(exist);
            var next = new KpiMetricDefinition
            {
                Id = Guid.NewGuid(),
                MetricCode = exist.MetricCode,
                MetricName = request.MetricName?.Trim() ?? exist.MetricName,
                DomainCode = request.DomainCode?.Trim().ToUpperInvariant() ?? exist.DomainCode,
                UnitCode = unit,
                FormulaDescription = formula,
                AggregationCode = aggregation,
                VersionNo = version,
                EffectiveFrom = from,
                EffectiveTo = request.EffectiveTo is { } to && to.Date >= from ? to.Date : null,
                IsActive = request.IsActive ?? true,
            };
            _unitOfWork.KpiMetricDefinitionRepository.Add(next);
            await _unitOfWork.KpiMetricDefinitionRepository.CommitAsync(typeSave);
            _savedMessage = string.Format(ConstantHelper.Message.KpiNewVersionCreated, version, from, exist.VersionNo, closeOn);
            return string.Empty;
        }

        /// <summary>Xóa mềm bản mới nhất chưa có số liệu KPI; bản trước (nếu bị đóng bởi bản này) mở lại hiệu lực. Bản cũ / đã có số liệu → không xóa.</summary>
        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // T07: bản ghi đang được tham chiếu (quan hệ logic, truy vấn có tham số) → không cho xóa.
            if ((await _referenceGuard.FindUsagesAsync("KpiMetricDefinition", (Guid)id)).Count > 0)
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            var delete = _unitOfWork.KpiMetricDefinitionRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            var db = _unitOfWork.DbContext;
            bool used = await db.KpiDailySnapshot.AnyAsync(x => !x.IsDeleted && x.MetricCode == delete.MetricCode && x.MetricVersion == delete.VersionNo)
                || await db.KpiMonthlySnapshot.AnyAsync(x => !x.IsDeleted && x.MetricCode == delete.MetricCode && x.MetricVersion == delete.VersionNo);
            if (used)
            {
                return ConstantHelper.Message.KpiDeleteHasSnapshot;
            }
            var versions = _unitOfWork.KpiMetricDefinitionRepository.GetAll().Where(x => x.MetricCode == delete.MetricCode && !x.IsDeleted).ToList();
            if (versions.Max(x => x.VersionNo) != delete.VersionNo)
            {
                return string.Format(ConstantHelper.Message.KpiVersionNotLatest, versions.Max(x => x.VersionNo));
            }
            _unitOfWork.KpiMetricDefinitionRepository.Update(delete);
            await _unitOfWork.KpiMetricDefinitionRepository.CommitAsync(typeSave);
            var previous = versions.Where(x => x.VersionNo < delete.VersionNo).OrderByDescending(x => x.VersionNo).FirstOrDefault();
            if (previous != null && previous.EffectiveTo == delete.EffectiveFrom.AddDays(-1))
            {
                // Commit riêng: CommitAsync(Delete) đánh dấu xóa mọi bản ghi đang sửa.
                previous.EffectiveTo = null;
                _unitOfWork.KpiMetricDefinitionRepository.Update(previous);
                await _unitOfWork.KpiMetricDefinitionRepository.CommitAsync(ConstantHelper.TypeSave.Update);
            }
            return string.Empty;
        }

        /// <summary>BE3.4: trả định nghĩa đã tách mục tiêu (targetValue, targetDirection) + đã có công thức tính chưa.</summary>
        public async Task<IResult<KpiDefinitionModel>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.KpiMetricDefinitionRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<KpiDefinitionModel>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<KpiDefinitionModel>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    Data = KpiSnapshotService.ToModel(data),
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
