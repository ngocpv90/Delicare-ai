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
        public IResults<Dish> GetAllData();
        public Task<IResult<Dish>> GetById(Guid id);
        // <summary>T45R: công thức hiệu lực + giá vốn / suất.</summary>
        public Task<IResult<RecipeVersionModel>> GetActiveRecipe(Guid id);
    }
}
