using System;

namespace AppDelicare.Model.Request
{
	public class StorageZoneRequest : BaseRequest
	{
		public Guid WarehouseId { get; set; }
		public string? Code { get; set; }
		public string? Name { get; set; }
		public string? TemperatureRange { get; set; }
		public string? Status { get; set; }
	}
}
