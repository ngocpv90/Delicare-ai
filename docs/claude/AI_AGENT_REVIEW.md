# AI_AGENT_REVIEW – Rà soát + chốt kỹ thuật cho đợt Trợ lý AI (AI-0, D-133)

Ngày rà soát: 26/11/2026, theo code `main` (sau UI-6.50). Đối chiếu `docs/reference/DB_SCHEMA_124.md` + entity `AppDelicare/AppDelicare.Model/Entity/*`. Đường dẫn dưới đây tính từ `AppDelicare/` nếu không ghi khác.
Mục đích: với từng thứ "Dùng lại" ở `AI_AGENT_ARCHITECTURE.md` §3 – **đã có gì · thiếu gì · bổ sung ở file nào**, để làm Đợt 1 không phải hỏi lại. **Không có thay đổi code ở AI-0.**

## 0. Tóm tắt – 8 điều quan trọng nhất

1. **Engine duyệt nhận `AGENT_WRITE` không cần đổi bảng**: 1 handler + 1 dòng nhãn + 1 mục menu duyệt. Nhưng **chưa có gì làm hết hạn đề xuất** (không có trạng thái Expired, không job) → AI-9 phải thêm bước hết hạn (đặt `Cancelled` + ghi chú "Hết hạn").
2. **Kiểu actor `Agent` đã có sẵn** (`ActorType.Agent = 3`, `ActorInfo.ForAgent(serviceUserId, agentCode)`, `Data/Share/CurrentActor.cs:7-27`) – chỉ test dùng. Nhật ký tự động đã ghi `AGENT:<mã>:<userId>`.
3. **Tài khoản agent giữ `AccountType = INTERNAL`** (không thêm giá trị AGENT): thêm AGENT sẽ phải sửa CHECK constraint (`CK_User_AccountType`), và `PermissionService` + `PermissionFilter` đang trả **không quyền / 403** cho mọi tài khoản không INTERNAL → agent mất hết quyền. Chặn đăng nhập web bằng **liên kết BotConfiguration** (mục 7).
4. **Liên kết agent ↔ tài khoản** dùng `UserExternalIdentity` (kênh `HERMES`, `ExternalUserId = mã agent`, `UserId = tài khoản AGENT_<MÃ>`) – không cột mới. Bot Telegram không bị lẫn vì `RecipientResolver.ActiveBotsAsync` chỉ lấy `ProviderCode = TELEGRAM`.
5. **Giao quyền** = `IsAllowed(GetAsync(agentUserId)) && IsAllowed(GetAsync(askingUserId))` – `PermissionService.GetAsync(userId)` dùng được cho user bất kỳ. Lưu ý: tài khoản agent **không được là admin** (IsAdmin bỏ qua mọi kiểm); người hỏi đang bị giới hạn bot (`BotLimited`) phải kiểm riêng.
6. **`IntegrationLog` không có cột payload / thời lượng** → token, chi phí, thời lượng ghi vào… không có chỗ. Chọn: `ErrorMessage` (2000) chứa JSON ngắn `{in,out,cost,ms}` khi `LogStatus = Succeeded` – GAP PROPOSED `IntegrationLog.PayloadJson` (mục 3).
7. **SDK MCP C#**: `ModelContextProtocol` + `ModelContextProtocol.AspNetCore` **2.4.0** (bản ổn định mới nhất, có nhóm phụ thuộc net8.0) – ghim `2.4.0`. Hermes trên máy anh **v0.23.1** đã hỗ trợ MCP HTTP (`url` + `headers`, Streamable HTTP).
8. **Công cụ đọc cũ (34 hàm, `LEGACY_GATEWAY.md`)**: 27 hàm đã có method Service tương ứng; thiếu `su_co_ngay`, `kiem_tra_phieu`, `chua_vao_ca` (một phần), `danh_muc` (chưa có tra cứu che SĐT / MST), 4 hàm ghi (thay bằng `propose_*` AI-10).

## 1. Engine duyệt (`ApprovalRequest` / `ApprovalItem` / `ApprovalDecision`)

