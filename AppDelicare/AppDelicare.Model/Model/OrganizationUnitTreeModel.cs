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
        // UI-6.41 (D-122)
        public bool IsCompany { get; set; }
        public Guid? CompanyId { get; set; }
        public string? UnitTypeName { get; set; }
        /// <summary>Số nhân viên gồm cả đơn vị con.</summary>
        public int TotalEmployees { get; set; }
        public bool NoManager { get; set; }
    }
}

namespace AppDelicare.Model.Model
{
    /// <summary>UI-6.41 (D-122): 4 chỉ số màn "Công ty & đơn vị".</summary>
    public class OrgSummaryModel
    {
        public int ActiveUnits { get; set; }
        public int Companies { get; set; }
        public int Kitchens { get; set; }
        public int Teams { get; set; }
        public int Employees { get; set; }
        public int NoManager { get; set; }
        public int EmployeesNoUnit { get; set; }
    }

    public class OrgMoveRequest
    {
        public Guid Id { get; set; }
        /// <summary>Trống = lên gốc của công ty CompanyId.</summary>
        public Guid? NewParentId { get; set; }
        public Guid? CompanyId { get; set; }
    }

    public class OrgSetStatusRequest
    {
        public Guid Id { get; set; }
        public string? Status { get; set; }
    }

    public class OrgSetManagerRequest
    {
        public Guid Id { get; set; }
        public Guid? EmployeeId { get; set; }
    }
}
