using System;

namespace AppDelicare.Model.ViewModels
{
	public class ProductionOutputViewModel : BaseViewModel
	{
		public Guid ProductionBatchId { get; set; }
		public string? ProductionBatchName { get; set; }
		public Guid DishId { get; set; }
		public string? DishName { get; set; }
		public decimal ProducedQuantity { get; set; }
		public decimal AcceptedQuantity { get; set; }
		public decimal RejectedQuantity { get; set; }
		public Guid? UnitId { get; set; }
		public string? UnitName { get; set; }
		public string? QualityStatus { get; set; }
		public string? Note { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public decimal? WasteQty { get; set; }
		public string? Remark { get; set; }
		public DateTime? CreatedDate { get; set; }
	}
}
