using System;

namespace AppDelicare.Model.SearchRequest
{
	public class CleaningBatchSearchRequest : BaseSearchRequest
	{
		public string? CleaningBatchNumber { get; set; }
		public DateTime StartDate { get; set; }
		public DateTime? EndDate { get; set; }
		public Guid? ResponsibleEmployeeId { get; set; }
		public string? QualityStatus { get; set; }
		public string? Status { get; set; }
	}
}
