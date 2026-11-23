using System;

namespace AppDelicare.Model.ViewModels
{
	public class MaterialIssueViewModel : BaseViewModel, IIngredientQuantityRow
	{
		/// <summary>MENU-4.7: tổng số lượng của nguyên liệu đang lọc trên chứng từ (chỉ có khi lọc IngredientId).</summary>
		public decimal? IngredientQuantity { get; set; }
		public string? IngredientUnitName { get; set; }
		public Guid ProductionOrderId { get; set; }
		public string? ProductionOrderName { get; set; }
		public Guid WarehouseId { get; set; }
		public string? WarehouseName { get; set; }
		public string? IssueNumber { get; set; }
		public DateTime IssueDate { get; set; }
		public string? Status { get; set; }
		public string? Note { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public DateTime? CreatedDate { get; set; }
	}
}
