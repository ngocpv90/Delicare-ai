using System;

namespace AppDelicare.Model.Request
{
	public class QcInspectionLineRequest : BaseRequest
	{
		public Guid QcInspectionId { get; set; }
		public Guid? GoodsReceiptLineId { get; set; }
		public string? Criterion { get; set; }
		public string? Result { get; set; }
		public string? Note { get; set; }
	}
}
