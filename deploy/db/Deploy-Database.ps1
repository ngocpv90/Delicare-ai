<#
.SYNOPSIS
  Triển khai / cập nhật DB DELICARE (T48.1, T48.3) – idempotent, chạy lại nhiều lần an toàn.
.DESCRIPTION
  Thứ tự:
    1. MIGRATION CẤU TRÚC BẢNG (2026-11-18): chạy DB/migrations/*.sql theo thứ tự tên file, mỗi file 1 lần (deploy/db/Migrations.ps1 –
       cùng quy tắc với deploy/db/migrations.sh): baseline 124 bảng chỉ chạy trên DB trống (cần -CreateSchemaIfEmpty), DB đã có bảng → ghi nhận;
       nâng cấp DB cũ 91 bảng chỉ chạy khi còn thiếu bảng; migration mới chạy 1 lần. Đã chạy lưu ở extended property DelicareMigration:<file>.
    2. Đúng thứ tự trong deploy/db/scripts.order (NGUỒN DUY NHẤT, dùng chung với deploy/docker/db-init.sh):
       mọi stored procedure DB/sp (CREATE OR ALTER) → tài khoản admin (00_Admin_seed.sql, chỉ khi có -AdminPassword)
       → seed cấu hình (chỉ thêm phần chưa có).
       SP có trong DB/sp mà chưa ghi vào scripts.order vẫn chạy (sau cùng) kèm cảnh báo; seed chưa ghi thì không chạy.
  sqlcmd luôn chạy với -f 65001 (UTF-8) – thiếu cờ này tên tiếng Việt bị lỗi font khi ghi vào DB.
  Bí mật không nằm trong repo: tài khoản SQL / mật khẩu có thể lấy từ biến môi trường
  DELICARE_SQL_USER, DELICARE_SQL_PASSWORD, DELICARE_ADMIN_PASSWORD (chỉ đặt trong phiên PowerShell, không ghi vào file của repo).
  KHÔNG chạy lên DB production khi chưa có checklist go-live (T48.2).
.EXAMPLE
  # Staging trên LocalDB của máy anh (Windows Authentication)
  ./deploy/db/Deploy-Database.ps1 -Server "(localdb)\MSSQLLocalDB" -Database AppDelicare_Staging -CreateDatabase -CreateSchemaIfEmpty -AdminPassword (Read-Host -AsSecureString "Mật khẩu admin")
.EXAMPLE
  # SQL Server dùng tài khoản SQL (hỏi mật khẩu)
  ./deploy/db/Deploy-Database.ps1 -Server "10.0.0.5,1433" -Database AppDelicare -SqlUser deploy -SqlPassword (Read-Host -AsSecureString)
.EXAMPLE
  # Dự phòng T48.3: từ máy dev FE vào SQL Server trong Docker (cổng 14333, chứng chỉ tự ký), mật khẩu từ biến môi trường
  $env:DELICARE_SQL_USER = "sa"; $env:DELICARE_SQL_PASSWORD = Read-Host "Mật khẩu SA"
  ./deploy/db/Deploy-Database.ps1 -Server "192.168.1.20,14333" -Database AppDelicare -TrustServerCertificate
.EXAMPLE
  # Chỉ in thứ tự script sẽ chạy (không kết nối DB)
  ./deploy/db/Deploy-Database.ps1 -ListOnly
.EXAMPLE
  # Xem migration nào sẽ chạy / ghi nhận trên 1 DB (không đổi gì)
  ./deploy/db/Deploy-Database.ps1 -Server "(localdb)\MSSQLLocalDB" -Database AppDelicare -ListMigrations
.EXAMPLE
  # Chỉ đồng bộ cấu trúc bảng (không chạy SP / seed)
  ./deploy/db/Deploy-Database.ps1 -Server "(localdb)\MSSQLLocalDB" -Database AppDelicare -MigrationsOnly
#>
[CmdletBinding()]
param(
    [string] $Server,
    [string] $Database,
    [string] $SqlUser = $env:DELICARE_SQL_USER,
    [SecureString] $SqlPassword,
    [switch] $TrustServerCertificate,
    [switch] $CreateDatabase,
    [switch] $CreateSchemaIfEmpty,
    [string] $AdminUserName = "admin",
    [SecureString] $AdminPassword,
    [switch] $ListOnly,
    [switch] $ListMigrations,
    [switch] $MigrationsOnly
)
$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path

function Plain([SecureString] $s) { if (-not $s) { return $null }; [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($s)) }

# ---- Thứ tự script: deploy/db/scripts.order ----
function Get-ScriptOrder {
    $orderFile = Join-Path $PSScriptRoot "scripts.order"
    if (-not (Test-Path $orderFile)) { throw "Không thấy $orderFile" }
    $list = New-Object System.Collections.Generic.List[string]
    $seen = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
    foreach ($raw in (Get-Content $orderFile -Encoding UTF8)) {
        $line = ($raw -replace '#.*$', '').Trim()
        if (-not $line) { continue }
        if (-not (Test-Path (Join-Path $root $line) -PathType Leaf)) { throw "scripts.order: không thấy file $line" }
        if (-not $seen.Add($line)) { throw "scripts.order: $line ghi 2 lần" }
        $list.Add($line)
    }
    # SP quên ghi vào scripts.order: vẫn chạy ở cuối (giữ hành vi cũ "chạy mọi DB/sp") + cảnh báo. Seed quên ghi: KHÔNG chạy.
    $extra = @(Get-ChildItem (Join-Path $root "DB/sp") -Filter "*.sql" | ForEach-Object { "DB/sp/" + $_.Name } |
        Where-Object { -not $seen.Contains($_) } | Sort-Object -CaseSensitive)
    foreach ($e in $extra) { Write-Warning "$e không có trong scripts.order – vẫn chạy ở cuối (hãy thêm vào scripts.order)"; $list.Add($e) }
    Get-ChildItem (Join-Path $root "DB/seed") -Filter "*.sql" | ForEach-Object { "DB/seed/" + $_.Name } |
        Where-Object { -not $seen.Contains($_) } | ForEach-Object { Write-Warning "$_ không có trong scripts.order – KHÔNG chạy" }
    return , $list.ToArray()
}

$order = Get-ScriptOrder
if ($ListOnly) {
    Write-Host "== Thứ tự sẽ chạy ($($order.Count) script):"
    $order | ForEach-Object { Write-Output $_ }
    return
}
if (-not $Server -or -not $Database) { throw "Cần -Server và -Database (hoặc -ListOnly)." }
if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) { throw "Cần cài sqlcmd (SQL Server Command Line Utilities)." }

# Tài khoản SQL: mật khẩu đưa cho sqlcmd qua biến môi trường SQLCMDPASSWORD (không nằm trên dòng lệnh); trả lại như cũ khi xong.
$oldSqlcmdPassword = $env:SQLCMDPASSWORD
[string[]] $auth = @("-E")
if ($SqlUser) {
    $pw = if ($SqlPassword) { Plain $SqlPassword } elseif ($env:DELICARE_SQL_PASSWORD) { $env:DELICARE_SQL_PASSWORD } else { Plain (Read-Host -AsSecureString "Mật khẩu SQL của $SqlUser") }
    $env:SQLCMDPASSWORD = $pw
    $auth = @("-U", $SqlUser)
}
if ($TrustServerCertificate) { $auth += "-C" }
if (-not $AdminPassword -and $env:DELICARE_ADMIN_PASSWORD) { $AdminPassword = ConvertTo-SecureString $env:DELICARE_ADMIN_PASSWORD -AsPlainText -Force }

function Invoke-Sql([string] $db, [string] $file) {
    $sqlArgs = @("-S", $Server, "-d", $db, "-b", "-I", "-f", "65001", "-i", $file) + $auth
    & sqlcmd @sqlArgs
    if ($LASTEXITCODE -ne 0) { throw "Lỗi khi chạy $file (exit $LASTEXITCODE)" }
}
function Query-Scalar([string] $db, [string] $sql) {
    $out = & sqlcmd -S $Server -d $db -b -h -1 -W -Q "SET NOCOUNT ON; $sql" @auth
    if ($LASTEXITCODE -ne 0) { throw "Lỗi truy vấn: $sql" }
    return ($out | Where-Object { $_ -match '\S' } | Select-Object -First 1).Trim()
}

try {
    Write-Host "== DELICARE DB deploy → $Server / $Database"

    if ($CreateDatabase) {
        $exists = Query-Scalar "master" "SELECT COUNT(*) FROM sys.databases WHERE name = N'$($Database.Replace("'", "''"))'"
        if ($exists -eq "0") {
            & sqlcmd -S $Server -d master -b -Q "CREATE DATABASE [$($Database.Replace(']', ']]'))]" @auth
            if ($LASTEXITCODE -ne 0) { throw "Không tạo được database $Database" }
            Write-Host "  + Đã tạo database $Database"
        }
    }

    # T48.5 (D-61): mức tương thích DB = 160 (SQL Server 2022): > 160 → đặt về 160; < 160 → chỉ cảnh báo.
    Invoke-Sql $Database (Join-Path $PSScriptRoot "compat-level.sql")

    # 2026-11-18: cấu trúc bảng = migration (DB/migrations) – cùng 1 bộ file cho mọi môi trường.
    . (Join-Path $PSScriptRoot "Migrations.ps1")
    if ($ListMigrations) {
        $plan = Get-MigrationPlan
        if ($plan.Count -eq 0) { Write-Host "  = Không có migration nào chờ – cấu trúc đã đồng bộ" }
        $plan | ForEach-Object { Write-Output ("{0,-6} {1}" -f $_.Action, $_.File.Name) }
        return
    }
    Invoke-Migrations -AllowCreateSchema:$CreateSchemaIfEmpty
    $tables = Query-Scalar $Database "SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0 AND name <> N'sysdiagrams'"
    Write-Host "  = Cấu trúc: $tables bảng"
    if ($MigrationsOnly) { Write-Host "== Xong (chỉ migration)."; return }

    $spCount = 0
    foreach ($rel in $order) {
        $file = Join-Path $root $rel
        if ($rel -like "*/00_Admin_seed.sql") {
            if (-not $AdminPassword) { Write-Host "  = Bỏ qua $rel (không có -AdminPassword)"; continue }
            # T48.0 (D-29): PBKDF2-SHA256, salt 16 byte, 100.000 vòng – cùng định dạng EncriptFunctionsHelper.GeneratePassword.
            $salt = New-Object byte[] 16
            [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($salt)
            $kdf = New-Object Security.Cryptography.Rfc2898DeriveBytes -ArgumentList ([Text.Encoding]::UTF8.GetBytes((Plain $AdminPassword))), $salt, 100000, ([Security.Cryptography.HashAlgorithmName]::SHA256)
            $hash = 'PBKDF2$100000$' + [Convert]::ToBase64String($salt) + '$' + [Convert]::ToBase64String($kdf.GetBytes(32))
            $kdf.Dispose()
            # Không truyền bằng "-v AdminPasswordHash=...": sqlcmd tách sai giá trị có "=" (đuôi base64) → lỗi "Invalid argument".
            # Dùng file tạm :setvar + :r (giá trị trong ngoặc kép).
            if ($AdminUserName -match '["\r\n]') { throw "AdminUserName không hợp lệ" }
            $wrap = Join-Path ([IO.Path]::GetTempPath()) "delicare_admin_$([Guid]::NewGuid().ToString('N')).sql"
            $text = ":setvar AdminUserName `"$AdminUserName`"`r`n:setvar AdminPasswordHash `"$hash`"`r`n:r `"$file`"`r`n"
            [IO.File]::WriteAllText($wrap, $text, (New-Object Text.UTF8Encoding $false))
            try { Invoke-Sql $Database $wrap } finally { Remove-Item $wrap -ErrorAction SilentlyContinue }
            Write-Host "  + Admin: $rel"
        } elseif ($rel -like "DB/sp/*") {
            Invoke-Sql $Database $file
            $spCount++
        } else {
            Invoke-Sql $Database $file
            Write-Host "  + Seed $rel"
        }
    }
    Write-Host "  + Stored procedure: $spCount file"
    Write-Host "== Xong."
} finally {
    $env:SQLCMDPASSWORD = $oldSqlcmdPassword
}
