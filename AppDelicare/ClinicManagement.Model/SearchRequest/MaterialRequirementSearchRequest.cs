using System;

namespace AppDelicare.Model.SearchRequest
{
	public class MaterialRequirementSearchRequest : BaseSearchRequest
	{
		public Guid? ProductionPlanId { get; set; }
		public Guid IngredientId { get; set; }
		public Guid UnitId { get; set; }
		public string? Status { get; set; }
	}
}
