# AI_AGENT_ARCHITECTURE – Trợ lý AI (Hermes) + ToolGateway + Kho tri thức (D-133)

> **Anh chốt 2026-11-26** ("đồng ý tạo task cho Claude Code phát triển"). Task: ROADMAP mục **★ AI – TRỢ LÝ AI**, các mã **AI-0 … AI-19**. Thay nội dung T30–T35, T43, T44, T50, T51 cũ.
> Bản gốc của tài liệu này là Claude Doc "Kiến trúc tích hợp agent, Hermes làm trung tâm điều khiển" (chat tài liệu, 2026-11-26). Mockup đã duyệt: `docs/frontend-handoff/assets/ai-agent/` (12 màn, mục 10).
> Luật cứng của `CLAUDE.md` vẫn áp dụng toàn bộ: khung code cũ (D-30), phân quyền cũ (D-31), không thêm bảng / cột, SQL Server 2022, không commit bí mật, agent không bao giờ ghi thẳng DB.
## 1. Trả lời nhanh

Hermes là bộ não điều phối. Nó chỉ nói chuyện với lõi AppDelicare qua một cổng duy nhất là `AppDelicare.ToolGateway`. Công cụ trong cổng gọi thẳng các Service sẵn có, và mọi lệnh ghi đều đi qua engine duyệt đang chạy.

| Câu hỏi | Trả lời ngắn | Chi tiết |
|---|---|---|
| Kiến trúc source code thế nào? | Thêm **1 project** `AppDelicare.ToolGateway` (MCP + REST). Thêm khoảng **6 file nghiệp vụ** vào Services, theo đúng khung cũ: `AgentWriteApprovalHandler` (handler thứ 17 cạnh 16 handler duyệt đang có), `KnowledgeService`, `AgentService`, exporter và job. Thêm **2 controller** `AgentController`, `KnowledgeController`. Thêm thư mục `agents/` chứa cấu hình Hermes. **Không thêm bảng hay cột nào.** | Mục 3 |
| Thêm bao nhiêu agent? | **13 agent**: 1 điều phối (Vivi), 11 agent phòng ban theo `AGENTS.md`, và 1 agent mới là **Thủ thư tri thức (KNOW)**. Lên dần trong 4 đợt, đợt đầu chỉ có 5 agent chỉ đọc. | Mục 4 |
| Phát sinh bao nhiêu tính năng? | **23 tính năng** chia 5 nhóm: cổng công cụ (7), agent và Hermes (5), trung tâm agent trên web (5), kho tri thức (5), kênh khách Zalo (1). Kèm khoảng 40 công cụ đọc và 22 công cụ ghi dạng đề xuất. | Mục 6 |
| Xây kho tri thức Obsidian ngay trong ứng dụng được không? | **Được, và nên làm.** Kho là thư mục markdown đúng chuẩn Obsidian, đặt trên một ổ dữ liệu dùng chung. Ứng dụng có màn "Kho tri thức" để đọc, tìm, soạn và duyệt ghi chú. Hermes đọc cùng thư mục đó. Ai thích Obsidian trên máy vẫn mở được bản đồng bộ. Không cần bảng mới. | Mục 7 |

Có một việc cần anh duyệt trước khi bắt đầu: project `ToolGateway` cần thư viện MCP cho .NET. `CLAUDE.md` quy định thư viện mới phải hỏi anh. Phương án thay thế nằm ở mục 9.


## 2. Kiến trúc tổng thể

Chỉ thêm hai phần mới là Hermes với các agent và `ToolGateway`. Lõi, kênh và database giữ nguyên.

*(Sơ đồ: kiến trúc tích hợp agent · 5 lớp)*

Người dùng web vẫn gọi thẳng lõi như hiện nay. Chỉ agent đi qua `ToolGateway`. Kho tri thức là thư mục dùng chung: lõi xuất vào đó, agent đọc qua cổng, Obsidian trên máy cá nhân chỉ là bản đồng bộ.


## 3. Kiến trúc source code

Nguyên tắc thiết kế: mọi nghiệp vụ của agent nằm trong `AppDelicare.Services` và viết theo mẫu cũ. `ToolGateway` chỉ là cửa vào thứ ba, đứng cạnh WebApi và Job. Nó chỉ chuyển lời gọi MCP thành lời gọi method Service. Nhờ vậy nút bấm trên web, job và agent chạy chung một đoạn code.

**Dùng lại, không thêm bảng:**

| Cần lưu | Bảng sẵn có | Cách dùng |
|---|---|---|
| Tài khoản agent | `User`, `UserGroup`, `UserClaim`, `UserRole` | Mỗi agent một `User` dịch vụ trong nhóm `AGENT_<MÃ>`. Quyền cấp trên chính ma trận quyền cũ |
| Danh mục agent, công tắc tắt | `IntegrationChannel` (kênh `HERMES`) + `BotConfiguration` | Giá trị cột: `BotCode` = mã agent, `BotStatus` = Active / Inactive, `TargetAudience` = phòng ban, `SecretReference` = tên khóa ngoài repo |
| Đề xuất ghi | `ApprovalRequest` + `ApprovalItem` + `ApprovalDecision` | `BusinessType = AGENT_WRITE`, `PayloadJson` = lệnh và bản xem trước, `CorrelationId` = lượt hội thoại, `DueAt` = hết hạn |
| Nhật ký lệnh, chi phí | `IntegrationLog` | `OperationCode` = tên công cụ, `IdempotencyKey` = mã xác nhận, `PayloadHash`, token và chi phí ghi trong payload |
| Tin nhắn chủ động | `OutboundMessage` | Agent xếp tin vào hàng đợi. Container `messaging` gửi đi như với mọi tin khác |
| Lịch sử sửa ghi chú | `AuditChange` | `EntityName = KnowledgeNote`, lưu đường dẫn, mã băm, người sửa. Cách này giống cách `BillingConfirmation` đang làm |
| Lần chạy job xuất kho | `AnalyticsRefreshLog` | Giống các job hiện có |

**Ba điểm bảo đảm không phá luật cứng:**

