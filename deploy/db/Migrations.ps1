<#
  DELICARE – BỘ CHẠY MIGRATION CẤU TRÚC BẢNG (PowerShell, dot-source từ Deploy-Database.ps1). Cùng quy tắc với deploy/db/migrations.sh – sửa thì sửa CẢ HAI.
  Nguồn cấu trúc DUY NHẤT: DB/migrations/*.sql theo thứ tự tên file. Loại (dòng đầu "-- delicare-migration: <loại>"):
    baseline        124 bảng gốc – CHẠY khi DB chưa có bảng; có rồi → ADOPT (ghi nhận, không chạy)
    legacy-upgrade  nâng DB cũ 91 bảng – CHẠY khi còn thiếu bảng của baseline; đủ rồi → ADOPT
    normal          thay đổi về sau – CHẠY 1 lần (phải chạy lại được)
  Ghi nhận bằng extended property của database (không thêm bảng): DelicareMigration:<file> = <RUN|ADOPT>|<giờ UTC>|sha256:<16 ký tự, đã bỏ \r>.
  Cần sẵn: Invoke-Sql <db> <file>, Query-Scalar <db> <sql>, biến $Database, $root.
#>

$script:MigPrefix = "DelicareMigration:"

function Get-MigrationDir { if ($env:MIGRATIONS_DIR) { return $env:MIGRATIONS_DIR } return (Join-Path $root "DB/migrations") }

function Get-MigrationFiles {
    # Thứ tự byte (Ordinal – giống "LC_ALL=C sort" của bản bash).
    $dir = Get-MigrationDir
    $names = [string[]]@(Get-ChildItem $dir -Filter "*.sql" -File | ForEach-Object { $_.Name })
    [Array]::Sort($names, [StringComparer]::Ordinal)
    return @($names | ForEach-Object { Get-Item -LiteralPath (Join-Path $dir $_) })
}

function Get-MigrationMode([IO.FileInfo] $file) {
    $head = Get-Content $file.FullName -TotalCount 5 -Encoding UTF8
    foreach ($line in $head) {
        if ($line -match '^--\s*delicare-migration:\s*([a-z-]+)') { return $Matches[1] }
    }
    return "normal"
}

function Get-MigrationHash([IO.FileInfo] $file) {
    $text = [IO.File]::ReadAllText($file.FullName, [Text.Encoding]::UTF8).Replace("`r", "")
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $bytes = $sha.ComputeHash((New-Object Text.UTF8Encoding $false).GetBytes($text)) } finally { $sha.Dispose() }
    return (-join ($bytes | ForEach-Object { $_.ToString("x2") })).Substring(0, 16)
}

function SqlLit([string] $s) { return $s.Replace("'", "''") }

function Get-MigrationRecord([IO.FileInfo] $file) {
    $name = SqlLit ($script:MigPrefix + $file.Name)
    # Query-Scalar bỏ dòng trống → chưa ghi nhận trả '-' rồi đổi thành chuỗi rỗng.
    $v = Query-Scalar $Database "SELECT ISNULL((SELECT CAST(value AS nvarchar(400)) FROM sys.extended_properties WHERE class = 0 AND name = N'$name'), N'-')"
    if ($v -eq '-') { return '' }
    return $v
}

function Set-MigrationRecord([IO.FileInfo] $file, [string] $action) {
    $name = SqlLit ($script:MigPrefix + $file.Name)
    $value = SqlLit ("{0}|{1}|sha256:{2}" -f $action, [DateTime]::UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"), (Get-MigrationHash $file))
    $tmp = Join-Path ([IO.Path]::GetTempPath()) "delicare_migrec_$([Guid]::NewGuid().ToString('N')).sql"
    @"
SET NOCOUNT ON;
IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE class = 0 AND name = N'$name')
  EXEC sys.sp_updateextendedproperty @name = N'$name', @value = N'$value';
ELSE
  EXEC sys.sp_addextendedproperty @name = N'$name', @value = N'$value';
"@ | Set-Content $tmp -Encoding UTF8
    try { Invoke-Sql $Database $tmp } finally { Remove-Item $tmp -ErrorAction SilentlyContinue }
}

function Get-BaselineTables {
    $base = Get-MigrationFiles | Where-Object { (Get-MigrationMode $_) -eq "baseline" } | Select-Object -First 1
    if (-not $base) { return @() }
    return @(Select-String -Path $base.FullName -Pattern '^CREATE TABLE \[dbo\]\.\[([^\]]+)\]' | ForEach-Object { $_.Matches[0].Groups[1].Value })
}

function Get-MissingBaselineCount {
    $tables = Get-BaselineTables
    if ($tables.Count -eq 0) { return 0 }
    $values = ($tables | ForEach-Object { "(N'" + (SqlLit $_) + "')" }) -join ","
    return [int](Query-Scalar $Database "SELECT COUNT(*) FROM (VALUES $values) v(t) WHERE OBJECT_ID(N'dbo.' + QUOTENAME(v.t), N'U') IS NULL")
}

<# Kế hoạch: danh sách @{ Action = RUN|ADOPT|WARN; File = FileInfo } cho file chưa ghi nhận (WARN = đã chạy nhưng nội dung đổi). #>
function Get-MigrationPlan {
    $tables = [int](Query-Scalar $Database "SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0 AND name <> N'sysdiagrams'")
    $missing = Get-MissingBaselineCount
    $plan = New-Object System.Collections.Generic.List[object]
    foreach ($f in Get-MigrationFiles) {
        $rec = Get-MigrationRecord $f
        if ($rec) {
            if ($rec -match 'sha256:([0-9a-f]+)' -and $Matches[1] -ne (Get-MigrationHash $f)) { $plan.Add(@{ Action = "WARN"; File = $f }) }
            continue
        }
        switch (Get-MigrationMode $f) {
            "baseline" { if ($tables -eq 0) { $plan.Add(@{ Action = "RUN"; File = $f }); $tables = 124; $missing = 0 } else { $plan.Add(@{ Action = "ADOPT"; File = $f }) } }
            "legacy-upgrade" { if ($missing -gt 0) { $plan.Add(@{ Action = "RUN"; File = $f }); $missing = 0 } else { $plan.Add(@{ Action = "ADOPT"; File = $f }) } }
            default { $plan.Add(@{ Action = "RUN"; File = $f }) }
        }
    }
    return , $plan.ToArray()
}

function Invoke-Migrations([switch] $AllowCreateSchema) {
    $plan = Get-MigrationPlan
    $baselineRun = $plan | Where-Object { $_.Action -eq "RUN" -and (Get-MigrationMode $_.File) -eq "baseline" }
    if ($baselineRun -and -not $AllowCreateSchema) { throw "Database rỗng. Thêm -CreateSchemaIfEmpty để tạo 124 bảng (migration baseline)." }
    $ran = 0; $adopted = 0
    foreach ($step in $plan) {
        $f = $step.File
        switch ($step.Action) {
            "WARN" { Write-Warning "Migration $($f.Name) đã chạy nhưng nội dung file đã ĐỔI – không chạy lại; thay đổi mới phải là file migration mới" }
            "ADOPT" { Set-MigrationRecord $f "ADOPT"; $adopted++; Write-Host "  = Migration $($f.Name): DB đã có cấu trúc này – ghi nhận, không chạy" }
            "RUN" {
                $tmp = Join-Path ([IO.Path]::GetTempPath()) "delicare_mig_$([Guid]::NewGuid().ToString('N')).sql"
                # Bỏ "USE [AppDelicare]" để chạy được với tên DB khác.
                ([IO.File]::ReadAllText($f.FullName, [Text.Encoding]::UTF8)) -replace '(?m)^USE \[[^\]]+\];?\s*$', '' | Set-Content $tmp -Encoding UTF8
                try { Invoke-Sql $Database $tmp } catch { throw "Lỗi khi chạy migration $($f.Name) – dừng (chưa ghi nhận, sửa rồi chạy lại): $_" } finally { Remove-Item $tmp -ErrorAction SilentlyContinue }
                Set-MigrationRecord $f "RUN"; $ran++
                Write-Host "  + Migration $($f.Name): đã chạy"
            }
        }
    }
    Write-Host "  = Migration: chạy $ran, ghi nhận $adopted, tổng $(@(Get-MigrationFiles).Count) file"
}

# ---- DEPLOY-SYNC-1 (D-134): script CHUYỂN ĐỔI DỮ LIỆU 1 LẦN (deploy/db/once.order) – cùng cơ chế ghi nhận với migration (bản bash: migrations.sh run_once_scripts) ----
# Ghi nhận: DelicareOnce:<tên file> = <RUN|ADOPT>|<giờ UTC>|sha256:<16>. Chạy theo thứ tự once.order (Invoke-Sql đã truyền -v DryRun=0).
# -Adopt: CHỈ ghi nhận, không chạy – cho DB đã chạy tay các script này (vd DB dev của anh: chạy lại sẽ đổi dữ liệu nạp sau).
function Invoke-OnceScripts([switch] $Adopt) {
    $orderFile = Join-Path $root "deploy/db/once.order"
    if (-not (Test-Path $orderFile)) { return }
    $saved = $script:MigPrefix
    $script:MigPrefix = "DelicareOnce:"
    try {
        foreach ($raw in Get-Content $orderFile -Encoding UTF8) {
            $rel = ($raw -replace '#.*$', '').Trim()
            if (-not $rel) { continue }
            $path = Join-Path $root $rel
            if (-not (Test-Path $path -PathType Leaf)) { throw "once.order: không thấy file $rel" }
            $file = Get-Item -LiteralPath $path
            if (Get-MigrationRecord $file) { continue }
            if ($Adopt) {
                Set-MigrationRecord $file "ADOPT"
                Write-Host "  = Ghi nhận (ADOPT) chuyển đổi $rel"
            } else {
                Invoke-Sql $Database $file.FullName
                Set-MigrationRecord $file "RUN"
                Write-Host "  + Chuyển đổi 1 lần: $rel"
            }
        }
    } finally { $script:MigPrefix = $saved }
}
