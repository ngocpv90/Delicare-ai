using System.ComponentModel.DataAnnotations;
namespace AppDelicare.Model.Request
{
	public class UnitMeasureRequest : BaseRequest
    {
		public string? Name { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		[MaxLength(50, ErrorMessage = "Code tối đa 50 ký tự.")]
		public string? Code { get; set; }
		[MaxLength(100, ErrorMessage = "UomType tối đa 100 ký tự.")]
		public string? UomType { get; set; }
		public int? DecimalPrecision { get; set; }
		[MaxLength(50, ErrorMessage = "Status tối đa 50 ký tự.")]
		public string? Status { get; set; }
		public string? Symbol { get; set; }
		public string? UnitType { get; set; }
	}
}
