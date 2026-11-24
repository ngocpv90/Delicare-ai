-- delicare-migration: normal
-- UI-6.26 (D-107, GAP-PRD-10 anh duyệt 2026-11-24): dây chuyền bếp gắn theo nhóm món – ra lệnh chia mẻ đúng dây chuyền.
-- Thêm đúng 1 cột dbo.DishCategorie.ProductionLineId uniqueidentifier NULL (tham chiếu logic → dbo.ProductionLine, KHÔNG tạo FK vật lý – D-30).
-- Không thêm bảng / FK / chỉ mục khác. Nhóm cũ giữ NULL (gán ban đầu theo tên: DB/scripts/UI626_dishcategory_productionline.sql). Chạy lại được (kiểm cột đã có).
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
IF COL_LENGTH(N'dbo.DishCategorie', N'ProductionLineId') IS NULL
    ALTER TABLE [dbo].[DishCategorie] ADD [ProductionLineId] [uniqueidentifier] NULL;
GO
