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
        /// <summary>T45R: ma trận quyền 1 nhóm – mọi menu đang hoạt động (menu chưa có dòng UserRole: Id rỗng, mọi cờ false).</summary>
        public Task<IResult<UserRoleModel>> GetMatrix(Guid userGroupId);
        /// <summary>T45R: cấp / thu hồi truy cập toàn bộ menu cấp 2 của nhóm. Thu hồi → tắt mọi cờ.</summary>
        public Task<IResult<int>> SaveAccessAll(Guid userGroupId, bool allowAccess);
        /// <summary>T45R: sao chép toàn bộ cờ quyền từ nhóm nguồn sang nhóm đích (ghi đè dòng của nhóm đích).</summary>
        public Task<IResult<int>> CopyFromUserGroup(Guid sourceUserGroupId, Guid targetUserGroupId);
        /// <summary>T45R: xem trước menu nhóm nhận được + danh sách user của nhóm (chỉ admin).</summary>
        public Task<IResult<UserGroupPreviewModel>> GetPreview(Guid userGroupId);
        /// <summary>T45R: quyền hiệu lực của 1 user = OR cờ trên mọi nhóm; admin → mọi menu, mọi cờ.</summary>
        public Task<IResult<UserRoleModel>> GetByUserId(Guid userId);
    }
}
