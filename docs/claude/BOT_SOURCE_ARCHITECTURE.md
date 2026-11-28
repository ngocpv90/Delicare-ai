# BOT_SOURCE_ARCHITECTURE – Kiến trúc ứng dụng và source cho bot / kênh tin nhắn

> **Trạng thái:** ✅ **anh chốt 2026-11-18 (D-62).** Thay toàn bộ bản nháp 2026-11-17 cùng tên.
> **Phạm vi:** bot Telegram nội bộ (R1), chuẩn bị sẵn cho Zalo OA / Facebook (sau), chỗ đứng của Hermes và kho tri thức.
> **Tính năng, trải nghiệm, mẫu tin:** `docs/claude/BOT_TELEGRAM.md`. **Task:** ROADMAP mục "ĐỢT BOT".
> **Không thay đổi ở đây:** Tool Gateway (ROADMAP T30–T35, ARCHITECTURE §2) và cấu trúc vault Obsidian (ARCHITECTURE §4) giữ như đã ghi – chốt chi tiết khi mở đợt R2 tương ứng.

## 1. Nguyên tắc (bắt buộc)

1. **Lõi .NET (`api` + `job`) là nơi duy nhất chạm DB và chứa nghiệp vụ.**
2. **Mọi kênh tin nhắn và AI là "khách" của API.** `AppDelicare.Messaging`, Hermes, agent **không** tham chiếu `AppDelicare.Services` / `Data` / `Model`, **không** có chuỗi kết nối DB.
3. **Bot không chứa nghiệp vụ:** hỏi người dùng → gọi API → trình bày. Không tự kiểm quyền thay API (chỉ ẩn nút cho gọn; API quyết định).
4. **Quyền:** bot gọi API nghiệp vụ bằng **JWT của chính user** (token exchange, hạn 10′) → `PermissionFilter` / ObjectMenu (D-31) áp dụng y như web; `CreatedBy` = user thật.
5. **Tin chủ động = outbox:** Service nghiệp vụ ghi `OutboundMessage` (**mã mẫu tin + dữ liệu**, không câu chữ) trong cùng transaction. Câu chữ chỉ nằm ở `AppDelicare.Messaging/Presentation`. Chỉ container `messaging` gửi ra ngoài.
6. **Luật CLAUDE.md giữ nguyên:** khung D-30 (`api/<Controller>/<Action>`, `[FromForm]`, `Result<T>`), không `/api/v1`, **không bảng / cột / FK / schema mới** (thiếu → GAP PROPOSED), SQL Server 2022 (D-61), không API tra cấu trúc DB (D-58), bí mật chỉ ở env / user-secrets, Linux + Docker (D-60), **không thêm gói NuGet mới** khi chưa được duyệt.

## 2. Container

| Container | Project | Việc | Chạm DB | Giữ bí mật |
|---|---|---|---|---|
| `nginx` (sửa) | `deploy/docker/nginx` | HTTPS, Angular; `/api/` → `api`; **`/tg/` → `messaging`**; **chặn `/api/BotChannel/` từ ngoài (404)**; sau này `/zalo/` → `messaging` | – | chứng chỉ |
| `api` (sửa) | `AppDelicare.WebApi` | REST cũ + **API nội bộ cho messaging** (`BotChannel`) + liên kết tài khoản + cấp JWT thay mặt user | Có | JWT key, **hash** khóa client, khóa ký mã liên kết |
| `job` (sửa) | `AppDelicare.Job` | Lịch nghiệp vụ đã có + **lịch thông báo** (ghi outbox) + leo thang | Có | chuỗi DB |
| **`messaging` (MỚI)** | **`AppDelicare.Messaging`** | **Tất cả bot**: Telegram Vận hành, Điều hành (R1); Zalo OA, Facebook (sau) | **Không** | token bot, secret webhook, khóa client `MESSAGING` |
| `hermes` (đợt agent) | `agents/hermes` | Hermes + agent; tự nhận tin của **bot Trợ lý** | **Không** | `ANTHROPIC_API_KEY`, token bot Trợ lý, khóa client agent |

