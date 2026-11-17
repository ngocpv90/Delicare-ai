-- T48.5 (D-61): giữ mức tương thích DB = 160 (SQL Server 2022). Idempotent – chạy trên DB đích (sqlcmd -d <DB>).
--   > 160 (DB tạo trên SQL Server 2025) → đặt về 160 + ghi log.
--   = 160 → giữ nguyên.
--   < 160 → chỉ cảnh báo, KHÔNG tự nâng (đổi mức tương thích lên cao là việc của anh / DBA).
-- Chỉ đổi cấu hình DB, không đụng bảng / cột / dữ liệu. Deploy-Database.ps1 và deploy/docker/db-init.sh gọi file này ngay sau khi tạo / mở DB.
SET NOCOUNT ON;
DECLARE @db sysname = DB_NAME();
DECLARE @level int = (SELECT compatibility_level FROM sys.databases WHERE database_id = DB_ID());
IF @level > 160
BEGIN
    DECLARE @sql nvarchar(400) = N'ALTER DATABASE ' + QUOTENAME(@db) + N' SET COMPATIBILITY_LEVEL = 160;';
    EXEC (@sql);
    PRINT N'  + Mức tương thích ' + @db + N': ' + CAST(@level AS nvarchar(10)) + N' → 160 (D-61, SQL Server 2022)';
END
ELSE IF @level < 160
    PRINT N'  ! CẢNH BÁO: mức tương thích ' + @db + N' = ' + CAST(@level AS nvarchar(10)) + N' (< 160) – không tự nâng; D-61 chuẩn là 160 (SQL Server 2022).';
ELSE
    PRINT N'  = Mức tương thích ' + @db + N': 160';
