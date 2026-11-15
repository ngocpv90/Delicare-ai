using AppDelicare.Model.ViewModels;

namespace AppDelicare.Model.Model
{
    /// <summary>T45R: thông tin người đang đăng nhập (spec 2.2 /meta/me): tài khoản + nhóm + menu theo quyền hiệu lực.</summary>
    public class CurrentUserModel
    {
        public UserModel? User { get; set; }
        /// <summary>Menu 2 cấp user được truy cập, kèm cờ quyền (ẩn / hiện nút).</summary>
        public List<UserRoleViewModel> ListMenu { get; set; } = [];
    }
}
