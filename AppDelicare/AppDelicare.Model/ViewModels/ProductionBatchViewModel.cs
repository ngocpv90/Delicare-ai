using System;

namespace AppDelicare.Model.ViewModels
{
	public class ProductionBatchViewModel : BaseViewModel
	{
		public Guid ProductionOrderId { get; set; }
		public string? ProductionOrderName { get; set; }
		public string? BatchNumber { get; set; }
		public DateTime StartDate { get; set; }
		public DateTime? EndDate { get; set; }
		public decimal? PlannedQuantity { get; set; }
		public decimal? ActualQuantity { get; set; }
		public string? QualityStatus { get; set; }
		public string? Status { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public Guid? DishId { get; set; }
		public string? DishName { get; set; }
		public Guid? RecipeId { get; set; }
		public decimal? AcceptedQty { get; set; }
		public decimal? RejectedQty { get; set; }
		public decimal? WasteQty { get; set; }
		public string? QcStatus { get; set; }
		public Guid? KitchenWorkstationId { get; set; }
		public string? KitchenWorkstationName { get; set; }
		public DateTime? CreatedDate { get; set; }
	}
}