**Đã có**
- `IApprovalHandler` (`Services/Interface/IApprovalHandler.cs:24-88`): bắt buộc `BusinessType`, `ScreenId`, `BuildPayloadAsync`, `DescribeAsync`, `ApproveAsync(id, actor, note)`, `RejectAsync`; có sẵn mặc định `ApproveAsync(..., proposalJson)` (:70), `GetSummaryAsync` (:60, khuôn APR-1.2), `CancelAsync` (:80), `StepScreenIdsAsync` (:48, nhiều cấp), `IApprovalProposalEditor.ReviseProposalAsync` (:94 – người duyệt sửa đề xuất trước khi duyệt).
- Đăng ký: Autofac quét assembly (`Services/Ioc/DelicareServicesModule.cs:27-29`); `ApprovalRequestService.HandlerOf` khớp BusinessType không phân biệt hoa thường (:243-247). **18 handler**: CONTRACT, LEAVE_REQUEST, MENU_CHANGE, ORDER_CHANGE_AFTER_CUTOFF, ORDER_DEVIATION, PERIOD_UNLOCK, PRICE_ADJUSTMENT, PURCHASE_ORDER, PURCHASE_REQUEST, RECIPE_VERSION, RECON_REOPEN, SHIFT_SWAP, STOCK_ADJUSTMENT, STOCK_COUNT, USER_ACCOUNT_CREATE / _GROUP / _REOPEN (+ PRODUCTION_PLAN_HELD là mục ảo, không handler).
- `CreateAsync(businessType, businessId, proposalJson)` (`ApprovalRequestService.cs:383`): chặn yêu cầu mở thứ 2 cùng (type, id); `RequestedBy = actor`; `DueAt` theo `BusinessRules:ApprovalStepDueHours` (48) / `ApprovalStepDueHoursByType[type]`; **`CorrelationId = ICurrentActor.CorrelationId`** (đặt qua `BeginScope(actor, correlationId)`); `PayloadJson = {"state":…,"proposal":…}`; mỗi cấp 1 `ApprovalStep`, 1 `ApprovalItem` (`APPROVE`); khi duyệt so mã băm state → đổi thì chặn.
- Cột: `ApprovalRequest` (RequestNo, BusinessType 100, BusinessId?, RequestedBy, RequestStatus 30, CurrentStepNo, PriorityCode, **DueAt**, **PayloadJson max**, **CorrelationId 100**, CompletedAt); `ApprovalItem` (ItemType, ItemId, ActionCode 50, PayloadJson, ItemStatus); `ApprovalDecision` (DecisionCode, DecidedBy, DecidedAt, Comment 2000, PayloadHash 128); `ApprovalStep`.
- Trạng thái: Pending, InProgress, Approved, Rejected, Cancelled (không có Expired).
- Nhãn loại: `Implement/Approval/ApprovalSummaries.cs:30-55` `TypeOf()` – thêm 1 dòng `"AGENT_WRITE" => ("Lệnh của agent", "<module>", "ai-agent-write")`.
- Quá hạn: `NotifyOverdueAsync` (`ApprovalRequestService.cs:811`) chỉ **báo** (bậc 1 người duyệt → COO → CEO, bot DH), gọi từ `BotWatchService.Run` (`Implement/Messaging/BotWatchService.cs:88-96`) theo nhịp `Messaging:WatchIntervalSeconds` = 300 s – **không đổi trạng thái**.

**Thiếu → bổ sung (AI-9)**
- `Implement/Approval/AgentWriteApprovalHandler.cs`: `BusinessType = "AGENT_WRITE"`, `ApproveAsync(..., proposalJson)` gọi `AgentCommandRegistry` chạy lệnh thật (method Service + method kiểm tra dùng chung web). `BusinessId` = Guid sinh cho mỗi lệnh (các truy vấn quá hạn / vắng / bản tin lọc `BusinessId != null`).
- Mục quyền duyệt `#/wf-inbox?type=ai-agent-write` (seed `Seeds.Menu5.cs` + script DB) – người xác nhận theo bảng anh chốt.
- Bước hết hạn: thêm vào job (gợi ý `AgentProposalExpiry` – `Services/Jobs/`, mỗi 15 phút / hoặc trong `BotWatchService`): yêu cầu `AGENT_WRITE` mở quá `DueAt` → `Cancelled`, `CompletedAt`, quyết định hệ thống "Hết hạn". Không thêm giá trị trạng thái mới.
- Người hỏi thật: `RequestedBy` = tài khoản agent → mã người hỏi ghi trong `PayloadJson.proposal.askedBy` (và CorrelationId lượt hội thoại). Chặn tự duyệt (`IsSelfBlocked` :372) phải xét **cả** `askedBy`.

## 2. Danh mục agent: `IntegrationChannel` + `BotConfiguration`

