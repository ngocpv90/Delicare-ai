using System;

namespace AppDelicare.Model.SearchRequest
{
	public class PackingResultSearchRequest : BaseSearchRequest
	{
		public Guid PackingOrderId { get; set; }
		public Guid? ProductionOutputId { get; set; }
		public string? QualityStatus { get; set; }
	}
}
