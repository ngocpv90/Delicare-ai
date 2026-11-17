using System;

namespace AppDelicare.Model.SearchRequest
{
	public class ForecastSearchRequest : BaseSearchRequest
	{
		public Guid? CustomerId { get; set; }
		public Guid? DeliveryLocationId { get; set; }
		public DateTime? ForecastDate { get; set; }
		public string? MealType { get; set; }
		public string? Status { get; set; }
	}
}
