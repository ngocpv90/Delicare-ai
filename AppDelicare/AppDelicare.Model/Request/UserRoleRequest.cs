namespace AppDelicare.Model.Request
{
    public class UserRoleRequest
    {
        public string? ListPermission { get; set; }
        // T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
        public Guid? UserGroupId { get; set; }
        public Guid? ObjectMenuId { get; set; }
        public bool? AllowAccess { get; set; }
        public bool? AllowAdd { get; set; }
        public bool? AllowEdit { get; set; }
        public bool? AllowDelete { get; set; }
        public bool? AllowPrint { get; set; }
        public bool? AllowExport { get; set; }
        public bool? AllowImport { get; set; }
    }
}
