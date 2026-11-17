# ARCHITECTURE – Kiến trúc mục tiêu 4 lớp

> **⚠ D-30 (2026-11-15): khung chuẩn của lớp 1 là code cũ.** Mọi chỗ trong file này nói `/api/v1/...` hoặc `AppDelicare.Application` (Command / Handler) **không còn áp dụng**: nghiệp vụ nằm trong `AppDelicare.Services` (Service theo entity + service nghiệp vụ cùng mẫu), API là controller `api/[controller]/<Action>`. Jobs, Integration, ToolGateway (R2) gọi các Service này thay cho Application. Chi tiết: CLAUDE.md mục 4.

```
             Quản lý (web)              Nội bộ               Khách
          Mission Control (Angular)   Telegram (3 bot)     Zalo OA
                    │                     │                   │
                    │ JWT                 │ webhook           │ webhook
                    ▼                     ▼                   ▼
┌─────────────────────────────┐   ┌──────────────────────────────────────────┐
│  LỚP 1 – LÕI (AppDelicare)  │   │ AppDelicare.Integration                  │
│  WebApi  /api/... (cũ)      │◄──│  Telegram/Zalo adapter → InboundMessage   │
│          /api/v1/... (mới)  │   │  OutboundMessage worker → gửi tin         │
│  Application (nghiệp vụ)    │   │  Obsidian exporter (một chiều → vault)    │
│  Data (EF Core + Dapper)    │   └──────────────────────────────────────────┘
│  Job (container riêng: 20h, 20h30,│                 ▲
│        21h, 5h30, 7h, 17h)  │                 │ sự kiện nghiệp vụ
│  SQL Server – 124 bảng dbo  │                 │
└──────────────▲──────────────┘                 │
               │ gọi Application (in-process)   │
┌──────────────┴──────────────────────────────────────────────────────────┐
│ LỚP 2 – AppDelicare.ToolGateway                                         │
│  MCP server (cho Hermes/agent) + REST /tools/* + POST /api/gateway (cũ) │
│  Danh bạ công cụ · phân quyền theo agent · propose/confirm/commit       │
│  IntegrationLog cho mọi lệnh · giới hạn tần suất · công tắc tắt agent   │
└──────────────▲──────────────────────────────────────────────────────────┘
               │ MCP (token dịch vụ của từng agent)
┌──────────────┴──────────────────────────────────────────────────────────┐
│ LỚP 3 – Hermes: Vivi (điều phối) + 11 agent phòng ban (AGENTS.md)       │
└──────────────┬──────────────────────────────────────────────────────────┘
               │ đọc kiến thức / ghi nhật ký vào 96-98
┌──────────────▼──────────────────────────────────────────────────────────┐
│ LỚP 4 – Obsidian vault (10 Ban GĐ … 99 Mẫu, BOD Bảo mật)                │
└─────────────────────────────────────────────────────────────────────────┘
```

## 1. Lớp 1 – Lõi nghiệp vụ

**Solution sau khi nâng cấp:**
| Project | Tham chiếu | Nội dung |
|---|---|---|
| AppDelicare.Model (thư mục AppDelicare.Model) | – | Entity (map đúng 124 bảng), DTO, Option, Helper |
| AppDelicare.Data | Model | DatabaseContext, Repository, UnitOfWork, Dapper, `ICurrentActor` |
| AppDelicare.Services | Data, Model | CRUD danh mục (sinh mã) |
| **AppDelicare.Application** (mới) | Data, Model | Use case nghiệp vụ theo miền: `Orders`, `Planning`, `Procurement`, `Inventory`, `Production`, `Quality`, `Logistics`, `Packaging`, `HR`, `Finance`, `Approval`, `Kpi`, `Common` (DocumentNumber, Clock, BusinessRules) |
| AppDelicare.WebApi | tất cả | Controller cũ + controller `/api/v1` mới, permission filter, ProblemDetails |
| **AppDelicare.Job** (mới, D-48) | Services | Worker Service riêng (container `job`), hẹn giờ theo giờ VN, gọi Service cũ. Xem mục 1b |
| **AppDelicare.Integration** (mới) | Application | Telegram, Zalo, Obsidian exporter, outbound worker |
| **AppDelicare.ToolGateway** (mới) | Application | Host riêng: MCP + REST cho agent |
| **AppDelicare.Tests** (mới) | Application, Data | xUnit: unit test quy tắc + integration test trên DB dev |
| **tools/AppDelicare.CodeGen** (mới) | – | Console đọc DDL → sinh Entity / Request / SearchRequest / ViewModel / Repository / Service / Controller / SP `uspXxx_GetAll` theo mẫu cũ |

