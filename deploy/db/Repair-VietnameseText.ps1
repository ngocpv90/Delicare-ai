<#
.SYNOPSIS
  Sửa tên / ghi chú tiếng Việt bị lỗi font trong các bảng cấu hình do seed từng chạy bằng sqlcmd THIẾU -f 65001
  (UTF-8 bị đọc như Windows-1252: "Trưa" → "TrÆ°a"). T50R1, 2026-11-17.
.DESCRIPTION
  Chỉ sửa giá trị giải mã ngược được (ký tự → byte Windows-1252 → UTF-8 hợp lệ); chữ tiếng Việt đúng không bao giờ giải mã được nên không bị đụng.
  Bảng: Company, OrganizationUnit, ObjectMenu, Shift, UserGroup, FoodSafetyChecklist, FoodSafetyChecklistItem (mọi cột nvarchar).
  UPDATE từng dòng theo Id (tham số hóa, điều kiện giá trị cũ), ModifiedBy = admin. Mặc định chỉ liệt kê; thêm -Apply để sửa. Không chạy lên production.
.EXAMPLE
  ./deploy/db/Repair-VietnameseText.ps1 -Server "(localdb)\MSSQLLocalDB" -Database AppDelicare_Staging          # chạy thử
  ./deploy/db/Repair-VietnameseText.ps1 -Server "(localdb)\MSSQLLocalDB" -Database AppDelicare_Staging -Apply   # sửa
#>
param([Parameter(Mandatory)][string]$Server, [Parameter(Mandatory)][string]$Database, [switch]$Apply)
$ErrorActionPreference = "Stop"
$cs = "Server=$Server;Database=$Database;Integrated Security=true;TrustServerCertificate=true"
$cp = [Text.Encoding]::GetEncoding(1252, [Text.EncoderFallback]::ExceptionFallback, [Text.DecoderFallback]::ExceptionFallback)
$utf8 = New-Object Text.UTF8Encoding($false, $true)
function Fix([string]$s) {
    if ([string]::IsNullOrEmpty($s)) { return $null }
    $bytes = New-Object System.Collections.Generic.List[byte]
    $suspicious = $false
    foreach ($ch in $s.ToCharArray()) {
        $code = [int]$ch
        if ($code -lt 0x80) { $bytes.Add([byte]$code); continue }
        $suspicious = $true
        if ($code -le 0xFF) { $bytes.Add([byte]$code); continue }
        try { $b = $cp.GetBytes([string]$ch) } catch { return $null }
        foreach ($x in $b) { $bytes.Add($x) }
    }
    if (-not $suspicious) { return $null }
    try { $d = $utf8.GetString($bytes.ToArray()) } catch { return $null }
    if ($d -eq $s -or $d -match '[\u0080-\u009F]') { return $null }
    return $d
}
$cn = New-Object System.Data.SqlClient.SqlConnection $cs; $cn.Open()
$admin = (New-Object System.Data.SqlClient.SqlCommand("SELECT TOP 1 Id FROM dbo.[User] WHERE UserName = N'admin' AND IsDeleted = 0", $cn)).ExecuteScalar()
if (-not $admin) { throw "Không có admin" }
$tables = @("Company", "OrganizationUnit", "ObjectMenu", "Shift", "UserGroup", "FoodSafetyChecklist", "FoodSafetyChecklistItem")
$total = 0
foreach ($t in $tables) {
    $cols = @(); $hasMod = $false
    $r = (New-Object System.Data.SqlClient.SqlCommand("SELECT c.name, t.name FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id WHERE c.object_id = OBJECT_ID(N'dbo.$t')", $cn)).ExecuteReader()
    while ($r.Read()) { if ($r.GetString(1) -eq 'nvarchar') { $cols += $r.GetString(0) }; if ($r.GetString(0) -eq 'ModifiedBy') { $hasMod = $true } }
    $r.Close()
    $sel = "SELECT Id, " + (($cols | % { "[$_]" }) -join ", ") + " FROM dbo.[$t]"
    $da = New-Object System.Data.SqlClient.SqlDataAdapter($sel, $cn); $dt = New-Object System.Data.DataTable; [void]$da.Fill($dt)
    foreach ($row in $dt.Rows) {
        foreach ($c in $cols) {
            if ($row[$c] -is [DBNull]) { continue }
            $fixed = Fix ([string]$row[$c])
            if ($fixed) {
                $total++
                Write-Host "$t.$c [$($row['Id'])]: $($row[$c]) -> $fixed"
                if ($Apply) {
                    $sql = "UPDATE dbo.[$t] SET [$c] = @v" + $(if ($hasMod) { ", ModifiedBy = @by, ModifiedDate = GETDATE()" } else { "" }) + " WHERE Id = @id AND [$c] = @old"
                    $cmd = New-Object System.Data.SqlClient.SqlCommand($sql, $cn)
                    [void]$cmd.Parameters.AddWithValue("@v", $fixed); [void]$cmd.Parameters.AddWithValue("@old", [string]$row[$c])
                    [void]$cmd.Parameters.AddWithValue("@id", $row['Id']); [void]$cmd.Parameters.AddWithValue("@by", $admin)
                    [void]$cmd.ExecuteNonQuery()
                }
            }
        }
    }
}
$cn.Close()
Write-Host "Tổng: $total giá trị $(if ($Apply) { 'đã sửa' } else { 'cần sửa (chạy thử)' })"
