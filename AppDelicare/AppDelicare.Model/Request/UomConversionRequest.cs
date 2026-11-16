using System.ComponentModel.DataAnnotations;
using System;

namespace AppDelicare.Model.Request
{
	public class UomConversionRequest : BaseRequest
	{
		public Guid FromUnitId { get; set; }
		public Guid ToUnitId { get; set; }
		public decimal ConversionFactor { get; set; }
		public DateTime? EffectiveFrom { get; set; }
		public DateTime? EffectiveTo { get; set; }
		public bool IsActive { get; set; }
		public string? Note { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		[MaxLength(50, ErrorMessage = "Status tối đa 50 ký tự.")]
		public string? Status { get; set; }
	}
}
