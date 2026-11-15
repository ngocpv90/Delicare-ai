using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request;

namespace AppDelicare.Services.Interface
{
    public interface IUserRoleService
    {
        public IResults<UserRole> GetAllData();
        public Task<IResult<UserRoleModel>> GetByUserGroupId(string listUserGroupId, bool isUseManage);
        public Task<IResult<bool>> DeleteByUserGroupId(Guid userGroupId);
        public Task<IResult<bool>> Insert(Guid userGroupId);
        public Task<IResult<bool>> Update(UserRoleRequest request);
    }
}
