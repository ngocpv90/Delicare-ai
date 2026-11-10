using System;

namespace AppDelicare.Model.ViewModels
{
	public class OrderReconciliationViewModel : BaseViewModel
	{
		public Guid OrderId { get; set; }
		public string? OrderName { get; set; }
		public Guid? ProofOfDeliveryId { get; set; }
		public string? ProofOfDeliveryName { get; set; }
		public DateTime ReconciliationDate { get; set; }
		public decimal OrderedQuantity { get; set; }
		public decimal DeliveredQuantity { get; set; }
		public decimal AcceptedQuantity { get; set; }
		public decimal? FinalAmount { get; set; }
		public string? Status { get; set; }
		public string? Note { get; set; }
	}
}
