/*
  UI-6.22 (D-103 – anh chốt 2026-11-24): quy đổi đơn vị gộp vào Đơn vị tính. Script DỮ LIỆU một lần (không tạo / sửa bảng, cột; không DELETE):
    1. Ẩn mục menu "Quy đổi đơn vị" (#/scm-uom-conversion): ObjectMenu.IsActive = 0, GIỮ dòng + UserRole (quyền API UomConversion theo màn Đơn vị tính).
    2. Đơn vị gốc chuẩn + quy đổi chuẩn nếu thiếu: kg, g, l (lít), ml (UomType = BASE); 1 kg = 1000 g, 1 l = 1000 ml (luôn hiệu lực).
    3. Đánh dấu loại đơn vị: UomType trống → BASE (đơn vị đang có quy đổi sang đơn vị khác ghi PACK khi tên là loại bao bì).
    4. Báo cáo đơn vị đóng gói CHƯA ghi kích cỡ (thùng / bao / khay … không có số) – lập docs/claude/UOM_REVIEW.md, KHÔNG tự tách.
  Ghi ModifiedDate / ModifiedBy (system) + AuditChange. Chạy lại lần 2 không đổi gì. Bắt buộc -v DryRun=1 / 0. DB dev chạy thật (D-92 a).
    sqlcmd -S <server> -d <db> -E -f 65001 -v DryRun=1 -i DB/scripts/UI622_uom_pack.sql -s "|" -W
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
DECLARE @RolesBefore INT = (SELECT COUNT(*) FROM dbo.UserRole);
-- 1. Ẩn menu.
DECLARE @Menu TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY);
INSERT INTO @Menu SELECT Id FROM dbo.ObjectMenu WHERE ISNULL(IsDeleted, 0) = 0 AND MetaLink = N'#/scm-uom-conversion' AND IsActive = 1;
UPDATE m SET m.IsActive = 0, m.ModifiedDate = @Now, m.ModifiedBy = @SystemUser FROM dbo.ObjectMenu AS m JOIN @Menu AS x ON x.Id = m.Id;
INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), Id, N'Modified', N'ObjectMenu', N'- IsActive: ''True'' → ''False'' (UI-6.22 D-103: quy đổi gộp vào Đơn vị tính, giữ quyền)', @Now, @SystemUser, 0 FROM @Menu;

-- 2. Đơn vị gốc chuẩn + quy đổi chuẩn.
DECLARE @Std TABLE (Code NVARCHAR(20), Name NVARCHAR(100));
INSERT INTO @Std VALUES (N'kg', N'Kilôgam'), (N'g', N'Gam'), (N'l', N'Lít'), (N'ml', N'Mililít');
DECLARE @NewUnit TABLE (Id UNIQUEIDENTIFIER, Code NVARCHAR(20));
INSERT INTO dbo.UnitMeasure (Id, Code, Name, Symbol, UomType, DecimalPrecision, Status, CreatedDate, CreatedBy, IsDeleted)
OUTPUT inserted.Id, inserted.Code INTO @NewUnit
SELECT NEWID(), s.Code, s.Name, s.Code, N'BASE', 3, N'Active', @Now, @SystemUser, 0 FROM @Std AS s
WHERE NOT EXISTS (SELECT 1 FROM dbo.UnitMeasure AS u WHERE ISNULL(u.IsDeleted, 0) = 0 AND u.Code = s.Code);
DECLARE @kg UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.UnitMeasure WHERE ISNULL(IsDeleted, 0) = 0 AND Code = N'kg' ORDER BY CreatedDate);
DECLARE @g UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.UnitMeasure WHERE ISNULL(IsDeleted, 0) = 0 AND Code = N'g' ORDER BY CreatedDate);
DECLARE @l UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.UnitMeasure WHERE ISNULL(IsDeleted, 0) = 0 AND Code = N'l' ORDER BY CreatedDate);
DECLARE @ml UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.UnitMeasure WHERE ISNULL(IsDeleted, 0) = 0 AND Code = N'ml' ORDER BY CreatedDate);
DECLARE @NewConv INT = 0;
INSERT INTO dbo.UomConversion (Id, FromUomId, ToUomId, FromUnitId, ToUnitId, ConversionFactor, Status, IsActive, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), x.F, x.T, x.F, x.T, 1000, N'Active', 1, @Now, @SystemUser, 0
FROM (VALUES (@kg, @g), (@l, @ml)) AS x(F, T)
WHERE x.F IS NOT NULL AND x.T IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.UomConversion AS c WHERE ISNULL(c.IsDeleted, 0) = 0 AND ((c.FromUnitId = x.F AND c.ToUnitId = x.T) OR (c.FromUnitId = x.T AND c.ToUnitId = x.F)));
SET @NewConv = @@ROWCOUNT;

-- 3. Loại đơn vị trống → BASE.
DECLARE @Typed TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY);
INSERT INTO @Typed SELECT Id FROM dbo.UnitMeasure WHERE ISNULL(IsDeleted, 0) = 0 AND UomType IS NULL;
UPDATE u SET u.UomType = N'BASE', u.ModifiedDate = @Now, u.ModifiedBy = @SystemUser FROM dbo.UnitMeasure AS u JOIN @Typed AS t ON t.Id = u.Id;

SELECT (SELECT COUNT(*) FROM @Menu) AS [Mục menu ẩn], (SELECT COUNT(*) FROM @NewUnit) AS [Đơn vị chuẩn thêm], @NewConv AS [Quy đổi chuẩn thêm],
       (SELECT COUNT(*) FROM @Typed) AS [Gán loại Gốc], @RolesBefore AS [UserRole trước], (SELECT COUNT(*) FROM dbo.UserRole) AS [UserRole sau],
       CASE WHEN @DryRun = 1 THEN N'CHẠY THỬ – đã hoàn tác' ELSE N'ĐÃ GHI' END AS [Kết quả];
-- 4. Báo cáo đơn vị đóng gói chưa có kích cỡ.
SELECT u.Code AS [Mã], u.Name AS [Đơn vị], (SELECT COUNT(*) FROM dbo.Ingredient i WHERE ISNULL(i.IsDeleted, 0) = 0 AND (i.PurchaseUomId = u.Id OR i.IssueUomId = u.Id OR i.BaseUnitId = u.Id)) AS [Nguyên liệu],
       (SELECT COUNT(*) FROM dbo.SupplierMaterial m WHERE ISNULL(m.IsDeleted, 0) = 0 AND m.PurchaseUomId = u.Id) AS [Giá NCC]
FROM dbo.UnitMeasure AS u
WHERE ISNULL(u.IsDeleted, 0) = 0 AND u.Name NOT LIKE N'%[0-9]%'
  AND (u.Name LIKE N'%thùng%' OR u.Name LIKE N'%bao%' OR u.Name LIKE N'%khay%' OR u.Name LIKE N'%hộp%' OR u.Name LIKE N'%chai%' OR u.Name LIKE N'%can%' OR u.Name LIKE N'%túi%' OR u.Name LIKE N'%kiện%' OR u.Name LIKE N'%lốc%');
IF (SELECT COUNT(*) FROM dbo.UserRole) <> @RolesBefore
    THROW 50102, N'Số dòng UserRole thay đổi – dừng.', 1;
IF @DryRun = 1
    ROLLBACK TRANSACTION;
ELSE
    COMMIT TRANSACTION;
