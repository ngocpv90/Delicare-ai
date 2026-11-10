namespace AppDelicare.Model.Request
{
    public class UserGroupRequest : BaseRequest
    {
        public string? Name { get; set; }
        public bool IsDefault { get; set; }
    }
}
