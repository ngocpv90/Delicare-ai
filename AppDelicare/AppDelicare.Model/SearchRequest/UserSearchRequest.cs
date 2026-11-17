namespace AppDelicare.Model.SearchRequest
{
    public class UserSearchRequest : BaseSearchRequest
    {
        public string? FullName { get; set; }
        public string? UserName { get; set; }
        public string? UserGroupId { get; set; }
        /// <summary>T45R.8 (FE-1): lọc theo trạng thái tài khoản (null = tất cả).</summary>
        public bool? IsActive { get; set; }
    }
}
