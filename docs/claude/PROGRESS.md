# PROGRESS – Nhật ký tiến độ (Claude Code cập nhật sau mỗi task)

## Trạng thái hiện tại
- **2026-11-17: BE2.1 DONE** trên nhánh `feat/BE2.1-flow-gaps` (worktree `wt-BE21`, từ `release/pilot`, chưa push / chưa gộp): đóng gói từ mẻ, xếp hàng lên xe (bắt buộc trước khi xuất phát), đơn giao tay / tách / gán – chuyển chuyến / hủy, lập chuyến nhiều xe theo khu vực, thực hiện giao, kế hoạch sinh theo ca (1 KH- / ngày), đề nghị mua chọn dòng + dự phòng, nhiệt độ tự do, hao hụt, bảng đơn, danh sách thực đơn / phản ánh, chi tiết + hủy yêu cầu duyệt, truy xuất 2 chiều. `dotnet test` **185/185**; `be2.1-flow-gaps.http` 138/138 + `pilot-flow.http` 121/121 chạy thật trên DB dev. PROPOSED chờ anh: DECISIONS **D-50 (a)–(h)**. Không SP / seed mới. Kế tiếp: T48.3 (ROADMAP) → BE2.2. Chi tiết: mục BE2.1 cuối file.
- **2026-11-16 (D-44): KHÔNG CHẠY PILOT – hoàn thiện toàn bộ luồng R1.** Mọi mục DEFERRED(PILOT) vào BE-2. Để sau khi xong luồng: bot, Hermes + quản lý agent, dashboard / KPI, báo cáo.
- **2026-11-16 (D-43): ĐỢT BE-2 – hoàn thiện toàn bộ API backend** (trừ bot, dashboard / KPI, báo cáo). Task kế tiếp: **BE2.1** (lấp chỗ thiếu luồng Pilot: đóng gói, xếp xe, đơn giao, lập chuyến nhiều xe…) → BE2.2 → … → BE2.9 (ROADMAP mục ★★★). FE của Claude Code tạm dừng; bạn frontend dùng `docs/frontend-handoff/`. Catalog đã cập nhật 2026-11-16: 124/234 xong, Pilot 114/140.
- **2026-11-17: BACKEND PILOT XONG (P0 → P9) – dừng, chờ anh UAT-PILOT** (ROADMAP P9 `DONE(PILOT)`, mục T50R1 + T48.1 cuối file). Luồng P3 → P8 chạy thật bằng API trên DB dev (`docs/api-tests/pilot-flow.http`, 117 request / 8 chốt an toàn đúng) + test end-to-end qua HTTP (`Pilot/PilotFlowSqlServerTests`). Staging (LocalDB `AppDelicare_Staging`, `http://localhost:5200`) đã cập nhật backend Pilot + bản build Angular.
- Nhánh: `feat/T50R1-pilot-flow` (từ `release/pilot` = T11 … T19 + T17.1 đã gộp, D-41). Chưa push / chưa gộp `main` – chờ anh duyệt.
- Build: `dotnet build --no-incremental` 0 lỗi, 10 cảnh báo; `dotnet test` **173/173** (baseline 169 + 4); `npm run build` 0 lỗi.
- **Tiếp theo (sau khi anh UAT-PILOT backend):** đợt FE theo D-40: FE-3 (phần còn lại) → FE của T45R.8 → FE-P2 → FE-P3 → FE-P4 → FE-P5 → FE-P7 → FE-P8 → UAT-PILOT trên giao diện (UAT_CHECKLIST mục PILOT-FE).
- Khung chuẩn = code cũ (D-30), không sinh `/api/v1`. Tiến độ API theo `docs/reference/DELICARE_API_Catalog.xlsx` **chưa cập nhật** cho T11 … T19 / T50R1 (máy không có python / openpyxl) – gợi ý trạng thái: mục T50R1 bên dưới + gợi ý cuối từng mục T13 … T19.
- Việc anh cần làm: (1) UAT-PILOT theo `docs/claude/UAT_CHECKLIST.md` mục PILOT trên staging; (2) duyệt / commit nhánh `feat/T50R1-pilot-flow`, rồi duyệt gộp `release/pilot` → `main`; (3) chốt các điểm PROPOSED của Pilot (mục T50R1 "Câu hỏi còn mở"); (4) IIS thật (deploy/README.md mục 4, cần quyền admin) nếu chọn IIS (D-15); (5) cập nhật / cho phép cài openpyxl để cập nhật Catalog; (6) các việc cũ: chạy T4 trong Visual Studio (T45R.6), D-26, D-27, đổi khóa đã lộ (SECRETS_TODO.md), nhánh `origin/develop`.
- Lịch sử trạng thái trước 2026-11-17 (R1.0 / R1.1, D-34, D-40) xem các mục nhật ký bên dưới.

---

## Nhật ký
<!-- Mẫu cho mỗi task:
### T05 – Bộ sinh mã mới · 2026-11-15 · DONE
Kế hoạch: 1) ... 2) ...
Đã làm: ...
File chính: ...
Kiểm tra: dotnet build OK (0 lỗi, N cảnh báo), dotnet test 12/12
AC: [x] ... [x] ... 
Còn lại / GAP: ...
Cần anh: ...
-->

### T00 – Khởi tạo môi trường làm việc · 2026-11-14 · DONE
Kế hoạch: 1) kiểm tra git 2) cài `.claude/` 3) tạo nhánh + commit tài liệu 4) build baseline 5) so DB dev với DDL 6) xuất SP 7) thử đăng nhập API 8) liệt kê vị trí bí mật.

**Git:** trước khi làm: nhánh `main` (1 commit `2501aef Initial commit`), remote `origin` = GitHub `ngocpv90/Delicare-ai`. Thay đổi chưa commit lúc đó: `.gitignore` (sửa), `CLAUDE.md`, `DB/`, `docs/` (mới) – anh xác nhận là bộ tài liệu cho T00 → đã commit vào `chore/T00-claude-setup`. `docs/claude/LOCAL_DEV.local.md` và `backup-db/` được `.gitignore` bỏ qua (đã kiểm tra bằng `git check-ignore`).

**Lệnh tắt:** `.claude/commands/` (next, task, status, verify, gap) + `.claude/settings.json`. Em **bỏ** 2 quyền `npm run build` / `npm ci` khỏi bản sao vì trái D-20 (bản gốc trong `docs/claude/claude-config/` giữ nguyên).

**Build baseline:**
- SDK: .NET 10.0.401 (có runtime ASP.NET Core 8.0.31 → chạy được app net8.0).
- `dotnet build AppDelicare/AppDelicare.sln`: **0 lỗi, 10 cảnh báo**: NU1903 × 3 loại (AutoMapper 13.0.1 lỗ hổng mức cao – A17), CS8604 (null có thể truyền vào `List<Claim>.Add` / `List<string>.Add`).
- Solution gồm 5 project: WebApi, Data, Model, Services, Generator.

**DB dev:** connection string trong `appsettings.json` trỏ `(localdb)\MSSQLLocalDB`, DB `AppDelicare`, Windows Auth (không phải `.\SQLEXPRESS`; instance SQLEXPRESS đang chạy nhưng không có DB này).
- **124 bảng `dbo` khớp 100% DDL 20261113**: tên bảng, tên cột, kiểu dữ liệu, NULL/NOT NULL (2264 cột). Khác biệt duy nhất: `CoreDataMergeConflict.EntityName/FieldName` DDL ghi `sysname`, DB báo `nvarchar(128)` – tương đương. Thêm bảng hệ thống `sysdiagrams` (SSMS). Không có FK vật lý, không có view. → **D-14 đã rõ, không cần chạy script nâng cấp.**
- 35 SP + 1 function. Đã xuất (chỉ đọc `OBJECT_DEFINITION`) dạng `CREATE OR ALTER`:
  - `DB/sp/` – 28 SP nghiệp vụ / hệ thống (`usp*`).
  - `DB/_ssms_diagrams/` – 7 SP `sp_*diagram*` + `fn_diagramobjects` (đồ của SSMS, không dùng).
- **SP code gọi mà DB thiếu: 74** → `DB/sp/_MISSING_FROM_DB.md` (A15). Ví dụ `api/Customer/GetAll` trả 500.
- SP có trong DB nhưng code không gọi: `uspSystem_BackupDB` – thực chất là RESTORE ghi đè DB (A16).

**Đăng nhập API:** chạy WebApi (Development, http://localhost:5125). `POST api/Authentication/Login` với tài khoản dev admin → 200, nhận JWT (hạn 7200 s, `isAdmin=true`, kèm `listMenu`). `GET api/UnitMeasure/GetAll` không token → 401; có token → 200 (`totalRecords=0`). Đã tắt WebApi sau khi thử.

**Vị trí bí mật trong code (chỉ vị trí, không ghi giá trị) – xử lý ở T01:**
| File:dòng | Loại |
|---|---|
| `AppDelicare/ClinicManagement.WebApi/Infrastructure/ConfigureServices.cs:11` | Khóa ký JWT (`SecretKey`) viết cứng |
| `AppDelicare/ClinicManagement.WebApi/Startup.cs:20` | ApplicationInsights QuickPulse `AuthenticationApiKey` |
| `AppDelicare/ClinicManagement.WebApi/Startup.cs:98` | License GemBox (`ComponentInfo.SetLicense`) |
| `AppDelicare/ClinicManagement.WebApi/appsettings.json:37` | Connection string (đã comment) có user `sa` + mật khẩu |
| `AppDelicare/ClinicManagement.Generator/App.config:4` | Connection string có user id + mật khẩu |
| WebApi `.csproj` | Chưa có `UserSecretsId` → T01 cần `dotnet user-secrets init` |

Các khóa trên đã nằm trong lịch sử git (commit `2501aef`) → khi làm T01 anh cần **sinh khóa mới / đổi mật khẩu**, không chỉ xóa khỏi code.

**Phát hiện thêm (ghi CODE_AUDIT):** A14 `GET api/AuditChange/DeleteData` xóa vật lý toàn DB (rất cao, D-26); A15 thiếu 74 SP; A16 `uspSystem_BackupDB`; A17 AutoMapper lỗ hổng.

File chính: `.claude/*`, `DB/sp/*.sql`, `DB/sp/_MISSING_FROM_DB.md`, `DB/_ssms_diagrams/*`, `DB/README.md`, `docs/claude/{PROGRESS,ROADMAP,DECISIONS,CODE_AUDIT}.md`.
Kiểm tra: `dotnet build` 0 lỗi; đăng nhập + gọi API bảo vệ OK; `git status` không có thay đổi trong `app-delicare-client/`.
AC: [x] nhánh + commit · [x] build baseline · [x] báo cáo lệch DB · [x] DB/sp + danh sách SP thiếu · [x] đăng nhập API · [x] danh sách vị trí bí mật.
Còn lại / GAP: không có GAP dữ liệu mới.
Cần anh: D-26; merge nhánh khi thấy ổn; chuẩn bị đổi khóa JWT / license / mật khẩu `sa` cho T01.

### T01 – Đưa bí mật ra khỏi code · 2026-11-14 · DONE
Kế hoạch: 1) tìm đủ vị trí bí mật 2) bật user-secrets, đặt khóa dev 3) đọc từ `IConfiguration` 4) kiểm tra bí mật lúc khởi động 5) placeholder + `.example` 6) `RequireExpirationTime`, thời hạn từ cấu hình 7) SECRETS_TODO 8) build + chạy thử.

Đã làm:
- `Infrastructure/StartupSecrets.cs` (mới): tên khóa cấu hình, `Validate()` dừng app và liệt kê đủ khóa thiếu / sai (thiếu `Jwt:SigningKey`, khóa < 32 byte, thiếu `ConnectionStrings:MainConnectionString`, `Jwt:AccessTokenMinutes` không hợp lệ) kèm cách đặt.
- `ConfigureServices.InitAuthentication(services, configuration)`: khóa ký lấy từ `Jwt:SigningKey`; `RequireExpirationTime = true`; `JwtIssuerOptions.ValidFor` lấy từ `Jwt:AccessTokenMinutes` (mặc định 120). Bỏ `ServiceLocator` trong hàm này.
- `Startup`: gọi `StartupSecrets.Validate` đầu tiên; QuickPulse key chỉ cấu hình khi có `ApplicationInsights:QuickPulseApiKey`; license GemBox từ `Licenses:GemBox`, trống thì dùng `FREE-LIMITED-KEY` và log cảnh báo.
- `JwtIssuerOptions`: xóa property `Secret` viết cứng (không nơi nào dùng). **T00 đã bỏ sót vị trí này**, T01 phát hiện khi quét rộng hơn.
- `ConfigurationCommon.Initialize(IConfiguration)`: trước đây lớp này chỉ đọc file `appsettings.json`, nên `DapperRepository` / `UnitOfWork` sẽ mất connection string khi chuyển sang user-secrets. Nay dùng cấu hình của host.
- `appsettings.json`: bỏ connection string (kể cả dòng comment có mật khẩu `sa`), thêm placeholder rỗng `Jwt`, `Licenses`, `ApplicationInsights`. Thêm `appsettings.Development.json.example`.
- `Generator/App.config`: connection string có user / mật khẩu → LocalDB + Windows Auth.
- `dotnet user-secrets init` (WebApi). Trên máy anh đã đặt: `Jwt:SigningKey` (**khóa ngẫu nhiên MỚI**, không dùng lại khóa cũ), `ConnectionStrings:MainConnectionString` (LocalDB), `Licenses:GemBox` (chép license hiện có).
- `docs/claude/SECRETS_TODO.md`: 6 mục anh cần đổi / thu hồi, cách đặt cho dev / staging / prod.

Kiểm tra:
- `dotnet build`: 0 lỗi, 10 cảnh báo (bằng mốc T00).
- Grep toàn repo (trừ `.git`, `bin`, `obj`, `.vs`, `backup-db`), kể cả `app-delicare-client/`: **0 file** còn chứa 6 chuỗi bí mật cũ.
- Chạy Development với user-secrets: login 200; token có `exp`, hạn 120 phút; `UnitMeasure/GetAll` không token 401, có token 200; `User/GetAll` (Dapper) 200 có dữ liệu; token sửa chữ ký 401.
- Thiếu / sai bí mật (`--ConnectionStrings:MainConnectionString=` `--Jwt:SigningKey=short`): app dừng, thông báo liệt kê 2 lỗi + cách đặt. Chạy Production thật (`--no-launch-profile`, không nạp user-secrets): app dừng, báo thiếu 2 khóa.
- Lưu ý: `dotnet run` dùng launch profile sẽ ép `ASPNETCORE_ENVIRONMENT=Development`. Muốn thử Production phải thêm `--no-launch-profile`.
- `git status`: không có thay đổi trong `app-delicare-client/`.

AC: [x] grep repo không còn chuỗi bí mật cũ · [x] app chạy được khi secret nằm trong user-secrets · [x] thiếu secret thì báo lỗi rõ lúc khởi động · [x] `RequireExpirationTime = true`, thời hạn từ cấu hình · [x] `.example` + SECRETS_TODO.
Còn lại: chưa có test tự động cho `StartupSecrets.Validate` (chưa có project test; thêm khi làm T09). `ServiceLocator.BuildServiceProvider` trong Startup để T03.
Cần anh: đổi khóa theo SECRETS_TODO.md (bắt buộc: khóa JWT staging / prod, mật khẩu `sa`, thu hồi QuickPulse key). Máy dev khác muốn chạy thì phải tự đặt user-secrets.

### T04 – Nâng gói lên .NET 8 đồng bộ · 2026-11-14 · DONE
Kế hoạch: 1) `dotnet list package --vulnerable` 2) tra bản 8.x mới nhất và bản vá 3) nâng gói chính 4) sửa lỗi build 5) ghim gói gián tiếp có lỗ hổng 6) giữ số cảnh báo ≤ mốc 7) chạy thử app.

Đã làm (gói thương mại EvoPdf, Syncfusion, GemBox **không đụng**):
| Gói | Cũ → Mới | Project |
|---|---|---|
| Microsoft.EntityFrameworkCore.SqlServer / Tools | 7.0.3 → 8.0.34 | WebApi |
| Microsoft.AspNetCore.Authentication.JwtBearer / OpenApi | 7.0.3 → 8.0.34 | WebApi |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 7.0.3 → 8.0.34 | Data |
| EFCore.BulkExtensions 7.0.1 → **EFCore.BulkExtensions.SqlServer** 8.1.3 | gói chỉ SQL Server, bỏ được Npgsql / MySqlConnector có lỗ hổng | Data |
| Microsoft.IdentityModel.Tokens | 6.27.0 → 7.7.3 (khớp bản JwtBearer 8 dùng, tránh trộn 7 / 8) | Model, WebApi |
| Swashbuckle.AspNetCore | 6.4.0 → 6.9.0 | WebApi |
| Serilog.AspNetCore | 6.1.0 → 8.0.3 | tất cả |
| Microsoft.Extensions.* | 7.0.x → 8.0.x | Model, WebApi |
| System.Data.SqlClient (gián tiếp, đã ngừng phát triển) → **Microsoft.Data.SqlClient 6.0.1** | đổi `using` ở `DapperRepository`, `UnitOfWork` | Data |
| AutoMapper.Extensions.Microsoft.DependencyInjection 12.0.0 → bỏ; Services dùng thẳng AutoMapper 13.0.1; Model bỏ hẳn (không dùng) | Model, Services |
| Ghim gói gián tiếp đã vá: System.Formats.Asn1 8.0.2, System.IO.Packaging 8.0.1, System.Net.Http 4.3.4, System.Security.Cryptography.Pkcs 8.0.2, System.Text.RegularExpressions 4.3.1 | Model |

- `Program.cs`: `UseSerilog` chuyển từ `IWebHostBuilder` (bị bỏ ở Serilog.AspNetCore 8) sang `IHostBuilder`. Cấu hình đọc từ `Serilog` trong appsettings vẫn như cũ.
- AutoMapper 13.0.1: **giữ**, vì bản vá 15.1.4+ là license thương mại → D-27. `NuGetAuditSuppress` 3 advisory **chỉ ở Services** để không báo trùng; cảnh báo vẫn hiện ở WebApi.

Kiểm tra:
- `dotnet build --no-incremental`: **0 lỗi, 10 cảnh báo** (= mốc T00: 6 dòng NU1903 AutoMapper ở WebApi + 4 dòng CS8604).
- `dotnet list package --vulnerable --include-transitive`: Data, Model **sạch**; WebApi / Services chỉ còn AutoMapper (D-27). Trước T04 có khoảng 15 gói có lỗ hổng.
- Chạy app: Swagger JSON 200 (572 route), UI 200; login 200; không token 401; `User/GetAll` (Dapper + Microsoft.Data.SqlClient) 200; `User/GetById` (EF Core 8) 200; `UnitMeasure/GetAllData`, `ObjectMenu/GetAll` 200; log không có Error / Exception.
- Chưa thử được: `DataTableBulkInsert` / `DataReaderBulkInsert` (SqlBulkCopy, chưa có màn hình gọi) và các thao tác ghi. Sẽ có test khi làm T09.
- `git status`: không có thay đổi trong `app-delicare-client/`.

AC: [x] build 0 lỗi · [x] số cảnh báo không tăng (10 = 10) · [x] app chạy.
Còn lại: D-27 AutoMapper. `Microsoft.EntityFrameworkCore.SqlServer` 8.0.34 kéo Microsoft.Data.SqlClient 5.1.x, còn Data dùng 6.0.1 → NuGet chọn 6.0.1 cho cả app (đã chạy thử OK).
Ghi chú: `AppDelicare/.vscode/` chưa được theo dõi, không phải em tạo, em không đụng. `.gitignore` gốc chỉ bỏ qua `.vscode/` ở thư mục gốc.

### T02 – Định danh người thao tác (ICurrentActor) · 2026-11-14 · DONE
Đã làm:
- `Data/Share/CurrentActor.cs` (mới): `ActorType` (User / System / Agent), `ActorInfo`, `ICurrentActor` (Actor, CorrelationId, `RequireActor()`, `BeginScope()` cho job / agent / bước đăng nhập), `UnauthorizedActorException`. HTTP lấy claim `id`; claim sai thì coi như không có người thao tác.
- `AuthenticatedService`: **bỏ fallback "user đầu tiên"**; `UserInfo` dùng truy vấn có tham số `@Id` (hết ghép chuỗi SQL).
- `DatabaseContext`: dùng `ICurrentActor`; có thay đổi mà không có người thao tác thì ném `UnauthorizedActorException`. Chặn ở **cả** `SaveChangesAsync(typeSave)` lẫn `SaveChanges()` / `SaveChangesAsync()` thường (trước đây 2 đường này bỏ qua audit). Audit ghi người thao tác + CorrelationId ở đầu cột `Changes` (GAP-AUD-01).
- `LoginService`: ghi `LocationTracking` trong `BeginScope(ActorInfo.ForUser(id))`.
- `CorrelationIdMiddleware` (đầu pipeline): header `X-Correlation-Id` hợp lệ (`^[A-Za-z0-9._-]{1,64}$`) hoặc tự sinh; trả lại ở response header; đẩy vào Serilog LogContext. Template log thêm `[{CorrelationId}]`, bật `Enrich: FromLogContext`.
- `appsettings`: `SystemUsers:JobUserId` (GAP-AUD-02). `HttpContextAccessor` đăng ký SingleInstance (đúng chuẩn ASP.NET Core).
- Project **`AppDelicare.Tests`** (xUnit, FluentAssertions 7.2.0 – bản 8 là license trả phí, EF InMemory) – nền cho T09.

Kiểm tra:
- `dotnet build`: 0 lỗi, 10 cảnh báo. `dotnet test`: **13/13 xanh**: chặn ghi khi không có người thao tác (3 đường SaveChanges); có token thì CreatedBy / ModifiedBy / DeleteddBy / AuditChange đúng; scope System cho job; scope lồng nhau; claim sai; middleware dùng header hợp lệ, tự sinh khi thiếu, từ chối header lạ (khoảng trắng, xuống dòng, quá 64 ký tự); log có thuộc tính CorrelationId.
- Chạy thật trên DB dev: login (header `X-Correlation-Id` được trả lại) → `TestDemo/Insert` bản ghi `[DEMO] T02 actor check`: `CreatedBy` = admin; AuditChange có `Actor: USER:<admin> | CorrelationId: t02-insert-check`; `LocationTracking.CreatedBy` = admin; log có `[t02-...]`. Sau đó xóa mềm bản ghi demo qua API: `DeleteddBy` = admin.
AC: [x] unit test chặn ghi khi không có người thao tác · [x] request có token thì CreatedBy đúng · [x] log có CorrelationId.
Còn lại: dùng `IClock` thay `DateTime.Now` để T05 (cùng D-18). Agent actor chỉ dùng thật ở Release 2.

### T03 – Pipeline HTTP chuẩn · 2026-11-14 · DONE (1/2 + 2/2)
> **Lưu ý:** phần dưới làm theo T03 bản cũ. Bộ tài liệu mới của anh (2026-11-14, CLAUDE.md / ROADMAP / DECISIONS / ORG_STRUCTURE…) đổi quy ước v1 sang envelope + If-Match + Idempotency-Key + X-Branch-Id và bỏ yêu cầu tương thích Angular (D-20). Phần 2/2 sẽ làm theo bản mới; lỗi v1 dùng envelope (D-28).
Đã làm:
- `Startup.Configure` theo thứ tự: CorrelationId → ExceptionHandler → HttpsRedirection → Swagger / Static → Routing → CORS → Authentication → Authorization → Endpoints. Bỏ `UseDeveloperExceptionPage` (lộ stack trace), `ServiceLocator.BuildServiceProvider`, `CorsMiddleware` (`Access-Control-Allow-Origin: *`), policy `AllowAnyOrigin` trong `DatabaseConnection`. `ServiceLocator.cs`, `CorsMiddleware.cs` → `_obsolete/AppDelicare/ClinicManagement.WebApi/Infrastructure/`.
- CORS từ `Cors:AllowedOrigins` (mặc định `http://localhost:4200`), expose `X-Correlation-Id`, `Content-Disposition`. Angular (đọc, không sửa): gọi `URL_API = '/api/'` qua `proxy.conf.json` → cùng origin, không bị ảnh hưởng; `jwt.interceptor` chỉ xử lý 401 / 403 → route cũ giữ nguyên mã HTTP.
- `ProblemDetailsExceptionHandler`: lỗi chưa bắt → log (kèm CorrelationId) + 500 ProblemDetails, không lộ message / stack; `UnauthorizedActorException` → 401. `AddProblemDetails`: mọi ProblemDetails có `traceId` = CorrelationId.
- Route mới: `Controllers/V1/ApiV1ControllerBase` (`FromResult` / `FromResults` → mã HTTP thật, lỗi → ProblemDetails); `ApiV1Convention` chỉ áp cho controller v1 (khai báo response 400 / 401 / 403 / 500 dạng problem cho Swagger). Route cũ **không đổi**.
- Endpoint v1 đầu tiên (Spec 2.2): `GET /api/v1/audit/{table}/{id}/history?take=` – lịch sử xử lý của bản ghi (AuditChange + tên người thao tác), validate `table` (regex) và `take` (1–200) → 400 ValidationProblemDetails.
- Swagger: XML comments (WebApi + Model, tắt CS1591 cho code cũ), mô tả quy ước v1 / route cũ, JWT kiểu `http bearer` (dán token, không cần chữ "Bearer").
- Mâu thuẫn tài liệu: Spec 1.2 đề xuất envelope `{success, data, …}` cho v1; CLAUDE.md mục 4 chốt `Result<T>` + ProblemDetails → **theo CLAUDE.md** (mới hơn, là quy ước của anh).

Kiểm tra:
- `dotnet build`: 0 lỗi, cảnh báo ≤ 10. `dotnet test`: **25/25 xanh** (thêm 12): integration test bằng `WebApplicationFactory` (DB dev qua user-secrets, không có DB thì Skip kèm lý do): `Supplier/GetAll` cũ HTTP 200 + đúng dạng `totalRecords / listData / responseCode / responseMessage`; route cũ / v1 không token 401; v1 `table` sai → 400 `application/problem+json` có `errors.table`, `traceId` = header gửi lên; `take=0` → 400; v1 hợp lệ 200; CORS: localhost:4200 được, origin lạ không có header; Swagger có endpoint v1, summary tiếng Việt, response 400, scheme bearer; mọi response có `X-Correlation-Id`; exception handler 500 không lộ chuỗi bí mật / tên exception / stack; actor thiếu → 401.
AC: [x] swagger chạy · [x] `Supplier/GetAll` cũ trả đúng dạng cũ · [x] endpoint v1 mẫu trả 400 ProblemDetails khi input sai.
Phát hiện: A18 – `Repository.GetAllNoneDeleted()` không lọc `IsDeleted` (sửa ở T05).

#### T03 phần 2/2 – quy ước v1 theo bộ tài liệu mới · 2026-11-14 · DONE
Đã làm (thư mục `WebApi/Api/`):
- `ApiEnvelope<T>` `{success, data, message, errors[{code, field, message}], traceId}`, `PagedData<T>` `{items, total, page, pageSize}`, `ErrorCodes` = Phụ lục A.
- `ApiControllerBase` (thay `ApiV1ControllerBase`): action trả dữ liệu thô; `ComputeETag` (ModifiedDate ?? CreatedDate), `SetETag`, `EnsureIfMatch` → 409 `CONCURRENCY_CONFLICT`.
- `EnvelopeResultFilter`: bọc mọi kết quả của controller v1; 400 model binding / DataAnnotations → `VALIDATION_FAILED` kèm `field` camelCase (route cũ giữ factory mặc định).
- `ExceptionToErrorMiddleware` (chỉ `/api/v1`): `ApiException` → mã HTTP + envelope; `UnauthorizedActorException` → 401; lỗi khác → 500 không lộ chi tiết; response lỗi không body (401 JWT, 403, 404 route, 405) cũng thành envelope (D-28).
- `IdempotencyFilter` + `[Idempotent]`: header `Idempotency-Key` (≤ 200) lưu vào sổ `IntegrationLog` (INSERT 1 dòng sau khi lệnh thành công, cùng transaction với lệnh trên SQL Server); gửi lại cùng nội dung → không chạy lại, trả mã cũ + `Idempotency-Replayed: true`; khác nội dung → 422; lệnh lỗi không lưu → thử lại được. GAP-IDEM-01/02.
- `BranchHeaderMiddleware` + `IBranchContext`: `X-Branch-Id` phải là GUID (sai → 400); kiểm tra quyền theo đơn vị ở T07.
- `PageQuery` (`page`, `pageSize` ≤ 200, `q`, `sort=field:asc`) + `ApplySort` theo whitelist (cột lạ → 400).
- `ApiJson`: camelCase; **không bao giờ ghi ra** `PassWord` / `Password` / `PasswordHash` (vẫn đọc được khi đăng nhập) – áp cho toàn app.
- Swagger nhóm theo module: `v1 / hr / shifts`, `v1 / audit`, `legacy / <Controller>`; mô tả quy ước v1.
- Entity `IntegrationLog` (đúng DDL, không kế thừa BaseEntity vì bảng không có DeletedDate / DeleteddBy) + DbSet.
- Endpoint mẫu **`/api/v1/hr/shifts`** (Spec 4.7): GET danh sách / chi tiết (ETag), POST (201, `[Idempotent]`), PUT (If-Match), DELETE (xóa mềm, 204). Kiểm tra: bắt buộc mã / tên / giờ (400), `companyId` không tồn tại (422 `REF_NOT_FOUND`), giờ bắt đầu = kết thúc (422 `BUSINESS_RULE`, GAP-HR-01), trùng mã (409 `DUPLICATE`), ca qua đêm hợp lệ (`isOvernight`). Sau khi lưu đọc lại bản ghi để ETag khớp độ chính xác `datetime` của SQL.
- `AuditController` chuyển sang envelope.
- `GemBoxLicense.Apply`: GemBox chỉ cho `SetLicense` 1 lần mỗi tiến trình (lỗi "serial key can be set only once" khi host khởi động lại – lộ ra khi chạy 2 test host).
- `docs/api-tests/v1-hr-shifts.http`: luồng thử bằng REST Client.

Kiểm tra:
- `dotnet build`: 0 lỗi, 6 cảnh báo. `dotnet test`: **45/45 xanh**. Test v1 chạy trên host InMemory + JWT tự ký (không cần DB): 201 / 200 envelope + phân trang; 400 thiếu trường (đủ field), sai giờ, sort / pageSize / page sai, X-Branch-Id sai; 422 `REF_NOT_FOUND`, `BUSINESS_RULE`; 409 `DUPLICATE`, `CONCURRENCY_CONFLICT` (dữ liệu không đổi); Idempotency gửi 2 lần → 1 ca + 1 log; key dùng lại khác nội dung → 422; lệnh lỗi rồi thử lại cùng key → 201; 404 sau xóa; 401 / 404 route dạng envelope; mật khẩu không bị serialize.
- Chạy thật trên SQL Server dev: POST 2 lần cùng key → 201 + `Idempotency-Replayed: true`, DB có 1 ca + 1 dòng IntegrationLog (`Succeeded`, 201); ETag lúc tạo = lúc GET; PUT ETag mới 200, ETag cũ 409; DELETE 204; tiếng Việt lưu / đọc đúng. Dữ liệu thử: ca `DEMO-T03`, `DEMO-T03-UTF8` (đã xóa mềm) và 1 dòng IntegrationLog key `demo-t03-live-1` (sổ chỉ ghi thêm, để lại).
AC: [x] swagger chạy · [x] 200 đúng envelope · [x] 400 / 422 đúng mã lỗi · [x] 409 khi If-Match lệch · [x] Idempotency-Key gửi 2 lần chỉ ghi 1 lần.
Còn lại: lọc cột deprecated làm theo DTO khi sinh mã (T05 / T45B); `[Permission]` ở T07.

