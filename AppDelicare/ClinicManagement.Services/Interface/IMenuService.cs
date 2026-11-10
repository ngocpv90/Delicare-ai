using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IMenuService
    {
        public Task<IResults<MenuViewModel>> GetAll(MenuSearchRequest request);
        public Task<IResult<bool>> Save(MenuRequest request, string typeSave);
        public IResults<Menu> GetAllData();
        public Task<IResult<Menu>> GetById(Guid id);
    }
}
