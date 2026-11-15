# ROADMAP – Danh sách task

**Trạng thái:** `TODO` · `DOING` · `DONE` · `BLOCKED(<lý do / D-xx>)` · `DEFERRED(R2)`.

Mỗi task có: mục tiêu, phụ thuộc, việc cần làm, **tiêu chí nghiệm thu (AC)**. Chỉ đánh `DONE` khi mọi AC đạt, build xanh và test xanh. Task lớn được phép tách thành Txx.1, Txx.2… ngay trong file này.

## ★ ƯU TIÊN CỦA ANH (2026-11-14): RELEASE 1 = CHỈ BACKEND (.NET + SQL) đủ API cho mockup

**Frontend Angular (`app-delicare-client/`) NGOÀI PHẠM VI (D-20): không đọc, không sửa, không build, không cần tương thích.** Backend làm đúng theo `DELICARE_API_Specification.md`. Sau khi xong backend anh sẽ giao frontend. Các task màn hình Angular (T42, T44-UI, T45, T46, T47) chuyển sang `DEFERRED(FE)`; thay bằng task API tương ứng: T45B, T46B, T47B.

**Chọn task theo THỨ TỰ BẢNG DƯỚI**, không theo số task. Hết mỗi lát:
- API chạy trên staging backend;
- Swagger đầy đủ;
- có **Postman / `.http` collection** trong `docs/api-tests/` để thử từng luồng;
- cập nhật `docs/claude/UAT_CHECKLIST.md` (kịch bản thử bằng API);
- báo anh.

| Lát | Backend giao được | Task theo thứ tự |
|---|---|---|
| R1.0 Nền móng | API an toàn, đúng 124 bảng, phân quyền thật, test, staging backend | T00 → T01 → T04 → T02 → T03 → T05 → T06 → T07 → T08 → T09 → **T48.1 (staging backend)** |
| R1.1 Danh mục và quản trị (P0–P2) | API quản trị, nhóm quyền, menu; danh mục khách, điểm giao, hợp đồng, NCC, nguyên liệu, đơn vị, kho, món, công thức, ca | T10 → T21.1 (seed Shift) → T45B → T49 (API import danh mục) → **UAT-1 (API)** |
| R1.2 Đơn hàng (P3) | Hợp đồng, thực đơn tuần (buổi × lựa chọn, FC), đơn hàng, chốt 20h, duyệt | T11 → T12 → T13 → T46B.1 (API P3 + in thực đơn) → **UAT-2** |
| R1.3 Kho và mua hàng (P4) | Nhập kho + QC, FEFO, kiểm kê, MRP, báo giá → PO, job 20h / 20h30 / 21h | T16 → T14 → T15 → T23.1 → T46B.2 (API P4 + in phiếu trước SX, đơn đặt hàng) → **UAT-3** |
| R1.4 Sản xuất và ATTP (P5–P6) | Kế hoạch mẻ, cấp NL, nhiệt độ lõi, lưu mẫu, checklist, truy xuất | T17 → T18 → T46B.3 (API P5 + in phiếu sản xuất) → T47B.1 (API P6) → **UAT-4** |
| R1.5 Giao vận, phản ánh, hộp/khay (P7) | Chuyến, điểm dừng, nhiệt độ giao, POD, phản ánh SLA, hộp/khay, vệ sinh | T19 → T20 → T47B.2 (API P7) → **UAT-5** |
| R1.6 Nhân sự, KPI, dashboard (P8) | Ca, chấm công, nghỉ / đổi ca, KPI, API dashboard điều hành | T21 → T22 → T23.2 → T47B.3 (API P8 + dashboard) → **UAT-6** |
| R1.7 Go-live backend | Production backend, tài liệu API cho team frontend | T48.2 → T50R1 |

**`DEFERRED(FE)`** – chờ anh giao frontend: T42, phần UI của T44, T45, T46, T47 (màn hình Angular).

**`DEFERRED(R2)`:**
- T30–T35: Tool Gateway / MCP / `POST /api/gateway` (T35 tùy chọn).
- T40–T41: Telegram, Zalo.
- T43: cấu hình agent.
- T50–T51: Obsidian.
- T60–T61: chỉ nạp dữ liệu demo Sheets (tùy chọn).
- GAP R2 (D-21…D-23): bán lẻ / POS, bảo trì thiết bị, CRM marketing, sổ CAPA, xuất dữ liệu sang MISA.
- T62–T63: **hủy**, vì Sheets là demo, không có cắt chuyển (D-16).

**Quy ước tách task:**
- T21.1 = chỉ phần seed Shift của T21.
- T23.1 / T23.2 = các job tương ứng trong T23.
- T46B.1 = API P3, T46B.2 = API P4, T46B.3 = API P5.
- T47B.1 = API P6, T47B.2 = API P7, T47B.3 = API P8 + dashboard.

Danh sách màn hình mỗi pha lấy theo `docs/mockup/js/data/catalog.js` (cột phase / screen_id). Endpoint và logic theo `docs/reference/DELICARE_API_Specification.md` + `DELICARE_API_Catalog.csv`.

---

## GIAI ĐOẠN 0 – Chuẩn bị