### T05 – Bộ sinh mã mới + đồng bộ entity với 124 bảng · 2026-11-14 · DONE
Đã làm:
- **`tools/AppDelicare.CodeGen`** (console .NET 8, có trong solution, README): đọc DDL (cột, kiểu, NULL, UNIQUE, DEFAULT) + DEPRECATED_COLUMNS + LOGICAL_RELATIONS; sinh theo mẫu `Warehouse*`: Entity, Request, SearchRequest, ViewModel, I/Repository, I/Service, Controller (route cũ), SP `usp<Class>_GetAll`, `DatabaseContext.Generated.cs`, `IUnitOfWork.Generated.cs` / `UnitOfWork.Generated.cs`, `AutoMapperGeneratedProfile`. Cờ `--only`, `--skip-existing` (mặc định), `--force`, `--layers`, `--dry-run`. Entity + file tổng hợp luôn sinh lại; các lớp khác chỉ tạo khi chưa có (không đè service sửa tay).
- Khác mẫu cũ có chủ đích: xóa **không** dùng SQL động `uspSystem_ExecuteQuery` (để ReferenceGuard T07); kiểm tra trùng theo 18 UNIQUE của DDL + `Code`; gán DEFAULT của SQL khi client không gửi; ghi song song cột deprecated; sổ chỉ ghi thêm không có Update / Delete.
- **Entity đồng bộ 124 bảng**: `[Table("TênThật")]` (Dishe, ProductionBatche… giữ tên bảng thật, class bỏ hậu tố), decimal / datetime / date có `[Column(TypeName)]`, `[MaxLength]`, `[Required]`. `BaseEntity` thêm `DeletedBy`; xóa mềm ghi cả `DeletedBy` và `DeleteddBy`; bảng thiếu cột nào thì EF `Ignore`. `CoreDataMergeConflict` (Id bigint, không cột audit) là entity riêng.
- `DatabaseContext`: `partial`, kế thừa `DbContext` (bỏ `IdentityDbContext` – không có bảng AspNet* và không code nào dùng, A11). `IUnitOfWork` / `UnitOfWork`: `partial`, thuộc tính repository viết tay thay bằng bản sinh.
- **Chuyển `_obsolete/AppDelicare/…` (81 file)**: AppConfig, Area, Branch, Department, Opportunity, Region, Lead(s), Product(s), Employees (không có bảng – D-13) và UnitOfMeasure (bảng đã thành `_Legacy`, dùng UnitMeasure – D-02). `ContractMeals` → `ContractMeal`.
- Sửa lỗi lộ ra khi đồng bộ: `CustomerService` kiểm tra trùng bằng cột `CifNo` **không tồn tại** (so với `request.Code`) → so `Code`; 28 SearchRequest có bộ lọc ngày `DateTime` không nullable (gửi `0001-01-01` → `SqlDateTime overflow`, 27 API GetAll trả 500) → `DateTime?`; A18 `Repository.GetAllNoneDeleted()` nay lọc `IsDeleted`.
- 102 SP `GetAll` mới trong `DB/sp/` (74 SP code cũ gọi mà DB thiếu + bảng mới), khai báo thêm mọi tham số service cũ đang truyền. **Đã chạy lên DB dev** (LocalDB): DB có 122 SP `usp*_GetAll`.
- Test `Schema/SchemaMappingTests` (đọc DDL trong test, dựng model SQL Server không kết nối): DDL 124 bảng; mọi DbSet → bảng có thật, mọi property → cột có thật; mọi bảng / cột DDL đều được map; kiểu C# khớp kiểu SQL; tên bảng legacy giữ nguyên.

Kiểm tra:
- `dotnet build --no-incremental`: 0 lỗi, **10 cảnh báo** (= mốc). `dotnet test`: **50/50 xanh**.
- Chạy thật trên DB dev: **119/119** route `api/<X>/GetAll` (Dapper + SP) trả 200 / `responseCode` 200; **117/117** route `GetAllData` (EF, đọc mọi cột) trả 200; log không có lỗi.
AC: [x] build xanh · [x] test DbSet ↔ bảng / property ↔ cột (đọc DDL) xanh · [x] không còn entity map tới bảng không tồn tại.
Còn lại: service cũ vẫn kiểm tra xóa bằng SQL động (A10) → T07. D-27 (AutoMapper) chưa chốt nên template vẫn dùng AutoMapper.

### T06 – Sinh mã cho 35 bảng còn thiếu · 2026-11-14 · DONE
Đã làm: CodeGen sinh trọn CRUD (Request, SearchRequest, ViewModel, Service, Controller, SP) cho **34 bảng** của CODE_AUDIT A1(c) – 238 file; bảng thứ 35 `CoreDataMergeConflict` chỉ có entity + repository (không theo khuôn BaseEntity). 2 bảng `_Legacy_20261113` chỉ có entity + repository (chỉ đọc). 7 sổ chỉ ghi thêm (`IntegrationLog`, `ApprovalDecision`, `TemperatureLog`, `PackagingLedger`, `AttendanceEvent`, `DeliveryEvent`, `ComplaintHistory`): controller **không có** Update / Delete, service từ chối sửa / xóa.
Kiểm tra: build xanh; SP trong `DB/sp/` và đã chạy lên DB dev; DbSet đủ **124** bảng (có entity đọc cho 2 bảng legacy); GetAll / GetAllData của các bảng mới chạy 200 trên DB dev (nằm trong 119 / 117 ở T05).
AC: [x] build xanh · [x] SP nằm trong `DB/sp/` · [x] DbSet đủ 124.

### T07 – Phân quyền UserRole × ObjectMenu + ReferenceGuard + seed tổ chức · 2026-11-14 · DONE
Đã làm:
- `WebApi/Security`: `[Permission(screenId, PermissionAction.View|Add|Edit|Delete|Print|Export|Import|Approve)]`, `[AdminOnly]`, `[SkipPermission]`; `PermissionService` (UserClaim → UserGroup → UserRole × ObjectMenu theo MetaLink `#/<screen-id>`, OR qua các nhóm, cache `Permissions:CacheMinutes` = 5′, `User.IsAdmin` bỏ qua, user không hoạt động → không quyền); `PermissionFilter` toàn cục (403; v1 → envelope `PERMISSION_DENIED`; action cần đăng nhập mà không khai báo quyền → chỉ admin).
- `PermissionConvention`: gắn quyền tự động cho **mọi controller CRUD cũ** theo `ScreenMap` (tên controller → screen-id của catalog.js; không có màn riêng → `dbx-explorer`) + tên action (Get* → View, Insert → Add, Update / Save* / Reset* → Edit, Delete → Delete, Export / Print / Import). `GetProfile`, `UpdateProfile`, `ChangePassword` chỉ cần đăng nhập. **`AuditChange/DeleteData` và `User/SaveIsAdmin` chỉ admin** (D-26, chống tự nâng quyền). v1: `hr/shifts` và `audit` khai báo tường minh.
- Duyệt (A): GAP-PERM-01 → `Permissions:ApproverGroups` trong appsettings (production: CEO, COO → **OPS-SCM không duyệt được kế hoạch sản xuất**).
- **ReferenceGuard** (`Data/Share`): đếm bản ghi chưa xóa ở các bảng nguồn theo `ReferenceMap.Generated.cs` (sinh từ LOGICAL_RELATIONS) bằng LINQ có tham số – chạy được cả SQL Server lẫn InMemory. Thay khối SQL động `uspSystem_ExecuteQuery` trong **83 service cũ** (qua `IAuditChangeService.IsReferencedAsync`, không phải sửa constructor) và template CodeGen. v1 `DELETE /hr/shifts/{id}` → 409 `IN_USE` kèm bảng đang dùng.
- CodeGen: sinh `ScreenMap.Generated.cs`, `ReferenceMap.Generated.cs`, `DB/seed/OrganizationUnit_seed.sql` (Company DLC + 33 đơn vị + 6 KitchenWorkstation), `ObjectMenu_seed.sql` (11 nhóm + 119 màn, MetaLink `#/<screen-id>`), `UserRole_seed.sql` (16 nhóm × 119 màn theo ma trận). Idempotent theo Code / MetaLink / Name, 1 batch, không DELETE, CreatedBy = tài khoản `admin` khai báo rõ. File do generator sở hữu (header `<auto-generated>`) nay luôn sinh lại.
- **Sửa lỗi CodeGen của T05**: regex đọc LOGICAL_RELATIONS đòi ký hiệu `→` (chỉ có ở tiêu đề) → map quan hệ rỗng → ViewModel / SP sinh mới thiếu nhãn `XxxName`. Đã sửa, sinh lại 22 ViewModel + 83 SP (có JOIN nhãn), đã chạy lại SP lên DB dev.
- **Đã chạy seed lên DB dev 2 lần**: Company 1, OrganizationUnit 33, KitchenWorkstation 6, ObjectMenu 156 (26 cũ + 130), UserGroup 36 (20 cũ + 16), UserRole 2.349 – lần 2 không đổi.

Kiểm tra:
- `dotnet build --no-incremental`: 0 lỗi, 10 cảnh báo. `dotnet test`: **61/61 xanh** (+11): không AllowAdd → 403 envelope (v1) và 403 (route cũ `Shift/Insert`), vẫn xem được; có AllowAdd / admin → 201; không AllowAccess → 403 cả khi xem; user không hoạt động → 403; DeleteData / SaveIsAdmin → 403 với user đủ quyền nhưng không phải admin; xóa ca đang có ShiftAssignment → 409 `IN_USE`, tham chiếu đã xóa mềm không chặn; OPS-SCM không duyệt `prod-plan`, COO / CEO duyệt được (đọc appsettings thật); **mọi endpoint cần đăng nhập đều có khai báo quyền** (> 600 endpoint); seed chạy 2 lần trên DB dev trong transaction rồi rollback → số dòng không đổi, không trùng mã / MetaLink / cặp nhóm × menu, cây OPS-KIT-COOK → OPS-KIT.
- Chạy thật trên DB dev: 119/119 `GetAll` 200 với admin; v1 shifts 200; log không lỗi.
AC: [x] không AllowAdd gọi Insert → 403 · [x] admin → 200 · [x] xóa bản ghi đang được tham chiếu → 409 · [x] seed tổ chức chạy 2 lần không nhân bản · [x] OPS-SCM không duyệt được kế hoạch sản xuất.
Còn lại: kiểm tra quyền theo đơn vị (`X-Branch-Id` ↔ OrganizationUnit của user) chưa có dữ liệu liên kết user ↔ đơn vị (GAP-C-10) → làm khi có Employee ↔ User. Cache quyền 5′: đổi quyền có hiệu lực sau tối đa 5 phút.

### T08 – Khóa sổ chỉ ghi thêm · 2026-11-14 · DONE
Đã làm:
- `DatabaseContext.AppendOnlyTypes` = 9 sổ CLAUDE.md 3.3 (InventoryTransaction, PackagingLedger, TemperatureLog, AuditChange, IntegrationLog, ApprovalDecision, AttendanceEvent, DeliveryEvent, ComplaintHistory). Có dòng sổ ở trạng thái Modified / Deleted (kể cả xóa mềm) → `AppendOnlyViolationException` trên **mọi** đường SaveChanges. Route v1 → 422 envelope.
- Bỏ `Update` / `Delete` của `InventoryTransactionController` cũ (các sổ sinh ở T06 vốn không có).
- `InventoryLedgerService` (Spec 7): `PostAsync` ghi sổ + upsert `InventoryBalance` theo (kho, vị trí, nguyên liệu, lô) trong 1 transaction, chặn tồn âm (422 `INSUFFICIENT_STOCK`), ghi song song cột deprecated NOT NULL (UomId, LotId, TransactionNo, OnHandQty / ReservedQty / AvailableQty). `ReverseAsync(id, reason)`: bút toán ngược dấu tham chiếu dòng gốc (GAP-INV-01), không đảo 2 lần / không đảo bút toán đảo (409 `INVALID_TRANSITION`). Dùng chung transaction ngoài nếu có (vd. `[Idempotent]`).
- `BusinessRuleException` (tầng service, không phụ thuộc WebApi) → envelope với mã HTTP + mã lỗi Phụ lục A.
- v1 `POST /api/v1/scm/inventory-transactions/{id}/reverse` (`[Idempotent]`, quyền Edit `#/scm-inv-txn`).

Kiểm tra:
- `dotnet build --no-incremental`: 0 lỗi, 10 cảnh báo. `dotnet test`: **71/71 xanh** (+11): danh sách 9 sổ; sửa dòng sổ bị chặn ở cả 3 đường SaveChanges; xóa mềm / xóa cứng bị chặn; bảng thường vẫn sửa được; nhập 120 → xuất 20 (tồn 100) → đảo phiếu xuất → **tồn 120**, dòng gốc giữ nguyên, dòng đảo có ReferenceType / ReferenceId / Reason / CreatedBy; đảo 2 lần / đảo bút toán đảo → 409; đảo phiếu nhập đã xuất bớt → 422, xuất quá tồn → 422; v1 reverse 201 → lần 2 409, thiếu lý do 400; route cũ không còn Update / Delete.
- **SQL Server thật**: Post + Reverse trên DB dev trong transaction rồi rollback – kiểu decimal(18,4) / cột deprecated NOT NULL ghi đúng, tồn về 0, rollback không để lại dòng sổ.
AC: [x] cập nhật / xóa sổ bị chặn · [x] bút toán đảo làm tồn trở về đúng.
Còn lại: `AuditChange/DeleteData` (đã khóa admin) vẫn DELETE vật lý bằng SQL nên **vượt qua khóa sổ** → D-26. Khóa dòng tồn khi nhiều người xuất cùng lúc (UPDLOCK / rowversion) làm cùng T16.

### T09 – Dự án test · 2026-11-14 · DONE
Đã làm: `AppDelicare.Tests` (xUnit, FluentAssertions 7.2.0, EF InMemory, Mvc.Testing, Xunit.SkippableFact) đã dựng dần từ T02 / T03 / T05 / T07 / T08; T09 bổ sung `ConnectionStrings:TestDb` (có thì test tích hợp dùng DB này thay MainConnectionString) và `AppDelicare.Tests/README.md` (3 loại test, cấu hình, quy ước: test ghi SQL Server phải ROLLBACK, không commit dữ liệu test vào sổ).
Kiểm tra: `dotnet test` trên máy anh **71/71 xanh**, 0 skip (DB dev có); không có DB → test tích hợp Skip kèm lý do.
AC: [x] `dotnet test` chạy được trên máy anh · [x] README test.
Ghi chú: "Unit test cho Application" làm khi có `AppDelicare.Application` (T10).

### T48.1 – Staging backend · 2026-11-14 · DONE
Đã làm:
- Cấu hình theo môi trường: `appsettings.Staging.json`, `appsettings.Production.json` (không có bí mật – chỉ biến môi trường); log path dùng `/` (chạy được Linux).
- `/health` (API + DB qua `AddDbContextCheck`, JSON: status, thời gian, environment, version) và `/health/live`; không cần đăng nhập, không lộ chi tiết.
- `deploy/db/Deploy-Database.ps1` (idempotent): tạo DB (tùy chọn) → **chỉ khi DB rỗng** chạy DDL chuẩn của PO (bỏ dòng `USE`) → mọi SP `DB/sp` → admin ban đầu (`DB/seed/00_Admin_seed.sql`, hash tính lúc chạy, không lưu mật khẩu) → seed tổ chức / menu / quyền. GAP-DEP-01.
- IIS: `deploy/iis/Publish-Iis.ps1` (publish có web.config), hướng dẫn Hosting Bundle, App Pool, biến môi trường bí mật trên máy chủ.
- Docker: `AppDelicare/ClinicManagement.WebApi/Dockerfile` (multi-stage, giờ VN, healthcheck, user không root), `AppDelicare/.dockerignore`, `deploy/docker/docker-compose.staging.yml` (SQL Server 2022 + `db-init` + API, không Angular), `db-init.sh`, `staging.env.example` (file thật gitignore). **Chưa chạy thử được – máy dev chưa cài Docker.**
- CI: `.github/workflows/backend-ci.yml` – build + test + báo cáo gói lỗ hổng cho mỗi PR / push main; không deploy.
- `deploy/README.md` (từng bước 3 cách + kiểm tra + sự cố), `docs/claude/UAT_CHECKLIST.md` (R1.0, 14 kịch bản), `docs/api-tests/r1.0-foundation.http`.
- Gỡ ProjectReference tới `AppDelicare.Generator` (T4 .NET Framework, không code nào dùng) khỏi WebApi để publish / build Linux được. `uspSystem_BackupDB` (RESTORE ghi đè DB) → `_obsolete/DB/sp/` để script triển khai không bao giờ cài.
- **Sửa lỗi lộ ra khi dựng DB mới**: đăng nhập lỗi 500 khi user không có Person; `uspUserRole_GetByUserGroupId` lỗi chuyển kiểu khi user chưa thuộc nhóm nào (chạy lại lên DB dev + staging); đăng nhập gọi `api.iplocation.net` (A20) → ghi IP client.

Kiểm tra (máy anh, mô phỏng máy sạch – DB mới, bí mật chỉ qua biến môi trường, chạy bản publish Release ở `Staging`):
- `Deploy-Database.ps1` trên DB **trống** `AppDelicare_Staging` (LocalDB): 124 bảng, 130 SP, admin, seed; chạy lần 2 không đổi gì (124 bảng / 1 user / 33 đơn vị / 130 menu / 16 nhóm / 1.904 quyền).
- `/health` Healthy (database Healthy, environment Staging); `/health/live` 200; Swagger UI + JSON 200; đăng nhập admin staging → token; **119/119** `GetAll` 200; OrganizationUnit 33, UserGroup 16, v1 shifts 200; LocationTracking ghi IP client.
- `dotnet build --no-incremental` 0 lỗi / 10 cảnh báo; `dotnet test` 71/71.
- Hash mật khẩu ở 3 nơi (C#/PowerShell, Node, `md5sum` trong container) cho cùng kết quả.
AC: [x] staging backend chạy được từ máy sạch theo README (cách A / B) · [x] `/health` xanh · [x] Swagger staging mở được · [x] đăng nhập qua API và gọi được API danh mục. (Cách C Docker: chưa kiểm được.)
Bí mật staging trên máy anh: `deploy/staging.local.md`, `deploy/staging.local.env.ps1` (gitignore, không commit).

### T10 – Khung AppDelicare.Application · 2026-11-14 · DONE
Đã làm:
- Project **`AppDelicare.Application`** (tham chiếu Data, Model; WebApi + Tests tham chiếu nó), thư mục theo miền bắt đầu từ `Common/`.
- Messaging tự viết (MediatR từ v13 là license thương mại): `ICommand<T>` / `IQuery<T>`, `IRequestHandler`, `IDispatcher` (validator → handler → phát sự kiện), `IDomainEvent` + `IDomainEventHandler` + `IDomainEventCollector`: sự kiện chỉ phát khi **lệnh ngoài cùng thành công**, lệnh lỗi → hủy sự kiện. Gọi handler kiểu mạnh (exception không bị bọc).
- Validator: **FluentValidation 12.2** (Apache-2.0). Lỗi → `ValidationFailedException` → 400 `VALIDATION_FAILED` từng `field` (camelCase).
- `IClock` / `SystemClock`: giờ VN theo `BusinessRules:TimeZone` (Kind = Unspecified để ghi thẳng cột datetime). `BusinessRulesOptions` + section `BusinessRules` trong appsettings (BUSINESS_RULES.md).
- `AddApplication()` quét assembly đăng ký handler / validator / handler sự kiện.
- Use case mẫu **`Common.LookupQuery`** (Spec 2.2): `GET /api/v1/meta/lookups/{table}?q&take&ids` → `[{ id, label, code, status }]`; nhãn DisplayName → Name → FullName → số chứng từ → Code; chỉ Active khi bảng có Status; `ids` lấy đúng giá trị đang chọn; nhận tên bảng thật (`Dishe`) hoặc tên class (`Dish`); chặn bảng nhạy cảm (GAP-API-02). Truy vấn động theo metadata EF, không SQL ghép chuỗi. `MetaController` mỏng chỉ gọi dispatcher.
- Sửa: dò bảng theo `[Table]` (đúng cả provider InMemory) trong LookupQuery và ReferenceGuard.

Kiểm tra: build 0 lỗi / 10 cảnh báo; test **83/83** (+12): dispatcher chạy handler + phát sự kiện sau thành công, lệnh lỗi hủy sự kiện, validator lỗi không chạy handler; IClock = UTC+7; BusinessRules đọc từ cấu hình + mặc định; lookup: thứ tự nhãn, lọc Inactive / đã xóa, tìm theo mã, theo ids, tên bảng legacy, chặn User / AuditChange / Patient / bảng lạ / _Legacy; end-to-end qua API (200, 400 `take`, 404 bảng chặn). Chạy thật trên DB dev: lookup OrganizationUnit / UserGroup / KitchenWorkstation / ObjectMenu / Company / Dishe 200, log không lỗi.
AC: [x] 1 use case mẫu (`Common.LookupQuery`) chạy end-to-end và có test.

### T21.1 – Seed ca làm việc · 2026-11-14 · DONE
Đã làm: `DB/seed/Shift_seed.sql` – 4 ca theo đề xuất D-19 (chưa chốt, mã ca chỉ nằm ở bảng Shift nên đổi lại dễ): SANG_6H Sáng 06:00–09:00, TRUA Trưa 10:30–13:30, TOI Tối 16:30–19:30, DEM_23H Đêm 23:00–01:00 (qua đêm). Giờ là PROPOSED. Idempotent theo Code, **không ghi đè** tên / giờ đã có (giữ chỉnh sửa qua `/api/v1/hr/shifts`). Thêm vào `Deploy-Database.ps1`, `db-init.sh`, `SeedTests`.
Đã chạy lên DB dev và staging (2 lần mỗi DB): đúng 4 ca, không trùng.
Kèm theo: log chuyển từ `wwwroot/Logs` sang `Logs/` (ngoài thư mục web tĩnh). Đã kiểm: `/Logs/*.log` hiện trả 404 (kiểu .log không được phục vụ) nên **chưa lộ**, nhưng để trong wwwroot dễ lộ nếu bật phục vụ mọi kiểu file, bị đóng gói khi publish và khóa file khi build.
Kiểm tra: test 83/83 (SeedTests kiểm thêm 4 ca, không trùng mã).
AC (phần seed của T21): [x] 4 ca TRUA, TOI, DEM_23H, SANG_6H.

### T45B.1 – CRUD chuẩn v1 cho màn hình P0–P2 · 2026-11-14 · DONE
Đã làm:
- CodeGen sinh `AppDelicare.Application/Common/Crud/TableMetadata.Generated.cs`: mỗi bảng – cột (kiểu, NOT NULL không DEFAULT, độ dài, audit, deprecated + cột thay thế, bảng đích theo LOGICAL_RELATIONS), UNIQUE, cột nhãn, cột tìm kiếm.
- `ResourceRegistry`: **39 resource** cho 34 màn P0–P2 (`/api/v1/{module}/{resource}` theo spec mục 3–5), cờ CanCreate / CanUpdate / CanDelete (audit-changes, access-logs, recipes, recipe-lines, standard-costs: chỉ đọc – ghi qua API nghiệp vụ T45B.4), luật riêng theo spec: org-unit chặn vòng cha–con, object-menu MetaLink duy nhất, delivery-location khung giờ + tọa độ, ingredient YieldPercent / StandardWastePercent / ShelfLifeDays, uom-conversion factor > 0 + from ≠ to + không trùng cặp, inventory-policy Min ≤ Safety ≤ Reorder ≤ Max + một dòng / (nguyên liệu, kho), price-policy UnitPrice > 0.
- `CrudService` (Application): danh sách (page / pageSize / q / sort / lọc theo cột camelCase / `xxxFrom` `xxxTo`), `summary` (total + byStatus), chi tiết + ETag + `{field}Label` cho cột tham chiếu, POST / PUT (chỉ trường gửi lên) / DELETE mềm. Kiểm tra: bắt buộc (gồm cột thay thế của cột cũ NOT NULL), kiểu, độ dài, trường lạ / cột deprecated / chỉ đọc → 400; UNIQUE + Code → 409 DUPLICATE; tham chiếu → 422 REF_NOT_FOUND; If-Match → 409; đang dùng → 409 IN_USE (ReferenceGuard). Ghi song song cột deprecated. Không trả mật khẩu / cột xóa mềm. Truy vấn động bằng expression tree (có tham số).
- WebApi: `CrudResourceController` + 39 controller con (route tường minh, Swagger nhóm theo module); quyền theo màn qua `PermissionConvention` (đọc → View, POST → Add, PUT → Edit, DELETE → Delete); POST có `[Idempotent]`. `GET /api/v1/meta/resources`, `GET /api/v1/meta/schema/{module}/{resource}` (trường, kiểu, bắt buộc, độ dài, bảng tham chiếu, trường nhãn) cho frontend.

Kiểm tra:
- Build 0 lỗi / 10 cảnh báo; test **91/91** (+8): vòng tạo / đọc / danh sách / summary; 400 trường lạ / sai kiểu / quá dài / cột deprecated / thiếu cột thay thế; 409 DUPLICATE; 422 REF_NOT_FOUND (đúng field); nhãn tham chiếu; If-Match 409; xóa đang dùng 409 IN_USE; ghi song song FromUomId; luật uom-conversion; resource chỉ đọc 405; phân quyền theo màn (xem được, thêm 403, màn khác 403); schema + danh sách resource.
- **SQL Server thật**: 39 resource × (danh sách có sort + summary) = **78/78 200**; tạo kho `[DEMO]` 201 → trùng 409 → sửa ETag mới 200 / cũ 409 → xóa 204 → đọc 404; lịch sử có Added + Modified; log không lỗi.

### T45B.2 – API quản trị riêng của P0 · 2026-11-14 · DONE
Đã làm (logic ở `AppDelicare.Application/Admin`, controller mỏng ở `WebApi/Controllers/V1/Admin`, `AuthAndMe.cs`):
- **Người dùng** `/api/v1/admin/users`: danh sách (isActive, groupId, q trên tên đăng nhập / họ tên / SĐT) kèm person + groups; chi tiết; tạo (Person → User → UserClaim trong 1 transaction, tên đăng nhập chuẩn hóa chữ thường, trùng → 409, mật khẩu ≥ 8, nhóm không có → 422); sửa hồ sơ; `PUT {id}/groups` (thêm / xóa mềm); `lock` (bắt buộc lý do, không tự khóa mình) / `unlock`; `reset-password` (**chỉ admin**, mật khẩu tạm 12 ký tự RNG, trả 1 lần); `effective-permissions` (OR mọi nhóm + `grantedBy`). Không bao giờ trả mật khẩu.
- **Ma trận quyền nhóm** `/api/v1/admin/user-groups/{id}/permissions` GET (cây menu + cờ) / PUT (chuẩn hóa: bật cờ bất kỳ → bật AllowAccess, tắt AllowAccess → tắt hết) / `bulk`; `preview` (chỉ admin).
- Sự kiện `PermissionsChanged` (phát sau commit) → `PermissionCacheInvalidator` xóa cache quyền **ngay** cho user liên quan (cấp / thu hồi quyền, đổi nhóm, khóa / mở).
- **Menu** `/admin/object-menus/tree`, `PUT reorder` (đổi cha / thứ tự, chặn vòng lặp). **Đơn vị** `/admin/org-units/tree` (số nhân viên, số kho, tên quản lý), `/admin/branches` (UnitType = BRANCH, mọi user đăng nhập). **Merge-conflict** `/admin/merge-conflicts` (lọc resolved) + `POST {id}/resolve` (Id bigint, xử lý 2 lần → 409).
- **Meta** `/api/v1/meta/menu` (cây menu user được truy cập + cờ), `/api/v1/meta/me` ({ user, branches, permissions }).
- **Auth v1** `POST /api/v1/auth/login` → `{ accessToken, expiresIn, tokenType, userId, … }`, sai → 401 envelope (không phân biệt sai mật khẩu / không tồn tại); `POST /api/v1/auth/change-password` (sai mật khẩu cũ → 422). Refresh token chưa làm (GAP-AUTH-01).
- `PasswordHasher` – một chỗ băm duy nhất (vẫn MD5 chờ D-29). **AuditChange không còn ghi giá trị mật khẩu (kể cả hash)** – chỉ ghi "đã thay đổi" (trước đây đổi mật khẩu là hash lọt vào nhật ký).

Kiểm tra: build 0 lỗi / 10 cảnh báo; test **98/98** (+7): tạo user (không lộ mật khẩu, chuẩn hóa tên, 409 / 400 / 422, lọc q + groupId); cấp quyền có hiệu lực ngay + chuẩn hóa cờ + grantedBy + thu hồi ngay; khóa (thiếu lý do 400, tự khóa 422) → mất quyền ngay → mở lại; reset chỉ admin; đổi mật khẩu sai / đúng; audit không chứa hash; cây menu + reorder + chặn vòng lặp; cây đơn vị + đếm nhân viên + branches; merge-conflict resolve + 409; /meta/me + /meta/menu theo user; login v1 trên DB dev (token / 401 envelope).
Còn lại (→ T45B.5): `/meta/statuses`, `/meta/reference-lists`, refresh token.

### T45B.3 – API riêng của P1 (master data) · 2026-11-14 · DONE
Đã làm:
- CRUD chuẩn thêm 3 hook: `Enrich` (thêm trường tính toán vào danh sách / chi tiết), `CustomFilters` (key lọc riêng), `AfterSave` (xử lý sau khi lưu, cùng request).
- `AppDelicare.Application/MasterData/P1MasterData.cs`:
  - Khách hàng: danh sách thêm `activeContracts`, `locationCount`, `avgMealsPerDay7d`, `branchName`; trùng CIF → 409; `GET /sales/customers/{id}/overview` (khách, liên hệ, điểm giao, hợp đồng + suất, 15 đơn gần nhất, KPI: số đơn, doanh thu đã đối soát qua Order, số phản ánh).
  - Điểm giao: không cho `status = Inactive` khi còn DeliveryOrder chưa kết thúc → 409 IN_USE.
  - Giá NCC: lọc `effectiveOn` (EffectiveFrom ≤ ngày ≤ EffectiveTo); đặt dòng mới `isPreferred` → tự tắt dòng ưu tiên cũ của cùng nguyên liệu.
  - Nhà cung cấp: `GET /scm/suppliers/{id}/overview` (bảng giá + tên nguyên liệu, PO đang mở, vấn đề NCC, KPI 90 ngày onTimeRate / rejectRate – GAP-P1-01).
  - Nguyên liệu: danh sách thêm `availableQuantity` (Σ InventoryBalance) và `belowMin`.
  - Kho: `GET /scm/warehouses/{id}/layout` (khu + nhiệt độ + vị trí, dùng StorageZoneId).
  - `IUomService.ConvertAsync` + `GET /scm/uom-conversions/convert`: chiều xuôi, ngược, qua đơn vị trung gian (BFS), chỉ quy đổi đang hiệu lực; không có → 422 UOM_CONVERSION_MISSING.
Kiểm tra: build 0 lỗi / 10 cảnh báo; test **103/103** (+5) phủ từng mục trên. SQL Server thật: danh sách khách / nguyên liệu (có trường tính toán), lọc effectiveOn, overview (404 khi không có), convert (422 khi thiếu quy đổi), users / org tree / meta me / menu đều chạy, log không lỗi.

### T45B.4 – API riêng của P2 (công thức, giá thành, chính sách giá) · 2026-11-14 · DONE
Đã làm (`AppDelicare.Application/Menu/Recipes.cs`, `WebApi/Controllers/V1/Menu/P2Controllers.cs`):
- **Phiên bản công thức** (không có bảng RecipeVersion – mỗi phiên bản là một Recipe cùng DishId): `GET /menu/dishes/{dishId}/recipes` (mới nhất trước, kèm dòng + chi phí / suất); `POST /menu/dishes/{dishId}/recipes` (VersionNo = max + 1, VersionCode = {mã món}-V{n}, Draft, sao chép dòng từ bản nguồn; chặn khi đã có bản Draft / PendingApproval → 409).
- **Dòng định lượng** chỉ khi Draft: `POST /menu/recipes/{id}/lines`, `PUT` / `DELETE /menu/recipe-lines/{id}` (quantity > 0, nguyên liệu / đơn vị phải tồn tại, ghi song song UomId).
- **Duyệt**: `submit` (≥ 1 dòng → PendingApproval + ApprovalRequest RECIPE_VERSION), `approve` / `reject` (quyền Approve – nhóm CEO, COO; từ chối bắt buộc lý do → về Draft) – GAP-P2-01.
- **Khóa** `POST /menu/recipes/{id}/lock {effectiveFrom}` – spec 5.2 bước 1–5: bản hiệu lực cũ → Obsolete (EffectiveTo = from − 1), bản này Locked / IsActive / LockedAt / LockedBy, SnapshotJson, StandardCost mới (cũ → Inactive), Dishe.StandardCost.
- **Giá thành** `GET /menu/recipes/{id}/cost?mealCount&on` – bước 6 qua `IRecipeCostCalculator` (giá NCC ưu tiên đang hiệu lực + UomService); dòng thiếu giá / thiếu quy đổi có `warning`, `complete = false`. `POST /production/standard-costs/recalculate` chạy lại bước 4–5 với giá hiện hành – GAP-P2-02.
- **Món**: danh sách thêm `activeRecipe` + `estimatedMaterialCost`.
- **Chính sách giá**: chặn chồng hiệu lực (422); `GET /sales/price-policies/resolve` (hợp đồng → khách → ContractMeal) – GAP-P2-03.
- CRUD chuẩn: convention **gỡ action không được phép** (resource chỉ đọc không còn POST / PUT / DELETE – Swagger đúng thực tế, không chiếm route của API nghiệp vụ như `PUT /menu/recipe-lines/{id}`).

Kiểm tra: build 0 lỗi / 10 cảnh báo; test **108/108** (+5): vòng đời đầy đủ V1 → khóa → V2 sao chép → khóa → V1 Obsolete (EffectiveTo đúng), chi phí có quy đổi g → kg (20 400 / suất), StandardCost Active / Inactive, Dishe.StandardCost, ApprovalRequest Approved, sửa dòng khi không Draft 409, khóa khi chưa duyệt 409; tính lại theo giá mới; thiếu giá → cảnh báo; tra đơn giá theo thứ tự + chồng hiệu lực 422 + nối tiếp hợp lệ; OPS-PLAN không duyệt được (403). **SQL Server thật** (transaction rồi rollback): tạo → dòng → gửi → duyệt → khóa, LockedAt / SnapshotJson / StandardCost 9 900 / UomId đúng, không để lại dữ liệu.

### CHORE – Đổi tên thư mục ClinicManagement.* → AppDelicare.* · 2026-11-14 · DONE
Theo yêu cầu của anh: `AppDelicare/ClinicManagement.{Model,Data,Services,WebApi}` → `AppDelicare/AppDelicare.{Model,Data,Services,WebApi}` (git mv, giữ lịch sử). Cập nhật đường dẫn trong `.sln`, các `.csproj`, Dockerfile, `deploy/*`, `CLAUDE.md`, `ARCHITECTURE.md`, `SECRETS_TODO.md`, `tools/AppDelicare.CodeGen`, `ClinicManagement.Generator/RunCopyFile.bat` (chỉ đích chép). Giữ nguyên `ClinicManagement.Generator`, `docs/reference/*` và các mục lịch sử cũ trong PROGRESS / CODE_AUDIT. Namespace, tên project, `UserSecretsId` không đổi.
Kiểm tra: build 0 lỗi / 10 cảnh báo; test **108/108**; CodeGen `--dry-run` ghi 0 file.

### T45R – Về khung code cũ, gỡ lớp v1 (D-30, D-31) · 2026-11-15 · DONE
Kế hoạch: 1) rà soát code cũ + kiểm kê v1 → `LEGACY_REVIEW.md` 2) hạ tầng: IClock / BusinessRules về tầng cũ, sửa transaction UnitOfWork, PermissionService sang Services, quyền duyệt = menu "Duyệt …" 3) quản trị P0 4) danh mục P1 5) công thức / giá P2 6) gỡ lớp v1 vào `_obsolete/v1` 7) viết lại test 8) tài liệu.

