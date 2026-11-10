
namespace AppDelicare.Model.Entity
{
    public class Area : BaseEntity
    {
        public string? Code { get; set; }
        public string? CodeName { get; set; }
        public string? NameVN { get; set; }
        public string? NameEN { get; set; }
        public string? FullNameVN { get; set; }
        public string? FullNameEN { get; set; }
        public Guid? RegionId { get; set; }
        public bool IsDefault { get; set; }
    }
}
