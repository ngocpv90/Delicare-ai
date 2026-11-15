using System;

namespace AppDelicare.Model.ViewModels
{
	public class UomConversionViewModel : BaseViewModel
	{
		public Guid FromUnitId { get; set; }
		public string? FromUnitName { get; set; }
		public Guid ToUnitId { get; set; }
		public string? ToUnitName { get; set; }
		public decimal ConversionFactor { get; set; }
		public DateTime? EffectiveFrom { get; set; }
		public DateTime? EffectiveTo { get; set; }
		public bool IsActive { get; set; }
		public string? Note { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public string? Status { get; set; }
		public DateTime? CreatedDate { get; set; }
	}
}
