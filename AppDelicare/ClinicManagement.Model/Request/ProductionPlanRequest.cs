using System;

namespace AppDelicare.Model.Request
{
	public class ProductionPlanRequest : BaseRequest
	{
		public string? PlanNumber { get; set; }
		public DateTime PlanDate { get; set; }
		public Guid? OrganizationUnitId { get; set; }
		public string? Status { get; set; }
		public string? Note { get; set; }
	}
}