- `ToolGateway` không có câu SQL riêng. Nó chỉ gọi Service, nên `PermissionService`, `AuditChange`, sổ chỉ ghi thêm và `ICurrentActor` đều áp dụng tự động.
- Hermes không có chuỗi kết nối DB (D-62).
- Thêm test kiểm chứng: gọi `propose_*` không được làm thay đổi bảng nghiệp vụ nào trước khi có `ApprovalDecision`.


## 4. Danh mục agent

Hệ thống có 13 agent: Vivi điều phối, 11 agent phòng ban và Thủ thư tri thức. Mỗi agent phục vụ đúng một nhóm vai trò trong `ORG_STRUCTURE.md` và chỉ thấy công cụ của mảng mình.

Bốn quy tắc chung cho mọi agent:

- Hạn chót do job của lõi lo.
- Agent không tự quyết định dữ liệu.
- Mọi lệnh ghi đều là đề xuất chờ người có quyền xác nhận.
- Agent lỗi hoặc tắt thì nghiệp vụ vẫn chạy đúng giờ.

| Mã | Agent | Mục đích | Việc chính | Đề xuất ghi → người xác nhận | Đợt |
|---|---|---|---|---|---|
| VIVI | Điều phối trung tâm | Ban GĐ nắm tình hình trong một tin; không việc quá hạn nào mà không ai biết | Nhận câu hỏi và sự kiện, giao việc cho agent phòng ban, tổng hợp bản tin 07:00 và 17:00, theo dõi đề xuất và duyệt quá hạn | Không ghi; chuyển cho agent phòng ban | 1 |
| SALES | Kinh doanh & hợp đồng | Không khách nào chưa chốt lúc 20:00; hợp đồng không hết hạn bất ngờ | Khách chưa đăng ký tuần, dự báo lệch, đơn phát sinh, hợp đồng sắp hết hạn, sức khỏe khách hàng | `propose_order_change` → TP Kinh doanh (sau 20:00 thêm duyệt quá giờ chốt) | 2 |
| PLAN | Kế hoạch & thực đơn | Kế hoạch 7 ngày tới không thiếu nguyên liệu, thực đơn tuần chốt đúng hạn | Giải thích ngày "Bị giữ lại", gợi ý món rẻ hơn khi thiếu giá, đánh giá ảnh hưởng khi đổi món | `propose_production_plan`, `propose_menu_change` → Trưởng bếp | 3 |
| PROC | Thu mua | Không thiếu nguyên liệu cho ca nào, giá mua theo bảng giá | Đọc thiếu hụt sau MRP, chọn NCC theo quy tắc gợi ý chung, theo dõi PO chờ gửi và hàng trễ | `propose_purchase_order` → TP Vận hành; từ 50 triệu thì Giám đốc (D-11) | 2 |
| WH | Kho | Tồn đúng, không lô hết hạn trong kho, xuất đúng FEFO | Báo cáo tồn buổi sáng, lô cận date, lô bị chặn, lệch kiểm kê | `propose_goods_receipt`, `propose_issue`, `propose_stock_count` → Thủ kho (lệch trên 2% thì cần duyệt) | 1 |
| KITCHEN | Bếp & sản xuất | Mẻ xong đúng giờ xuất bếp, đúng sản lượng | Tiến độ theo dây chuyền, mẻ trễ, sản lượng ca, đóng gói | `propose_batch_event` → Trưởng ca | 1 (chỉ đọc), ghi từ đợt 2 |
| QA | ATTP & truy xuất | 100% mẻ đạt từ 70°C, giao từ 60°C, có lưu mẫu, đủ checklist | Cảnh báo nhiệt độ (kể cả IoT), truy xuất lô ↔ mẻ ↔ khách, soạn biên bản sự cố | `propose_qc_result`, `propose_hold_lot` → Trưởng QA | 1 (chỉ đọc), ghi từ đợt 2 |
| LOG | Giao vận | Giao đúng giờ từ 95% | Chuyến sắp trễ, điểm giao thiếu suất, chữ ký nhận còn thiếu | `propose_route`, `propose_delivery_event` → Điều phối giao vận | 2 |
| PKG | Hộp khay & vệ sinh | Thất thoát hộp dưới ngưỡng hợp đồng, lô vệ sinh xong đủ bước | Hộp quá hạn thu hồi, khách vượt ngưỡng, lô vệ sinh dở dang | `propose_container_return`, `propose_cleaning_step` → Tổ trưởng vệ sinh | 3 |
| HR | Nhân sự | Mọi ca đủ người trước giờ vào ca | Người chưa vào ca, gợi ý người thay, đơn nghỉ / đổi ca chờ duyệt, công tháng thiếu chấm | `propose_leave`, `propose_shift_swap`, `propose_attendance_fix` → Trưởng bộ phận | 2 |
| ACC | Kế toán | Số liệu khớp AMIS, khóa kỳ đúng hạn | Chứng từ lỗi, bảng kê chưa xác nhận, lệch đối chiếu AMIS, nhắc khóa kỳ | `propose_container_loss_charge` → Kế toán trưởng. Không thay MISA (D-23) | 3 |
| CSKH | Chăm sóc khách (Zalo) | Khách tự xem và báo số suất, phản ánh được ghi ngay | Khách xem đơn và thực đơn **của chính mình**, chốt suất, báo thiếu, gửi phản ánh | `propose_portion_confirm`, `propose_shortage_report`, `propose_complaint` → Kinh doanh / điều phối | 4 (chờ D-06) |
| **KNOW** | **Thủ thư tri thức** (mới) | Trả lời được câu hỏi "làm thế nào"; mỗi sự cố đã đóng có một bài học | Tìm SOP và tiền lệ cho các agent khác, viết bài học vào thư mục 96–98, phát hiện SOP cũ mâu thuẫn với cấu hình `BusinessRules` | `propose_note_update` (sửa SOP) → Người phụ trách thư mục | 1 |

**Lý do thêm KNOW:** kho tri thức cần một người chịu trách nhiệm ghi và dọn. Nếu để 12 agent cùng ghi thì kho sẽ nhiễu rất nhanh. Nếu chỉ một agent ghi thì giữ được một giọng văn và một cấu trúc.

**Agent để sau, khi có bảng dữ liệu:** POS (điểm phát, bán lẻ) và MNT (thiết bị, bảo trì). Cả hai đang chờ GAP-POS-01 và GAP-MNT-01.


