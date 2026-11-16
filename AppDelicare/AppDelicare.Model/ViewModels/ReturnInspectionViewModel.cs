using System;

namespace AppDelicare.Model.ViewModels
{
	public class ReturnInspectionViewModel : BaseViewModel
	{
		public Guid ContainerReturnId { get; set; }
		public string? ContainerReturnName { get; set; }
		public DateTime InspectionDate { get; set; }
		public Guid? InspectorEmployeeId { get; set; }
		public string? InspectorEmployeeName { get; set; }
		public string? Result { get; set; }
		public string? Note { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public string? InspectionResult { get; set; }
		public decimal? DamagedQty { get; set; }
		public decimal? MissingQty { get; set; }
		public string? Remark { get; set; }
		public DateTime? CreatedDate { get; set; }
	}
}
