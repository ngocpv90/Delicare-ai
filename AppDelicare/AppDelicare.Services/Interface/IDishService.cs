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
    public interface IDishService
    {
        public Task<IResults<DishViewModel>> GetAll(DishSearchRequest request);
        public Task<IResult<bool>> Save(DishRequest request, string typeSave);
        /// <summary>UI-6.17 (D-98): thêm món – trả id + mã.</summary>
        public Task<IResult<DishCreatedModel>> Create(DishRequest request);
        public IResults<Dish> GetAllData();
        public Task<IResult<Dish>> GetById(Guid id);
        // <summary>T45R: công thức hiệu lực + giá vốn / suất.</summary>
        public Task<IResult<RecipeVersionModel>> GetActiveRecipe(Guid id);
        /// <summary>UI-6 (D-80): trang chi tiết toàn màn (…/GetOverview).</summary>
        public Task<IResult<DishOverviewModel>> GetOverview(Guid id);
    }
}
