using System.Text;

namespace AppDelicare.CodeGen;

/// <summary>
/// MENU-5 (D-79, anh chốt 2026-11-23): NGUỒN DUY NHẤT của phân quyền theo TỪNG MÀN + bố cục menu theo vai trò.
/// Thay ma trận theo nhóm màn của ORG_STRUCTURE §2 (GAP-PERM-02) và gộp các ngoại lệ cũ (RightOverrides D-69, RestrictedScreens D-71,
/// TechScreens D-78 (6), ApproverOverrides BIZ-2.1, nhóm duyệt của ExtraApprovals) vào một chỗ.
/// Sinh: <c>DB/seed/ObjectMenu_seed.sql</c> (khối MENU-5), <c>DB/seed/UserRole_seed.sql</c> (chỉ thêm dòng thiếu),
/// <c>DB/scripts/D79_menu_regroup.sql</c>, <c>DB/scripts/D79_permission_matrix.sql</c> (DB đang chạy – DryRun bắt buộc).
/// Không thêm bảng / cột: nhóm quyền (UserGroup.Name = mã), mục menu, quyền đều là dữ liệu.
/// </summary>
public static partial class Seeds
{
    // ------------------------------------------------------------------ cờ quyền
    /// <summary>Cờ UserRole: A = AllowAccess, Ad = Add, E = Edit, D = Delete, P = Print, X = Export, I = Import.</summary>
    public sealed record Right(bool A, bool Ad, bool E, bool D, bool P, bool X, bool I)
    {
        public static readonly Right None = new(false, false, false, false, false, false, false);
        public Right Or(Right o) => new(A || o.A, Ad || o.Ad, E || o.E, D || o.D, P || o.P, X || o.X, I || o.I);
        public bool Any => A || Ad || E || D || P || X || I;
        public string Bits => string.Concat(new[] { A, Ad, E, D, P, X, I }.Select(b => b ? '1' : '0'));
    }

    /// <summary>X = Xem.</summary>
    private static readonly Right ViewR = new(true, false, false, false, false, false, false);
    /// <summary>S = Thêm + Sửa.</summary>
    private static readonly Right EditR = new(true, true, true, false, false, false, false);
    /// <summary>Q = Thêm + Sửa + Xóa + Nhập (nhóm chủ dữ liệu – D-79 a2).</summary>
    private static readonly Right OwnR = new(true, true, true, true, false, false, true);
    /// <summary>(IX) = In + Xuất.</summary>
    private static readonly Right PrintR = new(true, false, false, false, true, true, false);
    /// <summary>Quyền D-71 / D-78 (6) giữ nguyên cho FIN-IT trên màn tích hợp / kỹ thuật: Thêm + Sửa + In + Xuất, không Xóa / Nhập.</summary>
    private static readonly Right TechAdminR = new(true, true, true, false, true, true, false);
    /// <summary>Quyền Duyệt (D-31): AllowAccess + AllowEdit trên mục <c>#/wf-inbox?type=&lt;id&gt;</c>.</summary>
    private static readonly Right ApproveR = new(true, false, true, false, false, false, false);

    // ------------------------------------------------------------------ nhóm quyền
    /// <summary>16 nhóm chuẩn cũ (ORG_STRUCTURE §2) + 3 nhóm mới D-79 (b). Thứ tự = thứ tự in báo cáo.</summary>
    public static readonly string[] StandardRoles =
    [
        "CEO", "CEO-PMO", "COO", "OPS-PLAN", "OPS-SCM", "OPS-WH", "OPS-SCM-HEAD", "OPS-KIT", "OPS-LOG", "OPS-POS", "OPS-MNT",
        "SAL-SALES", "SAL-CON", "SAL-HEAD", "SAL-CS", "FIN-ACC", "FIN-HR", "FIN-IT", "QA",
    ];

    /// <summary>D-79 (b): 3 nhóm mới – admin chuyển người sang trên màn Tài khoản; nhóm cũ giữ nguyên.</summary>
    public static readonly string[] NewRoles = ["SAL-HEAD", "OPS-SCM-HEAD", "OPS-WH"];

    // ------------------------------------------------------------------ bố cục menu (MENU-5.4)
    /// <summary>Nhóm menu cấp 1: MetaLink, tên, tên seed cũ (đổi tên chỉ khi DB đang mang 1 trong các tên này), thứ tự nhóm, ShortName, các mục cấp 2 theo thứ tự.</summary>
    public sealed record MenuGroup(string Link, string Name, string[] OldNames, int Order, string ShortName, string[] Items);

    public const string MyWorkGroupLink = "#/group/00";

    public static readonly MenuGroup[] Menu5Groups =
    [
        new(MyWorkGroupLink, "Việc của tôi", [], 10, "my", ["hr-my"]),
        new(ApprovalGroupLink, "Phê duyệt", [], 20, "approve",
        [
            "wf-inbox?type=wf-inbox", "wf-inbox?type=crm-contract", "wf-inbox?type=crm-price-policy", "wf-inbox?type=crm-order", "wf-inbox?type=menu-recipe",
            "wf-inbox?type=menu-menu", "wf-inbox?type=menu-change-request", "wf-inbox?type=scm-pr-l1", "wf-inbox?type=scm-pr-l2", "wf-inbox?type=scm-po",
            "wf-inbox?type=scm-po-director", "wf-inbox?type=scm-stockcount", "wf-inbox?type=scm-stockcount-l2", "wf-inbox?type=scm-adjustment",
            "wf-inbox?type=prod-plan", "wf-inbox?type=qc-inspection", "wf-inbox?type=prod-batch", "wf-inbox?type=scm-lot", "wf-inbox?type=hr-leave",
            "wf-inbox?type=hr-swap", "wf-inbox?type=scm-pr", "wf-inbox?type=adm-user",
        ]),
        new("#/group/01", "Điều hành", ["Tổng quan điều hành"], 100, "kpi", ["dash-exec", "kpi-daily", "kpi-monthly"]),
        new("#/group/02", "Kinh doanh", ["Kinh doanh & Đơn hàng"], 200, "sales",
            ["crm-customer", "crm-contract", "crm-price-policy", "crm-order-week", "crm-order", "crm-forecast", "crm-reconciliation", "crm-billing", "log-complaint", "crm-location", "crm-contact"]),
        new("#/group/03", "Thực đơn & Món ăn", [], 300, "menu", ["menu-menu", "menu-assignment", "menu-change-request", "menu-recipe", "menu-dish", "menu-dish-cat"]),
        new("#/group/04", "Mua hàng", ["Mua hàng & Kho"], 400, "scm",
            ["scm-requirement", "scm-pr", "scm-po", "scm-supplier", "scm-supplier-material", "scm-ingredient", "scm-ingredient-cat", "scm-uom", "scm-uom-conversion"]),
        new("#/group/04b", "Kho", [], 450, "wh",
            ["scm-gr", "scm-issue", "scm-inv-balance", "scm-lot", "scm-inv-txn", "scm-reservation", "scm-stockcount", "scm-consumption-var", "scm-warehouse", "scm-zone", "scm-location", "scm-inv-policy"]),
        new("#/group/05", "Sản xuất", ["Sản xuất & Giá thành"], 500, "production", ["prod-plan", "prod-order", "prod-batch", "prod-output", "prod-waste", "prod-line", "prod-workstation"]),
        new("#/group/05b", "Giá thành", [], 550, "cost", ["cost-standard", "cost-actual", "cost-meal"]),
        new("#/group/06", "Chất lượng & ATTP", ["QC & An toàn thực phẩm"], 600, "qc",
            ["qc-inspection", "qc-checklist-result", "qc-temperature", "qc-cleaning", "qc-cleaning-result", "qc-supplier-issue", "qc-checklist"]),
        new("#/group/07", "Đóng gói & Giao hàng", ["Đóng gói & Giao nhận"], 700, "logistics",
        [
            "log-packing-order", "log-packing-result", "log-delivery-order", "log-route", "log-loading", "log-execution", "log-gps", "log-pod",
            "log-container-return", "log-return-inspection", "log-packaging-ledger", "log-vehicle",
        ]),
        // #/wf-inbox (Hộp phê duyệt cũ) giữ dòng – là quyền của API duyệt (xem chi tiết mọi yêu cầu); FE bỏ khỏi sidebar khi có "Tất cả yêu cầu duyệt".
        new("#/group/08", "Nhân sự", ["Nhân sự & Phê duyệt"], 800, "hr", ["adm-employee", "hr-shift-assign", "hr-attendance", "hr-leave", "hr-swap", "hr-shift", "adm-position", "wf-inbox"]),
        new("#/group/10", "Quản trị hệ thống", [], 1000, "admin",
            ["adm-user", "adm-usergroup", "adm-permission", "adm-objectmenu", "adm-orgunit", "adm-company", "sys-import", "adm-reference", "adm-admin-unit", "adm-document"]),
        // bot-van-hanh / bot-dieu-hanh: mục "quyền vào bot" (D-62, seed Integration_Telegram) – chỉ sắp chỗ, quyền KHÔNG do ma trận này quản (FE không hiện trên sidebar).
        new("#/group/09", "Nhật ký & Tích hợp", ["Tích hợp & Chỉ số", "Tích hợp & KPI"], 1050, "kpi",
        [
            "adm-job", "adm-audit", "adm-access-log", "adm-merge-conflict", "int-channel", "int-bot", "int-identity", "int-inbound", "int-outbound", "int-log",
            "kpi-definition", "kpi-refresh", "bot-van-hanh", "bot-dieu-hanh",
        ]),
    ];

