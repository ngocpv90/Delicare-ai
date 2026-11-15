using System;

namespace AppDelicare.Model.Request
{
	public class ContractMealRequest : BaseRequest
	{
		public Guid ContractId { get; set; }
		public string? MealType { get; set; }
		public string? MealName { get; set; }
		public decimal? UnitPrice { get; set; }
		public Guid? UnitId { get; set; }
		public string? Status { get; set; }
	}
}
