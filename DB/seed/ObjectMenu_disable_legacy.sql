-- T45R.8 / D-37: TẮT (IsActive = 0) 26 mục ObjectMenu của app cũ (phòng khám) – KHÔNG xóa, KHÔNG đụng UserRole.
-- Idempotent: chạy nhiều lần không lỗi, lần sau không đổi gì (chỉ cập nhật dòng đang IsActive = 1).
-- Nhận diện: MetaLink kiểu cũ (không phải '#/<screen-id>' của catalogue DELICARE / DB/seed/ObjectMenu_seed.sql).
-- Nhóm menu cấp 1 chỉ tắt khi không còn mục con nào đang bật.
-- Chỉ chạy trên DB dev / staging (CLAUDE.md 3.1). Người sửa = tài khoản 'admin' (khai báo rõ, không lấy "user đầu tiên").
--
-- Danh sách phát hiện trên DB dev AppDelicare (2026-11-16, IsDeleted = 0):
--   Cấp 2 (21 mục):
--     D78FA182-E57B-4E04-344C-08DD128A3A80  benh-nhan
--     4FE83173-4B49-4640-1C54-08DC928383CB  chuc-nang-demo
--     7ED319FB-67DD-4ED7-AF06-08DD115D0EBB  don-vi-tinh
--     5626B059-D1B9-4A5D-AF01-08DD115D0EBB  kham-benh
--     0DF5BDA7-E825-439E-AF04-08DD115D0EBB  kho-thuoc-chinh
--     8DAB3FCF-6BA6-4A46-AF05-08DD115D0EBB  kho-thuoc-le
--     C32CBA22-295F-434D-571E-08DD0B0EAB9F  nguoi-dung
--     D573F383-9381-4755-AF02-08DD115D0EBB  nha-cung-cap
--     3D07DFA9-EC5F-4EFF-AD22-08DD18CBC9FE  nhap-ket-qua
--     0FED22D3-B19B-4D6F-BB50-C43970178206  phan-quyen-tai-khoan
--     BAABE0D7-40BC-4DD6-B5CD-56EC7204A0A1  quan-ly-chuc-nang
--     4EBE4053-9FBA-4854-5525-08DC90FEE5E5  quan-ly-dang-nhap
--     29F814C8-22A4-42CF-F2DB-08DD000DFBCB  quan-ly-don-vi
--     1578E687-759F-4A3E-F2DE-08DD000DFBCB  quan-ly-phuong-xa
--     935575E6-0E69-4A3D-F2DD-08DD000DFBCB  quan-ly-quan-huyen
--     3EB4EBDB-23A2-47F4-807B-575E167AA7D0  quan-ly-tai-khoan
--     F8CBC456-73E8-418A-55DB-08DCAE200A8D  quan-ly-thay-doi-du-lieu
--     E9B294EA-1EB7-414F-F2DC-08DD000DFBCB  quan-ly-tinh-thanh
--     50D1AF43-F3F1-4006-F2DA-08DD000DFBCB  quan-ly-vung
--     0FCF8879-2DFF-4247-DC80-08DCAF9F89A0  quan-ly-xoa-du-lieu-cu
--     BFDDBF26-35E4-4990-AF03-08DD115D0EBB  thuoc-sap-het-han
--   Cấp 1 (5 mục):
--     B14A0583-C10D-4E1A-8089-2DBC8EBF55DF  trang-chu   (Trang chủ – không có mục con)
--     9D2C8EB4-5120-496A-F2D9-08DD000DFBCB  #           (Danh mục)
--     4D8F3794-B3D0-4E2C-AEFF-08DD115D0EBB  #           (Khám bệnh)
--     A20FC7B5-6C0B-4E19-AF00-08DD115D0EBB  #           (Kho thuốc)
--     54A1A2C1-1D1A-4B43-A826-BEBCBEE743F2  #           (Hệ thống)
--   Không tính: 2 dòng rác đã xóa mềm từ trước (MetaLink '1', '123', IsDeleted = 1).
-- Khóa theo MetaLink (cấp 2, 'trang-chu') và theo Id (4 nhóm '#', vì MetaLink '#' không phân biệt được) để chạy được cả trên staging.
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @SeedBy UNIQUEIDENTIFIER = (SELECT TOP (1) Id FROM [dbo].[User] WHERE UserName = N'admin' AND IsDeleted = 0);
IF @SeedBy IS NULL THROW 50001, N'Không có tài khoản admin để ghi ModifiedBy.', 1;
DECLARE @Now DATETIME = GETDATE();
DECLARE @Level2 INT = 0, @Level1 INT = 0;

BEGIN TRAN;

-- 1) Mục cấp 2 của app cũ.
UPDATE [dbo].[ObjectMenu]
   SET IsActive = 0, ModifiedDate = @Now, ModifiedBy = @SeedBy
 WHERE IsDeleted = 0 AND IsActive = 1 AND ParentId IS NOT NULL
   AND MetaLink IN (N'benh-nhan', N'chuc-nang-demo', N'don-vi-tinh', N'kham-benh', N'kho-thuoc-chinh', N'kho-thuoc-le',
                    N'nguoi-dung', N'nha-cung-cap', N'nhap-ket-qua', N'phan-quyen-tai-khoan', N'quan-ly-chuc-nang',
                    N'quan-ly-dang-nhap', N'quan-ly-don-vi', N'quan-ly-phuong-xa', N'quan-ly-quan-huyen', N'quan-ly-tai-khoan',
                    N'quan-ly-thay-doi-du-lieu', N'quan-ly-tinh-thanh', N'quan-ly-vung', N'quan-ly-xoa-du-lieu-cu', N'thuoc-sap-het-han');
SET @Level2 = @@ROWCOUNT;

-- 2) Mục / nhóm cấp 1 của app cũ: chỉ tắt khi không còn mục con nào đang bật.
UPDATE m
   SET IsActive = 0, ModifiedDate = @Now, ModifiedBy = @SeedBy
  FROM [dbo].[ObjectMenu] AS m
 WHERE m.IsDeleted = 0 AND m.IsActive = 1 AND m.ParentId IS NULL
   AND (m.MetaLink = N'trang-chu'
        OR (m.MetaLink = N'#' AND m.Id IN ('9D2C8EB4-5120-496A-F2D9-08DD000DFBCB', '4D8F3794-B3D0-4E2C-AEFF-08DD115D0EBB',
                                         'A20FC7B5-6C0B-4E19-AF00-08DD115D0EBB', '54A1A2C1-1D1A-4B43-A826-BEBCBEE743F2')))
   AND NOT EXISTS (SELECT 1 FROM [dbo].[ObjectMenu] AS c WHERE c.ParentId = m.Id AND c.IsDeleted = 0 AND c.IsActive = 1);
SET @Level1 = @@ROWCOUNT;

COMMIT;

PRINT CONCAT(N'ObjectMenu_disable_legacy: tắt ', @Level2, N' mục cấp 2 + ', @Level1, N' mục cấp 1 (lần chạy lại: 0 + 0).');
