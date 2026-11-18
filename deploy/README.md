# Triển khai DELICARE backend

Tài liệu này dành cho anh hoặc IT. **Cách chuẩn cho test và production: Linux + Docker (D-60, anh chốt 2026-11-17)** – mục 10. Cách A / B (Windows, IIS) chỉ còn để chạy thử trên máy dev Windows; **không** dùng IIS / Windows Service / Task Scheduler cho production.

| Cách | Khi nào dùng | Đã chạy thử |
|---|---|---|
| **A. Trên máy anh (LocalDB)** | Thử nhanh, UAT nội bộ | ✅ 2026-11-14; cập nhật 2026-11-17 (backend Pilot + giao diện Angular, mục 8–9) |
| **B. IIS (Windows Server)** | Nếu chọn IIS cho production | ✅ phần publish (`Publish-Iis.ps1`, lần cuối 2026-11-17) + chạy bản publish ở chế độ Staging; chưa cài lên IIS thật (cần quyền admin – anh / IT làm theo mục 4) |
| **C. Docker (SQL Server + API + Job)** | **Cách chuẩn (D-60)**: server test / dev FE; production theo mục 10 | ⚠️ Chưa chạy Docker thật (máy dev chưa cài Docker). `db-init.sh` đã chạy thật bằng Git Bash + sqlcmd trên LocalDB (T48.3) |

Sau khi chạy, kiểm tra theo mục 6.

---

## 1. Chuẩn bị chung

- **.NET 8 SDK** (để build / publish): https://dotnet.microsoft.com/download/dotnet/8.0
- **sqlcmd** (SQL Server Command Line Utilities) để chạy script DB.
- Repo đã clone về máy.

**Bí mật không bao giờ nằm trong repo.** Mỗi môi trường cần 3 giá trị, đặt bằng **biến môi trường**:

| Biến môi trường | Ý nghĩa |
|---|---|
| `ConnectionStrings__MainConnectionString` | Chuỗi kết nối DB của môi trường đó |
| `Jwt__SigningKey` | Khóa ký JWT, ≥ 32 byte, **mỗi môi trường một khóa riêng** |
| `ASPNETCORE_ENVIRONMENT` | `Staging` hoặc `Production` |
| `Licenses__GemBox` | (tùy chọn) license GemBox |

Sinh khóa JWT (PowerShell):
```powershell
[Convert]::ToBase64String((1..64 | ForEach-Object { Get-Random -Maximum 256 }) -as [byte[]])
```

## 2. Dựng database (chung cho A và B)

Script `deploy/db/Deploy-Database.ps1` chạy được nhiều lần, không nhân bản dữ liệu:
1. **Migration cấu trúc bảng** (2026-11-18 – `DB/README.md` mục Migration): chạy `DB/migrations/*.sql` theo thứ tự tên, mỗi file 1 lần. DB **rỗng** → baseline 124 bảng (cần cờ `-CreateSchemaIfEmpty`); DB đã đủ bảng → chỉ ghi nhận; migration mới → chạy 1 lần. `-ListMigrations`: xem trước (không đổi gì); `-MigrationsOnly`: chỉ đồng bộ bảng, không chạy SP / seed.
2. Chạy đúng thứ tự trong **`deploy/db/scripts.order`** (T48.3 – nguồn thứ tự **duy nhất**, `deploy/docker/db-init.sh` đọc cùng file): mọi stored procedure `DB/sp/` → tài khoản `admin` (nếu truyền `-AdminPassword` / biến `DELICARE_ADMIN_PASSWORD` và chưa có user này; hash PBKDF2) → seed cơ cấu tổ chức, menu, nhóm quyền, ca, checklist (`DB/seed/`, chỉ thêm phần chưa có).
3. **Mức tương thích (T48.5, D-61):** ngay sau khi mở DB, chạy `deploy/db/compat-level.sql` – mức > 160 (DB tạo trên SQL Server 2025) → đặt về **160** (SQL Server 2022) và ghi log; = 160 → giữ; < 160 → chỉ cảnh báo, không tự nâng. `deploy/docker/db-init.sh` làm y hệt.
4. `-ListOnly`: chỉ in thứ tự script, không kết nối DB. Tài khoản SQL có thể lấy từ biến môi trường `DELICARE_SQL_USER` / `DELICARE_SQL_PASSWORD` (mật khẩu đưa cho sqlcmd qua `SQLCMDPASSWORD`, không nằm trên dòng lệnh). `-TrustServerCertificate` cho SQL Server dùng chứng chỉ tự ký (Docker).

