using System;

namespace AppDelicare.Model.ViewModels
{
	public class MaterialIssueLineViewModel : BaseViewModel
	{
		public Guid MaterialIssueId { get; set; }
		public string? MaterialIssueName { get; set; }
		public Guid IngredientId { get; set; }
		public string? IngredientName { get; set; }
		public Guid? MaterialLotId { get; set; }
		public string? MaterialLotName { get; set; }
		public decimal Quantity { get; set; }
		public Guid UnitId { get; set; }
		public string? UnitName { get; set; }
		public string? Note { get; set; }
	}
}
