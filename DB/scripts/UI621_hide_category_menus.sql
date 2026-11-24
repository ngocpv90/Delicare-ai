/*
  UI-6.21 (D-102 – anh chốt 2026-11-24): nhóm nguyên liệu / nhóm món quản lý ở cột trái màn Nguyên liệu / Món ăn → ẩn 2 mục menu riêng.
  Script DỮ LIỆU một lần (không tạo / sửa bảng, cột; không DELETE):
    Ẩn #/scm-ingredient-cat, #/menu-dish-cat: ObjectMenu.IsActive = 0, GIỮ dòng + UserRole (quyền API nhóm theo màn cha – ScreenMap.Parents; vai trò có quyền nhóm đều có quyền màn cha – UI-6.44).
    Link cũ mở màn cha (lọc đúng nhóm khi có ?id=).
  Ghi ModifiedDate / ModifiedBy (tài khoản system) + AuditChange. Chạy lại lần 2 không đổi gì. Số dòng UserRole phải không đổi.
  Bắt buộc truyền -v DryRun=1 (chạy thử) hoặc -v DryRun=0 (chạy thật). DB dev: chạy thật ngay trong task (D-92 a).
    sqlcmd -S <server> -d <db> -E -f 65001 -v DryRun=1 -i DB/scripts/UI621_hide_category_menus.sql -s "|" -W
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
DECLARE @DryRun BIT = $(DryRun);
DECLARE @SystemUser UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.[User] WHERE UserName = N'system' AND ISNULL(IsDeleted, 0) = 0);
IF @SystemUser IS NULL
    THROW 50101, N'Không có tài khoản "system" (seed T23) – không ghi được người thao tác.', 1;
DECLARE @Now DATETIME = DATEADD(HOUR, 7, SYSUTCDATETIME());

BEGIN TRANSACTION;
DECLARE @RolesBefore INT = (SELECT COUNT(*) FROM dbo.UserRole);
DECLARE @Menu TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY, MetaLink NVARCHAR(500), Name NVARCHAR(500));
INSERT INTO @Menu (Id, MetaLink, Name)
SELECT m.Id, m.MetaLink, m.Name FROM dbo.ObjectMenu AS m
WHERE ISNULL(m.IsDeleted, 0) = 0 AND m.MetaLink IN (N'#/scm-ingredient-cat', N'#/menu-dish-cat') AND m.IsActive = 1;

SELECT MetaLink AS [Mục menu], Name AS [Tên], N'Ẩn' AS [Đổi] FROM @Menu;

UPDATE m SET m.IsActive = 0, m.ModifiedDate = @Now, m.ModifiedBy = @SystemUser
FROM dbo.ObjectMenu AS m JOIN @Menu AS x ON x.Id = m.Id;

INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), x.Id, N'Modified', N'ObjectMenu', N'- IsActive: ''True'' => ''False'' (UI-6.21 D-102: ẩn mục nhóm, quản lý trong màn cha, giữ quyền)', @Now, @SystemUser, 0
FROM @Menu AS x;

DECLARE @RolesAfter INT = (SELECT COUNT(*) FROM dbo.UserRole);
SELECT (SELECT COUNT(*) FROM @Menu) AS [Mục menu đổi], @RolesBefore AS [UserRole trước], @RolesAfter AS [UserRole sau],
       CASE WHEN @DryRun = 1 THEN N'CHẠY THỬ – đã hoàn tác' ELSE N'ĐÃ GHI' END AS [Kết quả];
IF @RolesAfter <> @RolesBefore
    THROW 50102, N'Số dòng UserRole thay đổi – dừng.', 1;
IF @DryRun = 1
    ROLLBACK TRANSACTION;
ELSE
    COMMIT TRANSACTION;
