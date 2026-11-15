# PROGRESS – Nhật ký tiến độ (Claude Code cập nhật sau mỗi task)

## Trạng thái hiện tại
- Release: **R1 – CHỈ BACKEND** (.NET + SQL, đủ API cho mockup; Angular ngoài phạm vi – D-20) · Lát hiện tại: **R1.0 Nền móng**
- Task đang làm: T00, T01, T04 DONE – task kế tiếp: **T02** (ICurrentActor), theo thứ tự R1.0: T00 → T01 → T04 → T02 → T03 → T05…
- Nhánh: `feat/T04-net8-packages` (chuỗi nhánh: `chore/T00-claude-setup` → `feat/T01-secrets` → `feat/T04-net8-packages`; chưa merge, chưa push)
- Build backend: OK – 0 lỗi, 10 cảnh báo (2026-11-14) · Build frontend: ngoài phạm vi · Test: chưa có project test (T09)
- Việc anh cần làm: (1) đổi các khóa đã lộ theo `docs/claude/SECRETS_TODO.md`; (2) trả lời D-21 (endpoint `api/AuditChange/DeleteData`) và D-22 (AutoMapper); (3) merge chuỗi nhánh khi anh thấy ổn.

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

**Phát hiện thêm (ghi CODE_AUDIT):** A14 `GET api/AuditChange/DeleteData` xóa vật lý toàn DB (rất cao, D-21); A15 thiếu 74 SP; A16 `uspSystem_BackupDB`; A17 AutoMapper lỗ hổng.

File chính: `.claude/*`, `DB/sp/*.sql`, `DB/sp/_MISSING_FROM_DB.md`, `DB/_ssms_diagrams/*`, `DB/README.md`, `docs/claude/{PROGRESS,ROADMAP,DECISIONS,CODE_AUDIT}.md`.
Kiểm tra: `dotnet build` 0 lỗi; đăng nhập + gọi API bảo vệ OK; `git status` không có thay đổi trong `app-delicare-client/`.
AC: [x] nhánh + commit · [x] build baseline · [x] báo cáo lệch DB · [x] DB/sp + danh sách SP thiếu · [x] đăng nhập API · [x] danh sách vị trí bí mật.
Còn lại / GAP: không có GAP dữ liệu mới.
Cần anh: D-21; merge nhánh khi thấy ổn; chuẩn bị đổi khóa JWT / license / mật khẩu `sa` cho T01.

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
- AutoMapper 13.0.1: **giữ**, vì bản vá 15.1.4+ là license thương mại → D-22. `NuGetAuditSuppress` 3 advisory **chỉ ở Services** để không báo trùng; cảnh báo vẫn hiện ở WebApi.

Kiểm tra:
- `dotnet build --no-incremental`: **0 lỗi, 10 cảnh báo** (= mốc T00: 6 dòng NU1903 AutoMapper ở WebApi + 4 dòng CS8604).
- `dotnet list package --vulnerable --include-transitive`: Data, Model **sạch**; WebApi / Services chỉ còn AutoMapper (D-22). Trước T04 có khoảng 15 gói có lỗ hổng.
- Chạy app: Swagger JSON 200 (572 route), UI 200; login 200; không token 401; `User/GetAll` (Dapper + Microsoft.Data.SqlClient) 200; `User/GetById` (EF Core 8) 200; `UnitMeasure/GetAllData`, `ObjectMenu/GetAll` 200; log không có Error / Exception.
- Chưa thử được: `DataTableBulkInsert` / `DataReaderBulkInsert` (SqlBulkCopy, chưa có màn hình gọi) và các thao tác ghi. Sẽ có test khi làm T09.
- `git status`: không có thay đổi trong `app-delicare-client/`.

AC: [x] build 0 lỗi · [x] số cảnh báo không tăng (10 = 10) · [x] app chạy.
Còn lại: D-22 AutoMapper. `Microsoft.EntityFrameworkCore.SqlServer` 8.0.34 kéo Microsoft.Data.SqlClient 5.1.x, còn Data dùng 6.0.1 → NuGet chọn 6.0.1 cho cả app (đã chạy thử OK).
Ghi chú: `AppDelicare/.vscode/` chưa được theo dõi, không phải em tạo, em không đụng. `.gitignore` gốc chỉ bỏ qua `.vscode/` ở thư mục gốc.
