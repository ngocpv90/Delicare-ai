/*
  ACC-6 (D-131 j – anh chốt 2026-11-26): mục "Đóng gói & Giao hàng › Định mức bao bì" (#/log-packaging-norm) – ma trận vật tư bao bì × loại suất
  (số / 100 suất), lưu bằng công thức món bao bì BB-<buổi>, gửi duyệt như công thức (RECIPE_VERSION). Ghi kết quả đóng gói → phiếu xuất bao bì Nháp theo ca.
  Script DỮ LIỆU (không tạo bảng / cột, không DELETE):
    1. Thêm ObjectMenu cuối nhóm Đóng gói & Giao hàng (#/group/07, sau "Sổ khay / hộp" #/log-packaging-ledger) nếu chưa có.
    2. Cấp quyền (theo UserGroup.Code – D-124; đã có dòng thì chỉ OR thêm cờ, không bớt):
         OPS-PLAN : Truy cập + Thêm + Sửa
         FIN-ACC  : Truy cập + In + Xuất
         CEO, CEO-PMO, COO, OPS-SCM, OPS-SCM-HEAD, OPS-WH, OPS-KIT, OPS-LOG, FIN-IT : Truy cập
  Mục mới – không ẩn mục nào → luật UI-6.44 tự thỏa. Ghi CreatedBy (system) + AuditChange. Chạy lại = 0 việc.
  -v DryRun=1 (thử) / 0 (thật). DB dev chạy thật (D-92 a).
    sqlcmd -S <server> -d <db> -E -b -f 65001 -v DryRun=1 -i DB/scripts/ACC6_packaging_norm_menu.sql -s "|" -W
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
DECLARE @DryRun BIT = $(DryRun);
DECLARE @SystemUser UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.[User] WHERE UserName = N'system' AND ISNULL(IsDeleted, 0) = 0);
IF @SystemUser IS NULL
    THROW 50101, N'Không có tài khoản "system" (seed T23) – không ghi được người thao tác.', 1;
DECLARE @Now DATETIME = DATEADD(HOUR, 7, SYSUTCDATETIME());
DECLARE @Link NVARCHAR(200) = N'#/log-packaging-norm';

BEGIN TRANSACTION;
DECLARE @Group UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.ObjectMenu WHERE MetaLink = N'#/group/07' AND ISNULL(IsDeleted, 0) = 0);
IF @Group IS NULL
    THROW 50104, N'Không thấy nhóm menu Đóng gói & Giao hàng (#/group/07).', 1;
DECLARE @Menu UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.ObjectMenu WHERE MetaLink = @Link AND ISNULL(IsDeleted, 0) = 0);
DECLARE @MenuAdded INT = 0, @Shifted INT = 0, @RoleAdded INT = 0, @RoleOr INT = 0;
IF @Menu IS NULL
BEGIN
    DECLARE @After INT = ISNULL((SELECT TOP 1 OrderShow FROM dbo.ObjectMenu WHERE MetaLink = N'#/log-packaging-ledger' AND ParentId = @Group AND ISNULL(IsDeleted, 0) = 0),
                                (SELECT MAX(OrderShow) FROM dbo.ObjectMenu WHERE ParentId = @Group AND ISNULL(IsDeleted, 0) = 0));
    UPDATE dbo.ObjectMenu SET OrderShow = OrderShow + 1, ModifiedDate = @Now, ModifiedBy = @SystemUser
    WHERE ParentId = @Group AND ISNULL(IsDeleted, 0) = 0 AND OrderShow > ISNULL(@After, 0);
    SET @Shifted = @@ROWCOUNT;
    SET @Menu = NEWID();
    INSERT INTO dbo.ObjectMenu (Id, ParentId, Name, ShortName, MetaLink, LevelShow, OrderShow, IsActive, CreatedDate, CreatedBy, IsDeleted)
    VALUES (@Menu, @Group, N'Định mức bao bì', N'ACC', @Link, 2, ISNULL(@After, 0) + 1, 1, @Now, @SystemUser, 0);
    SET @MenuAdded = 1;
    INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
    VALUES (NEWID(), @Menu, N'Added', N'ObjectMenu', N'- ACC-6 (D-131 j): thêm mục "Đóng gói & Giao hàng › Định mức bao bì"', @Now, @SystemUser, 0);
END

DECLARE @Want TABLE (Code NVARCHAR(50) PRIMARY KEY, A BIT, Ad BIT, E BIT, P BIT, X BIT);
INSERT INTO @Want VALUES (N'OPS-PLAN', 1, 1, 1, 0, 0), (N'FIN-ACC', 1, 0, 0, 1, 1), (N'CEO', 1, 0, 0, 0, 0), (N'CEO-PMO', 1, 0, 0, 0, 0), (N'COO', 1, 0, 0, 0, 0),
                         (N'OPS-SCM', 1, 0, 0, 0, 0), (N'OPS-SCM-HEAD', 1, 0, 0, 0, 0), (N'OPS-WH', 1, 0, 0, 0, 0), (N'OPS-KIT', 1, 0, 0, 0, 0), (N'OPS-LOG', 1, 0, 0, 0, 0), (N'FIN-IT', 1, 0, 0, 0, 0);

-- Đã có dòng quyền: OR thêm cờ còn thiếu.
DECLARE @Or TABLE (UserGroupId UNIQUEIDENTIFIER, Code NVARCHAR(50));
UPDATE r SET AllowAccess = CASE WHEN w.A = 1 THEN 1 ELSE r.AllowAccess END, AllowAdd = CASE WHEN w.Ad = 1 THEN 1 ELSE r.AllowAdd END,
             AllowEdit = CASE WHEN w.E = 1 THEN 1 ELSE r.AllowEdit END, AllowPrint = CASE WHEN w.P = 1 THEN 1 ELSE r.AllowPrint END,
             AllowExport = CASE WHEN w.X = 1 THEN 1 ELSE r.AllowExport END, ModifiedDate = @Now, ModifiedBy = @SystemUser
OUTPUT inserted.UserGroupId, g.Code INTO @Or
FROM dbo.UserRole AS r JOIN dbo.UserGroup AS g ON g.Id = r.UserGroupId JOIN @Want AS w ON w.Code = g.Code
WHERE r.ObjectMenuId = @Menu AND ISNULL(r.IsDeleted, 0) = 0 AND ISNULL(g.IsDeleted, 0) = 0
  AND ((w.A = 1 AND ISNULL(r.AllowAccess, 0) = 0) OR (w.Ad = 1 AND ISNULL(r.AllowAdd, 0) = 0) OR (w.E = 1 AND ISNULL(r.AllowEdit, 0) = 0)
    OR (w.P = 1 AND ISNULL(r.AllowPrint, 0) = 0) OR (w.X = 1 AND ISNULL(r.AllowExport, 0) = 0));
SET @RoleOr = @@ROWCOUNT;

DECLARE @Grant TABLE (UserGroupId UNIQUEIDENTIFIER PRIMARY KEY, Code NVARCHAR(50));
INSERT INTO @Grant
SELECT g.Id, g.Code FROM dbo.UserGroup AS g JOIN @Want AS w ON w.Code = g.Code
WHERE ISNULL(g.IsDeleted, 0) = 0
  AND NOT EXISTS (SELECT 1 FROM dbo.UserRole AS r WHERE r.UserGroupId = g.Id AND r.ObjectMenuId = @Menu AND ISNULL(r.IsDeleted, 0) = 0);
INSERT INTO dbo.UserRole (Id, UserGroupId, ObjectMenuId, AllowAccess, AllowAdd, AllowEdit, AllowDelete, AllowPrint, AllowExport, AllowImport, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), x.UserGroupId, @Menu, w.A, w.Ad, w.E, 0, w.P, w.X, 0, @Now, @SystemUser, 0 FROM @Grant AS x JOIN @Want AS w ON w.Code = x.Code;
SET @RoleAdded = @@ROWCOUNT;
INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), x.UserGroupId, N'RoleSaveMany', N'UserGroup', N'- Định mức bao bì: cấp quyền (ACC-6)', @Now, @SystemUser, 0
FROM (SELECT UserGroupId FROM @Grant UNION SELECT UserGroupId FROM @Or) AS x;

SELECT @MenuAdded AS [Mục thêm], @Shifted AS [Mục lùi thứ tự], @RoleAdded AS [Dòng quyền thêm], @RoleOr AS [Dòng quyền bổ sung cờ],
       (SELECT STRING_AGG(Code, N', ') FROM (SELECT Code FROM @Grant UNION SELECT Code FROM @Or) AS c) AS [Nhóm được cấp],
       CASE WHEN @DryRun = 1 THEN N'CHẠY THỬ – đã hoàn tác' ELSE N'ĐÃ GHI' END AS [Kết quả];
IF @DryRun = 1 ROLLBACK TRANSACTION; ELSE COMMIT TRANSACTION;