## 5. Luồng vận hành

Có ba luồng: đọc, ghi 2 pha và sự kiện. Cả ba đều đi qua `ToolGateway`, nên mọi hành động của agent đều có một dòng `IntegrationLog` và một `CorrelationId` xuyên suốt.


### 5.1 Đọc – trả lời ngay

1. Người dùng hỏi qua bot Trợ lý hoặc ô chat trên web, ví dụ "Ngày mai thiếu nguyên liệu gì?". Vivi giao câu hỏi cho PROC.
2. PROC gọi công cụ `du_bao_thieu_hut` bằng khóa của mình.
3. `ToolGateway` kiểm tra theo thứ tự:
- khóa hợp lệ và ánh xạ được sang `User AGENT_PROC`;
- `BotStatus` đang Active;
- còn trong giới hạn tần suất;
- có quyền Xem trên màn tương ứng.

3. Đạt hết thì gọi `MaterialRequirementService`.
4. Kết quả trả về ở dạng JSON gọn: có giới hạn số dòng, không có số điện thoại, mã số thuế hay email. PROC trả lời kèm mã chứng từ và đường dẫn tới màn hình.


### 5.2 Ghi – 2 pha, luôn có người xác nhận

1. PROC gọi `propose_purchase_order`. `ToolGateway` chạy thử bằng đúng hàm kiểm tra của `PurchaseOrderService`. Bước này **không ghi** dữ liệu nghiệp vụ.
2. `ToolGateway` tạo `ApprovalRequest` với các giá trị:
- `BusinessType = AGENT_WRITE`;
- `PayloadJson` = lệnh, bản xem trước và agent đề xuất;
- `DueAt` = 15 phút sau.

3. Người có quyền nhận nút xác nhận trên bot Điều hành hoặc trên màn duyệt tổng. Đây là loại duyệt mới, quyền xác nhận cấp qua mục menu cấp 2 như mọi loại duyệt khác.
4. Người dùng bấm duyệt thì `AgentWriteApprovalHandler.ApproveAsync` tra `AgentCommandRegistry` và gọi method Service thật. Người thao tác ghi nhận là **người duyệt**, kèm `AgentCode`. Gọi lại nhiều lần vẫn chỉ ghi một lần (`IdempotencyKey`).
5. Nếu PO vượt ngưỡng 50 triệu, method Service tự tạo thêm bước duyệt Giám đốc như hiện nay. Agent không đi vòng qua được quy tắc nào.


### 5.3 Sự kiện – agent chủ động

1. Job của lõi vẫn chạy đúng giờ như hiện nay: khóa đơn 20:00, MRP, đề nghị mua, nhắc khóa kỳ…
2. `AgentEventFeed` gom sự kiện từ các nguồn đã có, không cần bảng mới:
- cảnh báo (`CommonService.Alerts`);
- duyệt đang chờ hoặc quá hạn;
- lần chạy job trong `AnalyticsRefreshLog`;
- phản ánh mới.

3. Hermes dùng cronjob có sẵn, 5 phút gọi `lay_su_kien(since)` một lần. Vivi phân mỗi sự kiện cho đúng agent.
4. Sự cố gấp (nhiệt độ ATTP, xe trễ lớn) thì lõi đẩy thẳng sang API server nội bộ của Hermes, không chờ lần quét tiếp theo.
5. Agent gửi tin qua `OutboundMessage`, để bot Vận hành / Điều hành chuyển tới đúng người. Tin chỉ gửi một lần nhờ `IdempotencyKey`.


## 6. Tính năng phát sinh thêm

Có 23 tính năng mới chia 5 nhóm. Người dùng sẽ thấy 4 màn mới:

- Trung tâm agent;
- Kho tri thức;
- loại "Lệnh của agent" trong màn duyệt tổng;
- ô chat Vivi.

Bên cạnh đó có thêm bot Trợ lý trên Telegram, sau này thêm Zalo cho khách. Cột "Đợt" khớp với lộ trình ở mục 9.

| # | Nhóm | Tính năng | Chỗ làm | Đợt |
|---|---|---|---|---|
| 1 | Cổng công cụ | Điểm MCP `/mcp` cho Hermes | ToolGateway | 1 |
| 2 | Cổng công cụ | REST `/tools/*` cho Mission Control và kiểm thử | ToolGateway | 1 |
| 3 | Cổng công cụ | Danh bạ công cụ có mô tả tiếng Việt, khoảng 40 công cụ đọc | ToolGateway + Services | 1 |
| 4 | Cổng công cụ | Tài khoản và khóa riêng cho từng agent (`AGENT_*`) | User / UserGroup cũ + ToolGateway | 1 |
| 5 | Cổng công cụ | Ghi 2 pha: 22 công cụ `propose_*` và `AgentWriteApprovalHandler` | Services + ToolGateway | 2 |
| 6 | Cổng công cụ | Nhật ký mọi lệnh, giới hạn tần suất, công tắc tắt từng agent hoặc tắt hết, chế độ chỉ đọc toàn cục | ToolGateway | 1 |
| 7 | Cổng công cụ | Nguồn sự kiện cho agent (`lay_su_kien`) | Services | 1 |
| 8 | Agent & Hermes | 13 cấu hình agent (SOUL, kỹ năng, bộ câu nghiệm thu) | `agents/` | 1–4 |
| 9 | Agent & Hermes | Bot Trợ lý trên Telegram, chỉ cho tài khoản đã liên kết `UserExternalIdentity` | Hermes | 1 |
| 10 | Agent & Hermes | Bản tin tự động: 07:00 (Ban GĐ), 17:00 (cuối ngày), sau khóa đơn và sau MRP | Hermes cron + VIVI | 1 |
| 11 | Agent & Hermes | Hỏi đáp tự nhiên trên toàn bộ số liệu, theo đúng quyền người hỏi | Hermes + ToolGateway | 1 |
| 12 | Agent & Hermes | Cảnh báo chủ động kèm nhận xét và phương án (ATTP, xe trễ, thiếu nguyên liệu, duyệt quá hạn) | Hermes + `OutboundMessage` | 2 |
| 13 | Trung tâm agent (web) | Danh sách agent, trạng thái, bật / tắt, công cụ được dùng | `AgentController` + `agent.js` | 1 |
| 14 | Trung tâm agent (web) | Dòng hoạt động: agent nào gọi gì, lúc nào, kết quả ra sao | `AgentController.GetActivity` | 1 |
| 15 | Trung tâm agent (web) | Hàng chờ xác nhận lệnh agent trong màn duyệt tổng | Duyệt tổng hiện có + loại mới | 2 |
| 16 | Trung tâm agent (web) | Chi phí AI theo agent, ngày, tháng; cảnh báo khi dùng tới 80% ngân sách | `AgentController.GetCost` | 2 |
| 17 | Trung tâm agent (web) | Ô chat với Vivi trên web, dùng chung lịch sử với bot Trợ lý | `AgentController.Chat` → API server của Hermes | 2 |
| 18 | Kho tri thức | Cây thư mục, đọc ghi chú (markdown, `[[liên kết]]`), tìm toàn văn | `KnowledgeController` + `knowledge.js` | 1 |
| 19 | Kho tri thức | Soạn và sửa ghi chú, SOP có duyệt, lịch sử sửa | `KnowledgeService` + `AuditChange` | 2 |
| 20 | Kho tri thức | Tự xuất từ lõi: bản tin, KPI, sự cố, thực đơn tuần, hồ sơ khách tóm tắt | `KnowledgeService.Export` + job | 1 |
| 21 | Kho tri thức | Tab "Tri thức" trên trang chi tiết (khách, NCC, món, lô…) hiện các ghi chú liên quan | `KnowledgeController.GetByEntity` | 3 |
| 22 | Kho tri thức | Đồng bộ sang Obsidian trên máy cá nhân (tùy chọn, chỉ các thư mục được phép) | Volume + công cụ đồng bộ | 3 |
| 23 | Kênh khách | Zalo OA cho khách qua agent CSKH | `Messaging` + CSKH | 4 |

