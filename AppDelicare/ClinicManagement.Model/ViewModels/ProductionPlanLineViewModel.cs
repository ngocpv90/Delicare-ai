using System;

namespace AppDelicare.Model.ViewModels
{
	public class ProductionPlanLineViewModel : BaseViewModel
	{
		public Guid ProductionPlanId { get; set; }
		public string? ProductionPlanName { get; set; }
		public Guid? OrderLineId { get; set; }
		public string? OrderLineName { get; set; }
		public Guid DishId { get; set; }
		public string? DishName { get; set; }
		public decimal PlannedQuantity { get; set; }
		public decimal? ActualQuantity { get; set; }
		public Guid? ShiftId { get; set; }
		public string? ShiftName { get; set; }
		public string? Status { get; set; }
	}
}
