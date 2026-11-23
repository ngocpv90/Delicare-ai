using System;

namespace AppDelicare.Model.ViewModels
{
	public class PurchaseRequestViewModel : BaseViewModel, IIngredientQuantityRow
	{
		/// <summary>MENU-4.7: tổng số lượng của nguyên liệu đang lọc trên chứng từ (chỉ có khi lọc IngredientId).</summary>
		public decimal? IngredientQuantity { get; set; }
		public string? IngredientUnitName { get; set; }
		public string? RequestNumber { get; set; }
		public Guid? RequestedByEmployeeId { get; set; }
		public string? RequestedByEmployeeName { get; set; }
		public DateTime RequestDate { get; set; }
		public DateTime? RequiredDate { get; set; }
		public string? Status { get; set; }
		public string? Note { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public DateTime? CreatedDate { get; set; }
	}
}