**Đã có**
- `IntegrationChannel` (ChannelCode, ChannelName, ProviderCode, EnvironmentCode, ChannelStatus, **ConfigJson max**, SecretReference 500, OwnerUserId, Note); `BotConfiguration` (IntegrationChannelId, **BotCode 50**, BotName, BotUsername, **TargetAudience 100**, WebhookPath, **BotStatus**, LastTokenRotatedAt, **SecretReference 500**, Note) – **không có ConfigJson ở bot**.
- Service / controller CRUD: `Implement/IntegrationChannelService.cs`, `BotConfigurationService.cs`; controller Insert / Update (form) / GetAll / GetById / GetAllData.
- Seed `DB/seed/Integration_Telegram_seed.sql`: kênh TELEGRAM_VH / _DH / _TRO_LY (TRO_LY = BOARD, **Inactive** "tới đợt agent"), `SecretReference = Telegram__Bots__<Mã>__Token` (tên biến môi trường).

**Chốt cách dùng (AI-2)**
- 1 kênh `ChannelCode = HERMES`, `ProviderCode = HERMES`, `ConfigJson` = cấu hình chung (mô hình mặc định, giới hạn ngân sách, URL API Hermes – không khóa).
- Mỗi agent 1 dòng `BotConfiguration`: `BotCode` = mã agent (VIVI, WH, KITCHEN, QA, KNOW…), `BotName` = tên hiển thị, `BotStatus` = Active / Inactive (**công tắc tắt**), `TargetAudience` = phòng ban, `SecretReference` = **tên** biến chứa khóa agent (vd `Agent__Keys__WH`), `Note` = giới hạn riêng (JSON ngắn) nếu cần. `ActiveBotsAsync` lọc `TELEGRAM` nên bot Vận hành / Điều hành không bị ảnh hưởng.
- Bot TRO_LY (Telegram) bật ở AI-8 cho bot Trợ lý; Hermes giữ gateway Telegram của TRO_LY (BOT_TELEGRAM.md:13 – không chạy trong `messaging`).

## 3. `IntegrationLog` – nhật ký lệnh + chi phí

**Đã có**: cột IntegrationChannelId?, DirectionCode 20, **OperationCode 100**, **CorrelationId 100**, **IdempotencyKey 200**, RequestAt, ResponseAt, HttpStatus, LogStatus 30, ErrorCode 100, ErrorMessage 2000, **PayloadHash 128**. `IntegrationLogService.Save` chỉ Insert (Update / Delete trả "chỉ ghi thêm"). Người ghi hiện tại: `BotChannelService.AddLog` (:499), `TelegramLinkService.AddLog` (:449), `UserTokenIssuer` (:64).

**Thiếu**: không có cột payload, không cột thời lượng (ResponseAt − RequestAt chỉ chính xác tới giây).
**Chốt (AI-3, AI-13)**: mỗi lời gọi công cụ 1 dòng: `IntegrationChannelId` = kênh HERMES, `DirectionCode = IN`, `OperationCode` = tên công cụ, `CorrelationId` = lượt hội thoại, `IdempotencyKey` = mã xác nhận (ghi), `PayloadHash` = SHA-256 tham số, `HttpStatus`, `LogStatus` Succeeded / Failed / Denied, `ErrorCode` = `AGENT:<MÃ>` khi thành công (để lọc theo agent) hoặc mã lỗi, `ErrorMessage` = JSON ngắn `{"ms":…,"in":…,"out":…,"cost":…,"user":"<askedBy>"}`. **GAP PROPOSED `GAP-AI-LOG`**: thêm `IntegrationLog.PayloadJson nvarchar(max)` + `DurationMs int` (chờ anh duyệt; tạm dùng cách trên).

## 4. `OutboundMessage` – tin chủ động

**Đã có**: `INotificationService.EnqueueAsync(NotificationRequest)` (`Implement/Messaging/NotificationService.cs:26` – tìm người nhận, giờ yên lặng, chống trùng theo `IdempotencyKey`, 1 dòng / người); `BusinessNotifier.SafeEnqueueAsync / ToMenusAsync / Callback` (`Implement/Messaging/BusinessNotifier.cs:19,38,70`); container `messaging` lấy tin qua `BotChannel/ClaimOutbound` (`WebApi/Controllers/BotChannelController.cs:39`, chỉ mạng nội bộ + khóa client; CTE READPAST/UPDLOCK, tôn trọng `notBefore`) → `MarkOutbound` (ghi IntegrationLog).
**Chốt (AI-8, AI-11)**: agent **không** ghi thẳng OutboundMessage – gọi công cụ `gui_tin` → method Service mới `AgentService.EnqueueMessage` → `BusinessNotifier.SafeEnqueueAsync` (BotCode TRO_LY / VH / DH, `CorrelationId` lượt, `IdempotencyKey` = `AGENT:<MÃ>:<khóa>`). Thiếu: template `AGENT_TEXT` trong `NotificationCatalog` (giống `ManualText` nhưng ghi nguồn agent).

