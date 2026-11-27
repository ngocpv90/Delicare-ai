-- delicare-migration: normal
-- RETAIL-2 (2026-11-27, D-136 j – anh duyệt): tài khoản khách lẻ theo loại.
--   Cá nhân (INDIVIDUAL): bắt buộc SĐT (đăng nhập). Doanh nghiệp (BUSINESS): bắt buộc thêm tên doanh nghiệp + mã số thuế + email;
--   vẫn đăng nhập bằng SĐT liên hệ. Tài khoản cũ (AccountType NULL) coi là cá nhân.
-- Không FK vật lý. Chỉ mục KHÔNG unique (MST không trùng kiểm ở Service). Chạy lại được. Cú pháp SQL Server 2022 (D-61).
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH(N'dbo.RetailCustomer', N'AccountType') IS NULL
    ALTER TABLE [dbo].[RetailCustomer] ADD [AccountType] [nvarchar](20) NULL;
GO
IF COL_LENGTH(N'dbo.RetailCustomer', N'CompanyName') IS NULL
    ALTER TABLE [dbo].[RetailCustomer] ADD [CompanyName] [nvarchar](255) NULL;
GO
IF COL_LENGTH(N'dbo.RetailCustomer', N'TaxCode') IS NULL
    ALTER TABLE [dbo].[RetailCustomer] ADD [TaxCode] [nvarchar](20) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RetailCustomer_TaxCode' AND object_id = OBJECT_ID(N'dbo.RetailCustomer'))
    CREATE NONCLUSTERED INDEX [IX_RetailCustomer_TaxCode] ON [dbo].[RetailCustomer] ([TaxCode]) WHERE [TaxCode] IS NOT NULL AND [IsDeleted] = 0;
GO
