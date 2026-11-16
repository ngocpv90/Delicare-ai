namespace AppDelicare.Model.Request
{
    public class AuditChangeRequest : BaseRequest
    {
        public string? Action { get; set; }
        public string? PropertyName { get; set; }
        public string? ValueOld { get; set; }
        public string? ValueNew { get; set; }
        // T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
        public Guid? IdChange { get; set; }
        public string? EntityName { get; set; }
        public string? Changes { get; set; }
    }
}
