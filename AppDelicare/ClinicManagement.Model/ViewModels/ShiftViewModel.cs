using System;

namespace AppDelicare.Model.ViewModels
{
	public class ShiftViewModel : BaseViewModel
	{
		public Guid? CompanyId { get; set; }
		public string? CompanyName { get; set; }
		public string? Code { get; set; }
		public string? Name { get; set; }
		public TimeSpan? StartTime { get; set; }
		public TimeSpan? EndTime { get; set; }
		public string? Status { get; set; }
		public string? Note { get; set; }
	}
}
