using System;

namespace AppDelicare.Model.Request
{
	public class ProductionOrderRequest : BaseRequest
	{
		public Guid ProductionPlanId { get; set; }
		public string? ProductionOrderNumber { get; set; }
		public DateTime PlannedStartDate { get; set; }
		public DateTime? ActualStartDate { get; set; }
		public DateTime? ActualEndDate { get; set; }
		public Guid? ShiftId { get; set; }
		public Guid? ResponsibleEmployeeId { get; set; }
		public string? Status { get; set; }
		public string? Note { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public Guid? DishId { get; set; }
		public Guid? RecipeId { get; set; }
		public decimal? PlannedQty { get; set; }
		public decimal? ActualQty { get; set; }
		public DateTime? StartTime { get; set; }
		public DateTime? EndTime { get; set; }
		public Guid? ProductionLineId { get; set; }
		public string? RecipeSnapshotJson { get; set; }
	}
}
