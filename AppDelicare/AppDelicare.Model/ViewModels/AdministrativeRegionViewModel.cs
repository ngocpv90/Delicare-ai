namespace AppDelicare.Model.ViewModels
{
	public class AdministrativeRegionViewModel : BaseViewModel
    {
		public string? CodeVN { get; set; }
		public string? CodeEN { get; set; }
		public string? NameVN { get; set; }
		public string? NameEN { get; set; }
		public bool IsDefault { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public DateTime? CreatedDate { get; set; }
	}
}