**Tổng:** 7 tính năng nền, 5 tính năng agent, 5 trên web, 5 kho tri thức, 1 kênh khách. Không tính năng nào cần bảng hay cột mới. Riêng Zalo vẫn chờ D-06 (định danh khách). Menu mới gồm nhóm "Trợ lý AI" (Trung tâm agent, Nhật ký agent), nhóm "Tri thức" và một mục duyệt "Lệnh của agent". Tất cả thêm qua seed `ObjectMenu` theo D-79.


## 7. Kho tri thức chung xây ngay trong ứng dụng

Làm được, và em khuyến nghị làm theo phương án B bên dưới. Kho là một thư mục markdown đúng chuẩn Obsidian, nằm trên volume `knowledge` của server. Ba bên dùng chung thư mục này:

- lõi: ghi ghi chú tự sinh, phục vụ màn Kho tri thức;
- Hermes: đọc qua `ToolGateway`;
- Obsidian trên máy cá nhân: bản đồng bộ, tùy chọn.

Nhờ vậy không cần bảng mới, và kho không bị khóa vào ứng dụng.

| Phương án | Cách làm | Ưu | Nhược |
|---|---|---|---|
| A. Obsidian thuần | Lõi xuất file ra vault, mọi người dùng app Obsidian riêng | Ít code nhất | Nhân viên phải cài thêm app và đồng bộ. Không phân quyền theo vai trò được. Không duyệt được SOP |
| **B. Kho trong app, lưu bằng file markdown (đề xuất)** | Màn "Kho tri thức" đọc, ghi file trên volume; quyền theo mục menu; lịch sử trong `AuditChange` | Dùng ngay trong DELI-CARE, phân quyền bằng ma trận cũ. Hermes và Obsidian dùng chung kho. **Không bảng mới** | Phải tự làm tìm kiếm và hiển thị markdown. Không có plugin và graph view của Obsidian |
| C. Kho trong database | Ghi chú lưu thành bảng (`KnowledgeNote`, `KnowledgeLink`…) | Tìm và phân quyền bằng SQL | **Phải thêm bảng**, trái luật 3.1. Mất tương thích Obsidian. Hermes phải đọc qua API |


### 7.1 Cấu trúc kho

Kho theo khung 10–99 đã có trong `ARCHITECTURE.md`:

> 10 Ban GĐ · 20 Kinh doanh & Hợp đồng · 30 Thực đơn & Công thức · 40 Kho & Truy xuất · 45 ATTP · 50 Sản xuất · 55 Giao vận · 60 Kế toán · 70 Nhân sự · 80 Đào tạo · 95 Nguồn BOT · 96 Nhật ký · 97 Tương tác · 98 Công việc · 99 Mẫu · BOD Bảo mật

Trong kho có hai loại ghi chú:

- **Ghi chú tự sinh**: bản tin, KPI, sự cố, thực đơn tuần. Lõi xuất ra một chiều, frontmatter gắn `source: delicare-core`. Loại này chỉ đọc, sửa cũng bị ghi đè ở lần xuất sau.
- **Ghi chú do người viết**: SOP, đào tạo, bài học, quyết định. Loại này sửa được trong app. Riêng SOP phải duyệt trước khi có hiệu lực.

Dữ liệu giao dịch không bao giờ được sửa trong kho. Nguyên tắc này giữ nguyên như thiết kế cũ.


### 7.2 Phân quyền, theo đúng cơ chế cũ

- Mỗi thư mục lớn là một mục `ObjectMenu` cấp 2 trong nhóm "Tri thức". `AllowAccess` cho đọc, `AllowEdit` cho soạn. Duyệt SOP dùng mục "Duyệt SOP" trong nhóm Phê duyệt. Quyền không nằm trong appsettings.
- Thư mục **BOD Bảo mật** chỉ Ban GĐ thấy. `ToolGateway` không bao giờ trả nội dung thư mục này cho agent, dù agent có quyền gì.
- Agent đọc được mọi thư mục nghiệp vụ mà tài khoản `AGENT_*` của nó được cấp. Agent chỉ ghi trực tiếp vào 96, 97, 98. Muốn sửa SOP thì phải qua `propose_note_update`.


### 7.3 Kỹ thuật

