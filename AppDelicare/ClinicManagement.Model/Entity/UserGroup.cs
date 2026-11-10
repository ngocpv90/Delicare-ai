namespace AppDelicare.Model.Entity
{
    public class UserGroup : BaseEntity
    {
        public string? Name { get; set; }
        public bool IsDefault { get; set; }
    }
}
