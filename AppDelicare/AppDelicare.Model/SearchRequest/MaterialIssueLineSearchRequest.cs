using System;

namespace AppDelicare.Model.SearchRequest
{
	public class MaterialIssueLineSearchRequest : BaseSearchRequest
	{
		public Guid? MaterialIssueId { get; set; }
		public Guid? IngredientId { get; set; }
		public Guid? MaterialLotId { get; set; }
		public Guid? UnitId { get; set; }
	}
}
