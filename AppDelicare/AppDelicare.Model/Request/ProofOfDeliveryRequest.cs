using System;

namespace AppDelicare.Model.Request
{
	public class ProofOfDeliveryRequest : BaseRequest
	{
		public Guid DeliveryOrderId { get; set; }
		public DateTime ConfirmedAt { get; set; }
		public string? ReceiverName { get; set; }
		public string? ReceiverPhone { get; set; }
		public decimal? DeliveredQuantity { get; set; }
		public decimal? AcceptedQuantity { get; set; }
		public decimal? RejectedQuantity { get; set; }
		public string? SignatureUrl { get; set; }
		public string? PhotoUrl { get; set; }
		public string? Remark { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public DateTime? ReceivedAt { get; set; }
	}
}
