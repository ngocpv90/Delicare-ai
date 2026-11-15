using System;

namespace AppDelicare.Model.Request
{
	public class ContainerReturnRequest : BaseRequest
	{
		public Guid? DeliveryOrderId { get; set; }
		public Guid? CustomerId { get; set; }
		public DateTime ReturnDate { get; set; }
		public int ExpectedQuantity { get; set; }
		public int ReturnedQuantity { get; set; }
		public int DamagedQuantity { get; set; }
		public string? Status { get; set; }
		public string? Note { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public decimal? Quantity { get; set; }
	}
}
