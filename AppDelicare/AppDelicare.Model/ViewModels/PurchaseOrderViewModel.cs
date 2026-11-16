using System;

namespace AppDelicare.Model.ViewModels
{
	public class PurchaseOrderViewModel : BaseViewModel
	{
		public Guid SupplierId { get; set; }
		public string? SupplierName { get; set; }
		public Guid? PurchaseRequestId { get; set; }
		public string? PurchaseRequestName { get; set; }
		public string? PurchaseOrderNumber { get; set; }
		public DateTime OrderDate { get; set; }
		public DateTime? ExpectedDeliveryDate { get; set; }
		public string? Status { get; set; }
		public decimal? TotalAmount { get; set; }
		public string? Note { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public DateTime? CreatedDate { get; set; }
	}
}