**Nguyên tắc thiết kế:**
- **Người thao tác (`ICurrentActor`):** gồm `ActorType` (User | Agent | System | Integration), `UserId`, `AgentCode`, `CorrelationId`. Agent và job đều có `User` riêng (tài khoản dịch vụ, `IsActive=1`, nhóm `AGENT_*` / `SYSTEM`). Không có actor thì từ chối ghi.
- **Lệnh nghiệp vụ:** validate → kiểm tra máy trạng thái → kiểm tra ngưỡng (nếu vượt thì tạo `ApprovalRequest` và trả 202 `PendingApproval`) → ghi trong transaction → phát `DomainEvent` sau commit.
- **Sự kiện:** dispatcher chạy trong tiến trình (MediatR notifications hoặc tự viết). Thông báo ra ngoài được lưu thành `OutboundMessage` rồi worker gửi đi. Không cần bảng outbox mới.
- **Duyệt:** `ApprovalService` dùng `ApprovalRequest` / `ApprovalStep` / `ApprovalItem` / `ApprovalDecision`. `BusinessType` ví dụ: `ORDER_CHANGE_AFTER_CUTOFF`, `PORTION_DEVIATION`, `STOCK_COUNT_VARIANCE`, `PURCHASE_ORDER`, `PRODUCTION_PLAN`, `MENU_CHANGE`, `AGENT_WRITE` (xác nhận lệnh ghi của agent).
- **Phân quyền:** `[Permission("<ObjectMenu.MetaLink>", PermissionAction.Add)]` + filter đọc `UserRole` (UserGroupId × ObjectMenuId). Cache 5 phút.
- **Job:** không dùng Hangfire (D-05 đã chốt) – xem mục 1b.

## 1b. AppDelicare.Job – job tự động (D-48, anh chốt 2026-11-16; làm sau khi hoàn thiện luồng – D-45)

**Nguyên tắc:** `AppDelicare.Job` là **cửa vào thứ hai** (giống `AppDelicare.WebApi`), chạy **container / tiến trình riêng**, dùng chung `AppDelicare.Services` / `Data` / `Model`. **Job không chứa nghiệp vụ** – chỉ hẹn giờ và gọi method Service cũ (nút bấm tay trên web và job chạy cùng một code). **Không Hangfire, không thêm bảng / database** (D-05).

```
AppDelicare.Model      Option/JobOptions.cs           cấu hình giờ chạy từng job
AppDelicare.Data       Share/SystemActor.cs           ICurrentActor = tài khoản "system" (seed dữ liệu, không thêm bảng)
AppDelicare.Services   (đã có) OrderService.LockByMealDate, MaterialRequirementService.Calculate, …  ← logic
                       Ioc/ServiceRegistration.cs     đăng ký DI dùng chung cho WebApi + Job (tách từ WebApi/Ioc)
AppDelicare.WebApi     Controllers/JobController.cs   (khung cũ) GetAll: job + lần chạy kế tiếp; Run: chạy tay (chỉ admin, gọi thẳng Service)
AppDelicare.Job        Worker Service .NET 8 (MỚI)
  ├─ Program.cs                    Host + Serilog + chuỗi kết nối + DI dùng chung (chạy trong container Linux – D-60)
  ├─ Scheduling/IScheduledJob.cs   Name, RunAsync(now, ct)
  ├─ Scheduling/JobSchedulerService.cs  BackgroundService, mỗi phút kiểm job đến giờ (giờ VN qua IClock), chạy bù job lỡ giờ trong ngày
  ├─ Scheduling/JobLock.cs         sp_getapplock theo tên job → không bao giờ chạy trùng (kể cả nhiều instance)
  ├─ Jobs/LockOrdersJob.cs 20:00 · MrpJob.cs 20:30 · PurchaseProposalJob.cs 21:00 · LotExpiryJob.cs 00:15
  │       ContractExpiryJob.cs 00:30 · ApprovalExpiryJob.cs mỗi giờ · ComplaintSlaJob.cs 15′
  │       (sau, cùng đợt bot) RemindUnconfirmedOrdersJob 19:00, KpiDailySnapshotJob 22:00 – gửi qua OutboundMessage
  ├─ appsettings.json              "Jobs": { "LockOrders": { "Enabled": true, "At": "20:00" }, … }
  └─ Dockerfile                    image riêng; docker-compose thêm service `job` (1 bản), phụ thuộc `db-init`
```