### T00 – Khởi tạo môi trường làm việc · `DONE` (2026-11-14)
- **Cài lệnh tắt:** nếu chưa có thư mục `.claude/` ở gốc repo thì sao chép `docs/claude/claude-config/commands/*` → `.claude/commands/` và `docs/claude/claude-config/settings.json` → `.claude/settings.json`. Báo anh: từ phiên sau có thể gõ `/next`, `/task T13`, `/status`, `/verify`, `/gap`.
- Repo đã có git. Chạy `git status`, `git branch -a`, `git log --oneline -5`, `git remote -v`. Ghi lại nhánh hiện tại và các file đang sửa dở.
  - **Không đụng** vào các file anh đang sửa dở. Ví dụ ngày 10/11: `IngredientService`, `CompanyService`, `SupplierService`, `WarehouseService`…
  - Nếu có thay đổi chưa commit: hỏi anh trước khi tạo nhánh.
- `.gitignore` ở gốc đã được cập nhật sẵn (bin/obj, node_modules, backup, `*.local.md`…). Kiểm tra `git status` để chắc `docs/claude/LOCAL_DEV.local.md` và `backup-db/` **không** bị theo dõi.
- Tạo nhánh `chore/T00-claude-setup`. Commit `CLAUDE.md`, `docs/`, `.claude/` và `.gitignore`.
- Build và ghi kết quả vào PROGRESS (baseline):
  - `dotnet --info`
  - `dotnet build AppDelicare/AppDelicare.sln`: số lỗi và số cảnh báo
  - **Không** build hay cài gì trong `app-delicare-client/` (ngoài phạm vi).
- **DB dev = `AppDelicare` trên SQL Server local** (xem LOCAL_DEV.local.md):
  - Kết nối bằng connection string trong `appsettings.json` / user-secrets (không in mật khẩu).
  - Truy vấn **chỉ đọc** `INFORMATION_SCHEMA.TABLES/COLUMNS`, so với `docs/reference/20261113_structure_db_appdelicare.sql`. Báo: DB đang có bao nhiêu bảng (91 = chưa nâng cấp; 124 = đã chạy script nâng cấp), lệch cột nào.
  - Nếu DB chưa nâng cấp: **dừng và hỏi anh** (D-14) có chạy `DB/migrations/20261113_restore_structure_db_appdelicare.sql` không. Nếu anh đồng ý: backup trước (`BACKUP DATABASE` ra thư mục `backup-db/`), chạy script, kiểm tra lại đủ 124 bảng.
  - **Lấy stored procedure hiện có ra file:** đọc `sys.procedures` + `OBJECT_DEFINITION` (chỉ đọc), ghi mỗi SP thành `DB/sp/<tên>.sql` dạng `CREATE OR ALTER`. Cùng làm với function / view nếu có. Liệt kê SP mà code gọi (`QueryMultipleUsingStoreProcAsync("usp...")`) nhưng **không có** trong DB.
  - Chạy WebApi, gọi `POST api/Authentication/Login` bằng tài khoản dev admin (LOCAL_DEV.local.md) qua Swagger hoặc `.http`, rồi gọi thử 1 API có `[Authorize]`.
- Liệt kê bí mật đang nằm trong code **theo vị trí file** (không ghi giá trị).
- AC:
  - [x] Nhánh `chore/T00-claude-setup` có commit tài liệu.
  - [x] PROGRESS có kết quả build backend.
  - [x] Có báo cáo lệch giữa DB dev và DDL 124 bảng (hoặc ghi rõ chưa kết nối được và lý do).
  - [x] `DB/sp/` chứa toàn bộ SP lấy từ DB dev; có danh sách SP code gọi mà DB thiếu.
  - [x] Đăng nhập qua API thành công, nhận token, gọi được API có bảo vệ.
  - [x] Có danh sách vị trí bí mật.

---

## GIAI ĐOẠN 1 – Làm sạch lõi (CODE_AUDIT A1–A12)

### T01 – Đưa bí mật ra khỏi code · phụ thuộc T00 · `DONE` (2026-11-14)
- Chuyển ra cấu hình: khóa ký JWT (`ConfigureServices.cs`), key ApplicationInsights QuickPulse, license GemBox (`Startup.cs`), connection string. Đọc từ `IConfiguration`: `Jwt:SigningKey`, `ApplicationInsights:QuickPulseApiKey`, `Licenses:GemBox`.
- Bật `dotnet user-secrets` cho WebApi. Tạo `appsettings.Development.json.example` chỉ chứa placeholder.
- Bật `RequireExpirationTime = true`. Thời hạn token lấy từ cấu hình.
- Thêm `docs/claude/SECRETS_TODO.md`: danh sách khóa anh cần **tạo mới / đổi** (không ghi giá trị cũ).
- AC: grep repo không còn chuỗi bí mật cũ; app chạy được khi secret nằm trong user-secrets; thiếu secret thì báo lỗi rõ lúc khởi động.

### T02 – Định danh người thao tác (ICurrentActor) · phụ thuộc T00 · `DONE` (2026-11-14)
- Bỏ hành vi fallback "user đầu tiên" trong `AuthenticatedService`. Tạo `ICurrentActor { ActorType, UserId?, AgentCode?, CorrelationId }`.
  - HTTP: lấy từ claim.
  - Job: actor `SYSTEM` (User dịch vụ cấu hình `SystemUsers:JobUserId`).
  - ToolGateway: actor `AGENT`.
- Viết lại `UserInfo` bằng truy vấn có tham số.
- `DatabaseContext.SaveChangesAsync` dùng `ICurrentActor`. Không có actor khi ghi thì ném `UnauthorizedActorException`.
- Middleware gắn `CorrelationId` (header `X-Correlation-Id` hoặc tự sinh) và đưa vào Serilog scope.
- AC: unit test chứng minh ghi khi không có actor thì bị chặn; request có token thì CreatedBy đúng; log có CorrelationId.

