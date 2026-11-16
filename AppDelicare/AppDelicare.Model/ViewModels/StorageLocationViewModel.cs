using System;

namespace AppDelicare.Model.ViewModels
{
	public class StorageLocationViewModel : BaseViewModel
	{
		public Guid WarehouseId { get; set; }
		public string? WarehouseName { get; set; }
		public Guid? StorageZoneId { get; set; }
		public string? StorageZoneName { get; set; }
		public string? Code { get; set; }
		public string? Name { get; set; }
		public string? LocationType { get; set; }
		public string? Status { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public DateTime? CreatedDate { get; set; }
	}
}