- **Đọc và ghi:** chỉ đi qua `KnowledgeService`. Service khóa theo từng file và kiểm mã băm trước khi ghi, nhờ vậy hai người sửa cùng lúc không đè mất công của nhau. Mỗi lần lưu ghi một dòng `AuditChange` (đường dẫn, băm cũ / mới, người sửa).
- **Hiển thị:** màn `knowledge.js` dựng markdown thành HTML. `[[Liên kết]]` bấm được. Có mục "Liên kết ngược". Mã chứng từ như `PO-261125-0003` tự thành đường dẫn tới trang chi tiết.
- **Tìm kiếm:** chỉ mục toàn văn giữ trong bộ nhớ của `KnowledgeService`, tìm được cả khi gõ không dấu. Chỉ mục dựng lại khi khởi động và khi có file đổi. Với vài nghìn ghi chú thì cách này đủ nhanh và không cần thư viện mới.
- **Backup:** volume `knowledge` được sao lưu hằng đêm cùng `DocumentStorage`.
- **Obsidian trên máy cá nhân:** khuyến nghị chỉ đồng bộ một chiều xuống máy, và sửa trong app để giữ được quyền và duyệt. Muốn sửa hai chiều thì chỉ mở cho thư mục cá nhân hoặc thư mục 80 Đào tạo.

**Giới hạn cần biết:** app không tái tạo hết Obsidian. Các phần không có là graph view, plugin và canvas. Ai cần những phần đó vẫn mở bản đồng bộ bằng Obsidian.


### 7.4 Kho chứa gì

Kho chứa **tri thức**, không chứa số liệu giao dịch. Tri thức ở đây là cách làm, quy định, lý do, bài học, bối cảnh, cùng các bản chụp có ngày của báo cáo. Mỗi thư mục có một vai trò làm chủ, và chỉ chủ mới được duyệt nội dung trong thư mục đó.

| Thư mục | Nội dung | Loại (tự sinh / người viết / agent) | Chủ thư mục |
|---|---|---|---|
| 10 Ban GĐ | Bản tin ngày, báo cáo tuần / tháng, KPI so mục tiêu, quyết định điều hành | Tự sinh + người viết | CEO / COO |
| 20 Kinh doanh & Hợp đồng | Hồ sơ khách tóm tắt (không số tài khoản), điều khoản đặc thù, phản ánh đã xử lý, ghi chú chăm sóc khách | Tự sinh + người viết | TP Kinh doanh |
| 30 Thực đơn & Công thức | Thực đơn tuần đã chốt, định mức đã khóa, quy tắc hao hụt, món thay thế, kinh nghiệm nấu | Tự sinh + người viết | Trưởng bếp |
| 40 Kho & Truy xuất | Quy trình nhập / xuất / kiểm kê, đánh giá NCC, phân tích lệch kiểm kê | Tự sinh + người viết | Thủ kho / Mua hàng |
| 45 ATTP | SOP ATTP, hồ sơ sự cố đã đóng, bài học, văn bản pháp quy (có nguồn) | Tự sinh + người viết | Trưởng QA |
| 50 Sản xuất | Quy trình dây chuyền, năng lực bàn nấu, kinh nghiệm xử lý mẻ trễ | Người viết | Trưởng bếp |
| 55 Giao vận | Đặc điểm điểm giao (lối vào, giờ nhận), tuyến, sự cố giao đã đóng | Tự sinh + người viết | Điều phối giao vận |
| 60 Kế toán | Quy trình đối soát, khóa kỳ, quy tắc khớp mã AMIS | Người viết | Kế toán trưởng |
| 70 Nhân sự | Nội quy, quy định ca, nghỉ phép (không lương, không hồ sơ cá nhân) | Người viết | TP Nhân sự |
| 80 Đào tạo | Tài liệu hướng dẫn theo vai trò, câu hỏi thường gặp | Người viết | TP Vận hành |
| 95 Nguồn BOT | SOUL, kỹ năng, mẫu trả lời của agent (bản đọc, gốc nằm ở `agents/`) | Tự sinh | Admin |
| 96 Nhật ký | Nhật ký việc của từng agent theo ngày | Agent | KNOW |
| 97 Tương tác | Quyết định, cam kết chốt trong hội thoại với agent | Agent | KNOW |
| 98 Công việc | Việc agent đang theo dõi, bài học nháp chờ duyệt | Agent | KNOW |
| 99 Mẫu | Mẫu ghi chú, SOP, sự cố, báo cáo | Người viết | Admin |
| BOD Bảo mật | Chiến lược, giá vốn đàm phán, nhân sự cấp cao | Người viết | CEO – agent không bao giờ đọc |

**Không đưa vào kho:**

- số liệu giao dịch thô (đơn, tồn, chấm công từng dòng);
- dữ liệu cá nhân nhạy cảm (số điện thoại, CCCD, lương, tài khoản ngân hàng);
- bí mật, khóa, mật khẩu;
- bản sao của thứ đã có trong lõi.


### 7.5 Luật cứng của kho

`KnowledgeService` và `ToolGateway` chặn bằng code mọi lệnh ghi sai luật, không dựa vào việc agent tự giữ kỷ luật.

1. **Lõi thắng kho.** Số liệu và trạng thái hiện tại luôn lấy từ lõi qua công cụ. Ghi chú tự sinh chỉ là bản chụp có ngày. Kho và lõi lệch nhau thì theo lõi và báo KNOW.
2. **Không chép ngưỡng.** SOP dẫn chiếu tới mã cấu hình, ví dụ `BusinessRules:PurchaseOrderDirectorApprovalAmount`, chứ không viết cứng "50 triệu". Khi hiển thị, ứng dụng tự thay bằng giá trị đang cấu hình, nên SOP không bao giờ cũ so với cấu hình.
3. **Frontmatter bắt buộc.** Thiếu trường là không lưu được:
- `id`, `type` (sop / su-co / bai-hoc / bao-cao / quyet-dinh / ho-so / nhat-ky / huong-dan);
- `source` (`delicare-core` / `human` / `agent:<MÃ>`), `owner` (vai trò), `status` (nháp / chờ duyệt / hiệu lực / lưu trữ);
- `valid_from`, `review_by`, `confidentiality` (công khai / nội bộ / hạn chế / BOD);
- `refs`: mã chứng từ hoặc mã danh mục liên quan.

