/*
  UI-6.19 (D-100 – anh chốt 2026-11-24): dọn bảng giá nhà cung cấp. Script DỮ LIỆU một lần (không tạo / sửa bảng, cột; không DELETE):
    1. PurchaseUomId trống → ĐVT mua của nguyên liệu, không có thì ĐVT kho (liệt kê các dòng đã gán để Mua hàng xác nhận đơn vị thật).
    2. Giá chồng khoảng cùng NCC × nguyên liệu → giá bắt đầu sớm hơn kết thúc = ngày bắt đầu giá sau − 1.
    3. DeliveryFrequency "hằng ngày" / "daily" / "hàng ngày" → HANG_NGAY (giá trị khác giữ nguyên – màn hiện "(giá trị cũ)").
  Ghi ModifiedDate / ModifiedBy (system) + AuditChange. Chạy lại lần 2 không đổi gì. Bắt buộc -v DryRun=1 / 0. DB dev chạy thật (D-92 a).
    sqlcmd -S <server> -d <db> -E -f 65001 -v DryRun=1 -i DB/scripts/UI619_supplier_material_cleanup.sql -s "|" -W
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
-- 1. Đơn vị giá trống.
DECLARE @Unit TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY, UnitId UNIQUEIDENTIFIER);
INSERT INTO @Unit
SELECT m.Id, COALESCE(i.PurchaseUomId, i.BaseUnitId, i.BaseUomId)
FROM dbo.SupplierMaterial AS m JOIN dbo.Ingredient AS i ON i.Id = m.IngredientId
WHERE ISNULL(m.IsDeleted, 0) = 0 AND m.PurchaseUomId IS NULL AND COALESCE(i.PurchaseUomId, i.BaseUnitId, i.BaseUomId) IS NOT NULL;
UPDATE m SET m.PurchaseUomId = u.UnitId, m.ModifiedDate = @Now, m.ModifiedBy = @SystemUser FROM dbo.SupplierMaterial AS m JOIN @Unit AS u ON u.Id = m.Id;
INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), Id, N'Modified', N'SupplierMaterial', N'- PurchaseUomId: gán ĐVT mua / kho của nguyên liệu (UI-6.19 D-100)', @Now, @SystemUser, 0 FROM @Unit;

-- 2. Chồng khoảng: dòng trước kết thúc trước ngày bắt đầu dòng sau.
DECLARE @End TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY, NewTo DATE);
;WITH o AS (
    SELECT m.Id, m.EffectiveFrom, m.EffectiveTo,
           LEAD(m.EffectiveFrom) OVER (PARTITION BY m.SupplierId, m.IngredientId ORDER BY ISNULL(m.EffectiveFrom, '19000101'), m.CreatedDate) AS NextFrom
    FROM dbo.SupplierMaterial AS m WHERE ISNULL(m.IsDeleted, 0) = 0)
INSERT INTO @End
SELECT Id, DATEADD(DAY, -1, CAST(NextFrom AS DATE)) FROM o
WHERE NextFrom IS NOT NULL AND (EffectiveTo IS NULL OR CAST(EffectiveTo AS DATE) >= CAST(NextFrom AS DATE))
  AND DATEADD(DAY, -1, CAST(NextFrom AS DATE)) >= CAST(ISNULL(EffectiveFrom, '19000101') AS DATE);
UPDATE m SET m.EffectiveTo = e.NewTo, m.ModifiedDate = @Now, m.ModifiedBy = @SystemUser FROM dbo.SupplierMaterial AS m JOIN @End AS e ON e.Id = m.Id;
INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), Id, N'Modified', N'SupplierMaterial', N'- EffectiveTo: hệ thống tự kết thúc giá chồng khoảng (UI-6.19 D-100)', @Now, @SystemUser, 0 FROM @End;

-- 3. Ngày giao "hằng ngày".
DECLARE @Freq TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY);
INSERT INTO @Freq SELECT Id FROM dbo.SupplierMaterial WHERE ISNULL(IsDeleted, 0) = 0 AND LOWER(LTRIM(RTRIM(DeliveryFrequency))) IN (N'hằng ngày', N'hàng ngày', N'daily', N'mỗi ngày');
UPDATE m SET m.DeliveryFrequency = N'HANG_NGAY', m.ModifiedDate = @Now, m.ModifiedBy = @SystemUser FROM dbo.SupplierMaterial AS m JOIN @Freq AS f ON f.Id = m.Id;

SELECT (SELECT COUNT(*) FROM @Unit) AS [Gán đơn vị giá], (SELECT COUNT(*) FROM @End) AS [Tự kết thúc giá chồng], (SELECT COUNT(*) FROM @Freq) AS [Ngày giao chuẩn hóa],
       CASE WHEN @DryRun = 1 THEN N'CHẠY THỬ – đã hoàn tác' ELSE N'ĐÃ GHI' END AS [Kết quả];
SELECT TOP 50 s.Name AS [NCC], i.Name AS [Nguyên liệu], u2.Code AS [Đơn vị vừa gán] FROM @Unit AS x
JOIN dbo.SupplierMaterial AS m ON m.Id = x.Id JOIN dbo.Supplier AS s ON s.Id = m.SupplierId JOIN dbo.Ingredient AS i ON i.Id = m.IngredientId LEFT JOIN dbo.UnitMeasure AS u2 ON u2.Id = x.UnitId;
IF @DryRun = 1
    ROLLBACK TRANSACTION;
ELSE
    COMMIT TRANSACTION;
