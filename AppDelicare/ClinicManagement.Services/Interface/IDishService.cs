using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IDishService
    {
        public Task<IResults<DishViewModel>> GetAll(DishSearchRequest request);
        public Task<IResult<bool>> Save(DishRequest request, string typeSave);
        public IResults<Dish> GetAllData();
        public Task<IResult<Dish>> GetById(Guid id);
    }
}
