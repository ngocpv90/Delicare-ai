// Sinh bởi tools/AppDelicare.CodeGen (T05), nay là file của dev (BE2.6: đơn đổi ca / nhường ca + duyệt qua engine SHIFT_SWAP, hủy) – CodeGen không ghi đè.
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
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Serilog;
using System.Net;

namespace AppDelicare.Services.Implement
{
    public partial class ShiftSwapRequestService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper, IReferenceGuard referenceGuard
        , IClock clock, ICurrentActor currentActor, IPermissionService permissionService, IDocumentNumberService documentNumberService
        , IApprovalRequestService approvalRequestService, IServiceProvider services) : IShiftSwapRequestService
    {
        /// <summary>Hoán đổi 2 ca.</summary>
        public const string TypeSwap = "SWAP";
        /// <summary>Nhường ca (không nhận lại ca nào).</summary>
        public const string TypeGive = "GIVE";

        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IReferenceGuard _referenceGuard = referenceGuard;
        private readonly IClock _clock = clock;
        private readonly ICurrentActor _currentActor = currentActor;
        private readonly IPermissionService _permissionService = permissionService;
        private readonly IDocumentNumberService _documentNumberService = documentNumberService;
        private readonly IApprovalRequestService _approvalRequestService = approvalRequestService;
        private readonly IServiceProvider _services = services;

        public async Task<IResults<ShiftSwapRequestViewModel>> GetAll(ShiftSwapRequestSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@RequesterEmployeeId", request.RequesterEmployeeId);
                dynamicParameters.Add("@TargetEmployeeId", request.TargetEmployeeId);
                dynamicParameters.Add("@CurrentShiftAssignmentId", request.CurrentShiftAssignmentId);
                dynamicParameters.Add("@ProposedShiftAssignmentId", request.ProposedShiftAssignmentId);
                dynamicParameters.Add("@RequestStatus", request.RequestStatus);
                dynamicParameters.Add("@ApprovalRequestId", request.ApprovalRequestId);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<ShiftSwapRequestViewModel>("uspShiftSwapRequest_GetAll", dynamicParameters)).ToList();
                int i = 0;
                list.ForEach(x =>
                {
                    i++;
                    x.STT = (request.Page - 1) * request.PageSize + i;
                });
                return new Results<ShiftSwapRequestViewModel>
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

        public IResults<ShiftSwapRequest> GetAllData()
        {
            try
            {
                var list = _unitOfWork.ShiftSwapRequestRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<ShiftSwapRequest>
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

        public async Task<IResult<bool>> Save(ShiftSwapRequestRequest request, string typeSave)
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

        /// <summary>BE2.6: đơn đổi ca chỉ tạo qua <c>ShiftSwapRequest/Create</c> (kiểm 2 ca, gửi duyệt SHIFT_SWAP).</summary>
        private Task<string> Insert(ShiftSwapRequestRequest request, string typeSave) => Task.FromResult(ConstantHelper.Message.HrSwapCrudBlocked);

        private Task<string> Update(ShiftSwapRequestRequest request, string typeSave) => Task.FromResult(ConstantHelper.Message.HrSwapCrudBlocked);

        /// <summary>BE2.6: chỉ xóa (mềm) đơn đã Từ chối / Hủy.</summary>
        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            var delete = _unitOfWork.ShiftSwapRequestRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            if (delete.RequestStatus is not (ShiftSwapStatus.Rejected or ShiftSwapStatus.Cancelled))
            {
                return string.Format(ConstantHelper.Message.StatusActionInvalid, "xóa", "đơn đổi ca",
                    ShiftSwapStatus.Label(ShiftSwapStatus.Rejected) + " / " + ShiftSwapStatus.Label(ShiftSwapStatus.Cancelled), ShiftSwapStatus.Label(delete.RequestStatus));
            }
            _unitOfWork.ShiftSwapRequestRepository.Update(delete);
            await _unitOfWork.ShiftSwapRequestRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<ShiftSwapRequest>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.ShiftSwapRequestRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<ShiftSwapRequest>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<ShiftSwapRequest>
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

        // ============================================================ BE2.6 – đơn đổi ca (spec 11.3, Catalog 208, 209)

        /// <summary>
        /// Kiểm 2 ca của đơn (lúc tạo và lại lúc duyệt): ca còn hiệu lực, Kế hoạch / Xác nhận, chưa chấm công, không nằm trong đơn đổi ca chờ duyệt khác; ca hiện tại của người xin,
        /// ca đề xuất của người nhận; sau khi đổi mỗi người không có 2 ca / ngày và không trùng ngày nghỉ phép Đã duyệt. Trả lỗi tiếng Việt hoặc null.
        /// </summary>
        private async Task<string?> ValidateAsync(ShiftAssignment current, ShiftAssignment? proposed, Guid requesterId, Guid targetId, Guid? swapId)
        {
            var db = _unitOfWork.DbContext;
            var rows = proposed == null ? new List<ShiftAssignment> { current } : [current, proposed];
            var ids = rows.Select(r => r.Id).ToList();
            var attended = await HrRules.AttendedAssignmentsAsync(db, ids);
            var shifts = await db.Shift.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Code);
            foreach (var r in rows)
            {
                if (r.IsDeleted)
                {
                    return ConstantHelper.Message.HrSwapAssignmentNotFound;
                }
                if (attended.Contains(r.Id) || !ShiftAssignmentStatus.Open.Contains(r.AssignmentStatus))
                {
                    return string.Format(ConstantHelper.Message.HrSwapAttended, shifts.GetValueOrDefault(r.ShiftId), HrRules.Day(r.WorkDate), ShiftAssignmentStatus.Label(r.AssignmentStatus));
                }
            }
            if (current.EmployeeId != requesterId || (proposed != null && proposed.EmployeeId != targetId))
            {
                return ConstantHelper.Message.HrSwapOwnerChanged;
            }
            bool otherPending = await db.ShiftSwapRequest.AsNoTracking().AnyAsync(s => !s.IsDeleted && (s.RequestStatus == ShiftSwapStatus.Pending || s.RequestStatus == ShiftSwapStatus.AwaitingConsent) && s.Id != swapId
                && (ids.Contains(s.CurrentShiftAssignmentId) || (s.ProposedShiftAssignmentId != null && ids.Contains(s.ProposedShiftAssignmentId.Value))));
            if (otherPending)
            {
                return ConstantHelper.Message.HrSwapOtherPending;
            }
            var names = await db.Employee.AsNoTracking().Where(e => e.Id == requesterId || e.Id == targetId).ToDictionaryAsync(e => e.Id, e => e.FullName);
            // Người nhận vào ngày của ca hiện tại / người xin vào ngày của ca đề xuất: không có ca khác, không nghỉ phép.
            var checks = new List<(Guid Employee, ShiftAssignment Incoming, Guid? Leaving)> { (targetId, current, proposed?.Id) };
            if (proposed != null)
            {
                checks.Add((requesterId, proposed, current.Id));
            }
            foreach (var (employeeId, incoming, leaving) in checks)
            {
                DateTime day = incoming.WorkDate.Date;
                bool busy = await db.ShiftAssignment.AsNoTracking().AnyAsync(a => !a.IsDeleted && a.EmployeeId == employeeId && a.WorkDate == day && a.Id != leaving && a.Id != incoming.Id);
                if (busy)
                {
                    return string.Format(ConstantHelper.Message.HrSwapTargetBusy, names.GetValueOrDefault(employeeId), HrRules.Day(day));
                }
                if ((await HrRules.LeavesAsync(db, [employeeId], day, day, true)).FirstOrDefault() is { } leave)
                {
                    return string.Format(ConstantHelper.Message.HrAssignmentOnLeave, names.GetValueOrDefault(employeeId), HrRules.Day(day), leave.RequestNo);
                }
            }
            return null;
        }

        /// <summary>
        /// Spec 11.3 POST shift-swap-requests (Catalog 208) – 1 transaction. Người xin = nhân viên của ca hiện tại (tài khoản của chính nhân viên đó, hoặc người có quyền Thêm trên
        /// <c>#/hr-swap</c>); có ca đề xuất → hoán đổi (người nhận = nhân viên của ca đề xuất), không có → nhường ca cho <c>TargetEmployeeId</c>. Kiểm theo <see cref="ValidateAsync"/>,
        /// lý do bắt buộc. Số <c>PH-YYMMDD-NNNN</c> (PROPOSED – D-55 (h)), Chờ duyệt, ApprovalRequest SHIFT_SWAP (mặc định 1 cấp mục <c>#/wf-inbox?type=hr-swap</c>).
        /// Người nhận đồng ý: làm ngoài hệ thống, người duyệt xác nhận (PROPOSED – D-55 (i)).
        /// </summary>
        public async Task<IResult<ShiftSwapDetailModel>> Create(ShiftSwapCreateRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var db = _unitOfWork.DbContext;
                Guid actor = _currentActor.RequireActor().UserId;
                var current = await db.ShiftAssignment.AsNoTracking().FirstOrDefaultAsync(a => a.Id == request.CurrentShiftAssignmentId && !a.IsDeleted);
                if (current == null)
                {
                    return Fail(ConstantHelper.Message.HrSwapAssignmentNotFound);
                }
                var self = await HrRules.ResolveEmployeeAsync(db, actor);
                if (self?.Id != current.EmployeeId && !_permissionService.IsAllowed(await _permissionService.GetAsync(actor), HrScreen.Swap, PermissionAction.Add))
                {
                    return Fail(string.Format(ConstantHelper.Message.HrNoRightOther, "lập đơn đổi ca", "Đơn đổi ca"));
                }
                ShiftAssignment? proposed = null;
                Guid targetId;
                if (request.ProposedShiftAssignmentId is { } pid && pid != Guid.Empty)
                {
                    proposed = await db.ShiftAssignment.AsNoTracking().FirstOrDefaultAsync(a => a.Id == pid && !a.IsDeleted);
                    if (proposed == null || proposed.Id == current.Id)
                    {
                        return Fail(ConstantHelper.Message.HrSwapAssignmentNotFound);
                    }
                    if (request.TargetEmployeeId is { } t && t != Guid.Empty && t != proposed.EmployeeId)
                    {
                        return Fail(ConstantHelper.Message.HrSwapTargetMismatch);
                    }
                    targetId = proposed.EmployeeId;
                    if (proposed.ShiftId == current.ShiftId && proposed.WorkDate.Date == current.WorkDate.Date)
                    {
                        return Fail(ConstantHelper.Message.HrSwapSameShift);
                    }
                }
                else
                {
                    if (request.TargetEmployeeId is not { } t || t == Guid.Empty)
                    {
                        return Fail(ConstantHelper.Message.HrSwapTargetRequired);
                    }
                    targetId = t;
                }
                if (targetId == current.EmployeeId)
                {
                    return Fail(ConstantHelper.Message.HrSwapSamePerson);
                }
                var people = await db.Employee.AsNoTracking().Where(e => (e.Id == targetId || e.Id == current.EmployeeId) && !e.IsDeleted).ToListAsync();
                if (people.Count != 2 || people.Any(e => !EmployeeStatus.IsWorking(e.Status)))
                {
                    return Fail(ConstantHelper.Message.HrEmployeeNotFound);
                }
                string? reason = request.Reason?.Trim();
                if (string.IsNullOrEmpty(reason) || reason.Length < 3)
                {
                    return Fail(ConstantHelper.Message.HrSwapReasonRequired);
                }
                if (await ValidateAsync(current, proposed, current.EmployeeId, targetId, null) is { } invalid)
                {
                    return Fail(invalid);
                }
                var entity = new ShiftSwapRequest
                {
                    Id = Guid.NewGuid(),
                    RequestNo = await _documentNumberService.NextAsync(DocumentKind.ShiftSwap, _clock.Today),
                    RequesterEmployeeId = current.EmployeeId,
                    TargetEmployeeId = targetId,
                    CurrentShiftAssignmentId = current.Id,
                    ProposedShiftAssignmentId = proposed?.Id,
                    Reason = reason,
                    RequestStatus = Hr.SwapConsentRequired ? ShiftSwapStatus.AwaitingConsent : ShiftSwapStatus.Pending,
                    SubmittedAt = _clock.Now,
                };
                _unitOfWork.ShiftSwapRequestRepository.Add(entity);
                await _unitOfWork.ShiftSwapRequestRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                if (Hr.SwapConsentRequired)
                {
                    // UI-6.39 (D-120): chờ người nhận đồng ý – chưa tạo yêu cầu duyệt.
                    _unitOfWork.CommitTransaction();
                    await NotifyTargetAsync(entity);
                    return Result<ShiftSwapDetailModel>.Success((await DetailAsync(entity.Id))!, string.Format(ConstantHelper.Message.HrSwapAwaitingConsent, entity.RequestNo));
                }
                var (approval, approvalError) = await _approvalRequestService.CreateAsync(ShiftSwapStatus.BusinessType, entity.Id);
                if (approvalError != null)
                {
                    return Fail(approvalError);
                }
                entity.ApprovalRequestId = approval!.Id;
                _unitOfWork.ShiftSwapRequestRepository.Update(entity);
                await _unitOfWork.ShiftSwapRequestRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                return Result<ShiftSwapDetailModel>.Success((await DetailAsync(entity.Id))!, string.Format(ConstantHelper.Message.HrSwapSubmitted, entity.RequestNo, approval.RequestNo));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Create: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }

            IResult<ShiftSwapDetailModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                return Result<ShiftSwapDetailModel>.Fail(message);
            }
        }

        private Task<ApprovalRequest?> LatestApprovalAsync(Guid id, bool openOnly)
            => _unitOfWork.DbContext.ApprovalRequest.AsNoTracking()
                .Where(a => !a.IsDeleted && a.BusinessType == ShiftSwapStatus.BusinessType && a.BusinessId == id && (!openOnly || ApprovalRequestService.OpenStatuses.Contains(a.RequestStatus)))
                .OrderByDescending(a => a.RequestedAt).FirstOrDefaultAsync();

        /// <summary>Duyệt / từ chối qua engine (quyền mục "Phê duyệt › Đơn đổi ca").</summary>
        public async Task<IResult<bool>> Decide(HrDecisionRequest request, bool approve)
        {
            try
            {
                var pending = await LatestApprovalAsync(request.Id, true);
                if (pending == null)
                {
                    return Result<bool>.Fail(ConstantHelper.Message.HrSwapNotPending, false);
                }
                return await _approvalRequestService.Decide(new ApprovalDecideRequest { Id = pending.Id, Approve = approve, Note = request.Note });
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Decide: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Spec 11.3 cancel (Catalog 209): chỉ Chờ duyệt, chỉ người tạo (engine kiểm) – hủy luôn ApprovalRequest, đơn → Đã hủy.</summary>
        public async Task<IResult<bool>> Cancel(HrCancelRequest request)
        {
            try
            {
                var swap = await _unitOfWork.DbContext.ShiftSwapRequest.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.Id && !s.IsDeleted);
                if (swap == null)
                {
                    return Result<bool>.Fail(ConstantHelper.Message.HrSwapNotFound, false);
                }
                if (swap.RequestStatus == ShiftSwapStatus.AwaitingConsent)
                {
                    // UI-6.39: chưa có yêu cầu duyệt – người tạo hủy trực tiếp.
                    var self = await HrRules.ResolveEmployeeAsync(_unitOfWork.DbContext, _currentActor.RequireActor().UserId);
                    if (self?.Id != swap.RequesterEmployeeId && swap.CreatedBy != _currentActor.RequireActor().UserId)
                    {
                        return Result<bool>.Fail(ConstantHelper.Message.HrSwapCancelNotOwner, false);
                    }
                    var row = _unitOfWork.ShiftSwapRequestRepository.GetSingle(x => x.Id == swap.Id)!;
                    row.RequestStatus = LeaveRequestStatus.Cancelled;
                    _unitOfWork.ShiftSwapRequestRepository.Update(row);
                    await _unitOfWork.ShiftSwapRequestRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                    return Result<bool>.Success(true, ConstantHelper.Message.HrSwapCancelled);
                }
                var pending = await LatestApprovalAsync(request.Id, true);
                if (swap.RequestStatus != ShiftSwapStatus.Pending || pending == null)
                {
                    return Result<bool>.Fail(ConstantHelper.Message.HrSwapNotPending, false);
                }
                return await _approvalRequestService.Cancel(new ApprovalCancelRequest { Id = pending.Id, Reason = request.Reason });
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Cancel: ", ex.ToString()));
                throw;
            }
        }

        private async Task<bool> CanViewAsync(ShiftSwapRequest r)
        {
            Guid actor = _currentActor.RequireActor().UserId;
            if (r.CreatedBy == actor)
            {
                return true;
            }
            var perms = await _permissionService.GetAsync(actor);
            if (perms.IsAdmin || _permissionService.IsAllowed(perms, HrScreen.Swap, PermissionAction.View) || _permissionService.IsAllowed(perms, ShiftSwapStatus.ScreenId, PermissionAction.Approve))
            {
                return true;
            }
            var self = await HrRules.ResolveEmployeeAsync(_unitOfWork.DbContext, actor);
            return self != null && (self.Id == r.RequesterEmployeeId || self.Id == r.TargetEmployeeId);
        }

        public async Task<IResult<ShiftSwapDetailModel>> GetDetail(Guid id)
        {
            try
            {
                var r = await _unitOfWork.DbContext.ShiftSwapRequest.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
                if (r == null)
                {
                    return Result<ShiftSwapDetailModel>.Fail(ConstantHelper.Message.HrSwapNotFound);
                }
                if (!await CanViewAsync(r))
                {
                    return Result<ShiftSwapDetailModel>.Fail(ConstantHelper.Message.HrRequestNoRight);
                }
                return Result<ShiftSwapDetailModel>.Success((await DetailAsync(id))!, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetDetail: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Đơn đổi ca người đang đăng nhập tạo hoặc của nhân viên gắn với tài khoản (là người xin hoặc người nhận), mới nhất trước, tối đa 100.</summary>
        public async Task<IResults<ShiftSwapDetailModel>> GetMine()
        {
            try
            {
                var db = _unitOfWork.DbContext;
                Guid actor = _currentActor.RequireActor().UserId;
                var employee = await HrRules.ResolveEmployeeAsync(db, actor);
                Guid? eid = employee?.Id;
                var ids = await db.ShiftSwapRequest.AsNoTracking()
                    .Where(s => !s.IsDeleted && (s.CreatedBy == actor || (eid != null && (s.RequesterEmployeeId == eid || s.TargetEmployeeId == eid))))
                    .OrderByDescending(s => s.SubmittedAt).Select(s => s.Id).Take(100).ToListAsync();
                var list = new List<ShiftSwapDetailModel>();
                foreach (var id in ids)
                {
                    list.Add((await DetailAsync(id))!);
                }
                return new Results<ShiftSwapDetailModel> { ResponseCode = Convert.ToInt32(HttpStatusCode.OK), ResponseMessage = ConstantHelper.Message.GetAllSuccessfully, ListData = list, TotalRecords = list.Count };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetMine: ", ex.ToString()));
                throw;
            }
        }

        private async Task<ShiftSwapDetailModel?> DetailAsync(Guid id)
        {
            var db = _unitOfWork.DbContext;
            var r = await db.ShiftSwapRequest.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (r == null)
            {
                return null;
            }
            var assignmentIds = new List<Guid> { r.CurrentShiftAssignmentId };
            if (r.ProposedShiftAssignmentId is { } p)
            {
                assignmentIds.Add(p);
            }
            var rows = await db.ShiftAssignment.AsNoTracking().Where(a => assignmentIds.Contains(a.Id)).ToListAsync();
            var models = await HrRules.ModelsAsync(db, rows);
            var names = await db.Employee.AsNoTracking().Where(e => e.Id == r.RequesterEmployeeId || e.Id == r.TargetEmployeeId).ToDictionaryAsync(e => e.Id, e => e.FullName);
            var approval = await LatestApprovalAsync(id, false);
            var detail = new ShiftSwapDetailModel
            {
                Request = r,
                RequesterName = names.GetValueOrDefault(r.RequesterEmployeeId),
                TargetName = names.GetValueOrDefault(r.TargetEmployeeId),
                StatusName = ShiftSwapStatus.Label(r.RequestStatus),
                SwapType = r.ProposedShiftAssignmentId == null ? TypeGive : TypeSwap,
                CurrentAssignment = models.FirstOrDefault(m => m.Id == r.CurrentShiftAssignmentId),
                ProposedAssignment = r.ProposedShiftAssignmentId is { } pp ? models.FirstOrDefault(m => m.Id == pp) : null,
                ApprovalRequestNo = approval?.RequestNo,
                PendingApproval = r.RequestStatus == ShiftSwapStatus.Pending ? await _approvalRequestService.OpenProgressAsync(ShiftSwapStatus.BusinessType, id) : null,
            };
            if (r.RequestStatus == ShiftSwapStatus.Pending)
            {
                var current = rows.FirstOrDefault(a => a.Id == r.CurrentShiftAssignmentId);
                var proposed = r.ProposedShiftAssignmentId is { } q ? rows.FirstOrDefault(a => a.Id == q) : null;
                string? problem = current == null ? ConstantHelper.Message.HrSwapAssignmentNotFound
                    : await ValidateAsync(current, proposed, r.RequesterEmployeeId, r.TargetEmployeeId, r.Id);
                if (problem != null)
                {
                    detail.ListWarning.Add(string.Format(ConstantHelper.Message.HrSwapWillFail, problem));
                }
                Guid actor = _currentActor.RequireActor().UserId;
                if (r.CreatedBy == actor)
                {
                    detail.AllowedActions.Add("cancel");
                }
                var perms = await _permissionService.GetAsync(actor);
                if (detail.PendingApproval is { ScreenId: { } screen, SelfBlocked: false } && _permissionService.IsAllowed(perms, screen, PermissionAction.Approve))
                {
                    detail.AllowedActions.Add("approve");
                    detail.AllowedActions.Add("reject");
                }
            }
            return detail;
        }

        /// <summary>Nội dung được duyệt của SHIFT_SWAP: 2 ca (nhân viên, ca, ngày, giờ) lúc gửi – ca bị sửa sau khi gửi → không duyệt được.</summary>
        public async Task<string?> BuildApprovalStateAsync(Guid id)
        {
            var db = _unitOfWork.DbContext;
            var r = await db.ShiftSwapRequest.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (r == null)
            {
                return null;
            }
            var ids = new List<Guid> { r.CurrentShiftAssignmentId };
            if (r.ProposedShiftAssignmentId is { } p)
            {
                ids.Add(p);
            }
            var rows = await db.ShiftAssignment.AsNoTracking().Where(a => ids.Contains(a.Id)).OrderBy(a => a.Id).ToListAsync();
            return JsonConvert.SerializeObject(new
            {
                r.Id, r.RequestNo, r.RequesterEmployeeId, r.TargetEmployeeId, r.CurrentShiftAssignmentId, r.ProposedShiftAssignmentId, r.Reason, r.RequestStatus,
                Assignments = rows.Select(a => new { a.Id, a.EmployeeId, a.ShiftId, WorkDate = a.WorkDate.ToString("yyyy-MM-dd"), a.PlannedStart, a.PlannedEnd }),
            });
        }

        /// <summary>Tiêu đề hộp duyệt: "Đổi ca PH-…: A (TRUA 20/12/2026) ↔ B (TOI 21/12/2026)" hoặc "Nhường ca PH-…: A → B (TRUA 20/12/2026)".</summary>
        public async Task<string?> DescribeAsync(Guid id)
        {
            var d = await DetailAsync(id);
            if (d?.Request is not { } r || d.CurrentAssignment is not { } c)
            {
                return null;
            }
            return d.ProposedAssignment is { } p
                ? $"Đổi ca {r.RequestNo}: {d.RequesterName} ({c.ShiftCode} {HrRules.Day(c.WorkDate)}) ↔ {d.TargetName} ({p.ShiftCode} {HrRules.Day(p.WorkDate)})"
                : $"Nhường ca {r.RequestNo}: {d.RequesterName} → {d.TargetName} ({c.ShiftCode} {HrRules.Day(c.WorkDate)})";
        }

        /// <summary>
        /// Chuyển 1 ca sang nhân viên khác – UNIQUE (EmployeeId, ShiftId, WorkDate) tính cả dòng đã xóa mềm: người mới đã có dòng (đã xóa) cùng ca / ngày → dùng lại dòng đó
        /// và xóa mềm dòng cũ; không có → đổi EmployeeId tại chỗ.
        /// </summary>
        private void Move(ShiftAssignment row, Guid toEmployeeId, string note)
        {
            var deleted = _unitOfWork.ShiftAssignmentRepository.GetAll()
                .FirstOrDefault(a => a.IsDeleted && a.EmployeeId == toEmployeeId && a.ShiftId == row.ShiftId && a.WorkDate == row.WorkDate);
            if (deleted != null)
            {
                deleted.IsDeleted = false;
                deleted.PlannedStart = row.PlannedStart;
                deleted.PlannedEnd = row.PlannedEnd;
                deleted.AssignmentStatus = row.AssignmentStatus;
                deleted.AssignedBy = row.AssignedBy;
                deleted.Note = HrRules.AppendNote(row.Note, note);
                _unitOfWork.ShiftAssignmentRepository.Update(deleted);
                ShiftAssignmentService.SoftDelete(row, note);
            }
            else
            {
                row.EmployeeId = toEmployeeId;
                row.Note = HrRules.AppendNote(row.Note, note);
            }
            _unitOfWork.ShiftAssignmentRepository.Update(row);
        }

        /// <summary>
        /// Engine (handler SHIFT_SWAP, trong transaction của engine). Duyệt → kiểm lại (chưa chấm công, không trùng ca / nghỉ phép) rồi hoán đổi EmployeeId giữa 2 ca
        /// (nhường ca: chuyển ca hiện tại cho người nhận), ghi chú "Đổi ca theo đơn PH-…"; từ chối → Từ chối; người gửi hủy → Đã hủy.
        /// </summary>
        public async Task<string?> ApplyDecisionAsync(Guid id, string decision)
        {
            var r = _unitOfWork.ShiftSwapRequestRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (r == null || r.RequestStatus != ShiftSwapStatus.Pending)
            {
                return ConstantHelper.Message.HrSwapNotPending;
            }
            if (decision == ShiftSwapStatus.Approved)
            {
                var current = _unitOfWork.ShiftAssignmentRepository.GetSingle(a => a.Id == r.CurrentShiftAssignmentId);
                var proposed = r.ProposedShiftAssignmentId is { } pid ? _unitOfWork.ShiftAssignmentRepository.GetSingle(a => a.Id == pid) : null;
                if (current == null || (r.ProposedShiftAssignmentId != null && proposed == null))
                {
                    return ConstantHelper.Message.HrSwapAssignmentNotFound;
                }
                if (await ValidateAsync(current, proposed, r.RequesterEmployeeId, r.TargetEmployeeId, r.Id) is { } invalid)
                {
                    return invalid;
                }
                string note = "Đổi ca theo đơn " + r.RequestNo;
                Move(current, r.TargetEmployeeId, note);
                if (proposed != null)
                {
                    Move(proposed, r.RequesterEmployeeId, note);
                }
            }
            r.RequestStatus = decision;
            _unitOfWork.ShiftSwapRequestRepository.Update(r);
            return null;
        }
    }
}
