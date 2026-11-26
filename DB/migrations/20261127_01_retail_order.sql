-- delicare-migration: normal
-- RETAIL-1 (anh chốt 2026-11-26: "module dành riêng cho khách lẻ, đặt trên web vào đó, doanh nghiệp để riêng" – chọn BẢNG MỚI RIÊNG).
-- Đặc tả: docs/frontend-handoff/RETAIL-1.md. Tách hẳn đơn doanh nghiệp (dbo.[Order] / hợp đồng KHÔNG đổi).
-- Thêm 3 bảng: RetailProduct (sản phẩm bán lẻ), RetailOrder (đơn khách lẻ), RetailOrderLine (dòng món).
-- Khuôn cột chuẩn như dbo.SupplierIssue (Id GUID, CreatedDate / CreatedBy / ModifiedDate / ModifiedBy / IsDeleted); giờ Việt Nam do ứng dụng gán.
-- KHÔNG FK vật lý (D-30 – quan hệ kiểm ở tầng Service). Mã G1: RetailOrder.OrderNumber BL-YYMMDD-NNNN, RetailProduct.Code SPL-NNNNN (BUSINESS_RULES §11).
-- Chỉ mục KHÔNG unique (ghi DECISIONS). Chạy lại được: kiểm bảng / chỉ mục đã có. Cú pháp SQL Server 2022 (D-61).
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.RetailProduct', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[RetailProduct] (
        [Id]           [uniqueidentifier] NOT NULL,
        [Code]         [nvarchar](50)     NOT NULL,   -- G1 SPL-NNNNN – máy chủ sinh, không đổi
        [Name]         [nvarchar](255)    NOT NULL,
        [CategoryCode] [nvarchar](50)     NOT NULL,   -- ReferenceLists:RetailCategory (appsettings)
        [MealTypes]    [nvarchar](100)    NOT NULL,   -- bữa được bán, mã ReferenceLists:MealType nối ';' (vd "Trưa;Tối") – chặn đặt món sáng cho bữa tối
        [Unit]         [nvarchar](50)     NOT NULL,   -- suất / combo / bát / phần / chai / hộp
        [Price]        [decimal](18, 2)   NOT NULL,   -- giá bán lẻ đã gồm VAT
        [Description]  [nvarchar](1000)   NULL,
        [Tags]         [nvarchar](255)    NULL,       -- nhãn hiển thị nối ';' (Bán chạy;Mới;Tiết kiệm;Lành mạnh)
        [Icon]         [nvarchar](50)     NULL,       -- biểu tượng tạm thay ảnh
        [ImageUrl]     [nvarchar](500)    NULL,       -- ảnh món (DocumentStorage) – để sau
        [SortOrder]    [int]              NOT NULL CONSTRAINT [DF_RetailProduct_SortOrder] DEFAULT (0),
        [Status]       [nvarchar](30)     NOT NULL,   -- Active (Đang bán) / Inactive (Ngừng bán)
        [CreatedDate]  [datetime2](0)     NOT NULL,
        [CreatedBy]    [uniqueidentifier] NULL,
        [ModifiedDate] [datetime2](0)     NULL,
        [ModifiedBy]   [uniqueidentifier] NULL,
        [IsDeleted]    [bit]              NOT NULL CONSTRAINT [DF_RetailProduct_IsDeleted] DEFAULT (0),
        CONSTRAINT [PK_RetailProduct] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END;
GO

