using AppDelicare.Model.Entity;

namespace AppDelicare.Model.Model
{
    public class UserModel : User
    {
        public string? FullName { get; set; }
        public string? ListUserGroupId { get; set; }
    }
}
