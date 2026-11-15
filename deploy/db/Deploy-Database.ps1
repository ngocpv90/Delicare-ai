<#
.SYNOPSIS
  Triển khai DB DELICARE (T48.1) – idempotent, chạy lại nhiều lần an toàn.
.DESCRIPTION
  Thứ tự:
    1. (tùy chọn -CreateSchemaIfEmpty) DB chưa có bảng nào → tạo 124 bảng từ DDL chuẩn của PO
       (docs/reference/20261113_structure_db_appdelicare.sql). DB đã có bảng → BỎ QUA (không bao giờ sửa bảng có sẵn).
    2. Mọi stored procedure trong DB/sp/*.sql (CREATE OR ALTER).
    3. Tài khoản quản trị ban đầu (DB/seed/00_Admin_seed.sql) nếu có -AdminPassword và user chưa tồn tại.
    4. Seed cấu hình: OrganizationUnit → ObjectMenu → UserRole.
  KHÔNG chạy lên DB production khi chưa có checklist go-live (T48.2).
.EXAMPLE
  # Staging trên LocalDB của máy anh (Windows Authentication)
  ./deploy/db/Deploy-Database.ps1 -Server "(localdb)\MSSQLLocalDB" -Database AppDelicare_Staging -CreateDatabase -CreateSchemaIfEmpty -AdminPassword (Read-Host -AsSecureString "Mật khẩu admin")
.EXAMPLE
  # SQL Server dùng tài khoản SQL
  ./deploy/db/Deploy-Database.ps1 -Server "10.0.0.5,1433" -Database AppDelicare -SqlUser deploy -SqlPassword (Read-Host -AsSecureString)
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Server,
    [Parameter(Mandatory)] [string] $Database,
    [string] $SqlUser,
    [SecureString] $SqlPassword,
    [switch] $CreateDatabase,
    [switch] $CreateSchemaIfEmpty,
    [string] $AdminUserName = "admin",
    [SecureString] $AdminPassword
)
$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path

function Plain([SecureString] $s) { if (-not $s) { return $null }; [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($s)) }

[string[]] $auth = if ($SqlUser) { @("-U", $SqlUser, "-P", (Plain $SqlPassword)) } else { @("-E") }
function Invoke-Sql([string] $db, [string] $file, [string[]] $vars = @()) {
    $sqlArgs = @("-S", $Server, "-d", $db, "-b", "-I", "-f", "65001", "-i", $file) + $auth
    if ($vars.Count -gt 0) { $sqlArgs += @("-v") + $vars }
    & sqlcmd @sqlArgs
    if ($LASTEXITCODE -ne 0) { throw "Lỗi khi chạy $file (exit $LASTEXITCODE)" }
}
function Query-Scalar([string] $db, [string] $sql) {
    $out = & sqlcmd -S $Server -d $db -b -h -1 -W -Q "SET NOCOUNT ON; $sql" @auth
    if ($LASTEXITCODE -ne 0) { throw "Lỗi truy vấn: $sql" }
    return ($out | Where-Object { $_ -match '\S' } | Select-Object -First 1).Trim()
}

Write-Host "== DELICARE DB deploy → $Server / $Database"
if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) { throw "Cần cài sqlcmd (SQL Server Command Line Utilities)." }

if ($CreateDatabase) {
    $exists = Query-Scalar "master" "SELECT COUNT(*) FROM sys.databases WHERE name = N'$($Database.Replace("'", "''"))'"
    if ($exists -eq "0") {
        & sqlcmd -S $Server -d master -b -Q "CREATE DATABASE [$($Database.Replace(']', ']]'))]" @auth
        if ($LASTEXITCODE -ne 0) { throw "Không tạo được database $Database" }
        Write-Host "  + Đã tạo database $Database"
    }
}

$tables = Query-Scalar $Database "SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0"
if ($tables -eq "0") {
    if (-not $CreateSchemaIfEmpty) { throw "Database rỗng. Thêm -CreateSchemaIfEmpty để tạo 124 bảng từ DDL chuẩn." }
    $ddl = Join-Path $root "docs/reference/20261113_structure_db_appdelicare.sql"
    $tmp = Join-Path ([IO.Path]::GetTempPath()) "delicare_ddl_$([Guid]::NewGuid().ToString('N')).sql"
    # Bỏ "USE [AppDelicare]" để chạy được với tên DB khác.
    (Get-Content $ddl -Raw -Encoding UTF8) -replace '(?m)^USE \[[^\]]+\]\s*$', '' | Set-Content $tmp -Encoding UTF8
    try { Invoke-Sql $Database $tmp } finally { Remove-Item $tmp -ErrorAction SilentlyContinue }
    $tables = Query-Scalar $Database "SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0"
    Write-Host "  + Đã tạo schema: $tables bảng"
} else {
    Write-Host "  = Schema đã có ($tables bảng) – không đụng tới bảng."
}

$sps = Get-ChildItem (Join-Path $root "DB/sp") -Filter "*.sql" | Sort-Object Name
foreach ($f in $sps) { Invoke-Sql $Database $f.FullName }
Write-Host "  + Stored procedure: $($sps.Count) file"

if ($AdminPassword) {
    $md5 = [Security.Cryptography.MD5]::Create()
    $hash = [BitConverter]::ToString($md5.ComputeHash([Text.Encoding]::UTF8.GetBytes((Plain $AdminPassword))))
    Invoke-Sql $Database (Join-Path $root "DB/seed/00_Admin_seed.sql") @("AdminUserName=$AdminUserName", "AdminPasswordHash=$hash")
}

foreach ($name in @("OrganizationUnit_seed.sql", "ObjectMenu_seed.sql", "UserRole_seed.sql")) {
    Invoke-Sql $Database (Join-Path $root "DB/seed/$name")
    Write-Host "  + Seed $name"
}
Write-Host "== Xong."