    /// <summary>Mục do seed / màn khác quản quyền – ma trận không đặt cờ (D-62 quyền vào bot).</summary>
    public static readonly string[] MatrixSkipLinks = ["#/bot-van-hanh", "#/bot-dieu-hanh"];

    /// <summary>Mục mới MENU-5 (chưa có trong seed cũ): link → (tên, ShortName/phase).</summary>
    public static readonly IReadOnlyDictionary<string, (string Name, string Phase)> Menu5NewItems = new Dictionary<string, (string, string)>
    {
        ["hr-my"] = ("Việc của tôi", "P8"),
        ["adm-job"] = ("Job tự động", "P0"),
        ["sys-import"] = ("Nhập dữ liệu gốc (Excel)", "P0"),
        ["wf-inbox?type=prod-batch"] = ("Mẻ nấu", "P5"),
        ["wf-inbox?type=scm-lot"] = ("Giữ / thả lô", "P4"),
        // HR-ACC-1.6 (D-82 f–h): cấp / đổi nhóm / mở lại tài khoản – người duyệt FIN-IT.
        ["wf-inbox?type=adm-user"] = ("Tài khoản", "P0"),
    };

    /// <summary>Đổi tên MENU-5 (mục duyệt rõ cấp): link → (tên seed cũ – có thể nhiều biến thể, tên mới). Chỉ đổi khi DB đang mang tên cũ.</summary>
    public static readonly IReadOnlyDictionary<string, (string[] Old, string New)> Menu5Renames = new Dictionary<string, (string[], string)>
    {
        ["wf-inbox?type=crm-order"] = (["Đơn đặt suất", "Đơn đặt suất ăn"], "Đơn đặt suất (sửa sau chốt)"),
        ["wf-inbox?type=scm-pr-l1"] = (["Đề nghị mua hàng (cấp 1 – Trưởng bộ phận Mua hàng)", "Đề nghị mua hàng (cấp 1 - Trưởng bộ phận Mua hàng)"], "Đề nghị mua – cấp 1"),
        ["wf-inbox?type=scm-pr-l2"] = (["Đề nghị mua hàng (cấp 2 – Giám đốc vận hành)", "Đề nghị mua hàng (cấp 2 - Giám đốc vận hành)"], "Đề nghị mua – cấp 2"),
        ["wf-inbox?type=scm-po-director"] = (["Đơn mua hàng (Giám đốc)"], "Đơn mua hàng ≥ 50 triệu"),
        ["wf-inbox?type=scm-stockcount"] = (["Phiếu kiểm kê"], "Phiếu kiểm kê – cấp 1"),
        ["wf-inbox?type=scm-stockcount-l2"] = (["Phiếu kiểm kê (cấp 2 – Kế toán)", "Phiếu kiểm kê (cấp 2 - Kế toán)"], "Phiếu kiểm kê – cấp 2"),
    };

    /// <summary>
    /// MENU-5: ẩn (IsActive = 0) – <c>crm-contact</c> (bảng con, nhập ở Khách hàng 360 – quyền API theo màn cha), mục duyệt không loại duyệt nào dùng:
    /// <c>scm-pr</c> cũ (D-52 a) và <c>crm-price-policy</c> (MENU-5.3: không có IApprovalHandler / action Duyệt nào dùng – ghi PROGRESS).
    /// </summary>
    public static readonly string[] Menu5HiddenLinks = ["#/crm-contact", "#/wf-inbox?type=scm-pr", "#/wf-inbox?type=crm-price-policy"];

    /// <summary>MENU-5.2: mở lại mục duyệt "Kiểm tra hàng nhập" (BIZ-3.7 đã ẩn) – nay là quyền Kết luận QC đầu vào.</summary>
    public static readonly string[] Menu5ShownLinks = ["#/wf-inbox?type=qc-inspection"];

    // ------------------------------------------------------------------ tập màn
    private static readonly string[] Kpi = ["dash-exec", "kpi-daily", "kpi-monthly"];
    private static readonly HashSet<string> NonBusinessGroups = [MyWorkGroupLink, ApprovalGroupLink, "#/group/10", "#/group/09"];

    /// <summary>"Toàn bộ màn nghiệp vụ": mục cấp 2 của nhóm Điều hành → Nhân sự, trừ Hộp phê duyệt cũ (#/wf-inbox – giới hạn riêng) và mục đã ẩn.</summary>
    private static string[] Business() => [.. Menu5Groups.Where(g => !NonBusinessGroups.Contains(g.Link)).SelectMany(g => g.Items)
        .Where(i => i != "wf-inbox" && !Menu5HiddenLinks.Contains("#/" + i))];

    private static string[] AdminScreens() => Menu5Groups.First(g => g.Link == "#/group/10").Items;

    /// <summary>Màn tích hợp / kỹ thuật (D-71, D-78 (6)) – chỉ FIN-IT.</summary>
    private static readonly string[] IntegrationTechScreens = ["adm-audit", "adm-merge-conflict", "kpi-definition", "kpi-refresh", "int-channel", "int-bot", "int-identity", "int-inbound", "int-outbound", "int-log"];

