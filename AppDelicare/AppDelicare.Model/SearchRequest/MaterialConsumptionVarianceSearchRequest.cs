using System;

namespace AppDelicare.Model.SearchRequest
{
	public class MaterialConsumptionVarianceSearchRequest : BaseSearchRequest
	{
		public Guid? ProductionOrderId { get; set; }
		public Guid? IngredientId { get; set; }
	}
}
