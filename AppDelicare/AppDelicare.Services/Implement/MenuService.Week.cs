using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6.11 (D-89): thực đơn tuần – một thực đơn chung / tuần (D-90). Lưới buổi → lựa chọn (SequenceNo) → nhóm món (DishCategory) × 7 ngày, mỗi ô một MenuLine.
    /// Đánh dấu: ô trống · món chưa có công thức khóa · suất FC% vượt <c>KpiIngredientToRevenueMaxPct</c> · món chính trùng trong 7 ngày (gồm cuối tuần trước, chỉ cảnh báo).
    /// Giá vốn món = <c>Dish.StandardCost</c> × khẩu phần; giá bán = đơn giá loại suất bình quân các hợp đồng hiệu lực. Tự lập nháp = nhân bản tuần N − <c>MenuCycleWeeks</c>.
    /// Không thêm bảng / cột.
    /// </summary>
    public partial class MenuService
    {
        public const string FlagEmpty = "EMPTY", FlagNoRecipe = "NO_RECIPE", FlagDuplicate = "DUPLICATE";
        private static readonly string[] WeekMealOrder = ["SANG", "TRUA", "TOI", "DEM"];

        /// <summary>Khung trống mẫu chuẩn (D-89): buổi → số lựa chọn + nhóm món (tên DishCategory).</summary>
        private static readonly Dictionary<string, (int Options, string[] Groups)> BlankTemplate = new(StringComparer.OrdinalIgnoreCase)
        {
            ["SANG"] = (3, ["Món chính", "Ăn kèm", "Đồ uống"]),
            ["TRUA"] = (1, ["Món chính 1", "Món chính 2", "Món phụ", "Rau", "Canh", "Cơm", "Tráng miệng"]),
            ["TOI"] = (1, ["Món chính 1", "Món chính 2", "Món phụ", "Rau", "Canh", "Cơm", "Tráng miệng"]),
            ["DEM"] = (1, ["Món chính", "Đồ uống"]),
        };

        private static bool IsMainCategory(string? name) => name != null && name.Contains("chính", StringComparison.OrdinalIgnoreCase);

        private async Task<Menu?> WeekMenuAsync(DateTime monday)
            => await _unitOfWork.DbContext.Menu.AsNoTracking()
                .Where(m => !m.IsDeleted && m.MenuType == MenuScope.Common && m.Status != MenuStatus.Expired
                    && (m.EffectiveFrom == null || m.EffectiveFrom <= monday.AddDays(6)) && (m.EffectiveTo == null || m.EffectiveTo >= monday))
                .OrderByDescending(m => m.EffectiveFrom == monday).ThenByDescending(m => m.EffectiveFrom == null || m.EffectiveFrom <= monday)
                .ThenByDescending(m => m.Status == MenuStatus.Published).ThenByDescending(m => m.EffectiveFrom).ThenByDescending(m => m.CreatedDate).FirstOrDefaultAsync();

        private async Task<Dictionary<string, decimal>> MealPricesAsync(DateTime monday)
        {
            DateTime end = monday.AddDays(7);
            var rows = await (from cm in _unitOfWork.DbContext.ContractMeal.AsNoTracking()
                              join c in _unitOfWork.DbContext.Contract.AsNoTracking() on cm.ContractId equals c.Id
                              where !cm.IsDeleted && !c.IsDeleted && c.Status == ContractStatus.Active && c.EffectiveFrom < end && (c.EffectiveTo == null || c.EffectiveTo >= monday)
                                    && cm.MealType != null && cm.UnitPrice != null && cm.UnitPrice > 0
                              select new { cm.MealType, cm.UnitPrice }).ToListAsync();
            return rows.GroupBy(r => r.MealType!.ToUpperInvariant()).ToDictionary(g => g.Key, g => Math.Round(g.Average(r => r.UnitPrice!.Value), 0));
        }

        private sealed record DishInfo(Guid Id, string? Name, Guid? CategoryId, decimal? Cost, bool HasRecipe);

        private async Task<Dictionary<Guid, DishInfo>> DishInfoAsync(IEnumerable<Guid> ids)
        {
            var list = ids.Distinct().ToList();
            var db = _unitOfWork.DbContext;
            var locked = (await db.Recipe.AsNoTracking().Where(r => list.Contains(r.DishId) && !r.IsDeleted && r.Status == RecipeStatus.Locked).Select(r => r.DishId).Distinct().ToListAsync()).ToHashSet();
            return await db.Dish.AsNoTracking().Where(d => list.Contains(d.Id))
                .Select(d => new { d.Id, d.Name, Cat = d.DishCategoryId ?? d.CategoryId, d.StandardCost })
                .ToDictionaryAsync(d => d.Id, d => new DishInfo(d.Id, d.Name, d.Cat, d.StandardCost, locked.Contains(d.Id)));
        }

        public async Task<IResult<MenuWeekModel>> GetWeek(MenuWeekRequest request)
        {
            try
            {
                DateTime monday = OrderService.MondayOf(request.WeekStart ?? _clock.Today.AddDays(7));
                var model = await BuildWeekAsync(monday);
                return Result<MenuWeekModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetWeek: ", ex.ToString()));
                throw;
            }
        }

        private async Task<MenuWeekModel> BuildWeekAsync(DateTime monday)
        {
            var db = _unitOfWork.DbContext;
            var model = new MenuWeekModel
            {
                WeekStart = monday, PublishDeadline = monday.AddDays(-4), ListDate = Enumerable.Range(0, 7).Select(i => monday.AddDays(i)).ToList(),
                FcTargetPct = _rules.KpiIngredientToRevenueMaxPct,
            };
            DateTime end = monday.AddDays(7);
            model.CustomerCount = await db.Contract.AsNoTracking().Where(c => !c.IsDeleted && c.Status == ContractStatus.Active && c.EffectiveFrom < end && (c.EffectiveTo == null || c.EffectiveTo >= monday))
                .Select(c => c.CustomerId).Distinct().CountAsync();
            var menu = await WeekMenuAsync(monday);
            var prices = await MealPricesAsync(monday);
            var categories = await db.DishCategory.AsNoTracking().Where(c => !c.IsDeleted).Select(c => new { c.Id, c.Name }).ToListAsync();
            string? CatName(Guid? id) => id is { } g ? categories.FirstOrDefault(c => c.Id == g)?.Name : null;
            var lines = menu == null ? [] : await db.MenuLine.AsNoTracking().Where(l => l.MenuId == menu.Id && !l.IsDeleted && l.DayOfWeek >= 1 && l.DayOfWeek <= 7).ToListAsync();
            // Tuần trước (để đánh dấu món chính trùng qua cuối tuần).
            var prevMenu = await WeekMenuAsync(monday.AddDays(-7));
            var prevLines = prevMenu == null || prevMenu.Id == menu?.Id ? [] : await db.MenuLine.AsNoTracking().Where(l => l.MenuId == prevMenu.Id && !l.IsDeleted && l.DayOfWeek >= 5).ToListAsync();
            var dishes = await DishInfoAsync(lines.Select(l => l.DishId).Concat(prevLines.Select(l => l.DishId)));
            if (menu != null)
            {
                model.MenuId = menu.Id;
                model.MenuCode = menu.Code;
                model.Status = menu.Status;
                model.StatusName = menu.Status switch { MenuStatus.Draft => "Nháp", MenuStatus.Approved => "Đã duyệt", MenuStatus.Published => "Đã công bố", MenuStatus.Expired => "Hết hiệu lực", _ => menu.Status };
                model.Editable = menu.Status == MenuStatus.Draft;
                model.SourceNote = menu.Note;
            }
            var meals = lines.Select(l => (l.MealType ?? string.Empty).ToUpperInvariant()).Where(m => m.Length > 0).Distinct().ToList();
            foreach (var m in WeekMealOrder)
            {
                if (!meals.Contains(m) && (menu == null || prices.ContainsKey(m)))
                {
                    meals.Add(m);
                }
            }
            decimal fcSum = 0m;
            int fcN = 0;
            foreach (string mealType in meals.OrderBy(MealRank))
            {
                var meal = new MenuWeekMealModel { MealType = mealType, MealName = MealLabel(mealType), Price = prices.TryGetValue(mealType, out var p) ? p : null };
                var ml = lines.Where(l => string.Equals(l.MealType, mealType, StringComparison.OrdinalIgnoreCase)).ToList();
                var options = ml.Select(l => l.SequenceNo ?? 1).Distinct().OrderBy(x => x).ToList();
                if (options.Count == 0)
                {
                    options = Enumerable.Range(1, BlankTemplate.TryGetValue(mealType, out var t0) ? t0.Options : 1).ToList();
                }
                foreach (int opt in options)
                {
                    var ol = ml.Where(l => (l.SequenceNo ?? 1) == opt).ToList();
                    var option = new MenuWeekOptionModel { OptionNo = opt };
                    // Nhóm món: theo nhóm của món trong các ô (thứ tự SortOrder); thực đơn chưa có món → khung mẫu chuẩn.
                    var rowCats = ol.OrderBy(l => l.SortOrder).Select(l => dishes.TryGetValue(l.DishId, out var d) ? d.CategoryId : null).Distinct().ToList();
                    if (rowCats.Count == 0 && BlankTemplate.TryGetValue(mealType, out var t))
                    {
                        rowCats = t.Groups.Select(g => (Guid?)categories.FirstOrDefault(c => string.Equals(c.Name, g, StringComparison.OrdinalIgnoreCase))?.Id).ToList();
                        if (rowCats.All(c => c == null))
                        {
                            rowCats = [null];
                        }
                    }
                    var templateNames = BlankTemplate.TryGetValue(mealType, out var tn) ? tn.Groups : [];
                    int ri = 0;
                    foreach (var cat in rowCats)
                    {
                        var row = new MenuWeekRowModel { CategoryId = cat, CategoryName = CatName(cat) ?? (ol.Count == 0 && ri < templateNames.Length ? templateNames[ri] : "Món") };
                        ri++;
                        for (int day = 1; day <= 7; day++)
                        {
                            var line = ol.Where(l => l.DayOfWeek == day && (dishes.TryGetValue(l.DishId, out var d) ? d.CategoryId : null) == cat).OrderBy(l => l.SortOrder).FirstOrDefault();
                            var cell = new MenuWeekCellModel { Day = day };
                            if (line == null)
                            {
                                cell.Flag = FlagEmpty;
                            }
                            else
                            {
                                var d = dishes.GetValueOrDefault(line.DishId);
                                cell.LineId = line.Id;
                                cell.DishId = line.DishId;
                                cell.DishName = d?.Name;
                                cell.Cost = d?.Cost is { } c ? Math.Round(c * (line.PortionQty ?? 1m), 0) : null;
                                if (d is { HasRecipe: false })
                                {
                                    cell.Flag = FlagNoRecipe;
                                }
                                else if (IsMainCategory(row.CategoryName) && IsDuplicateMain(line.DishId, day, mealType, lines, prevLines))
                                {
                                    cell.Flag = FlagDuplicate;
                                }
                            }
                            row.ListCell.Add(cell);
                        }
                        option.ListRow.Add(row);
                    }
                    for (int day = 1; day <= 7; day++)
                    {
                        decimal cost = ol.Where(l => l.DayOfWeek == day).Sum(l => (dishes.GetValueOrDefault(l.DishId)?.Cost ?? 0m) * (l.PortionQty ?? 1m));
                        option.ListCost.Add(Math.Round(cost, 0));
                        decimal? fc = meal.Price is > 0 && cost > 0 ? Math.Round(cost / meal.Price.Value * 100m, 1) : null; // món chưa có giá vốn chuẩn → không tính FC
                        option.ListFcPct.Add(fc);
                        if (fc is { } f)
                        {
                            fcSum += f;
                            fcN++;
                            if (f > model.FcTargetPct)
                            {
                                model.FcOver++;
                            }
                        }
                    }
                    meal.ListOption.Add(option);
                }
                model.ListMeal.Add(meal);
            }
            if (menu != null)
            {
                // UI-6.13 (D-91): đổi món cho mọi khách (yêu cầu không gắn đơn) – ô hiện món mới + dấu "đã đổi"; chờ duyệt → dấu chờ.
                var changes = await db.MenuChangeRequest.AsNoTracking().Where(r => !r.IsDeleted && r.MenuId == menu.Id && r.OrderId == null && r.MealDate >= monday && r.MealDate < end
                    && (r.RequestStatus == MenuChangeStatus.Approved || r.RequestStatus == MenuChangeStatus.Pending)).ToListAsync();
                var newIds = changes.Where(c => c.RequestedDishId != null).Select(c => c.RequestedDishId!.Value).Distinct().ToList();
                var newNames = await db.Dish.AsNoTracking().Where(d => newIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name);
                foreach (var meal in model.ListMeal)
                {
                    foreach (var cell in meal.ListOption.SelectMany(o => o.ListRow).SelectMany(r => r.ListCell).Where(c => c.DishId != null))
                    {
                        var ch = changes.Where(c => c.OriginalDishId == cell.DishId && MenuDayOf(c.MealDate) == cell.Day).OrderByDescending(c => c.CreatedDate).FirstOrDefault();
                        if (ch == null)
                        {
                            continue;
                        }
                        if (ch.RequestStatus == MenuChangeStatus.Pending)
                        {
                            cell.ChangePending = true;
                            continue;
                        }
                        cell.ChangedDishName = ch.RequestedDishId is { } n ? newNames.GetValueOrDefault(n) : null;
                        cell.ChangeNote = $"Đã đổi từ {cell.DishName} ({ch.RequestNo}) – {ch.Reason}";
                    }
                }
            }
            var cells = model.ListMeal.SelectMany(m => m.ListOption).SelectMany(o => o.ListRow).SelectMany(r => r.ListCell).ToList();
            model.EmptyCells = cells.Count(c => c.Flag == FlagEmpty);
            model.MissingRecipe = cells.Count(c => c.Flag == FlagNoRecipe);
            model.DuplicateMain = cells.Count(c => c.Flag == FlagDuplicate);
            model.AvgFcPct = fcN > 0 ? Math.Round(fcSum / fcN, 1) : null;
            model.CanSubmit = menu != null && model.Editable && model.EmptyCells == 0 && model.MissingRecipe == 0 && model.FcOver == 0;
            return model;
        }

        /// <summary>Món chính trùng: cùng món ở ngày khác trong 7 ngày (tuần này hoặc thứ Năm – Chủ nhật tuần trước, cách &lt; 7 ngày), cùng buổi.</summary>
        private static bool IsDuplicateMain(Guid dishId, int day, string mealType, List<MenuLine> week, List<MenuLine> prev)
            => week.Any(l => l.DishId == dishId && l.DayOfWeek != day && string.Equals(l.MealType, mealType, StringComparison.OrdinalIgnoreCase))
               || prev.Any(l => l.DishId == dishId && string.Equals(l.MealType, mealType, StringComparison.OrdinalIgnoreCase) && day + 7 - (l.DayOfWeek ?? 0) < 7);

        private async Task<(Menu? Menu, string? Error)> EditableMenuAsync(Guid menuId)
        {
            var menu = await _unitOfWork.DbContext.Menu.AsNoTracking().FirstOrDefaultAsync(m => m.Id == menuId && !m.IsDeleted);
            return menu == null ? (null, ConstantHelper.Message.RecordNotFoundMessage)
                : menu.Status != MenuStatus.Draft ? (null, ConstantHelper.Message.MenuWeekNotDraft)
                : (menu, null);
        }

        private static int MealSort(string? meal) => MealRank(meal) * 100;

        /// <summary>Lưu 1 ô (tạo / đổi / xóa MenuLine) – chỉ thực đơn Nháp. Món phải đang dùng.</summary>
        public async Task<IResult<MenuWeekModel>> SaveCell(MenuCellRequest request)
        {
            try
            {
                var (menu, error) = await EditableMenuAsync(request.MenuId);
                if (error != null)
                {
                    return Result<MenuWeekModel>.Fail(error);
                }
                if (request.Day < 1 || request.Day > 7 || string.IsNullOrWhiteSpace(request.MealType))
                {
                    return Result<MenuWeekModel>.Fail(ConstantHelper.Message.MenuCellInvalid);
                }
                string mealType = request.MealType.Trim().ToUpperInvariant();
                await ApplyCellAsync(menu!.Id, mealType, Math.Max(1, request.OptionNo), request.CategoryId, request.Day, request.DishId);
                await _unitOfWork.MenuLineRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                return Result<MenuWeekModel>.Success(await BuildWeekAsync(OrderService.MondayOf(menu.EffectiveFrom ?? _clock.Today)), ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " SaveCell: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Đặt món cho ô (buổi, lựa chọn, nhóm, ngày): ô đã có món cùng nhóm → đổi / xóa; chưa có → thêm. Chưa commit.</summary>
        private async Task ApplyCellAsync(Guid menuId, string mealType, int option, Guid? categoryId, int day, Guid? dishId)
        {
            var db = _unitOfWork.DbContext;
            var existing = await db.MenuLine.Where(l => l.MenuId == menuId && !l.IsDeleted && l.MealType == mealType && (l.SequenceNo ?? 1) == option && l.DayOfWeek == day).ToListAsync();
            var info = await DishInfoAsync(existing.Select(l => l.DishId).Concat(dishId is { } x ? [x] : []));
            var line = existing.FirstOrDefault(l => (info.GetValueOrDefault(l.DishId)?.CategoryId) == categoryId);
            if (dishId == null)
            {
                if (line != null)
                {
                    line.IsDeleted = true;
                    _unitOfWork.MenuLineRepository.Update(line);
                }
                return;
            }
            if (line != null)
            {
                line.DishId = dishId.Value;
                _unitOfWork.MenuLineRepository.Update(line);
                return;
            }
            int sort = MealSort(mealType) + existing.Count;
            _unitOfWork.MenuLineRepository.Add(new MenuLine
            {
                Id = Guid.NewGuid(), MenuId = menuId, DishId = dishId.Value, MealType = mealType, SequenceNo = option, DayOfWeek = day, SortOrder = sort, PortionQty = 1, IsRequired = true,
            });
        }

        /// <summary>Điền cả hàng: 7 ngày cùng món cho (buổi, lựa chọn, nhóm).</summary>
        public async Task<IResult<MenuWeekModel>> FillRow(MenuFillRowRequest request)
        {
            try
            {
                var (menu, error) = await EditableMenuAsync(request.MenuId);
                if (error != null)
                {
                    return Result<MenuWeekModel>.Fail(error);
                }
                string mealType = (request.MealType ?? string.Empty).Trim().ToUpperInvariant();
                for (int day = 1; day <= 7; day++)
                {
                    await ApplyCellAsync(menu!.Id, mealType, Math.Max(1, request.OptionNo), request.CategoryId, day, request.DishId);
                    await _unitOfWork.MenuLineRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                }
                return Result<MenuWeekModel>.Success(await BuildWeekAsync(OrderService.MondayOf(menu!.EffectiveFrom ?? _clock.Today)), ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " FillRow: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Chép cả cột ngày (mọi buổi) sang ngày khác – ngày đích bị thay toàn bộ.</summary>
        public async Task<IResult<MenuWeekModel>> CopyDay(MenuCopyDayRequest request)
        {
            try
            {
                var (menu, error) = await EditableMenuAsync(request.MenuId);
                if (error != null)
                {
                    return Result<MenuWeekModel>.Fail(error);
                }
                if (request.FromDay is < 1 or > 7 || request.ToDay is < 1 or > 7 || request.FromDay == request.ToDay)
                {
                    return Result<MenuWeekModel>.Fail(ConstantHelper.Message.MenuCellInvalid);
                }
                var db = _unitOfWork.DbContext;
                var all = await db.MenuLine.Where(l => l.MenuId == menu!.Id && !l.IsDeleted && (l.DayOfWeek == request.FromDay || l.DayOfWeek == request.ToDay)).ToListAsync();
                foreach (var l in all.Where(l => l.DayOfWeek == request.ToDay))
                {
                    l.IsDeleted = true;
                    _unitOfWork.MenuLineRepository.Update(l);
                }
                foreach (var l in all.Where(l => l.DayOfWeek == request.FromDay))
                {
                    _unitOfWork.MenuLineRepository.Add(new MenuLine
                    {
                        Id = Guid.NewGuid(), MenuId = l.MenuId, DishId = l.DishId, MealType = l.MealType, SequenceNo = l.SequenceNo, PortionQty = l.PortionQty, IsRequired = l.IsRequired,
                        DayOfWeek = request.ToDay, SortOrder = l.SortOrder, StandardQuantity = l.StandardQuantity, Note = l.Note,
                    });
                }
                await _unitOfWork.MenuLineRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                return Result<MenuWeekModel>.Success(await BuildWeekAsync(OrderService.MondayOf(menu!.EffectiveFrom ?? _clock.Today)), ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " CopyDay: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>
        /// 5 gợi ý cho ô: cùng nhóm món → có công thức khóa → chưa dùng trong <c>MenuNoRepeatDays</c> (14) ngày → giữ FC% suất trong mục tiêu; kèm giá vốn + FC% mới.
        /// Có <c>Search</c> → tìm theo tên trong mọi món đang dùng (vẫn xếp như trên).
        /// </summary>
        public async Task<IResults<MenuDishSuggestionModel>> SuggestDish(MenuSuggestRequest request)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var menu = await db.Menu.AsNoTracking().FirstOrDefaultAsync(m => m.Id == request.MenuId && !m.IsDeleted);
                if (menu == null)
                {
                    return new Results<MenuDishSuggestionModel> { ResponseCode = 400, ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage };
                }
                string mealType = (request.MealType ?? string.Empty).ToUpperInvariant();
                DateTime monday = OrderService.MondayOf(menu.EffectiveFrom ?? _clock.Today), date = monday.AddDays(Math.Clamp(request.Day, 1, 7) - 1);
                string? q = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();
                var candidates = await db.Dish.AsNoTracking()
                    .Where(d => !d.IsDeleted && (d.Status == null || d.Status == "Active") && (q == null ? (request.CategoryId == null || (d.DishCategoryId ?? d.CategoryId) == request.CategoryId) : d.Name!.Contains(q)))
                    .Select(d => d.Id).Take(400).ToListAsync();
                if (candidates.Count < 5 && q == null)
                {
                    candidates.AddRange(await db.Dish.AsNoTracking().Where(d => !d.IsDeleted && (d.Status == null || d.Status == "Active") && !candidates.Contains(d.Id)).Select(d => d.Id).Take(200).ToListAsync());
                }
                var info = await DishInfoAsync(candidates);
                // Ngày dùng gần nhất của món trong 14 ngày trước ô (mọi thực đơn chung).
                int noRepeat = Math.Max(1, _rules.MenuNoRepeatDays);
                DateTime since = date.AddDays(-noRepeat);
                var recentMenus = await db.Menu.AsNoTracking().Where(m => !m.IsDeleted && m.MenuType == MenuScope.Common && m.Status != MenuStatus.Expired
                    && (m.EffectiveFrom == null || m.EffectiveFrom < date) && (m.EffectiveTo == null || m.EffectiveTo >= since)).Select(m => new { m.Id, m.EffectiveFrom }).ToListAsync();
                var recentIds = recentMenus.Select(m => m.Id).ToList();
                var recentLines = await db.MenuLine.AsNoTracking().Where(l => recentIds.Contains(l.MenuId) && !l.IsDeleted && candidates.Contains(l.DishId)).Select(l => new { l.MenuId, l.DishId, l.DayOfWeek }).ToListAsync();
                var lastUsed = new Dictionary<Guid, int>();
                foreach (var l in recentLines)
                {
                    var mm = recentMenus.First(m => m.Id == l.MenuId);
                    DateTime mon = OrderService.MondayOf(mm.EffectiveFrom ?? since);
                    for (DateTime d0 = mon; d0 < date; d0 = d0.AddDays(7))
                    {
                        DateTime used = d0.AddDays((l.DayOfWeek ?? 1) - 1);
                        if (used >= since && used < date)
                        {
                            int days = (date - used).Days;
                            lastUsed[l.DishId] = lastUsed.TryGetValue(l.DishId, out var o) ? Math.Min(o, days) : days;
                        }
                    }
                }
                var prices = await MealPricesAsync(monday);
                decimal? price = prices.TryGetValue(mealType, out var p) ? p : null;
                var optLines = await db.MenuLine.AsNoTracking().Where(l => l.MenuId == menu.Id && !l.IsDeleted && l.MealType == mealType && (l.SequenceNo ?? 1) == request.OptionNo && l.DayOfWeek == request.Day).ToListAsync();
                var optInfo = await DishInfoAsync(optLines.Select(l => l.DishId));
                decimal others = optLines.Where(l => optInfo.GetValueOrDefault(l.DishId)?.CategoryId != request.CategoryId).Sum(l => (optInfo.GetValueOrDefault(l.DishId)?.Cost ?? 0m) * (l.PortionQty ?? 1m));
                var catNames = await db.DishCategory.AsNoTracking().Select(c => new { c.Id, c.Name }).ToDictionaryAsync(c => c.Id, c => c.Name);
                var list = info.Values.Select(d =>
                {
                    decimal? fc = price is > 0 ? Math.Round((others + (d.Cost ?? 0m)) / price.Value * 100m, 1) : null;
                    return new
                    {
                        Model = new MenuDishSuggestionModel
                        {
                            DishId = d.Id, DishName = d.Name, CategoryName = d.CategoryId is { } c ? catNames.GetValueOrDefault(c) : null, Cost = d.Cost, NewFcPct = fc, HasRecipe = d.HasRecipe,
                            DaysSinceUsed = lastUsed.TryGetValue(d.Id, out var u) ? u : null,
                        },
                        Same = request.CategoryId != null && d.CategoryId == request.CategoryId,
                        FcOk = fc == null || fc <= _rules.KpiIngredientToRevenueMaxPct,
                    };
                })
                .OrderByDescending(x => x.Same).ThenByDescending(x => x.Model.HasRecipe).ThenByDescending(x => x.Model.DaysSinceUsed == null).ThenByDescending(x => x.FcOk).ThenBy(x => x.Model.DishName)
                .Select(x => x.Model).Take(q == null ? 5 : 10).ToList();
                return new Results<MenuDishSuggestionModel> { ResponseCode = 200, ResponseMessage = ConstantHelper.Message.GetAllSuccessfully, ListData = list, TotalRecords = list.Count };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " SuggestDish: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Gửi duyệt: khóa khi còn ô trống / món thiếu công thức khóa / suất FC vượt; hợp lệ → báo người duyệt (COO) qua bot. Trạng thái giữ Nháp tới khi Duyệt.</summary>
        public async Task<IResult<MenuWeekModel>> SubmitWeek(MenuActionRequest request)
        {
            try
            {
                var (menu, error) = await EditableMenuAsync(request.Id);
                if (error != null)
                {
                    return Result<MenuWeekModel>.Fail(error);
                }
                var w = await BuildWeekAsync(OrderService.MondayOf(menu!.EffectiveFrom ?? _clock.Today));
                if (!w.CanSubmit)
                {
                    return Result<MenuWeekModel>.Fail(string.Format(ConstantHelper.Message.MenuWeekBlocked, w.EmptyCells, w.MissingRecipe, w.FcOver), w);
                }
                var approvers = await Messaging.RecipientResolver.UsersInGroupsAsync(_unitOfWork.DbContext, ["COO"]);
                if (approvers.Count > 0)
                {
                    await Messaging.BusinessNotifier.SafeEnqueueAsync(_notifications, new NotificationRequest
                    {
                        Template = Messaging.NotificationCatalog.ManualText, UserIds = [.. approvers], BotCode = MessagingCode.BotDieuHanh,
                        Data = new { text = $"🍽 Thực đơn chung tuần {w.WeekStart:dd/MM} ({menu.Code}) đã hoàn thiện – mời duyệt trước thứ Năm {w.PublishDeadline:dd/MM}." + (w.DuplicateMain > 0 ? $" Lưu ý {w.DuplicateMain} ô món chính trùng." : string.Empty) },
                        IdempotencyKey = $"MENU_SUBMIT:{menu.Id:N}:{_clock.Now:yyyyMMddHHmm}",
                    });
                }
                return Result<MenuWeekModel>.Success(w, string.Format(ConstantHelper.Message.MenuWeekSubmitted, menu.Code));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " SubmitWeek: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>
        /// Tự lập nháp tuần (job thứ Hai 06:00 / nút tay): đã có thực đơn chung cho tuần → bỏ qua; nguồn = tuần N − <c>MenuCycleWeeks</c> (4), thiếu → tuần gần nhất có món;
        /// <paramref name="sourceId"/> (nút tay "nhân bản tuần chọn") ưu tiên; <paramref name="blank"/> = khung trống mẫu chuẩn.
        /// </summary>
        public async Task<IResult<MenuAutoDraftResultModel>> AutoDraft(DateTime? weekStart, Guid? sourceId = null, bool blank = false)
        {
            try
            {
                DateTime monday = OrderService.MondayOf(weekStart ?? _clock.Today.AddDays(7));
                var result = new MenuAutoDraftResultModel { WeekStart = monday };
                var exist = await WeekMenuAsync(monday);
                if (exist != null && exist.EffectiveFrom >= monday)
                {
                    result.MenuId = exist.Id;
                    result.MenuCode = exist.Code;
                    result.Message = string.Format(ConstantHelper.Message.MenuWeekExists, exist.Code);
                    return Result<MenuAutoDraftResultModel>.Success(result, result.Message);
                }
                var db = _unitOfWork.DbContext;
                Menu? source = null;
                if (!blank)
                {
                    source = sourceId is { } sid ? await db.Menu.AsNoTracking().FirstOrDefaultAsync(m => m.Id == sid && !m.IsDeleted)
                        : await WeekMenuAsync(monday.AddDays(-7 * Math.Max(1, _rules.MenuCycleWeeks)));
                    if (source == null || !await db.MenuLine.AnyAsync(l => l.MenuId == source.Id && !l.IsDeleted))
                    {
                        var withLines = await db.Menu.AsNoTracking().Where(m => !m.IsDeleted && m.MenuType == MenuScope.Common && m.EffectiveFrom < monday && db.MenuLine.Any(l => l.MenuId == m.Id && !l.IsDeleted))
                            .OrderByDescending(m => m.EffectiveFrom).FirstOrDefaultAsync();
                        source = sourceId == null ? withLines : source;
                    }
                }
                Menu created;
                if (source != null)
                {
                    var r = await Clone(new MenuCloneRequest { Id = source.Id, EffectiveFrom = monday, EffectiveTo = monday.AddDays(6), Name = $"Thực đơn chung tuần {monday:dd/MM/yyyy}" });
                    if (r.ResponseCode != 200 || r.Data == null)
                    {
                        return Result<MenuAutoDraftResultModel>.Fail(r.ResponseMessage);
                    }
                    created = r.Data;
                    result.SourceCode = source.Code;
                }
                else
                {
                    string code = await _documentNumberService.NextAsync(DocumentKind.Menu, monday);
                    created = new Menu
                    {
                        Id = Guid.NewGuid(), Code = code, Name = $"Thực đơn chung tuần {monday:dd/MM/yyyy}", VersionNo = 1, EffectiveFrom = monday, EffectiveTo = monday.AddDays(6),
                        Status = MenuStatus.Draft, MenuType = MenuScope.Common, Note = "Khung trống mẫu chuẩn",
                    };
                    _unitOfWork.MenuRepository.Add(created);
                    await _unitOfWork.MenuRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                }
                result.Created = true;
                result.MenuId = created.Id;
                result.MenuCode = created.Code;
                result.Message = source != null ? string.Format(ConstantHelper.Message.MenuWeekDrafted, created.Code, source.Code) : string.Format(ConstantHelper.Message.MenuWeekBlankCreated, created.Code);
                return Result<MenuAutoDraftResultModel>.Success(result, result.Message);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " AutoDraft: ", ex.ToString()));
                throw;
            }
        }
    }
}
