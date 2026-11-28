-- delicare-migration: normal
-- AI-19 (2026-11-28, D-06 – anh duyệt thêm cột): CustomerContact.UserId – tài khoản CUSTOMER (D-82, không đăng nhập web) của người liên hệ khách,
--   dùng cho bot Zalo khách (UserExternalIdentity kênh ZALO trỏ tới tài khoản này). Mỗi tài khoản gắn tối đa 1 người liên hệ.
-- Chỉ 1 cột đã duyệt, không thêm bảng. Chạy lại được. Cú pháp SQL Server 2022 (D-61).
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH(N'dbo.CustomerContact', N'UserId') IS NULL
    ALTER TABLE [dbo].[CustomerContact] ADD [UserId] [uniqueidentifier] NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CustomerContact_User')
    ALTER TABLE [dbo].[CustomerContact] WITH CHECK ADD CONSTRAINT [FK_CustomerContact_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_CustomerContact_UserId' AND object_id = OBJECT_ID(N'dbo.CustomerContact'))
    CREATE UNIQUE NONCLUSTERED INDEX [UX_CustomerContact_UserId] ON [dbo].[CustomerContact] ([UserId]) WHERE [UserId] IS NOT NULL AND [IsDeleted] = 0;
GO
