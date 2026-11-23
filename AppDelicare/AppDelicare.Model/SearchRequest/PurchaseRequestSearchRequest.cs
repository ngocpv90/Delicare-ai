using System;

namespace AppDelicare.Model.SearchRequest
{
	public class PurchaseRequestSearchRequest : BaseSearchRequest
	{
		/// <summary>MENU-4.7: chỉ chứng từ có dòng chứa nguyên liệu này (kèm số lượng).</summary>
		public Guid? IngredientId { get; set; }
		public string? RequestNumber { get; set; }
		public Guid? RequestedByEmployeeId { get; set; }
		public DateTime? RequestDate { get; set; }
		public DateTime? RequiredDate { get; set; }
		public string? Status { get; set; }
	}
}
