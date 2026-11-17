using System;

namespace AppDelicare.Model.SearchRequest
{
	public class SupplierMaterialSearchRequest : BaseSearchRequest
	{
		public Guid? SupplierId { get; set; }
		public Guid? IngredientId { get; set; }
		public string? SupplierMaterialCode { get; set; }
		public DateTime? EffectiveFrom { get; set; }
		public DateTime? EffectiveTo { get; set; }
		public string? Status { get; set; }
		/// <summary>T45R (spec 4.3): chỉ lấy giá đang hiệu lực tại ngày này (EffectiveFrom ≤ ngày ≤ EffectiveTo).</summary>
		public DateTime? EffectiveOn { get; set; }
	}
}