## 5. `CommonService.Alerts` – nguồn sự kiện

**Đã có**: `GetAlerts(branchId)` (`Implement/CommonService.Alerts.cs:74`) – 13 mã (STOCK_BELOW_MIN, APPROVAL_PENDING, QC_INCOMING_PENDING, LOT_ON_HOLD, TEMPERATURE_OUT_OF_RANGE, COMPLAINT_OVERDUE, COMPLAINT_OPEN, OUTBOUND_FAILED, LOT_EXPIRING, ACCOUNT_RESIGNED_OPEN, SHIFT_SHORT, CLEAN_TRAYS_SHORT, ROUTE_NOT_READY), **lọc theo quyền của actor hiện tại** (`PermissionService.GetAsync(actor.UserId)` + `AlertScreens`), cache theo (user, chi nhánh). `GetSharedCounts` không lọc quyền (dashboard). Tiền lệ gọi ngoài HTTP: `Services/Jobs/BotJobs.cs:93`.
**Chốt (AI-4)**: `AgentService.GetEvents(since)` chạy trong scope `ActorInfo.ForAgent` → `GetAlerts` tự lọc theo quyền agent; cộng duyệt chờ / quá hạn (`ApprovalRequestService.GetMyPending` – theo actor), lần chạy job lỗi (`AnalyticsRefreshLog` RunStatus Failed từ `since`), tin gửi lỗi (`OutboundMessage` Failed). Thiếu: `since` – Alerts không có mốc thời gian → mỗi sự kiện mang khóa `<mã>:<ngày>:<số lượng>` để Hermes tự lọc trùng.

## 6. `AnalyticsRefreshLog` + khuôn host `AppDelicare.Job`

**Đã có**: cột JobCode, RunId, StartedAt, CompletedAt, RunStatus (Running / Succeeded / Skipped / Failed / Locked), RowsProcessed, SourceWatermark 500, ErrorMessage. Chỉ `Services/Jobs/AnalyticsRunGate.cs` ghi (khóa `sp_getapplock DELICARE_JOB_<code>`, ghi trên kết nối riêng nên Failed không mất khi rollback). `JobRunService.Run` (:121) đặt actor System + `BeginScope(actor, "job-<code>-<runId>")`. Job = `IScheduledJob` (tự đăng ký), lịch `Jobs:<Code>`.
**Khuôn cho `AppDelicare.ToolGateway` (AI-1)**: chép `AppDelicare.Job/Program.cs` (Generic Host + Autofac `DelicareServicesModule`, cấu hình `appsettings.shared*.json` → appsettings → user-secrets (Dev) → biến môi trường, `AddDelicareCore`, Serilog, kiểm `ConnectionStrings:MainConnectionString`), đổi sang `WebApplication` (Kestrel) vì cần HTTP; `.csproj` net8.0 tham chiếu **chỉ** `AppDelicare.Services` (Data / Model đi kèm), link appsettings WebApi như Job, cùng UserSecretsId. Dockerfile theo `AppDelicare.Job/Dockerfile` (aspnet:8.0, TZ, `USER app`, thư mục ghi được) + `EXPOSE 8080`, không publish cổng.
**Job xuất kho tri thức (AI-5)** ghi `AnalyticsRefreshLog` qua `JobRunService` như mọi job.

## 7. Tài khoản agent – `User.AccountType`, đăng nhập, `ICurrentActor`

**Đã có**
- `User.AccountType` (max 20, mặc định INTERNAL; migration `DB/migrations/20261123_02_hr_acc_person_link.sql:20-24`, CHECK `INTERNAL | CUSTOMER | SUPPLIER`; hằng `Implement/Identity/PersonLink.cs:19-27`).
- Đăng nhập: `AuthenticationController.Login` → `LoginService.Authenticate` (`Implement/LoginService.cs:32`): mật khẩu (:38-48) → IsDeleted (:49) → IsActive (:57) → không INTERNAL → 403 (:66-73) → mật khẩu tạm (:75).
- Tài khoản `system` (seed T23, `SystemUsers:JobUserId`) chỉ được chặn bằng `PassWord = '!NO-LOGIN!'` (không băm khớp) – **không có kiểm trong code**.

