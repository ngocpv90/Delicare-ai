using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6.13 (D-91 a, c): đổi món cho MỌI khách ngay trên ô Thực đơn tuần đã công bố – hệ thống tự xác định ngày / buổi / lựa chọn và mọi đơn dùng món đó
    /// (xem trước số khách / suất), lý do chọn danh mục. Tự duyệt khi đủ 4: cùng nhóm món + có công thức khóa + FC% suất không vượt mục tiêu + trước giờ chốt
    /// ("Hệ thống tự duyệt"); còn lại → engine duyệt MENU_CHANGE (COO). Áp món sau duyệt dùng lại <see cref="ApproveFromEngineAsync"/>. Không thêm bảng / cột.
    /// </summary>
    public partial class MenuChangeRequestService
    {
        private readonly AppDelicare.Model.Option.BusinessRulesOptions _rules = rules.Value;
        public const string AutoApprovedNote = "Hệ thống tự duyệt";
        public static readonly IReadOnlyDictionary<string, string> CellReasons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SHORTAGE"] = "Thiếu nguyên liệu", ["PRICE"] = "Giá tăng", ["SUPPLIER"] = "NCC giao lỗi", ["QUALITY"] = "Chất lượng nguyên liệu", ["OTHER"] = "Khác",
        };

        private sealed record CellScope(DateTime MealDate, List<(Guid OrderId, Guid CustomerId, decimal Qty)> Lines);

        private async Task<CellScope> ScopeAsync(Guid menuId, string mealType, int option, DateTime mealDate)
        {
            var db = _unitOfWork.DbContext;
            DateTime next = mealDate.AddDays(1);
            var rows = await (from o in db.Order.AsNoTracking()
                              join l in db.OrderLine.AsNoTracking() on o.Id equals l.OrderId
                              where !o.IsDeleted && !l.IsDeleted && o.MealDate >= mealDate && o.MealDate < next && !ClosedOrderStatuses.Contains(o.Status!)
                                    && l.MenuId == menuId && l.MealType == mealType && (l.MenuOptionNo == null || l.MenuOptionNo == option) && l.Quantity > 0
                              select new { o.Id, o.CustomerId, l.Quantity }).ToListAsync();
            return new CellScope(mealDate, rows.Select(r => (r.Id, r.CustomerId, r.Quantity)).ToList());
        }

        public async Task<IResult<MenuChangeCellPreviewModel>> PreviewCell(MenuChangeCellRequest request)
        {
            try
            {
                var (model, error) = await PreviewAsync(request);
                return error != null ? Result<MenuChangeCellPreviewModel>.Fail(error) : Result<MenuChangeCellPreviewModel>.Success(model!, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " PreviewCell: ", ex.ToString()));
                throw;
            }
        }

        private async Task<(MenuChangeCellPreviewModel? Model, string? Error)> PreviewAsync(MenuChangeCellRequest request)
        {
            var db = _unitOfWork.DbContext;
            var menu = await db.Menu.AsNoTracking().FirstOrDefaultAsync(m => m.Id == request.MenuId && !m.IsDeleted);
            if (menu == null || request.Day is < 1 or > 7 || string.IsNullOrWhiteSpace(request.MealType))
            {
                return (null, ConstantHelper.Message.MenuCellInvalid);
            }
            string mealType = request.MealType.Trim().ToUpperInvariant();
            DateTime monday = OrderService.MondayOf(menu.EffectiveFrom ?? _clock.Today), mealDate = monday.AddDays(request.Day - 1);
            var scope = await ScopeAsync(menu.Id, mealType, request.OptionNo, mealDate);
            var model = new MenuChangeCellPreviewModel
            {
                MealDate = mealDate, Customers = scope.Lines.Select(l => l.CustomerId).Distinct().Count(), Orders = scope.Lines.Select(l => l.OrderId).Distinct().Count(),
                Quantity = scope.Lines.Sum(l => l.Qty), CutoffAt = mealDate.AddDays(-1).AddHours(_rules.OrderCutoffHourPreviousDay),
            };
            model.BeforeCutoff = _clock.Now < model.CutoffAt;
            if (request.RequestedDishId is { } newId)
            {
                var ids = new[] { request.OriginalDishId, newId };
                var dishes = await db.Dish.AsNoTracking().Where(d => ids.Contains(d.Id)).Select(d => new { d.Id, Cat = d.DishCategoryId ?? d.CategoryId, d.StandardCost }).ToListAsync();
                var oldD = dishes.FirstOrDefault(d => d.Id == request.OriginalDishId);
                var newD = dishes.FirstOrDefault(d => d.Id == newId);
                model.SameCategory = oldD != null && newD != null && oldD.Cat == newD.Cat;
                model.HasRecipe = await db.Recipe.AsNoTracking().AnyAsync(r => r.DishId == newId && !r.IsDeleted && r.Status == RecipeStatus.Locked);
                // FC% suất sau đổi = (giá vốn các món khác cùng buổi / lựa chọn / ngày + giá vốn món mới) ÷ giá bán bình quân loại suất.
                var lines = await db.MenuLine.AsNoTracking().Where(l => l.MenuId == menu.Id && !l.IsDeleted && l.MealType == mealType && (l.SequenceNo ?? 1) == request.OptionNo && l.DayOfWeek == request.Day).ToListAsync();
                var lineDishIds = lines.Select(l => l.DishId).ToList();
                var costs = await db.Dish.AsNoTracking().Where(d => lineDishIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.StandardCost ?? 0m);
                decimal cost = lines.Where(l => l.DishId != request.OriginalDishId).Sum(l => costs.GetValueOrDefault(l.DishId) * (l.PortionQty ?? 1m)) + (newD?.StandardCost ?? 0m);
                DateTime end = monday.AddDays(7);
                var prices = await (from cm in db.ContractMeal.AsNoTracking()
                                    join c in db.Contract.AsNoTracking() on cm.ContractId equals c.Id
                                    where !cm.IsDeleted && !c.IsDeleted && c.Status == ContractStatus.Active && c.EffectiveFrom < end && (c.EffectiveTo == null || c.EffectiveTo >= monday)
                                          && cm.MealType == mealType && cm.UnitPrice != null && cm.UnitPrice > 0
                                    select cm.UnitPrice!.Value).ToListAsync();
                model.FcPct = prices.Count > 0 && cost > 0 ? Math.Round(cost / prices.Average() * 100m, 1) : null;
                model.FcOk = model.FcPct == null || model.FcPct <= _rules.KpiIngredientToRevenueMaxPct;
                if (!model.SameCategory) model.ListBlock.Add("Món thay khác nhóm món");
                if (!model.HasRecipe) model.ListBlock.Add("Món thay chưa có công thức khóa");
                if (!model.FcOk) model.ListBlock.Add($"FC% suất {model.FcPct:0.#}% vượt mục tiêu {_rules.KpiIngredientToRevenueMaxPct:0.#}%");
                if (!model.BeforeCutoff) model.ListBlock.Add($"Đã qua giờ chốt {model.CutoffAt:HH:mm dd/MM}");
                model.AutoApprove = model.ListBlock.Count == 0;
            }
            return (model, null);
        }

        /// <summary>Gửi đổi món cho mọi khách của ô (đơn không gắn – phạm vi ngày + thực đơn). Tự duyệt khi đủ 4 điều kiện.</summary>
        public async Task<IResult<MenuChangeDetailModel>> CreateForCell(MenuChangeCellRequest request)
        {
            try
            {
                if (request.RequestedDishId == null || request.RequestedDishId == request.OriginalDishId)
                {
                    return Result<MenuChangeDetailModel>.Fail(ConstantHelper.Message.MenuChangeDishRequired);
                }
                string? reasonName = request.ReasonCode != null && CellReasons.TryGetValue(request.ReasonCode, out var rn) ? rn : null;
                if (reasonName == null)
                {
                    return Result<MenuChangeDetailModel>.Fail(ConstantHelper.Message.MenuChangeReasonRequired);
                }
                var (p, error) = await PreviewAsync(request);
                if (error != null)
                {
                    return Result<MenuChangeDetailModel>.Fail(error);
                }
                var actor = _currentActor.RequireActor();
                string reason = $"{reasonName}{(string.IsNullOrWhiteSpace(request.Note) ? string.Empty : " – " + request.Note.Trim())} · Mọi khách ({p!.Customers} khách · {p.Quantity:0.##} suất)";
                var entity = new MenuChangeRequest
                {
                    Id = Guid.NewGuid(), RequestNo = await _documentNumberService.NextAsync(DocumentKind.MenuChange, _clock.Today), MenuId = request.MenuId,
                    OriginalDishId = request.OriginalDishId, RequestedDishId = request.RequestedDishId, MealDate = p.MealDate, Quantity = p.Quantity > 0 ? p.Quantity : null,
                    Reason = reason, RequestedBy = actor.UserId, RequestStatus = MenuChangeStatus.Pending,
                };
                using (var tran = await _unitOfWork.OpenTransaction())
                {
                    _unitOfWork.MenuChangeRequestRepository.Add(entity);
                    await _unitOfWork.MenuChangeRequestRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                    if (p.AutoApprove)
                    {
                        var applyError = await ApproveFromEngineAsync(entity.Id, actor.UserId);
                        if (applyError != null)
                        {
                            _unitOfWork.RollbackTransaction();
                            return Result<MenuChangeDetailModel>.Fail(applyError);
                        }
                        var saved = _unitOfWork.MenuChangeRequestRepository.GetSingle(x => x.Id == entity.Id);
                        saved!.Reason = reason + " · " + AutoApprovedNote;
                        _unitOfWork.MenuChangeRequestRepository.Update(saved);
                        await _unitOfWork.MenuChangeRequestRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                    }
                    else
                    {
                        var (_, approvalError) = await _approvalRequestService.CreateAsync(MenuChangeStatus.BusinessType, entity.Id);
                        if (approvalError != null)
                        {
                            _unitOfWork.RollbackTransaction();
                            return Result<MenuChangeDetailModel>.Fail(approvalError);
                        }
                    }
                    _unitOfWork.CommitTransaction();
                }
                return Result<MenuChangeDetailModel>.Success((await DetailAsync(entity.Id))!,
                    p.AutoApprove ? string.Format(ConstantHelper.Message.MenuChangeAutoApproved, entity.RequestNo) : string.Format(ConstantHelper.Message.MenuChangeSentToApprove, entity.RequestNo, string.Join("; ", p.ListBlock)));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " CreateForCell: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Tab "Đổi món" của Thực đơn tuần: yêu cầu đổi món có ngày ăn trong tuần (mọi khách / theo đơn), kể cả yêu cầu cũ.</summary>
        public async Task<IResults<MenuChangeWeekItemModel>> GetWeekChanges(MenuWeekRequest request)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                DateTime monday = OrderService.MondayOf(request.WeekStart ?? _clock.Today.AddDays(7)), end = monday.AddDays(7);
                var rows = await db.MenuChangeRequest.AsNoTracking().Where(r => !r.IsDeleted && r.MealDate >= monday && r.MealDate < end).OrderBy(r => r.MealDate).ThenBy(r => r.RequestNo).ToListAsync();
                var dishIds = rows.SelectMany(r => new[] { r.OriginalDishId, r.RequestedDishId }).Where(x => x != null).Select(x => x!.Value).Distinct().ToList();
                var dishes = await db.Dish.AsNoTracking().Where(d => dishIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name);
                var userIds = rows.SelectMany(r => new[] { r.RequestedBy, r.ApprovedBy }).Where(x => x != null).Select(x => x!.Value).Distinct().ToList();
                var users = await db.User.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.UserName);
                var orderIds = rows.Where(r => r.OrderId != null).Select(r => r.OrderId!.Value).ToList();
                var orderCus = await (from o in db.Order.AsNoTracking() join c in db.Customer.AsNoTracking() on o.CustomerId equals c.Id where orderIds.Contains(o.Id) select new { o.Id, Name = c.DisplayName ?? c.Name }).ToDictionaryAsync(x => x.Id, x => x.Name);
                var list = rows.Select(r => new MenuChangeWeekItemModel
                {
                    Id = r.Id, RequestNo = r.RequestNo, MealDate = r.MealDate, OriginalDish = r.OriginalDishId is { } o ? dishes.GetValueOrDefault(o) : null,
                    RequestedDish = r.RequestedDishId is { } n ? dishes.GetValueOrDefault(n) : null, Scope = r.OrderId is { } oid ? "Khách " + orderCus.GetValueOrDefault(oid) : "Mọi khách",
                    Quantity = r.Quantity, Reason = r.Reason, Status = r.RequestStatus, StatusName = r.RequestStatus switch { MenuChangeStatus.Pending => "Chờ duyệt", MenuChangeStatus.Approved => "Đã duyệt", MenuChangeStatus.Rejected => "Từ chối", MenuChangeStatus.Cancelled => "Đã hủy", _ => r.RequestStatus },
                    RequestedBy = r.RequestedBy is { } rb ? users.GetValueOrDefault(rb) : null, ApprovedBy = (r.Reason ?? string.Empty).Contains(AutoApprovedNote) ? AutoApprovedNote : r.ApprovedBy is { } ab ? users.GetValueOrDefault(ab) : null,
                    ApprovedAt = r.ApprovedAt, AutoApproved = (r.Reason ?? string.Empty).Contains(AutoApprovedNote), MenuId = r.MenuId, OriginalDishId = r.OriginalDishId, RequestedDishId = r.RequestedDishId,
                }).ToList();
                return new Results<MenuChangeWeekItemModel> { ResponseCode = 200, ResponseMessage = ConstantHelper.Message.GetAllSuccessfully, ListData = list, TotalRecords = list.Count };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetWeekChanges: ", ex.ToString()));
                throw;
            }
        }
    }
}
