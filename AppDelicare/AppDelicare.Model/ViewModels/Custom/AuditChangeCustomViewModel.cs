namespace AppDelicare.Model.ViewModels.Custom
{
    public class AuditChangeCustomViewModel : AuditChangeViewModel
    {
        public new DateTime CreatedDate { get; set; } // T45R: ViewModel gốc có CreatedDate (DateTime?) sau CodeGen --merge; bản Custom giữ kiểu cũ.
        public string? UserName { get; set; }
    }
}