### T03 – Pipeline HTTP chuẩn · phụ thuộc T01 · `DONE` (2026-11-14)
- Sửa thứ tự middleware: ExceptionHandler → HttpsRedirection → Static → Routing → CORS (danh sách origin lấy từ `Cors:AllowedOrigins`) → Authentication → Authorization → Endpoints. Bỏ `ServiceLocator.BuildServiceProvider`. Bỏ `CorsMiddleware` thừa.
- Global exception handler: lỗi chưa bắt → log + 500, không lộ stack trace.
- Áp dụng **quy ước spec mục 1.2** cho toàn bộ route `/api/v1`: envelope `{success, data, message, errors, traceId}`, ProblemDetails / mã lỗi theo Phụ lục A, phân trang chuẩn, camelCase, lọc cột deprecated và `PassWord`. Làm thành base class / filter / middleware dùng chung (`ApiControllerBase`, `EnvelopeResultFilter`, `ExceptionToErrorMiddleware`).
- Hỗ trợ `If-Match` / ETag (ModifiedDate), `Idempotency-Key` (lưu kết quả lệnh theo key – nơi lưu: `IntegrationLog` với `IdempotencyKey` có sẵn, PROPOSED), header `X-Branch-Id`.
- Route CRUD cũ sinh tự động: không đụng ở task này (sẽ được thay dần bằng v1 ở T45B…).
- Bật Swagger mô tả đầy đủ (XML comments), JWT trong Swagger, nhóm theo module.
- AC: swagger chạy; test tích hợp cho 1 endpoint v1 mẫu: 200 đúng envelope, 400 / 422 đúng mã lỗi, 409 khi If-Match lệch, Idempotency-Key gửi 2 lần chỉ ghi 1 lần.

### T04 – Nâng gói lên .NET 8 đồng bộ · phụ thuộc T00 · `DONE` (2026-11-14)
- EF Core 8.x, `Microsoft.AspNetCore.Authentication.JwtBearer` 8.x, `Microsoft.AspNetCore.OpenApi` 8.x, Swashbuckle 6.5+, `Microsoft.IdentityModel.Tokens` cùng họ, `EFCore.BulkExtensions` 8.x. Không nâng gói thương mại (EvoPdf, Syncfusion, GemBox) nếu không cần.
- AC: build 0 lỗi; số cảnh báo không tăng so với baseline; app chạy.

### T05 – Bộ sinh mã mới + đồng bộ entity với 124 bảng · phụ thuộc T04 · `DONE` (2026-11-14)
- Tạo `tools/AppDelicare.CodeGen` (console .NET 8). Đọc `docs/reference/20261113_structure_db_appdelicare.sql` (parse `CREATE TABLE [dbo].[X]`, cột, kiểu, null, default). Sinh đúng **mẫu file hiện có**: Entity (kế thừa BaseEntity, `[Table("TênThật")]`), Request, SearchRequest, ViewModel, IRepository/Repository, I/Service, Controller, mục UnitOfWork, DbSet, cấu hình AutoMapper, SP `DB/sp/usp<Entity>_GetAll.sql` (CREATE OR ALTER, phân trang, sort, SearchText).
- Tên class: bỏ hậu tố legacy (`Dishe`→`Dish`…) theo bảng ở CODE_AUDIT mục C. Kiểu `decimal(p,s)` sinh kèm `[Precision]`.
- Cặp cột trùng: theo `docs/reference/DEPRECATED_COLUMNS.md`. Thêm `DeletedBy` vào BaseEntity, cấu hình EF bỏ cột không tồn tại theo từng bảng.
- Chạy cho các entity **hiện có**, so sánh diff:
  - Sửa sai lệch cột.
  - Gắn `[Table]`.
  - Bỏ `IdentityDbContext` nếu không dùng (xác minh trước).
  - Entity không có bảng (`AppConfig, Area, Branch, Department, Employees, Leads, Opportunity, Products, Region`) chuyển vào `_obsolete/` (kèm repository, service, controller liên quan) và bỏ khỏi DbContext. Không cần xét Angular (D-20).
- Ghi `tools/AppDelicare.CodeGen/README.md`: cách chạy và cờ `--only <Table>`, `--skip-existing`.
- AC: build xanh; test "mọi DbSet map tới bảng có trong DDL và mọi property map tới cột có thật" (đọc DDL trong test) xanh; không còn entity map tới bảng không tồn tại.

### T06 – Sinh mã cho 35 bảng còn thiếu · phụ thuộc T05 · `DONE` (2026-11-14)
- Dùng CodeGen sinh CRUD cho danh sách CODE_AUDIT A1(c), trừ 2 bảng `_Legacy_20261113` (chỉ sinh entity đọc).
- Với các sổ chỉ ghi thêm (`IntegrationLog`, `ApprovalDecision`, `TemperatureLog`, `PackagingLedger`, `AttendanceEvent`, `DeliveryEvent`, `ComplaintHistory`) **không sinh** Update/Delete (xem T08).
- AC: build xanh; script SP nằm trong `DB/sp/`; DbSet đủ 124 − 2 legacy (hoặc đủ 124 nếu có entity đọc).

