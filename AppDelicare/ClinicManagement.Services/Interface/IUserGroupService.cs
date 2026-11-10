using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;

namespace AppDelicare.Services.Interface
{
    public interface IUserGroupService
    {
        public IResults<UserGroup> GetAllData();
        public Task<IResults<UserGroupViewModel>> GetAll(UserGroupSearchRequest request);
        public Task<IResult<bool>> Save(UserGroupRequest request, string typeSave);
        public Task<IResult<UserGroup>> GetById(Guid id);
    }
}