    private static string[] L(string s) => s.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    // ------------------------------------------------------------------ ma trận (MENU-5.1)
    /// <summary>Vai trò → màn (link không '#/') → cờ. Màn không có = không quyền. Các phần của cùng màn cộng dồn (OR).</summary>
    /// <remarks>Tính lười: các trường static nằm ở nhiều file partial – không phụ thuộc thứ tự khởi tạo.</remarks>
    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, Right>> ScreenMatrix => _screenMatrix ??= BuildMatrix();
    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, Right>>? _screenMatrix;

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, Right>> BuildMatrix()
    {
        var m = StandardRoles.ToDictionary(r => r, _ => new Dictionary<string, Right>());
        void G(string role, Right right, IEnumerable<string> screens)
        {
            foreach (string s in screens)
            {
                m[role][s] = m[role].TryGetValue(s, out var cur) ? cur.Or(right) : right;
            }
        }
        string[] business = Business();
        var ix = PrintR;

        // Ban điều hành – chỉ xem (D-79 c). Mọi nhóm xem "Việc của tôi".
        G("CEO", ViewR, business);
        G("CEO", ix, [.. Kpi, "crm-billing", "cost-meal"]);
        G("CEO", ViewR, ["adm-company", "adm-orgunit", "adm-employee", "int-outbound", "wf-inbox"]);
        G("CEO-PMO", ViewR, business);
        G("CEO-PMO", ix, Kpi);
        G("CEO-PMO", ViewR, ["adm-orgunit", "wf-inbox"]);
        G("COO", ViewR, business);
        G("COO", ix, Kpi);
        G("COO", ViewR, ["int-outbound", "wf-inbox"]);

        G("OPS-PLAN", OwnR, L("menu-dish-cat menu-dish menu-recipe menu-menu menu-assignment menu-change-request prod-line prod-workstation"));
        G("OPS-PLAN", EditR, L("prod-plan prod-order cost-standard scm-ingredient"));
        G("OPS-PLAN", ViewR, L("crm-customer crm-location crm-price-policy crm-forecast crm-order crm-order-week prod-batch prod-output prod-waste scm-requirement scm-pr scm-inv-balance scm-lot"));
        G("OPS-PLAN", ix, L("cost-actual cost-meal"));
        G("OPS-PLAN", ViewR, [.. L("qc-inspection qc-checklist-result qc-temperature qc-cleaning qc-cleaning-result qc-supplier-issue qc-checklist"), .. Kpi]);

        G("OPS-SCM", OwnR, L("scm-supplier scm-supplier-material scm-ingredient scm-ingredient-cat scm-uom scm-uom-conversion"));
        G("OPS-SCM", EditR.Or(ix), L("scm-requirement scm-pr scm-po"));
        G("OPS-SCM", ViewR, L("scm-gr scm-inv-balance scm-lot scm-inv-txn scm-issue scm-warehouse qc-supplier-issue qc-inspection prod-plan menu-recipe menu-menu"));

        G("OPS-WH", OwnR, L("scm-warehouse scm-zone scm-location scm-inv-policy"));
        G("OPS-WH", EditR.Or(ix), L("scm-gr scm-issue scm-reservation scm-inv-txn scm-stockcount scm-lot"));
        G("OPS-WH", ViewR, L("scm-po scm-supplier scm-ingredient scm-inv-balance scm-consumption-var prod-plan prod-order"));

        foreach (string src in new[] { "OPS-SCM", "OPS-WH" })
        {
            foreach (var kv in m[src])
            {
                G("OPS-SCM-HEAD", kv.Value, [kv.Key]);
            }
        }
        G("OPS-SCM-HEAD", ViewR, Kpi);

        G("OPS-KIT", EditR, L("prod-batch prod-waste log-packing-order log-packing-result qc-checklist-result qc-temperature qc-cleaning qc-cleaning-result log-return-inspection"));
        G("OPS-KIT", ViewR, L("prod-plan prod-order prod-output prod-line prod-workstation menu-dish menu-recipe menu-menu scm-issue scm-inv-balance qc-checklist hr-shift hr-shift-assign hr-attendance hr-leave hr-swap"));

        G("OPS-LOG", OwnR, ["log-vehicle"]);
        G("OPS-LOG", EditR, L("log-delivery-order log-route log-loading log-execution log-pod log-container-return"));
        G("OPS-LOG", ViewR, L("log-packing-order log-packing-result log-gps log-packaging-ledger crm-location crm-order prod-plan qc-temperature"));

        G("OPS-POS", EditR, L("crm-reconciliation log-pod log-container-return log-complaint"));
        G("OPS-POS", ViewR, L("crm-customer crm-location crm-order crm-order-week menu-menu log-execution log-packaging-ledger"));

        G("OPS-MNT", EditR, L("prod-line prod-workstation"));
        G("OPS-MNT", ViewR, L("qc-temperature prod-batch"));

        string[] salesView = L("crm-reconciliation menu-dish menu-menu menu-assignment log-complaint log-delivery-order log-execution log-pod log-packaging-ledger");
        G("SAL-SALES", EditR, L("crm-customer crm-location crm-price-policy crm-contract crm-forecast crm-order crm-order-week menu-change-request"));
        G("SAL-SALES", ViewR, salesView);
        G("SAL-SALES", ix, ["crm-billing"]);

        foreach (string role in new[] { "SAL-CON", "SAL-HEAD" })
        {
            G(role, OwnR.Or(ix), L("crm-customer crm-location crm-price-policy crm-contract crm-forecast crm-order crm-order-week"));
            G(role, EditR, L("crm-reconciliation menu-change-request"));
            G(role, ViewR, salesView);
            G(role, ix, ["crm-billing"]);
        }
        // SAL-HEAD = SAL-CON + tạo đơn bù (MENU-5.2: Thêm Phản ánh + Thêm Đơn đặt suất) + xem giá vốn, giá thành chuẩn, chỉ số.
        G("SAL-HEAD", EditR, ["log-complaint"]);
        G("SAL-HEAD", ViewR, [.. L("cost-meal cost-standard"), .. Kpi]);

        G("SAL-CS", EditR, L("log-complaint menu-change-request"));
        G("SAL-CS", ViewR, L("crm-customer crm-location crm-order menu-menu menu-dish log-execution log-pod crm-reconciliation"));

        G("FIN-ACC", ix, [.. L("crm-customer crm-contract crm-price-policy crm-order crm-reconciliation crm-billing scm-po scm-gr scm-issue scm-inv-balance scm-inv-txn scm-lot scm-stockcount scm-consumption-var cost-standard cost-actual cost-meal prod-waste log-packaging-ledger log-pod hr-attendance"), .. Kpi]);
        G("FIN-ACC", ViewR, L("scm-supplier scm-supplier-material"));

        G("FIN-HR", OwnR, L("hr-shift adm-position"));
        G("FIN-HR", EditR.Or(ix), L("adm-employee hr-shift-assign hr-attendance hr-leave hr-swap"));
        G("FIN-HR", ViewR, L("adm-orgunit int-identity"));

        // FIN-IT: Q (IX) toàn bộ Quản trị + Job tự động + nhật ký truy cập; màn tích hợp / kỹ thuật giữ quyền D-71 / D-78 (6); xem mọi màn nghiệp vụ.
        G("FIN-IT", OwnR.Or(ix), [.. AdminScreens(), "adm-job", "adm-access-log"]);
        G("FIN-IT", TechAdminR, IntegrationTechScreens);
        G("FIN-IT", ViewR, [.. business, "wf-inbox"]);

        G("QA", OwnR, L("qc-checklist qc-supplier-issue"));
        G("QA", EditR.Or(ix), L("qc-inspection qc-checklist-result qc-temperature qc-cleaning qc-cleaning-result log-return-inspection log-complaint"));
        G("QA", ViewR, [.. L("scm-supplier scm-ingredient scm-gr scm-lot scm-inv-balance prod-batch prod-output prod-plan menu-dish menu-recipe menu-menu log-delivery-order log-execution"), .. Kpi]);

        foreach (string role in StandardRoles)
        {
            G(role, ViewR, ["hr-my"]);
        }

        // Quyền duyệt – D-79 (d). "Tất cả yêu cầu duyệt": CEO / CEO-PMO / COO chỉ Xem (không làm người có nó duyệt được mọi loại).
        foreach (var (screen, roles) in Approvers5)
        {
            foreach (string role in roles)
            {
                G(role, ApproveR, ["wf-inbox?type=" + screen]);
            }
        }
        foreach (string role in new[] { "CEO", "CEO-PMO", "COO" })
        {
            G(role, ViewR, ["wf-inbox?type=wf-inbox"]);
        }
        return m.ToDictionary(kv => kv.Key, kv => (IReadOnlyDictionary<string, Right>)kv.Value);
    }

    /// <summary>D-79 (d): mục duyệt → nhóm duyệt (AllowAccess + AllowEdit). <c>crm-price-policy</c> giữ CEO cho khi có loại duyệt (mục đang ẩn).</summary>
    public static readonly (string Screen, string[] Roles)[] Approvers5 =
    [
        ("crm-contract", ["CEO"]), ("crm-price-policy", ["CEO"]), ("scm-po-director", ["CEO"]),
        ("crm-order", ["SAL-HEAD", "COO"]),
        ("menu-recipe", ["COO"]), ("menu-menu", ["COO"]), ("menu-change-request", ["COO"]), ("prod-plan", ["COO"]), ("scm-adjustment", ["COO"]), ("scm-po", ["COO"]),
        ("scm-pr-l1", ["OPS-SCM-HEAD"]), ("scm-pr-l2", ["COO"]),
        ("scm-stockcount", ["OPS-SCM-HEAD", "COO"]), ("scm-stockcount-l2", ["FIN-ACC"]),
        ("hr-leave", ["FIN-HR", "COO"]), ("hr-swap", ["FIN-HR", "COO"]),
        ("qc-inspection", ["QA"]), ("prod-batch", ["QA"]), ("scm-lot", ["QA"]),
        ("adm-user", ["FIN-IT"]), // HR-ACC-1.6 (D-82 f)
    ];

    /// <summary>Mọi mục cấp 2 mà ma trận quản (link '#/…'), theo thứ tự menu – trừ mục quyền vào bot.</summary>
    public static IEnumerable<string> MatrixLinks() => Menu5Groups.SelectMany(g => g.Items).Select(i => "#/" + i).Where(l => !MatrixSkipLinks.Contains(l));

    public static Right RightOf(string role, string link)
        => ScreenMatrix.TryGetValue(role, out var screens) && screens.TryGetValue(link.StartsWith("#/", StringComparison.Ordinal) ? link[2..] : link, out var r) ? r : Right.None;

    // ------------------------------------------------------------------ SQL dùng chung
    /// <summary>Dòng @R (nhóm, mục, cờ) cho mọi nhóm chuẩn × mục ma trận. <paramref name="nonZeroOnly"/> = chỉ dòng có quyền.</summary>
    private static List<string> MatrixRows(bool nonZeroOnly)
    {
        var rows = new List<string>();
        foreach (string role in StandardRoles)
        {
            foreach (string link in MatrixLinks())
            {
                var r = RightOf(role, link);
                if (nonZeroOnly && !r.Any)
                {
                    continue;
                }
                string b(bool v) => v ? "1" : "0";
                rows.Add($"({Sql(role)}, {Sql(link)}, {b(r.A)}, {b(r.Ad)}, {b(r.E)}, {b(r.D)}, {b(r.P)}, {b(r.X)}, {b(r.I)})");
            }
        }
        return rows;
    }

    private static void AppendValues(StringBuilder sb, string table, List<string> rows)
    {
        foreach (var chunk in rows.Chunk(500))
        {
            sb.AppendLine($"INSERT INTO {table} VALUES");
            sb.AppendLine("    " + string.Join(",\n    ", chunk) + ";");
        }
    }

    /// <summary>Nhóm cha cũ (seed trước MENU-5) của 1 mục: theo nhóm catalog.js, mục duyệt → Phê duyệt, đăng ký suất tuần → Kinh doanh, bot → nhóm 09.</summary>
    private static string? OldParentOf(string item, List<Screen> screens, List<string> menuOrder)
    {
        if (item.StartsWith("wf-inbox?type=", StringComparison.Ordinal))
        {
            return ApprovalGroupLink;
        }
        if (item is "bot-van-hanh" or "bot-dieu-hanh")
        {
            return "#/group/09";
        }
        var s = screens.FirstOrDefault(x => x.Id == item);
        return s == null ? null : $"#/group/{menuOrder.IndexOf(s.Group) + 1:00}";
    }

    /// <summary>Dải thứ tự seed cũ của nhóm: '#/group/NN' → NN01–NN99; Phê duyệt → 9901–9999.</summary>
    private static (int Min, int Max) OldRange(string? groupLink)
    {
        if (groupLink == ApprovalGroupLink)
        {
            return (9901, 9999);
        }
        if (groupLink != null && int.TryParse(groupLink["#/group/".Length..], out int g))
        {
            return (g * 100 + 1, g * 100 + 99);
        }
        return (0, -1);
    }

