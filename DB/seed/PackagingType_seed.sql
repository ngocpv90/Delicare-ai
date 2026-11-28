-- PKG-FEE (anh duyệt 2026-11-28): danh mục Loại hộp / khay – 3 loại đang dùng (trước đây appsettings ReferenceLists:PackagingType).
-- Idempotent, CHỈ THÊM theo Code – không ghi đè tên / phí đã sửa trên màn Loại hộp / khay. Phí thất thoát mặc định 0 (anh / Kế toán nhập trên màn).
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @SeedBy UNIQUEIDENTIFIER = (SELECT TOP (1) Id FROM [dbo].[User] WHERE UserName = N'admin' AND IsDeleted = 0);
IF @SeedBy IS NULL THROW 50001, N'Không có tài khoản admin để ghi CreatedBy cho seed.', 1;
DECLARE @Now DATETIME2(0) = SYSDATETIME();
DECLARE @T TABLE (Code NVARCHAR(50), Name NVARCHAR(200), SortOrder INT);
INSERT INTO @T VALUES (N'KHAY', N'Khay inox', 1), (N'HOP_GIU_NHIET', N'Hộp giữ nhiệt', 2), (N'THUNG_GIU_NHIET', N'Thùng giữ nhiệt', 3);
INSERT INTO [dbo].[PackagingType] (Id, Code, Name, LossFee, SortOrder, Status, Note, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), t.Code, t.Name, 0, t.SortOrder, N'Active', N'PKG-FEE seed – từ ReferenceLists:PackagingType', @Now, @SeedBy, 0
FROM @T t WHERE NOT EXISTS (SELECT 1 FROM [dbo].[PackagingType] p WHERE p.Code = t.Code AND p.IsDeleted = 0);
SELECT @@ROWCOUNT AS [Loại hộp thêm];
