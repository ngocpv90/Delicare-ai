using System.ComponentModel.DataAnnotations;
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
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		[MaxLength(100, ErrorMessage = "MealCategory tối đa 100 ký tự.")]
		public string? MealCategory { get; set; }
		public decimal? StandardPortion { get; set; }
		public decimal? StandardCost { get; set; }
	}
}
