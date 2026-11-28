using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Services.Implement.Knowledge;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement.Agent
{
    // ======================================================================= mô hình

    public sealed class AgentCenterAgentModel
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Short { get; set; } = string.Empty;
        public string Serves { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public int Wave { get; set; }
        public string Color { get; set; } = string.Empty;
        /// <summary>ok = đang chạy · key = bật nhưng máy chủ chưa có khóa agent · off = chưa bật / đang tắt.</summary>
        public string State { get; set; } = "off";
        public bool Active { get; set; }
        public bool Configured { get; set; }
        public int ToolCount { get; set; }
        public int ScheduleCount { get; set; }
        public int CallsToday { get; set; }
        public int DeniedToday { get; set; }
        public int FailedToday { get; set; }
        public DateTime? LastAt { get; set; }
        /// <summary>Lần gọi gần nhất: giờ · người hỏi · công cụ (nhật ký agent 7 ngày).</summary>
        public string? LastLine { get; set; }
        public int OpenTasks { get; set; }
        public List<string> ListAsk { get; set; } = [];
        public List<string> ListCan { get; set; } = [];
    }

    public sealed class AgentFeedModel
    {
        public DateTime At { get; set; }
        public string? Agent { get; set; }
        public string Text { get; set; } = string.Empty;
        public string? Who { get; set; }
        public string? Status { get; set; }
    }

    public sealed class AgentOverviewModel
    {
        public DateTime Date { get; set; }
        public DateTime GeneratedAt { get; set; }
        public int Running { get; set; }
        public int Total { get; set; }
        public int CallsToday { get; set; }
        public int Askers { get; set; }
        public int FailedToday { get; set; }
        public int DeniedToday { get; set; }
        public int SchedulesOn { get; set; }
        public int SchedulesTotal { get; set; }
        public int KnowledgeNotes { get; set; }
        public int TasksOpen { get; set; }
        public int TasksHot { get; set; }
        public int FlowAttention { get; set; }
        public int FlowSteps { get; set; }
        public DateTime FlowDate { get; set; }
        public List<AgentCenterAgentModel> ListAgent { get; set; } = [];
        public List<AgentFeedModel> ListFeed { get; set; } = [];
    }

    public sealed class AgentTaskModel
    {
        public string Key { get; set; } = string.Empty;
        /// <summary>ALERT (cảnh báo đang mở) · FLOW (bước luồng QT-SX-01 kẹt) · NOTE (việc agent ghi ở thư mục 98).</summary>
        public string Source { get; set; } = string.Empty;
        /// <summary>HOT = cần xử lý ngay · WARN = cần chú ý.</summary>
        public string Severity { get; set; } = "WARN";
        public string Title { get; set; } = string.Empty;
        public string? Detail { get; set; }
        public List<string> ListTodo { get; set; } = [];
        public string Agent { get; set; } = string.Empty;
        public string? Owner { get; set; }
        public string? Dept { get; set; }
        public string? Due { get; set; }
        public string? CloseWhen { get; set; }
        public string? Route { get; set; }
        public string? SourceName { get; set; }
        /// <summary>OPEN · AUTO (nguồn hết điều kiện – tự đóng) · DONE (người bấm Đã xử lý).</summary>
        public string Status { get; set; } = "OPEN";
        public DateTime? OpenedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public string? ClosedBy { get; set; }
        public string? Note { get; set; }
        /// <summary>Người xem có quyền Thêm trên màn nguồn (người làm việc đó) – lọc "Chỉ việc tôi phụ trách".</summary>
        public bool Mine { get; set; }
        /// <summary>Việc thư mục 98 (Source NOTE): đường dẫn ghi chú.</summary>
        public string? Path { get; set; }
    }

    public sealed class AgentTaskDeptModel
    {
        public string Dept { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public sealed class AgentTaskBoardModel
    {
        public DateTime Date { get; set; }
        public int Open { get; set; }
        public int Hot { get; set; }
        public int Warn { get; set; }
        public int ClosedToday { get; set; }
        /// <summary>Yêu cầu chờ người xem duyệt – chỉ dẫn link sang Phê duyệt (D-138 d: duyệt không nằm ở Giao việc).</summary>
        public int ApprovalsPending { get; set; }
        public List<AgentTaskModel> ListTask { get; set; } = [];
        public List<AgentTaskDeptModel> ListDept { get; set; } = [];
    }

    public sealed class AgentCloseTaskRequest
    {
        public string? Key { get; set; }
        public string? Note { get; set; }
    }

    public sealed class AgentFlowChainModel
    {
        public DateTime? At { get; set; }
        public string Kind { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        /// <summary>OK / WAIT / FAIL / INFO.</summary>
        public string Status { get; set; } = "INFO";
    }

    public sealed class AgentFlowStepDayModel
    {
        public int No { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Dept { get; set; } = string.Empty;
        public string Agent { get; set; } = string.Empty;
        public string AgentName { get; set; } = string.Empty;
        public string Screen { get; set; } = string.Empty;
        public string ScreenName { get; set; } = string.Empty;
        /// <summary>OK (Đạt) · RUN (Đang chạy) · WARN (Cần chú ý) · BAD (Có vấn đề) · WAIT (Chờ dữ liệu).</summary>
        public string Status { get; set; } = "WAIT";
        public string Note { get; set; } = string.Empty;
        public bool CanOpen { get; set; }
        public List<AgentFlowChainModel> ListChain { get; set; } = [];
    }

    public sealed class AgentFlowDayModel
    {
        public DateTime Date { get; set; }
        public DateTime GeneratedAt { get; set; }
        public int Ok { get; set; }
        public int Run { get; set; }
        public int Warn { get; set; }
        public int Bad { get; set; }
        public int Wait { get; set; }
        public List<AgentFlowStepDayModel> ListStep { get; set; } = [];
    }

    /// <summary>
    /// AI-21 (D-138) – Trung tâm Agent (<c>#/ai-agents?tab=…</c>): Tổng quan, Giao việc, Luồng liên phòng 12 bước QT-SX-01, danh sách agent cho tab Chat.
    /// Không bảng mới: trạng thái agent = BotConfiguration + IntegrationLog (AI-6); việc = cảnh báo đang mở (<c>CommonService.GetAlerts</c> – đã lọc theo quyền Xem màn nguồn
    /// của người xem) + bước luồng "Có vấn đề" / "Cần chú ý" + việc agent ghi ở thư mục 98; mở / tự đóng / đã xử lý ghi ở sổ việc theo ngày (thư mục 98).
    /// </summary>
    public interface IAgentCenterService
    {
        Task<IResult<AgentOverviewModel>> GetOverview();
        Task<IResult<AgentTaskBoardModel>> GetTasks(bool includeDone);
        Task<IResult<AgentTaskModel>> CloseTask(AgentCloseTaskRequest request);
        Task<IResult<AgentFlowDayModel>> GetFlowDay(DateTime? date);
        Task<IResults<AgentCenterAgentModel>> GetChatAgents();
    }

    public sealed class AgentCenterService(IUnitOfWork unitOfWork, IClock clock, ICurrentActor currentActor, IPermissionService permissions, ICommonService common,
        IKnowledgeService knowledge, IAgentAdminService admin, IAgentFlowService flows) : IAgentCenterService
    {
        private static Result<T> Status<T>(int code, string message) => new() { ResponseCode = code, ResponseMessage = message };

        // ---------------------------------------------------------------- danh mục hiển thị (không phải cấu hình)
        public static readonly IReadOnlyDictionary<string, string> ShortName = new Dictionary<string, string>
        {
            ["VIVI"] = "Vivi – Điều hành", ["WH"] = "Kho", ["KITCHEN"] = "Bếp", ["QA"] = "ATTP & Truy xuất", ["KNOW"] = "Thủ thư tri thức", ["SALES"] = "Kinh doanh",
            ["PLAN"] = "Kế hoạch SX", ["PROC"] = "Mua hàng", ["HR"] = "Nhân sự", ["LOG"] = "Giao vận", ["PKG"] = "Hộp khay & VS", ["ACC"] = "Kế toán", ["CSKH"] = "CSKH – Zalo",
        };

        /// <summary>Câu hỏi gợi ý của từng agent (tab Chat, thẻ agent).</summary>
        public static readonly IReadOnlyDictionary<string, string[]> Asks = new Dictionary<string, string[]>
        {
            ["VIVI"] = ["Tóm tắt tình hình hôm nay", "Bước nào trong luồng đang kẹt?", "Việc nào quá hạn?"],
            ["WH"] = ["Lô nào hết hạn trong 3 ngày?", "Nguyên liệu dưới định mức"],
            ["KITCHEN"] = ["Tiến độ ca trưa", "Mẻ nào đang trễ?"],
            ["QA"] = ["Nhiệt độ kho lạnh", "Lô đang tạm giữ"],
            ["KNOW"] = ["SOP làm lạnh nhanh", "Sự cố tương tự tháng trước"],
            ["SALES"] = ["Khách chưa chốt đơn ngày mai"],
            ["PLAN"] = ["Kế hoạch sản xuất ngày mai"],
            ["PROC"] = ["Thiếu nguyên liệu gì cho ngày mai?"],
            ["HR"] = ["Ai chưa vào ca hôm nay?"],
            ["LOG"] = ["Xe nào đang trễ?"],
            ["PKG"] = ["Khay thất thoát tuần này"],
            ["ACC"] = ["Giá vốn suất tuần này"],
            ["CSKH"] = ["Thực đơn tuần sau"],
        };

        private static string AgentName(string code) => ShortName.TryGetValue(code, out var n) ? n : code;

        // ---------------------------------------------------------------- việc từ cảnh báo
        private sealed record AlertMeta(string Agent, string Dept, string Owner, string[] Todo, string Due, string Close, string SourceName);

        /// <summary>Cảnh báo → việc nên làm, phụ trách theo vai trò, hạn, điều kiện tự đóng. APPROVAL_PENDING không thành việc (D-138 d – duyệt ở Phê duyệt).</summary>
        private static readonly IReadOnlyDictionary<string, AlertMeta> AlertTasks = new Dictionary<string, AlertMeta>
        {
            [CommonService.AlertCode.StockBelowMin] = new("WH", "Mua hàng", "Mua hàng, Thủ kho", ["Lên đề nghị mua bổ sung về mức tồn tối thiểu", "Kiểm tra đơn mua đang về"], "Trong ngày", "tồn ≥ định mức tối thiểu", "Tồn kho"),
            [CommonService.AlertCode.LotExpiring] = new("WH", "Kho", "Thủ kho, Trưởng bếp", ["Ưu tiên xuất lô cận hạn cho mẻ sớm nhất (FEFO)", "Cân nhắc điều chỉnh món để dùng hết nguyên liệu cận hạn"], "Ca kế tiếp", "lô cận hạn đã xuất hết", "Tồn kho › Theo lô"),
            [CommonService.AlertCode.LotOnHold] = new("QA", "Chất lượng", "QA", ["Kết luận lô tạm giữ: thả lô hoặc lập phiếu hủy", "Ghi lý do vào nhật ký lô"], "Trong ngày", "không còn lô tạm giữ còn tồn", "Tồn kho › Theo lô"),
            [CommonService.AlertCode.QcIncomingPending] = new("QA", "Chất lượng", "QA", ["Kết luận QC đầu vào cho phiếu nhập đang chờ"], "Trước khi xuất kho", "phiếu nhập hết trạng thái Chờ QC", "QC đầu vào"),
            [CommonService.AlertCode.TemperatureOutOfRange] = new("QA", "Chất lượng", "QA, Trưởng bếp", ["Kiểm tra thiết bị, đo lại nhiệt độ", "Cách ly hàng trong khu vực nếu vượt ngưỡng kéo dài"], "Ngay", "không còn lần đo ngoài ngưỡng trong cửa sổ theo dõi", "Nhật ký nhiệt độ"),
            [CommonService.AlertCode.ComplaintOverdue] = new("CSKH", "CSKH", "CSKH, Kinh doanh", ["Liên hệ khách, cập nhật hướng xử lý phản ánh quá hạn"], "Ngay", "phản ánh quá hạn đã xử lý / đóng", "Phản ánh khách hàng"),
            [CommonService.AlertCode.ComplaintOpen] = new("CSKH", "CSKH", "CSKH", ["Phân công và cập nhật tiến độ phản ánh đang mở"], "Trong hạn xử lý", "phản ánh đã xử lý / đóng", "Phản ánh khách hàng"),
            [CommonService.AlertCode.OutboundFailed] = new("VIVI", "IT", "IT", ["Kiểm tra kênh gửi (Telegram / Zalo / email) và gửi lại tin lỗi"], "Trong ngày", "không còn tin gửi lỗi", "Tin nhắn gửi đi"),
            [CommonService.AlertCode.AccountResignedOpen] = new("HR", "Nhân sự", "IT, Nhân sự", ["Khóa tài khoản của nhân viên đã nghỉ"], "Trong ngày", "tài khoản đã khóa", "Tài khoản"),
            [CommonService.AlertCode.ShiftShort] = new("HR", "Nhân sự", "Nhân sự, Trưởng bộ phận", ["Bổ sung người vào ca đang thiếu"], "Trước ngày làm", "ca đủ người", "Phân ca"),
            [CommonService.AlertCode.CleanTraysShort] = new("PKG", "Hộp khay", "Tổ vệ sinh, Giao vận", ["Ưu tiên vệ sinh khay cho chuyến sáng mai", "Báo Giao vận nếu phải dùng hộp thay thế"], "Trước ca giao", "đủ khay sạch cho ngày mai", "Vệ sinh khay hộp"),
            [CommonService.AlertCode.RouteNotReady] = new("LOG", "Giao vận", "Điều phối giao vận", ["Kiểm tra đóng gói / xếp xe chuyến sắp xuất bến"], "Trước giờ xuất bến", "chuyến đủ hàng hoặc đã xuất bến", "Điều phối giao hàng"),
        };

        // ---------------------------------------------------------------- 12 bước QT-SX-01
        private sealed record StepDef(int No, string Name, string Dept, string Agent, string Screen, string ScreenName, string[] Todo, string Owner, string Due, string Close, bool AlertBacked);

        /// <summary>QT-SX-01: đơn hàng → báo cáo. Bước có cảnh báo tương ứng (5, 8, 11) không sinh thêm việc (việc đã sinh từ cảnh báo).</summary>
        private static StepDef[] Defs =>
        [
            new(1, "Nhận & chốt đơn", "Kinh doanh", "SALES", "crm-order", "Đơn đặt suất", ["Liên hệ khách chốt số suất trước giờ khóa đơn"], "Kinh doanh", "Trước giờ khóa đơn", "đơn của ngày đã xác nhận / khóa", false),
            new(2, "Thực đơn", "Bếp / Kinh doanh", "PLAN", "menu-menu", "Thực đơn tuần", ["Lập / duyệt thực đơn cho ngày"], "Trưởng bếp, Kinh doanh", "Trước giờ khóa đơn", "thực đơn của ngày đã duyệt", false),
            new(3, "Đổi món chờ khách", "Kinh doanh", "SALES", "menu-menu", "Thực đơn tuần", ["Xử lý đề xuất đổi món đang chờ"], "Kinh doanh", "Trước giờ khóa đơn", "không còn đề xuất đổi món chờ", false),
            new(4, "Kế hoạch sản xuất", "Bếp", "PLAN", "prod-plan", "Kế hoạch sản xuất", ["Lập / duyệt kế hoạch sản xuất theo đơn đã khóa"], "Kế hoạch sản xuất", "Trước ca nấu", "kế hoạch của ngày đã duyệt", false),
            new(5, "Nguyên liệu & thiếu hụt", "Kho", "WH", "scm-inv-balance", "Tồn kho", [], "Thủ kho, Mua hàng", "Trong ngày", "đủ nguyên liệu, hết lô quá hạn", true),
            new(6, "Mua hàng (PO)", "Mua hàng", "PROC", "scm-po", "Đơn mua hàng", ["Duyệt / gửi đơn mua đang chờ", "Gọi NCC giao các đơn quá hẹn"], "Mua hàng", "Trong ngày", "không còn PO chờ duyệt / quá hẹn", false),
            new(7, "Sản xuất", "Bếp", "KITCHEN", "prod-batch", "Mẻ nấu", ["Kiểm tra mẻ lỗi / tạm giữ, nấu bù nếu thiếu"], "Trưởng ca bếp", "Trong ca", "mẻ của ngày đạt", false),
            new(8, "An toàn thực phẩm", "Chất lượng", "QA", "qc-temperature", "Nhật ký nhiệt độ", [], "QA", "Ngay", "nhiệt độ đạt, không mẻ tạm giữ", true),
            new(9, "Giao vận", "Giao vận", "LOG", "log-route", "Chuyến giao hàng", ["Lập chuyến / xếp xe cho đơn của ngày"], "Điều phối giao vận", "Trước giờ xuất bến", "chuyến của ngày đã lập", false),
            new(10, "Thu hồi hộp khay", "Hộp khay", "PKG", "log-packaging-ledger", "Sổ khay / hộp", ["Đối chiếu hộp thất thoát với khách"], "Giao vận, Kế toán", "Cuối ngày", "không còn hộp thất thoát chưa đối chiếu", false),
            new(11, "Phản ánh khách", "CSKH", "CSKH", "log-complaint", "Phản ánh khách hàng", [], "CSKH", "Trong hạn xử lý", "phản ánh đã đóng", true),
            new(12, "Báo cáo cuối ngày", "Ban giám đốc", "VIVI", "dash-exec", "Bảng điều hành", ["Kiểm tra job Báo cáo cuối ngày (Job tự động)"], "IT", "17:00", "báo cáo cuối ngày đã gửi", false),
        ];

        private static AgentFlowStepDayModel NewStep(StepDef d, string status, string note) => new()
        {
            No = d.No, Name = d.Name, Dept = d.Dept, Agent = d.Agent, AgentName = AgentName(d.Agent), Screen = d.Screen, ScreenName = d.ScreenName, Status = status, Note = note,
        };

        /// <summary>Trạng thái 12 bước của ngày <paramref name="day"/> – tính từ bảng sẵn có (không phụ thuộc người xem).</summary>
        public async Task<List<AgentFlowStepDayModel>> ComputeStepsAsync(DateTime day)
        {
            var db = unitOfWork.DbContext;
            DateTime d0 = day.Date, d1 = d0.AddDays(1), today = clock.Today, now = clock.Now;
            bool future = d0 > today, past = d0 < today;
            var D = Defs;
            var steps = new List<AgentFlowStepDayModel>();

            // 1. Đơn hàng
            var orders = await db.Order.AsNoTracking().Where(o => !o.IsDeleted && o.MealDate >= d0 && o.MealDate < d1 && o.Status != OrderStatus.Cancelled)
                .Select(o => new { o.Status, o.TotalQuantity }).ToListAsync();
            int nOrder = orders.Count, unconfirmed = orders.Count(o => o.Status is OrderStatus.Draft or OrderStatus.Submitted), confirmed = orders.Count(o => o.Status == OrderStatus.Confirmed);
            decimal meals = orders.Sum(o => o.TotalQuantity ?? 0);
            steps.Add(nOrder == 0
                ? NewStep(D[0], future ? "BAD" : "WAIT", future ? $"Chưa có đơn giao {d0:dd/MM} – cần chốt trước giờ khóa đơn" : $"Không có đơn giao {d0:dd/MM}")
                : unconfirmed > 0 ? NewStep(D[0], future ? "WARN" : "BAD", $"{unconfirmed} / {nOrder} đơn chưa xác nhận")
                : confirmed > 0 && future ? NewStep(D[0], "RUN", $"{nOrder} đơn đã xác nhận · {Vn(meals)} suất · chờ khóa đơn")
                : NewStep(D[0], "OK", $"{nOrder} đơn · {Vn(meals)} suất"));

            // 2. Thực đơn chung có hiệu lực ngày
            var menus = await db.Menu.AsNoTracking().Where(m => !m.IsDeleted && m.MenuType == MenuScope.Common && m.Status != MenuStatus.Expired
                    && m.EffectiveFrom != null && m.EffectiveFrom < d1 && (m.EffectiveTo == null || m.EffectiveTo >= d0))
                .Select(m => new { m.Code, m.Status }).ToListAsync();
            var menuOk = menus.FirstOrDefault(m => m.Status is MenuStatus.Approved or MenuStatus.Published);
            steps.Add(menuOk != null ? NewStep(D[1], "OK", $"Thực đơn {menuOk.Code} đã duyệt")
                : menus.Count > 0 ? NewStep(D[1], "WARN", $"Thực đơn {menus[0].Code} chưa duyệt")
                : NewStep(D[1], past ? "WAIT" : "BAD", $"Chưa lập thực đơn ngày {d0:dd/MM}"));

            // 3. Đề xuất đổi món chờ
            int changes = await db.MenuChangeRequest.AsNoTracking().CountAsync(r => !r.IsDeleted && r.MealDate >= d0 && r.MealDate < d1 && r.RequestStatus == MenuChangeStatus.Pending);
            steps.Add(changes > 0 ? NewStep(D[2], past ? "BAD" : "WARN", $"{changes} đề xuất đổi món đang chờ") : NewStep(D[2], "OK", "Không có đề xuất chờ"));

            // 4. Kế hoạch sản xuất
            var plans = await db.ProductionPlan.AsNoTracking().Where(p => !p.IsDeleted && p.ProductionDate >= d0 && p.ProductionDate < d1).Select(p => new { p.Id, p.Status }).ToListAsync();
            var planIds = plans.Select(p => p.Id).ToList();
            steps.Add(plans.Count == 0
                ? (nOrder == 0 ? NewStep(D[3], "WAIT", "Chưa có đơn – chưa lập kế hoạch")
                    : future && unconfirmed + confirmed > 0 ? NewStep(D[3], "RUN", "Tự lập sau khi khóa đơn")
                    : NewStep(D[3], "BAD", "Đơn đã khóa nhưng chưa có kế hoạch sản xuất"))
                : plans.Any(p => p.Status == ProductionPlanStatus.Draft) ? NewStep(D[3], "WARN", $"{plans.Count(p => p.Status == ProductionPlanStatus.Draft)} kế hoạch chưa duyệt")
                : NewStep(D[3], "OK", $"{plans.Count} kế hoạch đã duyệt"));

            // 5. Nguyên liệu: thiếu theo MRP của ngày, dưới định mức, lô quá hạn còn tồn
            int shortage = planIds.Count == 0 ? 0 : await db.MaterialRequirement.AsNoTracking().CountAsync(m => !m.IsDeleted && planIds.Contains(m.ProductionPlanId) && m.PurchaseQuantity > 0);
            var shared = await common.GetSharedCounts(null) ?? new SharedAlertCountsModel();
            int expired = await db.MaterialLot.AsNoTracking().CountAsync(l => !l.IsDeleted && l.Status == MaterialLotStatus.Available && l.ExpiryDate != null && l.ExpiryDate < today
                && db.InventoryBalance.Any(b => !b.IsDeleted && b.MaterialLotId == l.Id && b.OnHandQuantity > 0));
            var parts5 = new List<string>();
            if (shortage > 0) parts5.Add($"thiếu {shortage} nguyên liệu theo nhu cầu");
            if (shared.StockBelowMin > 0) parts5.Add($"{shared.StockBelowMin} nguyên liệu dưới định mức");
            if (expired > 0) parts5.Add($"{expired} lô quá hạn còn tồn");
            steps.Add(shortage > 0 && !past ? NewStep(D[4], "BAD", Cap(string.Join(" · ", parts5)))
                : parts5.Count > 0 ? NewStep(D[4], "WARN", Cap(string.Join(" · ", parts5)))
                : NewStep(D[4], "OK", "Đủ nguyên liệu, không lô quá hạn"));

            // 6. Đơn mua
            var pos = await db.PurchaseOrder.AsNoTracking().Where(p => !p.IsDeleted && p.Status != PurchaseOrderStatus.Cancelled && p.Status != PurchaseOrderStatus.Closed
                    && p.Status != PurchaseOrderStatus.Received && (p.ExpectedDeliveryDate ?? p.ExpectedDate) != null && (p.ExpectedDeliveryDate ?? p.ExpectedDate) < d1)
                .Select(p => new { p.Status, Due = p.ExpectedDeliveryDate ?? p.ExpectedDate }).ToListAsync();
            int poPending = pos.Count(p => p.Status is PurchaseOrderStatus.Draft or PurchaseOrderStatus.PendingApproval && p.Due >= d0);
            int poLate = pos.Count(p => p.Status is PurchaseOrderStatus.Approved or PurchaseOrderStatus.Sent or PurchaseOrderStatus.PartiallyReceived && p.Due < (today < d0 ? today : d0));
            int poOpen = pos.Count(p => p.Due >= d0);
            steps.Add(poLate > 0 ? NewStep(D[5], "WARN", $"{poLate} PO quá hẹn giao" + (poPending > 0 ? $" · {poPending} PO chờ duyệt" : string.Empty))
                : poPending > 0 ? NewStep(D[5], "WARN", $"{poPending} PO giao ngày này chưa duyệt / gửi")
                : NewStep(D[5], "OK", poOpen > 0 ? $"{poOpen} PO hẹn giao ngày này" : "Không có PO chờ"));

            // 7. Mẻ nấu
            var poIds = planIds.Count == 0 ? [] : await db.ProductionOrder.AsNoTracking().Where(o => !o.IsDeleted && planIds.Contains(o.ProductionPlanId)).Select(o => o.Id).ToListAsync();
            var batches = poIds.Count == 0 ? [] : await db.ProductionBatch.AsNoTracking().Where(b => !b.IsDeleted && poIds.Contains(b.ProductionOrderId) && b.Status != ProductionBatchStatus.Cancelled)
                .Select(b => new { b.Status, b.QcStatus }).ToListAsync();
            int bDone = batches.Count(b => b.Status is ProductionBatchStatus.Passed or ProductionBatchStatus.Packed);
            int bBad = batches.Count(b => b.Status == ProductionBatchStatus.Failed || b.QcStatus is BatchQcStatus.Hold or BatchQcStatus.Reject);
            steps.Add(batches.Count == 0
                ? NewStep(D[6], plans.Count > 0 && !future ? "BAD" : "WAIT", plans.Count > 0 && !future ? "Có kế hoạch nhưng chưa có mẻ nấu" : $"Chưa có mẻ nấu ngày {d0:dd/MM}")
                : bBad > 0 ? NewStep(D[6], "BAD", $"{bBad} mẻ lỗi / tạm giữ · {bDone} / {batches.Count} mẻ đạt")
                : bDone == batches.Count ? NewStep(D[6], "OK", $"{bDone} / {batches.Count} mẻ đạt")
                : NewStep(D[6], past ? "WARN" : "RUN", $"{bDone} / {batches.Count} mẻ đạt"));

            // 8. ATTP: nhiệt độ ngoài ngưỡng trong ngày, mẻ tạm giữ
            int tempBad = future ? 0 : await db.TemperatureLog.AsNoTracking().CountAsync(t => !t.IsDeleted && t.IsWithinRange == false && t.MeasuredAt >= d0 && t.MeasuredAt < d1);
            int tempAll = future ? 0 : await db.TemperatureLog.AsNoTracking().CountAsync(t => !t.IsDeleted && t.MeasuredAt >= d0 && t.MeasuredAt < d1);
            int hold = batches.Count(b => b.QcStatus == BatchQcStatus.Hold);
            steps.Add(tempBad > 0 ? NewStep(D[7], "BAD", $"{tempBad} lần đo ngoài ngưỡng")
                : hold > 0 ? NewStep(D[7], "WARN", $"{hold} mẻ tạm giữ chờ QA")
                : future || tempAll == 0 ? NewStep(D[7], "WAIT", "Chưa có lần đo nhiệt độ trong ngày")
                : NewStep(D[7], "OK", $"Nhiệt độ đạt ({tempAll} lần đo)"));

            // 9. Chuyến giao
            var routes = await db.DeliveryRoute.AsNoTracking().Where(r => !r.IsDeleted && r.DeliveryDate >= d0 && r.DeliveryDate < d1).Select(r => r.Status).ToListAsync();
            int rDone = routes.Count(s => s == DeliveryRouteStatus.Completed), rMoving = routes.Count(s => s is DeliveryRouteStatus.Loading or DeliveryRouteStatus.InTransit);
            steps.Add(routes.Count == 0
                ? (nOrder == 0 ? NewStep(D[8], "WAIT", "Không có đơn cần giao") : NewStep(D[8], past ? "BAD" : "WARN", $"Chưa lập chuyến cho {nOrder} đơn"))
                : rDone == routes.Count ? NewStep(D[8], "OK", $"{rDone} chuyến hoàn tất")
                : past ? NewStep(D[8], "WARN", $"{routes.Count - rDone} / {routes.Count} chuyến chưa hoàn tất")
                : NewStep(D[8], future && rMoving == 0 ? "OK" : "RUN", future && rMoving == 0 ? $"{routes.Count} chuyến đã lập" : $"{rDone} / {routes.Count} chuyến hoàn tất · {rMoving} đang chạy"));

            // 10. Hộp khay trong ngày
            var ledger = future ? [] : await db.PackagingLedger.AsNoTracking().Where(p => !p.IsDeleted && p.TransactionAt >= d0 && p.TransactionAt < d1)
                .Select(p => new { p.TransactionType, p.Quantity }).ToListAsync();
            decimal outQ = ledger.Where(x => x.TransactionType == PackagingTransactionType.Out).Sum(x => x.Quantity), inQ = ledger.Where(x => x.TransactionType == PackagingTransactionType.In).Sum(x => x.Quantity),
                lossQ = ledger.Where(x => x.TransactionType == PackagingTransactionType.Loss).Sum(x => x.Quantity);
            steps.Add(ledger.Count == 0 ? NewStep(D[9], "WAIT", "Chưa có số hộp xuất / thu hồi trong ngày")
                : lossQ > 0 ? NewStep(D[9], "WARN", $"Xuất {Vn(outQ)} · thu {Vn(inQ)} · thất thoát {Vn(lossQ)}")
                : NewStep(D[9], "OK", $"Xuất {Vn(outQ)} · thu {Vn(inQ)}"));

            // 11. Phản ánh: đang mở (quá hạn = có vấn đề), mới trong ngày
            var complaints = await db.CustomerComplaint.AsNoTracking().Where(c => !c.IsDeleted && (c.ComplaintStatus == ComplaintStatus.Open || c.ComplaintStatus == ComplaintStatus.InProgress) && c.ReceivedAt < d1)
                .Select(c => c.SlaDueAt).ToListAsync();
            int cOver = complaints.Count(s => s != null && s < now);
            steps.Add(cOver > 0 ? NewStep(D[10], "BAD", $"{cOver} phản ánh quá hạn xử lý")
                : complaints.Count > 0 ? NewStep(D[10], "WARN", $"{complaints.Count} phản ánh đang mở")
                : NewStep(D[10], "OK", "Không có phản ánh tồn"));

            // 12. Báo cáo cuối ngày (job DailyReport)
            var report = await db.AnalyticsRefreshLog.AsNoTracking().Where(r => r.JobCode == "DailyReport" && r.StartedAt >= d0 && r.StartedAt < d1)
                .OrderByDescending(r => r.StartedAt).Select(r => new { r.RunStatus, r.CompletedAt, r.StartedAt }).FirstOrDefaultAsync();
            steps.Add(report?.RunStatus == "Succeeded" ? NewStep(D[11], "OK", $"Đã gửi lúc {(report.CompletedAt ?? report.StartedAt):HH:mm}")
                : report?.RunStatus == "Failed" ? NewStep(D[11], "BAD", "Job Báo cáo cuối ngày lỗi")
                : past ? NewStep(D[11], "WARN", "Chưa gửi báo cáo cuối ngày")
                : NewStep(D[11], "WAIT", "Gửi lúc 17:00"));
            return steps;
        }

        private static readonly System.Globalization.CultureInfo Vi = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");

        private static string Vn(decimal v) => v.ToString("#,0.##", Vi);

        private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

        // ---------------------------------------------------------------- quyền
        private async Task<(Guid UserId, UserPermissions Perms)> ViewerAsync()
        {
            var actor = currentActor.RequireActor();
            return (actor.UserId, await permissions.GetAsync(actor.UserId));
        }

        // ---------------------------------------------------------------- Giao việc
        private sealed record Candidate(AgentTaskModel Task, string Screen);

        private async Task<(List<AgentTaskModel> Tasks, int Approvals, Dictionary<string, KnowledgeTaskMark> Marks)> BuildTasksAsync(KnowledgeTaskDone? done)
        {
            var (_, perms) = await ViewerAsync();
            DateTime today = clock.Today;
            bool Can(string screen, PermissionAction a) => permissions.IsAllowed(perms, screen, a);

            var alerts = (await common.GetAlerts(null)).ListData ?? [];
            int approvals = alerts.FirstOrDefault(a => a.Code == CommonService.AlertCode.ApprovalPending)?.Count ?? 0;
            var cands = new List<Candidate>();
            foreach (var a in alerts)
            {
                if (!AlertTasks.TryGetValue(a.Code, out var m))
                {
                    continue;
                }
                string screen = CommonService.AlertScreens[a.Code];
                cands.Add(new Candidate(new AgentTaskModel
                {
                    Key = $"ALERT:{a.Code}:{today:yyyyMMdd}", Source = "ALERT", Severity = a.Severity == CommonService.SeverityHigh ? "HOT" : "WARN", Title = a.Title, Detail = a.Detail,
                    ListTodo = [.. m.Todo], Agent = m.Agent, Owner = m.Owner, Dept = m.Dept, Due = m.Due, CloseWhen = m.Close, Route = a.Route, SourceName = m.SourceName,
                    Mine = Can(screen, PermissionAction.Add),
                }, screen));
            }

            // Bước luồng kẹt của hôm nay + ngày mai (bước có cảnh báo riêng không sinh thêm việc).
            var defs = Defs.ToDictionary(x => x.No);
            var flowSteps = new HashSet<int>();
            foreach (DateTime day in new[] { today, today.AddDays(1) })
            {
                foreach (var s in (await ComputeStepsAsync(day)).Where(s => s.Status is "BAD" or "WARN" && !defs[s.No].AlertBacked))
                {
                    // Bước kẹt cả hôm nay lẫn ngày mai (vd PO quá hẹn) → 1 việc (hôm nay).
                    if (!flowSteps.Add(s.No))
                    {
                        continue;
                    }
                    var d = defs[s.No];
                    if (!Can(d.Screen, PermissionAction.View))
                    {
                        continue;
                    }
                    cands.Add(new Candidate(new AgentTaskModel
                    {
                        Key = $"FLOW:{day:yyyyMMdd}:{s.No:00}", Source = "FLOW", Severity = s.Status == "BAD" ? "HOT" : "WARN",
                        Title = $"Bước {s.No} {s.Name} ({day:dd/MM}): {s.Note}", Detail = $"Luồng liên phòng QT-SX-01 ngày {day:dd/MM/yyyy} – bước {s.Name} {(s.Status == "BAD" ? "có vấn đề" : "cần chú ý")}.",
                        ListTodo = [.. d.Todo], Agent = d.Agent, Owner = d.Owner, Dept = s.Dept, Due = day == today ? "Hôm nay" : d.Due, CloseWhen = d.Close, Route = "#/" + d.Screen, SourceName = d.ScreenName,
                        Mine = Can(d.Screen, PermissionAction.Add),
                    }, d.Screen));
                }
            }

            // Người xem kiểm được nguồn của khóa nào (để tự đóng): cảnh báo → quyền Xem màn cảnh báo; bước luồng → quyền Xem màn của bước.
            bool CanJudge(string key)
            {
                string[] p = key.Split(':');
                if (p.Length < 3)
                {
                    return false;
                }
                if (p[0] == "ALERT")
                {
                    return p[2] == today.ToString("yyyyMMdd") && CommonService.AlertScreens.TryGetValue(p[1], out var sc) && AlertTasks.ContainsKey(p[1]) && Can(sc, PermissionAction.View);
                }
                return p[0] == "FLOW" && int.TryParse(p[2], out int no) && defs.TryGetValue(no, out var d) && Can(d.Screen, PermissionAction.View)
                    && (p[1] == today.ToString("yyyyMMdd") || p[1] == today.AddDays(1).ToString("yyyyMMdd"));
            }

            var marks = await knowledge.UpdateTaskLedger(today, [.. cands.Select(c => new KnowledgeTaskOpen(c.Task.Key, c.Task.Title, c.Task.Agent))], CanJudge, done);
            var list = new List<AgentTaskModel>();
            foreach (var c in cands)
            {
                var t = c.Task;
                if (marks.TryGetValue(t.Key, out var mk))
                {
                    t.OpenedAt = mk.OpenedAt;
                    if (mk.State == "xu-ly")
                    {
                        t.Status = "DONE"; t.ClosedAt = mk.ClosedAt; t.ClosedBy = mk.By; t.Note = mk.Note;
                    }
                }
                list.Add(t);
            }
            // Việc đã đóng trong ngày (tự đóng / đã xử lý) mà người xem kiểm được nguồn – hiện ở bộ lọc "Đã xử lý hôm nay".
            var present = list.Select(t => t.Key).ToHashSet(StringComparer.Ordinal);
            foreach (var mk in marks.Values.Where(m => m.State != "mo" && !present.Contains(m.Key) && CanJudge(m.Key)))
            {
                list.Add(new AgentTaskModel
                {
                    Key = mk.Key, Source = mk.Key.StartsWith("FLOW:", StringComparison.Ordinal) ? "FLOW" : "ALERT", Severity = "WARN", Title = mk.Title, Agent = mk.Agent,
                    Status = mk.State == "xu-ly" ? "DONE" : "AUTO", OpenedAt = mk.OpenedAt, ClosedAt = mk.ClosedAt, ClosedBy = mk.By, Note = mk.Note,
                    CloseWhen = mk.State == "tu-dong" ? "nguồn đã hết điều kiện" : null,
                });
            }

            // Việc agent ghi ở thư mục 98 (theo quyền thư mục kho của người xem).
            var notes = (await knowledge.GetTasks(true)).ListData ?? [];
            foreach (var n in notes)
            {
                bool closed = n.Status == "xong";
                if (closed && n.CreatedAt.Date < today.AddDays(-1))
                {
                    continue;
                }
                list.Add(new AgentTaskModel
                {
                    Key = "NOTE:" + n.Path, Source = "NOTE", Severity = n.Overdue ? "HOT" : "WARN", Title = n.Title, Detail = Cut(n.Content, 400), Agent = n.Agent, Owner = n.Owner,
                    Dept = "Việc agent theo dõi", Due = n.Due is { } due ? due.ToString("dd/MM/yyyy") : null, CloseWhen = "người phụ trách đánh dấu Đã xử lý", Route = "#/knowledge?path=" + Uri.EscapeDataString(n.Path),
                    SourceName = "Kho tri thức › 98 Công việc", Status = closed ? "DONE" : "OPEN", OpenedAt = n.CreatedAt == DateTime.MinValue ? null : n.CreatedAt, Path = n.Path, Mine = false,
                });
            }
            return (list, approvals, marks);
        }

        private static string? Cut(string? s, int n) => s == null ? null : s.Length <= n ? s : s[..n].TrimEnd() + "…";

        private static int Rank(AgentTaskModel t) => (t.Status == "OPEN" ? 0 : 10) + (t.Severity == "HOT" ? 0 : 1);

        public async Task<IResult<AgentTaskBoardModel>> GetTasks(bool includeDone)
        {
            var (list, approvals, _) = await BuildTasksAsync(null);
            var open = list.Where(t => t.Status == "OPEN").ToList();
            var board = new AgentTaskBoardModel
            {
                Date = clock.Today, Open = open.Count, Hot = open.Count(t => t.Severity == "HOT"), Warn = open.Count(t => t.Severity != "HOT"),
                ClosedToday = list.Count(t => t.Status != "OPEN" && t.ClosedAt?.Date == clock.Today), ApprovalsPending = approvals,
                ListTask = [.. list.Where(t => includeDone || t.Status == "OPEN").OrderBy(Rank).ThenBy(t => t.OpenedAt ?? DateTime.MaxValue)],
                ListDept = [.. open.GroupBy(t => t.Dept ?? "Khác").Select(g => new AgentTaskDeptModel { Dept = g.Key, Count = g.Count() }).OrderByDescending(x => x.Count).ThenBy(x => x.Dept)],
            };
            return Result<AgentTaskBoardModel>.Success(board, ConstantHelper.Message.GetAllSuccessfully);
        }

        /// <summary>"Đã xử lý": việc cảnh báo / bước luồng → dòng [xu-ly] trong sổ việc (người + ghi chú); việc thư mục 98 → trạng thái xong của chính ghi chú đó. Chỉ việc người xem đang thấy.</summary>
        public async Task<IResult<AgentTaskModel>> CloseTask(AgentCloseTaskRequest request)
        {
            string key = (request.Key ?? string.Empty).Trim();
            string? note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
            if (note != null && KnowledgeVault.ScanSensitive(note) is { } pii)
            {
                return Result<AgentTaskModel>.Fail($"Không lưu: {pii}");
            }
            if (key.StartsWith("NOTE:", StringComparison.Ordinal))
            {
                var r = await knowledge.SetTask(new KnowledgeTaskSetRequest { Path = key[5..], Status = "xong", Note = note ?? "đã xử lý (Trung tâm Agent)" });
                return r.ResponseCode == 200 ? Result<AgentTaskModel>.Success(new AgentTaskModel { Key = key, Source = "NOTE", Status = "DONE", ClosedAt = clock.Now, Note = note }, "Đã ghi Đã xử lý vào ghi chú việc")
                    : Status<AgentTaskModel>(r.ResponseCode, r.ResponseMessage ?? "Không lưu được");
            }
            var (list, _, _) = await BuildTasksAsync(null);
            var task = list.FirstOrDefault(t => t.Key == key);
            if (task == null)
            {
                return Status<AgentTaskModel>(404, "Không có việc này (đã tự đóng hoặc ngoài quyền của anh / chị)");
            }
            if (task.Status != "OPEN")
            {
                return Result<AgentTaskModel>.Success(task, task.Status == "AUTO" ? "Việc đã tự đóng – nguồn đã xử lý" : "Việc đã được đánh dấu Đã xử lý");
            }
            var actor = currentActor.RequireActor();
            string user = await unitOfWork.DbContext.User.AsNoTracking().Where(u => u.Id == actor.UserId).Select(u => u.UserName).FirstOrDefaultAsync() ?? "người dùng";
            var (after, _, _) = await BuildTasksAsync(new KnowledgeTaskDone(task.Key, task.Title, task.Agent, user, note));
            var res = after.FirstOrDefault(t => t.Key == key) ?? task;
            return Result<AgentTaskModel>.Success(res, "Đã ghi Đã xử lý – ghi chú lưu vào kho tri thức (98 Công việc)");
        }

        // ---------------------------------------------------------------- Luồng liên phòng (ngày)
        private static readonly Dictionary<string, string> KindVi = new() { ["EVENT"] = "Sự kiện", ["CALL"] = "Agent đọc", ["PROPOSAL"] = "Đề xuất", ["DECISION"] = "Người duyệt", ["RESULT"] = "Kết quả", ["MESSAGE"] = "Tin gửi" };

        public async Task<IResult<AgentFlowDayModel>> GetFlowDay(DateTime? date)
        {
            var (_, perms) = await ViewerAsync();
            DateTime day = (date ?? clock.Today).Date;
            var steps = await ComputeStepsAsync(day);
            var board = (await flows.GetFlows(day)).Data ?? new AgentFlowBoardModel();
            var marks = await knowledge.UpdateTaskLedger(clock.Today, [], _ => false, null);
            foreach (var s in steps)
            {
                s.CanOpen = permissions.IsAllowed(perms, s.Screen, PermissionAction.View);
                // Chuỗi xử lý theo CorrelationId (AI-11): luồng có agent của bước; việc đã sinh; kiểm tra lúc tính.
                foreach (var f in board.ListFlow.Where(f => f.Agents.Contains(s.Agent)).Take(3))
                {
                    s.ListChain.AddRange(f.Steps.Where(x => x.Agent == null || x.Agent == s.Agent).Take(6).Select(x => new AgentFlowChainModel { At = x.At, Kind = KindVi.GetValueOrDefault(x.Kind, x.Kind), Text = x.Title, Status = x.Status == "OK" ? "OK" : x.Status == "WAIT" ? "WAIT" : "FAIL" }));
                }
                if (marks.TryGetValue($"FLOW:{day:yyyyMMdd}:{s.No:00}", out var mk))
                {
                    s.ListChain.Add(new AgentFlowChainModel { At = mk.OpenedAt, Kind = "Giao việc", Text = $"\"{mk.Title}\" → {Defs[s.No - 1].Owner}", Status = "INFO" });
                    if (mk.State != "mo")
                    {
                        s.ListChain.Add(new AgentFlowChainModel { At = mk.ClosedAt, Kind = "Kết quả", Text = mk.State == "xu-ly" ? $"{mk.By} đánh dấu Đã xử lý{(mk.Note != null ? ": " + mk.Note : string.Empty)}" : "Tự đóng – nguồn đã xử lý", Status = "OK" });
                    }
                }
                s.ListChain.Add(new AgentFlowChainModel { At = clock.Now, Kind = "Kiểm tra", Text = $"Trạng thái tính lúc {clock.Now:HH:mm}: {s.Note}", Status = s.Status is "OK" ? "OK" : s.Status is "BAD" ? "FAIL" : s.Status is "WARN" ? "WAIT" : "INFO" });
                s.ListChain = [.. s.ListChain.OrderBy(c => c.At ?? DateTime.MaxValue)];
            }
            var m = new AgentFlowDayModel
            {
                Date = day, GeneratedAt = clock.Now, ListStep = steps, Ok = steps.Count(s => s.Status == "OK"), Run = steps.Count(s => s.Status == "RUN"), Warn = steps.Count(s => s.Status == "WARN"),
                Bad = steps.Count(s => s.Status == "BAD"), Wait = steps.Count(s => s.Status == "WAIT"),
            };
            return Result<AgentFlowDayModel>.Success(m, ConstantHelper.Message.GetAllSuccessfully);
        }

        // ---------------------------------------------------------------- Tổng quan + Chat
        private static AgentCenterAgentModel ToCenter(AgentSummaryModel a, int schedules) => new()
        {
            Code = a.Code, Name = a.Name, Short = AgentName(a.Code), Serves = a.Serves, Purpose = a.Purpose, Wave = a.Wave, Color = a.Color, Active = a.Active, Configured = a.Status != null,
            State = a.Active ? (a.HasKey ? "ok" : "key") : "off", ToolCount = a.ListTool.Count, ScheduleCount = schedules, CallsToday = a.CallsToday, DeniedToday = a.DeniedToday, FailedToday = a.FailedToday,
            LastAt = a.LastCallAt, ListAsk = [.. Asks.GetValueOrDefault(a.Code) ?? []],
            ListCan = [.. a.Purpose.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(Cap)],
        };

        public async Task<IResults<AgentCenterAgentModel>> GetChatAgents()
        {
            var list = (await admin.GetAll()).ListData ?? [];
            var sched = (await admin.GetSchedules()).ListData ?? [];
            return Results<AgentCenterAgentModel>.Success([.. list.Select(a => ToCenter(a, sched.Count(s => s.Agent == a.Code && s.Enabled)))], ConstantHelper.Message.GetAllSuccessfully);
        }

        public async Task<IResult<AgentOverviewModel>> GetOverview()
        {
            var list = (await admin.GetAll()).ListData ?? [];
            var sched = (await admin.GetSchedules()).ListData ?? [];
            var activity = (await admin.GetActivity(new AgentActivityRequest { PageSize = 200 })).Data?.ListRow ?? [];
            var agents = list.Select(a => ToCenter(a, sched.Count(s => s.Agent == a.Code && s.Enabled))).ToList();
            foreach (var a in agents)
            {
                var last = activity.FirstOrDefault(r => r.Agent == a.Code);
                if (last != null)
                {
                    a.LastAt ??= last.At;
                    a.LastLine = $"{last.At:dd/MM HH:mm} · {last.AskingUserName ?? (last.OnBehalfOf != null ? "Bot " + last.OnBehalfOf : "theo lịch")} · {last.Tool}";
                }
            }
            var (tasks, _, _) = await BuildTasksAsync(null);
            var open = tasks.Where(t => t.Status == "OPEN").ToList();
            foreach (var a in agents)
            {
                a.OpenTasks = open.Count(t => t.Agent == a.Code);
            }
            var steps = await ComputeStepsAsync(clock.Today);
            int notes = ((await knowledge.GetTree()).ListData ?? []).Sum(f => f.NoteCount);
            var today = activity.Where(r => r.At >= clock.Today).ToList();
            var m = new AgentOverviewModel
            {
                Date = clock.Today, GeneratedAt = clock.Now, ListAgent = agents, Running = agents.Count(a => a.State == "ok"), Total = agents.Count, CallsToday = agents.Sum(a => a.CallsToday),
                Askers = today.Select(r => r.AskingUserId?.ToString() ?? r.OnBehalfOf).Where(x => x != null).Distinct().Count(), FailedToday = agents.Sum(a => a.FailedToday), DeniedToday = agents.Sum(a => a.DeniedToday),
                SchedulesOn = sched.Count(s => s.Enabled && s.AgentActive), SchedulesTotal = sched.Count, KnowledgeNotes = notes, TasksOpen = open.Count, TasksHot = open.Count(t => t.Severity == "HOT"),
                FlowDate = clock.Today, FlowSteps = steps.Count, FlowAttention = steps.Count(s => s.Status is "BAD" or "WARN"),
                ListFeed = [.. activity.Take(8).Select(r => new AgentFeedModel
                {
                    At = r.At, Agent = r.Agent, Text = (r.Status == "Succeeded" ? "gọi " : r.Status == "Denied" ? "bị từ chối: " : "lỗi: ") + r.Tool + (r.Error is { Length: > 0 } e && r.Status != "Succeeded" ? $" ({Cut(e, 80)})" : string.Empty),
                    Who = r.AskingUserName ?? (r.OnBehalfOf != null ? "Bot " + r.OnBehalfOf : r.CorrelationId is { } c && c.StartsWith("sched:", StringComparison.Ordinal) ? "Lịch " + c.Split(':')[1] : "theo lịch"),
                    Status = r.Status,
                })],
            };
            return Result<AgentOverviewModel>.Success(m, ConstantHelper.Message.GetAllSuccessfully);
        }
    }
}
