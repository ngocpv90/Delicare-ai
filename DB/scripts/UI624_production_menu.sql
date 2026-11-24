/*
  UI-6.24 (D-105 – anh chốt 2026-11-24): menu Sản xuất 7 → 4 mục (Kế hoạch sản xuất · Mẻ nấu · Hao hụt · Dây chuyền & trạm bếp).
  Script DỮ LIỆU một lần (không tạo / sửa bảng, cột; không DELETE):
    1. Ẩn #/prod-order (Lệnh sản xuất – xem / thao tác trong Bảng sản xuất ngày ở trang kế hoạch), #/prod-output (Sản lượng – tab trong Mẻ nấu),
       #/prod-workstation (Trạm bếp – tab trong Dây chuyền): ObjectMenu.IsActive = 0, GIỮ dòng + UserRole. Quyền API theo màn cha (ScreenMap.ParentByClass:
       ProductionOrder → prod-plan, ProductionOutput → prod-batch, KitchenWorkstation → prod-line) – mọi vai trò có quyền mục cũ đều có quyền màn cha (UI-6.44).
    2. Đổi tên #/prod-line "Dây chuyền bếp" (hoặc tên gốc "Dây chuyền & năng lực") → "Dây chuyền & trạm bếp" (không ghi đè tên admin tự đặt).
  Link cũ: #/prod-order?id= → trang chi tiết lệnh; #/prod-order → Kế hoạch sản xuất; #/prod-output → Mẻ nấu (tab Sản lượng); #/prod-workstation → Dây chuyền (tab Trạm bếp).
  Ghi ModifiedDate / ModifiedBy (tài khoản system) + AuditChange. Chạy lại lần 2 không đổi gì. Số dòng UserRole phải không đổi.
  Bắt buộc truyền -v DryRun=1 (chạy thử) hoặc -v DryRun=0 (chạy thật). DB dev: chạy thật ngay trong task (D-92 a).
    sqlcmd -S <server> -d <db> -E -f 65001 -v DryRun=1 -i DB/scripts/UI624_production_menu.sql -s "|" -W
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
DECLARE @DryRun BIT = $(DryRun);
DECLARE @SystemUser UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.[User] WHERE UserName = N'system' AND ISNULL(IsDeleted, 0) = 0);
IF @SystemUser IS NULL
    THROW 50101, N'Không có tài khoản "system" (seed T23) – không ghi được người thao tác.', 1;
DECLARE @Now DATETIME = DATEADD(HOUR, 7, SYSUTCDATETIME());
DECLARE @NewName NVARCHAR(200) = N'Dây chuyền & trạm bếp';

BEGIN TRANSACTION;
DECLARE @RolesBefore INT = (SELECT COUNT(*) FROM dbo.UserRole);
DECLARE @Hide TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY, MetaLink NVARCHAR(500), Name NVARCHAR(500));
INSERT INTO @Hide (Id, MetaLink, Name)
SELECT m.Id, m.MetaLink, m.Name FROM dbo.ObjectMenu AS m
WHERE ISNULL(m.IsDeleted, 0) = 0 AND m.IsActive = 1 AND m.MetaLink IN (N'#/prod-order', N'#/prod-output', N'#/prod-workstation');
DECLARE @Rename TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY, OldName NVARCHAR(500));
INSERT INTO @Rename (Id, OldName)
SELECT m.Id, m.Name FROM dbo.ObjectMenu AS m
WHERE ISNULL(m.IsDeleted, 0) = 0 AND m.MetaLink = N'#/prod-line' AND m.Name IN (N'Dây chuyền bếp', N'Dây chuyền & năng lực');

SELECT MetaLink AS [Mục menu], Name AS [Tên], N'Ẩn' AS [Đổi] FROM @Hide
UNION ALL SELECT N'#/prod-line', OldName, N'Đổi tên → ' + @NewName FROM @Rename;

UPDATE m SET m.IsActive = 0, m.ModifiedDate = @Now, m.ModifiedBy = @SystemUser
FROM dbo.ObjectMenu AS m JOIN @Hide AS x ON x.Id = m.Id;
UPDATE m SET m.Name = @NewName, m.ModifiedDate = @Now, m.ModifiedBy = @SystemUser
FROM dbo.ObjectMenu AS m JOIN @Rename AS x ON x.Id = m.Id;

INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), x.Id, N'Modified', N'ObjectMenu', N'- IsActive: ''True'' => ''False'' (UI-6.24 D-105: menu Sản xuất 7 → 4 mục, quyền theo màn cha)', @Now, @SystemUser, 0 FROM @Hide AS x
UNION ALL
SELECT NEWID(), x.Id, N'Modified', N'ObjectMenu', N'- Name: ''' + x.OldName + N''' => ''' + @NewName + N''' (UI-6.24 D-105)', @Now, @SystemUser, 0 FROM @Rename AS x;

DECLARE @RolesAfter INT = (SELECT COUNT(*) FROM dbo.UserRole);
SELECT (SELECT COUNT(*) FROM @Hide) AS [Mục ẩn], (SELECT COUNT(*) FROM @Rename) AS [Đổi tên], @RolesBefore AS [UserRole trước], @RolesAfter AS [UserRole sau],
       CASE WHEN @DryRun = 1 THEN N'CHẠY THỬ – đã hoàn tác' ELSE N'ĐÃ GHI' END AS [Kết quả];
IF @RolesAfter <> @RolesBefore
    THROW 50102, N'Số dòng UserRole thay đổi – dừng.', 1;
IF @DryRun = 1
    ROLLBACK TRANSACTION;
ELSE
    COMMIT TRANSACTION;
