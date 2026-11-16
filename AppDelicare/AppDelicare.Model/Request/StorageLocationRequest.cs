using System;

namespace AppDelicare.Model.Request
{
	public class StorageLocationRequest : BaseRequest
	{
		public Guid WarehouseId { get; set; }
		public Guid? StorageZoneId { get; set; }
		public string? Code { get; set; }
		public string? Name { get; set; }
		public string? LocationType { get; set; }
		public string? Status { get; set; }
	}
}
