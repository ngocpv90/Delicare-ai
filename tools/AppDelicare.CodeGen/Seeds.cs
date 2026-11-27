using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AppDelicare.CodeGen;

/// <summary>
/// T07: sinh từ mockup + ORG_STRUCTURE.md:
/// <list type="bullet">
/// <item><c>ScreenMap.Generated.cs</c> – bảng → screen-id (MetaLink <c>#/&lt;id&gt;</c>) + nhóm ma trận quyền.</item>
/// <item><c>ReferenceMap.Generated.cs</c> – quan hệ logic (cột XxxId → bảng) cho ReferenceGuard.</item>
/// <item><c>DB/seed/*.sql</c> – Company, OrganizationUnit, KitchenWorkstation, ObjectMenu, UserGroup, UserRole (idempotent, 1 batch).</item>
/// </list>
/// </summary>
public static partial class Seeds
{
    /// <summary>Màn hình catalog.js. <c>Approvable</c> = có loại "AP" (duyệt) → có mục menu "Duyệt …" (D-31).</summary>
    public sealed record Screen(string Id, string Name, string Phase, string Group, string? Table, bool Approvable = false);

    public sealed record OrgUnit(string Code, string Name, string UnitType, string? Parent);

    /// <summary>Nhóm màn hình của catalog.js → cột của ma trận quyền (ORG_STRUCTURE mục 2).</summary>
    public static readonly IReadOnlyDictionary<string, string> GroupToMatrix = new Dictionary<string, string>
    {
        ["Quản trị hệ thống"] = "admin",
        ["Database Architecture"] = "admin",
        ["Kinh doanh & Đơn hàng"] = "sales",
        ["Thực đơn & Món ăn"] = "menu",
        ["Mua hàng & Kho"] = "scm",
        ["Sản xuất & Giá thành"] = "production",
        ["QC & An toàn thực phẩm"] = "qc",
        ["Đóng gói & Giao nhận"] = "logistics",
        ["Nhân sự & Phê duyệt"] = "hr",
        ["Tích hợp & KPI"] = "kpi",
        ["Tổng quan điều hành"] = "kpi",
    };

    /// <summary>Thứ tự cột ma trận trong ORG_STRUCTURE mục 2.</summary>
    public static readonly string[] MatrixColumns = ["admin", "sales", "menu", "scm", "production", "qc", "logistics", "hr", "kpi"];

    public const string FallbackScreen = "dbx-explorer";

