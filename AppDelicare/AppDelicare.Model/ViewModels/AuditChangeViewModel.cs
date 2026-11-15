namespace AppDelicare.Model.ViewModels
{
    public class AuditChangeViewModel : BaseViewModel
    {
        public Guid? IdChange { get; set; }
        public string? Action { get; set; }
        public string? EntityName { get; set; }
        public string? Changes { get; set; }
        // T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
        public DateTime? CreatedDate { get; set; }
    }
}
