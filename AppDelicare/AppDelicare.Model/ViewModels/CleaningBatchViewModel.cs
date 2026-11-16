using System;

namespace AppDelicare.Model.ViewModels
{
	public class CleaningBatchViewModel : BaseViewModel
	{
		public string? CleaningBatchNumber { get; set; }
		public DateTime StartDate { get; set; }
		public DateTime? EndDate { get; set; }
		public int ContainerQuantity { get; set; }
		public Guid? ResponsibleEmployeeId { get; set; }
		public string? ResponsibleEmployeeName { get; set; }
		public string? QualityStatus { get; set; }
		public string? Status { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public Guid? ReturnInspectionId { get; set; }
		public decimal? Quantity { get; set; }
		public string? QcResult { get; set; }
		public DateTime? CreatedDate { get; set; }
	}
}
