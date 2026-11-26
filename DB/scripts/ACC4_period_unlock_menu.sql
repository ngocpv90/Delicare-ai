/*
  ACC-4 (D-131 h – anh chốt 2026-11-26): mục duyệt "Phê duyệt › Mở khóa kỳ" (#/wf-inbox?type=acc-period-unlock)
  – Kế toán xin mở khóa kỳ kế toán đã khóa (lý do bắt buộc), COO duyệt (engine D-79 / D-31).
  Script DỮ LIỆU (không tạo bảng / cột, không DELETE):
    1. Thêm ObjectMenu (cùng nhóm cha với "Phê duyệt › Tài khoản") nếu chưa có.
    2. Cấp Truy cập + Sửa (= được duyệt, D-31) cho nhóm COO (theo UserGroup.Code – D-124) nếu chưa có dòng quyền.
  Mục mới – không ẩn mục nào → luật UI-6.44 không ai mất quyền xem tự thỏa. Ghi CreatedBy (system) + AuditChange. Chạy lại = 0 việc.
  -v DryRun=1 (thử) / 0 (thật). DB dev chạy thật (D-92 a).
    sqlcmd -S <server> -d <db> -E -b -f 65001 -v DryRun=1 -i DB/scripts/ACC4_period_unlock_menu.sql -s "|" -W
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
DECLARE @DryRun BIT = $(DryRun);
DECLARE @SystemUser UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.[User] WHERE UserName = N'system' AND ISNULL(IsDeleted, 0) = 0);
IF @SystemUser IS NULL
    THROW 50101, N'Không có tài khoản "system" (seed T23) – không ghi được người thao tác.', 1;
DECLARE @Now DATETIME = DATEADD(HOUR, 7, SYSUTCDATETIME());
DECLARE @Link NVARCHAR(200) = N'#/wf-inbox?type=acc-period-unlock';

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
    SELECT @Menu, @Parent, N'Mở khóa kỳ', N'ACC', @Link, 2,
           ISNULL((SELECT MAX(OrderShow) FROM dbo.ObjectMenu WHERE ParentId = @Parent AND ISNULL(IsDeleted, 0) = 0), 0) + 10, 1, @Now, @SystemUser, 0;
    SET @MenuAdded = 1;
    INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
    VALUES (NEWID(), @Menu, N'Added', N'ObjectMenu', N'- ACC-4 (D-131 h): thêm mục duyệt "Phê duyệt › Mở khóa kỳ"', @Now, @SystemUser, 0);
END

DECLARE @Grant TABLE (UserGroupId UNIQUEIDENTIFIER PRIMARY KEY, Code NVARCHAR(50));
INSERT INTO @Grant
SELECT g.Id, g.Code FROM dbo.UserGroup AS g
WHERE ISNULL(g.IsDeleted, 0) = 0 AND g.Code IN (N'COO')
  AND NOT EXISTS (SELECT 1 FROM dbo.UserRole AS r WHERE r.UserGroupId = g.Id AND r.ObjectMenuId = @Menu AND ISNULL(r.IsDeleted, 0) = 0);
INSERT INTO dbo.UserRole (Id, UserGroupId, ObjectMenuId, AllowAccess, AllowAdd, AllowEdit, AllowDelete, AllowPrint, AllowExport, AllowImport, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), x.UserGroupId, @Menu, 1, 0, 1, 0, 0, 0, 0, @Now, @SystemUser, 0 FROM @Grant AS x;
SET @RoleAdded = @@ROWCOUNT;
INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), x.UserGroupId, N'RoleSaveMany', N'UserGroup', N'- Mở khóa kỳ: Không => Được duyệt (ACC-4)', @Now, @SystemUser, 0 FROM @Grant AS x;

SELECT @MenuAdded AS [Mục thêm], @RoleAdded AS [Dòng quyền thêm], (SELECT STRING_AGG(Code, N', ') FROM @Grant) AS [Nhóm được cấp],
       CASE WHEN @DryRun = 1 THEN N'CHẠY THỬ – đã hoàn tác' ELSE N'ĐÃ GHI' END AS [Kết quả];
IF @DryRun = 1 ROLLBACK TRANSACTION; ELSE COMMIT TRANSACTION;