Rà soát (đã có – bổ sung gì – ở đâu): `docs/claude/LEGACY_REVIEW.md` (bảng màn hình P0–P2 và bảng endpoint v1 → action cũ).

Đã làm:
- **Hạ tầng (giữ trong khung cũ):**
  - `IClock` → `Data/Share/Clock.cs`, `BusinessRulesOptions` / `PermissionOptions` → `Model/Option`.
  - **Sửa `UnitOfWork.OpenTransaction` (CODE_AUDIT A21)**: trước đây EF không nằm trong transaction. Nay transaction mở trên kết nối DbContext, Dapper dùng chung, service lồng nhau tham gia transaction ngoài; InMemory bỏ qua.
  - `Result<T>.Success / Fail` (cùng hình dạng cũ).
  - `IPermissionService` / `PermissionService` → `AppDelicare.Services`; service User / UserClaim / UserGroup(UserRole) / ObjectMenu xóa cache quyền ngay khi ghi.
  - **Quyền duyệt (D-31)**: mục ObjectMenu "Duyệt – …" (`#/approve/<screen-id>`, 14 màn có loại AP) + UserRole.AllowEdit; bỏ `Permissions:ApproverGroups` và `ScreenMap.MatrixGroup`. Seed chỉ thêm phần thiếu.
- **P0**:
  - `UserRole/GetMatrix`, `Update` (tạo dòng thiếu, chuẩn hóa cờ), `SaveAccessAll`, `CopyFromUserGroup`, `GetPreview` (admin), `GetByUserId`.
  - `User/GetCurrent`; `SaveIsActive` chặn tự khóa; `ResetPassword` chỉ admin.
  - **Nhóm mới không còn toàn quyền** (A22, PROPOSED).
  - `ObjectMenu/GetTree`, `UpdateOrder` (chặn vòng lặp), MetaLink duy nhất.
  - `OrganizationUnit/GetTree`, `GetBranches`, chặn vòng lặp cha.
  - `CoreDataMergeConflict` controller / service / SP mới (Id bigint).
- **P1**:
  - Luật trong Insert / Update: Customer (CIF), CustomerDeliveryLocation (giờ giao, tọa độ, chặn ngừng khi còn đơn), Ingredient, SupplierMaterial (1 NCC ưu tiên), UomConversion, InventoryPolicy, Shift.
  - `Customer/GetOverview`, `Supplier/GetOverview`, `Warehouse/GetLayout`, `Ingredient/GetStock`, `UomConversion/Convert`, `InventoryTransaction/Reverse`.
- **P2**:
  - `Recipe/GetByDishId, CreateVersion, Submit, Approve, Reject, Lock, CalculateCost`; Insert luôn tạo Draft, Update / Delete chỉ khi Draft; RecipeLine chỉ sửa khi Draft, ghi song song UomId.
  - `StandardCost/Recalculate`, `Dish/GetActiveRecipe`.
  - `PricePolicy/Resolve` + đơn giá > 0 + chặn chồng hiệu lực; checklist (Code, VersionNo).
- **CodeGen**:
  - `--merge` bổ sung cột DDL còn thiếu vào Request / ViewModel cũ (A23, 144 file).
  - SP `GetAll` có `@EffectiveOn` cho bảng có hiệu lực (10 SP, đã cài lên DB dev).
  - Bỏ layer `crud`; không thêm kiểm trùng Code đơn lẻ khi DDL có UNIQUE chứa Code.
- **Gỡ v1 → `_obsolete/v1/`** (không xóa):
  - `Controllers/V1/**` và `Api/**` (giữ `ApiJson` – ẩn mật khẩu – chuyển `Infrastructure`).
  - Project `AppDelicare.Application` (đã gỡ khỏi sln / csproj), test v1, `CrudMetadataGen.cs`, `v1-hr-shifts.http`.
  - Startup bỏ envelope / idempotency / X-Branch-Id; Swagger nhóm theo controller.

Kiểm tra:
- Build: `dotnet build --no-incremental` 0 lỗi / 10 cảnh báo (= mốc).
- Test: **76/76**, gồm 3 test chạy SQL Server dev:
  - rollback transaction UnitOfWork thật sự hủy ghi EF;
  - vòng đời công thức trên DB thật rồi rollback;
  - chạy thử action mới dùng SP.
- CodeGen `--dry-run` ghi 0 file.
- Seed ObjectMenu / UserRole chạy trên DB dev: 14 mục duyệt; "Duyệt – Công thức" cấp cho CEO, COO.
- `grep api/v1` trong .cs = 0, ngoài `_obsolete` và 2 test kiểm tra sự vắng mặt.
- `git status` không có thay đổi trong `app-delicare-client/`.

AC: [x] LEGACY_REVIEW · [x] không route v1 · [x] đúng mẫu WarehouseController, không controller inject DbContext / UnitOfWork · [x] không service quản trị song song · [x] quyền không cấu hình appsettings / tên nhóm · [x] build + test xanh.

Còn lại / GAP:
- `.http` chưa chạy tay từng request.
- Update theo kiểu cũ là **ghi đè đủ trường** (AutoMapper): frontend phải gửi lại mọi trường, nếu không cột mới sẽ thành null. Ghi rõ trong T45B.5.
- Cột NOT NULL chưa có `[Required]` ở DTO cũ: thiếu thì trả 500 (theo hành vi cũ). Sẽ xem lại ở T45B.5.

Cần anh: duyệt 2 điểm PROPOSED (nhóm mới không có quyền; ResetPassword / GetPreview chỉ admin).

### T45B.5 – Bàn giao frontend P0–P2, trạng thái, danh mục tham chiếu · 2026-11-15 · DONE
Kế hoạch: 1) rà soát: chưa có action trạng thái / tham chiếu; STATUS_CATALOG (47 bộ), Gap Register sheet 08 (danh mục chưa có bảng) 2) CodeGen sinh StatusCatalog 3) MetaDataService / MetaDataController (mẫu cũ, không gắn bảng) 4) tài liệu bàn giao 5) test, .http, UAT.

Đã làm:
- CodeGen layer `status` → `Model/Helper/StatusCatalog.Generated.cs` (47 bộ, mã + nhãn tiếng Việt + nguồn).
- `MetaData/GetStatuses?key=`, `MetaData/GetReferenceLists?name=`; chỉ cần đăng nhập.
- Danh mục tham chiếu trong `appsettings.json` → `ReferenceLists`: 9 danh mục (MealType, WarehouseType, StorageType, WasteType, AdjustmentReason, ReturnInspectionResult, CustomerType, SupplierType, QcResult), đều PROPOSED.
  - **MealType theo D-10 / BUSINESS_RULES** (SANG / TRUA / TOI / DEM), không theo Gap Register (BREAKFAST / LUNCH…).
- `docs/frontend-handoff/P0-P2.md`:
  - quy ước (đăng nhập, Result, lỗi, danh sách / form, ghi đè đủ trường, quyền, duyệt);
  - 265 action theo màn hình, sinh từ code controller.
- `.http` r1.1 có thêm MetaData; UAT_CHECKLIST thêm R1.1 (13 kịch bản), sửa R1.0 theo route cũ.

Kiểm tra: build 0 lỗi / 10 cảnh báo; test **79/79** (+3: 47 bộ trạng thái và lọc key, danh mục từ cấu hình, user không quyền vẫn đọc được, chưa đăng nhập 401).

AC: [x] bảng đối chiếu màn hình → action · [x] test · [x] .http.

Còn lại / GAP: GAP-AUTH-01 (refresh token) vẫn mở. Danh mục tham chiếu và MealType chờ BA/PO chốt.

### T49 – Nạp dữ liệu danh mục ban đầu (Excel) · 2026-11-15 · DONE
Kế hoạch:
1. Rà soát: `ImportExcelController.Save` / `ImportService` cũ chỉ nạp TestDemo (sheet 1, tiêu đề = tên thuộc tính, không báo lỗi từng dòng, lưu file vào wwwroot). Đọc cấu trúc file thật: chỉ dòng tiêu đề, không chép dữ liệu.
2. `MasterDataImportService` + action trên `ImportExcelController`.
3. File mẫu từng danh mục.
4. Nạp file thật.
5. Test (InMemory + SQL Server).
6. Tài liệu.

Đã làm:
- `ImportExcel/GetTemplateList`, `GetTemplate?tableName=` (xlsx: tiêu đề tiếng Việt, cột * bắt buộc, ghi chú, 1 dòng ví dụ [DEMO]), `Import` (multipart).
- Quyền: cờ **Import** trên màn hình đích, theo UserRole (D-31).
- **11 file mẫu**: Customer, CustomerDeliveryLocation, Supplier, UnitMeasure, Ingredient, Warehouse, Employee, Vehicle, Dish, RecipeLine (định mức), SupplierMaterial (bảng giá NCC).
- **3 bố cục file thật**: Rule Menu – Ngân hàng món; Rule Menu – Chi tiết món và hao hụt, giá; Tổng hợp báo giá – Summary.
- Cách nạp:
  - Idempotent theo mã; món / nhóm từ file thật nhận diện theo tên, mã sinh không dấu.
  - Nhóm NL, nhóm món, ĐVT chưa có thì tạo.
  - Định mức tạo công thức Draft V1 nếu món chưa có công thức.
  - Ghi song song cột cũ (BaseUomId, CategoryId, EmployeeCode, UomId).
  - Kiểm luật như service (hao hụt, hạn dùng, giờ giao, tọa độ, CIF, đơn giá > 0, 1 NCC ưu tiên); kiểm độ dài theo [MaxLength].
  - Dòng lỗi bỏ qua và báo số dòng + lý do; dòng hợp lệ ghi trong 1 transaction.
  - File đọc từ stream, không lưu trên server.
- CIF trống → dùng mã khách (cột NOT NULL) – PROPOSED.

Kiểm tra:
- Build 0 lỗi / 10 cảnh báo.
- Test **85/85** (+6):
  - tải mẫu;
  - lỗi từng dòng + nạp lại không trùng;
  - chuỗi NL → món → định mức → bảng giá (NCC ưu tiên);
  - 3 bố cục file thật với dữ liệu [DEMO] tự tạo;
  - sai bố cục, sai loại, không quyền → 403;
  - **SQL Server**: dòng ví dụ của cả 11 mẫu nạp được vào DB thật (transaction + rollback).
- Không nạp dữ liệu thật; file tạm đã xóa.

AC: [x] nạp file mẫu thành công · [x] nạp lại không trùng · [x] dòng lỗi được báo rõ.

Còn lại / GAP:
- **GAP-IMP-01**: "Chi tiết món và hao hụt, giá" không có cột **định lượng** → không tạo dòng định mức. Phải nhập bằng mẫu RecipeLine. % hao hụt theo từng món (cột G/H) và giá cột O chưa nạp.
- **GAP-IMP-02**: SupplierMaterial không có cột VAT → % VAT trong báo giá không nạp.
- Sheet "Rule" (quy tắc thực đơn) và "DS NCC" (địa chỉ, điện thoại NCC) chưa nạp: để T13 / R1.2.

Cần anh: tự nạp file thật (UAT R1.1 mục 15); chốt cách xử lý GAP-IMP-01, ví dụ bổ sung cột định lượng vào file, hoặc lấy định lượng mặc định từ sheet Rule.

### Deploy staging sau lát R1.1 · 2026-11-15 · DONE
- `deploy/db/Deploy-Database.ps1` lên `AppDelicare_Staging` (LocalDB): giữ nguyên 124 bảng; 130 SP; seed tổ chức / menu (gồm 14 mục "Duyệt …") / nhóm quyền / ca.
- Publish Release, chạy ở chế độ Staging:
  - `/health` Healthy;
  - gọi không token → 401;
  - Swagger 747 route, 0 route `/api/v1`, có ImportExcel/Import, Recipe/Lock, MetaData.
- **Lỗi phát hiện khi deploy và đã sửa:** `CodeGen --merge` so tên phân biệt hoa thường, nên thêm `QCInspectionId` vào DTO đã có `QcInspectionId` (trùng tên camelCase → Swagger JSON lỗi).
  - Đã gỡ thuộc tính trùng; Merger so tên không phân biệt hoa thường.
  - Thêm test `DtoNamingTests`.
- Test 86/86.
- **Dừng lát R1.1 – chờ anh UAT-1** theo `docs/claude/UAT_CHECKLIST.md` mục R1.0 + R1.1 (`deploy/README.md` mục 3 để chạy staging).

### T45R.6 – Đổi tên bộ sinh T4 thành AppDelicare.Generator · 2026-11-15 · DONE (chờ anh chạy T4)
Nhánh `chore/T45R.6-rename-generator` (từ `feat/T45R-legacy-framework`).

Đã làm:
- `git mv` 20 file (giữ lịch sử): `AppDelicare/ClinicManagement.Generator` → `AppDelicare/AppDelicare.Generator`; 14 thư mục con `ClinicManagement.*` → `AppDelicare.*`.
  - Danh sách thư mục con: Data.Connection, Data.Interface, Data.Interface.UnitOfWork, Data.Repositories, Data.Repositories.UnitOfWork, Model.Entity, Model.Request, Model.SearchRequest, Model.ViewModels, Services.Implement, Services.Interface, WebApi.Controllers, WebApi.Infrastructure.
- Sửa tham chiếu:
  - `AppDelicare.sln`;
  - `AppDelicare.Generator.csproj` (RootNamespace, AssemblyName, Compile Include);
  - `Properties/AssemblyInfo.cs`;
  - `RunCopyFile.bat` (mọi dòng copy / del, giữ CRLF);
  - 14 `.tt`: Namespace và using `ClinicManagement.*` → `AppDelicare.*`, khớp namespace thật;
  - `OrmLite.Core.ttinclude`: `DefaultClinicManagementspace` → `DefaultNamespace` (dấu vết lần thay chữ "Name" cũ, chỉ 2 chỗ: định nghĩa + override);
  - `.dockerignore`, `CLAUDE.md` mục 2, `tools/AppDelicare.CodeGen/README.md`, `CODE_AUDIT` (A1, A2, A12).
- Không chạy T4, không sinh lại code, không đổi nội dung template ngoài tên / namespace.

Kiểm tra:
- Build 0 lỗi / 10 cảnh báo (= mốc); test 86/86.
- `RunCopyFile.bat`: 24 / 24 đường dẫn khớp thư mục thật (14 thư mục nguồn T4 + 10 thư mục đích).
- grep "ClinicManagement" trong AppDelicare, tools, docs/claude, CLAUDE.md = 0. Ngoại lệ:
  - log cũ (không theo dõi);
  - mô tả task T45R.6 trong ROADMAP;
  - D-32 trong DECISIONS;
  - câu lịch sử lần đổi tên 2026-11-14 trong CLAUDE.md.

Cần anh: mở solution bằng Visual Studio, chạy T4 một lần trên nhánh này, xác nhận file sinh ra đúng chỗ rồi mới merge.

### Theo CLAUDE.md 4.3 (anh bổ sung 2026-11-15): seed không ghi đè, MetaLink mục duyệt · DONE
Nhánh `fix/T45R-seed-approval-link` (từ `chore/T45R.6-rename-generator`).
- **Seed chỉ thêm phần chưa có**:
  - `ObjectMenu_seed.sql` bỏ `ELSE UPDATE` tên / thứ tự / cha;
  - `UserRole_seed.sql` bỏ khối `UPDATE ur` (trước đây chạy lại seed sẽ đưa quyền về ma trận đề xuất, ghi đè chỉnh sửa của admin);
  - `OrganizationUnit_seed.sql` giữ nguyên (ngoài phạm vi 4.3).
- **MetaLink mục duyệt** = hộp duyệt lọc theo loại: `#/wf-inbox?type=<screen-id>` (thay `#/approve/<screen-id>`).
  - `wf-inbox` là màn "Hộp duyệt" trong catalog.js. Vẫn 14 mục, 1 mục / màn có loại AP (PROPOSED – anh có thể gộp theo nhóm khi UAT-1).
  - Seed đổi MetaLink kiểu cũ tại chỗ, giữ dòng và quyền đã gán. Đã chạy trên DB dev và staging: 0 mục cũ, 14 mục mới; "Duyệt – Công thức" vẫn của CEO, COO.
- Test 87/87 (+1 SQL Server `Seeds_do_not_overwrite_admin_changes`: thu hồi quyền duyệt của CEO + đổi tên menu → chạy lại seed → giữ nguyên; rollback).

### T48.0 – Băm mật khẩu an toàn (D-29) · 2026-11-16 · DONE
Nhánh `feat/T48.0-password-hash` (từ `main`).

Rà soát code cũ:
- **Đăng nhập** so `MD5(mật khẩu)` trong SQL.
- **Tạo user / Reset** lưu `MD5(tên đăng nhập)`, tức mật khẩu khởi tạo = tên đăng nhập.
- **Lỗi cũ ở ChangePassword**: kiểm và lưu `MD5(mật khẩu + tên)`, nên sau khi đổi mật khẩu user **không đăng nhập lại được**. Ngoài ra đòi độ dài ≥ 9 trong khi thông báo ghi "từ 8 ký tự".

Đã làm (chỉ `EncriptFunctionsHelper`, `LoginService`, `UserService`, script deploy – không đổi API, bảng, cột):
- `GeneratePassword` = PBKDF2-SHA256, salt ngẫu nhiên 16 byte, 100.000 vòng, chuỗi `PBKDF2$100000$<salt>$<hash>` trong `User.PassWord` (nvarchar(max)).
- `VerifyPassword(mật khẩu, chuỗi lưu, tên, out needsUpgrade)`:
  - PBKDF2: so sánh bằng `FixedTimeEquals`;
  - MD5 cũ: chấp nhận `MD5(mk)` và `MD5(mk + tên)`, kèm cờ cần nâng.
- Đăng nhập: tìm theo tên rồi kiểm; hash cũ đúng → ghi PBKDF2 ngay; `GetClaimsIdentity` kiểm cùng cách. Không log mật khẩu / hash.
- Tạo / Reset / InsertUserAfter tự ghi PBKDF2 qua `GeneratePassword`.
- ChangePassword: kiểm mật khẩu cũ bằng `VerifyPassword`, lưu PBKDF2 của mật khẩu mới (sửa lỗi không đăng nhập lại được); tối thiểu 8 ký tự đúng thông báo.
- `Deploy-Database.ps1` tạo admin bằng PBKDF2 cùng định dạng (PowerShell 5.1); `00_Admin_seed.sql` sửa chú thích.

Kiểm tra:
- Build 0 lỗi / 10 cảnh báo.
- Test **92/92** (+5 `PasswordHashTests`):
  - MD5 cũ đăng nhập → nâng PBKDF2 → đăng nhập lần 2;
  - hash `MD5(mk + tên)` đăng nhập được;
  - sai mật khẩu bị từ chối (cũ / mới);
  - 2 user cùng mật khẩu có hash khác nhau;
  - hash do PowerShell sinh kiểm được bằng C#;
  - tạo / đổi / reset ghi PBKDF2, đổi xong đăng nhập bằng mật khẩu mới, < 8 ký tự bị từ chối.
- Trên SQL Server dev: admin đã tự nâng lên PBKDF2 sau lần đăng nhập của test tích hợp.

AC: [x] cả 4 mục.

Còn lại / cần anh:
- **GAP-ADM-02 chưa đủ**: reset vẫn đặt mật khẩu = tên đăng nhập và trả `Result<bool>` (giữ API theo T48.0). Muốn "mật khẩu tạm 12 ký tự trả một lần" thì phải đổi response của `User/ResetPassword` → cần anh duyệt.
- Mỗi lần đăng nhập tính PBKDF2 2–3 lần (~0,1–0,2 s), chấp nhận cho Pilot.

### FE-0 – Khung ứng dụng Angular (D-35, D-36) · 2026-11-16 · DONE (chờ thử tay)
Nhánh `feat/FE-0-app-shell` (từ `feat/T48.0-password-hash`). Chỉ sửa trong `app-delicare-client/`.

Mốc ban đầu: `npm run build` của bản cũ **lỗi sẵn 5 lỗi** ở `main/medical-examination`. Không sửa file đó; gỡ route nên module không còn được build.

Đã làm:
- MetaLink `#/<screen-id>` (cả `?type=`) xử lý một chỗ trong `helpers/common-helper.ts` (`menuPath`, `menuLink`, `findMenuByUrl`); `permission()` / `checkPermission()` dùng chung.
- Đăng nhập, phiên:
  - guard gọi `api/User/GetCurrent` khi tải lại trang;
  - sửa lỗi cũ "chưa đăng nhập vẫn qua guard" (`userValue` là `{}`);
  - 401 → về đăng nhập kèm `returnUrl`; 403 → màn hình tự báo.
- Route theo screen-id: `adm-user`, `adm-usergroup`, `adm-objectmenu`, `adm-person`, `adm-access-log`, `adm-audit`, `scm-supplier`, `scm-uom`. Đường dẫn cũ chuyển hướng sang các route này; mục menu chưa có màn hình → "Chức năng chưa có".
- Gỡ khỏi route (không xóa file): demo w3crm (`pages`, `components`, `plugins`), `patient*`, `medical-*`, `test-demo`, `delete-data` (D-26).
- Sidebar dựng từ `listMenu` (nhóm cấp 1 → màn cấp 2); ô chọn chi nhánh `api/OrganizationUnit/GetBranches`; bỏ nút sáng / tối.
- Tên DELI-CARE + tên pháp nhân (tab, footer, đăng nhập); không còn chữ "phòng khám" / "W3CRM" trên giao diện.
- Giao diện D-36 trong `assets/css/clinic.css` (không sửa `style.css`): màu, font Be Vietnam Pro (Google Fonts), cỡ 14 / 13 / 12px, dòng ≥ 44px, nút ≥ 36px.
- Thành phần dùng chung: `shared/page-header`, `shared/status-badge`, `shared/detail-drawer`; `services/meta-data.service.ts` (cache GetStatuses / GetReferenceLists).
- Màn mẫu `scm-uom` (unit-measure) theo khung mới.
- Đổi mật khẩu sửa 3 lỗi: báo thành công cả khi lỗi; đòi ≥ 9 ký tự; gửi sai trường.

Kiểm tra: `npm run build` **0 lỗi**, 1 cảnh báo có sẵn (`<link style.css>` trong index.html). **Chưa thử trên trình duyệt, chưa chụp ảnh trước / sau** (không có công cụ trình duyệt trong phiên này).

Cần anh / còn lại:
- Thử tay theo UAT (đăng nhập, tải lại, đổi mật khẩu, 401, sidebar, chi nhánh, scm-uom).
- DB dev còn **26 mục ObjectMenu của app cũ** với MetaLink kiểu cũ (`quan-ly-dang-nhap`, `quan-ly-vung`…) – vẫn hiện trên menu nếu nhóm có quyền. Em không tự tắt (dữ liệu của anh): anh chọn tắt (IsActive = 0) hay giữ.
- Font đang tải từ Google Fonts; muốn chạy nội bộ không mạng thì cần file woff2.
- Màn `administrative-*`, `orgchart` còn route cũ, ngoài menu: gỡ hay giữ?
- Chi nhánh mới lưu ở trình duyệt; màn nào cần lọc theo chi nhánh sẽ đọc giá trị này.
- Màn `scm-uom` mới đổi giao diện; form vẫn chỉ có trường Tên (Update ghi đè đủ trường → FE-2 bổ sung).

### FE-1 – Màn quản trị P0 · 2026-11-16 · DONE (chờ thử tay)
Nhánh `feat/FE-1-admin` (từ FE-0). Chỉ sửa trong `app-delicare-client/`.

Đã làm:
- Màn `adm-user` (rà soát màn cũ `main/user`): lọc, nhóm, khóa / mở, reset (chỉ admin), drawer "Quyền hiệu lực". Sửa lỗi cũ: user mới mặc định là admin.
- Màn `adm-usergroup`: popup phân quyền cũ thay bằng link sang `adm-permission`.
- Màn `adm-permission` (mới) – ma trận 7 cờ:
  - lưu theo dòng đã đổi qua `UserRole/Update`;
  - Cấp / Thu hồi tất cả, Sao chép từ nhóm, Xem trước (admin);
  - dòng duyệt `#/wf-inbox?type=` gắn nhãn "Duyệt".
- Màn `adm-objectmenu`: đổi sang dạng cây; kéo thả bằng CDK có sẵn + nút lên / xuống, lưu qua `UpdateOrder`.
- Màn `adm-orgunit` (mới, cây `GetTree`).
- Màn `adm-employee`, `adm-position` (mới).
- Màn `adm-person` đổi giao diện.
- Helper `toFormData` gửi đủ trường khi Update (Update ghi đè đủ trường).
- Service: user-role (5 action mới), object-menu (cây, sắp xếp), organization-unit, employee, position, company.

Kiểm tra: `npm run build` 0 lỗi (1 cảnh báo có sẵn). Kịch bản thử tay: UAT_CHECKLIST mục PILOT-FE (chưa thử trên trình duyệt).

Phát hiện → việc backend nhỏ (làm sau T13, ghi ở đây):
- `UserRole/SaveAccessAll` chỉ cấp menu cấp 2 → sidebar ẩn nhóm cấp 1. FE tạm gửi thêm Update bật nhóm.
- `User/GetAll` chưa có lọc IsActive (SP / SearchRequest).
- `ObjectMenu` cho đặt cha cho menu cấp 1 đang có con (thành 3 cấp) – cần chặn ở `UpdateOrder` / Update.

Cần anh:
- **Tạo user kèm hồ sơ trong 1 bước chưa làm**: `Person/Insert` đã tự tạo 1 user (tên tự sinh, chưa có nhóm), nên làm thêm sẽ ra 2 tài khoản. Hiện màn user chỉ chọn hồ sơ có sẵn – anh chọn cách xử lý.
- Danh sách loại đơn vị / trạng thái đơn vị lấy theo seed (PROPOSED), chưa có trong STATUS_CATALOG.
### T11 – Mã chứng từ (Pilot P1) · 2026-11-16 · DONE
Nhánh `feat/T11-document-number` (từ `feat/T48.0-password-hash`, làm trong worktree riêng song song FE-0).

Rà soát: chưa có service sinh mã; `RecipeService.Submit` tự đặt `RV-yyMMdd-HHmmssfff` (có thể trùng khi bấm đồng thời, sai mẫu XN).

Đã làm:
- `IDocumentNumberService.NextAsync(kind, date, suffix)` (`AppDelicare.Services`, Autofac tự đăng ký), đủ 10 loại BUSINESS_RULES §11:
  - PH (đơn, nhập, xuất, kiểm kê, đề nghị mua, lệnh SX, đóng gói, giao – dùng chung, không trùng giữa các bảng – PROPOSED);
  - XN, PA, PO, DN;
  - NL / ME / CX: hậu tố NCC / ca / xe, bắt buộc;
  - KH: `KH-YYMMDD`, phiên bản sau `-2`, `-3`;
  - VS: `VS-YYMMDD-<CA>`, lần sau `-2`.
- SQL Server:
  - `sp_getapplock` (Exclusive, LockOwner = Transaction) theo mã gốc;
  - `MAX(TRY_CAST(phần số))` gộp cột chuẩn + cột cũ (DEPRECATED_COLUMNS);
  - tên bảng / cột lấy từ metadata EF, giá trị là tham số, LIKE có escape;
  - bắt buộc gọi trong transaction đang mở (sau T45R, `UnitOfWork.OpenTransaction` đã bao EF).
- Không bảng đếm, không SEQUENCE.
- Hậu tố: bỏ dấu, chữ hoa, chỉ A–Z 0–9, tối đa 20 ký tự.
- `RecipeService.Submit` dùng mã `XN-` cho ApprovalRequest.RequestNo.

Kiểm tra:
- Build 0 lỗi.
- Test **105/105** (+13):
  - định dạng 10 loại;
  - tăng tiếp theo dữ liệu có sẵn (cả cột cũ `ReceiptNo`, PH chung nhiều bảng), đếm riêng theo hậu tố và theo ngày;
  - hậu tố bắt buộc;
  - **SQL Server: 50 transaction song song → 50 mã khác nhau, liên tục XN-991231-001..050** (dùng ngày 2099, xóa đúng 50 dòng test theo Id).

AC: [x] 50 lần song song không trùng · [x] đúng định dạng mọi loại.

GAP:
- `AttendanceEvent` không có cột mã → loại CC chưa có chỗ ghi (ngoài Pilot).
- Bảng có cột cũ NOT NULL (vd. `MaterialLot.LotNo`) → nơi tạo bản ghi phải ghi song song cả cột cũ (T15).

### T12 – Duyệt 1 cấp (Pilot P2) · 2026-11-16 · DONE(PILOT)
Nhánh `feat/T12-approval` (từ `feat/T11-document-number`, worktree riêng).

Rà soát:
- `ApprovalRequestService` / `Controller` chỉ có CRUD sinh mã – CRUD sửa được cả `RequestStatus` (duyệt "chui").
- Duyệt công thức tự cập nhật ApprovalRequest, không ghi ApprovalDecision.
- `ApprovalDecision.ApprovalStepId` NOT NULL nên cần ApprovalStep.

Đã làm (trong `ApprovalRequestService` cũ, gỡ header auto-generated):
- `IApprovalHandler` – registry theo BusinessType: BuildPayload / Describe / Approve / Reject; đăng ký trong `Ioc/ComponentRegistrar`; lấy handler lúc dùng qua IServiceProvider để tránh vòng phụ thuộc.
- `CreateAsync(businessType, businessId)`: gọi trong transaction của nghiệp vụ; mã `XN-` (T11) + ApprovalStep bước 1 + PayloadJson; chặn 2 yêu cầu Pending cùng chứng từ.
- `GetMyPending(type)`: chỉ loại người đăng nhập có quyền duyệt (mục `#/wf-inbox?type=<screen>`, D-31; admin thấy hết); kèm tiêu đề, người gửi.
- `Decide`, trong 1 transaction:
  - kiểm quyền duyệt;
  - từ chối bắt buộc lý do;
  - **so hash nội dung lúc gửi với nội dung hiện tại – khác thì không duyệt, không ghi gì**;
  - ghi ApprovalDecision (chỉ ghi thêm, PayloadHash SHA-256), cập nhật bước + yêu cầu;
  - thực thi handler (lỗi → rollback toàn bộ).