```powershell
# DB staging mới trên SQL Server (Windows Authentication)
./deploy/db/Deploy-Database.ps1 -Server "TÊN_SQL_SERVER" -Database AppDelicare_Staging `
    -CreateDatabase -CreateSchemaIfEmpty -AdminPassword (Read-Host -AsSecureString "Mật khẩu admin")

# Dùng tài khoản SQL thay cho Windows Authentication
./deploy/db/Deploy-Database.ps1 -Server "10.0.0.5,1433" -Database AppDelicare_Staging -SqlUser deploy `
    -SqlPassword (Read-Host -AsSecureString "Mật khẩu SQL") -CreateDatabase -CreateSchemaIfEmpty `
    -AdminPassword (Read-Host -AsSecureString "Mật khẩu admin")
```

> ⚠️ Không chạy lên DB production khi chưa có checklist go-live (T48.2: backup trước, phương án quay lui).

> ⚠️ **D-61:** chuẩn là **SQL Server 2022** (mức 160). **Không chuyển dữ liệu giữa các máy bằng `.bak`** – bản sao lưu từ SQL Server 2025 không khôi phục được trên 2022; chỉ dựng / cập nhật DB bằng script (`scripts.order`). Xem mục 11.

## 3. Cách A – chạy trên máy anh

Đã dựng sẵn trên máy anh: DB `AppDelicare_Staging` trên `(localdb)\MSSQLLocalDB`. Tài khoản và khóa nằm trong 2 file **không commit**: `deploy/staging.local.md` và `deploy/staging.local.env.ps1`.

```powershell
cd <repo>
. ./deploy/staging.local.env.ps1                      # nạp biến môi trường Staging
dotnet publish AppDelicare/AppDelicare.WebApi/AppDelicare.WebApi.csproj -c Release -o C:\Deploy\delicare-staging
cd C:\Deploy\delicare-staging
dotnet AppDelicare.WebApi.dll --urls http://localhost:5200
```

Xóa staging khi không dùng nữa:
```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -Q "DROP DATABASE AppDelicare_Staging"
```

## 4. Cách B – IIS (Windows Server)

1. Cài **ASP.NET Core 8.0 Hosting Bundle** trên máy chủ (https://dotnet.microsoft.com/download/dotnet/8.0 → "Hosting Bundle"), rồi chạy `iisreset`.
2. Dựng DB theo mục 2.
3. Publish:
   ```powershell
   ./deploy/iis/Publish-Iis.ps1 -Output C:\Deploy\delicare-api -Environment Staging
   ```
   Chép thư mục `C:\Deploy\delicare-api` lên máy chủ, ví dụ `D:\Sites\delicare-api`.
4. Trong IIS Manager:
   - Tạo **Application Pool** `delicare-api`, đặt **.NET CLR version = No Managed Code**.
   - Tạo **Site**, trỏ tới thư mục ở bước 3, gán App Pool trên, gắn binding (port / domain; staging có thể dùng HTTP nội bộ).
5. Đặt **biến môi trường bí mật** cho site. Cách an toàn: IIS Manager → site → **Configuration Editor** → section `system.webServer/aspNetCore` → `environmentVariables`, thêm 3 biến ở mục 1. Cách này ghi vào `web.config` **trên máy chủ**; không chép file đó về repo.
6. Cấp quyền ghi thư mục `Logs` (cạnh `AppDelicare.WebApi.dll`) cho tài khoản `IIS AppPool\delicare-api`.
7. Kiểm tra theo mục 6.

## 5. Cách C – Docker

```powershell
cd <repo>
copy deploy\docker\staging.env.example deploy\docker\staging.env   # điền mật khẩu SA, admin, khóa JWT (file KHÔNG commit)
docker compose -f deploy/docker/docker-compose.staging.yml --env-file deploy/docker/staging.env up -d --build
docker compose -f deploy/docker/docker-compose.staging.yml logs -f db-init   # xem khởi tạo DB
```

- `db`: SQL Server 2022 (cổng máy 14333).
- `db-init`: tạo DB, 124 bảng (lần đầu), SP, admin (lần đầu, PBKDF2), seed rồi dừng – theo `deploy/db/scripts.order`, chạy lại được.
- `api`: chạy ở cổng 8080. Tệp đính kèm (BE2.7) nằm ở volume `delicare-docs` → `/app/App_Data/DocumentFiles` (`DocumentStorage__RootPath`); `down -v` xóa cả tệp.

**Cập nhật DB sau `git pull` (T48.3, server Docker của dev FE):**
```bash
git pull && docker compose -f deploy/docker/docker-compose.staging.yml --env-file deploy/docker/staging.env run --rm db-init
```
hoặc `bash deploy/docker/update-db.sh` (chỉ chạy khi có thay đổi DB, ghi log, cron 5 phút tùy chọn). Chi tiết + phương án dự phòng (`Deploy-Database.ps1 -Server "<ip>,14333" -TrustServerCertificate` từ máy dev FE): `deploy/docker/README.md`.

Dừng: `docker compose ... down`. Thêm `-v` nếu muốn xóa luôn dữ liệu.

### Thư mục tệp đính kèm (BE2.7 – `DocumentStorage`)
- Tệp người dùng tải lên (`DocumentFile/Upload`) lưu **ngoài wwwroot**, tên = SHA-256, tại `DocumentStorage:RootPath` (mặc định `App_Data/DocumentFiles` cạnh `AppDelicare.WebApi.dll`). Giới hạn `DocumentStorage:MaxFileSizeMb` (20), đuôi `AllowedExtensions` (pdf, jpg, png, xlsx, docx – tệp chạy được luôn bị chặn).
- **IIS (cách B):** cấp quyền ghi thư mục `App_Data\DocumentFiles` cho `IIS AppPool\delicare-api` (như `Logs`), hoặc đặt biến môi trường `DocumentStorage__RootPath=D:\DelicareData\DocumentFiles` ra ổ dữ liệu riêng; **sao lưu thư mục này cùng DB** (DB chỉ giữ khóa lưu trữ). Không chép đè thư mục này khi cập nhật bản build.
- **Docker (cách C):** volume `delicare-docs` (đã khai báo trong `docker-compose.staging.yml`). Kestrel / IIS mặc định nhận request tới ~28–30 MB – tăng `MaxFileSizeMb` quá 25 thì phải tăng giới hạn request.
- Mẫu in (BE2.7.1): **PDF mặc định** (PDFsharp-MigraDoc 6.2.4, MIT) + xlsx / docx. Font PDF **Noto Sans nhúng trong `AppDelicare.Services.dll`** (giấy phép `Fonts/NotoSans-OFL.txt` đi kèm bản publish) → IIS và Docker **không cần cài font** (không thêm gói apt). Đổi mặc định: `BusinessRules__Common__PrintDefaultFormat=xlsx`. Word cần `Licenses__GemBox` (thiếu → bản dùng thử của GemBox).

### Cảm biến nhiệt độ IoT (BE3.2)
- API `POST api/IotTemperature/Receive` dùng **khóa riêng** (header `X-Api-Key`), không dùng đăng nhập. Khóa là **bí mật**: đặt biến môi trường cho API `Iot__ApiKeys__0=<khóa ≥ 32 ký tự>` (thêm `Iot__ApiKeys__1`… khi đổi khóa / nhiều cổng). Không đặt = API IoT tắt (401).
- Khai báo thiết bị → khu vực kho: `Iot__Devices__<MÃ-THIẾT-BỊ>__StorageZoneCode=<mã khu vực>` (hoặc `__StorageZoneId`), tùy chọn `__MeasurementType`, `__Enabled=false`. Docker: thêm vào `environment` của service `api`; IIS: `environmentVariables` của site.
- Chi tiết + giới hạn: `docs/frontend-handoff/BE3.2.md`.

### Job tự động (T23 – `AppDelicare.Job`, D-48)
Tiến trình **riêng** (không chạy trong WebApi – IIS tự tắt app khi không có truy cập). Chạy **đúng 1 bản**; nếu lỡ chạy 2 bản, khóa `sp_getapplock` theo mã job chặn chạy trùng.

| Job | Giờ (VN) | Việc (gọi đúng Service của nút bấm tay) |
|---|---|---|
| `LockOrders` | 20:00 | Khóa đơn Đã xác nhận của ngày mai (`Order/LockByMealDate`) |
| `Mrp` | 20:30 | MRP ngày sản xuất mai + đề nghị mua Nháp (`MaterialRequirement/Calculate`) |
| `LotExpiry` | 00:15 | Lô quá hạn → Hết hạn + hao hụt (`MaterialLot/ProcessExpired`) |
| `ContractExpiry` | 00:30 | Hợp đồng quá ngày hết hiệu lực → Hết hạn (`Contract/ExpireOverdue`) |
| `MarkAbsent` | 08:00 | Chốt vắng ca của hôm qua (`ShiftAssignment/MarkAbsent`) |

- **Lịch / bật tắt:** mục `Jobs` trong `appsettings.json` **của WebApi** (Job đọc file này dưới tên `appsettings.shared.json`), ví dụ biến môi trường `Jobs__Mrp__At=21:00`, `Jobs__LockOrders__Enabled=false`. Lỡ giờ (máy tắt) → chạy bù ngay khi bật lại, **trong cùng ngày**.
- **Tài khoản ghi:** `system` (seed `DB/seed/SystemUser_seed.sql`, Id cố định = `SystemUsers:JobUserId`, không đăng nhập được). Chạy tay `api/Job/Run` ghi dưới admin bấm.
- **Lịch sử chạy:** bảng `AnalyticsRefreshLog` (Succeeded / Skipped = nghiệp vụ từ chối hoặc không có gì / Failed / Locked) + log file `Logs/AppDelicare.Job*.log`. Xem: `GET api/Job/GetAll` (admin).
- **Bí mật:** chỉ cần `ConnectionStrings__MainConnectionString` (+ `Licenses__GemBox` nếu có). Môi trường: `DOTNET_ENVIRONMENT=Staging` / `Production`.
- **Docker (cách C):** service `job` trong `docker-compose.staging.yml` (đã khai báo, không scale).
- **Chạy thử trên máy dev Windows (cách A – không dùng cho production, D-60):**
  ```powershell
  dotnet publish AppDelicare/AppDelicare.Job/AppDelicare.Job.csproj -c Release -o C:\Deploy\delicare-job
  # chạy thử: liệt kê job, chạy 1 job rồi thoát
  $env:DOTNET_ENVIRONMENT = "Staging"; $env:ConnectionStrings__MainConnectionString = "<chuỗi kết nối>"
  dotnet C:\Deploy\delicare-job\AppDelicare.Job.dll --list
  dotnet C:\Deploy\delicare-job\AppDelicare.Job.dll --run ContractExpiry
  ```
  Chạy liên tục trên server: **chỉ Docker** (service `job`, `restart: unless-stopped`, HEALTHCHECK bằng file nhịp – mục 10). Không Task Scheduler / Windows Service (D-60, D-59 g đã bỏ).
- Cập nhật bản build Job: dừng tác vụ / container → publish đè → chạy lại. Deploy API không làm dừng Job và ngược lại.

## 6. Kiểm tra sau triển khai

| Bước | Lệnh / thao tác | Kỳ vọng |
|---|---|---|
| Sức khỏe | `GET /health` | `{"status":"Healthy","checks":{"database":{"status":"Healthy"}}, "environment":"Staging"}` |
| Còn sống | `GET /health/live` | HTTP 200 |
| Swagger | mở `/swagger` | Thấy nhóm theo controller (`Recipe`, `UserRole`…), không còn `/api/v1` |
| Đăng nhập | `POST /api/Authentication/Login` `{ "userName": "admin", "passWord": "…" }` | `data.token` |
| API danh mục | Swagger → Authorize (dán token) → `GET /api/OrganizationUnit/GetAll?Page=1&PageSize=50` | 33 đơn vị (DLC, CEO, OPS…) |
| Action nghiệp vụ | `GET /api/User/GetCurrent` | `{ "responseCode": 200, "data": { "user": {...}, "listMenu": [...] } }` |

File thử nhanh bằng VS Code REST Client: `docs/api-tests/*.http`.

## 7. Sự cố thường gặp

| Hiện tượng | Nguyên nhân / cách xử lý |
|---|---|
| App dừng ngay, báo "Cấu hình bí mật chưa đủ" | Thiếu `Jwt__SigningKey` hoặc `ConnectionStrings__MainConnectionString` |
| `/health` báo `Unhealthy` | Sai chuỗi kết nối, SQL Server không cho kết nối từ máy chủ API, hoặc firewall |
| Đăng nhập trả 400 "Tài khoản hoặc mật khẩu không đúng" | Sai mật khẩu; tạo lại admin trên DB mới bằng `-AdminPassword` |
| IIS lỗi 500.19 / 500.31 | Chưa cài Hosting Bundle hoặc chưa `iisreset` |
| Script PowerShell báo lỗi chuỗi tiếng Việt | Dùng đúng file trong repo (đã lưu UTF-8 BOM); không lưu lại bằng Notepad dạng ANSI |

## 8. Giao diện Angular trên staging (Pilot, cách A)

Angular gọi API bằng đường dẫn tương đối `/api/` và dùng route dạng `#/…` → chỉ cần đặt bản build vào `wwwroot` của API (API đã bật `UseDefaultFiles` + `UseStaticFiles`), cùng một địa chỉ, không cần CORS / proxy.

```powershell
cd <repo>\app-delicare-client
npm ci            # lần đầu
npm run build     # ra dist\w3crm\browser
Copy-Item .\dist\w3crm\browser\* C:\Deploy\delicare-staging\wwwroot -Recurse -Force   # giữ nguyên Import / Logs / Template có sẵn
```
Mở `http://localhost:5200/` → trang đăng nhập DELI-CARE. IIS (cách B): chép giống vậy vào `wwwroot` của site. Docker (cách C): `app-delicare-client/Dockerfile` + `nginx.conf` (nginx chuyển `/api/` sang container API).

## 9. Cập nhật staging (lặp lại mỗi đợt; đã làm 2026-11-17 sau backend Pilot và sau BE2.9)

1. DB: `./deploy/db/Deploy-Database.ps1 -Server "(localdb)\MSSQLLocalDB" -Database AppDelicare_Staging` (migration bảng chưa chạy – mỗi file 1 lần – rồi mọi SP + seed theo `deploy/db/scripts.order`; không đụng dữ liệu nghiệp vụ). Nếu tên tiếng Việt từng bị lỗi font (seed chạy tay thiếu `-f 65001`): `./deploy/db/Repair-VietnameseText.ps1 -Server … -Database … [-Apply]`.
2. Dừng bản API đang chạy (cửa sổ `dotnet AppDelicare.WebApi.dll`), rồi publish đè: `./deploy/iis/Publish-Iis.ps1 -Output C:\Deploy\delicare-staging -Environment Staging` (hoặc lệnh `dotnet publish` ở mục 3).
3. Build + chép Angular (mục 8).
4. Chạy: `. ./deploy/staging.local.env.ps1; cd C:\Deploy\delicare-staging; dotnet AppDelicare.WebApi.dll --urls http://localhost:5200`.
5. Kiểm tra mục 6 + mở `http://localhost:5200/`; toàn luồng: `docs/api-tests/full-flow.http` (đổi `@base = http://localhost:5200`, `@password` = mật khẩu admin staging, `@key` / `@day` / `@dow` mới) và `docs/claude/UAT_CHECKLIST.md` mục TOÀN LUỒNG R1 (bản cũ: `pilot-flow.http`, mục PILOT).
6. Tệp đính kèm (BE2.7): thư mục `C:\Deploy\delicare-staging\App_Data\DocumentFiles` (tạo 1 lần; `dotnet publish -o` không xóa thư mục này – **không xóa khi cập nhật**, sao lưu cùng DB). Muốn để ngoài thư mục bản build: đặt `$env:DocumentStorage__RootPath` trước bước 4.
7. Dừng: Ctrl+C ở cửa sổ đang chạy (hoặc `Get-CimInstance Win32_Process | ? { $_.CommandLine -like "*AppDelicare.WebApi.dll*" } | % { Stop-Process -Id $_.ProcessId }`).

Lần cập nhật gần nhất: **2026-11-17 – BE3.7** (T23 + BE3.1–BE3.5: DB 131 SP + seed SystemUser, KpiMetricDefinition; publish đè; `full-flow.http` 217 / 217 trên staging). Lần trước: **2026-11-17 – BE2.9** (toàn bộ BE-2: BE2.1–BE2.7 + sửa lỗi BE2.9): DB 131 SP + 8 seed theo `scripts.order` (không đụng bảng), publish đè (giữ bản build Angular cũ trong `wwwroot`), smoke 49 / 49, `full-flow.http` 216 / 216 trên staging (dữ liệu `[DEMO]` key B29S). Sau đó đã **dừng** API staging.

## 10. Production Linux + Docker (T48.4, D-60)

**Thành phần** (`deploy/docker/docker-compose.production.yml`): `nginx` (build Angular từ `app-delicare-client` + HTTPS + chuyển `/api`, `/Import` → `api`), `api` (AppDelicare.WebApi), `job` (AppDelicare.Job – **đúng 1 bản**), `db-init` (chạy tay). **SQL Server không nằm trong compose production** – server / container riêng có ổ dữ liệu + backup.

**SQL Server (anh / IT chuẩn bị):**
- Đặt múi giờ **Asia/Ho_Chi_Minh** cho máy / container SQL Server (container: `TZ=Asia/Ho_Chi_Minh`). Lý do: cột mặc định `sysdatetime()` và SP so ngày theo giờ DB phải trùng giờ VN của ứng dụng. Lệch > 5 phút → `/health` báo `dbclock: Degraded` (kèm số phút lệch) và Job ghi cảnh báo lúc khởi động.
- Backup full hằng đêm + log 15 phút; volume tệp đính kèm `delicare-docs` backup cùng DB.
- Tài khoản ứng dụng riêng (**không dùng sa** cho api / job): `db-init` tạo bằng `deploy/db/app-login.sql` khi đặt `APP_DB_USER` + `APP_DB_PASSWORD` – quyền `db_datareader`, `db_datawriter`, `GRANT EXECUTE`, **không DDL**. Tài khoản quản trị SQL (`DB_ADMIN_USER` / `DB_ADMIN_PASSWORD`) chỉ dùng cho `db-init`.

**Lần đầu trên server Linux** (đã cài Docker Engine + compose plugin, clone repo):
```bash
cp deploy/docker/production.env.example deploy/docker/production.env   # điền – KHÔNG commit (gitignore)
mkdir -p deploy/docker/certs   # fullchain.pem + privkey.pem của domain (KHÔNG commit)
docker compose -f deploy/docker/docker-compose.production.yml --env-file deploy/docker/production.env run --rm db-init
docker compose -f deploy/docker/docker-compose.production.yml --env-file deploy/docker/production.env up -d --build
docker compose -f deploy/docker/docker-compose.production.yml ps        # api / job / nginx: healthy
```

**Cập nhật bản mới:** `git pull` → `run --rm db-init` (SP / seed mới) → `up -d --build`. Deploy `api` không làm dừng `job` và ngược lại.

**Kiểm tra:** `https://<domain>/health` → `database: Healthy`, `dbclock: Healthy`; `docker compose … exec job dotnet AppDelicare.Job.dll --list`; trạng thái `job` = healthy (HEALTHCHECK `--healthcheck`: vòng hẹn giờ ghi file nhịp `/tmp/delicare-job-heartbeat` trong 2 phút gần nhất).

**Log:** trong Docker chỉ ghi console (`Logging__ConsoleOnly=true`) – Docker giữ + xoay vòng (`json-file`, 20 MB × 10 / container): `docker compose … logs -f api job`. Chạy ngoài Docker (máy dev) vẫn ghi file `Logs/`.

**Bí mật** (chỉ trong `production.env` trên server): mật khẩu SQL quản trị + ứng dụng, `JWT_SIGNING_KEY`, `ADMIN_PASSWORD` (lần đầu), `GEMBOX_LICENSE`, `IOT_API_KEY_0/1`.

Máy dev hiện chưa có Docker → compose production **chưa chạy thật**; đã kiểm: build, test, `db-init.sh` (cú pháp + dry-run), `app-login.sql` trên LocalDB (tạo / chạy lại / quyền / đăng nhập SQL), `--healthcheck` + file nhịp, `/health` dbclock (PROGRESS T48.4).

## 11. Tương thích SQL Server 2022 (T48.5, D-61) – bắt buộc trước mỗi lần gộp `main`

- **Chuẩn:** SQL Server 2022, mức tương thích **160**. Máy anh giữ SQL Server 2025 cho DB dev nhưng DB đặt mức 160 (đã hạ 2026-11-17); máy dev FE, server test, production dùng 2022.
- **Script deploy giữ mức 160:** `deploy/db/compat-level.sql` (gọi từ `Deploy-Database.ps1` và `db-init.sh`) – xem mục 2 bước 3.
- **Chốt chặn cú pháp:** test `AppDelicare.Tests/Schema/SqlServer2022CompatibilityTests` quét `DB/**/*.sql`, `deploy/db/**/*.sql` và chuỗi SQL trong `AppDelicare.*/**/*.cs`; gặp cú pháp chỉ có từ 2025 (danh sách cấm ở CLAUDE.md mục 3.1, quy tắc 0) → test đỏ. Mức 160 không chặn hết các hàm này nên test là chốt chặn chính.
- **Kiểm trên SQL Server 2022 thật** (cổng riêng 14339, không đụng DB dev; cần Docker):

```bash
# 1) SQL Server 2022 tạm (giờ VN), mật khẩu chỉ trong phiên shell
export SA_PASSWORD='<mật khẩu mạnh tạm>'
docker run -d --name delicare-sql2022-check -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD="$SA_PASSWORD" -e TZ=Asia/Ho_Chi_Minh \
  -p 14339:1433 mcr.microsoft.com/mssql/server:2022-latest
