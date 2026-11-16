using System;

namespace AppDelicare.Model.SearchRequest
{
	public class ContractSearchRequest : BaseSearchRequest
	{
		public Guid CustomerId { get; set; }
		public string? ContractNumber { get; set; }
		public string? Name { get; set; }
		public DateTime? SignedDate { get; set; }
		public DateTime? EffectiveFrom { get; set; }
		public DateTime? EffectiveTo { get; set; }
		public string? Status { get; set; }
	}
}
