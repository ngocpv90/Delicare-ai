# Triển khai DELICARE backend (staging)

Tài liệu này dành cho anh hoặc IT. Có 3 cách chạy staging, chọn 1. D-15 (IIS hay Docker cho production) chưa chốt nên cả hai đều được chuẩn bị.

| Cách | Khi nào dùng | Đã chạy thử |
|---|---|---|
| **A. Trên máy anh (LocalDB)** | Thử nhanh, UAT nội bộ | ✅ 2026-11-14 |
| **B. IIS (Windows Server)** | Nếu chọn IIS cho production | ✅ phần publish + chạy bản publish ở chế độ Staging; chưa cài lên IIS thật |
| **C. Docker (SQL Server + API)** | Nếu chọn Docker / Linux | ⚠️ Chưa chạy được: máy dev chưa cài Docker |

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
1. DB **rỗng** + có cờ `-CreateSchemaIfEmpty` → tạo 124 bảng từ DDL chuẩn của PO. DB đã có bảng → **không đụng tới bảng**.
2. Cài mọi stored procedure trong `DB/sp/`.
3. Tạo tài khoản `admin` (nếu truyền `-AdminPassword` và chưa có user này).
4. Seed cơ cấu tổ chức, menu, nhóm quyền (`DB/seed/`).

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
- `db-init`: tạo DB, 124 bảng (lần đầu), SP, admin, seed rồi dừng.
- `api`: chạy ở cổng 8080.

Dừng: `docker compose ... down`. Thêm `-v` nếu muốn xóa luôn dữ liệu.

## 6. Kiểm tra sau triển khai

| Bước | Lệnh / thao tác | Kỳ vọng |
|---|---|---|
| Sức khỏe | `GET /health` | `{"status":"Healthy","checks":{"database":{"status":"Healthy"}}, "environment":"Staging"}` |
| Còn sống | `GET /health/live` | HTTP 200 |
| Swagger | mở `/swagger` | Thấy nhóm `v1 / …` và `legacy / …` |
| Đăng nhập | `POST /api/Authentication/Login` `{ "userName": "admin", "passWord": "…" }` | `data.token` |
| API danh mục | Swagger → Authorize (dán token) → `GET /api/OrganizationUnit/GetAll?Page=1&PageSize=50` | 33 đơn vị (DLC, CEO, OPS…) |
| API v1 | `GET /api/v1/hr/shifts` | `{ "success": true, "data": { "items": [...] } }` |

File thử nhanh bằng VS Code REST Client: `docs/api-tests/*.http`.

## 7. Sự cố thường gặp

| Hiện tượng | Nguyên nhân / cách xử lý |
|---|---|
| App dừng ngay, báo "Cấu hình bí mật chưa đủ" | Thiếu `Jwt__SigningKey` hoặc `ConnectionStrings__MainConnectionString` |
| `/health` báo `Unhealthy` | Sai chuỗi kết nối, SQL Server không cho kết nối từ máy chủ API, hoặc firewall |
| Đăng nhập trả 400 "Tài khoản hoặc mật khẩu không đúng" | Sai mật khẩu; tạo lại admin trên DB mới bằng `-AdminPassword` |
| IIS lỗi 500.19 / 500.31 | Chưa cài Hosting Bundle hoặc chưa `iisreset` |
| Script PowerShell báo lỗi chuỗi tiếng Việt | Dùng đúng file trong repo (đã lưu UTF-8 BOM); không lưu lại bằng Notepad dạng ANSI |
