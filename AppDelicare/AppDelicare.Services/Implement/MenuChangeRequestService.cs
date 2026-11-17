#nullable enable
using AutoMapper;
using Dapper;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Globalization;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using AppDelicare.Services.Interface;
using Serilog;
using System.Net;

namespace AppDelicare.Services.Implement
{
    public class MenuChangeRequestService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper, IReferenceGuard referenceGuard
        , IClock clock, ICurrentActor currentActor, IDocumentNumberService documentNumberService, IApprovalRequestService approvalRequestService) : IMenuChangeRequestService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IReferenceGuard _referenceGuard = referenceGuard;
        private readonly IClock _clock = clock;
        private readonly ICurrentActor _currentActor = currentActor;
        private readonly IDocumentNumberService _documentNumberService = documentNumberService;
        private readonly IApprovalRequestService _approvalRequestService = approvalRequestService;

        public async Task<IResults<MenuChangeRequestViewModel>> GetAll(MenuChangeRequestSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@OrderId", request.OrderId);
                dynamicParameters.Add("@MenuId", request.MenuId);
                dynamicParameters.Add("@OriginalDishId", request.OriginalDishId);
                dynamicParameters.Add("@RequestedDishId", request.RequestedDishId);
                dynamicParameters.Add("@ShiftId", request.ShiftId);
                dynamicParameters.Add("@RequestStatus", request.RequestStatus);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<MenuChangeRequestViewModel>("uspMenuChangeRequest_GetAll", dynamicParameters)).ToList();
                int i = 0;
                list.ForEach(x =>
                {
                    i++;
                    x.STT = (request.Page - 1) * request.PageSize + i;
                });
                return new Results<MenuChangeRequestViewModel>
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

        public IResults<MenuChangeRequest> GetAllData()
        {
            try
            {
                var list = _unitOfWork.MenuChangeRequestRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<MenuChangeRequest>
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

        public async Task<IResult<bool>> Save(MenuChangeRequestRequest request, string typeSave)
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

        /// <summary>BE2.2: yêu cầu đổi món chỉ tạo qua <c>MenuChangeRequest/Create</c> (số PH-, người gửi, gửi duyệt MENU_CHANGE) – CRUD Insert không ghi.</summary>
        private Task<string> Insert(MenuChangeRequestRequest request, string typeSave)
            => Task.FromResult(ConstantHelper.Message.MenuChangeCrudBlocked);

        /// <summary>BE2.2: không sửa trực tiếp (nội dung đã gửi duyệt – hash); duyệt / từ chối / hủy qua hộp duyệt.</summary>
        private Task<string> Update(MenuChangeRequestRequest request, string typeSave)
            => Task.FromResult(ConstantHelper.Message.MenuChangeCrudBlocked);

        /// <summary>BE2.2: chỉ xóa (mềm) yêu cầu đã Từ chối / Hủy.</summary>
        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // T07: bản ghi đang được tham chiếu (quan hệ logic, truy vấn có tham số) → không cho xóa.
            if ((await _referenceGuard.FindUsagesAsync("MenuChangeRequest", (Guid)id)).Count > 0)
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            var delete = _unitOfWork.MenuChangeRequestRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            if (delete.RequestStatus is not (MenuChangeStatus.Rejected or MenuChangeStatus.Cancelled))
            {
                return string.Format(ConstantHelper.Message.StatusActionInvalid, "xóa", "yêu cầu đổi món",
                    MenuChangeStatus.Label(MenuChangeStatus.Rejected) + " / " + MenuChangeStatus.Label(MenuChangeStatus.Cancelled), MenuChangeStatus.Label(delete.RequestStatus));
            }
            _unitOfWork.MenuChangeRequestRepository.Update(delete);
            await _unitOfWork.MenuChangeRequestRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<MenuChangeRequest>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.MenuChangeRequestRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<MenuChangeRequest>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<MenuChangeRequest>
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

        // ============================================================ BE2.2 – yêu cầu đổi món (spec 6.4, Catalog 104)
        private static readonly string[] ClosedOrderStatuses = [OrderStatus.Cancelled, OrderStatus.Delivered, OrderStatus.Reconciled];

        private static string Num(decimal? v) => (v ?? 0m).ToString("0.####", CultureInfo.InvariantCulture);

        /// <summary>Thực đơn (trong <paramref name="menuIds"/>) có món <paramref name="dishId"/> ở thứ của ngày ăn.</summary>
        private async Task<List<Guid>> MenusWithDishAsync(IReadOnlyCollection<Guid> menuIds, Guid dishId, DateTime mealDate)
        {
            int dayOfWeek = MenuService.MenuDayOf(mealDate);
            return await _unitOfWork.DbContext.MenuLine.AsNoTracking()
                .Where(l => menuIds.Contains(l.MenuId) && !l.IsDeleted && l.DishId == dishId && l.DayOfWeek == dayOfWeek)
                .Select(l => l.MenuId).Distinct().ToListAsync();
        }

        /// <summary>
        /// Spec 6.4 POST change-requests (Catalog 104) – 1 transaction: kiểm lý do, món cũ / mới (khác nhau, đang dùng), số suất &gt; 0; có đơn → đơn chưa giao / chưa hủy,
        /// ngày ăn = ngày ăn của đơn, món cũ phải có trong thực đơn của đơn ở thứ đó, số suất trống = Σ suất dòng đơn có thực đơn đó, ca trống = ca của dòng (nếu 1 ca);
        /// không đơn → ngày ăn + số suất bắt buộc (đổi cho mọi đơn của ngày / ca). Số <c>PH-YYMMDD-NNNN</c>, RequestedBy = người đăng nhập, Chờ duyệt,
        /// ApprovalRequest MENU_CHANGE (engine T12, quyền mục <c>#/wf-inbox?type=menu-change-request</c>). Xin khách chấp thuận (G1) làm ngoài hệ thống (PROPOSED – D-51).
        /// </summary>
        public async Task<IResult<MenuChangeDetailModel>> Create(MenuChangeCreateRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var db = _unitOfWork.DbContext;
                string? reason = request.Reason?.Trim();
                if (string.IsNullOrEmpty(reason) || reason.Length < 3)
                {
                    return Fail(ConstantHelper.Message.MenuChangeReasonRequired);
                }
                var dishIds = new[] { request.OriginalDishId, request.RequestedDishId };
                if (request.OriginalDishId == request.RequestedDishId
                    || await db.Dish.AsNoTracking().CountAsync(d => dishIds.Contains(d.Id) && !d.IsDeleted) != 2)
                {
                    return Fail(ConstantHelper.Message.MenuChangeDishRequired);
                }
                if (request.Quantity is <= 0)
                {
                    return Fail(ConstantHelper.Message.MenuChangeQuantityInvalid);
                }
                if (request.ShiftId is { } sid && !await db.Shift.AsNoTracking().AnyAsync(s => s.Id == sid && !s.IsDeleted))
                {
                    return Fail(string.Format(ConstantHelper.Message.NotExist, "Ca").Trim());
                }
                DateTime mealDate;
                Guid? menuId = request.MenuId is { } m && m != Guid.Empty ? m : null;
                Guid? shiftId = request.ShiftId;
                decimal? quantity = request.Quantity;
                if (request.OrderId is { } orderId && orderId != Guid.Empty)
                {
                    var order = await db.Order.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted);
                    if (order == null)
                    {
                        return Fail(ConstantHelper.Message.ReconcileOrderNotFound);
                    }
                    if (ClosedOrderStatuses.Contains(order.Status))
                    {
                        return Fail(string.Format(ConstantHelper.Message.MenuChangeOrderInvalid, order.OrderNumber ?? order.OrderNo, OrderStatus.Label(order.Status)));
                    }
                    mealDate = order.MealDate.Date;
                    if (request.MealDate is { } md && md.Date != mealDate)
                    {
                        return Fail(string.Format(ConstantHelper.Message.MenuChangeDateMismatch, mealDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)));
                    }
                    var lines = await db.OrderLine.AsNoTracking().Where(l => l.OrderId == orderId && !l.IsDeleted && l.MenuId != null && l.Quantity > 0).ToListAsync();
                    var candidates = menuId != null ? [menuId.Value] : lines.Select(l => l.MenuId!.Value).Distinct().ToList();
                    var menus = await MenusWithDishAsync(candidates, request.OriginalDishId, mealDate);
                    if (menus.Count == 0)
                    {
                        return Fail(string.Format(ConstantHelper.Message.MenuChangeDishNotInMenu, mealDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)));
                    }
                    menuId ??= menus.Count == 1 ? menus[0] : null;
                    var affected = lines.Where(l => menus.Contains(l.MenuId!.Value) && (shiftId == null || l.ShiftId == shiftId)).ToList();
                    quantity ??= affected.Sum(l => l.Quantity);
                    shiftId ??= affected.Select(l => l.ShiftId).Distinct().Count() == 1 ? affected[0].ShiftId : null;
                    if (quantity is not > 0)
                    {
                        return Fail(ConstantHelper.Message.MenuChangeQuantityInvalid);
                    }
                }
                else
                {
                    if (request.MealDate == null)
                    {
                        return Fail(string.Format(ConstantHelper.Message.NotExist, "Ngày ăn").Trim());
                    }
                    if (quantity == null)
                    {
                        return Fail(ConstantHelper.Message.MenuChangeQuantityInvalid);
                    }
                    mealDate = request.MealDate.Value.Date;
                    if (menuId != null && (await MenusWithDishAsync([menuId.Value], request.OriginalDishId, mealDate)).Count == 0)
                    {
                        return Fail(string.Format(ConstantHelper.Message.MenuChangeDishNotInMenu, mealDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)));
                    }
                }
                var entity = new MenuChangeRequest
                {
                    Id = Guid.NewGuid(),
                    RequestNo = await _documentNumberService.NextAsync(DocumentKind.Voucher, _clock.Today),
                    OrderId = request.OrderId is { } oid && oid != Guid.Empty ? oid : null,
                    MenuId = menuId,
                    OriginalDishId = request.OriginalDishId,
                    RequestedDishId = request.RequestedDishId,
                    MealDate = mealDate,
                    ShiftId = shiftId,
                    Quantity = quantity,
                    Reason = reason,
                    RequestedBy = _currentActor.RequireActor().UserId,
                    RequestStatus = MenuChangeStatus.Pending,
                };
                _unitOfWork.MenuChangeRequestRepository.Add(entity);
                await _unitOfWork.MenuChangeRequestRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                var (approval, approvalError) = await _approvalRequestService.CreateAsync(MenuChangeStatus.BusinessType, entity.Id);
                if (approvalError != null)
                {
                    return Fail(approvalError);
                }
                _unitOfWork.CommitTransaction();
                return Result<MenuChangeDetailModel>.Success((await DetailAsync(entity.Id))!, string.Format(ConstantHelper.Message.MenuChangeSubmitted, entity.RequestNo, approval!.RequestNo));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Create: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }

            IResult<MenuChangeDetailModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                return Result<MenuChangeDetailModel>.Fail(message);
            }
        }

        private Task<ApprovalRequest?> LatestApprovalAsync(Guid id, bool pendingOnly)
            => _unitOfWork.DbContext.ApprovalRequest.AsNoTracking()
                .Where(a => !a.IsDeleted && a.BusinessType == MenuChangeStatus.BusinessType && a.BusinessId == id && (!pendingOnly || a.RequestStatus == ApprovalRequestService.Pending))
                .OrderByDescending(a => a.RequestedAt).FirstOrDefaultAsync();

        /// <summary>Duyệt / từ chối qua engine T12 (<c>ApprovalRequestService.Decide</c>: quyền mục "Duyệt – Yêu cầu đổi món", hash, ApprovalDecision).</summary>
        public async Task<IResult<bool>> Decide(MenuChangeActionRequest request, bool approve)
        {
            try
            {
                var pending = await LatestApprovalAsync(request.Id, true);
                if (pending == null)
                {
                    return Result<bool>.Fail(ConstantHelper.Message.MenuChangeNotPending, false);
                }
                return await _approvalRequestService.Decide(new ApprovalDecideRequest { Id = pending.Id, Approve = approve, Note = request.Note });
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Decide: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<MenuChangeDetailModel>> GetDetail(Guid id)
        {
            try
            {
                var detail = await DetailAsync(id);
                return detail == null ? Result<MenuChangeDetailModel>.Fail(ConstantHelper.Message.MenuChangeNotFound)
                    : Result<MenuChangeDetailModel>.Success(detail, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetDetail: ", ex.ToString()));
                throw;
            }
        }

        private async Task<MenuChangeDetailModel?> DetailAsync(Guid id)
        {
            var db = _unitOfWork.DbContext;
            var r = await db.MenuChangeRequest.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (r == null)
            {
                return null;
            }
            var dishIds = new[] { r.OriginalDishId, r.RequestedDishId }.Where(x => x != null).Select(x => x!.Value).ToList();
            var dishes = await db.Dish.AsNoTracking().Where(d => dishIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name);
            var order = r.OrderId is { } oid ? await db.Order.AsNoTracking().FirstOrDefaultAsync(o => o.Id == oid) : null;
            var approval = await LatestApprovalAsync(id, false);
            DateTime day = r.MealDate.Date, next = day.AddDays(1);
            var plan = await db.ProductionPlan.AsNoTracking().Where(p => !p.IsDeleted && p.ProductionDate >= day && p.ProductionDate < next)
                .OrderBy(p => p.CreatedDate).FirstOrDefaultAsync();
            var detail = new MenuChangeDetailModel
            {
                Request = r,
                StatusName = MenuChangeStatus.Label(r.RequestStatus),
                OrderNumber = order?.OrderNumber ?? order?.OrderNo,
                CustomerName = order == null ? null : await db.Customer.AsNoTracking().Where(c => c.Id == order.CustomerId).Select(c => c.DisplayName ?? c.Name).FirstOrDefaultAsync(),
                MenuCode = r.MenuId is { } mid ? await db.Menu.AsNoTracking().Where(m => m.Id == mid).Select(m => m.Code).FirstOrDefaultAsync() : null,
                OriginalDishName = r.OriginalDishId is { } o ? dishes.GetValueOrDefault(o) : null,
                RequestedDishName = r.RequestedDishId is { } q ? dishes.GetValueOrDefault(q) : null,
                ShiftCode = r.ShiftId is { } s ? await db.Shift.AsNoTracking().Where(x => x.Id == s).Select(x => x.Code).FirstOrDefaultAsync() : null,
                RequestedByName = r.RequestedBy is { } u ? await db.User.AsNoTracking().Where(x => x.Id == u).Select(x => x.UserName).FirstOrDefaultAsync() : null,
                ApprovalRequestId = approval?.Id,
                ApprovalRequestNo = approval?.RequestNo,
                ProductionPlanId = plan?.Id,
                ProductionPlanNumber = plan?.PlanNumber ?? plan?.PlanNo,
                ProductionPlanStatus = plan?.Status,
                PlanNeedsManualAction = plan != null && plan.Status != ProductionPlanStatus.Draft,
            };
            if (detail.PlanNeedsManualAction && r.RequestStatus is MenuChangeStatus.Pending or MenuChangeStatus.Approved)
            {
                detail.ListWarning.Add($"Kế hoạch {detail.ProductionPlanNumber} đang {ProductionPlanStatus.Label(plan!.Status)} – đổi món không tự áp vào lệnh sản xuất / mẻ đã lập, bếp trưởng xử lý tay");
            }
            return detail;
        }

        /// <summary>Nội dung được duyệt của MENU_CHANGE (sửa sau khi gửi → không duyệt được).</summary>
        public async Task<string?> BuildApprovalStateAsync(Guid id)
        {
            var r = await _unitOfWork.DbContext.MenuChangeRequest.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            return r == null ? null : JsonConvert.SerializeObject(new
            {
                r.Id, r.RequestStatus, r.OrderId, r.MenuId, r.OriginalDishId, r.RequestedDishId, MealDate = r.MealDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                r.ShiftId, Quantity = Num(r.Quantity), r.Reason,
            });
        }

        /// <summary>Tiêu đề hộp duyệt: số phiếu – món cũ → món mới – ngày ăn – số suất (– đơn).</summary>
        public async Task<string?> DescribeAsync(Guid id)
        {
            var detail = await DetailAsync(id);
            if (detail?.Request is not { } r)
            {
                return null;
            }
            return $"Đổi món {r.RequestNo}: {detail.OriginalDishName} → {detail.RequestedDishName} – ngày ăn {r.MealDate:dd/MM/yyyy} – {Num(r.Quantity)} suất"
                + (detail.OrderNumber != null ? $" – đơn {detail.OrderNumber}" : string.Empty);
        }

        /// <summary>
        /// Engine (handler MENU_CHANGE) – duyệt: Đã duyệt + ApprovedBy / ApprovedAt; kế hoạch sản xuất của ngày còn <b>Nháp</b> → cập nhật dòng kế hoạch ngay
        /// (bớt món cũ tối đa số suất đổi, thêm / cộng món mới cùng ca); kế hoạch đã Duyệt / Phát hành → không sửa (chi tiết yêu cầu báo bếp trưởng xử lý tay).
        /// Kế hoạch sinh lại / MRP tính lại đều áp đổi món đã duyệt (<see cref="ProductionRules.ApplyMenuChanges"/>).
        /// </summary>
        public async Task<string?> ApproveFromEngineAsync(Guid id, Guid actorUserId)
        {
            var r = _unitOfWork.MenuChangeRequestRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (r == null || r.RequestStatus != MenuChangeStatus.Pending)
            {
                return ConstantHelper.Message.MenuChangeNotPending;
            }
            r.RequestStatus = MenuChangeStatus.Approved;
            r.ApprovedBy = actorUserId;
            r.ApprovedAt = _clock.Now;
            _unitOfWork.MenuChangeRequestRepository.Update(r);
            DateTime day = r.MealDate.Date, next = day.AddDays(1);
            var plan = _unitOfWork.ProductionPlanRepository.GetAll().Where(p => !p.IsDeleted && p.ProductionDate >= day && p.ProductionDate < next)
                .OrderBy(p => p.CreatedDate).FirstOrDefault();
            if (plan == null || plan.Status != ProductionPlanStatus.Draft)
            {
                return null;
            }
            var lines = _unitOfWork.ProductionPlanLineRepository.GetAll().Where(l => l.ProductionPlanId == plan.Id && !l.IsDeleted).ToList();
            decimal remaining = r.Quantity ?? decimal.MaxValue, moved = 0m;
            foreach (var line in lines.Where(l => l.DishId == r.OriginalDishId && (r.ShiftId == null || l.ShiftId == null || l.ShiftId == r.ShiftId)))
            {
                decimal take = Math.Min(line.PlannedQuantity, remaining);
                if (take <= 0)
                {
                    continue;
                }
                line.PlannedQuantity -= take;
                line.PlannedQty = line.PlannedQuantity;
                remaining -= take;
                moved += take;
                _unitOfWork.ProductionPlanLineRepository.Update(line);
            }
            if (moved > 0)
            {
                var target = lines.FirstOrDefault(l => l.DishId == r.RequestedDishId && l.ShiftId == r.ShiftId)
                    ?? lines.FirstOrDefault(l => l.DishId == r.RequestedDishId && r.ShiftId == null);
                if (target == null)
                {
                    target = new ProductionPlanLine
                    {
                        Id = Guid.NewGuid(), ProductionPlanId = plan.Id, DishId = r.RequestedDishId!.Value, ShiftId = r.ShiftId, Status = ProductionPlanStatus.Draft,
                    };
                    _unitOfWork.ProductionPlanLineRepository.Add(target);
                }
                else
                {
                    _unitOfWork.ProductionPlanLineRepository.Update(target);
                }
                target.PlannedQuantity += moved;
                target.PlannedQty = target.PlannedQuantity;
            }
            return null;
        }

        /// <summary>Engine (handler MENU_CHANGE): từ chối → Từ chối; người gửi hủy → Đã hủy.</summary>
        public Task<string?> CloseFromEngineAsync(Guid id, bool cancelled)
        {
            var r = _unitOfWork.MenuChangeRequestRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (r == null || r.RequestStatus != MenuChangeStatus.Pending)
            {
                return Task.FromResult<string?>(ConstantHelper.Message.MenuChangeNotPending);
            }
            r.RequestStatus = cancelled ? MenuChangeStatus.Cancelled : MenuChangeStatus.Rejected;
            _unitOfWork.MenuChangeRequestRepository.Update(r);
            return Task.FromResult<string?>(null);
        }
    }
}
