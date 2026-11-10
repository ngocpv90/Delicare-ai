using System;

namespace AppDelicare.Model.ViewModels
{
	public class StorageZoneViewModel : BaseViewModel
	{
		public Guid WarehouseId { get; set; }
		public string? WarehouseName { get; set; }
		public string? Code { get; set; }
		public string? Name { get; set; }
		public string? TemperatureRange { get; set; }
		public string? Status { get; set; }
	}
}