4. **Mỗi quy trình chỉ có một SOP hiệu lực.** Sửa bằng cách tạo phiên bản mới và duyệt. Bản cũ chuyển sang lưu trữ, không xóa.
5. **Chủ thư mục duyệt.** Ghi chú để tham khảo thì người có quyền Sửa được lưu thẳng. SOP và quyết định phải qua mục duyệt "Duyệt SOP".
6. **Agent chỉ ghi trực tiếp vào 96, 97, 98** và phải dùng mẫu cố định. Muốn đưa nội dung vào thư mục nghiệp vụ thì phải qua `propose_note_update` và chủ thư mục duyệt.
7. **Ghi chú tự sinh chỉ đọc.** Nút Sửa bị ẩn. Muốn bổ sung bình luận thì viết ghi chú riêng và liên kết `[[...]]` tới ghi chú tự sinh.
8. **Bảo mật.** Không dữ liệu cá nhân nhạy cảm, không bí mật; khi lưu, máy chủ quét mẫu số điện thoại, CCCD, số tài khoản và từ chối nếu gặp. Agent không bao giờ đọc thư mục BOD hoặc ghi chú gắn `confidentiality: BOD`.
9. **Hạn rà soát:**
- SOP 6 tháng một lần;
- hướng dẫn đào tạo 12 tháng;
- hồ sơ điểm giao mỗi khi đổi hợp đồng.

9. Quá hạn thì KNOW nhắc chủ thư mục, và ghi chú bị gắn nhãn "cần rà soát" khi agent trích dẫn.
10. **Thời gian lưu giữ:**
- nhật ký 96: 90 ngày, sau đó KNOW gộp thành bản tóm tắt tháng;
- bản tin ngày: 13 tháng;
- báo cáo tháng, sự cố, bài học, quyết định: giữ lâu dài.

11. **Trích dẫn bắt buộc.** Câu trả lời của agent dùng tới kho phải nêu ghi chú nguồn (tiêu đề và ngày hiệu lực). Không có nguồn thì nói rõ là kho chưa có.


### 7.6 Nguồn dữ liệu được vào kho

Chỉ có bốn nguồn được phép. Mọi ghi chú phải ghi rõ nguồn ở trường `source`.

| Nguồn | Vào đâu | Cách vào | Kiểm soát |
|---|---|---|---|
| **Lõi AppDelicare** (`delicare-core`) | 10, 20, 30, 40, 45, 55 | Exporter trong `KnowledgeService`, chạy theo job và theo sự kiện lớn | Tự động, chỉ đọc. Chỉ xuất dữ liệu đã chốt: đơn đã khóa, sự cố đã đóng, thực đơn đã duyệt |
| **Người trong công ty** (`human`) | Mọi thư mục theo quyền | Soạn trên màn Kho tri thức, hoặc nhập file Word / PDF được chuyển sang markdown | SOP và quyết định phải duyệt; ghi chú tham khảo lưu thẳng |
| **Agent** (`agent:<MÃ>`) | 96, 97, 98; thư mục khác chỉ qua đề xuất | Công cụ `ghi_nhat_ky`, `propose_note_update` | Mẫu cố định; nội dung nghiệp vụ phải có người duyệt |
| **Bên ngoài** (văn bản pháp quy, tiêu chuẩn, tài liệu NCC) | 45, 40, 60, 70 | Chỉ người nhập, bắt buộc ghi số hiệu văn bản và ngày hiệu lực | Chủ thư mục duyệt. Agent không tự tải từ web (công cụ web của Hermes đã tắt) |


### 7.7 Khi nào ghi

Ghi khi có một việc đã chốt hoặc một điều đáng nhớ. Không ghi mỗi lần tra cứu.

| Sự kiện | Ai ghi | Ghi gì | Vào đâu |
|---|---|---|---|
| 07:00 và 17:00 hằng ngày | Exporter + VIVI | Bản tin sáng, báo cáo cuối ngày | 10 |
| Thứ Hai, ngày 1 hằng tháng | Exporter | Báo cáo tuần / tháng so kỳ trước | 10 |
| Thực đơn tuần được duyệt; công thức bị khóa | Exporter | Thực đơn, định mức phiên bản khóa | 30 |
| Sự cố ATTP hoặc giao hàng đóng | Exporter, sau đó KNOW | Hồ sơ sự cố (tự sinh), bài học nháp → 98 → chủ thư mục duyệt → 45 / 55 | 45, 55, 98 |
| Phản ánh khách đóng | Exporter | Tóm tắt phản ánh và cách xử lý, gắn vào hồ sơ khách | 20 |
| Kiểm kê lệch trên 2% được duyệt | WH đề xuất | Ghi chú phân tích nguyên nhân | 40 (qua duyệt) |
| Agent xong một việc có kết quả (bản tin, đề xuất, cảnh báo) | Agent đó | 1 dòng nhật ký: việc, kết quả, mã đề xuất | 96 |
| Người dùng chốt quyết định hoặc cam kết trong hội thoại ("từ tuần sau đặt NCC B") | VIVI | Quyết định, người chốt, thời điểm | 97 |
| Agent nhận theo dõi một việc có hạn | Agent đó | Việc, hạn, người phụ trách; đóng khi xong | 98 |
| Phát hiện SOP sai hoặc thiếu | Người hoặc KNOW | Bản SOP mới, qua đề xuất và duyệt | Thư mục nghiệp vụ |

**Không ghi:**

- câu hỏi tra cứu thông thường và câu trả lời có thể hỏi lại lõi;
- từng lệnh gọi công cụ (đã có `IntegrationLog`);
- số liệu giao dịch thô;
- nội dung cá nhân trong hội thoại.


### 7.8 Khi nào tra cứu

Agent quyết định tra lõi hay tra kho theo **loại câu hỏi**. Quy tắc này ghi trong SOUL của mọi agent và được kiểm bằng bộ câu nghiệm thu.

