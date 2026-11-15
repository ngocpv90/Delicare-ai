using System;

namespace AppDelicare.Model.ViewModels
{
	public class PositionViewModel : BaseViewModel
	{
		public Guid? CompanyId { get; set; }
		public string? CompanyName { get; set; }
		public string? Code { get; set; }
		public string? Name { get; set; }
		public string? Description { get; set; }
		public string? Status { get; set; }
	}
}
