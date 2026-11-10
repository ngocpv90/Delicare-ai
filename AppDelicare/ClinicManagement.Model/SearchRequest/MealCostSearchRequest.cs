using System;

namespace AppDelicare.Model.SearchRequest
{
	public class MealCostSearchRequest : BaseSearchRequest
	{
		public Guid? OrderId { get; set; }
		public Guid? OrderLineId { get; set; }
		public Guid? DishId { get; set; }
		public decimal TotalActualCost { get; set; }
		public DateTime CalculatedAt { get; set; }
	}
}
