-- BE2.4 – Danh mục công đoạn sản xuất theo tổ (T17, ORG_STRUCTURE.md: Sơ chế → Nấu → Chia suất / Đóng gói; tổ Cơm / Canh dùng công đoạn Nấu với tổ OPS-KIT-RICE).
-- Không có bảng công đoạn (GAP-PRD-09): công đoạn = mục của checklist PRD_STAGE (ItemCode, SortOrder = thứ tự, IsRequired); sự kiện bắt đầu / xong ghi
-- FoodSafetyChecklistResult (ProductionBatch/StartStage, FinishStage). Tổ mặc định: BusinessRules:ProductionStageTeams.
-- Dữ liệu cấu hình (không phải DEMO). PROPOSED – BA/PO CONFIRM (DECISIONS D-53 e): mã, tên, thứ tự công đoạn.
-- Idempotent theo Code / ItemCode: chưa có thì thêm; đã có thì KHÔNG ghi đè (giữ chỉnh sửa của người dùng). Không DELETE.
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @SeedBy UNIQUEIDENTIFIER = (SELECT TOP (1) Id FROM [dbo].[User] WHERE UserName = N'admin' AND IsDeleted = 0);
IF @SeedBy IS NULL THROW 50001, N'Không có tài khoản admin để ghi CreatedBy cho seed.', 1;
DECLARE @Now DATETIME = GETDATE();

BEGIN TRAN;
IF NOT EXISTS (SELECT 1 FROM [dbo].[FoodSafetyChecklist] WHERE Code = N'PRD_STAGE' AND IsDeleted = 0)
    INSERT INTO [dbo].[FoodSafetyChecklist] (Id, Code, Name, VersionNo, AreaCode, FrequencyCode, EffectiveFrom, Status, Note, CreatedDate, CreatedBy, IsDeleted)
    VALUES (NEWID(), N'PRD_STAGE', N'Công đoạn sản xuất theo tổ', 1, N'KITCHEN', N'BATCH', CAST(@Now AS DATE), N'Active',
            N'BE2.4 – PROPOSED (D-53 e). Mỗi mục = 1 công đoạn của mẻ, SortOrder = thứ tự; ghi qua ProductionBatch/StartStage – FinishStage.', @Now, @SeedBy, 0);

DECLARE @ChecklistId UNIQUEIDENTIFIER = (SELECT TOP (1) Id FROM [dbo].[FoodSafetyChecklist] WHERE Code = N'PRD_STAGE' AND IsDeleted = 0 ORDER BY VersionNo DESC);

DECLARE @I TABLE (ItemCode NVARCHAR(50), ItemName NVARCHAR(500), Description NVARCHAR(1000), SortOrder INT, IsRequired BIT);
INSERT INTO @I VALUES
    (N'SO_CHE', N'Sơ chế', N'Tổ Sơ chế (OPS-KIT-PREP): nhận nguyên liệu đã xuất kho, sơ chế, chuyển tổ Nấu.', 1, 1),
    (N'NAU', N'Nấu', N'Tổ Nấu (OPS-KIT-COOK) hoặc tổ Cơm / Canh (OPS-KIT-RICE): bắt đầu Nấu = bắt đầu mẻ; xong Nấu mới nấu xong mẻ (đo nhiệt độ lõi ≥ 70 °C).', 2, 1),
    (N'CHIA_SUAT', N'Chia suất / Đóng gói', N'Tổ Chia suất / Đóng gói (OPS-KIT-PORT): chỉ khi mẻ Đạt QC; xong Chia suất mới đóng gói mẻ.', 3, 1);

INSERT INTO [dbo].[FoodSafetyChecklistItem] (Id, ChecklistId, ItemCode, ItemName, Description, SortOrder, IsCritical, ExpectedValue, UnitCode, IsRequired, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), @ChecklistId, i.ItemCode, i.ItemName, i.Description, i.SortOrder, 0, NULL, NULL, i.IsRequired, @Now, @SeedBy, 0
FROM @I i
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[FoodSafetyChecklistItem] x WHERE x.ChecklistId = @ChecklistId AND x.ItemCode = i.ItemCode AND x.IsDeleted = 0);
COMMIT;
