using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;

namespace AppDelicare.Services.Interface
{
    public interface IObjectMenuService
    {
        public IResults<ObjectMenu> GetAllData();
        public Task<IResults<ObjectMenuViewModel>> GetAll(ObjectMenuSearchRequest request);
        public Task<IResult<bool>> Save(ObjectMenuRequest request, string typeSave);
        public Task<IResult<ObjectMenu>> GetById(Guid id);
        public Task<IResult<bool>> SaveIsActive(Guid id);
    }
}
