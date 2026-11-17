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
    public class ContainerReturnService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService, IDocumentNumberService documentNumberService, IClock clock, ICurrentActor currentActor
        , IOptions<BusinessRulesOptions> rules, IOptions<ReferenceListsOptions> referenceLists) : IContainerReturnService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;
        private readonly IDocumentNumberService _documentNumberService = documentNumberService;
        private readonly IClock _clock = clock;
        private readonly ICurrentActor _currentActor = currentActor;
        private readonly BusinessRulesOptions _rules = rules.Value;
        private readonly ReferenceListsOptions _referenceLists = referenceLists.Value;

        public async Task<IResults<ContainerReturnViewModel>> GetAll(ContainerReturnSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@DeliveryOrderId", request.DeliveryOrderId);
                dynamicParameters.Add("@CustomerId", request.CustomerId);
                dynamicParameters.Add("@ReturnDate", request.ReturnDate);
                //dynamicParameters.Add("@ExpectedQuantity", request.ExpectedQuantity);
                //dynamicParameters.Add("@ReturnedQuantity", request.ReturnedQuantity);
                //dynamicParameters.Add("@DamagedQuantity", request.DamagedQuantity);
                dynamicParameters.Add("@Status", request.Status);
                //dynamicParameters.Add("@Note", request.Note);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<ContainerReturnViewModel>("uspContainerReturn_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<ContainerReturnViewModel>
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

        public IResults<ContainerReturn> GetAllData()
        {
            try
            {
                var list = _unitOfWork.ContainerReturnRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<ContainerReturn>
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

        public async Task<IResult<bool>> Save(ContainerReturnRequest request, string typeSave)
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

        /// <summary>BE2.5: phiếu thu hồi sinh khi bàn giao (Chờ thu hồi) hoặc qua ContainerReturn/Record (ghi sổ bao bì IN) – CRUD Insert không ghi.</summary>
        private static Task<string> Insert(ContainerReturnRequest request, string typeSave)
            => Task.FromResult(string.Format(ConstantHelper.Message.DeliveryCrudBlocked, "Phiếu thu hồi khay / hộp", "ContainerReturn/Record, Inspect"));

        /// <summary>BE2.5: số thu / hỏng / trạng thái gắn sổ bao bì – chỉ đổi qua ContainerReturn/Record, Inspect.</summary>
        private static Task<string> Update(ContainerReturnRequest request, string typeSave)
            => Task.FromResult(string.Format(ConstantHelper.Message.DeliveryCrudBlocked, "Phiếu thu hồi khay / hộp", "ContainerReturn/Record, Inspect"));

        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            var delete = _unitOfWork.ContainerReturnRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // BE2.5: chỉ xóa phiếu Chờ thu hồi (chưa ghi sổ bao bì IN); khay vẫn tính còn ở khách theo sổ bao bì.
            if (delete.Status != ContainerReturnStatus.Expected)
            {
                return string.Format(ConstantHelper.Message.DeliveryCrudBlocked, "Phiếu thu hồi đã ghi sổ bao bì", "PackagingLedger/Record (bút toán điều chỉnh)");
            }
            // T07: ReferenceGuard (quan hệ logic, truy vấn có tham số) thay SQL động uspSystem_ExecuteQuery (A10).
            if (await _auditChangeService.IsReferencedAsync(nameof(ContainerReturn), (Guid)id))
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            _unitOfWork.ContainerReturnRepository.Update(delete);
            await _unitOfWork.ContainerReturnRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<ContainerReturn>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.ContainerReturnRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<ContainerReturn>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<ContainerReturn>
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

        /// <summary>BE2.5 (spec 10.4): chi tiết phiếu thu hồi – khách, đơn giao, loại bao bì, số còn ở khách, kiểm tra, mẻ vệ sinh, sổ bao bì của đơn giao.</summary>
        public async Task<IResult<ContainerReturnDetailModel>> GetDetail(Guid id)
        {
            try
            {
                var detail = await DetailAsync(id);
                return detail == null ? Result<ContainerReturnDetailModel>.Fail(ConstantHelper.Message.ContainerReturnNotFound)
                    : Result<ContainerReturnDetailModel>.Success(detail, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetDetail: ", ex.ToString()));
                throw;
            }
        }

        private async Task<ContainerReturnDetailModel?> DetailAsync(Guid id)
        {
            var db = _unitOfWork.DbContext;
            var cr = await db.ContainerReturn.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
            if (cr == null)
            {
                return null;
            }
            var deliveryOrder = await db.DeliveryOrder.AsNoTracking().FirstOrDefaultAsync(d => d.Id == cr.DeliveryOrderId);
            var customer = cr.CustomerId is { } cid ? await db.Customer.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cid) : null;
            string? number = deliveryOrder?.DeliveryNumber ?? deliveryOrder?.DeliveryNo;
            string? type = PackagingRules.TypeOf(cr.Note);
            var inspections = await db.ReturnInspection.AsNoTracking().Where(r => !r.IsDeleted && r.ContainerReturnId == cr.Id).OrderBy(r => r.InspectionDate).ToListAsync();
            var inspectionIds = inspections.Select(i => i.Id).ToList();
            var detail = new ContainerReturnDetailModel
            {
                Return = cr,
                StatusName = ContainerReturnStatus.Label(cr.Status),
                CustomerName = customer?.DisplayName ?? customer?.Name,
                DeliveryNumber = number,
                PackagingType = type,
                ListInspection = inspections,
                ListCleaningBatch = await db.CleaningBatch.AsNoTracking().Where(b => !b.IsDeleted && inspectionIds.Contains(b.ReturnInspectionId)).OrderBy(b => b.StartDate).ToListAsync(),
                ListLedger = number == null || cr.CustomerId == null ? [] : await db.PackagingLedger.AsNoTracking()
                    .Where(l => !l.IsDeleted && l.CustomerId == cr.CustomerId && l.ReferenceNo == number).OrderBy(l => l.TransactionAt).ToListAsync(),
            };
            if (type != null && cr.CustomerId is { } customerId)
            {
                detail.OutstandingOfCustomer = await PackagingRules.OutstandingAsync(db, customerId, type);
                if (number != null)
                {
                    detail.OutstandingOfDelivery = await PackagingRules.OutstandingAsync(db, customerId, type, number);
                }
            }
            detail.AllowedActions = cr.Status switch
            {
                ContainerReturnStatus.Expected => ["record", "delete"],
                ContainerReturnStatus.Returned => ["inspect"],
                _ => [],
            };
            return detail;
        }

        /// <summary>BE2.5 (spec 10.4 – màn #/log-container-return): phiếu thu hồi theo trạng thái / khách / ngày thu hồi + đếm theo trạng thái.</summary>
        public async Task<IResult<ContainerReturnListModel>> GetList(ContainerReturnListRequest request)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var query = db.ContainerReturn.AsNoTracking().Where(c => !c.IsDeleted);
                if (request.CustomerId is { } cid && cid != Guid.Empty)
                {
                    query = query.Where(c => c.CustomerId == cid);
                }
                if (request.From is { } from)
                {
                    query = query.Where(c => c.ReturnDate >= from.Date);
                }
                if (request.To is { } to)
                {
                    DateTime end = to.Date.AddDays(1);
                    query = query.Where(c => c.ReturnDate < end);
                }
                var all = await query.ToListAsync();
                var counts = all.GroupBy(c => c.Status ?? string.Empty).ToDictionary(g => g.Key, g => g.Count());
                if (!string.IsNullOrWhiteSpace(request.Status))
                {
                    all = [.. all.Where(c => c.Status == request.Status.Trim())];
                }
                int page = Math.Max(1, request.Page), size = Math.Clamp(request.PageSize, 1, 500);
                var paged = all.OrderByDescending(c => c.ReturnDate).ThenByDescending(c => c.CreatedDate).Skip((page - 1) * size).Take(size).ToList();
                var customerIds = paged.Where(c => c.CustomerId != null).Select(c => c.CustomerId!.Value).Distinct().ToList();
                var customers = await db.Customer.AsNoTracking().Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.DisplayName ?? c.Name);
                var deliveryIds = paged.Select(c => c.DeliveryOrderId).Distinct().ToList();
                var deliveries = await db.DeliveryOrder.AsNoTracking().Where(d => deliveryIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.DeliveryNumber ?? d.DeliveryNo);
                var returnIds = paged.Select(c => c.Id).ToList();
                var inspections = (await db.ReturnInspection.AsNoTracking().Where(r => !r.IsDeleted && returnIds.Contains(r.ContainerReturnId)).ToListAsync())
                    .GroupBy(r => r.ContainerReturnId).ToDictionary(g => g.Key, g => g.OrderBy(r => r.InspectionDate).Last());
                return Result<ContainerReturnListModel>.Success(new ContainerReturnListModel
                {
                    TotalRecords = all.Count,
                    StatusCounts = counts,
                    ListItem = [.. paged.Select(c =>
                    {
                        inspections.TryGetValue(c.Id, out var ins);
                        return new ContainerReturnListItemModel
                        {
                            Return = c,
                            StatusName = ContainerReturnStatus.Label(c.Status),
                            CustomerName = c.CustomerId is { } k ? customers.GetValueOrDefault(k) : null,
                            DeliveryNumber = deliveries.GetValueOrDefault(c.DeliveryOrderId),
                            PackagingType = PackagingRules.TypeOf(c.Note),
                            InspectionResult = ins?.InspectionResult,
                            InspectionResultName = ins == null ? null : ReturnInspectionResult.Label(ins.InspectionResult),
                        };
                    })],
                }, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetList: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Khung chung thao tác phiếu thu hồi (1 transaction): người thao tác, chạy thân (lỗi hoặc Id phiếu), lưu, commit, trả chi tiết + cảnh báo.</summary>
        private async Task<IResult<ContainerReturnDetailModel>> RunAsync(string action, Func<ActorInfo, DateTime, List<string>, Task<(string? Error, Guid Id)>> body)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var actor = _currentActor.RequireActor();
                var warnings = new List<string>();
                var (error, id) = await body(actor, _clock.Now, warnings);
                if (error != null)
                {
                    _unitOfWork.RollbackTransaction();
                    _unitOfWork.DbContext.ChangeTracker.Clear();
                    return Result<ContainerReturnDetailModel>.Fail(error);
                }
                await _unitOfWork.ContainerReturnRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                var detail = await DetailAsync(id);
                detail!.ListWarning.InsertRange(0, warnings);
                return Result<ContainerReturnDetailModel>.Success(detail, ConstantHelper.Message.SaveSuccess);
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
        /// BE2.5 (spec 10.4 POST container-returns, Catalog 194; PROPOSED D-54 b): ghi thu hồi khay / hộp của đơn giao Đã giao – khách lấy từ đơn giao; loại bao bì = gửi lên
        /// hoặc loại đã giao (OUT) của đơn giao; số phải thu = gửi lên hoặc số còn ở khách của đơn giao. Không thu quá số khay loại đó còn ở khách.
        /// Phiếu Chờ thu hồi (sinh lúc bàn giao) → Đã thu hồi; không có thì lập phiếu mới. Sổ bao bì IN: phần tốt (GOOD) + phần hỏng (DAMAGED) – sổ chỉ ghi thêm.
        /// </summary>
        public Task<IResult<ContainerReturnDetailModel>> Record(ContainerReturnRecordRequest request)
            => RunAsync("Record", async (actor, now, warnings) =>
            {
                var db = _unitOfWork.DbContext;
                var deliveryOrder = await db.DeliveryOrder.AsNoTracking().FirstOrDefaultAsync(d => d.Id == request.DeliveryOrderId && !d.IsDeleted);
                if (deliveryOrder == null)
                {
                    return (ConstantHelper.Message.DeliveryOrderNotFound, Guid.Empty);
                }
                if (deliveryOrder.Status != DeliveryOrderStatus.Delivered)
                {
                    return (string.Format(ConstantHelper.Message.ContainerDeliveryNotDelivered, DeliveryOrderStatus.Label(deliveryOrder.Status)), Guid.Empty);
                }
                int returned = request.ReturnedQuantity, damaged = request.DamagedQuantity ?? 0;
                if (returned <= 0 || damaged < 0 || damaged > returned || request.ExpectedQuantity is < 0)
                {
                    return (ConstantHelper.Message.ContainerQuantityInvalid, Guid.Empty);
                }
                string number = deliveryOrder.DeliveryNumber ?? deliveryOrder.DeliveryNo ?? deliveryOrder.Id.ToString("N")[..8];
                string? type = DeliveryRules.Clean(request.PackagingType);
                if (type == null)
                {
                    var types = await PackagingRules.DeliveredTypesAsync(db, deliveryOrder.CustomerId, number);
                    if (types.Count == 0)
                    {
                        return (ConstantHelper.Message.ContainerPackagingUnknown, Guid.Empty);
                    }
                    if (types.Count > 1)
                    {
                        return (string.Format(ConstantHelper.Message.ContainerPackagingAmbiguous, string.Join(", ", types)), Guid.Empty);
                    }
                    type = types[0];
                }
                decimal atCustomer = await PackagingRules.OutstandingAsync(db, deliveryOrder.CustomerId, type);
                var customer = await db.Customer.AsNoTracking().FirstOrDefaultAsync(c => c.Id == deliveryOrder.CustomerId);
                string customerName = customer?.DisplayName ?? customer?.Name ?? string.Empty;
                if (returned > atCustomer)
                {
                    return (string.Format(ConstantHelper.Message.ContainerOverOutstanding, customerName, PackagingRules.Num(Math.Max(0, atCustomer)), type, returned), Guid.Empty);
                }
                decimal ofDelivery = await PackagingRules.OutstandingAsync(db, deliveryOrder.CustomerId, type, number);
                int expected = request.ExpectedQuantity ?? (int)Math.Max(0, ofDelivery);
                if (returned > expected)
                {
                    warnings.Add(string.Format(ConstantHelper.Message.ContainerMoreThanExpected, returned, expected));
                }
                var cr = _unitOfWork.ContainerReturnRepository.GetSingle(c => c.DeliveryOrderId == deliveryOrder.Id && !c.IsDeleted && c.Status == ContainerReturnStatus.Expected);
                if (cr == null)
                {
                    cr = new ContainerReturn { Id = Guid.NewGuid(), DeliveryOrderId = deliveryOrder.Id };
                    _unitOfWork.ContainerReturnRepository.Add(cr);
                }
                else
                {
                    _unitOfWork.ContainerReturnRepository.Update(cr);
                }
                cr.CustomerId = deliveryOrder.CustomerId;
                cr.ReturnDate = request.ReturnDate ?? now;
                cr.ExpectedQuantity = expected;
                cr.ReturnedQuantity = returned;
                cr.DamagedQuantity = damaged;
                cr.Quantity = returned;
                cr.Status = ContainerReturnStatus.Returned;
                cr.Note = PackagingRules.WithType(DeliveryRules.Clean(request.Note) ?? PackagingRules.NoteText(cr.Note), type);
                string note = $"Thu hồi đơn giao {number}" + (DeliveryRules.Clean(request.Note) is { } n ? " – " + n : string.Empty);
                if (returned - damaged > 0)
                {
                    PackagingRules.Add(_unitOfWork, actor, now, PackagingTransactionType.In, type, returned - damaged, deliveryOrder, null, PackagingCondition.Good, note);
                }
                if (damaged > 0)
                {
                    PackagingRules.Add(_unitOfWork, actor, now, PackagingTransactionType.In, type, damaged, deliveryOrder, null, PackagingCondition.Damaged, note + " – hỏng");
                }
                decimal left = ofDelivery - returned;
                if (left > 0)
                {
                    warnings.Add(string.Format(ConstantHelper.Message.ContainerStillOutstanding, PackagingRules.Num(left), type, number));
                }
                return (null, cr.Id);
            });

        /// <summary>
        /// BE2.5 (spec 10.4 POST container-returns/{id}/inspect, Catalog 195): kiểm tra vật dụng thu hồi của phiếu Đã thu hồi → ReturnInspection (InspectionDate = bây giờ),
        /// phiếu → Đã kiểm tra. Kết quả OK / SHORTAGE / DAMAGED / DIRTY / REJECTED (ReferenceLists ReturnInspectionResult); khác OK bắt buộc ghi chú; số "bẩn" ghi qua kết quả DIRTY
        /// (GAP-C-05). Khay dùng được (trả về − hỏng; REJECTED = 0) → tự lập mẻ vệ sinh VS- (<c>CleaningAutoCreateOnInspect</c>).
        /// </summary>
        public Task<IResult<ContainerReturnDetailModel>> Inspect(ContainerReturnInspectRequest request)
            => RunAsync("Inspect", async (actor, now, warnings) =>
            {
                var db = _unitOfWork.DbContext;
                var cr = _unitOfWork.ContainerReturnRepository.GetSingle(c => c.Id == request.Id && !c.IsDeleted);
                if (cr == null)
                {
                    return (ConstantHelper.Message.ContainerReturnNotFound, Guid.Empty);
                }
                if (cr.Status != ContainerReturnStatus.Returned)
                {
                    return (string.Format(ConstantHelper.Message.StatusActionInvalid, "kiểm tra vật dụng thu hồi", "phiếu thu hồi",
                        ContainerReturnStatus.Label(ContainerReturnStatus.Returned), ContainerReturnStatus.Label(cr.Status)), Guid.Empty);
                }
                string result = request.InspectionResult.Trim().ToUpperInvariant();
                var allowed = _referenceLists.Lists.TryGetValue("ReturnInspectionResult", out var list) && list.Count != 0
                    ? list.Select(x => x.Code.ToUpperInvariant()).ToArray() : ReturnInspectionResult.All;
                if (!allowed.Contains(result))
                {
                    return (string.Format(ConstantHelper.Message.ContainerInspectResultInvalid, string.Join(", ", allowed)), Guid.Empty);
                }
                decimal damaged = request.DamagedQty ?? cr.DamagedQuantity;
                decimal missing = request.MissingQty ?? Math.Max(0, cr.ExpectedQuantity - cr.ReturnedQuantity);
                if (damaged < 0 || damaged > cr.ReturnedQuantity || missing < 0)
                {
                    return (ConstantHelper.Message.ContainerInspectQtyInvalid, Guid.Empty);
                }
                string? note = DeliveryRules.Clean(request.Note);
                if (result != ReturnInspectionResult.Ok && note == null)
                {
                    return (string.Format(ConstantHelper.Message.ContainerInspectNoteRequired, ReturnInspectionResult.Label(result)), Guid.Empty);
                }
                foreach (var eid in new[] { request.InspectorEmployeeId, request.ResponsibleEmployeeId })
                {
                    if (eid is { } e && e != Guid.Empty && !await db.Employee.AsNoTracking().AnyAsync(x => x.Id == e))
                    {
                        return (string.Format(ConstantHelper.Message.StageEmployeeNotFound, e), Guid.Empty);
                    }
                }
                var inspection = new ReturnInspection
                {
                    Id = Guid.NewGuid(),
                    ContainerReturnId = cr.Id,
                    InspectionResult = result,
                    Result = result,
                    DamagedQty = damaged,
                    MissingQty = missing,
                    Remark = note,
                    Note = note,
                    InspectionDate = now,
                    InspectorEmployeeId = request.InspectorEmployeeId is { } i && i != Guid.Empty ? i : null,
                };
                _unitOfWork.ReturnInspectionRepository.Add(inspection);
                cr.Status = ContainerReturnStatus.Inspected;
                _unitOfWork.ContainerReturnRepository.Update(cr);
                var (usable, remaining) = await CleaningRules.UsableAsync(db, inspection, cr);
                if (_rules.CleaningAutoCreateOnInspect && remaining > 0)
                {
                    var batch = await CleaningRules.NewBatchAsync(_unitOfWork, _documentNumberService, now, inspection.Id, remaining,
                        request.ResponsibleEmployeeId is { } r && r != Guid.Empty ? r : null, null);
                    warnings.Add(string.Format(ConstantHelper.Message.ContainerCleaningCreated, CleaningRules.Number(batch), remaining));
                }
                else if (usable == 0)
                {
                    warnings.Add(ConstantHelper.Message.ContainerNoUsable);
                }
                var deliveryOrder = await db.DeliveryOrder.AsNoTracking().FirstOrDefaultAsync(d => d.Id == cr.DeliveryOrderId);
                string? type = PackagingRules.TypeOf(cr.Note);
                string? number = deliveryOrder?.DeliveryNumber ?? deliveryOrder?.DeliveryNo;
                if (missing > 0 && type != null && number != null && cr.CustomerId is { } cid
                    && await PackagingRules.OutstandingAsync(db, cid, type, number) is var left and > 0)
                {
                    warnings.Add(string.Format(ConstantHelper.Message.ContainerStillOutstanding, PackagingRules.Num(left), type, number));
                }
                return (null, cr.Id);
            });
    }
}
