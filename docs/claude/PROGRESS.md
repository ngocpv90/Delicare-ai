# PROGRESS – Nhật ký tiến độ (Claude Code cập nhật sau mỗi task)

## Trạng thái hiện tại
- Release: **R1 – CHỈ BACKEND** · Lát hiện tại: **R1.1 Danh mục và quản trị** (R1.0 xong, chờ anh UAT song song)
- **⚠ ĐỔI HƯỚNG (D-30, anh chốt 2026-11-15): khung chuẩn = code cũ, không sinh thêm API `/api/v1`.** Xem CLAUDE.md mục 4, ROADMAP T45R.
- T45R, T45B.5 **DONE** (2026-11-15). Lát R1.1 còn: **T49** (nhập Excel danh mục) → **UAT-1**.
- Nhánh: `feat/T45R-legacy-framework` (từ `chore/rename-clinicmanagement` ← `feat/T45B.4-p2-special` ← … T00; chưa merge, chưa push)
- Build backend: OK – 0 lỗi, 10 cảnh báo (no-incremental; 2026-11-15) · Build frontend: ngoài phạm vi · Test: `AppDelicare.Tests` 79/79 xanh
- Việc anh cần làm: (1) UAT R1.0 theo `docs/claude/UAT_CHECKLIST.md` trên staging (deploy/README.md mục 3); (2) duyệt ma trận quyền (D-25) và 2 điểm PROPOSED của T45R (nhóm mới không có quyền; ResetPassword chỉ admin); (3) chốt D-26 (DeleteData), D-27 (AutoMapper – chỉ còn chuyện license), D-29 (băm mật khẩu); (4) đổi khóa đã lộ (SECRETS_TODO.md); (5) merge chuỗi nhánh.

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
