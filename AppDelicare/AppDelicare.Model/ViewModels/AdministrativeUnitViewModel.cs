namespace AppDelicare.Model.ViewModels
{
	public class AdministrativeUnitViewModel : BaseViewModel
    {
		public string? CodeNameVN { get; set; }
		public string? CodeNameEN { get; set; }
		public string? FullNameVN { get; set; }
		public string? FullNameEN { get; set; }
		public string? ShortNameVN { get; set; }
		public string? ShortNameEN { get; set; }
		public bool IsDefault { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public DateTime? CreatedDate { get; set; }
	}
}
