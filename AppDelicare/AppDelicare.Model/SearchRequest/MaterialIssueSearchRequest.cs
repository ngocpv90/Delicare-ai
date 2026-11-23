using System;

namespace AppDelicare.Model.SearchRequest
{
	public class MaterialIssueSearchRequest : BaseSearchRequest
	{
		/// <summary>MENU-4.7: chỉ chứng từ có dòng chứa nguyên liệu này (kèm số lượng).</summary>
		public Guid? IngredientId { get; set; }
		public Guid? ProductionOrderId { get; set; }
		public Guid? WarehouseId { get; set; }
		public DateTime? IssueDate { get; set; }
		public string? Status { get; set; }
	}
}
