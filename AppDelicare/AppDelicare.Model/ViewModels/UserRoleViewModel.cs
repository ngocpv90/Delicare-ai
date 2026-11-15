namespace AppDelicare.Model.ViewModels
{
    public class UserRoleViewModel : BaseViewModel
    {
        public string? ObjectMenuName { get; set; }
        public string? ShortName { get; set; }
        public Guid? ParentId { get; set; }
        public string? MetaLink { get; set; }
        public string? IconName { get; set; }
        public int? LevelShow { get; set; }
        public int? OrderShow { get; set; }
        public string? DataTarget { get; set; }
        public string? AriaControls { get; set; }
        public Guid? ObjectMenuId { get; set; }
        public bool AllowAccess { get; set; }
        public bool AllowAdd { get; set; }
        public bool AllowEdit { get; set; }
        public bool AllowDelete { get; set; }
        public bool AllowPrint { get; set; }
        public bool AllowExport { get; set; }
        public bool AllowImport { get; set; }
        public List<UserRoleViewModel>? Children { get; set; }
        // T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
        public Guid? UserGroupId { get; set; }
        public string? UserGroupName { get; set; }
        public DateTime? CreatedDate { get; set; }
    }
}
