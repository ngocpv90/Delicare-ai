namespace AppDelicare.Model.ViewModels.Custom
{
    /// <summary>
    /// T45R.8 (D-38, D-39): kết quả <c>Person/Insert</c> (và <c>User/Insert</c>) – tài khoản vừa tạo.
    /// <see cref="TempPassword"/> là mật khẩu tạm (hạn 24 giờ) chỉ trả MỘT LẦN cho admin, không lưu bản rõ, không log.
    /// </summary>
    public class PersonUserCreatedViewModel
    {
        public Guid? PersonId { get; set; }
        public Guid UserId { get; set; }
        public string? UserName { get; set; }
        public string? TempPassword { get; set; }
    }
}
