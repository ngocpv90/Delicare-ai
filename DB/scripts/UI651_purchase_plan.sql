/*
  UI-6.51 (D-137): dọn đề nghị mua NHÁP TRÙNG trước khi bật kế hoạch mua tự động. Nguyên nhân: "Kế hoạch mua 7 ngày" (bấm tay) và MrpJob 20:30 cùng lập
  đề nghị Nháp cho cùng nguyên liệu + ngày cần (Calculate trước đây không trừ đề nghị đang mở).
  Script DỮ LIỆU (không tạo / sửa bảng, cột; KHÔNG xóa cứng): với mỗi (nguyên liệu, ngày cần) có dòng ở > 1 đề nghị Nháp chưa lên PO, GIỮ đề nghị mới nhất;
  đề nghị Nháp cũ hơn mà MỌI dòng đều trùng → Status = Cancelled + ghi chú "Hủy: trùng …" + AuditChange. Đề nghị còn dòng không trùng → giữ, in ra để người mua xem.
  Chạy qua deploy/db/once.order (mỗi DB 1 lần; không trùng = 0 việc). Chạy tay: -v DryRun=1 (thử) / 0 (thật). Chạy lại = 0 việc.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
DECLARE @DryRun BIT = $(DryRun);
DECLARE @By UNIQUEIDENTIFIER = ISNULL((SELECT TOP 1 Id FROM dbo.[User] WHERE UserName = N'system' AND ISNULL(IsDeleted, 0) = 0),
                                      (SELECT TOP 1 Id FROM dbo.[User] WHERE UserName = N'admin' AND ISNULL(IsDeleted, 0) = 0));
DECLARE @Now DATETIME = DATEADD(HOUR, 7, SYSUTCDATETIME());
BEGIN TRANSACTION;
DECLARE @L TABLE (PrId UNIQUEIDENTIFIER, IngredientId UNIQUEIDENTIFIER, NeedDate DATE, PrCreated DATETIME);
INSERT INTO @L
SELECT p.Id, l.IngredientId, CAST(ISNULL(l.RequiredDate, ISNULL(p.RequiredDate, p.RequestDate)) AS DATE), p.CreatedDate
FROM dbo.PurchaseRequestLine l JOIN dbo.PurchaseRequest p ON p.Id = l.PurchaseRequestId
WHERE ISNULL(l.IsDeleted, 0) = 0 AND ISNULL(p.IsDeleted, 0) = 0 AND p.Status = N'Draft' AND l.Quantity > 0
  AND NOT EXISTS (SELECT 1 FROM dbo.PurchaseOrder o WHERE o.PurchaseRequestId = p.Id AND ISNULL(o.IsDeleted, 0) = 0);
-- Dòng trùng = có đề nghị Nháp KHÁC mới hơn cho cùng nguyên liệu + ngày cần.
DECLARE @Dup TABLE (PrId UNIQUEIDENTIFIER, IngredientId UNIQUEIDENTIFIER, NeedDate DATE);
INSERT INTO @Dup
SELECT a.PrId, a.IngredientId, a.NeedDate FROM @L a
WHERE EXISTS (SELECT 1 FROM @L b WHERE b.IngredientId = a.IngredientId AND b.NeedDate = a.NeedDate AND b.PrId <> a.PrId
              AND (b.PrCreated > a.PrCreated OR (b.PrCreated = a.PrCreated AND b.PrId > a.PrId)));
DECLARE @Cancel TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY);
INSERT INTO @Cancel SELECT DISTINCT d.PrId FROM @Dup d
WHERE NOT EXISTS (SELECT 1 FROM @L x WHERE x.PrId = d.PrId AND NOT EXISTS (SELECT 1 FROM @Dup y WHERE y.PrId = x.PrId AND y.IngredientId = x.IngredientId AND y.NeedDate = x.NeedDate));

UPDATE p SET Status = N'Cancelled', Note = LEFT(CONCAT(ISNULL(p.Note + N' | ', N''), N'Hủy: trùng nguyên liệu + ngày cần với đề nghị Nháp mới hơn (UI-6.51 dọn trước kế hoạch mua tự động)'), 1000),
             ModifiedDate = @Now, ModifiedBy = @By
FROM dbo.PurchaseRequest p JOIN @Cancel c ON c.Id = p.Id;
DECLARE @Cancelled INT = @@ROWCOUNT;
INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), c.Id, N'Cancelled', N'PurchaseRequest', N'- UI-6.51 (D-137): đề nghị Nháp trùng (mọi dòng có ở đề nghị Nháp mới hơn) – chuyển Hủy', @Now, @By, 0 FROM @Cancel c;

SELECT p.RequestNumber AS [Đề nghị Nháp có dòng trùng nhưng còn dòng riêng – người mua xem], COUNT(*) AS [Dòng trùng]
FROM @Dup d JOIN dbo.PurchaseRequest p ON p.Id = d.PrId WHERE d.PrId NOT IN (SELECT Id FROM @Cancel) GROUP BY p.RequestNumber;
SELECT @Cancelled AS [Đề nghị Nháp trùng chuyển Hủy], CASE WHEN @DryRun = 1 THEN N'CHẠY THỬ – đã hoàn tác' ELSE N'ĐÃ GHI' END AS [Kết quả];
IF @DryRun = 1 ROLLBACK TRANSACTION; ELSE COMMIT TRANSACTION;
