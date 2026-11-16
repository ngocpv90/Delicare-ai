using System;

namespace AppDelicare.Model.ViewModels
{
	public class OrderViewModel : BaseViewModel
	{
		public Guid CustomerId { get; set; }
		public string? CustomerName { get; set; }
		public Guid? ContractId { get; set; }
		public string? ContractName { get; set; }
		public Guid? DeliveryLocationId { get; set; }
		public string? DeliveryLocationName { get; set; }
		public string? OrderNumber { get; set; }
		public DateTime OrderDate { get; set; }
		public DateTime DeliveryDate { get; set; }
		public string? Status { get; set; }
		public decimal? TotalQuantity { get; set; }
		public decimal? TotalAmount { get; set; }
		public string? Note { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public DateTime? MealDate { get; set; }
		public DateTime? CreatedDate { get; set; }
	}
}
