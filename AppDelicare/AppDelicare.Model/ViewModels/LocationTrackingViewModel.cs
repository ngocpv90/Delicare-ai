namespace AppDelicare.Model.ViewModels
{
    public class LocationTrackingViewModel : BaseViewModel
    {
        public string? IPv4 { get; set; }
        public string? PcName { get; set; }
        // T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
        public DateTime? CreatedDate { get; set; }
    }
}
