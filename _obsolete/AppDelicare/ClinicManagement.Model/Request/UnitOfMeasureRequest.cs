using System;

namespace AppDelicare.Model.Request
{
	public class UnitOfMeasureRequest : BaseRequest
	{
		public string? Code { get; set; }
		public string? Name { get; set; }
		public string? Symbol { get; set; }
		public string? UnitType { get; set; }
		public string? Status { get; set; }
	}
}
