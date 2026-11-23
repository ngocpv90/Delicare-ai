using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;

namespace AppDelicare.Services.Interface
{
    public interface IRecipeService
    {
        public Task<IResults<RecipeViewModel>> GetAll(RecipeSearchRequest request);
        public Task<IResult<bool>> Save(RecipeRequest request, string typeSave);
        public IResults<Recipe> GetAllData();
        public Task<IResult<Recipe>> GetById(Guid id);
        // <summary>T45R: phiên bản công thức của món.</summary>
        public Task<IResults<RecipeVersionModel>> GetByDishId(Guid dishId);
        public Task<IResult<Recipe>> CreateVersion(RecipeVersionRequest request);
        public Task<IResult<bool>> Submit(Guid id, DateTime? effectiveFrom = null);
        /// <summary>BIZ-2.3: khóa &amp; áp dụng không mở transaction riêng (gọi từ duyệt cấp cuối trong transaction của engine). Trả lỗi hoặc null.</summary>
        public Task<string?> LockCoreAsync(Guid id, DateTime from, Guid actorUserId);
        public Task<IResult<bool>> Decide(RecipeDecisionRequest request, bool approve);
        public Task<IResult<bool>> Lock(RecipeLockRequest request);
        public Task<StandardCost> UpsertStandardCostAsync(Guid dishId, Guid recipeId, DateTime from);
        public Task<IResult<RecipeCostModel>> CalculateCost(Guid id, decimal mealCount, DateTime? on);
        public Task<RecipeCostModel?> CalculateCostAsync(Guid recipeId, decimal mealCount, DateTime? on);
    }
}
