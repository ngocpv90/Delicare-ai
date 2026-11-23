using System;

namespace AppDelicare.Model.ViewModels
{
	public class GoodsReceiptViewModel : BaseViewModel, IIngredientQuantityRow
	{
		/// <summary>MENU-4.7: tổng số lượng của nguyên liệu đang lọc trên chứng từ (chỉ có khi lọc IngredientId).</summary>
		public decimal? IngredientQuantity { get; set; }
		public string? IngredientUnitName { get; set; }
		public Guid? PurchaseOrderId { get; set; }
		public string? PurchaseOrderName { get; set; }
		public Guid SupplierId { get; set; }
		public string? SupplierName { get; set; }
		public string? ReceiptNumber { get; set; }
		public DateTime ReceiptDate { get; set; }
		public Guid? WarehouseId { get; set; }
		public string? WarehouseName { get; set; }
		public string? Status { get; set; }
		public string? Note { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public DateTime? CreatedDate { get; set; }
	}
}
