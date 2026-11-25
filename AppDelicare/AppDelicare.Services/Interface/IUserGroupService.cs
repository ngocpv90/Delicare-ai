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
        // UI-6.43 (D-124)
        public Task<IResult<AppDelicare.Model.Model.UserGroupListModel>> GetList(string? keyword, bool emptyOnly);
        public Task<IResult<AppDelicare.Model.Model.AccountBulkResultModel>> AddUsers(AppDelicare.Model.Model.UserGroupUsersRequest request);
        public Task<IResult<AppDelicare.Model.Model.AccountBulkResultModel>> RemoveUsers(AppDelicare.Model.Model.UserGroupUsersRequest request);
        public Task<IResult<AppDelicare.Model.Model.UserGroupDetailModel>> GetDetail(Guid id);
        public Task<IResult<bool>> Retire(AppDelicare.Model.Model.UserGroupStatusRequest request);
    }
}
