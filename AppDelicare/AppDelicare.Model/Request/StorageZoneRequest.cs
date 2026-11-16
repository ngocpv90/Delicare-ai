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
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public decimal? TemperatureMin { get; set; }
		public decimal? TemperatureMax { get; set; }
	}
}
