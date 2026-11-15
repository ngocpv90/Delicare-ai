using AppDelicare.Application.Common;
using AppDelicare.Application.Menu;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.WebApi.Api;
using AppDelicare.WebApi.Security;
using Microsoft.AspNetCore.Mvc;

namespace AppDelicare.WebApi.Controllers.V1.Menu
{
    public sealed class CreateRecipeBody
    {
        /// <summary>Sao chép dòng định lượng từ phiên bản này (tùy chọn).</summary>
        public Guid? CopyFromRecipeId { get; init; }
    }

    public sealed class DecisionBody
    {
        /// <summary>Ghi chú (bắt buộc khi từ chối).</summary>
        public string? Note { get; init; }
    }

    public sealed class LockBody
    {
        /// <example>2026-12-01</example>
        public DateTime EffectiveFrom { get; init; }
    }

    public sealed class RecalculateBody
    {
        /// <summary>Danh sách món; trống = mọi món có công thức đang hiệu lực.</summary>
        public List<Guid>? DishIds { get; init; }
    }

    /// <summary>
    /// Công thức và phiên bản (spec 5.2) – không có bảng RecipeVersion: mỗi phiên bản là một dòng Recipe cùng DishId (VersionNo).
    /// Luồng: Draft → (submit) PendingApproval → (approve) Approved → (lock) Locked; bản cũ → Obsolete. Danh sách / chi tiết chuẩn ở <c>/menu/recipes</c>.
    /// </summary>
    [Route(RoutePrefix + "/menu")]
    public sealed class RecipesController(IDispatcher dispatcher, ICurrentActor actor) : ApiControllerBase
    {
        private const string Screen = "menu-recipe";

