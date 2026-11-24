/*
  UI-6.27 (D-108 – anh chốt 2026-11-24): 3 màn Giá thành gộp thành 1 báo cáo "Giá vốn & lãi gộp" trong nhóm Điều hành.
  Script DỮ LIỆU một lần (không tạo / sửa bảng, cột; không DELETE):
    1. #/cost-meal: chuyển sang nhóm Điều hành (#/group/01), đứng sau "Chỉ số theo tháng", đổi tên "Giá vốn & lãi gộp" (giữ Id + UserRole – quyền đã cấp giữ nguyên).
    2. Ẩn #/cost-standard, #/cost-actual (IsActive = 0, giữ dòng + UserRole – quyền API theo cost-meal: ScreenMap.ParentByClass) và nhóm #/group/05b "Giá thành".
  Link cũ #/cost-standard, #/cost-actual mở màn mới tab Theo món. Ghi ModifiedDate / ModifiedBy (system) + AuditChange. Chạy lại lần 2 không đổi gì; UserRole không đổi.
  Bắt buộc truyền -v DryRun=1 (chạy thử) hoặc -v DryRun=0 (chạy thật). DB dev: chạy thật ngay trong task (D-92 a).
    sqlcmd -S <server> -d <db> -E -f 65001 -v DryRun=1 -i DB/scripts/UI627_cost_menu.sql -s "|" -W
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
DECLARE @DryRun BIT = $(DryRun);
DECLARE @SystemUser UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.[User] WHERE UserName = N'system' AND ISNULL(IsDeleted, 0) = 0);
IF @SystemUser IS NULL
    THROW 50101, N'Không có tài khoản "system" (seed T23) – không ghi được người thao tác.', 1;
DECLARE @Now DATETIME = DATEADD(HOUR, 7, SYSUTCDATETIME());
DECLARE @NewName NVARCHAR(200) = N'Giá vốn & lãi gộp';

BEGIN TRANSACTION;
DECLARE @RolesBefore INT = (SELECT COUNT(*) FROM dbo.UserRole);
DECLARE @G01 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.ObjectMenu WHERE MetaLink = N'#/group/01' AND ISNULL(IsDeleted, 0) = 0);
DECLARE @After INT = ISNULL((SELECT MAX(OrderShow) FROM dbo.ObjectMenu WHERE ParentId = @G01 AND ISNULL(IsDeleted, 0) = 0 AND MetaLink IN (N'#/dash-exec', N'#/kpi-daily', N'#/kpi-monthly')), 103) + 1;
DECLARE @Move TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY, OldName NVARCHAR(500), OldParent UNIQUEIDENTIFIER NULL, OldOrder INT NULL);
INSERT INTO @Move SELECT m.Id, m.Name, m.ParentId, m.OrderShow FROM dbo.ObjectMenu AS m
WHERE ISNULL(m.IsDeleted, 0) = 0 AND m.MetaLink = N'#/cost-meal' AND @G01 IS NOT NULL
  AND (ISNULL(m.ParentId, '00000000-0000-0000-0000-000000000000') <> @G01 OR m.Name IN (N'Giá vốn suất ăn', N'Giá thành mỗi suất & Margin'));
DECLARE @Hide TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY, MetaLink NVARCHAR(500), Name NVARCHAR(500));
INSERT INTO @Hide SELECT m.Id, m.MetaLink, m.Name FROM dbo.ObjectMenu AS m
WHERE ISNULL(m.IsDeleted, 0) = 0 AND m.IsActive = 1 AND m.MetaLink IN (N'#/cost-standard', N'#/cost-actual', N'#/group/05b');

SELECT N'#/cost-meal' AS [Mục menu], x.OldName AS [Tên], N'Chuyển nhóm Điều hành + đổi tên → ' + @NewName AS [Đổi] FROM @Move AS x
UNION ALL SELECT MetaLink, Name, N'Ẩn' FROM @Hide;

UPDATE m SET m.ParentId = @G01, m.OrderShow = @After,
    m.Name = CASE WHEN m.Name IN (N'Giá vốn suất ăn', N'Giá thành mỗi suất & Margin') THEN @NewName ELSE m.Name END,
    m.ModifiedDate = @Now, m.ModifiedBy = @SystemUser
FROM dbo.ObjectMenu AS m JOIN @Move AS x ON x.Id = m.Id;
UPDATE m SET m.IsActive = 0, m.ModifiedDate = @Now, m.ModifiedBy = @SystemUser FROM dbo.ObjectMenu AS m JOIN @Hide AS x ON x.Id = m.Id;
INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), x.Id, N'Modified', N'ObjectMenu', N'- ParentId / Name: nhóm Giá thành → Điều hành, "' + x.OldName + N'" => "' + @NewName + N'" (UI-6.27 D-108)', @Now, @SystemUser, 0 FROM @Move AS x
UNION ALL
SELECT NEWID(), x.Id, N'Modified', N'ObjectMenu', N'- IsActive: ''True'' => ''False'' (UI-6.27 D-108: gộp vào Giá vốn & lãi gộp, giữ quyền)', @Now, @SystemUser, 0 FROM @Hide AS x;

DECLARE @RolesAfter INT = (SELECT COUNT(*) FROM dbo.UserRole);
SELECT (SELECT COUNT(*) FROM @Move) AS [Chuyển / đổi tên], (SELECT COUNT(*) FROM @Hide) AS [Mục ẩn], @RolesBefore AS [UserRole trước], @RolesAfter AS [UserRole sau],
       CASE WHEN @DryRun = 1 THEN N'CHẠY THỬ – đã hoàn tác' ELSE N'ĐÃ GHI' END AS [Kết quả];
IF @RolesAfter <> @RolesBefore
    THROW 50102, N'Số dòng UserRole thay đổi – dừng.', 1;
IF @DryRun = 1 ROLLBACK TRANSACTION; ELSE COMMIT TRANSACTION;
