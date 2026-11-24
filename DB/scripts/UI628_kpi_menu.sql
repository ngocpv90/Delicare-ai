/*
  UI-6.28 (D-109 – anh chốt 2026-11-24): "Chỉ số theo ngày" + "Chỉ số theo tháng" gộp thành "Chỉ số kinh doanh" (#/kpi-daily, công tắc Theo ngày | Theo tháng);
  nhóm Điều hành: Bảng điều hành · Chỉ số kinh doanh · Giá vốn & lãi gộp · Phê duyệt (Phê duyệt cuối – mục FE, vẫn hiện số việc chờ).
  Script DỮ LIỆU một lần (không tạo / sửa bảng, cột; không DELETE):
    1. Đổi tên #/kpi-daily → "Chỉ số kinh doanh" (khi tên đang là tên cũ). 2. Thứ tự OrderShow nhóm 01: dash-exec 101, kpi-daily 102, cost-meal 103, kpi-monthly 104.
    3. Ẩn #/kpi-monthly (IsActive = 0, giữ dòng + UserRole). 4. Quyền gộp: nhóm có Xem #/kpi-monthly mà chưa có dòng #/kpi-daily → thêm dòng Xem (+ In / Xuất như cũ) cho #/kpi-daily.
  Link cũ #/kpi-monthly mở #/kpi-daily?view=month. Ghi ModifiedDate / ModifiedBy (system) + AuditChange. Chạy lại lần 2 = 0 việc.
  -v DryRun=1 (thử) / 0 (thật). DB dev chạy thật (D-92 a).
    sqlcmd -S <server> -d <db> -E -f 65001 -v DryRun=1 -i DB/scripts/UI628_kpi_menu.sql -s "|" -W
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
DECLARE @DryRun BIT = $(DryRun);
DECLARE @SystemUser UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.[User] WHERE UserName = N'system' AND ISNULL(IsDeleted, 0) = 0);
IF @SystemUser IS NULL
    THROW 50101, N'Không có tài khoản "system" (seed T23) – không ghi được người thao tác.', 1;
DECLARE @Now DATETIME = DATEADD(HOUR, 7, SYSUTCDATETIME());
DECLARE @NewName NVARCHAR(200) = N'Chỉ số kinh doanh';

BEGIN TRANSACTION;
DECLARE @Daily UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.ObjectMenu WHERE MetaLink = N'#/kpi-daily' AND ISNULL(IsDeleted, 0) = 0);
DECLARE @Monthly UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.ObjectMenu WHERE MetaLink = N'#/kpi-monthly' AND ISNULL(IsDeleted, 0) = 0);
DECLARE @Order TABLE (MetaLink NVARCHAR(200) PRIMARY KEY, Ord INT);
INSERT INTO @Order VALUES (N'#/dash-exec', 101), (N'#/kpi-daily', 102), (N'#/cost-meal', 103), (N'#/kpi-monthly', 104);
DECLARE @Change TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY, MetaLink NVARCHAR(200), OldName NVARCHAR(500), OldOrd INT, NewOrd INT, Rename BIT, Hide BIT);
INSERT INTO @Change
SELECT m.Id, m.MetaLink, m.Name, m.OrderShow, o.Ord,
       CASE WHEN m.MetaLink = N'#/kpi-daily' AND m.Name IN (N'Chỉ số theo ngày', N'KPI ngày') THEN 1 ELSE 0 END,
       CASE WHEN m.MetaLink = N'#/kpi-monthly' AND m.IsActive = 1 THEN 1 ELSE 0 END
FROM dbo.ObjectMenu AS m JOIN @Order AS o ON o.MetaLink = m.MetaLink
WHERE ISNULL(m.IsDeleted, 0) = 0
  AND (ISNULL(m.OrderShow, -1) <> o.Ord OR (m.MetaLink = N'#/kpi-daily' AND m.Name IN (N'Chỉ số theo ngày', N'KPI ngày')) OR (m.MetaLink = N'#/kpi-monthly' AND m.IsActive = 1));
DECLARE @Grant TABLE (UserGroupId UNIQUEIDENTIFIER PRIMARY KEY, AllowPrint BIT, AllowExport BIT);
INSERT INTO @Grant SELECT r.UserGroupId, MAX(CAST(r.AllowPrint AS INT)), MAX(CAST(r.AllowExport AS INT)) FROM dbo.UserRole AS r
WHERE r.ObjectMenuId = @Monthly AND ISNULL(r.IsDeleted, 0) = 0 AND r.AllowAccess = 1 AND @Daily IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.UserRole AS d WHERE d.ObjectMenuId = @Daily AND d.UserGroupId = r.UserGroupId AND ISNULL(d.IsDeleted, 0) = 0 AND d.AllowAccess = 1)
GROUP BY r.UserGroupId;

SELECT c.MetaLink AS [Mục menu], c.OldName AS [Tên], CONCAT(CASE WHEN c.Rename = 1 THEN N'Đổi tên → ' + @NewName + N'; ' ELSE N'' END, CASE WHEN c.Hide = 1 THEN N'Ẩn; ' ELSE N'' END,
       CASE WHEN ISNULL(c.OldOrd, -1) <> c.NewOrd THEN N'Thứ tự ' + CAST(ISNULL(c.OldOrd, 0) AS NVARCHAR(10)) + N' → ' + CAST(c.NewOrd AS NVARCHAR(10)) ELSE N'' END) AS [Đổi]
FROM @Change AS c
UNION ALL SELECT N'#/kpi-daily', g.Name, N'Cấp quyền Xem (có quyền Chỉ số theo tháng)' FROM @Grant AS x JOIN dbo.UserGroup AS g ON g.Id = x.UserGroupId;

UPDATE m SET m.OrderShow = c.NewOrd, m.Name = CASE WHEN c.Rename = 1 THEN @NewName ELSE m.Name END, m.IsActive = CASE WHEN c.Hide = 1 THEN 0 ELSE m.IsActive END,
    m.ModifiedDate = @Now, m.ModifiedBy = @SystemUser
FROM dbo.ObjectMenu AS m JOIN @Change AS c ON c.Id = m.Id;
INSERT INTO dbo.UserRole (Id, UserGroupId, ObjectMenuId, AllowAccess, AllowAdd, AllowEdit, AllowDelete, AllowPrint, AllowExport, AllowImport, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), x.UserGroupId, @Daily, 1, 0, 0, 0, x.AllowPrint, x.AllowExport, 0, @Now, @SystemUser, 0 FROM @Grant AS x;
INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), c.Id, N'Modified', N'ObjectMenu', N'- UI-6.28 (D-109): ' + CASE WHEN c.Rename = 1 THEN N'Name => ''' + @NewName + N'''; ' ELSE N'' END + CASE WHEN c.Hide = 1 THEN N'IsActive => False; ' ELSE N'' END
       + N'OrderShow => ' + CAST(c.NewOrd AS NVARCHAR(10)), @Now, @SystemUser, 0 FROM @Change AS c;

SELECT (SELECT COUNT(*) FROM @Change) AS [Mục đổi], (SELECT COUNT(*) FROM @Grant) AS [Nhóm được cấp Xem], CASE WHEN @DryRun = 1 THEN N'CHẠY THỬ – đã hoàn tác' ELSE N'ĐÃ GHI' END AS [Kết quả];
IF @DryRun = 1 ROLLBACK TRANSACTION; ELSE COMMIT TRANSACTION;
