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

    /// <summary>Mục "Duyệt …" không gắn 1 màn hình catalog.js (chỉ là mục quyền duyệt) + nhóm được seed quyền duyệt (AllowAccess + AllowEdit).</summary>
    public sealed record ExtraApproval(string Id, string Name, string Phase, string[] Roles);

    /// <summary>
    /// D-69 (UG-1 C-17, C-23 – anh chốt 2026-11-19): quyền theo TỪNG MÀN bổ sung cho ma trận theo nhóm màn (ORG_STRUCTURE §2 chỉ chia theo nhóm).
    /// Chỉ NÂNG quyền (OR với ma trận), không hạ. Roles = ["*"] → mọi nhóm. DB đang chạy: DB/scripts/UG1_D69_permissions.sql.
    /// </summary>
    public sealed record RightOverride(string Screen, string[] Roles, bool Add, bool Edit);

    public static readonly IReadOnlyList<RightOverride> RightOverrides =
    [
        new("log-packing-order", ["OPS-KIT"], Add: true, Edit: true),    // C-17: tổ Chia suất / Đóng gói thuộc Bếp
        new("log-packing-result", ["OPS-KIT"], Add: true, Edit: true),
        // C-18 (tự chấm công / lập đơn nghỉ – đổi ca của mình): KHÔNG cấp quyền màn hr-* – quyền Thêm = lập hộ người khác, Truy cập = xem của mọi người.
        // Tự phục vụ đi qua API SkipPermission (CheckIn / CheckOut / LeaveRequest.Create / ShiftSwapRequest.Create) trên màn hr-my (mở cho mọi người).
        new("adm-employee", ["FIN-HR"], Add: true, Edit: true),          // C-23: Nhân sự sửa hồ sơ (SĐT = khóa liên kết bot), tạo mã liên kết Telegram
    ];

    /// <summary>
    /// D-71 (PROPOSED, làm theo quy tắc ROADMAP – mục PROPOSED chưa chốt thì làm theo đề xuất): màn GIỚI HẠN – chỉ nhóm liệt kê có quyền, nhóm khác = 0
    /// (khác RightOverrides chỉ nâng). Màn tích hợp bot (cấu hình kênh / bot, định danh, nhật ký tin) chỉ quản trị FIN-IT; CEO / COO xem tin gửi đi để thấy tin lỗi.
    /// DB đang chạy: DB/scripts/D71_revoke_int_screens.sql.
    /// </summary>
    public sealed record ScreenRight(bool Add, bool Edit, bool Delete, bool Export);

    /// <summary>D-78 (6): màn kỹ thuật (MENU-4 nhóm E) ngoài các màn tích hợp D-71.</summary>
    public static readonly string[] TechScreens = ["adm-audit", "adm-merge-conflict", "kpi-definition", "kpi-refresh"];

    public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, ScreenRight>> RestrictedScreens = BuildRestricted();

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, ScreenRight>> BuildRestricted()
    {
        var admin = new ScreenRight(Add: true, Edit: true, Delete: false, Export: true);
        var view = new ScreenRight(false, false, false, false);
        var map = new Dictionary<string, IReadOnlyDictionary<string, ScreenRight>>();
        foreach (string s in new[] { "int-channel", "int-bot", "int-identity", "int-inbound", "int-log" })
        {
            map[s] = new Dictionary<string, ScreenRight> { ["FIN-IT"] = admin };
        }
        map["int-outbound"] = new Dictionary<string, ScreenRight> { ["FIN-IT"] = admin, ["CEO"] = view, ["COO"] = view };
        // D-78 (6) – MENU-4 nhóm E: màn kỹ thuật chỉ admin / FIN-IT. Danh mục chỉ số vẫn ĐỌC được qua quyền xem KPI / bảng điều hành (PermissionConvention.ReadAlsoScreens).
        // DB đang chạy: DB/scripts/D78_restrict_tech_screens.sql.
        foreach (string s in TechScreens)
        {
            map[s] = new Dictionary<string, ScreenRight> { ["FIN-IT"] = admin };
        }
        return map;
    }

    private static RightOverride? OverrideOf(string screen, string role)
        => RightOverrides.FirstOrDefault(o => o.Screen == screen && (o.Roles.Contains("*") || o.Roles.Contains(role)));

    /// <summary>
    /// T15.1 (D-11, anh chốt 2026-11-16): PO tổng ≥ <c>BusinessRules:PurchaseOrderDirectorApprovalAmount</c> do Giám đốc duyệt → mục
    /// <c>#/wf-inbox?type=scm-po-director</c>, chỉ seed quyền cho nhóm CEO (admin đổi trên ma trận quyền).
    /// <para>BE2.3 (D-46, anh chốt 2026-11-16 – nhóm duyệt từng cấp PROPOSED): đề nghị mua 2 cấp <c>scm-pr-l1</c> (Trưởng bộ phận Mua hàng → seed OPS-SCM) →
    /// <c>scm-pr-l2</c> (Giám đốc vận hành → seed COO); kiểm kê lệch &gt; 2% cấp 2 Kế toán <c>scm-stockcount-l2</c> (spec 2.3 → seed FIN-ACC).</para>
    /// </summary>
    /// <summary>BIZ-2.1 (D-77 – B6): mục duyệt có nhóm duyệt riêng (không lấy theo ma trận). "Đơn đặt suất" (sửa / thêm đơn sau giờ chốt) = Trưởng KD (SAL-CON) + COO, bỏ CEO.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> ApproverOverrides = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["crm-order"] = ["SAL-CON", "COO"],
    };

    public static readonly IReadOnlyList<ExtraApproval> ExtraApprovals =
    [
        new("scm-po-director", "Đơn mua hàng (Giám đốc)", "P4", ["CEO"]),
        new("scm-pr-l1", "Đề nghị mua hàng (cấp 1 – Trưởng bộ phận Mua hàng)", "P4", ["OPS-SCM"]),
        new("scm-pr-l2", "Đề nghị mua hàng (cấp 2 – Giám đốc vận hành)", "P4", ["COO"]),
        new("scm-stockcount-l2", "Phiếu kiểm kê (cấp 2 – Kế toán)", "P4", ["FIN-ACC"]),
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

    /// <summary>Vai trò → (cột ma trận → chuỗi quyền gốc, vd. "V E X").</summary>
    public static List<(string Role, string Label, Dictionary<string, string> Rights)> LoadMatrix(string root)
    {
        var result = new List<(string, string, Dictionary<string, string>)>();
        foreach (string line in OrgSection(root, "## 2."))
        {
            if (!line.StartsWith("| ", StringComparison.Ordinal) || line.StartsWith("| Nhóm màn hình", StringComparison.Ordinal) || line.StartsWith("|---", StringComparison.Ordinal))
            {
                continue;
            }
            string[] cells = line.Trim().Trim('|').Split('|').Select(c => c.Trim()).ToArray();
            if (cells.Length != 1 + MatrixColumns.Length)
            {
                continue;
            }
            var rights = new Dictionary<string, string>();
            for (int i = 0; i < MatrixColumns.Length; i++)
            {
                rights[MatrixColumns[i]] = cells[i + 1];
            }
            foreach (string role in RoleCodes(cells[0]))
            {
                result.Add((role, cells[0], rights));
            }
        }
        return result;
    }

    /// <summary>"SAL-SALES / SAL-CON" → 2 nhóm; "COO (trưởng OPS)" → COO; "QA (4.1–4.5)" → QA; "FIN-IT (admin)" → FIN-IT.</summary>
    private static IEnumerable<string> RoleCodes(string label)
    {
        string noParen = Regex.Replace(label, @"\(.*?\)", string.Empty);
        return noParen.Split('/').Select(s => s.Trim()).Where(s => s.Length > 0);
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

    /// <summary>Chuỗi quyền gốc → cờ UserRole. V = xem; E = thêm/sửa (+ xóa, nhập – PROPOSED); X = in/xuất. A (duyệt) cấu hình riêng.</summary>
    public static (bool Access, bool Add, bool Edit, bool Delete, bool Print, bool Export, bool Import, bool Approve) Flags(string raw)
    {
        string letters = Regex.Replace(raw, @"\(.*?\)", " ");
        var tokens = letters.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        bool v = tokens.Contains("V"), e = tokens.Contains("E"), x = tokens.Contains("X"), a = tokens.Contains("A");
        return (v || e || x || a, e, e, e, x, x, e, a);
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
            string screen = screens.FirstOrDefault(s => string.Equals(s.Table, t.Name, StringComparison.OrdinalIgnoreCase))?.Id ?? FallbackScreen;
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
        ("#/crm-order", "Đơn đặt suất ăn", "Đơn đặt suất"),
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
        ("#/prod-workstation", "Trạm làm việc bếp", "Trạm bếp"),
        ("#/prod-plan", "Kế hoạch sản xuất theo ngày/ca", "Kế hoạch sản xuất"),
        ("#/prod-plan-line", "Dòng kế hoạch theo món", "Chi tiết kế hoạch sản xuất"),
        ("#/prod-batch", "Bảng điều độ mẻ (ca × dây chuyền)", "Mẻ nấu"),
        ("#/prod-output", "Ghi nhận sản lượng", "Sản lượng"),
        ("#/cost-actual", "Giá thành thực tế theo mẻ/lệnh", "Giá thành thực tế"),
        ("#/cost-meal", "Giá thành mỗi suất & Margin", "Giá vốn suất ăn"),
        ("#/prod-waste", "Ghi nhận hao hụt", "Hao hụt"),
        ("#/group/06", "QC & An toàn thực phẩm", "Chất lượng & ATTP"),
        ("#/qc-checklist", "Bộ checklist ATTP", "Bộ kiểm tra ATTP"),
        ("#/qc-checklist-item", "Tiêu chí checklist", "Tiêu chí kiểm tra"),
        ("#/qc-inspection", "Phiếu QC đầu vào", "Kiểm tra hàng nhập"),
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

    public static readonly string[] HiddenMenuLinks = ["#/group/11", "#/dbx-explorer", "#/dbx-gap", .. Menu4HiddenLinks, .. Biz3HiddenLinks];

    /// <summary>Tên hiển thị của mục menu theo MENU-2 (không có trong bảng → giữ tên catalog).</summary>
    public static string MenuName(string link, string name) => MenuRenames.FirstOrDefault(r => r.Link == link).New ?? name;

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
        sb.AppendLine("COMMIT;");
        return sb.ToString();
    }

    public static string UserRoleSeed(List<Screen> screens, List<(string Role, string Label, Dictionary<string, string> Rights)> matrix)
    {
        var sb = new StringBuilder(SeedHeader);
        sb.AppendLine("BEGIN TRAN;");
        sb.AppendLine("-- UserGroup: Name = mã vai trò (bảng UserGroup không có cột Code). Ma trận quyền: PROPOSED (D-25), anh duyệt khi UAT-1.");
        sb.AppendLine("-- CLAUDE.md 4.3: chỉ THÊM dòng chưa có (nhóm × menu), KHÔNG ghi đè quyền admin đã chỉnh trên ma trận.");
        sb.AppendLine("-- Quy đổi: V → AllowAccess; E → Add + Edit + Delete + Import (PROPOSED); X → Print + Export. A (duyệt) → AllowAccess + AllowEdit trên mục \"Duyệt …\" (D-31), chỉ thêm khi chưa có – không ghi đè chỉnh sửa của admin.");
        sb.AppendLine("-- D-69: ngoại lệ theo từng màn (Seeds.RightOverrides – đóng gói cho OPS-KIT, FIN-HR sửa hồ sơ nhân viên). D-71: màn tích hợp int-* chỉ FIN-IT (CEO / COO xem int-outbound) – Seeds.RestrictedScreens.");
        foreach (var (role, _, _) in matrix)
        {
            sb.AppendLine($"IF NOT EXISTS (SELECT 1 FROM [dbo].[UserGroup] WHERE Name = {Sql(role)} AND IsDeleted = 0)");
            sb.AppendLine($"    INSERT INTO [dbo].[UserGroup] (Id, Name, IsDefault, CreatedDate, CreatedBy, IsDeleted) VALUES (NEWID(), {Sql(role)}, 0, @Now, @SeedBy, 0);");
        }
        sb.AppendLine();
        sb.AppendLine("DECLARE @R TABLE (GroupName NVARCHAR(100), MetaLink NVARCHAR(200), A BIT, Ad BIT, E BIT, D BIT, P BIT, X BIT, I BIT);");
        var rows = new List<string>();
        foreach (var (role, _, rights) in matrix)
        {
            foreach (var s in screens)
            {
                var f = Flags(rights[GroupToMatrix[s.Group]]);
                if (OverrideOf(s.Id, role) is { } o)
                {
                    f = (true, f.Add || o.Add, f.Edit || o.Edit, f.Delete, f.Print, f.Export, f.Import, f.Approve);   // D-69: chỉ nâng
                }
                if (RestrictedScreens.TryGetValue(s.Id, out var allowed))   // D-71: màn giới hạn – đặt đúng quyền, nhóm khác = 0
                {
                    f = allowed.TryGetValue(role, out var r) ? (true, r.Add, r.Edit, r.Delete, r.Export, r.Export, false, false) : (false, false, false, false, false, false, false, false);
                }
                string b(bool v) => v ? "1" : "0";
                rows.Add($"({Sql(role)}, {Sql("#/" + s.Id)}, {b(f.Access)}, {b(f.Add)}, {b(f.Edit)}, {b(f.Delete)}, {b(f.Print)}, {b(f.Export)}, {b(f.Import)})");
            }
        }
        foreach (var chunk in rows.Chunk(500))
        {
            sb.AppendLine("INSERT INTO @R VALUES");
            sb.AppendLine("    " + string.Join(",\n    ", chunk) + ";");
        }
        sb.AppendLine("""


            INSERT INTO [dbo].[UserRole] (Id, UserGroupId, ObjectMenuId, AllowAccess, AllowAdd, AllowEdit, AllowDelete, AllowPrint, AllowExport, AllowImport, CreatedDate, CreatedBy, IsDeleted)
            SELECT NEWID(), g.Id, m.Id, r.A, r.Ad, r.E, r.D, r.P, r.X, r.I, @Now, @SeedBy, 0
            FROM @R r
            JOIN [dbo].[UserGroup] g ON g.Name = r.GroupName AND g.IsDeleted = 0
            JOIN [dbo].[ObjectMenu] m ON m.MetaLink = r.MetaLink AND m.IsDeleted = 0
            WHERE NOT EXISTS (SELECT 1 FROM [dbo].[UserRole] ur WHERE ur.UserGroupId = g.Id AND ur.ObjectMenuId = m.Id AND ur.IsDeleted = 0);
            """);
        var approvals = matrix.SelectMany(m => screens.Where(s => s.Approvable && !ApproverOverrides.ContainsKey(s.Id) && Flags(m.Rights[GroupToMatrix[s.Group]]).Approve)
            .Select(s => $"({Sql(m.Role)}, {Sql(ApprovalLinkPrefix + s.Id)})")).ToList();
        approvals.AddRange(ApproverOverrides.SelectMany(kv => kv.Value.Select(r => $"({Sql(r)}, {Sql(ApprovalLinkPrefix + kv.Key)})")));
        approvals.AddRange(ExtraApprovals.SelectMany(x => x.Roles.Select(r => $"({Sql(r)}, {Sql(ApprovalLinkPrefix + x.Id)})")));
        if (approvals.Count > 0)
        {
            sb.AppendLine("-- D-31: quyền duyệt (cột A) – chỉ thêm khi chưa có dòng.");
            sb.AppendLine("DECLARE @A TABLE (GroupName NVARCHAR(100), MetaLink NVARCHAR(200));");
            sb.AppendLine("INSERT INTO @A VALUES");
            sb.AppendLine("    " + string.Join(",\n    ", approvals) + ";");
            sb.AppendLine("""
                INSERT INTO [dbo].[UserRole] (Id, UserGroupId, ObjectMenuId, AllowAccess, AllowAdd, AllowEdit, AllowDelete, AllowPrint, AllowExport, AllowImport, CreatedDate, CreatedBy, IsDeleted)
                SELECT NEWID(), g.Id, m.Id, 1, 0, 1, 0, 0, 0, 0, @Now, @SeedBy, 0
                FROM @A a
                JOIN [dbo].[UserGroup] g ON g.Name = a.GroupName AND g.IsDeleted = 0
                JOIN [dbo].[ObjectMenu] m ON m.MetaLink = a.MetaLink AND m.IsDeleted = 0
                WHERE NOT EXISTS (SELECT 1 FROM [dbo].[UserRole] ur WHERE ur.UserGroupId = g.Id AND ur.ObjectMenuId = m.Id AND ur.IsDeleted = 0);
                """);
        }
        sb.AppendLine("COMMIT;");
        return sb.ToString();
    }

    /// <summary>Tóm tắt quyền duyệt seed: mục "Duyệt …" → các nhóm (để in ra console khi chạy CodeGen).</summary>
    public static Dictionary<string, List<string>> Approvers(List<Screen> screens, List<(string Role, string Label, Dictionary<string, string> Rights)> matrix)
        => screens.Where(s => s.Approvable).ToDictionary(s => s.Id, s => ApproverOverrides.TryGetValue(s.Id, out var o) ? [.. o] : matrix.Where(m => Flags(m.Rights[GroupToMatrix[s.Group]]).Approve).Select(m => m.Role).ToList())
            .Concat(ExtraApprovals.Select(x => new KeyValuePair<string, List<string>>(x.Id, x.Roles.ToList()))).ToDictionary(kv => kv.Key, kv => kv.Value);

    [GeneratedRegex(@"^\|\s*([A-Z0-9-]+)\s*\|\s*([^|]+?)\s*\|\s*(COMPANY|BOARD|DIVISION|DEPARTMENT|TEAM)\s*\|\s*([^|]*?)\s*\|\s*$")]
    private static partial Regex OrgRow();
}
