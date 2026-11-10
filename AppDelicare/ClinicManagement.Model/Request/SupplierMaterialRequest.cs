using System;

namespace AppDelicare.Model.Request
{
	public class SupplierMaterialRequest : BaseRequest
	{
		public Guid SupplierId { get; set; }
		public Guid IngredientId { get; set; }
		public string? SupplierMaterialCode { get; set; }
		public decimal? UnitPrice { get; set; }
		public decimal? MinimumOrderQuantity { get; set; }
		public int? LeadTimeDays { get; set; }
		public string? QualityRequirement { get; set; }
		public DateTime? EffectiveFrom { get; set; }
		public DateTime? EffectiveTo { get; set; }
		public bool IsPreferred { get; set; }
		public string? Status { get; set; }
	}
}
