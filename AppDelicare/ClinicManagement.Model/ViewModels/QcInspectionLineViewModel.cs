using System;

namespace AppDelicare.Model.ViewModels
{
	public class QcInspectionLineViewModel : BaseViewModel
	{
		public Guid QcInspectionId { get; set; }
		public string? QcInspectionName { get; set; }
		public Guid? GoodsReceiptLineId { get; set; }
		public string? GoodsReceiptLineName { get; set; }
		public string? Criterion { get; set; }
		public string? Result { get; set; }
		public string? Note { get; set; }
	}
}
