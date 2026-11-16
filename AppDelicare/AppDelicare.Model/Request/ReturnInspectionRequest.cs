using System.ComponentModel.DataAnnotations;
using System;

namespace AppDelicare.Model.Request
{
	public class ReturnInspectionRequest : BaseRequest
	{
		public Guid ContainerReturnId { get; set; }
		public DateTime InspectionDate { get; set; }
		public Guid? InspectorEmployeeId { get; set; }
		public string? Result { get; set; }
		public string? Note { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		[MaxLength(50, ErrorMessage = "InspectionResult tối đa 50 ký tự.")]
		public string? InspectionResult { get; set; }
		public decimal? DamagedQty { get; set; }
		public decimal? MissingQty { get; set; }
		public string? Remark { get; set; }
	}
}