        /// <summary>Các phiên bản của món (mới nhất trước) kèm dòng định lượng và chi phí NVL / suất.</summary>
        [HttpGet("dishes/{dishId:guid}/recipes"), Permission(Screen, PermissionAction.View)]
        [ProducesResponseType(typeof(ApiEnvelope<IReadOnlyList<RecipeVersion>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Versions(Guid dishId, CancellationToken ct) => Ok(await dispatcher.SendAsync(new DishRecipesQuery(dishId), ct));

        /// <summary>Tạo phiên bản nháp mới (VersionNo = max + 1, VersionCode = {mã món}-V{n}); chặn nếu đã có bản nháp / chờ duyệt.</summary>
        [HttpPost("dishes/{dishId:guid}/recipes"), Idempotent, Permission(Screen, PermissionAction.Add)]
        [ProducesResponseType(typeof(ApiEnvelope<Recipe>), StatusCodes.Status201Created)]
        public async Task<IActionResult> CreateVersion(Guid dishId, [FromBody] CreateRecipeBody? body, CancellationToken ct)
            => StatusCode(StatusCodes.Status201Created, await dispatcher.SendAsync(new CreateRecipeVersionCommand(dishId, body?.CopyFromRecipeId), ct));

        /// <summary>Thêm dòng định lượng (chỉ khi Draft). Quantity cho số suất chuẩn (mặc định 100); dùng UnitId.</summary>
        [HttpPost("recipes/{id:guid}/lines"), Permission("menu-recipe-line", PermissionAction.Add)]
        [ProducesResponseType(typeof(ApiEnvelope<RecipeLine>), StatusCodes.Status201Created)]
        public async Task<IActionResult> AddLine(Guid id, [FromBody] RecipeLineInput line, CancellationToken ct)
            => StatusCode(StatusCodes.Status201Created, await dispatcher.SendAsync(new AddRecipeLineCommand(id, line), ct));

        /// <summary>Sửa dòng định lượng (chỉ khi Draft).</summary>
        [HttpPut("recipe-lines/{lineId:guid}"), Permission("menu-recipe-line", PermissionAction.Edit)]
        [ProducesResponseType(typeof(ApiEnvelope<RecipeLine>), StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateLine(Guid lineId, [FromBody] RecipeLineInput line, CancellationToken ct)
            => Ok(await dispatcher.SendAsync(new UpdateRecipeLineCommand(lineId, line), ct));

        /// <summary>Xóa dòng định lượng (chỉ khi Draft).</summary>
        [HttpDelete("recipe-lines/{lineId:guid}"), Permission("menu-recipe-line", PermissionAction.Delete)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> DeleteLine(Guid lineId, CancellationToken ct)
        {
            await dispatcher.SendAsync(new DeleteRecipeLineCommand(lineId), ct);
            return NoContent();
        }

        /// <summary>Gửi duyệt (≥ 1 dòng) → PendingApproval, tạo ApprovalRequest RECIPE_VERSION.</summary>
        [HttpPost("recipes/{id:guid}/submit"), Permission(Screen, PermissionAction.Edit)]
        [ProducesResponseType(typeof(ApiEnvelope<Recipe>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Submit(Guid id, CancellationToken ct) => Ok(await dispatcher.SendAsync(new SubmitRecipeCommand(id, actor.RequireActor().UserId), ct));

        /// <summary>Duyệt (nhóm được duyệt "Thực đơn &amp; Món": CEO, COO – appsettings Permissions:ApproverGroups). Tạm thời trước khi có ApprovalService chung.</summary>
        [HttpPost("recipes/{id:guid}/approve"), Permission(Screen, PermissionAction.Approve)]
        [ProducesResponseType(typeof(ApiEnvelope<Recipe>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Approve(Guid id, [FromBody] DecisionBody? body, CancellationToken ct)
            => Ok(await dispatcher.SendAsync(new DecideRecipeCommand(id, true, body?.Note, actor.RequireActor().UserId), ct));

        /// <summary>Từ chối (bắt buộc ghi chú) → về Draft.</summary>
        [HttpPost("recipes/{id:guid}/reject"), Permission(Screen, PermissionAction.Approve)]
        [ProducesResponseType(typeof(ApiEnvelope<Recipe>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Reject(Guid id, [FromBody] DecisionBody body, CancellationToken ct)
            => Ok(await dispatcher.SendAsync(new DecideRecipeCommand(id, false, body.Note, actor.RequireActor().UserId), ct));

        /// <summary>Khóa phiên bản đã duyệt: bản cũ → Obsolete, bản này Locked + hiệu lực, SnapshotJson, StandardCost mới, cập nhật Dishe.StandardCost (spec 5.2 bước 1–5).</summary>
        [HttpPost("recipes/{id:guid}/lock"), Permission(Screen, PermissionAction.Approve)]
        [ProducesResponseType(typeof(ApiEnvelope<Recipe>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Lock(Guid id, [FromBody] LockBody body, CancellationToken ct)
            => Ok(await dispatcher.SendAsync(new LockRecipeCommand(id, body.EffectiveFrom, actor.RequireActor().UserId), ct));

        /// <summary>Chi phí NVL theo giá NCC ưu tiên đang hiệu lực (spec 5.2 bước 6). Dòng thiếu giá / quy đổi có <c>warning</c>.</summary>
        [HttpGet("recipes/{id:guid}/cost"), Permission(Screen, PermissionAction.View)]
        [ProducesResponseType(typeof(ApiEnvelope<RecipeCost>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Cost(Guid id, [FromQuery] decimal mealCount = 100, [FromQuery] DateTime? on = null, CancellationToken ct = default)
            => Ok(await dispatcher.SendAsync(new RecipeCostQuery(id, mealCount, on), ct));
    }

    /// <summary>Giá thành chuẩn (spec 5.3) – chỉ đọc (<c>/production/standard-costs</c>) và tính lại.</summary>
    [Route(RoutePrefix + "/production/standard-costs")]
    public sealed class StandardCostRecalcController(IDispatcher dispatcher) : ApiControllerBase
    {
        /// <summary>Tính lại giá thành chuẩn với giá NCC hiện hành (bước 4–5) cho các món có công thức đang hiệu lực.</summary>
        [HttpPost("recalculate"), Permission("cost-standard", PermissionAction.Edit)]
        [ProducesResponseType(typeof(ApiEnvelope<IReadOnlyList<StandardCost>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Recalculate([FromBody] RecalculateBody? body, CancellationToken ct)
            => Ok(await dispatcher.SendAsync(new RecalculateStandardCostsCommand(body?.DishIds), ct));
    }

    /// <summary>Đơn giá áp dụng (spec 5.4).</summary>
    [Route(RoutePrefix + "/sales/price-policies")]
    public sealed class PriceResolveController(IDispatcher dispatcher) : ApiControllerBase
    {
        /// <summary>Thứ tự: chính sách theo hợp đồng → theo khách → ContractMeal.UnitPrice. Không có → 404.</summary>
        [HttpGet("resolve"), Permission("crm-price-policy", PermissionAction.View)]
        [ProducesResponseType(typeof(ApiEnvelope<ResolvedPrice>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Resolve([FromQuery] Guid customerId, [FromQuery] Guid? contractId, [FromQuery] string? mealType, [FromQuery] DateTime? date, CancellationToken ct)
            => Ok(await dispatcher.SendAsync(new ResolvePriceQuery(customerId, contractId, mealType, date), ct));
    }
}