    /// <summary>Thứ tự nhóm cũ (seed trước MENU-5): '#/group/NN' → NN*100; Phê duyệt → 9900; nhóm mới → không có.</summary>
    private static int OldGroupOrder(string link) => link == ApprovalGroupLink ? 9900 : link == MyWorkGroupLink ? -1 : int.TryParse(link["#/group/".Length..], out int g) ? g * 100 : -1;

    /// <summary>
    /// Bảng kế hoạch MENU-5 (@P): mỗi dòng = 1 mục menu – cấp, nhóm cha cũ + dải thứ tự cũ, tên cũ (|…|), nhóm cha mới, thứ tự mới, tên mới, ShortName, ẩn / mở.
    /// Quy tắc không ghi đè admin: chỉ chuyển nhóm / thứ tự khi mục còn ở nhóm cha cũ với thứ tự trong dải seed cũ; chỉ đổi tên khi tên đang là tên seed cũ.
    /// </summary>
    private static void AppendPlan(StringBuilder sb, List<Screen> screens, List<string> menuOrder)
    {
        sb.AppendLine("DECLARE @P TABLE (Link NVARCHAR(400) PRIMARY KEY, Lvl INT, OldParent NVARCHAR(400) NULL, OldMin INT, OldMax INT, OldNames NVARCHAR(1000),");
        sb.AppendLine("                  NewParent NVARCHAR(400) NULL, NewOrder INT, NewName NVARCHAR(500), ShortName NVARCHAR(100), SetActive INT NULL);");
        var rows = new List<string>();
        string Null(string? v) => v == null ? "NULL" : Sql(v);
        string Active(string link) => Menu5HiddenLinks.Contains(link) ? "0" : Menu5ShownLinks.Contains(link) ? "1" : "NULL";
        foreach (var g in Menu5Groups)
        {
            int old = OldGroupOrder(g.Link);
            rows.Add($"({Sql(g.Link)}, 1, NULL, {old}, {old}, {Sql(string.Join("|", g.OldNames))}, NULL, {g.Order}, {Sql(g.Name)}, {Sql(g.ShortName)}, NULL)");
            int i = 0;
            foreach (string item in g.Items)
            {
                i++;
                string link = "#/" + item;
                int order = g.Link == ApprovalGroupLink ? 9900 + i : g.Order + i;
                string? oldParent = Menu5NewItems.ContainsKey(item) ? null : OldParentOf(item, screens, menuOrder);
                var (min, max) = OldRange(oldParent);
                string name = Menu5NewItems.TryGetValue(item, out var nw) ? nw.Name : Menu5Renames.TryGetValue(item, out var rn) ? rn.New : string.Empty;
                string oldNames = Menu5Renames.TryGetValue(item, out var rn2) ? string.Join("|", rn2.Old) : string.Empty;
                string shortName = Menu5NewItems.TryGetValue(item, out var nw2) ? nw2.Phase : string.Empty;
                rows.Add($"({Sql(link)}, 2, {Null(oldParent)}, {min}, {max}, {Sql(oldNames)}, {Sql(g.Link)}, {order}, {Sql(name)}, {Sql(shortName)}, {Active(link)})");
            }
        }
        AppendValues(sb, "@P", rows);
    }

    /// <summary>Thêm nhóm / mục mới chưa có (theo MetaLink) – dùng chung seed + script.</summary>
    private const string Menu5InsertSql = """
        INSERT INTO [dbo].[ObjectMenu] (Id, Name, ShortName, MetaLink, ParentId, LevelShow, OrderShow, IsActive, IsDefault, CreatedDate, CreatedBy, IsDeleted)
        SELECT NEWID(), p.NewName, p.ShortName, p.Link, NULL, 1, p.NewOrder, 1, 0, @Now, @By, 0
        FROM @P p WHERE p.Lvl = 1 AND NOT EXISTS (SELECT 1 FROM [dbo].[ObjectMenu] m WHERE m.MetaLink = p.Link AND m.IsDeleted = 0);
        INSERT INTO [dbo].[ObjectMenu] (Id, Name, ShortName, MetaLink, ParentId, LevelShow, OrderShow, IsActive, IsDefault, CreatedDate, CreatedBy, IsDeleted)
        SELECT NEWID(), p.NewName, p.ShortName, p.Link, np.Id, 2, p.NewOrder, 1, 0, @Now, @By, 0
        FROM @P p JOIN [dbo].[ObjectMenu] np ON np.MetaLink = p.NewParent AND np.IsDeleted = 0
        WHERE p.Lvl = 2 AND p.OldParent IS NULL AND p.NewName <> N'' AND NOT EXISTS (SELECT 1 FROM [dbo].[ObjectMenu] m WHERE m.MetaLink = p.Link AND m.IsDeleted = 0);
        """;

    /// <summary>Tính thay đổi (@C) cho mục đã có: chuyển nhóm / thứ tự, đổi tên, ẩn / mở – theo quy tắc không ghi đè admin.</summary>
    private const string Menu5DiffSql = """
        DECLARE @C TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY, Link NVARCHAR(400), OldParentId UNIQUEIDENTIFIER NULL, NewParentId UNIQUEIDENTIFIER NULL, OldOrder INT NULL, NewOrder INT NULL,
                          OldName NVARCHAR(500), NewName NVARCHAR(500), OldActive BIT, NewActive BIT, Moved BIT, Kept BIT);
        INSERT INTO @C
        SELECT m.Id, p.Link, m.ParentId,
               CASE WHEN mv.Ok = 1 THEN np.Id ELSE m.ParentId END,
               m.OrderShow,
               CASE WHEN mv.Ok = 1 THEN p.NewOrder ELSE m.OrderShow END,
               m.Name,
               CASE WHEN p.OldNames <> N'' AND CHARINDEX(N'|' + ISNULL(m.Name, N'') + N'|', N'|' + p.OldNames + N'|') > 0 THEN p.NewName ELSE m.Name END,
               m.IsActive,
               CASE WHEN p.SetActive IS NULL THEN m.IsActive ELSE CAST(p.SetActive AS BIT) END,
               mv.Ok,
               CASE WHEN mv.Ok = 0 AND mv.Target = 0 THEN 1 ELSE 0 END
        FROM @P p
        JOIN [dbo].[ObjectMenu] m ON m.MetaLink = p.Link AND m.IsDeleted = 0 /*SEED_ONLY_NEW*/
        LEFT JOIN [dbo].[ObjectMenu] np ON np.MetaLink = p.NewParent AND np.IsDeleted = 0
        LEFT JOIN [dbo].[ObjectMenu] op ON op.MetaLink = p.OldParent AND op.IsDeleted = 0
        CROSS APPLY (SELECT
            -- đã đúng vị trí mới
            CASE WHEN ISNULL(m.OrderShow, -1) = p.NewOrder AND (p.Lvl = 1 OR m.ParentId = np.Id) THEN 1 ELSE 0 END AS Target,
            -- được chuyển: nhóm cấp 1 còn thứ tự seed cũ; mục cấp 2 còn ở nhóm cha cũ (hoặc đã ở nhóm mới) với thứ tự trong dải seed cũ
            CASE WHEN ISNULL(m.OrderShow, -1) = p.NewOrder AND (p.Lvl = 1 OR m.ParentId = np.Id) THEN 0
                 WHEN p.Lvl = 1 AND m.OrderShow BETWEEN p.OldMin AND p.OldMax THEN 1
                 WHEN p.Lvl = 2 AND np.Id IS NOT NULL AND (m.ParentId = op.Id OR m.ParentId = np.Id) AND m.OrderShow BETWEEN p.OldMin AND p.OldMax THEN 1
                 ELSE 0 END AS Ok) mv;
        """;

    private const string Menu5ApplySql = """
        UPDATE m SET m.ParentId = c.NewParentId, m.OrderShow = c.NewOrder, m.Name = c.NewName, m.IsActive = c.NewActive, m.ModifiedDate = @Now, m.ModifiedBy = @By
        FROM [dbo].[ObjectMenu] m JOIN @C c ON c.Id = m.Id
        WHERE c.Moved = 1 OR ISNULL(c.OldName, N'') <> ISNULL(c.NewName, N'') OR c.OldActive <> c.NewActive;
        """;

    /// <summary>Khối MENU-5 gắn cuối <c>ObjectMenu_seed.sql</c> (DB mới: dựng bố cục cũ rồi chuyển sang bố cục MENU-5; DB đã có: idempotent, không ghi đè admin).</summary>
    public static string Menu5SeedBlock(List<Screen> screens, List<string> menuOrder)
    {
        var sb = new StringBuilder();
        sb.AppendLine("-- MENU-5 (D-79): 14 nhóm theo vai trò. Seed CHỈ THÊM nhóm / mục mới (đặt thẳng vào chỗ mới); chuyển nhóm / thứ tự / đổi tên / ẩn – mở CHỈ áp cho dòng do chính lần chạy");
        sb.AppendLine("-- này tạo (DB mới). DB đang chạy: sắp lại bằng DB/scripts/D79_menu_regroup.sql (DryRun, AuditChange). Nguồn: tools/AppDelicare.CodeGen/Seeds.Menu5.cs.");
        sb.AppendLine("DECLARE @By UNIQUEIDENTIFIER = @SeedBy;");
        AppendPlan(sb, screens, menuOrder);
        sb.AppendLine(Menu5InsertSql);
        sb.AppendLine(Menu5DiffSql.Replace("/*SEED_ONLY_NEW*/", "AND m.CreatedDate = @Now AND m.CreatedBy = @SeedBy"));
        sb.AppendLine(Menu5ApplySql);
        return sb.ToString();
    }

