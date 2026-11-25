-- delicare-migration: normal
-- GAP-LOG-DRV (D-128, anh duyệt 2026-11-25): tài xế mặc định của xe – tự lập chuyến sau chốt đơn gán tài xế này (UI-6.49 (10) chuyển từ phương án tạm).
-- Thêm đúng 1 cột dbo.Vehicle.DefaultDriverEmployeeId uniqueidentifier NULL (tham chiếu logic → dbo.Employee, KHÔNG tạo FK vật lý – D-30).
-- Không thêm bảng / FK / chỉ mục khác. Xe cũ giữ NULL (điều phối gán ở màn Xe giao hàng; chưa gán → dùng tài xế chuyến gần nhất). Chạy lại được (kiểm cột đã có).
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
IF COL_LENGTH(N'dbo.Vehicle', N'DefaultDriverEmployeeId') IS NULL
    ALTER TABLE [dbo].[Vehicle] ADD [DefaultDriverEmployeeId] [uniqueidentifier] NULL;
GO
