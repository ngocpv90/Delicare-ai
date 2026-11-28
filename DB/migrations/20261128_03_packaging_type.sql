-- delicare-migration: normal
-- PKG-FEE (2026-11-28, anh duyệt bảng mới): danh mục LOẠI HỘP / KHAY (trước đây chỉ ở appsettings ReferenceLists:PackagingType)
--   + phí thất thoát mỗi cái theo loại (trước đây BusinessRules:ContainerLossFee – mặc định 0). Sổ hộp khay / bảng kê / agent đọc từ đây.
-- KHÔNG FK vật lý (D-30) – PackagingLedger.PackagingType vẫn lưu MÃ loại. Mã không trùng (chỉ mục duy nhất có lọc). Chạy lại được. SQL Server 2022 (D-61).
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.PackagingType', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[PackagingType] (
        [Id]           [uniqueidentifier] NOT NULL,
        [Code]         [nvarchar](50)     NOT NULL,   -- mã loại (KHAY, HOP_GIU_NHIET…) – ghi vào PackagingLedger.PackagingType, PackingResult.PackagingType
        [Name]         [nvarchar](200)    NOT NULL,
        [LossFee]      [decimal](18, 2)   NOT NULL CONSTRAINT [DF_PackagingType_LossFee] DEFAULT (0),   -- phí thất thoát / cái (đ) – bảng kê tính phần vượt ngưỡng
        [SortOrder]    [int]              NULL,
        [Status]       [nvarchar](30)     NOT NULL,   -- Active / Inactive (ngừng dùng – không chọn được cho giao dịch mới)
        [Note]         [nvarchar](500)    NULL,
        [CreatedDate]  [datetime2](0)     NOT NULL,
        [CreatedBy]    [uniqueidentifier] NULL,
        [ModifiedDate] [datetime2](0)     NULL,
        [ModifiedBy]   [uniqueidentifier] NULL,
        [IsDeleted]    [bit]              NOT NULL CONSTRAINT [DF_PackagingType_IsDeleted] DEFAULT (0),
        CONSTRAINT [PK_PackagingType] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_PackagingType_Code' AND object_id = OBJECT_ID(N'dbo.PackagingType'))
    CREATE UNIQUE NONCLUSTERED INDEX [UX_PackagingType_Code] ON [dbo].[PackagingType] ([Code]) WHERE [IsDeleted] = 0;
GO
