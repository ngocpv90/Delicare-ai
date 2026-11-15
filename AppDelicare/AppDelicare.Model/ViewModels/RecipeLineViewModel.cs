using System;

namespace AppDelicare.Model.ViewModels
{
	public class RecipeLineViewModel : BaseViewModel
	{
		public Guid RecipeId { get; set; }
		public string? RecipeName { get; set; }
		public Guid IngredientId { get; set; }
		public string? IngredientName { get; set; }
		public decimal Quantity { get; set; }
		public Guid UnitId { get; set; }
		public string? UnitName { get; set; }
		public decimal? ExpectedWasteRate { get; set; }
		public string? PreparationNote { get; set; }
		public int SortOrder { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public decimal? WastePercent { get; set; }
		public decimal? YieldPercent { get; set; }
		public int? SequenceNo { get; set; }
		public string? Note { get; set; }
		public DateTime? CreatedDate { get; set; }
	}
}