**Quy ước:**
1. Mỗi job = 1 file mỏng; job **chạy lại không sai dữ liệu** (idempotent: đơn đã khóa thì bỏ qua…).
2. Lịch sử chạy: Serilog + ghi `AnalyticsRefreshLog` (bảng có sẵn: JobCode, RunId, Started / Completed, RunStatus, RowsProcessed).
3. Mọi ghi dữ liệu qua Service → AuditChange ghi người thực hiện = `system`.
4. Test: đồng hồ giả (`IClock`) để chạy job ở mọi mốc giờ; test khóa chống trùng.
5. Triển khai: **Linux + Docker (D-60)** = container `job` riêng (đúng 1 bản, `restart: unless-stopped`, health check bằng file nhịp; API chạy bao nhiêu bản tùy ý). Không dùng Windows Service / Task Scheduler. Deploy API không làm dừng job và ngược lại.
6. **Không** chạy job bên trong WebApi (web app có thể bị dừng / khởi động lại theo truy cập hoặc khi deploy → job không chạy đúng giờ, và scale API lên nhiều bản sẽ chạy trùng).

**Đã cài (T23, 2026-11-17 – PROPOSED D-59):** khác sơ đồ trên ở chỗ job nằm trong `AppDelicare.Services/Jobs/` (`IScheduledJob`, `JobSchedule`, `JobRunService` = khóa + log + actor, `DailyJobs.cs` = 5 job) để `JobController` của WebApi chạy tay cùng code; `AppDelicare.Job` chỉ có `Program.cs` (host, `--list`, `--run <mã>`) + `JobSchedulerService` (mỗi 30 giây). DI dùng chung: `AppDelicare.Services/Ioc/ServiceRegistration.AddDelicareCore` + `DelicareServicesModule` (Autofac); AutoMapper profile chuyển sang `AppDelicare.Services/Mapping/` (CodeGen ghi vào đó). Tài khoản job: user `system` (Id cố định, seed `SystemUser_seed.sql`). Lịch `Jobs:<mã>` ở `appsettings.json` của WebApi (Job đọc qua link `appsettings.shared.json`). Windows: Task Scheduler (Windows Service chờ duyệt gói – D-59 g). Deploy: `deploy/README.md` mục "Job tự động".

## 2. Lớp 2 – Tool Gateway
- **Danh bạ công cụ** (`tools.json` hoặc attribute): `name`, `description` (tiếng Việt, rõ khi nào dùng), `inputSchema`, `mode` = read | write, `domain`, `allowedAgents`, `allowedRoles`, `risk` = low | medium | high, `confirm` = none | requester | role:<code>.
- **Đọc:** gọi Application query rồi trả JSON gọn. Có giới hạn số dòng và không trả dữ liệu cá nhân (SĐT, MST, email) trừ khi vai trò cho phép.
- **Ghi – 2 pha:**
  1. `propose_<action>`: validate và chạy thử (dry-run), **không ghi dữ liệu nghiệp vụ**. Tạo `ApprovalRequest` (BusinessType=`AGENT_WRITE`, `PayloadJson` = lệnh, `CorrelationId`) + `ApprovalItem`. Trả `{confirmationId, summary, preview, expiresAt}`.
  2. Người có quyền xác nhận qua nút Telegram, Mission Control hoặc tool `confirm` của người dùng thật. Ghi `ApprovalDecision`.
  3. `commit` chạy lệnh thật với `IdempotencyKey` = confirmationId. Gọi lại nhiều lần vẫn chỉ ghi một lần.
  - Hết hạn 15 phút (cấu hình). Hủy được.
- **Log:** mỗi lệnh ghi 1 dòng `IntegrationLog` (`OperationCode` = tên tool, `CorrelationId`, `IdempotencyKey`, `PayloadHash`, `LogStatus`, `HttpStatus`, `ErrorCode`). `AuditChange` do DbContext tự ghi. Nối với nhau qua `CorrelationId` trong `IntegrationLog` và `PayloadJson`.
- **An toàn:** giới hạn tần suất theo agent; công tắc tắt (`BotConfiguration.BotStatus` = DISABLED thì từ chối mọi lệnh); chế độ chỉ đọc toàn cục.
- **Tương thích cũ:** `POST /api/gateway` nhận `{fn, args, telegram_id}`, **luôn trả HTTP 200** `{ok, ...}`, ánh xạ 34 hàm (LEGACY_GATEWAY.md). Người gọi được xác định qua `UserExternalIdentity` (kênh TELEGRAM).
- **Xác thực agent:** mỗi agent một client credential (API key hoặc JWT ký riêng), lưu ngoài repo, map sang `User` dịch vụ của agent.

