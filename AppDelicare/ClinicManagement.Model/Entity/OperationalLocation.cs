namespace AppDelicare.Model.Entity
{
    public class OperationalLocation : BaseEntity
    {
        public Guid? CompanyId { get; set; }
        public Guid? OrganizationUnitId { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? LocationType { get; set; }
        public string? Address { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string? ContactName { get; set; }
        public string? ContactPhone { get; set; }
        public string? Status { get; set; }
        public string? Note { get; set; }
    }
}
