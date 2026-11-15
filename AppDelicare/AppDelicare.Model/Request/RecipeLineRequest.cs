using System;

namespace AppDelicare.Model.Request
{
	public class RecipeLineRequest : BaseRequest
	{
		public Guid RecipeId { get; set; }
		public Guid IngredientId { get; set; }
		public decimal Quantity { get; set; }
		public Guid UnitId { get; set; }
		public decimal? ExpectedWasteRate { get; set; }
		public string? PreparationNote { get; set; }
		public int SortOrder { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public decimal? WastePercent { get; set; }
		public decimal? YieldPercent { get; set; }
		public int? SequenceNo { get; set; }
		public string? Note { get; set; }
	}
}