IF OBJECT_ID(N'dbo.RetailOrder', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[RetailOrder] (
        [Id]            [uniqueidentifier] NOT NULL,
        [OrderNumber]   [nvarchar](50)     NOT NULL,  -- G1 BL-YYMMDD-NNNN – máy chủ sinh, không đổi
        [Channel]       [nvarchar](20)     NOT NULL,  -- WEB / PHONE / ZALO
        [CustomerName]  [nvarchar](200)    NOT NULL,
        [PhoneNumber]   [nvarchar](20)     NOT NULL,  -- đã chuẩn hóa 0xxxxxxxxx
        [Address]       [nvarchar](500)    NOT NULL,
        [DeliveryDate]  [date]             NOT NULL,
        [MealType]      [nvarchar](50)     NOT NULL,  -- mã ReferenceLists:MealType
        [Note]          [nvarchar](1000)   NULL,
        [OrderStatus]   [nvarchar](30)     NOT NULL,  -- New / Confirmed / Delivering / Completed / Cancelled (STATUS_CATALOG)
        [SubTotal]      [decimal](18, 2)   NOT NULL,
        [ShippingFee]   [decimal](18, 2)   NOT NULL CONSTRAINT [DF_RetailOrder_ShippingFee] DEFAULT (0),
        [TotalAmount]   [decimal](18, 2)   NOT NULL,
        [PaymentMethod] [nvarchar](30)     NULL,      -- COD / BANK
        [PaymentStatus] [nvarchar](30)     NOT NULL,  -- UNPAID / PAID
        [ConfirmedAt]   [datetime2](0)     NULL,
        [ConfirmedBy]   [uniqueidentifier] NULL,
        [DeliveringAt]  [datetime2](0)     NULL,
        [CompletedAt]   [datetime2](0)     NULL,
        [CancelledAt]   [datetime2](0)     NULL,
        [CancelReason]  [nvarchar](500)    NULL,
        [ClientIp]        [nvarchar](64)   NULL,      -- chống spam đơn WEB – không hiển thị
        [ClientRequestId] [nvarchar](64)   NULL,      -- mã lần gửi do trang khách sinh: gửi lại (mạng chập chờn / hết giờ chờ) trả lại đơn cũ, không tạo đơn trùng
        [CreatedDate]   [datetime2](0)     NOT NULL,
        [CreatedBy]     [uniqueidentifier] NULL,      -- đơn WEB: tài khoản system
        [ModifiedDate]  [datetime2](0)     NULL,
        [ModifiedBy]    [uniqueidentifier] NULL,
        [IsDeleted]     [bit]              NOT NULL CONSTRAINT [DF_RetailOrder_IsDeleted] DEFAULT (0),
        CONSTRAINT [PK_RetailOrder] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END;
GO

IF OBJECT_ID(N'dbo.RetailOrderLine', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[RetailOrderLine] (
        [Id]              [uniqueidentifier] NOT NULL,
        [RetailOrderId]   [uniqueidentifier] NOT NULL,  -- → dbo.RetailOrder (logic)
        [RetailProductId] [uniqueidentifier] NOT NULL,  -- → dbo.RetailProduct (logic)
        [ProductName]     [nvarchar](255)    NOT NULL,  -- chụp lại lúc đặt: đổi tên / giá sau không ảnh hưởng đơn cũ
        [Unit]            [nvarchar](50)     NOT NULL,
        [Quantity]        [decimal](18, 2)   NOT NULL,
        [UnitPrice]       [decimal](18, 2)   NOT NULL,
        [LineAmount]      [decimal](18, 2)   NOT NULL,
        [CreatedDate]     [datetime2](0)     NOT NULL,
        [CreatedBy]       [uniqueidentifier] NULL,
        [ModifiedDate]    [datetime2](0)     NULL,
        [ModifiedBy]      [uniqueidentifier] NULL,
        [IsDeleted]       [bit]              NOT NULL CONSTRAINT [DF_RetailOrderLine_IsDeleted] DEFAULT (0),
        CONSTRAINT [PK_RetailOrderLine] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END;
GO

-- Chỉ mục không unique: danh sách theo trạng thái / ngày nhận, tìm theo SĐT, tổng hợp cho bếp theo ngày + bữa, dòng theo đơn.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RetailOrder_Status_Date' AND object_id = OBJECT_ID(N'dbo.RetailOrder'))
    CREATE NONCLUSTERED INDEX [IX_RetailOrder_Status_Date] ON [dbo].[RetailOrder] ([OrderStatus], [DeliveryDate]) WHERE [IsDeleted] = 0;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RetailOrder_Date_Meal' AND object_id = OBJECT_ID(N'dbo.RetailOrder'))
    CREATE NONCLUSTERED INDEX [IX_RetailOrder_Date_Meal] ON [dbo].[RetailOrder] ([DeliveryDate], [MealType]) INCLUDE ([OrderStatus]) WHERE [IsDeleted] = 0;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RetailOrder_Phone' AND object_id = OBJECT_ID(N'dbo.RetailOrder'))
    CREATE NONCLUSTERED INDEX [IX_RetailOrder_Phone] ON [dbo].[RetailOrder] ([PhoneNumber]) WHERE [IsDeleted] = 0;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RetailOrder_OrderNumber' AND object_id = OBJECT_ID(N'dbo.RetailOrder'))
    CREATE NONCLUSTERED INDEX [IX_RetailOrder_OrderNumber] ON [dbo].[RetailOrder] ([OrderNumber]) WHERE [IsDeleted] = 0;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RetailOrder_ClientRequestId' AND object_id = OBJECT_ID(N'dbo.RetailOrder'))
    CREATE NONCLUSTERED INDEX [IX_RetailOrder_ClientRequestId] ON [dbo].[RetailOrder] ([ClientRequestId]) WHERE [ClientRequestId] IS NOT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RetailOrderLine_Order' AND object_id = OBJECT_ID(N'dbo.RetailOrderLine'))
    CREATE NONCLUSTERED INDEX [IX_RetailOrderLine_Order] ON [dbo].[RetailOrderLine] ([RetailOrderId]) WHERE [IsDeleted] = 0;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RetailProduct_Status' AND object_id = OBJECT_ID(N'dbo.RetailProduct'))
    CREATE NONCLUSTERED INDEX [IX_RetailProduct_Status] ON [dbo].[RetailProduct] ([Status], [SortOrder]) WHERE [IsDeleted] = 0;
GO
