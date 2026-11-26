using System.Globalization;
using System.Text.RegularExpressions;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// ACC-6 (D-131 j – anh chốt 2026-11-26): định mức bao bì & tự xuất khi đóng gói. Không bảng mới (D-126 tự chọn):
    /// <list type="bullet">
    /// <item>Định mức = công thức của "món bao bì" <c>BB-&lt;buổi&gt;</c> (SANG / TRUA / TOI / DEM / COM_SUAT) thuộc nhóm món <c>BAO_BI</c>; dòng công thức = vật tư × số dùng cho
    /// <c>StandardOutputQty</c> (100) suất. Sửa ma trận = sửa bản Nháp (tự tạo bản mới khi bản đang áp dụng đã khóa) → Gửi duyệt qua engine <c>RECIPE_VERSION</c>
    /// sẵn có (duyệt cấp cuối = khóa &amp; áp dụng). Đang chờ duyệt thì không sửa.</item>
    /// <item>Ghi kết quả đóng gói (<c>PackingOrder/RecordResultMany</c>) → theo ngày × ca: suất đạt (mỗi đơn lấy món đóng nhiều nhất) × định mức bản đang áp dụng
    /// → phiếu xuất Nháp "Xuất bao bì" thẻ <c>[XUAT_BB:yyyyMMdd|mã ca|buổi|số suất]</c>; lần sau chỉ lập thêm cho phần suất chưa có phiếu (kho sửa phiếu Nháp vẫn giữ).
    /// Thiếu tồn → bot Vận hành báo Mua hàng (OPS-SCM / OPS-SCM-HEAD) + liên kết lập đề nghị mua.</item>
    /// <item>Job <c>PackagingIssueAutoPost</c>: phiếu bao bì còn Nháp của ca đã hết → tự ghi sổ (<c>BusinessRules:PackagingIssueAutoPost</c>).</item>
    /// <item>Chứng từ kế toán ACC-2: phiếu có thẻ → loại "Xuất bao bì" (PACKAGING), đối tượng THCP = thành phẩm của buổi.</item>
    /// </list>
    /// </summary>
    public class PackagingNormService(IUnitOfWork unitOfWork, IClock clock, ICurrentActor currentActor, IOptions<BusinessRulesOptions> rules,
        INotificationService notifications, IServiceProvider services) : IPackagingNormService
    {
        public const string CategoryCode = "BAO_BI";
        public const string DishPrefix = "BB-";
        public const string TagPrefix = "[XUAT_BB:";
        public const string ScreenId = "log-packaging-norm";
        public static readonly (string Code, string Label)[] Meals = [("SANG", "Suất sáng"), ("TRUA", "Suất trưa"), ("TOI", "Suất tối"), ("DEM", "Suất đêm"), ("COM_SUAT", "Cơm suất (bán lẻ)")];
        private static readonly Regex TagRx = new(@"\[XUAT_BB:(\d{8})\|([^|\]]*)\|([^|\]]*)\|([\d.]+)\]", RegexOptions.Compiled);
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly ICurrentActor _currentActor = currentActor;
        private readonly BusinessRulesOptions _rules = rules.Value;
        private readonly INotificationService _notifications = notifications;
        private readonly IServiceProvider _services = services;

        public sealed record PackagingTag(DateTime Day, string ShiftCode, string Meal, decimal Portions);

        public static string Tag(DateTime day, string? shiftCode, string meal, decimal portions)
            => $"{TagPrefix}{day:yyyyMMdd}|{shiftCode}|{meal}|{portions.ToString("0.###", CultureInfo.InvariantCulture)}]";

        public static PackagingTag? ParseTag(string? note)
        {
            var m = TagRx.Match(note ?? string.Empty);
            return m.Success && DateTime.TryParseExact(m.Groups[1].Value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                ? new PackagingTag(d, m.Groups[2].Value, m.Groups[3].Value, decimal.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture)) : null;
        }

        private static string? MealCode(string? meal) => Meals.Select(x => x.Code).FirstOrDefault(c => string.Equals(c, meal?.Trim(), StringComparison.OrdinalIgnoreCase));

        private static string StatusName(string? s) => s switch
        {
            RecipeStatus.Draft => "Nháp – chưa gửi duyệt", RecipeStatus.PendingApproval => "Chờ duyệt", RecipeStatus.Approved => "Đã duyệt", _ => s ?? string.Empty,
        };

        // ---------------------------------------------------------------- ma trận

        public async Task<IResult<PackagingNormMatrixModel>> GetMatrix()
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var codes = Meals.Select(m => DishPrefix + m.Code).ToList();
                var dishes = await db.Dish.AsNoTracking().Where(d => !d.IsDeleted && codes.Contains(d.Code!)).ToListAsync();
                var dishIds = dishes.Select(d => d.Id).ToList();
                var recipes = await db.Recipe.AsNoTracking().Where(r => !r.IsDeleted && dishIds.Contains(r.DishId)).ToListAsync();
                var model = new PackagingNormMatrixModel { PerPortions = _rules.RecipeStandardServings > 0 ? _rules.RecipeStandardServings : 100, AutoPost = _rules.PackagingIssueAutoPost };
                var pick = new List<(string Meal, Recipe R, bool Active)>();
                foreach (var (code, label) in Meals)
                {
                    var dish = dishes.FirstOrDefault(d => d.Code == DishPrefix + code);
                    var mm = new PackagingNormMealModel { Code = code, Label = label, DishId = dish?.Id, DishCode = dish?.Code };
                    if (dish != null)
                    {
                        var active = recipes.Where(r => r.DishId == dish.Id && r.IsActive && r.Status == RecipeStatus.Locked).OrderByDescending(r => r.VersionNo).FirstOrDefault();
                        var work = recipes.Where(r => r.DishId == dish.Id && r.Status is RecipeStatus.Draft or RecipeStatus.PendingApproval or RecipeStatus.Approved).OrderByDescending(r => r.VersionNo).FirstOrDefault();
                        if (active != null)
                        {
                            (mm.ActiveVersion, mm.ActiveFrom) = (active.VersionNo, active.EffectiveFrom);
                            pick.Add((code, active, true));
                        }
                        if (work != null)
                        {
                            (mm.WorkRecipeId, mm.WorkStatus, mm.WorkStatusName, mm.WorkVersion) = (work.Id, work.Status, StatusName(work.Status), work.VersionNo);
                            pick.Add((code, work, false));
                        }
                    }
                    model.ListMeal.Add(mm);
                }
                var rIds = pick.Select(p => p.R.Id).ToList();
                var lines = await db.RecipeLine.AsNoTracking().Where(l => !l.IsDeleted && rIds.Contains(l.RecipeId)).ToListAsync();
                var ingIds = lines.Select(l => l.IngredientId).Distinct().ToList();
                var ings = await db.Ingredient.AsNoTracking().Where(i => ingIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id);
                var units = await UnitNamesAsync();
                var rows = new Dictionary<Guid, PackagingNormRowModel>();
                foreach (var (meal, r, active) in pick)
                {
                    decimal basis = r.StandardOutputQty is > 0 ? r.StandardOutputQty.Value : model.PerPortions;
                    foreach (var l in lines.Where(l => l.RecipeId == r.Id))
                    {
                        if (!rows.TryGetValue(l.IngredientId, out var row))
                        {
                            var ing = ings.GetValueOrDefault(l.IngredientId);
                            row = rows[l.IngredientId] = new PackagingNormRowModel
                            {
                                IngredientId = l.IngredientId, Code = ing?.Code, Name = ing?.Name, UnitName = units.GetValueOrDefault(l.UnitId != Guid.Empty ? l.UnitId : l.UomId),
                            };
                        }
                        var target = active ? row.Active : row.Work;
                        target[meal] = target.GetValueOrDefault(meal) + Math.Round(l.Quantity * model.PerPortions / basis, 3);
                    }
                }
                model.ListRow = [.. rows.Values.OrderBy(r => r.Code)];
                model.DraftIssues = await db.MaterialIssue.AsNoTracking().CountAsync(i => !i.IsDeleted && i.Status == MaterialIssueStatus.Draft && i.Note != null && i.Note.Contains(TagPrefix));
                return Result<PackagingNormMatrixModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetMatrix: ", ex.ToString()));
                throw;
            }
        }

        private async Task<Dictionary<Guid, string>> UnitNamesAsync()
            => await _unitOfWork.DbContext.UnitMeasure.AsNoTracking().Select(u => new { u.Id, u.Name, u.Code }).ToDictionaryAsync(u => u.Id, u => u.Name ?? u.Code ?? string.Empty);

        /// <summary>Vật tư để thêm dòng: tìm theo mã / tên; trống = vật tư mã CC… (công cụ, bao bì).</summary>
        public async Task<IResult<List<PackagingSupplyModel>>> GetSupplies(string? search)
        {
            var db = _unitOfWork.DbContext;
            string q = search?.Trim() ?? string.Empty;
            var list = await db.Ingredient.AsNoTracking().Where(i => !i.IsDeleted && (i.Status == null || i.Status == "Active")
                    && (q == string.Empty ? i.Code!.StartsWith("CC") : (i.Code!.Contains(q) || i.Name!.Contains(q))))
                .OrderBy(i => i.Code).Take(50).Select(i => new { i.Id, i.Code, i.Name, i.BaseUnitId }).ToListAsync();
            var units = await UnitNamesAsync();
            return Result<List<PackagingSupplyModel>>.Success([.. list.Select(i => new PackagingSupplyModel
            {
                IngredientId = i.Id, Code = i.Code, Name = i.Name, UnitName = i.BaseUnitId is { } u ? units.GetValueOrDefault(u) : null,
            })], ConstantHelper.Message.GetAllSuccessfully);
        }

        /// <summary>Lưu cột định mức của 1 loại suất vào bản Nháp (tự tạo nhóm / món bao bì / bản Nháp mới chép từ bản đang áp dụng).</summary>
        public async Task<IResult<bool>> SaveMeal(PackagingNormSaveRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var actor = _currentActor.RequireActor().UserId;
                var db = _unitOfWork.DbContext;
                DateTime now = _clock.Now;
                string? meal = MealCode(request.Meal);
                if (meal == null)
                {
                    return Fail(Msg.MealInvalid);
                }
                var input = request.ListLine.Where(l => l.IngredientId != Guid.Empty).GroupBy(l => l.IngredientId).Select(g => g.Last()).ToList();
                if (input.Any(l => l.PerHundred < 0))
                {
                    return Fail(Msg.Negative);
                }
                input = [.. input.Where(l => l.PerHundred > 0)];
                if (input.Count == 0)
                {
                    return Fail(Msg.NeedLine);
                }
                var ingIds = input.Select(l => l.IngredientId).ToList();
                var ings = await db.Ingredient.AsNoTracking().Where(i => ingIds.Contains(i.Id) && !i.IsDeleted).ToDictionaryAsync(i => i.Id);
                if (ings.Count != ingIds.Count)
                {
                    return Fail(Msg.SupplyNotFound);
                }
                if (ings.Values.FirstOrDefault(i => (i.BaseUnitId ?? Guid.Empty) == Guid.Empty) is { } noUnit)
                {
                    return Fail(string.Format(Msg.NoUnit, noUnit.Code));
                }
                var dish = await EnsureDishAsync(meal, actor, now);
                var recipes = await db.Recipe.Where(r => !r.IsDeleted && r.DishId == dish.Id).ToListAsync();
                if (recipes.Any(r => r.Status is RecipeStatus.PendingApproval or RecipeStatus.Approved))
                {
                    return Fail(Msg.Pending);
                }
                decimal basis = _rules.RecipeStandardServings > 0 ? _rules.RecipeStandardServings : 100;
                var draft = recipes.FirstOrDefault(r => r.Status == RecipeStatus.Draft);
                if (draft == null)
                {
                    int no = recipes.Count == 0 ? 1 : recipes.Max(r => r.VersionNo) + 1;
                    draft = new Recipe
                    {
                        Id = Guid.NewGuid(), DishId = dish.Id, VersionNo = no, VersionCode = $"{dish.Code}-V{no}", Status = RecipeStatus.Draft, IsActive = false, StandardOutputQty = basis,
                        YieldQuantity = basis, YieldPercent = 100, Note = "Định mức bao bì (ACC-6) – số dùng cho " + basis.ToString("0", CultureInfo.InvariantCulture) + " suất",
                        CreatedDate = now, CreatedBy = actor, IsDeleted = false,
                    };
                    db.Recipe.Add(draft);
                }
                basis = draft.StandardOutputQty is > 0 ? draft.StandardOutputQty.Value : basis;
                foreach (var old in await db.RecipeLine.Where(l => l.RecipeId == draft.Id && !l.IsDeleted).ToListAsync())
                {
                    old.IsDeleted = true;
                }
                int seq = 0;
                foreach (var l in input.OrderBy(l => ings[l.IngredientId].Code))
                {
                    seq++;
                    Guid unit = ings[l.IngredientId].BaseUnitId!.Value;
                    db.RecipeLine.Add(new RecipeLine
                    {
                        Id = Guid.NewGuid(), RecipeId = draft.Id, IngredientId = l.IngredientId, Quantity = Math.Round(l.PerHundred!.Value * basis / 100m, 4), UomId = unit, UnitId = unit,
                        SequenceNo = seq, SortOrder = seq, WastePercent = 0, CreatedDate = now, CreatedBy = actor, IsDeleted = false,
                    });
                }
                await db.SaveChangesAsync();
                _unitOfWork.CommitTransaction();
                return Result<bool>.Success(true, string.Format(Msg.Saved, Meals.First(m => m.Code == meal).Label.ToLowerInvariant()));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " SaveMeal: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }

            Result<bool> Fail(string m)
            {
                _unitOfWork.RollbackTransaction();
                return Result<bool>.Fail(m, false);
            }
        }

        private async Task<Dish> EnsureDishAsync(string meal, Guid actor, DateTime now)
        {
            var db = _unitOfWork.DbContext;
            var cat = await db.DishCategory.FirstOrDefaultAsync(c => !c.IsDeleted && c.Code == CategoryCode);
            if (cat == null)
            {
                cat = new DishCategory
                {
                    Id = Guid.NewGuid(), Code = CategoryCode, Name = "Bao bì (định mức – không lên thực đơn)", Status = "Active",
                    Description = "ACC-6: món bao bì theo loại suất – công thức = định mức vật tư bao bì / 100 suất", CreatedDate = now, CreatedBy = actor, IsDeleted = false,
                };
                db.DishCategory.Add(cat);
            }
            string code = DishPrefix + meal;
            var dish = await db.Dish.FirstOrDefaultAsync(d => !d.IsDeleted && d.Code == code);
            if (dish == null)
            {
                dish = new Dish
                {
                    Id = Guid.NewGuid(), Code = code, Name = "Bao bì " + Meals.First(m => m.Code == meal).Label.ToLowerInvariant(), CategoryId = cat.Id, DishCategoryId = cat.Id,
                    MealCategory = meal, StandardPortion = 1, Status = "Active", Note = "ACC-6: định mức bao bì – không đưa vào thực đơn", CreatedDate = now, CreatedBy = actor, IsDeleted = false,
                };
                db.Dish.Add(dish);
            }
            return dish;
        }

        /// <summary>Gửi duyệt bản Nháp của 1 loại suất (engine RECIPE_VERSION – duyệt cấp cuối = khóa &amp; áp dụng từ ngày chọn).</summary>
        public async Task<IResult<bool>> Submit(PackagingNormSubmitRequest request)
        {
            string? meal = MealCode(request.Meal);
            if (meal == null)
            {
                return Result<bool>.Fail(Msg.MealInvalid, false);
            }
            var db = _unitOfWork.DbContext;
            var draft = await db.Recipe.AsNoTracking().Where(r => !r.IsDeleted && r.Status == RecipeStatus.Draft)
                .Join(db.Dish.AsNoTracking().Where(d => !d.IsDeleted && d.Code == DishPrefix + meal), r => r.DishId, d => d.Id, (r, d) => r).FirstOrDefaultAsync();
            if (draft == null)
            {
                return Result<bool>.Fail(Msg.NoDraft, false);
            }
            var r = await _services.GetRequiredService<IRecipeService>().Submit(draft.Id, request.EffectiveFrom);
            return r.ResponseCode == 200 ? Result<bool>.Success(true, Msg.Submitted) : r;
        }

        // ---------------------------------------------------------------- tự xuất khi đóng gói

        /// <summary>Gọi sau khi ghi kết quả đóng gói (đã commit): lập phiếu xuất bao bì Nháp cho phần suất đạt chưa có phiếu của ngày <paramref name="day"/>.</summary>
        public async Task<List<string>> AfterPackingAsync(DateTime day)
        {
            var warnings = new List<string>();
            if (!_rules.PackagingIssueAutoDraft)
            {
                return warnings;
            }
            try
            {
                var db = _unitOfWork.DbContext;
                day = day.Date;
                DateTime next = day.AddDays(1);
                var packs = await db.PackingOrder.AsNoTracking().Where(p => !p.IsDeleted && p.PackingDate >= day && p.PackingDate < next)
                    .Select(p => new { p.Id, p.OrderId, p.ProductionBatchId }).ToListAsync();
                if (packs.Count == 0)
                {
                    return warnings;
                }
                var pIds = packs.Select(p => p.Id).ToList();
                var packed = (await db.PackingResult.AsNoTracking().Where(r => !r.IsDeleted && pIds.Contains(r.PackingOrderId))
                    .Select(r => new { r.PackingOrderId, Qty = r.PackedQuantity - r.RejectedQuantity }).ToListAsync())
                    .GroupBy(r => r.PackingOrderId).ToDictionary(g => g.Key, g => g.Sum(x => x.Qty));
                var bIds = packs.Select(p => p.ProductionBatchId).Distinct().ToList();
                var batches = await db.ProductionBatch.AsNoTracking().Where(b => bIds.Contains(b.Id)).Select(b => new { b.Id, b.ProductionOrderId, b.DishId }).ToDictionaryAsync(b => b.Id);
                var poIds = batches.Values.Select(b => b.ProductionOrderId).Distinct().ToList();
                var shiftOfPo = await db.ProductionOrder.AsNoTracking().Where(o => poIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.ShiftId);
                var shifts = await db.Shift.AsNoTracking().Select(s => new { s.Id, s.Code, s.Name }).ToDictionaryAsync(s => s.Id);
                // Suất đạt theo ca: mỗi đơn (hoặc mẻ khi không gắn đơn) lấy món đóng được nhiều suất nhất.
                var portions = packs.Where(p => packed.ContainsKey(p.Id) && batches.ContainsKey(p.ProductionBatchId))
                    .Select(p => new { Shift = shiftOfPo.GetValueOrDefault(batches[p.ProductionBatchId].ProductionOrderId), Key = p.OrderId ?? p.ProductionBatchId, batches[p.ProductionBatchId].DishId, Qty = packed[p.Id] })
                    .Where(x => x.Shift is { } s && s != Guid.Empty)
                    .GroupBy(x => x.Shift!.Value)
                    .ToDictionary(g => g.Key, g => g.GroupBy(x => x.Key).Sum(o => o.GroupBy(x => x.DishId).Max(d => d.Sum(x => x.Qty))));
                var existing = (await db.MaterialIssue.AsNoTracking().Where(i => !i.IsDeleted && i.Status != MaterialIssueStatus.Cancelled && i.Note != null && i.Note.Contains(TagPrefix + day.ToString("yyyyMMdd")))
                    .Select(i => i.Note).ToListAsync()).Select(ParseTag).Where(t => t != null).ToList();
                foreach (var (shiftId, total) in portions)
                {
                    var sh = shifts.GetValueOrDefault(shiftId);
                    string? meal = AccountingVoucherService.MealOfShift(sh?.Code, sh?.Name);
                    if (meal == null || total <= 0)
                    {
                        continue;
                    }
                    string shiftCode = sh?.Code ?? shiftId.ToString("N")[..6];
                    decimal covered = existing.Where(t => t!.ShiftCode == shiftCode).Sum(t => t!.Portions);
                    decimal delta = total - covered;
                    if (delta <= 0)
                    {
                        continue;
                    }
                    var norm = await NormAsync(meal);
                    if (norm.Count == 0)
                    {
                        warnings.Add(string.Format(Msg.NoNorm, Meals.First(m => m.Code == meal).Label.ToLowerInvariant()));
                        continue;
                    }
                    var need = norm.Select(n => (n.IngredientId, Qty: Math.Round(n.PerPortion * delta, 3))).Where(n => n.Qty > 0).ToList();
                    var ids = need.Select(n => n.IngredientId).ToList();
                    var stock = await db.InventoryBalance.AsNoTracking().Where(b => !b.IsDeleted && ids.Contains(b.IngredientId) && b.AvailableQuantity > 0)
                        .Select(b => new { b.WarehouseId, b.IngredientId, b.AvailableQuantity }).ToListAsync();
                    Guid? wh = stock.GroupBy(b => b.WarehouseId).OrderByDescending(g => g.Select(x => x.IngredientId).Distinct().Count()).ThenByDescending(g => g.Sum(x => x.AvailableQuantity))
                        .Select(g => (Guid?)g.Key).FirstOrDefault();
                    var short_ = need.Where(n => (wh == null ? 0m : stock.Where(b => b.WarehouseId == wh && b.IngredientId == n.IngredientId).Sum(b => b.AvailableQuantity)) < n.Qty).ToList();
                    string label = Meals.First(m => m.Code == meal).Label.ToLowerInvariant();
                    if (short_.Count > 0)
                    {
                        await NotifyShortageAsync(day, shiftCode, label, short_);
                        warnings.Add(string.Format(Msg.Short, short_.Count, label));
                    }
                    var lines = need.Except(short_).ToList();
                    if (wh == null || lines.Count == 0)
                    {
                        continue;
                    }
                    var res = await _services.GetRequiredService<IMaterialIssueService>().Create(new MaterialIssueCreateRequest
                    {
                        WarehouseId = wh.Value, IssueDate = day == _clock.Today ? _clock.Now : day.AddHours(12),
                        Note = $"{Tag(day, shiftCode, meal, delta)} Xuất bao bì {label} ca {shiftCode} ngày {day:dd/MM} – {delta:0.##} suất đạt × định mức; hệ thống lập nháp khi đóng gói, kho sửa được, tự ghi sổ khi hết ca",
                        ListLine = [.. lines.Select(n => new MaterialIssueCreateLineRequest { IngredientId = n.IngredientId, Quantity = n.Qty, Note = "Định mức bao bì" })],
                    });
                    if (res.ResponseCode != 200)
                    {
                        warnings.Add(string.Format(Msg.DraftFailed, label, res.ResponseMessage));
                        continue;
                    }
                    existing.Add(new PackagingTag(day, shiftCode, meal, delta));
                    warnings.Add(string.Format(Msg.DraftCreated, res.Data?.MaterialIssue?.IssueNumber, label, delta.ToString("0.##", CultureInfo.InvariantCulture)));
                }
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " AfterPacking: ", ex.ToString()));
                warnings.Add(Msg.AutoFailed);
            }
            return warnings;
        }

        /// <summary>Định mức bản đang áp dụng của loại suất: vật tư → số / 1 suất.</summary>
        private async Task<List<(Guid IngredientId, decimal PerPortion)>> NormAsync(string meal)
        {
            var db = _unitOfWork.DbContext;
            var recipe = await db.Recipe.AsNoTracking().Where(r => !r.IsDeleted && r.IsActive && r.Status == RecipeStatus.Locked)
                .Join(db.Dish.AsNoTracking().Where(d => !d.IsDeleted && d.Code == DishPrefix + meal), r => r.DishId, d => d.Id, (r, d) => r)
                .OrderByDescending(r => r.VersionNo).FirstOrDefaultAsync();
            if (recipe == null)
            {
                return [];
            }
            decimal basis = recipe.StandardOutputQty is > 0 ? recipe.StandardOutputQty.Value : (_rules.RecipeStandardServings > 0 ? _rules.RecipeStandardServings : 100);
            return [.. (await db.RecipeLine.AsNoTracking().Where(l => !l.IsDeleted && l.RecipeId == recipe.Id).ToListAsync())
                .GroupBy(l => l.IngredientId).Select(g => (g.Key, g.Sum(l => l.Quantity) / basis))];
        }

        private async Task NotifyShortageAsync(DateTime day, string shiftCode, string label, List<(Guid IngredientId, decimal Qty)> lines)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var ids = lines.Select(l => l.IngredientId).ToList();
                var names = await db.Ingredient.AsNoTracking().Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => $"{i.Code} {i.Name}");
                var users = await RecipientResolver.UsersInGroupsAsync(db, ["OPS-SCM", "OPS-SCM-HEAD"]);
                if (users.Count == 0)
                {
                    return;
                }
                string text = $"📦 Thiếu vật tư bao bì cho {label} ca {shiftCode} ngày {day:dd/MM}: " + string.Join("; ", lines.Select(l => $"{names.GetValueOrDefault(l.IngredientId)} cần {l.Qty:0.##}"))
                    + ".\nBấm 🛒 để lập đề nghị mua nháp phần thiếu · định mức: #/" + ScreenId;
                await BusinessNotifier.SafeEnqueueAsync(_notifications, new NotificationRequest
                {
                    Template = NotificationCatalog.ManualText, UserIds = [.. users], BotCode = MessagingCode.BotVanHanh, Data = new { text },
                    Buttons = new[] { new NotificationButton("🛒 Tạo đề nghị mua", $"op:bpr:{day:yyyyMMdd}.{shiftCode}") }, // ACC-6: ThaoTacActions → PackagingNorm/CreateShortagePr
                    IdempotencyKey = $"PACKAGING_SHORT:{day:yyyyMMdd}:{shiftCode}:{string.Join(",", ids.OrderBy(x => x).Select(x => x.ToString("N")[..6]))}",
                });
            }
            catch (Exception ex)
            {
                Log.Warning(string.Concat("[", GetType().Name, "]", " NotifyShortage: ", ex.Message));
            }
        }

        public const string PurchaseTagPrefix = "[DN_BB:";

        /// <summary>
        /// ACC-6 (D-131 j): nút "🛒 Tạo đề nghị mua" trên tin thiếu bao bì – đề nghị mua Nháp cho phần thiếu của ngày × ca: suất đạt của ca × định mức − tồn khả dụng
        /// (mọi kho). Đã có đề nghị cùng ngày × ca (thẻ <c>[DN_BB:yyyyMMdd|mã ca]</c>, chưa hủy) → trả số cũ, không lập trùng.
        /// </summary>
        public async Task<IResult<string>> CreateShortagePurchaseRequest(DateTime day, string? shiftCode)
        {
            var db = _unitOfWork.DbContext;
            day = day.Date;
            string tag = $"{PurchaseTagPrefix}{day:yyyyMMdd}|{shiftCode}]";
            var old = await db.PurchaseRequest.AsNoTracking().Where(p => !p.IsDeleted && p.Note != null && p.Note.Contains(tag) && p.Status != "Cancelled" && p.Status != "Rejected")
                .Select(p => p.RequestNumber ?? p.RequestNo).FirstOrDefaultAsync();
            if (old != null)
            {
                return Result<string>.Success(old, string.Format(Msg.PrExists, old));
            }
            var shift = await db.Shift.AsNoTracking().FirstOrDefaultAsync(s => !s.IsDeleted && s.Code == shiftCode);
            string? meal = AccountingVoucherService.MealOfShift(shift?.Code, shift?.Name);
            if (shift == null || meal == null)
            {
                return Result<string>.Fail(Msg.PrNoShift);
            }
            decimal portions = (await db.MaterialIssue.AsNoTracking().Where(i => !i.IsDeleted && i.Status != MaterialIssueStatus.Cancelled && i.Note != null && i.Note.Contains(TagPrefix + day.ToString("yyyyMMdd")))
                .Select(i => i.Note).ToListAsync()).Select(ParseTag).Where(t => t != null && t.ShiftCode == shiftCode).Sum(t => t!.Portions);
            portions = Math.Max(portions, await PackedPortionsAsync(day, shift.Id));
            var norm = await NormAsync(meal);
            var ids = norm.Select(n => n.IngredientId).ToList();
            var stock = (await db.InventoryBalance.AsNoTracking().Where(b => !b.IsDeleted && ids.Contains(b.IngredientId)).Select(b => new { b.IngredientId, b.AvailableQuantity }).ToListAsync())
                .GroupBy(b => b.IngredientId).ToDictionary(g => g.Key, g => g.Sum(x => x.AvailableQuantity));
            var lines = norm.Select(n => (n.IngredientId, Qty: Math.Ceiling(n.PerPortion * portions - stock.GetValueOrDefault(n.IngredientId)))).Where(n => n.Qty > 0).ToList();
            if (lines.Count == 0)
            {
                return Result<string>.Fail(Msg.PrNothing);
            }
            var r = await _services.GetRequiredService<IPurchaseRequestService>().CreateWithLines(new PurchaseRequestCreateRequest
            {
                RequiredDate = _clock.Today.AddDays(1), Submit = false,
                Note = $"{tag} Bổ sung vật tư bao bì thiếu cho {Meals.First(m => m.Code == meal).Label.ToLowerInvariant()} ca {shiftCode} ngày {day:dd/MM} (định mức bao bì)",
                ListLine = [.. lines.Select(l => new PurchaseRequestLineInput { IngredientId = l.IngredientId, Quantity = l.Qty, Note = "Thiếu bao bì khi đóng gói" })],
            });
            if (r.ResponseCode != 200 || r.Data == null)
            {
                return Result<string>.Fail(r.ResponseMessage ?? Msg.PrNothing);
            }
            string number = r.Data.PurchaseRequest.RequestNumber ?? r.Data.PurchaseRequest.RequestNo ?? string.Empty;
            return Result<string>.Success(number, string.Format(Msg.PrCreated, number, lines.Count));
        }

        /// <summary>Suất đạt của 1 ca trong ngày (mỗi đơn lấy món đóng nhiều suất nhất).</summary>
        private async Task<decimal> PackedPortionsAsync(DateTime day, Guid shiftId)
        {
            var db = _unitOfWork.DbContext;
            DateTime next = day.AddDays(1);
            var rows = await (from p in db.PackingOrder.AsNoTracking()
                              join b in db.ProductionBatch.AsNoTracking() on p.ProductionBatchId equals b.Id
                              join o in db.ProductionOrder.AsNoTracking() on b.ProductionOrderId equals o.Id
                              join r in db.PackingResult.AsNoTracking() on p.Id equals r.PackingOrderId
                              where !p.IsDeleted && !r.IsDeleted && p.PackingDate >= day && p.PackingDate < next && o.ShiftId == shiftId
                              select new { Key = p.OrderId ?? p.ProductionBatchId, b.DishId, Qty = r.PackedQuantity - r.RejectedQuantity }).ToListAsync();
            return rows.GroupBy(x => x.Key).Sum(o => o.GroupBy(x => x.DishId).Max(d => d.Sum(x => x.Qty)));
        }

        // ---------------------------------------------------------------- tự ghi sổ khi hết ca

        public async Task<(int Posted, List<string> Failed)> AutoPostAsync(DateTime now)
        {
            var failed = new List<string>();
            int posted = 0;
            if (!_rules.PackagingIssueAutoPost)
            {
                return (0, failed);
            }
            var db = _unitOfWork.DbContext;
            var drafts = await db.MaterialIssue.AsNoTracking().Where(i => !i.IsDeleted && i.Status == MaterialIssueStatus.Draft && i.Note != null && i.Note.Contains(TagPrefix))
                .Select(i => new { i.Id, i.Note, Number = i.IssueNumber ?? i.IssueNo }).ToListAsync();
            var shifts = await db.Shift.AsNoTracking().Where(s => !s.IsDeleted).Select(s => new { s.Code, s.StartTime, s.EndTime }).ToListAsync();
            var issues = _services.GetRequiredService<IMaterialIssueService>();
            foreach (var d in drafts)
            {
                var t = ParseTag(d.Note);
                if (t == null)
                {
                    continue;
                }
                var sh = shifts.FirstOrDefault(s => s.Code == t.ShiftCode);
                DateTime end = sh?.EndTime is { } e ? t.Day.Add(e).AddDays(sh.StartTime is { } st && e <= st ? 1 : 0) : t.Day.AddDays(1);
                if (end > now)
                {
                    continue;
                }
                var r = await issues.Post(new InventoryDocumentActionRequest { Id = d.Id, Note = "Hệ thống tự ghi sổ phiếu xuất bao bì khi hết ca (ACC-6)" });
                db.ChangeTracker.Clear();
                if (r.ResponseCode == 200)
                {
                    posted++;
                }
                else
                {
                    failed.Add($"{d.Number}: {r.ResponseMessage}");
                }
            }
            if (failed.Count > 0)
            {
                try
                {
                    var users = await RecipientResolver.UsersInGroupsAsync(db, ["OPS-WH", "OPS-SCM-HEAD"]);
                    if (users.Count > 0)
                    {
                        await BusinessNotifier.SafeEnqueueAsync(_notifications, new NotificationRequest
                        {
                            Template = NotificationCatalog.ManualText, UserIds = [.. users], BotCode = MessagingCode.BotVanHanh,
                            Data = new { text = $"⚠️ {failed.Count} phiếu xuất bao bì chưa tự ghi sổ được: " + string.Join("; ", failed.Take(5)) + "\nMở Kho › Xuất kho sản xuất để xử lý." },
                            IdempotencyKey = $"PACKAGING_POST_FAIL:{now:yyyyMMddHH}",
                        });
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(string.Concat("[", GetType().Name, "]", " AutoPost notify: ", ex.Message));
                }
            }
            return (posted, failed);
        }

        public static class Msg
        {
            public const string MealInvalid = "Loại suất không hợp lệ (SANG, TRUA, TOI, DEM, COM_SUAT)";
            public const string Negative = "Định mức không được âm";
            public const string NeedLine = "Cần ít nhất 1 vật tư có định mức lớn hơn 0";
            public const string SupplyNotFound = "Không tìm thấy vật tư";
            public const string NoUnit = "Vật tư {0} chưa có đơn vị tính cơ bản";
            public const string Pending = "Định mức loại suất này đang chờ duyệt – chờ duyệt xong rồi sửa tiếp";
            public const string Saved = "Đã lưu định mức {0} (bản nháp) – bấm Gửi duyệt để áp dụng";
            public const string NoDraft = "Không có bản nháp để gửi duyệt – sửa định mức trước";
            public const string Submitted = "Đã gửi duyệt định mức – duyệt xong sẽ áp dụng";
            public const string NoNorm = "Chưa có định mức bao bì đã duyệt cho {0} – chưa lập phiếu xuất bao bì";
            public const string Short = "Thiếu tồn {0} vật tư bao bì cho {1} – đã báo Mua hàng";
            public const string DraftFailed = "Không lập được phiếu xuất bao bì {0}: {1}";
            public const string DraftCreated = "Đã lập phiếu xuất bao bì nháp {0} ({1}, {2} suất)";
            public const string AutoFailed = "Không tự lập được phiếu xuất bao bì – kho lập tay";
            public const string PrExists = "Đã có đề nghị mua bao bì {0} cho ca này";
            public const string PrNoShift = "Không xác định được ca / buổi để lập đề nghị mua";
            public const string PrNothing = "Tồn hiện đủ – không cần đề nghị mua thêm";
            public const string PrCreated = "Đã lập đề nghị mua nháp {0} ({1} vật tư bao bì) – Mua hàng kiểm rồi gửi duyệt";
        }
    }
}
