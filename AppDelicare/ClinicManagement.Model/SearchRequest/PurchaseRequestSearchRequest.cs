using System;

namespace AppDelicare.Model.SearchRequest
{
	public class PurchaseRequestSearchRequest : BaseSearchRequest
	{
		public string? RequestNumber { get; set; }
		public Guid? RequestedByEmployeeId { get; set; }
		public DateTime RequestDate { get; set; }
		public DateTime? RequiredDate { get; set; }
		public string? Status { get; set; }
	}
}
