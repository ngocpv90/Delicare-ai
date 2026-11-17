using System;

namespace AppDelicare.Model.SearchRequest
{
	public class StockCountLineSearchRequest : BaseSearchRequest
	{
		public Guid? StockCountId { get; set; }
		public Guid? IngredientId { get; set; }
		public Guid? MaterialLotId { get; set; }
		public Guid? UnitId { get; set; }
	}
}
