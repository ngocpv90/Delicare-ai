/*
  RETAIL-1 (anh chốt 2026-11-26 – module Bán lẻ tách riêng đơn doanh nghiệp): nhóm menu "Bán lẻ" (#/group/02c, thứ tự 260 – sau Kế toán), 2 mục:
    Đơn khách lẻ (#/sal-retail-order) · Sản phẩm bán lẻ (#/sal-retail-product). Giao diện: app-delicare-client/src/assets/delicare/js/modules/retail.js.
  Script DỮ LIỆU (không tạo / sửa bảng, cột; không DELETE; chạy lại = 0 việc). Bảng RetailOrder… đi bằng migration DB/proposed/20261127_01_retail_order.sql (chuyển vào DB/migrations cùng backend). Chạy script này SAU khi có bảng + API.
    1. Thêm dòng nhóm + 2 mục (khi chưa có).
    2. Quyền (theo UserGroup.Code – D-124; đã có dòng thì chỉ CỘNG cờ (OR), không thu quyền nào) – PROPOSED – BA/PO CONFIRM:
         SAL-HEAD          : cả 2 mục – toàn quyền
         SAL, SAL-SALES, SAL-CS, OPS-POS : Đơn khách lẻ – Truy cập + Thêm + Sửa + In + Xuất; Sản phẩm – Truy cập
         OPS-LOG           : Đơn khách lẻ – Truy cập + Sửa + In (giao hàng: Đang giao / Hoàn thành)
         OPS-KIT, OPS-PLAN : Đơn khách lẻ – Truy cập + In (tổng hợp cho bếp)
         FIN-ACC           : cả 2 mục – Truy cập + In + Xuất (doanh thu bán lẻ)
         CEO, COO          : cả 2 mục – Truy cập + In + Xuất
         CEO-PMO, FIN-IT   : cả 2 mục – Truy cập
  Ghi CreatedBy / ModifiedBy (system) + AuditChange.
  -v DryRun=1 (thử) / 0 (thật). DB dev chạy thật (D-92 a).
    sqlcmd -S <server> -d <db> -E -b -f 65001 -v DryRun=1 -i DB/scripts/RETAIL1_menu.sql -s "|" -W
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

DECLARE @Raised TABLE (UserGroupId UNIQUEIDENTIFIER);
UPDATE u SET u.AllowAccess = CASE WHEN t.A = 1 THEN 1 ELSE u.AllowAccess END, u.AllowAdd = CASE WHEN t.Ad = 1 THEN 1 ELSE u.AllowAdd END,
             u.AllowEdit = CASE WHEN t.E = 1 THEN 1 ELSE u.AllowEdit END, u.AllowDelete = CASE WHEN t.D = 1 THEN 1 ELSE u.AllowDelete END,
             u.AllowPrint = CASE WHEN t.P = 1 THEN 1 ELSE u.AllowPrint END, u.AllowExport = CASE WHEN t.X = 1 THEN 1 ELSE u.AllowExport END,
             u.AllowImport = CASE WHEN t.I = 1 THEN 1 ELSE u.AllowImport END, u.ModifiedDate = @Now, u.ModifiedBy = @SystemUser
OUTPUT inserted.UserGroupId INTO @Raised (UserGroupId)
FROM dbo.UserRole AS u JOIN @T AS t ON t.UserGroupId = u.UserGroupId AND t.ObjectMenuId = u.ObjectMenuId
WHERE ISNULL(u.IsDeleted, 0) = 0
  AND ((t.A = 1 AND ISNULL(u.AllowAccess, 0) = 0) OR (t.Ad = 1 AND ISNULL(u.AllowAdd, 0) = 0) OR (t.E = 1 AND ISNULL(u.AllowEdit, 0) = 0) OR (t.D = 1 AND ISNULL(u.AllowDelete, 0) = 0)
    OR (t.P = 1 AND ISNULL(u.AllowPrint, 0) = 0) OR (t.X = 1 AND ISNULL(u.AllowExport, 0) = 0) OR (t.I = 1 AND ISNULL(u.AllowImport, 0) = 0));
SET @RoleRaised = @@ROWCOUNT;
IF @RoleAdded + @RoleRaised > 0
    INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
    SELECT NEWID(), g.Id, N'RoleSaveMany', N'UserGroup', N'- RETAIL-1: quyền nhóm Bán lẻ (thêm / cộng cờ, không thu)', @Now, @SystemUser, 0
    FROM dbo.UserGroup AS g WHERE g.Id IN (SELECT UserGroupId FROM @T) AND ISNULL(g.IsDeleted, 0) = 0;

SELECT @GroupAdded AS [Thêm nhóm], @ItemAdded AS [Thêm mục], @RoleAdded AS [Dòng quyền thêm], @RoleRaised AS [Dòng quyền cộng cờ],
       CASE WHEN @DryRun = 1 THEN N'CHẠY THỬ – đã hoàn tác' ELSE N'ĐÃ GHI' END AS [Kết quả];
IF @DryRun = 1 ROLLBACK TRANSACTION; ELSE COMMIT TRANSACTION;
