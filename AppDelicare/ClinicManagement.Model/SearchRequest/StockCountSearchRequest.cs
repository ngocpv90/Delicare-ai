using System;

namespace AppDelicare.Model.SearchRequest
{
	public class StockCountSearchRequest : BaseSearchRequest
	{
		public Guid WarehouseId { get; set; }
		public string? CountNumber { get; set; }
		public DateTime? CountDate { get; set; }
		public string? Status { get; set; }
		public Guid? ResponsibleEmployeeId { get; set; }
	}
}
