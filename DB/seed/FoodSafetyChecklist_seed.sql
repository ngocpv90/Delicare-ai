-- T17 – Checklist kiểm soát mẻ nấu / thành phẩm (BUSINESS_RULES §6: "Mỗi mẻ phải lưu mẫu thức ăn … ChecklistItem mã SAMPLE_RETAINED. Seed checklist item này (PROPOSED)").
-- Dữ liệu cấu hình (không phải DEMO). PROPOSED – BA/PO CONFIRM: mã bộ PRD_BATCH, tên mục, tần suất theo mẻ.
-- Idempotent theo Code / ItemCode: chưa có thì thêm; đã có thì KHÔNG ghi đè (giữ chỉnh sửa của người dùng). Không DELETE.
-- Mục CORE_TEMP chỉ để hiển thị trên checklist giấy / màn hình – nhiệt độ lõi ghi ở TemperatureLog (MeasurementType = CORE), không ở bảng kết quả checklist.
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @SeedBy UNIQUEIDENTIFIER = (SELECT TOP (1) Id FROM [dbo].[User] WHERE UserName = N'admin' AND IsDeleted = 0);
IF @SeedBy IS NULL THROW 50001, N'Không có tài khoản admin để ghi CreatedBy cho seed.', 1;
DECLARE @Now DATETIME = GETDATE();

BEGIN TRAN;
IF NOT EXISTS (SELECT 1 FROM [dbo].[FoodSafetyChecklist] WHERE Code = N'PRD_BATCH' AND IsDeleted = 0)
    INSERT INTO [dbo].[FoodSafetyChecklist] (Id, Code, Name, VersionNo, AreaCode, FrequencyCode, EffectiveFrom, Status, Note, CreatedDate, CreatedBy, IsDeleted)
    VALUES (NEWID(), N'PRD_BATCH', N'Kiểm soát mẻ nấu / thành phẩm', 1, N'KITCHEN', N'BATCH', CAST(@Now AS DATE), N'Active',
            N'T17 – PROPOSED (BUSINESS_RULES §6). Lưu mẫu ghi qua ProductionBatch/RecordSampleRetained.', @Now, @SeedBy, 0);

DECLARE @ChecklistId UNIQUEIDENTIFIER = (SELECT TOP (1) Id FROM [dbo].[FoodSafetyChecklist] WHERE Code = N'PRD_BATCH' AND IsDeleted = 0 ORDER BY VersionNo DESC);

DECLARE @I TABLE (ItemCode NVARCHAR(50), ItemName NVARCHAR(500), Description NVARCHAR(1000), SortOrder INT, IsCritical BIT, ExpectedValue NVARCHAR(500), UnitCode NVARCHAR(30));
INSERT INTO @I VALUES
    (N'SAMPLE_RETAINED', N'Lưu mẫu thức ăn của mẻ', N'Mỗi mẻ lưu mẫu (người lưu, giờ lưu, ảnh) – bắt buộc trước khi mẻ đạt QC (BusinessRules:ProductionRequireSampleBeforeRelease).', 1, 1, N'Đã lưu', NULL),
    (N'CORE_TEMP', N'Nhiệt độ lõi khi nấu xong', N'Ghi ở TemperatureLog (CORE). Dưới ngưỡng thì chặn đạt QC / đóng gói.', 2, 1, N'>= 70', N'C');

INSERT INTO [dbo].[FoodSafetyChecklistItem] (Id, ChecklistId, ItemCode, ItemName, Description, SortOrder, IsCritical, ExpectedValue, UnitCode, IsRequired, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), @ChecklistId, i.ItemCode, i.ItemName, i.Description, i.SortOrder, i.IsCritical, i.ExpectedValue, i.UnitCode, 1, @Now, @SeedBy, 0
FROM @I i
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[FoodSafetyChecklistItem] x WHERE x.ChecklistId = @ChecklistId AND x.ItemCode = i.ItemCode AND x.IsDeleted = 0);
COMMIT;
