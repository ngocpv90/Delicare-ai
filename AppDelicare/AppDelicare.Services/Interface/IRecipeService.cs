using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IRecipeService
    {
        public Task<IResults<RecipeViewModel>> GetAll(RecipeSearchRequest request);
        public Task<IResult<bool>> Save(RecipeRequest request, string typeSave);
        public IResults<Recipe> GetAllData();
        public Task<IResult<Recipe>> GetById(Guid id);
    }
}
