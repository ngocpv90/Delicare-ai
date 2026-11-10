using System;

namespace AppDelicare.Model.Request
{
	public class DishCategoryRequest : BaseRequest
	{
		public Guid? ParentId { get; set; }
		public string? Code { get; set; }
		public string? Name { get; set; }
		public string? Description { get; set; }
		public string? Status { get; set; }
	}
}
