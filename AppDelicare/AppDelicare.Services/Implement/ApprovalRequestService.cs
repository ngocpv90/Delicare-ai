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
using AppDelicare.Model.Option;
using Microsoft.Extensions.Options;
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
        , IDocumentNumberService documentNumberService, IOptions<BusinessRulesOptions> rules) : IApprovalRequestService
    {
        private readonly BusinessRulesOptions _rules = rules.Value;
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

        // ============================================================ T12 – duyệt (registry IApprovalHandler); BE2.3 – nhiều cấp theo ApprovalStep
        public const string Pending = "Pending";
        /// <summary>BE2.3 (STATUS_CATALOG ApprovalRequest: Pending → InProgress → Approved / Rejected / Cancelled): đã qua ít nhất 1 cấp, còn cấp sau.</summary>
        public const string InProgress = "InProgress";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public const string Cancelled = "Cancelled";
        /// <summary>ApprovalStep.DecisionStatus (STATUS_CATALOG: Waiting → Approved / Rejected / Skipped; Cancelled – GAP-APR-01).</summary>
        public const string StepWaiting = "Waiting";
        public const string StepSkipped = "Skipped";
        /// <summary>BE2.3: ApprovalItem.ActionCode của dòng lưu quy trình (spec 2.3 bước 4) – PayloadJson = {"steps":["scm-pr-l1","scm-pr-l2"]} (GAP-APR-02).</summary>
        public const string RouteActionCode = "APPROVE";

        /// <summary>BE2.3: yêu cầu còn mở (chờ cấp 1 hoặc đang duyệt các cấp sau). Dùng cho mọi kiểm tra "đang chờ duyệt" của nghiệp vụ.</summary>
        public static readonly string[] OpenStatuses = [Pending, InProgress];

        public static bool IsOpen(string? status) => status is Pending or InProgress;

        private IEnumerable<IApprovalHandler> Handlers
            => (IEnumerable<IApprovalHandler>?)_serviceProvider.GetService(typeof(IEnumerable<IApprovalHandler>)) ?? [];

        private IApprovalHandler? HandlerOf(string? businessType)
            => Handlers.FirstOrDefault(h => h.BusinessType.Equals(businessType, StringComparison.OrdinalIgnoreCase));

        private static string Hash(string? payload)
            => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload ?? string.Empty)));

        /// <summary>
        /// T13: PayloadJson có đề xuất = {"state": "&lt;nội dung chuỗi&gt;", "proposal": "&lt;đề xuất chuỗi&gt;"} (cả hai lưu dạng chuỗi để hash
        /// nội dung đúng từng byte). Không có đề xuất → PayloadJson = nội dung như T12 (tương thích ngược).
        /// </summary>
        private const string StateKey = "state";
        private const string ProposalKey = "proposal";

        private static string? WrapPayload(string? state, string? proposal)
            => proposal == null ? state : Newtonsoft.Json.JsonConvert.SerializeObject(new Dictionary<string, string?> { [StateKey] = state, [ProposalKey] = proposal });

        /// <summary>Tách PayloadJson đã lưu thành (nội dung, đề xuất). Định dạng cũ → (PayloadJson, null).</summary>
        public static (string? State, string? Proposal) UnwrapPayload(string? payloadJson)
        {
            if (string.IsNullOrEmpty(payloadJson) || !payloadJson.TrimStart().StartsWith('{'))
            {
                return (payloadJson, null);
            }
            try
            {
                var obj = Newtonsoft.Json.Linq.JObject.Parse(payloadJson);
                if (obj.Count == 2 && obj[StateKey] is Newtonsoft.Json.Linq.JValue { Type: Newtonsoft.Json.Linq.JTokenType.String or Newtonsoft.Json.Linq.JTokenType.Null } s
                    && obj[ProposalKey] is Newtonsoft.Json.Linq.JValue { Type: Newtonsoft.Json.Linq.JTokenType.String } p)
                {
                    return ((string?)s.Value, (string?)p.Value);
                }
            }
            catch (Newtonsoft.Json.JsonException)
            {
                // không phải JSON hợp lệ → coi như nội dung cũ
            }
            return (payloadJson, null);
        }

        // ------------------------------------------------------------ BE2.3 – quy trình, hạn, tên bước

        /// <summary>
        /// Các bước của 1 nghiệp vụ: cấu hình <c>BusinessRules:ApprovalSteps[BusinessType]</c> (spec 2.3, GAP-F-08) nếu có, không thì mặc định của handler
        /// (<see cref="IApprovalHandler.StepScreenIdsAsync"/>; handler cũ = 1 bước).
        /// </summary>
        private async Task<List<string>> StepsForAsync(IApprovalHandler handler, Guid businessId)
        {
            IEnumerable<string> steps = _rules.ApprovalSteps.TryGetValue(handler.BusinessType, out var configured) && configured is { Length: > 0 }
                ? configured
                : await handler.StepScreenIdsAsync(businessId);
            var list = steps.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList();
            return list.Count == 0 ? [handler.ScreenId] : list;
        }

        /// <summary>Mọi mục quyền mà handler có thể dùng (khai báo của handler + cấu hình) – để hộp duyệt lọc handler theo quyền / type.</summary>
        private IEnumerable<string> ScreensOf(IApprovalHandler handler)
            => handler.ApprovalScreenIds.Concat(_rules.ApprovalSteps.TryGetValue(handler.BusinessType, out var configured) ? configured : []);

        /// <summary>Hạn của 1 bước (giờ theo BusinessType, không có → mặc định; 0 = không hạn).</summary>
        private DateTime? DueOf(string? businessType, DateTime from)
        {
            int hours = businessType != null && _rules.ApprovalStepDueHoursByType.TryGetValue(businessType, out var h) ? h : _rules.ApprovalStepDueHours;
            return hours > 0 ? from.AddHours(hours) : null;
        }

        private static List<string>? ParseRoute(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }
            try
            {
                return Newtonsoft.Json.Linq.JObject.Parse(json)["steps"]?.ToObject<List<string>>();
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return null;
            }
        }

        /// <summary>Quy trình đã lưu lúc gửi (ApprovalItem ActionCode APPROVE) của các yêu cầu; yêu cầu trước BE2.3 không có → null (dùng handler).</summary>
        private async Task<Dictionary<Guid, List<string>?>> RoutesAsync(IEnumerable<Guid> requestIds)
        {
            var ids = requestIds.Distinct().ToList();
            var items = await _unitOfWork.DbContext.ApprovalItem.AsNoTracking()
                .Where(i => ids.Contains(i.ApprovalRequestId) && !i.IsDeleted && i.ActionCode == RouteActionCode)
                .ToListAsync();
            return ids.ToDictionary(id => id, id => ParseRoute(items.Where(i => i.ApprovalRequestId == id).OrderBy(i => i.CreatedDate).Select(i => i.PayloadJson).FirstOrDefault()));
        }

        /// <summary>Mục quyền của bước <paramref name="stepNo"/>: theo quy trình đã lưu; không có (yêu cầu cũ) → mục của handler (T15.1).</summary>
        private static async Task<string> StepScreenAsync(IApprovalHandler handler, Guid? businessId, int stepNo, List<string>? route)
            => route != null && stepNo >= 1 && stepNo <= route.Count ? route[stepNo - 1]
                : businessId is { } id ? await handler.RequiredScreenIdAsync(id) : handler.ScreenId;

        /// <summary>Tên bước = tên mục menu "Duyệt – …" (ObjectMenu.MetaLink = <c>#/wf-inbox?type=&lt;screen&gt;</c>); không có mục → screen-id.</summary>
        private async Task<Dictionary<string, string>> StepNamesAsync(IEnumerable<string> screens)
        {
            var links = screens.Where(s => !string.IsNullOrEmpty(s)).Distinct(StringComparer.OrdinalIgnoreCase).Select(s => "#/wf-inbox?type=" + s).ToList();
            var menus = await _unitOfWork.DbContext.ObjectMenu.AsNoTracking().Where(m => !m.IsDeleted && m.MetaLink != null && links.Contains(m.MetaLink))
                .Select(m => new { m.MetaLink, m.Name }).ToListAsync();
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in menus)
            {
                map.TryAdd(m.MetaLink!["#/wf-inbox?type=".Length..], m.Name ?? m.MetaLink!);
            }
            return map;
        }

        private static string StepStatusLabel(string? status) => status switch
        {
            StepWaiting => "Chờ duyệt",
            Approved => "Đã duyệt",
            Rejected => "Từ chối",
            StepSkipped => "Bỏ qua",
            Cancelled => "Đã hủy",
            _ => status ?? string.Empty,
        };

        /// <summary>Bước đang chờ = bước Waiting có StepNo nhỏ nhất (spec 11.5 decide bước 1).</summary>
        private static ApprovalStep? CurrentStep(IEnumerable<ApprovalStep> steps)
            => steps.Where(s => !s.IsDeleted && s.DecisionStatus == StepWaiting).OrderBy(s => s.StepNo).FirstOrDefault();

        /// <summary>
        /// Tạo yêu cầu duyệt (mã XN-) + các bước duyệt theo quy trình (BE2.3: 1 ApprovalStep / cấp, bước 1 được giao ngay, có hạn) + ApprovalItem lưu quy trình,
        /// lưu nội dung được duyệt. Gọi TRONG transaction của nghiệp vụ gửi duyệt. Đã có yêu cầu đang mở cho cùng nghiệp vụ → trả lỗi.
        /// </summary>
        public async Task<(ApprovalRequest? Request, string? Error)> CreateAsync(string businessType, Guid businessId, string? proposalJson = null)
        {
            var handler = HandlerOf(businessType);
            if (handler == null)
            {
                return (null, $"Chưa khai báo xử lý duyệt cho loại {businessType}.");
            }
            if (await _unitOfWork.ApprovalRequestRepository.GetAll().AnyAsync(a => !a.IsDeleted && a.BusinessType == businessType && a.BusinessId == businessId && OpenStatuses.Contains(a.RequestStatus)))
            {
                return (null, ConstantHelper.Message.ApprovalAlreadyPending);
            }
            var steps = await StepsForAsync(handler, businessId);
            var actor = _currentActor.RequireActor().UserId;
            DateTime now = _clock.Now;
            DateTime? due = DueOf(businessType, now);
            var request = new ApprovalRequest
            {
                Id = Guid.NewGuid(),
                RequestNo = await _documentNumberService.NextAsync(DocumentKind.Approval, _clock.Today),
                BusinessType = businessType,
                BusinessId = businessId,
                RequestStatus = Pending,
                CurrentStepNo = 1,
                RequestedAt = now,
                RequestedBy = actor,
                DueAt = due,
                CorrelationId = _currentActor.CorrelationId.Length <= 100 ? _currentActor.CorrelationId : _currentActor.CorrelationId[..100],
                PayloadJson = WrapPayload(await handler.BuildPayloadAsync(businessId), proposalJson),
            };
            _unitOfWork.ApprovalRequestRepository.Add(request);
            for (int i = 0; i < steps.Count; i++)
            {
                _unitOfWork.ApprovalStepRepository.Add(new ApprovalStep
                {
                    Id = Guid.NewGuid(), ApprovalRequestId = request.Id, StepNo = i + 1, DecisionStatus = StepWaiting,
                    AssignedAt = i == 0 ? now : null, DueAt = i == 0 ? due : null,
                });
            }
            // spec 2.3 bước 4: ApprovalItem (ItemType = BusinessType, ItemId = BusinessId, ActionCode = APPROVE); PayloadJson = quy trình đã chốt lúc gửi (GAP-APR-02).
            _unitOfWork.ApprovalItemRepository.Add(new ApprovalItem
            {
                Id = Guid.NewGuid(), ApprovalRequestId = request.Id, ItemType = businessType, ItemId = businessId, ActionCode = RouteActionCode, ItemStatus = Pending,
                PayloadJson = Newtonsoft.Json.JsonConvert.SerializeObject(new { steps }),
            });
            await _unitOfWork.ApprovalRequestRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
            return (request, null);
        }

        /// <summary>
        /// BE2.3: yêu cầu đang mở của 1 chứng từ + cấp đang chờ (cho chi tiết đề nghị mua, kiểm kê, PO…). Không có → null.
        /// </summary>
        public async Task<ApprovalProgressModel?> OpenProgressAsync(string businessType, Guid businessId)
        {
            var db = _unitOfWork.DbContext;
            var r = await db.ApprovalRequest.AsNoTracking()
                .Where(a => !a.IsDeleted && a.BusinessType == businessType && a.BusinessId == businessId && OpenStatuses.Contains(a.RequestStatus))
                .OrderByDescending(a => a.RequestedAt).FirstOrDefaultAsync();
            var handler = HandlerOf(businessType);
            if (r == null || handler == null)
            {
                return null;
            }
            var steps = await db.ApprovalStep.AsNoTracking().Where(s => s.ApprovalRequestId == r.Id && !s.IsDeleted).ToListAsync();
            var route = (await RoutesAsync([r.Id]))[r.Id];
            var step = CurrentStep(steps);
            int stepNo = step?.StepNo ?? r.CurrentStepNo ?? 1;
            string screen = await StepScreenAsync(handler, r.BusinessId, stepNo, route);
            DateTime? due = step?.DueAt ?? r.DueAt;
            return new ApprovalProgressModel
            {
                Id = r.Id, RequestNo = r.RequestNo, RequestStatus = r.RequestStatus, StepNo = stepNo, StepCount = Math.Max(route?.Count ?? 0, Math.Max(steps.Count, 1)),
                ScreenId = screen, StepName = (await StepNamesAsync([screen])).GetValueOrDefault(screen, screen), DueAt = due, IsOverdue = due != null && due < _clock.Now,
            };
        }

        /// <summary>
        /// Yêu cầu đang mở mà người đăng nhập duyệt được ở **cấp đang chờ** (quyền Duyệt trên mục của bước – D-31; admin thấy hết). type = mục quyền (screen-id) để lọc.
        /// BE2.3: người đã quyết định 1 cấp của yêu cầu không thấy cấp sau (trừ admin) khi bật <c>ApprovalRequireDistinctApprovers</c>.
        /// </summary>
        public async Task<IResults<ApprovalPendingModel>> GetMyPending(string? type)
        {
            try
            {
                Guid me = _currentActor.RequireActor().UserId;
                var perms = await _permissionService.GetAsync(me);
                var handlers = Handlers
                    .Where(h => string.IsNullOrEmpty(type) || ScreensOf(h).Contains(type, StringComparer.OrdinalIgnoreCase))
                    .Where(h => ScreensOf(h).Any(screen => _permissionService.IsAllowed(perms, screen, PermissionAction.Approve)))
                    .ToList();
                var types = handlers.Select(h => h.BusinessType).ToList();
                var rows = await _unitOfWork.ApprovalRequestRepository.GetAll().AsNoTracking()
                    .Where(a => !a.IsDeleted && OpenStatuses.Contains(a.RequestStatus) && a.BusinessType != null && types.Contains(a.BusinessType))
                    .OrderBy(a => a.RequestedAt).ToListAsync();
                var rowIds = rows.Select(r => r.Id).ToList();
                var allSteps = await _unitOfWork.DbContext.ApprovalStep.AsNoTracking().Where(s => rowIds.Contains(s.ApprovalRequestId) && !s.IsDeleted).ToListAsync();
                var routes = await RoutesAsync(rowIds);
                var userIds = rows.Where(r => r.RequestedBy != null).Select(r => r.RequestedBy!.Value).Distinct().ToList();
                var names = await UserNamesAsync(userIds);
                DateTime now = _clock.Now;
                var list = new List<ApprovalPendingModel>();
                foreach (var r in rows)
                {
                    var h = handlers.First(x => x.BusinessType.Equals(r.BusinessType, StringComparison.OrdinalIgnoreCase));
                    var steps = allSteps.Where(s => s.ApprovalRequestId == r.Id).ToList();
                    var step = CurrentStep(steps);
                    int stepNo = step?.StepNo ?? r.CurrentStepNo ?? 1;
                    var route = routes[r.Id];
                    // T15.1 / BE2.3: mục quyền theo bước đang chờ (vd. scm-pr-l1 → scm-pr-l2; PO ≥ ngưỡng GĐ → scm-po-director); lọc type theo mục đó.
                    string screen = await StepScreenAsync(h, r.BusinessId, stepNo, route);
                    if ((!string.IsNullOrEmpty(type) && !screen.Equals(type, StringComparison.OrdinalIgnoreCase))
                        || !_permissionService.IsAllowed(perms, screen, PermissionAction.Approve)
                        || (_rules.ApprovalRequireDistinctApprovers && !perms.IsAdmin && steps.Any(s => s.StepNo != stepNo && s.ApproverUserId == me)))
                    {
                        continue;
                    }
                    DateTime? due = step?.DueAt ?? r.DueAt;
                    list.Add(new ApprovalPendingModel
                    {
                        Id = r.Id, RequestNo = r.RequestNo, BusinessType = r.BusinessType, BusinessId = r.BusinessId, ScreenId = screen,
                        Title = r.BusinessId is { } bid ? await h.DescribeAsync(bid) : null,
                        RequestedAt = r.RequestedAt, RequestedBy = r.RequestedBy,
                        RequestedByName = r.RequestedBy is { } uid ? names.GetValueOrDefault(uid) : null,
                        RequestStatus = r.RequestStatus, StepNo = stepNo, StepCount = Math.Max(route?.Count ?? 0, Math.Max(steps.Count, 1)),
                        DueAt = due, IsOverdue = due != null && due < now,
                    });
                }
                var stepNames = await StepNamesAsync(list.Select(x => x.ScreenId!));
                list.ForEach(x => x.StepName = stepNames.GetValueOrDefault(x.ScreenId!, x.ScreenId!));
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
        /// Duyệt / từ chối **cấp đang chờ** trong 1 transaction (spec 11.5 decide): kiểm quyền Duyệt trên mục của bước, kiểm nội dung không bị sửa sau khi gửi
        /// (hash – mỗi cấp), ghi ApprovalDecision (chỉ ghi thêm, có PayloadHash), cập nhật bước + yêu cầu. BE2.3: duyệt cấp chưa cuối → giao cấp sau
        /// (yêu cầu InProgress), không thực thi nghiệp vụ; duyệt cấp cuối → thực thi handler; từ chối ở bất kỳ cấp nào → các cấp sau Skipped + handler từ chối.
        /// Lỗi ở bất kỳ bước nào → không ghi gì.
        /// </summary>
        public async Task<IResult<bool>> Decide(ApprovalDecideRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var (error, message) = await DecideCoreAsync(request);
                if (error != null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<bool>.Fail(error, false);
                }
                _unitOfWork.CommitTransaction();
                return Result<bool>.Success(true, message ?? ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Decide: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        private async Task<(string? Error, string? Message)> DecideCoreAsync(ApprovalDecideRequest request)
        {
            // Rà soát BE: 2 lần duyệt cùng lúc chạy lần lượt – lần 2 thấy yêu cầu đã đóng (không áp điều chỉnh tồn 2 lần).
            await DbLocks.DocumentAsync(_unitOfWork.DbContext, "ApprovalRequest", request.Id);
            var approval = _unitOfWork.ApprovalRequestRepository.GetSingle(a => a.Id == request.Id && !a.IsDeleted);
            if (approval == null)
            {
                return (ConstantHelper.Message.RecordNotFoundMessage, null);
            }
            if (!IsOpen(approval.RequestStatus))
            {
                return (string.Format(ConstantHelper.Message.ApprovalNotPending, StatusLabel(approval.RequestStatus)), null);
            }
            var handler = HandlerOf(approval.BusinessType);
            if (handler == null || approval.BusinessId is not { } businessId)
            {
                return ($"Chưa khai báo xử lý duyệt cho loại {approval.BusinessType}.", null);
            }
            var steps = _unitOfWork.ApprovalStepRepository.GetAll().Where(s => s.ApprovalRequestId == approval.Id && !s.IsDeleted).OrderBy(s => s.StepNo).ToList();
            var route = (await RoutesAsync([approval.Id]))[approval.Id];
            int stepCount = Math.Max(route?.Count ?? 0, Math.Max(steps.Count, 1));
            var step = CurrentStep(steps);
            int stepNo = step?.StepNo ?? approval.CurrentStepNo ?? 1;
            Guid actor = _currentActor.RequireActor().UserId;
            var perms = await _permissionService.GetAsync(actor);
            string screen = await StepScreenAsync(handler, businessId, stepNo, route);
            if (!_permissionService.IsAllowed(perms, screen, PermissionAction.Approve))
            {
                return (ConstantHelper.Message.ApprovalNoRight, null);
            }
            if (_rules.ApprovalRequireDistinctApprovers && !perms.IsAdmin && steps.FirstOrDefault(s => s.StepNo != stepNo && s.ApproverUserId == actor) is { } mine)
            {
                return (string.Format(ConstantHelper.Message.ApprovalAlreadyDecidedStep, mine.StepNo), null);
            }
            DateTime now = _clock.Now;
            DateTime? due = step?.DueAt;
            if (_rules.ApprovalBlockOverdueDecision && due != null && due < now)
            {
                return (string.Format(ConstantHelper.Message.ApprovalStepOverdue, stepNo, due), null);
            }
            string? note = request.Note?.Trim();
            if (!request.Approve && string.IsNullOrEmpty(note))
            {
                return (ConstantHelper.Message.RejectReasonRequired, null);
            }
            string current = await handler.BuildPayloadAsync(businessId) ?? string.Empty;
            // T13: hash chỉ tính trên nội dung nghiệp vụ (state); đề xuất (nếu có) chuyển cho handler khi duyệt. BE2.3: kiểm ở mọi cấp.
            var (submittedState, proposal) = UnwrapPayload(approval.PayloadJson);
            string submittedHash = Hash(submittedState);
            if (request.Approve && Hash(current) != submittedHash)
            {
                return (ConstantHelper.Message.ApprovalPayloadChanged, null);
            }
            if (step == null)
            {
                // Yêu cầu tạo trước T12 (không có bước) → tạo bước để ghi quyết định.
                step = new ApprovalStep { Id = Guid.NewGuid(), ApprovalRequestId = approval.Id, StepNo = stepNo, AssignedAt = approval.RequestedAt };
                _unitOfWork.ApprovalStepRepository.Add(step);
                steps.Add(step);
            }
            else
            {
                _unitOfWork.ApprovalStepRepository.Update(step);
            }
            step.DecisionStatus = request.Approve ? Approved : Rejected;
            step.DecidedAt = now;
            step.DecisionNote = due != null && due < now ? string.Concat("[QUÁ HẠN] ", note).Trim() : note;
            step.ApproverUserId = actor;
            _unitOfWork.ApprovalDecisionRepository.Add(new ApprovalDecision
            {
                Id = Guid.NewGuid(), ApprovalRequestId = approval.Id, ApprovalStepId = step.Id, DecisionCode = request.Approve ? "APPROVE" : "REJECT",
                DecidedBy = actor, DecidedAt = now, Comment = note, PayloadHash = submittedHash,
            });
            string? message = null;
            if (request.Approve && stepNo < stepCount)
            {
                // BE2.3: còn cấp sau → giao bước kế tiếp, yêu cầu Đang duyệt; nghiệp vụ chưa đổi (handler chỉ chạy ở cấp cuối).
                var next = steps.FirstOrDefault(s => s.StepNo == stepNo + 1);
                if (next == null)
                {
                    next = new ApprovalStep { Id = Guid.NewGuid(), ApprovalRequestId = approval.Id, StepNo = stepNo + 1, DecisionStatus = StepWaiting };
                    _unitOfWork.ApprovalStepRepository.Add(next);
                }
                else
                {
                    _unitOfWork.ApprovalStepRepository.Update(next);
                }
                next.AssignedAt = now;
                next.DueAt = DueOf(approval.BusinessType, now);
                approval.RequestStatus = InProgress;
                approval.CurrentStepNo = next.StepNo;
                approval.DueAt = next.DueAt;
                _unitOfWork.ApprovalRequestRepository.Update(approval);
                string nextScreen = await StepScreenAsync(handler, businessId, next.StepNo, route);
                message = string.Format(ConstantHelper.Message.ApprovalStepApproved, stepNo, stepCount, next.StepNo,
                    (await StepNamesAsync([nextScreen])).GetValueOrDefault(nextScreen, nextScreen));
            }
            else
            {
                approval.RequestStatus = request.Approve ? Approved : Rejected;
                approval.CompletedAt = now;
                _unitOfWork.ApprovalRequestRepository.Update(approval);
                foreach (var rest in steps.Where(s => s.StepNo > stepNo && s.DecisionStatus == StepWaiting))
                {
                    rest.DecisionStatus = StepSkipped;
                    _unitOfWork.ApprovalStepRepository.Update(rest);
                }
                await SetRouteStatusAsync(approval.Id, approval.RequestStatus);
                // Nhiều cấp: lý do từ chối kèm cấp để bản ghi nghiệp vụ biết cấp nào trả về (vd. đề nghị mua về Nháp).
                string? rejectNote = stepCount > 1 ? $"[Cấp {stepNo}/{stepCount}] {note}" : note;
                string? executed = request.Approve
                    ? await handler.ApproveAsync(businessId, actor, note, proposal)
                    : await handler.RejectAsync(businessId, actor, rejectNote!);
                if (executed != null)
                {
                    return (executed, null);
                }
            }
            await _unitOfWork.ApprovalRequestRepository.CommitAsync(ConstantHelper.TypeSave.Update);
            return (null, message);
        }

        /// <summary>Cập nhật ItemStatus của dòng ApprovalItem lưu quy trình theo kết quả yêu cầu (không phải sổ chỉ ghi thêm).</summary>
        private async Task SetRouteStatusAsync(Guid requestId, string status)
        {
            foreach (var item in await _unitOfWork.DbContext.ApprovalItem.Where(i => i.ApprovalRequestId == requestId && !i.IsDeleted && i.ActionCode == RouteActionCode).ToListAsync())
            {
                item.ItemStatus = status;
                _unitOfWork.ApprovalItemRepository.Update(item);
            }
        }

        // ============================================================ BE2.1 (spec 11.5) – chi tiết + hủy yêu cầu duyệt

        private async Task<Dictionary<Guid, string?>> UserNamesAsync(IEnumerable<Guid> ids)
        {
            var list = ids.Distinct().ToList();
            return await (from u in _unitOfWork.UserRepository.GetAll()
                          join p in _unitOfWork.PersonRepository.GetAll() on u.PersonId equals p.Id into pp
                          from p in pp.DefaultIfEmpty()
                          where list.Contains(u.Id)
                          select new { u.Id, Name = p != null && p.FullName != null ? p.FullName : u.UserName }).ToDictionaryAsync(x => x.Id, x => (string?)x.Name);
        }

        private static string StatusLabel(string? status) => status switch
        {
            Pending => "Chờ duyệt",
            InProgress => "Đang duyệt",
            Approved => "Đã duyệt",
            Rejected => "Từ chối",
            Cancelled => "Đã hủy",
            _ => status ?? string.Empty,
        };

        /// <summary>Bản ghi nghiệp vụ được duyệt (theo BusinessType → bảng, mục 2.3 spec): PO + dòng, công thức + dòng, đơn hàng + dòng, đề nghị mua + dòng, kiểm kê + dòng…</summary>
        private async Task<ApprovalBusinessModel> BusinessAsync(string? businessType, Guid? businessId)
        {
            var db = _unitOfWork.DbContext;
            var model = new ApprovalBusinessModel { Id = businessId };
            if (businessId is not { } id)
            {
                return model;
            }
            switch (businessType)
            {
                case PurchaseOrderStatus.BusinessType:
                    model.Table = "PurchaseOrder";
                    model.Record = await db.PurchaseOrder.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
                    model.Lines = await db.PurchaseOrderLine.AsNoTracking().Where(x => x.PurchaseOrderId == id && !x.IsDeleted).ToListAsync();
                    break;
                case PurchaseRequestStatus.BusinessType:
                    model.Table = "PurchaseRequest";
                    model.Record = await db.PurchaseRequest.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
                    model.Lines = await db.PurchaseRequestLine.AsNoTracking().Where(x => x.PurchaseRequestId == id && !x.IsDeleted).OrderBy(x => x.CreatedDate).ToListAsync();
                    break;
                case StockCountStatus.BusinessType:
                    model.Table = "StockCount";
                    model.Record = await db.StockCount.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
                    model.Lines = await db.StockCountLine.AsNoTracking().Where(x => x.StockCountId == id && !x.IsDeleted && x.VarianceQuantity != 0).ToListAsync();
                    break;
                case StockAdjustmentStatus.BusinessType:
                    // Không có bảng phiếu điều chỉnh (GAP-F-04): nội dung đề nghị nằm ở ProposalJson; đã duyệt → bút toán ADJUSTMENT có ReferenceId = BusinessId.
                    model.Table = "InventoryTransaction";
                    model.Lines = await db.InventoryTransaction.AsNoTracking()
                        .Where(x => !x.IsDeleted && x.ReferenceType == InventoryReferenceType.ManualAdjustment && x.ReferenceId == id).ToListAsync();
                    break;
                case RecipeStatus.BusinessType:
                    model.Table = "Recipe";
                    model.Record = await db.Recipe.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
                    model.Lines = await db.RecipeLine.AsNoTracking().Where(x => x.RecipeId == id && !x.IsDeleted).OrderBy(x => x.SortOrder).ToListAsync();
                    break;
                case ContractStatus.BusinessType:
                    model.Table = "Contract";
                    model.Record = await db.Contract.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
                    model.Lines = await db.ContractMeal.AsNoTracking().Where(x => x.ContractId == id && !x.IsDeleted).OrderBy(x => x.MealType).ToListAsync();
                    break;
                case MenuChangeStatus.BusinessType:
                    model.Table = "MenuChangeRequest";
                    model.Record = await db.MenuChangeRequest.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
                    break;
                case LeaveRequestStatus.BusinessType:
                    // BE2.6: đơn nghỉ + các ca đang phân trong khoảng nghỉ (sẽ bỏ khi duyệt).
                    model.Table = "LeaveRequest";
                    var leave = await db.LeaveRequest.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
                    model.Record = leave;
                    model.Lines = leave == null ? null : await db.ShiftAssignment.AsNoTracking()
                        .Where(x => !x.IsDeleted && x.EmployeeId == leave.EmployeeId && x.WorkDate >= leave.FromDate && x.WorkDate <= leave.ToDate).OrderBy(x => x.WorkDate).ToListAsync();
                    break;
                case ShiftSwapStatus.BusinessType:
                    // BE2.6: đơn đổi ca + 2 ca (hiện tại, đề xuất).
                    model.Table = "ShiftSwapRequest";
                    var swap = await db.ShiftSwapRequest.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
                    model.Record = swap;
                    model.Lines = swap == null ? null : await db.ShiftAssignment.AsNoTracking()
                        .Where(x => x.Id == swap.CurrentShiftAssignmentId || x.Id == swap.ProposedShiftAssignmentId).ToListAsync();
                    break;
                case OrderStatus.DeviationBusinessType:
                case OrderStatus.ChangeAfterCutoffBusinessType:
                    model.Table = "Order";
                    model.Record = await db.Order.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
                    model.Lines = await db.OrderLine.AsNoTracking().Where(x => x.OrderId == id && !x.IsDeleted).ToListAsync();
                    break;
                default:
                    model.Table = businessType;
                    break;
            }
            return model;
        }

        /// <summary>
        /// BE2.1 (spec 11.5 GET approvals/{id}, Catalog 211): yêu cầu + bước + quyết định (tên người quyết định) + mục + bản ghi nghiệp vụ (kèm dòng) + đề xuất,
        /// <c>canDecide</c> (còn mở và có quyền Duyệt mục của **cấp đang chờ**), <c>canCancel</c> (còn mở và là người gửi / admin).
        /// BE2.3: <c>listStepInfo</c> (mục quyền, tên bước, người quyết định, hạn / quá hạn từng bước), <c>currentStepNo</c> / <c>stepCount</c>.
        /// </summary>
        public async Task<IResult<ApprovalDetailModel>> GetDetail(Guid id)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var approval = await db.ApprovalRequest.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);
                if (approval == null)
                {
                    return Result<ApprovalDetailModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                Guid actor = _currentActor.RequireActor().UserId;
                var perms = await _permissionService.GetAsync(actor);
                var handler = HandlerOf(approval.BusinessType);
                var steps = await db.ApprovalStep.AsNoTracking().Where(s => s.ApprovalRequestId == id && !s.IsDeleted).OrderBy(s => s.StepNo).ToListAsync();
                var route = (await RoutesAsync([id]))[id];
                int stepCount = Math.Max(route?.Count ?? 0, Math.Max(steps.Count, 1));
                bool open = IsOpen(approval.RequestStatus);
                var current = open ? CurrentStep(steps) : null;
                int currentNo = open ? current?.StepNo ?? approval.CurrentStepNo ?? 1 : 0;
                var screens = new Dictionary<int, string>();
                if (handler != null)
                {
                    for (int n = 1; n <= stepCount; n++)
                    {
                        screens[n] = await StepScreenAsync(handler, approval.BusinessId, n, route);
                    }
                }
                string? screen = handler == null ? null : screens.GetValueOrDefault(open ? currentNo : stepCount);
                bool canApproveCurrent = open && screen != null && _permissionService.IsAllowed(perms, screen, PermissionAction.Approve)
                    && !(_rules.ApprovalRequireDistinctApprovers && !perms.IsAdmin && steps.Any(s => s.StepNo != currentNo && s.ApproverUserId == actor));
                bool canApproveAny = screens.Values.Any(s => _permissionService.IsAllowed(perms, s, PermissionAction.Approve));
                // Xem được: người gửi, người có quyền Duyệt một cấp của yêu cầu, admin, hoặc người có quyền Xem hộp duyệt chung #/wf-inbox.
                if (approval.RequestedBy != actor && !perms.IsAdmin && !canApproveAny && !_permissionService.IsAllowed(perms, "wf-inbox", PermissionAction.View))
                {
                    return Result<ApprovalDetailModel>.Fail(ConstantHelper.Message.ApprovalNoRight);
                }
                var decisions = await db.ApprovalDecision.AsNoTracking().Where(d => d.ApprovalRequestId == id && !d.IsDeleted).OrderBy(d => d.DecidedAt).ToListAsync();
                var names = await UserNamesAsync(decisions.Select(d => d.DecidedBy)
                    .Concat(steps.Where(s => s.ApproverUserId != null).Select(s => s.ApproverUserId!.Value))
                    .Concat(approval.RequestedBy is { } rb ? [rb] : []));
                var stepNames = await StepNamesAsync(screens.Values);
                DateTime now = _clock.Now;
                DateTime? due = open ? current?.DueAt ?? approval.DueAt : null;
                return Result<ApprovalDetailModel>.Success(new ApprovalDetailModel
                {
                    Request = approval,
                    StatusName = StatusLabel(approval.RequestStatus),
                    RequestedByName = approval.RequestedBy is { } r ? names.GetValueOrDefault(r) : null,
                    ScreenId = screen,
                    Title = handler != null && approval.BusinessId is { } b2 ? await handler.DescribeAsync(b2) : null,
                    ListStep = steps,
                    ListStepInfo = [.. steps.Select(s => new ApprovalStepInfoModel
                    {
                        Id = s.Id, StepNo = s.StepNo, ScreenId = screens.GetValueOrDefault(s.StepNo),
                        StepName = screens.TryGetValue(s.StepNo, out var sc) ? stepNames.GetValueOrDefault(sc, sc) : null,
                        DecisionStatus = s.DecisionStatus, DecisionStatusName = StepStatusLabel(s.DecisionStatus), IsCurrent = open && s.StepNo == currentNo,
                        ApproverUserId = s.ApproverUserId, ApproverName = s.ApproverUserId is { } au ? names.GetValueOrDefault(au) : null,
                        AssignedAt = s.AssignedAt, DueAt = s.DueAt, IsOverdue = s.DecisionStatus == StepWaiting && s.DueAt != null && s.DueAt < now,
                        DecidedAt = s.DecidedAt, DecisionNote = s.DecisionNote,
                    })],
                    ListDecision = [.. decisions.Select(d => new ApprovalDecisionItemModel { Decision = d, DecidedByName = names.GetValueOrDefault(d.DecidedBy) })],
                    ListItem = await db.ApprovalItem.AsNoTracking().Where(i => i.ApprovalRequestId == id && !i.IsDeleted).ToListAsync(),
                    Business = await BusinessAsync(approval.BusinessType, approval.BusinessId),
                    ProposalJson = UnwrapPayload(approval.PayloadJson).Proposal,
                    CanDecide = canApproveCurrent,
                    CanCancel = open && (approval.RequestedBy == actor || perms.IsAdmin),
                    CurrentStepNo = currentNo,
                    StepCount = stepCount,
                    DueAt = due,
                    IsOverdue = due != null && due < now,
                }, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetDetail: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>
        /// BE2.1 (spec 11.5 cancel, Catalog 213): người gửi (hoặc admin) hủy yêu cầu còn mở (BE2.3: ở bất kỳ cấp nào), lý do bắt buộc – 1 transaction: bản ghi
        /// nghiệp vụ về Nháp qua handler (<see cref="IApprovalHandler.CancelAsync"/>), bước đang chờ → Cancelled, các cấp sau → Skipped, ghi thêm ApprovalDecision
        /// <c>CANCEL</c> (sổ chỉ ghi thêm, PayloadHash nội dung đã gửi), yêu cầu → Cancelled (PROPOSED – GAP-APR-01).
        /// </summary>
        public async Task<IResult<bool>> Cancel(ApprovalCancelRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                await DbLocks.DocumentAsync(_unitOfWork.DbContext, "ApprovalRequest", request.Id); // Rà soát BE: hủy và duyệt cùng lúc chạy lần lượt.
                var approval = _unitOfWork.ApprovalRequestRepository.GetSingle(a => a.Id == request.Id && !a.IsDeleted);
                if (approval == null)
                {
                    return Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                if (!IsOpen(approval.RequestStatus))
                {
                    return Fail(string.Format(ConstantHelper.Message.ApprovalNotPending, StatusLabel(approval.RequestStatus)));
                }
                Guid actor = _currentActor.RequireActor().UserId;
                var perms = await _permissionService.GetAsync(actor);
                if (approval.RequestedBy != actor && !perms.IsAdmin)
                {
                    return Fail(ConstantHelper.Message.ApprovalCancelOnlyRequester);
                }
                string? reason = request.Reason?.Trim();
                if (string.IsNullOrEmpty(reason))
                {
                    return Fail(ConstantHelper.Message.ApprovalCancelReasonRequired);
                }
                var handler = HandlerOf(approval.BusinessType);
                if (handler == null || approval.BusinessId is not { } businessId)
                {
                    return Fail($"Chưa khai báo xử lý duyệt cho loại {approval.BusinessType}.");
                }
                string? executed = await handler.CancelAsync(businessId, actor, reason);
                if (executed != null)
                {
                    return Fail(executed);
                }
                DateTime now = _clock.Now;
                var steps = _unitOfWork.ApprovalStepRepository.GetAll().Where(s => s.ApprovalRequestId == approval.Id && !s.IsDeleted).OrderBy(s => s.StepNo).ToList();
                var step = CurrentStep(steps);
                if (step == null)
                {
                    step = new ApprovalStep { Id = Guid.NewGuid(), ApprovalRequestId = approval.Id, StepNo = approval.CurrentStepNo ?? 1, AssignedAt = approval.RequestedAt };
                    _unitOfWork.ApprovalStepRepository.Add(step);
                }
                else
                {
                    _unitOfWork.ApprovalStepRepository.Update(step);
                }
                step.DecisionStatus = Cancelled;
                step.DecidedAt = now;
                step.DecisionNote = "Người gửi hủy: " + reason;
                foreach (var rest in steps.Where(s => s.Id != step.Id && s.StepNo > step.StepNo && s.DecisionStatus == StepWaiting))
                {
                    rest.DecisionStatus = StepSkipped;
                    _unitOfWork.ApprovalStepRepository.Update(rest);
                }
                _unitOfWork.ApprovalDecisionRepository.Add(new ApprovalDecision
                {
                    Id = Guid.NewGuid(), ApprovalRequestId = approval.Id, ApprovalStepId = step.Id, DecisionCode = "CANCEL",
                    DecidedBy = actor, DecidedAt = now, Comment = reason, PayloadHash = Hash(UnwrapPayload(approval.PayloadJson).State),
                });
                approval.RequestStatus = Cancelled;
                approval.CompletedAt = now;
                _unitOfWork.ApprovalRequestRepository.Update(approval);
                await SetRouteStatusAsync(approval.Id, Cancelled);
                await _unitOfWork.ApprovalRequestRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                return Result<bool>.Success(true, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Cancel: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }

            IResult<bool> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                return Result<bool>.Fail(message, false);
            }
        }
    }
}