**Chốt (AI-2)** – "không mật khẩu + chặn trong LoginService theo liên kết BotConfiguration, không theo tên nhóm cứng":
- Mỗi agent: `User` `UserName = agent.<mã>` (vd `agent.wh`), `AccountType = INTERNAL`, `PassWord = '!NO-LOGIN!'`, `IsActive` theo công tắc, nhóm `AGENT_<MÃ>` (`UserGroup`, quyền cấp trên ma trận cũ – **không IsAdmin**).
- Liên kết: `UserExternalIdentity` (`UserId` = tài khoản agent, `IntegrationChannelId` = kênh HERMES, `ExternalUserId` = mã agent, `IdentityStatus = Active`).
- `LoginService.Authenticate` sau dòng :73: nếu tồn tại `UserExternalIdentity` của user trên kênh `ProviderCode = HERMES` → 403 "Tài khoản agent không đăng nhập web". Thêm cùng kiểm vào luồng làm mới phiên / cấp token bot (`UserTokenIssuer`) để agent không lấy JWT người dùng.
- Actor: ToolGateway mở `ICurrentActor.BeginScope(ActorInfo.ForAgent(agentUserId, mã), "<lượt hội thoại>")` cho mỗi lời gọi → `CreatedBy` = tài khoản agent, nhật ký ghi `AGENT:<mã>:<id>`.
- Không thêm giá trị AccountType (lý do ở §0 mục 3).

## 8. `PermissionService` – quyền giao nhau

**Đã có**: `GetAsync(Guid userId)` (`Implement/PermissionService.cs:34` – user bất kỳ, cache, user ngừng → Blocked, không INTERNAL → None), `IsAllowed(perms, screenId, action)` (:98 – IsAdmin luôn đúng; Approve = `#/wf-inbox?type=<màn>` Access + Edit), `Restrict` (:83 – chỉ cắt khi principal HTTP là chính user có claim `BotLimited`).
**Thiếu → bổ sung (AI-2)**: `Implement/AgentAccess.cs` (static, như `CommonAccess`): `Task<string?> DenyAsync(IPermissionService, Guid agentUserId, Guid? askingUserId, string screenId, PermissionAction action)` = từ chối nếu agent **hoặc** người hỏi không có quyền; người hỏi có identity `Pending` (bot giới hạn) → bỏ quyền Duyệt như `Restrict`. Việc theo lịch (`askingUserId = null`) chỉ dùng quyền agent. Mọi công cụ gọi `AgentAccess.DenyAsync` trước khi gọi Service (Service vẫn tự kiểm theo actor agent).

## 9. `UserExternalIdentity` – người hỏi qua Telegram

**Đã có**: cột UserId?, EmployeeId?, IntegrationChannelId, ExternalUserId 255, ExternalUsername, DisplayName, LinkedAt, IdentityStatus (Active / Pending / Blocked), LastSeenAt. Tra: `TelegramLinkService.FindIdentityAsync(db, botCode, externalUserId)` (`Implement/Identity/TelegramLinkService.cs:255`); quyền dùng bot `CanUseBot` (:252); `UserTokenIssuer` cấp JWT 10 phút + claim BotLimited khi Pending.
**Chốt**: Hermes gửi kèm mỗi lời gọi `telegram_user_id` + bot (`TRO_LY`) → ToolGateway tra `FindIdentityAsync("TRO_LY", id)` → `askingUserId`. Không có / bị chặn → chỉ trả lời công khai, không dùng công cụ dữ liệu. Không cần thêm gì ở bảng.

## 10. `AuditChange` – lịch sử ghi chú kho tri thức

