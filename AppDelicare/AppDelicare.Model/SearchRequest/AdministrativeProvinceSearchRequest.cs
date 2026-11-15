namespace AppDelicare.Model.SearchRequest
{
    public class AdministrativeProvinceSearchRequest : BaseSearchRequest
    {
        public Guid? AdministrativeUnitId { get; set; }
        public Guid? AdministrativeRegionId { get; set; }

    }
}