- `messaging`: **1 bản**, `restart: unless-stopped`, healthcheck `/health`.
- Hermes: **dev chạy trên máy anh** (cài bằng script); **server test / production chạy container `hermes`** trong cùng compose.
- Volume mới `obsidian-vault` (đợt T50): `job` ghi, `api` chỉ đọc.

```
Telegram ──/tg/{VH|DH}──► nginx ──► messaging ──HTTP nội bộ (X-Client-Key + JWT user)──► api ──► SQL Server
Telegram (bot Trợ lý) ──► hermes ──► Tool Gateway (T30)                                    ▲
job ── ghi OutboundMessage / lịch / leo thang ─────────────────────────────────────────────┘
messaging ◄── ClaimOutbound (lấy tin cần gửi) ── api
```

## 3. Project và phụ thuộc

```
AppDelicare.Model ◄── AppDelicare.Data ◄── AppDelicare.Services ◄── AppDelicare.WebApi   (api)
                                                               ◄── AppDelicare.Job      (job)
AppDelicare.Messaging (messaging) – KHÔNG ProjectReference nào; gọi WebApi qua HTTP
AppDelicare.Tests ──► mọi project (kể cả Messaging – test hợp đồng DTO)
agents/hermes (Python + cấu hình, ngoài solution)
```

- **Bỏ project `AppDelicare.Integration`** (ARCHITECTURE §1 cũ): Telegram / Zalo / Facebook → `AppDelicare.Messaging/Channels/`; **Obsidian exporter → `AppDelicare.Services` + `AppDelicare.Job`** (vì cần DB) khi làm T50.
- `AppDelicare.Messaging` **không tham chiếu Model** (Model kéo ClosedXML, EPPlus…): tự khai DTO tối thiểu ở `Core/Contracts`; `AppDelicare.Tests/Messaging/Contracts` so tên trường với Request / ViewModel của Model.
- Gói: ASP.NET Core (Kestrel) + `Serilog.AspNetCore`, `Serilog.Sinks.Async` (đã có trong repo); gọi Telegram bằng `HttpClient` tự viết. Không thư viện Telegram.

## 4. Dữ liệu dùng (không thêm bảng / cột)

| Bảng | Dùng thế nào |
|---|---|
| `IntegrationChannel` | **Mỗi bot một dòng**: `ProviderCode = TELEGRAM`, `ChannelCode = TELEGRAM_VH` / `TELEGRAM_DH` / `TELEGRAM_TRO_LY`, `EnvironmentCode` = Development / Staging / Production. `ConfigJson` = cấu hình không bí mật (chat id nhóm, bán kính…) – **API đọc phải che** các khóa có tên `*secret*`, `*token*`, `*key*` |
| `BotConfiguration` | 1 dòng / bot: `BotCode` = `VH` / `DH` / `TRO_LY`, `BotUsername`, `TargetAudience` = INTERNAL / MANAGER / BOARD, `WebhookPath`, `BotStatus`, `SecretReference` = **tên biến môi trường** (vd. `Telegram__Bots__VH__Token`) |
| `UserExternalIdentity` | 1 dòng / (người, bot) – vì người dùng phải `/start` từng bot thì bot mới nhắn được. `ExternalUserId` = telegram user id, `UserId` và / hoặc `EmployeeId` (cả hai cho phép trống), `IdentityStatus` Active / Revoked / **Blocked** (người dùng chặn bot), `LastSeenAt` |
| `InboundMessage` | Update thô. `IdempotencyKey = TG:{botCode}:{update_id}`; `ProcessingStatus` Received → **Processing** → Processed / Failed / **Ignored** |
| `OutboundMessage` | `PayloadJson = { template, data, buttons, scope, level, notBefore?, replyContext?, pin? }`; `MessageStatus` Queued → **Sending** → Sent / Failed / **Blocked** / **Cancelled**; `CorrelationId` = **mã sự việc** (gom các tin cùng một sự việc để sửa đồng loạt: nhận xử lý, đã duyệt); `ProviderMessageId` = message_id Telegram |
| `IntegrationLog` | Chỉ `api` ghi (sổ chỉ ghi thêm): IN / OUT / TOKEN (cấp JWT) / LINK |
| `ObjectMenu` + `UserRole` | Quyền vào bot = mục seed `#/bot-van-hanh`, `#/bot-dieu-hanh` (dữ liệu, không đổi lược đồ); chức năng trong bot = quyền ObjectMenu của màn tương ứng |
| `Employee.Phone`, `Person.PhoneNumber` | Đăng ký bằng chia sẻ số điện thoại (BOT_TELEGRAM.md §2) |

