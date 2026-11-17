using AppDelicare.Model.ViewModels;

namespace AppDelicare.Model.Response
{
    public sealed class AuthenticateResponse : AuthenticateViewModel
    {
        public string? UserId { get; set; }
        public string? Token { get; set; }
        public int ExpireTime { get; set; }
        /// <summary>T45R.8 (D-39): đăng nhập bằng mật khẩu tạm → phải đổi mật khẩu trước khi dùng chức năng khác (trường thêm, không đổi trường cũ).</summary>
        public bool MustChangePassword { get; set; }
    }
}
