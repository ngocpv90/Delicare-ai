-- delicare-migration: normal
-- ORD-OPT-1.1 (D-74 d, anh duyệt 2026-11-22): dòng đơn lưu số suất theo LỰA CHỌN của thực đơn.
-- Thêm đúng 1 cột dbo.OrderLine.MenuOptionNo int NULL (1–9 = MenuLine.SequenceNo của thực đơn áp dụng; NULL = dòng cũ không theo lựa chọn).
-- Không thêm bảng / FK / chỉ mục khác. Dòng cũ giữ NULL. Chạy lại được (kiểm cột / ràng buộc đã có).
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
IF COL_LENGTH(N'dbo.OrderLine', N'MenuOptionNo') IS NULL
    ALTER TABLE [dbo].[OrderLine] ADD [MenuOptionNo] [int] NULL;
GO
IF OBJECT_ID(N'dbo.CK_OrderLine_MenuOptionNo', N'C') IS NULL
    ALTER TABLE [dbo].[OrderLine] WITH CHECK ADD CONSTRAINT [CK_OrderLine_MenuOptionNo] CHECK ([MenuOptionNo] IS NULL OR [MenuOptionNo] BETWEEN 1 AND 9);
GO
