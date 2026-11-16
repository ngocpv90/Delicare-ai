using System;

namespace AppDelicare.Model.SearchRequest
{
	public class ProductionPlanSearchRequest : BaseSearchRequest
	{
		public DateTime? PlanDate { get; set; }
		public Guid? OrganizationUnitId { get; set; }
		public string? Status { get; set; }
	}
}
