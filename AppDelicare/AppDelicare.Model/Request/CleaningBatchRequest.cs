using System.ComponentModel.DataAnnotations;
using System;

namespace AppDelicare.Model.Request
{
	public class CleaningBatchRequest : BaseRequest
	{
		public string? CleaningBatchNumber { get; set; }
		public DateTime StartDate { get; set; }
		public DateTime? EndDate { get; set; }
		public int ContainerQuantity { get; set; }
		public Guid? ResponsibleEmployeeId { get; set; }
		public string? QualityStatus { get; set; }
		public string? Status { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public Guid? ReturnInspectionId { get; set; }
		public decimal? Quantity { get; set; }
		[MaxLength(50, ErrorMessage = "QcResult tối đa 50 ký tự.")]
		public string? QcResult { get; set; }
	}
}
