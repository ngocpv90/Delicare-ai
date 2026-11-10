using System;

namespace AppDelicare.Model.ViewModels
{
	public class GoodsReceiptLineViewModel : BaseViewModel
	{
		public Guid GoodsReceiptId { get; set; }
		public string? GoodsReceiptName { get; set; }
		public Guid IngredientId { get; set; }
		public string? IngredientName { get; set; }
		public decimal ReceivedQuantity { get; set; }
		public decimal AcceptedQuantity { get; set; }
		public decimal RejectedQuantity { get; set; }
		public Guid UnitId { get; set; }
		public string? UnitName { get; set; }
		public string? LotNumber { get; set; }
		public DateTime? ManufactureDate { get; set; }
		public DateTime? ExpiryDate { get; set; }
		public string? QualityStatus { get; set; }
	}
}
