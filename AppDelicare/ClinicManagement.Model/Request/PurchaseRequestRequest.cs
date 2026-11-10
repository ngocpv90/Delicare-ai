using System;

namespace AppDelicare.Model.Request
{
	public class PurchaseRequestRequest : BaseRequest
	{
		public string? RequestNumber { get; set; }
		public Guid? RequestedByEmployeeId { get; set; }
		public DateTime RequestDate { get; set; }
		public DateTime? RequiredDate { get; set; }
		public string? Status { get; set; }
		public string? Note { get; set; }
	}
}
