using System;

namespace AppDelicare.Model.SearchRequest
{
	public class OrderLineSearchRequest : BaseSearchRequest
	{
		public Guid? OrderId { get; set; }
		public Guid? ContractMealId { get; set; }
		public Guid? MenuId { get; set; }
		public string? MealType { get; set; }
	}
}
