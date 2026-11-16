using System;

namespace AppDelicare.Model.Request
{
	public class OrderLineRequest : BaseRequest
	{
		public Guid OrderId { get; set; }
		public Guid? ContractMealId { get; set; }
		public Guid? MenuId { get; set; }
		public string? MealType { get; set; }
		public decimal Quantity { get; set; }
		public decimal UnitPrice { get; set; }
		public decimal LineAmount { get; set; }
		public string? Note { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public Guid? ShiftId { get; set; }
	}
}