**Đã có khuôn** (`BillingConfirmation.cs`, `AccountingPeriodRules.cs`, `SupplierReturnService.cs:117`): `EntityName` hằng, `IdChange` Guid, `Action`, `Changes` JSON, `CreatedBy / CreatedDate` gán tay, chỉ ghi thêm (`DatabaseContext.AppendOnlyTypes`). Dòng thêm tay **không** có đầu "Actor | CorrelationId" như dòng tự động → tự ghi vào JSON.
**Chốt (AI-5)**: `Implement/Knowledge/KnowledgeAudit.cs`: `Entity = "KnowledgeNote"`, Action Created / Updated / Approved / Archived / Exported, `IdChange` = Guid xác định từ đường dẫn (MD5 đường dẫn chuẩn hóa – như `AccountingVoucherService.KeyGuid`), `Changes = {path, sha256, prevSha, actor, correlationId, note}`. Lịch sử = `Where EntityName + IdChange OrderBy CreatedDate`. `CommonService.GetHistory` không dùng được (chưa đăng ký tài nguyên) – màn Kho tri thức có API lịch sử riêng.

## 11. Lưu trữ + Docker

**Đã có**: `DocumentStorage:RootPath` (mặc định `App_Data/DocumentFiles`, tuyệt đối hóa ở `Services/Ioc/ServiceRegistration.cs:35-41`). Compose gốc `docker-compose.yml`: api, web, job (profile job), db-sync, messaging, **hermes** (profile hermes, `nousresearch/hermes-agent`, volume `hermes-data`, ghi chú đã nhắc "Tool Gateway"); volume api-import, api-docs, hermes-data. `deploy/docker/docker-compose.staging.yml` / `.production.yml`: db / api / job / messaging (+ nginx) – **chưa có hermes**, volume `delicare-docs`.
**Bổ sung (AI-1, AI-5)**: tùy chọn `Knowledge:VaultPath` (mặc định `App_Data/Knowledge`, tuyệt đối hóa như DocumentStorage) + volume `knowledge` gắn api / job / toolgateway ở cả 3 compose; service `toolgateway` sau `messaging`, chỉ `expose`, biến `ConnectionStrings__…` như api, khóa agent qua biến môi trường; hermes (staging / production) thêm ở AI-8 / AI-15. `deploy/README.md` thêm đoạn cạnh mục DocumentStorage (:112-115).

## 12. Công cụ đọc cũ (`LEGACY_GATEWAY.md`, 34 hàm: 29 đọc + 5 ghi) → method Service

| Hàm cũ | Method Service hiện có | Ghi chú |
|---|---|---|
| nguoi_dung_hien_tai | `IUserService.GetCurrent` | trả người hỏi, không phải agent |
| get_ton_kho | `IInventoryBalanceService.GetSummary` | |
| get_lo_can_date | `IMaterialLotService.GetList(ExpiringWithinDays, InStock)` | |
| du_bao_thieu_hut | `IMaterialRequirementService.GetHorizon` | |
| tinh_nguyen_lieu | `IMaterialRequirementService.GetByDate` | chỉ đọc – không gọi `Calculate` |
| get_don_hang_ngay / don_hang_khoang | `IOrderService.GetDay` / `GetBoard(MealDateFrom/To)` | |
| truy_xuat_lo / truy_xuat_me | `IMaterialLotService.Trace(Guid)` / `IProductionBatchService.Trace(Guid)` | công cụ đổi mã lô / số mẻ → Id trước |
| bao_cao_ngay / kpi_khoang | `IKpiSnapshotService.GetDaily` / `GetMonthly` | |
| bao_cao_dieu_hanh | `IDashboardService.GetExecutive` | |
| su_co_ngay | **thiếu** | gộp mới `AgentService.GetIncidents(date)` từ Alerts + `ITemperatureLogService.GetDayBoard` + `ICustomerComplaintService.GetList` |
| ke_hoach_ngay | `IProductionPlanService.GetDays` / `GetDayBoard` | |
| tien_do_bep / _chi_tiet | `IProductionBatchService.GetBoard` / `GetStageBoard` | |
| hao_hut_khoang | `IMealWasteRecordService.GetSummary` | |
| thuc_don_tuan / doi_mon_khoang | `IMenuService.GetWeek` / `IMenuChangeRequestService.GetWeekChanges` | đổi món theo tuần – khoảng bất kỳ ghép nhiều tuần |
| giao_van_ngay / giao_van_truc_tiep | `IDeliveryRouteService.GetBoard` / `GetTrackBoard` | |
| phan_anh_khach | `ICustomerComplaintService.GetList` | nội dung khách = khối **dữ liệu** (chống chèn lệnh) |
| hop_khay | `IPackagingLedgerService.GetBalance` | |
| cham_cong_ngay / cong_thang | `IAttendanceEventService.GetDayBoard` / `GetMonthGrid` | |
| chua_vao_ca | một phần (`GetDayBoard`) | lọc "chưa vào ca" trong công cụ |
| danh_muc | `ICommonService.GlobalSearch` | **thiếu** bản che SĐT / MST / email → thêm `CommonService.AgentLookup` |
| ds_cho_duyet | `IApprovalRequestService.GetMyPending` | theo người hỏi |
| kiem_tra_phieu | **thiếu** | AI-9: tách method kiểm tra trong Service (dùng chung web) |
| tao_phieu / xac_nhan_phieu / huy_phieu / duyet_phieu / xu_ly_appsheet_kho | – | thay bằng `propose_*` 2 pha (AI-9, AI-10); `duyet_phieu` = `IApprovalRequestService.Decide` (người, không agent) |

