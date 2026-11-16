using System;

namespace AppDelicare.Model.SearchRequest
{
	public class MaterialReservationSearchRequest : BaseSearchRequest
	{
		public Guid ProductionOrderId { get; set; }
		public Guid IngredientId { get; set; }
		public Guid UnitId { get; set; }
		public string? Status { get; set; }
	}
}