- CRUD `ApprovalRequest/Update` không đổi được trạng thái / nghiệp vụ / nội dung / mã.
- Action: `ApprovalRequest/GetMyPending`, `ApprovalRequest/Decide` (chỉ cần đăng nhập, service tự kiểm quyền).
- Công thức (RECIPE_VERSION, `RecipeApprovalHandler`): `Recipe/Submit` tạo yêu cầu qua engine; `Recipe/Approve` / `Reject` gọi `Decide`. Hành vi cũ giữ nguyên, nay có ApprovalDecision.

Kiểm tra:
- Build 0 lỗi.
- Test **110/110** (+5 ApprovalTests):
  - gửi → mã XN + bước + người có quyền thấy, người không quyền không thấy / không duyệt được;
  - duyệt → thực thi + ApprovalDecision;
  - từ chối cần lý do và không thực thi;
  - nội dung bị sửa → không duyệt, không ghi;
  - CRUD không đổi được trạng thái; Recipe/Approve đi qua engine.
- RecipeServiceTests cũ vẫn xanh.

AC (bản Pilot): [x] tạo → duyệt → thực thi · [x] từ chối không thực thi · [x] payload bị sửa thì không duyệt.

DEFERRED(PILOT): duyệt nhiều cấp theo ApproverRoleCode, hết hạn tự động (job T23).

Handler PO và đơn lệch > 30% làm cùng T15 / T13.

**Chưa cập nhật `DELICARE_API_Catalog.xlsx` (sheet API_Catalog)** cho T11 / T12 và các task trước: máy chưa có `openpyxl`, lệnh cài bị chặn – cần anh cho phép cài hoặc tự cập nhật.

### T13 + T46B.1 – Thực đơn tuần + Đơn hàng (Pilot P3) · 2026-11-16 · DONE(PILOT)
Nhánh `feat/T13-order-menu` (worktree `wt-T11`, từ T12).

Kế hoạch:
1. Rà soát Menu / MenuLine / MenuAssignment / Order / OrderLine / Contract / ContractMeal → LEGACY_REVIEW mục (c).
2. Mở rộng engine duyệt T12: đề xuất (proposal) đi kèm yêu cầu duyệt, tương thích ngược.
3. Thực đơn tuần: lịch + giá vốn + FC%, luật dòng thực đơn, Approve / Publish / Expire / Clone.
4. Phân công thực đơn: kiểm chồng hiệu lực, Resolve.
5. Đơn hàng: mẫu tạo đơn, Create, UpdateLines, máy trạng thái, giờ chốt 20h D-1, lệch suất > 30% → duyệt.
6. In "Thực đơn gửi khách" (.xlsx).
7. Test (InMemory + SQL Server rollback), tài liệu bàn giao, `.http`.

Rà soát: 7 bảng đều có CRUD sinh mã, chưa có nghiệp vụ; CRUD Menu / Order đổi được trạng thái, số, tổng tiền; OrderLine CRUD không tính lại tiền. `MenuLine` không có cột lựa chọn (GAP-MENU-01), `OrderLine` không có cột điểm giao (GAP-ORD-01). Chi tiết: LEGACY_REVIEW (c).

Đã làm (đều trong file cũ, đúng mẫu 4.2):
- **Engine duyệt (T12 mở rộng):** `IApprovalRequestService.CreateAsync(type, id, proposalJson = null)`; có đề xuất → `PayloadJson = {"state":"<nội dung>","proposal":"<đề xuất>"}` (lưu dạng chuỗi để hash đúng byte), hash chỉ trên state; không có đề xuất → định dạng T12. `IApprovalHandler.ApproveAsync(…, proposalJson)` là default method gọi bản cũ → `RecipeApprovalHandler` không phải sửa. Test T12 giữ xanh.
- **Thực đơn (`MenuService` / `MenuController`, `MenuLineService`):** `GetCalendar` (hàng = loại suất × lựa chọn × nhóm món, cột thứ 1–7, giá vốn / suất từng món theo công thức khóa, tổng + FC% theo (loại suất, lựa chọn, thứ), giá bán nhập tay hoặc tra theo khách); `Approve` (quyền duyệt `menu-menu`), `Publish` (đủ món mỗi thứ trong hiệu lực × loại suất đã gán), `Expire`, `Clone`; Insert luôn Draft, Update / Delete theo trạng thái; dòng thực đơn: thứ 1–7, lựa chọn 1–9, chỉ khi Draft / Approved, món chưa có công thức khóa → cảnh báo không chặn.
- **Phân công (`MenuAssignmentService` / `Controller`):** luật Insert / Update (hợp đồng thuộc khách + có loại suất, điểm giao thuộc khách, không chồng hiệu lực); `Resolve` (điểm giao → khách; chỉ thực đơn Approved / Published).
- **Đơn hàng (`OrderService` / `OrderController`):** `GetNewTemplate`, `GetDetail` (allowedActions, cutoffAt), `Create` (JSON – có dòng lồng nhau; 1 transaction; số `PH-YYMMDD-NNNN` qua `IDocumentNumberService.Voucher` – theo BUSINESS_RULES §11, không dùng `DH-` của spec; ghi song song OrderNo / OrderNumber, Amount / LineAmount; MenuId qua Resolve; UnitPrice qua `PricePolicyService.Resolve`; ca mặc định `BusinessRules:MealTypeDefaultShift`; bỏ dòng 0; chặn trùng đơn khách / điểm / ngày), `UpdateLines` (JSON), `Submit`, `Confirm`, `Lock`, `LockByMealDate` (nút thay job 20h), `Cancel` (lý do bắt buộc). CRUD Insert chuyển sang Create; Update chỉ Note / DeliveryDate đơn Draft; Delete chỉ Draft (kèm dòng); OrderLine CRUD ghi → lỗi nghiệp vụ.
- **Giờ chốt (PROPOSED, GAP-ORD-02):** ngày ăn D chốt lúc 20:00 ngày D-1 (giờ VN qua `IClock`, giờ từ `BusinessRulesOptions`). Từ giờ chốt: chặn Create / UpdateLines / Submit / Confirm / Cancel đơn Confirmed; cho Lock, Cancel đơn Draft / Submitted. `LockByMealDate` chỉ chạy khi đã tới giờ chốt, khóa đơn Confirmed, trả danh sách đơn chưa xác nhận + đơn còn yêu cầu lệch suất chờ duyệt.
- **Lệch suất (PROPOSED, GAP-ORD-03):** đơn Confirmed, dòng bất kỳ lệch (mới − cũ) / cũ (trị tuyệt đối) > 30% (`PortionDeviationPctNeedApproval`) → không ghi, tạo ApprovalRequest `ORDER_DEVIATION` (handler `OrderDeviationApprovalHandler`, màn `crm-order`) kèm đề xuất; duyệt → ghi số mới + tính lại tiền; từ chối → không đổi (ghi chú lý do vào đơn); đơn bị chốt / sửa sau khi gửi → không duyệt được (hash). Đúng 30% ghi ngay. Khi đang chờ duyệt không sửa tiếp được.
- **In "Thực đơn gửi khách" (T46B.1):** `Menu/ExportForCustomer` (.xlsx, ClosedXML) theo bố cục sheet "4. Menu_Sent" của file thật (chỉ lấy cấu trúc, không chép dữ liệu): tên / địa chỉ pháp nhân từ bảng `Company` (Menu.CompanyId, không có thì công ty đầu tiên), tiêu đề, dự án, hiệu lực, bảng Thứ × buổi (Món + Trọng lượng thành phẩm = `Dish.StandardServingSize`); nhiều lựa chọn → tiền tố "LC n:".
- **Quyền:** theo `PermissionConvention` (Get* = View, Create / Clone = Add, ExportForCustomer = Export, còn lại Edit). Khai báo rõ: `Menu/Approve` = Approve (`menu-menu`), `Menu/Publish`, `Expire` = Edit, `Menu/Clone` = Add, `Order/Confirm`, `Lock`, `LockByMealDate`, `Cancel` = Edit trên `crm-order` (thao tác vận hành, không phải duyệt – PROPOSED). Duyệt lệch suất = mục menu mới `#/wf-inbox?type=crm-order` "Duyệt – Đơn đặt suất ăn" (CodeGen `Seeds.ExtraApprovable`, sinh lại `ObjectMenu_seed.sql` / `UserRole_seed.sql`: chỉ thêm, nhóm CEO theo ma trận).
- Thông báo tiếng Việt mới trong `ConstantHelper.Message`; trạng thái trong `OrderStatus` / `MenuStatus` (`Model/Helper/OrderStatus.cs`).
- Test: `TestClock` (IClock điều khiển được, mặc định giờ thật) đăng ký trong `InMemoryApiFactory`.

File chính: `Services/Implement/{OrderService, MenuService, MenuLineService, MenuAssignmentService, OrderLineService, ApprovalRequestService}.cs`, `Services/Implement/Approval/OrderDeviationApprovalHandler.cs`, `Services/Interface/{IOrderService, IMenuService, IMenuAssignmentService, IApprovalHandler, IApprovalRequestService}.cs`, `WebApi/Controllers/{Order, Menu, MenuAssignment}Controller.cs`, `Model/Request/Custom/{Order, Menu}CustomRequest.cs`, `Model/Model/{Order, Menu}Models.cs`, `Model/Helper/OrderStatus.cs`, `Model/Option/BusinessRulesOptions.cs` + `appsettings.json` (`MealTypeDefaultShift`), `tools/AppDelicare.CodeGen/Seeds.cs`, `DB/seed/{ObjectMenu, UserRole}_seed.sql`, tài liệu `docs/frontend-handoff/P3.md`, `docs/api-tests/p3-menu-order.http`.

Kiểm tra:
- `dotnet build AppDelicare.sln --no-incremental`: 0 lỗi, 11 cảnh báo (= baseline worktree: 10 + 1 MSB3245 Generator).
- `dotnet test`: **123/123** xanh (+13): `Sales/OrderTests` (8: mẫu → tạo → gửi → xác nhận → chốt, số PH-, ghi song song cột cũ, tổng tiền; chuyển trạng thái sai; giờ chốt trước / đúng 20:00 D-1 + LockByMealDate; lệch ≤ 30% ghi ngay, > 30% → duyệt / từ chối; duyệt sau khi đơn bị chốt bị chặn; thứ tự tra giá hợp đồng → khách → ContractMeal; không có hợp đồng; tương thích ngược PayloadJson), `Menu/MenuCalendarTests` (4: lịch + giá vốn + FC%, cảnh báo món chưa có công thức, thứ ngoài 1–7; Approve / Publish / Expire / Clone; phân công + Resolve + chồng hiệu lực; file in mở được bằng ClosedXML), `Sales/OrderSqlServerTests` (1: SQL Server dev, create → submit → confirm → lock trong transaction ngoài rồi rollback – đã chạy, không bị bỏ qua).
- `git status`: không có thay đổi trong `app-delicare-client/`; không file mới trong `Controllers/V1/`, `Api/`, `AppDelicare.Application/`.

AC:
- [x] T13: test các nhánh cutoff và deviation; số tổng của đơn đúng; Submit / Confirm / Cancel (+ Lock) theo máy trạng thái; giá theo PricePolicie / ContractMeal.
- [x] T46B.1 (Pilot): thực đơn tuần buổi × lựa chọn + giá vốn / FC%; đơn theo khách → điểm giao; mẫu in Thực đơn gửi khách có test sinh file mở được.
- [ ] `DELICARE_API_Catalog.xlsx` chưa cập nhật (máy chưa có `openpyxl`) – các dòng spec 6.2, 6.3, 6.6 cần ghi "Đã xong" / "Một phần" + action thực tế (xem `docs/frontend-handoff/P3.md`).

DEFERRED(PILOT):
- Job 20:00 tự khóa đơn (dùng nút `Order/LockByMealDate`), job hết hạn hợp đồng.
- Sửa đơn sau giờ chốt qua duyệt `ORDER_CHANGE_AFTER_CUTOFF`; lệch suất so với trung bình 4 tuần cùng thứ của điểm giao.
- Dự báo (Forecast): số lượng gợi ý trong mẫu tạo đơn (= 0), lệch dự báo > 10% khi xác nhận; danh sách dạng kanban (`view=board`, `statusCounts`), `orderedMeals` / `assignedCustomerCount` trong danh sách thực đơn.
- Hợp đồng: Create kèm suất, Submit duyệt, Terminate (spec 6.1) – Pilot dùng CRUD Contract / ContractMeal, hợp đồng phải `Status = Active`.
- Yêu cầu đổi món (MenuChangeRequest, spec 6.4), đối soát (6.7 – T19).
- Đẩy thực đơn đã công bố qua Zalo cho khách.
- In thực đơn: logo (chưa có file), bản PDF.

Câu hỏi cho anh (DECISIONS GAP-MENU-01, GAP-MENU-02, GAP-ORD-01…03):
1. "Lựa chọn 1/2/3" lưu ở `MenuLine.SequenceNo` được không? Số suất dự kiến theo lựa chọn có cần lưu (chưa có cột)?
2. 1 đơn = 1 điểm giao (OrderLine không có cột điểm giao) – anh đồng ý?
3. Sau 20:00 D-1: chặn tạo / sửa / gửi / xác nhận / hủy đơn đã xác nhận; vẫn cho chốt và hủy đơn chưa xác nhận – đúng ý anh? Đơn chưa xác nhận lúc chốt thì xử lý thế nào (hiện chỉ báo danh sách)?
4. Lệch suất Pilot = so với số đã xác nhận (không phải trung bình 4 tuần) – tạm được không? Ai duyệt (seed hiện chỉ CEO theo ma trận; COO có cần)?
5. FC% tính trên giá bán chưa VAT từ chính sách giá / ContractMeal; `PortionQty` của dòng thực đơn có nhân vào giá vốn không?
6. Xác nhận đơn (Confirm) và Chốt (Lock) dùng quyền Sửa trên `crm-order` – hay cần tách quyền riêng?

Cần anh: chạy lại `DB/seed/ObjectMenu_seed.sql` + `UserRole_seed.sql` trên DB dev / staging (chỉ thêm mục "Duyệt – Đơn đặt suất ăn" và quyền CEO); cho phép cài `openpyxl` để cập nhật Catalog.

### T16 – Sổ kho (Pilot P4) · 2026-11-16 · DONE(PILOT)
Nhánh `feat/T16-inventory` (worktree `wt-T11`, từ T13).

Kế hoạch:
1. Rà soát InventoryTransaction / InventoryBalance / MaterialLot / MaterialIssue(+Line) / StockCount(+Line) / MaterialReservation / InventoryPolicie / Warehouse / StorageLocation → LEGACY_REVIEW mục (d).
2. Quy tắc lô dùng chung (chặn hết hạn / QC / tạm giữ, thứ tự FEFO / FIFO, cảnh báo) – lớp tĩnh `InventoryLotRules`, dùng chung transaction của service gọi.
3. Sổ cái: điều chỉnh tay, chuyển kho, sổ cái lọc khoảng ngày (SP mới), tính lại tồn; chặn CRUD ghi thẳng sổ / tồn.
4. Tồn: tổng hợp theo kho × nguyên liệu (xuất được, sắp hết hạn, mức tồn), theo lô (thứ tự FEFO, lý do chặn).
5. Phiếu xuất: gợi ý lô, Create (PH-), Post (ISSUE qua `InventoryLedgerService`), Cancel (đảo).
6. Kiểm kê: Create (dòng từ tồn), RecordCounts, Post (ADJUSTMENT; lệch > 2% chỉ admin).
7. Test InMemory + SQL Server (rollback), tài liệu bàn giao P4, `.http`.

Rà soát: cả 9 bảng chỉ có CRUD sinh mã; nghiệp vụ duy nhất là `InventoryLedgerService` (T08) + `InventoryTransaction/Reverse`. Lỗ hổng: `InventoryTransaction/Insert` ghi thẳng vào sổ (không cập nhật tồn); `InventoryBalance` CRUD sửa thẳng tồn; `MaterialLot/Update` đổi được QC KHONG_DAT → DAT và trạng thái; MaterialIssue / StockCount CRUD không số, không dòng, không ghi sổ, đổi trạng thái tùy ý; dòng phiếu sửa tùy ý. Chi tiết: LEGACY_REVIEW (d).

Đã làm (đều trong file cũ, đúng mẫu 4.2; thêm 1 lớp tĩnh dùng chung, không có service / controller mới):
- **`InventoryLotRules`** (`Services/Implement`, lớp tĩnh – không đăng ký DI): `BlockReason` (HSD < hôm nay giờ VN, QC `KHONG_DAT` / `REJECT` / `HOLD`, lô OnHold / Expired), `UseFefoAsync` (InventoryPolicie, không có → FEFO – D-24), `LoadAsync` (tồn theo dòng + lô + thứ tự xuất), `Suggest`, `IsEarlier` (cảnh báo), kiểm kho / vị trí / lô / đơn vị, phân bổ trên các vị trí.
- **Sổ cái (`InventoryLedgerService`)**: `RebuildBalancesAsync` (tính lại OnHand từ sổ theo (kho, vị trí, NL, lô), giữ Reserved, ghi song song cột cũ); chặn bút toán khác đơn vị dòng tồn (`UNIT_MISMATCH`). T11: thêm `InventoryTransaction.TransactionNumber / TransactionNo` vào nguồn MAX của mã `PH-`.
- **`InventoryTransactionService` / `Controller`**: `GetLedger` (SP mới `DB/sp/uspInventoryTransaction_GetLedger.sql` – đã chạy trên DB dev), `Adjust` (ADJUSTMENT, số PH-, lý do bắt buộc, ReferenceType ManualAdjustment), `Transfer` (TRANSFER_OUT / TRANSFER_IN cùng ReferenceId). CRUD Insert / Update / Delete → lỗi nghiệp vụ.
- **`InventoryBalanceService` / `Controller`**: `GetSummary`, `GetByLot`, `RebuildBalance` (`[AdminOnly]`). CRUD → lỗi nghiệp vụ.
- **`MaterialLotService` / `Controller`**: Insert (số lô bắt buộc hoặc sinh `NL-` theo mã NCC, Status Available / OnHold, ghi song song LotNo / QualityStatus), Update (giữ Status, chặn sửa QC không đạt, chặn đổi NL khi có giao dịch), Delete (chặn khi có giao dịch / tồn); mới `Hold`, `Release`.
- **`MaterialIssueService` / `Controller`**: `SuggestLots`, `Create` (JSON; chặn lô hết hạn / QC / tạm giữ / vượt tồn; cảnh báo lô nên xuất trước; dòng không chọn lô → tự phân bổ), `Post` (kiểm lại toàn bộ rồi ghi ISSUE, 1 transaction), `Cancel` (Draft → Hủy; Issued → bút toán đảo), `GetDetail`. CRUD: Insert → Create; Update chỉ Note / IssueDate phiếu Nháp; Delete chỉ Nháp. `MaterialIssueLine` CRUD → lỗi nghiệp vụ.
- **`StockCountService` / `Controller`**: `Create` (form; dòng từ tồn, Physical mặc định = System), `RecordCounts` (JSON; thêm dòng hàng ngoài sổ), `Post` (dòng lệch phải có lý do; lệch dòng > `StockCountVariancePctNeedApproval` → chỉ admin; ADJUSTMENT `ReferenceType = StockCount`; ApprovedBy / ApprovedAt = người ghi sổ), `GetDetail` (% lệch, vượt ngưỡng). CRUD: Insert → Create; Update chỉ Note / người phụ trách; Delete chỉ Draft / Counting. `StockCountLine` CRUD → lỗi nghiệp vụ.
- Hằng trạng thái `Model/Helper/InventoryStatus.cs` (MaterialLot, QC, MaterialIssue, StockCount, ReferenceType); request `Model/Request/Custom/InventoryCustomRequest.cs`; model `Model/Model/InventoryModels.cs`; thông báo tiếng Việt trong `ConstantHelper.Message`; `Results<T>.Success / Fail` (giống `Result<T>`).
- Quyền theo `PermissionConvention` (Get* = View, Create = Add, còn lại Edit); `InventoryBalance/RebuildBalance` = AdminOnly. Không thêm mục menu / seed.

File chính: `Services/Implement/{InventoryLotRules, InventoryLedgerService, InventoryTransactionService, InventoryBalanceService, MaterialLotService, MaterialIssueService, MaterialIssueLineService, StockCountService, StockCountLineService, DocumentNumberService}.cs`, `Services/Interface/I{InventoryLedger, InventoryTransaction, InventoryBalance, MaterialLot, MaterialIssue, StockCount}Service.cs`, `WebApi/Controllers/{InventoryTransaction, InventoryBalance, MaterialLot, MaterialIssue, StockCount}Controller.cs`, `Data/Repositories/Results.cs`, `Model/Helper/{InventoryStatus, ConstantHelper}.cs`, `Model/Request/Custom/InventoryCustomRequest.cs`, `Model/Model/InventoryModels.cs`, `DB/sp/uspInventoryTransaction_GetLedger.sql`, test `Tests/Inventory/{InventoryTests, InventorySqlServerTests}.cs`, tài liệu `docs/frontend-handoff/P4.md`, `docs/api-tests/p4-inventory.http`.

Kiểm tra:
- `dotnet build AppDelicare.sln --no-incremental`: 0 lỗi, 11 cảnh báo (= baseline worktree).
- `dotnet test`: **131/131** xanh (+8):
  - `Inventory/InventoryTests` (7): gợi ý FEFO bỏ lô hết hạn / KHONG_DAT / HOLD / tạm giữ + GetByLot + GetSummary; FIFO theo ngày nhận và FEFO theo HSD; phiếu xuất chặn lô hết hạn / REJECT / vượt tồn, cảnh báo FEFO, tự phân bổ, Post ghi ISSUE, Post thiếu tồn không ghi gì, lô bị tạm giữ sau khi lập phiếu; hủy phiếu đã xuất → đảo, tồn trở lại; chuỗi điều chỉnh / chuyển kho / xuất / đảo khớp sổ + RebuildBalance sửa tồn lệch; kiểm kê (lý do bắt buộc, lệch 20% người thường bị chặn – admin ghi được, lệch ~1% người thường ghi được, ADJUSTMENT đúng); CRUD cũ không phá sổ / lô.
  - `Inventory/InventorySqlServerTests` (1, SQL Server dev): nhập 3 lô, lô hết hạn bị chặn, Create FEFO với số PH- qua sp_getapplock, Post ghi ISSUE + cột cũ song song, thiếu tồn bị chặn, rollback không để lại dữ liệu, gọi SP GetLedger – đã chạy, không bị bỏ qua.
- `git status`: không có thay đổi trong `app-delicare-client/`; không file mới trong `Controllers/V1/`, `Api/`, `AppDelicare.Application/`.

AC (T16, bản Pilot):
- [x] Post IN / OUT / ADJUSTMENT / REVERSAL (+ TRANSFER) qua `InventoryLedgerService`; InventoryBalance tính lại (theo từng bút toán + `RebuildBalance`).
- [x] Gợi ý lô FEFO (mặc định) / FIFO theo `InventoryPolicie.UseFefo`; luôn chặn lô hết hạn, KHONG_DAT / REJECT / HOLD, tạm giữ; cảnh báo khi còn lô nên xuất trước.
- [x] Test FEFO, chặn, tồn sau chuỗi giao dịch, đảo bút toán.
- [~] Kiểm kê: ghi nhận + ghi sổ điều chỉnh xong; **duyệt lệch > 2% → DEFERRED(PILOT)** (Pilot: chỉ admin ghi sổ).
- [ ] Giữ chỗ (MaterialReservation) → DEFERRED(PILOT).
- [ ] `DELICARE_API_Catalog.xlsx` chưa cập nhật (không cài được `openpyxl`). Gợi ý: 7.5 lots = Một phần (thiếu trace); 7.6 summary / by-lot / transactions / rebuild / transfer / adjust = Đã xong; 7.7 = Một phần (suggest theo nguyên liệu, create / post / cancel; reservations chưa); 7.8 = Một phần (create / lines / post; submit duyệt chưa). Action thực tế: `docs/frontend-handoff/P4.md`.

DEFERRED(PILOT):
- Duyệt kiểm kê lệch > 2% (`PendingApproval`, ApprovalRequest STOCK_COUNT) và duyệt điều chỉnh tay (STOCK_ADJUSTMENT) qua engine T12.
- MaterialReservation (giữ chỗ theo lệnh SX, giảm ReservedQuantity khi xuất, Reserved → Issued) – làm cùng T14 / T17; gợi ý theo `productionOrderId` (spec 7.7) – T17.
- Job hết hạn lô hằng ngày (LotExpiryJob: Status Expired + WASTE) – T23. Pilot: lô hết hạn đã bị chặn theo HSD, hủy hàng bằng `Adjust` âm.
- `MaterialLot.Status = Consumed` khi hết tồn; truy xuất lô (`trace`, T18); quy đổi đơn vị khi xuất; phân trang `GetSummary`.

Câu hỏi cho anh (DECISIONS GAP-INV-02 … GAP-INV-11):
1. Phiếu xuất chưa gắn lệnh SX ghi `ProductionOrderId` = Guid rỗng – tạm được trong Pilot? (T17 sẽ gắn mẻ / lệnh.)
2. Lô chưa có kết quả QC (nhập tay / tồn đầu kỳ) cho xuất – đúng ý anh? `HOLD` có chặn xuất không (BUSINESS_RULES ánh xạ DAT_MOT_PHAN = HOLD, phần đạt được nhập)?
3. Không có chính sách tồn → FEFO; DB mặc định `UseFefo = 0` → khi tạo chính sách cho NL có hạn dùng có cần tự bật FEFO?
4. Kiểm kê: ngưỡng 2% xét từng dòng (lô) hay tổng phiếu / giá trị? Dòng chưa đếm coi như khớp – được không?
5. Điều chỉnh tay (tồn đầu kỳ, hủy hàng) Pilot chỉ cần quyền Sửa `scm-inv-txn`, chưa duyệt – được không?

Cần anh: chạy `DB/sp/uspInventoryTransaction_GetLedger.sql` trên staging (DB dev đã chạy); cho phép cài `openpyxl` để cập nhật Catalog.

### T14 – Tính nhu cầu NL + đề nghị mua nháp (Pilot P5) · 2026-11-16 · DONE(PILOT)
Nhánh `feat/T14-mrp` (worktree `wt-T11`, từ T16).

Kế hoạch:
1. Rà soát MaterialRequirement / ProductionPlan(+Line) / PurchaseRequest(+Line) / SupplierMaterial / Recipe / InventoryBalance → LEGACY_REVIEW mục (e).
2. Đơn Locked × thực đơn (MenuLine theo loại suất + thứ) → món × suất; món × công thức khóa (snapshot) × hao hụt → nhu cầu theo nguyên liệu (quy đổi về đơn vị cơ sở).
3. Trừ tồn xuất được (InventoryLotRules, tại ngày SX), cộng SafetyStock (D-12), NCC gợi ý + MOQ.
4. Ghi trong 1 transaction: kế hoạch neo KH-, dòng kế hoạch, MaterialRequirement (cập nhật tại chỗ), PurchaseRequest Draft PH- + dòng.
5. Đọc: GetByDate / GetDetail. Test InMemory (bộ số tính tay) + SQL Server rollback. Tài liệu P5, `.http`.

Đã làm (đúng mẫu 4.2, trong file cũ):
- **`MaterialRequirementService` / `MaterialRequirementController`**: `Calculate` (POST form `ProductionDate, WarehouseId?, OrganizationUnitId?, Note?`), `GetByDate` (GET `productionDate, organizationUnitId?`; chưa tính → `data = null`), `GetDetail` (GET `productionPlanId`). CRUD Insert / Update / Delete → lỗi nghiệp vụ.
- Thuật toán (PROPOSED, DECISIONS GAP-MRP-01…06): đơn **Locked** của ngày (đơn chưa chốt → cảnh báo); suất dòng đơn chia đều các lựa chọn (SequenceNo); công thức Locked / Obsolete hiệu lực tại ngày (SnapshotJson) → Locked + IsActive → Approved mới nhất (cảnh báo); nhu cầu = định lượng × suất ÷ suất chuẩn × (1 + hao hụt %) (WastePercent, trống = Ingredient.StandardWastePercent), quy đổi về `BaseUnitId` (thiếu quy đổi → cảnh báo + `incomplete`); khả dụng = AvailableQuantity của lô không bị chặn tại ngày SX (lô hết hạn trước ngày SX, QC không đạt, tạm giữ → `blockedQuantity`); thiếu = max(0, nhu cầu + SafetyStock − khả dụng − hàng sắp về (0)); mua = max(thiếu, MOQ) làm tròn lên; NCC ưu tiên → giá → lead time (cảnh báo không kịp ngày).
- Ghi: `ProductionPlan` Draft `KH-YYMMDD` (sp_getapplock của mã KH- tuần tự hóa 2 lần tính cùng ngày) + `ProductionPlanLine` theo món; `MaterialRequirement` theo nguyên liệu (`Shortage / Sufficient / Requested`, ghi song song cột cũ *Qty); `PurchaseRequest` Draft `PH-` + dòng (ghi song song `RequestNo`, `UomId`), NCC gợi ý trong Note. Chi tiết không có cột → JSON trong Note (GAP-MRP-02). Tính lại: cập nhật tại chỗ, dòng thừa xóa mềm; PR Draft giữ số; PR đã gửi duyệt / duyệt / lập PO → chặn; kế hoạch đã duyệt → chặn.
- Cấu hình `BusinessRules:MrpIncludeSafetyStock` (mặc định true, D-12). Trạng thái `Model/Helper/PurchasingStatus.cs`; model `Model/Model/MaterialRequirementModels.cs`; request `Model/Request/Custom/MaterialRequirementCustomRequest.cs`; thông báo tiếng Việt trong `ConstantHelper.Message`.
- Quyền theo `PermissionConvention` (`scm-requirement`: Get* = View, Calculate = Edit). Không thêm mục menu / seed. Không SP mới cho T14.

File T14: `Services/Implement/MaterialRequirementService.cs`, `Services/Interface/IMaterialRequirementService.cs`, `WebApi/Controllers/MaterialRequirementController.cs`, `Model/Helper/{PurchasingStatus, ConstantHelper}.cs`, `Model/Model/MaterialRequirementModels.cs`, `Model/Request/Custom/MaterialRequirementCustomRequest.cs`, `Model/Option/BusinessRulesOptions.cs`, `WebApi/appsettings.json`, test `Tests/Purchasing/{MrpTests, MrpSqlServerTests}.cs`, tài liệu `docs/frontend-handoff/P5.md`, `docs/api-tests/p5-mrp.http`.

Sửa nhỏ backend theo FE-2 (cùng nhánh, file riêng để commit riêng):
- Lọc cha trống: 7 SearchRequest `Guid` → `Guid?` + service đổi `Guid.Empty` → NULL (CustomerContact, CustomerDeliveryLocation, Contract, ContractMeal, SupplierMaterial ×2, InventoryPolicy, StorageZone). SP đã xử lý NULL – không sửa SP.
- Form đọc số theo InvariantCulture: `WebApi/Infrastructure/InvariantFormValueProviders.cs` + 1 dòng `Startup.cs` (GAP-FORM-01). Test giả lập máy chủ vi-VN: "12.5" → 12.5 (bỏ sửa thì test ra 125 – đã thử).
- `DB/sp/uspSupplier_GetAll.sql`, `DB/sp/uspUnitMeasure_GetAll.sql`: đủ cột ViewModel + lọc Status (+ CompanyId); `SupplierService` / `UnitMeasureService` truyền tham số; `UnitMeasureSearchRequest.Status` mới. **Đã chạy trên DB dev.**
- UserRole/SaveAccessAll cấp 1, User/GetAll IsActive, chặn menu cấp 3: **không làm ở nhánh này** – chuyển sang T45R.8 (theo điều phối).

