using System;

namespace AppDelicare.Model.SearchRequest
{
	public class ProductionPlanLineSearchRequest : BaseSearchRequest
	{
		public Guid ProductionPlanId { get; set; }
		public Guid? OrderLineId { get; set; }
		public Guid DishId { get; set; }
		public Guid? ShiftId { get; set; }
		public string? Status { get; set; }
	}
}
