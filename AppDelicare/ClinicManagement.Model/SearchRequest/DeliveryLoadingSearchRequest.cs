using System;

namespace AppDelicare.Model.SearchRequest
{
	public class DeliveryLoadingSearchRequest : BaseSearchRequest
	{
		public Guid DeliveryOrderId { get; set; }
		public Guid? EmployeeId { get; set; }
		public DateTime? LoadingDate { get; set; }
		public string? Status { get; set; }
	}
}
