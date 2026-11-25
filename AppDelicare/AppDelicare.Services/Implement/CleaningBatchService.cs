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
using AppDelicare.Data.Share;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Model.Request.Custom;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AppDelicare.Services.Implement
{
    public partial class CleaningBatchService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService, IDocumentNumberService documentNumberService, IClock clock, ICurrentActor currentActor
        , IOptions<BusinessRulesOptions> rules, IOptions<ReferenceListsOptions> referenceLists) : ICleaningBatchService
    {
        private readonly ReferenceListsOptions _referenceLists = referenceLists.Value; // UI-6.32: tên loại bao bì
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;
        private readonly IDocumentNumberService _documentNumberService = documentNumberService;
        private readonly IClock _clock = clock;
        private readonly ICurrentActor _currentActor = currentActor;
        private readonly BusinessRulesOptions _rules = rules.Value;

        public async Task<IResults<CleaningBatchViewModel>> GetAll(CleaningBatchSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@CleaningBatchNumber", request.CleaningBatchNumber);
                dynamicParameters.Add("@StartDate", request.StartDate);
                dynamicParameters.Add("@EndDate", request.EndDate);
                //dynamicParameters.Add("@ContainerQuantity", request.ContainerQuantity);
                dynamicParameters.Add("@ResponsibleEmployeeId", request.ResponsibleEmployeeId);
                dynamicParameters.Add("@QualityStatus", request.QualityStatus);
                dynamicParameters.Add("@Status", request.Status);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<CleaningBatchViewModel>("uspCleaningBatch_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<CleaningBatchViewModel>
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

        public IResults<CleaningBatch> GetAllData()
        {
            try
            {
                var list = _unitOfWork.CleaningBatchRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<CleaningBatch>
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

        public async Task<IResult<bool>> Save(CleaningBatchRequest request, string typeSave)
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

        /// <summary>BE2.5: mẻ vệ sinh chỉ lập qua CleaningBatch/Create hoặc ContainerReturn/Inspect (số VS-, số khay theo phiếu kiểm tra) – CRUD Insert không ghi.</summary>
        private static Task<string> Insert(CleaningBatchRequest request, string typeSave)
            => Task.FromResult(string.Format(ConstantHelper.Message.DeliveryCrudBlocked, "Mẻ vệ sinh khay / hộp", "màn Vệ sinh khay hộp (kiểm tra thu hồi tự lập mẻ, Ghi bước, Kết luận)"));

        /// <summary>BE2.5: trạng thái / kết quả QC / số khay chỉ đổi qua CleaningBatch/RecordResults, Complete – CRUD Update không ghi.</summary>
        private static Task<string> Update(CleaningBatchRequest request, string typeSave)
            => Task.FromResult(string.Format(ConstantHelper.Message.DeliveryCrudBlocked, "Mẻ vệ sinh khay / hộp", "màn Vệ sinh khay hộp (Ghi bước, Kết luận)"));

        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // T07: ReferenceGuard (quan hệ logic, truy vấn có tham số) thay SQL động uspSystem_ExecuteQuery (A10).
            if (await _auditChangeService.IsReferencedAsync(nameof(CleaningBatch), (Guid)id))
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            var delete = _unitOfWork.CleaningBatchRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // BE2.5: chỉ xóa mẻ Đang vệ sinh chưa ghi bước nào (lập nhầm); mẻ đã ghi bước / đã kết luận là bằng chứng vệ sinh.
            if (delete.Status != CleaningBatchStatus.InProgress || await _unitOfWork.DbContext.CleaningChecklistResult.AnyAsync(r => !r.IsDeleted && r.CleaningBatchId == delete.Id))
            {
                return string.Format(ConstantHelper.Message.DeliveryCrudBlocked, "Mẻ vệ sinh đã ghi bước / đã kết luận", "Kết luận Không đạt rồi Vệ sinh lại");
            }
            _unitOfWork.CleaningBatchRepository.Update(delete);
            await _unitOfWork.CleaningBatchRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<CleaningBatch>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.CleaningBatchRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<CleaningBatch>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<CleaningBatch>
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

        /// <summary>BE2.5 (spec 9.4): chi tiết mẻ vệ sinh – 6 bước + kết quả mới nhất, nguồn thu hồi / kiểm tra, thao tác được phép.</summary>
        public async Task<IResult<CleaningBatchDetailModel>> GetDetail(Guid id)
        {
            try
            {
                var detail = await CleaningRules.DetailAsync(_unitOfWork.DbContext, _rules, id);
                if (detail != null && detail.Batch.Status == CleaningBatchStatus.InProgress)
                {
                    detail.SuggestedChemical = await SuggestedChemicalAsync(detail.PackagingType); // UI-6.32: điền sẵn hóa chất khử khuẩn
                }
                return detail == null ? Result<CleaningBatchDetailModel>.Fail(ConstantHelper.Message.CleaningBatchNotFound)
                    : Result<CleaningBatchDetailModel>.Success(detail, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetDetail: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Khung chung thao tác mẻ vệ sinh (1 transaction): người thao tác, chạy thân (lỗi nghiệp vụ hoặc Id mẻ), lưu, commit, trả chi tiết + cảnh báo.</summary>
        private async Task<IResult<CleaningBatchDetailModel>> RunAsync(string action, Func<ActorInfo, DateTime, List<string>, Task<(string? Error, Guid BatchId)>> body)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var actor = _currentActor.RequireActor();
                var warnings = new List<string>();
                var (error, batchId) = await body(actor, _clock.Now, warnings);
                if (error != null)
                {
                    _unitOfWork.RollbackTransaction();
                    _unitOfWork.DbContext.ChangeTracker.Clear();
                    return Result<CleaningBatchDetailModel>.Fail(error);
                }
                await _unitOfWork.CleaningBatchRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                var detail = await CleaningRules.DetailAsync(_unitOfWork.DbContext, _rules, batchId);
                detail!.ListWarning.InsertRange(0, warnings);
                return Result<CleaningBatchDetailModel>.Success(detail, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ", action, ": ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                throw;
            }
        }

        /// <summary>
        /// BE2.5 (spec 9.4 POST cleaning-batches, Catalog 167): lập mẻ vệ sinh Đang vệ sinh từ phiếu kiểm tra thu hồi – số VS-YYMMDD-&lt;CA&gt; (ca gửi lên hoặc ca theo giờ),
        /// số khay ≤ số dùng được còn lại của phiếu (trả về − hỏng, trừ khay đã vào mẻ chưa bị loại). Kiểm tra thu hồi đã tự lập mẻ (CleaningAutoCreateOnInspect) –
        /// lập tay dùng cho mẻ vệ sinh lại sau khi mẻ trước Không đạt hoặc chia mẻ.
        /// </summary>
        public Task<IResult<CleaningBatchDetailModel>> Create(CleaningBatchCreateRequest request)
            => RunAsync("Create", async (actor, now, warnings) =>
            {
                var db = _unitOfWork.DbContext;
                var inspection = await db.ReturnInspection.AsNoTracking().FirstOrDefaultAsync(r => r.Id == request.ReturnInspectionId && !r.IsDeleted);
                var containerReturn = inspection == null ? null : await db.ContainerReturn.AsNoTracking().FirstOrDefaultAsync(c => c.Id == inspection.ContainerReturnId && !c.IsDeleted);
                if (inspection == null || containerReturn == null)
                {
                    return (ConstantHelper.Message.CleaningInspectionNotFound, Guid.Empty);
                }
                var (_, remaining) = await CleaningRules.UsableAsync(db, inspection, containerReturn);
                if (remaining <= 0)
                {
                    return (ConstantHelper.Message.CleaningNothingUsable, Guid.Empty);
                }
                int quantity = request.ContainerQuantity ?? remaining;
                if (quantity <= 0 || quantity > remaining)
                {
                    return (string.Format(ConstantHelper.Message.CleaningQuantityInvalid, remaining), Guid.Empty);
                }
                Shift? shift = null;
                if (request.ShiftId is { } sid && sid != Guid.Empty)
                {
                    shift = await db.Shift.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sid && !s.IsDeleted);
                    if (shift == null)
                    {
                        return (ConstantHelper.Message.ChecklistShiftNotFound, Guid.Empty);
                    }
                }
                Guid? responsible = request.ResponsibleEmployeeId is { } eid && eid != Guid.Empty ? eid : null;
                if (responsible != null && !await db.Employee.AsNoTracking().AnyAsync(e => e.Id == responsible))
                {
                    return (string.Format(ConstantHelper.Message.StageEmployeeNotFound, responsible), Guid.Empty);
                }
                var batch = await CleaningRules.NewBatchAsync(_unitOfWork, _documentNumberService, now, inspection.Id, quantity, responsible, shift);
                if (await FoodSafetyRules.ByCodeAsync(db, _rules.CleaningChecklistCode) == null)
                {
                    warnings.Add(string.Format(ConstantHelper.Message.CleaningChecklistMissing, _rules.CleaningChecklistCode));
                }
                return (null, batch.Id);
            });

        /// <summary>
        /// BE2.5 (spec 9.4 results, Catalog 168; BUSINESS_RULES §8 "lô vệ sinh 6 bước"): ghi kết quả các bước của mẻ Đang vệ sinh (checklist VS-KHAY) – mỗi bước 1 dòng mới
        /// (giữ lịch sử, dòng mới nhất là kết quả hiện tại). Đi lần lượt: bước chỉ ghi được khi mọi bước bắt buộc đứng trước đã ĐẠT; FAIL bắt buộc hành động khắc phục;
        /// bước khử khuẩn (<c>CleaningChemicalRequiredItems</c>) ĐẠT phải ghi hóa chất.
        /// </summary>
        public Task<IResult<CleaningBatchDetailModel>> RecordResults(CleaningRecordRequest request)
            => RunAsync("RecordResults", async (actor, now, warnings) =>
            {
                var db = _unitOfWork.DbContext;
                var batch = await db.CleaningBatch.AsNoTracking().FirstOrDefaultAsync(b => b.Id == request.Id && !b.IsDeleted);
                if (batch == null)
                {
                    return (ConstantHelper.Message.CleaningBatchNotFound, Guid.Empty);
                }
                if (batch.Status != CleaningBatchStatus.InProgress)
                {
                    return (InvalidStatus(batch, "ghi bước vệ sinh"), Guid.Empty);
                }
                var checklist = await CleaningRules.ChecklistAsync(db, _rules, request.ChecklistId);
                if (checklist == null)
                {
                    return (request.ChecklistId != null ? ConstantHelper.Message.ChecklistNotFound
                        : string.Format(ConstantHelper.Message.CleaningChecklistMissing, _rules.CleaningChecklistCode), Guid.Empty);
                }
                if (request.ListItem.Count == 0)
                {
                    return (ConstantHelper.Message.ChecklistNoItems, Guid.Empty);
                }
                var items = await FoodSafetyRules.ItemsAsync(db, checklist.Id);
                var existing = FoodSafetyRules.Latest(await CleaningRules.ResultsAsync(db, batch.Id), r => r.ChecklistItemId);
                var state = existing.ToDictionary(x => x.Key, x => x.Value.ResultCode);
                var rows = new List<(FoodSafetyChecklistItem Item, string Code, CleaningRecordItemRequest Input)>();
                foreach (var input in request.ListItem)
                {
                    var item = input.ChecklistItemId is { } iid && iid != Guid.Empty
                        ? items.FirstOrDefault(i => i.Id == iid)
                        : items.FirstOrDefault(i => string.Equals(i.ItemCode, input.ItemCode?.Trim(), StringComparison.OrdinalIgnoreCase));
                    string label = input.ItemCode ?? input.ChecklistItemId?.ToString() ?? string.Empty;
                    if (item == null)
                    {
                        return (string.Format(ConstantHelper.Message.ChecklistItemNotInChecklist, label, checklist.Code), Guid.Empty);
                    }
                    string name = item.ItemName ?? item.ItemCode ?? label;
                    if (rows.Any(r => r.Item.Id == item.Id))
                    {
                        return (string.Format(ConstantHelper.Message.ChecklistItemDuplicate, name), Guid.Empty);
                    }
                    string? code = FoodSafetyCode.NormalizeResult(input.ResultCode);
                    if (code == null)
                    {
                        return (string.Format(ConstantHelper.Message.ChecklistResultInvalid, name), Guid.Empty);
                    }
                    if (code == FoodSafetyCode.ResultFail && string.IsNullOrWhiteSpace(input.CorrectiveAction))
                    {
                        return (string.Format(ConstantHelper.Message.ChecklistFailNeedsCorrective, name), Guid.Empty);
                    }
                    if (code == FoodSafetyCode.ResultPass && string.IsNullOrWhiteSpace(input.ChemicalUsed)
                        && _rules.CleaningChemicalRequiredItems.Contains(item.ItemCode ?? string.Empty, StringComparer.OrdinalIgnoreCase))
                    {
                        return (string.Format(ConstantHelper.Message.CleaningChemicalRequired, name), Guid.Empty);
                    }
                    rows.Add((item, code, input));
                }
                // Đi lần lượt các bước (theo SortOrder): bước bắt buộc đứng trước phải ĐẠT (đã ghi trước hoặc trong cùng lần ghi).
                foreach (var row in rows.OrderBy(r => r.Item.SortOrder).ThenBy(r => r.Item.ItemCode, StringComparer.Ordinal))
                {
                    int index = items.FindIndex(i => i.Id == row.Item.Id);
                    var blocking = items.Take(index).FirstOrDefault(i => i.IsRequired && state.GetValueOrDefault(i.Id) != FoodSafetyCode.ResultPass);
                    if (blocking != null)
                    {
                        return (string.Format(ConstantHelper.Message.CleaningStepOrder, row.Item.ItemName ?? row.Item.ItemCode, blocking.ItemName ?? blocking.ItemCode), Guid.Empty);
                    }
                    state[row.Item.Id] = row.Code;
                }
                foreach (var (item, code, input) in rows)
                {
                    _unitOfWork.CleaningChecklistResultRepository.Add(new CleaningChecklistResult
                    {
                        Id = Guid.NewGuid(),
                        CleaningBatchId = batch.Id,
                        WorkstationId = request.WorkstationId is { } w && w != Guid.Empty ? w : null,
                        ChecklistId = checklist.Id,
                        ChecklistItemId = item.Id,
                        ResultCode = code,
                        ActualValue = FoodSafetyRules.Clean(input.ActualValue),
                        CheckedAt = FoodSafetyRules.NextCheckedAt(now, existing.GetValueOrDefault(item.Id)?.CheckedAt),
                        CheckedBy = actor.UserId,
                        ChemicalUsed = FoodSafetyRules.Clean(input.ChemicalUsed),
                        CorrectiveAction = FoodSafetyRules.Clean(input.CorrectiveAction),
                        EvidenceUrl = FoodSafetyRules.Clean(input.EvidenceUrl),
                    });
                    if (code == FoodSafetyCode.ResultFail)
                    {
                        warnings.Add($"Bước {item.ItemName ?? item.ItemCode} KHÔNG ĐẠT – làm lại bước này trước khi sang bước sau");
                    }
                }
                return (null, batch.Id);
            });

        /// <summary>
        /// BE2.5 (spec 9.4 complete, Catalog 169): kết luận mẻ Đang vệ sinh → Hoàn tất (EndDate). PASS: mọi bước bắt buộc phải ĐẠT → sổ bao bì AVAILABLE
        /// (số khay sạch sẵn sàng dùng lại, PROPOSED spec). REJECT: lý do bắt buộc, ghi KHÔNG ĐẠT ở bước kiểm tra cuối (lý do = hành động khắc phục);
        /// số khay trở lại "chờ vệ sinh" của phiếu kiểm tra (lập mẻ mới bằng CleaningBatch/Create).
        /// </summary>
        public Task<IResult<CleaningBatchDetailModel>> Complete(CleaningCompleteRequest request)
            => RunAsync("Complete", (actor, now, warnings) => CompleteCoreAsync(request, actor, now, warnings));

        /// <summary>Thân kết luận mẻ (dùng chung Complete và PassAll – UI-6.32), trong giao dịch đang mở.</summary>
        private async Task<(string? Error, Guid BatchId)> CompleteCoreAsync(CleaningCompleteRequest request, ActorInfo actor, DateTime now, List<string> warnings)
            {
                var db = _unitOfWork.DbContext;
                var batch = _unitOfWork.CleaningBatchRepository.GetSingle(b => b.Id == request.Id && !b.IsDeleted);
                if (batch == null)
                {
                    return (ConstantHelper.Message.CleaningBatchNotFound, Guid.Empty);
                }
                if (batch.Status != CleaningBatchStatus.InProgress)
                {
                    return (InvalidStatus(batch, "kết luận mẻ vệ sinh"), Guid.Empty);
                }
                string? qc = request.QcResult?.Trim().ToUpperInvariant() switch
                {
                    "PASS" or "DAT" or "OK" => CleaningBatchStatus.QcPass,
                    "REJECT" or "FAIL" or "KHONG_DAT" => CleaningBatchStatus.QcReject,
                    _ => null,
                };
                if (qc == null)
                {
                    return (ConstantHelper.Message.CleaningQcInvalid, Guid.Empty);
                }
                var detail = await CleaningRules.DetailAsync(db, _rules, batch.Id);
                if (detail!.ChecklistId == null)
                {
                    return (string.Format(ConstantHelper.Message.CleaningChecklistMissing, _rules.CleaningChecklistCode), Guid.Empty);
                }
                string number = CleaningRules.Number(batch);
                if (qc == CleaningBatchStatus.QcPass)
                {
                    var notPassed = detail.ListStep.Where(s => s.Item.IsRequired && s.Result?.ResultCode != FoodSafetyCode.ResultPass).Select(s => s.Item.ItemName ?? s.Item.ItemCode).ToList();
                    if (notPassed.Count != 0)
                    {
                        return (string.Format(ConstantHelper.Message.CleaningNotAllPassed, number, string.Join(", ", notPassed)), Guid.Empty);
                    }
                    string type = detail.PackagingType ?? _rules.PackagingDefaultType;
                    PackagingRules.Add(_unitOfWork, actor, now, PackagingTransactionType.Available, type, batch.ContainerQuantity, null, null, PackagingCondition.Clean,
                        $"Mẻ vệ sinh {number} đạt" + (detail.CustomerName == null ? string.Empty : $" – thu hồi từ {detail.CustomerName} ({detail.DeliveryNumber})"), number);
                    warnings.Add(string.Format(ConstantHelper.Message.CleaningAvailableRecorded, batch.ContainerQuantity, type));
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(request.Note))
                    {
                        return (ConstantHelper.Message.CleaningRejectReasonRequired, Guid.Empty);
                    }
                    var last = detail.ListStep.LastOrDefault();
                    if (last != null)
                    {
                        _unitOfWork.CleaningChecklistResultRepository.Add(new CleaningChecklistResult
                        {
                            Id = Guid.NewGuid(),
                            CleaningBatchId = batch.Id,
                            ChecklistId = detail.ChecklistId,
                            ChecklistItemId = last.Item.Id,
                            ResultCode = FoodSafetyCode.ResultFail,
                            ActualValue = "QC kết luận Không đạt",
                            CheckedAt = FoodSafetyRules.NextCheckedAt(now, last.Result?.CheckedAt),
                            CheckedBy = actor.UserId,
                            CorrectiveAction = request.Note.Trim(),
                        });
                    }
                    warnings.Add(string.Format(ConstantHelper.Message.CleaningRejectedWarning, number, batch.ContainerQuantity));
                }
                batch.Status = CleaningBatchStatus.Completed;
                batch.QcResult = qc;
                batch.QualityStatus = qc;
                batch.EndDate = now;
                batch.EndTime = now;
                _unitOfWork.CleaningBatchRepository.Update(batch);
                return (null, batch.Id);
            }

        private static string InvalidStatus(CleaningBatch batch, string action)
            => string.Format(ConstantHelper.Message.StatusActionInvalid, action, "mẻ vệ sinh " + CleaningRules.Number(batch),
                CleaningBatchStatus.Label(CleaningBatchStatus.InProgress), CleaningBatchStatus.Label(batch.Status));
    }
}
