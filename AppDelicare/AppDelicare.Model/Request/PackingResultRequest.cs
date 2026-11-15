using System;

namespace AppDelicare.Model.Request
{
	public class PackingResultRequest : BaseRequest
	{
		public Guid PackingOrderId { get; set; }
		public Guid? ProductionOutputId { get; set; }
		public decimal PackedQuantity { get; set; }
		public decimal RejectedQuantity { get; set; }
		public string? PackagingType { get; set; }
		public string? QualityStatus { get; set; }
		public string? Note { get; set; }
	}
}
