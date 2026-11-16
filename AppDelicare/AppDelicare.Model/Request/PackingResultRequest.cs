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
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public decimal? PlannedQty { get; set; }
		public decimal? DamagedQty { get; set; }
		public decimal? AcceptedQty { get; set; }
		public string? Remark { get; set; }
	}
}