### T07 – Phân quyền theo UserRole × ObjectMenu · phụ thuộc T05 · `DONE` (2026-11-14)
- Attribute `[Permission("<MetaLink>", PermissionAction.View|Add|Edit|Delete|Print|Export|Import)]` + `PermissionFilter`. Đọc UserRole theo UserGroup của user, cache 5′. `User.IsAdmin` bỏ qua kiểm tra.
- Gắn attribute cho mọi controller (CodeGen cập nhật template). MetaLink = `#/<screen-id>` theo `screen_id` của mockup (`docs/mockup/js/data/catalog.js`), đúng quy ước spec mục 1.2.
- Script seed `DB/seed/ObjectMenu_seed.sql` theo menu P0–P8 của mockup (`[DEMO]` không cần vì đây là dữ liệu cấu hình; ghi nguồn).
- **Seed cơ cấu tổ chức thật** theo `docs/claude/ORG_STRUCTURE.md`: `OrganizationUnit` (cây CEO → khối → bộ phận → tổ), `UserGroup` (mỗi vai trò), `UserRole` theo ma trận quyền mục 2 (PROPOSED, D-25). Các tổ bếp đồng thời seed `KitchenWorkstation`. Script idempotent theo Code / Name.
- Thay kiểm tra xóa bằng SQL động bằng `ReferenceGuard`: danh sách quan hệ logic (cột `XxxId` → bảng `Xxx`) sinh từ DDL; khi xóa mềm thì đếm tham chiếu bằng truy vấn có tham số.
- AC: test – user không có AllowAdd gọi Insert thì 403; admin thì 200; xóa bản ghi đang được tham chiếu thì 409; seed tổ chức chạy 2 lần không nhân bản; user nhóm OPS-SCM không duyệt được kế hoạch sản xuất.

### T08 – Khóa sổ chỉ ghi thêm · phụ thuộc T06 · `DONE` (2026-11-14)
- Trong `DatabaseContext`: các entity sổ (CLAUDE.md 3.3) có `EntityState.Modified` / `Deleted` thì ném `AppendOnlyViolationException`.
- Bỏ endpoint Update/Delete của các sổ. Thêm `InventoryLedgerService.Reverse(transactionId, reason)` ghi bút toán đảo.
- AC: test cập nhật / xóa sổ bị chặn; bút toán đảo làm tồn trở về đúng.

### T09 – Dự án test · phụ thuộc T02 · `DONE` (2026-11-14)
- `AppDelicare.Tests` (xUnit, FluentAssertions). Unit test cho Application. Integration test dùng DB dev (connection `ConnectionStrings:TestDb`), mỗi test chạy trong transaction rồi rollback. Không có DB thì skip có thông báo.
- AC: `dotnet test` chạy được trên máy anh; README test.

---

## GIAI ĐOẠN 2 – Nghiệp vụ lõi (Application)

### T10 – Khung AppDelicare.Application · phụ thuộc T05, T09 · `DONE` (2026-11-14)
- Project mới. Cấu trúc thư mục theo miền (ARCHITECTURE §1).
- Mẫu `Command` / `Query` / `Handler` / `Validator` (FluentValidation). `IClock` (giờ VN). `BusinessRulesOptions` (BUSINESS_RULES). Dispatcher sự kiện sau commit. `Result` chuẩn.
- Controller `/api/v1/...` mỏng, chỉ gọi handler.
- AC: 1 use case mẫu (`Common.LookupQuery`) chạy end-to-end và có test.

### T11 – Mã chứng từ · phụ thuộc T10 · `TODO`
- `IDocumentNumberService` theo BUSINESS_RULES §11, dùng `sp_getapplock` + MAX cột đích.
- AC: test chạy song song 50 lần không trùng mã; đúng định dạng cho mọi loại.

### T12 – ApprovalService · phụ thuộc T10, T11 · `TODO`
- Tạo / duyệt / từ chối / hết hạn. Nhiều bước theo `ApproverRoleCode`. `ApprovalDecision` chỉ ghi thêm, có `PayloadHash` chống sửa payload. Khi được duyệt thì thực thi lệnh gốc (registry `BusinessType` → handler).
- API: `GET /api/v1/approvals/my-pending`, `POST /api/v1/approvals/{id}/decide`.
- AC: test tạo → duyệt → thực thi; từ chối không ghi; payload bị sửa thì từ chối.

### T13 – Đơn hàng · phụ thuộc T12 · `TODO`
- Máy trạng thái Order. Submit / Confirm / Cancel. Kiểm tra giờ chốt 20h D-1. Kiểm tra lệch suất > 30%. Tính giá theo ContractMeal / PricePolicie.
- API theo spec mục P3 trong `DELICARE_API_Specification.md`.
- AC: test các nhánh cutoff và deviation; số tổng của đơn đúng.

### T14 – MRP và đề xuất mua · phụ thuộc T13 · `TODO`
- Tính MaterialRequirement từ đơn Locked × Recipe (đã khóa) × hao hụt, quy đổi đơn vị. Thiếu hụt so với tồn khả dụng và giữ chỗ. Sinh PurchaseRequest nháp, gợi ý NCC (SupplierMaterial).
- Khóa định mức: `Recipe.SnapshotJson` / `LockedAt` / `LockedBy`.
- AC: test số liệu với bộ dữ liệu mẫu nhỏ (tính tay được).

