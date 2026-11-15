using System;

namespace AppDelicare.Model.ViewModels
{
	public class ProductionOrderViewModel : BaseViewModel
	{
		public Guid ProductionPlanId { get; set; }
		public string? ProductionPlanName { get; set; }
		public string? ProductionOrderNumber { get; set; }
		public DateTime PlannedStartDate { get; set; }
		public DateTime? ActualStartDate { get; set; }
		public DateTime? ActualEndDate { get; set; }
		public Guid? ShiftId { get; set; }
		public string? ShiftName { get; set; }
		public Guid? ResponsibleEmployeeId { get; set; }
		public string? ResponsibleEmployeeName { get; set; }
		public string? Status { get; set; }
		public string? Note { get; set; }
	}
}