## 3. Lớp 3 – Agent: xem AGENTS.md

## 4. Lớp 4 – Obsidian
- Vault: `10 Ban GĐ · 20 Kinh doanh & Hợp đồng · 30 Thực đơn & Công thức · 40 Kho & Truy xuất · 45 ATTP · 50 Sản xuất · 55 Giao vận · 60 Kế toán · 70 Nhân sự · 80 Đào tạo · 95 Nguồn BOT · 96 Nhật ký · 97 Tương tác · 98 Công việc · 99 Mẫu · BOD Bảo mật`.
- **Exporter** trong `AppDelicare.Integration` ghi markdown và frontmatter (`id`, `type`, `source: delicare-core`, `generated_at`, `link` về Mission Control). Chạy hằng đêm và khi có sự kiện lớn (đơn đã chốt, sự cố ATTP, báo cáo ngày).
  - 10: bản tin, KPI.
  - 20: hồ sơ khách / hợp đồng (tóm tắt, không có số tài khoản).
  - 30: thực đơn tuần, định mức đã khóa.
  - 40: tồn kho, lô cận date, truy xuất.
  - 45: sự cố ATTP, checklist.
  - 50: kế hoạch / mẻ.
  - 55: chuyến / sự cố giao.
  - 60: tổng hợp chi phí.
  - 70: chấm công tổng hợp.
- **Một chiều:** sửa trong vault không chảy ngược về DB. File sinh tự động có cảnh báo "không sửa tay".
- Agent **đọc** 80 Đào tạo, 99 Mẫu và các thư mục nghiệp vụ. Agent **chỉ ghi** vào 96 Nhật ký, 97 Tương tác, 98 Công việc.
- **BOD Bảo mật** không bao giờ được index hay đọc bởi agent.
- Đường dẫn vault lấy từ cấu hình `Obsidian:VaultPath`.

## 5. Kênh
- **Mission Control** (Angular, route `/mission-control`): hộp duyệt (ApprovalRequest đang chờ người dùng hiện tại), hàng chờ xác nhận lệnh agent, dòng hoạt động agent (IntegrationLog), trạng thái job, bảng KPI ngày, cảnh báo (nhiệt độ, trễ, thiếu NL, phản ánh quá hạn).
- **Telegram:** webhook `/api/integration/telegram/{botCode}` **trả 200 ngay** (Telegram coi 302 là lỗi và gửi lại mãi), lưu `InboundMessage` rồi xử lý bất đồng bộ. Người dùng được định danh qua `UserExternalIdentity` (`ExternalUserId` = telegram id).
- **Zalo OA:** webhook `/api/integration/zalo`. Khách chỉ được: xem đơn / thực đơn, chốt số suất (thành đề xuất, nhân viên kinh doanh xác nhận), báo thiếu suất, gửi phản ánh. Liên kết khách: GAP-INT-01 (`UserExternalIdentity` cần `UserId`; khách không phải User → tạm dùng `ExternalUserId` + `CustomerContact` qua `Note` / `ExternalData`, chờ PO chốt).

## 6. Hạ tầng (đề xuất)
- **Prod = test = Linux + Docker (D-60, anh chốt 2026-11-17):**
  - `nginx` – phục vụ Angular build, HTTPS, chuyển `/api` → `api`; `api` – AppDelicare.WebApi; `job` – AppDelicare.Job (1 bản); SQL Server 2019+ riêng (không nằm chung compose production), có ổ dữ liệu.
  - SQL Server đặt `TZ=Asia/Ho_Chi_Minh` (cột mặc định `sysdatetime()` cùng giờ với IClock). Ứng dụng dùng tài khoản SQL riêng (đọc / ghi / EXECUTE, không DDL), **không dùng `sa`**.
  - Bí mật (chuỗi kết nối, JWT, khóa IoT, license) qua `.env` trên server, không commit. Log ra console, Docker giữ + xoay vòng.
  - Backup full hằng đêm + log 15′; tệp đính kèm (`DocumentStorage`) nằm trên volume riêng, backup cùng DB.
  - `deploy/iis/` chỉ còn dùng chạy thử trên máy dev Windows.
- ToolGateway và AppDelicare.Job chạy tiến trình / container riêng, tách khỏi WebApi.
- Giám sát: Serilog → file + Seq (tùy chọn). Health check `/health`.
