using System;

namespace AppDelicare.Model.SearchRequest
{
	public class StorageLocationSearchRequest : BaseSearchRequest
	{
		public Guid WarehouseId { get; set; }
		public Guid? StorageZoneId { get; set; }
		public string? Code { get; set; }
		public string? Name { get; set; }
		public string? Status { get; set; }
	}
}