Thiếu chỗ chứa → GAP (DECISIONS D-62): **GAP-INT-02** unique index có lọc `IdempotencyKey`, **GAP-INT-03** cột `ScheduledAt` cho `OutboundMessage` (tạm `PayloadJson.notBefore`), **GAP-INT-04** chỗ lưu bền dòng thời gian của sự việc liên phòng (tạm `PayloadJson` tin gốc).

## 5. Cây thư mục

Ký hiệu: **(MỚI)** · **(SỬA)** · **(KIỂM)** chỉ kiểm tra.

### 5.1 `AppDelicare/AppDelicare.Messaging/` (MỚI – container `messaging`)

```
AppDelicare.Messaging/
├── AppDelicare.Messaging.csproj          Microsoft.NET.Sdk.Web, net8.0
├── Program.cs                            Kestrel: POST /tg/{botCode}, GET /health; --healthcheck
├── appsettings.json                      cấu hình không bí mật
├── Dockerfile
├── Options/
│   ├── MessagingHostOptions.cs           ApiBaseUrl, ClientKey, Mode (Polling | Webhook), WebhookBaseUrl
│   ├── TelegramOptions.cs                Bots{VH|DH → Token, WebhookSecret, Username}
│   └── SendingOptions.cs                 MaxPerSecond (25), PerChatPerSecond (1), BatchSize, UndoSeconds (10)
├── Channels/                             MỖI KÊNH MỘT THƯ MỤC – chỉ chứa phần "nói chuyện với nhà cung cấp"
│   ├── IChannelSender.cs                 Send / Edit / Pin / AnswerCallback → ChannelSendResult
│   ├── ChannelSendResult.cs              Sent(ProviderMessageId) | RetryAfter(seconds) | Blocked | OutOfWindow | Failed
│   └── Telegram/
│       ├── TelegramClient.cs             sendMessage, editMessageText, editMessageReplyMarkup, pinChatMessage,
│       │                                 answerCallbackQuery, sendChatAction, setWebhook, deleteWebhook,
│       │                                 setMyCommands (scope theo chat), getUpdates, getFile
│       ├── TelegramApiException.cs
│       ├── TelegramUpdateParser.cs       lệnh / nút / vị trí (kể cả vị trí trực tiếp) / ảnh / file / liên hệ / reply
│       ├── TelegramSecretValidator.cs    header X-Telegram-Bot-Api-Secret-Token, so khớp constant-time
│       ├── TelegramWebhookEndpoint.cs    kiểm secret → api BotChannel/ReceiveUpdate → 200 (api lỗi → 503)
│       ├── TelegramPollingService.cs     chỉ khi Mode = Polling (máy dev)
│       ├── TelegramStartupService.cs     Mode = Webhook: setWebhook cho từng bot khi khởi động
│       ├── TelegramFormatter.cs          nội dung trung tính → HTML Telegram (escape, cắt 3.800 ký tự)
│       └── Models/                       TgUpdate, TgMessage, TgCallbackQuery, TgLocation, TgContact,
│                                         TgPhoto, TgDocument, TgInlineKeyboard, TgReplyKeyboard, TgForceReply
│   (Zalo/, Facebook/ – thêm khi làm kênh đó, cùng giao diện IChannelSender)
├── Core/
│   ├── DelicareApiClient.cs              HttpClient → api/<Controller>/<Action> ([FromForm] / query), đọc Result<T>
│   ├── BotChannelApi.cs                  bọc các action nội bộ api/BotChannel/*
│   ├── UserTokenCache.cs                 JWT theo user (10′), tự xin lại qua BotChannel/IssueUserToken
│   ├── BotUserContext.cs                 người đang thao tác: telegram id, userId / employeeId, quyền menu (cache ngắn)
│   └── Contracts/                        DTO tối thiểu: ApiResult<T>, PendingApproval, ShiftItem, BatchItem, TripItem…
├── Processing/
│   ├── InboundPump.cs                    xử lý ngay khi nhận + quét BotChannel/ClaimInbound mỗi 5″
│   ├── UpdateDispatcher.cs               lệnh / nút / vị trí / ảnh / liên hệ / gõ mã / trả lời ForceReply → Action / Flow
│   ├── OutboundPump.cs                   ClaimOutbound → kiểm notBefore → render → gửi → MarkOutbound; 429 / 403 / thử lại
│   └── UndoWindow.cs                     giữ quyết định rủi ro thấp UndoSeconds giây rồi mới gọi API
├── Audiences/                            THEO ĐỐI TƯỢNG NGƯỜI DÙNG
│   ├── Internal/                         nhân viên (bot VH) và quản lý (bot DH)
│   │   ├── BotAction.cs                  Bot, Phân hệ, Nhãn, Lệnh, Mục ObjectMenu + cờ, Handler, Thứ tự,
│   │   │                                 Khung thời gian trong ca, Mẫu mã nhận dạng, Ẩn nút
│   │   ├── BotActionRegistry.cs
│   │   ├── Chung/                        dùng ở cả VH và DH
│   │   │   ├── OnboardingActions.cs      /start (mã, CHAMCONG, QR vật thể), chia sẻ số điện thoại, /huylienket
│   │   │   ├── SelfServiceActions.cs     📍 chấm công, 📅 lịch ca, 🏖 xin nghỉ, 🔁 đổi ca, 📝 đơn của tôi
│   │   │   ├── ApprovalActions.cs        danh sách chờ duyệt, duyệt theo mức rủi ro, từ chối có lý do
│   │   │   ├── IncidentActions.cs        nhận xử lý, ghi chú vào thẻ sự việc, đóng sự việc
│   │   │   └── HelpActions.cs            /menu, /help, góp ý
│   │   ├── VanHanh/                      bot VH theo phân hệ: Bep, ATTP, GiaoVan, DiemPhat, Kho, KeHoach,
│   │   │                                 KinhDoanh, Cskh, KeToan, NhanSu, QuanTri (mỗi phân hệ 1 file *Actions.cs)
│   │   └── DieuHanh/                     bot DH: Overview, Approvals (gom), CrossIncidents, Reports, Kpi
│   └── Customer/                         (Zalo – sau) xem đơn, thực đơn, chốt suất, thiếu suất, phản ánh
├── Experience/                           KHUNG TRẢI NGHIỆM (BOT_TELEGRAM.md §1)
│   ├── KeyboardBuilder.cs                3 nút cố định; ≤ 6 chức năng thì hiện phẳng
│   ├── ContextKeyboard.cs                bàn phím theo việc đang làm (giao hàng, chấm công…), trả về khi xong
│   ├── MenuNavigator.cs                  📂 Chức năng → phân hệ → chức năng (sửa cùng một tin, có ⬅️)
│   ├── CodeSearch.cs                     gõ NL- / ME- / PO- / CX- / PA- / PH- … → mở thẳng chi tiết
│   ├── CommandScopeSync.cs               danh sách lệnh "/" riêng cho từng chat theo quyền
│   ├── InputWidgets.cs                   nút số đặt sẵn, bước ±, checklist bấm ✅ / ❌ trong một tin
│   ├── ShiftCard.cs                      Thẻ ca / Thẻ ngày: dựng, ghim, cập nhật (BOT-2.1)
│   └── TaskCard.cs                       Thẻ việc: nút thao tác, đổi trạng thái đồng loạt theo CorrelationId
├── Conversations/
│   ├── ReplyContext.cs                   ngữ cảnh ForceReply nằm trong PayloadJson của OutboundMessage gốc
│   └── Flows/                            RejectReason, LeaveRequest, SwapRequest, Complaint, BatchFinish,
│                                         DeliveryStop, ContainerReturn, Trace…
├── Presentation/
│   ├── CapNguoiNhan.cs                   cấp LD / QL / NV (như bản Apps Script)
│   ├── MessageTemplates.cs               mã mẫu tin → nội dung trung tính (tiêu đề, dòng, nút)
│   └── Renderers/                        mỗi phân hệ một file
└── Callback/
    └── CallbackData.cs                   "<loại>:<hành động>:<id>" ≤ 64 byte; Guid dạng N (32 ký tự)
```

