using System;

namespace AppDelicare.Model.SearchRequest
{
    public class VehicleSearchRequest : BaseSearchRequest
    {
        public Guid? CompanyId { get; set; }
        public Guid? OrganizationUnitId { get; set; }
        public string? VehicleType { get; set; }
        public string? Status { get; set; }
    }
}
