/*
  REAL-1 (9) (D-129): gắn nhãn dữ liệu thử "[DEMO] REAL-1" (CLAUDE.md §3.1-10) cho mọi bản ghi đã nạp từ bộ Chuan-hoa-REAL1 – nhận theo MÃ / phạm vi nạp:
    NCC mã NCC-…, nguyên liệu + nhóm NL còn dùng (không tên [DEMO]), món mã MON-nnnn + công thức + nhóm món chứa món đó, khách mã KHnnnnn + hợp đồng /
    đơn / dự báo của khách đó, 3 kho thật (Kho khô / mát / đông), 4 dây chuyền DC-…, thực đơn chung có ghi chú duyệt "REAL-1:" (3 tuần), lô tồn đầu kỳ "REAL-1 TDK …".
  Quyết định tự chọn: GHÉP nhãn vào ĐẦU Note ("[DEMO] REAL-1 · <ghi chú cũ>") thay vì ghi đè – giữ ghi chú giả định đã ghi lúc nạp. Đã có nhãn → bỏ qua (chạy lại = 0).
  Không có cột Note: ĐVT (UnitMeasure), điểm giao (CustomerDeliveryLocation), bảng giá NCC (SupplierMaterial), suất hợp đồng (ContractMeal) – nhận diện qua mã.
  KHÔNG đụng sổ chỉ ghi thêm (InventoryTransaction, AuditChange…). Ghi ModifiedDate / ModifiedBy (system) + 1 dòng AuditChange / bảng.
  -v DryRun=1 (thử) / 0 (thật).
    sqlcmd -S <server> -d <db> -E -b -f 65001 -v DryRun=1 -i DB/scripts/REAL1_tag.sql -s "|" -W
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
DECLARE @DryRun BIT = $(DryRun);
DECLARE @SystemUser UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.[User] WHERE UserName = N'system' AND ISNULL(IsDeleted, 0) = 0);
IF @SystemUser IS NULL
    THROW 50102, N'Không có tài khoản "system" (seed T23) – không ghi được người thao tác.', 1;
DECLARE @Now DATETIME = DATEADD(HOUR, 7, SYSUTCDATETIME());
DECLARE @Tag NVARCHAR(20) = N'[DEMO] REAL-1';
DECLARE @Has NVARCHAR(30) = N'\[DEMO\] REAL-1%';   -- đã gắn nhãn
DECLARE @Demo NVARCHAR(20) = N'\[DEMO\]%';         -- tên dữ liệu demo cũ (không phải REAL-1)
DECLARE @R TABLE (Bang NVARCHAR(60), SoDong INT);

BEGIN TRANSACTION;
DECLARE @Cust TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY);
INSERT INTO @Cust SELECT Id FROM dbo.Customer
WHERE ISNULL(IsDeleted, 0) = 0 AND Code LIKE N'KH[0-9][0-9][0-9][0-9][0-9]' AND Name NOT LIKE @Demo ESCAPE N'\';
DECLARE @Dish TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY, CategoryId UNIQUEIDENTIFIER NULL);
INSERT INTO @Dish SELECT Id, ISNULL(DishCategoryId, CategoryId) FROM dbo.Dishe WHERE ISNULL(IsDeleted, 0) = 0 AND Code LIKE N'MON-[0-9][0-9][0-9][0-9]';

UPDATE dbo.Supplier SET Note = CASE WHEN ISNULL(Note, N'') = N'' THEN @Tag ELSE CONCAT(@Tag, N' · ', Note) END, ModifiedDate = @Now, ModifiedBy = @SystemUser
WHERE ISNULL(IsDeleted, 0) = 0 AND Code LIKE N'NCC-%' AND Name NOT LIKE @Demo ESCAPE N'\' AND ISNULL(Note, N'') NOT LIKE @Has ESCAPE N'\';
INSERT INTO @R VALUES (N'Supplier', @@ROWCOUNT);
UPDATE dbo.Ingredient SET Note = CASE WHEN ISNULL(Note, N'') = N'' THEN @Tag ELSE CONCAT(@Tag, N' · ', Note) END, ModifiedDate = @Now, ModifiedBy = @SystemUser
WHERE ISNULL(IsDeleted, 0) = 0 AND Name NOT LIKE @Demo ESCAPE N'\' AND ISNULL(Note, N'') NOT LIKE @Has ESCAPE N'\';
INSERT INTO @R VALUES (N'Ingredient', @@ROWCOUNT);
UPDATE g SET g.Note = CASE WHEN ISNULL(g.Note, N'') = N'' THEN @Tag ELSE CONCAT(@Tag, N' · ', g.Note) END, g.ModifiedDate = @Now, g.ModifiedBy = @SystemUser
FROM dbo.IngredientCategorie AS g
WHERE ISNULL(g.IsDeleted, 0) = 0 AND g.Name NOT LIKE @Demo ESCAPE N'\' AND ISNULL(g.Note, N'') NOT LIKE @Has ESCAPE N'\'
  AND EXISTS (SELECT 1 FROM dbo.Ingredient AS i WHERE ISNULL(i.IsDeleted, 0) = 0 AND i.IngredientCategoryId = g.Id AND i.Name NOT LIKE @Demo ESCAPE N'\');
INSERT INTO @R VALUES (N'IngredientCategorie', @@ROWCOUNT);
UPDATE d SET d.Note = CASE WHEN ISNULL(d.Note, N'') = N'' THEN @Tag ELSE CONCAT(@Tag, N' · ', d.Note) END, d.ModifiedDate = @Now, d.ModifiedBy = @SystemUser
FROM dbo.Dishe AS d JOIN @Dish AS x ON x.Id = d.Id WHERE ISNULL(d.Note, N'') NOT LIKE @Has ESCAPE N'\';
INSERT INTO @R VALUES (N'Dishe', @@ROWCOUNT);
UPDATE g SET g.Note = CASE WHEN ISNULL(g.Note, N'') = N'' THEN @Tag ELSE CONCAT(@Tag, N' · ', g.Note) END, g.ModifiedDate = @Now, g.ModifiedBy = @SystemUser
FROM dbo.DishCategorie AS g WHERE ISNULL(g.IsDeleted, 0) = 0 AND g.Id IN (SELECT CategoryId FROM @Dish) AND ISNULL(g.Note, N'') NOT LIKE @Has ESCAPE N'\';
INSERT INTO @R VALUES (N'DishCategorie', @@ROWCOUNT);
UPDATE r SET r.Note = CASE WHEN ISNULL(r.Note, N'') = N'' THEN @Tag ELSE CONCAT(@Tag, N' · ', r.Note) END, r.ModifiedDate = @Now, r.ModifiedBy = @SystemUser
FROM dbo.Recipe AS r JOIN @Dish AS x ON x.Id = r.DishId WHERE ISNULL(r.IsDeleted, 0) = 0 AND ISNULL(r.Note, N'') NOT LIKE @Has ESCAPE N'\';
INSERT INTO @R VALUES (N'Recipe', @@ROWCOUNT);
UPDATE c SET c.Note = CASE WHEN ISNULL(c.Note, N'') = N'' THEN @Tag ELSE CONCAT(@Tag, N' · ', c.Note) END, c.ModifiedDate = @Now, c.ModifiedBy = @SystemUser
FROM dbo.Customer AS c JOIN @Cust AS x ON x.Id = c.Id WHERE ISNULL(c.Note, N'') NOT LIKE @Has ESCAPE N'\';
INSERT INTO @R VALUES (N'Customer', @@ROWCOUNT);
UPDATE k SET k.Note = CASE WHEN ISNULL(k.Note, N'') = N'' THEN @Tag ELSE CONCAT(@Tag, N' · ', k.Note) END, k.ModifiedDate = @Now, k.ModifiedBy = @SystemUser
FROM dbo.Contract AS k JOIN @Cust AS x ON x.Id = k.CustomerId WHERE ISNULL(k.IsDeleted, 0) = 0 AND ISNULL(k.Note, N'') NOT LIKE @Has ESCAPE N'\';
INSERT INTO @R VALUES (N'Contract', @@ROWCOUNT);
UPDATE o SET o.Note = CASE WHEN ISNULL(o.Note, N'') = N'' THEN @Tag ELSE CONCAT(@Tag, N' · ', o.Note) END, o.ModifiedDate = @Now, o.ModifiedBy = @SystemUser
FROM dbo.[Order] AS o JOIN @Cust AS x ON x.Id = o.CustomerId WHERE ISNULL(o.IsDeleted, 0) = 0 AND ISNULL(o.Note, N'') NOT LIKE @Has ESCAPE N'\';
INSERT INTO @R VALUES (N'Order', @@ROWCOUNT);
UPDATE f SET f.Note = CASE WHEN ISNULL(f.Note, N'') = N'' THEN @Tag ELSE CONCAT(@Tag, N' · ', f.Note) END, f.ModifiedDate = @Now, f.ModifiedBy = @SystemUser
FROM dbo.Forecast AS f JOIN @Cust AS x ON x.Id = f.CustomerId WHERE ISNULL(f.IsDeleted, 0) = 0 AND ISNULL(f.Note, N'') NOT LIKE @Has ESCAPE N'\';
INSERT INTO @R VALUES (N'Forecast', @@ROWCOUNT);
UPDATE dbo.Warehouse SET Note = CASE WHEN ISNULL(Note, N'') = N'' THEN @Tag ELSE CONCAT(@Tag, N' · ', Note) END, ModifiedDate = @Now, ModifiedBy = @SystemUser
WHERE ISNULL(IsDeleted, 0) = 0 AND Name IN (N'Kho khô', N'Kho mát', N'Kho đông') AND ISNULL(Note, N'') NOT LIKE @Has ESCAPE N'\';
INSERT INTO @R VALUES (N'Warehouse', @@ROWCOUNT);
UPDATE dbo.ProductionLine SET Note = LEFT(CASE WHEN ISNULL(Note, N'') = N'' THEN @Tag ELSE CONCAT(@Tag, N' · ', Note) END, 1000), ModifiedDate = @Now, ModifiedBy = @SystemUser
WHERE ISNULL(IsDeleted, 0) = 0 AND Code IN (N'DC-NAU-CHINH', N'DC-RAU-CANH', N'DC-SANG-DEM', N'DC-PHU-TM') AND ISNULL(Note, N'') NOT LIKE @Has ESCAPE N'\';
INSERT INTO @R VALUES (N'ProductionLine', @@ROWCOUNT);
UPDATE dbo.Menu SET Note = CASE WHEN ISNULL(Note, N'') = N'' THEN @Tag ELSE CONCAT(@Tag, N' · ', Note) END, ModifiedDate = @Now, ModifiedBy = @SystemUser
WHERE ISNULL(IsDeleted, 0) = 0 AND MenuType = N'COMMON' AND Note LIKE N'%REAL-1:%' AND Name NOT LIKE @Demo ESCAPE N'\'
  AND ISNULL(Note, N'') NOT LIKE @Has ESCAPE N'\';   -- nhận theo ghi chú duyệt của tools/real1/06-menus (tuần dời được bằng REAL1_START)
INSERT INTO @R VALUES (N'Menu', @@ROWCOUNT);
UPDATE dbo.MaterialLot SET Note = CONCAT(@Tag, N' · ', Note), ModifiedDate = @Now, ModifiedBy = @SystemUser
WHERE ISNULL(IsDeleted, 0) = 0 AND Note LIKE N'REAL-1 TDK %';
INSERT INTO @R VALUES (N'MaterialLot', @@ROWCOUNT);

INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), @SystemUser, N'Modified', r.Bang, CONCAT(N'- REAL-1: gắn nhãn "', @Tag, N'" vào Note ', r.SoDong, N' dòng'), @Now, @SystemUser, 0 FROM @R AS r WHERE r.SoDong > 0;

SELECT Bang AS [Bảng], SoDong AS [Số dòng gắn nhãn] FROM @R;
SELECT SUM(SoDong) AS [Tổng], CASE WHEN @DryRun = 1 THEN N'CHẠY THỬ – đã hoàn tác' ELSE N'ĐÃ GHI' END AS [Kết quả] FROM @R;
IF @DryRun = 1 ROLLBACK TRANSACTION; ELSE COMMIT TRANSACTION;
