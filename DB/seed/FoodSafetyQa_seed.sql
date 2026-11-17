-- BE2.5 – Checklist ATTP theo ca / mẻ (T18 phần nghiệp vụ, QA 4.3 / 4.4 ORG_STRUCTURE) + checklist vệ sinh khay / hộp 6 bước (T20, BUSINESS_RULES §8).
-- Ghi kết quả: FoodSafetyChecklistResult/Record (bộ ATTP), CleaningBatch/RecordResults (bộ VS-KHAY – kết quả ở CleaningChecklistResult).
-- Hạn tương đối theo mã bộ (BusinessRules:ChecklistOffsetsMinutes, tính từ giờ xuất bếp của ca): PREP −150′, PREP_AREA_CLEAN −90′, BLAST_CHILL −20′, KITCHEN_CLEAN +90′.
-- Tiêu chí IsCritical = 1 KHÔNG ĐẠT gắn mẻ → giữ mẻ (BusinessRules:FoodSafetyCriticalFailHoldsBatch).
-- Dữ liệu cấu hình (không phải DEMO). PROPOSED – BA/PO CONFIRM (DECISIONS D-54): mã bộ, tên / thứ tự / trọng yếu của tiêu chí – QA sửa trên màn checklist.
-- Idempotent theo Code / ItemCode: chưa có thì thêm; đã có thì KHÔNG ghi đè (giữ chỉnh sửa của người dùng). Không DELETE.
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @SeedBy UNIQUEIDENTIFIER = (SELECT TOP (1) Id FROM [dbo].[User] WHERE UserName = N'admin' AND IsDeleted = 0);
IF @SeedBy IS NULL THROW 50001, N'Không có tài khoản admin để ghi CreatedBy cho seed.', 1;
DECLARE @Now DATETIME = GETDATE();

DECLARE @C TABLE (Code NVARCHAR(50), Name NVARCHAR(255), AreaCode NVARCHAR(50), FrequencyCode NVARCHAR(30), Note NVARCHAR(1000));
INSERT INTO @C VALUES
    (N'VS-KHAY', N'Vệ sinh khay / hộp – 6 bước', N'PACKAGING', N'BATCH',
        N'BE2.5 – PROPOSED (D-54 f). Mỗi mục = 1 bước của mẻ vệ sinh VS-, đi lần lượt; ghi qua CleaningBatch/RecordResults, kết luận CleaningBatch/Complete.'),
    (N'QA_FG', N'Kiểm soát thành phẩm (QA 4.3)', N'KITCHEN', N'BATCH',
        N'BE2.5 – PROPOSED (D-54 c). Theo mẻ: cảm quan, trọng lượng suất, lưu mẫu + người ký. Ghi qua FoodSafetyChecklistResult/Record kèm mẻ.'),
    (N'PREP', N'Kiểm tra sơ chế', N'KITCHEN', N'SHIFT',
        N'BE2.5 – PROPOSED. Hạn = giờ xuất bếp − 150 phút (ChecklistOffsetsMinutes).'),
    (N'PREP_AREA_CLEAN', N'Vệ sinh khu sơ chế', N'KITCHEN', N'SHIFT',
        N'BE2.5 – PROPOSED. Hạn = giờ xuất bếp − 90 phút.'),
    (N'BLAST_CHILL', N'Xả nguội / làm lạnh nhanh', N'KITCHEN', N'SHIFT',
        N'BE2.5 – PROPOSED. Hạn = giờ xuất bếp − 20 phút.'),
    (N'KITCHEN_CLEAN', N'Vệ sinh bếp – 5S sau ca (QA 4.4)', N'KITCHEN', N'SHIFT',
        N'BE2.5 – PROPOSED. Hạn = giờ xuất bếp + 90 phút.');

