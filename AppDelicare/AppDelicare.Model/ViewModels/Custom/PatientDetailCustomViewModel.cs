namespace AppDelicare.Model.ViewModels.Custom
{
    public class PatientDetailCustomViewModel : PatientDetailViewModel
    {
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? AdministrativeProvinceName { get; set; }
        public string? AdministrativeDistrictName { get; set; }
        public string? AdministrativeWardName { get; set; }
        public string? Address { get; set; }
        public new DateTime CreatedDate { get; set; } // T45R: ViewModel gốc có CreatedDate (DateTime?) sau CodeGen --merge; bản Custom giữ kiểu cũ.
    }
}
