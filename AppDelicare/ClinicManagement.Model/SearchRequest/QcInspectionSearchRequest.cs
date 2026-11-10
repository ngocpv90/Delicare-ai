using System;

namespace AppDelicare.Model.SearchRequest
{
	public class QcInspectionSearchRequest : BaseSearchRequest
	{
		public Guid? GoodsReceiptId { get; set; }
		public string? InspectionNumber { get; set; }
		public DateTime InspectionDate { get; set; }
		public Guid? InspectorEmployeeId { get; set; }
		public string? Result { get; set; }
	}
}