Kiểm tra:
- `dotnet build AppDelicare.sln --no-incremental`: 0 lỗi, 11 cảnh báo (= baseline).
- `dotnet test`: **137/137** xanh (+6): `Purchasing/MrpTests` (3: bộ số tính tay – Thịt 24,75 kg / tồn 10 + 5 bị chặn / an toàn 2 / MOQ 20 → mua 20; Rau 18 kg, lô KHONG_DAT bị chặn, CT chỉ Approved → cảnh báo; Gạo 36 kg, tồn 50 000 g quy đổi 50 kg, an toàn 20 → mua 6, chưa có NCC; PR PH- 3 dòng, 2 270 000; tính lại sau nhập thêm gạo → cùng kế hoạch / PR, dòng gạo Sufficient, dòng PR gạo xóa mềm; PR PendingApproval → chặn; không có đơn chốt / kho sai / CRUD → 400; kế hoạch Approved → chặn), `Purchasing/MrpSqlServerTests` (1, SQL Server dev: Calculate + tính lại với KH- / PH- qua sp_getapplock trong transaction ngoài rồi rollback – đã chạy, không bị bỏ qua), `Common/FormCultureTests` (1), `MasterData/SearchFilterSqlServerTests` (1, SQL Server dev, chỉ đọc).
- `git status`: không có thay đổi trong `app-delicare-client/`; không file mới trong `Controllers/V1/`, `Api/`, `AppDelicare.Application/`.

AC (T14, bản Pilot):
- [x] MaterialRequirement từ đơn Locked × công thức đã khóa (snapshot) × hao hụt, quy đổi đơn vị.
- [x] Thiếu hụt so với tồn khả dụng (đã trừ giữ chỗ trên InventoryBalance, bỏ lô bị chặn) + SafetyStock (D-12).
- [x] PurchaseRequest nháp + gợi ý NCC (SupplierMaterial ưu tiên / giá / MOQ).
- [x] Test số liệu với bộ dữ liệu nhỏ tính tay được (+ SQL Server rollback).
- [ ] `DELICARE_API_Catalog.xlsx` chưa cập nhật (máy không có python / openpyxl). Gợi ý: 7.1 GET material-requirements = Đã xong (`MaterialRequirement/GetByDate`, `GetDetail`); calculate = Đã xong (`MaterialRequirement/Calculate`, theo ngày thay vì productionPlanId); create-purchase-request = Một phần (gộp vào Calculate, chưa bufferPercent / duyệt).

DEFERRED(PILOT):
- Job 20:30 (MRP) / 21:00 (đề nghị mua) – T23; Pilot bấm tay `Calculate`.
- Hàng sắp về từ PO (`ExpectedIncomingQuantity`) – sau T15; giữ chỗ `MaterialReservation` theo lệnh SX – T17.
- `bufferPercent`, chọn dòng khi lập đề nghị, duyệt PR 2 cấp, PR → PO – T15. Hao hụt 2 tầng, quy cách đóng gói.
- Lọc đơn theo chi nhánh (Order không có cột chi nhánh).

Câu hỏi cho anh (DECISIONS GAP-MRP-01…06, D-12):
1. MRP tự tạo kế hoạch sản xuất Draft `KH-` theo ngày (chưa theo ca) làm chỗ neo – được không? T17 sẽ tách ca từ kế hoạch này.
2. Suất chia đều cho các lựa chọn món (chưa có số suất theo lựa chọn) – tạm được? Hay tính đủ suất cho mọi lựa chọn (mua dư)?
3. Hao hụt: dùng (1 + WastePercent %) theo BUSINESS_RULES §3 hay chia cho (1 − hao hụt) / YieldPercent như spec 7.1 và Phiếu trước SX?
4. Chỉ đơn Đã chốt được tính; đơn Đã xác nhận chưa chốt chỉ cảnh báo – đúng ý anh?
5. Đề nghị mua đã gửi duyệt thì không cho tính lại – hay cho tính và tạo đề nghị bổ sung phần chênh?

Cần anh: chạy `DB/sp/uspSupplier_GetAll.sql`, `DB/sp/uspUnitMeasure_GetAll.sql` trên staging (DB dev đã chạy).

### T15 – Mua hàng → nhận hàng → QC đầu vào (Pilot P6) · 2026-11-16 · DONE(PILOT)
Nhánh `feat/T15-purchasing` (worktree `wt-T11`, từ T14). Chỉ backend (D-40).

Kế hoạch:
1. Rà soát PurchaseRequest / PurchaseOrder / GoodsReceipt / QCInspection (+Line), SupplierIssue, TemperatureLog, SupplierMaterial → LEGACY_REVIEW mục (f).
2. Đề nghị mua → PO: `ConvertToPurchaseOrder` theo NCC gợi ý (PO-), chặn CRUD phá trạng thái.
3. PO: UpdateLines, Submit → duyệt 1 cấp qua engine T12 (handler mới PURCHASE_ORDER), Send / Cancel / Close.
4. Phiếu nhập theo PO đã duyệt (PH-, nhiệt độ nhận) + phiếu QC; kết luận QC DAT / DAT_MOT_PHAN / KHONG_DAT.
5. Ghi sổ: lô NL-, nhập kho phần đạt (InventoryLedgerService), TemperatureLog RECEIVING, SupplierIssue, trạng thái PO.
6. In Đơn đặt hàng NCC + Phiếu trước sản xuất (.xlsx, bố cục file thật – chỉ cấu trúc).
7. Test InMemory + SQL Server (rollback); tài liệu P5.md, `.http`, DECISIONS (GAP-PUR-01…08).

Rà soát: cả 8 bảng chỉ có CRUD sinh mã – CRUD đổi được trạng thái / số / tổng tiền, dòng sửa tùy ý, phiếu nhập không ghi sổ. Lọc `SupplierId` của GetAll PO / phiếu nhập là `Guid` → danh sách luôn rỗng khi không lọc (đã sửa). Mục duyệt `#/wf-inbox?type=scm-po` đã có trong `ObjectMenu_seed.sql` và DB dev (CEO, COO) → **không cần seed mới**. Chi tiết: LEGACY_REVIEW (f).

Đã làm (đều trong file cũ, đúng mẫu 4.2; thêm 1 lớp tĩnh `PurchasingRules` + 1 handler duyệt, không có service / controller mới):
- **Đề nghị mua (`PurchaseRequestService` / `Controller`)**: `GetDetail` (dòng + NCC gợi ý + tiền ước tính + PO đã lập), `ConvertToPurchaseOrder` (form; Nháp / Đã duyệt; mỗi nguyên liệu chọn bảng giá đang hiệu lực tại ngày cần: ưu tiên → giá → lead time; thiếu NCC → lỗi kèm tên; mỗi NCC 1 PO Nháp `PO-YYMMDD-NNNN` (T11, ghi song song PONo / ExpectedDate / UomId / Amount); quy đổi sang đơn vị mua; dưới MOQ / chưa có giá → cảnh báo; đề nghị → Converted), `Cancel`. CRUD: Insert luôn Draft + số PH-; Update / Delete chỉ Draft. `PurchaseRequestLineService`: chỉ ghi khi đề nghị Draft, số lượng > 0, ghi song song UomId.
- **Đơn mua (`PurchaseOrderService` / `Controller`)**: `GetDetail` (dòng + đã nhận / đang chờ / còn lại, phiếu nhập, yêu cầu duyệt đang chờ, `needsDirectorApproval`, `allowedActions`), `UpdateLines` (JSON, Draft, mỗi nguyên liệu 1 dòng, đơn vị / giá mặc định theo bảng giá NCC, tính lại tổng), `Submit` (Draft → PendingApproval + `ApprovalRequest` PURCHASE_ORDER; tổng ≤ `PurchaseOrderAutoApproveMaxAmount` > 0 → tự Approved), `Approve` / `Reject` (gọi `ApprovalRequestService.Decide`; quyền Approve trên `scm-po` = mục "Duyệt – Đơn mua hàng"; hash nội dung = trạng thái + NCC + tổng + dòng; từ chối → về Draft), `Send`, `Cancel` (lý do; chưa có phiếu nhập), `Close`, `ExportOrder` (.xlsx theo sheet "2. Form_PO" của file thật). CRUD: Insert = Draft + PO- + tổng 0 (kiểm NCC), Update chỉ Draft, Delete chỉ PO Draft lập tay. `PurchaseOrderLine` CRUD ghi → lỗi. GetAll truyền thêm Status / PurchaseRequestId; `SupplierId` trống = không lọc.
- **Handler duyệt**: `Services/Implement/Approval/PurchaseOrderApprovalHandler` (BusinessType `PURCHASE_ORDER`, ScreenId `scm-po`, tiêu đề hộp duyệt "PO … – NCC – tổng tiền" + cảnh báo > 50 triệu theo D-11).
- **Phiếu nhập (`GoodsReceiptService` / `Controller`)**: `Create` (JSON; PO Approved / Sent / PartiallyReceived – PO chưa duyệt bị chặn; nhận ≤ còn lại + 10%; số `PH-YYMMDD-NNNN`; dòng: số nhận, đơn vị + đơn giá theo PO, số lô NCC, NSX / HSD (trống = nhận + ShelfLifeDays), nhiệt độ nhận; tạo QCInspection cùng số + QCInspectionLine; nguyên liệu `QcRequired = false` → tự DAT; cảnh báo nhiệt độ), `Post` (form; chỉ khi đã kết luận QC; mỗi dòng 1 lô `NL-YYMMDD-<mã NCC>-NN`, QcStatus = kết quả, KHONG_DAT → OnHold; RECEIPT qua `InventoryLedgerService` **chỉ số đạt**; TemperatureLog RECEIVING gắn lô, IsWithinRange theo ngưỡng; 1 SupplierIssue nếu có dòng không đạt / đạt một phần; phiếu → Posted; PO → PartiallyReceived / Received), `Cancel`, `GetDetail` (dòng + kết quả QC + nhiệt độ / ngưỡng / kết quả gợi ý + lô). CRUD Insert / Delete → lỗi, Update chỉ Note. `GoodsReceiptLine` CRUD ghi → lỗi. GetAll: NCC / PO / kho trống = không lọc.
- **QC đầu vào (`QcInspectionService` / `Controller`)**: `Conclude` (JSON; mọi dòng phải có kết quả; DAT_MOT_PHAN cần 0 < số đạt < số nhận + nhận xét; KHONG_DAT cần nhận xét; HSD trước ngày nhận → chỉ KHONG_DAT; nhiệt độ vượt ngưỡng mà chấp nhận → bắt buộc lý do; nhận cả PASS / HOLD / REJECT; ghi QC line + GoodsReceiptLine.QualityStatus / Accepted / Rejected + QCInspection.Result; phiếu → Accepted / PartiallyAccepted / Rejected; kết luận lại được khi chưa ghi sổ), `GetDetail`. CRUD Insert / Delete → lỗi, Update chỉ Note / Remark. `QcInspectionLine` CRUD ghi → lỗi.
- **In Phiếu trước sản xuất**: `MaterialRequirement/ExportPreProduction?id=<planId>` (.xlsx theo `Phieu_Truoc sx.xlsx`: số suất order, món × suất, nguyên liệu cần / tồn xuất được / bị chặn / tồn an toàn / Đủ – Thiếu / SL đề nghị mua / NCC gợi ý, cột ghi tay sơ chế + chất lượng). Chỉ đọc dòng tiêu đề của file thật, không chép dữ liệu.
- Cấu hình `BusinessRules` (PROPOSED): `PurchaseOrderAutoApproveMaxAmount` 0, `PurchaseOrderDirectorApprovalAmount` 50 000 000, `GoodsReceiptOverReceivePct` 10, `ReceivingTempMaxC` { CHILLED 5, FROZEN −18 }, `SupplierIssueDueDays` 2. `ReferenceLists:QcResult` đổi sang DAT / DAT_MOT_PHAN / KHONG_DAT.
- Hằng trạng thái `PurchaseOrderStatus`, `GoodsReceiptStatus`, `IncomingQcResult`, `ReceivingCode` (`Model/Helper/PurchasingStatus.cs`); request `Model/Request/Custom/PurchasingCustomRequest.cs`; model `Model/Model/PurchasingModels.cs`; thông báo tiếng Việt trong `ConstantHelper.Message`.
- Quyền theo `PermissionConvention`: Get* = View; `GoodsReceipt/Create` = Add; `ExportOrder`, `ExportPreProduction` = Export; còn lại (ConvertToPurchaseOrder, UpdateLines, Submit, Send, Cancel, Close, Post, Conclude) = Edit trên màn tương ứng; `PurchaseOrder/Approve`, `Reject` = Approve `scm-po` (khai báo rõ). Không thêm mục menu / seed / SP.

File chính: `Services/Implement/{PurchaseRequestService, PurchaseRequestLineService, PurchaseOrderService, PurchaseOrderLineService, GoodsReceiptService, GoodsReceiptLineService, QCInspectionService, QcInspectionLineService, MaterialRequirementService, PurchasingRules}.cs`, `Services/Implement/Approval/PurchaseOrderApprovalHandler.cs`, `Services/Interface/I{PurchaseRequest, PurchaseOrder, GoodsReceipt, QcInspection, MaterialRequirement}Service.cs`, `WebApi/Controllers/{PurchaseRequest, PurchaseOrder, GoodsReceipt, QcInspection, MaterialRequirement}Controller.cs`, `Model/Helper/{PurchasingStatus, ConstantHelper}.cs`, `Model/Option/BusinessRulesOptions.cs`, `Model/Request/Custom/PurchasingCustomRequest.cs`, `Model/Model/PurchasingModels.cs`, `Model/SearchRequest/{PurchaseOrder, GoodsReceipt}SearchRequest.cs`, `WebApi/appsettings.json`, test `Tests/Purchasing/{PurchasingTests, PurchasingSqlServerTests}.cs`, tài liệu `docs/frontend-handoff/P5.md` (mục 4–9), `docs/api-tests/p6-purchasing.http`.

Kiểm tra:
- `dotnet build AppDelicare.sln --no-incremental`: 0 lỗi, 11 cảnh báo (= baseline).
- `dotnet test`: **143/143** xanh (+6):
  - `Purchasing/PurchasingTests` (5, InMemory): lập PO theo NCC ưu tiên (thắng NCC rẻ hơn), số `PO-261220-NNNN`, tổng 2 150 000 / 450 000, cảnh báo MOQ, đề nghị → Converted, không lập lại / không sửa dòng, PO từ đề nghị không xóa, UpdateLines tính lại tổng + chặn trùng nguyên liệu, nguyên liệu chưa có NCC → lỗi kèm tên; duyệt PO: phiếu nhập trên PO Nháp / Chờ duyệt bị chặn, yêu cầu `XN-` PURCHASE_ORDER, người không có quyền không thấy / không duyệt (Decide 400, Approve 403), người có quyền mục `#/wf-inbox?type=scm-po` thấy tiêu đề và duyệt → Approved + ApprovalDecision (hash 64 ký tự), PO bị sửa sau khi gửi → không duyệt được, từ chối cần lý do → về Nháp, in Đơn đặt hàng mở được, Send, Cancel; nhận hàng: vượt 10% bị chặn, số `PH-`, cảnh báo + gợi ý KHONG_DAT khi vượt nhiệt độ, ghi sổ trước QC bị chặn, 5 nhánh lỗi kết luận QC, ghi sổ → 3 lô `NL-261220-SAPUR3-NN`, chỉ nhập Thịt 10 + Rau 15 (DAT_MOT_PHAN), Cá KHONG_DAT không nhập + lô OnHold, TemperatureLog RECEIVING ×3 (Rau ngoài ngưỡng, có hành động khắc phục), SupplierIssue High / TEMPERATURE / hạn +2 ngày, PO Nhận một phần, **xuất kho lô KHONG_DAT bị chặn, lô DAT_MOT_PHAN chỉ xuất được 15**, lô KHONG_DAT không sửa được thành DAT, nhận bù → Received → Close → không nhận thêm; CRUD cũ không phá máy trạng thái; MRP → đề nghị → PO 2 000 000 + in Phiếu trước sản xuất (SL cần 40, thiếu 40, NCC gợi ý) + MRP không tính lại khi đề nghị đã lập PO.
  - `Purchasing/PurchasingSqlServerTests` (1, SQL Server dev): PR → PO (PO- qua sp_getapplock) → Submit (XN-) → Approve qua engine → phiếu nhập PH- → QC DAT_MOT_PHAN 8 / 10 → ghi sổ: lô NL-, RECEIPT 8, tồn 8, TemperatureLog 1, PO PartiallyReceived, ApprovalDecision APPROVE – trong transaction ngoài rồi rollback, không để lại dữ liệu (đã chạy, không bị bỏ qua).
- `git status`: không có thay đổi trong `app-delicare-client/`; không file mới trong `Controllers/V1/`, `Api/`, `AppDelicare.Application/`; không SP / seed mới (không cần chạy script nào trên DB dev / staging).

AC (T15, bản Pilot):
- [x] PR → PO (gộp theo NCC, giá từ SupplierMaterial) → GoodsReceipt (PH-) → QC DAT / DAT_MOT_PHAN / KHONG_DAT → MaterialLot `NL-…` → InventoryTransaction IN (chỉ phần đạt).
- [x] PO duyệt 1 cấp qua engine (quyền qua mục menu, hash nội dung); phiếu nhập trên PO chưa duyệt bị chặn; mã PO- / PH- / NL- đúng định dạng.
- [x] Lô KHONG_DAT không xuất được (MaterialIssue bị chặn); DAT_MOT_PHAN chỉ nhập phần đạt.
- [x] SupplierIssue khi không đạt / đạt một phần; TemperatureLog RECEIVING (ngưỡng mát / đông lạnh PROPOSED).
- [x] In Đơn đặt hàng NCC + Phiếu trước sản xuất (.xlsx, test mở được).
- [x] 1 test SQL Server rollback cho toàn luồng PR → PO → GR → QC → tồn.
- [ ] `DELICARE_API_Catalog.xlsx` không sửa (theo điều phối). Gợi ý cột J / L (cột M = T15): 7.2 GET purchase-requests = Đã xong (`PurchaseRequest/GetAll`, `GetDetail`); POST = Đã xong (`PurchaseRequest/Insert` + `PurchaseRequestLine/Insert`); submit = Không làm (Pilot – GAP-PUR-06); convert-to-po = Đã xong (`PurchaseRequest/ConvertToPurchaseOrder`). 7.3 approve = Đã xong (`PurchaseOrder/Submit` + `Approve` / `Reject`, 1 cấp); send = Một phần (`PurchaseOrder/Send`, chưa gửi email / Zalo); receive = Đã xong (`GoodsReceipt/Create`); close / cancel = Đã xong (`PurchaseOrder/Close`, `Cancel`). 7.4 GET goods-receipts = Đã xong (`GoodsReceipt/GetAll`, `GetDetail`); post = Đã xong (`GoodsReceipt/Post`). 9.1 GET inspections = Đã xong (`QcInspection/GetAll`, `GetDetail`); conclude = Đã xong (`QcInspection/Conclude`). 9.3 temperature-logs = Một phần (ghi RECEIVING khi nhập hàng; series / IoT chưa). 9.5 supplier-issues = Một phần (CRUD + tự tạo khi QC; transition chưa).

DEFERRED(PILOT):
- Duyệt đề nghị mua 2 cấp (spec 7.2 submit), duyệt PO 2 cấp theo ngưỡng D-11 (> 50 triệu GĐ) – engine T12 mới 1 cấp.
- Gửi PO cho NCC qua email / Zalo (OutboundMessage), bản PDF; VAT trên mẫu in (GAP-PUR-07); logo.
- Trả hàng NCC / hủy lô KHONG_DAT (chứng từ trả hàng, bút toán RETURN), đảo phiếu nhập đã ghi sổ; chuyển trạng thái sự cố NCC (`transition`, spec 9.5).
- Hàng sắp về từ PO cho MRP (`ExpectedIncomingQuantity` – T14 vẫn = 0); vị trí kho theo `Ingredient.StorageType` khi ghi sổ (Pilot: chọn `StorageLocationId` hoặc trống); mã thiết bị đo nhiệt độ; so sánh báo giá nhiều NCC (sheet `Price_Compare` của file thật).

Câu hỏi cho anh (DECISIONS GAP-PUR-01…08, D-11):
1. Pilot không bắt buộc duyệt đề nghị mua (chỉ duyệt PO 1 cấp) – được không? PO > 50 triệu có cần GĐ duyệt thêm ngay trong Pilot?
2. PO bị từ chối → về Nháp để sửa gửi lại (thay vì trạng thái Từ chối) – đúng ý anh? Cho nhận hàng khi PO mới "Đã duyệt" (chưa bấm "Đã gửi NCC") – được không?
3. Hàng không đạt (KHONG_DAT / phần trả của DAT_MOT_PHAN) không nhập kho, chỉ tạo lô tạm giữ để truy xuất và ghi sự cố NCC – hay anh muốn nhập vào kho "chờ trả" rồi xuất trả?
4. Ngưỡng nhiệt độ nhận: mát ≤ 5 °C, đông lạnh ≤ −18 °C, hàng khô / thường không kiểm – đúng chưa? Vượt ngưỡng mà vẫn nhận (ghi lý do) có được không hay bắt buộc KHONG_DAT?
5. Dung sai nhận vượt 10% / dòng PO có phù hợp (rau, thịt cân lệch)?
6. In Đơn đặt hàng: có cần cột VAT (mẫu thật có % VAT theo NCC – chưa có cột trong DB)?

### T14.1 – MRP: dự phòng lựa chọn + tính bổ sung (anh chốt 2026-11-16) · 2026-11-16 · DONE(PILOT)
Nhánh `feat/T14.1-mrp-buffer` (worktree `wt-T11`, từ T15). Chỉ backend + tài liệu.

Anh chốt 2026-11-16 (ghi ở DECISIONS mục "Anh chốt 2026-11-16", GAP-MRP-01 / 03 / 05 / 06 đánh dấu "Anh chốt 2026-11-16"): kế hoạch nháp `KH-` theo ngày; chia đều lựa chọn + dự phòng 15% / lựa chọn; hao hụt giữ (1 + %); chỉ đơn Locked; đề nghị mua đã đi tiếp → tính lại vẫn chặn + thêm tính bổ sung; T45R.8: IsActive mặc định không hoạt động, ChangePassword giữ hành vi cũ.

Đã làm (trong file cũ, đúng mẫu 4.2; không bảng / cột / SP / seed mới):
- **Dự phòng lựa chọn:** `BusinessRules:MrpOptionBufferPct` (mặc định 15, 0 = tắt; `BusinessRulesOptions` + `appsettings.json`). Thực đơn có ≥ 2 lựa chọn cho loại suất: mỗi món = suất ÷ số lựa chọn × (1 + 15%); 1 lựa chọn không cộng. PROPOSED: món có trong nhiều lựa chọn (cơm chung) không vượt số suất dòng đơn (món chung mọi lựa chọn không cộng dự phòng). `listDish[].bufferPortions` mới (phần dự phòng, đã nằm trong `portions`).
- **`MaterialRequirement/CalculateTopUp`** (POST form `ProductionDate, WarehouseId?, OrganizationUnitId?, Note?`; quyền Edit `scm-requirement` theo PermissionConvention): cùng khóa ngày với Calculate; kế hoạch phải có + còn Nháp; cần ≥ 1 đề nghị của kế hoạch đã Chờ duyệt / Duyệt / Lập PO (còn Nháp → dùng Calculate). Tính lại nhu cầu; đã đề nghị = Σ dòng các đề nghị chưa Hủy / Từ chối (chính + bổ sung trước) − đã nhận đạt (phiếu nhập Đã ghi sổ của PO lập từ các đề nghị đó); chênh lệch dương → **1 đề nghị mua Nháp `PH-` mới** chỉ cho phần chênh (MOQ + NCC gợi ý như Calculate; Note "BỔ SUNG – … bổ sung cho PH-…"); không thiếu thêm → không lập, thông báo; âm → cảnh báo. Cập nhật tại chỗ dòng nhu cầu (`PurchaseQuantity` = đã đề nghị + bổ sung; nguyên liệu hết nhu cầu giữ dòng nhu cầu 0) + dòng kế hoạch; liên kết đề nghị bổ sung trong `ProductionPlan.Note` (`TopUpPurchaseRequestIds`) – GAP-MRP-08 (PROPOSED). 1 transaction, `RequireActor`.
- Calculate: thông báo chặn chỉ hướng sang CalculateTopUp; kế hoạch đã có đề nghị bổ sung còn hiệu lực → chặn (tránh lập trùng). Kết quả GetByDate / GetDetail / Calculate thêm `listTopUpPurchaseRequest[]`; CalculateTopUp thêm `topUp` (`purchaseRequest`, `listComparedRequestNumber`, `listLine[]` thiếu / đã đề nghị / chênh lệch / bổ sung / NCC, `listWarning`).
- Tách phần tính chung của Calculate thành `ComputeAsync` / `PriceAsync` / `UpsertPlanLines` / `SoftDeleteAsync` (cùng service) để Calculate và CalculateTopUp dùng chung – số liệu Calculate không đổi khi dự phòng = 0.

File: `Services/Implement/MaterialRequirementService.cs`, `Services/Interface/IMaterialRequirementService.cs`, `WebApi/Controllers/MaterialRequirementController.cs`, `Model/Model/MaterialRequirementModels.cs`, `Model/Helper/ConstantHelper.cs`, `Model/Option/BusinessRulesOptions.cs`, `WebApi/appsettings.json`, test `Tests/Purchasing/MrpTests.cs`, tài liệu `docs/claude/{DECISIONS, BUSINESS_RULES, PROGRESS}.md`, `docs/frontend-handoff/P5.md`, `docs/api-tests/p5-mrp.http`. Không đụng MaterialIssue* / ProductionPlan* / ProductionBatch* / ProductionOrder* service (T17 song song).

Kiểm tra:
- `dotnet build AppDelicare.sln --no-incremental`: 0 lỗi, 11 cảnh báo (= baseline).
- `dotnet test`: **145/145** xanh (+2, `Purchasing/MrpTests`): (1) 2 lựa chọn 100 suất → món A = B = 50 × 1,15 = 57,5 (dự phòng 7,5, NL 5,75 kg), cơm chung = 100, thực đơn 1 lựa chọn TOI 40 không dự phòng; cấu hình 0 → 50; (2) Calculate → đề nghị 1 thịt 10 → bổ sung khi còn Nháp bị chặn → gửi duyệt + tăng đơn 100 → 130 → Calculate chặn, CalculateTopUp lập đề nghị Nháp mới chỉ thịt 3 (300 000), đề nghị 1 không đổi, dòng nhu cầu 13 / mua 13 → gọi lại không lập → giảm còn 110 → chỉ cảnh báo thừa 2 → hủy đề nghị 1 nhưng còn đề nghị bổ sung → Calculate chặn. Test T14 cũ chạy với dự phòng 0 (giữ bộ số tính tay).

PROPOSED (chờ anh xác nhận – GAP-MRP-03 / 08):
1. Món chung nhiều lựa chọn không vượt số suất dòng đơn (không cộng dự phòng cho cơm / canh chung).
2. "Đã đề nghị" trừ phần đã nhận đạt (đã nằm trong tồn); đề nghị Hủy / Từ chối không tính; PO bị hủy không tự trừ.
3. Calculate bị chặn khi kế hoạch đã có đề nghị bổ sung còn hiệu lực; bổ sung chỉ khi kế hoạch còn Nháp.
4. Thừa do làm tròn MOQ cũng hiện cảnh báo "thừa" (theo đúng ý "âm → cảnh báo").
Release 2: suất theo từng lựa chọn (đặt trước / tỉ lệ 4 tuần) cần cột mới – GAP-MRP-07.

### T15.1 – Quy tắc mua hàng anh chốt 2026-11-16 (nhiệt độ 3 vùng, GĐ duyệt PO ≥ 50 triệu, VAT phương án A) · 2026-11-16 · DONE(PILOT)
Nhánh `feat/T15.1-po-rules` (worktree `wt-T11`, từ T15 + T14.1). Chỉ backend + seed + tài liệu. Không đụng MaterialIssue* / Production* / MaterialRequirementService.

Kế hoạch: (1) cấu hình `ReceivingTemp` 3 vùng thay `ReceivingTempMaxC`, kiểm ở QcInspection/Conclude, đánh dấu + sự cố NCC ở GoodsReceipt/Post; (2) engine duyệt: mục quyền theo từng yêu cầu (hook mặc định trong `IApprovalHandler`), PO ≥ ngưỡng → `scm-po-director`; seed mục menu + quyền CEO (CodeGen + seed khớp nhau), chạy seed DB dev; (3) VAT tính khi đọc / in; (4) test; (5) tài liệu.

Đã làm (trong file cũ, đúng mẫu 4.2; không bảng / cột / SP mới):
- **Nhiệt độ nhận 3 vùng** – `BusinessRulesOptions.ReceivingTemp` (`ReceivingTempRule { AcceptMax, ConditionalMax, RequireNoThawConfirmation }`: CHILLED 5 / 8, FROZEN −18 / −15 + xác nhận rã đông; `ReceivingTempMaxC` bỏ hẳn, appsettings đổi theo). `PurchasingRules`: `ReceivingTempRuleOf`, `TempZone` (ACCEPT / CONDITIONAL / REJECT / MISSING / NOT_CHECKED – hằng `ReceivingTempZone`), `SuggestResult(zone, …)`, `ConditionalNote`. `QcInspection/Conclude`: khi kết luận DAT / DAT_MOT_PHAN – chưa đo (mát / đông lạnh) → lỗi; vùng loại → lỗi (chỉ KHONG_DAT); vùng có điều kiện → bắt buộc `remark`, đông lạnh thêm `noThawConfirmed` (request mới); ghi `QCInspectionLine.Note` = `[UU_TIEN_DUNG_TRUOC] Nhận có điều kiện …`. `GoodsReceipt/Create`: cảnh báo theo vùng; `QcRequired = false` chỉ tự DAT khi vùng nhận / không kiểm. `GoodsReceipt/Post`: lô nhận có điều kiện → Note thêm cờ ưu tiên dùng trước; dòng có điều kiện (kể cả DAT) luôn vào `SupplierIssue` (TEMPERATURE). Chi tiết phiếu nhập thêm `temperatureZone(Name)`, `conditionalMaxTemperature`, `noThawConfirmationRequired`, `useFirst`.
- **GĐ duyệt PO ≥ 50 triệu** – `IApprovalHandler` thêm 2 member mặc định: `ApprovalScreenIds` (= [ScreenId]) và `RequiredScreenIdAsync(businessId)` (= ScreenId) → handler cũ (công thức, đơn lệch) không đổi. `ApprovalRequestService.GetMyPending` lọc theo mục quyền của **từng yêu cầu** (type = mục đó), `Decide` kiểm Approve trên mục đó (ApprovalNoRight). `PurchaseOrderApprovalHandler`: mục `scm-po` / `scm-po-director` theo `PurchaseOrderService.ApprovalScreenIdAsync` (tổng ≥ `PurchaseOrderDirectorApprovalAmount`). `PurchaseOrder/Approve` / `Reject`: bỏ `[Permission("scm-po", Approve)]` → `[SkipPermission]` (engine kiểm, như `ApprovalRequest/Decide`) – người không quyền nhận 400 thay vì 403. Submit: PO lớn không tự duyệt, thông báo "cần Giám đốc duyệt". GetDetail: `needsDirectorApproval` (≥), `approvalScreenId`.
- **Seed**: `tools/AppDelicare.CodeGen/Seeds.cs` thêm `ExtraApprovals` (`scm-po-director`, "Duyệt – Đơn mua hàng (Giám đốc)", P4, nhóm CEO) → sinh lại `DB/seed/ObjectMenu_seed.sql` (+1 mục OrderShow 9916) và `UserRole_seed.sql` (+ CEO AllowAccess / AllowEdit) bằng `dotnet run --project tools/AppDelicare.CodeGen -- --layers security` (chỉ 2 file seed đổi, đúng phần thêm). **Đã chạy 2 seed trên DB dev `(localdb)\MSSQLLocalDB` / `AppDelicare`** (insert-only, idempotent): có mục `#/wf-inbox?type=scm-po-director`, CEO = 1 / 1.
- **VAT phương án A** – `BusinessRulesOptions.PurchaseVat { DefaultPct = 8, ByIngredientCategoryCode = {} }` (appsettings có ví dụ PROPOSED `RAU: 5` trong chú thích). `PurchasingRules.VatPctByIngredientAsync` (mã nhóm NL → nhóm cha ≤ 5 cấp → mặc định), `VatAmount` (làm tròn đồng). `PurchaseOrder/GetDetail`: `listLine[].vatPct / vatAmount / amountInclVat`, `subtotalAmount / vatAmount / totalAmountInclVat`. `ExportOrder`: cột % VAT + Tiền VAT, chân bảng chưa VAT / thuế VAT / gồm VAT. Không ghi DB.
- Tài liệu: DECISIONS (mục "Anh chốt 2026-11-16 (T15 mua hàng → T15.1)", ý 7 T14.1, D-11 đã chốt, GAP-PUR-02/03/04/07/08 cập nhật, **GAP-PUR-09** (cột VAT → R2, cần anh duyệt migration), **GAP-PUR-10** (cờ ưu tiên dùng trước trong Note), GAP-MRP-03 / 08 chốt), BUSINESS_RULES §4, `docs/frontend-handoff/P5.md` mục 10, `docs/api-tests/p6-purchasing.http` (21–24).

