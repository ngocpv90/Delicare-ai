using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IRecipeLineService
    {
        public Task<IResults<RecipeLineViewModel>> GetAll(RecipeLineSearchRequest request);
        public Task<IResult<bool>> Save(RecipeLineRequest request, string typeSave);
        public IResults<RecipeLine> GetAllData();
        public Task<IResult<RecipeLine>> GetById(Guid id);
    }
}
