using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IMenuLineService
    {
        public Task<IResults<MenuLineViewModel>> GetAll(MenuLineSearchRequest request);
        public Task<IResult<bool>> Save(MenuLineRequest request, string typeSave);
        public IResults<MenuLine> GetAllData();
        public Task<IResult<MenuLine>> GetById(Guid id);
    }
}