Lưu ý: `BOT_TELEGRAM.md:140` ghi "26 công cụ đọc" – lệch với 29 trong `LEGACY_GATEWAY.md`; danh bạ AI-3 lấy theo `AI_AGENT_ARCHITECTURE.md` §4 (~40).

## 13. Thư viện MCP (anh đã duyệt SDK chính thức)

- **`ModelContextProtocol` + `ModelContextProtocol.AspNetCore` 2.4.0** – bản ổn định mới nhất trên NuGet (11/2026), có nhóm phụ thuộc **net8.0**; gói AspNetCore là bản cho máy chủ MCP qua HTTP (Streamable HTTP, `MapMcp()`). **Ghim đúng `2.4.0`** trong `AppDelicare.ToolGateway.csproj` (không dùng `*`). Hiện không .csproj nào tham chiếu MCP / JSON-RPC.
- Kiểm lại khi làm AI-1 (bước đầu): build net8.0 + chạy thử `initialize` / `tools/list` từ Hermes. Không chạy được → phương án dự phòng: lớp JSON-RPC tối thiểu (`initialize`, `tools/list`, `tools/call`) trong ToolGateway, ghi "Quyết định tự chọn".
- Nguồn: [nuget.org – ModelContextProtocol.AspNetCore](https://www.nuget.org/packages/modelcontextprotocol.aspnetcore), [bản 2.3.1](https://www.nuget.org/packages/modelcontextprotocol.aspnetcore) (net8.0 tương thích).

## 14. Hermes Agent (máy anh – chỉ tham khảo, không đọc / in khóa)

Chi tiết ở `app-delicare-agents/README.md`. Tóm tắt: **v0.23.1** (2026.9.24, cài git); cấu hình `~/.hermes/config.yaml` (model mặc định Sonnet 5.5, nén Haiku 5.5, đã tắt terminal / file / code / browser / web…, `skills.write_approval: true`); MCP hiện là **stdio** (`py delicare_mcp.py`) gọi Apps Script cũ – sẽ thay bằng MCP HTTP trỏ `toolgateway` (`url` + `headers`); API server mặc định cổng **8642** (OpenAI-compatible, khóa `API_SERVER_KEY`); cron `hermes cron add` (biểu thức 5 trường, múi giờ theo profile); profile `hermes -p <tên>`; 6 skill tiếng Việt trong `~/.hermes/skills/delicare/`.

## 15. Việc tài liệu (làm trong AI-0)

- `CLAUDE.md` dòng PHẠM VI: bỏ "Hermes + quản lý agent" khỏi "ĐỂ SAU" → "đợt AI theo D-133".
- `docs/claude/AGENTS.md`: 13 agent (thêm KNOW, gộp thực đơn vào PLAN), bỏ "Hangfire", trỏ về `AI_AGENT_ARCHITECTURE.md`.
- ROADMAP T30–T35, T43, T44, T50, T51 đã ghi `REPLACED → AI-x` (anh cập nhật cùng D-133).

## 16. GAP PROPOSED phát sinh (chờ anh)

| Mã | Đề xuất | Tạm thời |
|---|---|---|
| GAP-AI-LOG | `IntegrationLog.PayloadJson nvarchar(max)` + `DurationMs int` | JSON ngắn trong `ErrorMessage` khi thành công |
| GAP-AI-EXP | Trạng thái duyệt `Expired` | `Cancelled` + quyết định hệ thống "Hết hạn" |
| GAP-AI-BOTCFG | `BotConfiguration.ConfigJson` (cấu hình riêng từng agent) | `IntegrationChannel.ConfigJson` của kênh HERMES theo khóa mã agent |
