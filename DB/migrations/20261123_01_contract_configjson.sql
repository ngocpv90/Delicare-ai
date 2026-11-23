-- delicare-migration: normal
-- D-78 (2) – GAP-BIZ-01 (anh duyệt 2026-11-23): cấu hình theo từng hợp đồng (JSON) – trước mắt "cách tính tiền" đối soát:
-- {"billingBasis": "RECEIVED" | "SERVED" | "ORDERED" | "DELIVERED" | "ACCEPTED"}; trống = BusinessRules:ReconciliationBillingBasis.
-- Thêm đúng 1 cột dbo.Contract.ConfigJson nvarchar(max) NULL. Không thêm bảng / FK / chỉ mục. Hợp đồng cũ giữ NULL. Chạy lại được.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
IF COL_LENGTH(N'dbo.Contract', N'ConfigJson') IS NULL
    ALTER TABLE [dbo].[Contract] ADD [ConfigJson] [nvarchar](max) NULL;
GO
