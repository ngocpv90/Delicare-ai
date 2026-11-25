namespace AppDelicare.Model.Request
{
    public class UserGroupRequest : BaseRequest
    {
        public string? Name { get; set; }
        public bool IsDefault { get; set; }
        /// <summary>UI-6.43 (D-124): mã nhóm G2 (trống khi sửa = giữ mã cũ), mô tả, khối (ReferenceLists.UserGroupBlock).</summary>
        public string? Code { get; set; }
        public string? Description { get; set; }
        public string? GroupBlock { get; set; }
    }
}
