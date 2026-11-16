using System;

namespace AppDelicare.Model.ViewModels
{
	public class MenuLineViewModel : BaseViewModel
	{
		public Guid MenuId { get; set; }
		public string? MenuName { get; set; }
		public Guid DishId { get; set; }
		public string? DishName { get; set; }
		public string? MealType { get; set; }
		public int? DayOfWeek { get; set; }
		public int SortOrder { get; set; }
		public decimal? StandardQuantity { get; set; }
		public string? Note { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public int? SequenceNo { get; set; }
		public decimal? PortionQty { get; set; }
		public bool? IsRequired { get; set; }
		public DateTime? CreatedDate { get; set; }
	}
}
