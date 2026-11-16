namespace AppDelicare.Model.ViewModels
{
	public class UnitMeasureViewModel : BaseViewModel
    {
		public string? Name { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public string? Code { get; set; }
		public string? UomType { get; set; }
		public int? DecimalPrecision { get; set; }
		public string? Status { get; set; }
		public string? Symbol { get; set; }
		public string? UnitType { get; set; }
		public DateTime? CreatedDate { get; set; }
	}
}
