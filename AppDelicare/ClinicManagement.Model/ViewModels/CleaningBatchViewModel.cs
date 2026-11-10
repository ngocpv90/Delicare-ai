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
	}
}