### 5.2 `AppDelicare/AppDelicare.WebApi/` (SỬA – container `api`)

```
Controllers/
├── BotChannelController.cs          (MỚI) NỘI BỘ – chỉ ClientKeyFilter, nginx chặn từ ngoài:
│     ReceiveUpdate · ClaimInbound · CompleteInbound · ClaimOutbound · MarkOutbound
│     ResolveIdentity · LinkByCode · LinkByPhone · IssueUserToken · TouchIdentity
│     GetUsage (đếm chức năng đã dùng) · ReportLocation (vào / ra bán kính)
│     Employee*: GetMyShifts · CheckIn / CheckOut · GetMyRequests  (cho người chỉ có EmployeeId)
├── TelegramLinkController.cs        (MỚI) user web / HR: CreateCode (cho mình), CreateCodeForEmployee (HR), GetMine, Unlink
├── InboundMessageController.cs      (SỬA) Reprocess (216)
├── OutboundMessageController.cs     (SỬA) Enqueue gửi tay (217), Retry (218), GetConversation (219)
├── UserExternalIdentityController.cs (SỬA) Link / Unlink cho admin (220)
└── IntegrationChannelController.cs, BotConfigurationController.cs (KIỂM / SỬA) che ConfigJson, không trả bí mật
Security/ClientKeyFilter.cs          (MỚI) header X-Client-Key → khách (MESSAGING; sau này HERMES, AGENT_<CODE>), so hash
Startup.cs                           (SỬA) filter, Options Clients / Messaging
```

