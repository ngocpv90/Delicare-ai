using System;

namespace AppDelicare.Model.ViewModels
{
	public class ReturnInspectionViewModel : BaseViewModel
	{
		public Guid ContainerReturnId { get; set; }
		public string? ContainerReturnName { get; set; }
		public DateTime InspectionDate { get; set; }
		public Guid? InspectorEmployeeId { get; set; }
		public string? InspectorEmployeeName { get; set; }
		public string? Result { get; set; }
		public string? Note { get; set; }
	}
}
