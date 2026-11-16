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
    /// T15.1 (D-11, anh chốt 2026-11-16): PO tổng ≥ <c>BusinessRules:PurchaseOrderDirectorApprovalAmount</c> do Giám đốc duyệt → mục
    /// <c>#/wf-inbox?type=scm-po-director</c>, chỉ seed quyền cho nhóm CEO (admin đổi trên ma trận quyền).
    /// </summary>
    public static readonly IReadOnlyList<ExtraApproval> ExtraApprovals = [new("scm-po-director", "Đơn mua hàng (Giám đốc)", "P4", ["CEO"])];

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
            sb.AppendLine($"    INSERT INTO [dbo].[ObjectMenu] (Id, Name, ShortName, MetaLink, ParentId, LevelShow, OrderShow, IsActive, IsDefault, CreatedDate, CreatedBy, IsDeleted) VALUES (NEWID(), {Sql(group)}, {Sql(GroupToMatrix[group])}, {Sql(groupLink)}, NULL, 1, {g * 100}, 1, 0, @Now, @SeedBy, 0);");
            int i = 0;
            foreach (var s in screens.Where(s => s.Group == group))
            {
                i++;
                string link = "#/" + s.Id;
                string parent = $"(SELECT TOP (1) Id FROM [dbo].[ObjectMenu] WHERE MetaLink = {Sql(groupLink)} AND IsDeleted = 0)";
                int order = g * 100 + i;
                sb.AppendLine($"IF NOT EXISTS (SELECT 1 FROM [dbo].[ObjectMenu] WHERE MetaLink = {Sql(link)} AND IsDeleted = 0)");
                sb.AppendLine($"    INSERT INTO [dbo].[ObjectMenu] (Id, Name, ShortName, MetaLink, ParentId, LevelShow, OrderShow, IsActive, IsDefault, CreatedDate, CreatedBy, IsDeleted) VALUES (NEWID(), {Sql(s.Name)}, {Sql(s.Phase)}, {Sql(link)}, {parent}, 2, {order}, 1, 0, @Now, @SeedBy, 0);");
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
            sb.AppendLine($"    INSERT INTO [dbo].[ObjectMenu] (Id, Name, ShortName, MetaLink, ParentId, LevelShow, OrderShow, IsActive, IsDefault, CreatedDate, CreatedBy, IsDeleted) VALUES (NEWID(), {Sql("Duyệt – " + s.Name)}, {Sql(s.Phase)}, {Sql(link)}, {parent}, 2, {9900 + a}, 1, 0, @Now, @SeedBy, 0);");
        }
        sb.AppendLine("-- T15.1: mục quyền duyệt không gắn màn hình catalog (PO ≥ ngưỡng Giám đốc duyệt – D-11).");
        foreach (var x in ExtraApprovals)
        {
            a++;
            string link = ApprovalLinkPrefix + x.Id;
            string parent = $"(SELECT TOP (1) Id FROM [dbo].[ObjectMenu] WHERE MetaLink = {Sql(ApprovalGroupLink)} AND IsDeleted = 0)";
            sb.AppendLine($"IF NOT EXISTS (SELECT 1 FROM [dbo].[ObjectMenu] WHERE MetaLink = {Sql(link)} AND IsDeleted = 0)");
            sb.AppendLine($"    INSERT INTO [dbo].[ObjectMenu] (Id, Name, ShortName, MetaLink, ParentId, LevelShow, OrderShow, IsActive, IsDefault, CreatedDate, CreatedBy, IsDeleted) VALUES (NEWID(), {Sql("Duyệt – " + x.Name)}, {Sql(x.Phase)}, {Sql(link)}, {parent}, 2, {9900 + a}, 1, 0, @Now, @SeedBy, 0);");
        }
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
        var approvals = matrix.SelectMany(m => screens.Where(s => s.Approvable && Flags(m.Rights[GroupToMatrix[s.Group]]).Approve)
            .Select(s => $"({Sql(m.Role)}, {Sql(ApprovalLinkPrefix + s.Id)})")).ToList();
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
        => screens.Where(s => s.Approvable).ToDictionary(s => s.Id, s => matrix.Where(m => Flags(m.Rights[GroupToMatrix[s.Group]]).Approve).Select(m => m.Role).ToList())
            .Concat(ExtraApprovals.Select(x => new KeyValuePair<string, List<string>>(x.Id, x.Roles.ToList()))).ToDictionary(kv => kv.Key, kv => kv.Value);

    [GeneratedRegex(@"^\|\s*([A-Z0-9-]+)\s*\|\s*([^|]+?)\s*\|\s*(COMPANY|BOARD|DIVISION|DEPARTMENT|TEAM)\s*\|\s*([^|]*?)\s*\|\s*$")]
    private static partial Regex OrgRow();
}