    /// <summary>
    /// RETAIL-1: màn không có trong catalog.js của mockup (khai báo ở FE modules/retail.js) – bảng → screen-id cho ScreenMap.
    /// Dòng đơn (RetailOrderLine) nhập ở màn đơn: quyền theo màn cha (ScreenMap.Parents.cs).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> ExtraScreenByTable = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["RetailProduct"] = "sal-retail-product",
        ["RetailOrder"] = "sal-retail-order",
        ["RetailOrderLine"] = "sal-retail-order",
        ["RetailCustomer"] = "sal-retail-order",
    };

    /// <summary>D-31: nhóm menu "Phê duyệt" và mục "Duyệt …" của từng màn hình có loại AP. Quyền duyệt = UserRole.AllowEdit trên mục đó.</summary>
    public const string ApprovalGroupLink = "#/group/approve";
    public const string ApprovalLinkPrefix = "#/wf-inbox?type=";
    /// <summary>Tiền tố cũ (bản seed 2026-11-15 sáng) – seed đổi MetaLink tại chỗ sang tiền tố mới, giữ quyền đã gán.</summary>
    public const string LegacyApprovalLinkPrefix = "#/approve/";

    public static List<Screen> LoadScreens(string root, out List<string> menuOrder)
    {
        string js = File.ReadAllText(Path.Combine(root, "docs", "mockup", "js", "data", "catalog.js"));
        int start = js.IndexOf('{');
        int end = js.LastIndexOf('}');
        using var doc = JsonDocument.Parse(js[start..(end + 1)]);
        menuOrder = doc.RootElement.GetProperty("menuOrder").EnumerateArray().Select(e => e.GetString()!).ToList();
        return doc.RootElement.GetProperty("funcs").EnumerateArray().Select(f => new Screen(
            f.GetProperty("id").GetString()!,
            f.GetProperty("name").GetString()!,
            f.GetProperty("ph").GetString()!,
            f.GetProperty("grp").GetString()!,
            f.TryGetProperty("table", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null,
            (f.TryGetProperty("types", out var ty) && ty.EnumerateArray().Any(x => x.GetString() == "AP"))
                || ExtraApprovable.Contains(f.GetProperty("id").GetString()!))).ToList();
    }

    /// <summary>
    /// Màn hình cần mục "Duyệt …" dù catalog.js không ghi loại AP: <c>crm-order</c> – T13 duyệt đơn sửa lệch suất &gt; 30%
    /// (ApprovalRequest.BusinessType ORDER_DEVIATION, ROADMAP Pilot P2/P3).
    /// </summary>
    private static readonly HashSet<string> ExtraApprovable = new(StringComparer.OrdinalIgnoreCase) { "crm-order" };

    /// <summary>Mục "Duyệt …" không gắn 1 màn hình catalog.js (chỉ là mục quyền duyệt). Nhóm duyệt: Seeds.Menu5.cs (Approvers5 – D-79 d).</summary>
    public sealed record ExtraApproval(string Id, string Name, string Phase);

    // MENU-5 (D-79): RightOverrides (D-69), RestrictedScreens (D-71), TechScreens (D-78 (6)), ApproverOverrides (BIZ-2.1) và nhóm duyệt của ExtraApprovals
    // đã gộp vào ma trận theo từng màn Seeds.Menu5.cs (ScreenMatrix + Approvers5) – một nguồn duy nhất.

    /// <summary>T15.1 (D-11) PO ≥ ngưỡng Giám đốc; BE2.3 (D-46) đề nghị mua 2 cấp; kiểm kê cấp 2 Kế toán (spec 2.3).</summary>
    public static readonly IReadOnlyList<ExtraApproval> ExtraApprovals =
    [
        new("scm-po-director", "Đơn mua hàng (Giám đốc)", "P4"),
        new("scm-pr-l1", "Đề nghị mua hàng (cấp 1 – Trưởng bộ phận Mua hàng)", "P4"),
        new("scm-pr-l2", "Đề nghị mua hàng (cấp 2 – Giám đốc vận hành)", "P4"),
        new("scm-stockcount-l2", "Phiếu kiểm kê (cấp 2 – Kế toán)", "P4"),
    ];

    public static List<OrgUnit> LoadOrgUnits(string root)
    {
        var result = new List<OrgUnit>();
        foreach (string line in OrgSection(root, "## 1."))
        {
            var m = OrgRow().Match(line);
            if (m.Success)
            {
                string parent = m.Groups[4].Value.Trim();
                result.Add(new OrgUnit(m.Groups[1].Value.Trim(), m.Groups[2].Value.Trim(), m.Groups[3].Value.Trim(), parent is "–" or "-" or "" ? null : parent));
            }
        }
        return result;
    }

    private static IEnumerable<string> OrgSection(string root, string heading)
    {
        bool inside = false;
        foreach (string line in File.ReadLines(Path.Combine(root, "docs", "claude", "ORG_STRUCTURE.md")))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                inside = line.StartsWith(heading, StringComparison.Ordinal);
                continue;
            }
            if (inside)
            {
                yield return line;
            }
        }
    }

    private static string Sql(string s) => "N'" + s.Replace("'", "''") + "'";

    // --------------------------------------------------------------- C# maps
    public static string ScreenMapCode(Dictionary<string, Table> tables, List<Screen> screens)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Templates.Header);
        sb.AppendLine("#nullable enable");
        sb.AppendLine("namespace AppDelicare.WebApi.Security");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>Bảng → màn hình mockup (catalog.js). MetaLink = \"#/\" + screen-id (Spec 1.2). Bảng không có màn hình riêng → dbx-explorer (PROPOSED).</summary>");
        sb.AppendLine("    public static partial class ScreenMap");
        sb.AppendLine("    {");
        sb.AppendLine($"        public const string FallbackScreen = \"{FallbackScreen}\";");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>Tên class C# (= tên controller cũ) → screen-id.</summary>");
        sb.AppendLine("        public static readonly IReadOnlyDictionary<string, string> ByClass = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)");
        sb.AppendLine("        {");
        foreach (var t in tables.Values.OrderBy(t => t.Name, StringComparer.Ordinal))
        {
            string screen = screens.FirstOrDefault(s => string.Equals(s.Table, t.Name, StringComparison.OrdinalIgnoreCase))?.Id
                ?? (ExtraScreenByTable.TryGetValue(t.Name, out var extra) ? extra : FallbackScreen);
            sb.AppendLine($"            [\"{Metadata.ClassOf(t.Name)}\"] = \"{screen}\",");
        }
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    public static string ReferenceMapCode(Dictionary<string, Table> tables, Metadata meta)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Templates.Header);
        sb.AppendLine("#nullable enable");
        sb.AppendLine("namespace AppDelicare.Data.Share");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>Quan hệ logic (LOGICAL_RELATIONS.md – không có FK vật lý): bảng đích → các (bảng nguồn, cột) trỏ tới nó.</summary>");
        sb.AppendLine("    public static partial class ReferenceMap");
        sb.AppendLine("    {");
        sb.AppendLine("        public static readonly IReadOnlyDictionary<string, (string SourceTable, string Column)[]> Incoming = new Dictionary<string, (string, string)[]>(StringComparer.OrdinalIgnoreCase)");
        sb.AppendLine("        {");
        var incoming = meta.Relations
            .SelectMany(kv => kv.Value.Select(c => (Source: kv.Key, Column: c.Key, Target: c.Value)))
            .Where(r => tables.TryGetValue(r.Source, out var st) && st.Has(r.Column) && tables.ContainsKey(r.Target)
                && !Metadata.IsLegacyReadOnly(st) && !meta.IsDeprecated(st, r.Column))
            .GroupBy(r => r.Target, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.Ordinal);
        foreach (var g in incoming)
        {
            string items = string.Join(", ", g.OrderBy(r => r.Source, StringComparer.Ordinal).ThenBy(r => r.Column, StringComparer.Ordinal).Select(r => $"(\"{r.Source}\", \"{r.Column}\")"));
            sb.AppendLine($"            [\"{g.Key}\"] = [{items}],");
        }
        sb.AppendLine("        };");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>Tên class C# → tên bảng thật.</summary>");
        sb.AppendLine("        public static readonly IReadOnlyDictionary<string, string> TableOfClass = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)");
        sb.AppendLine("        {");
        foreach (var t in tables.Values.OrderBy(t => t.Name, StringComparer.Ordinal))
        {
            sb.AppendLine($"            [\"{Metadata.ClassOf(t.Name)}\"] = \"{t.Name}\",");
        }
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    // --------------------------------------------------------------- SQL seeds (1 batch, không GO – chạy được bằng SqlCommand trong test)
    private const string SeedHeader = """
        -- <auto-generated> tools/AppDelicare.CodeGen (T07) từ docs/claude/ORG_STRUCTURE.md + docs/mockup/js/data/catalog.js.
        -- Idempotent: chạy nhiều lần không nhân bản (khóa theo Code / MetaLink / Name). Không DELETE dữ liệu cũ.
        -- Chỉ chạy trên DB dev / UAT. Người tạo = tài khoản dev 'admin' (khai báo rõ ràng, KHÔNG lấy "user đầu tiên").
        SET NOCOUNT ON;
        SET XACT_ABORT ON;
        DECLARE @SeedBy UNIQUEIDENTIFIER = (SELECT TOP (1) Id FROM [dbo].[User] WHERE UserName = N'admin' AND IsDeleted = 0);
        IF @SeedBy IS NULL THROW 50001, N'Không có tài khoản admin để ghi CreatedBy cho seed.', 1;
        DECLARE @Now DATETIME = GETDATE();

        """;

    public static string OrganizationSeed(List<OrgUnit> units)
    {
        var sb = new StringBuilder(SeedHeader);
        var company = units.First(u => u.UnitType == "COMPANY");
        sb.AppendLine("BEGIN TRAN;");
        sb.AppendLine("-- Company (OrganizationUnit.CompanyId là NOT NULL)");
        sb.AppendLine($"IF NOT EXISTS (SELECT 1 FROM [dbo].[Company] WHERE Code = {Sql(company.Code)} AND IsDeleted = 0)");
        sb.AppendLine($"    INSERT INTO [dbo].[Company] (Id, Code, Name, Status, Note, CreatedDate, CreatedBy, IsDeleted) VALUES (NEWID(), {Sql(company.Code)}, {Sql(company.Name)}, N'Active', N'Seed T07 – ORG_STRUCTURE.md', @Now, @SeedBy, 0);");
        sb.AppendLine($"DECLARE @CompanyId UNIQUEIDENTIFIER = (SELECT TOP (1) Id FROM [dbo].[Company] WHERE Code = {Sql(company.Code)} AND IsDeleted = 0);");
        sb.AppendLine();
        sb.AppendLine("-- OrganizationUnit: thêm nếu chưa có, cập nhật tên / loại / cha nếu đã có (theo Code)");
        foreach (var u in units)
        {
            string parent = u.Parent == null ? "NULL" : $"(SELECT TOP (1) Id FROM [dbo].[OrganizationUnit] WHERE Code = {Sql(u.Parent)} AND IsDeleted = 0)";
            sb.AppendLine($"IF NOT EXISTS (SELECT 1 FROM [dbo].[OrganizationUnit] WHERE Code = {Sql(u.Code)} AND IsDeleted = 0)");
            sb.AppendLine($"    INSERT INTO [dbo].[OrganizationUnit] (Id, CompanyId, Code, Name, UnitType, ParentId, Status, Note, CreatedDate, CreatedBy, IsDeleted) VALUES (NEWID(), @CompanyId, {Sql(u.Code)}, {Sql(u.Name)}, {Sql(u.UnitType)}, {parent}, N'Active', N'Seed T07 – ORG_STRUCTURE.md', @Now, @SeedBy, 0);");
            sb.AppendLine("ELSE");
            sb.AppendLine($"    UPDATE [dbo].[OrganizationUnit] SET Name = {Sql(u.Name)}, UnitType = {Sql(u.UnitType)}, ParentId = {parent}, ModifiedDate = @Now, ModifiedBy = @SeedBy WHERE Code = {Sql(u.Code)} AND IsDeleted = 0 AND (ISNULL(Name, N'') <> {Sql(u.Name)} OR ISNULL(UnitType, N'') <> {Sql(u.UnitType)} OR ISNULL(ParentId, '00000000-0000-0000-0000-000000000000') <> ISNULL({parent}, '00000000-0000-0000-0000-000000000000'));");
        }
        sb.AppendLine();
        sb.AppendLine("-- KitchenWorkstation: các tổ trong bếp (TEAM con của OPS-KIT) – ORG_STRUCTURE mục 1");
        foreach (var u in units.Where(u => u.UnitType == "TEAM" && u.Parent == "OPS-KIT"))
        {
            sb.AppendLine($"IF NOT EXISTS (SELECT 1 FROM [dbo].[KitchenWorkstation] WHERE Code = {Sql(u.Code)} AND IsDeleted = 0)");
            sb.AppendLine($"    INSERT INTO [dbo].[KitchenWorkstation] (Id, CompanyId, OrganizationUnitId, Code, Name, Status, Note, CreatedDate, CreatedBy, IsDeleted) VALUES (NEWID(), @CompanyId, (SELECT TOP (1) Id FROM [dbo].[OrganizationUnit] WHERE Code = {Sql(u.Code)} AND IsDeleted = 0), {Sql(u.Code)}, {Sql(u.Name)}, N'Active', N'Seed T07 – ORG_STRUCTURE.md', SYSDATETIME(), @SeedBy, 0);");
        }
        sb.AppendLine("COMMIT;");
        return sb.ToString();
    }

    // ---------------------------------------------------------------- MENU-2 (D-75, anh chốt 2026-11-22)
    /// <summary>MENU-2: đổi tên 110 mục menu (khóa theo MetaLink, bảng ROADMAP MENU-2). INSERT dùng tên mới; DB đã có: UPDATE chỉ khi tên đang là tên cũ hoặc "Duyệt – " + tên cũ (không ghi đè tên admin tự sửa). Màn mới sau này đặt tên theo D-75 (a).</summary>
    public static readonly (string Link, string Old, string New)[] MenuRenames =
    [
        ("#/group/01", "Tổng quan điều hành", "Điều hành"),
        ("#/dash-exec", "Tổng quan điều hành", "Bảng điều hành"),
        ("#/group/02", "Kinh doanh & Đơn hàng", "Kinh doanh"),
        ("#/crm-customer", "Hồ sơ khách hàng 360°", "Khách hàng"),
        ("#/crm-contact", "Đầu mối liên hệ khách hàng", "Người liên hệ"),
        ("#/crm-location", "Điểm giao hàng & khung giờ nhận", "Điểm giao hàng"),
        ("#/crm-price-policy", "Chính sách giá theo KH/HĐ", "Chính sách giá"),
        ("#/crm-contract", "Hợp đồng suất ăn", "Hợp đồng"),
        ("#/crm-contract-meal", "Loại suất theo hợp đồng", "Loại suất & đơn giá"),
        ("#/crm-order", "Đơn đặt suất ăn", "Đơn hàng theo ngày"), // UI-6.15 (D-96): đổi tên lần 2 – script DB/scripts/UI615_rename_crm_order.sql
        ("#/crm-order-line", "Số lượng theo ca / loại suất", "Chi tiết đơn đặt suất"),
        ("#/crm-reconciliation", "Đối soát Đặt–SX–Đóng–Giao–Nhận", "Đối soát suất ăn"),
        ("#/menu-dish", "Danh mục món ăn", "Món ăn"),
        ("#/menu-recipe", "Công thức & phiên bản", "Công thức chế biến"),
        ("#/menu-recipe-line", "Định lượng nguyên liệu", "Chi tiết định lượng"),
        ("#/menu-menu", "Thực đơn & phiên bản", "Thực đơn tuần"),
        ("#/menu-menu-line", "Lịch món theo ngày/bữa", "Chi tiết thực đơn"),
        ("#/menu-assignment", "Gán thực đơn cho KH / điểm giao / loại suất", "Thực đơn riêng cho khách"),
        ("#/scm-supplier", "Hồ sơ nhà cung cấp", "Nhà cung cấp"),
        ("#/scm-ingredient", "Danh mục nguyên liệu", "Nguyên liệu"),
        ("#/scm-supplier-material", "Bảng giá & MOQ theo NCC", "Bảng giá nhà cung cấp"),
        ("#/scm-warehouse", "Danh mục kho", "Kho"),
        ("#/scm-zone", "Khu vực bảo quản & ngưỡng nhiệt", "Khu bảo quản"),
        ("#/scm-location", "Vị trí lưu trữ (bin)", "Vị trí kệ"),
        ("#/scm-inv-policy", "Min / Max / Safety / Reorder", "Định mức tồn kho"),
        ("#/scm-requirement", "Tính nhu cầu nguyên liệu (MRP)", "Tính nhu cầu nguyên liệu"),
        ("#/scm-pr-line", "Dòng đề nghị mua", "Chi tiết đề nghị mua"),
        ("#/scm-po-line", "Dòng đơn mua", "Chi tiết đơn mua"),
        ("#/scm-gr-line", "Dòng nhập – lô, HSD", "Chi tiết phiếu nhập"),
        ("#/scm-lot", "Lô & hạn sử dụng (FEFO)", "Lô hàng & hạn dùng"),
        ("#/scm-inv-txn", "Sổ cái kho (ledger)", "Sổ kho"),
        ("#/scm-inv-balance", "Tồn kho theo kho / lô", "Tồn kho"),
        ("#/scm-issue", "Phiếu xuất cho sản xuất", "Phiếu xuất sản xuất"),
        ("#/scm-issue-line", "Dòng xuất theo lô", "Chi tiết phiếu xuất"),
        ("#/scm-stockcount-line", "Dòng kiểm kê & chênh lệch", "Chi tiết kiểm kê"),
        ("#/scm-consumption-var", "Tiêu hao lý thuyết vs thực tế", "Chênh lệch tiêu hao"),
        ("#/cost-standard", "Giá thành chuẩn theo công thức", "Giá thành chuẩn"),
        ("#/prod-line", "Dây chuyền & năng lực", "Dây chuyền bếp"),
        ("#/prod-line", "Dây chuyền bếp", "Dây chuyền & trạm bếp"), // UI-6.24 (D-105): chạy sau dòng trên (đổi tên nối tiếp)
        ("#/prod-workstation", "Trạm làm việc bếp", "Trạm bếp"),
        ("#/prod-plan", "Kế hoạch sản xuất theo ngày/ca", "Kế hoạch sản xuất"),
        ("#/prod-plan-line", "Dòng kế hoạch theo món", "Chi tiết kế hoạch sản xuất"),
        ("#/prod-batch", "Bảng điều độ mẻ (ca × dây chuyền)", "Mẻ nấu"),
        ("#/prod-output", "Ghi nhận sản lượng", "Sản lượng"),
        ("#/cost-actual", "Giá thành thực tế theo mẻ/lệnh", "Giá thành thực tế"),
        ("#/cost-meal", "Giá thành mỗi suất & Margin", "Giá vốn suất ăn"),
        ("#/cost-meal", "Giá vốn suất ăn", "Giá vốn & lãi gộp"), // UI-6.27 (D-108): chạy sau dòng trên
        ("#/prod-waste", "Ghi nhận hao hụt", "Hao hụt"),
        ("#/group/06", "QC & An toàn thực phẩm", "Chất lượng & ATTP"),
        ("#/qc-checklist", "Bộ checklist ATTP", "Bộ kiểm tra ATTP"),
        ("#/qc-checklist-item", "Tiêu chí checklist", "Tiêu chí kiểm tra"),
        ("#/qc-inspection", "Phiếu QC đầu vào", "Kiểm tra hàng nhập"),
        // UI-6.29 (D-110): chạy sau các dòng đổi tên cũ của cùng mục.
        ("#/scm-gr", "Phiếu nhập kho", "Nhận hàng"),
        ("#/scm-issue", "Phiếu xuất sản xuất", "Xuất kho sản xuất"),
        ("#/scm-warehouse", "Kho", "Kho & vị trí"),
        ("#/qc-inspection", "Kiểm tra hàng nhập", "Sổ kiểm tra hàng nhập"),
        ("#/scm-stockcount", "Phiếu kiểm kê", "Kiểm kê"),
        ("#/qc-inspection-line", "Tiêu chí QC theo dòng nhập", "Chi tiết kiểm tra hàng nhập"),
        ("#/qc-checklist-result", "Thực hiện checklist theo ca/mẻ", "Kiểm tra theo ca / mẻ"),
        ("#/qc-temperature", "Nhật ký nhiệt độ kho/mẻ", "Nhật ký nhiệt độ"),
        ("#/qc-cleaning", "Mẻ vệ sinh khay/hộp", "Vệ sinh khay hộp"),
        ("#/qc-cleaning-result", "Checklist vệ sinh", "Kiểm tra vệ sinh"),
        ("#/qc-supplier-issue", "Sự cố / NCR nhà cung cấp", "Sự cố nhà cung cấp"),
        ("#/group/07", "Đóng gói & Giao nhận", "Đóng gói & Giao hàng"),
        ("#/log-vehicle", "Danh mục phương tiện", "Xe giao hàng"),
        ("#/log-packing-order", "Lệnh đóng suất", "Lệnh đóng gói"),
        ("#/log-route", "Lập tuyến & điều phối", "Chuyến giao hàng"),
        ("#/log-stop", "Thứ tự điểm dừng", "Điểm dừng"),
        ("#/log-event", "Timeline sự kiện giao hàng", "Diễn biến giao hàng"),
        ("#/log-pod", "Xác nhận giao hàng (POD)", "Xác nhận đã giao"),
        ("#/log-container-return", "Thu hồi khay/hộp", "Thu hồi khay hộp"),
        ("#/log-return-inspection", "Kiểm tra vật dụng thu hồi", "Kiểm tra khay hộp thu hồi"),
        ("#/log-packaging-ledger", "Sổ bao bì theo khách hàng", "Khay hộp theo khách"),
        ("#/log-packing-result", "Kết quả đóng suất", "Kết quả đóng gói"),
        ("#/log-gps", "Bản đồ vị trí xe", "Vị trí xe"),
        ("#/log-complaint", "Khiếu nại khách hàng", "Phản ánh khách hàng"),
        ("#/log-complaint-history", "Lịch sử xử lý khiếu nại", "Lịch sử xử lý phản ánh"),
        ("#/group/08", "Nhân sự & Phê duyệt", "Nhân sự"),
        ("#/hr-shift", "Danh mục ca làm việc (4 ca)", "Ca làm việc"),
        ("#/hr-shift-assign", "Lịch phân ca nhân sự", "Phân ca"),
        ("#/hr-attendance", "Chấm công theo ca", "Chấm công"),
        ("#/wf-inbox", "Hộp phê duyệt tập trung", "Hộp phê duyệt"),
        ("#/wf-step", "Bước phê duyệt", "Các bước duyệt"),
        ("#/wf-decision", "Quyết định phê duyệt", "Lịch sử duyệt"),
        ("#/wf-item", "Đối tượng gắn với yêu cầu", "Chứng từ trong yêu cầu duyệt"),
        ("#/group/09", "Tích hợp & KPI", "Tích hợp & Chỉ số"),
        ("#/int-channel", "Kênh Zalo / Telegram / Facebook", "Kênh nhắn tin"),
        ("#/int-bot", "Cấu hình Bot", "Bot"),
        ("#/int-identity", "Liên kết tài khoản Zalo/Telegram", "Liên kết tài khoản chat"),
        ("#/int-inbound", "Tin nhắn vào", "Tin nhắn đến"),
        ("#/int-outbound", "Tin nhắn ra", "Tin nhắn đi"),
        ("#/kpi-definition", "Định nghĩa KPI", "Danh mục chỉ số"),
        ("#/kpi-daily", "KPI ngày", "Chỉ số theo ngày"),
        ("#/kpi-daily", "Chỉ số theo ngày", "Chỉ số kinh doanh"), // UI-6.28 (D-109): chạy sau dòng trên
        ("#/kpi-monthly", "KPI tháng", "Chỉ số theo tháng"),
        ("#/kpi-refresh", "Nhật ký làm mới KPI", "Nhật ký tính chỉ số"),
        ("#/adm-company", "Công ty / Pháp nhân", "Công ty"),
        ("#/adm-orgunit", "Cây đơn vị tổ chức", "Sơ đồ tổ chức"),
        ("#/adm-employee", "Hồ sơ nhân viên", "Nhân viên"),
        ("#/adm-person", "Thông tin cá nhân của tài khoản", "Thông tin cá nhân"),
        ("#/adm-user", "Tài khoản người dùng", "Tài khoản"),
        ("#/adm-permission", "Ma trận quyền Nhóm × Chức năng", "Phân quyền"),
        ("#/adm-usergroup", "Nhóm người dùng (Vai trò)", "Nhóm quyền"),
        ("#/adm-objectmenu", "Cấu hình menu chức năng (ObjectMenu)", "Cấu hình menu"),
        ("#/adm-position", "Danh mục chức vụ", "Chức vụ"),
        ("#/adm-admin-unit", "Vùng / Tỉnh / Huyện / Xã", "Địa giới hành chính"),
        ("#/adm-document", "Kho tài liệu đính kèm", "Tài liệu đính kèm"),
        ("#/adm-access-log", "Nhật ký máy truy cập (IP/PC)", "Nhật ký truy cập"),
        ("#/adm-merge-conflict", "Xung đột chuẩn hóa dữ liệu lõi", "Dữ liệu trùng cần gộp"),
        ("#/adm-reference", "Danh mục tham chiếu (Meal Type, lý do, loại…)", "Danh mục dùng chung"),
        ("#/wf-inbox?type=menu-recipe", "Công thức & phiên bản", "Công thức chế biến"),
        ("#/wf-inbox?type=crm-price-policy", "Chính sách giá theo KH/HĐ", "Chính sách giá"),
        ("#/wf-inbox?type=crm-contract", "Hợp đồng suất ăn", "Hợp đồng"),
        ("#/wf-inbox?type=menu-menu", "Thực đơn & phiên bản", "Thực đơn tuần"),
        ("#/wf-inbox?type=crm-order", "Đơn đặt suất ăn", "Đơn đặt suất"),
        ("#/wf-inbox?type=prod-plan", "Kế hoạch sản xuất theo ngày/ca", "Kế hoạch sản xuất"),
        ("#/wf-inbox?type=qc-inspection", "Phiếu QC đầu vào", "Kiểm tra hàng nhập"),
        ("#/wf-inbox?type=wf-inbox", "Hộp phê duyệt tập trung", "Tất cả yêu cầu duyệt"),
        // UI-6.34 (D-115)
        ("#/log-packing-order", "Lệnh đóng gói", "Chia suất & đóng gói"),
        ("#/log-route", "Chuyến giao hàng", "Điều phối giao hàng"),
        // UI-6.35 (D-116)
        ("#/log-execution", "Theo dõi chuyến giao", "Theo dõi giao hàng"),
        // UI-6.39 (D-120)
        ("#/hr-leave", "Đơn xin nghỉ", "Nghỉ & đổi ca"),
        ("#/wf-inbox?type=hr-leave", "Đơn xin nghỉ", "Nghỉ & đổi ca"),
        // UI-6.41 (D-122)
        ("#/adm-orgunit", "Sơ đồ tổ chức", "Công ty & đơn vị"),
    ];

    /// <summary>
    /// MENU-4 (D-75 d, e, f – anh chốt 2026-11-23): ẩn bảng con (nhập / xem ở màn cha – quyền API chuyển sang màn cha, ScreenMap.ParentByClass),
    /// màn chưa làm (Phiếu chuyển kho, Phiếu điều chỉnh tồn – nghiệp vụ thật ở nút trên màn Tồn kho) và "Thông tin cá nhân" (trùng Tài khoản).
    /// KHÔNG ẩn #/wf-inbox: dòng này là quyền của cả API duyệt (ApprovalRequest) – chỉ bỏ khỏi sidebar ở FE (core/app.js).
    /// </summary>
    public static readonly string[] Menu4HiddenLinks =
    [
        "#/crm-contract-meal", "#/crm-order-line", "#/menu-recipe-line", "#/menu-menu-line", "#/scm-pr-line", "#/scm-po-line", "#/scm-gr-line",
        "#/scm-issue-line", "#/scm-stockcount-line", "#/prod-plan-line", "#/qc-inspection-line", "#/qc-checklist-item", "#/log-stop", "#/log-event",
        "#/log-complaint-history", "#/wf-step", "#/wf-decision", "#/wf-item", "#/scm-transfer", "#/scm-adjustment", "#/adm-person",
    ];

    /// <summary>MENU-2 / D-58: nhóm Database Architecture + 2 màn tra cứu cấu trúc DB; MENU-4: thêm Menu4HiddenLinks → IsActive = 0 (không xóa dòng).</summary>
    /// <summary>BIZ-3.7 (D-77 – B7): mục "Phê duyệt › Kiểm tra hàng nhập" – không loại duyệt nào dùng (QC đầu vào kết luận trực tiếp, BIZ-2.6 tự ghi sổ) → ẩn.</summary>
    public static readonly string[] Biz3HiddenLinks = ["#/wf-inbox?type=qc-inspection"];

    /// <remarks>MENU-5.2 (D-79): mục duyệt "Kiểm tra hàng nhập" mở lại (quyền Kết luận QC) – bỏ Biz3HiddenLinks khỏi danh sách ẩn.</remarks>
    /// <summary>UI-6.12 (D-90): bỏ thực đơn riêng theo khách – mục #/menu-assignment ẩn (IsActive = 0), giữ dòng + quyền; script DB/scripts/UI612_hide_menu_assignment.sql.</summary>
    /// <remarks>UI-6.4 (D-83): + "#/crm-price-policy" – điều chỉnh giá làm trong tab giá của hợp đồng (phụ lục), script DB/scripts/UI64_hide_price_policy_menu.sql.</remarks>
    public static readonly string[] Ui6HiddenLinks = ["#/menu-assignment", "#/crm-price-policy", "#/menu-change-request", "#/crm-location", "#/scm-ingredient-cat", "#/menu-dish-cat", "#/scm-uom-conversion",
        "#/prod-order", "#/prod-output", "#/prod-workstation",
        "#/cost-standard", "#/cost-actual", "#/group/05b",
        "#/kpi-monthly", // UI-6.28 (D-109): gộp vào Chỉ số kinh doanh
        "#/scm-zone", "#/scm-location", "#/scm-lot", "#/scm-inv-txn", "#/scm-inv-policy", "#/scm-reservation", "#/scm-consumption-var", // UI-6.29 (D-110): Kho 12 → 5 mục
        "#/qc-cleaning-result", "#/log-return-inspection", // UI-6.32 (D-113)
        "#/log-packing-result", "#/log-delivery-order", "#/log-loading", // UI-6.34 (D-115)
        "#/log-gps", "#/log-pod", "#/log-container-return", // UI-6.35 (D-116)
        "#/hr-swap", "#/wf-inbox?type=hr-swap", // UI-6.39 (D-120)
        "#/adm-company", "#/adm-permission"]; // UI-6.43 (D-124): Phân quyền là tab của trang Nhóm quyền // UI-6.41 (D-122): Công ty & đơn vị (quyền sửa công ty giữ theo mục Công ty) // UI-6.39 (D-120): Nghỉ & đổi ca gộp Đơn xin nghỉ + Đơn đổi ca (quyền duyệt từng loại giữ nguyên) // UI-6.35 (D-116): Theo dõi giao hàng + Khay hộp theo khách // UI-6.34 (D-115): Đóng gói & giao hàng – 2 mục thao tác; UI-6.32 (D-113): Kiểm tra vệ sinh + Kiểm tra khay thu hồi gộp vào Vệ sinh khay hộp // UI-6.27 (D-108): 3 màn Giá thành gộp vào Giá vốn & lãi gộp (Điều hành) // UI-6.24 (D-105): menu Sản xuất 7 → 4 mục

    public static string[] HiddenMenuLinks => ["#/group/11", "#/dbx-explorer", "#/dbx-gap", .. Menu4HiddenLinks, .. Ui6HiddenLinks];

    /// <summary>Tên hiển thị của mục menu theo MENU-2 (không có trong bảng → giữ tên catalog).</summary>
    public static string MenuName(string link, string name) => MenuRenames.LastOrDefault(r => r.Link == link).New ?? name; // UI-6.24: tên mới nhất khi một mục đổi tên nhiều lần

    private static int MenuActive(string link) => HiddenMenuLinks.Contains(link) ? 0 : 1;

    public static string ObjectMenuSeed(List<Screen> screens, List<string> menuOrder)
    {
        var sb = new StringBuilder(SeedHeader);
        sb.AppendLine("BEGIN TRAN;");
        sb.AppendLine("-- CLAUDE.md 4.3: chỉ THÊM menu chưa có (theo MetaLink), không ghi đè tên / thứ tự admin đã chỉnh.");
        sb.AppendLine("-- Menu cấp 1 = nhóm màn hình (MetaLink '#/group/<n>'), cấp 2 = màn hình (MetaLink '#/<screen-id>'), thứ tự theo catalog.menuOrder.");
        int g = 0;
        foreach (string group in menuOrder)
        {
            g++;
            string groupLink = $"#/group/{g:00}";
            sb.AppendLine($"IF NOT EXISTS (SELECT 1 FROM [dbo].[ObjectMenu] WHERE MetaLink = {Sql(groupLink)} AND IsDeleted = 0)");
            sb.AppendLine($"    INSERT INTO [dbo].[ObjectMenu] (Id, Name, ShortName, MetaLink, ParentId, LevelShow, OrderShow, IsActive, IsDefault, CreatedDate, CreatedBy, IsDeleted) VALUES (NEWID(), {Sql(MenuName(groupLink, group))}, {Sql(GroupToMatrix[group])}, {Sql(groupLink)}, NULL, 1, {g * 100}, {MenuActive(groupLink)}, 0, @Now, @SeedBy, 0);");
            int i = 0;
            foreach (var s in screens.Where(s => s.Group == group))
            {
                i++;
                string link = "#/" + s.Id;
                string parent = $"(SELECT TOP (1) Id FROM [dbo].[ObjectMenu] WHERE MetaLink = {Sql(groupLink)} AND IsDeleted = 0)";
                int order = g * 100 + i;
                sb.AppendLine($"IF NOT EXISTS (SELECT 1 FROM [dbo].[ObjectMenu] WHERE MetaLink = {Sql(link)} AND IsDeleted = 0)");
                sb.AppendLine($"    INSERT INTO [dbo].[ObjectMenu] (Id, Name, ShortName, MetaLink, ParentId, LevelShow, OrderShow, IsActive, IsDefault, CreatedDate, CreatedBy, IsDeleted) VALUES (NEWID(), {Sql(MenuName(link, s.Name))}, {Sql(s.Phase)}, {Sql(link)}, {parent}, 2, {order}, {MenuActive(link)}, 0, @Now, @SeedBy, 0);");
            }
        }
        sb.AppendLine();
        sb.AppendLine("-- D-31: nhóm \"Phê duyệt\" + mục \"Duyệt …\" cho màn hình có loại AP (MetaLink '#/wf-inbox?type=<screen-id>' – hộp duyệt lọc theo loại, CLAUDE.md 4.3). Chỉ thêm khi chưa có.");
        sb.AppendLine($"-- Đổi MetaLink mục duyệt kiểu cũ '{LegacyApprovalLinkPrefix}<id>' → '{ApprovalLinkPrefix}<id>' (giữ dòng và quyền đã gán).");
        sb.AppendLine($"UPDATE [dbo].[ObjectMenu] SET MetaLink = N'{ApprovalLinkPrefix}' + SUBSTRING(MetaLink, {LegacyApprovalLinkPrefix.Length + 1}, 200), ModifiedDate = @Now, ModifiedBy = @SeedBy WHERE MetaLink LIKE N'{LegacyApprovalLinkPrefix}%' AND IsDeleted = 0;");
        sb.AppendLine($"IF NOT EXISTS (SELECT 1 FROM [dbo].[ObjectMenu] WHERE MetaLink = {Sql(ApprovalGroupLink)} AND IsDeleted = 0)");
        sb.AppendLine($"    INSERT INTO [dbo].[ObjectMenu] (Id, Name, ShortName, MetaLink, ParentId, LevelShow, OrderShow, IsActive, IsDefault, CreatedDate, CreatedBy, IsDeleted) VALUES (NEWID(), N'Phê duyệt', N'approve', {Sql(ApprovalGroupLink)}, NULL, 1, 9900, 1, 0, @Now, @SeedBy, 0);");
        int a = 0;
        foreach (var s in screens.Where(s => s.Approvable))
        {
            a++;
            string link = ApprovalLinkPrefix + s.Id;
            string parent = $"(SELECT TOP (1) Id FROM [dbo].[ObjectMenu] WHERE MetaLink = {Sql(ApprovalGroupLink)} AND IsDeleted = 0)";
            sb.AppendLine($"IF NOT EXISTS (SELECT 1 FROM [dbo].[ObjectMenu] WHERE MetaLink = {Sql(link)} AND IsDeleted = 0)");
            sb.AppendLine($"    INSERT INTO [dbo].[ObjectMenu] (Id, Name, ShortName, MetaLink, ParentId, LevelShow, OrderShow, IsActive, IsDefault, CreatedDate, CreatedBy, IsDeleted) VALUES (NEWID(), {Sql(MenuName(link, s.Name))}, {Sql(s.Phase)}, {Sql(link)}, {parent}, 2, {9900 + a}, 1, 0, @Now, @SeedBy, 0);");
        }
        sb.AppendLine("-- T15.1: mục quyền duyệt không gắn màn hình catalog (PO ≥ ngưỡng Giám đốc duyệt – D-11).");
        foreach (var x in ExtraApprovals)
        {
            a++;
            string link = ApprovalLinkPrefix + x.Id;
            string parent = $"(SELECT TOP (1) Id FROM [dbo].[ObjectMenu] WHERE MetaLink = {Sql(ApprovalGroupLink)} AND IsDeleted = 0)";
            sb.AppendLine($"IF NOT EXISTS (SELECT 1 FROM [dbo].[ObjectMenu] WHERE MetaLink = {Sql(link)} AND IsDeleted = 0)");
            sb.AppendLine($"    INSERT INTO [dbo].[ObjectMenu] (Id, Name, ShortName, MetaLink, ParentId, LevelShow, OrderShow, IsActive, IsDefault, CreatedDate, CreatedBy, IsDeleted) VALUES (NEWID(), {Sql(x.Name)}, {Sql(x.Phase)}, {Sql(link)}, {parent}, 2, {9900 + a}, 1, 0, @Now, @SeedBy, 0);");
        }
        // MENU-1 (anh yêu cầu 2026-11-22): nhóm Phê duyệt đã nói rõ là duyệt → tên mục con không có tiền tố "Duyệt – ". DB đã có: bỏ tiền tố (idempotent, chỉ nhóm Phê duyệt).
        sb.AppendLine("-- MENU-1: bỏ tiền tố \"Duyệt – \" ở tên mục nhóm Phê duyệt (giữ Id / MetaLink / quyền). Chạy lại không đổi gì.");
        sb.AppendLine("UPDATE [dbo].[ObjectMenu] SET Name = SUBSTRING(Name, 9, 500), ModifiedDate = @Now, ModifiedBy = @SeedBy WHERE MetaLink LIKE N'#/wf-inbox?type=%' AND Name LIKE N'Duyệt – %' AND IsDeleted = 0;");
        // MENU-2 (D-75): đổi tên 110 mục theo MetaLink – chỉ khi tên đang là tên cũ (hoặc "Duyệt – " + tên cũ); ẩn nhóm Database Architecture. Chạy lại không đổi gì.
        sb.AppendLine("-- MENU-2 (D-75): đổi tên 110 mục menu theo MetaLink – chỉ khi tên đang là tên cũ / \"Duyệt – \" + tên cũ (không ghi đè tên admin tự sửa); giữ Id / MetaLink / thứ tự / quyền.");
        foreach (var (link, oldName, newName) in MenuRenames)
        {
            sb.AppendLine($"UPDATE [dbo].[ObjectMenu] SET Name = {Sql(newName)}, ModifiedDate = @Now, ModifiedBy = @SeedBy WHERE MetaLink = {Sql(link)} AND IsDeleted = 0 AND Name IN ({Sql(oldName)}, {Sql("Duyệt – " + oldName)});");
        }
        sb.AppendLine("-- MENU-2 / D-58: ẩn nhóm Database Architecture và 2 màn tra cứu cấu trúc DB; MENU-4 (D-75 e): ẩn bảng con, màn chưa làm, mục trùng (IsActive = 0, không xóa).");
        sb.AppendLine($"UPDATE [dbo].[ObjectMenu] SET IsActive = 0, ModifiedDate = @Now, ModifiedBy = @SeedBy WHERE MetaLink IN ({string.Join(", ", HiddenMenuLinks.Select(Sql))}) AND IsDeleted = 0 AND IsActive = 1;");
        sb.Append(Menu5SeedBlock(screens, menuOrder));
        sb.AppendLine("COMMIT;");
        return sb.ToString();
    }

    [GeneratedRegex(@"^\|\s*([A-Z0-9-]+)\s*\|\s*([^|]+?)\s*\|\s*(COMPANY|BOARD|DIVISION|DEPARTMENT|TEAM)\s*\|\s*([^|]*?)\s*\|\s*$")]
    private static partial Regex OrgRow();
}
