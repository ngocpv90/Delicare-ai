using System;

namespace AppDelicare.Model.ViewModels
{
	public class MaterialLotViewModel : BaseViewModel
	{
		public Guid IngredientId { get; set; }
		public string? IngredientName { get; set; }
		public Guid? SupplierId { get; set; }
		public string? SupplierName { get; set; }
		public Guid? GoodsReceiptLineId { get; set; }
		public string? GoodsReceiptLineName { get; set; }
		public string? LotNumber { get; set; }
		public DateTime? ManufactureDate { get; set; }
		public DateTime? ExpiryDate { get; set; }
		public DateTime? ReceivedDate { get; set; }
		public string? QualityStatus { get; set; }
		public string? Status { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public decimal? UnitCost { get; set; }
		public string? QcStatus { get; set; }
		public string? Note { get; set; }
		public DateTime? CreatedDate { get; set; }
	}
}
