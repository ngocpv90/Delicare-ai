using System;

namespace AppDelicare.Model.ViewModels
{
	public class ContractMealViewModel : BaseViewModel
	{
		public Guid ContractId { get; set; }
		public string? ContractName { get; set; }
		public string? MealType { get; set; }
		public string? MealName { get; set; }
		public decimal? UnitPrice { get; set; }
		public Guid? UnitId { get; set; }
		public string? UnitName { get; set; }
		public string? Status { get; set; }
	}
}