### T15 – Mua hàng, nhận hàng, QC đầu vào · phụ thuộc T14, T16 · `TODO`
- PR → PO (duyệt theo giá trị, ngưỡng PROPOSED trong cấu hình) → GoodsReceipt → QC (DAT / DAT_MOT_PHAN / KHONG_DAT) → MaterialLot `NL-…` → InventoryTransaction IN. SupplierIssue khi không đạt. TemperatureLog RECEIVING.
- AC: test lô KHONG_DAT không xuất được; DAT_MOT_PHAN chỉ nhập phần đạt.

### T16 – Sổ kho (InventoryLedgerService) · phụ thuộc T08, T11 · `TODO`
- Post IN / OUT / ADJUSTMENT / REVERSAL. Tính lại InventoryBalance. Gợi ý lô xuất theo `InventoryPolicie.UseFefo`: FEFO (mặc định) hoặc FIFO theo ngày nhận (D-24). Luôn chặn lô hết hạn, chặn lô KHONG_DAT; cảnh báo khi còn lô nên xuất trước. Kiểm kê: lệch > 2% thì duyệt. Giữ chỗ (MaterialReservation).
- AC: test FEFO, chặn, tồn sau chuỗi giao dịch, đảo bút toán.

### T17 – Sản xuất · phụ thuộc T14, T16 · `TODO`
- Kế hoạch ngược từ giờ xuất bếp (cutoff − 60′), chia mẻ theo ProductionLine, mã `ME-…`, cấp NL cho mẻ (MaterialIssue FEFO), sự kiện mẻ (bắt đầu / xong), nhiệt độ lõi ≥ 70°C (TemperatureLog CORE), lưu mẫu (FoodSafetyChecklistResult SAMPLE_RETAINED), sản lượng / hao hụt (ProductionOutput / MealWasteRecord).
- **Công đoạn theo tổ** (ORG_STRUCTURE.md): mỗi mẻ đi qua Sơ chế → Nấu → Cơm/Canh (nếu có) → Chia suất/Đóng gói; mỗi công đoạn ghi bắt đầu / kết thúc / người làm / tổ (`KitchenWorkstationId`) bằng `FoodSafetyChecklistResult` hoặc sự kiện mẻ có sẵn (chọn theo DB_SCHEMA_124.md, thiếu thì GAP). API tiến độ bếp theo tổ.
- AC: test lập kế hoạch đúng giờ; mẻ < 70°C bị chặn đóng gói; không chuyển công đoạn khi công đoạn trước chưa xong.

### T18 – ATTP và truy xuất · phụ thuộc T17 · `TODO`
- Checklist theo ca với hạn tương đối (BUSINESS_RULES §6). Seed checklist **kiểm soát thành phẩm** (QA 4.3): cảm quan, trọng lượng suất, lưu mẫu + người ký; checklist **5S / vệ sinh bếp** (QA 4.4). Truy xuất xuôi lô → mẻ → đơn / khách và ngược mẻ → lô.
- **Báo cáo NCR tổng hợp** (QA 4.5, GAP-QA-01 tạm): gom SupplierIssue, CustomerComplaint, các dòng checklist / nhiệt độ không đạt kèm CorrectiveAction, lọc theo kỳ / bộ phận.
- AC: test truy xuất 2 chiều trên dữ liệu mẫu.

### T19 – Giao vận và phản ánh · phụ thuộc T17 · `TODO`
- DeliveryRoute `CX-…`, DeliveryStop, DeliveryEvent, nhiệt độ ≥ 60°C, thiếu / thừa suất, ProofOfDeliverie. CustomerComplaint `PA-…` với SLA 4/24/72h.
- **Điểm phát suất (mô hình a, D-21)**: nhân viên điểm (nhóm OPS-POS) xác nhận nhận hàng, ghi **suất thực tế phục vụ** → `OrderReconciliation` (đặt / giao / thực tế / chênh lệch); báo cáo suất theo điểm, ngày, buổi.
- AC: test SLA và cảnh báo nhiệt độ.

### T20 – Hộp / khay và vệ sinh · phụ thuộc T19 · `TODO`
- PackagingLedger OUT / RETURN / LOSS, tỷ lệ thất thoát theo khách, sự kiện vượt ngưỡng. CleaningBatche `VS-…` 6 bước.
- AC: test tỷ lệ thất thoát; lô vệ sinh phải đủ 6 bước mới hoàn tất.

### T21 – Nhân sự · phụ thuộc T10 · `TODO` (T21.1 seed Shift: `DONE` 2026-11-14)
- Seed Shift (TRUA, TOI, DEM_23H, SANG_6H). ShiftAssignment, AttendanceEvent (GPS so với KitchenWorkstation, QR, máy, chấm hộ), LeaveRequest, ShiftSwapRequest (duyệt).
- AC: test hợp lệ theo bán kính; chấm hộ phải ghi rõ SourceChannel.

### T22 – KPI · phụ thuộc T13–T21 · `TODO`
- Seed KpiMetricDefinition (BUSINESS_RULES §12). Tính KpiDailySnapshot / KpiMonthlySnapshot. API dashboard theo spec mục 12.
- AC: test công thức cho từng KPI.

