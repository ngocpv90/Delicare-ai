namespace AppDelicare.Model.Model
{
    /// <summary>T45R: nút cây đơn vị tổ chức (spec 3.5) kèm số nhân viên, số kho, tên quản lý.</summary>
    public class OrganizationUnitTreeModel
    {
        public Guid Id { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? UnitType { get; set; }
        public Guid? ParentId { get; set; }
        public Guid? ManagerId { get; set; }
        public string? ManagerName { get; set; }
        public int EmployeeCount { get; set; }
        public int WarehouseCount { get; set; }
        public string? Status { get; set; }
        public List<OrganizationUnitTreeModel> Children { get; set; } = [];
    }
}
