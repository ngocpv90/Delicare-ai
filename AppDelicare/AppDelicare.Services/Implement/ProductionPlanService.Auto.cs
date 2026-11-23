using AppDelicare.Data.Share;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Implement.Messaging;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// BIZ-2.4 (D-77 – B5, C3): sau chốt đơn (job 20:00 + nút Chốt theo ngày) → <c>Generate</c> → tự Duyệt + tự Phát hành mọi ca có nhu cầu.
    /// KHÔNG tự phát hành (kế hoạch Nháp, bot 🔴 báo Kế hoạch + COO kèm lý do) khi: kế hoạch đã bị sửa tay, món thiếu công thức khóa, thiếu nguyên liệu sau giữ chỗ,
    /// mẻ không xếp kịp giờ xuất bếp (giờ bắt đầu đã qua). Lỗi bước tự động không làm hỏng việc chốt đơn – trả trạng thái để người làm tay.
    /// <para>Kiểm "thiếu NL / không kịp giờ" cần phát hành thử: chạy Duyệt + Phát hành trong 1 transaction CSDL rồi hủy nếu có lý do giữ lại
    /// (CSDL không hỗ trợ transaction – test InMemory – chỉ kiểm được trước: sửa tay, thiếu công thức khóa).</para>
    /// </summary>
    public partial class ProductionPlanService
    {
        public const string AutoReleased = "RELEASED";
        public const string AutoHeld = "HELD";
        public const string AutoSkipped = "SKIPPED";
        public const string AutoNone = "NONE";

        private static string Prefix(string template) => template[..Math.Max(0, template.IndexOf('{'))];

        public async Task<ProductionAutoPlanModel> AutoPlanAfterLockAsync(DateTime productionDate)
        {
            var db = _unitOfWork.DbContext;
            DateTime date = productionDate.Date, next = date.AddDays(1);
            var result = new ProductionAutoPlanModel { ProductionDate = date };
            try
            {
                var existing = await db.ProductionPlan.AsNoTracking()
                    .Where(p => !p.IsDeleted && p.ProductionDate >= date && p.ProductionDate < next && p.OrganizationUnitId == null && p.ShiftId == null)
                    .OrderBy(p => p.CreatedDate).FirstOrDefaultAsync();
                if (existing != null)
                {
                    (result.PlanId, result.PlanNumber) = (existing.Id, existing.PlanNumber ?? existing.PlanNo);
                    if (existing.Status != ProductionPlanStatus.Draft)
                    {
                        result.Status = AutoSkipped;
                        result.ListReason.Add(string.Format(ConstantHelper.Message.AutoPlanAlreadyApproved, result.PlanNumber, ProductionPlanStatus.Label(existing.Status)));
                        return result;
                    }
                    if (await ManuallyEditedAsync(existing))
                    {
                        return await HoldAsync(result, [string.Format(ConstantHelper.Message.AutoPlanEditedByHand, result.PlanNumber)]);
                    }
                }

                var generated = await Generate(new ProductionPlanGenerateRequest { ProductionDate = date, Note = "Hệ thống tự lập sau chốt đơn" });
                if (generated.ResponseCode != 200 || generated.Data?.ProductionPlan is not { } plan)
                {
                    result.Status = AutoNone;
                    result.ListReason.Add(generated.ResponseMessage ?? string.Empty);
                    return result;
                }
                (result.PlanId, result.PlanNumber) = (plan.Id, plan.PlanNumber ?? plan.PlanNo);
                result.ListWarning.AddRange(generated.Data.ListWarning);

                // Trước: món thiếu công thức KHÓA tại ngày (công thức Đã duyệt chưa khóa cũng giữ lại – BIZ-2.3 duyệt = khóa).
                var dishIds = await db.ProductionPlanLine.AsNoTracking().Where(l => l.ProductionPlanId == plan.Id && !l.IsDeleted && l.PlannedQuantity > 0).Select(l => l.DishId).Distinct().ToListAsync();
                var noRecipe = new List<string>();
                foreach (Guid dishId in dishIds)
                {
                    var (recipe, locked, lines) = await ProductionRules.SelectRecipeAsync(db, dishId, date);
                    if (recipe == null || !locked || lines.Count == 0)
                    {
                        noRecipe.Add(await db.Dish.AsNoTracking().Where(d => d.Id == dishId).Select(d => d.Name ?? d.Code).FirstOrDefaultAsync() ?? dishId.ToString());
                    }
                }
                if (noRecipe.Count != 0)
                {
                    return await HoldAsync(result, [string.Format(ConstantHelper.Message.AutoPlanNoLockedRecipe, string.Join(", ", noRecipe))]);
                }

                // Duyệt + phát hành thử trong 1 transaction CSDL: thiếu NL sau giữ chỗ / mẻ không kịp giờ → hủy, giữ Nháp.
                bool relational = db.Database.IsRelational() && db.Database.CurrentTransaction == null;
                var tx = relational ? await db.Database.BeginTransactionAsync() : null;
                try
                {
                    var approved = await Approve(new ProductionActionRequest { Id = plan.Id, Note = "Hệ thống tự duyệt sau chốt đơn" });
                    if (approved.ResponseCode != 200)
                    {
                        if (tx != null) { await tx.RollbackAsync(); db.ChangeTracker.Clear(); }
                        return await HoldAsync(result, [approved.ResponseMessage ?? string.Empty]);
                    }
                    var released = await Release(new ProductionPlanReleaseRequest { Id = plan.Id, Note = "Hệ thống tự phát hành sau chốt đơn" });
                    if (released.ResponseCode != 200)
                    {
                        if (tx != null) { await tx.RollbackAsync(); db.ChangeTracker.Clear(); }
                        else { await RevertToDraftAsync(plan.Id); }
                        return await HoldAsync(result, [released.ResponseMessage ?? string.Empty]);
                    }
                    var warnings = released.Data?.ListWarning ?? [];
                    string[] blocking = [Prefix(ConstantHelper.Message.ReservationShort), Prefix(ConstantHelper.Message.ReservationUnitMissing), Prefix(ConstantHelper.Message.ReleaseStartInPast)];
                    var reasons = warnings.Where(w => blocking.Any(b => b.Length > 0 && w.StartsWith(b, StringComparison.Ordinal))).Distinct().ToList();
                    if (reasons.Count != 0 && tx != null)
                    {
                        await tx.RollbackAsync();
                        db.ChangeTracker.Clear();
                        return await HoldAsync(result, reasons);
                    }
                    if (tx != null)
                    {
                        await tx.CommitAsync();
                    }
                    result.Status = AutoReleased;
                    result.ListWarning.AddRange(warnings);
                    result.ProductionOrderCount = released.Data?.ListOrder.Count ?? 0;
                    await MarkAutoAsync(plan.Id, "Hệ thống tự duyệt + phát hành sau chốt đơn");
                    await BusinessNotifier.ToMenusAsync(_notifications, db, NotificationCatalog.PlanReady, ["#/prod-plan"],
                        new { title = $"🟢 Hệ thống đã phát hành kế hoạch {result.PlanNumber} ngày {date:dd/MM}: {result.ProductionOrderCount} lệnh SX", lines = warnings.Take(5).ToList() },
                        $"{NotificationCatalog.PlanReady}:AUTO:{date:yyyyMMdd}");
                    return result;
                }
                finally
                {
                    if (tx != null)
                    {
                        await tx.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[ProductionPlanService] AutoPlanAfterLockAsync {Date}", date);
                result.Status = AutoHeld;
                result.ListReason.Add(ConstantHelper.Message.AutoPlanError);
                return result;
            }
        }

        /// <summary>Kế hoạch Nháp có dòng sửa sau lần sinh (Note JSON GeneratedAt) hoặc không do Generate sinh → coi là sửa tay.</summary>
        private async Task<bool> ManuallyEditedAsync(Model.Entity.ProductionPlan plan)
        {
            DateTime? generatedAt = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(plan.Note) && plan.Note.TrimStart().StartsWith('{'))
                {
                    var json = JObject.Parse(plan.Note);
                    if ((string?)json["Source"] != "GENERATE")
                    {
                        return true;
                    }
                    generatedAt = (DateTime?)json["GeneratedAt"];
                }
                else
                {
                    return true;
                }
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return true;
            }
            var limit = (generatedAt ?? DateTime.MinValue).AddSeconds(5);
            return await _unitOfWork.DbContext.ProductionPlanLine.AsNoTracking()
                .AnyAsync(l => l.ProductionPlanId == plan.Id && l.ModifiedDate != null && l.ModifiedDate > limit);
        }

        private async Task<ProductionAutoPlanModel> HoldAsync(ProductionAutoPlanModel result, List<string> reasons)
        {
            result.Status = AutoHeld;
            result.ListReason.AddRange(reasons.Where(r => !string.IsNullOrWhiteSpace(r)));
            if (result.PlanId is { } id)
            {
                await MarkAutoAsync(id, "Hệ thống KHÔNG tự phát hành – chờ COO duyệt: " + string.Join("; ", result.ListReason));
            }
            var db = _unitOfWork.DbContext;
            var users = new HashSet<Guid>(await RecipientResolver.UsersWithMenuOnlyAsync(db, "#/prod-plan", true));
            users.UnionWith(await RecipientResolver.UsersInGroupsAsync(db, ["COO"]));
            await BusinessNotifier.SafeEnqueueAsync(_notifications, new NotificationRequest
            {
                Template = NotificationCatalog.PlanReady, UserIds = [.. users],
                Data = new { title = $"🔴 Kế hoạch {result.PlanNumber ?? string.Empty} ngày {result.ProductionDate:dd/MM} chưa tự phát hành – cần COO duyệt", lines = result.ListReason.Take(5).ToList() },
                IdempotencyKey = $"{NotificationCatalog.PlanReady}:HELD:{result.ProductionDate:yyyyMMdd}",
            });
            return result;
        }

        /// <summary>Ghi lịch sử "Hệ thống tự …" vào Note JSON của kế hoạch (khóa AutoRun) – AuditChange ghi lần sửa.</summary>
        private async Task MarkAutoAsync(Guid planId, string text)
        {
            var plan = _unitOfWork.ProductionPlanRepository.GetSingle(p => p.Id == planId && !p.IsDeleted);
            if (plan == null)
            {
                return;
            }
            JObject json;
            try
            {
                json = !string.IsNullOrWhiteSpace(plan.Note) && plan.Note.TrimStart().StartsWith('{') ? JObject.Parse(plan.Note) : new JObject { ["UserNote"] = plan.Note };
            }
            catch (Newtonsoft.Json.JsonException)
            {
                json = new JObject { ["UserNote"] = plan.Note };
            }
            json["AutoRun"] = $"{_clock.Now:HH:mm dd/MM/yyyy} {text}";
            plan.Note = json.ToString(Newtonsoft.Json.Formatting.None);
            _unitOfWork.ProductionPlanRepository.Update(plan);
            await _unitOfWork.ProductionPlanRepository.CommitAsync(ConstantHelper.TypeSave.Update);
        }

        /// <summary>CSDL không có transaction (InMemory): phát hành lỗi sau khi đã duyệt → trả kế hoạch về Nháp.</summary>
        private async Task RevertToDraftAsync(Guid planId)
        {
            var plan = _unitOfWork.ProductionPlanRepository.GetSingle(p => p.Id == planId && !p.IsDeleted);
            if (plan != null && plan.Status == ProductionPlanStatus.Approved)
            {
                plan.Status = ProductionPlanStatus.Draft;
                _unitOfWork.ProductionPlanRepository.Update(plan);
                await _unitOfWork.ProductionPlanRepository.CommitAsync(ConstantHelper.TypeSave.Update);
            }
        }
    }
}
