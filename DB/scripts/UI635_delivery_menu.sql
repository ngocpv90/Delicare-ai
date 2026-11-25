/*
  UI-6.35 (D-116 – anh chốt 2026-11-24): "Theo dõi giao hàng" (#/log-execution, đổi tên từ "Theo dõi chuyến giao") + "Khay hộp theo khách".
  Script DỮ LIỆU một lần (không tạo / sửa bảng, cột; không DELETE):
    1. Đổi tên #/log-execution (khi đang là tên cũ).
    2. Ẩn (IsActive = 0, giữ dòng + UserRole làm quyền): Vị trí xe, Xác nhận đã giao → Theo dõi giao hàng; Thu hồi khay hộp → Khay hộp theo khách.
    3. Hợp quyền mục ẩn vào mục cha (thêm dòng / bật thêm cờ; không tắt cờ nào).
  Link cũ mở đúng màn / tab (FE core/app.js). Ghi ModifiedDate / ModifiedBy (system) + AuditChange. Chạy lại lần 2 = 0 việc.
  -v DryRun=1 (thử) / 0 (thật). DB dev chạy thật (D-92 a).
    sqlcmd -S <server> -d <db> -E -b -f 65001 -v DryRun=1 -i DB/scripts/UI635_delivery_menu.sql -s "|" -W
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
DECLARE @Rename TABLE (MetaLink NVARCHAR(200) PRIMARY KEY, NewName NVARCHAR(200), OldNames NVARCHAR(1000));
INSERT INTO @Rename VALUES
    (N'#/log-execution', N'Theo dõi giao hàng', N'|Theo dõi chuyến giao|Theo dõi chuyến|');
DECLARE @Merge TABLE (Child NVARCHAR(200) PRIMARY KEY, Parent NVARCHAR(200));
INSERT INTO @Merge VALUES (N'#/log-gps', N'#/log-execution'), (N'#/log-pod', N'#/log-execution'), (N'#/log-container-return', N'#/log-packaging-ledger');

DECLARE @Change TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY, MetaLink NVARCHAR(200), OldName NVARCHAR(500), NewName NVARCHAR(200) NULL, Hide BIT);
INSERT INTO @Change
SELECT m.Id, m.MetaLink, m.Name,
       CASE WHEN r.MetaLink IS NOT NULL AND CHARINDEX(N'|' + m.Name + N'|', r.OldNames) > 0 THEN r.NewName END,
       CASE WHEN g.Child IS NOT NULL AND m.IsActive = 1 THEN 1 ELSE 0 END
FROM dbo.ObjectMenu AS m
LEFT JOIN @Rename AS r ON r.MetaLink = m.MetaLink
LEFT JOIN @Merge AS g ON g.Child = m.MetaLink
WHERE ISNULL(m.IsDeleted, 0) = 0
  AND ((r.MetaLink IS NOT NULL AND CHARINDEX(N'|' + m.Name + N'|', r.OldNames) > 0) OR (g.Child IS NOT NULL AND m.IsActive = 1));

-- Quyền hợp nhất theo nhóm × màn cha (OR các cờ của mục ẩn).
DECLARE @Want TABLE (UserGroupId UNIQUEIDENTIFIER, ParentId UNIQUEIDENTIFIER, A BIT, Ad BIT, E BIT, D BIT, P BIT, X BIT, I BIT, PRIMARY KEY (UserGroupId, ParentId));
INSERT INTO @Want
SELECT r.UserGroupId, pm.Id, MAX(CAST(r.AllowAccess AS INT)), MAX(CAST(ISNULL(r.AllowAdd, 0) AS INT)), MAX(CAST(ISNULL(r.AllowEdit, 0) AS INT)), MAX(CAST(ISNULL(r.AllowDelete, 0) AS INT)),
       MAX(CAST(ISNULL(r.AllowPrint, 0) AS INT)), MAX(CAST(ISNULL(r.AllowExport, 0) AS INT)), MAX(CAST(ISNULL(r.AllowImport, 0) AS INT))
FROM dbo.UserRole AS r
JOIN dbo.ObjectMenu AS cm ON cm.Id = r.ObjectMenuId AND ISNULL(cm.IsDeleted, 0) = 0
JOIN @Merge AS g ON g.Child = cm.MetaLink
JOIN dbo.ObjectMenu AS pm ON pm.MetaLink = g.Parent AND ISNULL(pm.IsDeleted, 0) = 0
WHERE ISNULL(r.IsDeleted, 0) = 0 AND r.AllowAccess = 1
GROUP BY r.UserGroupId, pm.Id;
DECLARE @Grant TABLE (UserGroupId UNIQUEIDENTIFIER, ParentId UNIQUEIDENTIFIER, RoleId UNIQUEIDENTIFIER NULL, A BIT, Ad BIT, E BIT, D BIT, P BIT, X BIT, I BIT, PRIMARY KEY (UserGroupId, ParentId));
INSERT INTO @Grant
SELECT w.UserGroupId, w.ParentId, cur.Id,
       CASE WHEN w.A = 1 OR ISNULL(cur.AllowAccess, 0) = 1 THEN 1 ELSE 0 END, CASE WHEN w.Ad = 1 OR ISNULL(cur.AllowAdd, 0) = 1 THEN 1 ELSE 0 END,
       CASE WHEN w.E = 1 OR ISNULL(cur.AllowEdit, 0) = 1 THEN 1 ELSE 0 END, CASE WHEN w.D = 1 OR ISNULL(cur.AllowDelete, 0) = 1 THEN 1 ELSE 0 END,
       CASE WHEN w.P = 1 OR ISNULL(cur.AllowPrint, 0) = 1 THEN 1 ELSE 0 END, CASE WHEN w.X = 1 OR ISNULL(cur.AllowExport, 0) = 1 THEN 1 ELSE 0 END,
       CASE WHEN w.I = 1 OR ISNULL(cur.AllowImport, 0) = 1 THEN 1 ELSE 0 END
FROM @Want AS w
OUTER APPLY (SELECT TOP 1 u.* FROM dbo.UserRole AS u WHERE u.UserGroupId = w.UserGroupId AND u.ObjectMenuId = w.ParentId AND ISNULL(u.IsDeleted, 0) = 0 ORDER BY u.AllowAccess DESC, u.CreatedDate) AS cur
WHERE cur.Id IS NULL
   OR (w.A = 1 AND ISNULL(cur.AllowAccess, 0) = 0) OR (w.Ad = 1 AND ISNULL(cur.AllowAdd, 0) = 0) OR (w.E = 1 AND ISNULL(cur.AllowEdit, 0) = 0) OR (w.D = 1 AND ISNULL(cur.AllowDelete, 0) = 0)
   OR (w.P = 1 AND ISNULL(cur.AllowPrint, 0) = 0) OR (w.X = 1 AND ISNULL(cur.AllowExport, 0) = 0) OR (w.I = 1 AND ISNULL(cur.AllowImport, 0) = 0);

SELECT c.MetaLink AS [Mục menu], c.OldName AS [Tên], CONCAT(CASE WHEN c.NewName IS NOT NULL THEN N'Đổi tên → ' + c.NewName + N'; ' ELSE N'' END, CASE WHEN c.Hide = 1 THEN N'Ẩn' ELSE N'' END) AS [Đổi]
FROM @Change AS c
UNION ALL
SELECT pm.MetaLink, ug.Name, CONCAT(CASE WHEN x.RoleId IS NULL THEN N'Thêm dòng quyền ' ELSE N'Bật thêm quyền ' END,
       CONCAT(CAST(x.A AS CHAR(1)), CAST(x.Ad AS CHAR(1)), CAST(x.E AS CHAR(1)), CAST(x.D AS CHAR(1)), CAST(x.P AS CHAR(1)), CAST(x.X AS CHAR(1)), CAST(x.I AS CHAR(1))))
FROM @Grant AS x JOIN dbo.ObjectMenu AS pm ON pm.Id = x.ParentId JOIN dbo.UserGroup AS ug ON ug.Id = x.UserGroupId;

UPDATE m SET m.Name = ISNULL(c.NewName, m.Name), m.IsActive = CASE WHEN c.Hide = 1 THEN 0 ELSE m.IsActive END, m.ModifiedDate = @Now, m.ModifiedBy = @SystemUser
FROM dbo.ObjectMenu AS m JOIN @Change AS c ON c.Id = m.Id;
UPDATE u SET u.AllowAccess = x.A, u.AllowAdd = x.Ad, u.AllowEdit = x.E, u.AllowDelete = x.D, u.AllowPrint = x.P, u.AllowExport = x.X, u.AllowImport = x.I, u.ModifiedDate = @Now, u.ModifiedBy = @SystemUser
FROM dbo.UserRole AS u JOIN @Grant AS x ON x.RoleId = u.Id;
INSERT INTO dbo.UserRole (Id, UserGroupId, ObjectMenuId, AllowAccess, AllowAdd, AllowEdit, AllowDelete, AllowPrint, AllowExport, AllowImport, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), x.UserGroupId, x.ParentId, x.A, x.Ad, x.E, x.D, x.P, x.X, x.I, @Now, @SystemUser, 0 FROM @Grant AS x WHERE x.RoleId IS NULL;
INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), c.Id, N'Modified', N'ObjectMenu', N'- UI-6.35 (D-116): ' + CASE WHEN c.NewName IS NOT NULL THEN N'Name => ''' + c.NewName + N'''; ' ELSE N'' END + CASE WHEN c.Hide = 1 THEN N'IsActive => False' ELSE N'' END,
       @Now, @SystemUser, 0 FROM @Change AS c;

SELECT (SELECT COUNT(*) FROM @Change) AS [Mục đổi], (SELECT COUNT(*) FROM @Grant) AS [Dòng quyền hợp nhất], CASE WHEN @DryRun = 1 THEN N'CHẠY THỬ – đã hoàn tác' ELSE N'ĐÃ GHI' END AS [Kết quả];
IF @DryRun = 1 ROLLBACK TRANSACTION; ELSE COMMIT TRANSACTION;
