using System;

namespace AppDelicare.Model.Request
{
	public class StockCountRequest : BaseRequest
	{
		public Guid WarehouseId { get; set; }
		public string? CountNumber { get; set; }
		public DateTime CountDate { get; set; }
		public string? CountType { get; set; }
		public string? Status { get; set; }
		public Guid? ResponsibleEmployeeId { get; set; }
		public string? Note { get; set; }
	}
}