File: `Model/Option/BusinessRulesOptions.cs`, `Model/Helper/{PurchasingStatus, ConstantHelper}.cs`, `Model/Model/PurchasingModels.cs`, `Model/Request/Custom/PurchasingCustomRequest.cs`, `Services/Interface/{IApprovalHandler, IPurchaseOrderService}.cs`, `Services/Implement/{ApprovalRequestService, PurchaseOrderService, GoodsReceiptService, QCInspectionService, PurchasingRules}.cs`, `Services/Implement/Approval/PurchaseOrderApprovalHandler.cs`, `WebApi/Controllers/PurchaseOrderController.cs`, `WebApi/appsettings.json`, `tools/AppDelicare.CodeGen/Seeds.cs`, `DB/seed/{ObjectMenu, UserRole}_seed.sql`, test `Tests/Purchasing/{PurchasingOwnerRulesTests (mới), PurchasingTests, MrpTests}.cs`.

Kiểm tra:
- `dotnet build AppDelicare.sln --no-incremental`: 0 lỗi, 11 cảnh báo (= baseline).
- `dotnet test`: **149/149** xanh (+4):
  - `PurchasingOwnerRulesTests.Receiving_temperature_three_zones…`: mát 4 nhận / 7 có điều kiện (thiếu lý do → lỗi; lô `[UU_TIEN_DUNG_TRUOC]`, sự cố NCC) / 9 loại (DAT hoặc DAT_MOT_PHAN kèm lý do → lỗi; KHONG_DAT → lô OnHold, không tồn); đông lạnh −20 nhận / −16 (thiếu `noThawConfirmed` → lỗi) / −12 loại; DRY không nhiệt độ OK; mát chưa đo → lỗi, gửi `temperature` lúc QC → OK; 6 dòng nhập kho, 7 TemperatureLog; phiếu 2 chỉ 1 dòng có điều kiện DAT → sự cố TEMPERATURE / Medium.
  - `…Po_at_or_above_director_threshold…`: PO 60 triệu – người duyệt `scm-po` không thấy (GetMyPending không type / `scm-po`), Approve / Reject / Decide → "không có quyền"; GĐ (`scm-po-director`) thấy trong `type=scm-po-director` (screenId đúng, tiêu đề "Giám đốc"), duyệt → Approved; PO 10 triệu – người duyệt `scm-po` duyệt, GĐ không duyệt được; PO đúng 50 triệu → `needsDirectorApproval`.
  - `…Vat_is_computed…`: Thịt 1 000 000 × 8% = 80 000; Rau (nhóm con của "RAUTUOI" = 5%) 400 000 → 20 000; Muối 37 035 × 8% = 2 962,8 → 2 963; tổng 1 437 035 / 102 963 / 1 539 998 – GetDetail và file in; `purchaseOrder.totalAmount` không đổi.
  - `MrpTests.CalculateTopUp_subtracts_received_qc_passed_quantity…` (GAP-MRP-08 ý 1 anh chốt): đề nghị 10 → PO → nhận 6 DAT ghi sổ → bổ sung: thiếu 4, đã đề nghị 4, chênh 0, không cảnh báo thừa; tăng 130 suất → bổ sung đúng 3.
  - Test T15 cũ chỉnh theo quy tắc mới: Rau 8 °C gợi ý DAT (vùng có điều kiện); Approve không quyền → 400 thay 403.
- `git status`: không thay đổi trong `app-delicare-client/`; không file mới trong `Controllers/V1/`, `Api/`, `AppDelicare.Application/`.

PROPOSED (chờ anh xác nhận):
1. Bảng % VAT theo nhóm nguyên liệu (vd. rau củ tươi 5%) – đang để trống, mọi dòng 8%; cần mã nhóm thật (GAP-PUR-09).
2. Cờ "ưu tiên dùng trước" chỉ nằm trong Note (GAP-PUR-10); xuất kho FEFO chưa tự ưu tiên lô này (MaterialIssue – T17).
3. Hàng mát / đông lạnh chưa đo nhiệt độ: chặn khi kết luận DAT / DAT_MOT_PHAN (KHONG_DAT vẫn cho) – không chặn ở bước lập phiếu nhập (chỉ cảnh báo).
4. Hộp duyệt: `type=scm-po` chỉ trả PO dưới ngưỡng, PO lớn chỉ ở `type=scm-po-director` (người có cả 2 quyền xem 2 hộp).
5. Phiếu nhập chỉ có dòng nhận có điều kiện (đều DAT) → sự cố NCC mức Medium.
### T45R.8 – Hoàn thiện quản trị người dùng (D-37, D-38, D-39) · 2026-11-16 · Backend DONE · FE TODO (D-40)
Nhánh `feat/T45R.8-admin` (worktree `wt-T45R8`). Chỉ backend; phần giao diện (`adm-user` 1 form, popup mật khẩu tạm, chuyển màn Đổi mật khẩu) làm cùng đợt FE.

Rà soát code cũ (đã có – bổ sung vào đúng file):
- `Person/Insert` → `PersonService.Insert` → `UserService.InsertUserAfter`: tự sinh tên (lỗi khi họ tên có 2 dấu cách hoặc > 6 từ, không bỏ dấu chữ cái đầu), mật khẩu = tên đăng nhập, không gán nhóm, User `IsActive = false`.
- `User/Insert` không chặn Person đã có User; `User/ResetPassword` đặt mật khẩu = tên đăng nhập, trả `Result<bool>`.
- `PermissionFilter` không có khái niệm "phải đổi mật khẩu"; `UserRole/SaveAccessAll` chỉ cấp menu cấp 2; `uspUser_GetAll` không lọc IsActive; `ObjectMenu/Insert|Update` cho tạo cấp 3.

Đã làm:
- **D-37** `DB/seed/ObjectMenu_disable_legacy.sql` (idempotent, liệt kê 26 Id + MetaLink): `IsActive = 0` cho 21 mục cấp 2 kiểu cũ (`benh-nhan`, `quan-ly-…`, `kho-thuoc-…`…) và 5 mục cấp 1 (`trang-chu` + 4 nhóm `#` theo Id: Danh mục, Khám bệnh, Kho thuốc, Hệ thống) – nhóm chỉ tắt khi không còn con đang bật. Không xóa, không đụng UserRole. **DB dev: chạy 2 lần – lần 1 tắt 21 + 5, lần 2 tắt 0, không lỗi.** Đã thêm vào `deploy/db/Deploy-Database.ps1` và `deploy/docker/db-init.sh` (sau `ObjectMenu_seed.sql`). Staging: anh chạy.
- **D-38** `PersonCustomRequest` + `UserName`, `ListUserGroupId`, `IsActive` (chỉ trường request). `PersonService.InsertPersonUser` (action `Person/Insert`): kiểm tài khoản **trước** khi ghi (tên đăng nhập / nhóm) → Person → `UserService.InsertUserAfter(personId, fullName, typeSave, userName?, listUserGroupId?, isActive)` → `UserClaimService.SaveAfterUser`, tất cả trong 1 transaction; trả `Result<PersonUserCreatedViewModel>` { PersonId, UserId, UserName, TempPassword }. `UserService.ValidateNewAccount`: tên nhập vào → chữ thường, `^[a-z0-9][a-z0-9._-]{1,49}$`, không trùng (LOWER, bỏ bản xóa mềm); trống → sinh như cũ (tên + chữ cái đầu, bỏ dấu cả chữ đầu, trùng thêm 1, 2…). `User/Insert` → `UserService.InsertAccount`: chặn Person đã có User, Person không tồn tại, trùng tên; mật khẩu tạm; trả `Result<PersonUserCreatedViewModel>`. `Person/GetAllDataByKey?isEdit=false` loại mọi hồ sơ đã có User (kể cả đang khóa).
- **D-39** `EncriptFunctionsHelper`: `GenerateTemporaryPassword` (12 ký tự, `RandomNumberGenerator`, không 0 O 1 l I, ≥ 1 hoa / thường / số, xáo Fisher–Yates), `GenerateTemporaryPasswordHash` → `PBKDF2T$<hết hạn unix>$100000$<salt>$<hash>`, `IsTemporary`, `IsTemporaryExpired`; `VerifyPassword` hiểu dạng tạm. Hạn = `IClock.UtcNow` + 24 giờ. `User/ResetPassword` → `Result<string>` (Data = mật khẩu tạm, một lần; log chỉ ghi user + người thao tác; AuditChange tự ghi Actor / CreatedBy = admin, IdChange = user, "PassWord: (đã thay đổi – không lưu giá trị)"). `LoginService`: mật khẩu tạm quá hạn → `code 400` "Mật khẩu tạm đã hết hạn, liên hệ quản trị"; còn hạn → `data.mustChangePassword = true` + claim `pwd_temp`. `PermissionFilter`: token `pwd_temp` mà DB vẫn là mật khẩu tạm → 403 `{ responseCode: 403, responseMessage: "Cần đổi mật khẩu" }` cho mọi action trừ `User/ChangePassword`, `User/GetCurrent`, `Authentication/*` (kiểm trước cả SkipPermission). `ChangePassword` lưu PBKDF2 thường (chặn khi mật khẩu tạm đã hết hạn); đổi xong token cũ dùng bình thường. `User/GetCurrent` thêm `mustChangePassword`.
- **FE-1 follow-ups:** `UserRole/SaveAccessAll` gồm cả menu cấp 1; `User/GetAll` lọc `IsActive` (`UserSearchRequest.IsActive`, SP `uspUser_GetAll` thêm `@IsActive BIT = NULL` – đã chạy trên DB dev); `ObjectMenu/Insert|Update` chặn cấp 3 (cha phải là mục cấp 1, mục có con không được có cha), `LevelShow` chuẩn hóa theo `ParentId`.
- Docs: `docs/frontend-handoff/P0-P2.md` (mục 1.1, bảng adm-person / adm-user / adm-permission / adm-objectmenu, **mục 6** mới), `docs/api-tests/t45r8-admin-users.http`, UAT_CHECKLIST (T45R.8), DECISIONS (GAP-ADM-02 cập nhật, GAP-ADM-04, GAP-ADM-05), LEGACY_REVIEW.

Thay đổi hợp đồng API (FE cần biết):
- `Person/Insert`: `data` từ `true` → `{ personId, userId, userName, tempPassword }`.
- `User/Insert`: `data` từ `true` → cùng dạng trên; mật khẩu ban đầu không còn = tên đăng nhập.
- `User/ResetPassword`: `Result<bool>` → `Result<string>` (Data = mật khẩu tạm). Route / method (GET) giữ nguyên.
- `Authentication/Login`: thêm `data.mustChangePassword` (trường cũ giữ nguyên); `User/GetCurrent`: thêm `mustChangePassword`.
- Mọi API: có thể trả 403 kèm body "Cần đổi mật khẩu" khi token là mật khẩu tạm.

Kiểm tra:
- `dotnet build AppDelicare.sln --no-incremental`: 0 lỗi, 11 cảnh báo (= baseline).
- `dotnet test`: **140/140** xanh (+9; baseline 131):
  - `Admin/UserAccountTests` (7): tạo 1 bước (1 Person + 1 User + 2 nhóm, tên chuẩn hóa, PBKDF2T$, đăng nhập được + mustChangePassword); tự sinh tên `hadt` / `hadt1`, không gửi IsActive → false; trùng tên (khác hoa thường) / tên có dấu / nhóm sai bị chặn và không để lại Person; `User/Insert` cho Person đã có User / trùng tên bị chặn, hồ sơ chưa có User thì được; 300 mật khẩu tạm đúng 12 ký tự, không ký tự dễ nhầm, đủ hoa / thường / số + định dạng hash; reset trả mật khẩu tạm, AuditChange có "ai reset cho ai", không chứa mật khẩu (và không chứa hash tạm), log (Serilog sink bắt tạm) không chứa mật khẩu; đăng nhập tạm → claim `pwd_temp`, GetCurrent được, `UserGroup/GetAllData` / `Person/GetProfile` / `User/GetById` 403 "Cần đổi mật khẩu" (kể cả admin), đổi xong token cũ dùng được, đăng nhập lại bình thường; TestClock: +23 giờ đăng nhập được, +24 giờ 1 phút bị từ chối đúng thông báo, ChangePassword bằng mật khẩu tạm hết hạn bị chặn.
  - `Admin/AdminServiceTests`: + `Object_menu_insert_update_reject_a_third_level`; `SaveAccessAll` kiểm thêm menu cấp 1.
  - `Admin/UserAccountSqlServerTests` (1, SQL Server dev – đã chạy, không skip): `User/GetAll?IsActive=true|false`.
  - `Security/PasswordHashTests`: sửa kịch bản tạo / reset theo D-39 (mật khẩu ban đầu và mật khẩu reset là mật khẩu tạm trả về, lưu PBKDF2T$). MD5 cũ / PBKDF2 thường vẫn đăng nhập và nâng cấp như cũ.
- `git status`: không có thay đổi trong `app-delicare-client/`; không file mới trong `Controllers/V1/`, `Api/`, `AppDelicare.Application/`.

AC (phần backend): [x] test tạo 1 bước / chặn; [x] test mật khẩu tạm (12 ký tự, chỉ ChangePassword / GetCurrent, đổi xong bình thường, quá 24h, không trong AuditChange / log); [x] script D-37 chạy 2 lần (DB dev); [x] build / test xanh. Còn: `npm run build` + UAT PILOT-FE (phần FE), Catalog.

`DELICARE_API_Catalog.xlsx` **chưa cập nhật** (không có openpyxl). Gợi ý:
- Dòng 35 (`POST /admin/users`): J = `Đã xong`, L = `POST api/Person/Insert` (form: hồ sơ + UserName, ListUserGroupId, IsActive → PersonUserCreatedViewModel); hồ sơ có sẵn: `POST api/User/Insert`; M = `T45R.8`; K = "Mật khẩu tạm thay cho password; IsActive mặc định false (GAP-ADM-05)".
- Dòng 38 (`POST /admin/users/{id}/reset-password`): J = `Đã xong`, L = `GET api/User/ResetPassword?id=` (Result<string> = mật khẩu tạm), M = `T45R.8`.
- (Dòng 34 – danh sách user lọc isActive: có thể ghi thêm L = `GET api/User/GetAll?IsActive=`, M = `T45R.8`.)

PROPOSED – BA/PO CONFIRM: quy tắc tên đăng nhập (GAP-ADM-04); `IsActive` mặc định false khi không gửi (GAP-ADM-05); `User/Insert` dùng mật khẩu tạm + đổi `data`; giữ `ResetPassword` là GET.

Cần anh: chạy trên staging `DB/seed/ObjectMenu_disable_legacy.sql` và `DB/sp/uspUser_GetAll.sql` (hoặc chạy lại `Deploy-Database.ps1`); chốt GAP-ADM-04 / 05.
### T17 – Sản xuất theo mẻ (Pilot P7) · 2026-11-16 · DONE(PILOT)
Nhánh `feat/T17-production` (worktree `wt-T17`, từ T15). Chỉ backend (D-40).

Kế hoạch:
1. Rà soát ProductionPlan(+Line) / ProductionOrder / ProductionBatche / ProductionOutput / MealWasteRecord / TemperatureLog / FoodSafetyChecklist(+Item, +Result) / ProductionLine / Shift → LEGACY_REVIEW mục (g). Đọc (không sửa) `MaterialRequirementService` (T14 tạo KH- + dòng món).
2. Duyệt kế hoạch (1 cấp) → phát hành: lệnh SX PH- theo món (công thức khóa + ảnh chụp định lượng), mẻ ME-YYMMDD-<CA>-NN chia theo dây chuyền, lịch ngược từ giờ xuất bếp.
3. Xuất NL cho mẻ: tính theo định lượng × phần mẻ, gọi `MaterialIssueService.Create` / `Post` (FEFO, chặn lô) gắn lệnh SX thật.
4. Sự kiện mẻ: Start → Finish; nhiệt độ lõi (TemperatureLog CORE), lưu mẫu (SAMPLE_RETAINED), sản lượng / hao hụt; QC đạt / giữ / loại; đóng gói; hoàn thành lệnh / kế hoạch.
5. In Phiếu sản xuất (.xlsx, theo tiêu đề file thật `Phieu san xuat.xlsx`).
6. Chặn CRUD cũ phá máy trạng thái. Seed checklist SAMPLE_RETAINED.
7. Test InMemory (bộ số tính tay) + 1 test SQL Server rollback; tài liệu P7.md, `.http`, DECISIONS (GAP-PRD), LEGACY_REVIEW, BUSINESS_RULES.

Rà soát: cả 10 bảng chỉ có CRUD sinh mã – CRUD tạo lệnh / mẻ không số, đổi trạng thái / QC tùy ý (đóng gói được khi chưa đo nhiệt độ, chưa lưu mẫu). DB dev chưa có `ProductionLine`, chưa có checklist `SAMPLE_RETAINED`. Chi tiết: LEGACY_REVIEW (g).

Đã làm (đều trong file cũ, đúng mẫu 4.2; thêm 1 lớp tĩnh dùng chung `ProductionRules`, không có service / controller mới):
- **Kế hoạch (`ProductionPlanService` / `ProductionPlanController`, `prod-plan`)**: `GetDetail`; `Approve` (Nháp → Đã duyệt, `[Permission("prod-plan", Approve)]` = mục "Duyệt – Kế hoạch sản xuất" đã có trong seed); `Release` (form `Id, ShiftId?, ProductionLineId?, DispatchTime?, Note?`, 1 transaction): ca (mặc định `ProductionDefaultShiftCode` = TRUA), giờ xuất bếp (giờ giao sớm nhất `CustomerDeliveryLocation.DeliveryWindowFrom` của đơn Đã chốt, dòng thuộc ca − 60′; không có → `Shift.StartTime` − 60′; hoặc nhập tay), mẻ cuối xong trước xuất bếp 30′ (`ProductionPackingMinutes`), mỗi món → 1 lệnh SX `PH-` (Released, công thức chọn như MRP: khóa hiệu lực → khóa đang dùng → Đã duyệt + cảnh báo; không có → chặn; `RecipeSnapshotJson`), chia mẻ ⌈phần ÷ `CapacityMealPerBatch`⌉ (mẻ đầy trước) × `MinutesPerBatch` (trống 60′), các mẻ của món nối tiếp, món song song; mã `ME-YYMMDD-<CA>-NN` (T11, sinh mã đầu rồi cộng dần trong cùng khóa `sp_getapplock`) theo giờ bắt đầu rồi tên món; StartDate / EndDate = giờ kế hoạch; dòng kế hoạch gắn ca → Released; kế hoạch → Released; đơn Đã chốt của ngày (dòng thuộc ca) → `InProduction`. `ExportProductionSheet` (.xlsx). CRUD: Insert / Update → lỗi (tạo bằng MRP, Note là JSON MRP); Delete chỉ Nháp.
- **Dòng kế hoạch (`ProductionPlanLineService`)**: Insert / Update / Delete chỉ khi kế hoạch Nháp, 1 món 1 dòng, số phần > 0, ghi song song PlannedQty.
- **Lệnh SX (`ProductionOrderService` / `Controller`, `prod-order`)**: `GetDetail` (ảnh chụp định lượng, mẻ, phiếu xuất). CRUD: Insert / Delete → lỗi; Update chỉ Note / ResponsibleEmployeeId. Trạng thái tự đổi: Released → InProgress (mẻ đầu bắt đầu) → Completed (mọi mẻ Packed / Failed; ActualQty = Σ số đạt mẻ đóng gói); mọi lệnh xong → kế hoạch Completed.
- **Mẻ (`ProductionBatchService` / `Controller`, `prod-batch`)**: `GetBoard` (mẻ của ngày + statusCounts + phần kế hoạch / đạt), `GetDetail` (nguyên liệu cần / đã xuất, phiếu xuất, nhiệt độ, lưu mẫu, sản lượng, hao hụt, `listBlocker`, `allowedActions`); `CreateMaterialIssue` (form `Id, WarehouseId, Post`; định lượng × phần mẻ ÷ suất chuẩn × (1 + hao hụt %) quy đổi sang đơn vị tồn → `MaterialIssueService.Create` (FEFO / FIFO, chặn lô hết hạn / KHONG_DAT / tạm giữ / thiếu tồn) + `Post` trong 1 transaction; `ProductionOrderId` thật + "Mẻ ME-…" trong Note; 1 phiếu tự động / mẻ); `Start`; `Finish`; `RecordCoreTemperature` (TemperatureLog CORE, Min 70 °C, IsWithinRange ở server, dưới ngưỡng bắt buộc hành động khắc phục, RecordedBy = người thao tác); `RecordSampleRetained` (FoodSafetyChecklistResult SAMPLE_RETAINED, 1 lần / mẻ); `RecordOutput` (ProductionOutput 1 dòng / mẻ, ghi song song cột cũ; số đạt / loại của mẻ); `RecordWaste` (MealWasteRecord, WasteQty mẻ = Σ); `Release` (Chờ QC → Đạt – **chặn** khi nhiệt độ lõi gần nhất < 70 °C / chưa đo, chưa lưu mẫu, chưa ghi sản lượng); `Hold`; `Reject` (lý do; toàn bộ thành hao hụt QC_REJECT); `Pack` (Đạt → Đã đóng gói – kiểm lại nhiệt độ lõi gần nhất ≥ 70 °C); `ExportProductionSheet`. CRUD: Insert / Delete → lỗi; Update chỉ trạm bếp.
- **`ProductionOutput`**: CRUD ghi → lỗi (qua RecordOutput). **`MealWasteRecord`**: dòng gắn mẻ chỉ qua RecordWaste; gỡ header auto-generated.
- **`ProductionRules`** (lớp tĩnh): chọn công thức, ảnh chụp, lịch ngược `Schedule`, giờ xuất bếp, nguyên liệu mẻ, điều kiện ATTP, tóm tắt lệnh / mẻ, mẫu in Phiếu sản xuất (bố cục theo tiêu đề file thật: tên pháp nhân, Số suất order / thực tế, PHIẾU SẢN XUẤT, Thứ – ngày; Buổi | Tên món | Tên NL | ĐVT | Số lượng (Định mức / SL cần / Thực xuất / Xuất thêm / Lý do) | SC ký nhận | Sơ chế (SL sau SC / Tiêu hao / Trả về) | Bếp ký nhận | Ghi chú – chỉ đọc tiêu đề, không chép dữ liệu).
- Cấu hình `BusinessRules` (PROPOSED): `ProductionDefaultShiftCode` TRUA, `ProductionPackingMinutes` 30, `ProductionDefaultMinutesPerBatch` 60, `ProductionRequireSampleBeforeRelease` true (dùng sẵn `TransportMinutesBeforeCutoff` 60, `CoreTempMinC` 70).
- Hằng `Model/Helper/ProductionStatus.cs` (ProductionOrderStatus, ProductionBatchStatus, BatchQcStatus, ProductionCode); request `Model/Request/Custom/ProductionCustomRequest.cs`; model `Model/Model/ProductionModels.cs`; thông báo tiếng Việt trong `ConstantHelper.Message`.
- Seed mới `DB/seed/FoodSafetyChecklist_seed.sql` (bộ `PRD_BATCH`: SAMPLE_RETAINED, CORE_TEMP – cấu hình PROPOSED, chỉ thêm khi chưa có) – **đã chạy trên DB dev 2 lần** (lần 2 không thêm dòng).
- Quyền theo `PermissionConvention`: Get* = View; `CreateMaterialIssue` = Add; `Export*` = Export; còn lại Edit (`prod-plan`, `prod-order`, `prod-batch`); `ProductionPlan/Approve` = Approve `prod-plan` (khai báo rõ). Không thêm mục menu / seed quyền / SP.

File chính: `Services/Implement/{ProductionRules, ProductionPlanService, ProductionPlanLineService, ProductionOrderService, ProductionBatchService, ProductionOutputService, MealWasteRecordService}.cs`, `Services/Interface/I{ProductionPlan, ProductionOrder, ProductionBatch}Service.cs`, `WebApi/Controllers/{ProductionPlan, ProductionOrder, ProductionBatch}Controller.cs`, `Model/Helper/{ProductionStatus, ConstantHelper}.cs`, `Model/Option/BusinessRulesOptions.cs`, `Model/Request/Custom/ProductionCustomRequest.cs`, `Model/Model/ProductionModels.cs`, `WebApi/appsettings.json`, `DB/seed/FoodSafetyChecklist_seed.sql`, test `Tests/Production/{ProductionTests, ProductionSqlServerTests}.cs`, tài liệu `docs/frontend-handoff/P7.md`, `docs/api-tests/p7-production.http`.