### 5.3 `AppDelicare/AppDelicare.Services/` (SỬA)

```
Interface/  INotificationService · IBotChannelService · ITelegramLinkService · IClientCredentialService · IUserTokenIssuer
Implement/
├── Messaging/                       (MỚI)
│   ├── NotificationService.cs       Enqueue(người nhận, mã mẫu, dữ liệu, correlationId, idempotencyKey) → OutboundMessage
│   │                                trong transaction đang mở; chọn bot theo phạm vi; chọn notBefore theo giờ yên lặng
│   ├── NotificationCatalog.cs       danh mục mẫu tin: phạm vi (cá nhân / phòng / liên phòng / duyệt), mức (KHẨN / THƯỜNG /
│   │                                TỔNG HỢP), người nhận theo quyền, bậc leo thang, mức rủi ro duyệt
│   ├── RecipientResolver.cs         người nhận theo UserGroup × ObjectMenu, theo ca, nhóm chat; fallback bot nhà
│   ├── BotChannelService.cs         lưu update (chống trùng UPDLOCK / HOLDLOCK), claim READPAST, trạng thái, IntegrationLog
│   ├── MessagingRules.cs            giãn cách thử lại, giờ yên lặng theo ca, hạn tin
│   └── Notifications/               ApprovalNotifications · ShiftNotifications · FoodSafetyNotifications ·
│                                    ProductionNotifications · DeliveryNotifications · InventoryNotifications ·
│                                    SalesNotifications · HrNotifications · SystemNotifications
├── Identity/                        (MỚI)
│   ├── TelegramLinkService.cs       mã liên kết, liên kết bằng số điện thoại, gỡ liên kết
│   ├── LinkCodeSigner.cs            userId / employeeId + hạn + HMAC, base64url ≤ 64 ký tự
│   ├── PhoneMatcher.cs              chuẩn hóa số VN (+84 / 0), khớp duy nhất Employee.Phone / Person.PhoneNumber
│   ├── ClientCredentialService.cs   khóa client → loại khách
│   └── UserTokenIssuer.cs           JWT thay mặt user đã liên kết (JwtFactoryService cũ), claim client=MESSAGING
├── ApprovalRequestService.cs        (SỬA) Submit / Decide / Cancel → ApprovalNotifications
├── ShiftAssignmentService.cs        (SỬA) Confirm → ShiftNotifications (notificationSent = true, D-55 k)
├── FoodSafetyChecklistResultService.cs, IotTemperatureService.cs, ProductionBatchService.cs,
│   DeliveryRouteService.cs, DeliveryStopService.cs, MaterialRequirementService.cs, CustomerComplaintService.cs,
│   OrderService.cs, MenuService.cs, PurchaseOrderService.cs, AttendanceEventService.cs, LeaveRequestService.cs
│                                    (SỬA) phát sự kiện theo NotificationCatalog – chỉ thêm lệnh Enqueue, không đổi nghiệp vụ
├── InboundMessageService.cs, OutboundMessageService.cs, UserExternalIdentityService.cs, IntegrationChannelService.cs (SỬA)
└── PermissionService.cs             (KIỂM) dùng cho RecipientResolver (ai có quyền nhận)
Jobs/
├── BotJobs.cs                       (MỚI) ShiftBrief (15′ trước ca), Escalation (mỗi phút), MorningBrief 07:00, DailyReport 17:00,
│                                    RemindUnconfirmedOrders 19:00, IngredientsTonight 21:00, ProgressWatch 15′,
│                                    ShiftReminder + NotCheckedIn mỗi giờ, ApprovalOverdue, ComplaintSla, LeaveHandover
├── DailyJobs.cs                     (SỬA) LockOrders / Mrp ghi thông báo kết quả
Ioc/DelicareServicesModule.cs        (SỬA)
```

