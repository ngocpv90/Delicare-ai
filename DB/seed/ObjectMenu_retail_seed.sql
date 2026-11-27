/*
  RETAIL-1 – SEED menu + quyền nhóm "Bán lẻ" (#/group/02c: #/sal-retail-order, #/sal-retail-product) – D-134: nằm trong deploy/db/scripts.order,
  mọi DB (máy anh, máy FE, server test, DB mới) có cùng menu sau Deploy-Database.ps1 / db-init.
  CHỈ THÊM phần chưa có: nhóm + 2 mục (theo MetaLink), dòng UserRole chưa có (theo nhóm × mục). KHÔNG sửa / thu / cộng cờ dòng quyền đã có
  (admin chỉnh trên màn ma trận quyền được giữ nguyên). Chạy lại = 0 việc. Bản chạy tay có DryRun + cộng cờ: DB/scripts/RETAIL1_menu.sql.
  Quyền đề xuất theo UserGroup.Code (DB chưa điền Code thì khớp Name) – PROPOSED – BA/PO CONFIRM: xem đầu DB/scripts/RETAIL1_menu.sql.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
DECLARE @DryRun BIT = 0;
DECLARE @SystemUser UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.[User] WHERE UserName = N'system' AND ISNULL(IsDeleted, 0) = 0);
IF @SystemUser IS NULL
    THROW 50101, N'Không có tài khoản "system" (seed T23) – không ghi được người thao tác.', 1;
DECLARE @Now DATETIME = DATEADD(HOUR, 7, SYSUTCDATETIME());

BEGIN TRANSACTION;
DECLARE @GroupAdded INT = 0, @ItemAdded INT = 0, @RoleAdded INT = 0, @RoleRaised INT = 0;
IF NOT EXISTS (SELECT 1 FROM dbo.ObjectMenu WHERE MetaLink = N'#/group/02c' AND ISNULL(IsDeleted, 0) = 0)
BEGIN
    INSERT INTO dbo.ObjectMenu (Id, Name, ShortName, ParentId, MetaLink, IconName, LevelShow, OrderShow, IsActive, IsDefault, CreatedDate, CreatedBy, IsDeleted)
    VALUES (NEWID(), N'Bán lẻ', N'retail', NULL, N'#/group/02c', N'cart', 1, 260, 1, 0, @Now, @SystemUser, 0);
    SET @GroupAdded = 1;
END;
DECLARE @Group UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.ObjectMenu WHERE MetaLink = N'#/group/02c' AND ISNULL(IsDeleted, 0) = 0);

DECLARE @Want TABLE (MetaLink NVARCHAR(200) PRIMARY KEY, Name NVARCHAR(200), OrderShow INT);
INSERT INTO @Want VALUES
    (N'#/sal-retail-order', N'Đơn khách lẻ', 261),
    (N'#/sal-retail-product', N'Sản phẩm bán lẻ', 262);
DECLARE @New TABLE (Id UNIQUEIDENTIFIER, MetaLink NVARCHAR(200));
INSERT INTO dbo.ObjectMenu (Id, ParentId, Name, ShortName, MetaLink, LevelShow, OrderShow, IsActive, CreatedDate, CreatedBy, IsDeleted)
OUTPUT inserted.Id, inserted.MetaLink INTO @New
SELECT NEWID(), @Group, w.Name, N'RETAIL', w.MetaLink, 2, w.OrderShow, 1, @Now, @SystemUser, 0
FROM @Want AS w WHERE NOT EXISTS (SELECT 1 FROM dbo.ObjectMenu AS m WHERE m.MetaLink = w.MetaLink AND ISNULL(m.IsDeleted, 0) = 0);
SET @ItemAdded = @@ROWCOUNT;
INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), n.Id, N'Added', N'ObjectMenu', N'- RETAIL-1: thêm mục "Bán lẻ › ' + w.Name + N'"', @Now, @SystemUser, 0 FROM @New AS n JOIN @Want AS w ON w.MetaLink = n.MetaLink;

-- Quyền: A (truy cập), Ad (thêm), E (sửa), D (xóa), P (in), X (xuất), I (nhập).
DECLARE @R TABLE (Code NVARCHAR(50), MetaLink NVARCHAR(200), A BIT, Ad BIT, E BIT, D BIT, P BIT, X BIT, I BIT);
INSERT INTO @R VALUES
    (N'SAL-HEAD', N'#/sal-retail-order', 1, 1, 1, 1, 1, 1, 1), (N'SAL-HEAD', N'#/sal-retail-product', 1, 1, 1, 1, 1, 1, 1),
    (N'SAL', N'#/sal-retail-order', 1, 1, 1, 0, 1, 1, 0), (N'SAL', N'#/sal-retail-product', 1, 0, 0, 0, 0, 0, 0),
    (N'SAL-SALES', N'#/sal-retail-order', 1, 1, 1, 0, 1, 1, 0), (N'SAL-SALES', N'#/sal-retail-product', 1, 0, 0, 0, 0, 0, 0),
    (N'SAL-CS', N'#/sal-retail-order', 1, 1, 1, 0, 1, 1, 0), (N'SAL-CS', N'#/sal-retail-product', 1, 0, 0, 0, 0, 0, 0),
    (N'OPS-POS', N'#/sal-retail-order', 1, 1, 1, 0, 1, 1, 0), (N'OPS-POS', N'#/sal-retail-product', 1, 0, 0, 0, 0, 0, 0),
    (N'OPS-LOG', N'#/sal-retail-order', 1, 0, 1, 0, 1, 0, 0),
    (N'OPS-KIT', N'#/sal-retail-order', 1, 0, 0, 0, 1, 0, 0), (N'OPS-PLAN', N'#/sal-retail-order', 1, 0, 0, 0, 1, 0, 0),
    (N'FIN-ACC', N'#/sal-retail-order', 1, 0, 0, 0, 1, 1, 0), (N'FIN-ACC', N'#/sal-retail-product', 1, 0, 0, 0, 1, 1, 0),
    (N'CEO', N'#/sal-retail-order', 1, 0, 0, 0, 1, 1, 0), (N'CEO', N'#/sal-retail-product', 1, 0, 0, 0, 1, 1, 0),
    (N'COO', N'#/sal-retail-order', 1, 0, 0, 0, 1, 1, 0), (N'COO', N'#/sal-retail-product', 1, 0, 0, 0, 1, 1, 0),
    (N'CEO-PMO', N'#/sal-retail-order', 1, 0, 0, 0, 0, 0, 0), (N'CEO-PMO', N'#/sal-retail-product', 1, 0, 0, 0, 0, 0, 0),
    (N'FIN-IT', N'#/sal-retail-order', 1, 0, 0, 0, 0, 0, 0), (N'FIN-IT', N'#/sal-retail-product', 1, 0, 0, 0, 0, 0, 0);
DECLARE @T TABLE (UserGroupId UNIQUEIDENTIFIER, ObjectMenuId UNIQUEIDENTIFIER, A BIT, Ad BIT, E BIT, D BIT, P BIT, X BIT, I BIT);
INSERT INTO @T
SELECT g.Id, m.Id, r.A, r.Ad, r.E, r.D, r.P, r.X, r.I
FROM @R AS r
-- Khớp UserGroup.Code (D-124); DB chưa điền Code (cột trống) thì khớp theo Name – tránh 0 dòng quyền như đã gặp trên DB dev 2026-11-27.
JOIN dbo.UserGroup AS g ON (g.Code = r.Code OR (NULLIF(LTRIM(RTRIM(g.Code)), N'') IS NULL AND g.Name = r.Code)) AND ISNULL(g.IsDeleted, 0) = 0
JOIN dbo.ObjectMenu AS m ON m.MetaLink = r.MetaLink AND ISNULL(m.IsDeleted, 0) = 0;

INSERT INTO dbo.UserRole (Id, UserGroupId, ObjectMenuId, AllowAccess, AllowAdd, AllowEdit, AllowDelete, AllowPrint, AllowExport, AllowImport, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), t.UserGroupId, t.ObjectMenuId, t.A, t.Ad, t.E, t.D, t.P, t.X, t.I, @Now, @SystemUser, 0 FROM @T AS t
WHERE NOT EXISTS (SELECT 1 FROM dbo.UserRole AS u WHERE u.UserGroupId = t.UserGroupId AND u.ObjectMenuId = t.ObjectMenuId AND ISNULL(u.IsDeleted, 0) = 0);
SET @RoleAdded = @@ROWCOUNT;

-- Seed chạy mỗi lần deploy: KHÔNG cộng cờ cho dòng quyền đã có (admin có thể đã chỉnh trên màn ma trận quyền – CLAUDE.md 4.3).

IF @RoleAdded + @RoleRaised > 0
    INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
    SELECT NEWID(), g.Id, N'RoleSaveMany', N'UserGroup', N'- RETAIL-1 (seed): thêm quyền nhóm Bán lẻ còn thiếu', @Now, @SystemUser, 0
    FROM dbo.UserGroup AS g WHERE g.Id IN (SELECT UserGroupId FROM @T) AND ISNULL(g.IsDeleted, 0) = 0;

SELECT @GroupAdded AS [Thêm nhóm], @ItemAdded AS [Thêm mục], @RoleAdded AS [Dòng quyền thêm], @RoleRaised AS [Dòng quyền cộng cờ],
       CASE WHEN @DryRun = 1 THEN N'CHẠY THỬ – đã hoàn tác' ELSE N'ĐÃ GHI' END AS [Kết quả];
IF @DryRun = 1 ROLLBACK TRANSACTION; ELSE COMMIT TRANSACTION;
