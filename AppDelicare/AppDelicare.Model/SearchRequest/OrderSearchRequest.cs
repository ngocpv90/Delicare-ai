using System;

namespace AppDelicare.Model.SearchRequest
{
	public class OrderSearchRequest : BaseSearchRequest
	{
		public Guid? CustomerId { get; set; }
		public Guid? ContractId { get; set; }
		public Guid? DeliveryLocationId { get; set; }
		public string? OrderNumber { get; set; }
		public DateTime? OrderDate { get; set; }
		public DateTime? DeliveryDate { get; set; }
		public string? Status { get; set; }
		/// <summary>UI-2 (D-66 f3): lọc theo ngày ăn (MealDate) từ – đến, gồm cả 2 đầu.</summary>
		public DateTime? FromMealDate { get; set; }
		public DateTime? ToMealDate { get; set; }
		public decimal? TotalQuantity { get; set; }
		public decimal? TotalAmount { get; set; }
	}
}