### 5.4 Các project / thư mục khác

```
AppDelicare.Model/    Option/ MessagingOptions.cs, ClientsOptions.cs · Request/ BotChannel*, TelegramLink*, IdentityLink*,
                      OutboundMessageEnqueue* · Response/ BotChannel*, UserTokenResponse, ConversationItem*
AppDelicare.Job/      không giữ token; chỉ đăng ký thêm BotJobs qua IScheduledJob đã có
AppDelicare.Tests/Messaging/      (container messaging) Experience, Processing, Callback, Contracts (DTO ↔ Model)
AppDelicare.Tests/Notifications/  (lõi) outbox, BotChannelService, Identity (mã, số điện thoại, token), RecipientResolver,
                                  Escalation, giờ yên lặng theo ca
agents/hermes/        (đợt T43) SOUL.md, skills/, mcp/, config.example.yaml, env.example, Dockerfile – không chứa khóa
deploy/docker/        compose prod / staging: service messaging (+ hermes đợt agent); nginx /tg/ → messaging, chặn
                      /api/BotChannel/; production.env.example / staging.env.example thêm biến mục 6 (placeholder)
DB/seed/              Integration_Telegram_seed.sql (IntegrationChannel + BotConfiguration theo môi trường, ObjectMenu
                      #/bot-van-hanh, #/bot-dieu-hanh), add-only theo Code
DB/proposed/          GAP_INT_02_idempotency_index.sql (PROPOSED – không tự chạy)
docs/                 claude/BOT_TELEGRAM.md · api-tests/bot-*.http · frontend-handoff/BOT-*.md
AppDelicare.sln       (SỬA) thêm AppDelicare.Messaging
```

## 6. Luồng chính