### T23 – Jobs (Hangfire) · phụ thuộc T13, T14 · `TODO` (R1: T23.1 = LockOrders 20:00, Mrp 20:30, PurchaseProposal 21:00, ApprovalExpiry, ComplaintSlaWatch; T23.2 = KpiDailySnapshot 22:00, RemindUnconfirmedOrders 19:00 – trong R1 chỉ tạo danh sách + thông báo trong app, chưa gửi Zalo/Telegram)
- Project `AppDelicare.Jobs`. Lịch theo AGENTS.md §2, múi giờ Asia/Ho_Chi_Minh. Storage: theo D-05 (chưa chốt thì InMemory). Mỗi lần chạy ghi `AnalyticsRefreshLog` (JobCode, RunId, Started / Completed, RunStatus, RowsProcessed).
- Dashboard Hangfire chỉ cho admin.
- AC: chạy tay từng job được; job idempotent (chạy 2 lần không nhân đôi dữ liệu).

---

## GIAI ĐOẠN 3 – Cổng công cụ (Tool Gateway)

### T30 – Project AppDelicare.ToolGateway · phụ thuộc T10 · `DEFERRED(R2)`
- ASP.NET Core host riêng, gói MCP chính thức cho C# (`ModelContextProtocol` + `ModelContextProtocol.AspNetCore`, transport HTTP/SSE). Tham chiếu Application.
- AC: một MCP client (vd. MCP Inspector) liệt kê được tool `ping`.

### T31 – Định danh agent · phụ thuộc T30, T02 · `DEFERRED(R2)`
- Seed User dịch vụ cho 12 agent + UserGroup `AGENT_<CODE>` + `BotConfiguration`. Xác thực bằng API key (hash lưu ngoài repo; `BotConfiguration.SecretReference` chỉ chứa tên secret).
- AC: key sai thì 401; agent bị DISABLED thì 403.

### T32 – Công cụ đọc · phụ thuộc T31, các query ở giai đoạn 2 · `DEFERRED(R2)`
- Đăng ký 26 tool đọc theo tên hàm cũ (LEGACY_GATEWAY.md) + tool đọc mới theo AGENTS.md. Kiểm quyền theo agent và vai trò. Giới hạn số dòng. Che dữ liệu cá nhân.
- AC: test mỗi tool trả JSON đúng schema; agent gọi tool ngoài danh sách thì bị từ chối.

### T33 – Công cụ ghi 2 pha · phụ thuộc T12, T32 · `DEFERRED(R2)`
- `propose_*` → ApprovalRequest(AGENT_WRITE) → xác nhận (Telegram callback / Mission Control / tool `confirm` của người dùng thật) → `commit` idempotent. Hết hạn 15′.
- AC: test propose không ghi dữ liệu nghiệp vụ; commit 2 lần chỉ ghi 1 lần; quá hạn thì từ chối.

### T34 – Log và an toàn · phụ thuộc T32 · `DEFERRED(R2)`
- IntegrationLog cho mọi lệnh. Giới hạn tần suất theo agent. Công tắc chỉ đọc toàn cục. API xem log cho Mission Control.
- AC: mọi lệnh có log với CorrelationId; vượt giới hạn tần suất thì 429.

### T35 – POST /api/gateway tương thích · phụ thuộc T32, T33 · `DEFERRED(R2)` – **tùy chọn**, chỉ làm nếu anh yêu cầu (D-16)
- Nhận `{fn, args, telegram_id}`, luôn HTTP 200 `{ok,…}`, mã lỗi như cũ, ánh xạ 34 hàm. Phiếu: `kiem_tra_phieu` / `tao_phieu` / `xac_nhan_phieu` / `huy_phieu` / `duyet_phieu` dùng cột tiếng Việt cũ.
- AC: bộ test hợp đồng: 34 fn × (thành công, không quyền, tham số sai).

---

## GIAI ĐOẠN 4 – Kênh, Mission Control, agent

### T40 – Telegram · phụ thuộc T33 · `DEFERRED(R2)`
- Webhook theo botCode, trả 200 ngay, lưu InboundMessage, xử lý bất đồng bộ. OutboundMessage worker (retry, idempotent). Nút inline xác nhận / từ chối cho AGENT_WRITE và Approval. Liên kết `UserExternalIdentity`.
- AC: test webhook giả lập; xác nhận bằng callback chạy đúng commit.

### T41 – Zalo OA · phụ thuộc T40 · `DEFERRED(R2)` (còn chờ D-06)
- Webhook, gửi tin, các lệnh của khách (AGENTS.md CSKH).

