using System;

namespace AppDelicare.Model.ViewModels
{
	public class StockCountViewModel : BaseViewModel
	{
		public Guid WarehouseId { get; set; }
		public string? WarehouseName { get; set; }
		public string? CountNumber { get; set; }
		public DateTime CountDate { get; set; }
		public string? CountType { get; set; }
		public string? Status { get; set; }
		public Guid? ResponsibleEmployeeId { get; set; }
		public string? ResponsibleEmployeeName { get; set; }
		public string? Note { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public Guid? ApprovedBy { get; set; }
		public DateTime? ApprovedAt { get; set; }
		public DateTime? CreatedDate { get; set; }
	}
}
