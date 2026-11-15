using System;

namespace AppDelicare.Model.SearchRequest
{
	public class SupplierMaterialSearchRequest : BaseSearchRequest
	{
		public Guid SupplierId { get; set; }
		public Guid IngredientId { get; set; }
		public string? SupplierMaterialCode { get; set; }
		public DateTime? EffectiveFrom { get; set; }
		public DateTime? EffectiveTo { get; set; }
		public string? Status { get; set; }
	}
}
