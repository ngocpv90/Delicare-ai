/*
  UI-6.36 (D-117 – anh chốt 2026-11-24): dọn dữ liệu nhân viên theo luật mới.
  Script DỮ LIỆU một lần (không tạo / sửa bảng, cột; không DELETE):
    1. Điền Employee.CompanyId theo công ty của đơn vị (OrganizationUnit.CompanyId) cho nhân viên trống / lệch.
    2. LIỆT KÊ (không sửa) nhân viên chưa nghỉ việc trùng số điện thoại (so theo số đã bỏ khoảng trắng / dấu chấm / gạch, +84 → 0)
       và nhân viên trống họ tên – để anh / NS xử lý tay.
  Ghi ModifiedDate / ModifiedBy (system) + AuditChange. Chạy lại lần 2 = 0 việc.
  -v DryRun=1 (thử) / 0 (thật). DB dev chạy thật (D-92 a).
    sqlcmd -S <server> -d <db> -E -b -f 65001 -v DryRun=1 -i DB/scripts/UI636_employee_cleanup.sql -s "|" -W
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
DECLARE @Fix TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY, Code NVARCHAR(100), FullName NVARCHAR(500), OldCompany UNIQUEIDENTIFIER NULL, NewCompany UNIQUEIDENTIFIER);
INSERT INTO @Fix
SELECT e.Id, ISNULL(e.Code, e.EmployeeCode), e.FullName, e.CompanyId, ou.CompanyId
FROM dbo.Employee AS e
JOIN dbo.OrganizationUnit AS ou ON ou.Id = e.OrganizationUnitId AND ISNULL(ou.IsDeleted, 0) = 0
WHERE ISNULL(e.IsDeleted, 0) = 0 AND ou.CompanyId <> '00000000-0000-0000-0000-000000000000'
  AND (e.CompanyId IS NULL OR e.CompanyId <> ou.CompanyId);

SELECT f.Code AS [Mã NV], f.FullName AS [Họ tên], ISNULL(CONVERT(NVARCHAR(36), f.OldCompany), N'(trống)') AS [Công ty cũ], CONVERT(NVARCHAR(36), f.NewCompany) AS [Công ty theo đơn vị]
FROM @Fix AS f;

UPDATE e SET e.CompanyId = f.NewCompany, e.ModifiedDate = @Now, e.ModifiedBy = @SystemUser
FROM dbo.Employee AS e JOIN @Fix AS f ON f.Id = e.Id;
INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), f.Id, N'Modified', N'Employee', N'- UI-6.36 (D-117): CompanyId => ''' + CONVERT(NVARCHAR(36), f.NewCompany) + N''' (theo đơn vị)', @Now, @SystemUser, 0 FROM @Fix AS f;

-- Liệt kê: trùng số điện thoại giữa người chưa nghỉ việc.
;WITH P AS (
    SELECT e.Id, ISNULL(e.Code, e.EmployeeCode) AS Code, e.FullName, e.Phone,
           CASE WHEN LEFT(x.p, 3) = N'+84' THEN N'0' + SUBSTRING(x.p, 4, 20) WHEN LEFT(x.p, 2) = N'84' AND LEN(x.p) = 11 THEN N'0' + SUBSTRING(x.p, 3, 20) ELSE x.p END AS Norm
    FROM dbo.Employee AS e
    CROSS APPLY (SELECT REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(e.Phone)), N' ', N''), N'.', N''), N'-', N''), N'(', N''), N')', N'') AS p) AS x
    WHERE ISNULL(e.IsDeleted, 0) = 0 AND ISNULL(e.Status, N'Active') <> N'Resigned' AND NULLIF(LTRIM(RTRIM(e.Phone)), N'') IS NOT NULL)
SELECT p.Norm AS [SĐT trùng], p.Code AS [Mã NV], p.FullName AS [Họ tên], p.Phone AS [SĐT đang ghi]
FROM P AS p WHERE p.Norm IN (SELECT Norm FROM P GROUP BY Norm HAVING COUNT(*) > 1)
ORDER BY p.Norm, p.Code;

-- Liệt kê: trống họ tên.
SELECT ISNULL(e.Code, e.EmployeeCode) AS [Mã NV trống họ tên], e.Phone AS [SĐT], e.Status AS [Trạng thái]
FROM dbo.Employee AS e WHERE ISNULL(e.IsDeleted, 0) = 0 AND NULLIF(LTRIM(RTRIM(e.FullName)), N'') IS NULL;

SELECT (SELECT COUNT(*) FROM @Fix) AS [Nhân viên sửa công ty], CASE WHEN @DryRun = 1 THEN N'CHẠY THỬ – đã hoàn tác' ELSE N'ĐÃ GHI' END AS [Kết quả];
IF @DryRun = 1 ROLLBACK TRANSACTION; ELSE COMMIT TRANSACTION;
