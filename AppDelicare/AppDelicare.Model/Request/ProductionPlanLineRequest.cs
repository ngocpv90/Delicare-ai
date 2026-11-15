using System;

namespace AppDelicare.Model.Request
{
	public class ProductionPlanLineRequest : BaseRequest
	{
		public Guid ProductionPlanId { get; set; }
		public Guid? OrderLineId { get; set; }
		public Guid DishId { get; set; }
		public decimal PlannedQuantity { get; set; }
		public decimal? ActualQuantity { get; set; }
		public Guid? ShiftId { get; set; }
		public string? Status { get; set; }
	}
}
