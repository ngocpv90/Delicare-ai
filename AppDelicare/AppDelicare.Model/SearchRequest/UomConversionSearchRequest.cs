using System;

namespace AppDelicare.Model.SearchRequest
{
	public class UomConversionSearchRequest : BaseSearchRequest
	{
		public Guid FromUnitId { get; set; }
		public Guid ToUnitId { get; set; }
		public DateTime? EffectiveFrom { get; set; }
		public DateTime? EffectiveTo { get; set; }
	}
}
