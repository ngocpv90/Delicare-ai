# PROGRESS – Nhật ký tiến độ (Claude Code cập nhật sau mỗi task)

## Trạng thái hiện tại
- Release: **R1 – CHỈ BACKEND** · Lát hiện tại: **R1.1 Danh mục và quản trị** (R1.0 xong, chờ anh UAT song song)
- **⚠ ĐỔI HƯỚNG (D-30, anh chốt 2026-11-15): khung chuẩn = code cũ, không sinh thêm API `/api/v1`.** Xem CLAUDE.md mục 4, ROADMAP T45R.
- **2026-11-16 (D-34): ưu tiên LÁT PILOT** (ROADMAP mục ★★). Task kế tiếp: **T48.0 (D-29 băm mật khẩu)** → **FE-0 → FE-3** (frontend màn hình đã có API, D-35) → T11 → T12 (1 cấp) → T13 + T46B.1 → T16 → T14 → T15 → T17 (theo mẻ) → T19 (tối thiểu) → UAT-PILOT. Cần anh: chạy T4 trong Visual Studio để xác nhận T45R.6; D-35 đã chốt (frontend Pilot; chạy thử 1–2 khách, 1 bếp, ca trưa). Tiến độ API: `docs/reference/DELICARE_API_Catalog.xlsx` (58/234 xong, Pilot 53/140 – 2026-11-16). Thư mục `AppDelicare/AppDelicare.Application` chỉ còn `bin/obj` (rác build, không trong git) – có thể xóa tay.
- T45R, T45B.5, T49 **DONE** (2026-11-15). **Lát R1.1 xong phần code → dừng chờ anh UAT-1** (UAT_CHECKLIST R1.1). Sau UAT-1: lát R1.2 (T11 mã chứng từ → T12 ApprovalService → T13 đơn hàng → T46B.1).
- Nhánh: `main` (2026-11-15: fast-forward toàn bộ chuỗi T00 → … → T45R.6 → sửa seed, đã push lên origin). Nhánh `origin/develop` (2 commit của máy khác, cấu trúc cũ + Angular) KHÔNG merge – cần anh quyết.
- Build backend: OK – 0 lỗi, 11 cảnh báo (no-incremental; 2026-11-16, nhánh `feat/T17-production`) · Test: `AppDelicare.Tests` 148/148 xanh · T17 (P7) DONE(PILOT) 2026-11-16 → kế tiếp T19
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