# 2) db-init 2 lần trên DB trống (lần 2 không được đổi số dòng seed)
for i in 1 2; do REPO_ROOT=. DB_SERVER=localhost,14339 DB_NAME=AppDelicare_Check ADMIN_PASSWORD='<mật khẩu admin tạm>' \
  SQLCMD_BIN=sqlcmd bash deploy/docker/db-init.sh; done
# 3) Chạy WebApi trỏ vào DB này rồi chạy docs/api-tests/full-flow.http (phải đạt hết)
# 4) Dọn: docker rm -f delicare-sql2022-check
```

  Kết quả (số SP, số dòng seed lần 1 / lần 2, `full-flow.http`) ghi vào PROGRESS mục của đợt gộp.
- Máy dev hiện chưa có Docker → bước "kiểm trên SQL Server 2022 thật" **chưa chạy** (PROGRESS T48.5).

## 12. Bot Telegram – container `messaging` (BOT-1.3, D-62)

`AppDelicare/AppDelicare.Messaging` – tất cả bot (VH, DH), **không chạm DB**, gọi api qua HTTP (khóa client). Đã có trong 3 compose: `deploy/docker/docker-compose.production.yml` (Webhook, `https://${SERVER_NAME}/tg/<BOT>`), `deploy/docker/docker-compose.staging.yml` và `docker-compose.yml` gốc (mặc định **Polling** – không cần domain). Token để trống → bot tắt, web / api không ảnh hưởng.

