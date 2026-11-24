/*
  UI-6.12 (D-90 – anh chốt 2026-11-23): không áp dụng thực đơn riêng cho từng khách – mọi khách dùng thực đơn chung của tuần.
  Script DỮ LIỆU một lần (không tạo / sửa bảng, cột; không DELETE):
    - Ẩn mục menu "Thực đơn riêng cho khách" (#/menu-assignment): ObjectMenu.IsActive = 0, GIỮ dòng + UserRole (quyền không đổi).
    - Dữ liệu MenuAssignment cũ GIỮ NGUYÊN, không đọc nữa (tra thực đơn chỉ lấy thực đơn chung Đã công bố – MenuAssignmentService.ResolveAsync).
      Script in số phân công còn hiệu lực từ hôm nay để biết khách nào trước đây dùng thực đơn riêng.
  Ghi ModifiedDate / ModifiedBy (tài khoản system) + AuditChange. Chạy lại lần 2 không đổi gì. Số dòng UserRole phải không đổi.

  Bắt buộc truyền -v DryRun=1 (chạy thử) hoặc -v DryRun=0 (chạy thật). DB dev: chạy thật ngay trong task (D-92 a).
    sqlcmd -S <server> -d <db> -E -f 65001 -v DryRun=1 -i DB/scripts/UI612_hide_menu_assignment.sql -s "|" -W
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

DECLARE @DryRun BIT = $(DryRun);
DECLARE @SystemUser UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.[User] WHERE UserName = N'system' AND ISNULL(IsDeleted, 0) = 0);
IF @SystemUser IS NULL
    THROW 50101, N'Không có tài khoản "system" (seed T23) – không ghi được người thao tác.', 1;
DECLARE @Now DATETIME = DATEADD(HOUR, 7, SYSUTCDATETIME());
DECLARE @Today DATE = CAST(@Now AS DATE);

BEGIN TRANSACTION;

DECLARE @RolesBefore INT = (SELECT COUNT(*) FROM dbo.UserRole);
DECLARE @Hide TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY, MetaLink NVARCHAR(500), Name NVARCHAR(500));
INSERT INTO @Hide (Id, MetaLink, Name)
SELECT m.Id, m.MetaLink, m.Name FROM dbo.ObjectMenu AS m
WHERE m.MetaLink = N'#/menu-assignment' AND ISNULL(m.IsDeleted, 0) = 0 AND m.IsActive = 1;

SELECT h.MetaLink AS [Ẩn (IsActive = 0)], h.Name AS [Tên] FROM @Hide AS h;
SELECT c.Code AS [Mã KH], c.DisplayName AS [Khách hàng], m.Code AS [Thực đơn riêng], a.MealType AS [Loại suất],
       CONVERT(VARCHAR(10), a.EffectiveFrom, 103) AS [Từ], ISNULL(CONVERT(VARCHAR(10), a.EffectiveTo, 103), N'không hạn') AS [Đến]
FROM dbo.MenuAssignment AS a
JOIN dbo.Customer AS c ON c.Id = a.CustomerId
JOIN dbo.Menu AS m ON m.Id = a.MenuId
WHERE ISNULL(a.IsDeleted, 0) = 0 AND (a.Status IS NULL OR a.Status = N'Active') AND (a.EffectiveTo IS NULL OR a.EffectiveTo >= @Today)
ORDER BY c.Code;

IF @DryRun = 1
BEGIN
    ROLLBACK TRANSACTION;
    PRINT N'Chạy thử: không ghi gì. Chạy thật: -v DryRun=0. Phân công liệt kê ở trên KHÔNG còn được dùng (khách nhận thực đơn chung).';
    RETURN;
END

UPDATE o SET o.IsActive = 0, o.ModifiedDate = @Now, o.ModifiedBy = @SystemUser
FROM dbo.ObjectMenu AS o JOIN @Hide AS h ON h.Id = o.Id;

INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), h.Id, N'Modified', N'ObjectMenu',
       N'- Actor: SYSTEM:' + LOWER(CAST(@SystemUser AS NVARCHAR(36))) + N' | UI-6.12 (D-90) bỏ thực đơn riêng theo khách <br />' + CHAR(13) + CHAR(10)
       + N'- IsActive: ''True'' → ''False'' <br />',
       @Now, @SystemUser, 0
FROM @Hide AS h;

DECLARE @RolesAfter INT = (SELECT COUNT(*) FROM dbo.UserRole);
IF @RolesAfter <> @RolesBefore
    THROW 50102, N'Số dòng UserRole thay đổi – hủy (quyền không được đổi).', 1;

COMMIT TRANSACTION;
SELECT (SELECT COUNT(*) FROM @Hide) AS [Đã ẩn], @RolesBefore AS [UserRole trước], @RolesAfter AS [UserRole sau];
PRINT N'Đã áp dụng. Chạy lại lần 2 sẽ không đổi gì.';
