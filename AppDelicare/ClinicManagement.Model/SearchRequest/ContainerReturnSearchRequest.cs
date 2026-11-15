using System;

namespace AppDelicare.Model.SearchRequest
{
	public class ContainerReturnSearchRequest : BaseSearchRequest
	{
		public Guid? DeliveryOrderId { get; set; }
		public Guid? CustomerId { get; set; }
		public DateTime? ReturnDate { get; set; }
		public string? Status { get; set; }
	}
}
