-- ORD-OPT-1.8 (D-74 g, anh chốt 2026-11-22): mục menu "Đăng ký suất tuần" (#/crm-order-week) – nhóm Kinh doanh & Đơn hàng, cạnh "Đơn đặt suất ăn".
-- Quyền như #/crm-order: mọi nhóm đang có quyền #/crm-order được chép đúng các cờ sang #/crm-order-week.
-- Idempotent: chỉ THÊM phần chưa có (theo MetaLink / nhóm), không ghi đè chỉnh sửa của admin, không DELETE. Chạy sau ObjectMenu_seed.sql + UserRole_seed.sql.
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @SeedBy UNIQUEIDENTIFIER = (SELECT TOP (1) Id FROM [dbo].[User] WHERE UserName = N'admin' AND IsDeleted = 0);
IF @SeedBy IS NULL THROW 50001, N'Không có tài khoản admin để ghi CreatedBy cho seed.', 1;
DECLARE @Now DATETIME = GETDATE();
BEGIN TRAN;
DECLARE @Order UNIQUEIDENTIFIER = (SELECT TOP (1) Id FROM [dbo].[ObjectMenu] WHERE MetaLink = N'#/crm-order' AND IsDeleted = 0);
IF @Order IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[ObjectMenu] WHERE MetaLink = N'#/crm-order-week' AND IsDeleted = 0)
    INSERT INTO [dbo].[ObjectMenu] (Id, Name, ShortName, MetaLink, ParentId, LevelShow, OrderShow, IsActive, IsDefault, CreatedDate, CreatedBy, IsDeleted)
    SELECT NEWID(), N'Đăng ký suất tuần', N'P3', N'#/crm-order-week', m.ParentId, m.LevelShow, m.OrderShow, 1, 0, @Now, @SeedBy, 0
    FROM [dbo].[ObjectMenu] m WHERE m.Id = @Order;
DECLARE @Week UNIQUEIDENTIFIER = (SELECT TOP (1) Id FROM [dbo].[ObjectMenu] WHERE MetaLink = N'#/crm-order-week' AND IsDeleted = 0);
IF @Order IS NOT NULL AND @Week IS NOT NULL
    INSERT INTO [dbo].[UserRole] (Id, UserGroupId, ObjectMenuId, AllowAccess, AllowAdd, AllowEdit, AllowDelete, AllowPrint, AllowExport, AllowImport, CreatedDate, CreatedBy, IsDeleted)
    SELECT NEWID(), r.UserGroupId, @Week, r.AllowAccess, r.AllowAdd, r.AllowEdit, r.AllowDelete, r.AllowPrint, r.AllowExport, r.AllowImport, @Now, @SeedBy, 0
    FROM [dbo].[UserRole] r
    WHERE r.ObjectMenuId = @Order AND r.IsDeleted = 0
      AND NOT EXISTS (SELECT 1 FROM [dbo].[UserRole] x WHERE x.UserGroupId = r.UserGroupId AND x.ObjectMenuId = @Week AND x.IsDeleted = 0);
COMMIT;
