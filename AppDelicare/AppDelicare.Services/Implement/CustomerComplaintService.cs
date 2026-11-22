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
using Serilog;
using System.Net;

using AppDelicare.Services.Implement.Messaging;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// T19 (spec 10.5, BUSINESS_RULES §9, Pilot P8) – phản ánh khách <c>PA-yyMMdd-NNNN</c>: Create (Mới, hạn SLA theo mức độ) → Assign (Đang xử lý) → Resolve (Đã xử lý)
    /// → Close (Đóng); Reject (Mới / Đang xử lý → Từ chối); AddComment. Mọi bước ghi ComplaintHistory (sổ chỉ ghi thêm). Cảnh báo / leo thang SLA tự động → sau Pilot.
    /// </summary>
    public class CustomerComplaintService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper, IReferenceGuard referenceGuard
        , IDocumentNumberService documentNumberService, IClock clock, ICurrentActor currentActor, IOptions<BusinessRulesOptions> rules, INotificationService notifications, IOptions<ReferenceListsOptions> referenceLists) : ICustomerComplaintService
    {
        private readonly ReferenceListsOptions _referenceLists = referenceLists.Value;
        private readonly INotificationService _notifications = notifications; // BOT-1.7
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IReferenceGuard _referenceGuard = referenceGuard;
        private readonly IDocumentNumberService _documentNumberService = documentNumberService;
        private readonly IClock _clock = clock;
        private readonly ICurrentActor _currentActor = currentActor;
        private readonly BusinessRulesOptions _rules = rules.Value;

        public async Task<IResults<CustomerComplaintViewModel>> GetAll(CustomerComplaintSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@CustomerId", request.CustomerId);
                dynamicParameters.Add("@OrderId", request.OrderId);
                dynamicParameters.Add("@DeliveryOrderId", request.DeliveryOrderId);
                dynamicParameters.Add("@DeliveryRouteId", request.DeliveryRouteId);
                dynamicParameters.Add("@ComplaintStatus", request.ComplaintStatus);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<CustomerComplaintViewModel>("uspCustomerComplaint_GetAll", dynamicParameters)).ToList();
                int i = 0;
                list.ForEach(x =>
                {
                    i++;
                    x.STT = (request.Page - 1) * request.PageSize + i;
                });
                return new Results<CustomerComplaintViewModel>
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

        public IResults<CustomerComplaint> GetAllData()
        {
            try
            {
                var list = _unitOfWork.CustomerComplaintRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<CustomerComplaint>
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

        public async Task<IResult<bool>> Save(CustomerComplaintRequest request, string typeSave)
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

        /// <summary>T19: phản ánh tạo qua CustomerComplaint/Create (số PA-, hạn SLA, lịch sử) – CRUD Insert không ghi.</summary>
        private Task<string> Insert(CustomerComplaintRequest request, string typeSave)
            => Task.FromResult(string.Format(ConstantHelper.Message.DeliveryCrudBlocked, "Phản ánh", "CustomerComplaint/Create"));

        /// <summary>T19: CRUD Update chỉ sửa phân loại / kênh / mô tả / mức độ (tính lại hạn SLA từ lúc nhận) khi Mới / Đang xử lý; trạng thái qua action.</summary>
        private async Task<string> Update(CustomerComplaintRequest request, string typeSave)
        {
            var exist = _unitOfWork.CustomerComplaintRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            if (exist.ComplaintStatus is not (ComplaintStatus.Open or ComplaintStatus.InProgress))
            {
                return InvalidStatus(exist, "sửa phản ánh", ComplaintStatus.Open, ComplaintStatus.InProgress);
            }
            if (request.Severity != null)
            {
                string? severity = ComplaintSeverity.Normalize(request.Severity);
                if (severity == null)
                {
                    return string.Format(ConstantHelper.Message.ComplaintSeverityInvalid, request.Severity);
                }
                exist.Severity = severity;
                exist.SlaDueAt = DeliveryRules.SlaDue(exist.ReceivedAt, severity, _rules);
            }
            // Lỗi UAT 2026-11-21: kênh tiếp nhận / phân loại lấy từ danh mục ReferenceLists (giá trị cũ giữ được khi không đổi).
            var (category, categoryError) = ReferenceCatalogs.CheckColumn(_referenceLists, "CustomerComplaint.CategoryCode", request.CategoryCode, exist.CategoryCode);
            var (channel, channelError) = ReferenceCatalogs.CheckColumn(_referenceLists, "CustomerComplaint.SourceChannel", request.SourceChannel, exist.SourceChannel);
            if ((categoryError ?? channelError) is { } catalogError)
            {
                return catalogError;
            }
            exist.CategoryCode = category ?? exist.CategoryCode;
            exist.SourceChannel = channel ?? exist.SourceChannel;
            exist.Description = DeliveryRules.Clean(request.Description) ?? exist.Description;
            _unitOfWork.CustomerComplaintRepository.Update(exist);
            await _unitOfWork.CustomerComplaintRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        /// <summary>T19: phản ánh không xóa (có số PA-, lịch sử) – dùng Reject.</summary>
        private Task<string> Delete(Guid? id, string typeSave)
            => Task.FromResult(string.Format(ConstantHelper.Message.DeliveryCrudBlocked, "Phản ánh", "CustomerComplaint/Reject (không xóa)"));

        private static string InvalidStatus(CustomerComplaint complaint, string action, params string[] expected)
            => string.Format(ConstantHelper.Message.StatusActionInvalid, action, "phản ánh " + complaint.ComplaintNo,
                string.Join(" / ", expected.Select(ComplaintStatus.Label)), ComplaintStatus.Label(complaint.ComplaintStatus));

        /// <summary>
        /// T19 (spec 10.5 POST complaints): tạo phản ánh – số <c>PA-yyMMdd-NNNN</c> (T11), lúc nhận = bây giờ (hoặc nhập), Mới, mức độ High / Medium / Low (mặc định Medium),
        /// hạn xử lý = lúc nhận + 4 / 24 / 72 giờ (<c>BusinessRules:ComplaintSlaHours</c>), lịch sử CREATED.
        /// Gắn được điểm dừng (→ đơn giao + chuyến + đơn hàng), đơn giao, chuyến, đơn hàng (phải cùng khách); mẻ nấu ghi vào mô tả "[Mẻ ME-…]" (không có cột – GAP-DLV-06).
        /// </summary>
        public async Task<IResult<CustomerComplaintDetailModel>> Create(ComplaintCreateRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var actor = _currentActor.RequireActor();
                var db = _unitOfWork.DbContext;
                if (!await db.Customer.AsNoTracking().AnyAsync(c => c.Id == request.CustomerId && !c.IsDeleted))
                {
                    return Fail(ConstantHelper.Message.ComplaintCustomerNotFound);
                }
                string? severity = ComplaintSeverity.Normalize(request.Severity);
                if (severity == null)
                {
                    return Fail(string.Format(ConstantHelper.Message.ComplaintSeverityInvalid, request.Severity));
                }
                if (string.IsNullOrWhiteSpace(request.Description))
                {
                    return Fail(ConstantHelper.Message.ComplaintCommentRequired);
                }
                // Lỗi UAT 2026-11-21: kênh tiếp nhận / phân loại lấy từ danh mục ReferenceLists (nhận mã hoặc nhãn → lưu mã).
                var (channel, channelError) = ReferenceCatalogs.CheckColumn(_referenceLists, "CustomerComplaint.SourceChannel", request.SourceChannel);
                var (category, categoryError) = ReferenceCatalogs.CheckColumn(_referenceLists, "CustomerComplaint.CategoryCode", request.CategoryCode);
                if ((channelError ?? categoryError) is { } catalogError)
                {
                    return Fail(catalogError);
                }
                Guid? orderId = Empty(request.OrderId), deliveryOrderId = Empty(request.DeliveryOrderId), routeId = Empty(request.DeliveryRouteId);
                if (Empty(request.DeliveryStopId) is { } stopId)
                {
                    var stop = await db.DeliveryStop.AsNoTracking().FirstOrDefaultAsync(s => s.Id == stopId && !s.IsDeleted);
                    if (stop == null || (stop.CustomerId != null && stop.CustomerId != request.CustomerId))
                    {
                        return Fail(string.Format(ConstantHelper.Message.ComplaintLinkInvalid, "Điểm dừng"));
                    }
                    routeId ??= stop.DeliveryRouteId;
                    deliveryOrderId ??= stop.DeliveryOrderId;
                }
                if (deliveryOrderId is { } doid)
                {
                    var deliveryOrder = await db.DeliveryOrder.AsNoTracking().FirstOrDefaultAsync(d => d.Id == doid && !d.IsDeleted);
                    if (deliveryOrder == null || deliveryOrder.CustomerId != request.CustomerId)
                    {
                        return Fail(string.Format(ConstantHelper.Message.ComplaintLinkInvalid, "Đơn giao"));
                    }
                    orderId ??= deliveryOrder.OrderId;
                    routeId ??= deliveryOrder.DeliveryRouteId;
                }
                if (orderId is { } oid && !await db.Order.AsNoTracking().AnyAsync(o => o.Id == oid && !o.IsDeleted && o.CustomerId == request.CustomerId))
                {
                    return Fail(string.Format(ConstantHelper.Message.ComplaintLinkInvalid, "Đơn hàng"));
                }
                if (routeId is { } rid && !await db.DeliveryRoute.AsNoTracking().AnyAsync(r => r.Id == rid && !r.IsDeleted))
                {
                    return Fail(string.Format(ConstantHelper.Message.ComplaintLinkInvalid, "Chuyến xe"));
                }
                string description = request.Description.Trim();
                if (Empty(request.ProductionBatchId) is { } batchId)
                {
                    var batch = await db.ProductionBatch.AsNoTracking().FirstOrDefaultAsync(b => b.Id == batchId && !b.IsDeleted);
                    if (batch == null)
                    {
                        return Fail(string.Format(ConstantHelper.Message.ComplaintLinkInvalid, "Mẻ nấu"));
                    }
                    description += " [Mẻ " + (batch.BatchNumber ?? batch.BatchNo) + "]";
                }
                var complaint = await DeliveryRules.NewComplaintAsync(_unitOfWork, _documentNumberService, _rules, actor, request.ReceivedAt ?? _clock.Now,
                    request.CustomerId, orderId, deliveryOrderId, routeId, channel, category, severity, description);
                await _unitOfWork.CustomerComplaintRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                // BOT-1.7: COMPLAINT_NEW → CSKH (quyền Thêm trên phản ánh); mức cao → bot DH (liên phòng).
                string? customerName = await _unitOfWork.DbContext.Customer.AsNoTracking().Where(c => c.Id == request.CustomerId).Select(c => c.Name).FirstOrDefaultAsync();
                await BusinessNotifier.ToMenusAsync(_notifications, _unitOfWork.DbContext, NotificationCatalog.ComplaintNew, ["#/log-complaint"],
                    new { title = $"{complaint.ComplaintNo} – {customerName}", detail = description, lines = new[] { "Mức: " + ComplaintSeverity.Label(severity) } },
                    $"{NotificationCatalog.ComplaintNew}:{complaint.Id:N}",
                    buttons: [new NotificationButton("📋 Xem phản ánh", BusinessNotifier.Callback("cmp", "v", complaint.Id))], except: actor.UserId, requireAdd: true,
                    botCode: severity == ComplaintSeverity.High ? AppDelicare.Model.Helper.MessagingCode.BotDieuHanh : null);
                _unitOfWork.CommitTransaction();
                return Result<CustomerComplaintDetailModel>.Success((await DetailAsync(complaint.Id))!, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Create: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                throw;
            }

            IResult<CustomerComplaintDetailModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                return Result<CustomerComplaintDetailModel>.Fail(message);
            }

            static Guid? Empty(Guid? id) => id is { } g && g != Guid.Empty ? g : null;
        }

        /// <summary>Khung chung thao tác phản ánh (1 transaction): nạp, kiểm trạng thái, chạy thân (đổi trạng thái + lịch sử), lưu, trả chi tiết.</summary>
        private async Task<IResult<CustomerComplaintDetailModel>> RunAsync(Guid id, string action, string[] allowed,
            Func<CustomerComplaint, ActorInfo, DateTime, Task<string?>> body)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var actor = _currentActor.RequireActor();
                var complaint = _unitOfWork.CustomerComplaintRepository.GetSingle(c => c.Id == id && !c.IsDeleted);
                if (complaint == null)
                {
                    return Fail(ConstantHelper.Message.ComplaintNotFound);
                }
                if (!allowed.Contains(complaint.ComplaintStatus))
                {
                    return Fail(InvalidStatus(complaint, action, allowed));
                }
                string? error = await body(complaint, actor, _clock.Now);
                if (error != null)
                {
                    return Fail(error);
                }
                _unitOfWork.CustomerComplaintRepository.Update(complaint);
                await _unitOfWork.CustomerComplaintRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                return Result<CustomerComplaintDetailModel>.Success((await DetailAsync(complaint.Id))!, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ", action, ": ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                throw;
            }

            IResult<CustomerComplaintDetailModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                return Result<CustomerComplaintDetailModel>.Fail(message);
            }
        }

        private void Transition(CustomerComplaint complaint, ActorInfo actor, DateTime at, string eventType, string newStatus, string? comment)
        {
            string? old = complaint.ComplaintStatus;
            complaint.ComplaintStatus = newStatus;
            DeliveryRules.AddHistory(_unitOfWork, actor, at, complaint.Id, eventType, old, newStatus, comment);
        }

        /// <summary>T19: giao xử lý – Mới / Đang xử lý → Đang xử lý; người xử lý (User.Id, trống = người thao tác); ghi chú tùy chọn.</summary>
        public Task<IResult<CustomerComplaintDetailModel>> Assign(ComplaintActionRequest request)
            => RunAsync(request.Id, "giao xử lý", [ComplaintStatus.Open, ComplaintStatus.InProgress], async (complaint, actor, at) =>
            {
                Guid assignee = request.AssignedTo is { } a && a != Guid.Empty ? a : actor.UserId;
                if (!await _unitOfWork.DbContext.User.AsNoTracking().AnyAsync(u => u.Id == assignee && !u.IsDeleted))
                {
                    return ConstantHelper.Message.ComplaintAssigneeNotFound;
                }
                complaint.AssignedTo = assignee;
                Transition(complaint, actor, at, DeliveryCode.ComplaintAssigned, ComplaintStatus.InProgress, request.Comment);
                return null;
            });

        /// <summary>T19: đã xử lý – Đang xử lý → Đã xử lý; cách xử lý bắt buộc (Resolution), ResolvedAt.</summary>
        public Task<IResult<CustomerComplaintDetailModel>> Resolve(ComplaintActionRequest request)
            => RunAsync(request.Id, "ghi đã xử lý", [ComplaintStatus.InProgress], (complaint, actor, at) =>
            {
                if (string.IsNullOrWhiteSpace(request.Comment))
                {
                    return Task.FromResult<string?>(ConstantHelper.Message.ComplaintCommentRequired);
                }
                complaint.Resolution = request.Comment.Trim();
                complaint.ResolvedAt = at;
                Transition(complaint, actor, at, DeliveryCode.ComplaintResolved, ComplaintStatus.Resolved, request.Comment);
                return Task.FromResult<string?>(null);
            });

        /// <summary>T19: đóng – Đã xử lý → Đóng (khách đã xác nhận); ghi chú tùy chọn.</summary>
        public Task<IResult<CustomerComplaintDetailModel>> Close(ComplaintActionRequest request)
            => RunAsync(request.Id, "đóng phản ánh", [ComplaintStatus.Resolved], (complaint, actor, at) =>
            {
                Transition(complaint, actor, at, DeliveryCode.ComplaintClosed, ComplaintStatus.Closed, request.Comment);
                return Task.FromResult<string?>(null);
            });

        /// <summary>T19: từ chối – Mới / Đang xử lý → Từ chối (phản ánh không đúng); lý do bắt buộc.</summary>
        public Task<IResult<CustomerComplaintDetailModel>> Reject(ComplaintActionRequest request)
            => RunAsync(request.Id, "từ chối phản ánh", [ComplaintStatus.Open, ComplaintStatus.InProgress], (complaint, actor, at) =>
            {
                if (string.IsNullOrWhiteSpace(request.Comment))
                {
                    return Task.FromResult<string?>(ConstantHelper.Message.ComplaintCommentRequired);
                }
                complaint.Resolution = request.Comment.Trim();
                complaint.ResolvedAt = at;
                Transition(complaint, actor, at, DeliveryCode.ComplaintRejected, ComplaintStatus.Rejected, request.Comment);
                return Task.FromResult<string?>(null);
            });

        /// <summary>T19: thêm ghi chú xử lý (không đổi trạng thái) khi chưa Đóng / Từ chối.</summary>
        public Task<IResult<CustomerComplaintDetailModel>> AddComment(ComplaintActionRequest request)
            => RunAsync(request.Id, "ghi chú", [ComplaintStatus.Open, ComplaintStatus.InProgress, ComplaintStatus.Resolved], (complaint, actor, at) =>
            {
                if (string.IsNullOrWhiteSpace(request.Comment))
                {
                    return Task.FromResult<string?>(ConstantHelper.Message.ComplaintCommentRequired);
                }
                DeliveryRules.AddHistory(_unitOfWork, actor, at, complaint.Id, DeliveryCode.ComplaintComment, complaint.ComplaintStatus, complaint.ComplaintStatus, request.Comment);
                return Task.FromResult<string?>(null);
            });

        /// <summary>T19: chi tiết phản ánh + lịch sử + quá hạn SLA + thao tác được phép.</summary>
        public async Task<IResult<CustomerComplaintDetailModel>> GetDetail(Guid id)
        {
            try
            {
                var detail = await DetailAsync(id);
                return detail == null ? Result<CustomerComplaintDetailModel>.Fail(ConstantHelper.Message.ComplaintNotFound)
                    : Result<CustomerComplaintDetailModel>.Success(detail, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetDetail: ", ex.ToString()));
                throw;
            }
        }

        private async Task<CustomerComplaintDetailModel?> DetailAsync(Guid id)
        {
            var db = _unitOfWork.DbContext;
            var complaint = await db.CustomerComplaint.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
            if (complaint == null)
            {
                return null;
            }
            var customer = await db.Customer.AsNoTracking().FirstOrDefaultAsync(c => c.Id == complaint.CustomerId);
            var order = complaint.OrderId == null ? null : await db.Order.AsNoTracking().FirstOrDefaultAsync(o => o.Id == complaint.OrderId);
            var deliveryOrder = complaint.DeliveryOrderId == null ? null : await db.DeliveryOrder.AsNoTracking().FirstOrDefaultAsync(d => d.Id == complaint.DeliveryOrderId);
            var route = complaint.DeliveryRouteId == null ? null : await db.DeliveryRoute.AsNoTracking().FirstOrDefaultAsync(r => r.Id == complaint.DeliveryRouteId);
            var assignee = complaint.AssignedTo == null ? null : await db.User.AsNoTracking().FirstOrDefaultAsync(u => u.Id == complaint.AssignedTo);
            return new CustomerComplaintDetailModel
            {
                Complaint = complaint,
                StatusName = ComplaintStatus.Label(complaint.ComplaintStatus),
                SeverityName = ComplaintSeverity.Label(complaint.Severity),
                CustomerName = customer?.DisplayName ?? customer?.Name,
                OrderNumber = order?.OrderNumber ?? order?.OrderNo,
                DeliveryNumber = deliveryOrder?.DeliveryNumber ?? deliveryOrder?.DeliveryNo,
                RouteNo = route?.RouteNo,
                AssignedToName = assignee?.UserName,
                IsOverdue = complaint.SlaDueAt != null && complaint.SlaDueAt < _clock.Now && !ComplaintStatus.Done.Contains(complaint.ComplaintStatus),
                ListHistory = await db.ComplaintHistory.AsNoTracking().Where(h => h.ComplaintId == id && !h.IsDeleted).OrderBy(h => h.EventAt).ThenBy(h => h.CreatedDate).ToListAsync(),
                AllowedActions = complaint.ComplaintStatus switch
                {
                    ComplaintStatus.Open => ["assign", "reject", "addComment", "update"],
                    ComplaintStatus.InProgress => ["assign", "resolve", "reject", "addComment", "update"],
                    ComplaintStatus.Resolved => ["close", "addComment"],
                    _ => [],
                },
            };
        }

        /// <summary>
        /// BE2.1 (spec 10.5 GET complaints, Catalog 197): danh sách phản ánh đủ trường (khách, đơn, đơn giao, chuyến, người xử lý, tên trạng thái / mức độ),
        /// <c>isOverdue</c> = SlaDueAt &lt; bây giờ và chưa Đã xử lý / Đóng / Từ chối, số giờ còn lại; lọc trạng thái / mức độ / khách / quá hạn / kỳ nhận / tìm số, mô tả;
        /// đếm theo trạng thái + số quá hạn (trên tập đã lọc, trước phân trang). Quá hạn xếp trước, rồi hạn SLA gần nhất.
        /// </summary>
        public async Task<IResult<CustomerComplaintListModel>> GetList(ComplaintListRequest request)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var query = db.CustomerComplaint.AsNoTracking().Where(c => !c.IsDeleted);
                if (request.Mine == true)
                {
                    Guid me = _currentActor.RequireActor().UserId;
                    query = query.Where(c => c.CreatedBy == me);
                }
                if (!string.IsNullOrWhiteSpace(request.Status))
                {
                    query = query.Where(c => c.ComplaintStatus == request.Status);
                }
                if (!string.IsNullOrWhiteSpace(request.Severity))
                {
                    string severity = ComplaintSeverity.Normalize(request.Severity) ?? request.Severity.Trim();
                    query = query.Where(c => c.Severity == severity);
                }
                if (request.CustomerId is { } cid && cid != Guid.Empty)
                {
                    query = query.Where(c => c.CustomerId == cid);
                }
                if (request.From is { } from)
                {
                    query = query.Where(c => c.ReceivedAt >= from.Date);
                }
                if (request.To is { } to)
                {
                    DateTime end = to.Date.AddDays(1);
                    query = query.Where(c => c.ReceivedAt < end);
                }
                if (!string.IsNullOrWhiteSpace(request.SearchText))
                {
                    string q = request.SearchText.Trim();
                    query = query.Where(c => (c.ComplaintNo != null && c.ComplaintNo.Contains(q)) || (c.Description != null && c.Description.Contains(q)));
                }
                DateTime now = _clock.Now;
                var all = await query.ToListAsync();
                bool Overdue(CustomerComplaint c) => c.SlaDueAt != null && c.SlaDueAt < now && !ComplaintStatus.Done.Contains(c.ComplaintStatus);
                if (request.Overdue == true)
                {
                    all = [.. all.Where(Overdue)];
                }
                var counts = all.GroupBy(c => c.ComplaintStatus ?? string.Empty).ToDictionary(g => g.Key, g => g.Count());
                int page = Math.Max(1, request.Page), size = Math.Clamp(request.PageSize, 1, 500);
                var paged = all.OrderByDescending(Overdue).ThenBy(c => c.SlaDueAt ?? DateTime.MaxValue).ThenByDescending(c => c.ReceivedAt)
                    .Skip((page - 1) * size).Take(size).ToList();
                var customerIds = paged.Select(c => c.CustomerId).Distinct().ToList();
                var customers = await db.Customer.AsNoTracking().Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.DisplayName ?? c.Name);
                var orderIds = paged.Where(c => c.OrderId != null).Select(c => c.OrderId!.Value).Distinct().ToList();
                var orders = await db.Order.AsNoTracking().Where(o => orderIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.OrderNumber ?? o.OrderNo);
                var deliveryIds = paged.Where(c => c.DeliveryOrderId != null).Select(c => c.DeliveryOrderId!.Value).Distinct().ToList();
                var deliveries = await db.DeliveryOrder.AsNoTracking().Where(d => deliveryIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.DeliveryNumber ?? d.DeliveryNo);
                var routeIds = paged.Where(c => c.DeliveryRouteId != null).Select(c => c.DeliveryRouteId!.Value).Distinct().ToList();
                var routes = await db.DeliveryRoute.AsNoTracking().Where(r => routeIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.RouteNo ?? r.Code);
                var userIds = paged.Where(c => c.AssignedTo != null).Select(c => c.AssignedTo!.Value).Distinct().ToList();
                var users = await db.User.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.UserName);
                var list = paged.Select(c => new CustomerComplaintListItemModel
                {
                    Complaint = c,
                    StatusName = ComplaintStatus.Label(c.ComplaintStatus),
                    SeverityName = ComplaintSeverity.Label(c.Severity),
                    CustomerName = customers.GetValueOrDefault(c.CustomerId),
                    OrderNumber = c.OrderId is { } o ? orders.GetValueOrDefault(o) : null,
                    DeliveryNumber = c.DeliveryOrderId is { } d ? deliveries.GetValueOrDefault(d) : null,
                    RouteNo = c.DeliveryRouteId is { } r ? routes.GetValueOrDefault(r) : null,
                    AssignedToName = c.AssignedTo is { } u ? users.GetValueOrDefault(u) : null,
                    IsOverdue = Overdue(c),
                    HoursToDue = c.SlaDueAt is { } due && !ComplaintStatus.Done.Contains(c.ComplaintStatus) ? Math.Round((decimal)(due - now).TotalHours, 1) : null,
                }).ToList();
                return Result<CustomerComplaintListModel>.Success(new CustomerComplaintListModel
                {
                    ListItem = list,
                    TotalRecords = all.Count,
                    StatusCounts = counts,
                    OverdueCount = all.Count(Overdue),
                }, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetList: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<CustomerComplaint>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.CustomerComplaintRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<CustomerComplaint>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<CustomerComplaint>
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
