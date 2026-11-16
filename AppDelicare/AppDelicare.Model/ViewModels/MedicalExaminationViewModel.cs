namespace AppDelicare.Model.ViewModels
{
	public class MedicalExaminationViewModel : BaseViewModel
    {
		public string? Name { get; set; }
		public decimal Price { get; set; }
		public string? NormalIndex { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public DateTime? CreatedDate { get; set; }
	}
}
