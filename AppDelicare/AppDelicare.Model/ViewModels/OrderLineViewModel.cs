using System;

namespace AppDelicare.Model.ViewModels
{
	public class OrderLineViewModel : BaseViewModel
	{
		public Guid OrderId { get; set; }
		public string? OrderName { get; set; }
		public Guid? ContractMealId { get; set; }
		public string? ContractMealName { get; set; }
		public Guid? MenuId { get; set; }
		public string? MenuName { get; set; }
		public string? MealType { get; set; }
		public decimal Quantity { get; set; }
		public decimal UnitPrice { get; set; }
		public decimal LineAmount { get; set; }
		public string? Note { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public Guid? ShiftId { get; set; }
		public string? ShiftName { get; set; }
		public DateTime? CreatedDate { get; set; }
	}
}
