# SECRETS_TODO – Bí mật anh cần tạo mới / đổi (T01, 2026-11-14)

Không ghi giá trị cũ hay mới ở đây. Các giá trị cũ dưới đây **đã nằm trong lịch sử git** (commit `2501aef Initial commit`, đã có trên remote `origin`), nên phải coi là **đã lộ**. Xóa khỏi code chưa đủ, phải **đổi**.

## 1. Danh sách

| # | Bí mật | Vị trí cũ (đã gỡ) | Khóa cấu hình mới | Bắt buộc? | Việc anh cần làm |
|---|---|---|---|---|---|
| 1 | Khóa ký JWT | `WebApi/Infrastructure/ConfigureServices.cs` (const `SecretKey`); cùng giá trị ở `Model/Option/JwtIssuerOptions.cs` (property `Secret`, không nơi nào dùng) | `Jwt:SigningKey` (≥ 32 byte) | Có, thiếu thì app không khởi động | **Dev (máy anh): em đã sinh khóa ngẫu nhiên MỚI** và đặt trong user-secrets. **Staging / prod:** sinh khóa riêng cho từng môi trường (lệnh ở mục 2), đặt bằng biến môi trường `Jwt__SigningKey`. Không dùng lại khóa cũ. Đổi khóa thì mọi token cũ hết hiệu lực, người dùng phải đăng nhập lại. |
| 2 | Connection string DB | `WebApi/appsettings.json` | `ConnectionStrings:MainConnectionString` | Có | Dev: đã đặt trong user-secrets (LocalDB, Windows Auth, không có mật khẩu). Prod: biến môi trường `ConnectionStrings__MainConnectionString`, dùng **login SQL riêng quyền thấp**, không dùng `sa`. |
| 3 | Mật khẩu SQL `sa` (rất ngắn) | Dòng comment trong `WebApi/appsettings.json`; `Generator/App.config` | – (không dùng nữa) | – | **Đổi mật khẩu `sa`** trên mọi SQL Server từng dùng mật khẩu này; nên tắt login `sa`. `Generator/App.config` nay dùng LocalDB + Windows Auth. |
| 4 | API key ApplicationInsights QuickPulse (Live Metrics) | `WebApi/Startup.cs` | `ApplicationInsights:QuickPulseApiKey` | Không, trống thì bỏ qua | Vào Azure Portal → Application Insights → API Access và **xóa (revoke) key cũ**. Chỉ tạo key mới nếu thật sự dùng Live Metrics. Hiện app chưa cấu hình ApplicationInsights connection string, nên key này gần như không có tác dụng. |
| 5 | License GemBox.Document | `WebApi/Startup.cs` | `Licenses:GemBox` | Không, trống thì chạy FREE-LIMITED (có log cảnh báo) | Dev: em đã chép license hiện có vào user-secrets máy anh. Nếu repo từng công khai: liên hệ GemBox xin cấp lại. Prod: biến môi trường `Licenses__GemBox`. |
| 6 | Tài khoản app `admin / admin` | Dữ liệu DB dev | – | – | Đổi trước go-live (T48.2). |

## 2. Cách đặt

**Dev (máy anh) – user-secrets** (đã bật, `UserSecretsId` trong `AppDelicare.WebApi.csproj`):
```powershell
cd AppDelicare/ClinicManagement.WebApi
dotnet user-secrets list                     # xem tên khóa đã đặt
dotnet user-secrets set "Jwt:SigningKey" "<giá trị>"
```
Sinh khóa ngẫu nhiên 64 byte:
```powershell
[Convert]::ToBase64String((1..64 | ForEach-Object { Get-Random -Maximum 256 }) -as [byte[]])
# hoặc: node -e "console.log(require('crypto').randomBytes(64).toString('base64'))"
```

**Staging / prod – biến môi trường** (`:` thay bằng `__`):
`Jwt__SigningKey`, `ConnectionStrings__MainConnectionString`, `Licenses__GemBox`, `ApplicationInsights__QuickPulseApiKey`.
IIS: đặt trong `web.config` → `<environmentVariables>` của site (không commit file có giá trị), hoặc Application Pool. Docker: `--env-file` không commit.

Mẫu placeholder: `AppDelicare/ClinicManagement.WebApi/appsettings.Development.json.example`.

## 3. Lịch sử git (anh quyết)
Giá trị cũ vẫn đọc được trong commit `2501aef`. Đổi khóa (mục 1) là **bắt buộc** và đủ để vô hiệu hóa. Nếu muốn xóa hẳn khỏi lịch sử thì phải viết lại lịch sử (`git filter-repo`) và force-push. Việc này ảnh hưởng mọi bản clone, em **không tự làm**.
