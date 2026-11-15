using System;

namespace AppDelicare.Model.Request
{
	public class QcInspectionRequest : BaseRequest
	{
		public Guid? GoodsReceiptId { get; set; }
		public string? InspectionNumber { get; set; }
		public DateTime InspectionDate { get; set; }
		public Guid? InspectorEmployeeId { get; set; }
		public string? Result { get; set; }
		public string? Note { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public string? Remark { get; set; }
	}
}
