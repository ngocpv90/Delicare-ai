using System;

namespace AppDelicare.Model.ViewModels
{
	public class PurchaseRequestLineViewModel : BaseViewModel
	{
		public Guid PurchaseRequestId { get; set; }
		public string? PurchaseRequestName { get; set; }
		public Guid IngredientId { get; set; }
		public string? IngredientName { get; set; }
		public decimal Quantity { get; set; }
		public Guid UnitId { get; set; }
		public string? UnitName { get; set; }
		public DateTime? RequiredDate { get; set; }
		public string? Note { get; set; }
	}
}
