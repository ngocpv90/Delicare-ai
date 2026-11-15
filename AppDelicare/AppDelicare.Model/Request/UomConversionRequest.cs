using System;

namespace AppDelicare.Model.Request
{
	public class UomConversionRequest : BaseRequest
	{
		public Guid FromUnitId { get; set; }
		public Guid ToUnitId { get; set; }
		public decimal ConversionFactor { get; set; }
		public DateTime? EffectiveFrom { get; set; }
		public DateTime? EffectiveTo { get; set; }
		public bool IsActive { get; set; }
		public string? Note { get; set; }
	}
}
