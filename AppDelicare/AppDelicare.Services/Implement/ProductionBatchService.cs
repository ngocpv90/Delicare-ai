using AutoMapper;
using Dapper;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Model.Request;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using System.Net;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// T17 (Pilot P7) – mẻ nấu: Planned → Cooking (Start) → QCPending (Finish) → Passed (Release) / Failed (Reject) → Packed (Pack).
    /// Nhiệt độ lõi (TemperatureLog CORE, sổ chỉ ghi thêm) ≥ CoreTempMinC và lưu mẫu (SAMPLE_RETAINED) là điều kiện bắt buộc để đạt QC / đóng gói.
    /// </summary>
    public class ProductionBatchService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService, IMaterialIssueService materialIssueService, IUomConversionService uomConversionService
        , IClock clock, ICurrentActor currentActor, IOptions<BusinessRulesOptions> rules) : IProductionBatchService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;
        private readonly IMaterialIssueService _materialIssueService = materialIssueService;
        private readonly IUomConversionService _uomConversionService = uomConversionService;
        private readonly IClock _clock = clock;
        private readonly ICurrentActor _currentActor = currentActor;
        private readonly BusinessRulesOptions _rules = rules.Value;

        public async Task<IResults<ProductionBatchViewModel>> GetAll(ProductionBatchSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@ProductionOrderId", request.ProductionOrderId == Guid.Empty ? null : request.ProductionOrderId);
                dynamicParameters.Add("@BatchNumber", request.BatchNumber);
                dynamicParameters.Add("@StartDate", request.StartDate);
                dynamicParameters.Add("@EndDate", request.EndDate);
                //dynamicParameters.Add("@PlannedQuantity", request.PlannedQuantity);
                //dynamicParameters.Add("@ActualQuantity", request.ActualQuantity);
                dynamicParameters.Add("@QualityStatus", request.QualityStatus);
                dynamicParameters.Add("@Status", request.Status);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<ProductionBatchViewModel>("uspProductionBatch_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<ProductionBatchViewModel>
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

        public IResults<ProductionBatch> GetAllData()
        {
            try
            {
                var list = _unitOfWork.ProductionBatchRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<ProductionBatch>
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

        public async Task<IResult<bool>> Save(ProductionBatchRequest request, string typeSave)
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

        /// <summary>T17: mẻ chỉ sinh khi phát hành kế hoạch (ProductionPlan/Release – số ME-, lịch ngược) – CRUD Insert không ghi.</summary>
        private Task<string> Insert(ProductionBatchRequest request, string typeSave)
            => Task.FromResult(string.Format(ConstantHelper.Message.ProductionCrudBlocked, "Mẻ nấu", "ProductionPlan/Release, ProductionBatch/Start / Finish / Release / Pack"));

        /// <summary>T17: CRUD Update chỉ đổi trạm bếp của mẻ Kế hoạch / Đang nấu; trạng thái, số, số lượng, giờ chỉ đổi qua action nghiệp vụ.</summary>
        private async Task<string> Update(ProductionBatchRequest request, string typeSave)
        {
            var exist = _unitOfWork.ProductionBatchRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            if (exist.Status is not (ProductionBatchStatus.Planned or ProductionBatchStatus.Cooking))
            {
                return InvalidStatus(exist, "sửa mẻ", ProductionBatchStatus.Planned, ProductionBatchStatus.Cooking);
            }
            if (request.KitchenWorkstationId is { } ws && ws != Guid.Empty
                && !await _unitOfWork.DbContext.KitchenWorkstation.AsNoTracking().AnyAsync(k => k.Id == ws && !k.IsDeleted))
            {
                return ConstantHelper.Message.WorkstationNotFound;
            }
            exist.KitchenWorkstationId = request.KitchenWorkstationId is { } w && w != Guid.Empty ? w : null;
            _unitOfWork.ProductionBatchRepository.Update(exist);
            await _unitOfWork.ProductionBatchRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        /// <summary>T17: mẻ không xóa (đã có số ME-, lịch, có thể đã xuất NL / đo nhiệt độ) – hủy mẻ để sau Pilot.</summary>
        private Task<string> Delete(Guid? id, string typeSave)
            => Task.FromResult(string.Format(ConstantHelper.Message.ProductionCrudBlocked, "Mẻ nấu", "không xóa – Reject nếu không đạt"));

        /// <summary>BE2.1 (spec 7.5 trace, Catalog 118 / 119 – chiều ngược): mẻ → phiếu xuất → lô NL → phiếu nhập → PO → NCC (+ nhiệt độ), kèm xuôi mẻ → đóng gói → đơn → đơn giao → khách.</summary>
        public async Task<IResult<TraceModel>> Trace(Guid id)
        {
            try
            {
                var model = await TraceRules.FromBatchAsync(_unitOfWork.DbContext, id, _rules);
                return model == null ? Result<TraceModel>.Fail(ConstantHelper.Message.BatchNotFound) : Result<TraceModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Trace: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<ProductionBatch>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.ProductionBatchRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<ProductionBatch>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<ProductionBatch>
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

        private static string InvalidStatus(ProductionBatch batch, string action, params string[] expected)
            => string.Format(ConstantHelper.Message.StatusActionInvalid, action, "mẻ " + (batch.BatchNumber ?? batch.BatchNo),
                string.Join(" / ", expected.Select(ProductionBatchStatus.Label)), ProductionBatchStatus.Label(batch.Status));

        private static string Number(ProductionBatch batch) => batch.BatchNumber ?? batch.BatchNo ?? batch.Id.ToString("N")[..8];

        /// <summary>
        /// Khung chung của thao tác mẻ (1 transaction): nạp mẻ + lệnh SX (đang theo dõi), kiểm trạng thái, chạy <paramref name="body"/> (trả lỗi nghiệp vụ hoặc null),
        /// lưu, commit, trả chi tiết mẻ + cảnh báo.
        /// </summary>
        private async Task<IResult<ProductionBatchDetailModel>> RunAsync(Guid id, string action, string[] allowed,
            Func<ProductionBatch, ProductionOrder, ActorInfo, List<string>, Task<string?>> body)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var actor = _currentActor.RequireActor();
                var batch = _unitOfWork.ProductionBatchRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
                if (batch == null)
                {
                    return Fail(ConstantHelper.Message.BatchNotFound);
                }
                if (!allowed.Contains(batch.Status))
                {
                    return Fail(InvalidStatus(batch, action, allowed));
                }
                var order = _unitOfWork.ProductionOrderRepository.GetSingle(x => x.Id == batch.ProductionOrderId && !x.IsDeleted);
                if (order == null)
                {
                    return Fail(ConstantHelper.Message.ProductionOrderNotFound);
                }
                var warnings = new List<string>();
                string? error = await body(batch, order, actor, warnings);
                if (error != null)
                {
                    return Fail(error);
                }
                _unitOfWork.ProductionBatchRepository.Update(batch);
                await _unitOfWork.ProductionBatchRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                var detail = await DetailAsync(batch.Id);
                detail!.ListWarning.InsertRange(0, warnings);
                return Result<ProductionBatchDetailModel>.Success(detail, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ", action, ": ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }

            IResult<ProductionBatchDetailModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                return Result<ProductionBatchDetailModel>.Fail(message);
            }
        }

        /// <summary>
        /// T17 (spec 7.7 / 8.3, Pilot P7): lập phiếu xuất nguyên liệu cho mẻ – số lượng = định lượng (ảnh chụp lệnh SX) × phần kế hoạch của mẻ ÷ suất chuẩn × (1 + hao hụt %),
        /// quy đổi sang đơn vị tồn kho; lô tự chọn FEFO / FIFO, bỏ lô hết hạn / QC KHONG_DAT / tạm giữ (MaterialIssueService.Create – T16), phiếu gắn lệnh SX
        /// (ProductionOrderId) + số mẻ trong Note (GAP-PRD-04). <c>Post = true</c> → ghi sổ ISSUE ngay; lỗi bất kỳ → không ghi gì (1 transaction).
        /// Mỗi mẻ 1 phiếu tự động (Nháp / Đã xuất); xuất thêm qua MaterialIssue/Create.
        /// </summary>
        public async Task<IResult<MaterialIssueDetailModel>> CreateMaterialIssue(ProductionBatchIssueRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                _currentActor.RequireActor();
                var db = _unitOfWork.DbContext;
                var batch = await db.ProductionBatch.AsNoTracking().FirstOrDefaultAsync(b => b.Id == request.Id && !b.IsDeleted);
                if (batch == null)
                {
                    return Fail(ConstantHelper.Message.BatchNotFound);
                }
                if (batch.Status is not (ProductionBatchStatus.Planned or ProductionBatchStatus.Cooking))
                {
                    return Fail(InvalidStatus(batch, "xuất nguyên liệu cho mẻ", ProductionBatchStatus.Planned, ProductionBatchStatus.Cooking));
                }
                var order = await db.ProductionOrder.AsNoTracking().FirstOrDefaultAsync(o => o.Id == batch.ProductionOrderId && !o.IsDeleted);
                if (order == null)
                {
                    return Fail(ConstantHelper.Message.ProductionOrderNotFound);
                }
                var existing = await ProductionRules.IssuesOf(db, batch).Where(i => i.Status != MaterialIssueStatus.Cancelled).FirstOrDefaultAsync();
                if (existing != null)
                {
                    return Fail(string.Format(ConstantHelper.Message.BatchIssueExists, Number(batch), existing.IssueNumber ?? existing.IssueNo, MaterialIssueStatus.Label(existing.Status)));
                }
                var materials = await ProductionRules.MaterialsAsync(db, order, batch, _rules);
                if (materials.Count == 0)
                {
                    return Fail(ConstantHelper.Message.BatchIssueNoMaterial);
                }
                var units = await db.UnitMeasure.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Name ?? u.Code);
                var lines = new List<MaterialIssueCreateLineRequest>();
                foreach (var material in materials.Where(m => m.RequiredQuantity > 0))
                {
                    var ingredient = await db.Ingredient.AsNoTracking().FirstAsync(i => i.Id == material.IngredientId);
                    Guid stockUnit = await ProductionRules.StockUnitAsync(db, request.WarehouseId, ingredient) ?? material.RecipeUnitId;
                    decimal? quantity = await ProductionRules.ToUnitAsync(_uomConversionService, material.RequiredQuantity, material.RecipeUnitId, stockUnit, _clock.Today);
                    if (quantity == null)
                    {
                        return Fail(string.Format(ConstantHelper.Message.BatchIssueUnitMissing, ingredient.Name, units.GetValueOrDefault(material.RecipeUnitId), units.GetValueOrDefault(stockUnit)));
                    }
                    var merged = lines.FirstOrDefault(l => l.IngredientId == ingredient.Id && l.UnitId == stockUnit);
                    if (merged != null)
                    {
                        merged.Quantity += ProductionRules.Ceil4(quantity.Value);
                        continue;
                    }
                    lines.Add(new MaterialIssueCreateLineRequest { IngredientId = ingredient.Id, Quantity = ProductionRules.Ceil4(quantity.Value), UnitId = stockUnit });
                }
                string note = $"{ProductionCode.IssueBatchTag}{Number(batch)} – lệnh SX {order.ProductionOrderNumber ?? order.ProductionOrderNo}"
                    + (string.IsNullOrWhiteSpace(request.Note) ? string.Empty : " – " + request.Note.Trim());
                var created = await _materialIssueService.Create(new MaterialIssueCreateRequest
                {
                    WarehouseId = request.WarehouseId,
                    ProductionOrderId = order.Id,
                    IssueDate = _clock.Now,
                    Note = note,
                    ListLine = lines,
                });
                if (created.ResponseCode != Convert.ToInt32(HttpStatusCode.OK) || created.Data?.MaterialIssue == null)
                {
                    return Fail(created.ResponseMessage ?? ConstantHelper.Message.InternalServer);
                }
                var result = created.Data;
                if (request.Post)
                {
                    var posted = await _materialIssueService.Post(new InventoryDocumentActionRequest { Id = result.MaterialIssue.Id });
                    if (posted.ResponseCode != Convert.ToInt32(HttpStatusCode.OK) || posted.Data == null)
                    {
                        return Fail(posted.ResponseMessage ?? ConstantHelper.Message.InternalServer);
                    }
                    posted.Data.ListWarning.AddRange(result.ListWarning);
                    result = posted.Data;
                }
                _unitOfWork.CommitTransaction();
                return Result<MaterialIssueDetailModel>.Success(result, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " CreateMaterialIssue: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                throw;
            }

            IResult<MaterialIssueDetailModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                return Result<MaterialIssueDetailModel>.Fail(message);
            }
        }

        /// <summary>T17 (spec 8.3 start): Kế hoạch → Đang nấu, giờ bắt đầu thực tế (StartTime); lệnh SX Phát hành → Đang SX. Chưa xuất NL → cảnh báo (không chặn).</summary>
        public Task<IResult<ProductionBatchDetailModel>> Start(ProductionBatchStartRequest request)
            => RunAsync(request.Id, "bắt đầu nấu", [ProductionBatchStatus.Planned], async (batch, order, actor, warnings) =>
            {
                var db = _unitOfWork.DbContext;
                if (request.KitchenWorkstationId is { } ws && ws != Guid.Empty)
                {
                    if (!await db.KitchenWorkstation.AsNoTracking().AnyAsync(k => k.Id == ws && !k.IsDeleted))
                    {
                        return ConstantHelper.Message.WorkstationNotFound;
                    }
                    batch.KitchenWorkstationId = ws;
                }
                if (!await ProductionRules.IssuesOf(db, batch).AnyAsync(i => i.Status == MaterialIssueStatus.Issued))
                {
                    warnings.Add(string.Format(ConstantHelper.Message.BatchNotIssuedWarning, Number(batch)));
                }
                DateTime now = _clock.Now;
                batch.Status = ProductionBatchStatus.Cooking;
                batch.StartTime = now;
                if (order.Status == ProductionOrderStatus.Released)
                {
                    order.Status = ProductionOrderStatus.InProgress;
                }
                order.ActualStartDate ??= now;
                _unitOfWork.ProductionOrderRepository.Update(order);
                return null;
            });

        /// <summary>T17 (spec 8.3 finish): Đang nấu → Chờ QC, giờ xong thực tế (EndTime), số phần thực nấu (tùy chọn).</summary>
        public Task<IResult<ProductionBatchDetailModel>> Finish(ProductionBatchFinishRequest request)
            => RunAsync(request.Id, "nấu xong", [ProductionBatchStatus.Cooking], (batch, order, actor, warnings) =>
            {
                if (request.ActualQuantity is < 0)
                {
                    return Task.FromResult<string?>(ConstantHelper.Message.OutputQuantityInvalid);
                }
                batch.Status = ProductionBatchStatus.QcPending;
                batch.EndTime = _clock.Now;
                if (request.ActualQuantity is > 0)
                {
                    batch.ActualQty = request.ActualQuantity;
                    batch.ActualQuantity = request.ActualQuantity;
                }
                return Task.FromResult<string?>(null);
            });

        /// <summary>
        /// T17 (BUSINESS_RULES §6): ghi nhiệt độ lõi – TemperatureLog CORE (sổ chỉ ghi thêm), MinAllowedC = CoreTempMinC (70 °C), IsWithinRange tính ở server.
        /// Dưới ngưỡng → bắt buộc hành động khắc phục, mẻ bị chặn đạt QC / đóng gói tới khi đo lại đạt (lần đo gần nhất quyết định).
        /// </summary>
        public Task<IResult<ProductionBatchDetailModel>> RecordCoreTemperature(ProductionBatchTemperatureRequest request)
            => RunAsync(request.Id, "ghi nhiệt độ lõi", [ProductionBatchStatus.Cooking, ProductionBatchStatus.QcPending, ProductionBatchStatus.Passed], (batch, order, actor, warnings) =>
            {
                decimal min = _rules.CoreTempMinC;
                bool within = request.TemperatureC >= min;
                string? corrective = string.IsNullOrWhiteSpace(request.CorrectiveAction) ? null : request.CorrectiveAction.Trim();
                if (!within && corrective == null)
                {
                    return Task.FromResult<string?>(string.Format(ConstantHelper.Message.CoreTempCorrectiveRequired, ProductionRules.Num(request.TemperatureC), ProductionRules.Num(min)));
                }
                _unitOfWork.TemperatureLogRepository.Add(new TemperatureLog
                {
                    Id = Guid.NewGuid(),
                    ProductionBatchId = batch.Id,
                    MeasurementType = ProductionCode.CoreMeasurement,
                    TemperatureC = request.TemperatureC,
                    MinAllowedC = min,
                    IsWithinRange = within,
                    MeasuredAt = request.MeasuredAt ?? _clock.Now,
                    DeviceCode = string.IsNullOrWhiteSpace(request.DeviceCode) ? null : request.DeviceCode.Trim(),
                    SourceChannel = "MANUAL",
                    RecordedBy = actor.UserId,
                    CorrectiveAction = corrective,
                    EvidenceUrl = string.IsNullOrWhiteSpace(request.EvidenceUrl) ? null : request.EvidenceUrl.Trim(),
                });
                if (!within)
                {
                    warnings.Add(string.Format(ConstantHelper.Message.CoreTempBelowWarning, ProductionRules.Num(request.TemperatureC), ProductionRules.Num(min)));
                }
                return Task.FromResult<string?>(null);
            });

        /// <summary>
        /// T17 (BUSINESS_RULES §6): ghi lưu mẫu thức ăn – FoodSafetyChecklistResult mục SAMPLE_RETAINED (ResultCode PASS, người / giờ lưu, ảnh), gắn mẻ + ca + trạm.
        /// Mỗi mẻ 1 lần.
        /// </summary>
        public Task<IResult<ProductionBatchDetailModel>> RecordSampleRetained(ProductionBatchSampleRequest request)
            // Anh chốt 2026-11-16: lưu mẫu bắt buộc trước khi giao → ghi được cả khi mẻ Đã đóng gói (chặn ở DeliveryRoute/Dispatch).
            => RunAsync(request.Id, "ghi lưu mẫu", [ProductionBatchStatus.Cooking, ProductionBatchStatus.QcPending, ProductionBatchStatus.Passed, ProductionBatchStatus.Packed], async (batch, order, actor, warnings) =>
            {
                var db = _unitOfWork.DbContext;
                var item = await ProductionRules.SampleItemAsync(db);
                if (item == null)
                {
                    return ConstantHelper.Message.SampleChecklistMissing;
                }
                var existing = (await ProductionRules.SamplesAsync(db, batch.Id)).FirstOrDefault();
                if (existing != null)
                {
                    return string.Format(ConstantHelper.Message.SampleAlready, ProductionRules.Time(existing.CheckedAt));
                }
                DateTime at = request.CheckedAt ?? _clock.Now;
                _unitOfWork.FoodSafetyChecklistResultRepository.Add(new FoodSafetyChecklistResult
                {
                    Id = Guid.NewGuid(),
                    ChecklistId = item.ChecklistId,
                    ChecklistItemId = item.Id,
                    CheckDate = at.Date,
                    ShiftId = order.ShiftId,
                    ProductionBatchId = batch.Id,
                    WorkstationId = batch.KitchenWorkstationId,
                    ResultCode = ProductionCode.ResultPass,
                    ActualValue = string.IsNullOrWhiteSpace(request.ActualValue) ? null : request.ActualValue.Trim(),
                    CheckedBy = actor.UserId,
                    CheckedAt = at,
                    EvidenceUrl = string.IsNullOrWhiteSpace(request.EvidenceUrl) ? null : request.EvidenceUrl.Trim(),
                });
                return null;
            });

        /// <summary>
        /// T17 (spec 8.3 / 8.4): ghi sản lượng mẻ – ProductionOutput (1 dòng / mẻ, ghi lại được tới khi đóng gói): số nấu được, số loại, số đạt = nấu − loại;
        /// mẻ: ActualQuantity / AcceptedQty / RejectedQty (ghi song song cột cũ OutputQty / AcceptedQty / RejectedQty của ProductionOutput).
        /// </summary>
        public Task<IResult<ProductionBatchDetailModel>> RecordOutput(ProductionBatchOutputRequest request)
            => RunAsync(request.Id, "ghi sản lượng", [ProductionBatchStatus.QcPending, ProductionBatchStatus.Passed], (batch, order, actor, warnings) =>
            {
                decimal rejected = request.RejectedQuantity ?? 0m;
                if (request.ProducedQuantity <= 0 || rejected < 0 || rejected > request.ProducedQuantity)
                {
                    return Task.FromResult<string?>(ConstantHelper.Message.OutputQuantityInvalid);
                }
                var output = UpsertOutput(batch, request.ProducedQuantity, rejected, batch.QcStatus, request.Note);
                batch.ActualQty = output.ProducedQuantity;
                batch.ActualQuantity = output.ProducedQuantity;
                batch.AcceptedQty = output.AcceptedQuantity;
                batch.RejectedQty = output.RejectedQuantity;
                return Task.FromResult<string?>(null);
            });

        private ProductionOutput UpsertOutput(ProductionBatch batch, decimal produced, decimal rejected, string? quality, string? note)
        {
            var output = _unitOfWork.ProductionOutputRepository.GetSingle(o => o.ProductionBatchId == batch.Id && !o.IsDeleted);
            if (output == null)
            {
                output = new ProductionOutput { Id = Guid.NewGuid(), ProductionBatchId = batch.Id, DishId = batch.DishId };
                _unitOfWork.ProductionOutputRepository.Add(output);
            }
            else
            {
                _unitOfWork.ProductionOutputRepository.Update(output);
            }
            output.ProducedQuantity = produced;
            output.OutputQty = produced;
            output.RejectedQuantity = rejected;
            output.RejectedQty = rejected;
            output.AcceptedQuantity = produced - rejected;
            output.AcceptedQty = produced - rejected;
            output.WasteQty = batch.WasteQty;
            output.QualityStatus = quality;
            if (!string.IsNullOrWhiteSpace(note))
            {
                output.Note = note.Trim();
            }
            return output;
        }

        /// <summary>T17 (spec 8.4 waste-records): ghi hao hụt / suất thừa của mẻ – MealWasteRecord (thêm dòng, mặc định OVERPRODUCTION); WasteQty của mẻ = Σ suất hao hụt.</summary>
        public Task<IResult<ProductionBatchDetailModel>> RecordWaste(ProductionBatchWasteRequest request)
            => RunAsync(request.Id, "ghi hao hụt", [ProductionBatchStatus.Cooking, ProductionBatchStatus.QcPending, ProductionBatchStatus.Passed, ProductionBatchStatus.Packed, ProductionBatchStatus.Failed],
                async (batch, order, actor, warnings) =>
                {
                    if (request.QuantityMeals is not > 0 && request.QuantityKg is not > 0 || request.QuantityMeals < 0 || request.QuantityKg < 0)
                    {
                        return ConstantHelper.Message.WasteQuantityRequired;
                    }
                    string type = string.IsNullOrWhiteSpace(request.WasteType) ? ProductionCode.WasteOverProduction : request.WasteType.Trim().ToUpperInvariant();
                    AddWaste(batch, order, actor, type, request.QuantityMeals, request.QuantityKg, request.Reason, request.Disposition);
                    batch.WasteQty = await WasteMealsAsync(batch.Id) + (request.QuantityMeals ?? 0m);
                    return null;
                });

        private void AddWaste(ProductionBatch batch, ProductionOrder order, ActorInfo actor, string type, decimal? meals, decimal? kg, string? reason, string? disposition)
            => _unitOfWork.MealWasteRecordRepository.Add(new MealWasteRecord
            {
                Id = Guid.NewGuid(),
                ProductionBatchId = batch.Id,
                ProductionOrderId = order.Id,
                DishId = batch.DishId,
                ShiftId = order.ShiftId,
                WasteType = type,
                QuantityMeals = meals,
                QuantityKg = kg,
                Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
                Disposition = string.IsNullOrWhiteSpace(disposition) ? null : disposition.Trim(),
                OccurredAt = _clock.Now,
                RecordedBy = actor.UserId,
            });

        private Task<decimal> WasteMealsAsync(Guid batchId)
            => _unitOfWork.DbContext.MealWasteRecord.AsNoTracking().Where(w => !w.IsDeleted && w.ProductionBatchId == batchId).SumAsync(w => w.QuantityMeals ?? 0m);

        /// <summary>
        /// T17 (spec 8.3 qc PASS, BUSINESS_RULES §6): Chờ QC → Đạt. Chặn nếu nhiệt độ lõi đo gần nhất &lt; 70 °C (hoặc chưa đo), chưa lưu mẫu
        /// (<c>ProductionRequireSampleBeforeRelease</c>, PROPOSED) hoặc chưa ghi sản lượng.
        /// </summary>
        public Task<IResult<ProductionBatchDetailModel>> Release(ProductionActionRequest request)
            => RunAsync(request.Id, "cho mẻ đạt QC", [ProductionBatchStatus.QcPending], async (batch, order, actor, warnings) =>
            {
                var blockers = await ProductionRules.SafetyBlockersAsync(_unitOfWork.DbContext, batch, _rules, needOutput: true);
                if (blockers.Count != 0)
                {
                    return string.Format(ConstantHelper.Message.BatchCannotRelease, Number(batch), string.Join("; ", blockers));
                }
                if (!_rules.ProductionRequireSampleBeforeRelease && (await ProductionRules.SamplesAsync(_unitOfWork.DbContext, batch.Id)).Count == 0)
                {
                    // Anh chốt 2026-11-16: lưu mẫu bắt buộc trước khi giao (chặn ở DeliveryRoute/Dispatch) – đạt QC chỉ cảnh báo.
                    warnings.Add(ConstantHelper.Message.SampleMissing + " – bắt buộc trước khi xuất chuyến giao");
                }
                batch.Status = ProductionBatchStatus.Passed;
                batch.QcStatus = BatchQcStatus.Pass;
                batch.QualityStatus = BatchQcStatus.Pass;
                SetOutputQuality(batch, BatchQcStatus.Pass, request.Note);
                return null;
            });

        private void SetOutputQuality(ProductionBatch batch, string quality, string? remark)
        {
            var output = _unitOfWork.ProductionOutputRepository.GetSingle(o => o.ProductionBatchId == batch.Id && !o.IsDeleted);
            if (output == null)
            {
                return;
            }
            output.QualityStatus = quality;
            if (!string.IsNullOrWhiteSpace(remark))
            {
                output.Remark = remark.Trim();
            }
            _unitOfWork.ProductionOutputRepository.Update(output);
        }

        /// <summary>T17 (spec 8.3 qc HOLD): tạm giữ mẻ chờ QC (vẫn Chờ QC), nhận xét bắt buộc.</summary>
        public Task<IResult<ProductionBatchDetailModel>> Hold(ProductionActionRequest request)
            => RunAsync(request.Id, "tạm giữ mẻ", [ProductionBatchStatus.QcPending], (batch, order, actor, warnings) =>
            {
                if (string.IsNullOrWhiteSpace(request.Note))
                {
                    return Task.FromResult<string?>(ConstantHelper.Message.BatchReasonRequired);
                }
                batch.QcStatus = BatchQcStatus.Hold;
                batch.QualityStatus = BatchQcStatus.Hold;
                SetOutputQuality(batch, BatchQcStatus.Hold, request.Note);
                return Task.FromResult<string?>(null);
            });

        /// <summary>
        /// T17 (spec 8.3 qc REJECT): Chờ QC / Đạt → Không đạt, lý do bắt buộc. Toàn bộ số nấu được (sản lượng đã ghi, không có thì số thực nấu / kế hoạch) bị loại:
        /// ProductionOutput (đạt 0), MealWasteRecord QC_REJECT. Lệnh SX hoàn thành khi mọi mẻ đã đóng gói / không đạt.
        /// </summary>
        public Task<IResult<ProductionBatchDetailModel>> Reject(ProductionActionRequest request)
            => RunAsync(request.Id, "loại mẻ", [ProductionBatchStatus.QcPending, ProductionBatchStatus.Passed], async (batch, order, actor, warnings) =>
            {
                if (string.IsNullOrWhiteSpace(request.Note))
                {
                    return ConstantHelper.Message.BatchReasonRequired;
                }
                var existing = await _unitOfWork.DbContext.ProductionOutput.AsNoTracking().FirstOrDefaultAsync(o => o.ProductionBatchId == batch.Id && !o.IsDeleted);
                decimal produced = existing?.ProducedQuantity ?? batch.ActualQuantity ?? batch.ActualQty ?? batch.PlannedQuantity ?? batch.PlannedQty ?? 0m;
                batch.Status = ProductionBatchStatus.Failed;
                batch.QcStatus = BatchQcStatus.Reject;
                batch.QualityStatus = BatchQcStatus.Reject;
                batch.ActualQty ??= produced;
                batch.ActualQuantity ??= produced;
                batch.AcceptedQty = 0;
                batch.RejectedQty = produced;
                AddWaste(batch, order, actor, ProductionCode.WasteQcReject, produced, null, request.Note, null);
                batch.WasteQty = await WasteMealsAsync(batch.Id) + produced;
                var output = UpsertOutput(batch, produced, produced, BatchQcStatus.Reject, null);
                output.Remark = request.Note!.Trim();
                await CompleteOrderIfDoneAsync(batch, order);
                return null;
            });

        /// <summary>
        /// T17 (BUSINESS_RULES §6): Đạt → Đã đóng gói. Kiểm lại ATTP – nhiệt độ lõi đo gần nhất &lt; 70 °C (đo lại sau khi đạt) → chặn đóng gói.
        /// Mọi mẻ của lệnh đã đóng gói / không đạt → lệnh Hoàn thành (ActualQty = Σ số đạt); mọi lệnh của kế hoạch hoàn thành → kế hoạch Hoàn thành.
        /// </summary>
        public Task<IResult<ProductionBatchDetailModel>> Pack(ProductionActionRequest request)
            => RunAsync(request.Id, "đóng gói mẻ", [ProductionBatchStatus.Passed], async (batch, order, actor, warnings) =>
            {
                var blockers = await ProductionRules.SafetyBlockersAsync(_unitOfWork.DbContext, batch, _rules, needOutput: true);
                if (blockers.Count != 0)
                {
                    return string.Format(ConstantHelper.Message.BatchCannotPack, Number(batch), string.Join("; ", blockers));
                }
                // BE2.1: mẻ còn lệnh đóng gói chưa xong (Kế hoạch / Đang đóng gói) → hoàn tất bằng PackingOrder/RecordResult (mẻ tự Đã đóng gói khi chia hết và mọi lệnh
                // đã đóng gói). Mọi lệnh đã xong mà mẻ còn phần dư chưa chia → được đóng gói mẻ trực tiếp (phần dư ghi hao hụt nấu dư nếu bỏ).
                var packing = await _unitOfWork.DbContext.PackingOrder.AsNoTracking()
                    .Where(p => !p.IsDeleted && p.ProductionBatchId == batch.Id && (p.Status == PackingOrderStatus.Planned || p.Status == PackingOrderStatus.Packing))
                    .Select(p => p.PackingOrderNumber).ToListAsync();
                if (packing.Count != 0)
                {
                    return string.Format(ConstantHelper.Message.BatchPackViaPackingOrders, Number(batch), string.Join(", ", packing));
                }
                batch.Status = ProductionBatchStatus.Packed;
                await CompleteOrderIfDoneAsync(batch, order);
                return null;
            });

        /// <summary>Lệnh SX hoàn thành khi mọi mẻ (kể cả mẻ đang xử lý) đã đóng gói / không đạt; kế hoạch hoàn thành khi mọi lệnh hoàn thành (dùng chung với PackingOrderService – BE2.1).</summary>
        private Task CompleteOrderIfDoneAsync(ProductionBatch current, ProductionOrder order)
            => ProductionRules.CompleteOrderIfDoneAsync(_unitOfWork, _clock.Now, current, order);

        /// <summary>T17 (spec 8.3 batch-board): mẻ của 1 ngày sản xuất (theo kế hoạch của ngày) + đếm theo trạng thái, số phần kế hoạch / đạt.</summary>
        public async Task<IResult<ProductionBoardModel>> GetBoard(ProductionBoardRequest request)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                DateTime date = request.ProductionDate.Date, next = date.AddDays(1);
                var planIds = await db.ProductionPlan.AsNoTracking().Where(p => !p.IsDeleted && p.ProductionDate >= date && p.ProductionDate < next).Select(p => p.Id).ToListAsync();
                var orderIds = await db.ProductionOrder.AsNoTracking()
                    .Where(o => !o.IsDeleted && planIds.Contains(o.ProductionPlanId) && (request.ShiftId == null || request.ShiftId == Guid.Empty || o.ShiftId == request.ShiftId))
                    .Select(o => o.Id).ToListAsync();
                var batches = await db.ProductionBatch.AsNoTracking()
                    .Where(b => !b.IsDeleted && orderIds.Contains(b.ProductionOrderId) && (string.IsNullOrEmpty(request.Status) || b.Status == request.Status))
                    .ToListAsync();
                var list = await ProductionRules.BatchBriefsAsync(db, batches, _rules);
                return Result<ProductionBoardModel>.Success(new ProductionBoardModel
                {
                    ProductionDate = date,
                    ListBatch = list,
                    StatusCounts = list.GroupBy(b => b.Status ?? string.Empty).ToDictionary(g => g.Key, g => g.Count()),
                    PlannedPortions = list.Sum(b => b.PlannedQuantity),
                    PassedPortions = list.Where(b => b.Status is ProductionBatchStatus.Passed or ProductionBatchStatus.Packed).Sum(b => b.AcceptedQuantity ?? b.ActualQuantity ?? 0m),
                }, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetBoard: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>T17 (spec 8.3 batches/{id}): chi tiết mẻ – lệnh SX, nguyên liệu, phiếu xuất, nhiệt độ lõi, lưu mẫu, sản lượng, hao hụt, điều kiện còn thiếu.</summary>
        public async Task<IResult<ProductionBatchDetailModel>> GetDetail(Guid id)
        {
            try
            {
                var detail = await DetailAsync(id);
                return detail == null ? Result<ProductionBatchDetailModel>.Fail(ConstantHelper.Message.BatchNotFound)
                    : Result<ProductionBatchDetailModel>.Success(detail, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetDetail: ", ex.ToString()));
                throw;
            }
        }

        private async Task<ProductionBatchDetailModel?> DetailAsync(Guid id)
        {
            var db = _unitOfWork.DbContext;
            var batch = await db.ProductionBatch.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted);
            var order = batch == null ? null : await db.ProductionOrder.AsNoTracking().FirstOrDefaultAsync(o => o.Id == batch.ProductionOrderId);
            if (batch == null || order == null)
            {
                return null;
            }
            var issues = await ProductionRules.IssuesOf(db, batch).OrderBy(i => i.CreatedDate).ToListAsync();
            var detail = new ProductionBatchDetailModel
            {
                Batch = (await ProductionRules.BatchBriefsAsync(db, [batch], _rules)).Single(),
                Order = (await ProductionRules.OrderBriefsAsync(db, [order])).Single(),
                CoreTempMinC = _rules.CoreTempMinC,
                ListMaterial = await ProductionRules.MaterialsAsync(db, order, batch, _rules),
                ListIssue = [.. issues.Select(i => new ProductionIssueBriefModel
                {
                    Id = i.Id,
                    IssueNumber = i.IssueNumber ?? i.IssueNo,
                    IssueDate = i.IssueDate,
                    Status = i.Status,
                    StatusName = MaterialIssueStatus.Label(i.Status),
                    WarehouseId = i.WarehouseId,
                })],
                ListTemperature = await db.TemperatureLog.AsNoTracking()
                    .Where(t => !t.IsDeleted && t.ProductionBatchId == id && t.MeasurementType == ProductionCode.CoreMeasurement)
                    .OrderByDescending(t => t.MeasuredAt).ThenByDescending(t => t.CreatedDate).ToListAsync(),
                ListSample = await ProductionRules.SamplesAsync(db, id),
                Output = await db.ProductionOutput.AsNoTracking().FirstOrDefaultAsync(o => o.ProductionBatchId == id && !o.IsDeleted),
                ListWaste = await db.MealWasteRecord.AsNoTracking().Where(w => !w.IsDeleted && w.ProductionBatchId == id).OrderBy(w => w.OccurredAt).ToListAsync(),
                AllowedActions = batch.Status switch
                {
                    ProductionBatchStatus.Planned => ["createMaterialIssue", "start", "exportProductionSheet"],
                    ProductionBatchStatus.Cooking => ["createMaterialIssue", "recordCoreTemperature", "recordSampleRetained", "recordWaste", "finish", "exportProductionSheet"],
                    ProductionBatchStatus.QcPending => ["recordCoreTemperature", "recordSampleRetained", "recordOutput", "recordWaste", "release", "hold", "reject", "exportProductionSheet"],
                    ProductionBatchStatus.Passed => ["recordCoreTemperature", "recordSampleRetained", "recordOutput", "recordWaste", "pack", "reject", "exportProductionSheet"],
                    _ => ["recordWaste", "exportProductionSheet"],
                },
            };
            // T17.1 (anh chốt 2026-11-16, D-42): mẻ Đã đóng gói chưa lưu mẫu vẫn ghi lưu mẫu được (bắt buộc trước khi xuất chuyến).
            if (batch.Status == ProductionBatchStatus.Packed && !detail.Batch.SampleRetained)
            {
                detail.AllowedActions = ["recordSampleRetained", .. detail.AllowedActions];
            }
            if (batch.Status is ProductionBatchStatus.Cooking or ProductionBatchStatus.QcPending or ProductionBatchStatus.Passed)
            {
                detail.ListBlocker = await ProductionRules.SafetyBlockersAsync(db, batch, _rules, needOutput: true);
            }
            if (!detail.Batch.MaterialIssued && batch.Status is ProductionBatchStatus.Planned or ProductionBatchStatus.Cooking)
            {
                detail.ListWarning.Add(string.Format(ConstantHelper.Message.BatchNotIssuedWarning, Number(batch)));
            }
            return detail;
        }

        /// <summary>T17: in "Phiếu sản xuất" (.xlsx) của 1 mẻ (bố cục file thật Phieu san xuat.xlsx).</summary>
        public async Task<IResult<byte[]>> ExportProductionSheet(Guid id)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var batch = await db.ProductionBatch.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted);
                var order = batch == null ? null : await db.ProductionOrder.AsNoTracking().FirstOrDefaultAsync(o => o.Id == batch.ProductionOrderId);
                if (batch == null || order == null)
                {
                    return Result<byte[]>.Fail(ConstantHelper.Message.BatchNotFound);
                }
                var plan = await db.ProductionPlan.AsNoTracking().FirstOrDefaultAsync(p => p.Id == order.ProductionPlanId);
                var sheet = await ProductionRules.SheetBatchesAsync(db, [order], [batch], _rules);
                var company = await db.Company.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.CreatedDate).FirstOrDefaultAsync();
                byte[] file = ProductionRules.BuildProductionSheet(company?.Name, plan?.ProductionDate ?? batch.StartDate.Date,
                    $"Mẻ {Number(batch)} – lệnh SX {order.ProductionOrderNumber ?? order.ProductionOrderNo}", sheet);
                return Result<byte[]>.Success(file, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ExportProductionSheet: ", ex.ToString()));
                throw;
            }
        }
    }
}
