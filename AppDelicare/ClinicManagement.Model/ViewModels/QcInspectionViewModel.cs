using System;

namespace AppDelicare.Model.ViewModels
{
	public class QcInspectionViewModel : BaseViewModel
	{
		public Guid? GoodsReceiptId { get; set; }
		public string? GoodsReceiptName { get; set; }
		public string? InspectionNumber { get; set; }
		public DateTime InspectionDate { get; set; }
		public Guid? InspectorEmployeeId { get; set; }
		public string? InspectorEmployeeName { get; set; }
		public string? Result { get; set; }
		public string? Note { get; set; }
	}
}
