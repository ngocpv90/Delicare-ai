namespace AppDelicare.Model.SearchRequest
{
	public class MedicalExaminationSearchRequest : BaseSearchRequest
    {
		public decimal? PriceFrom { get; set; }
		public decimal? PriceTo { get; set; }
		public DateTime? CreatedDateFrom { get; set; }
		public DateTime? CreatedDateTo { get; set; }
	}
}
