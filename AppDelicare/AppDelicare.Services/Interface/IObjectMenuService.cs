using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;
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
        /// <summary>T45R: cây menu theo ParentId / LevelShow / OrderShow.</summary>
        public Task<IResult<List<ObjectMenuTreeModel>>> GetTree(bool includeInactive);
        /// <summary>T45R: đổi cha / thứ tự nhiều menu (LevelShow = 1 nếu không có cha, 2 nếu có); chặn vòng lặp.</summary>
        public Task<IResult<bool>> UpdateOrder(List<ObjectMenuOrderRequest> request);
    }
}
