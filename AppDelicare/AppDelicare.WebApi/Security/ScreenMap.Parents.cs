#nullable enable
namespace AppDelicare.WebApi.Security
{
    /// <summary>
    /// MENU-4.3 (D-75 e, anh chốt 2026-11-23): bảng con không còn mục menu riêng (ObjectMenu.IsActive = 0) – quyền chỉ tính trên mục menu đang bật
    /// (PermissionService), nên API của bảng con kiểm quyền theo <b>màn cha</b> (nơi nhập / xem dòng), để màn cha vẫn chạy cho người không phải admin.
    /// Sửa dòng con = sửa chứng từ cha: Thêm dòng cần Sửa (hoặc Thêm) màn cha, Sửa dòng cần Sửa, Xóa dòng cần Sửa (hoặc Xóa); Xem / Xuất / In / Nhập giữ nguyên thao tác.
    /// Viết tay (không sinh từ DDL) – <see cref="ByClass"/> vẫn giữ màn gốc cho tài liệu / CommonRegistry.
    /// </summary>
    public static partial class ScreenMap
    {
        /// <summary>Tên class (= tên controller) → (màn cha, ghi dòng tính là Sửa màn cha).</summary>
        public static readonly IReadOnlyDictionary<string, (string Screen, bool WriteAsEdit)> ParentByClass = new Dictionary<string, (string, bool)>(StringComparer.OrdinalIgnoreCase)
        {
            ["ContractMeal"] = ("crm-contract", true),
            ["OrderLine"] = ("crm-order", true),
            ["RecipeLine"] = ("menu-recipe", true),
            ["MenuLine"] = ("menu-menu", true),
            ["PurchaseRequestLine"] = ("scm-pr", true),
            ["PurchaseOrderLine"] = ("scm-po", true),
            ["GoodsReceiptLine"] = ("scm-gr", true),
            ["MaterialIssueLine"] = ("scm-issue", true),
            ["StockCountLine"] = ("scm-stockcount", true),
            ["ProductionPlanLine"] = ("prod-plan", true),
            ["QcInspectionLine"] = ("qc-inspection", true),
            ["FoodSafetyChecklistItem"] = ("qc-checklist", true),
            ["DeliveryStop"] = ("log-route", true),
            ["DeliveryEvent"] = ("log-execution", true),
            ["ComplaintHistory"] = ("log-complaint", true),
            ["ApprovalStep"] = ("wf-inbox", true),
            ["ApprovalDecision"] = ("wf-inbox", true),
            ["ApprovalItem"] = ("wf-inbox", true),
            // "Thông tin cá nhân" ẩn (trùng Tài khoản / Nhân viên): hồ sơ người dùng tạo / sửa ở màn Tài khoản (admin.js – Person/Insert khi tạo người dùng).
            ["Person"] = ("adm-user", false),
            // MENU-5.4 (D-79): "Người liên hệ" ẩn – nhập / sửa ở Khách hàng 360 (ghi liên hệ = Sửa khách hàng).
            ["CustomerContact"] = ("crm-customer", true),
        };

        /// <summary>Màn hình ẩn khỏi menu (MENU-4) – link tới đây mở màn cha. Dùng cho test / tài liệu.</summary>
        public static readonly IReadOnlyDictionary<string, string> HiddenScreenParent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["crm-contract-meal"] = "crm-contract", ["crm-order-line"] = "crm-order", ["menu-recipe-line"] = "menu-recipe", ["menu-menu-line"] = "menu-menu",
            ["scm-pr-line"] = "scm-pr", ["scm-po-line"] = "scm-po", ["scm-gr-line"] = "scm-gr", ["scm-issue-line"] = "scm-issue",
            ["scm-stockcount-line"] = "scm-stockcount", ["prod-plan-line"] = "prod-plan", ["qc-inspection-line"] = "qc-inspection", ["qc-checklist-item"] = "qc-checklist",
            ["log-stop"] = "log-route", ["log-event"] = "log-execution", ["log-complaint-history"] = "log-complaint",
            ["wf-step"] = "wf-inbox", ["wf-decision"] = "wf-inbox", ["wf-item"] = "wf-inbox", ["adm-person"] = "adm-user",
            ["scm-transfer"] = "scm-inv-balance", ["scm-adjustment"] = "scm-inv-balance", ["crm-contact"] = "crm-customer",
        };
    }
}
