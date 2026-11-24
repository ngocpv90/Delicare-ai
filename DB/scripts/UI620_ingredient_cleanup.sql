/*
  UI-6.20 (D-101 – anh chốt 2026-11-24): dọn dữ liệu nguyên liệu cho form mới. Script DỮ LIỆU một lần (không tạo / sửa bảng, cột; không DELETE):
    1. StorageType tự do → 3 mã chuẩn khi nhận diện được (mát / lạnh / chill → CHILLED; đông / frozen → FROZEN; khô / thường / ambient → DRY);
       còn lại GIỮ NGUYÊN + liệt kê để anh / QA xác nhận.
    2. Đồng bộ 2 cờ hạn dùng: ExpiryControl = IsExpiryManaged = 1 nếu một trong hai bật hoặc có ShelfLifeDays > 0.
    3. PurchaseUomId / IssueUomId trống → BaseUnitId (đơn vị kho).
    4. YieldPercent = 100 − StandardWastePercent (khi có hao hụt chuẩn và đang lệch).
    5. Báo nguyên liệu Mát / Đông lạnh đang tắt QcRequired (chỉ liệt kê, không tự bật).
  Ghi ModifiedDate / ModifiedBy (system) + AuditChange. Chạy lại lần 2 không đổi gì. Bắt buộc -v DryRun=1 / 0. DB dev chạy thật (D-92 a).
    sqlcmd -S <server> -d <db> -E -f 65001 -v DryRun=1 -i DB/scripts/UI620_ingredient_cleanup.sql -s "|" -W
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
DECLARE @Fix TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY, OldStorage NVARCHAR(100), NewStorage NVARCHAR(100), OldExpiry BIT, NewExpiry BIT,
    OldPur UNIQUEIDENTIFIER, NewPur UNIQUEIDENTIFIER, OldIss UNIQUEIDENTIFIER, NewIss UNIQUEIDENTIFIER, OldYield DECIMAL(18, 4), NewYield DECIMAL(18, 4));
INSERT INTO @Fix
SELECT i.Id, i.StorageType, x.NewStorage, i.IsExpiryManaged, x.NewExpiry, i.PurchaseUomId, ISNULL(i.PurchaseUomId, i.BaseUnitId), i.IssueUomId, ISNULL(i.IssueUomId, i.BaseUnitId),
       i.YieldPercent, CASE WHEN i.StandardWastePercent IS NOT NULL AND i.StandardWastePercent >= 0 AND i.StandardWastePercent < 100 THEN 100 - i.StandardWastePercent ELSE i.YieldPercent END
FROM dbo.Ingredient AS i
CROSS APPLY (SELECT
    CASE WHEN UPPER(ISNULL(i.StorageType, N'')) IN (N'DRY', N'CHILLED', N'FROZEN') THEN UPPER(i.StorageType)
         WHEN LOWER(ISNULL(i.StorageType, N'')) LIKE N'%đông%' OR LOWER(ISNULL(i.StorageType, N'')) LIKE N'%frozen%' OR LOWER(ISNULL(i.StorageType, N'')) LIKE N'%freez%' THEN N'FROZEN'
         WHEN LOWER(ISNULL(i.StorageType, N'')) LIKE N'%mát%' OR LOWER(ISNULL(i.StorageType, N'')) LIKE N'%lạnh%' OR LOWER(ISNULL(i.StorageType, N'')) LIKE N'%chill%' OR LOWER(ISNULL(i.StorageType, N'')) LIKE N'%cool%' THEN N'CHILLED'
         WHEN LOWER(ISNULL(i.StorageType, N'')) LIKE N'%khô%' OR LOWER(ISNULL(i.StorageType, N'')) LIKE N'%thường%' OR LOWER(ISNULL(i.StorageType, N'')) LIKE N'%ambient%' OR LOWER(ISNULL(i.StorageType, N'')) LIKE N'%dry%' THEN N'DRY'
         ELSE i.StorageType END AS NewStorage,
    CASE WHEN ISNULL(i.IsExpiryManaged, 0) = 1 OR ISNULL(i.ExpiryControl, 0) = 1 OR ISNULL(i.ShelfLifeDays, 0) > 0 THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS NewExpiry) AS x
WHERE ISNULL(i.IsDeleted, 0) = 0
  AND (ISNULL(i.StorageType, N'') <> ISNULL(x.NewStorage, N'') COLLATE Latin1_General_CS_AS
    OR ISNULL(i.IsExpiryManaged, 0) <> x.NewExpiry OR ISNULL(i.ExpiryControl, 0) <> x.NewExpiry
    OR (i.PurchaseUomId IS NULL AND i.BaseUnitId IS NOT NULL) OR (i.IssueUomId IS NULL AND i.BaseUnitId IS NOT NULL)
    OR (i.StandardWastePercent IS NOT NULL AND i.StandardWastePercent >= 0 AND i.StandardWastePercent < 100 AND ISNULL(i.YieldPercent, -1) <> 100 - i.StandardWastePercent));

SELECT SUM(CASE WHEN ISNULL(OldStorage, N'') <> ISNULL(NewStorage, N'') COLLATE Latin1_General_CS_AS THEN 1 ELSE 0 END) AS [Loại bảo quản chuẩn hóa],
       SUM(CASE WHEN ISNULL(OldExpiry, 0) <> NewExpiry THEN 1 ELSE 0 END) AS [Đồng bộ hạn dùng],
       SUM(CASE WHEN OldPur IS NULL AND NewPur IS NOT NULL THEN 1 ELSE 0 END) AS [ĐVT mua = kho],
       SUM(CASE WHEN OldIss IS NULL AND NewIss IS NOT NULL THEN 1 ELSE 0 END) AS [ĐVT xuất = kho],
       SUM(CASE WHEN ISNULL(OldYield, -1) <> ISNULL(NewYield, -1) THEN 1 ELSE 0 END) AS [Thu hồi theo hao hụt] FROM @Fix;

UPDATE i SET i.StorageType = x.NewStorage, i.IsExpiryManaged = x.NewExpiry, i.ExpiryControl = x.NewExpiry, i.PurchaseUomId = x.NewPur, i.IssueUomId = x.NewIss,
    i.YieldPercent = x.NewYield, i.ModifiedDate = @Now, i.ModifiedBy = @SystemUser
FROM dbo.Ingredient AS i JOIN @Fix AS x ON x.Id = i.Id;

INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), x.Id, N'Modified', N'Ingredient',
       CONCAT(CASE WHEN ISNULL(x.OldStorage, N'') <> ISNULL(x.NewStorage, N'') COLLATE Latin1_General_CS_AS THEN N'- StorageType: ''' + ISNULL(x.OldStorage, N'') + N''' → ''' + ISNULL(x.NewStorage, N'') + N''' ' ELSE N'' END,
              CASE WHEN ISNULL(x.OldExpiry, 0) <> x.NewExpiry THEN N'- IsExpiryManaged: ''' + CAST(ISNULL(x.OldExpiry, 0) AS NVARCHAR(1)) + N''' → ''' + CAST(x.NewExpiry AS NVARCHAR(1)) + N''' ' ELSE N'' END,
              N'(UI-6.20 D-101)'), @Now, @SystemUser, 0
FROM @Fix AS x;

-- Còn lại cho anh / QA: loại bảo quản không nhận diện được + Mát / Đông lạnh đang tắt kiểm tra khi nhận.
SELECT Code AS [Mã], Name AS [Nguyên liệu], StorageType AS [Loại bảo quản chưa nhận diện] FROM dbo.Ingredient
WHERE ISNULL(IsDeleted, 0) = 0 AND StorageType IS NOT NULL AND StorageType NOT IN (N'DRY', N'CHILLED', N'FROZEN');
SELECT COUNT(*) AS [Mát / Đông lạnh đang tắt kiểm tra khi nhận] FROM dbo.Ingredient
WHERE ISNULL(IsDeleted, 0) = 0 AND StorageType IN (N'CHILLED', N'FROZEN') AND ISNULL(QcRequired, 0) = 0;

SELECT (SELECT COUNT(*) FROM @Fix) AS [Nguyên liệu sửa], CASE WHEN @DryRun = 1 THEN N'CHẠY THỬ – đã hoàn tác' ELSE N'ĐÃ GHI' END AS [Kết quả];
IF @DryRun = 1
    ROLLBACK TRANSACTION;
ELSE
    COMMIT TRANSACTION;
