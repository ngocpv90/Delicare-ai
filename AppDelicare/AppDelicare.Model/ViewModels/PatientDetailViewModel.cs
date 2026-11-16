namespace AppDelicare.Model.ViewModels
{
	public class PatientDetailViewModel : BaseViewModel
    {
		public Guid? PatientId { get; set; }
		public string? PatientName { get; set; }
		public Guid? MedicalExaminationId { get; set; }
		public string? MedicalExaminationName { get; set; }
		public Guid? PersonId { get; set; }
		public string? PersonName { get; set; }
		public decimal Price { get; set; }
		public int Quantity { get; set; }
		public decimal TotalPrice { get; set; }
		public string? Result { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public DateTime? CreatedDate { get; set; }
	}
}
