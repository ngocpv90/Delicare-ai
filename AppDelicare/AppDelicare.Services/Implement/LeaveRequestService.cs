// Sinh bởi tools/AppDelicare.CodeGen (T05), nay là file của dev (BE2.6: đơn nghỉ + duyệt qua engine LEAVE_REQUEST, hủy) – CodeGen không ghi đè.
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
using AppDelicare.Model.Request;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Serilog;
using System.Globalization;
using System.Net;

namespace AppDelicare.Services.Implement
{
    public class LeaveRequestService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper, IReferenceGuard referenceGuard
        , IClock clock, ICurrentActor currentActor, IPermissionService permissionService, IDocumentNumberService documentNumberService
        , IApprovalRequestService approvalRequestService, IOptions<BusinessRulesOptions> rules, IOptions<ReferenceListsOptions> referenceLists) : ILeaveRequestService
    {
        /// <summary>Danh mục loại nghỉ (appsettings <c>ReferenceLists:LeaveType</c> – PROPOSED, D-55 (f)).</summary>
        public const string LeaveTypeList = "LeaveType";

        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IReferenceGuard _referenceGuard = referenceGuard;
        private readonly IClock _clock = clock;
        private readonly ICurrentActor _currentActor = currentActor;
        private readonly IPermissionService _permissionService = permissionService;
        private readonly IDocumentNumberService _documentNumberService = documentNumberService;
        private readonly IApprovalRequestService _approvalRequestService = approvalRequestService;
        private readonly HrRulesOptions _hr = rules.Value.Hr;
        private readonly ReferenceListsOptions _referenceLists = referenceLists.Value;

        public async Task<IResults<LeaveRequestViewModel>> GetAll(LeaveRequestSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@EmployeeId", request.EmployeeId);
                dynamicParameters.Add("@RequestStatus", request.RequestStatus);
                dynamicParameters.Add("@ApprovalRequestId", request.ApprovalRequestId);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<LeaveRequestViewModel>("uspLeaveRequest_GetAll", dynamicParameters)).ToList();
                int i = 0;
                list.ForEach(x =>
                {
                    i++;
                    x.STT = (request.Page - 1) * request.PageSize + i;
                });
                return new Results<LeaveRequestViewModel>
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

        public IResults<LeaveRequest> GetAllData()
        {
            try
            {
                var list = _unitOfWork.LeaveRequestRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<LeaveRequest>
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

        public async Task<IResult<bool>> Save(LeaveRequestRequest request, string typeSave)
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

        /// <summary>BE2.6: đơn nghỉ chỉ tạo qua <c>LeaveRequest/Create</c> (số DN-, kiểm chồng đơn, gửi duyệt LEAVE_REQUEST).</summary>
        private Task<string> Insert(LeaveRequestRequest request, string typeSave) => Task.FromResult(ConstantHelper.Message.HrLeaveCrudBlocked);

        /// <summary>BE2.6: không sửa trực tiếp (nội dung đã gửi duyệt – hash); duyệt / từ chối / hủy qua hộp duyệt.</summary>
        private Task<string> Update(LeaveRequestRequest request, string typeSave) => Task.FromResult(ConstantHelper.Message.HrLeaveCrudBlocked);

        /// <summary>BE2.6: chỉ xóa (mềm) đơn đã Từ chối / Hủy.</summary>
        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            var delete = _unitOfWork.LeaveRequestRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            if (delete.RequestStatus is not (LeaveRequestStatus.Rejected or LeaveRequestStatus.Cancelled))
            {
                return string.Format(ConstantHelper.Message.StatusActionInvalid, "xóa", "đơn nghỉ",
                    LeaveRequestStatus.Label(LeaveRequestStatus.Rejected) + " / " + LeaveRequestStatus.Label(LeaveRequestStatus.Cancelled), LeaveRequestStatus.Label(delete.RequestStatus));
            }
            _unitOfWork.LeaveRequestRepository.Update(delete);
            await _unitOfWork.LeaveRequestRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<LeaveRequest>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.LeaveRequestRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<LeaveRequest>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<LeaveRequest>
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

        // ============================================================ BE2.6 – đơn nghỉ (spec 11.3, Catalog 207, 209)
        private string? LeaveTypeName(string? code)
            => _referenceLists.Lists.TryGetValue(LeaveTypeList, out var items) ? items.FirstOrDefault(i => string.Equals(i.Code, code, StringComparison.OrdinalIgnoreCase))?.Label : null;

        /// <summary>
        /// Spec 11.3 POST leave-requests (Catalog 207) – 1 transaction: nhân viên (trống = của tài khoản; người khác cần quyền Thêm trên <c>#/hr-leave</c>), loại nghỉ theo
        /// <c>ReferenceLists:LeaveType</c>, đến ngày ≥ từ ngày, ≤ <c>Hr:LeaveMaxDays</c> ngày, lập lùi ≤ <c>Hr:LeaveMaxBackdateDays</c>, lý do bắt buộc, không chồng đơn Chờ duyệt / Đã duyệt khác,
        /// không trùng ngày đã chấm công. Số <c>DN-YYMMDD-NNNN</c> (BUSINESS_RULES §11), Chờ duyệt, ApprovalRequest LEAVE_REQUEST (engine BE2.3, mặc định 1 cấp mục
        /// <c>#/wf-inbox?type=hr-leave</c>), ghi <c>ApprovalRequestId</c>.
        /// </summary>
        public Task<IResult<LeaveRequestDetailModel>> Create(LeaveCreateRequest request) => CreateCore(request, null);

        /// <summary>BOT-1.5a (D-62 p8): người chỉ có EmployeeId lập đơn nghỉ qua bot – nhân viên lấy từ định danh (không theo người thao tác = system), mọi quy tắc như Create.</summary>
        public Task<IResult<LeaveRequestDetailModel>> CreateForEmployee(Guid employeeId, LeaveCreateRequest request)
        {
            request.EmployeeId = null;
            return CreateCore(request, employeeId);
        }

        private async Task<IResult<LeaveRequestDetailModel>> CreateCore(LeaveCreateRequest request, Guid? botEmployeeId)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var db = _unitOfWork.DbContext;
                Guid actor = _currentActor.RequireActor().UserId;
                var self = botEmployeeId is { } botEmp
                    ? await db.Employee.AsNoTracking().FirstOrDefaultAsync(e => e.Id == botEmp && !e.IsDeleted)
                    : await HrRules.ResolveEmployeeAsync(db, actor);
                Employee? employee;
                if (request.EmployeeId is { } wanted && wanted != Guid.Empty && wanted != self?.Id)
                {
                    if (!_permissionService.IsAllowed(await _permissionService.GetAsync(actor), HrScreen.Leave, PermissionAction.Add))
                    {
                        return Fail(string.Format(ConstantHelper.Message.HrNoRightOther, "lập đơn nghỉ", "Đơn xin nghỉ"));
                    }
                    employee = await db.Employee.AsNoTracking().FirstOrDefaultAsync(e => e.Id == wanted && !e.IsDeleted);
                }
                else
                {
                    if (self == null)
                    {
                        return Fail(ConstantHelper.Message.HrUserNoEmployee);
                    }
                    employee = self;
                }
                if (employee == null || !EmployeeStatus.IsWorking(employee.Status))
                {
                    return Fail(ConstantHelper.Message.HrEmployeeNotFound);
                }
                string type = (request.LeaveTypeCode ?? string.Empty).Trim().ToUpperInvariant();
                bool hasList = _referenceLists.Lists.TryGetValue(LeaveTypeList, out var types) && types.Count > 0;
                if (type.Length == 0 || (hasList && !types!.Any(t => string.Equals(t.Code, type, StringComparison.OrdinalIgnoreCase))))
                {
                    return Fail(string.Format(ConstantHelper.Message.HrLeaveTypeInvalid, hasList ? string.Join(", ", types!.Select(t => t.Code)) : "?"));
                }
                DateTime from = request.FromDate.Date, to = request.ToDate.Date, today = _clock.Today;
                if (request.FromDate == default || request.ToDate == default || to < from || (to - from).TotalDays + 1 > _hr.LeaveMaxDays)
                {
                    return Fail(string.Format(ConstantHelper.Message.HrLeaveDateInvalid, _hr.LeaveMaxDays));
                }
                if (from < today.AddDays(-_hr.LeaveMaxBackdateDays))
                {
                    return Fail(string.Format(ConstantHelper.Message.HrLeaveTooOld, _hr.LeaveMaxBackdateDays));
                }
                string? reason = request.Reason?.Trim();
                if (string.IsNullOrEmpty(reason) || reason.Length < 3)
                {
                    return Fail(ConstantHelper.Message.HrLeaveReasonRequired);
                }
                if ((await HrRules.LeavesAsync(db, [employee.Id], from, to, false)).OrderBy(l => l.FromDate).FirstOrDefault() is { } overlap)
                {
                    return Fail(string.Format(ConstantHelper.Message.HrLeaveOverlap, employee.FullName, overlap.RequestNo,
                        HrRules.Day(overlap.FromDate) + " – " + HrRules.Day(overlap.ToDate), LeaveRequestStatus.Label(overlap.RequestStatus)));
                }
                var assignments = await db.ShiftAssignment.AsNoTracking().Where(a => !a.IsDeleted && a.EmployeeId == employee.Id && a.WorkDate >= from && a.WorkDate <= to).ToListAsync();
                var attended = await HrRules.AttendedAssignmentsAsync(db, assignments.Select(a => a.Id).ToList());
                if (assignments.FirstOrDefault(a => attended.Contains(a.Id) || !ShiftAssignmentStatus.Open.Contains(a.AssignmentStatus)) is { } worked)
                {
                    string? code = await db.Shift.AsNoTracking().Where(s => s.Id == worked.ShiftId).Select(s => s.Code).FirstOrDefaultAsync();
                    return Fail(string.Format(ConstantHelper.Message.HrLeaveAttended, employee.FullName, code, HrRules.Day(worked.WorkDate), ShiftAssignmentStatus.Label(worked.AssignmentStatus)));
                }
                var entity = new LeaveRequest
                {
                    Id = Guid.NewGuid(),
                    RequestNo = await _documentNumberService.NextAsync(DocumentKind.Leave, today),
                    EmployeeId = employee.Id,
                    FromDate = from,
                    ToDate = to,
                    LeaveTypeCode = type,
                    Reason = reason,
                    RequestStatus = LeaveRequestStatus.Pending,
                    SubmittedAt = _clock.Now,
                };
                _unitOfWork.LeaveRequestRepository.Add(entity);
                await _unitOfWork.LeaveRequestRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                var (approval, approvalError) = await _approvalRequestService.CreateAsync(LeaveRequestStatus.BusinessType, entity.Id);
                if (approvalError != null)
                {
                    return Fail(approvalError);
                }
                entity.ApprovalRequestId = approval!.Id;
                _unitOfWork.LeaveRequestRepository.Update(entity);
                await _unitOfWork.LeaveRequestRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                return Result<LeaveRequestDetailModel>.Success((await DetailAsync(entity.Id))!, string.Format(ConstantHelper.Message.HrLeaveSubmitted, entity.RequestNo, approval.RequestNo));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Create: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }

            IResult<LeaveRequestDetailModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                return Result<LeaveRequestDetailModel>.Fail(message);
            }
        }

        private Task<ApprovalRequest?> LatestApprovalAsync(Guid id, bool openOnly)
            => _unitOfWork.DbContext.ApprovalRequest.AsNoTracking()
                .Where(a => !a.IsDeleted && a.BusinessType == LeaveRequestStatus.BusinessType && a.BusinessId == id && (!openOnly || ApprovalRequestService.OpenStatuses.Contains(a.RequestStatus)))
                .OrderByDescending(a => a.RequestedAt).FirstOrDefaultAsync();

        /// <summary>Duyệt / từ chối qua engine (<c>ApprovalRequestService.Decide</c>: quyền mục "Phê duyệt › Đơn xin nghỉ", hash, ApprovalDecision, nhiều cấp nếu cấu hình).</summary>
        public async Task<IResult<bool>> Decide(HrDecisionRequest request, bool approve)
        {
            try
            {
                var pending = await LatestApprovalAsync(request.Id, true);
                if (pending == null)
                {
                    return Result<bool>.Fail(ConstantHelper.Message.HrLeaveNotPending, false);
                }
                return await _approvalRequestService.Decide(new ApprovalDecideRequest { Id = pending.Id, Approve = approve, Note = request.Note });
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Decide: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Spec 11.3 cancel (Catalog 209): chỉ đơn Chờ duyệt, chỉ người tạo (engine kiểm người gửi, admin được) – hủy luôn ApprovalRequest, đơn → Đã hủy.</summary>
        public async Task<IResult<bool>> Cancel(HrCancelRequest request)
        {
            try
            {
                var leave = await _unitOfWork.DbContext.LeaveRequest.AsNoTracking().FirstOrDefaultAsync(l => l.Id == request.Id && !l.IsDeleted);
                if (leave == null)
                {
                    return Result<bool>.Fail(ConstantHelper.Message.HrLeaveNotFound, false);
                }
                var pending = await LatestApprovalAsync(request.Id, true);
                if (leave.RequestStatus != LeaveRequestStatus.Pending || pending == null)
                {
                    return Result<bool>.Fail(ConstantHelper.Message.HrLeaveNotPending, false);
                }
                return await _approvalRequestService.Cancel(new ApprovalCancelRequest { Id = pending.Id, Reason = request.Reason });
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Cancel: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Xem được: người tạo, nhân viên của đơn, người có quyền Xem màn đơn nghỉ, người có quyền Duyệt đơn nghỉ, admin.</summary>
        private async Task<bool> CanViewAsync(LeaveRequest r)
        {
            Guid actor = _currentActor.RequireActor().UserId;
            if (r.CreatedBy == actor)
            {
                return true;
            }
            var perms = await _permissionService.GetAsync(actor);
            if (perms.IsAdmin || _permissionService.IsAllowed(perms, HrScreen.Leave, PermissionAction.View) || _permissionService.IsAllowed(perms, LeaveRequestStatus.ScreenId, PermissionAction.Approve))
            {
                return true;
            }
            return (await HrRules.ResolveEmployeeAsync(_unitOfWork.DbContext, actor))?.Id == r.EmployeeId;
        }

        public async Task<IResult<LeaveRequestDetailModel>> GetDetail(Guid id)
        {
            try
            {
                var r = await _unitOfWork.DbContext.LeaveRequest.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
                if (r == null)
                {
                    return Result<LeaveRequestDetailModel>.Fail(ConstantHelper.Message.HrLeaveNotFound);
                }
                if (!await CanViewAsync(r))
                {
                    return Result<LeaveRequestDetailModel>.Fail(ConstantHelper.Message.HrRequestNoRight);
                }
                return Result<LeaveRequestDetailModel>.Success((await DetailAsync(id))!, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetDetail: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Đơn nghỉ của nhân viên gắn với tài khoản đang đăng nhập (mới nhất trước, tối đa 100) – màn tự phục vụ.</summary>
        public async Task<IResults<LeaveRequestDetailModel>> GetMine()
        {
            try
            {
                var db = _unitOfWork.DbContext;
                Guid actor = _currentActor.RequireActor().UserId;
                var employee = await HrRules.ResolveEmployeeAsync(db, actor);
                var ids = await db.LeaveRequest.AsNoTracking()
                    .Where(l => !l.IsDeleted && (l.CreatedBy == actor || (employee != null && l.EmployeeId == employee.Id)))
                    .OrderByDescending(l => l.SubmittedAt).Select(l => l.Id).Take(100).ToListAsync();
                var list = new List<LeaveRequestDetailModel>();
                foreach (var id in ids)
                {
                    list.Add((await DetailAsync(id))!);
                }
                return new Results<LeaveRequestDetailModel> { ResponseCode = Convert.ToInt32(HttpStatusCode.OK), ResponseMessage = ConstantHelper.Message.GetAllSuccessfully, ListData = list, TotalRecords = list.Count };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetMine: ", ex.ToString()));
                throw;
            }
        }

        private string RemovedMark(LeaveRequest r) => "Nghỉ theo đơn " + r.RequestNo;

        private async Task<LeaveRequestDetailModel?> DetailAsync(Guid id)
        {
            var db = _unitOfWork.DbContext;
            var r = await db.LeaveRequest.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (r == null)
            {
                return null;
            }
            var employee = await db.Employee.AsNoTracking().FirstOrDefaultAsync(e => e.Id == r.EmployeeId);
            var approval = await LatestApprovalAsync(id, false);
            string mark = RemovedMark(r);
            var rows = await db.ShiftAssignment.AsNoTracking()
                .Where(a => a.EmployeeId == r.EmployeeId && a.WorkDate >= r.FromDate && a.WorkDate <= r.ToDate && (!a.IsDeleted || (a.Note != null && a.Note.Contains(mark))))
                .ToListAsync();
            var detail = new LeaveRequestDetailModel
            {
                Request = r,
                EmployeeCode = employee?.Code ?? employee?.EmployeeCode,
                EmployeeName = employee?.FullName,
                LeaveTypeName = LeaveTypeName(r.LeaveTypeCode),
                StatusName = LeaveRequestStatus.Label(r.RequestStatus),
                Days = (int)(r.ToDate.Date - r.FromDate.Date).TotalDays + 1,
                ApprovalRequestNo = approval?.RequestNo,
                PendingApproval = r.RequestStatus == LeaveRequestStatus.Pending ? await _approvalRequestService.OpenProgressAsync(LeaveRequestStatus.BusinessType, id) : null,
                ListAffectedAssignment = await HrRules.ModelsAsync(db, rows),
                // UI-6.39 (D-120): hệ quả khi duyệt – phép năm còn, tổ còn người, người nghỉ trùng, người có thể thay.
                Impact = await HrRequestService.LeaveImpactAsync(db, _hr, r.EmployeeId, r.FromDate, r.ToDate, r.LeaveTypeCode, r.Id),
            };
            foreach (var a in detail.ListAffectedAssignment)
            {
                if (a.IsDeleted)
                {
                    detail.ListWarning.Add(string.Format(ConstantHelper.Message.HrLeaveShiftShort, a.ShiftCode, HrRules.Day(a.WorkDate), detail.EmployeeName));
                }
                else if (r.RequestStatus == LeaveRequestStatus.Pending && ShiftAssignmentStatus.Open.Contains(a.Status))
                {
                    detail.ListWarning.Add(string.Format(ConstantHelper.Message.HrLeaveShiftWillRemove, a.ShiftCode, HrRules.Day(a.WorkDate)));
                }
            }
            if (r.RequestStatus == LeaveRequestStatus.Pending)
            {
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

        /// <summary>Nội dung được duyệt của LEAVE_REQUEST (sửa sau khi gửi → không duyệt được).</summary>
        public async Task<string?> BuildApprovalStateAsync(Guid id)
        {
            var r = await _unitOfWork.DbContext.LeaveRequest.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            return r == null ? null : JsonConvert.SerializeObject(new
            {
                r.Id, r.RequestNo, r.EmployeeId, FromDate = r.FromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), ToDate = r.ToDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                r.LeaveTypeCode, r.Reason, r.RequestStatus,
            });
        }

        /// <summary>Tiêu đề hộp duyệt: số đơn – nhân viên – loại – khoảng ngày (– số ca bị bỏ).</summary>
        public async Task<string?> DescribeAsync(Guid id)
        {
            var detail = await DetailAsync(id);
            if (detail?.Request is not { } r)
            {
                return null;
            }
            int shifts = detail.ListAffectedAssignment.Count(a => !a.IsDeleted && ShiftAssignmentStatus.Open.Contains(a.Status));
            return $"Đơn nghỉ {r.RequestNo}: {detail.EmployeeName} – {detail.LeaveTypeName ?? r.LeaveTypeCode} – {HrRules.Day(r.FromDate)} → {HrRules.Day(r.ToDate)} ({detail.Days} ngày)"
                + (shifts > 0 ? $" – bỏ {shifts} ca đã phân" : string.Empty);
        }

        /// <summary>
        /// Engine (handler LEAVE_REQUEST, chạy trong transaction của engine). Duyệt → Đã duyệt + xóa mềm các ca Kế hoạch / Xác nhận chưa chấm công trong khoảng nghỉ (Note "Nghỉ theo
        /// đơn DN-…" – chi tiết đơn liệt kê "ca thiếu người"); từ chối → Từ chối; người gửi hủy → Đã hủy.
        /// </summary>
        public async Task<string?> ApplyDecisionAsync(Guid id, string decision)
        {
            var r = _unitOfWork.LeaveRequestRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (r == null || r.RequestStatus != LeaveRequestStatus.Pending)
            {
                return ConstantHelper.Message.HrLeaveNotPending;
            }
            r.RequestStatus = decision;
            _unitOfWork.LeaveRequestRepository.Update(r);
            if (decision != LeaveRequestStatus.Approved)
            {
                return null;
            }
            var rows = _unitOfWork.ShiftAssignmentRepository.GetAll()
                .Where(a => !a.IsDeleted && a.EmployeeId == r.EmployeeId && a.WorkDate >= r.FromDate && a.WorkDate <= r.ToDate).ToList();
            var attended = await HrRules.AttendedAssignmentsAsync(_unitOfWork.DbContext, rows.Select(a => a.Id).ToList());
            foreach (var a in rows.Where(a => ShiftAssignmentStatus.Open.Contains(a.AssignmentStatus) && !attended.Contains(a.Id)))
            {
                ShiftAssignmentService.SoftDelete(a, RemovedMark(r));
                _unitOfWork.ShiftAssignmentRepository.Update(a);
            }
            return null;
        }
    }
}
