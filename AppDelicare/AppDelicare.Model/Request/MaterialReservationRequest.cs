using System;

namespace AppDelicare.Model.Request
{
	public class MaterialReservationRequest : BaseRequest
	{
		public Guid ProductionOrderId { get; set; }
		public Guid IngredientId { get; set; }
		public decimal ReservedQuantity { get; set; }
		public Guid UnitId { get; set; }
		public string? Status { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public decimal? RequiredQty { get; set; }
	}
}