| Loại câu hỏi / việc | Tra ở đâu | Ví dụ |
|---|---|---|
| Số liệu, trạng thái hiện tại | **Chỉ lõi**, không tra kho | "Tồn thịt heo còn bao nhiêu?", "Chuyến nào đang trễ?" |
| Cách làm, quy định, vì sao | **Kho**, chỉ SOP đang hiệu lực | "Nhận hàng lạnh kiểm những gì?" |
| Xử lý sự cố đang xảy ra | Lõi cho số liệu + kho cho SOP và tiền lệ (45, 55, 96) | "Mẻ ME-… chỉ 68°C, làm gì?" |
| Trước khi tạo đề xuất ghi | Đọc SOP liên quan và dẫn vào phần lý do của đề xuất | PROC dẫn quy trình chọn NCC khi đề xuất PO |
| Bản tin, so kỳ | Số từ lõi; kho chỉ cho bối cảnh (quyết định 97, việc đang theo dõi 98) | VIVI nhắc lại cam kết tuần trước |
| Lịch sử, "trước đây đã xử lý thế nào" | Kho, kể cả ghi chú đã lưu trữ | "Lần trước khách A phản ánh gì?" |

**Thứ tự ưu tiên khi nhiều ghi chú cùng khớp:**

1. Bỏ bản nháp và bản lưu trữ, trừ khi câu hỏi hỏi về lịch sử.
2. Ưu tiên SOP đã duyệt, rồi đến ghi chú tự sinh, cuối cùng mới đến ghi chú của agent.
3. Trong cùng một loại, lấy bản có `valid_from` mới nhất.
4. Mỗi lượt đọc tối đa 5 ghi chú, và công cụ trả về đoạn liên quan chứ không trả cả file, để tiết kiệm token.

**Phạm vi đọc của từng agent.** `tim_tri_thuc` tự lọc theo bảng dưới, dù agent có hỏi gì. Agent nào cũng đọc được 96–98 của chính mình.

| Agent | Thư mục được đọc |
|---|---|
| VIVI, KNOW | Tất cả, trừ BOD |
| SALES | 20, 30, 55, 80 |
| PLAN | 30, 40, 50, 80 |
| PROC | 30, 40, 60, 80 |
| WH | 40, 45, 80 |
| KITCHEN | 30, 45, 50, 80 |
| QA | 40, 45, 50, 55, 80 |
| LOG | 20, 55, 80 |
| PKG | 20, 45, 55, 80 |
| HR | 70, 80 |
| ACC | 20, 40, 60, 80 |
| CSKH | Chỉ ghi chú gắn `confidentiality: công khai` (thực đơn tuần, quy định giao nhận), và chỉ của chính khách đang hỏi |

**Công cụ thực thi:**

- `tim_tri_thuc(câu hỏi, thư mục?)`: lọc theo quyền, trạng thái và độ mật, trả về đoạn trích kèm `id` và ngày hiệu lực.
- `doc_ghi_chu(id)`: đọc cả ghi chú.
- `ghi_nhat_ky(loại, nội dung)`: chỉ ghi được vào 96 / 97 / 98, theo đúng mẫu.
- `propose_note_update(id, bản mới, lý do)`: đề xuất sửa ghi chú, chờ duyệt.

Mỗi luật ở 7.5–7.8 có một test trong `AppDelicare.Tests/Knowledge`. Ví dụ: agent ghi vào 45 phải bị từ chối; CSKH tìm ghi chú nội bộ phải ra rỗng.


## 8. Triển khai

Giữ nguyên `docker-compose.yml` hiện tại, chỉ thêm container `toolgateway` và volume `knowledge`. Hermes làm việc trong mạng Docker nội bộ, không có chuỗi kết nối DB.

| Container | Thay đổi | Kết nối |
|---|---|---|
| `api` | Thêm `AgentController`, `KnowledgeController` | SQL Server, volume `knowledge` |
| `job` | Thêm job xuất kho tri thức, job hết hạn đề xuất | SQL Server, volume `knowledge` |
| `toolgateway` (mới) | MCP `/mcp`, REST `/tools/*`; chỉ mở trong mạng nội bộ | Services, volume `knowledge` |
| `hermes` | Cấu hình từ `agents/`; MCP trỏ vào `toolgateway`; tắt terminal, file, code, browser, web | `toolgateway`, Telegram |
| `messaging` | Không đổi | `api` |

**Bố trí Hermes:**

- **Đợt 1:** một container, một profile Vivi. Agent phòng ban là kỹ năng cộng trợ lý con. Mỗi agent có mục MCP và khóa riêng, nên `ToolGateway` vẫn biết agent nào gọi.
- **Từ đợt 2:** tách thành 3 container theo nhóm, mỗi container một volume:
- `hermes-vivi`: VIVI, KNOW;
- `hermes-van-hanh`: KITCHEN, QA, LOG, PKG, WH;
- `hermes-van-phong`: SALES, PLAN, PROC, HR, ACC, CSKH.

- Tách để lỗi hay chi phí của một nhóm không kéo theo các nhóm khác.
- **Ranh giới bảo mật thật nằm ở ****`ToolGateway`**** và bước người xác nhận**, không nằm ở Hermes. Nếu mô hình AI làm sai thì tối đa cũng chỉ tạo ra một đề xuất bị từ chối.

**Bảo mật và chi phí:**

- Khóa agent và khóa API mô hình để trong biến môi trường trên server, không commit. `BotConfiguration.SecretReference` chỉ ghi tên biến.
- Nội dung khách gửi (phản ánh, tin Zalo) được đánh dấu là dữ liệu, không phải lệnh. Agent làm theo chỉ dẫn lạ thì cũng chỉ tạo được đề xuất.
- Mô hình: Sonnet cho Vivi và các phân tích, Haiku cho việc theo lịch. Đặt giới hạn chi tiêu cứng ở nhà cung cấp; `AgentService` cảnh báo khi dùng tới 80%.


## 9. Lộ trình và việc cần anh chốt

Làm 4 đợt, nối tiếp R1 khi lõi đã chạy trên server. Phải qua được cửa của đợt trước mới sang đợt sau. Mã task theo ROADMAP hiện có.

