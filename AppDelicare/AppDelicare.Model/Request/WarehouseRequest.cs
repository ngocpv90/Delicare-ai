using System;

namespace AppDelicare.Model.Request
{
	public class WarehouseRequest : BaseRequest
	{
		public Guid? CompanyId { get; set; }
		public Guid? OrganizationUnitId { get; set; }
		public string? Code { get; set; }
		public string? Name { get; set; }
		public string? WarehouseType { get; set; }
		public string? Address { get; set; }
		public string? Status { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public string? Note { get; set; }
	}
}
