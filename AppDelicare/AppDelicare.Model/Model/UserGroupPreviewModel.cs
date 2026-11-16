using AppDelicare.Model.ViewModels;

namespace AppDelicare.Model.Model
{
    /// <summary>T45R: xem trước quyền của nhóm (spec 3.3).</summary>
    public class UserGroupPreviewModel
    {
        public Guid UserGroupId { get; set; }
        public string? UserGroupName { get; set; }
        /// <summary>Số màn hình (menu cấp 2) nhóm được truy cập.</summary>
        public int ScreenCount { get; set; }
        /// <summary>Menu nhóm được truy cập (cây 2 cấp, kèm cờ).</summary>
        public List<UserRoleViewModel> ListMenu { get; set; } = [];
        /// <summary>Thành viên của nhóm (Id, UserName).</summary>
        public List<UserGroupMemberModel> ListUser { get; set; } = [];
    }

    public class UserGroupMemberModel
    {
        public Guid Id { get; set; }
        public string? UserName { get; set; }
    }
}