Kiểm tra:
- `dotnet build AppDelicare.sln --no-incremental`: 0 lỗi, 11 cảnh báo (= baseline).
- `dotnet test`: **148/148** xanh (+5):
  - `Production/ProductionTests` (4, InMemory): lịch ngược tính tay (`Schedule`: 250 phần / 100 / 60′ / xong 10:00 → 07–08, 08–09, 09–10 (50)); phát hành: giao 11:30 → xuất bếp 10:30 → 4 mẻ `ME-261221-<CA>-01…04` đúng giờ / số phần / thứ tự (Thịt#1, Thịt#2, Canh, Thịt#3), lệnh `PH-261220-NNNN` Released + ảnh chụp, đơn → InProduction, dòng kế hoạch gắn ca; CRUD cũ bị chặn (kế hoạch, dòng kế hoạch đã phát hành, mẻ, lệnh, sản lượng, hao hụt gắn mẻ; Update mẻ không đổi trạng thái); in Phiếu sản xuất mở được (SL cần 13,2 kg); xuất NL cho mẻ: 13,2 kg = L1 10 + L2 3,2 (FEFO), bỏ lô hết hạn 50 kg + KHONG_DAT 50 kg, phiếu gắn lệnh SX thật + số mẻ, 1 phiếu / mẻ, mẻ 50 phần cần 6,6 chỉ còn 3,6 xuất được → chặn, không ghi phiếu, lô bị chặn không có bút toán xuất; ATTP: 65 °C không khắc phục → 400, có khắc phục → lưu + cảnh báo, Release bị chặn (65 °C + chưa lưu mẫu + chưa sản lượng), đo lại 75,5 °C → còn chặn vì chưa lưu mẫu, lưu mẫu (1 lần) → Passed; đo lại 68 °C → Pack bị chặn, 72 °C → Packed; sổ nhiệt độ 4 dòng đúng IsWithinRange / Min 70 / người ghi; sản lượng 80 / đạt 78 / loại 2, hao hụt 3; lệnh Canh Completed (ActualQty 78); 3 mẻ Thịt Reject (lý do bắt buộc) → QC_REJECT ×3 → kế hoạch Completed; GetBoard; in phiếu mẻ (Rau 4 kg).
  - `Production/ProductionSqlServerTests` (1, SQL Server dev): kế hoạch → Approve → Release (PH- + ME- qua sp_getapplock, xuất bếp 10:30 → mẻ 09:00) → CreateMaterialIssue Post (5 kg từ lô còn hạn, lô hết hạn 30 kg không đụng) → Start → nhiệt độ 74,5 → lưu mẫu → Finish → sản lượng 50 / loại 1 → Release → Pack → lệnh + kế hoạch Completed – trong transaction ngoài rồi rollback, không để lại dữ liệu (đã chạy, không bị bỏ qua).
- `git status`: không có thay đổi trong `app-delicare-client/`; không file mới trong `Controllers/V1/`, `Api/`, `AppDelicare.Application/`; không sửa `MaterialRequirementService`.

AC (T17, bản Pilot):
- [x] Lập kế hoạch đúng giờ (lịch ngược từ giờ xuất bếp, tính tay được) + chia mẻ theo dây chuyền + mã `ME-YYMMDD-<CA>-NN`.
- [x] Mẻ < 70 °C bị chặn đạt QC / đóng gói (lần đo gần nhất; đo lại đạt mới qua).
- [x] Mẻ chưa lưu mẫu không được đạt QC (PROPOSED, cấu hình `ProductionRequireSampleBeforeRelease`).
- [x] Xuất NL cho mẻ dùng FEFO, chặn lô hết hạn / KHONG_DAT; phiếu xuất gắn lệnh SX thật.
- [x] Sản lượng / hao hụt (ProductionOutput / MealWasteRecord); in Phiếu sản xuất (.xlsx).
- [x] 1 test SQL Server rollback cho plan → batch → issue → start → temp → sample → finish → output (→ release → pack).
- [ ] "Không chuyển công đoạn khi công đoạn trước chưa xong" (công đoạn theo tổ) → DEFERRED(PILOT).
- [ ] `DELICARE_API_Catalog.xlsx` không sửa (theo điều phối). Gợi ý cột J / L (M = T17): 8.1 GET plans = Đã xong (`ProductionPlan/GetAll`, `GetDetail`); generate = Một phần (MRP `MaterialRequirement/Calculate` tạo KH- theo ngày); lines = Đã xong (`ProductionPlanLine/Insert / Update / Delete`, chỉ Nháp); approve = Đã xong (`ProductionPlan/Approve`); release = Đã xong (`ProductionPlan/Release`, chưa giữ chỗ). 8.2 GET orders = Đã xong (`ProductionOrder/GetAll`, `GetDetail`); complete = Đã xong (tự động khi đóng gói / loại hết mẻ); close / cancel = Chưa bắt đầu. 8.3 batch-board / batches/{id} = Đã xong (`ProductionBatch/GetBoard`, `GetDetail`); start / finish = Đã xong; qc = Đã xong (`Release` / `Hold` / `Reject`); create-packing-order = Một phần (`Pack`, chưa sinh PackingOrder). 8.4 outputs = Đã xong (`ProductionBatch/RecordOutput` + `ProductionOutput/GetAll`); waste-records = Một phần (`ProductionBatch/RecordWaste`, MealWasteRecord CRUD; summary chưa). 9.3 temperature-logs = Một phần (+ CORE của mẻ).

DEFERRED(PILOT):
- Công đoạn theo tổ (Sơ chế → Nấu → Cơm / Canh → Chia suất, người làm / tổ, API tiến độ bếp theo tổ).
- Nhiều ca / ngày (tách kế hoạch theo `OrderLine.ShiftId`), phân món → dây chuyền theo nhóm món, nhiều dây chuyền song song, phiên bản kế hoạch (GAP-PRD-02).
- Giữ chỗ `MaterialReservation` khi phát hành; quy đổi đơn vị khi xuất ngoài định lượng; xuất thêm / trả về NL cho mẻ (dùng `MaterialIssue/Create` / `Cancel`).
- Đóng / hủy lệnh SX, hủy mẻ, `PackingOrder` khi đóng gói (T19 / T47B.2), giá thành thực tế (spec 8.5), báo cáo hao hụt (summary), checklist ATTP theo ca (T18).

Câu hỏi cho anh (DECISIONS GAP-PRD-01, 03…08):
1. Pilot 1 ca: phát hành gán cả kế hoạch ngày cho 1 ca (mặc định TRUA) – được không? Khi chạy nhiều ca có cần MRP tách kế hoạch theo ca không?
2. Giờ xuất bếp = giờ giao sớm nhất của điểm giao (`DeliveryWindowFrom`) − 60′; mẻ cuối xong trước 30′ để chia suất / đóng gói; các món nấu song song – đúng thực tế bếp? Mẻ đầy trước, mẻ cuối phần lẻ – hay chia đều?
3. Ngưỡng nhiệt độ lõi 70 °C (BUSINESS_RULES) hay 75 °C (spec 8.3)? Đo lại sau khi đạt mà < 70 °C thì chặn đóng gói – đúng ý anh?
4. Bắt buộc lưu mẫu trước khi mẻ "Đạt" (đề xuất) – hay chỉ trước khi giao?
5. Xác nhận kế hoạch (Approve) bấm 1 cấp với quyền mục "Duyệt – Kế hoạch sản xuất" – có cần qua hộp duyệt (ApprovalRequest) như PO không?
6. Phát hành kế hoạch chuyển đơn hàng Đã chốt → Đang sản xuất – đúng không? Lệnh SX dùng mã `PH-` (BUSINESS_RULES §11) – được chứ?

Cần anh: chạy `DB/seed/FoodSafetyChecklist_seed.sql` trên staging (DB dev đã chạy); khai báo `ProductionLine` (năng lực mẻ, phút / mẻ) và giờ giao của điểm giao trước khi chạy thử.

### T17.1 – Sản xuất 4 ca / ngày + chia mẻ đều (anh chốt 2026-11-16) · 2026-11-16 · DONE(PILOT)
Nhánh `feat/T17.1-shifts` (từ `release/pilot`). Chỉ backend + tài liệu. Không bảng / cột / SP / seed mới.

Anh chốt 2026-11-16 (DECISIONS mục "Anh chốt 2026-11-16 (T17)"; GAP-PRD-01 / 06 / 07 đánh dấu đã chốt, GAP-PRD-08 phần lưu mẫu = D-42, GAP-PRD-05 giữ nguyên): Pilot 4 ca / ngày, KH- vẫn theo ngày, phát hành tách theo ca; lịch giữ như T17; chia mẻ đều; duyệt 1 cấp; phát hành → đơn Đang SX; lệnh SX `PH-`.

Đã làm (trong file cũ, đúng mẫu 4.2):
- **`ProductionRules.SplitByShiftAsync`** (mới, lớp tĩnh): chia số phần từng dòng kế hoạch theo ca = tỉ lệ suất của món trong ca, tính như MRP (T14 / T14.1: dòng đơn Đã chốt / Đang SX × MenuLine cùng loại suất + thứ, chia đều lựa chọn + `MrpOptionBufferPct`); ca = `OrderLine.ShiftId`, chưa gán → `ProductionDefaultShiftCode` (cảnh báo); món không có trong đơn → ca mặc định; làm tròn 4 số lẻ, ca cuối nhận phần còn lại (tổng = dòng kế hoạch). Ca sắp theo `StartTime`.
- **`ProductionRules.Schedule`**: chia mẻ đều – N = ⌈phần ÷ năng lực⌉, phần nguyên chia đều, các mẻ đầu nhận phần dư, phần lẻ thập phân vào mẻ cuối (250 / 100 → 84, 83, 83; 57,5 / 50 → 29, 28,5).
- **`ProductionRules.ResolveDispatchAsync`**: thêm `fallbackShiftId`; dòng chưa gán ca thuộc ca mặc định (không còn tính cho mọi ca); đơn có dòng ở ≥ 2 ca không dùng khung giờ điểm giao (PROPOSED).
- **`ProductionPlan/Release`**: tách theo ca – `ShiftId` = chỉ ca đó (ca đã phát hành → 400 "đã phát hành", ca không có suất → 400), trống = mọi ca chưa phát hành; `DispatchTime` nhập tay chỉ khi phát hành 1 ca. Mỗi ca: giờ xuất bếp riêng → lệnh SX `PH-` / món (`ShiftId`, `PlannedQty` = phần của ca) + mẻ `ME-yyMMdd-<ca>-NN` (số mẻ riêng từng ca). Ca đã phát hành = có lệnh SX (chưa hủy) của kế hoạch gắn ca. Kế hoạch giữ Đã duyệt tới khi mọi ca có nhu cầu đã phát hành → Đã phát hành; dòng kế hoạch Released khi mọi ca của món đã phát hành (`ShiftId` chỉ khi món thuộc 1 ca); đơn Đã chốt có dòng thuộc ca vừa phát hành → `InProduction`. Hoàn thành kế hoạch (ProductionBatchService) vốn chỉ khi kế hoạch Released → không đổi.
- **`ProductionPlan/GetDetail`**: `listShift[]` (`shiftId, shiftCode, shiftName, startTime, portions, released, dispatchAt, orderCount, batchCount`), `listLine[].listShift[]` (`shiftId, shiftCode, quantity`); `allowedActions` Đã duyệt = `release` (còn ca) + `exportProductionSheet` (đã có lệnh SX); `listWarning` dòng đơn chưa gán ca.
- **`ProductionBatch/GetDetail`**: mẻ Đã đóng gói chưa lưu mẫu → `allowedActions` có `recordSampleRetained` (D-42).
- **Giao (`DeliveryRules.OrderShiftIdsAsync`)**: ca của đơn trên chuyến – dòng chưa gán ca → ca mặc định (cùng quy tắc phát hành); dùng cho `DeliveryRoute/Create` (cảnh báo) và `Dispatch` / chi tiết chuyến. Kiểm mẻ vẫn theo ngày + ca (T19).
- Thông báo mới `ConstantHelper.Message.Release{LinesWithoutShift, ShiftAlready, ShiftNoDemand, NothingPending, DispatchNeedsShift}`; model `ProductionPlanShiftModel`, `ProductionPlanLineShiftModel`; chú thích `BusinessRulesOptions.ProductionDefaultShiftCode`, `ProductionPlanReleaseRequest`.

File: `Services/Implement/{ProductionRules, ProductionPlanService, ProductionBatchService, DeliveryRules, DeliveryRouteService}.cs`, `Model/Model/ProductionModels.cs`, `Model/Helper/ConstantHelper.cs`, `Model/Option/BusinessRulesOptions.cs`, `Model/Request/Custom/ProductionCustomRequest.cs`, test `Tests/Production/{ProductionShiftTests (mới), ProductionTests, ProductionSqlServerTests}.cs`, tài liệu `docs/claude/{DECISIONS, BUSINESS_RULES, PROGRESS}.md`, `docs/frontend-handoff/P7.md`, `docs/api-tests/p7-production.http`.

Kiểm tra:
- `dotnet build AppDelicare.sln --no-incremental`: 0 lỗi, 10 cảnh báo (≤ 11 baseline).
- `dotnet test`: **169/169** xanh (+2, baseline 167):
  - `ProductionShiftTests.Release_splits_the_daily_plan_by_shift…`: đơn TRUA 250 (giao 11:30) + TOI 100 (giao 17:30), kế hoạch Thịt 350 / Canh 80 → listShift TRUA 330 / TOI 100, Thịt 250 + 100; `DispatchTime` khi còn 2 ca → 400; phát hành TOI → 1 mẻ `ME-261221-TOIS1-01` 100 phần 15:00–16:00, xuất bếp 16:30, kế hoạch vẫn Approved (`release` + `exportProductionSheet`), đơn TOI InProduction, đơn TRUA còn Locked; phát hành lại TOI → 400; phát hành phần còn lại → TRUA `ME-261221-TRUAS1-01…04` 84 / 83 / 80 (Canh) / 83, 07:00–10:00; kế hoạch Released; 3 lệnh SX `PH-` khác số (TOI Thịt 100, TRUA Thịt 250, Canh 80); dòng Thịt Released không gắn ca, Canh gắn TRUA.
  - `ProductionShiftTests.Dispatch_of_an_evening_route…`: phát hành cả ngày 1 lần; mẻ TOI đóng gói chưa lưu mẫu → `allowedActions` có `recordSampleRetained`; chuyến TOI (đơn TOI) chỉ liệt kê mẻ TOI, xuất phát bị chặn chỉ vì mẻ TOI chưa lưu mẫu (không nhắc mẻ TRUA); lưu mẫu → xuất phát được trong khi 4 mẻ TRUA còn Kế hoạch.
  - Sửa test T17: lịch tính tay 84 / 83 / 83 (+ 300 → 100×3, 201 → 67×3, 57,5 / 50 → 29 / 28,5); phát hành P1 mẻ 84 / 83 / 80 / 83, Phiếu sản xuất SL cần 11,088; xuất NL mẻ 84 phần = L1 10 + L2 1,088, mẻ 83 cuối cần 10,956 > 7,956 còn → chặn; seed thêm thực đơn cho đơn (phát hành chia theo ca cần dòng đơn × thực đơn). SQL Server test: seed thêm đơn Locked + thực đơn, kiểm `listShift` + đơn → InProduction (đã chạy, không skip).
- `git status`: không thay đổi trong `app-delicare-client/`, `docs/reference/`.

PROPOSED (chờ anh xác nhận – DECISIONS "Anh chốt 2026-11-16 (T17)" mục PROPOSED):
1. Số phần món theo ca = dòng kế hoạch chia theo tỉ lệ suất của món trong từng ca (tính như MRP); món không có trong đơn → ca mặc định.
2. Kế hoạch giữ Đã duyệt tới khi mọi ca có nhu cầu đã phát hành; "ca đã phát hành" suy ra từ lệnh SX (không cột trạng thái theo ca).
3. Giờ xuất bếp nhập tay chỉ khi phát hành 1 ca.
4. Đơn có dòng ở ≥ 2 ca không dùng khung giờ điểm giao (1 điểm giao chỉ có 1 khung giờ) → giờ bắt đầu ca − 60′.
5. Chuyến có đơn chưa gán ca → kiểm mẻ của ca mặc định.
6. Mã ca bỏ ký tự đặc biệt trong mã mẻ (`SANG_6H` → `SANG6H`, `DEM_23H` → `DEM23H`).

### T19 – Giao + đối soát + phản ánh (Pilot P8) · 2026-11-16 · DONE(PILOT)
Nhánh `feat/T19-delivery` (worktree `wt-T17`, trên T17). Chỉ backend (D-40).

Kế hoạch:
1. Rà soát DeliveryRoute / DeliveryStop / DeliveryOrder / DeliveryEvent / ProofOfDeliverie / OrderReconciliation / CustomerComplaint / ComplaintHistory / Vehicle / TemperatureLog → LEGACY_REVIEW mục (h).
2. Lập chuyến `CX-` (đơn giao `PH-` + điểm dừng) từ đơn Đang sản xuất của ngày / ca.
3. Nhiệt độ giao ≥ 60 °C (TemperatureLog DELIVERY + DeliveryEvent TEMP_CHECK), xuất phát (chặn nhiệt độ, mẻ chưa đóng gói, mẻ chưa lưu mẫu – D-42).
4. Đến điểm / bàn giao + POD (nhiệt độ bàn giao) / thất bại; đơn hàng → Đã giao; chuyến hoàn thành.
5. Đối soát suất thực tế phục vụ → OrderReconciliation, đóng → đơn Đã đối soát.
6. Phản ánh `PA-` cơ bản (tạo / giao xử lý / đã xử lý / đóng / từ chối / ghi chú).
7. Chặn CRUD cũ phá máy trạng thái; test InMemory + 1 test SQL Server rollback; tài liệu.

Rà soát: 10 bảng chỉ có CRUD sinh mã (tạo chuyến / đơn giao không số, đổi trạng thái tùy ý, POD / đối soát nhập tay, `DeliveryEvent/Insert` ghi được nhiệt độ "đạt" giả, phản ánh không số / không SLA). Mã CX / PA / PH (DeliveryOrder) đã có trong `DocumentNumberService` (T11). Chi tiết: LEGACY_REVIEW (h).

**Anh chốt 2026-11-16 (D-42, điều phối chuyển):** lưu mẫu bắt buộc **trước khi giao**, không phải trước khi đạt QC → `BusinessRules:ProductionRequireSampleBeforeRelease` mặc định `false` (appsettings + Options); `ProductionBatch/Release` khi chưa lưu mẫu chỉ **cảnh báo**; `ProductionBatch/RecordSampleRetained` nhận thêm mẻ Đã đóng gói (để bổ sung mẫu trước khi xuất chuyến). Chỉ sửa đúng 2 chỗ này trong `ProductionBatchService` + kỳ vọng test T17 (`ProductionTests`: đạt QC khi chưa lưu mẫu → Passed + cảnh báo, ghi mẫu sau).

Đã làm (đều trong file cũ, đúng mẫu 4.2; thêm 1 lớp tĩnh dùng chung `DeliveryRules`, không có service / controller mới):
- **Chuyến (`DeliveryRouteService` / `DeliveryRouteController`, `log-route`)**: `GetBoard` (chuyến của ngày, tải / % tải, statusCounts), `GetReadyOrders`, `GetDetail` (điểm dừng + POD, sự kiện, mẻ của ngày / ca + đã lưu mẫu, `listBlocker`, `allowedActions`), `Create` (JSON – có danh sách đơn: xe hợp lệ (không Maintenance / Inactive), tài xế = Employee, đơn Đang sản xuất của ngày + dòng thuộc ca, chưa có đơn giao còn hiệu lực; số `CX-YYMMDD-<mã xe>-NN`; mỗi đơn 1 đơn giao `PH-` (DeliveryNumber + DeliveryNo) + 1 điểm dừng, giờ dự kiến = `DeliveryWindowFrom`, thứ tự theo giờ; giờ xuất phát kế hoạch = giờ sớm nhất − 60′; cảnh báo vượt tải / xe bận / sản xuất chưa sẵn sàng), `RecordTemperature` (xuất phát hoặc tại điểm), `Dispatch` (chặn khi nhiệt độ xuất phát gần nhất < 60 °C / chưa đo, mẻ của ngày / ca chưa Đã đóng gói, mẻ chưa lưu mẫu – thông báo liệt kê số `ME-`; có nhiệt độ dưới ngưỡng kèm khắc phục → vẫn lưu lần đo, trả 400), `ExportDeliveryNote` (.xlsx). CRUD: Insert → lỗi; Update chỉ tên / mô tả / tài xế / giờ khi Kế hoạch; Delete chỉ Kế hoạch chưa có sự kiện (xóa mềm kèm điểm dừng + đơn giao).
- **Điểm dừng (`DeliveryStopService` / `DeliveryStopController`, `log-stop`)**: `Arrive`, `Deliver` (nhiệt độ bàn giao gần nhất tại điểm ≥ 60 °C; POD giao / nhận / từ chối, người nhận, đường dẫn chữ ký / ảnh; điểm Đã giao + DeliveredMealQty + ShortageQty; đơn giao Đã giao; đơn hàng → `Delivered` khi mọi đơn giao còn hiệu lực đã giao; suất bị từ chối → phản ánh `PA-` POD / SHORTAGE tự động; mọi điểm xong → chuyến Completed), `Fail` (lý do; đơn giao Failed, đơn hàng giữ InProduction để xếp chuyến bù). CRUD: Insert / Delete → lỗi; Update chỉ thứ tự / giờ dự kiến / ghi chú khi Kế hoạch.
- **Đối soát (`OrderReconciliationService` / `Controller`, `crm-reconciliation`)**: `GetPendingOrders`, `GetDetail`, `GetByOrder`, `Reconcile` (đơn Đã giao có POD; suất phục vụ ≤ Σ suất nhận POD, phục vụ + trả về + hủy ≤ nhận; Ordered / Delivered / Accepted (= phục vụ) / Variance / VariancePercent / Status Khớp – Chênh lệch / ProofOfDeliveryId / FinalAmount = giá trị đơn × suất tính tiền ÷ suất đặt theo `ReconciliationBillingBasis` = SERVED; ghi song song cột cũ; trả về / hủy / suất nhận / cơ sở tính tiền → JSON trong `Note`), `Close` (|chênh| > 1% bắt buộc ghi chú; đơn hàng → `Reconciled`). CRUD → lỗi.
- **Phản ánh (`CustomerComplaintService` / `Controller`, `log-complaint`)**: `Create` (`PA-YYMMDD-NNNN`, mức độ High / Medium / Low, `SlaDueAt` = lúc nhận + 4 / 24 / 72 giờ, liên kết đơn / điểm dừng / đơn giao / chuyến cùng khách, mẻ ghi vào mô tả), `Assign`, `Resolve`, `Close`, `Reject`, `AddComment`, `GetDetail` (lịch sử, `isOverdue`). Mọi bước ghi `ComplaintHistory`. CRUD: Insert / Delete → lỗi; Update chỉ phân loại / kênh / mô tả / mức độ khi Mới / Đang xử lý.
- **Chặn CRUD khác**: `DeliveryOrder` (Insert / Delete → lỗi, Update chỉ ghi chú), `ProofOfDelivery` (Insert / Update / Delete → lỗi), `DeliveryEvent` / `ComplaintHistory` Insert → lỗi (sổ chỉ ghi thêm, ghi qua action). Gỡ header auto-generated ở các file đã sửa tay.
- **`DeliveryRules`** (lớp tĩnh): ghi nhiệt độ giao (TemperatureLog DELIVERY + DeliveryEvent TEMP_CHECK, `ExternalEventId = TemperatureLog:<Id>`), lần đo gần nhất (cùng giây → thấp nhất), mẻ phục vụ chuyến + đã lưu mẫu, điều kiện xuất phát, hạn SLA, tạo phản ánh, chi tiết chuyến, mẫu in Biên bản giao hàng.
- Cấu hình `BusinessRules` (PROPOSED): `DeliveryRequirePackedBatches` true, `DeliveryRequireSampleRetained` true (D-42), `ReconciliationBillingBasis` SERVED, `ReconciliationVarianceRemarkPct` 1, `DeliveryAutoComplaintOnRejected` true (dùng sẵn `DeliveryTempMinC` 60, `ComplaintSlaHours`, `TransportMinutesBeforeCutoff`).
- Hằng `Model/Helper/DeliveryStatus.cs`; request `Model/Request/Custom/DeliveryCustomRequest.cs`; model `Model/Model/DeliveryModels.cs`; thông báo tiếng Việt trong `ConstantHelper.Message`.
- Quyền theo `PermissionConvention` (Get* = View, `Create` = Add, `Export*` = Export, còn lại Edit) trên `log-route`, `log-stop`, `crm-reconciliation`, `log-complaint` (ScreenMap + ObjectMenu đã có). **Không thêm SP, seed, mục menu.**

File chính: `Services/Implement/{DeliveryRules, DeliveryRouteService, DeliveryStopService, DeliveryOrderService, DeliveryEventService, ProofOfDeliveryService, OrderReconciliationService, CustomerComplaintService, ComplaintHistoryService, ProductionBatchService}.cs`, `Services/Interface/I{DeliveryRoute, DeliveryStop, OrderReconciliation, CustomerComplaint}Service.cs`, `WebApi/Controllers/{DeliveryRoute, DeliveryStop, OrderReconciliation, CustomerComplaint}Controller.cs`, `Model/Helper/{DeliveryStatus, ConstantHelper}.cs`, `Model/Option/BusinessRulesOptions.cs`, `Model/Request/Custom/DeliveryCustomRequest.cs`, `Model/Model/DeliveryModels.cs`, `WebApi/appsettings.json`, test `Tests/Delivery/{DeliveryTests, DeliverySqlServerTests}.cs` (+ sửa `Tests/Production/ProductionTests.cs`), tài liệu `docs/frontend-handoff/P8.md`, `docs/api-tests/p8-delivery.http`, BUSINESS_RULES §6 / §7 / §9, LEGACY_REVIEW (h), DECISIONS D-42 + GAP-DLV-01…07.

Kiểm tra:
- `dotnet build AppDelicare.sln --no-incremental`: 0 lỗi, 11 cảnh báo (= baseline).
- `dotnet test`: **152/152** xanh (+4):
  - `Delivery/DeliveryTests` (3, InMemory): luồng đủ – chờ xếp chuyến → lập `CX-261221-XED1-01` (điểm #1 B 11:00 40 suất, #2 A 11:30 60 suất, PH- liên tiếp, xuất phát kế hoạch 10:00, tải 100 / 120 = 83,33%), đơn đã có chuyến không xếp lại; xuất phát bị chặn (chưa đo + "Mẻ chưa lưu mẫu thức ăn (bắt buộc trước khi giao): ME-261221-D1-02"); 55 °C không khắc phục → 400 không ghi; 55 °C có khắc phục → lưu TemperatureLog DELIVERY (IsWithinRange = false, Min 60, người ghi) + sự kiện trỏ TemperatureLog, vẫn chặn; lưu mẫu mẻ Đã đóng gói + đo 65 °C → Đang giao; bàn giao 58 °C → lưu lần đo, chặn; 62 °C → B Đã giao (đơn B Delivered); A chưa đo tại điểm → chặn, nhận > giao → chặn; 63 °C giao 58 / nhận 56 → thiếu 2, từ chối 2 → phản ánh PA- POD / SHORTAGE, chuyến Completed; in biên bản mở được; đối soát A (phục vụ 57 > 56 → chặn; 50 + 4 + 3 > 56 → chặn; 50 / 4 / 2 → chênh −10, −16,67%, 1.500.000, Variance), đóng thiếu ghi chú → chặn, có ghi chú → Closed, đơn A Reconciled; B phục vụ 40 → Matched 1.200.000, đóng không cần ghi chú. Mẻ chưa đóng gói ("ME-261221-D2-02 (Đạt)") chặn xuất phát; CRUD cũ (DeliveryRoute / DeliveryEvent / ProofOfDelivery / OrderReconciliation / CustomerComplaint / ComplaintHistory Insert) → 400; xóa chuyến có lần đo → chặn, chuyến chưa đo → xóa được, đơn trở lại chờ xếp chuyến; chuyến thứ 2 cùng xe số `-02`. Phản ánh: mức độ sai / đơn khác khách → 400; tạo `PA-261221-NNNN` High → hạn +4 giờ, mẻ trong mô tả; Resolve khi Mới → 400; Assign → AddComment → Resolve (bắt buộc nội dung) → Reject khi Đã xử lý → 400 → Close; lịch sử CREATED / ASSIGNED / COMMENT / RESOLVED / CLOSED; Update khi Đóng → 400; LOW nhận cách 80 giờ → quá hạn, từ chối → hết quá hạn.
  - `Delivery/DeliverySqlServerTests` (1, SQL Server dev): chuyến `CX-991229-<XE>-NN` + đơn giao `PH-991229-NNNN` (sp_getapplock) → đo 57,5 °C (khắc phục) → xuất phát bị chặn → xuất phát 65 °C → đến điểm → bàn giao 64 °C + POD → chuyến Completed → đối soát 48 / 50 (−4%, 1.440.000) → đóng (đơn Reconciled) → phản ánh PA- tạo / giao / xử lý / đóng (4 dòng lịch sử) – trong transaction ngoài rồi rollback, không để lại dữ liệu (đã chạy, không bị bỏ qua).
  - `Production/ProductionTests` sửa theo D-42 (đạt QC khi chưa lưu mẫu → Passed + cảnh báo).
- `git status`: không có thay đổi trong `app-delicare-client/`; không file mới trong `Controllers/V1/`, `Api/`, `AppDelicare.Application/`; không sửa PurchaseOrder* / GoodsReceipt* / QcInspection*, `ApprovalRequestService`, `IApprovalHandler`, seed ObjectMenu / UserRole, User / Person / Login, PermissionFilter.
- Script SQL: **không có** SP / seed mới (dùng SP `usp*_GetAll` sẵn có). Không chạy script nào lên DB dev.

AC (T19, bản Pilot):
- [x] Nhiệt độ giao < 60 °C chặn xuất phát và bàn giao (lần đo gần nhất; dưới ngưỡng bắt buộc khắc phục, vẫn lưu sổ).
- [x] Mẻ chưa lưu mẫu chặn xuất phát, thông báo liệt kê số ME- (D-42); mẻ chưa đóng gói chặn xuất phát.
- [x] Định dạng `CX-YYMMDD-<XE>-NN`, `PA-YYMMDD-NNNN` (+ `PH-` đơn giao).
- [x] Đối soát tính tay được (đặt / giao / nhận / phục vụ / trả về / hủy, chênh lệch, giá trị); đơn → Đã giao → Đã đối soát.
- [x] Vòng đời phản ánh (tạo / giao xử lý / đã xử lý / đóng / từ chối / ghi chú, lịch sử, hạn SLA, quá hạn khi xem).
- [x] 1 test SQL Server rollback: chuyến → xuất phát → bàn giao / POD → đối soát (+ phản ánh).
- [ ] Cảnh báo SLA tự động / leo thang, báo cáo suất theo điểm / ngày / buổi → DEFERRED(PILOT).
- [ ] `DELICARE_API_Catalog.xlsx` không sửa (theo điều phối). Gợi ý cột J / L (M = T19): 10.2 GET routes = Đã xong (`DeliveryRoute/GetBoard`, `GetDetail`); routes/plan = Một phần (`DeliveryRoute/Create`, 1 xe / lần, không chia khu vực); POST/PUT routes = Một phần (`Create`, `Update` khi Kế hoạch); routes/{id}/stops = Một phần (`DeliveryStop/Update`); load = Chưa bắt đầu; dispatch = Đã xong (`DeliveryRoute/Dispatch`); complete = Đã xong (tự động khi mọi điểm xong). Delivery-orders thủ công / split / route / cancel = Chưa bắt đầu (GET = CRUD `DeliveryOrder/GetAll`). 10.3 executions GET = Một phần (`DeliveryRoute/GetDetail`); arrive = Đã xong (`DeliveryStop/Arrive`); pod = Đã xong (`DeliveryStop/Deliver`, chưa ghi PackagingLedger); fail = Đã xong (`DeliveryStop/Fail`); events = Một phần (`GetDetail.listEvent`, `DeliveryEvent/GetAll`). 10.5 complaints GET = Một phần (CRUD GetAll; `isOverdue` ở GetDetail); POST = Đã xong (`CustomerComplaint/Create`); transition = Đã xong (`Assign` / `Resolve` / `Close` / `Reject`); history = Đã xong (`GetDetail.listHistory`). 6.7 reconciliations GET = Một phần (`GetPendingOrders`, `GetAll`; chưa có chart); reconcile = Đã xong (`OrderReconciliation/Reconcile`); close = Đã xong (`Close`). In biên bản giao nhận = Một phần (`DeliveryRoute/ExportDeliveryNote`).

DEFERRED(PILOT):
- Xếp hàng lên xe (`DeliveryLoading`, trạng thái Loading), `DeliveryExecution`, PackingOrder / PackingResult khi đóng gói, PackagingLedger OUT / RETURN (T20).
- Lập chuyến nhiều xe / theo khu vực, tách / chuyển / hủy đơn giao tay, 1 đơn nhiều điểm giao (GAP-ORD-01).
- Cảnh báo / leo thang SLA phản ánh và cảnh báo nhiệt độ tự động (T23), báo cáo suất theo điểm / ngày / buổi, biểu đồ Đặt – Giao – Nhận (spec 6.7 chart), KPI giao đúng giờ.
- Gửi Zalo cho khách khi xuất phát; tọa độ GPS liên tục.
- `ProducedQty` / `PackedQty` trong đối soát (cần gắn mẻ ↔ đơn).
- `allowedActions` của mẻ Đã đóng gói chưa có `recordSampleRetained` (API đã nhận) – để T17.1 bổ sung.

Câu hỏi cho anh (DECISIONS D-42, GAP-DLV-01…07):
1. Xuất phát bị chặn khi **còn mẻ của ngày / ca chưa đóng gói xong** – đúng thực tế (hay cho xe chạy khi đủ món của các điểm trên chuyến)?
2. Nhiệt độ: đo **1 lần lúc xuất phát** (cấp chuyến) + **1 lần mỗi điểm khi bàn giao** – đủ chưa, hay cần đo theo từng thùng / món?
3. Bàn giao dưới 60 °C: hiện **không cho bàn giao** (phải báo thất bại để giao bù) – hay cho khách nhận kèm biên bản?
4. Đối soát tính tiền theo **suất thực tế phục vụ** (spec 6.7) – hay theo suất đặt / số tối thiểu hợp đồng / max(đặt, phục vụ)? Cấu hình `ReconciliationBillingBasis` đổi được.
5. Suất bị từ chối khi bàn giao có tự tạo phản ánh (mức Trung bình) không?
6. Mẫu "Biên bản giao hàng" thật (nếu có) để em sửa file in.

Cần anh: không có script SQL mới; khai báo `Vehicle.Code` (mã ngắn dùng trong số chuyến), tài xế (Employee) và `DeliveryWindowFrom` của điểm giao trước khi chạy thử.

### T50R1 (phần Pilot) + T48.1 + phần còn lại của T-MERGE (Pilot P9) · 2026-11-17 · DONE(PILOT)
Nhánh `feat/T50R1-pilot-flow` (từ `release/pilot`). Backend + tài liệu + deploy staging; không đụng `app-delicare-client/src`, `docs/reference/*`.

Kế hoạch:
1. T-MERGE còn lại: `DB/README.md` mục "Pilot" (SP / seed theo thứ tự), `Deploy-Database.ps1` + `db-init.sh` đủ seed, chạy lại trên DB dev bằng `sqlcmd -b -I -f 65001`.
2. `docs/api-tests/pilot-flow.http` – 1 luồng P3 → P8, 1 bộ `[DEMO]`, có các chốt an toàn; test tự động qua HTTP (transaction ngoài rồi rollback).
3. Chạy API thật trên DB dev, chạy luồng; lỗi tìm được → sửa + test.
4. `UAT_CHECKLIST.md` mục PILOT; `docs/frontend-handoff/README.md`.
5. T48.1: deploy staging (DB + backend + Angular), smoke test.

Đã làm:
- **DB / deploy:** `DB/README.md` mục "Pilot" (11 script theo thứ tự chạy, so với `9d03b40`). `deploy/db/Deploy-Database.ps1` **bị hỏng từ T48.0** (đoạn băm mật khẩu admin bị chèn lặp 3 lần – chuỗi `'PBKDF2$100000…` không đóng, file không chạy được) → viết lại đoạn cuối, kiểm cú pháp bằng PowerShell parser, chạy thật trên staging; thêm `FoodSafetyChecklist_seed.sql` vào danh sách seed của `Deploy-Database.ps1` và `deploy/docker/db-init.sh`. DB dev: chạy lại 4 SP + 6 seed Pilot theo thứ tự với `-f 65001` (exit 0; `ObjectMenu_disable_legacy` lần chạy lại tắt 0).
- **Tên tiếng Việt lỗi font** trên DB dev / staging (seed từng chạy thiếu `-f 65001`: tên + ghi chú 4 ca, tên công ty DLC, ghi chú 33 đơn vị, mục "Duyệt – Đơn đặt suất ăn"): script mới `deploy/db/Repair-VietnameseText.ps1` (giải mã ngược Windows-1252 → UTF-8, chỉ sửa giá trị giải mã được, UPDATE từng dòng theo Id + giá trị cũ, ModifiedBy = admin, mặc định chạy thử) – dev sửa 44 giá trị, staging 9; chạy lại: 0.
- **`docs/api-tests/pilot-flow.http`** (117 request): danh mục tối thiểu (đơn vị, kho, NL mát / khô, NCC + bảng giá, 2 món + công thức gửi duyệt / duyệt / khóa, khách + điểm giao 11:00, hợp đồng + suất TRUA 35.000, xe, dây chuyền 1000 suất / mẻ, 2 lô bị chặn) → thực đơn tuần → phân công → đơn 1000 suất → chốt → MRP → PO 62,4 triệu → hộp duyệt GĐ → nhận hàng + QC → nhập kho → kế hoạch → phát hành → 2 mẻ → xuất NL → nấu / nhiệt độ lõi / lưu mẫu / sản lượng / đạt / đóng gói → chuyến → xuất phát → bàn giao + POD → đối soát → phản ánh. Biến + `# @name` theo cú pháp REST Client; 8 chốt an toàn ghi "→ 400".
- **Test `AppDelicare.Tests/Pilot/PilotFlowSqlServerTests.cs`** (SQL Server dev, `[SkippableFact]`): cùng luồng **qua HTTP thật** (routing, model binding, PermissionFilter, controller → service). Giữ 1 transaction ngoài qua nhiều request bằng `IStartupFilter` đặt `HttpContext.RequestServices` = 1 scope dùng chung có DbContext đang mở transaction (UnitOfWork tham gia transaction ngoài); cuối test rollback, kiểm số dòng Order / MaterialLot / InventoryTransaction / ProductionBatch / DeliveryRoute / CustomerComplaint / TemperatureLog không đổi. Kiểm: số PH- / PO- / NL- (2 lô khác số) / ME- / CX- / PA-, tổng 35.000.000 / 62.400.000, blockedQuantity 80, hộp duyệt scm-po không có PO / scm-po-director có, QC 9 °C bị chặn, xuất NL không lấy lô hết hạn / KHONG_DAT, lõi 65 °C chặn đạt QC, xuất phát bị chặn vì 55 °C và vì mẻ chưa lưu mẫu, bàn giao 58 °C bị chặn, đối soát 34.650.000, đơn Reconciled, 2 phản ánh.
- **Chạy thật trên DB dev** (`dotnet run` WebApi Development, `http://localhost:5125`), dữ liệu `[DEMO]` để lại trên dev:
  - **P9A** (PowerShell `Invoke-RestMethod`, ngày ăn 25/11/2026): chạy hết luồng nhưng lộ 2 lỗi (dưới). Số: đơn `PH-261117-0003`, `KH-261125`, đề nghị `PH-261117-0004`, `PO-261117-0001`, `XN-261117-005`, phiếu nhập `PH-261117-0005`, 2 lô **cùng số** `NL-261117-DEMOP9A-01` (lỗi – giữ nguyên để đối chiếu), mẻ `ME-261125-TRUA-01/02`, `CX-261125-DEMOP9A-01`, `PA-261117-0001/0002`.
  - **P9B** (PowerShell, sau khi sửa, ngày 26/11/2026): **88/88 bước đúng kỳ vọng**. Đơn `PH-261117-0012` (35.000.000 → Reconciled), `KH-261126`, đề nghị `PH-261117-0013`, `PO-261117-0002` (62.400.000, needsDirectorApproval, chỉ ở hộp `scm-po-director`, yêu cầu `XN-261117-008`), phiếu nhập / QC `PH-261117-0014`, lô `NL-261117-DEMOP9B-01` (bò 200) / `-02` (gạo 120), lệnh SX `PH-261117-0015/0016`, mẻ `ME-261126-TRUA-01` (bò kho) / `-02` (cơm) 08:30, phiếu xuất `PH-261117-0017/0018`, chuyến `CX-261126-DEMOP9B-01` + đơn giao `PH-261126-0001` → Completed, đối soát −10 (−1%) 34.650.000, phản ánh `PA-261117-0003` (tự động khi bàn giao) + `PA-261117-0004`.
  - **P9C** (chạy chính file `pilot-flow.http` bằng trình chạy nhỏ đọc cú pháp REST Client, ngày 02/12/2026): **117 request đúng kỳ vọng**. Đơn `PH-261117-0021` → Reconciled, `PO-261117-0003` → Received, lô `NL-261117-DEMOP9C-01/02`, mẻ `ME-261202-TRUA-01/02` Packed, `CX-261202-DEMOP9C-01` Completed, `PA-261117-0005/0006`.
- **Staging (T48.1):** `Deploy-Database.ps1` trên `AppDelicare_Staging` (131 SP + 6 seed, không đụng bảng) + sửa lỗi font; `Publish-Iis.ps1 -Output C:\Deploy\delicare-staging -Environment Staging`; `npm run build` rồi chép `dist\w3crm\browser` vào `wwwroot` (cùng địa chỉ API, Angular dùng `/api/` + `#/`); chạy `dotnet AppDelicare.WebApi.dll --urls http://localhost:5200` với biến môi trường của `staging.local.env.ps1`. Smoke test: `/health` Healthy (Staging), Swagger 200, đăng nhập admin, GET 200 cho `User/GetCurrent`, `Menu`, `Order`, `MaterialLot`, `InventoryBalance/GetSummary`, `MaterialRequirement/GetByDate`, `PurchaseRequest`, `PurchaseOrder`, `GoodsReceipt`, `QcInspection`, `ApprovalRequest/GetMyPending`, `ProductionPlan`, `ProductionBatch/GetBoard`, `DeliveryRoute/GetBoard`, `OrderReconciliation/GetPendingOrders`, `CustomerComplaint`, `Shift` (tên ca đúng dấu); trang `/` trả giao diện DELI-CARE. IIS thật: không làm (cần quyền admin – anh / IT theo deploy/README.md mục 4). Docker: máy chưa có Docker. `deploy/README.md` thêm mục 8 (Angular trên staging) + 9 (cập nhật staging).
- `UAT_CHECKLIST.md` mục **PILOT** (22 kịch bản API theo thứ tự request, vai trò gợi ý, kỳ vọng, các chốt an toàn) + ghi chú **PILOT-FE** (sau đợt FE, D-40). `docs/frontend-handoff/README.md` (mục lục P0-P2 … P8).

Lỗi tìm được và đã sửa (đều có test):
1. **Danh sách rỗng khi không lọc theo cha** – 35 SearchRequest còn lọc `Guid` không null (`MaterialLot.IngredientId`, `Order.CustomerId`, `Recipe.DishId`, `ProductionBatch.ProductionOrderId`, `MaterialIssue`, `InventoryBalance`, `InventoryTransaction`, `MenuAssignment`, `OrderReconciliation`, `DeliveryOrder`, `ProofOfDelivery`, `StockCount`…) → bỏ trống bị bind thành Guid rỗng, SP lọc `= 0000…` → `GetAll` luôn 0 dòng (màn Lô, Đơn hàng, Công thức, Mẻ… trống). Sửa như FE-2: thuộc tính → `Guid?`, 58 chỗ truyền tham số trong service đổi Guid rỗng → NULL. Test: `SearchFilterSqlServerTests.Pilot_lists_without_parent_filter_return_all_rows` (SQL Server, so với số dòng DB) + `Every_guid_filter_of_search_requests_is_nullable` (chặn tái phát cho mọi SearchRequest). `StorageLocation.WarehouseId`, `UomConversion.FromUnitId / ToUnitId`: service vốn không truyền lọc (ngoài Pilot – để nguyên).
2. **Trùng số chứng từ khi sinh nhiều mã trước 1 lần lưu** – phiếu nhập 2 dòng → 2 lô cùng số `NL-…-01` (cũng ảnh hưởng đề nghị mua nhiều NCC → nhiều PO cùng số). Nguyên nhân: trên SQL Server `DocumentNumberService` chỉ đọc MAX từ dòng đã lưu (nhánh InMemory có tính bản ghi đang theo dõi nên test InMemory không bắt được). Sửa gốc: cộng thêm mã của bản ghi `Added` đang theo dõi trong DbContext (dùng chung hàm `MaxOf` với nhánh InMemory). Test: `DocumentNumberSqlServerTests.Numbers_generated_in_a_loop_before_saving_are_distinct_on_sql_server` (3 lô → -01 / -02 / -03) + kiểm trong test luồng Pilot.
3. **`Deploy-Database.ps1` hỏng cú pháp** (từ T48.0) – xem trên; đã chạy thật trên staging.
4. **Test chập chờn** `UserAccountTests.Reset_returns_temp_password_…` (có từ trước: 1/169 lỗi ở baseline) – host test khác khởi động song song ghi đè `Serilog.Log.Logger` → chuyển lớp test vào collection không song song `StaticLoggerCollection`. 3 lần chạy liên tiếp 173/173.
Ghi chú (không phải lỗi): 2 lần đo nhiệt độ trong cùng 1 giây → lấy giá trị **thấp nhất** (thiết kế T19, an toàn) – test / `.http` ghi lần đo thấp kèm `MeasuredAt` lùi 10–15 phút như thực tế.

File chính: `AppDelicare.Services/Implement/DocumentNumberService.cs`; 35 file `AppDelicare.Model/SearchRequest/*SearchRequest.cs` + 33 service tương ứng (`AppDelicare.Services/Implement/*Service.cs`, chỉ dòng `dynamicParameters.Add`); test `Tests/Pilot/PilotFlowSqlServerTests.cs` (mới), `Tests/MasterData/SearchFilterSqlServerTests.cs`, `Tests/Common/DocumentNumberTests.cs`, `Tests/Admin/UserAccountTests.cs`, `Tests/Integration/ApiFactory.cs`; `DB/README.md`, `deploy/db/Deploy-Database.ps1`, `deploy/db/Repair-VietnameseText.ps1` (mới), `deploy/docker/db-init.sh`, `deploy/README.md`; `docs/api-tests/pilot-flow.http` (mới), `docs/claude/{UAT_CHECKLIST, ROADMAP, PROGRESS}.md`, `docs/frontend-handoff/README.md` (mới).

Kiểm tra:
- `dotnet build AppDelicare.sln --no-incremental`: 0 lỗi, 10 cảnh báo (≤ 11).
- `dotnet test`: **173/173** (+4: PilotFlow 1, SearchFilter 2, DocumentNumber 1), chạy 3 lần liên tiếp đều xanh; test SQL Server không bị bỏ qua.
- `npm run build` (`app-delicare-client`): 0 lỗi (1 cảnh báo cũ "Unable to locate stylesheet D:\assets\css\style.css").
- `git status`: không thay đổi trong `app-delicare-client/` (`dist` đã gitignore), `docs/reference/`; không file mới trong `Controllers/V1/`, `Api/`, `AppDelicare.Application/`; `api/v1` trong `*.cs` không tăng (3, đều trong test cũ).

AC (P9): [x] `pilot-flow.http` chạy hết P3 → P8 bằng 1 bộ `[DEMO]` (chạy thật trên DB dev) · [x] test tự động end-to-end qua HTTP · [x] UAT_CHECKLIST mục PILOT (API) · [~] mục PILOT phần thao tác trên giao diện → sau đợt FE (D-40) · [x] deploy staging backend + bản build Angular, smoke test · [ ] IIS thật (cần quyền admin) · [ ] Catalog (không có openpyxl).

`DELICARE_API_Catalog.xlsx` – **chưa cập nhật** (máy không có python / openpyxl; T-MERGE: không tự gộp file nhị phân). Gợi ý cho anh / lần sau (cột J Trạng thái, K ghi chú, M Task):
- Theo gợi ý đã ghi cuối các mục T13 (6.2 / 6.3 / 6.6), T16 (7.5 – 7.8), T14 (7.1), T15 (7.2 – 7.4, 9.1, 9.3, 9.5), T17 (8.1 – 8.4, 9.3), T19 (6.7, 10.2, 10.3, 10.5) và T45R.8 (dòng 34, 35, 38) phía trên.
- T50R1 không thêm action mới; các dòng GET danh sách của Lô / Đơn hàng / Công thức / Mẻ / Lệnh SX / Phiếu xuất / Kiểm kê / Phân công thực đơn / Đối soát / Đơn giao / POD: cột K thêm "T50R1: bỏ trống lọc cha = không lọc".
- Cột M thêm `T50R1` cho các dòng thuộc luồng P3 → P8 đã chạy end-to-end (đơn, MRP, PR, PO, nhận hàng, QC, kế hoạch, mẻ, chuyến, POD, đối soát, phản ánh).

Câu hỏi còn mở (PROPOSED – gom từ các task Pilot, chi tiết ở DECISIONS):
1. GAP-ORD-01…03, GAP-MENU-01/02: 1 đơn = 1 điểm giao; lựa chọn món lưu ở `MenuLine.SequenceNo`; xử lý đơn chưa xác nhận lúc chốt 20:00 D-1; lệch suất so với số đã xác nhận (chưa so trung bình 4 tuần), ai duyệt lệch suất (seed chỉ CEO).
2. GAP-INV-02…11: phiếu xuất tay không gắn lệnh SX; lô chưa có kết quả QC vẫn cho xuất; ngưỡng kiểm kê 2% theo dòng; điều chỉnh tay chưa cần duyệt.
3. GAP-MRP-02/04/08 + T14.1: chi tiết MRP trong Note (JSON); món chung nhiều lựa chọn không cộng dự phòng; cách tính "đã đề nghị".
4. GAP-PUR-01/02/05/06/07/09/10: không duyệt đề nghị mua; PO bị từ chối về Nháp; hàng không đạt không nhập kho; dung sai nhận 10%; % VAT theo nhóm NL (đang 8% mọi dòng); cờ "ưu tiên dùng trước" chỉ trong Note.
5. GAP-PRD-01…08 + T17.1 (a)–(f): chia phần theo ca, trạng thái phát hành theo ca, giờ xuất bếp nhập tay chỉ khi 1 ca, đơn nhiều ca, mã ca trong số mẻ.
6. GAP-DLV-01…07: chặn xuất phát khi còn mẻ của ngày / ca chưa đóng gói; đo nhiệt độ 1 lần / chuyến + 1 lần / điểm; bàn giao < 60 °C không cho nhận; tính tiền theo suất phục vụ; tự tạo phản ánh khi có suất từ chối; mẫu Biên bản giao hàng thật.
7. GAP-ADM-04/05: quy tắc tên đăng nhập; `IsActive` mặc định false.
8. Mới (T50R1, GAP-DOC-01): số đơn giao `PH-` lấy theo **ngày giao** (`PH-261126-0001`), chứng từ khác theo ngày lập – giữ hay thống nhất? Hai lần đo nhiệt độ cùng giây lấy giá trị thấp nhất – giữ?

Cần anh:
- UAT-PILOT theo `UAT_CHECKLIST.md` mục PILOT trên staging `http://localhost:5200` (chạy lại theo deploy/README.md mục 9 bước 4; mật khẩu admin staging trong `deploy/staging.local.md`).
- Duyệt / commit nhánh; chốt các câu hỏi trên; IIS thật nếu chọn IIS (deploy/README.md mục 4); cập nhật Catalog.

### BE2.1 – Lấp chỗ thiếu của luồng Pilot · 2026-11-17 · DONE
Nhánh `feat/BE2.1-flow-gaps` (worktree `wt-BE21`, từ `release/pilot` @ `f9b3231`). Chỉ backend (D-49); không đụng `app-delicare-client/`, `docs/reference/*`, `deploy/db/Deploy-Database.ps1`, `deploy/docker/db-init.sh` (T48.3 đang sửa song song).

Kế hoạch:
1. Rà soát: PackingOrder / PackingResult / DeliveryLoading / DeliveryExecution / DeliveryOrder chỉ có CRUD sinh mã (ghi tay được mọi trường); DeliveryRoute / Stop, ProductionBatch, ProductionPlan, MaterialRequirement, TemperatureLog, MealWasteRecord, Order, Menu, CustomerComplaint, ApprovalRequest, MaterialLot đã có nghiệp vụ T13–T19 → bổ sung method vào đúng service + action vào đúng controller (không service / controller mới; thêm 1 lớp tĩnh dùng chung `TraceRules` như `DeliveryRules`).
2. Đóng gói từ mẻ (171–175) + tách `CompleteOrderIfDoneAsync` sang `ProductionRules` dùng chung.
3. Đơn giao tay / tách / gán – chuyển / hủy (184–187), lập chuyến nhiều xe theo khu vực + đổi thứ tự (177–179), xếp hàng lên xe (180), thực hiện giao (188).
4. Kế hoạch sinh theo ca (137), đề nghị mua chọn dòng + dự phòng (107).
5. Nhiệt độ tự do (165), hao hụt (156), bảng đơn (90), thực đơn (82), phản ánh (197), chi tiết + hủy yêu cầu duyệt (211, 213), truy xuất 2 chiều (118, 119).
6. Test InMemory + SQL Server rollback; `.http` chạy thật trên DB dev; handoff; DECISIONS / ROADMAP / BUSINESS_RULES.

Đã làm (mẫu 4.2; quyền theo `PermissionConvention`, ngoại lệ ghi `[Permission]` / `[SkipPermission]`):
- **Đóng gói** (`PackingOrderService` / `PackingOrderController`, `log-packing-order`): `GetList`, `GetByBatch`, `GenerateFromBatch` (Thêm – mẻ Đạt QC + lõi ≥ 70 °C + đã ghi sản lượng; số phần đạt còn lại chia cho đơn Đã chốt / Đang SX cùng ngày + ca theo tỉ lệ suất món, phần dư lớn nhất; lệnh `PH-`), `Create` (Σ ≤ số đạt), `Start`, `RecordResult` (đóng gói + hỏng + loại = kế hoạch; `PackingResult`; hỏng / loại → `MealWasteRecord` PACKING_DAMAGE + WasteQty mẻ; mẻ chia hết + mọi lệnh xong → mẻ Đã đóng gói → lệnh SX / kế hoạch hoàn thành), `Cancel` (chỉ Kế hoạch, lý do). `ProductionBatch/Pack` chặn khi còn lệnh chưa xong. CRUD PackingOrder / PackingResult → 400.
- **Xếp xe / chuyến** (`DeliveryRouteService`): `Load` (JSON; DeliveryLoading Loaded + DeliveryExecution LOADED, chuyến → Đang xếp, sự kiện LOADED; danh sách trống = đơn giao chưa xếp), `Plan` (JSON, nhiều xe theo khu vực), `UpdateStops` (JSON); `Create` nhận thêm `listDeliveryOrderId`, đơn hàng lấy **phần suất còn lại**; tài xế trùng khung giờ → chặn (Create / Update). `Dispatch`: chặn khi còn đơn giao chưa xếp (`BusinessRules:DeliveryRequireLoading` = true), thực hiện giao → DISPATCHED. `DeliveryStop/Arrive / Deliver / Fail` cập nhật thực hiện giao (ARRIVED / DELIVERED / FAILED); đơn hàng → Đã giao chỉ khi không còn suất chưa lập đơn giao. Chi tiết chuyến thêm `loadedQty`, `executionStatus`; `allowedActions` thêm `load`, `updateStops` (Kế hoạch không còn `dispatch`).
- **Đơn giao** (`DeliveryOrderService` / `DeliveryOrderController`, `log-delivery-order`): `GetList`, `Create` (JSON), `Split` (JSON), `AssignRoute`, `Cancel`; số suất ở tag `[SUAT:n]` (GAP-DLV-08); đánh số lại điểm dừng 2 bước qua số âm vì UNIQUE (route, sequence) tính cả dòng xóa mềm (GAP-DLV-10). CRUD Insert → 400; Update giữ tag.
- **Thực hiện giao** (`DeliveryExecutionService` / `Controller`, `log-execution`): `GetBoard` + `statusCounts`; CRUD DeliveryExecution / DeliveryLoading → 400.
- **Kế hoạch** `ProductionPlan/Generate` (Thêm – 1 KH- / ngày từ đơn Đã xác nhận / Đã chốt, `ShiftId` dòng khi món thuộc 1 ca; chặn khi đã Duyệt / đã tính nhu cầu NL) – dùng `ProductionRules.DishDemandsAsync` (mới, cùng công thức MRP).
- **MRP** `MaterialRequirement/CreatePurchaseRequest` (JSON – dòng chọn + % dự phòng, làm tròn lên 4 số lẻ; cập nhật đề nghị Nháp tại chỗ; đã gửi duyệt → chặn).
- **Nhiệt độ** `TemperatureLog/Record` (Thêm `qc-temperature`; ngưỡng khu kho / HOT_HOLDING ≥ 60; CORE / DELIVERY / RECEIVING → chặn; ngoài ngưỡng → lưu + cảnh báo); CRUD Insert dùng cùng logic (bỏ IsWithinRange / RecordedBy của client). Gỡ header auto-generated của `TemperatureLog*`, `MealWasteRecord*`.
- **Hao hụt** `MealWasteRecord/Record` (Thêm `prod-waste`; mẻ / lệnh SX / món + ca; loại theo `ReferenceLists:WasteType`) + `GetList` (tổng suất / kg, theo loại); CRUD Insert → 400.
- **Danh sách**: `Order/GetBoard` (8 cột + statusCounts, lọc BranchId của khách), `Menu/GetList` (lineCount, dishCount, assignedCustomerCount, orderedMeals), `CustomerComplaint/GetList` (đủ trường, isOverdue, hoursToDue, statusCounts, overdueCount, phân trang), `MaterialLot/GetList` (tồn, hạn, bị chặn, sắp hết hạn).
- **Duyệt** `ApprovalRequest/GetDetail` + `Cancel` (`[SkipPermission]` như GetMyPending / Decide, service kiểm người gửi / người duyệt / admin); `IApprovalHandler.CancelAsync` mặc định = xử lý từ chối (handler cũ không sửa); ghi ApprovalDecision CANCEL.
- **Truy xuất** `MaterialLot/Trace` (xuôi) + `ProductionBatch/Trace` (ngược) – `TraceRules` (lớp tĩnh), liên kết suy luận đánh dấu `inferred` (GAP-TRC-01).
- Cấu hình: `BusinessRules:DeliveryRequireLoading` = true; `ReferenceLists:WasteType` + `PACKING_DAMAGE`; `ReferenceLists:TemperatureMeasurementType` (mới). Thông báo tiếng Việt trong `ConstantHelper.Message`. Hằng `DeliveryLoadingStatus`, `DeliveryExecutionStatus`, `PackingOrderStatus`, `DeliveryOrderStatus.Cancelled` (`Model/Helper/DeliveryStatus.cs`).
- **Không có SP / seed mới**, không thêm bảng / cột, không mục menu mới (mọi screen-id đã có trong `ObjectMenu_seed.sql`) → `deploy/db/scripts.order` (T48.3) không cần thêm dòng nào.

File chính: `AppDelicare.Services/Implement/{PackingOrderService, PackingResultService, DeliveryRouteService, DeliveryOrderService, DeliveryExecutionService, DeliveryLoadingService, DeliveryStopService, DeliveryRules, ProductionRules, ProductionBatchService, ProductionPlanService, MaterialRequirementService, TemperatureLogService, MealWasteRecordService, OrderService, MenuService, CustomerComplaintService, ApprovalRequestService, MaterialLotService, TraceRules (mới)}.cs`, interface tương ứng + `IApprovalHandler.cs`, 14 controller, `Model/Helper/{DeliveryStatus, ConstantHelper}.cs`, `Model/Option/BusinessRulesOptions.cs`, `Model/Model/{DeliveryModels, FlowGapModels (mới)}.cs`, `Model/Request/Custom/{DeliveryCustomRequest, MaterialRequirementCustomRequest, ProductionCustomRequest, PackingCustomRequest (mới), FlowGapCustomRequest (mới)}.cs`, `WebApi/appsettings.json`; test `Tests/Delivery/{FlowGapDeliveryTests, FlowGapSqlServerTests}.cs`, `Tests/Production/FlowGapTests.cs` (mới) + sửa `DeliveryTests`, `DeliverySqlServerTests`, `ProductionShiftTests`, `PilotFlowSqlServerTests` (thêm bước Load); `docs/api-tests/be2.1-flow-gaps.http` (mới), `pilot-flow.http`; `docs/frontend-handoff/BE2.1.md` (mới), `README.md`, `P7.md`, `P8.md`; `docs/claude/{DECISIONS, ROADMAP, BUSINESS_RULES, PROGRESS}.md`, `DB/README.md`.

Kiểm tra:
- `dotnet build AppDelicare.sln --no-incremental`: 0 lỗi, 11 cảnh báo (cảnh báo cũ, không thêm mới).
- `dotnet test`: **185/185** (baseline 173 + 12: FlowGapDeliveryTests 4, FlowGapTests 7, FlowGapSqlServerTests 1 – SQL Server dev, không bị bỏ qua; 4 test cũ sửa để xếp hàng trước khi xuất phát).
  - FlowGapDeliveryTests: sinh lệnh đóng gói 100 → A 60 / B 40; chia hết thì chặn sinh lại / lập tay vượt / Pack thẳng; hủy B → còn 40 → lập tay lại; kết quả lệch tổng → chặn; 58 + 1 + 1 → hao hụt PACKING_DAMAGE 2; lệnh cuối → mẻ Packed, lệnh SX Completed; mẻ lõi 65 °C / chưa Đạt QC → chặn. Đơn giao tay 25 (điểm Quận 7) → vượt 35 còn lại chặn, điểm của khách khác chặn → tách 15 + 10 → chuyến (35 còn lại + 15) → gán 10 vào đầu → đổi thứ tự → chuyển sang chuyến B (đánh số lại cả 2, điểm cũ xóa mềm) → hủy đơn giao B (lý do bắt buộc) → xếp hàng 34 / 35 (cảnh báo) → chưa xếp hết thì chặn xuất phát; đã xếp thì không hủy / không gán vào chuyến Đang xếp → xuất phát → bảng thực hiện giao (DISPATCHED 2, READY 2, đơn Hủy không hiện) → đến điểm → ARRIVED; CRUD cũ chặn. Lập chuyến 2 xe: Quận Tân Bình (A 60 + C 30) → xe 100, TP. Thủ Đức (B 40) → xe 50, `CX-261217-XAR1-01`, hết hàng thì chặn.
  - FlowGapTests: Generate (Đã xác nhận + Đã chốt, bỏ Nháp: cơm 150 / gà 100 ca trưa / cá 50 ca tối; sinh lại cập nhật tại chỗ; đã duyệt chặn); CreatePurchaseRequest (dự phòng 5% → 105 / 35; chọn lại 10% → 110, dòng gạo xóa mềm; đã gửi duyệt chặn); nhiệt độ (thiếu đối tượng / CORE / DELIVERY / loại lạ chặn; 7 °C ngoài 0…5 → lưu + cảnh báo, server tính IsWithinRange; HOT_HOLDING 55 không đạt; CRUD Insert tính ở server); hao hụt (loại sai / thiếu số / mẻ chưa nấu / không đối tượng chặn; WasteQty mẻ 5; tổng 5 suất 5,5 kg); bảng đơn 8 cột / BranchId / tìm; thực đơn 3 dòng 2 món 1 khách 80 suất; phản ánh quá hạn 1 (hoursToDue −4) / lọc mức độ; duyệt: chi tiết công thức + dòng, người lạ không hủy / không xem, hủy → công thức Draft, Cancelled + CANCEL, admin hủy được của người khác; truy xuất lô → NCC / PO / phiếu nhập / mẻ 1 / lệnh đóng gói / đơn A / khách A, ngược mẻ 2 → đơn suy luận A + B, mẻ 1 → lô → NCC + lõi 78 °C; danh sách lô sắp hết hạn.
  - FlowGapSqlServerTests: đóng gói (số PH- qua sp_getapplock) → mẻ Packed; đơn giao tay → chuyến → tách → gán vào số 1 → đảo thứ tự → hủy (số âm) → lập lại + gán cuối (tái dùng số 3) → xếp hàng 3 dòng → xuất phát → 3 DISPATCHED; rollback, số dòng không đổi.
- **Chạy thật trên DB dev** (`dotnet run` WebApi Development, cổng 5321, trình chạy nhỏ đọc cú pháp REST Client – kiểm `responseCode` 200 / 400 theo đánh dấu "→ 400"):
  - `be2.1-flow-gaps.http` **138/138** (11 chốt an toàn trả 400) – chạy 3 lần (key B21H 30/12/2026 – lộ 1 lỗi ở tiêu đề của file .http (đánh dấu "→ 400" nhầm), đã sửa; B21J 06/01/2027; B21K 13/01/2027). Số chứng từ lần B21K: đơn `PH-261117-0074/0075/0076` (300 / 200 / 150 → Đang SX), kế hoạch `KH-270113`, đề nghị `PH-261117-0077` (rau 68,25 = 65 × 1,05 → Đã lập PO), PO gửi duyệt rồi người gửi hủy (về Nháp), mẻ `ME-270113-TRUA-01` → 3 lệnh đóng gói `PH-261117-0080/0081/0082` → Packed, đơn giao `PH-270113-0001` (60, Hủy) … `-0006`, chuyến `CX-270113-DEMOB21KX1-01` (Quận 7: 150 + 200 + 40 = 390 / 400) và `CX-270113-DEMOB21KX2-01` (Quận 1: 200 + 60) → Đang giao, 5 đơn giao DISPATCHED.
  - `pilot-flow.http` **121/121** (thêm lệnh đóng gói mẻ 1 + chốt Pack thẳng + bước Load) – chạy 2 lần (B21B 16/12/2026, B21C 23/12/2026).
  - Dữ liệu `[DEMO]` để lại trên DB dev (như T50R1). Không chạy script SQL nào.
- `git status`: không thay đổi trong `app-delicare-client/`, `docs/reference/`, `deploy/`; không file mới trong `Controllers/V1/`, `Api/`, `AppDelicare.Application/`; không `api/v1` mới.

AC (BE2.1): [x] mọi mục phạm vi có API + test · [x] chốt ATTP giữ nguyên (lõi ≥ 70 °C kiểm lại khi đóng gói, giao ≥ 60 °C, lưu mẫu trước khi giao, lô hết hạn / KHONG_DAT bị chặn – không đổi) · [x] `.http` chạy thật · [x] handoff · [x] không bảng / cột / SP mới · [ ] Catalog Excel (máy không có openpyxl – bảng dưới).

Bảng cập nhật `DELICARE_API_Catalog.xlsx` (sheet `API_Catalog`, cột J / L / K; cột M = `BE2.1`):
| STT | Trạng thái (J) | API thực tế – khung cũ (L) | Ghi chú (K) |
|---|---|---|---|
| 82 | Đã xong | `GET Menu/GetList` | lineCount, dishCount, assignedCustomerCount, orderedMeals |
| 90 | Đã xong | `GET Order/GetBoard` | 8 cột + statusCounts; danh sách = `Order/GetAll` |
| 107 | Một phần | `POST MaterialRequirement/CreatePurchaseRequest` | Chưa gọi duyệt 2 cấp (BE2.3, D-46); làm tròn 4 số lẻ (D-50 g) |
| 118 | Đã xong | `GET MaterialLot/GetList` | + `MaterialLot/GetAll` (CRUD) |
| 119 | Đã xong | `GET MaterialLot/Trace`, `GET ProductionBatch/Trace` | Liên kết suy luận `inferred` (GAP-TRC-01); chưa QR (GAP-F-10) |
| 137 | Một phần | `POST ProductionPlan/Generate` | 1 KH- / ngày, tách ca ở dòng (D-50 a); chưa áp MenuChangeRequest (BE2.2) |
| 156 | Đã xong | `POST MealWasteRecord/Record`, `GET MealWasteRecord/GetList`, `ProductionBatch/RecordWaste` | Tổng hợp biểu đồ (157) để sau |
| 165 | Đã xong | `POST TemperatureLog/Record` | CORE / DELIVERY / RECEIVING qua nghiệp vụ; ngoài ngưỡng lưu + cảnh báo |
| 171 | Đã xong | `GET PackingOrder/GetList`, `GetByBatch` | |
| 172 | Đã xong | `POST PackingOrder/RecordResult` | |
| 173 | Đã xong | `POST PackingOrder/Create`, `GenerateFromBatch` | Sinh từ mẻ theo tỉ lệ suất (GAP-PCK-01) |
| 174 | Đã xong | `POST PackingOrder/Start` | |
| 175 | Đã xong | `POST PackingOrder/Cancel` | |
| 177 | Đã xong | `POST DeliveryRoute/Plan` | Khu vực theo AreaId / địa chỉ (GAP-DLV-09) |
| 178 | Đã xong | `POST DeliveryRoute/Create`, `PUT DeliveryRoute/Update` | Tài xế không trùng khung giờ |
| 179 | Đã xong | `PUT DeliveryRoute/UpdateStops` | |
| 180 | Đã xong | `POST DeliveryRoute/Load` | Bắt buộc trước Dispatch (D-50 b); chưa liên kết PackingOrder (GAP-C-03) |
| 181 | Đã xong | `POST DeliveryRoute/Dispatch` | (T19) + yêu cầu đã xếp hàng, thực hiện giao DISPATCHED; chưa gửi Zalo |
| 183 | Đã xong | `GET DeliveryOrder/GetList` | mealQuantity, routeNo, unassigned |
| 184 | Đã xong | `POST DeliveryOrder/Create` | Số suất ở tag Note (GAP-DLV-08) |
| 185 | Đã xong | `POST DeliveryOrder/Split` | |
| 186 | Đã xong | `POST DeliveryOrder/AssignRoute` | |
| 187 | Đã xong | `POST DeliveryOrder/Cancel` | |
| 188 | Đã xong | `GET DeliveryExecution/GetBoard` | + statusCounts |
| 197 | Đã xong | `GET CustomerComplaint/GetList` | isOverdue, hoursToDue, statusCounts |
| 211 | Đã xong | `GET ApprovalRequest/GetDetail` | business.record + lines |
| 213 | Đã xong | `POST ApprovalRequest/Cancel` | Cancelled + ApprovalDecision CANCEL (GAP-APR-01) |

PROPOSED chờ anh (DECISIONS **D-50**, mẫu D-49 – em đã cài theo "Đề xuất", không mục nào BLOCKED):
(a) Generate giữ 1 KH- / ngày (spec: mỗi ca 1 kế hoạch); (b) xếp hàng bắt buộc trước khi xuất phát; (c) số suất đơn giao ở tag `[SUAT:n]` trong Note (dài hạn: cột `DeliveryOrder.MealQuantity` – cần anh duyệt migration); (d) khu vực = AreaId / quận trong địa chỉ; (e) lệnh đóng gói chia theo tỉ lệ suất, vẫn cho Pack thẳng mẻ chưa có lệnh; (f) yêu cầu duyệt thêm trạng thái Cancelled; (g) dự phòng làm tròn 4 số lẻ, không lưu; (h) truy xuất suy luận + danh mục loại đo nhiệt độ.

Cần anh: không có script SQL mới. Khi gộp nhánh: appsettings có 3 khóa mới (DB/README.md); T48.3 không cần thêm dòng vào `deploy/db/scripts.order`. FE: đọc `docs/frontend-handoff/BE2.1.md` mục "Thay đổi hành vi" (xếp xe trước khi xuất phát, nhiều đơn giao / đơn).
