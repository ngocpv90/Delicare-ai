<#
.SYNOPSIS
  Publish DELICARE API cho IIS (T48.1). Tạo thư mục publish (có web.config) để chép lên máy chủ IIS.
.EXAMPLE
  ./deploy/iis/Publish-Iis.ps1 -Output C:\Deploy\delicare-api -Environment Staging
#>
param(
    [Parameter(Mandatory)] [string] $Output,
    [ValidateSet("Staging", "Production")] [string] $Environment = "Staging"
)
$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$project = Join-Path $root "AppDelicare/AppDelicare.WebApi/AppDelicare.WebApi.csproj"
dotnet publish $project -c Release -o $Output /p:EnvironmentName=$Environment
if ($LASTEXITCODE -ne 0) { throw "dotnet publish lỗi" }
New-Item -ItemType Directory -Force (Join-Path $Output "Logs") | Out-Null
Write-Host "Đã publish vào $Output (ASPNETCORE_ENVIRONMENT=$Environment ghi trong web.config)."
Write-Host "Bước tiếp theo: deploy/README.md mục IIS – đặt biến môi trường bí mật cho Application Pool, KHÔNG ghi vào web.config trong repo."
