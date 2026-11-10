using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IIngredientService
    {
        public Task<IResults<IngredientViewModel>> GetAll(IngredientSearchRequest request);
        public Task<IResult<bool>> Save(IngredientRequest request, string typeSave);
        public IResults<Ingredient> GetAllData();
        public Task<IResult<Ingredient>> GetById(Guid id);
    }
}
