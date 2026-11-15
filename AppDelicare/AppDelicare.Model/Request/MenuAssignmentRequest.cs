using System;

namespace AppDelicare.Model.Request
{
	public class MenuAssignmentRequest : BaseRequest
	{
		public Guid CustomerId { get; set; }
		public Guid? ContractId { get; set; }
		public Guid MenuId { get; set; }
		public Guid? DeliveryLocationId { get; set; }
		public DateTime EffectiveFrom { get; set; }
		public DateTime? EffectiveTo { get; set; }
		public string? Status { get; set; }
	}
}