    private const string ScriptHeaderCommon = """
        SET NOCOUNT ON;
        SET XACT_ABORT ON;

        DECLARE @DryRun BIT = $(DryRun);
        DECLARE @By UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.[User] WHERE UserName = N'system' AND ISNULL(IsDeleted, 0) = 0);
        IF @By IS NULL
            THROW 50101, N'Không có tài khoản "system" (seed T23) – không ghi được người thao tác.', 1;
        DECLARE @Now DATETIME = DATEADD(HOUR, 7, SYSUTCDATETIME()); -- giờ Việt Nam như ứng dụng

        """;

    /// <summary>DB/scripts/D79_menu_regroup.sql – chạy thử / chạy thật trên DB đang chạy (sau deploy API + seed).</summary>
    public static string Menu5RegroupScript(List<Screen> screens, List<string> menuOrder)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""
            /*
              <auto-generated> tools/AppDelicare.CodeGen (MENU-5.4, D-79 e – anh chốt 2026-11-23) – nguồn Seeds.Menu5.cs (Menu5Groups). Không sửa tay.
              Script DỮ LIỆU một lần cho DB đang chạy: sắp lại menu thành 14 nhóm theo vai trò. Không tạo / sửa bảng, cột; không DELETE; giữ Id / MetaLink / quyền.
                - Thêm nhóm: Việc của tôi (#/group/00), Kho (#/group/04b), Giá thành (#/group/05b); thêm mục: Việc của tôi (#/hr-my), Job tự động (#/adm-job),
                  Nhập dữ liệu gốc (#/sys-import), Phê duyệt › Mẻ nấu (#/wf-inbox?type=prod-batch), Phê duyệt › Giữ / thả lô (#/wf-inbox?type=scm-lot).
                - Chuyển nhóm / thứ tự: CHỈ khi mục còn ở nhóm cha cũ với thứ tự trong dải seed cũ (nhóm NN: NN01–NN99, Phê duyệt: 9901–9999) – mục admin đã tự
                  chuyển / đặt thứ tự ngoài dải được GIỮ NGUYÊN và in ra danh sách "Giữ nguyên".
                - Đổi tên: nhóm Mua hàng & Kho → Mua hàng, Sản xuất & Giá thành → Sản xuất, Tích hợp & Chỉ số → Nhật ký & Tích hợp; mục duyệt rõ cấp – chỉ khi tên đang là tên seed cũ.
                - Ẩn (IsActive = 0): Người liên hệ (#/crm-contact – nhập ở Khách hàng 360), Phê duyệt › Đề nghị mua hàng cũ (scm-pr), Phê duyệt › Chính sách giá
                  (không loại duyệt nào dùng). Mở lại: Phê duyệt › Kiểm tra hàng nhập (quyền Kết luận QC đầu vào – MENU-5.2).
              Ghi AuditChange (người sửa = tài khoản "system"). Số dòng UserRole không đổi (quyền: D79_permission_matrix.sql). Chạy lại lần 2 không đổi gì.

              Bắt buộc truyền -v DryRun=1 (chạy thử, in đối chiếu) hoặc -v DryRun=0 (chạy thật).
              THỨ TỰ (chỉ sau khi backup): deploy API MENU-5 → seed (ObjectMenu_seed.sql, UserRole_seed.sql) → script này → D79_permission_matrix.sql.
                sqlcmd -S <server> -d <db> -E -f 65001 -v DryRun=1 -i DB/scripts/D79_menu_regroup.sql -W -s "|"
                sqlcmd -S <server> -d <db> -E -f 65001 -v DryRun=0 -i DB/scripts/D79_menu_regroup.sql -W -s "|"
              Người dùng đang đăng nhập tải lại trang để thấy menu mới.
            */
            """);
        sb.Append(ScriptHeaderCommon);
        sb.AppendLine("BEGIN TRANSACTION;");
        sb.AppendLine("DECLARE @RolesBefore INT = (SELECT COUNT(*) FROM dbo.UserRole);");
        sb.AppendLine("DECLARE @MenusBefore INT = (SELECT COUNT(*) FROM dbo.ObjectMenu WHERE IsDeleted = 0);");
        AppendPlan(sb, screens, menuOrder);
        sb.AppendLine(Menu5InsertSql);
        sb.AppendLine("DECLARE @Added INT = (SELECT COUNT(*) FROM dbo.ObjectMenu WHERE IsDeleted = 0) - @MenusBefore;");
        sb.AppendLine(Menu5DiffSql);
        sb.AppendLine("""
            SELECT p.Link AS [Mục mới], p.NewName AS [Tên], p.NewParent AS [Nhóm]
            FROM @P p WHERE EXISTS (SELECT 1 FROM dbo.ObjectMenu m WHERE m.MetaLink = p.Link AND m.IsDeleted = 0 AND m.CreatedDate = @Now AND m.CreatedBy = @By)
            ORDER BY p.NewParent, p.NewOrder;
            SELECT c.Link AS [Mục], ISNULL(op.MetaLink, N'') AS [Nhóm cũ], ISNULL(np.MetaLink, N'') AS [Nhóm mới], c.OldOrder AS [Thứ tự cũ], c.NewOrder AS [Thứ tự mới],
                   CASE WHEN ISNULL(c.OldName, N'') <> ISNULL(c.NewName, N'') THEN c.OldName + N' → ' + c.NewName ELSE N'' END AS [Đổi tên],
                   CASE WHEN c.OldActive <> c.NewActive THEN CASE WHEN c.NewActive = 1 THEN N'MỞ LẠI' ELSE N'ẨN' END ELSE N'' END AS [Ẩn / mở]
            FROM @C c LEFT JOIN dbo.ObjectMenu op ON op.Id = c.OldParentId LEFT JOIN dbo.ObjectMenu np ON np.Id = c.NewParentId
            WHERE c.Moved = 1 OR ISNULL(c.OldName, N'') <> ISNULL(c.NewName, N'') OR c.OldActive <> c.NewActive
            ORDER BY ISNULL(c.NewOrder, 0);
            SELECT c.Link AS [Giữ nguyên (admin đã chuyển / đặt thứ tự)], op.MetaLink AS [Nhóm hiện tại], c.OldOrder AS [Thứ tự hiện tại], c.OldName AS [Tên]
            FROM @C c LEFT JOIN dbo.ObjectMenu op ON op.Id = c.OldParentId WHERE c.Kept = 1 ORDER BY c.Link;
            SELECT @Added AS [Thêm mục], (SELECT COUNT(*) FROM @C WHERE Moved = 1) AS [Chuyển nhóm / thứ tự],
                   (SELECT COUNT(*) FROM @C WHERE ISNULL(OldName, N'') <> ISNULL(NewName, N'')) AS [Đổi tên],
                   (SELECT COUNT(*) FROM @C WHERE OldActive <> NewActive) AS [Ẩn / mở], (SELECT COUNT(*) FROM @C WHERE Kept = 1) AS [Giữ nguyên];

