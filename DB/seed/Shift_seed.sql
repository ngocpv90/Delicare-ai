-- T21.1 – Ca làm việc / ca giao (BUSINESS_RULES.md mục 0–1). Mã theo đề xuất D-19 (PROPOSED – BA/PO CONFIRM):
--   SÁNG ↔ SANG_6H · TRƯA ↔ TRUA · TỐI ↔ TOI · ĐÊM ↔ DEM_23H. Giờ bắt đầu / kết thúc là ĐỀ XUẤT – anh sửa qua API /api/v1/hr/shifts.
-- Idempotent theo Code: chưa có thì thêm; đã có thì KHÔNG ghi đè tên / giờ (giữ chỉnh sửa của người dùng). Không DELETE.
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @SeedBy UNIQUEIDENTIFIER = (SELECT TOP (1) Id FROM [dbo].[User] WHERE UserName = N'admin' AND IsDeleted = 0);
IF @SeedBy IS NULL THROW 50001, N'Không có tài khoản admin để ghi CreatedBy cho seed.', 1;
DECLARE @CompanyId UNIQUEIDENTIFIER = (SELECT TOP (1) Id FROM [dbo].[Company] WHERE Code = N'DLC' AND IsDeleted = 0);
DECLARE @Now DATETIME = GETDATE();

BEGIN TRAN;
DECLARE @S TABLE (Code NVARCHAR(50), Name NVARCHAR(255), StartTime TIME(7), EndTime TIME(7), Note NVARCHAR(500));
INSERT INTO @S VALUES
    (N'SANG_6H', N'Sáng',  '06:00', '09:00', N'Buổi SÁNG – giao 6h. Giờ PROPOSED (D-19).'),
    (N'TRUA',    N'Trưa',  '10:30', '13:30', N'Buổi TRƯA. Giờ PROPOSED (D-19).'),
    (N'TOI',     N'Tối',   '16:30', '19:30', N'Buổi TỐI. Giờ PROPOSED (D-19).'),
    (N'DEM_23H', N'Đêm',   '23:00', '01:00', N'Buổi ĐÊM – giao 23h, ca qua đêm. Giờ PROPOSED (D-19).');

INSERT INTO [dbo].[Shift] (Id, CompanyId, Code, Name, StartTime, EndTime, Status, Note, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), @CompanyId, s.Code, s.Name, s.StartTime, s.EndTime, N'Active', s.Note, @Now, @SeedBy, 0
FROM @S s
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[Shift] x WHERE x.Code = s.Code AND x.IsDeleted = 0);
COMMIT;
