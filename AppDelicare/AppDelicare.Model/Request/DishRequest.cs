using System;

namespace AppDelicare.Model.Request
{
	public class DishRequest : BaseRequest
	{
		public Guid? DishCategoryId { get; set; }
		public Guid? DefaultUnitId { get; set; }
		public string? Code { get; set; }
		public string? Name { get; set; }
		public string? Description { get; set; }
		public decimal? StandardServingSize { get; set; }
		public string? Status { get; set; }
		public string? Note { get; set; }
	}
}
