using AppDelicare.Data.Interface;
using AppDelicare.Data.Share;
using AppDelicare.Model.Helper;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace AppDelicare.Services.Implement.Approval
{
    /// <summary>
    /// T12 – duyệt phiên bản công thức (BusinessType RECIPE_VERSION, màn menu-recipe). Nội dung được duyệt = món, mã phiên bản,
    /// số suất chuẩn và các dòng định lượng; nếu bị sửa sau khi gửi thì không duyệt được.
    /// </summary>
    public class RecipeApprovalHandler(IUnitOfWork unitOfWork, IClock clock, IServiceProvider services) : IApprovalHandler
    {
        private readonly IServiceProvider _services = services;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;

        public string BusinessType => RecipeStatus.BusinessType;

        public string ScreenId => "menu-recipe";

        public async Task<string?> BuildPayloadAsync(Guid businessId)
        {
            var recipe = await _unitOfWork.RecipeRepository.GetAll().AsNoTracking().FirstOrDefaultAsync(r => r.Id == businessId && !r.IsDeleted);
            if (recipe == null)
            {
                return null;
            }
            var lines = await _unitOfWork.RecipeLineRepository.GetAll().AsNoTracking().Where(l => l.RecipeId == businessId && !l.IsDeleted)
                .OrderBy(l => l.SortOrder).ThenBy(l => l.IngredientId)
                .Select(l => new { l.IngredientId, l.Quantity, l.UnitId, l.WastePercent, l.ExpectedWasteRate }).ToListAsync();
            return JsonConvert.SerializeObject(new { recipe.Id, recipe.DishId, recipe.VersionCode, recipe.StandardOutputQty, Lines = lines });
        }

        public async Task<string?> DescribeAsync(Guid businessId)
        {
            var row = await (from r in _unitOfWork.RecipeRepository.GetAll()
                             join d in _unitOfWork.DishRepository.GetAll() on r.DishId equals d.Id
                             where r.Id == businessId
                             select new { r.VersionCode, d.Name }).FirstOrDefaultAsync();
            return row == null ? null : $"Công thức {row.VersionCode} – {row.Name}";
        }

        /// <summary>BIZ-2.3 (D-77 – B4): duyệt cấp cuối = Đã duyệt + KHÓA &amp; ÁP DỤNG ngay từ ngày chọn khi gửi (đã qua → hôm nay); bỏ bước Khóa riêng.</summary>
        public async Task<string?> ApproveAsync(Guid businessId, Guid actorUserId, string? note)
        {
            var recipe = _unitOfWork.RecipeRepository.GetSingle(r => r.Id == businessId && !r.IsDeleted);
            if (recipe == null || recipe.Status != RecipeStatus.PendingApproval)
            {
                return string.Format(ConstantHelper.Message.InvalidStatusForAction, "duyệt", RecipeStatus.PendingApproval, recipe?.Status ?? "(không có)");
            }
            recipe.Status = RecipeStatus.Approved;
            recipe.ApprovedAt = _clock.Now;
            recipe.ApprovedBy = actorUserId;
            _unitOfWork.RecipeRepository.Update(recipe);
            DateTime from = recipe.EffectiveFrom is { } f && f.Date >= _clock.Today ? f.Date : _clock.Today;
            return await _services.GetRequiredService<IRecipeService>().LockCoreAsync(recipe.Id, from, actorUserId);
        }

        public Task<string?> RejectAsync(Guid businessId, Guid actorUserId, string note)
        {
            var recipe = _unitOfWork.RecipeRepository.GetSingle(r => r.Id == businessId && !r.IsDeleted);
            if (recipe == null || recipe.Status != RecipeStatus.PendingApproval)
            {
                return Task.FromResult<string?>(string.Format(ConstantHelper.Message.InvalidStatusForAction, "từ chối", RecipeStatus.PendingApproval, recipe?.Status ?? "(không có)"));
            }
            recipe.Status = RecipeStatus.Draft;
            recipe.Note = string.IsNullOrWhiteSpace(recipe.Note) ? $"Từ chối: {note}" : $"{recipe.Note}\nTừ chối: {note}";
            _unitOfWork.RecipeRepository.Update(recipe);
            return Task.FromResult<string?>(null);
        }

        /// <summary>APR-1.2 (D-81 e): khuôn tóm tắt chung – ApprovalSummaries.</summary>
        public Task<AppDelicare.Model.Model.ApprovalSummaryModel?> GetSummaryAsync(Guid businessId, ApprovalSummaryContext ctx) => ApprovalSummaries.RecipeAsync(businessId, ctx);
    }
}
