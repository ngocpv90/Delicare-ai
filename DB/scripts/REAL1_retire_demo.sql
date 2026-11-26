/*
  REAL-1 (0) (D-129 e – anh chốt 2026-11-26): dọn dữ liệu [DEMO] cũ trước khi nạp dữ liệu vận hành thật – KHÔNG xóa cứng.
  (a) Chứng từ có ngày ≥ hôm nay (giờ VN) của danh mục [DEMO]:
      - Đơn hàng của khách [DEMO]: Nháp / Đã gửi / Đã xác nhận → Hủy (đúng máy trạng thái – OrderService.Cancel); trạng thái không có bước Hủy
        (Đã chốt, Đang SX, Đã giao, Đã đối soát) → xóa mềm (để ngày đó không còn nhu cầu giả khi lập kế hoạch thật).
      - Kế hoạch SX (không có trạng thái Hủy, 1 kế hoạch / ngày) → xóa mềm; lệnh SX chưa xong của kế hoạch đó → Hủy; mẻ Kế hoạch / Đang nấu → Hủy.
      - Chuyến giao (không có trạng thái Hủy) của xe [DEMO] → xóa mềm; đơn giao Kế hoạch → Hủy.
      - Dự báo của khách [DEMO] → Bị thay thế (Superseded). Thực đơn tên [DEMO] còn hiệu lực → Hết hạn + xóa mềm.
      - Đề nghị mua Nháp / Chờ duyệt → Hủy; đơn mua Nháp / Đã gửi (chưa nhận) → Hủy.
  (b) Danh mục tên [DEMO] (khách, điểm giao, món, nhóm món, nguyên liệu, nhóm NL, NCC, bảng giá NCC của NCC / NL [DEMO], kho, xe, dây chuyền, ĐVT,
      nhân viên [DEMO] – trừ nhân viên gắn tài khoản uat.*) → Ngừng hoạt động (Inactive) + xóa mềm.
  (c) KHÔNG đụng: sổ chỉ ghi thêm (AttendanceEvent, InventoryTransaction, PackagingLedger, AuditChange…), chứng từ ngày < hôm nay, đơn vị tổ chức,
      nhóm quyền, menu, quyền, tài khoản (uat.*, admin, system).
  Ghi ModifiedDate / ModifiedBy (system) + 1 dòng AuditChange / bảng. Chạy lại = 0 việc. Sau mỗi lần chạy seed-demo / full-flow thì chạy lại.
  -v DryRun=1 (thử) / 0 (thật).
    sqlcmd -S <server> -d <db> -E -b -f 65001 -v DryRun=1 -i DB/scripts/REAL1_retire_demo.sql -s "|" -W
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
DECLARE @Why NVARCHAR(100) = N'REAL-1: thay dữ liệu demo';
DECLARE @Like NVARCHAR(20) = N'\[DEMO\]%';
DECLARE @R TABLE (Bang NVARCHAR(60), ViecLam NVARCHAR(60), SoDong INT);

BEGIN TRANSACTION;
DECLARE @DemoCust TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY);
INSERT INTO @DemoCust SELECT Id FROM dbo.Customer WHERE Name LIKE N'\[DEMO\]%' ESCAPE N'\';

-- (a) Đơn hàng
UPDATE o SET o.Status = N'Cancelled', o.ModifiedDate = @Now, o.ModifiedBy = @SystemUser
FROM dbo.[Order] AS o JOIN @DemoCust AS c ON c.Id = o.CustomerId
WHERE ISNULL(o.IsDeleted, 0) = 0 AND o.MealDate >= @Today AND o.Status IN (N'Draft', N'Submitted', N'Confirmed');
INSERT INTO @R VALUES (N'Order', N'Hủy', @@ROWCOUNT);
UPDATE o SET o.IsDeleted = 1, o.DeletedDate = @Now, o.ModifiedDate = @Now, o.ModifiedBy = @SystemUser
FROM dbo.[Order] AS o JOIN @DemoCust AS c ON c.Id = o.CustomerId
WHERE ISNULL(o.IsDeleted, 0) = 0 AND o.MealDate >= @Today AND o.Status IN (N'Locked', N'InProduction', N'Delivered', N'Reconciled');
INSERT INTO @R VALUES (N'Order', N'Xóa mềm (không có bước Hủy)', @@ROWCOUNT);

-- Hợp đồng còn mở của khách [DEMO] (không có bước Hủy) → Chấm dứt, hết hiệu lực hôm qua – không còn kéo giá suất bình quân / ma trận dự báo / bảng kê.
UPDATE k SET k.Status = N'Terminated', k.EffectiveTo = DATEADD(DAY, -1, @Today), k.ModifiedDate = @Now, k.ModifiedBy = @SystemUser,
       k.Note = CONCAT(k.Note, CASE WHEN k.Note IS NULL OR k.Note = N'' THEN N'' ELSE NCHAR(10) END, @Why)
FROM dbo.Contract AS k JOIN @DemoCust AS c ON c.Id = k.CustomerId
WHERE ISNULL(k.IsDeleted, 0) = 0 AND k.Status IN (N'Draft', N'PendingApproval', N'Active');
INSERT INTO @R VALUES (N'Contract', N'Chấm dứt (không có bước Hủy)', @@ROWCOUNT);

-- Kế hoạch SX ngày ≥ hôm nay (mọi đơn hiện có đều của khách [DEMO]): lệnh / mẻ chưa xong → Hủy; kế hoạch → xóa mềm.
DECLARE @Plans TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY);
INSERT INTO @Plans SELECT p.Id FROM dbo.ProductionPlan AS p
WHERE ISNULL(p.IsDeleted, 0) = 0 AND p.ProductionDate >= @Today
  AND NOT EXISTS (SELECT 1 FROM dbo.[Order] AS o WHERE ISNULL(o.IsDeleted, 0) = 0 AND o.MealDate = p.ProductionDate AND o.Status <> N'Cancelled'
                  AND o.CustomerId NOT IN (SELECT Id FROM @DemoCust));
UPDATE b SET b.Status = N'Cancelled', b.ModifiedDate = @Now, b.ModifiedBy = @SystemUser
FROM dbo.ProductionBatche AS b JOIN dbo.ProductionOrder AS po ON po.Id = b.ProductionOrderId JOIN @Plans AS p ON p.Id = po.ProductionPlanId
WHERE ISNULL(b.IsDeleted, 0) = 0 AND b.Status IN (N'Planned', N'Cooking');
INSERT INTO @R VALUES (N'ProductionBatche', N'Hủy', @@ROWCOUNT);
UPDATE po SET po.Status = N'Cancelled', po.ModifiedDate = @Now, po.ModifiedBy = @SystemUser
FROM dbo.ProductionOrder AS po JOIN @Plans AS p ON p.Id = po.ProductionPlanId
WHERE ISNULL(po.IsDeleted, 0) = 0 AND po.Status IN (N'Planned', N'Released', N'InProgress');
INSERT INTO @R VALUES (N'ProductionOrder', N'Hủy', @@ROWCOUNT);
UPDATE p SET p.IsDeleted = 1, p.DeletedDate = @Now, p.ModifiedDate = @Now, p.ModifiedBy = @SystemUser
FROM dbo.ProductionPlan AS p JOIN @Plans AS x ON x.Id = p.Id;
INSERT INTO @R VALUES (N'ProductionPlan', N'Xóa mềm (không có bước Hủy)', @@ROWCOUNT);

-- Chuyến của xe [DEMO] ngày ≥ hôm nay; đơn giao Kế hoạch → Hủy.
DECLARE @Routes TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY);
INSERT INTO @Routes SELECT r.Id FROM dbo.DeliveryRoute AS r JOIN dbo.Vehicle AS v ON v.Id = r.VehicleId
WHERE ISNULL(r.IsDeleted, 0) = 0 AND r.DeliveryDate >= @Today AND v.LicensePlate LIKE N'\[DEMO\]%' ESCAPE N'\';
UPDATE d SET d.Status = N'Cancelled', d.ModifiedDate = @Now, d.ModifiedBy = @SystemUser
FROM dbo.DeliveryOrder AS d JOIN @Routes AS r ON r.Id = d.DeliveryRouteId
WHERE ISNULL(d.IsDeleted, 0) = 0 AND d.Status = N'Planned';
INSERT INTO @R VALUES (N'DeliveryOrder', N'Hủy', @@ROWCOUNT);
UPDATE r SET r.IsDeleted = 1, r.DeletedDate = @Now, r.ModifiedDate = @Now, r.ModifiedBy = @SystemUser
FROM dbo.DeliveryRoute AS r JOIN @Routes AS x ON x.Id = r.Id;
INSERT INTO @R VALUES (N'DeliveryRoute', N'Xóa mềm (không có bước Hủy)', @@ROWCOUNT);

-- Dự báo, thực đơn
UPDATE f SET f.Status = N'Superseded', f.ModifiedDate = @Now, f.ModifiedBy = @SystemUser
FROM dbo.Forecast AS f JOIN @DemoCust AS c ON c.Id = f.CustomerId
WHERE ISNULL(f.IsDeleted, 0) = 0 AND f.ForecastDate >= @Today AND ISNULL(f.Status, N'') <> N'Superseded';
INSERT INTO @R VALUES (N'Forecast', N'Bị thay thế', @@ROWCOUNT);
UPDATE m SET m.Status = N'Expired', m.IsDeleted = 1, m.DeletedDate = @Now, m.ModifiedDate = @Now, m.ModifiedBy = @SystemUser
FROM dbo.Menu AS m
WHERE ISNULL(m.IsDeleted, 0) = 0 AND m.Name LIKE N'\[DEMO\]%' ESCAPE N'\' AND (m.EffectiveTo IS NULL OR m.EffectiveTo >= @Today);
INSERT INTO @R VALUES (N'Menu', N'Hết hạn + xóa mềm', @@ROWCOUNT);

-- Mua hàng
UPDATE pr SET pr.Status = N'Cancelled', pr.ModifiedDate = @Now, pr.ModifiedBy = @SystemUser
FROM dbo.PurchaseRequest AS pr WHERE ISNULL(pr.IsDeleted, 0) = 0 AND pr.RequiredDate >= @Today AND pr.Status IN (N'Draft', N'PendingApproval')
  AND NOT EXISTS (SELECT 1 FROM dbo.PurchaseRequestLine AS l JOIN dbo.Ingredient AS i ON i.Id = l.IngredientId WHERE l.PurchaseRequestId = pr.Id AND i.Name NOT LIKE @Like ESCAPE N'\'); -- chỉ đề nghị toàn NL [DEMO]
INSERT INTO @R VALUES (N'PurchaseRequest', N'Hủy', @@ROWCOUNT);
UPDATE po SET po.Status = N'Cancelled', po.ModifiedDate = @Now, po.ModifiedBy = @SystemUser
FROM dbo.PurchaseOrder AS po JOIN dbo.Supplier AS su ON su.Id = po.SupplierId
WHERE ISNULL(po.IsDeleted, 0) = 0 AND po.ExpectedDeliveryDate >= @Today AND po.Status IN (N'Draft', N'Sent') AND su.Name LIKE @Like ESCAPE N'\';
INSERT INTO @R VALUES (N'PurchaseOrder', N'Hủy', @@ROWCOUNT);

-- (b) Danh mục [DEMO] → Ngừng hoạt động + xóa mềm
UPDATE dbo.SupplierMaterial SET Status = N'Inactive', IsDeleted = 1, DeletedDate = @Now, ModifiedDate = @Now, ModifiedBy = @SystemUser
WHERE ISNULL(IsDeleted, 0) = 0 AND (SupplierId IN (SELECT Id FROM dbo.Supplier WHERE Name LIKE @Like ESCAPE N'\') OR IngredientId IN (SELECT Id FROM dbo.Ingredient WHERE Name LIKE @Like ESCAPE N'\'));
INSERT INTO @R VALUES (N'SupplierMaterial', N'Ngừng + xóa mềm', @@ROWCOUNT);
UPDATE dbo.Customer SET Status = N'Inactive', IsDeleted = 1, DeletedDate = @Now, ModifiedDate = @Now, ModifiedBy = @SystemUser WHERE ISNULL(IsDeleted, 0) = 0 AND Name LIKE @Like ESCAPE N'\';
INSERT INTO @R VALUES (N'Customer', N'Ngừng + xóa mềm', @@ROWCOUNT);
UPDATE dbo.CustomerDeliveryLocation SET Status = N'Inactive', IsDeleted = 1, DeletedDate = @Now, ModifiedDate = @Now, ModifiedBy = @SystemUser WHERE ISNULL(IsDeleted, 0) = 0 AND (Name LIKE @Like ESCAPE N'\' OR CustomerId IN (SELECT Id FROM @DemoCust));
INSERT INTO @R VALUES (N'CustomerDeliveryLocation', N'Ngừng + xóa mềm', @@ROWCOUNT);
UPDATE dbo.Dishe SET Status = N'Inactive', IsDeleted = 1, DeletedDate = @Now, ModifiedDate = @Now, ModifiedBy = @SystemUser WHERE ISNULL(IsDeleted, 0) = 0 AND Name LIKE @Like ESCAPE N'\';
INSERT INTO @R VALUES (N'Dishe', N'Ngừng + xóa mềm', @@ROWCOUNT);
-- Quyết định tự chọn REAL-1 (0): món "Ngân hàng món" cũ (mã không phải MON-xxxx, không [DEMO]) trùng ĐÚNG tên 1 món REAL-1 (MON-xxxx) đã nạp
-- → ngừng + xóa mềm, tránh 2 món cùng tên trên màn chọn món. Trước khi nạp 04_MonAn = 0 dòng; chạy lại sau bước (2).
UPDATE d SET d.Status = N'Inactive', d.IsDeleted = 1, d.DeletedDate = @Now, d.ModifiedDate = @Now, d.ModifiedBy = @SystemUser
FROM dbo.Dishe AS d
WHERE ISNULL(d.IsDeleted, 0) = 0 AND d.Code NOT LIKE N'MON-%' AND d.Name NOT LIKE @Like ESCAPE N'\'
  AND EXISTS (SELECT 1 FROM dbo.Dishe AS r WHERE ISNULL(r.IsDeleted, 0) = 0 AND r.Code LIKE N'MON-[0-9][0-9][0-9][0-9]' AND r.Name = d.Name AND r.Id <> d.Id);
INSERT INTO @R VALUES (N'Dishe', N'Ngừng + xóa mềm (ngân hàng cũ trùng tên REAL-1)', @@ROWCOUNT);
-- Nhóm món ngân hàng cũ đã rỗng (mọi món của nhóm do script này ngừng – ModifiedBy system, không còn món đang dùng / nhóm con) → ngừng + xóa mềm.
UPDATE g SET g.Status = N'Inactive', g.IsDeleted = 1, g.DeletedDate = @Now, g.ModifiedDate = @Now, g.ModifiedBy = @SystemUser
FROM dbo.DishCategorie AS g
WHERE ISNULL(g.IsDeleted, 0) = 0 AND g.Name NOT LIKE @Like ESCAPE N'\'
  AND EXISTS (SELECT 1 FROM dbo.Dishe AS d WHERE ISNULL(d.DishCategoryId, d.CategoryId) = g.Id AND d.IsDeleted = 1 AND d.ModifiedBy = @SystemUser)
  AND NOT EXISTS (SELECT 1 FROM dbo.Dishe AS d WHERE ISNULL(d.DishCategoryId, d.CategoryId) = g.Id AND ISNULL(d.IsDeleted, 0) = 0)
  AND NOT EXISTS (SELECT 1 FROM dbo.DishCategorie AS ch WHERE ch.ParentId = g.Id AND ISNULL(ch.IsDeleted, 0) = 0);
INSERT INTO @R VALUES (N'DishCategorie', N'Ngừng + xóa mềm (nhóm ngân hàng cũ đã rỗng)', @@ROWCOUNT);
UPDATE dbo.DishCategorie SET Status = N'Inactive', IsDeleted = 1, DeletedDate = @Now, ModifiedDate = @Now, ModifiedBy = @SystemUser WHERE ISNULL(IsDeleted, 0) = 0 AND Name LIKE @Like ESCAPE N'\';
INSERT INTO @R VALUES (N'DishCategorie', N'Ngừng + xóa mềm', @@ROWCOUNT);
UPDATE dbo.Ingredient SET Status = N'Inactive', IsDeleted = 1, DeletedDate = @Now, ModifiedDate = @Now, ModifiedBy = @SystemUser WHERE ISNULL(IsDeleted, 0) = 0 AND Name LIKE @Like ESCAPE N'\';
INSERT INTO @R VALUES (N'Ingredient', N'Ngừng + xóa mềm', @@ROWCOUNT);
UPDATE dbo.IngredientCategorie SET Status = N'Inactive', IsDeleted = 1, DeletedDate = @Now, ModifiedDate = @Now, ModifiedBy = @SystemUser WHERE ISNULL(IsDeleted, 0) = 0 AND Name LIKE @Like ESCAPE N'\';
INSERT INTO @R VALUES (N'IngredientCategorie', N'Ngừng + xóa mềm', @@ROWCOUNT);
UPDATE dbo.Supplier SET Status = N'Inactive', IsDeleted = 1, DeletedDate = @Now, ModifiedDate = @Now, ModifiedBy = @SystemUser WHERE ISNULL(IsDeleted, 0) = 0 AND Name LIKE @Like ESCAPE N'\';
INSERT INTO @R VALUES (N'Supplier', N'Ngừng + xóa mềm', @@ROWCOUNT);
UPDATE dbo.Warehouse SET Status = N'Inactive', IsDeleted = 1, DeletedDate = @Now, ModifiedDate = @Now, ModifiedBy = @SystemUser WHERE ISNULL(IsDeleted, 0) = 0 AND Name LIKE @Like ESCAPE N'\';
INSERT INTO @R VALUES (N'Warehouse', N'Ngừng + xóa mềm', @@ROWCOUNT);
UPDATE dbo.Vehicle SET Status = N'Inactive', IsDeleted = 1, DeletedDate = @Now, ModifiedDate = @Now, ModifiedBy = @SystemUser WHERE ISNULL(IsDeleted, 0) = 0 AND LicensePlate LIKE @Like ESCAPE N'\';
INSERT INTO @R VALUES (N'Vehicle', N'Ngừng + xóa mềm', @@ROWCOUNT);
UPDATE dbo.ProductionLine SET Status = N'Inactive', IsDeleted = 1, ModifiedDate = @Now, ModifiedBy = @SystemUser WHERE ISNULL(IsDeleted, 0) = 0 AND Name LIKE @Like ESCAPE N'\';
INSERT INTO @R VALUES (N'ProductionLine', N'Ngừng + xóa mềm', @@ROWCOUNT);
UPDATE dbo.UnitMeasure SET Status = N'Inactive', IsDeleted = 1, DeletedDate = @Now, ModifiedDate = @Now, ModifiedBy = @SystemUser WHERE ISNULL(IsDeleted, 0) = 0 AND Name LIKE @Like ESCAPE N'\';
INSERT INTO @R VALUES (N'UnitMeasure', N'Ngừng + xóa mềm', @@ROWCOUNT);
UPDATE e SET e.Status = N'Inactive', e.IsDeleted = 1, e.DeletedDate = @Now, e.ModifiedDate = @Now, e.ModifiedBy = @SystemUser
FROM dbo.Employee AS e
WHERE ISNULL(e.IsDeleted, 0) = 0 AND e.FullName LIKE @Like ESCAPE N'\'
  AND NOT EXISTS (SELECT 1 FROM dbo.[User] AS u WHERE ISNULL(u.IsDeleted, 0) = 0 AND u.PersonId IS NOT NULL AND u.PersonId = e.PersonId
                  AND (u.UserName LIKE N'uat.%' OR u.UserName IN (N'admin', N'system')));
INSERT INTO @R VALUES (N'Employee', N'Ngừng + xóa mềm (trừ NV gắn tài khoản uat.*)', @@ROWCOUNT);

INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), @SystemUser, N'Modified', r.Bang, CONCAT(N'- ', @Why, N': ', r.ViecLam, N' ', r.SoDong, N' dòng'), @Now, @SystemUser, 0 FROM @R AS r WHERE r.SoDong > 0;

SELECT Bang AS [Bảng], ViecLam AS [Việc], SoDong AS [Số dòng] FROM @R;
SELECT SUM(SoDong) AS [Tổng], CASE WHEN @DryRun = 1 THEN N'CHẠY THỬ – đã hoàn tác' ELSE N'ĐÃ GHI' END AS [Kết quả] FROM @R;
IF @DryRun = 1 ROLLBACK TRANSACTION; ELSE COMMIT TRANSACTION;
