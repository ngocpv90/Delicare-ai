/*
  REF-1 (GAP-REF-01, anh chốt 2026-11-22) – script DỮ LIỆU một lần (không tạo / sửa bảng, cột; không DELETE).
  Chuẩn hóa cột mã danh mục tham chiếu về MÃ của danh mục ReferenceLists: giá trị khớp mã khác hoa / thường hoặc khớp NHÃN
  (vd. "trua", "Trưa" → TRUA; "Kho lạnh" → COLD; "zalo" → ZALO). Giá trị KHÔNG khớp mã / nhãn nào: KHÔNG sửa – chỉ liệt kê để xử lý tay
  (ứng dụng vẫn mở / sửa được bản ghi, giữ giá trị cũ khi không đổi cột đó). Chạy lại lần 2 không đổi gì (idempotent).
  Danh mục + cột chép từ appsettings.json (ReferenceLists) và Model/Helper/ReferenceCatalogs.cs lúc sinh script (2026-11-22) –
  đổi danh mục / thêm cột thì cập nhật 2 bảng #Cat, #Col bên dưới cho khớp.
  Loại khay / hộp đã ghi tự do (vd. "[DEMO] hộp giữ nhiệt") KHÔNG đổi: số khay còn ở khách + phiếu thu hồi tính theo đúng chuỗi đó.

  Bắt buộc truyền -v DryRun=1 (chạy thử) hoặc -v DryRun=0 (chạy thật).
  CÁCH CHẠY (chỉ sau khi backup):
    Chạy thử (in bảng đối chiếu + giá trị ngoài danh mục, KHÔNG ghi):
      sqlcmd -S <server> -d <db> -E -f 65001 -v DryRun=1 -i DB/scripts/REF1_normalize_catalog_codes.sql -s "," -W -o ref1-preview.csv
    Chạy thật:
      sqlcmd -S <server> -d <db> -E -f 65001 -v DryRun=0 -i DB/scripts/REF1_normalize_catalog_codes.sql -s "," -W -o ref1-applied.csv
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @DryRun BIT = $(DryRun);
DECLARE @SystemUser UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.[User] WHERE UserName = N'system' AND ISNULL(IsDeleted, 0) = 0);
IF @SystemUser IS NULL
    THROW 50111, N'Không có tài khoản "system" (seed T23) – không ghi được người thao tác.', 1;
DECLARE @Now DATETIME = DATEADD(HOUR, 7, SYSUTCDATETIME()); -- giờ Việt Nam như ứng dụng

CREATE TABLE #Cat (ListName NVARCHAR(100), Code NVARCHAR(100), Label NVARCHAR(255));
INSERT INTO #Cat (ListName, Code, Label) VALUES
  (N'MealType', N'SANG', N'Sáng'),
  (N'MealType', N'TRUA', N'Trưa'),
  (N'MealType', N'TOI', N'Tối'),
  (N'MealType', N'DEM', N'Đêm'),
  (N'LeaveType', N'ANNUAL', N'Nghỉ phép năm'),
  (N'LeaveType', N'SICK', N'Nghỉ ốm'),
  (N'LeaveType', N'PERSONAL', N'Việc riêng (có lương)'),
  (N'LeaveType', N'UNPAID', N'Nghỉ không lương'),
  (N'LeaveType', N'MATERNITY', N'Thai sản'),
  (N'LeaveType', N'OTHER', N'Khác'),
  (N'AttendanceSource', N'GPS', N'GPS (điện thoại)'),
  (N'AttendanceSource', N'QR', N'Quét QR tại bếp'),
  (N'AttendanceSource', N'MACHINE', N'Máy chấm công'),
  (N'AttendanceSource', N'PROXY', N'Chấm hộ'),
  (N'WarehouseType', N'RAW', N'Nguyên liệu'),
  (N'WarehouseType', N'FINISHED', N'Thành phẩm'),
  (N'WarehouseType', N'PACKAGING', N'Bao bì'),
  (N'WarehouseType', N'COLD', N'Kho lạnh'),
  (N'WarehouseType', N'KITCHEN', N'Kho bếp'),
  (N'StorageType', N'AMBIENT', N'Nhiệt độ thường'),
  (N'StorageType', N'CHILLED', N'Mát'),
  (N'StorageType', N'FROZEN', N'Đông lạnh'),
  (N'StorageType', N'DRY', N'Khô'),
  (N'SupplierType', N'MEAT', N'Thịt'),
  (N'SupplierType', N'VEGETABLE', N'Rau củ'),
  (N'SupplierType', N'DRY_GOODS', N'Hàng khô'),
  (N'SupplierType', N'PACKAGING', N'Bao bì'),
  (N'SupplierType', N'OTHER', N'Khác'),
  (N'WasteType', N'OVERPRODUCTION', N'Nấu dư'),
  (N'WasteType', N'QC_REJECT', N'QC loại'),
  (N'WasteType', N'SPOILAGE', N'Hư hỏng'),
  (N'WasteType', N'PREP_LOSS', N'Hao hụt sơ chế'),
  (N'WasteType', N'RETURNED', N'Khách trả lại'),
  (N'WasteType', N'PACKING_DAMAGE', N'Hỏng / loại khi đóng gói'),
  (N'TemperatureMeasurementType', N'STORAGE', N'Kho / tủ bảo quản'),
  (N'TemperatureMeasurementType', N'HOT_HOLDING', N'Giữ nóng thành phẩm'),
  (N'TemperatureMeasurementType', N'BLAST_CHILL', N'Làm lạnh nhanh'),
  (N'TemperatureMeasurementType', N'PACKING_AREA', N'Khu chia suất / đóng gói'),
  (N'TemperatureMeasurementType', N'AMBIENT', N'Nhiệt độ phòng'),
  (N'PackagingType', N'KHAY', N'Khay inox'),
  (N'PackagingType', N'HOP_GIU_NHIET', N'Hộp giữ nhiệt'),
  (N'PackagingType', N'THUNG_GIU_NHIET', N'Thùng giữ nhiệt'),
  (N'PackagingTransactionType', N'OUT', N'Giao cho khách'),
  (N'PackagingTransactionType', N'IN', N'Khách trả'),
  (N'PackagingTransactionType', N'LOSS', N'Thất thoát'),
  (N'PackagingTransactionType', N'AVAILABLE', N'Sẵn sàng dùng lại'),
  (N'ComplaintChannel', N'PHONE', N'Điện thoại'),
  (N'ComplaintChannel', N'ZALO', N'Zalo'),
  (N'ComplaintChannel', N'EMAIL', N'Email'),
  (N'ComplaintChannel', N'DIRECT', N'Trực tiếp'),
  (N'ComplaintChannel', N'TELEGRAM', N'Telegram (bot)'),
  (N'ComplaintChannel', N'POD', N'Biên bản giao nhận'),
  (N'ComplaintCategory', N'QUALITY', N'Chất lượng món'),
  (N'ComplaintCategory', N'SHORTAGE', N'Thiếu suất'),
  (N'ComplaintCategory', N'LATE', N'Giao trễ'),
  (N'ComplaintCategory', N'HYGIENE', N'Vệ sinh / an toàn thực phẩm'),
  (N'ComplaintCategory', N'PACKAGING', N'Bao bì / khay'),
  (N'ComplaintCategory', N'SERVICE', N'Thái độ phục vụ'),
  (N'ComplaintCategory', N'OTHER', N'Khác');

CREATE TABLE #Col (Tbl SYSNAME, Col SYSNAME, ListName NVARCHAR(100), Entity NVARCHAR(100), ColLabel NVARCHAR(255));
INSERT INTO #Col (Tbl, Col, ListName, Entity, ColLabel) VALUES
  (N'ContractMeal', N'MealType', N'MealType', N'ContractMeal', N'Loại suất'),
  (N'Forecast', N'MealType', N'MealType', N'Forecast', N'Loại suất'),
  (N'MenuLine', N'MealType', N'MealType', N'MenuLine', N'Loại suất'),
  (N'MenuAssignment', N'MealType', N'MealType', N'MenuAssignment', N'Loại suất'),
  (N'OrderLine', N'MealType', N'MealType', N'OrderLine', N'Loại suất'),
  (N'PricePolicie', N'MealType', N'MealType', N'PricePolicy', N'Loại suất'),
  (N'LeaveRequest', N'LeaveTypeCode', N'LeaveType', N'LeaveRequest', N'Loại nghỉ'),
  (N'AttendanceEvent', N'SourceChannel', N'AttendanceSource', N'AttendanceEvent', N'Hình thức chấm công'),
  (N'Warehouse', N'WarehouseType', N'WarehouseType', N'Warehouse', N'Loại kho'),
  (N'Ingredient', N'StorageType', N'StorageType', N'Ingredient', N'Kiểu bảo quản'),
  (N'Supplier', N'SupplierType', N'SupplierType', N'Supplier', N'Loại nhà cung cấp'),
  (N'MealWasteRecord', N'WasteType', N'WasteType', N'MealWasteRecord', N'Loại hủy'),
  (N'TemperatureLog', N'MeasurementType', N'TemperatureMeasurementType', N'TemperatureLog', N'Loại đo nhiệt độ'),
  (N'PackingResult', N'PackagingType', N'PackagingType', N'PackingResult', N'Loại khay / hộp'),
  (N'PackagingLedger', N'PackagingType', N'PackagingType', N'PackagingLedger', N'Loại khay / hộp'),
  (N'PackagingLedger', N'TransactionType', N'PackagingTransactionType', N'PackagingLedger', N'Loại phát sinh bao bì'),
  (N'CustomerComplaint', N'SourceChannel', N'ComplaintChannel', N'CustomerComplaint', N'Kênh tiếp nhận'),
  (N'CustomerComplaint', N'CategoryCode', N'ComplaintCategory', N'CustomerComplaint', N'Phân loại phản ánh');

CREATE TABLE #Map (Tbl SYSNAME, Col SYSNAME, Entity NVARCHAR(100), ColLabel NVARCHAR(255), Id UNIQUEIDENTIFIER, OldValue NVARCHAR(500), NewValue NVARCHAR(100));
CREATE TABLE #Unmatched (Tbl SYSNAME, Col SYSNAME, ColLabel NVARCHAR(255), Value NVARCHAR(500), Rows INT);

DECLARE @Tbl SYSNAME, @Col SYSNAME, @List NVARCHAR(100), @Entity NVARCHAR(100), @ColLabel NVARCHAR(255), @Sql NVARCHAR(MAX);
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT Tbl, Col, ListName, Entity, ColLabel FROM #Col;
OPEN c;
FETCH NEXT FROM c INTO @Tbl, @Col, @List, @Entity, @ColLabel;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF COL_LENGTH(N'dbo.' + @Tbl, @Col) IS NOT NULL
    BEGIN
        -- Khớp mã (không phân biệt hoa thường, bỏ khoảng trắng 2 đầu) hoặc khớp nhãn; chỉ lấy dòng có giá trị khác mã chuẩn (so nhị phân).
        SET @Sql = N'INSERT INTO #Map (Tbl, Col, Entity, ColLabel, Id, OldValue, NewValue)
            SELECT @Tbl, @Col, @Entity, @ColLabel, t.Id, t.' + QUOTENAME(@Col) + N', k.Code
            FROM dbo.' + QUOTENAME(@Tbl) + N' AS t
            CROSS APPLY (SELECT TOP 1 x.Code FROM #Cat AS x WHERE x.ListName = @List
                         AND (UPPER(LTRIM(RTRIM(t.' + QUOTENAME(@Col) + N'))) = UPPER(x.Code) COLLATE Latin1_General_CI_AS
                              OR LTRIM(RTRIM(t.' + QUOTENAME(@Col) + N')) = x.Label COLLATE Vietnamese_CI_AS)
                         ORDER BY CASE WHEN UPPER(LTRIM(RTRIM(t.' + QUOTENAME(@Col) + N'))) = UPPER(x.Code) COLLATE Latin1_General_CI_AS THEN 0 ELSE 1 END) AS k
            WHERE ISNULL(t.IsDeleted, 0) = 0 AND t.' + QUOTENAME(@Col) + N' IS NOT NULL
              AND t.' + QUOTENAME(@Col) + N' COLLATE Latin1_General_BIN <> k.Code COLLATE Latin1_General_BIN;
            INSERT INTO #Unmatched (Tbl, Col, ColLabel, Value, Rows)
            SELECT @Tbl, @Col, @ColLabel, t.' + QUOTENAME(@Col) + N', COUNT(*)
            FROM dbo.' + QUOTENAME(@Tbl) + N' AS t
            WHERE ISNULL(t.IsDeleted, 0) = 0 AND NULLIF(LTRIM(RTRIM(t.' + QUOTENAME(@Col) + N')), N'''') IS NOT NULL
              AND NOT EXISTS (SELECT 1 FROM #Cat AS x WHERE x.ListName = @List
                         AND (UPPER(LTRIM(RTRIM(t.' + QUOTENAME(@Col) + N'))) = UPPER(x.Code) COLLATE Latin1_General_CI_AS
                              OR LTRIM(RTRIM(t.' + QUOTENAME(@Col) + N')) = x.Label COLLATE Vietnamese_CI_AS))
            GROUP BY t.' + QUOTENAME(@Col) + N';';
        EXEC sp_executesql @Sql, N'@Tbl SYSNAME, @Col SYSNAME, @Entity NVARCHAR(100), @ColLabel NVARCHAR(255), @List NVARCHAR(100)', @Tbl, @Col, @Entity, @ColLabel, @List;
    END
    FETCH NEXT FROM c INTO @Tbl, @Col, @List, @Entity, @ColLabel;
END
CLOSE c; DEALLOCATE c;
-- Loại đo nhiệt độ do nghiệp vụ tự ghi (lõi mẻ nấu / lúc giao / nhận hàng – *Status.cs), không nhập tay → không phải giá trị lỗi.
DELETE FROM #Unmatched WHERE Tbl = N'TemperatureLog' AND Col = N'MeasurementType' AND Value IN (N'CORE', N'DELIVERY', N'RECEIVING');

-- 1) Sẽ đổi (gom theo cột + giá trị).
SELECT m.Tbl AS [Bảng], m.Col AS [Cột], m.OldValue AS [Giá trị cũ], m.NewValue AS [Mã mới], COUNT(*) AS [Số dòng]
FROM #Map AS m GROUP BY m.Tbl, m.Col, m.OldValue, m.NewValue ORDER BY m.Tbl, m.Col, m.OldValue;
-- 2) Ngoài danh mục – KHÔNG đổi, xử lý tay (sửa trên màn hình hoặc bổ sung danh mục nếu đúng nghiệp vụ).
SELECT u.Tbl AS [Bảng], u.Col AS [Cột], u.ColLabel AS [Trường], u.Value AS [Giá trị ngoài danh mục], u.Rows AS [Số dòng]
FROM #Unmatched AS u ORDER BY u.Tbl, u.Col, u.Rows DESC;

IF @DryRun = 1
BEGIN
    SELECT N'CHẠY THỬ' AS [Chế độ], (SELECT COUNT(*) FROM #Map) AS [Sẽ đổi], (SELECT ISNULL(SUM(Rows), 0) FROM #Unmatched) AS [Ngoài danh mục];
    PRINT N'Chạy thử: không ghi gì. Chạy thật: thêm -v DryRun=0 (sau khi backup).';
    RETURN;
END

BEGIN TRANSACTION;
DECLARE u CURSOR LOCAL FAST_FORWARD FOR SELECT DISTINCT Tbl, Col FROM #Map;
OPEN u;
FETCH NEXT FROM u INTO @Tbl, @Col;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Sql = N'UPDATE t SET t.' + QUOTENAME(@Col) + N' = m.NewValue'
        + CASE WHEN COL_LENGTH(N'dbo.' + @Tbl, N'ModifiedDate') IS NOT NULL THEN N', t.ModifiedDate = @Now' ELSE N'' END
        + CASE WHEN COL_LENGTH(N'dbo.' + @Tbl, N'ModifiedBy') IS NOT NULL THEN N', t.ModifiedBy = @SystemUser' ELSE N'' END
        + N' FROM dbo.' + QUOTENAME(@Tbl) + N' AS t JOIN #Map AS m ON m.Id = t.Id AND m.Tbl = @Tbl AND m.Col = @Col;';
    EXEC sp_executesql @Sql, N'@Tbl SYSNAME, @Col SYSNAME, @Now DATETIME, @SystemUser UNIQUEIDENTIFIER', @Tbl, @Col, @Now, @SystemUser;
    FETCH NEXT FROM u INTO @Tbl, @Col;
END
CLOSE u; DEALLOCATE u;

INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), m.Id, N'Modified', m.Entity,
       N'- Actor: SYSTEM:' + LOWER(CAST(@SystemUser AS NVARCHAR(36))) + N' | REF-1 chuẩn hóa mã danh mục <br />' + CHAR(13) + CHAR(10)
       + N'- ' + m.Col + N': ''' + m.OldValue + N''' → ''' + m.NewValue + N''' <br />',
       @Now, @SystemUser, 0
FROM #Map AS m;

COMMIT TRANSACTION;
SELECT N'ĐÃ ÁP DỤNG' AS [Chế độ], (SELECT COUNT(*) FROM #Map) AS [Đã đổi], (SELECT ISNULL(SUM(Rows), 0) FROM #Unmatched) AS [Ngoài danh mục – xử lý tay];
PRINT N'Đã áp dụng. Chạy lại lần 2 sẽ không đổi gì.';
