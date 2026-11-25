/*
  UI-6.49 (6) (D-127 e – anh chốt 2026-11-24): mục duyệt "Phê duyệt › Xếp xe chưa đủ hàng" (#/wf-inbox?type=log-load-short)
  – xếp xe có điều kiện (DeliveryRoute/Load + OverrideReason) khi chuyến chưa đủ thực đơn; trước đây chỉ quản trị.
  Script DỮ LIỆU (không tạo bảng / cột, không DELETE):
    1. Thêm ObjectMenu (cùng nhóm cha với "Phê duyệt › Tài khoản") nếu chưa có.
    2. Cấp Truy cập + Sửa (= được duyệt, D-31) cho nhóm COO, OPS-SCM-HEAD (theo UserGroup.Code – D-124) nếu chưa có dòng quyền.
  Mục mới – không ẩn mục nào → luật UI-6.44 không ai mất quyền xem tự thỏa. Ghi CreatedBy (system) + AuditChange. Chạy lại = 0 việc.
  -v DryRun=1 (thử) / 0 (thật). DB dev chạy thật (D-92 a).
    sqlcmd -S <server> -d <db> -E -b -f 65001 -v DryRun=1 -i DB/scripts/UI649_load_short_menu.sql -s "|" -W
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
DECLARE @DryRun BIT = $(DryRun);
DECLARE @SystemUser UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.[User] WHERE UserName = N'system' AND ISNULL(IsDeleted, 0) = 0);
IF @SystemUser IS NULL
    THROW 50101, N'Không có tài khoản "system" (seed T23) – không ghi được người thao tác.', 1;
DECLARE @Now DATETIME = DATEADD(HOUR, 7, SYSUTCDATETIME());
DECLARE @Link NVARCHAR(200) = N'#/wf-inbox?type=log-load-short';

BEGIN TRANSACTION;
DECLARE @Parent UNIQUEIDENTIFIER = (SELECT TOP 1 ParentId FROM dbo.ObjectMenu WHERE MetaLink = N'#/wf-inbox?type=adm-user' AND ISNULL(IsDeleted, 0) = 0);
IF @Parent IS NULL
    THROW 50104, N'Không thấy nhóm menu Phê duyệt (mục #/wf-inbox?type=adm-user).', 1;
DECLARE @Menu UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.ObjectMenu WHERE MetaLink = @Link AND ISNULL(IsDeleted, 0) = 0);
DECLARE @MenuAdded INT = 0, @RoleAdded INT = 0;
IF @Menu IS NULL
BEGIN
    SET @Menu = NEWID();
    INSERT INTO dbo.ObjectMenu (Id, ParentId, Name, ShortName, MetaLink, LevelShow, OrderShow, IsActive, CreatedDate, CreatedBy, IsDeleted)
    SELECT @Menu, @Parent, N'Xếp xe chưa đủ hàng', N'P6', @Link, 2,
           ISNULL((SELECT MAX(OrderShow) FROM dbo.ObjectMenu WHERE ParentId = @Parent AND ISNULL(IsDeleted, 0) = 0), 0) + 10, 1, @Now, @SystemUser, 0;
    SET @MenuAdded = 1;
    INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
    VALUES (NEWID(), @Menu, N'Added', N'ObjectMenu', N'- UI-6.49 (6) (D-127 e): thêm mục duyệt "Phê duyệt › Xếp xe chưa đủ hàng"', @Now, @SystemUser, 0);
END

DECLARE @Grant TABLE (UserGroupId UNIQUEIDENTIFIER PRIMARY KEY, Code NVARCHAR(50));
INSERT INTO @Grant
SELECT g.Id, g.Code FROM dbo.UserGroup AS g
WHERE ISNULL(g.IsDeleted, 0) = 0 AND g.Code IN (N'COO', N'OPS-SCM-HEAD')
  AND NOT EXISTS (SELECT 1 FROM dbo.UserRole AS r WHERE r.UserGroupId = g.Id AND r.ObjectMenuId = @Menu AND ISNULL(r.IsDeleted, 0) = 0);
INSERT INTO dbo.UserRole (Id, UserGroupId, ObjectMenuId, AllowAccess, AllowAdd, AllowEdit, AllowDelete, AllowPrint, AllowExport, AllowImport, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), x.UserGroupId, @Menu, 1, 0, 1, 0, 0, 0, 0, @Now, @SystemUser, 0 FROM @Grant AS x;
SET @RoleAdded = @@ROWCOUNT;
INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), x.UserGroupId, N'RoleSaveMany', N'UserGroup', N'- Xếp xe chưa đủ hàng: Không => Được duyệt (UI-6.49 (6))', @Now, @SystemUser, 0 FROM @Grant AS x;

SELECT @MenuAdded AS [Mục thêm], @RoleAdded AS [Dòng quyền thêm], (SELECT STRING_AGG(Code, N', ') FROM @Grant) AS [Nhóm được cấp],
       CASE WHEN @DryRun = 1 THEN N'CHẠY THỬ – đã hoàn tác' ELSE N'ĐÃ GHI' END AS [Kết quả];
IF @DryRun = 1 ROLLBACK TRANSACTION; ELSE COMMIT TRANSACTION;
