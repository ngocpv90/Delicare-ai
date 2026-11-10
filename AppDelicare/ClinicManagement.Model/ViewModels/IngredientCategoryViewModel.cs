using System;

namespace AppDelicare.Model.ViewModels
{
	public class IngredientCategoryViewModel : BaseViewModel
	{
		public Guid? ParentId { get; set; }
		public string? ParentName { get; set; }
		public string? Code { get; set; }
		public string? Name { get; set; }
		public string? Description { get; set; }
		public string? Status { get; set; }
	}
}
