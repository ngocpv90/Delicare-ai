namespace AppDelicare.Model.ViewModels.Custom
{
    public class PatientCustomViewModel : PatientViewModel
    {
        public string? PersonName { get; set; }
        public new DateTime CreatedDate { get; set; } // T45R: ViewModel gốc có CreatedDate (DateTime?) sau CodeGen --merge; bản Custom giữ kiểu cũ.
    }
}
