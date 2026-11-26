/*
  ACC-5 (D-131 i – anh chốt 2026-11-26): mục "Kho › Trả hàng NCC" (#/scm-supplier-return) – Thủ kho lập phiếu trả (lô tạm giữ),
  Mua hàng xác nhận NCC đồng ý / không nhận, Thủ kho ghi sổ xuất trả (sổ kho RETURN) → chứng từ "Trả lại hàng mua" (Kế toán).
  Script DỮ LIỆU (không tạo bảng / cột, không DELETE):
    1. Thêm ObjectMenu ngay sau "Phiếu nhận" (#/scm-gr) trong nhóm Kho (#/group/04b) nếu chưa có; các mục sau nó lùi 1 thứ tự.
    2. Cấp quyền (theo UserGroup.Code – D-124; đã có dòng thì chỉ OR thêm cờ, không bớt):
         OPS-WH, OPS-SCM-HEAD : Truy cập + Thêm + Sửa + In + Xuất
         OPS-SCM              : Truy cập + Sửa (xác nhận NCC)
         FIN-ACC              : Truy cập + In + Xuất
         CEO, CEO-PMO, COO, FIN-IT : Truy cập
  Mục mới – không ẩn mục nào → luật UI-6.44 không ai mất quyền xem tự thỏa. Ghi CreatedBy (system) + AuditChange. Chạy lại = 0 việc.
  -v DryRun=1 (thử) / 0 (thật). DB dev chạy thật (D-92 a).
    sqlcmd -S <server> -d <db> -E -b -f 65001 -v DryRun=1 -i DB/scripts/ACC5_supplier_return_menu.sql -s "|" -W
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
DECLARE @DryRun BIT = $(DryRun);
DECLARE @SystemUser UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.[User] WHERE UserName = N'system' AND ISNULL(IsDeleted, 0) = 0);
IF @SystemUser IS NULL
    THROW 50101, N'Không có tài khoản "system" (seed T23) – không ghi được người thao tác.', 1;
DECLARE @Now DATETIME = DATEADD(HOUR, 7, SYSUTCDATETIME());
DECLARE @Link NVARCHAR(200) = N'#/scm-supplier-return';

BEGIN TRANSACTION;
DECLARE @Group UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.ObjectMenu WHERE MetaLink = N'#/group/04b' AND ISNULL(IsDeleted, 0) = 0);
IF @Group IS NULL
    THROW 50104, N'Không thấy nhóm menu Kho (#/group/04b) – chạy D79_menu_regroup.sql trước.', 1;
DECLARE @Menu UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.ObjectMenu WHERE MetaLink = @Link AND ISNULL(IsDeleted, 0) = 0);
DECLARE @MenuAdded INT = 0, @Shifted INT = 0, @RoleAdded INT = 0, @RoleOr INT = 0;
IF @Menu IS NULL
BEGIN
    DECLARE @After INT = ISNULL((SELECT TOP 1 OrderShow FROM dbo.ObjectMenu WHERE MetaLink = N'#/scm-gr' AND ParentId = @Group AND ISNULL(IsDeleted, 0) = 0),
                                (SELECT MAX(OrderShow) FROM dbo.ObjectMenu WHERE ParentId = @Group AND ISNULL(IsDeleted, 0) = 0));
    UPDATE dbo.ObjectMenu SET OrderShow = OrderShow + 1, ModifiedDate = @Now, ModifiedBy = @SystemUser
    WHERE ParentId = @Group AND ISNULL(IsDeleted, 0) = 0 AND OrderShow > ISNULL(@After, 0);
    SET @Shifted = @@ROWCOUNT;
    SET @Menu = NEWID();
    INSERT INTO dbo.ObjectMenu (Id, ParentId, Name, ShortName, MetaLink, LevelShow, OrderShow, IsActive, CreatedDate, CreatedBy, IsDeleted)
    VALUES (@Menu, @Group, N'Trả hàng NCC', N'ACC', @Link, 2, ISNULL(@After, 0) + 1, 1, @Now, @SystemUser, 0);
    SET @MenuAdded = 1;
    INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
    VALUES (NEWID(), @Menu, N'Added', N'ObjectMenu', N'- ACC-5 (D-131 i): thêm mục "Kho › Trả hàng NCC"', @Now, @SystemUser, 0);
END

DECLARE @Want TABLE (Code NVARCHAR(50) PRIMARY KEY, A BIT, Ad BIT, E BIT, P BIT, X BIT);
INSERT INTO @Want VALUES (N'OPS-WH', 1, 1, 1, 1, 1), (N'OPS-SCM-HEAD', 1, 1, 1, 1, 1), (N'OPS-SCM', 1, 0, 1, 0, 0), (N'FIN-ACC', 1, 0, 0, 1, 1),
                         (N'CEO', 1, 0, 0, 0, 0), (N'CEO-PMO', 1, 0, 0, 0, 0), (N'COO', 1, 0, 0, 0, 0), (N'FIN-IT', 1, 0, 0, 0, 0);

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
SELECT NEWID(), x.UserGroupId, N'RoleSaveMany', N'UserGroup', N'- Trả hàng NCC: cấp quyền (ACC-5)', @Now, @SystemUser, 0
FROM (SELECT UserGroupId FROM @Grant UNION SELECT UserGroupId FROM @Or) AS x;

SELECT @MenuAdded AS [Mục thêm], @Shifted AS [Mục lùi thứ tự], @RoleAdded AS [Dòng quyền thêm], @RoleOr AS [Dòng quyền bổ sung cờ],
       (SELECT STRING_AGG(Code, N', ') FROM (SELECT Code FROM @Grant UNION SELECT Code FROM @Or) AS c) AS [Nhóm được cấp],
       CASE WHEN @DryRun = 1 THEN N'CHẠY THỬ – đã hoàn tác' ELSE N'ĐÃ GHI' END AS [Kết quả];
IF @DryRun = 1 ROLLBACK TRANSACTION; ELSE COMMIT TRANSACTION;
