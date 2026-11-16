using System;

namespace AppDelicare.Model.Request
{
	public class ForecastRequest : BaseRequest
	{
		public Guid CustomerId { get; set; }
		public Guid? DeliveryLocationId { get; set; }
		public DateTime ForecastDate { get; set; }
		public string? MealType { get; set; }
		public decimal ForecastQuantity { get; set; }
		public string? Status { get; set; }
		public string? Note { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public Guid? ShiftId { get; set; }
		public int? VersionNo { get; set; }
	}
}