1. **Tạo bot** bằng @BotFather – mỗi môi trường 1 bộ (DEV / TEST / PROD): VH ("Delicare Vận hành"), DH ("Delicare Điều hành"). Ghi token + tên bot (không @).
2. **Khóa** (sinh trên server, KHÔNG commit): `MESSAGING_CLIENT_KEY` = `openssl rand -hex 32`; `MESSAGING_CLIENT_KEY_HASH` = `printf %s "<khóa>" | sha256sum` (api so khóa bằng hash); `MESSAGING_LINK_SIGNING_KEY` = `openssl rand -hex 32`; Webhook: `TELEGRAM_VH_WEBHOOK_SECRET`, `TELEGRAM_DH_WEBHOOK_SECRET` = `openssl rand -hex 24`.
3. Điền vào `production.env` / `staging.env` / `.env` (biến mẫu đã có trong `*.env.example`), `docker compose … up -d --build`.
4. **Webhook (production)**: messaging tự `setWebhook` khi khởi động; nginx `/tg/` → `messaging:8080` (resolver Docker – messaging tắt thì `/tg/` trả 502, nginx vẫn chạy, Telegram tự gửi lại). `/api/BotChannel/` từ ngoài → 404.
5. **Kiểm**: `GET http://messaging:8080/health` (trong mạng Docker) hoặc `docker compose ps` (HEALTHCHECK `--healthcheck`); trên Telegram: quét QR cổng / gửi `/start` → nút chia sẻ số điện thoại → liên kết.

