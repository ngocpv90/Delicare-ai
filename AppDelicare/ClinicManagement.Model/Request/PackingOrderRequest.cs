using System;

namespace AppDelicare.Model.Request
{
	public class PackingOrderRequest : BaseRequest
	{
		public Guid? ProductionBatchId { get; set; }
		public Guid? OrderId { get; set; }
		public string? PackingOrderNumber { get; set; }
		public DateTime PackingDate { get; set; }
		public string? Status { get; set; }
		public string? Note { get; set; }
	}
}
