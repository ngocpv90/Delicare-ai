using System;

namespace AppDelicare.Model.Request
{
	public class InventoryPolicyRequest : BaseRequest
	{
		public Guid IngredientId { get; set; }
		public Guid? WarehouseId { get; set; }
		public decimal? MinimumStock { get; set; }
		public decimal? MaximumStock { get; set; }
		public decimal? SafetyStock { get; set; }
		public bool UseFefo { get; set; }
		public int? ExpiryWarningDays { get; set; }
		public string? Status { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public decimal? MinStock { get; set; }
		public decimal? MaxStock { get; set; }
		public decimal? ReorderPoint { get; set; }
		public decimal? CriticalLevel { get; set; }
		public int? LeadTimeDays { get; set; }
	}
}