**Máy dev (Polling, bộ bot DEV):** `dotnet user-secrets set "Telegram:Bots:VH:Token" "<token DEV>" --project AppDelicare/AppDelicare.Messaging` (+ `Messaging:ClientKey`), api đặt `Clients:MESSAGING:KeyHash` + `Messaging:LinkSigningKey` (user-secrets của WebApi), chạy `dotnet run --project AppDelicare/AppDelicare.Messaging --urls http://localhost:5130`.

**Thử không cần Telegram thật** (`tools/bot-smoke/`): `node tools/bot-smoke/fake-telegram.mjs 18081` (Telegram giả) → chạy messaging với `Telegram__ApiBaseUrl=http://localhost:18081`, `Telegram__Bots__VH__Token=FAKE`, `Messaging__Mode=Polling` → `node tools/bot-smoke/flow.mjs <số điện thoại không trùng>` (BOT-1.3) / `node tools/bot-smoke/flow-experience.mjs` (BOT-1.4 – menu, /cho, chấm công, tra mã; không ghi quyết định duyệt) / `node tools/bot-smoke/flow-vanhanh.mjs` (BOT-1.5a – Bếp, ATTP, lịch ca, xin nghỉ; không ghi dữ liệu) / `node tools/bot-smoke/flow-giaovan.mjs [<Id chuyến mẫu> <ngày>]` (BOT-1.5b – giao vận, điểm phát; không ghi dữ liệu) / `node tools/bot-smoke/flow-15c.mjs` (BOT-1.5c – đổi ca, phản ánh, nhân sự, 17 lệnh xem; không ghi dữ liệu): QR cổng → chia sẻ số (của người khác bị từ chối, số trùng bị từ chối) → chào → ⚡ Ca của tôi → web gửi tay qua outbox → bot gửi → `/huylienket`.