DECLARE @I TABLE (Code NVARCHAR(50), ItemCode NVARCHAR(50), ItemName NVARCHAR(500), Description NVARCHAR(1000), SortOrder INT, IsCritical BIT, ExpectedValue NVARCHAR(500), UnitCode NVARCHAR(30));
INSERT INTO @I VALUES
    (N'VS-KHAY', N'NHAN_QR', N'1. Nhận / quét QR khay thu hồi', N'Đếm khay theo phiếu thu hồi, quét mã lô / phiếu.', 1, 0, NULL, NULL),
    (N'VS-KHAY', N'PHAN_LOAI', N'2. Phân loại', N'Tách khay hỏng / cần loại, khay dùng được vào dây chuyền rửa.', 2, 0, NULL, NULL),
    (N'VS-KHAY', N'RUA_SO', N'3. Rửa sơ', N'Gạt thức ăn thừa, ngâm / xịt nước.', 3, 0, NULL, NULL),
    (N'VS-KHAY', N'RUA_MAY', N'4. Rửa máy', N'Rửa máy theo chương trình, nhiệt độ nước rửa ≥ 60 °C.', 4, 0, N'>= 60', N'C'),
    (N'VS-KHAY', N'KHU_KHUAN', N'5. Khử khuẩn', N'Ghi hóa chất / nồng độ / thời gian (bắt buộc ghi hóa chất – BusinessRules:CleaningChemicalRequiredItems).', 5, 1, NULL, NULL),
    (N'VS-KHAY', N'KIEM_TRA_NHAP_KHO', N'6. Kiểm tra và nhập kho sạch', N'Khay khô, sạch, không mùi; QC kết luận mẻ (Complete).', 6, 1, NULL, NULL),
    (N'QA_FG', N'CAM_QUAN', N'Cảm quan (màu, mùi, vị, trạng thái)', N'Không đạt → giữ mẻ chờ QC.', 1, 1, N'Đạt', NULL),
    (N'QA_FG', N'TRONG_LUONG_SUAT', N'Trọng lượng suất', N'Cân ngẫu nhiên suất, sai lệch trong ± 5% định lượng.', 2, 1, N'± 5%', N'%'),
    (N'QA_FG', N'LUU_MAU_KY', N'Lưu mẫu + người ký', N'Xác nhận đã lưu mẫu (ProductionBatch/RecordSampleRetained) và người lưu ký sổ.', 3, 0, N'Đã ký', NULL),
    (N'PREP', N'NGUYEN_LIEU_DUNG_LO', N'Nguyên liệu đúng lô / còn hạn', N'Đối chiếu phiếu xuất kho, không dùng lô hết hạn / tạm giữ.', 1, 1, NULL, NULL),
    (N'PREP', N'TACH_SONG_CHIN', N'Tách dụng cụ sống / chín', N'Thớt, dao, khay phân màu.', 2, 1, NULL, NULL),
    (N'PREP', N'RA_DONG_DUNG_CACH', N'Rã đông đúng cách', N'Rã đông trong tủ mát / nước chảy, không để nhiệt độ phòng.', 3, 0, NULL, NULL),
    (N'PREP_AREA_CLEAN', N'BE_MAT_SACH', N'Bề mặt / thớt / dao sạch', NULL, 1, 0, NULL, NULL),
    (N'PREP_AREA_CLEAN', N'RAC_THAI', N'Rác thải đã thu gom, thùng có nắp', NULL, 2, 0, NULL, NULL),
    (N'PREP_AREA_CLEAN', N'HOA_CHAT_DUNG_NONG_DO', N'Hóa chất vệ sinh đúng nồng độ', NULL, 3, 0, NULL, NULL),
    (N'BLAST_CHILL', N'NHIET_DO_SAU_LAM_LANH', N'Nhiệt độ thành phẩm sau làm lạnh', N'Làm lạnh nhanh xuống ≤ 5 °C trong thời gian quy định.', 1, 1, N'<= 5', N'C'),
    (N'BLAST_CHILL', N'THOI_GIAN_LAM_LANH', N'Thời gian làm lạnh', NULL, 2, 0, N'<= 120', N'phút'),
    (N'KITCHEN_CLEAN', N'SANG_LOC', N'Sàng lọc – loại bỏ vật dụng không cần thiết', NULL, 1, 0, NULL, NULL),
    (N'KITCHEN_CLEAN', N'SAP_XEP', N'Sắp xếp dụng cụ đúng vị trí', NULL, 2, 0, NULL, NULL),
    (N'KITCHEN_CLEAN', N'SACH_SE', N'Sạch sẽ – sàn, bếp, chụp hút, cống', NULL, 3, 0, NULL, NULL),
    (N'KITCHEN_CLEAN', N'BAO_HO_CA_NHAN', N'Bảo hộ / vệ sinh cá nhân nhân viên', N'Mũ, khẩu trang, tạp dề, găng tay; không đeo trang sức.', 4, 1, NULL, NULL),
    (N'KITCHEN_CLEAN', N'TU_LANH_NHIET_DO', N'Tủ lạnh / kho mát đúng nhiệt độ (đã ghi nhật ký)', N'Nhật ký nhiệt độ ở TemperatureLog/Record.', 5, 0, NULL, NULL);

BEGIN TRAN;
INSERT INTO [dbo].[FoodSafetyChecklist] (Id, Code, Name, VersionNo, AreaCode, FrequencyCode, EffectiveFrom, Status, Note, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), c.Code, c.Name, 1, c.AreaCode, c.FrequencyCode, CAST(@Now AS DATE), N'Active', c.Note, @Now, @SeedBy, 0
FROM @C c
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[FoodSafetyChecklist] x WHERE x.Code = c.Code AND x.IsDeleted = 0);

INSERT INTO [dbo].[FoodSafetyChecklistItem] (Id, ChecklistId, ItemCode, ItemName, Description, SortOrder, IsCritical, ExpectedValue, UnitCode, IsRequired, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), h.Id, i.ItemCode, i.ItemName, i.Description, i.SortOrder, i.IsCritical, i.ExpectedValue, i.UnitCode, 1, @Now, @SeedBy, 0
FROM @I i
CROSS APPLY (SELECT TOP (1) x.Id FROM [dbo].[FoodSafetyChecklist] x WHERE x.Code = i.Code AND x.IsDeleted = 0 ORDER BY x.VersionNo DESC) h
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[FoodSafetyChecklistItem] y WHERE y.ChecklistId = h.Id AND y.ItemCode = i.ItemCode AND y.IsDeleted = 0);
COMMIT;
