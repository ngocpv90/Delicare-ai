using Microsoft.AspNetCore.Http;

namespace AppDelicare.Model.Request.Custom
{
    public class PersonCustomRequest : PersonRequest
    {
        public IFormFile? File { get; set; }

        // T45R.8 (D-38): tạo người dùng 1 bước – Person/Insert tạo luôn đúng 1 tài khoản. Chỉ là trường request, KHÔNG có cột Person tương ứng.
        /// <summary>Tên đăng nhập (tùy chọn). Trống → tự sinh từ họ tên như cũ. Không dấu, không khoảng trắng, không trùng (không phân biệt hoa thường).</summary>
        public string? UserName { get; set; }
        /// <summary>Nhóm quyền của tài khoản mới, Id cách nhau bằng dấu phẩy (như <c>UserRequest.ListUserGroupId</c>).</summary>
        public string? ListUserGroupId { get; set; }
        /// <summary>Tài khoản đang dùng. Không gửi → false (giữ hành vi cũ: tài khoản tạo kèm hồ sơ chưa kích hoạt).</summary>
        public bool? IsActive { get; set; }
        /// <summary>HR-ACC-1.5 (D-82 e): tạo tài khoản từ màn Tài khoản cho 1 nhân viên → gắn Employee.PersonId = Person vừa tạo (nhân viên chưa gắn người khác).</summary>
        public Guid? EmployeeId { get; set; }
    }
}
