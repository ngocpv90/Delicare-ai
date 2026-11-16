// Sinh bởi tools/AppDelicare.CodeGen (T05), nay là file của dev (T12: duyệt 1 cấp) – CodeGen không ghi đè.
#nullable enable
using AutoMapper;
using Dapper;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Model.Model;
using Microsoft.EntityFrameworkCore;
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
    public class ApprovalRequestService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper, IReferenceGuard referenceGuard
        , IServiceProvider serviceProvider, IPermissionService permissionService, ICurrentActor currentActor, IClock clock
        , IDocumentNumberService documentNumberService) : IApprovalRequestService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IReferenceGuard _referenceGuard = referenceGuard;
        // T12: handler lấy lúc dùng (tránh vòng phụ thuộc RecipeService → ApprovalRequestService → handler).
        private readonly IServiceProvider _serviceProvider = serviceProvider;
        private readonly IPermissionService _permissionService = permissionService;
        private readonly ICurrentActor _currentActor = currentActor;
        private readonly IClock _clock = clock;
        private readonly IDocumentNumberService _documentNumberService = documentNumberService;

        public async Task<IResults<ApprovalRequestViewModel>> GetAll(ApprovalRequestSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@BusinessType", request.BusinessType);
                dynamicParameters.Add("@BusinessId", request.BusinessId);
                dynamicParameters.Add("@RequestStatus", request.RequestStatus);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<ApprovalRequestViewModel>("uspApprovalRequest_GetAll", dynamicParameters)).ToList();
                int i = 0;
                list.ForEach(x =>
                {
                    i++;
                    x.STT = (request.Page - 1) * request.PageSize + i;
                });
                return new Results<ApprovalRequestViewModel>
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

        public IResults<ApprovalRequest> GetAllData()
        {
            try
            {
                var list = _unitOfWork.ApprovalRequestRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<ApprovalRequest>
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

        public async Task<IResult<bool>> Save(ApprovalRequestRequest request, string typeSave)
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

        private async Task<string> Insert(ApprovalRequestRequest request, string typeSave)
        {
            if (_unitOfWork.ApprovalRequestRepository.GetSingle(x => x.RequestNo == request.RequestNo && !x.IsDeleted) != null)
            {
                return "Dữ liệu trùng (RequestNo) – vi phạm UQ_ApprovalRequest_RequestNo.";
            }
            var insert = _mapper.Map<ApprovalRequest>(request);
            if (request.RequestStatus == null) insert.RequestStatus = "Pending";
            var entity = insert;
            _unitOfWork.ApprovalRequestRepository.Add(insert);
            await _unitOfWork.ApprovalRequestRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Update(ApprovalRequestRequest request, string typeSave)
        {
            var exist = _unitOfWork.ApprovalRequestRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            if (_unitOfWork.ApprovalRequestRepository.GetSingle(x => x.Id != request.Id && x.RequestNo == request.RequestNo && !x.IsDeleted) != null)
            {
                return "Dữ liệu trùng (RequestNo) – vi phạm UQ_ApprovalRequest_RequestNo.";
            }
            // T12: trạng thái, nghiệp vụ, nội dung, mã chỉ đổi qua Decide – CRUD không sửa được.
            var keep = (exist.RequestStatus, exist.BusinessType, exist.BusinessId, exist.PayloadJson, exist.RequestNo, exist.CompletedAt, exist.CurrentStepNo);
            var entity = _mapper.Map(request, exist);
            (entity.RequestStatus, entity.BusinessType, entity.BusinessId, entity.PayloadJson, entity.RequestNo, entity.CompletedAt, entity.CurrentStepNo) = keep;
            _unitOfWork.ApprovalRequestRepository.Update(entity);
            await _unitOfWork.ApprovalRequestRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // T07: bản ghi đang được tham chiếu (quan hệ logic, truy vấn có tham số) → không cho xóa.
            if ((await _referenceGuard.FindUsagesAsync("ApprovalRequest", (Guid)id)).Count > 0)
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            var delete = _unitOfWork.ApprovalRequestRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            _unitOfWork.ApprovalRequestRepository.Update(delete);
            await _unitOfWork.ApprovalRequestRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<ApprovalRequest>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.ApprovalRequestRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<ApprovalRequest>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<ApprovalRequest>
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

        // ============================================================ T12 – duyệt 1 cấp (registry IApprovalHandler)
        public const string Pending = "Pending";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";

        private IApprovalHandler? HandlerOf(string? businessType)
            => ((IEnumerable<IApprovalHandler>?)_serviceProvider.GetService(typeof(IEnumerable<IApprovalHandler>)) ?? [])
                .FirstOrDefault(h => h.BusinessType.Equals(businessType, StringComparison.OrdinalIgnoreCase));

        private static string Hash(string? payload)
            => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload ?? string.Empty)));

        /// <summary>
        /// Tạo yêu cầu duyệt (mã XN-) + 1 bước duyệt, lưu nội dung được duyệt. Gọi TRONG transaction của nghiệp vụ gửi duyệt.
        /// Đã có yêu cầu Pending cho cùng nghiệp vụ → trả lỗi.
        /// </summary>
        public async Task<(ApprovalRequest? Request, string? Error)> CreateAsync(string businessType, Guid businessId)
        {
            var handler = HandlerOf(businessType);
            if (handler == null)
            {
                return (null, $"Chưa khai báo xử lý duyệt cho loại {businessType}.");
            }
            if (await _unitOfWork.ApprovalRequestRepository.GetAll().AnyAsync(a => !a.IsDeleted && a.BusinessType == businessType && a.BusinessId == businessId && a.RequestStatus == Pending))
            {
                return (null, ConstantHelper.Message.ApprovalAlreadyPending);
            }
            var actor = _currentActor.RequireActor().UserId;
            var request = new ApprovalRequest
            {
                Id = Guid.NewGuid(),
                RequestNo = await _documentNumberService.NextAsync(DocumentKind.Approval, _clock.Today),
                BusinessType = businessType,
                BusinessId = businessId,
                RequestStatus = Pending,
                CurrentStepNo = 1,
                RequestedAt = _clock.Now,
                RequestedBy = actor,
                CorrelationId = _currentActor.CorrelationId.Length <= 100 ? _currentActor.CorrelationId : _currentActor.CorrelationId[..100],
                PayloadJson = await handler.BuildPayloadAsync(businessId),
            };
            _unitOfWork.ApprovalRequestRepository.Add(request);
            _unitOfWork.ApprovalStepRepository.Add(new ApprovalStep
            {
                Id = Guid.NewGuid(), ApprovalRequestId = request.Id, StepNo = 1, DecisionStatus = "Waiting", AssignedAt = _clock.Now,
            });
            await _unitOfWork.ApprovalRequestRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
            return (request, null);
        }

        /// <summary>Yêu cầu Pending mà người đang đăng nhập được duyệt (quyền duyệt theo màn hình nghiệp vụ; admin thấy hết). type = screen-id để lọc.</summary>
        public async Task<IResults<ApprovalPendingModel>> GetMyPending(string? type)
        {
            try
            {
                var perms = await _permissionService.GetAsync(_currentActor.RequireActor().UserId);
                var handlers = ((IEnumerable<IApprovalHandler>?)_serviceProvider.GetService(typeof(IEnumerable<IApprovalHandler>)) ?? [])
                    .Where(h => string.IsNullOrEmpty(type) || h.ScreenId.Equals(type, StringComparison.OrdinalIgnoreCase))
                    .Where(h => _permissionService.IsAllowed(perms, h.ScreenId, PermissionAction.Approve))
                    .ToList();
                var types = handlers.Select(h => h.BusinessType).ToList();
                var rows = await _unitOfWork.ApprovalRequestRepository.GetAll().AsNoTracking()
                    .Where(a => !a.IsDeleted && a.RequestStatus == Pending && a.BusinessType != null && types.Contains(a.BusinessType))
                    .OrderBy(a => a.RequestedAt).ToListAsync();
                var userIds = rows.Where(r => r.RequestedBy != null).Select(r => r.RequestedBy!.Value).Distinct().ToList();
                var names = await (from u in _unitOfWork.UserRepository.GetAll()
                                   join p in _unitOfWork.PersonRepository.GetAll() on u.PersonId equals p.Id into pp
                                   from p in pp.DefaultIfEmpty()
                                   where userIds.Contains(u.Id)
                                   select new { u.Id, Name = p != null && p.FullName != null ? p.FullName : u.UserName }).ToDictionaryAsync(x => x.Id, x => x.Name);
                var list = new List<ApprovalPendingModel>();
                foreach (var r in rows)
                {
                    var h = handlers.First(x => x.BusinessType.Equals(r.BusinessType, StringComparison.OrdinalIgnoreCase));
                    list.Add(new ApprovalPendingModel
                    {
                        Id = r.Id, RequestNo = r.RequestNo, BusinessType = r.BusinessType, BusinessId = r.BusinessId, ScreenId = h.ScreenId,
                        Title = r.BusinessId is { } bid ? await h.DescribeAsync(bid) : null,
                        RequestedAt = r.RequestedAt, RequestedBy = r.RequestedBy,
                        RequestedByName = r.RequestedBy is { } uid ? names.GetValueOrDefault(uid) : null,
                    });
                }
                return new Results<ApprovalPendingModel>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    ListData = list,
                    TotalRecords = list.Count
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetMyPending: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>
        /// Duyệt / từ chối 1 cấp trong 1 transaction: kiểm quyền duyệt (mục menu "Duyệt – …"), kiểm nội dung không bị sửa sau khi gửi (hash),
        /// ghi ApprovalDecision (chỉ ghi thêm, có PayloadHash), cập nhật bước + yêu cầu, rồi thực thi nghiệp vụ (handler). Lỗi ở bất kỳ bước nào → không ghi gì.
        /// </summary>
        public async Task<IResult<bool>> Decide(ApprovalDecideRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                string? error = await DecideCoreAsync(request);
                if (error != null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<bool>.Fail(error, false);
                }
                _unitOfWork.CommitTransaction();
                return Result<bool>.Success(true, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Decide: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        private async Task<string?> DecideCoreAsync(ApprovalDecideRequest request)
        {
            var approval = _unitOfWork.ApprovalRequestRepository.GetSingle(a => a.Id == request.Id && !a.IsDeleted);
            if (approval == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            if (approval.RequestStatus != Pending)
            {
                return string.Format(ConstantHelper.Message.ApprovalNotPending, approval.RequestStatus);
            }
            var handler = HandlerOf(approval.BusinessType);
            if (handler == null || approval.BusinessId is not { } businessId)
            {
                return $"Chưa khai báo xử lý duyệt cho loại {approval.BusinessType}.";
            }
            Guid actor = _currentActor.RequireActor().UserId;
            var perms = await _permissionService.GetAsync(actor);
            if (!_permissionService.IsAllowed(perms, handler.ScreenId, PermissionAction.Approve))
            {
                return ConstantHelper.Message.ApprovalNoRight;
            }
            string? note = request.Note?.Trim();
            if (!request.Approve && string.IsNullOrEmpty(note))
            {
                return ConstantHelper.Message.RejectReasonRequired;
            }
            string current = await handler.BuildPayloadAsync(businessId) ?? string.Empty;
            string submittedHash = Hash(approval.PayloadJson);
            if (request.Approve && Hash(current) != submittedHash)
            {
                return ConstantHelper.Message.ApprovalPayloadChanged;
            }
            var step = _unitOfWork.ApprovalStepRepository.GetAll().Where(s => s.ApprovalRequestId == approval.Id && !s.IsDeleted)
                .OrderBy(s => s.StepNo).FirstOrDefault(s => s.DecisionStatus == "Waiting");
            if (step == null)
            {
                // Yêu cầu tạo trước T12 (không có bước) → tạo bước 1 để ghi quyết định.
                step = new ApprovalStep { Id = Guid.NewGuid(), ApprovalRequestId = approval.Id, StepNo = 1, AssignedAt = approval.RequestedAt };
                _unitOfWork.ApprovalStepRepository.Add(step);
            }
            else
            {
                _unitOfWork.ApprovalStepRepository.Update(step);
            }
            DateTime now = _clock.Now;
            step.DecisionStatus = request.Approve ? Approved : Rejected;
            step.DecidedAt = now;
            step.DecisionNote = note;
            step.ApproverUserId = actor;
            _unitOfWork.ApprovalDecisionRepository.Add(new ApprovalDecision
            {
                Id = Guid.NewGuid(), ApprovalRequestId = approval.Id, ApprovalStepId = step.Id, DecisionCode = request.Approve ? "APPROVE" : "REJECT",
                DecidedBy = actor, DecidedAt = now, Comment = note, PayloadHash = submittedHash,
            });
            approval.RequestStatus = request.Approve ? Approved : Rejected;
            approval.CompletedAt = now;
            _unitOfWork.ApprovalRequestRepository.Update(approval);
            string? executed = request.Approve
                ? await handler.ApproveAsync(businessId, actor, note)
                : await handler.RejectAsync(businessId, actor, note!);
            if (executed != null)
            {
                return executed;
            }
            await _unitOfWork.ApprovalRequestRepository.CommitAsync(ConstantHelper.TypeSave.Update);
            return null;
        }
    }
}
