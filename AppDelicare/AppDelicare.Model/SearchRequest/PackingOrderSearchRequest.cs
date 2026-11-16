using System;

namespace AppDelicare.Model.SearchRequest
{
	public class PackingOrderSearchRequest : BaseSearchRequest
	{
		public Guid? ProductionBatchId { get; set; }
		public Guid? OrderId { get; set; }
		public DateTime? PackingDate { get; set; }
		public string? Status { get; set; }
	}
}
