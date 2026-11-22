/*
  MENU-1 (anh yêu cầu 2026-11-22) – script DỮ LIỆU một lần (không tạo / sửa bảng, cột; không DELETE).
  Bỏ tiền tố "Duyệt – " (chữ "Duyệt", khoảng trắng, gạch dài, khoảng trắng = 8 ký tự) ở tên mục menu nhóm Phê duyệt
  (MetaLink '#/wf-inbox?type=...'). Chỉ đổi ObjectMenu.Name; giữ Id, MetaLink, ShortName, OrderShow và quyền đã gán (UserRole).
  Mục ngoài nhóm Phê duyệt không bị đụng. Chạy lại lần 2 không đổi gì (idempotent).
  Tên bước của yêu cầu duyệt ĐÃ tạo trước (ApprovalStep) giữ nguyên – không sửa dữ liệu lịch sử.

  Bắt buộc truyền -v DryRun=1 (chạy thử) hoặc -v DryRun=0 (chạy thật).
  CÁCH CHẠY (chỉ sau khi backup):
    Chạy thử (in tên trước / sau, KHÔNG ghi):
      sqlcmd -S <server> -d <db> -E -f 65001 -v DryRun=1 -i DB/scripts/MENU1_rename_approve_menu.sql -s "," -W -o menu1-preview.csv
    Chạy thật:
      sqlcmd -S <server> -d <db> -E -f 65001 -v DryRun=0 -i DB/scripts/MENU1_rename_approve_menu.sql -s "," -W -o menu1-applied.csv
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @DryRun BIT = $(DryRun);
DECLARE @SystemUser UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.[User] WHERE UserName = N'system' AND ISNULL(IsDeleted, 0) = 0);
IF @SystemUser IS NULL
    THROW 50101, N'Không có tài khoản "system" (seed T23) – không ghi được người thao tác.', 1;
DECLARE @Now DATETIME = DATEADD(HOUR, 7, SYSUTCDATETIME()); -- giờ Việt Nam như ứng dụng
DECLARE @Prefix NVARCHAR(20) = N'Duyệt – ';

BEGIN TRANSACTION;

DECLARE @RolesBefore INT = (SELECT COUNT(*) FROM dbo.UserRole);
DECLARE @Map TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY, MetaLink NVARCHAR(500), OldName NVARCHAR(500), NewName NVARCHAR(500));
INSERT INTO @Map (Id, MetaLink, OldName, NewName)
SELECT m.Id, m.MetaLink, m.Name, SUBSTRING(m.Name, LEN(@Prefix + N'x'), 500)
FROM dbo.ObjectMenu AS m
WHERE m.MetaLink LIKE N'#/wf-inbox?type=%' AND m.Name LIKE @Prefix + N'%' AND ISNULL(m.IsDeleted, 0) = 0;

-- Bảng đối chiếu (in cả khi chạy thử).
SELECT m.MetaLink AS [MetaLink], m.OldName AS [Tên cũ], m.NewName AS [Tên mới] FROM @Map AS m ORDER BY m.MetaLink;

IF @DryRun = 1
BEGIN
    ROLLBACK TRANSACTION;
    PRINT N'Chạy thử: không ghi gì. Chạy thật: thêm -v DryRun=0 (sau khi backup).';
    RETURN;
END

UPDATE o SET o.Name = m.NewName, o.ModifiedDate = @Now, o.ModifiedBy = @SystemUser
FROM dbo.ObjectMenu AS o JOIN @Map AS m ON m.Id = o.Id;

INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), m.Id, N'Modified', N'ObjectMenu',
       N'- Actor: SYSTEM:' + LOWER(CAST(@SystemUser AS NVARCHAR(36))) + N' | MENU-1 bỏ tiền tố "Duyệt – " <br />' + CHAR(13) + CHAR(10)
       + N'- Name: ''' + m.OldName + N''' → ''' + m.NewName + N''' <br />',
       @Now, @SystemUser, 0
FROM @Map AS m;

DECLARE @RolesAfter INT = (SELECT COUNT(*) FROM dbo.UserRole);
IF @RolesAfter <> @RolesBefore
    THROW 50102, N'Số dòng UserRole thay đổi – hủy (quyền không được đổi).', 1;

COMMIT TRANSACTION;
SELECT @RolesBefore AS [UserRole trước], @RolesAfter AS [UserRole sau];
PRINT N'Đã áp dụng. Chạy lại lần 2 sẽ không đổi gì.';
