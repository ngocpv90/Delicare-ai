using System;

namespace AppDelicare.Model.ViewModels
{
	public class UnitOfMeasureViewModel : BaseViewModel
	{
		public string? Code { get; set; }
		public string? Name { get; set; }
		public string? Symbol { get; set; }
		public string? UnitType { get; set; }
		public string? Status { get; set; }
	}
}
