/*
  UI-6.40 (D-121 – anh chốt 2026-11-24): nhóm menu "Danh mục hệ thống" (#/group/12, thứ tự 950 – ngay trên Quản trị hệ thống).
  Script DỮ LIỆU một lần (không tạo / sửa bảng, cột; không DELETE; KHÔNG đụng UserRole × ObjectMenu – quyền từng mục giữ nguyên):
    1. Thêm dòng nhóm #/group/12 (khi chưa có).
    2. Chuyển 10 mục (+ 5 mục ẩn đi theo màn cha) sang nhóm mới đúng thứ tự: Công ty & đơn vị · Chức vụ · Ca làm việc · Đơn vị tính · Kho & vị trí ·
       Dây chuyền & bàn nấu · Xe giao hàng · Danh mục dùng chung · Địa giới hành chính · Danh mục chỉ số. Giữ mã màn, link, quyền.
  Nhóm chỉ hiện khi người dùng có quyền Xem ít nhất một mục (sidebar lọc theo quyền như mọi nhóm). Ghi ModifiedDate / ModifiedBy (system) + AuditChange. Chạy lại = 0 việc.
  -v DryRun=1 (thử) / 0 (thật). DB dev chạy thật (D-92 a).
    sqlcmd -S <server> -d <db> -E -b -f 65001 -v DryRun=1 -i DB/scripts/UI640_catalog_menu.sql -s "|" -W
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
DECLARE @DryRun BIT = $(DryRun);
DECLARE @SystemUser UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.[User] WHERE UserName = N'system' AND ISNULL(IsDeleted, 0) = 0);
IF @SystemUser IS NULL
    THROW 50101, N'Không có tài khoản "system" (seed T23) – không ghi được người thao tác.', 1;
DECLARE @Now DATETIME = DATEADD(HOUR, 7, SYSUTCDATETIME());
DECLARE @RolesBefore INT = (SELECT COUNT(*) FROM dbo.UserRole);

BEGIN TRANSACTION;
DECLARE @Added INT = 0;
IF NOT EXISTS (SELECT 1 FROM dbo.ObjectMenu WHERE MetaLink = N'#/group/12' AND ISNULL(IsDeleted, 0) = 0)
BEGIN
    INSERT INTO dbo.ObjectMenu (Id, Name, ShortName, ParentId, MetaLink, IconName, LevelShow, OrderShow, IsActive, IsDefault, CreatedDate, CreatedBy, IsDeleted)
    VALUES (NEWID(), N'Danh mục hệ thống', N'catalog', NULL, N'#/group/12', N'catalog', 1, 950, 1, 0, @Now, @SystemUser, 0);
    SET @Added = 1;
END;
DECLARE @Group UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.ObjectMenu WHERE MetaLink = N'#/group/12' AND ISNULL(IsDeleted, 0) = 0);
DECLARE @Want TABLE (MetaLink NVARCHAR(200) PRIMARY KEY, OrderShow INT);
INSERT INTO @Want VALUES
    (N'#/adm-orgunit', 951),
    (N'#/adm-company', 952),
    (N'#/adm-position', 953),
    (N'#/hr-shift', 954),
    (N'#/scm-uom', 955),
    (N'#/scm-uom-conversion', 956),
    (N'#/scm-warehouse', 957),
    (N'#/scm-zone', 958),
    (N'#/scm-location', 959),
    (N'#/prod-line', 960),
    (N'#/prod-workstation', 961),
    (N'#/log-vehicle', 962),
    (N'#/adm-reference', 963),
    (N'#/adm-admin-unit', 964),
    (N'#/kpi-definition', 965);
DECLARE @Move TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY, MetaLink NVARCHAR(200), OldParent NVARCHAR(200), OldOrder INT, NewOrder INT);
INSERT INTO @Move
SELECT m.Id, m.MetaLink, p.MetaLink, m.OrderShow, w.OrderShow
FROM dbo.ObjectMenu AS m JOIN @Want AS w ON w.MetaLink = m.MetaLink
LEFT JOIN dbo.ObjectMenu AS p ON p.Id = m.ParentId
WHERE ISNULL(m.IsDeleted, 0) = 0 AND (ISNULL(m.ParentId, '00000000-0000-0000-0000-000000000000') <> ISNULL(@Group, '00000000-0000-0000-0000-000000000000') OR ISNULL(m.OrderShow, 0) <> w.OrderShow);

SELECT x.MetaLink AS [Mục], ISNULL(x.OldParent, N'(không)') AS [Nhóm cũ], CONCAT(x.OldOrder, N' → ', x.NewOrder) AS [Thứ tự] FROM @Move AS x ORDER BY x.NewOrder;

UPDATE m SET m.ParentId = @Group, m.OrderShow = x.NewOrder, m.ModifiedDate = @Now, m.ModifiedBy = @SystemUser
FROM dbo.ObjectMenu AS m JOIN @Move AS x ON x.Id = m.Id;
INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), x.Id, N'Modified', N'ObjectMenu', N'- UI-6.40 (D-121): ParentId => ''#/group/12''; OrderShow => ''' + CAST(x.NewOrder AS NVARCHAR(10)) + N'''', @Now, @SystemUser, 0 FROM @Move AS x;

IF (SELECT COUNT(*) FROM dbo.UserRole) <> @RolesBefore
    THROW 50102, N'Số dòng UserRole thay đổi – dừng (script không được đụng quyền).', 1;
SELECT @Added AS [Thêm nhóm], (SELECT COUNT(*) FROM @Move) AS [Mục chuyển], CASE WHEN @DryRun = 1 THEN N'CHẠY THỬ – đã hoàn tác' ELSE N'ĐÃ GHI' END AS [Kết quả];
IF @DryRun = 1 ROLLBACK TRANSACTION; ELSE COMMIT TRANSACTION;
