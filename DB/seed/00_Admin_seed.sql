-- T48.1: tài khoản quản trị ban đầu cho DB MỚI (staging / UAT). Chạy bằng sqlcmd với biến (KHÔNG ghi mật khẩu vào file):
--   sqlcmd ... -v AdminUserName="admin" AdminPasswordHash="<MD5 dạng AB-CD-..>" -i DB/seed/00_Admin_seed.sql
-- deploy/db/Deploy-Database.ps1 tự tính hash từ mật khẩu nhập vào. Đã có user này thì KHÔNG đổi gì (idempotent).
-- Lưu ý: hash MD5 là cách app hiện dùng (CODE_AUDIT A19 – cần nâng cấp). Đổi mật khẩu ngay sau lần đăng nhập đầu.
SET NOCOUNT ON;
DECLARE @UserName NVARCHAR(100) = LOWER(N'$(AdminUserName)');
IF NOT EXISTS (SELECT 1 FROM [dbo].[User] WHERE UserName = @UserName AND IsDeleted = 0)
BEGIN
    DECLARE @Id UNIQUEIDENTIFIER = NEWID();
    INSERT INTO [dbo].[User] (Id, UserName, PassWord, IsActive, IsAdmin, IsDefault, CreatedDate, CreatedBy, IsDeleted)
    VALUES (@Id, @UserName, N'$(AdminPasswordHash)', 1, 1, 0, GETDATE(), @Id, 0);
    PRINT N'Đã tạo tài khoản quản trị ' + @UserName;
END
ELSE
    PRINT N'Tài khoản ' + @UserName + N' đã có – giữ nguyên.';
