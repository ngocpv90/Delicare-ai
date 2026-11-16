-- T48.1: tài khoản quản trị ban đầu cho DB MỚI (staging / UAT). Chạy bằng sqlcmd với biến (KHÔNG ghi mật khẩu vào file):
--   sqlcmd ... -v AdminUserName="admin" AdminPasswordHash="<PBKDF2$100000$salt$hash>" -i DB/seed/00_Admin_seed.sql
-- deploy/db/Deploy-Database.ps1 tự tính hash từ mật khẩu nhập vào. Đã có user này thì KHÔNG đổi gì (idempotent).
-- T48.0 (D-29): hash PBKDF2 (cùng định dạng app). Hash MD5 cũ vẫn đăng nhập được và tự nâng lên PBKDF2. Đổi mật khẩu sau lần đăng nhập đầu.
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
