using System;

namespace AppDelicare.Model.ViewModels
{
	public class ProductionPlanViewModel : BaseViewModel
	{
		public string? PlanNumber { get; set; }
		public DateTime PlanDate { get; set; }
		public Guid? OrganizationUnitId { get; set; }
		public string? OrganizationUnitName { get; set; }
		public string? Status { get; set; }
		public string? Note { get; set; }
	}
}
