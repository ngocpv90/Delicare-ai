using System;

namespace AppDelicare.Model.Request
{
	public class MaterialIssueLineRequest : BaseRequest
	{
		public Guid MaterialIssueId { get; set; }
		public Guid IngredientId { get; set; }
		public Guid? MaterialLotId { get; set; }
		public decimal Quantity { get; set; }
		public Guid UnitId { get; set; }
		public string? Note { get; set; }
	}
}
