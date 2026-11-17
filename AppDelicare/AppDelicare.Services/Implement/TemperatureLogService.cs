#nullable enable
using AutoMapper;
using Dapper;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Model.Request.Custom;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using AppDelicare.Services.Interface;
using Serilog;
using System.Net;

namespace AppDelicare.Services.Implement
{
    /// <summary>TemperatureLog – sổ chỉ ghi thêm (T08). BE2.1: ghi nhiệt độ tự do (Record), CRUD Insert tính ngưỡng ở server; CORE / DELIVERY / RECEIVING qua nghiệp vụ.</summary>
    public class TemperatureLogService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper, IReferenceGuard referenceGuard
        , IClock clock, ICurrentActor currentActor, IOptions<BusinessRulesOptions> rules, IOptions<ReferenceListsOptions> referenceLists) : ITemperatureLogService
    {
        private readonly IClock _clock = clock;
        private readonly ICurrentActor _currentActor = currentActor;
        private readonly BusinessRulesOptions _rules = rules.Value;
        private readonly ReferenceListsOptions _referenceLists = referenceLists.Value;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IReferenceGuard _referenceGuard = referenceGuard;

        public async Task<IResults<TemperatureLogViewModel>> GetAll(TemperatureLogSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@StorageZoneId", request.StorageZoneId);
                dynamicParameters.Add("@StorageLocationId", request.StorageLocationId);
                dynamicParameters.Add("@MaterialLotId", request.MaterialLotId);
                dynamicParameters.Add("@ProductionBatchId", request.ProductionBatchId);
                dynamicParameters.Add("@MeasurementType", request.MeasurementType);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<TemperatureLogViewModel>("uspTemperatureLog_GetAll", dynamicParameters)).ToList();
                int i = 0;
                list.ForEach(x =>
                {
                    i++;
                    x.STT = (request.Page - 1) * request.PageSize + i;
                });
                return new Results<TemperatureLogViewModel>
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

        public IResults<TemperatureLog> GetAllData()
        {
            try
            {
                var list = _unitOfWork.TemperatureLogRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<TemperatureLog>
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

        public async Task<IResult<bool>> Save(TemperatureLogRequest request, string typeSave)
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
      case ConstantHelper.TypeSave.Delete:
          result = "Sổ chỉ ghi thêm: không sửa / xóa. Dùng bút toán đảo (CLAUDE.md 3.3).";
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

        /// <summary>BE2.1: CRUD Insert đi chung đường với TemperatureLog/Record – ngưỡng, IsWithinRange, người ghi tính ở server (không nhận từ client).</summary>
        private async Task<string> Insert(TemperatureLogRequest request, string typeSave)
        {
            var (_, error) = await RecordCoreAsync(new TemperatureRecordRequest
            {
                StorageZoneId = request.StorageZoneId,
                StorageLocationId = request.StorageLocationId,
                MaterialLotId = request.MaterialLotId,
                ProductionBatchId = request.ProductionBatchId,
                MeasurementType = request.MeasurementType,
                TemperatureC = request.TemperatureC,
                MeasuredAt = request.MeasuredAt,
                DeviceCode = request.DeviceCode,
                SourceChannel = request.SourceChannel,
                CorrectiveAction = request.CorrectiveAction,
                EvidenceUrl = request.EvidenceUrl,
            }, []);
            return error ?? string.Empty;
        }

        /// <summary>Loại đo phải ghi qua nghiệp vụ riêng (gắn kiểm soát ATTP chặn đạt QC / xuất phát / nhận hàng).</summary>
        private static readonly Dictionary<string, string> ViaAction = new(StringComparer.OrdinalIgnoreCase)
        {
            [ProductionCode.CoreMeasurement] = "ProductionBatch/RecordCoreTemperature",
            ["CORE_TEMP"] = "ProductionBatch/RecordCoreTemperature",
            [DeliveryCode.DeliveryMeasurement] = "DeliveryRoute/RecordTemperature (xuất phát / bàn giao)",
            ["RECEIVING"] = "QcInspection (nhận hàng – GoodsReceipt)",
        };

        private const string HotHolding = "HOT_HOLDING";

        /// <summary>
        /// Thân ghi 1 lần đo (trong transaction đang mở, có lưu): kiểm đối tượng đo, loại đo, ngưỡng; trả (lần đo, lỗi). Ngoài ngưỡng mà không có khắc phục → vẫn lưu + cảnh báo (spec 9.3).
        /// </summary>
        private async Task<(TemperatureLog? Log, string? Error)> RecordCoreAsync(TemperatureRecordRequest request, List<string> warnings)
        {
            var actor = _currentActor.RequireActor();
            var db = _unitOfWork.DbContext;
            Guid? zoneId = request.StorageZoneId is { } z && z != Guid.Empty ? z : null;
            Guid? locationId = request.StorageLocationId is { } l && l != Guid.Empty ? l : null;
            Guid? lotId = request.MaterialLotId is { } m && m != Guid.Empty ? m : null;
            Guid? batchId = request.ProductionBatchId is { } b && b != Guid.Empty ? b : null;
            if (zoneId == null && locationId == null && lotId == null && batchId == null)
            {
                return (null, ConstantHelper.Message.TempTargetRequired);
            }
            string type = (request.MeasurementType ?? string.Empty).Trim().ToUpperInvariant();
            if (ViaAction.TryGetValue(type, out string? via))
            {
                return (null, string.Format(ConstantHelper.Message.TempTypeViaAction, type, via));
            }
            if (_referenceLists.Lists.TryGetValue("TemperatureMeasurementType", out var allowed) && allowed.Count != 0
                && !allowed.Any(a => string.Equals(a.Code, type, StringComparison.OrdinalIgnoreCase)))
            {
                return (null, $"Loại đo {type} không có trong danh mục TemperatureMeasurementType ({string.Join(", ", allowed.Select(a => a.Code))})");
            }
            if (request.TemperatureC is not { } temperature)
            {
                return (null, ConstantHelper.Message.ModelStateMessage);
            }
            var names = new List<string>();
            StorageZone? zone = null;
            if (locationId != null)
            {
                var location = await db.StorageLocation.AsNoTracking().FirstOrDefaultAsync(x => x.Id == locationId && !x.IsDeleted);
                if (location == null)
                {
                    return (null, string.Format(ConstantHelper.Message.TempTargetNotFound, "Vị trí kho"));
                }
                names.Add("Vị trí " + (location.Code ?? location.Name));
                zoneId ??= location.StorageZoneId ?? (location.ZoneId != Guid.Empty ? location.ZoneId : null);
            }
            if (zoneId != null)
            {
                zone = await db.StorageZone.AsNoTracking().FirstOrDefaultAsync(x => x.Id == zoneId && !x.IsDeleted);
                if (zone == null)
                {
                    return (null, string.Format(ConstantHelper.Message.TempTargetNotFound, "Khu kho"));
                }
                names.Insert(0, "Khu " + (zone.Code ?? zone.Name));
            }
            if (lotId != null)
            {
                var lot = await db.MaterialLot.AsNoTracking().FirstOrDefaultAsync(x => x.Id == lotId && !x.IsDeleted);
                if (lot == null)
                {
                    return (null, string.Format(ConstantHelper.Message.TempTargetNotFound, "Lô nguyên liệu"));
                }
                names.Add("Lô " + (lot.LotNumber ?? lot.LotNo));
            }
            if (batchId != null)
            {
                var batch = await db.ProductionBatch.AsNoTracking().FirstOrDefaultAsync(x => x.Id == batchId && !x.IsDeleted);
                if (batch == null)
                {
                    return (null, ConstantHelper.Message.BatchNotFound);
                }
                names.Add("Mẻ " + (batch.BatchNumber ?? batch.BatchNo));
            }
            // Ngưỡng: khu kho (TemperatureMin / Max); giữ nóng thành phẩm ≥ DeliveryTempMinC (PROPOSED – GAP-QA-02); loại khác không có ngưỡng → IsWithinRange trống.
            decimal? min = zone?.TemperatureMin, max = zone?.TemperatureMax;
            if (type == HotHolding)
            {
                min = _rules.DeliveryTempMinC;
                max = null;
            }
            bool? within = min == null && max == null ? null : (min == null || temperature >= min) && (max == null || temperature <= max);
            string? corrective = string.IsNullOrWhiteSpace(request.CorrectiveAction) ? null : request.CorrectiveAction.Trim();
            DateTime now = _clock.Now;
            var log = new TemperatureLog
            {
                Id = Guid.NewGuid(),
                StorageZoneId = zoneId,
                StorageLocationId = locationId,
                MaterialLotId = lotId,
                ProductionBatchId = batchId,
                MeasurementType = type,
                TemperatureC = temperature,
                MinAllowedC = min,
                MaxAllowedC = max,
                IsWithinRange = within,
                MeasuredAt = request.MeasuredAt is { } at && at <= now ? at : now,
                DeviceCode = string.IsNullOrWhiteSpace(request.DeviceCode) ? null : request.DeviceCode.Trim(),
                SourceChannel = string.IsNullOrWhiteSpace(request.SourceChannel) ? DeliveryCode.SourceManual : request.SourceChannel.Trim().ToUpperInvariant(),
                RecordedBy = actor.UserId,
                CorrectiveAction = corrective,
                EvidenceUrl = string.IsNullOrWhiteSpace(request.EvidenceUrl) ? null : request.EvidenceUrl.Trim(),
            };
            _unitOfWork.TemperatureLogRepository.Add(log);
            await _unitOfWork.TemperatureLogRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
            if (within == false && corrective == null)
            {
                string range = min != null && max != null ? $"{ProductionRules.Num(min.Value)}…{ProductionRules.Num(max.Value)} °C"
                    : min != null ? $"≥ {ProductionRules.Num(min.Value)} °C" : $"≤ {ProductionRules.Num(max!.Value)} °C";
                warnings.Add(string.Format(ConstantHelper.Message.TempOutOfRangeWarning, ProductionRules.Num(temperature), range));
            }
            names.Insert(0, type);
            return (log, null);
        }

        /// <summary>
        /// BE2.1 (spec 9.3 POST temperature-logs, Catalog 165): ghi nhiệt độ tự do (khu kho / vị trí / lô / mẻ) – sổ chỉ ghi thêm. Ngưỡng lấy từ khu kho
        /// (<c>StorageZone.TemperatureMin / Max</c>, vị trí → khu của vị trí); HOT_HOLDING ≥ DeliveryTempMinC; ngoài ngưỡng thiếu khắc phục → vẫn lưu + cảnh báo.
        /// CORE / DELIVERY / RECEIVING → chặn, ghi qua nghiệp vụ mẻ / chuyến / nhận hàng (để không ghi "đạt" ngoài kiểm soát ATTP). Loại đo theo ReferenceLists TemperatureMeasurementType.
        /// </summary>
        public async Task<IResult<TemperatureLogRecordModel>> Record(TemperatureRecordRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var warnings = new List<string>();
                var (log, error) = await RecordCoreAsync(request, warnings);
                if (log == null)
                {
                    _unitOfWork.RollbackTransaction();
                    _unitOfWork.DbContext.ChangeTracker.Clear();
                    return Result<TemperatureLogRecordModel>.Fail(error!);
                }
                _unitOfWork.CommitTransaction();
                return Result<TemperatureLogRecordModel>.Success(new TemperatureLogRecordModel { Log = log, ListWarning = warnings }, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Record: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                throw;
            }
        }

        /// <summary>
        /// BE3.1 (Catalog 163, spec 9.3 temperature-logs/series): chuỗi nhiệt độ của 1 khu vực kho (StorageZoneId của lần đo) trong [from, to]
        /// (mặc định 48 giờ tới bây giờ, tối đa 31 ngày – BusinessRules:Charts). Ngưỡng = StorageZone.TemperatureMin / Max. Quá <c>TemperatureMaxPoints</c>
        /// lần đo → gộp theo khoảng N phút (trung bình, min / max, đạt khi mọi lần đo trong khoảng đạt – điểm vượt ngưỡng không bị "làm mờ").
        /// </summary>
        public async Task<IResult<TemperatureSeriesModel>> GetSeries(TemperatureSeriesRequest request)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var zone = await db.StorageZone.AsNoTracking().FirstOrDefaultAsync(z => z.Id == request.StorageZoneId && !z.IsDeleted);
                if (zone == null)
                {
                    return Result<TemperatureSeriesModel>.Fail(ConstantHelper.Message.StorageZoneNotFound);
                }
                var charts = _rules.Charts;
                DateTime to = request.To ?? (request.From?.AddHours(charts.TemperatureDefaultHours) ?? _clock.Now);
                DateTime from = request.From ?? to.AddHours(-charts.TemperatureDefaultHours);
                if (from >= to || (to - from).TotalDays > charts.TemperatureMaxRangeDays)
                {
                    return Result<TemperatureSeriesModel>.Fail(string.Format(ConstantHelper.Message.ChartRangeInvalid, charts.TemperatureMaxRangeDays));
                }
                var logs = await db.TemperatureLog.AsNoTracking()
                    .Where(t => !t.IsDeleted && t.StorageZoneId == zone.Id && t.MeasuredAt >= from && t.MeasuredAt <= to)
                    .OrderBy(t => t.MeasuredAt)
                    .Select(t => new { t.MeasuredAt, t.TemperatureC, t.IsWithinRange })
                    .ToListAsync();
                var model = new TemperatureSeriesModel
                {
                    StorageZoneId = zone.Id,
                    ZoneCode = zone.Code,
                    ZoneName = zone.Name,
                    TemperatureMin = zone.TemperatureMin,
                    TemperatureMax = zone.TemperatureMax,
                    From = from,
                    To = to,
                    MeasurementCount = logs.Count,
                    OutOfRangeCount = logs.Count(t => t.IsWithinRange == false),
                };
                int maxPoints = Math.Max(10, charts.TemperatureMaxPoints);
                if (logs.Count <= maxPoints)
                {
                    model.Points = logs.Select(t => new TemperaturePointModel { MeasuredAt = t.MeasuredAt, TemperatureC = t.TemperatureC, IsWithinRange = t.IsWithinRange }).ToList();
                    return Result<TemperatureSeriesModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
                }
                int bucket = Math.Max(1, (int)Math.Ceiling((to - from).TotalMinutes / maxPoints));
                model.BucketMinutes = bucket;
                model.Points = logs
                    .GroupBy(t => from.AddMinutes(Math.Floor((t.MeasuredAt - from).TotalMinutes / bucket) * bucket))
                    .Select(g => new TemperaturePointModel
                    {
                        MeasuredAt = g.Key,
                        TemperatureC = Math.Round(g.Average(t => t.TemperatureC), 2),
                        MinC = g.Min(t => t.TemperatureC),
                        MaxC = g.Max(t => t.TemperatureC),
                        IsWithinRange = g.Any(t => t.IsWithinRange == false) ? false : g.All(t => t.IsWithinRange == true) ? true : null,
                        Count = g.Count(),
                    })
                    .OrderBy(p => p.MeasuredAt)
                    .ToList();
                return Result<TemperatureSeriesModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetSeries: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<TemperatureLog>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.TemperatureLogRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<TemperatureLog>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<TemperatureLog>
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
