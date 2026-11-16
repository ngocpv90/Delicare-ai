using System;

namespace AppDelicare.Model.SearchRequest
{
	public class StorageZoneSearchRequest : BaseSearchRequest
	{
		public Guid? WarehouseId { get; set; }
		public string? Code { get; set; }
		public string? Name { get; set; }
		public string? Status { get; set; }
	}
}
