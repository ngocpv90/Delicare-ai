using System;

namespace AppDelicare.Model.SearchRequest
{
	public class ContractMealSearchRequest : BaseSearchRequest
	{
		public Guid ContractId { get; set; }
		public string? MealType { get; set; }
		public Guid? UnitId { get; set; }
		public string? Status { get; set; }
	}
}
