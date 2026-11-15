using System.Text.Json;
using AppDelicare.Application.Common;
using AppDelicare.Application.Common.Crud;
using AppDelicare.Application.MasterData;
using AppDelicare.Data.Connection;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AppDelicare.Application.Menu
{
    /// <summary>Trạng thái Recipe (Phụ lục B – PROPOSED): Draft → PendingApproval → Approved → Locked → Obsolete; Rejected về lại Draft.</summary>
    public static class RecipeStatus
    {
        public const string Draft = "Draft";
        public const string PendingApproval = "PendingApproval";
        public const string Approved = "Approved";
        public const string Locked = "Locked";
        public const string Obsolete = "Obsolete";
        public const string BusinessType = "RECIPE_VERSION";
    }

    public sealed record RecipeLineCost(Guid LineId, Guid IngredientId, string? IngredientName, decimal Quantity, Guid UnitId,
        decimal? UnitPrice, Guid? PriceUnitId, decimal? Amount, string? Warning);

    public sealed record RecipeCost(Guid RecipeId, decimal MealCount, decimal StandardServings, IReadOnlyList<RecipeLineCost> Lines,
        decimal TotalMaterialCost, decimal CostPerMeal, bool Complete);

    /// <summary>
    /// Spec 5.2 bước 6: chi phí NVL / suất = Σ (RecipeLine.Quantity × giá NCC ưu tiên đang hiệu lực) ÷ số suất chuẩn (StandardOutputQty, mặc định 100).
    /// Khác đơn vị → quy đổi qua UomService. Dòng thiếu giá / thiếu quy đổi: không tính, có cảnh báo, <c>Complete = false</c>.
    /// </summary>
    public interface IRecipeCostCalculator
    {
        Task<RecipeCost> CalculateAsync(Guid recipeId, decimal mealCount, DateTime? on, CancellationToken cancellationToken);
    }

    public sealed class RecipeCostCalculator(DatabaseContext db, IUomService uom, IOptions<BusinessRulesOptions> rules) : IRecipeCostCalculator
    {
        public async Task<RecipeCost> CalculateAsync(Guid recipeId, decimal mealCount, DateTime? on, CancellationToken ct)
        {
            var recipe = await db.Recipe.AsNoTracking().FirstOrDefaultAsync(r => r.Id == recipeId && !r.IsDeleted, ct)
                ?? throw new BusinessRuleException(404, "NOT_FOUND", "Không tìm thấy công thức.");
            decimal servings = recipe.StandardOutputQty is > 0 ? recipe.StandardOutputQty.Value : rules.Value.RecipeStandardServings;
            DateTime day = (on ?? DateTime.Today).Date;
            var lines = await db.RecipeLine.AsNoTracking().Where(l => l.RecipeId == recipeId && !l.IsDeleted).OrderBy(l => l.SortOrder).ThenBy(l => l.SequenceNo).ToListAsync(ct);
            var ingredientIds = lines.Select(l => l.IngredientId).Distinct().ToList();
            var names = await db.Ingredient.AsNoTracking().Where(i => ingredientIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Name, ct);
            var prices = await db.SupplierMaterial.AsNoTracking()
                .Where(m => !m.IsDeleted && m.IsPreferred && ingredientIds.Contains(m.IngredientId)
                    && (m.EffectiveFrom == null || m.EffectiveFrom <= day) && (m.EffectiveTo == null || m.EffectiveTo >= day))
                .OrderByDescending(m => m.EffectiveFrom).ToListAsync(ct);

            var result = new List<RecipeLineCost>();
            foreach (var l in lines)
            {
                var price = prices.FirstOrDefault(p => p.IngredientId == l.IngredientId);
                if (price?.UnitPrice is not { } unitPrice)
                {
                    result.Add(new RecipeLineCost(l.Id, l.IngredientId, names.GetValueOrDefault(l.IngredientId), l.Quantity, l.UnitId, null, null, null, "Chưa có giá NCC ưu tiên đang hiệu lực."));
                    continue;
                }
                Guid priceUnit = price.PurchaseUomId ?? l.UnitId;
                decimal qtyInPriceUnit;
                try
                {
                    qtyInPriceUnit = await uom.ConvertAsync(l.Quantity, l.UnitId, priceUnit, day, ct);
                }
                catch (BusinessRuleException)
                {
                    result.Add(new RecipeLineCost(l.Id, l.IngredientId, names.GetValueOrDefault(l.IngredientId), l.Quantity, l.UnitId, unitPrice, priceUnit, null, "Thiếu quy đổi đơn vị giữa định lượng và đơn vị giá NCC."));
                    continue;
                }
                result.Add(new RecipeLineCost(l.Id, l.IngredientId, names.GetValueOrDefault(l.IngredientId), l.Quantity, l.UnitId, unitPrice, priceUnit,
                    Math.Round(qtyInPriceUnit * unitPrice, 2), null));
            }
            decimal totalForStandard = result.Sum(r => r.Amount ?? 0m);
            decimal perMeal = servings > 0 ? Math.Round(totalForStandard / servings, 4) : 0m;
            return new RecipeCost(recipeId, mealCount, servings, result, Math.Round(perMeal * mealCount, 2), perMeal, result.All(r => r.Warning == null));
        }
    }

    internal static class Recipes
    {
        public static async Task<Recipe> RequireAsync(DatabaseContext db, Guid id, CancellationToken ct, bool tracking = true)
            => await (tracking ? db.Recipe : db.Recipe.AsNoTracking()).FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, ct)
                ?? throw new BusinessRuleException(404, "NOT_FOUND", "Không tìm thấy công thức.");

        public static void EnsureStatus(Recipe r, string expected, string action)
        {
            if (r.Status != expected)
            {
                throw new BusinessRuleException(409, "INVALID_TRANSITION", $"Chỉ {action} khi công thức ở trạng thái {expected} (hiện tại: {r.Status ?? "(trống)"}).");
            }
        }

        public static async Task EnsureDraftAsync(DatabaseContext db, Guid recipeId, CancellationToken ct)
            => EnsureStatus(await RequireAsync(db, recipeId, ct, tracking: false), RecipeStatus.Draft, "sửa định lượng");
    }

    // ================================================================ GET /menu/dishes/{dishId}/recipes
    public sealed record RecipeVersion(Guid Id, int VersionNo, string? VersionCode, string? Status, bool IsActive, DateTime? EffectiveFrom, DateTime? EffectiveTo,
        DateTime? LockedAt, decimal? StandardOutputQty, IReadOnlyList<RecipeLine> Lines, decimal CostPerMeal, bool CostComplete);

    public sealed record DishRecipesQuery(Guid DishId) : IQuery<IReadOnlyList<RecipeVersion>>;

    public sealed class DishRecipesHandler(DatabaseContext db, IRecipeCostCalculator cost) : IRequestHandler<DishRecipesQuery, IReadOnlyList<RecipeVersion>>
    {
        public async Task<IReadOnlyList<RecipeVersion>> HandleAsync(DishRecipesQuery q, CancellationToken ct)
        {
            if (!await db.Dish.AsNoTracking().AnyAsync(d => d.Id == q.DishId && !d.IsDeleted, ct))
            {
                throw new BusinessRuleException(404, "NOT_FOUND", "Không tìm thấy món.");
            }
            var recipes = await db.Recipe.AsNoTracking().Where(r => r.DishId == q.DishId && !r.IsDeleted).OrderByDescending(r => r.VersionNo).ToListAsync(ct);
            var ids = recipes.Select(r => r.Id).ToList();
            var lines = await db.RecipeLine.AsNoTracking().Where(l => ids.Contains(l.RecipeId) && !l.IsDeleted).OrderBy(l => l.SortOrder).ToListAsync(ct);
            var result = new List<RecipeVersion>();
            foreach (var r in recipes)
            {
                var c = await cost.CalculateAsync(r.Id, 1, null, ct);
                result.Add(new RecipeVersion(r.Id, r.VersionNo, r.VersionCode, r.Status, r.IsActive, r.EffectiveFrom, r.EffectiveTo, r.LockedAt, r.StandardOutputQty,
                    lines.Where(l => l.RecipeId == r.Id).ToList(), c.CostPerMeal, c.Complete));
            }
            return result;
        }
    }

    // ================================================================ POST /menu/dishes/{dishId}/recipes
    public sealed record CreateRecipeVersionCommand(Guid DishId, Guid? CopyFromRecipeId) : ICommand<Recipe>;

    /// <summary>VersionNo = max + 1, VersionCode = {Dishe.Code}-V{n}, Draft, IsActive = 0; sao chép RecipeLine từ bản nguồn. Chặn nếu đã có bản Draft / PendingApproval.</summary>
    public sealed class CreateRecipeVersionHandler(DatabaseContext db, IOptions<BusinessRulesOptions> rules) : IRequestHandler<CreateRecipeVersionCommand, Recipe>
    {
        public async Task<Recipe> HandleAsync(CreateRecipeVersionCommand c, CancellationToken ct)
        {
            var dish = await db.Dish.AsNoTracking().FirstOrDefaultAsync(d => d.Id == c.DishId && !d.IsDeleted, ct)
                ?? throw new BusinessRuleException(404, "NOT_FOUND", "Không tìm thấy món.");
            var existing = await db.Recipe.AsNoTracking().Where(r => r.DishId == c.DishId && !r.IsDeleted).ToListAsync(ct);
            if (existing.Any(r => r.Status is RecipeStatus.Draft or RecipeStatus.PendingApproval))
            {
                throw new BusinessRuleException(409, "INVALID_TRANSITION", "Món đang có bản công thức nháp / chờ duyệt – hoàn tất bản đó trước.");
            }
            Recipe? source = null;
            if (c.CopyFromRecipeId is { } srcId)
            {
                source = existing.FirstOrDefault(r => r.Id == srcId)
                    ?? throw new BusinessRuleException(422, "REF_NOT_FOUND", "Công thức nguồn không thuộc món này.", "copyFromRecipeId");
            }
            int version = existing.Count == 0 ? 1 : existing.Max(r => r.VersionNo) + 1;
            var recipe = new Recipe
            {
                Id = Guid.NewGuid(),
                DishId = c.DishId,
                VersionNo = version,
                VersionCode = $"{dish.Code}-V{version}",
                Status = RecipeStatus.Draft,
                IsActive = false,
                StandardOutputQty = source?.StandardOutputQty ?? rules.Value.RecipeStandardServings,
                YieldQuantity = source?.YieldQuantity ?? rules.Value.RecipeStandardServings,
                YieldUnitId = source?.YieldUnitId,
                YieldPercent = source?.YieldPercent,
                Instructions = source?.Instructions,
            };
            db.Recipe.Add(recipe);
            if (source != null)
            {
                var lines = await db.RecipeLine.AsNoTracking().Where(l => l.RecipeId == source.Id && !l.IsDeleted).ToListAsync(ct);
                foreach (var l in lines)
                {
                    db.RecipeLine.Add(new RecipeLine
                    {
                        Id = Guid.NewGuid(), RecipeId = recipe.Id, IngredientId = l.IngredientId, Quantity = l.Quantity, UnitId = l.UnitId, UomId = l.UnitId,
                        WastePercent = l.WastePercent, ExpectedWasteRate = l.ExpectedWasteRate, YieldPercent = l.YieldPercent,
                        SequenceNo = l.SequenceNo, SortOrder = l.SortOrder, PreparationNote = l.PreparationNote, Note = l.Note,
                    });
                }
            }
            await db.SaveChangesAsync(ConstantHelper.TypeSave.Insert, ct);
            return recipe;
        }
    }

    // ================================================================ dòng định lượng (chỉ khi Draft)
    public sealed record RecipeLineInput(Guid IngredientId, decimal Quantity, Guid UnitId, decimal? WastePercent, int? SequenceNo, string? PreparationNote);

    public sealed record AddRecipeLineCommand(Guid RecipeId, RecipeLineInput Line) : ICommand<RecipeLine>;

    public sealed record UpdateRecipeLineCommand(Guid LineId, RecipeLineInput Line) : ICommand<RecipeLine>;

    public sealed record DeleteRecipeLineCommand(Guid LineId) : ICommand<bool>;

    public sealed class RecipeLineInputRules : AbstractValidator<RecipeLineInput>
    {
        public RecipeLineInputRules()
        {
            RuleFor(x => x.IngredientId).NotEmpty().WithMessage("Nguyên liệu là bắt buộc.");
            RuleFor(x => x.UnitId).NotEmpty().WithMessage("Đơn vị là bắt buộc.");
            RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Định lượng (cho số suất chuẩn, mặc định 100) phải > 0.");
            RuleFor(x => x.WastePercent).InclusiveBetween(0, 99.9999m).When(x => x.WastePercent != null).WithMessage("Hao hụt phải trong [0, 100).");
        }
    }

    public sealed class AddRecipeLineValidator : AbstractValidator<AddRecipeLineCommand>
    {
        public AddRecipeLineValidator() => RuleFor(x => x.Line).SetValidator(new RecipeLineInputRules());
    }

    public sealed class UpdateRecipeLineValidator : AbstractValidator<UpdateRecipeLineCommand>
    {
        public UpdateRecipeLineValidator() => RuleFor(x => x.Line).SetValidator(new RecipeLineInputRules());
    }

    internal static class LineRefs
    {
        public static async Task EnsureAsync(DatabaseContext db, RecipeLineInput l, CancellationToken ct)
        {
            if (!await db.Ingredient.AsNoTracking().AnyAsync(i => i.Id == l.IngredientId && !i.IsDeleted, ct))
            {
                throw new BusinessRuleException(422, "REF_NOT_FOUND", "Nguyên liệu không tồn tại.", "ingredientId");
            }
            if (!await db.UnitMeasure.AsNoTracking().AnyAsync(u => u.Id == l.UnitId && !u.IsDeleted, ct))
            {
                throw new BusinessRuleException(422, "REF_NOT_FOUND", "Đơn vị không tồn tại.", "unitId");
            }
        }

        public static void Apply(RecipeLine e, RecipeLineInput l)
        {
            e.IngredientId = l.IngredientId;
            e.Quantity = l.Quantity;
            e.UnitId = e.UomId = l.UnitId; // ghi song song cột cũ UomId (NOT NULL)
            e.WastePercent = l.WastePercent;
            e.SequenceNo = l.SequenceNo;
            e.SortOrder = l.SequenceNo ?? e.SortOrder;
            e.PreparationNote = l.PreparationNote;
        }
    }

    public sealed class AddRecipeLineHandler(DatabaseContext db) : IRequestHandler<AddRecipeLineCommand, RecipeLine>
    {
        public async Task<RecipeLine> HandleAsync(AddRecipeLineCommand c, CancellationToken ct)
        {
            await Recipes.EnsureDraftAsync(db, c.RecipeId, ct);
            await LineRefs.EnsureAsync(db, c.Line, ct);
            int nextSort = (await db.RecipeLine.Where(l => l.RecipeId == c.RecipeId && !l.IsDeleted).MaxAsync(l => (int?)l.SortOrder, ct) ?? 0) + 1;
            var line = new RecipeLine { Id = Guid.NewGuid(), RecipeId = c.RecipeId, SortOrder = nextSort };
            LineRefs.Apply(line, c.Line);
            db.RecipeLine.Add(line);
            await db.SaveChangesAsync(ConstantHelper.TypeSave.Insert, ct);
            return line;
        }
    }

    public sealed class UpdateRecipeLineHandler(DatabaseContext db) : IRequestHandler<UpdateRecipeLineCommand, RecipeLine>
    {
        public async Task<RecipeLine> HandleAsync(UpdateRecipeLineCommand c, CancellationToken ct)
        {
            var line = await db.RecipeLine.FirstOrDefaultAsync(l => l.Id == c.LineId && !l.IsDeleted, ct)
                ?? throw new BusinessRuleException(404, "NOT_FOUND", "Không tìm thấy dòng định lượng.");
            await Recipes.EnsureDraftAsync(db, line.RecipeId, ct);
            await LineRefs.EnsureAsync(db, c.Line, ct);
            LineRefs.Apply(line, c.Line);
            await db.SaveChangesAsync(ConstantHelper.TypeSave.Update, ct);
            return line;
        }
    }

    public sealed class DeleteRecipeLineHandler(DatabaseContext db) : IRequestHandler<DeleteRecipeLineCommand, bool>
    {
        public async Task<bool> HandleAsync(DeleteRecipeLineCommand c, CancellationToken ct)
        {
            var line = await db.RecipeLine.FirstOrDefaultAsync(l => l.Id == c.LineId && !l.IsDeleted, ct)
                ?? throw new BusinessRuleException(404, "NOT_FOUND", "Không tìm thấy dòng định lượng.");
            await Recipes.EnsureDraftAsync(db, line.RecipeId, ct);
            db.Entry(line).State = EntityState.Modified;
            await db.SaveChangesAsync(ConstantHelper.TypeSave.Delete, ct);
            return true;
        }
    }

    // ================================================================ submit / approve / reject
    public sealed record SubmitRecipeCommand(Guid RecipeId, Guid ActorUserId) : ICommand<Recipe>;

    /// <summary>Phải có ≥ 1 dòng. Tạo ApprovalRequest (RECIPE_VERSION, Pending) → Status = PendingApproval. Số phiếu tạm "RV-yyMMdd-HHmmssfff" (mã chuẩn ở T11).</summary>
    public sealed class SubmitRecipeHandler(DatabaseContext db, IClock clock, ICurrentActor actor) : IRequestHandler<SubmitRecipeCommand, Recipe>
    {
        public async Task<Recipe> HandleAsync(SubmitRecipeCommand c, CancellationToken ct)
        {
            var recipe = await Recipes.RequireAsync(db, c.RecipeId, ct);
            Recipes.EnsureStatus(recipe, RecipeStatus.Draft, "gửi duyệt");
            if (!await db.RecipeLine.AnyAsync(l => l.RecipeId == recipe.Id && !l.IsDeleted, ct))
            {
                throw new BusinessRuleException(422, "BUSINESS_RULE", "Công thức phải có ít nhất 1 dòng định lượng.");
            }
            recipe.Status = RecipeStatus.PendingApproval;
            db.ApprovalRequest.Add(new ApprovalRequest
            {
                Id = Guid.NewGuid(),
                BusinessType = RecipeStatus.BusinessType,
                BusinessId = recipe.Id,
                RequestNo = "RV-" + clock.Now.ToString("yyMMdd-HHmmssfff", System.Globalization.CultureInfo.InvariantCulture),
                RequestStatus = "Pending",
                RequestedAt = clock.Now,
                RequestedBy = c.ActorUserId,
                CorrelationId = actor.CorrelationId.Length <= 100 ? actor.CorrelationId : actor.CorrelationId[..100],
                PayloadJson = JsonSerializer.Serialize(new { recipe.Id, recipe.DishId, recipe.VersionCode }),
            });
            await db.SaveChangesAsync(ConstantHelper.TypeSave.Insert, ct);
            return recipe;
        }
    }

    public sealed record DecideRecipeCommand(Guid RecipeId, bool Approve, string? Note, Guid ActorUserId) : ICommand<Recipe>;

    /// <summary>
    /// Duyệt / từ chối tạm thời (PROPOSED – trước khi có ApprovalService chung): duyệt → Approved (ApprovedAt / By); từ chối → về Draft.
    /// ApprovalRequest tương ứng → Approved / Rejected, CompletedAt.
    /// </summary>
    public sealed class DecideRecipeHandler(DatabaseContext db, IClock clock) : IRequestHandler<DecideRecipeCommand, Recipe>
    {
        public async Task<Recipe> HandleAsync(DecideRecipeCommand c, CancellationToken ct)
        {
            var recipe = await Recipes.RequireAsync(db, c.RecipeId, ct);
            Recipes.EnsureStatus(recipe, RecipeStatus.PendingApproval, c.Approve ? "duyệt" : "từ chối");
            if (!c.Approve && string.IsNullOrWhiteSpace(c.Note))
            {
                throw new BusinessRuleException(400, "VALIDATION_FAILED", "Từ chối phải ghi lý do.", "note");
            }
            recipe.Status = c.Approve ? RecipeStatus.Approved : RecipeStatus.Draft;
            if (c.Approve)
            {
                recipe.ApprovedAt = clock.Now;
                recipe.ApprovedBy = c.ActorUserId;
            }
            else
            {
                recipe.Note = string.IsNullOrWhiteSpace(recipe.Note) ? $"Từ chối: {c.Note}" : $"{recipe.Note}\nTừ chối: {c.Note}";
            }
            var request = await db.ApprovalRequest.Where(a => !a.IsDeleted && a.BusinessType == RecipeStatus.BusinessType && a.BusinessId == recipe.Id && a.RequestStatus == "Pending")
                .OrderByDescending(a => a.RequestedAt).FirstOrDefaultAsync(ct);
            if (request != null)
            {
                request.RequestStatus = c.Approve ? "Approved" : "Rejected";
                request.CompletedAt = clock.Now;
            }
            await db.SaveChangesAsync(ConstantHelper.TypeSave.Update, ct);
            return recipe;
        }
    }

    // ================================================================ POST /menu/recipes/{id}/lock (spec 5.2 bước 1–5)
    public sealed record LockRecipeCommand(Guid RecipeId, DateTime EffectiveFrom, Guid ActorUserId) : ICommand<Recipe>;

    public sealed class LockRecipeHandler(DatabaseContext db, IClock clock, IRecipeCostCalculator cost, IOptions<BusinessRulesOptions> rules)
        : IRequestHandler<LockRecipeCommand, Recipe>
    {
        public async Task<Recipe> HandleAsync(LockRecipeCommand c, CancellationToken ct)
        {
            var recipe = await Recipes.RequireAsync(db, c.RecipeId, ct);
            Recipes.EnsureStatus(recipe, RecipeStatus.Approved, "khóa");
            DateTime from = c.EffectiveFrom.Date;
            await using var tx = db.Database.IsRelational() && db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync(ct) : null;

            // 1. Bản đang hiệu lực khác của món → Obsolete, EffectiveTo = from − 1 ngày.
            var actives = await db.Recipe.Where(r => r.DishId == recipe.DishId && r.Id != recipe.Id && r.IsActive && !r.IsDeleted).ToListAsync(ct);
            foreach (var old in actives)
            {
                old.IsActive = false;
                old.Status = RecipeStatus.Obsolete;
                old.EffectiveTo = from.AddDays(-1);
            }
            // 2. Bản hiện tại → Locked, IsActive, EffectiveFrom, LockedAt / By.
            recipe.Status = RecipeStatus.Locked;
            recipe.IsActive = true;
            recipe.EffectiveFrom = from;
            recipe.EffectiveTo = null;
            recipe.LockedAt = clock.Now;
            recipe.LockedBy = c.ActorUserId;
            // 3. SnapshotJson toàn bộ RecipeLine.
            var lines = await db.RecipeLine.AsNoTracking().Where(l => l.RecipeId == recipe.Id && !l.IsDeleted).OrderBy(l => l.SortOrder).ToListAsync(ct);
            recipe.SnapshotJson = JsonSerializer.Serialize(lines.Select(l => new { l.IngredientId, l.Quantity, l.UnitId, l.WastePercent, l.SequenceNo }));
            await db.SaveChangesAsync(ConstantHelper.TypeSave.Update, ct);

            // 4–5. StandardCost + Dishe.StandardCost.
            await StandardCosts.UpsertAsync(db, cost, rules.Value, recipe.DishId, recipe.Id, from, ct);
            if (tx != null)
            {
                await tx.CommitAsync(ct);
            }
            return recipe;
        }
    }

    internal static class StandardCosts
    {
        public const string Active = "Active";
        public const string Inactive = "Inactive";

        /// <summary>
        /// Bước 4–5: tạo StandardCost mới cho (DishId, RecipeId) – MaterialCost = chi phí NVL / suất (bước 6), Labor / Overhead / suất từ cấu hình
        /// (PROPOSED), TotalCost = CostPerServing = tổng; dòng StandardCost cũ của món → Inactive; Dishe.StandardCost = CostPerServing.
        /// </summary>
        public static async Task<StandardCost> UpsertAsync(DatabaseContext db, IRecipeCostCalculator cost, BusinessRulesOptions rules, Guid dishId, Guid recipeId, DateTime from, CancellationToken ct)
        {
            var c = await cost.CalculateAsync(recipeId, 1, from, ct);
            foreach (var old in await db.StandardCost.Where(s => s.DishId == dishId && !s.IsDeleted && s.Status != Inactive).ToListAsync(ct))
            {
                old.Status = Inactive;
                old.EffectiveTo ??= from.AddDays(-1);
            }
            decimal material = Math.Round(c.CostPerMeal, 2);
            decimal total = material + rules.RecipeLaborCostPerServing + rules.RecipeOverheadCostPerServing;
            var sc = new StandardCost
            {
                Id = Guid.NewGuid(), DishId = dishId, RecipeId = recipeId, MaterialCost = material,
                LaborCost = rules.RecipeLaborCostPerServing, OverheadCost = rules.RecipeOverheadCostPerServing,
                TotalCost = total, CostPerServing = total, CurrencyCode = "VND", EffectiveFrom = from, Status = Active,
            };
            db.StandardCost.Add(sc);
            var dish = await db.Dish.FirstAsync(d => d.Id == dishId, ct);
            dish.StandardCost = Math.Round(total, 2);
            await db.SaveChangesAsync(ConstantHelper.TypeSave.Insert, ct);
            return sc;
        }
    }

    // ================================================================ GET /menu/recipes/{id}/cost
    public sealed record RecipeCostQuery(Guid RecipeId, decimal MealCount, DateTime? On) : IQuery<RecipeCost>;

    public sealed class RecipeCostQueryValidator : AbstractValidator<RecipeCostQuery>
    {
        public RecipeCostQueryValidator() => RuleFor(x => x.MealCount).GreaterThan(0).WithMessage("mealCount phải > 0.");
    }

    public sealed class RecipeCostHandler(IRecipeCostCalculator cost) : IRequestHandler<RecipeCostQuery, RecipeCost>
    {
        public Task<RecipeCost> HandleAsync(RecipeCostQuery q, CancellationToken ct) => cost.CalculateAsync(q.RecipeId, q.MealCount, q.On, ct);
    }

    // ================================================================ POST /production/standard-costs/recalculate (spec 5.3)
    public sealed record RecalculateStandardCostsCommand(IReadOnlyList<Guid>? DishIds) : ICommand<IReadOnlyList<StandardCost>>;

    /// <summary>Chạy lại bước 4–5 với giá NCC hiện hành cho món có công thức đang hiệu lực (Locked, IsActive).</summary>
    public sealed class RecalculateStandardCostsHandler(DatabaseContext db, IRecipeCostCalculator cost, IOptions<BusinessRulesOptions> rules, IClock clock)
        : IRequestHandler<RecalculateStandardCostsCommand, IReadOnlyList<StandardCost>>
    {
        public async Task<IReadOnlyList<StandardCost>> HandleAsync(RecalculateStandardCostsCommand c, CancellationToken ct)
        {
            var active = db.Recipe.AsNoTracking().Where(r => r.IsActive && !r.IsDeleted && r.Status == RecipeStatus.Locked);
            if (c.DishIds is { Count: > 0 } ids)
            {
                active = active.Where(r => ids.Contains(r.DishId));
            }
            var recipes = await active.ToListAsync(ct);
            var result = new List<StandardCost>();
            foreach (var r in recipes)
            {
                result.Add(await StandardCosts.UpsertAsync(db, cost, rules.Value, r.DishId, r.Id, clock.Today, ct));
            }
            return result;
        }
    }

    // ================================================================ dishes enrich (spec 5.1)
    public static class P2Hooks
    {
        /// <summary>activeRecipe { id, versionCode, status } + estimatedMaterialCost (chi phí NVL / suất của công thức hiệu lực).</summary>
        public static async Task DishEnrich(CrudEnrichContext c)
        {
            var ids = c.Ids;
            var recipes = await c.Db.Recipe.AsNoTracking().Where(r => ids.Contains(r.DishId) && r.IsActive && !r.IsDeleted)
                .Select(r => new { r.Id, r.DishId, r.VersionCode, r.Status }).ToListAsync(c.CancellationToken);
            var calculator = new RecipeCostCalculator(c.Db, new UomService(c.Db), Options.Create(new BusinessRulesOptions()));
            foreach (var item in c.Items)
            {
                var r = recipes.FirstOrDefault(x => x.DishId == (Guid)item["id"]!);
                item["activeRecipe"] = r == null ? null : new { r.Id, r.VersionCode, r.Status };
                item["estimatedMaterialCost"] = r == null ? null : (await calculator.CalculateAsync(r.Id, 1, null, c.CancellationToken)).CostPerMeal;
            }
        }

        /// <summary>Spec 5.4: không chồng hiệu lực cùng (CustomerId, ContractId, MealType) giữa các chính sách Active.</summary>
        public static async Task PricePolicyNoOverlap(CrudRuleContext c)
        {
            if (c.Get<string>("Status") is { } st && st != "Active")
            {
                return;
            }
            Guid? customer = c.Values.GetValueOrDefault("CustomerId") as Guid?;
            Guid? contract = c.Values.GetValueOrDefault("ContractId") as Guid?;
            string? meal = c.Get<string>("MealType");
            DateTime from = c.Values.GetValueOrDefault("EffectiveFrom") as DateTime? ?? DateTime.MinValue;
            DateTime to = c.Values.GetValueOrDefault("EffectiveTo") as DateTime? ?? DateTime.MaxValue;
            bool overlap = await c.Db.PricePolicy.AnyAsync(p => !p.IsDeleted && p.Id != c.Id && (p.Status == null || p.Status == "Active")
                && p.CustomerId == customer && p.ContractId == contract && p.MealType == meal
                && (p.EffectiveFrom == null || p.EffectiveFrom <= to) && (p.EffectiveTo == null || p.EffectiveTo >= from), c.CancellationToken);
            if (overlap)
            {
                throw new BusinessRuleException(422, "BUSINESS_RULE", "Chồng thời gian hiệu lực với chính sách giá khác cùng khách / hợp đồng / loại suất.", "effectiveFrom");
            }
        }
    }

    // ================================================================ GET /sales/price-policies/resolve (spec 5.4)
    public sealed record ResolvedPrice(decimal UnitPrice, string Source, Guid SourceId);

    public sealed record ResolvePriceQuery(Guid CustomerId, Guid? ContractId, string? MealType, DateTime? Date) : IQuery<ResolvedPrice>;

    /// <summary>Thứ tự (PROPOSED): PricePolicie theo hợp đồng → PricePolicie theo khách (không gắn hợp đồng) → ContractMeal.UnitPrice.</summary>
    public sealed class ResolvePriceHandler(DatabaseContext db) : IRequestHandler<ResolvePriceQuery, ResolvedPrice>
    {
        public async Task<ResolvedPrice> HandleAsync(ResolvePriceQuery q, CancellationToken ct)
        {
            DateTime day = (q.Date ?? DateTime.Today).Date;
            IQueryable<PricePolicy> Effective() => db.PricePolicy.AsNoTracking().Where(p => !p.IsDeleted && (p.Status == null || p.Status == "Active")
                && (p.EffectiveFrom == null || p.EffectiveFrom <= day) && (p.EffectiveTo == null || p.EffectiveTo >= day)
                && (q.MealType == null || p.MealType == q.MealType || p.MealType == null) && p.UnitPrice > 0);
            if (q.ContractId is { } contractId)
            {
                var byContract = await Effective().Where(p => p.ContractId == contractId).OrderByDescending(p => p.MealType != null).ThenByDescending(p => p.EffectiveFrom).FirstOrDefaultAsync(ct);
                if (byContract != null)
                {
                    return new ResolvedPrice(byContract.UnitPrice, "PricePolicie:Contract", byContract.Id);
                }
            }
            var byCustomer = await Effective().Where(p => p.CustomerId == q.CustomerId && p.ContractId == null).OrderByDescending(p => p.MealType != null).ThenByDescending(p => p.EffectiveFrom).FirstOrDefaultAsync(ct);
            if (byCustomer != null)
            {
                return new ResolvedPrice(byCustomer.UnitPrice, "PricePolicie:Customer", byCustomer.Id);
            }
            if (q.ContractId is { } cid)
            {
                var meal = await db.ContractMeal.AsNoTracking().Where(m => !m.IsDeleted && m.ContractId == cid && (q.MealType == null || m.MealType == q.MealType) && m.UnitPrice > 0)
                    .FirstOrDefaultAsync(ct);
                if (meal != null)
                {
                    return new ResolvedPrice(meal.UnitPrice!.Value, "ContractMeal", meal.Id);
                }
            }
            throw new BusinessRuleException(404, "NOT_FOUND", "Không tìm thấy đơn giá áp dụng cho khách / hợp đồng / loại suất này.");
        }
    }
}
