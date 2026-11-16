namespace AppDelicare.Model.ViewModels.Custom
{
    public class LocationTrackingCustomViewModel : LocationTrackingViewModel
    {
        public string? UserName { get; set; }
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public new DateTime CreatedDate { get; set; } // T45R: ViewModel gốc có CreatedDate (DateTime?) sau CodeGen --merge; bản Custom giữ kiểu cũ.
    }
}