1. **Tin đến:** Telegram → nginx `/tg/{botCode}` → `TelegramWebhookEndpoint` (kiểm secret) → `BotChannel/ReceiveUpdate` (lưu, chống trùng) → 200 → `InboundPump` xử lý ngay → `UpdateDispatcher` → Action / Flow → `DelicareApiClient` gọi API nghiệp vụ bằng JWT user → render → `IChannelSender` → `BotChannel/CompleteInbound`.
2. **Tin chủ động:** Service / Job → `NotificationService.Enqueue` (outbox, cùng transaction; bot theo phạm vi; `notBefore` theo ca) → `OutboundPump` (`ClaimOutbound`) → render → gửi → `MarkOutbound`.
3. **Sửa đồng loạt:** nhận xử lý / đã duyệt → `api` tìm các `OutboundMessage` cùng `CorrelationId` → messaging sửa từng tin qua `ProviderMessageId`.
4. **Leo thang:** `BotJobs.Escalation` tìm tin KHẨN chưa có người nhận quá hạn → Enqueue cho bậc kế tiếp.
5. **Đăng ký:** QR cổng → `/start CHAMCONG` → chưa liên kết → nút chia sẻ số điện thoại → `BotChannel/LinkByPhone` → khớp duy nhất → liên kết. Người có quyền duyệt / quản trị: thêm mã lấy trên web (`TelegramLink/CreateCode`).

## 7. Cấu hình và bí mật (`:` → `__` khi đặt biến môi trường)

| Container | Bí mật (env / user-secrets – không commit) | Cấu hình thường |
|---|---|---|
| `api` | `Clients__MESSAGING__KeyHash`, `Messaging__LinkSigningKey` | `Messaging:*` (thử lại, giờ yên lặng, leo thang, mức rủi ro duyệt, hạn token 10′) |
| `job` | (như hiện tại) | `Jobs:<Code>:At` cho BotJobs |
| `messaging` | `Messaging__ClientKey`, `Telegram__Bots__VH__Token`, `Telegram__Bots__VH__WebhookSecret`, `…__DH__…` | `Messaging:ApiBaseUrl = http://api:8080`, `Telegram:Mode`, `Telegram:WebhookBaseUrl`, `Sending:*` |
| `hermes` | `ANTHROPIC_API_KEY`, token bot Trợ lý, khóa client agent | URL Tool Gateway, mô hình |

**Môi trường:** 3 bộ bot DEV / TEST / PROD (mỗi bộ: VH, DH, TRO_LY). DEV chạy polling trên máy anh; TEST / PROD webhook. `BotCode` giống nhau ở mọi môi trường, chỉ khác token.

## 8. Mở rộng Zalo / Facebook – Zalo ĐÃ LÀM ở AI-19 (D-06: cột CustomerContact.UserId); Facebook chưa làm

> AI-19: `Channels/Zalo/ZaloChannel.cs` (webhook kiểm chữ ký, client gửi CS, pump tin chờ), API `CustomerChannelController` (khóa client) + `CustomerChannelService` (lệnh khách = đề xuất CSKH), token OA lưu mã hóa ở ConfigJson. Hội thoại khách dùng cú pháp số (không lưu trạng thái hội thoại). Chi tiết: `agents/README.md` mục Zalo OA.

- Thêm `Channels/Zalo/` (client, webhook kiểm chữ ký, làm mới token – refresh token lưu mã hóa ở `api`, chính sách khung gửi 48 giờ / 7 ngày / mẫu ZBS) và `Audiences/Customer/`.
- Lõi thêm `CustomerChannelController` (nội bộ, client key): `api` tự tra định danh Zalo → `CustomerContact` → `CustomerId`, **chỉ trả dữ liệu của khách đó**; mọi thao tác ghi của khách là **đề xuất** cho nhân viên xác nhận. Liên kết khách: GAP-INT-01 / D-06 (chốt khi làm Zalo).
- Ngay từ R1: mọi API / bảng / outbox dùng `channelCode`, không viết cứng "telegram"; mẫu tin trung tính + formatter theo kênh.
