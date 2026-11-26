/*
  ACC-1 (D-131 b – anh chốt 2026-11-26): nhóm menu "Kế toán" (#/group/02b, thứ tự 250 – ngay sau Kinh doanh), 5 mục:
    Chứng từ kế toán (#/acc-voucher) · Bảng kê thanh toán (#/crm-billing – CHUYỂN từ Kinh doanh, giữ mã màn + quyền) · Khớp mã AMIS (#/acc-amis-code)
    · Đối chiếu với AMIS (#/acc-amis-recon) · Khóa kỳ (#/acc-period).
  Script DỮ LIỆU (không tạo / sửa bảng, cột; không DELETE; chạy lại = 0 việc):
    1. Thêm dòng nhóm + 4 mục mới (khi chưa có); chuyển Bảng kê sang nhóm mới.
    2. Quyền theo ma trận MENU-5 (tools/AppDelicare.CodeGen/Seeds.Menu5.cs – ACC-1): thêm dòng còn thiếu; dòng đã có chỉ được CỘNG cờ (OR) – không thu quyền nào.
       FIN-ACC Q (IX) cả 5 mục; SAL-HEAD Q (IX) Bảng kê; CEO / COO Xem (IX); CEO-PMO, FIN-IT Xem (toàn bộ màn nghiệp vụ). Kinh doanh khác giữ quyền Bảng kê đang có.
  Ghi CreatedBy / ModifiedBy (system) + AuditChange.
  -v DryRun=1 (thử) / 0 (thật). DB dev chạy thật (D-92 a).
    sqlcmd -S <server> -d <db> -E -b -f 65001 -v DryRun=1 -i DB/scripts/ACC1_menu.sql -s "|" -W
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
DECLARE @GroupAdded INT = 0, @ItemAdded INT = 0, @Moved INT = 0, @RoleAdded INT = 0, @RoleRaised INT = 0;
IF NOT EXISTS (SELECT 1 FROM dbo.ObjectMenu WHERE MetaLink = N'#/group/02b' AND ISNULL(IsDeleted, 0) = 0)
BEGIN
    INSERT INTO dbo.ObjectMenu (Id, Name, ShortName, ParentId, MetaLink, IconName, LevelShow, OrderShow, IsActive, IsDefault, CreatedDate, CreatedBy, IsDeleted)
    VALUES (NEWID(), N'Kế toán', N'acc', NULL, N'#/group/02b', N'calc', 1, 250, 1, 0, @Now, @SystemUser, 0);
    SET @GroupAdded = 1;
END;
DECLARE @Group UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.ObjectMenu WHERE MetaLink = N'#/group/02b' AND ISNULL(IsDeleted, 0) = 0);

DECLARE @Want TABLE (MetaLink NVARCHAR(200) PRIMARY KEY, Name NVARCHAR(200), OrderShow INT);
INSERT INTO @Want VALUES
    (N'#/acc-voucher', N'Chứng từ kế toán', 251),
    (N'#/crm-billing', N'Bảng kê thanh toán', 252),
    (N'#/acc-amis-code', N'Khớp mã AMIS', 253),
    (N'#/acc-amis-recon', N'Đối chiếu với AMIS', 254),
    (N'#/acc-period', N'Khóa kỳ', 255);
DECLARE @New TABLE (Id UNIQUEIDENTIFIER, MetaLink NVARCHAR(200));
INSERT INTO dbo.ObjectMenu (Id, ParentId, Name, ShortName, MetaLink, LevelShow, OrderShow, IsActive, CreatedDate, CreatedBy, IsDeleted)
OUTPUT inserted.Id, inserted.MetaLink INTO @New
SELECT NEWID(), @Group, w.Name, N'ACC', w.MetaLink, 2, w.OrderShow, 1, @Now, @SystemUser, 0
FROM @Want AS w WHERE NOT EXISTS (SELECT 1 FROM dbo.ObjectMenu AS m WHERE m.MetaLink = w.MetaLink AND ISNULL(m.IsDeleted, 0) = 0);
SET @ItemAdded = @@ROWCOUNT;
INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), n.Id, N'Added', N'ObjectMenu', N'- ACC-1 (D-131 b): thêm mục "Kế toán › ' + w.Name + N'"', @Now, @SystemUser, 0 FROM @New AS n JOIN @Want AS w ON w.MetaLink = n.MetaLink;

DECLARE @Move TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY, MetaLink NVARCHAR(200), NewOrder INT);
INSERT INTO @Move SELECT m.Id, m.MetaLink, w.OrderShow FROM dbo.ObjectMenu AS m JOIN @Want AS w ON w.MetaLink = m.MetaLink
WHERE ISNULL(m.IsDeleted, 0) = 0 AND (ISNULL(m.ParentId, '00000000-0000-0000-0000-000000000000') <> @Group OR ISNULL(m.OrderShow, 0) <> w.OrderShow);
UPDATE m SET m.ParentId = @Group, m.OrderShow = x.NewOrder, m.ModifiedDate = @Now, m.ModifiedBy = @SystemUser FROM dbo.ObjectMenu AS m JOIN @Move AS x ON x.Id = m.Id;
SET @Moved = @@ROWCOUNT;
INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), x.Id, N'Modified', N'ObjectMenu', N'- ACC-1 (D-131 b): ParentId => ''#/group/02b''; OrderShow => ''' + CAST(x.NewOrder AS NVARCHAR(10)) + N'''', @Now, @SystemUser, 0 FROM @Move AS x;

-- Quyền (nhóm theo UserGroup.Code – D-124): A, Ad, E, D, P, X, I.
DECLARE @R TABLE (Code NVARCHAR(50), MetaLink NVARCHAR(200), A BIT, Ad BIT, E BIT, D BIT, P BIT, X BIT, I BIT);
INSERT INTO @R VALUES
    (N'CEO', N'#/acc-voucher', 1, 0, 0, 0, 1, 1, 0), (N'CEO', N'#/crm-billing', 1, 0, 0, 0, 1, 1, 0), (N'CEO', N'#/acc-amis-code', 1, 0, 0, 0, 1, 1, 0),
    (N'CEO', N'#/acc-amis-recon', 1, 0, 0, 0, 1, 1, 0), (N'CEO', N'#/acc-period', 1, 0, 0, 0, 1, 1, 0),
    (N'CEO-PMO', N'#/acc-voucher', 1, 0, 0, 0, 0, 0, 0), (N'CEO-PMO', N'#/crm-billing', 1, 0, 0, 0, 0, 0, 0), (N'CEO-PMO', N'#/acc-amis-code', 1, 0, 0, 0, 0, 0, 0),
    (N'CEO-PMO', N'#/acc-amis-recon', 1, 0, 0, 0, 0, 0, 0), (N'CEO-PMO', N'#/acc-period', 1, 0, 0, 0, 0, 0, 0),
    (N'COO', N'#/acc-voucher', 1, 0, 0, 0, 1, 1, 0), (N'COO', N'#/crm-billing', 1, 0, 0, 0, 1, 1, 0), (N'COO', N'#/acc-amis-code', 1, 0, 0, 0, 1, 1, 0),
    (N'COO', N'#/acc-amis-recon', 1, 0, 0, 0, 1, 1, 0), (N'COO', N'#/acc-period', 1, 0, 0, 0, 1, 1, 0),
    (N'SAL-HEAD', N'#/crm-billing', 1, 1, 1, 1, 1, 1, 1),
    (N'FIN-ACC', N'#/acc-voucher', 1, 1, 1, 1, 1, 1, 1), (N'FIN-ACC', N'#/crm-billing', 1, 1, 1, 1, 1, 1, 1), (N'FIN-ACC', N'#/acc-amis-code', 1, 1, 1, 1, 1, 1, 1),
    (N'FIN-ACC', N'#/acc-amis-recon', 1, 1, 1, 1, 1, 1, 1), (N'FIN-ACC', N'#/acc-period', 1, 1, 1, 1, 1, 1, 1),
    (N'FIN-IT', N'#/acc-voucher', 1, 0, 0, 0, 0, 0, 0), (N'FIN-IT', N'#/crm-billing', 1, 0, 0, 0, 0, 0, 0), (N'FIN-IT', N'#/acc-amis-code', 1, 0, 0, 0, 0, 0, 0),
    (N'FIN-IT', N'#/acc-amis-recon', 1, 0, 0, 0, 0, 0, 0), (N'FIN-IT', N'#/acc-period', 1, 0, 0, 0, 0, 0, 0);
DECLARE @T TABLE (UserGroupId UNIQUEIDENTIFIER, ObjectMenuId UNIQUEIDENTIFIER, Code NVARCHAR(50), MetaLink NVARCHAR(200), A BIT, Ad BIT, E BIT, D BIT, P BIT, X BIT, I BIT);
INSERT INTO @T
SELECT g.Id, m.Id, r.Code, r.MetaLink, r.A, r.Ad, r.E, r.D, r.P, r.X, r.I
FROM @R AS r
JOIN dbo.UserGroup AS g ON g.Code = r.Code AND ISNULL(g.IsDeleted, 0) = 0
JOIN dbo.ObjectMenu AS m ON m.MetaLink = r.MetaLink AND ISNULL(m.IsDeleted, 0) = 0;

INSERT INTO dbo.UserRole (Id, UserGroupId, ObjectMenuId, AllowAccess, AllowAdd, AllowEdit, AllowDelete, AllowPrint, AllowExport, AllowImport, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), t.UserGroupId, t.ObjectMenuId, t.A, t.Ad, t.E, t.D, t.P, t.X, t.I, @Now, @SystemUser, 0 FROM @T AS t
WHERE NOT EXISTS (SELECT 1 FROM dbo.UserRole AS u WHERE u.UserGroupId = t.UserGroupId AND u.ObjectMenuId = t.ObjectMenuId AND ISNULL(u.IsDeleted, 0) = 0);
SET @RoleAdded = @@ROWCOUNT;

UPDATE u SET u.AllowAccess = CASE WHEN t.A = 1 THEN 1 ELSE u.AllowAccess END, u.AllowAdd = CASE WHEN t.Ad = 1 THEN 1 ELSE u.AllowAdd END,
             u.AllowEdit = CASE WHEN t.E = 1 THEN 1 ELSE u.AllowEdit END, u.AllowDelete = CASE WHEN t.D = 1 THEN 1 ELSE u.AllowDelete END,
             u.AllowPrint = CASE WHEN t.P = 1 THEN 1 ELSE u.AllowPrint END, u.AllowExport = CASE WHEN t.X = 1 THEN 1 ELSE u.AllowExport END,
             u.AllowImport = CASE WHEN t.I = 1 THEN 1 ELSE u.AllowImport END, u.ModifiedDate = @Now, u.ModifiedBy = @SystemUser
OUTPUT inserted.UserGroupId INTO @New (Id)
FROM dbo.UserRole AS u JOIN @T AS t ON t.UserGroupId = u.UserGroupId AND t.ObjectMenuId = u.ObjectMenuId
WHERE ISNULL(u.IsDeleted, 0) = 0
  AND ((t.A = 1 AND ISNULL(u.AllowAccess, 0) = 0) OR (t.Ad = 1 AND ISNULL(u.AllowAdd, 0) = 0) OR (t.E = 1 AND ISNULL(u.AllowEdit, 0) = 0) OR (t.D = 1 AND ISNULL(u.AllowDelete, 0) = 0)
    OR (t.P = 1 AND ISNULL(u.AllowPrint, 0) = 0) OR (t.X = 1 AND ISNULL(u.AllowExport, 0) = 0) OR (t.I = 1 AND ISNULL(u.AllowImport, 0) = 0));
SET @RoleRaised = @@ROWCOUNT;
IF @RoleAdded + @RoleRaised > 0
    INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
    SELECT NEWID(), g.Id, N'RoleSaveMany', N'UserGroup', N'- ACC-1 (D-131 b): quyền nhóm Kế toán theo ma trận MENU-5 (thêm / cộng cờ, không thu)', @Now, @SystemUser, 0
    FROM dbo.UserGroup AS g WHERE g.Id IN (SELECT UserGroupId FROM @T) AND ISNULL(g.IsDeleted, 0) = 0;

SELECT @GroupAdded AS [Thêm nhóm], @ItemAdded AS [Thêm mục], @Moved AS [Mục chuyển / sắp], @RoleAdded AS [Dòng quyền thêm], @RoleRaised AS [Dòng quyền cộng cờ],
       CASE WHEN @DryRun = 1 THEN N'CHẠY THỬ – đã hoàn tác' ELSE N'ĐÃ GHI' END AS [Kết quả];
IF @DryRun = 1 ROLLBACK TRANSACTION; ELSE COMMIT TRANSACTION;
