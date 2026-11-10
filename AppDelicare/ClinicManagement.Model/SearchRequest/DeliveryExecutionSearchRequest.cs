using System;

namespace AppDelicare.Model.SearchRequest
{
	public class DeliveryExecutionSearchRequest : BaseSearchRequest
	{
		public Guid DeliveryOrderId { get; set; }
		public DateTime? DepartedAt { get; set; }
		public DateTime? ArrivedAt { get; set; }
		public DateTime? DeliveredAt { get; set; }
		public string? Status { get; set; }
	}
}
