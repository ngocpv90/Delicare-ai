using System;

namespace AppDelicare.Model.SearchRequest
{
    public class OperationalLocationSearchRequest : BaseSearchRequest
    {
        public Guid? CompanyId { get; set; }
        public Guid? OrganizationUnitId { get; set; }
        public string? LocationType { get; set; }
        public string? Status { get; set; }
    }
}
