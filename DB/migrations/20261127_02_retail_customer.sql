-- delicare-migration: normal
-- RETAIL-2 (2026-11-27, anh giao: "làm phần login / đăng ký tài khoản … Đơn của tôi … truy xuất nguồn gốc … theo dõi chuyến … be chưa có thì viết mới").
-- D-136 (PROPOSED – BA/PO CONFIRM): tài khoản khách lẻ TÁCH khỏi dbo.[User] (User là tài khoản nhân viên, gắn nhóm quyền / menu nội bộ –
-- trộn khách vào sẽ lộ màn nội bộ và làm bẩn danh sách người dùng).
--   + Bảng RetailCustomer: tài khoản khách lẻ đăng nhập bằng SĐT + mật khẩu (PBKDF2, không MD5).
--   + RetailOrder.RetailCustomerId: đơn đặt khi đã đăng nhập → hiện ở "Đơn của tôi" (đơn khách vãng lai vẫn NULL).
--   + RetailOrder.DeliveryRouteId: chuyến giao chở đơn (nhân viên gán khi chuyển "Đang giao") → khách theo dõi chuyến.
-- KHÔNG FK vật lý (D-30). Chỉ mục KHÔNG unique (trùng SĐT chặn ở tầng Service). Chạy lại được. Cú pháp SQL Server 2022 (D-61).
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.RetailCustomer', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[RetailCustomer] (
        [Id]                [uniqueidentifier] NOT NULL,
        [PhoneNumber]       [nvarchar](20)     NOT NULL,   -- tên đăng nhập, đã chuẩn hóa 0xxxxxxxxx, không trùng (kiểm ở Service)
        [FullName]          [nvarchar](200)    NOT NULL,
        [Email]             [nvarchar](255)    NULL,
        [Address]           [nvarchar](500)    NULL,       -- địa chỉ giao mặc định (điền sẵn khi đặt)
        [PasswordHash]      [nvarchar](300)    NOT NULL,   -- PBKDF2-SHA256: "PBKDF2$<vòng>$<salt b64>$<hash b64>" – không bao giờ trả ra API
        [PasswordChangedAt] [datetime2](0)     NOT NULL,   -- đổi mật khẩu → phiên đăng nhập cũ hết hiệu lực
        [LastLoginAt]       [datetime2](0)     NULL,
        [Status]            [nvarchar](30)     NOT NULL,   -- Active (Đang hoạt động) / Inactive (Ngừng hoạt động – khóa đăng nhập)
        [CreatedDate]       [datetime2](0)     NOT NULL,
        [CreatedBy]         [uniqueidentifier] NULL,       -- tự đăng ký: tài khoản system
        [ModifiedDate]      [datetime2](0)     NULL,
        [ModifiedBy]        [uniqueidentifier] NULL,
        [IsDeleted]         [bit]              NOT NULL CONSTRAINT [DF_RetailCustomer_IsDeleted] DEFAULT (0),
        CONSTRAINT [PK_RetailCustomer] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END;
GO

IF COL_LENGTH(N'dbo.RetailOrder', N'RetailCustomerId') IS NULL
    ALTER TABLE [dbo].[RetailOrder] ADD [RetailCustomerId] [uniqueidentifier] NULL;
GO
IF COL_LENGTH(N'dbo.RetailOrder', N'DeliveryRouteId') IS NULL
    ALTER TABLE [dbo].[RetailOrder] ADD [DeliveryRouteId] [uniqueidentifier] NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RetailCustomer_Phone' AND object_id = OBJECT_ID(N'dbo.RetailCustomer'))
    CREATE NONCLUSTERED INDEX [IX_RetailCustomer_Phone] ON [dbo].[RetailCustomer] ([PhoneNumber]) WHERE [IsDeleted] = 0;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RetailOrder_Customer' AND object_id = OBJECT_ID(N'dbo.RetailOrder'))
    CREATE NONCLUSTERED INDEX [IX_RetailOrder_Customer] ON [dbo].[RetailOrder] ([RetailCustomerId], [DeliveryDate]) WHERE [RetailCustomerId] IS NOT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RetailOrder_Route' AND object_id = OBJECT_ID(N'dbo.RetailOrder'))
    CREATE NONCLUSTERED INDEX [IX_RetailOrder_Route] ON [dbo].[RetailOrder] ([DeliveryRouteId]) WHERE [DeliveryRouteId] IS NOT NULL;
GO