### T42 – Angular: dọn template và khung DELICARE · phụ thuộc T05 · `DEFERRED(FE)`
- Ẩn hoặc gỡ route demo của template w3crm (pages/*) khỏi menu. Sidebar sinh từ ObjectMenu theo quyền. Đặt màu thương hiệu #91AF42 / #F07825 (theo mockup). Interceptor xử lý lỗi chuẩn (T03).
- AC: `npm run build` xanh; menu hiển thị theo quyền.

### T43 – Cấu hình agent · phụ thuộc T32 · `DEFERRED(R2)`
- Thư mục `agents/` (AGENTS.md §4): 12 file mô tả agent + `hermes.config.example.yaml` trỏ tới MCP của ToolGateway.
- AC: anh nạp được vào Hermes theo README trong `agents/`.

### T44 – Mission Control · `DEFERRED(FE)` (UI). Phần backend (API hộp duyệt, cảnh báo) đã nằm trong T12 / T46B / T47B
- Module Angular `/mission-control`: hộp duyệt, hàng chờ xác nhận lệnh agent, dòng hoạt động agent, trạng thái job, KPI hôm nay, cảnh báo.
- AC: duyệt / từ chối được từ UI; dữ liệu khớp API.

### T45 – Màn hình nghiệp vụ P0–P2 · `DEFERRED(FE)`
- Theo mockup và spec: quản trị, danh mục (khách, điểm giao, NCC, nguyên liệu, món, công thức, đơn vị…). Dùng mẫu module supplier. Có thể sinh khung bằng CodeGen (thêm template Angular).
- AC: CRUD chạy với API thật cho từng màn hình trong danh sách P0–P2 của `catalog.js`.

### T46 – Màn hình nghiệp vụ P3–P5 · `DEFERRED(FE)` (ghi chú bố cục bên dưới dùng lại cho T46B)
- Hợp đồng, thực đơn, đơn hàng, mua hàng, kho, kế hoạch sản xuất, giá thành.
- **Bám bố cục file thật** (BUSINESS_DOCS.md): thực đơn tuần theo buổi × lựa chọn có giá vốn / FC; nhập suất theo khách → địa điểm → vị trí; so sánh báo giá → PO.
- **Mẫu in** (GemBox Document / Excel export) theo file thật: Thực đơn gửi khách, Phiếu trước sản xuất, Phiếu sản xuất, Đơn đặt hàng NCC. Header pháp nhân lấy từ cấu hình.

### T47 – Màn hình nghiệp vụ P6–P8 · `DEFERRED(FE)`
- QC / ATTP, đóng gói / giao vận / phản ánh / hộp, nhân sự, KPI dashboard.


### T45B – API màn hình P0–P2 (quản trị và danh mục) · phụ thuộc T07, T10 · `DOING`
- Tách: **T45B.1** CRUD chuẩn generic cho 39 resource (`DONE` 2026-11-14) · **T45B.2** P0 riêng: users (tạo kèm Person + nhóm, lock/unlock, reset), ma trận quyền nhóm (GET/PUT/bulk/preview, effective-permissions), object-menus tree/reorder, org-units tree / branches, merge-conflicts (Id bigint), `/meta/menu`, `/meta/me`, `/meta/statuses`, `/meta/reference-lists` · **T45B.3** P1 riêng: customer overview / list mở rộng, supplier overview, supplier-materials ưu tiên + effectiveOn, ingredients kèm tồn, warehouse layout, UomService.Convert, delivery-location chặn ngừng khi còn đơn · **T45B.4** P2 riêng: dishes kèm activeRecipe, recipes phiên bản (tạo / dòng / submit / lock / cost), standard-costs recalculate, price-policies resolve + chặn chồng hiệu lực, checklist UNIQUE (Code, VersionNo) · **T45B.5** `docs/frontend-handoff/P0-P2.md` + `.http'
- Với **mỗi màn hình P0–P2** trong `catalog.js`: đủ endpoint theo `DELICARE_API_Specification.md`, gồm danh sách (lọc / phân trang / sắp xếp), chi tiết, thêm / sửa / xóa mềm, dropdown lookup, import / export nếu spec có.
- Quan hệ logic kiểm bằng `LOGICAL_RELATIONS.md`. Phân quyền `[Permission]`. Swagger mô tả.
- Toàn bộ theo `/api/v1/...` và quy ước spec. Endpoint CRUD cũ tương ứng có thể gỡ khi bản v1 đã đủ và có test.
- AC: bảng đối chiếu `docs/frontend-handoff/P0-P2.md` (màn hình → endpoint → trạng thái); test tích hợp cho mỗi nhóm; file `.http`.

### T46B – API màn hình P3–P5 · phụ thuộc T13–T17 · `TODO`
- T46B.1 P3 (hợp đồng, thực đơn tuần buổi × lựa chọn + giá vốn / FC, đơn hàng theo khách → địa điểm → vị trí). T46B.2 P4 (mua hàng: báo giá → so sánh → PO; kho). T46B.3 P5 (kế hoạch sản xuất, giá thành).
- **API in / xuất file** theo mẫu thật (BUSINESS_DOCS.md), dùng GemBox / Excel export có sẵn: Thực đơn gửi khách, Phiếu trước sản xuất, Phiếu sản xuất, Đơn đặt hàng NCC. Header pháp nhân lấy từ cấu hình. Trả file (docx / xlsx / pdf).
- AC: như T45B cho P3–P5; mỗi mẫu in có test sinh file không lỗi.

### T47B – API màn hình P6–P8 · phụ thuộc T18–T22 · `TODO`
- T47B.1 P6 (QC / ATTP, checklist, nhiệt độ, truy xuất). T47B.2 P7 (đóng gói, giao vận, POD, phản ánh, hộp / khay, vệ sinh). T47B.3 P8 (nhân sự, KPI, **API dashboard điều hành**, hộp duyệt ApprovalRequest của user, cảnh báo nghiệp vụ).
- AC: như T45B cho P6–P8.

### T48 – Triển khai (staging → production) · phụ thuộc T03 · `TODO`
- **T48.1 staging (làm ngay trong R1.0):** `DONE` (2026-11-14 – IIS / LocalDB chạy thử OK; Docker chưa chạy được vì máy dev chưa có Docker)
  - Cấu hình môi trường `Development` / `Staging` / `Production` (appsettings theo môi trường, bí mật qua biến môi trường).
  - **D-15 chưa chốt → không được chặn.** Chuẩn bị cả hai cách ở mức tối thiểu, không phụ thuộc hạ tầng cụ thể:
    - (a) **IIS:** `dotnet publish -c Release`, web.config, hướng dẫn cài .NET 8 Hosting Bundle.
    - (b) **Docker:** Dockerfile cho API + `docker-compose.staging.yml` (API + SQL Server container cho staging thử nghiệm). **Không** đưa Angular vào.
  - Staging đầu tiên có thể chạy ngay trên máy anh bằng docker-compose hoặc IIS Express.
  - Health check `/health` (API + DB). Script SQL triển khai: chạy `DB/sp/*.sql` và `DB/seed/*.sql` theo thứ tự, idempotent.
  - CI (nếu repo có remote GitHub / GitLab / Azure DevOps): pipeline build + test cho mỗi PR. Không tự deploy production.
  - Viết `deploy/README.md`: từng bước anh (hoặc IT) làm theo.
- **T48.2 production (R1.7):**
  - Checklist go-live: backup DB, chạy script, cấu hình HTTPS, CORS đúng domain, tạo tài khoản admin, phân quyền nhóm, theo dõi log.
  - Phương án quay lui.
- AC: staging backend chạy được từ máy sạch theo README; `/health` xanh; Swagger staging mở được; đăng nhập qua API và gọi được API danh mục.

### T49 – Nạp dữ liệu danh mục ban đầu · phụ thuộc T45B · `TODO`
- **API** (không làm màn hình) **Import Excel** cho danh mục: khách, điểm giao, NCC, nguyên liệu, món, công thức / định mức, đơn vị, kho, nhân viên, xe. Dùng `ImportService` / `TemplateExcelHelper` sẵn có. Mỗi danh mục có một file mẫu.
- Cột của file mẫu theo bảng `dbo` đích, đặt tên tiếng Việt dễ hiểu. **Ưu tiên tương thích file thật của anh** (BUSINESS_DOCS.md): import trực tiếp sheet "Ngân hàng món" và "Chi tiết món và hao hụt, giá" của `Rule Menu.xlsx` → DishCategorie / Dishe / Ingredient / Recipe / RecipeLine; bảng giá NCC từ `Tool_Tong hop bao gia` → Supplier / SupplierMaterial. Người dùng tự chạy import; Claude Code chỉ test bằng file mẫu tự tạo `[DEMO]`, không nạp dữ liệu thật.
- Kiểm tra trùng mã, báo lỗi từng dòng, idempotent theo mã.
- AC: nạp file mẫu thành công; nạp lại không trùng; dòng lỗi được báo rõ.

### T50R1 – Tài liệu API và UAT backend · song song các lát · `TODO`
- `docs/claude/UAT_CHECKLIST.md`: mỗi lát có kịch bản thử **bằng API** (vai trò / token, request theo thứ tự, kết quả mong đợi), lấy từ luồng trong `DELICARE_Business_Workflow.md`.
- `docs/api-tests/*.http` (hoặc Postman collection): chạy được từng luồng end-to-end.
- `docs/frontend-handoff/`: mỗi màn hình mockup ↔ danh sách endpoint, input / output mẫu, để team Angular dùng khi anh giao frontend.
- AC: mỗi lát R1 có checklist UAT + file `.http` trước khi báo anh.

---

## GIAI ĐOẠN 5 – Second Brain

### T50 – Obsidian exporter · phụ thuộc T22 · `DEFERRED(R2)`
- Trong `AppDelicare.Integration`: ghi markdown vào `Obsidian:VaultPath` theo cấu trúc số (ARCHITECTURE §4). Template trong `templates/obsidian/`. Chạy hằng đêm và theo sự kiện. Không bao giờ ghi vào `BOD Bảo mật`.
- AC: chạy với vault thử tạo đúng thư mục / file, có frontmatter; chạy lại không nhân bản file.

### T51 – Vùng ghi của agent · phụ thuộc T50, T33 · `DEFERRED(R2)`
- Tool `vault_append_log` (96), `vault_save_interaction` (97), `vault_create_task` (98). Ghi qua lõi, có log. Tool đọc `vault_search` loại trừ BOD.
- AC: test agent không ghi được ngoài 96 / 97 / 98.

---

## GIAI ĐOẠN 6 – Chuyển dữ liệu và cắt chuyển

### T60 – Bảng ánh xạ cấp cột · phụ thuộc T06 · `DEFERRED(R2)`
- Hoàn thiện MIGRATION_SHEETS_TO_DBO.md tới cấp cột (Sheets col → dbo table.col, chuyển kiểu, tra mã). Ghi mọi GAP vào DECISIONS.
- AC: mọi cột của 42 sổ có đích hoặc lý do bỏ.

### T61 – Importer · phụ thuộc T60 · `DEFERRED(R2)`
- Console `tools/AppDelicare.Importer`: đọc thư mục sao lưu xlsx / csv, nạp idempotent, báo cáo số dòng / lỗi, ghi IntegrationLog. Chạy được hằng đêm.
- AC: nạp 2 lần cùng bộ dữ liệu không sinh trùng; báo cáo lệch.

### T62 – Đối soát hằng ngày · `CANCELLED (D-16: Sheets là demo)`
- So KPI ngày SQL với `KPI_Ngay` Sheets, xuất báo cáo khác biệt.

### T63 – Kế hoạch cắt chuyển · `CANCELLED (D-16)`
- Checklist đổi URL Hermes và webhook bot, chuyển Sheets sang chỉ đọc, phương án quay lui. Viết `docs/claude/CUTOVER.md`.