1. **Đợt 1 – Chỉ đọc**
- T30 `ToolGateway` và công cụ đọc; T31 danh tính agent; T43 cấu hình VIVI, WH, KITCHEN, QA, KNOW.
- T50 kho tri thức: màn đọc và tìm, xuất tự động.
- Trung tâm agent: danh sách và nhật ký. Bot Trợ lý. Bản tin 07:00 / 17:00.
- Cửa qua: bộ câu nghiệm thu của 5 agent đúng số; 2 tuần bản tin không sai số; chi phí trong ngân sách.

2. **Đợt 2 – Ghi 2 pha**
- T33 `propose_*` và `AgentWriteApprovalHandler`; loại duyệt "Lệnh của agent".
- Bật ghi cho WH, KITCHEN, QA. Thêm PROC, SALES, LOG, HR.
- Cảnh báo chủ động; sửa ghi chú và SOP có duyệt; chi phí theo agent; chat trên web.
- Tách Hermes thành 3 container.
- Cửa qua: test chứng minh đề xuất không ghi gì trước khi duyệt; tỉ lệ đề xuất được duyệt mà không sửa đạt mức anh chốt.

3. **Đợt 3 – Đủ phòng ban**
- PLAN, PKG, ACC.
- Tab "Tri thức" trên trang chi tiết; đồng bộ Obsidian trên máy cá nhân.

4. **Đợt 4 – Khách hàng**
- T41 Zalo OA và agent CSKH, sau khi chốt D-06.


**Cần anh chốt trước Đợt 1:**

- **Thư viện MCP cho .NET** (thư viện mới, theo `CLAUDE.md` phải hỏi anh). Em đề xuất dùng SDK MCP chính thức cho C#, nếu kiểm tra thấy đã ổn định. Phương án khác là tự viết lớp JSON-RPC nhỏ: không phụ thuộc ai nhưng tốn công tự bảo trì.
- **Kho tri thức theo phương án B** (file markdown trên volume, màn trong app) hay A hoặc C (mục 7).
- **Thêm agent KNOW**, nâng tổng số lên 13, và gộp thực đơn vào PLAN.
- **Ngân sách AI theo tháng** và mô hình mặc định. Em đề xuất Sonnet cho Vivi, Haiku cho việc theo lịch.
- **Người xác nhận** từng loại đề xuất, và ai được dùng bot Trợ lý ở Đợt 1. Em đề xuất chỉ Ban GĐ và các trưởng bộ phận.
- **Thời điểm bắt đầu R2:** sau khi lõi lên server thật và chạy thử một khách, hay làm song song ngay từ bây giờ.

## 10. Màn hình theo mockup (`docs/frontend-handoff/assets/ai-agent/`)

Các file `*.dc.html` là mockup dạng canvas: đọc phần markup (style inline, nhãn tiếng Việt) để lấy bố cục, nhãn, cột, nút; phần `class Component` chỉ là dữ liệu giả và trạng thái demo. **Không chép nguyên** vào app – dựng lại trên engine `app-delicare-client/src/assets/delicare/js` theo chuẩn §4.5 (UI.kpis, UI.chart, trang chi tiết toàn màn D-80, tiếng Việt D-88). Bỏ phần chỉ để trình diễn (dữ liệu giả, nút chuyển vai trò…).

| File mockup | Màn | screen-id đề xuất (`ObjectMenu.MetaLink`) | Task |
|---|---|---|---|
| `Main.dc.html` | Trung tâm agent: thẻ 13 agent (icon, trạng thái, việc hôm nay, chi phí), bật / tắt, nút "Trò chuyện"; ngăn chat bên phải cao bằng màn hình, nổi đè lên trang (không làm trang hẹp lại), tab chọn agent | `ai-agents` | AI-6, AI-14 |
| `AgentDetail.dc.html` | Trang chi tiết agent (D-80): mục đích, công cụ được dùng, quyền thư mục tri thức, lịch chạy, hoạt động gần đây, chi phí | `ai-agents` (trang chi tiết `#/ai-agents/<MÃ>`) | AI-6 |
| `AgentLog.dc.html` | Nhật ký agent: mọi lệnh gọi công cụ (agent, công cụ, người hỏi, kết quả, thời gian, token), lọc theo agent / kết quả / ngày | `ai-agent-log` | AI-6 |
| `Schedule.dc.html` | Lịch chạy: việc theo giờ của từng agent (bản tin 07:00, 17:00, sau khóa đơn, sau MRP…), lần chạy gần nhất, bật / tắt | `ai-schedule` | AI-6 (xem), AI-11 (bật / tắt) |
| `Tasks.dc.html` | Việc agent đang theo dõi (thư mục 98): việc, hạn, người phụ trách, agent, trạng thái | `ai-tasks` | AI-12 |
| `Flow.dc.html` | Luồng liên phòng: chuỗi sự kiện → agent → đề xuất → duyệt theo `CorrelationId` (chốt đơn → MRP → mua hàng → kho…) | `ai-flow` | AI-11 |
| `Knowledge.dc.html` | Kho tri thức: cây thư mục 10–99, đọc ghi chú markdown, `[[liên kết]]`, liên kết ngược, tìm không dấu, soạn / duyệt SOP | `knowledge` (+ mục quyền theo thư mục) | AI-5, AI-12 |
| `Approval.dc.html` | Loại duyệt "Lệnh của agent" trong màn Phê duyệt tổng: bản xem trước lệnh, agent đề xuất, lý do + SOP dẫn chiếu, hết hạn 15′ | `wf-inbox?type=ai-agent-cmd` (theo mẫu HR-ACC-1.6) | AI-9 |
| `Assistant.dc.html` | Bot Trợ lý (Telegram, Hermes) 390px: hỏi đáp, bản tin, nút xác nhận đề xuất | – (Hermes) | AI-7, AI-8 |
| `BotOps.dc.html` | Bot Vận hành 390px nhận tin chủ động của agent qua `OutboundMessage` | – (`messaging`) | AI-11 |
| `Zalo.dc.html` | Zalo OA cho khách (CSKH) | – | AI-19 |
| `Icons.dc.html` | Bộ icon 13 agent (màu chữ / nền, path SVG 24×24) → xuất thành `src/assets/delicare/img/agents/<MÃ>.svg`, dùng chung web + bot | – | AI-6 |