            IF @DryRun = 1
            BEGIN
                ROLLBACK TRANSACTION;
                PRINT N'Chạy thử (DryRun=1): không ghi gì. Chạy thật: -v DryRun=0 (sau khi backup, sau khi deploy API MENU-5).';
                RETURN;
            END
            """);
        sb.AppendLine(Menu5ApplySql);
        sb.AppendLine("""
            INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
            SELECT NEWID(), c.Id, N'Modified', N'ObjectMenu',
                   N'- Actor: SYSTEM:' + LOWER(CAST(@By AS NVARCHAR(36))) + N' | MENU-5 sắp lại menu theo vai trò (D-79 e) <br />' + CHAR(13) + CHAR(10)
                   + CASE WHEN c.Moved = 1 THEN N'- ParentId: ''' + ISNULL(CAST(c.OldParentId AS NVARCHAR(36)), N'') + N''' → ''' + ISNULL(CAST(c.NewParentId AS NVARCHAR(36)), N'') + N''' <br />' + CHAR(13) + CHAR(10)
                        + N'- OrderShow: ''' + ISNULL(CAST(c.OldOrder AS NVARCHAR(20)), N'') + N''' → ''' + ISNULL(CAST(c.NewOrder AS NVARCHAR(20)), N'') + N''' <br />' + CHAR(13) + CHAR(10) ELSE N'' END
                   + CASE WHEN ISNULL(c.OldName, N'') <> ISNULL(c.NewName, N'') THEN N'- Name: ''' + ISNULL(c.OldName, N'') + N''' → ''' + c.NewName + N''' <br />' + CHAR(13) + CHAR(10) ELSE N'' END
                   + CASE WHEN c.OldActive <> c.NewActive THEN N'- IsActive: ''' + CASE WHEN c.OldActive = 1 THEN N'True' ELSE N'False' END + N''' → ''' + CASE WHEN c.NewActive = 1 THEN N'True' ELSE N'False' END + N''' <br />' ELSE N'' END,
                   @Now, @By, 0
            FROM @C c
            WHERE c.Moved = 1 OR ISNULL(c.OldName, N'') <> ISNULL(c.NewName, N'') OR c.OldActive <> c.NewActive;
            INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
            SELECT NEWID(), m.Id, N'Added', N'ObjectMenu', N'- Actor: SYSTEM:' + LOWER(CAST(@By AS NVARCHAR(36))) + N' | MENU-5 thêm mục ' + m.MetaLink + N' (' + m.Name + N') <br />', @Now, @By, 0
            FROM dbo.ObjectMenu m WHERE m.IsDeleted = 0 AND m.CreatedDate = @Now AND m.CreatedBy = @By AND m.MetaLink IN (SELECT Link FROM @P);

            DECLARE @RolesAfter INT = (SELECT COUNT(*) FROM dbo.UserRole);
            IF @RolesAfter <> @RolesBefore
                THROW 50102, N'Số dòng UserRole thay đổi – hủy (quyền đặt ở D79_permission_matrix.sql).', 1;
            COMMIT TRANSACTION;
            PRINT N'Đã áp dụng. Chạy lại lần 2 sẽ không đổi gì. Tiếp theo: D79_permission_matrix.sql (DryRun trước).';
            """);
        return sb.ToString();
    }

    /// <summary>DB/scripts/D79_permission_matrix.sql – đặt cờ quyền đúng ma trận MENU-5.1 cho 19 nhóm chuẩn trên mọi mục cấp 2 đang bật.</summary>
    public static string PermissionMatrixScript()
    {
        var sb = new StringBuilder();
        sb.AppendLine("""
            /*
              <auto-generated> tools/AppDelicare.CodeGen (MENU-5.1, D-79 – anh chốt 2026-11-23) – nguồn Seeds.Menu5.cs (ScreenMatrix + Approvers5). Không sửa tay.
              Script DỮ LIỆU một lần cho DB đang chạy: đặt cờ UserRole (Truy cập / Thêm / Sửa / Xóa / In / Xuất / Nhập) ĐÚNG ma trận theo từng màn cho 19 nhóm chuẩn
              (16 nhóm cũ + SAL-HEAD, OPS-SCM-HEAD, OPS-WH – thêm UserGroup nếu chưa có) trên MỌI mục menu cấp 2 đang bật (ObjectMenu.IsActive = 1),
              kể cả mục Phê duyệt (quyền Duyệt = Truy cập + Sửa – D-79 d). Mục không có trong ma trận = không quyền.
              KHÔNG đụng: nhóm ngoài 19 nhóm chuẩn, User.IsAdmin, mục quyền vào bot (#/bot-van-hanh, #/bot-dieu-hanh – D-62), mục đang ẩn, nhóm menu cấp 1.
              In: từng nhóm – số ô quyền được CẤP (0 → 1) / bị THU (1 → 0) + tổng; danh sách từng dòng (trước → sau, thứ tự cờ XemThemSuaXoaInXuatNhap).
              Ghi AuditChange (người sửa = tài khoản "system"). Chạy lại lần 2 không đổi gì.

              Bắt buộc truyền -v DryRun=1 (chạy thử) hoặc -v DryRun=0 (chạy thật).
              THỨ TỰ (chỉ sau khi backup): deploy API MENU-5 → seed → D79_menu_regroup.sql → script này.
                sqlcmd -S <server> -d <db> -E -f 65001 -v DryRun=1 -i DB/scripts/D79_permission_matrix.sql -W -s "|" -o d79-preview.txt
                sqlcmd -S <server> -d <db> -E -f 65001 -v DryRun=0 -i DB/scripts/D79_permission_matrix.sql -W -s "|" -o d79-applied.txt
              Người dùng đang đăng nhập tải lại trang (quyền máy chủ cache tối đa 5 phút – Permissions:CacheMinutes).
            */
            """);
        sb.Append(ScriptHeaderCommon);
        sb.AppendLine("BEGIN TRANSACTION;");
        sb.AppendLine("DECLARE @Roles TABLE (Name NVARCHAR(100) PRIMARY KEY, Ord INT);");
        sb.AppendLine("INSERT INTO @Roles VALUES " + string.Join(", ", StandardRoles.Select((r, i) => $"({Sql(r)}, {i + 1})")) + ";");
        sb.AppendLine("DECLARE @Skip TABLE (MetaLink NVARCHAR(400) PRIMARY KEY);");
        sb.AppendLine("INSERT INTO @Skip VALUES " + string.Join(", ", MatrixSkipLinks.Select(l => $"({Sql(l)})")) + ";");
        sb.AppendLine("-- D-79 (b): nhóm quyền mới (UserGroup.Name = mã) – thêm nếu chưa có.");
        sb.AppendLine("""
            INSERT INTO dbo.UserGroup (Id, Name, IsDefault, CreatedDate, CreatedBy, IsDeleted)
            SELECT NEWID(), r.Name, 0, @Now, @By, 0 FROM @Roles r WHERE NOT EXISTS (SELECT 1 FROM dbo.UserGroup g WHERE g.Name = r.Name AND ISNULL(g.IsDeleted, 0) = 0);
            DECLARE @NewGroups INT = @@ROWCOUNT;
            """);
        sb.AppendLine("-- Quyền đích (chỉ dòng có quyền) – không có dòng ở đây = không quyền.");
        sb.AppendLine("DECLARE @Allow TABLE (GroupName NVARCHAR(100), MetaLink NVARCHAR(300), A BIT, Ad BIT, E BIT, D BIT, P BIT, X BIT, I BIT, PRIMARY KEY (GroupName, MetaLink));");
        AppendValues(sb, "@Allow", MatrixRows(nonZeroOnly: true));
        sb.AppendLine("""

            DECLARE @T TABLE (UserGroupId UNIQUEIDENTIFIER, GroupName NVARCHAR(100), Ord INT, ObjectMenuId UNIQUEIDENTIFIER, MetaLink NVARCHAR(400), MenuName NVARCHAR(500),
                              A BIT, Ad BIT, E BIT, D BIT, P BIT, X BIT, I BIT, UserRoleId UNIQUEIDENTIFIER NULL,
                              OA BIT, OAd BIT, OE BIT, OD BIT, OP BIT, OX BIT, OI BIT);
            INSERT INTO @T
            SELECT g.Id, g.Name, r.Ord, m.Id, m.MetaLink, m.Name,
                   ISNULL(a.A, 0), ISNULL(a.Ad, 0), ISNULL(a.E, 0), ISNULL(a.D, 0), ISNULL(a.P, 0), ISNULL(a.X, 0), ISNULL(a.I, 0),
                   ur.Id, ISNULL(ur.AllowAccess, 0), ISNULL(ur.AllowAdd, 0), ISNULL(ur.AllowEdit, 0), ISNULL(ur.AllowDelete, 0), ISNULL(ur.AllowPrint, 0), ISNULL(ur.AllowExport, 0), ISNULL(ur.AllowImport, 0)
            FROM dbo.UserGroup g
            JOIN @Roles r ON r.Name = g.Name
            CROSS JOIN dbo.ObjectMenu m
            LEFT JOIN @Allow a ON a.GroupName = g.Name AND a.MetaLink = m.MetaLink
            OUTER APPLY (SELECT TOP 1 x.* FROM dbo.UserRole x WHERE x.UserGroupId = g.Id AND x.ObjectMenuId = m.Id AND ISNULL(x.IsDeleted, 0) = 0 ORDER BY x.CreatedDate) ur
            WHERE ISNULL(g.IsDeleted, 0) = 0 AND ISNULL(m.IsDeleted, 0) = 0 AND m.IsActive = 1 AND m.LevelShow = 2 AND m.MetaLink LIKE N'#/%'
              AND NOT EXISTS (SELECT 1 FROM @Skip s WHERE s.MetaLink = m.MetaLink);

            -- Ô cấp / thu: đếm theo từng cờ (7 cờ / dòng).
            DECLARE @Diff TABLE (UserGroupId UNIQUEIDENTIFIER, ObjectMenuId UNIQUEIDENTIFIER, Granted INT, Revoked INT, PRIMARY KEY (UserGroupId, ObjectMenuId));
            INSERT INTO @Diff
            SELECT UserGroupId, ObjectMenuId,
                   (CASE WHEN A = 1 AND OA = 0 THEN 1 ELSE 0 END) + (CASE WHEN Ad = 1 AND OAd = 0 THEN 1 ELSE 0 END) + (CASE WHEN E = 1 AND OE = 0 THEN 1 ELSE 0 END)
                   + (CASE WHEN D = 1 AND OD = 0 THEN 1 ELSE 0 END) + (CASE WHEN P = 1 AND OP = 0 THEN 1 ELSE 0 END) + (CASE WHEN X = 1 AND OX = 0 THEN 1 ELSE 0 END) + (CASE WHEN I = 1 AND OI = 0 THEN 1 ELSE 0 END),
                   (CASE WHEN A = 0 AND OA = 1 THEN 1 ELSE 0 END) + (CASE WHEN Ad = 0 AND OAd = 1 THEN 1 ELSE 0 END) + (CASE WHEN E = 0 AND OE = 1 THEN 1 ELSE 0 END)
                   + (CASE WHEN D = 0 AND OD = 1 THEN 1 ELSE 0 END) + (CASE WHEN P = 0 AND OP = 1 THEN 1 ELSE 0 END) + (CASE WHEN X = 0 AND OX = 1 THEN 1 ELSE 0 END) + (CASE WHEN I = 0 AND OI = 1 THEN 1 ELSE 0 END)
            FROM @T
            WHERE OA <> A OR OAd <> Ad OR OE <> E OR OD <> D OR OP <> P OR OX <> X OR OI <> I;

            SELECT t.GroupName AS [Nhóm], COUNT(d.ObjectMenuId) AS [Dòng đổi], ISNULL(SUM(d.Granted), 0) AS [Ô được cấp], ISNULL(SUM(d.Revoked), 0) AS [Ô bị thu],
                   SUM(CASE WHEN t.A = 1 THEN 1 ELSE 0 END) AS [Mục được vào sau khi chạy]
            FROM @T t LEFT JOIN @Diff d ON d.UserGroupId = t.UserGroupId AND d.ObjectMenuId = t.ObjectMenuId
            GROUP BY t.GroupName, t.Ord ORDER BY t.Ord;
            SELECT COUNT(*) AS [Tổng dòng đổi], ISNULL(SUM(Granted), 0) AS [Tổng ô được cấp], ISNULL(SUM(Revoked), 0) AS [Tổng ô bị thu], @NewGroups AS [Nhóm mới thêm] FROM @Diff;
            SELECT t.GroupName AS [Nhóm], t.MetaLink AS [Mục], t.MenuName AS [Tên],
                   CASE WHEN t.UserRoleId IS NULL THEN N'THÊM DÒNG' WHEN t.A = 0 THEN N'THU HẾT' WHEN d.Revoked > 0 AND d.Granted > 0 THEN N'ĐỔI' WHEN d.Revoked > 0 THEN N'THU BỚT' ELSE N'CẤP THÊM' END AS [Thao tác],
                   CONCAT(CAST(t.OA AS INT), CAST(t.OAd AS INT), CAST(t.OE AS INT), CAST(t.OD AS INT), CAST(t.OP AS INT), CAST(t.OX AS INT), CAST(t.OI AS INT)) AS [Trước_XemThemSuaXoaInXuatNhap],
                   CONCAT(CAST(t.A AS INT), CAST(t.Ad AS INT), CAST(t.E AS INT), CAST(t.D AS INT), CAST(t.P AS INT), CAST(t.X AS INT), CAST(t.I AS INT)) AS [Sau]
            FROM @T t JOIN @Diff d ON d.UserGroupId = t.UserGroupId AND d.ObjectMenuId = t.ObjectMenuId
            ORDER BY t.Ord, t.MetaLink;

            IF @DryRun = 1
            BEGIN
                ROLLBACK TRANSACTION;
                PRINT N'Chạy thử (DryRun=1): không ghi gì. Chạy thật: -v DryRun=0 (sau khi backup, sau D79_menu_regroup.sql).';
                RETURN;
            END

            UPDATE ur SET AllowAccess = t.A, AllowAdd = t.Ad, AllowEdit = t.E, AllowDelete = t.D, AllowPrint = t.P, AllowExport = t.X, AllowImport = t.I,
                ModifiedDate = @Now, ModifiedBy = @By
            FROM dbo.UserRole ur
            JOIN @T t ON t.UserRoleId = ur.Id
            JOIN @Diff d ON d.UserGroupId = t.UserGroupId AND d.ObjectMenuId = t.ObjectMenuId;
            PRINT CONCAT(N'Cập nhật: ', @@ROWCOUNT, N' dòng.');

            INSERT INTO dbo.UserRole (Id, UserGroupId, ObjectMenuId, AllowAccess, AllowAdd, AllowEdit, AllowDelete, AllowPrint, AllowExport, AllowImport, CreatedDate, CreatedBy, IsDeleted)
            SELECT NEWID(), t.UserGroupId, t.ObjectMenuId, t.A, t.Ad, t.E, t.D, t.P, t.X, t.I, @Now, @By, 0
            FROM @T t JOIN @Diff d ON d.UserGroupId = t.UserGroupId AND d.ObjectMenuId = t.ObjectMenuId
            WHERE t.UserRoleId IS NULL;
            PRINT CONCAT(N'Thêm dòng: ', @@ROWCOUNT, N' dòng.');

            INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
            SELECT NEWID(), ISNULL(t.UserRoleId, (SELECT TOP 1 x.Id FROM dbo.UserRole x WHERE x.UserGroupId = t.UserGroupId AND x.ObjectMenuId = t.ObjectMenuId AND x.IsDeleted = 0)),
                   CASE WHEN t.UserRoleId IS NULL THEN N'Added' ELSE N'Modified' END, N'UserRole',
                   N'- Actor: SYSTEM:' + LOWER(CAST(@By AS NVARCHAR(36))) + N' | MENU-5 ma trận quyền theo từng màn (D-79) – ' + t.GroupName + N' × ' + t.MetaLink + N' <br />' + CHAR(13) + CHAR(10)
                   + N'- XemThemSuaXoaInXuatNhap: ''' + CONCAT(CAST(t.OA AS INT), CAST(t.OAd AS INT), CAST(t.OE AS INT), CAST(t.OD AS INT), CAST(t.OP AS INT), CAST(t.OX AS INT), CAST(t.OI AS INT))
                   + N''' → ''' + CONCAT(CAST(t.A AS INT), CAST(t.Ad AS INT), CAST(t.E AS INT), CAST(t.D AS INT), CAST(t.P AS INT), CAST(t.X AS INT), CAST(t.I AS INT)) + N''' <br />',
                   @Now, @By, 0
            FROM @T t JOIN @Diff d ON d.UserGroupId = t.UserGroupId AND d.ObjectMenuId = t.ObjectMenuId;

            COMMIT TRANSACTION;
            PRINT N'Đã áp dụng. Chạy lại lần 2 sẽ không đổi gì. Admin chuyển người sang SAL-HEAD / OPS-SCM-HEAD / OPS-WH trên màn Tài khoản.';
            """);
        return sb.ToString();
    }

    /// <summary>UserRole_seed.sql (MENU-5): 19 nhóm + dòng quyền theo ma trận từng màn – CHỈ THÊM dòng chưa có, không ghi đè quyền admin đã chỉnh (CLAUDE.md 4.3).</summary>
    public static string UserRoleSeed5()
    {
        var sb = new StringBuilder(SeedHeader);
        sb.AppendLine("BEGIN TRAN;");
        sb.AppendLine("-- MENU-5 (D-79): ma trận quyền THEO TỪNG MÀN – nguồn tools/AppDelicare.CodeGen/Seeds.Menu5.cs (ScreenMatrix + Approvers5), thay ma trận theo nhóm màn ORG_STRUCTURE §2.");
        sb.AppendLine("-- UserGroup: Name = mã vai trò (bảng UserGroup không có cột Code). 19 nhóm chuẩn (16 cũ + SAL-HEAD, OPS-SCM-HEAD, OPS-WH).");
        sb.AppendLine("-- CLAUDE.md 4.3: chỉ THÊM dòng chưa có (nhóm × menu), KHÔNG ghi đè quyền admin đã chỉnh. DB đang chạy đặt lại theo ma trận: DB/scripts/D79_permission_matrix.sql.");
        sb.AppendLine("-- Ký hiệu: Q = Thêm + Sửa + Xóa + Nhập · S = Thêm + Sửa · X = Xem · IX = In + Xuất · Duyệt = Xem + Sửa trên mục #/wf-inbox?type=<màn> (D-31).");
        foreach (string role in StandardRoles)
        {
            sb.AppendLine($"IF NOT EXISTS (SELECT 1 FROM [dbo].[UserGroup] WHERE Name = {Sql(role)} AND IsDeleted = 0)");
            sb.AppendLine($"    INSERT INTO [dbo].[UserGroup] (Id, Name, IsDefault, CreatedDate, CreatedBy, IsDeleted) VALUES (NEWID(), {Sql(role)}, 0, @Now, @SeedBy, 0);");
        }
        sb.AppendLine();
        sb.AppendLine("DECLARE @R TABLE (GroupName NVARCHAR(100), MetaLink NVARCHAR(400), A BIT, Ad BIT, E BIT, D BIT, P BIT, X BIT, I BIT);");
        AppendValues(sb, "@R", MatrixRows(nonZeroOnly: false));
        sb.AppendLine("""

            INSERT INTO [dbo].[UserRole] (Id, UserGroupId, ObjectMenuId, AllowAccess, AllowAdd, AllowEdit, AllowDelete, AllowPrint, AllowExport, AllowImport, CreatedDate, CreatedBy, IsDeleted)
            SELECT NEWID(), g.Id, m.Id, r.A, r.Ad, r.E, r.D, r.P, r.X, r.I, @Now, @SeedBy, 0
            FROM @R r
            JOIN [dbo].[UserGroup] g ON g.Name = r.GroupName AND g.IsDeleted = 0
            JOIN [dbo].[ObjectMenu] m ON m.MetaLink = r.MetaLink AND m.IsDeleted = 0
            WHERE NOT EXISTS (SELECT 1 FROM [dbo].[UserRole] ur WHERE ur.UserGroupId = g.Id AND ur.ObjectMenuId = m.Id AND ur.IsDeleted = 0);
            COMMIT;
            """);
        return sb.ToString();
    }

    // ------------------------------------------------------------------ tài liệu sinh (MENU-5.6)
    /// <summary>Tên hiển thị của mục menu (giống seed: tên MENU-2 / MENU-5, mục mới, mục duyệt).</summary>
    private static string ItemName(string item, List<Screen> screens)
    {
        if (Menu5NewItems.TryGetValue(item, out var nw))
        {
            return nw.Name;
        }
        if (Menu5Renames.TryGetValue(item, out var rn))
        {
            return rn.New;
        }
        if (item is "bot-van-hanh" or "bot-dieu-hanh")
        {
            return item == "bot-van-hanh" ? "Bot Telegram – Vận hành" : "Bot Telegram – Điều hành";
        }
        string id = item.StartsWith("wf-inbox?type=", StringComparison.Ordinal) ? item["wf-inbox?type=".Length..] : item;
        string baseName = screens.FirstOrDefault(s => s.Id == id)?.Name ?? ExtraApprovals.FirstOrDefault(x => x.Id == id)?.Name ?? id;
        return MenuName("#/" + item, baseName);
    }

    /// <summary>Ký hiệu quyền như ROADMAP MENU-5.1: Q / S / X, (IX), Duyệt; "—" = không có.</summary>
    private static string RightLabel(string item, Right r)
    {
        if (!r.A)
        {
            return "—";
        }
        if (item.StartsWith("wf-inbox?type=", StringComparison.Ordinal) && item != "wf-inbox?type=wf-inbox")
        {
            return r.E ? "Duyệt" : "X";
        }
        string b = r.D && r.I ? "Q" : r.Ad && r.E ? "S" : r.E ? "Sửa" : "X";
        return r.P || r.X ? b + " (IX)" : b;
    }

    /// <summary>Bảng markdown: Menu › Mục | screen-id | quyền từng vai trò. <paramref name="hideViewOnly"/> = bỏ dòng mọi vai trò chỉ Xem.</summary>
    public static string RoleTableMd(string[] roles, List<Screen> screens, bool hideViewOnly = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine("| Menu › Mục | screen-id | " + string.Join(" | ", roles) + " |");
        sb.AppendLine("|---|---|" + string.Concat(roles.Select(_ => "---|")));
        foreach (var g in Menu5Groups)
        {
            foreach (string item in g.Items.Where(i => !Menu5HiddenLinks.Contains("#/" + i) && !MatrixSkipLinks.Contains("#/" + i)))
            {
                var rights = roles.Select(r => RightOf(r, item)).ToList();
                if (!rights.Any(r => r.A) || (hideViewOnly && rights.All(r => RightLabel(item, r) is "X" or "—")))
                {
                    continue;
                }
                sb.AppendLine($"| {g.Name} › {ItemName(item, screens)} | `{item}` | " + string.Join(" | ", roles.Select((r, i) => RightLabel(item, rights[i]))) + " |");
            }
        }
        return sb.ToString();
    }

    /// <summary>docs/claude/MENU5_MATRIX.md – toàn bộ ma trận 19 nhóm (bản đọc của Seeds.Menu5.cs, thay bảng theo nhóm màn của ORG_STRUCTURE §2).</summary>
    public static string MatrixDoc(List<Screen> screens)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!-- auto-generated: tools/AppDelicare.CodeGen (--layers security) từ Seeds.Menu5.cs – không sửa tay. -->");
        sb.AppendLine("# MENU-5 – Ma trận quyền theo từng màn (D-79)");
        sb.AppendLine();
        sb.AppendLine("Nguồn duy nhất: `tools/AppDelicare.CodeGen/Seeds.Menu5.cs` (ScreenMatrix + Approvers5) → `DB/seed/UserRole_seed.sql` (chỉ thêm dòng thiếu) và `DB/scripts/D79_permission_matrix.sql` (DB đang chạy). Thay bảng theo nhóm màn của ORG_STRUCTURE §2 (GAP-PERM-02).");
        sb.AppendLine();
        sb.AppendLine("**Ký hiệu:** Q = Xem + Thêm + Sửa + Xóa + Nhập · S = Xem + Thêm + Sửa · X = Xem · (IX) = có In + Xuất · Duyệt = Xem + Sửa trên mục \"Phê duyệt › …\" · — = không có. Mục \"Việc của tôi\" mọi nhóm Xem. Mục quyền vào bot (`#/bot-van-hanh`, `#/bot-dieu-hanh` – D-62) không thuộc ma trận này.");
        sb.AppendLine();
        sb.Append(RoleTableMd(StandardRoles, screens));
        sb.AppendLine();
        sb.AppendLine("## Số mục được vào / số ô quyền");
        sb.AppendLine();
        sb.AppendLine("```");
        sb.AppendLine(Menu5Summary());
        sb.AppendLine("```");
        return sb.ToString();
    }

    /// <summary>HDSD → vai trò của bảng "Màn hình được dùng" (mục 2). <c>true</c> = chỉ dòng có quyền hơn Xem (vai trò xem mọi màn nghiệp vụ).</summary>
    public static readonly (string File, string[] Roles, bool Compact)[] GuideRoles =
    [
        ("01_QUAN_TRI.md", ["FIN-IT"], true),
        ("02_KINH_DOANH.md", ["SAL-SALES", "SAL-CON", "SAL-HEAD", "SAL-CS"], false),
        ("03_KE_HOACH.md", ["OPS-PLAN"], false),
        ("04_MUA_HANG.md", ["OPS-SCM", "OPS-SCM-HEAD"], false),
        ("05_KHO.md", ["OPS-WH", "OPS-SCM-HEAD"], false),
        ("06_QC_ATTP.md", ["QA"], false),
        ("07_BEP_SAN_XUAT.md", ["OPS-KIT", "OPS-MNT"], false),
        ("08_DONG_GOI.md", ["OPS-KIT", "OPS-LOG"], false),
        ("09_GIAO_VAN.md", ["OPS-LOG"], false),
        ("10_DIEM_PHAT.md", ["OPS-POS"], false),
        ("11_NHAN_SU.md", ["FIN-HR"], false),
        ("13_BAN_DIEU_HANH.md", ["CEO", "CEO-PMO", "COO", "FIN-ACC"], true),
    ];

    public const string GuideBegin = "<!-- MENU5:BEGIN – bảng sinh từ ma trận D-79 (tools/AppDelicare.CodeGen --layers security), không sửa tay -->";
    public const string GuideEnd = "<!-- MENU5:END -->";

    /// <summary>
    /// Thay bảng của mục "## 2. Màn hình được dùng" bằng khối sinh (giữa 2 dấu mốc). Lần đầu: bảng markdown đầu tiên của mục 2 được thay bằng khối.
    /// Trả null nếu không tìm thấy mục 2.
    /// </summary>
    public static string? PatchGuide(string text, string[] roles, bool compact, List<Screen> screens)
    {
        string nl = text.Contains("\r\n") ? "\r\n" : "\n";
        string block = GuideBegin + "\n"
            + (compact ? $"Xem mọi màn nghiệp vụ; bảng dưới chỉ liệt kê mục có quyền hơn **Xem** (đủ ma trận: `docs/claude/MENU5_MATRIX.md`).\n\n" : "Quyền theo ma trận D-79 (đủ 19 nhóm: `docs/claude/MENU5_MATRIX.md`).\n\n")
            + RoleTableMd(roles, screens, compact) + GuideEnd;
        block = block.Replace("\r\n", "\n").Replace("\n", nl);
        int b = text.IndexOf(GuideBegin, StringComparison.Ordinal);
        if (b >= 0)
        {
            int e = text.IndexOf(GuideEnd, b, StringComparison.Ordinal);
            return e < 0 ? null : text[..b] + block + text[(e + GuideEnd.Length)..];
        }
        var lines = text.Split(nl).ToList();
        int h = lines.FindIndex(l => l.StartsWith("## 2.", StringComparison.Ordinal));
        if (h < 0)
        {
            return null;
        }
        int start = lines.FindIndex(h + 1, l => l.StartsWith('|') || l.StartsWith("## ", StringComparison.Ordinal));
        if (start < 0 || !lines[start].StartsWith('|'))
        {
            return null;
        }
        int end = start;
        while (end < lines.Count && lines[end].StartsWith('|'))
        {
            end++;
        }
        lines.RemoveRange(start, end - start);
        lines.Insert(start, block);
        return string.Join(nl, lines);
    }

    /// <summary>Tóm tắt in ra console: số mục được vào / số ô quyền theo nhóm; người duyệt từng mục.</summary>
    public static string Menu5Summary()
    {
        var sb = new StringBuilder();
        foreach (string role in StandardRoles)
        {
            var rights = MatrixLinks().Select(l => RightOf(role, l)).ToList();
            sb.AppendLine($"  {role,-13} vào {rights.Count(r => r.A),3} mục · ô quyền {rights.Sum(r => r.Bits.Count(c => c == '1')),4}");
        }
        sb.Append("  Duyệt: " + string.Join("; ", Approvers5.Select(a => $"{a.Screen} = {string.Join(", ", a.Roles)}")));
        return sb.ToString();
    }
}
