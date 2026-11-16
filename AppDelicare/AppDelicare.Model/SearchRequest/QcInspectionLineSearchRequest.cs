using System;

namespace AppDelicare.Model.SearchRequest
{
	public class QcInspectionLineSearchRequest : BaseSearchRequest
	{
		public Guid? QcInspectionId { get; set; }
		public Guid? GoodsReceiptLineId { get; set; }
		public string? Result { get; set; }
	}
}
