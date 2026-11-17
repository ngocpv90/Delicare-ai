-- T48.4 (D-60): tài khoản SQL RIÊNG cho ứng dụng (api + job) – KHÔNG dùng sa. Quyền: đọc + ghi dữ liệu + EXECUTE SP; KHÔNG quyền DDL.
-- Chạy trong database ứng dụng (vd. AppDelicare) bằng tài khoản quản trị (sa / db-init). Idempotent: đã có login / user thì chỉ bảo đảm quyền.
-- Biến sqlcmd (truyền bằng file tạm :setvar + :r – KHÔNG ghi mật khẩu vào repo / dòng lệnh):
--   AppLogin        tên đăng nhập (vd. delicare_app) – không được là sa
--   AppPasswordHex  mật khẩu dạng hex của chuỗi ASCII (tránh ký tự đặc biệt bị sqlcmd hiểu sai) – mật khẩu mạnh theo chính sách SQL Server
--   ResetPassword   1 = đặt lại mật khẩu khi login đã có; 0 = giữ nguyên
-- Không tạo / sửa bảng. Chỉ chạy trên server SQL của môi trường đó (Docker dev / staging / production – do anh / IT chạy).
SET NOCOUNT ON;
DECLARE @login sysname = N'$(AppLogin)';
DECLARE @pw nvarchar(256) = CONVERT(varchar(256), CONVERT(varbinary(256), '0x$(AppPasswordHex)', 1));
DECLARE @reset bit = CASE WHEN N'$(ResetPassword)' = N'1' THEN 1 ELSE 0 END;
DECLARE @sql nvarchar(max);

IF @login IS NULL OR LTRIM(RTRIM(@login)) = N'' OR @login = N'sa'
    THROW 50001, N'AppLogin không hợp lệ (trống hoặc sa).', 1;
IF LEN(@pw) < 12
    THROW 50002, N'Mật khẩu tài khoản ứng dụng phải >= 12 ký tự.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = @login)
BEGIN
    SET @sql = N'CREATE LOGIN ' + QUOTENAME(@login) + N' WITH PASSWORD = ' + QUOTENAME(@pw, '''')
             + N', CHECK_POLICY = ON, DEFAULT_DATABASE = ' + QUOTENAME(DB_NAME()) + N';';
    EXEC sys.sp_executesql @sql;
    PRINT N'Đã tạo login ' + @login;
END
ELSE IF @reset = 1
BEGIN
    SET @sql = N'ALTER LOGIN ' + QUOTENAME(@login) + N' WITH PASSWORD = ' + QUOTENAME(@pw, '''') + N';';
    EXEC sys.sp_executesql @sql;
    PRINT N'Đã đặt lại mật khẩu login ' + @login;
END
ELSE
    PRINT N'Login ' + @login + N' đã có – giữ mật khẩu.';

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = @login)
BEGIN
    SET @sql = N'CREATE USER ' + QUOTENAME(@login) + N' FOR LOGIN ' + QUOTENAME(@login) + N';';
    EXEC sys.sp_executesql @sql;
    PRINT N'Đã tạo user ' + @login + N' trong ' + DB_NAME();
END

SET @sql = N'ALTER ROLE db_datareader ADD MEMBER ' + QUOTENAME(@login) + N'; '
         + N'ALTER ROLE db_datawriter ADD MEMBER ' + QUOTENAME(@login) + N'; '
         + N'GRANT EXECUTE TO ' + QUOTENAME(@login) + N';';
EXEC sys.sp_executesql @sql;
PRINT N'Quyền: db_datareader, db_datawriter, EXECUTE (không DDL).';
