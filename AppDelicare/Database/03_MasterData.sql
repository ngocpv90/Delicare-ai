-- =====================================================================================
-- Master Data (Lớp dữ liệu Master): bảng, stored procedure GetAll, menu + phân quyền.
-- Sinh tự động cùng màn hình web (app-delicare-client/src/app/main/master-data).
-- Chạy lại nhiều lần được: chỉ tạo bảng/cột còn thiếu, procedure dùng CREATE OR ALTER,
-- menu/quyền chỉ thêm khi chưa có. Không xoá hay sửa dữ liệu đang có.
-- =====================================================================================
USE [Delicare]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

/****** Table Company ******/
IF OBJECT_ID(N'[dbo].[Company]', N'U') IS NULL
    CREATE TABLE [dbo].[Company] ([Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_Company] PRIMARY KEY CLUSTERED);
GO
IF COL_LENGTH(N'dbo.Company', N'CreatedDate') IS NULL ALTER TABLE [dbo].[Company] ADD [CreatedDate] DATETIME NOT NULL DEFAULT (GETDATE());
IF COL_LENGTH(N'dbo.Company', N'CreatedBy') IS NULL ALTER TABLE [dbo].[Company] ADD [CreatedBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Company', N'ModifiedDate') IS NULL ALTER TABLE [dbo].[Company] ADD [ModifiedDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.Company', N'ModifiedBy') IS NULL ALTER TABLE [dbo].[Company] ADD [ModifiedBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Company', N'DeletedDate') IS NULL ALTER TABLE [dbo].[Company] ADD [DeletedDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.Company', N'DeleteddBy') IS NULL ALTER TABLE [dbo].[Company] ADD [DeleteddBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Company', N'IsDeleted') IS NULL ALTER TABLE [dbo].[Company] ADD [IsDeleted] BIT NOT NULL DEFAULT (0);
IF COL_LENGTH(N'dbo.Company', N'Code') IS NULL ALTER TABLE [dbo].[Company] ADD [Code] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Company', N'Name') IS NULL ALTER TABLE [dbo].[Company] ADD [Name] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Company', N'TaxCode') IS NULL ALTER TABLE [dbo].[Company] ADD [TaxCode] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Company', N'Address') IS NULL ALTER TABLE [dbo].[Company] ADD [Address] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Company', N'Phone') IS NULL ALTER TABLE [dbo].[Company] ADD [Phone] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Company', N'Email') IS NULL ALTER TABLE [dbo].[Company] ADD [Email] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Company', N'Status') IS NULL ALTER TABLE [dbo].[Company] ADD [Status] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Company', N'Note') IS NULL ALTER TABLE [dbo].[Company] ADD [Note] NVARCHAR(MAX) NULL;
GO

/****** Table OrganizationUnit ******/
IF OBJECT_ID(N'[dbo].[OrganizationUnit]', N'U') IS NULL
    CREATE TABLE [dbo].[OrganizationUnit] ([Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_OrganizationUnit] PRIMARY KEY CLUSTERED);
GO
IF COL_LENGTH(N'dbo.OrganizationUnit', N'CreatedDate') IS NULL ALTER TABLE [dbo].[OrganizationUnit] ADD [CreatedDate] DATETIME NOT NULL DEFAULT (GETDATE());
IF COL_LENGTH(N'dbo.OrganizationUnit', N'CreatedBy') IS NULL ALTER TABLE [dbo].[OrganizationUnit] ADD [CreatedBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.OrganizationUnit', N'ModifiedDate') IS NULL ALTER TABLE [dbo].[OrganizationUnit] ADD [ModifiedDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.OrganizationUnit', N'ModifiedBy') IS NULL ALTER TABLE [dbo].[OrganizationUnit] ADD [ModifiedBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.OrganizationUnit', N'DeletedDate') IS NULL ALTER TABLE [dbo].[OrganizationUnit] ADD [DeletedDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.OrganizationUnit', N'DeleteddBy') IS NULL ALTER TABLE [dbo].[OrganizationUnit] ADD [DeleteddBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.OrganizationUnit', N'IsDeleted') IS NULL ALTER TABLE [dbo].[OrganizationUnit] ADD [IsDeleted] BIT NOT NULL DEFAULT (0);
IF COL_LENGTH(N'dbo.OrganizationUnit', N'CompanyId') IS NULL ALTER TABLE [dbo].[OrganizationUnit] ADD [CompanyId] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.OrganizationUnit', N'ParentId') IS NULL ALTER TABLE [dbo].[OrganizationUnit] ADD [ParentId] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.OrganizationUnit', N'Code') IS NULL ALTER TABLE [dbo].[OrganizationUnit] ADD [Code] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.OrganizationUnit', N'Name') IS NULL ALTER TABLE [dbo].[OrganizationUnit] ADD [Name] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.OrganizationUnit', N'UnitType') IS NULL ALTER TABLE [dbo].[OrganizationUnit] ADD [UnitType] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.OrganizationUnit', N'Address') IS NULL ALTER TABLE [dbo].[OrganizationUnit] ADD [Address] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.OrganizationUnit', N'Phone') IS NULL ALTER TABLE [dbo].[OrganizationUnit] ADD [Phone] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.OrganizationUnit', N'Status') IS NULL ALTER TABLE [dbo].[OrganizationUnit] ADD [Status] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.OrganizationUnit', N'Note') IS NULL ALTER TABLE [dbo].[OrganizationUnit] ADD [Note] NVARCHAR(MAX) NULL;
GO

/****** Table OperationalLocation ******/
IF OBJECT_ID(N'[dbo].[OperationalLocation]', N'U') IS NULL
    CREATE TABLE [dbo].[OperationalLocation] ([Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_OperationalLocation] PRIMARY KEY CLUSTERED);
GO
IF COL_LENGTH(N'dbo.OperationalLocation', N'CreatedDate') IS NULL ALTER TABLE [dbo].[OperationalLocation] ADD [CreatedDate] DATETIME NOT NULL DEFAULT (GETDATE());
IF COL_LENGTH(N'dbo.OperationalLocation', N'CreatedBy') IS NULL ALTER TABLE [dbo].[OperationalLocation] ADD [CreatedBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.OperationalLocation', N'ModifiedDate') IS NULL ALTER TABLE [dbo].[OperationalLocation] ADD [ModifiedDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.OperationalLocation', N'ModifiedBy') IS NULL ALTER TABLE [dbo].[OperationalLocation] ADD [ModifiedBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.OperationalLocation', N'DeletedDate') IS NULL ALTER TABLE [dbo].[OperationalLocation] ADD [DeletedDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.OperationalLocation', N'DeleteddBy') IS NULL ALTER TABLE [dbo].[OperationalLocation] ADD [DeleteddBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.OperationalLocation', N'IsDeleted') IS NULL ALTER TABLE [dbo].[OperationalLocation] ADD [IsDeleted] BIT NOT NULL DEFAULT (0);
IF COL_LENGTH(N'dbo.OperationalLocation', N'CompanyId') IS NULL ALTER TABLE [dbo].[OperationalLocation] ADD [CompanyId] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.OperationalLocation', N'OrganizationUnitId') IS NULL ALTER TABLE [dbo].[OperationalLocation] ADD [OrganizationUnitId] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.OperationalLocation', N'Code') IS NULL ALTER TABLE [dbo].[OperationalLocation] ADD [Code] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.OperationalLocation', N'Name') IS NULL ALTER TABLE [dbo].[OperationalLocation] ADD [Name] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.OperationalLocation', N'LocationType') IS NULL ALTER TABLE [dbo].[OperationalLocation] ADD [LocationType] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.OperationalLocation', N'Address') IS NULL ALTER TABLE [dbo].[OperationalLocation] ADD [Address] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.OperationalLocation', N'Latitude') IS NULL ALTER TABLE [dbo].[OperationalLocation] ADD [Latitude] DECIMAL(18, 4) NULL;
IF COL_LENGTH(N'dbo.OperationalLocation', N'Longitude') IS NULL ALTER TABLE [dbo].[OperationalLocation] ADD [Longitude] DECIMAL(18, 4) NULL;
IF COL_LENGTH(N'dbo.OperationalLocation', N'ContactName') IS NULL ALTER TABLE [dbo].[OperationalLocation] ADD [ContactName] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.OperationalLocation', N'ContactPhone') IS NULL ALTER TABLE [dbo].[OperationalLocation] ADD [ContactPhone] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.OperationalLocation', N'Status') IS NULL ALTER TABLE [dbo].[OperationalLocation] ADD [Status] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.OperationalLocation', N'Note') IS NULL ALTER TABLE [dbo].[OperationalLocation] ADD [Note] NVARCHAR(MAX) NULL;
GO

/****** Table Customer ******/
IF OBJECT_ID(N'[dbo].[Customer]', N'U') IS NULL
    CREATE TABLE [dbo].[Customer] ([Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_Customer] PRIMARY KEY CLUSTERED);
GO
IF COL_LENGTH(N'dbo.Customer', N'CreatedDate') IS NULL ALTER TABLE [dbo].[Customer] ADD [CreatedDate] DATETIME NOT NULL DEFAULT (GETDATE());
IF COL_LENGTH(N'dbo.Customer', N'CreatedBy') IS NULL ALTER TABLE [dbo].[Customer] ADD [CreatedBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Customer', N'ModifiedDate') IS NULL ALTER TABLE [dbo].[Customer] ADD [ModifiedDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.Customer', N'ModifiedBy') IS NULL ALTER TABLE [dbo].[Customer] ADD [ModifiedBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Customer', N'DeletedDate') IS NULL ALTER TABLE [dbo].[Customer] ADD [DeletedDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.Customer', N'DeleteddBy') IS NULL ALTER TABLE [dbo].[Customer] ADD [DeleteddBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Customer', N'IsDeleted') IS NULL ALTER TABLE [dbo].[Customer] ADD [IsDeleted] BIT NOT NULL DEFAULT (0);
IF COL_LENGTH(N'dbo.Customer', N'CustomerType') IS NULL ALTER TABLE [dbo].[Customer] ADD [CustomerType] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Customer', N'StatusId') IS NULL ALTER TABLE [dbo].[Customer] ADD [StatusId] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Customer', N'CifNo') IS NULL ALTER TABLE [dbo].[Customer] ADD [CifNo] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Customer', N'DisplayName') IS NULL ALTER TABLE [dbo].[Customer] ADD [DisplayName] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Customer', N'PhoneNumber') IS NULL ALTER TABLE [dbo].[Customer] ADD [PhoneNumber] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Customer', N'Email') IS NULL ALTER TABLE [dbo].[Customer] ADD [Email] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Customer', N'TaxNo') IS NULL ALTER TABLE [dbo].[Customer] ADD [TaxNo] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Customer', N'FirstName') IS NULL ALTER TABLE [dbo].[Customer] ADD [FirstName] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Customer', N'LastName') IS NULL ALTER TABLE [dbo].[Customer] ADD [LastName] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Customer', N'Gender') IS NULL ALTER TABLE [dbo].[Customer] ADD [Gender] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Customer', N'DateOfBirth') IS NULL ALTER TABLE [dbo].[Customer] ADD [DateOfBirth] DATETIME NULL;
IF COL_LENGTH(N'dbo.Customer', N'IdentityNumber') IS NULL ALTER TABLE [dbo].[Customer] ADD [IdentityNumber] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Customer', N'IdentityDate') IS NULL ALTER TABLE [dbo].[Customer] ADD [IdentityDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.Customer', N'IdentityPlace') IS NULL ALTER TABLE [dbo].[Customer] ADD [IdentityPlace] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Customer', N'Occupation') IS NULL ALTER TABLE [dbo].[Customer] ADD [Occupation] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Customer', N'LegalName') IS NULL ALTER TABLE [dbo].[Customer] ADD [LegalName] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Customer', N'BusinessRegistrationNumber') IS NULL ALTER TABLE [dbo].[Customer] ADD [BusinessRegistrationNumber] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Customer', N'RegistrationDate') IS NULL ALTER TABLE [dbo].[Customer] ADD [RegistrationDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.Customer', N'RegistrationPlace') IS NULL ALTER TABLE [dbo].[Customer] ADD [RegistrationPlace] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Customer', N'CompanySizeId') IS NULL ALTER TABLE [dbo].[Customer] ADD [CompanySizeId] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Customer', N'IndustryId') IS NULL ALTER TABLE [dbo].[Customer] ADD [IndustryId] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Customer', N'LegalRepresentativeName') IS NULL ALTER TABLE [dbo].[Customer] ADD [LegalRepresentativeName] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Customer', N'ContactPersonName') IS NULL ALTER TABLE [dbo].[Customer] ADD [ContactPersonName] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Customer', N'ContactPersonPhone') IS NULL ALTER TABLE [dbo].[Customer] ADD [ContactPersonPhone] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Customer', N'ContactPersonEmail') IS NULL ALTER TABLE [dbo].[Customer] ADD [ContactPersonEmail] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Customer', N'ProvinceId') IS NULL ALTER TABLE [dbo].[Customer] ADD [ProvinceId] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Customer', N'Districtd') IS NULL ALTER TABLE [dbo].[Customer] ADD [Districtd] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Customer', N'WardId') IS NULL ALTER TABLE [dbo].[Customer] ADD [WardId] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Customer', N'Address') IS NULL ALTER TABLE [dbo].[Customer] ADD [Address] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Customer', N'SegmentId') IS NULL ALTER TABLE [dbo].[Customer] ADD [SegmentId] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Customer', N'SourceId') IS NULL ALTER TABLE [dbo].[Customer] ADD [SourceId] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Customer', N'RegionId') IS NULL ALTER TABLE [dbo].[Customer] ADD [RegionId] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Customer', N'AreaId') IS NULL ALTER TABLE [dbo].[Customer] ADD [AreaId] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Customer', N'BranchId') IS NULL ALTER TABLE [dbo].[Customer] ADD [BranchId] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Customer', N'DepartmentId') IS NULL ALTER TABLE [dbo].[Customer] ADD [DepartmentId] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Customer', N'UserAssignId') IS NULL ALTER TABLE [dbo].[Customer] ADD [UserAssignId] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Customer', N'Note') IS NULL ALTER TABLE [dbo].[Customer] ADD [Note] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Customer', N'ExternalData') IS NULL ALTER TABLE [dbo].[Customer] ADD [ExternalData] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Customer', N'ExternalValue') IS NULL ALTER TABLE [dbo].[Customer] ADD [ExternalValue] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Customer', N'CompanyId') IS NULL ALTER TABLE [dbo].[Customer] ADD [CompanyId] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Customer', N'CompanyName') IS NULL ALTER TABLE [dbo].[Customer] ADD [CompanyName] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Customer', N'Code') IS NULL ALTER TABLE [dbo].[Customer] ADD [Code] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Customer', N'Name') IS NULL ALTER TABLE [dbo].[Customer] ADD [Name] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Customer', N'TaxCode') IS NULL ALTER TABLE [dbo].[Customer] ADD [TaxCode] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Customer', N'Phone') IS NULL ALTER TABLE [dbo].[Customer] ADD [Phone] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Customer', N'Status') IS NULL ALTER TABLE [dbo].[Customer] ADD [Status] NVARCHAR(MAX) NULL;
GO

/****** Table CustomerContact ******/
IF OBJECT_ID(N'[dbo].[CustomerContact]', N'U') IS NULL
    CREATE TABLE [dbo].[CustomerContact] ([Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_CustomerContact] PRIMARY KEY CLUSTERED);
GO
IF COL_LENGTH(N'dbo.CustomerContact', N'CreatedDate') IS NULL ALTER TABLE [dbo].[CustomerContact] ADD [CreatedDate] DATETIME NOT NULL DEFAULT (GETDATE());
IF COL_LENGTH(N'dbo.CustomerContact', N'CreatedBy') IS NULL ALTER TABLE [dbo].[CustomerContact] ADD [CreatedBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.CustomerContact', N'ModifiedDate') IS NULL ALTER TABLE [dbo].[CustomerContact] ADD [ModifiedDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.CustomerContact', N'ModifiedBy') IS NULL ALTER TABLE [dbo].[CustomerContact] ADD [ModifiedBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.CustomerContact', N'DeletedDate') IS NULL ALTER TABLE [dbo].[CustomerContact] ADD [DeletedDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.CustomerContact', N'DeleteddBy') IS NULL ALTER TABLE [dbo].[CustomerContact] ADD [DeleteddBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.CustomerContact', N'IsDeleted') IS NULL ALTER TABLE [dbo].[CustomerContact] ADD [IsDeleted] BIT NOT NULL DEFAULT (0);
IF COL_LENGTH(N'dbo.CustomerContact', N'CustomerId') IS NULL ALTER TABLE [dbo].[CustomerContact] ADD [CustomerId] UNIQUEIDENTIFIER NOT NULL DEFAULT ('00000000-0000-0000-0000-000000000000');
IF COL_LENGTH(N'dbo.CustomerContact', N'FullName') IS NULL ALTER TABLE [dbo].[CustomerContact] ADD [FullName] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.CustomerContact', N'Position') IS NULL ALTER TABLE [dbo].[CustomerContact] ADD [Position] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.CustomerContact', N'Phone') IS NULL ALTER TABLE [dbo].[CustomerContact] ADD [Phone] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.CustomerContact', N'Email') IS NULL ALTER TABLE [dbo].[CustomerContact] ADD [Email] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.CustomerContact', N'IsPrimary') IS NULL ALTER TABLE [dbo].[CustomerContact] ADD [IsPrimary] BIT NOT NULL DEFAULT (0);
IF COL_LENGTH(N'dbo.CustomerContact', N'Note') IS NULL ALTER TABLE [dbo].[CustomerContact] ADD [Note] NVARCHAR(MAX) NULL;
GO

/****** Table CustomerDeliveryLocation ******/
IF OBJECT_ID(N'[dbo].[CustomerDeliveryLocation]', N'U') IS NULL
    CREATE TABLE [dbo].[CustomerDeliveryLocation] ([Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_CustomerDeliveryLocation] PRIMARY KEY CLUSTERED);
GO
IF COL_LENGTH(N'dbo.CustomerDeliveryLocation', N'CreatedDate') IS NULL ALTER TABLE [dbo].[CustomerDeliveryLocation] ADD [CreatedDate] DATETIME NOT NULL DEFAULT (GETDATE());
IF COL_LENGTH(N'dbo.CustomerDeliveryLocation', N'CreatedBy') IS NULL ALTER TABLE [dbo].[CustomerDeliveryLocation] ADD [CreatedBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.CustomerDeliveryLocation', N'ModifiedDate') IS NULL ALTER TABLE [dbo].[CustomerDeliveryLocation] ADD [ModifiedDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.CustomerDeliveryLocation', N'ModifiedBy') IS NULL ALTER TABLE [dbo].[CustomerDeliveryLocation] ADD [ModifiedBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.CustomerDeliveryLocation', N'DeletedDate') IS NULL ALTER TABLE [dbo].[CustomerDeliveryLocation] ADD [DeletedDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.CustomerDeliveryLocation', N'DeleteddBy') IS NULL ALTER TABLE [dbo].[CustomerDeliveryLocation] ADD [DeleteddBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.CustomerDeliveryLocation', N'IsDeleted') IS NULL ALTER TABLE [dbo].[CustomerDeliveryLocation] ADD [IsDeleted] BIT NOT NULL DEFAULT (0);
IF COL_LENGTH(N'dbo.CustomerDeliveryLocation', N'CustomerId') IS NULL ALTER TABLE [dbo].[CustomerDeliveryLocation] ADD [CustomerId] UNIQUEIDENTIFIER NOT NULL DEFAULT ('00000000-0000-0000-0000-000000000000');
IF COL_LENGTH(N'dbo.CustomerDeliveryLocation', N'Code') IS NULL ALTER TABLE [dbo].[CustomerDeliveryLocation] ADD [Code] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.CustomerDeliveryLocation', N'Name') IS NULL ALTER TABLE [dbo].[CustomerDeliveryLocation] ADD [Name] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.CustomerDeliveryLocation', N'Address') IS NULL ALTER TABLE [dbo].[CustomerDeliveryLocation] ADD [Address] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.CustomerDeliveryLocation', N'ContactName') IS NULL ALTER TABLE [dbo].[CustomerDeliveryLocation] ADD [ContactName] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.CustomerDeliveryLocation', N'ContactPhone') IS NULL ALTER TABLE [dbo].[CustomerDeliveryLocation] ADD [ContactPhone] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.CustomerDeliveryLocation', N'DeliveryInstructions') IS NULL ALTER TABLE [dbo].[CustomerDeliveryLocation] ADD [DeliveryInstructions] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.CustomerDeliveryLocation', N'Status') IS NULL ALTER TABLE [dbo].[CustomerDeliveryLocation] ADD [Status] NVARCHAR(MAX) NULL;
GO

/****** Table Supplier ******/
IF OBJECT_ID(N'[dbo].[Supplier]', N'U') IS NULL
    CREATE TABLE [dbo].[Supplier] ([Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_Supplier] PRIMARY KEY CLUSTERED);
GO
IF COL_LENGTH(N'dbo.Supplier', N'CreatedDate') IS NULL ALTER TABLE [dbo].[Supplier] ADD [CreatedDate] DATETIME NOT NULL DEFAULT (GETDATE());
IF COL_LENGTH(N'dbo.Supplier', N'CreatedBy') IS NULL ALTER TABLE [dbo].[Supplier] ADD [CreatedBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Supplier', N'ModifiedDate') IS NULL ALTER TABLE [dbo].[Supplier] ADD [ModifiedDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.Supplier', N'ModifiedBy') IS NULL ALTER TABLE [dbo].[Supplier] ADD [ModifiedBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Supplier', N'DeletedDate') IS NULL ALTER TABLE [dbo].[Supplier] ADD [DeletedDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.Supplier', N'DeleteddBy') IS NULL ALTER TABLE [dbo].[Supplier] ADD [DeleteddBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Supplier', N'IsDeleted') IS NULL ALTER TABLE [dbo].[Supplier] ADD [IsDeleted] BIT NOT NULL DEFAULT (0);
IF COL_LENGTH(N'dbo.Supplier', N'CompanyId') IS NULL ALTER TABLE [dbo].[Supplier] ADD [CompanyId] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Supplier', N'Code') IS NULL ALTER TABLE [dbo].[Supplier] ADD [Code] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Supplier', N'Name') IS NULL ALTER TABLE [dbo].[Supplier] ADD [Name] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Supplier', N'TaxCode') IS NULL ALTER TABLE [dbo].[Supplier] ADD [TaxCode] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Supplier', N'Address') IS NULL ALTER TABLE [dbo].[Supplier] ADD [Address] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Supplier', N'Phone') IS NULL ALTER TABLE [dbo].[Supplier] ADD [Phone] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Supplier', N'Email') IS NULL ALTER TABLE [dbo].[Supplier] ADD [Email] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Supplier', N'ContactName') IS NULL ALTER TABLE [dbo].[Supplier] ADD [ContactName] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Supplier', N'Status') IS NULL ALTER TABLE [dbo].[Supplier] ADD [Status] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Supplier', N'Note') IS NULL ALTER TABLE [dbo].[Supplier] ADD [Note] NVARCHAR(MAX) NULL;
GO

/****** Table SupplierMaterial ******/
IF OBJECT_ID(N'[dbo].[SupplierMaterial]', N'U') IS NULL
    CREATE TABLE [dbo].[SupplierMaterial] ([Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_SupplierMaterial] PRIMARY KEY CLUSTERED);
GO
IF COL_LENGTH(N'dbo.SupplierMaterial', N'CreatedDate') IS NULL ALTER TABLE [dbo].[SupplierMaterial] ADD [CreatedDate] DATETIME NOT NULL DEFAULT (GETDATE());
IF COL_LENGTH(N'dbo.SupplierMaterial', N'CreatedBy') IS NULL ALTER TABLE [dbo].[SupplierMaterial] ADD [CreatedBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.SupplierMaterial', N'ModifiedDate') IS NULL ALTER TABLE [dbo].[SupplierMaterial] ADD [ModifiedDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.SupplierMaterial', N'ModifiedBy') IS NULL ALTER TABLE [dbo].[SupplierMaterial] ADD [ModifiedBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.SupplierMaterial', N'DeletedDate') IS NULL ALTER TABLE [dbo].[SupplierMaterial] ADD [DeletedDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.SupplierMaterial', N'DeleteddBy') IS NULL ALTER TABLE [dbo].[SupplierMaterial] ADD [DeleteddBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.SupplierMaterial', N'IsDeleted') IS NULL ALTER TABLE [dbo].[SupplierMaterial] ADD [IsDeleted] BIT NOT NULL DEFAULT (0);
IF COL_LENGTH(N'dbo.SupplierMaterial', N'SupplierId') IS NULL ALTER TABLE [dbo].[SupplierMaterial] ADD [SupplierId] UNIQUEIDENTIFIER NOT NULL DEFAULT ('00000000-0000-0000-0000-000000000000');
IF COL_LENGTH(N'dbo.SupplierMaterial', N'IngredientId') IS NULL ALTER TABLE [dbo].[SupplierMaterial] ADD [IngredientId] UNIQUEIDENTIFIER NOT NULL DEFAULT ('00000000-0000-0000-0000-000000000000');
IF COL_LENGTH(N'dbo.SupplierMaterial', N'SupplierMaterialCode') IS NULL ALTER TABLE [dbo].[SupplierMaterial] ADD [SupplierMaterialCode] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.SupplierMaterial', N'UnitPrice') IS NULL ALTER TABLE [dbo].[SupplierMaterial] ADD [UnitPrice] DECIMAL(18, 4) NULL;
IF COL_LENGTH(N'dbo.SupplierMaterial', N'MinimumOrderQuantity') IS NULL ALTER TABLE [dbo].[SupplierMaterial] ADD [MinimumOrderQuantity] DECIMAL(18, 4) NULL;
IF COL_LENGTH(N'dbo.SupplierMaterial', N'LeadTimeDays') IS NULL ALTER TABLE [dbo].[SupplierMaterial] ADD [LeadTimeDays] INT NULL;
IF COL_LENGTH(N'dbo.SupplierMaterial', N'QualityRequirement') IS NULL ALTER TABLE [dbo].[SupplierMaterial] ADD [QualityRequirement] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.SupplierMaterial', N'EffectiveFrom') IS NULL ALTER TABLE [dbo].[SupplierMaterial] ADD [EffectiveFrom] DATETIME NULL;
IF COL_LENGTH(N'dbo.SupplierMaterial', N'EffectiveTo') IS NULL ALTER TABLE [dbo].[SupplierMaterial] ADD [EffectiveTo] DATETIME NULL;
IF COL_LENGTH(N'dbo.SupplierMaterial', N'IsPreferred') IS NULL ALTER TABLE [dbo].[SupplierMaterial] ADD [IsPreferred] BIT NOT NULL DEFAULT (0);
IF COL_LENGTH(N'dbo.SupplierMaterial', N'Status') IS NULL ALTER TABLE [dbo].[SupplierMaterial] ADD [Status] NVARCHAR(MAX) NULL;
GO

/****** Table Ingredient ******/
IF OBJECT_ID(N'[dbo].[Ingredient]', N'U') IS NULL
    CREATE TABLE [dbo].[Ingredient] ([Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_Ingredient] PRIMARY KEY CLUSTERED);
GO
IF COL_LENGTH(N'dbo.Ingredient', N'CreatedDate') IS NULL ALTER TABLE [dbo].[Ingredient] ADD [CreatedDate] DATETIME NOT NULL DEFAULT (GETDATE());
IF COL_LENGTH(N'dbo.Ingredient', N'CreatedBy') IS NULL ALTER TABLE [dbo].[Ingredient] ADD [CreatedBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Ingredient', N'ModifiedDate') IS NULL ALTER TABLE [dbo].[Ingredient] ADD [ModifiedDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.Ingredient', N'ModifiedBy') IS NULL ALTER TABLE [dbo].[Ingredient] ADD [ModifiedBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Ingredient', N'DeletedDate') IS NULL ALTER TABLE [dbo].[Ingredient] ADD [DeletedDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.Ingredient', N'DeleteddBy') IS NULL ALTER TABLE [dbo].[Ingredient] ADD [DeleteddBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Ingredient', N'IsDeleted') IS NULL ALTER TABLE [dbo].[Ingredient] ADD [IsDeleted] BIT NOT NULL DEFAULT (0);
IF COL_LENGTH(N'dbo.Ingredient', N'CompanyId') IS NULL ALTER TABLE [dbo].[Ingredient] ADD [CompanyId] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Ingredient', N'IngredientCategoryId') IS NULL ALTER TABLE [dbo].[Ingredient] ADD [IngredientCategoryId] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Ingredient', N'BaseUnitId') IS NULL ALTER TABLE [dbo].[Ingredient] ADD [BaseUnitId] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Ingredient', N'Code') IS NULL ALTER TABLE [dbo].[Ingredient] ADD [Code] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Ingredient', N'Name') IS NULL ALTER TABLE [dbo].[Ingredient] ADD [Name] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Ingredient', N'Specification') IS NULL ALTER TABLE [dbo].[Ingredient] ADD [Specification] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Ingredient', N'StorageCondition') IS NULL ALTER TABLE [dbo].[Ingredient] ADD [StorageCondition] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Ingredient', N'IsExpiryManaged') IS NULL ALTER TABLE [dbo].[Ingredient] ADD [IsExpiryManaged] BIT NOT NULL DEFAULT (0);
IF COL_LENGTH(N'dbo.Ingredient', N'ShelfLifeDays') IS NULL ALTER TABLE [dbo].[Ingredient] ADD [ShelfLifeDays] INT NULL;
IF COL_LENGTH(N'dbo.Ingredient', N'MinimumStock') IS NULL ALTER TABLE [dbo].[Ingredient] ADD [MinimumStock] DECIMAL(18, 4) NULL;
IF COL_LENGTH(N'dbo.Ingredient', N'SafetyStock') IS NULL ALTER TABLE [dbo].[Ingredient] ADD [SafetyStock] DECIMAL(18, 4) NULL;
IF COL_LENGTH(N'dbo.Ingredient', N'Status') IS NULL ALTER TABLE [dbo].[Ingredient] ADD [Status] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Ingredient', N'Note') IS NULL ALTER TABLE [dbo].[Ingredient] ADD [Note] NVARCHAR(MAX) NULL;
GO

/****** Table Dish ******/
IF OBJECT_ID(N'[dbo].[Dish]', N'U') IS NULL
    CREATE TABLE [dbo].[Dish] ([Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_Dish] PRIMARY KEY CLUSTERED);
GO
IF COL_LENGTH(N'dbo.Dish', N'CreatedDate') IS NULL ALTER TABLE [dbo].[Dish] ADD [CreatedDate] DATETIME NOT NULL DEFAULT (GETDATE());
IF COL_LENGTH(N'dbo.Dish', N'CreatedBy') IS NULL ALTER TABLE [dbo].[Dish] ADD [CreatedBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Dish', N'ModifiedDate') IS NULL ALTER TABLE [dbo].[Dish] ADD [ModifiedDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.Dish', N'ModifiedBy') IS NULL ALTER TABLE [dbo].[Dish] ADD [ModifiedBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Dish', N'DeletedDate') IS NULL ALTER TABLE [dbo].[Dish] ADD [DeletedDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.Dish', N'DeleteddBy') IS NULL ALTER TABLE [dbo].[Dish] ADD [DeleteddBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Dish', N'IsDeleted') IS NULL ALTER TABLE [dbo].[Dish] ADD [IsDeleted] BIT NOT NULL DEFAULT (0);
IF COL_LENGTH(N'dbo.Dish', N'DishCategoryId') IS NULL ALTER TABLE [dbo].[Dish] ADD [DishCategoryId] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Dish', N'DefaultUnitId') IS NULL ALTER TABLE [dbo].[Dish] ADD [DefaultUnitId] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Dish', N'Code') IS NULL ALTER TABLE [dbo].[Dish] ADD [Code] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Dish', N'Name') IS NULL ALTER TABLE [dbo].[Dish] ADD [Name] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Dish', N'Description') IS NULL ALTER TABLE [dbo].[Dish] ADD [Description] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Dish', N'StandardServingSize') IS NULL ALTER TABLE [dbo].[Dish] ADD [StandardServingSize] DECIMAL(18, 4) NULL;
IF COL_LENGTH(N'dbo.Dish', N'Status') IS NULL ALTER TABLE [dbo].[Dish] ADD [Status] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Dish', N'Note') IS NULL ALTER TABLE [dbo].[Dish] ADD [Note] NVARCHAR(MAX) NULL;
GO

/****** Table Warehouse ******/
IF OBJECT_ID(N'[dbo].[Warehouse]', N'U') IS NULL
    CREATE TABLE [dbo].[Warehouse] ([Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_Warehouse] PRIMARY KEY CLUSTERED);
GO
IF COL_LENGTH(N'dbo.Warehouse', N'CreatedDate') IS NULL ALTER TABLE [dbo].[Warehouse] ADD [CreatedDate] DATETIME NOT NULL DEFAULT (GETDATE());
IF COL_LENGTH(N'dbo.Warehouse', N'CreatedBy') IS NULL ALTER TABLE [dbo].[Warehouse] ADD [CreatedBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Warehouse', N'ModifiedDate') IS NULL ALTER TABLE [dbo].[Warehouse] ADD [ModifiedDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.Warehouse', N'ModifiedBy') IS NULL ALTER TABLE [dbo].[Warehouse] ADD [ModifiedBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Warehouse', N'DeletedDate') IS NULL ALTER TABLE [dbo].[Warehouse] ADD [DeletedDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.Warehouse', N'DeleteddBy') IS NULL ALTER TABLE [dbo].[Warehouse] ADD [DeleteddBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Warehouse', N'IsDeleted') IS NULL ALTER TABLE [dbo].[Warehouse] ADD [IsDeleted] BIT NOT NULL DEFAULT (0);
IF COL_LENGTH(N'dbo.Warehouse', N'CompanyId') IS NULL ALTER TABLE [dbo].[Warehouse] ADD [CompanyId] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Warehouse', N'OrganizationUnitId') IS NULL ALTER TABLE [dbo].[Warehouse] ADD [OrganizationUnitId] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Warehouse', N'Code') IS NULL ALTER TABLE [dbo].[Warehouse] ADD [Code] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Warehouse', N'Name') IS NULL ALTER TABLE [dbo].[Warehouse] ADD [Name] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Warehouse', N'WarehouseType') IS NULL ALTER TABLE [dbo].[Warehouse] ADD [WarehouseType] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Warehouse', N'Address') IS NULL ALTER TABLE [dbo].[Warehouse] ADD [Address] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Warehouse', N'Status') IS NULL ALTER TABLE [dbo].[Warehouse] ADD [Status] NVARCHAR(MAX) NULL;
GO

/****** Table StorageZone ******/
IF OBJECT_ID(N'[dbo].[StorageZone]', N'U') IS NULL
    CREATE TABLE [dbo].[StorageZone] ([Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_StorageZone] PRIMARY KEY CLUSTERED);
GO
IF COL_LENGTH(N'dbo.StorageZone', N'CreatedDate') IS NULL ALTER TABLE [dbo].[StorageZone] ADD [CreatedDate] DATETIME NOT NULL DEFAULT (GETDATE());
IF COL_LENGTH(N'dbo.StorageZone', N'CreatedBy') IS NULL ALTER TABLE [dbo].[StorageZone] ADD [CreatedBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.StorageZone', N'ModifiedDate') IS NULL ALTER TABLE [dbo].[StorageZone] ADD [ModifiedDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.StorageZone', N'ModifiedBy') IS NULL ALTER TABLE [dbo].[StorageZone] ADD [ModifiedBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.StorageZone', N'DeletedDate') IS NULL ALTER TABLE [dbo].[StorageZone] ADD [DeletedDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.StorageZone', N'DeleteddBy') IS NULL ALTER TABLE [dbo].[StorageZone] ADD [DeleteddBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.StorageZone', N'IsDeleted') IS NULL ALTER TABLE [dbo].[StorageZone] ADD [IsDeleted] BIT NOT NULL DEFAULT (0);
IF COL_LENGTH(N'dbo.StorageZone', N'WarehouseId') IS NULL ALTER TABLE [dbo].[StorageZone] ADD [WarehouseId] UNIQUEIDENTIFIER NOT NULL DEFAULT ('00000000-0000-0000-0000-000000000000');
IF COL_LENGTH(N'dbo.StorageZone', N'Code') IS NULL ALTER TABLE [dbo].[StorageZone] ADD [Code] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.StorageZone', N'Name') IS NULL ALTER TABLE [dbo].[StorageZone] ADD [Name] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.StorageZone', N'TemperatureRange') IS NULL ALTER TABLE [dbo].[StorageZone] ADD [TemperatureRange] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.StorageZone', N'Status') IS NULL ALTER TABLE [dbo].[StorageZone] ADD [Status] NVARCHAR(MAX) NULL;
GO

/****** Table StorageLocation ******/
IF OBJECT_ID(N'[dbo].[StorageLocation]', N'U') IS NULL
    CREATE TABLE [dbo].[StorageLocation] ([Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_StorageLocation] PRIMARY KEY CLUSTERED);
GO
IF COL_LENGTH(N'dbo.StorageLocation', N'CreatedDate') IS NULL ALTER TABLE [dbo].[StorageLocation] ADD [CreatedDate] DATETIME NOT NULL DEFAULT (GETDATE());
IF COL_LENGTH(N'dbo.StorageLocation', N'CreatedBy') IS NULL ALTER TABLE [dbo].[StorageLocation] ADD [CreatedBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.StorageLocation', N'ModifiedDate') IS NULL ALTER TABLE [dbo].[StorageLocation] ADD [ModifiedDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.StorageLocation', N'ModifiedBy') IS NULL ALTER TABLE [dbo].[StorageLocation] ADD [ModifiedBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.StorageLocation', N'DeletedDate') IS NULL ALTER TABLE [dbo].[StorageLocation] ADD [DeletedDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.StorageLocation', N'DeleteddBy') IS NULL ALTER TABLE [dbo].[StorageLocation] ADD [DeleteddBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.StorageLocation', N'IsDeleted') IS NULL ALTER TABLE [dbo].[StorageLocation] ADD [IsDeleted] BIT NOT NULL DEFAULT (0);
IF COL_LENGTH(N'dbo.StorageLocation', N'WarehouseId') IS NULL ALTER TABLE [dbo].[StorageLocation] ADD [WarehouseId] UNIQUEIDENTIFIER NOT NULL DEFAULT ('00000000-0000-0000-0000-000000000000');
IF COL_LENGTH(N'dbo.StorageLocation', N'StorageZoneId') IS NULL ALTER TABLE [dbo].[StorageLocation] ADD [StorageZoneId] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.StorageLocation', N'Code') IS NULL ALTER TABLE [dbo].[StorageLocation] ADD [Code] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.StorageLocation', N'Name') IS NULL ALTER TABLE [dbo].[StorageLocation] ADD [Name] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.StorageLocation', N'LocationType') IS NULL ALTER TABLE [dbo].[StorageLocation] ADD [LocationType] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.StorageLocation', N'Status') IS NULL ALTER TABLE [dbo].[StorageLocation] ADD [Status] NVARCHAR(MAX) NULL;
GO

/****** Table Employee ******/
IF OBJECT_ID(N'[dbo].[Employee]', N'U') IS NULL
    CREATE TABLE [dbo].[Employee] ([Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_Employee] PRIMARY KEY CLUSTERED);
GO
IF COL_LENGTH(N'dbo.Employee', N'CreatedDate') IS NULL ALTER TABLE [dbo].[Employee] ADD [CreatedDate] DATETIME NOT NULL DEFAULT (GETDATE());
IF COL_LENGTH(N'dbo.Employee', N'CreatedBy') IS NULL ALTER TABLE [dbo].[Employee] ADD [CreatedBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Employee', N'ModifiedDate') IS NULL ALTER TABLE [dbo].[Employee] ADD [ModifiedDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.Employee', N'ModifiedBy') IS NULL ALTER TABLE [dbo].[Employee] ADD [ModifiedBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Employee', N'DeletedDate') IS NULL ALTER TABLE [dbo].[Employee] ADD [DeletedDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.Employee', N'DeleteddBy') IS NULL ALTER TABLE [dbo].[Employee] ADD [DeleteddBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Employee', N'IsDeleted') IS NULL ALTER TABLE [dbo].[Employee] ADD [IsDeleted] BIT NOT NULL DEFAULT (0);
IF COL_LENGTH(N'dbo.Employee', N'Code') IS NULL ALTER TABLE [dbo].[Employee] ADD [Code] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Employee', N'UserId') IS NULL ALTER TABLE [dbo].[Employee] ADD [UserId] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Employee', N'FullName') IS NULL ALTER TABLE [dbo].[Employee] ADD [FullName] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Employee', N'Gender') IS NULL ALTER TABLE [dbo].[Employee] ADD [Gender] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Employee', N'DateOfBirth') IS NULL ALTER TABLE [dbo].[Employee] ADD [DateOfBirth] DATETIME NULL;
IF COL_LENGTH(N'dbo.Employee', N'IdentityNumber') IS NULL ALTER TABLE [dbo].[Employee] ADD [IdentityNumber] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Employee', N'Title') IS NULL ALTER TABLE [dbo].[Employee] ADD [Title] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Employee', N'JobPosition') IS NULL ALTER TABLE [dbo].[Employee] ADD [JobPosition] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Employee', N'EmployeeStatus') IS NULL ALTER TABLE [dbo].[Employee] ADD [EmployeeStatus] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Employee', N'JoinDate') IS NULL ALTER TABLE [dbo].[Employee] ADD [JoinDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.Employee', N'TerminationDate') IS NULL ALTER TABLE [dbo].[Employee] ADD [TerminationDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.Employee', N'WorkEmail') IS NULL ALTER TABLE [dbo].[Employee] ADD [WorkEmail] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Employee', N'WorkPhone') IS NULL ALTER TABLE [dbo].[Employee] ADD [WorkPhone] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Employee', N'MobilePhone') IS NULL ALTER TABLE [dbo].[Employee] ADD [MobilePhone] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Employee', N'RegionId') IS NULL ALTER TABLE [dbo].[Employee] ADD [RegionId] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Employee', N'AreaId') IS NULL ALTER TABLE [dbo].[Employee] ADD [AreaId] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Employee', N'BranchId') IS NULL ALTER TABLE [dbo].[Employee] ADD [BranchId] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Employee', N'DepartmentId') IS NULL ALTER TABLE [dbo].[Employee] ADD [DepartmentId] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Employee', N'DirectManagerId') IS NULL ALTER TABLE [dbo].[Employee] ADD [DirectManagerId] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Employee', N'ExternalValue') IS NULL ALTER TABLE [dbo].[Employee] ADD [ExternalValue] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Employee', N'ExternalData') IS NULL ALTER TABLE [dbo].[Employee] ADD [ExternalData] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Employee', N'Note') IS NULL ALTER TABLE [dbo].[Employee] ADD [Note] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Employee', N'CompanyId') IS NULL ALTER TABLE [dbo].[Employee] ADD [CompanyId] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Employee', N'OrganizationUnitId') IS NULL ALTER TABLE [dbo].[Employee] ADD [OrganizationUnitId] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Employee', N'PositionId') IS NULL ALTER TABLE [dbo].[Employee] ADD [PositionId] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Employee', N'Phone') IS NULL ALTER TABLE [dbo].[Employee] ADD [Phone] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Employee', N'Email') IS NULL ALTER TABLE [dbo].[Employee] ADD [Email] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Employee', N'Address') IS NULL ALTER TABLE [dbo].[Employee] ADD [Address] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Employee', N'HireDate') IS NULL ALTER TABLE [dbo].[Employee] ADD [HireDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.Employee', N'Status') IS NULL ALTER TABLE [dbo].[Employee] ADD [Status] NVARCHAR(MAX) NULL;
GO

/****** Table Vehicle ******/
IF OBJECT_ID(N'[dbo].[Vehicle]', N'U') IS NULL
    CREATE TABLE [dbo].[Vehicle] ([Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_Vehicle] PRIMARY KEY CLUSTERED);
GO
IF COL_LENGTH(N'dbo.Vehicle', N'CreatedDate') IS NULL ALTER TABLE [dbo].[Vehicle] ADD [CreatedDate] DATETIME NOT NULL DEFAULT (GETDATE());
IF COL_LENGTH(N'dbo.Vehicle', N'CreatedBy') IS NULL ALTER TABLE [dbo].[Vehicle] ADD [CreatedBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Vehicle', N'ModifiedDate') IS NULL ALTER TABLE [dbo].[Vehicle] ADD [ModifiedDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.Vehicle', N'ModifiedBy') IS NULL ALTER TABLE [dbo].[Vehicle] ADD [ModifiedBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Vehicle', N'DeletedDate') IS NULL ALTER TABLE [dbo].[Vehicle] ADD [DeletedDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.Vehicle', N'DeleteddBy') IS NULL ALTER TABLE [dbo].[Vehicle] ADD [DeleteddBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Vehicle', N'IsDeleted') IS NULL ALTER TABLE [dbo].[Vehicle] ADD [IsDeleted] BIT NOT NULL DEFAULT (0);
IF COL_LENGTH(N'dbo.Vehicle', N'CompanyId') IS NULL ALTER TABLE [dbo].[Vehicle] ADD [CompanyId] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Vehicle', N'OrganizationUnitId') IS NULL ALTER TABLE [dbo].[Vehicle] ADD [OrganizationUnitId] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Vehicle', N'Code') IS NULL ALTER TABLE [dbo].[Vehicle] ADD [Code] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Vehicle', N'LicensePlate') IS NULL ALTER TABLE [dbo].[Vehicle] ADD [LicensePlate] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Vehicle', N'Name') IS NULL ALTER TABLE [dbo].[Vehicle] ADD [Name] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Vehicle', N'VehicleType') IS NULL ALTER TABLE [dbo].[Vehicle] ADD [VehicleType] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Vehicle', N'Brand') IS NULL ALTER TABLE [dbo].[Vehicle] ADD [Brand] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Vehicle', N'LoadCapacityKg') IS NULL ALTER TABLE [dbo].[Vehicle] ADD [LoadCapacityKg] DECIMAL(18, 4) NULL;
IF COL_LENGTH(N'dbo.Vehicle', N'VolumeM3') IS NULL ALTER TABLE [dbo].[Vehicle] ADD [VolumeM3] DECIMAL(18, 4) NULL;
IF COL_LENGTH(N'dbo.Vehicle', N'IsRefrigerated') IS NULL ALTER TABLE [dbo].[Vehicle] ADD [IsRefrigerated] BIT NOT NULL DEFAULT (0);
IF COL_LENGTH(N'dbo.Vehicle', N'DefaultDriverId') IS NULL ALTER TABLE [dbo].[Vehicle] ADD [DefaultDriverId] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Vehicle', N'RegistrationExpiryDate') IS NULL ALTER TABLE [dbo].[Vehicle] ADD [RegistrationExpiryDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.Vehicle', N'InsuranceExpiryDate') IS NULL ALTER TABLE [dbo].[Vehicle] ADD [InsuranceExpiryDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.Vehicle', N'Status') IS NULL ALTER TABLE [dbo].[Vehicle] ADD [Status] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Vehicle', N'Note') IS NULL ALTER TABLE [dbo].[Vehicle] ADD [Note] NVARCHAR(MAX) NULL;
GO

/****** Table IngredientCategory ******/
IF OBJECT_ID(N'[dbo].[IngredientCategory]', N'U') IS NULL
    CREATE TABLE [dbo].[IngredientCategory] ([Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_IngredientCategory] PRIMARY KEY CLUSTERED);
GO
IF COL_LENGTH(N'dbo.IngredientCategory', N'CreatedDate') IS NULL ALTER TABLE [dbo].[IngredientCategory] ADD [CreatedDate] DATETIME NOT NULL DEFAULT (GETDATE());
IF COL_LENGTH(N'dbo.IngredientCategory', N'CreatedBy') IS NULL ALTER TABLE [dbo].[IngredientCategory] ADD [CreatedBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.IngredientCategory', N'ModifiedDate') IS NULL ALTER TABLE [dbo].[IngredientCategory] ADD [ModifiedDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.IngredientCategory', N'ModifiedBy') IS NULL ALTER TABLE [dbo].[IngredientCategory] ADD [ModifiedBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.IngredientCategory', N'DeletedDate') IS NULL ALTER TABLE [dbo].[IngredientCategory] ADD [DeletedDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.IngredientCategory', N'DeleteddBy') IS NULL ALTER TABLE [dbo].[IngredientCategory] ADD [DeleteddBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.IngredientCategory', N'IsDeleted') IS NULL ALTER TABLE [dbo].[IngredientCategory] ADD [IsDeleted] BIT NOT NULL DEFAULT (0);
IF COL_LENGTH(N'dbo.IngredientCategory', N'ParentId') IS NULL ALTER TABLE [dbo].[IngredientCategory] ADD [ParentId] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.IngredientCategory', N'Code') IS NULL ALTER TABLE [dbo].[IngredientCategory] ADD [Code] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.IngredientCategory', N'Name') IS NULL ALTER TABLE [dbo].[IngredientCategory] ADD [Name] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.IngredientCategory', N'Description') IS NULL ALTER TABLE [dbo].[IngredientCategory] ADD [Description] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.IngredientCategory', N'Status') IS NULL ALTER TABLE [dbo].[IngredientCategory] ADD [Status] NVARCHAR(MAX) NULL;
GO

/****** Table UnitOfMeasure ******/
IF OBJECT_ID(N'[dbo].[UnitOfMeasure]', N'U') IS NULL
    CREATE TABLE [dbo].[UnitOfMeasure] ([Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_UnitOfMeasure] PRIMARY KEY CLUSTERED);
GO
IF COL_LENGTH(N'dbo.UnitOfMeasure', N'CreatedDate') IS NULL ALTER TABLE [dbo].[UnitOfMeasure] ADD [CreatedDate] DATETIME NOT NULL DEFAULT (GETDATE());
IF COL_LENGTH(N'dbo.UnitOfMeasure', N'CreatedBy') IS NULL ALTER TABLE [dbo].[UnitOfMeasure] ADD [CreatedBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.UnitOfMeasure', N'ModifiedDate') IS NULL ALTER TABLE [dbo].[UnitOfMeasure] ADD [ModifiedDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.UnitOfMeasure', N'ModifiedBy') IS NULL ALTER TABLE [dbo].[UnitOfMeasure] ADD [ModifiedBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.UnitOfMeasure', N'DeletedDate') IS NULL ALTER TABLE [dbo].[UnitOfMeasure] ADD [DeletedDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.UnitOfMeasure', N'DeleteddBy') IS NULL ALTER TABLE [dbo].[UnitOfMeasure] ADD [DeleteddBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.UnitOfMeasure', N'IsDeleted') IS NULL ALTER TABLE [dbo].[UnitOfMeasure] ADD [IsDeleted] BIT NOT NULL DEFAULT (0);
IF COL_LENGTH(N'dbo.UnitOfMeasure', N'Code') IS NULL ALTER TABLE [dbo].[UnitOfMeasure] ADD [Code] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.UnitOfMeasure', N'Name') IS NULL ALTER TABLE [dbo].[UnitOfMeasure] ADD [Name] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.UnitOfMeasure', N'Symbol') IS NULL ALTER TABLE [dbo].[UnitOfMeasure] ADD [Symbol] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.UnitOfMeasure', N'UnitType') IS NULL ALTER TABLE [dbo].[UnitOfMeasure] ADD [UnitType] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.UnitOfMeasure', N'Status') IS NULL ALTER TABLE [dbo].[UnitOfMeasure] ADD [Status] NVARCHAR(MAX) NULL;
GO

/****** Table DishCategory ******/
IF OBJECT_ID(N'[dbo].[DishCategory]', N'U') IS NULL
    CREATE TABLE [dbo].[DishCategory] ([Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_DishCategory] PRIMARY KEY CLUSTERED);
GO
IF COL_LENGTH(N'dbo.DishCategory', N'CreatedDate') IS NULL ALTER TABLE [dbo].[DishCategory] ADD [CreatedDate] DATETIME NOT NULL DEFAULT (GETDATE());
IF COL_LENGTH(N'dbo.DishCategory', N'CreatedBy') IS NULL ALTER TABLE [dbo].[DishCategory] ADD [CreatedBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.DishCategory', N'ModifiedDate') IS NULL ALTER TABLE [dbo].[DishCategory] ADD [ModifiedDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.DishCategory', N'ModifiedBy') IS NULL ALTER TABLE [dbo].[DishCategory] ADD [ModifiedBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.DishCategory', N'DeletedDate') IS NULL ALTER TABLE [dbo].[DishCategory] ADD [DeletedDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.DishCategory', N'DeleteddBy') IS NULL ALTER TABLE [dbo].[DishCategory] ADD [DeleteddBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.DishCategory', N'IsDeleted') IS NULL ALTER TABLE [dbo].[DishCategory] ADD [IsDeleted] BIT NOT NULL DEFAULT (0);
IF COL_LENGTH(N'dbo.DishCategory', N'ParentId') IS NULL ALTER TABLE [dbo].[DishCategory] ADD [ParentId] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.DishCategory', N'Code') IS NULL ALTER TABLE [dbo].[DishCategory] ADD [Code] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.DishCategory', N'Name') IS NULL ALTER TABLE [dbo].[DishCategory] ADD [Name] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.DishCategory', N'Description') IS NULL ALTER TABLE [dbo].[DishCategory] ADD [Description] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.DishCategory', N'Status') IS NULL ALTER TABLE [dbo].[DishCategory] ADD [Status] NVARCHAR(MAX) NULL;
GO

/****** Table Position ******/
IF OBJECT_ID(N'[dbo].[Position]', N'U') IS NULL
    CREATE TABLE [dbo].[Position] ([Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_Position] PRIMARY KEY CLUSTERED);
GO
IF COL_LENGTH(N'dbo.Position', N'CreatedDate') IS NULL ALTER TABLE [dbo].[Position] ADD [CreatedDate] DATETIME NOT NULL DEFAULT (GETDATE());
IF COL_LENGTH(N'dbo.Position', N'CreatedBy') IS NULL ALTER TABLE [dbo].[Position] ADD [CreatedBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Position', N'ModifiedDate') IS NULL ALTER TABLE [dbo].[Position] ADD [ModifiedDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.Position', N'ModifiedBy') IS NULL ALTER TABLE [dbo].[Position] ADD [ModifiedBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Position', N'DeletedDate') IS NULL ALTER TABLE [dbo].[Position] ADD [DeletedDate] DATETIME NULL;
IF COL_LENGTH(N'dbo.Position', N'DeleteddBy') IS NULL ALTER TABLE [dbo].[Position] ADD [DeleteddBy] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Position', N'IsDeleted') IS NULL ALTER TABLE [dbo].[Position] ADD [IsDeleted] BIT NOT NULL DEFAULT (0);
IF COL_LENGTH(N'dbo.Position', N'CompanyId') IS NULL ALTER TABLE [dbo].[Position] ADD [CompanyId] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Position', N'Code') IS NULL ALTER TABLE [dbo].[Position] ADD [Code] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Position', N'Name') IS NULL ALTER TABLE [dbo].[Position] ADD [Name] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Position', N'Description') IS NULL ALTER TABLE [dbo].[Position] ADD [Description] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Position', N'Status') IS NULL ALTER TABLE [dbo].[Position] ADD [Status] NVARCHAR(MAX) NULL;
GO

/****** uspCompany_GetAll ******/
CREATE OR ALTER PROCEDURE [dbo].[uspCompany_GetAll] (
    @SearchText NVARCHAR(MAX) = NULL,
    @Page INT = 1,
    @PageSize INT = 10,
    @SortField NVARCHAR(MAX) = NULL,
    @SortOrder NVARCHAR(MAX) = NULL)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        ;WITH TempResult AS (
            SELECT t.[Id]
                 , t.[Code]
                 , t.[Name]
                 , t.[TaxCode]
                 , t.[Address]
                 , t.[Phone]
                 , t.[Email]
                 , t.[Status]
                 , t.[Note]
                 , t.[CreatedDate]
            FROM [dbo].[Company] AS t

            WHERE t.[IsDeleted] = 0
              AND (ISNULL(@SearchText, N'') = N''
                OR t.[Code] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[Name] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[TaxCode] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[Phone] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[Email] LIKE CONCAT(N'%', @SearchText, N'%'))
        ),
        TempCount AS (SELECT COUNT(*) AS TotalRow FROM TempResult)
        SELECT r.*, c.TotalRow
        FROM TempResult AS r CROSS JOIN TempCount AS c
        ORDER BY
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'code' THEN r.[Code] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'name' THEN r.[Name] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'taxCode' THEN r.[TaxCode] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'phone' THEN r.[Phone] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'email' THEN r.[Email] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'status' THEN r.[Status] END ASC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'code' THEN r.[Code] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'name' THEN r.[Name] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'taxCode' THEN r.[TaxCode] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'phone' THEN r.[Phone] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'email' THEN r.[Email] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'status' THEN r.[Status] END DESC,
            r.[CreatedDate] DESC
        OFFSET (@PageSize * (@Page - 1)) ROWS FETCH NEXT @PageSize ROWS ONLY
        OPTION (RECOMPILE);
    END TRY
    BEGIN CATCH
        THROW;
    END CATCH;
END
GO

/****** uspOrganizationUnit_GetAll ******/
CREATE OR ALTER PROCEDURE [dbo].[uspOrganizationUnit_GetAll] (
    @SearchText NVARCHAR(MAX) = NULL,
    @CompanyId UNIQUEIDENTIFIER = NULL,
    @ParentId UNIQUEIDENTIFIER = NULL,
    @Page INT = 1,
    @PageSize INT = 10,
    @SortField NVARCHAR(MAX) = NULL,
    @SortOrder NVARCHAR(MAX) = NULL)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        ;WITH TempResult AS (
            SELECT t.[Id]
                 , t.[CompanyId]
                 , j1.[Name] AS [CompanyName]
                 , t.[ParentId]
                 , j2.[Name] AS [ParentName]
                 , t.[Code]
                 , t.[Name]
                 , t.[UnitType]
                 , t.[Address]
                 , t.[Phone]
                 , t.[Status]
                 , t.[Note]
                 , t.[CreatedDate]
            FROM [dbo].[OrganizationUnit] AS t
            LEFT JOIN [dbo].[Company] AS j1 ON j1.[Id] = t.[CompanyId]
            LEFT JOIN [dbo].[OrganizationUnit] AS j2 ON j2.[Id] = t.[ParentId]
            WHERE t.[IsDeleted] = 0
              AND (ISNULL(@SearchText, N'') = N''
                OR t.[Code] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[Name] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[Phone] LIKE CONCAT(N'%', @SearchText, N'%'))
              AND (@CompanyId IS NULL OR @CompanyId = '00000000-0000-0000-0000-000000000000' OR t.[CompanyId] = @CompanyId)
              AND (@ParentId IS NULL OR @ParentId = '00000000-0000-0000-0000-000000000000' OR t.[ParentId] = @ParentId)
        ),
        TempCount AS (SELECT COUNT(*) AS TotalRow FROM TempResult)
        SELECT r.*, c.TotalRow
        FROM TempResult AS r CROSS JOIN TempCount AS c
        ORDER BY
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'code' THEN r.[Code] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'name' THEN r.[Name] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'unitType' THEN r.[UnitType] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'parentName' THEN r.[ParentName] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'companyName' THEN r.[CompanyName] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'phone' THEN r.[Phone] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'status' THEN r.[Status] END ASC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'code' THEN r.[Code] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'name' THEN r.[Name] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'unitType' THEN r.[UnitType] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'parentName' THEN r.[ParentName] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'companyName' THEN r.[CompanyName] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'phone' THEN r.[Phone] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'status' THEN r.[Status] END DESC,
            r.[CreatedDate] DESC
        OFFSET (@PageSize * (@Page - 1)) ROWS FETCH NEXT @PageSize ROWS ONLY
        OPTION (RECOMPILE);
    END TRY
    BEGIN CATCH
        THROW;
    END CATCH;
END
GO

/****** uspOperationalLocation_GetAll ******/
CREATE OR ALTER PROCEDURE [dbo].[uspOperationalLocation_GetAll] (
    @SearchText NVARCHAR(MAX) = NULL,
    @CompanyId UNIQUEIDENTIFIER = NULL,
    @OrganizationUnitId UNIQUEIDENTIFIER = NULL,
    @LocationType NVARCHAR(MAX) = NULL,
    @Status NVARCHAR(MAX) = NULL,
    @Page INT = 1,
    @PageSize INT = 10,
    @SortField NVARCHAR(MAX) = NULL,
    @SortOrder NVARCHAR(MAX) = NULL)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        ;WITH TempResult AS (
            SELECT t.[Id]
                 , t.[CompanyId]
                 , t.[OrganizationUnitId]
                 , j1.[Name] AS [CompanyName]
                 , j2.[Name] AS [OrganizationUnitName]
                 , t.[Code]
                 , t.[Name]
                 , t.[LocationType]
                 , t.[Address]
                 , t.[Latitude]
                 , t.[Longitude]
                 , t.[ContactName]
                 , t.[ContactPhone]
                 , t.[Status]
                 , t.[Note]
                 , t.[CreatedDate]
            FROM [dbo].[OperationalLocation] AS t
            LEFT JOIN [dbo].[Company] AS j1 ON j1.[Id] = t.[CompanyId]
            LEFT JOIN [dbo].[OrganizationUnit] AS j2 ON j2.[Id] = t.[OrganizationUnitId]
            WHERE t.[IsDeleted] = 0
              AND (ISNULL(@SearchText, N'') = N''
                OR t.[Code] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[Name] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[Address] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[ContactName] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[ContactPhone] LIKE CONCAT(N'%', @SearchText, N'%'))
              AND (@CompanyId IS NULL OR @CompanyId = '00000000-0000-0000-0000-000000000000' OR t.[CompanyId] = @CompanyId)
              AND (@OrganizationUnitId IS NULL OR @OrganizationUnitId = '00000000-0000-0000-0000-000000000000' OR t.[OrganizationUnitId] = @OrganizationUnitId)
              AND (ISNULL(@LocationType, N'') = N'' OR t.[LocationType] = @LocationType)
              AND (ISNULL(@Status, N'') = N'' OR t.[Status] = @Status)
        ),
        TempCount AS (SELECT COUNT(*) AS TotalRow FROM TempResult)
        SELECT r.*, c.TotalRow
        FROM TempResult AS r CROSS JOIN TempCount AS c
        ORDER BY
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'code' THEN r.[Code] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'name' THEN r.[Name] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'locationType' THEN r.[LocationType] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'address' THEN r.[Address] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'contactPhone' THEN r.[ContactPhone] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'organizationUnitName' THEN r.[OrganizationUnitName] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'status' THEN r.[Status] END ASC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'code' THEN r.[Code] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'name' THEN r.[Name] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'locationType' THEN r.[LocationType] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'address' THEN r.[Address] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'contactPhone' THEN r.[ContactPhone] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'organizationUnitName' THEN r.[OrganizationUnitName] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'status' THEN r.[Status] END DESC,
            r.[CreatedDate] DESC
        OFFSET (@PageSize * (@Page - 1)) ROWS FETCH NEXT @PageSize ROWS ONLY
        OPTION (RECOMPILE);
    END TRY
    BEGIN CATCH
        THROW;
    END CATCH;
END
GO

/****** uspCustomer_GetAll ******/
CREATE OR ALTER PROCEDURE [dbo].[uspCustomer_GetAll] (
    @SearchText NVARCHAR(MAX) = NULL,
    @CompanyId UNIQUEIDENTIFIER = NULL,
    @Code NVARCHAR(MAX) = NULL,
    @Name NVARCHAR(MAX) = NULL,
    @TaxCode NVARCHAR(MAX) = NULL,
    @Phone NVARCHAR(MAX) = NULL,
    @Email NVARCHAR(MAX) = NULL,
    @Address NVARCHAR(MAX) = NULL,
    @CustomerType NVARCHAR(MAX) = NULL,
    @Status NVARCHAR(MAX) = NULL,
    @Note NVARCHAR(MAX) = NULL,
    @Page INT = 1,
    @PageSize INT = 10,
    @SortField NVARCHAR(MAX) = NULL,
    @SortOrder NVARCHAR(MAX) = NULL)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        ;WITH TempResult AS (
            SELECT t.[Id]
                 , t.[CompanyId]
                 , j1.[Name] AS [CompanyName]
                 , t.[Code]
                 , t.[Name]
                 , t.[TaxCode]
                 , t.[Phone]
                 , t.[Email]
                 , t.[Address]
                 , t.[CustomerType]
                 , t.[Status]
                 , t.[Note]
                 , t.[CreatedDate]
            FROM [dbo].[Customer] AS t
            LEFT JOIN [dbo].[Company] AS j1 ON j1.[Id] = t.[CompanyId]
            WHERE t.[IsDeleted] = 0
              AND (ISNULL(@SearchText, N'') = N''
                OR t.[Code] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[Name] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[TaxCode] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[Phone] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[Email] LIKE CONCAT(N'%', @SearchText, N'%'))
              AND (@CompanyId IS NULL OR @CompanyId = '00000000-0000-0000-0000-000000000000' OR t.[CompanyId] = @CompanyId)
              AND (ISNULL(@CustomerType, N'') = N'' OR t.[CustomerType] = @CustomerType)
              AND (ISNULL(@Status, N'') = N'' OR t.[Status] = @Status)
        ),
        TempCount AS (SELECT COUNT(*) AS TotalRow FROM TempResult)
        SELECT r.*, c.TotalRow
        FROM TempResult AS r CROSS JOIN TempCount AS c
        ORDER BY
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'code' THEN r.[Code] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'name' THEN r.[Name] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'customerType' THEN r.[CustomerType] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'taxCode' THEN r.[TaxCode] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'phone' THEN r.[Phone] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'email' THEN r.[Email] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'status' THEN r.[Status] END ASC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'code' THEN r.[Code] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'name' THEN r.[Name] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'customerType' THEN r.[CustomerType] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'taxCode' THEN r.[TaxCode] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'phone' THEN r.[Phone] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'email' THEN r.[Email] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'status' THEN r.[Status] END DESC,
            r.[CreatedDate] DESC
        OFFSET (@PageSize * (@Page - 1)) ROWS FETCH NEXT @PageSize ROWS ONLY
        OPTION (RECOMPILE);
    END TRY
    BEGIN CATCH
        THROW;
    END CATCH;
END
GO

/****** uspCustomerContact_GetAll ******/
CREATE OR ALTER PROCEDURE [dbo].[uspCustomerContact_GetAll] (
    @SearchText NVARCHAR(MAX) = NULL,
    @CustomerId UNIQUEIDENTIFIER = NULL,
    @FullName NVARCHAR(MAX) = NULL,
    @Position NVARCHAR(MAX) = NULL,
    @Phone NVARCHAR(MAX) = NULL,
    @Email NVARCHAR(MAX) = NULL,
    @IsPrimary BIT = NULL,
    @Note NVARCHAR(MAX) = NULL,
    @Page INT = 1,
    @PageSize INT = 10,
    @SortField NVARCHAR(MAX) = NULL,
    @SortOrder NVARCHAR(MAX) = NULL)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        ;WITH TempResult AS (
            SELECT t.[Id]
                 , t.[CustomerId]
                 , j1.[Name] AS [CustomerName]
                 , t.[FullName]
                 , t.[Position]
                 , t.[Phone]
                 , t.[Email]
                 , t.[IsPrimary]
                 , t.[Note]
                 , t.[CreatedDate]
            FROM [dbo].[CustomerContact] AS t
            LEFT JOIN [dbo].[Customer] AS j1 ON j1.[Id] = t.[CustomerId]
            WHERE t.[IsDeleted] = 0
              AND (ISNULL(@SearchText, N'') = N''
                OR t.[FullName] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[Position] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[Phone] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[Email] LIKE CONCAT(N'%', @SearchText, N'%')
                OR j1.[Name] LIKE CONCAT(N'%', @SearchText, N'%'))
              AND (@CustomerId IS NULL OR @CustomerId = '00000000-0000-0000-0000-000000000000' OR t.[CustomerId] = @CustomerId)
        ),
        TempCount AS (SELECT COUNT(*) AS TotalRow FROM TempResult)
        SELECT r.*, c.TotalRow
        FROM TempResult AS r CROSS JOIN TempCount AS c
        ORDER BY
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'customerName' THEN r.[CustomerName] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'fullName' THEN r.[FullName] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'position' THEN r.[Position] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'phone' THEN r.[Phone] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'email' THEN r.[Email] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'isPrimary' THEN r.[IsPrimary] END ASC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'customerName' THEN r.[CustomerName] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'fullName' THEN r.[FullName] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'position' THEN r.[Position] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'phone' THEN r.[Phone] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'email' THEN r.[Email] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'isPrimary' THEN r.[IsPrimary] END DESC,
            r.[CreatedDate] DESC
        OFFSET (@PageSize * (@Page - 1)) ROWS FETCH NEXT @PageSize ROWS ONLY
        OPTION (RECOMPILE);
    END TRY
    BEGIN CATCH
        THROW;
    END CATCH;
END
GO

/****** uspCustomerDeliveryLocation_GetAll ******/
CREATE OR ALTER PROCEDURE [dbo].[uspCustomerDeliveryLocation_GetAll] (
    @SearchText NVARCHAR(MAX) = NULL,
    @CustomerId UNIQUEIDENTIFIER = NULL,
    @Code NVARCHAR(MAX) = NULL,
    @Name NVARCHAR(MAX) = NULL,
    @Address NVARCHAR(MAX) = NULL,
    @ContactName NVARCHAR(MAX) = NULL,
    @ContactPhone NVARCHAR(MAX) = NULL,
    @DeliveryInstructions NVARCHAR(MAX) = NULL,
    @Status NVARCHAR(MAX) = NULL,
    @Page INT = 1,
    @PageSize INT = 10,
    @SortField NVARCHAR(MAX) = NULL,
    @SortOrder NVARCHAR(MAX) = NULL)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        ;WITH TempResult AS (
            SELECT t.[Id]
                 , t.[CustomerId]
                 , j1.[Name] AS [CustomerName]
                 , t.[Code]
                 , t.[Name]
                 , t.[Address]
                 , t.[ContactName]
                 , t.[ContactPhone]
                 , t.[DeliveryInstructions]
                 , t.[Status]
                 , t.[CreatedDate]
            FROM [dbo].[CustomerDeliveryLocation] AS t
            LEFT JOIN [dbo].[Customer] AS j1 ON j1.[Id] = t.[CustomerId]
            WHERE t.[IsDeleted] = 0
              AND (ISNULL(@SearchText, N'') = N''
                OR t.[Code] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[Name] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[Address] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[ContactName] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[ContactPhone] LIKE CONCAT(N'%', @SearchText, N'%'))
              AND (@CustomerId IS NULL OR @CustomerId = '00000000-0000-0000-0000-000000000000' OR t.[CustomerId] = @CustomerId)
              AND (ISNULL(@Status, N'') = N'' OR t.[Status] = @Status)
        ),
        TempCount AS (SELECT COUNT(*) AS TotalRow FROM TempResult)
        SELECT r.*, c.TotalRow
        FROM TempResult AS r CROSS JOIN TempCount AS c
        ORDER BY
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'customerName' THEN r.[CustomerName] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'code' THEN r.[Code] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'name' THEN r.[Name] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'address' THEN r.[Address] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'contactName' THEN r.[ContactName] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'contactPhone' THEN r.[ContactPhone] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'status' THEN r.[Status] END ASC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'customerName' THEN r.[CustomerName] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'code' THEN r.[Code] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'name' THEN r.[Name] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'address' THEN r.[Address] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'contactName' THEN r.[ContactName] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'contactPhone' THEN r.[ContactPhone] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'status' THEN r.[Status] END DESC,
            r.[CreatedDate] DESC
        OFFSET (@PageSize * (@Page - 1)) ROWS FETCH NEXT @PageSize ROWS ONLY
        OPTION (RECOMPILE);
    END TRY
    BEGIN CATCH
        THROW;
    END CATCH;
END
GO

/****** uspSupplier_GetAll ******/
CREATE OR ALTER PROCEDURE [dbo].[uspSupplier_GetAll] (
    @SearchText NVARCHAR(MAX) = NULL,
    @CompanyId UNIQUEIDENTIFIER = NULL,
    @Page INT = 1,
    @PageSize INT = 10,
    @SortField NVARCHAR(MAX) = NULL,
    @SortOrder NVARCHAR(MAX) = NULL)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        ;WITH TempResult AS (
            SELECT t.[Id]
                 , t.[CompanyId]
                 , j1.[Name] AS [CompanyName]
                 , t.[Code]
                 , t.[Name]
                 , t.[TaxCode]
                 , t.[Address]
                 , t.[Phone]
                 , t.[Email]
                 , t.[ContactName]
                 , t.[Status]
                 , t.[Note]
                 , t.[CreatedDate]
            FROM [dbo].[Supplier] AS t
            LEFT JOIN [dbo].[Company] AS j1 ON j1.[Id] = t.[CompanyId]
            WHERE t.[IsDeleted] = 0
              AND (ISNULL(@SearchText, N'') = N''
                OR t.[Code] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[Name] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[TaxCode] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[Phone] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[Email] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[ContactName] LIKE CONCAT(N'%', @SearchText, N'%'))
              AND (@CompanyId IS NULL OR @CompanyId = '00000000-0000-0000-0000-000000000000' OR t.[CompanyId] = @CompanyId)
        ),
        TempCount AS (SELECT COUNT(*) AS TotalRow FROM TempResult)
        SELECT r.*, c.TotalRow
        FROM TempResult AS r CROSS JOIN TempCount AS c
        ORDER BY
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'code' THEN r.[Code] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'name' THEN r.[Name] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'taxCode' THEN r.[TaxCode] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'contactName' THEN r.[ContactName] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'phone' THEN r.[Phone] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'email' THEN r.[Email] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'status' THEN r.[Status] END ASC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'code' THEN r.[Code] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'name' THEN r.[Name] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'taxCode' THEN r.[TaxCode] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'contactName' THEN r.[ContactName] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'phone' THEN r.[Phone] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'email' THEN r.[Email] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'status' THEN r.[Status] END DESC,
            r.[CreatedDate] DESC
        OFFSET (@PageSize * (@Page - 1)) ROWS FETCH NEXT @PageSize ROWS ONLY
        OPTION (RECOMPILE);
    END TRY
    BEGIN CATCH
        THROW;
    END CATCH;
END
GO

/****** uspSupplierMaterial_GetAll ******/
CREATE OR ALTER PROCEDURE [dbo].[uspSupplierMaterial_GetAll] (
    @SearchText NVARCHAR(MAX) = NULL,
    @SupplierId UNIQUEIDENTIFIER = NULL,
    @IngredientId UNIQUEIDENTIFIER = NULL,
    @SupplierMaterialCode NVARCHAR(MAX) = NULL,
    @UnitPrice DECIMAL(18, 4) = NULL,
    @MinimumOrderQuantity DECIMAL(18, 4) = NULL,
    @LeadTimeDays INT = NULL,
    @QualityRequirement NVARCHAR(MAX) = NULL,
    @EffectiveFrom DATETIME = NULL,
    @EffectiveTo DATETIME = NULL,
    @IsPreferred BIT = NULL,
    @Status NVARCHAR(MAX) = NULL,
    @Page INT = 1,
    @PageSize INT = 10,
    @SortField NVARCHAR(MAX) = NULL,
    @SortOrder NVARCHAR(MAX) = NULL)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        ;WITH TempResult AS (
            SELECT t.[Id]
                 , t.[SupplierId]
                 , j1.[Name] AS [SupplierName]
                 , t.[IngredientId]
                 , j2.[Name] AS [IngredientName]
                 , t.[SupplierMaterialCode]
                 , t.[UnitPrice]
                 , t.[MinimumOrderQuantity]
                 , t.[LeadTimeDays]
                 , t.[QualityRequirement]
                 , t.[EffectiveFrom]
                 , t.[EffectiveTo]
                 , t.[IsPreferred]
                 , t.[Status]
                 , t.[CreatedDate]
            FROM [dbo].[SupplierMaterial] AS t
            LEFT JOIN [dbo].[Supplier] AS j1 ON j1.[Id] = t.[SupplierId]
            LEFT JOIN [dbo].[Ingredient] AS j2 ON j2.[Id] = t.[IngredientId]
            WHERE t.[IsDeleted] = 0
              AND (ISNULL(@SearchText, N'') = N''
                OR t.[SupplierMaterialCode] LIKE CONCAT(N'%', @SearchText, N'%')
                OR j1.[Name] LIKE CONCAT(N'%', @SearchText, N'%')
                OR j2.[Name] LIKE CONCAT(N'%', @SearchText, N'%')
                OR j2.[Code] LIKE CONCAT(N'%', @SearchText, N'%'))
              AND (@SupplierId IS NULL OR @SupplierId = '00000000-0000-0000-0000-000000000000' OR t.[SupplierId] = @SupplierId)
              AND (@IngredientId IS NULL OR @IngredientId = '00000000-0000-0000-0000-000000000000' OR t.[IngredientId] = @IngredientId)
              AND (ISNULL(@Status, N'') = N'' OR t.[Status] = @Status)
        ),
        TempCount AS (SELECT COUNT(*) AS TotalRow FROM TempResult)
        SELECT r.*, c.TotalRow
        FROM TempResult AS r CROSS JOIN TempCount AS c
        ORDER BY
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'supplierName' THEN r.[SupplierName] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'ingredientName' THEN r.[IngredientName] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'supplierMaterialCode' THEN r.[SupplierMaterialCode] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'unitPrice' THEN r.[UnitPrice] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'minimumOrderQuantity' THEN r.[MinimumOrderQuantity] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'leadTimeDays' THEN r.[LeadTimeDays] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'isPreferred' THEN r.[IsPreferred] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'status' THEN r.[Status] END ASC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'supplierName' THEN r.[SupplierName] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'ingredientName' THEN r.[IngredientName] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'supplierMaterialCode' THEN r.[SupplierMaterialCode] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'unitPrice' THEN r.[UnitPrice] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'minimumOrderQuantity' THEN r.[MinimumOrderQuantity] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'leadTimeDays' THEN r.[LeadTimeDays] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'isPreferred' THEN r.[IsPreferred] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'status' THEN r.[Status] END DESC,
            r.[CreatedDate] DESC
        OFFSET (@PageSize * (@Page - 1)) ROWS FETCH NEXT @PageSize ROWS ONLY
        OPTION (RECOMPILE);
    END TRY
    BEGIN CATCH
        THROW;
    END CATCH;
END
GO

/****** uspIngredient_GetAll ******/
CREATE OR ALTER PROCEDURE [dbo].[uspIngredient_GetAll] (
    @SearchText NVARCHAR(MAX) = NULL,
    @Page INT = 1,
    @PageSize INT = 10,
    @SortField NVARCHAR(MAX) = NULL,
    @SortOrder NVARCHAR(MAX) = NULL)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        ;WITH TempResult AS (
            SELECT t.[Id]
                 , t.[CompanyId]
                 , j1.[Name] AS [CompanyName]
                 , t.[IngredientCategoryId]
                 , j2.[Name] AS [IngredientCategoryName]
                 , t.[BaseUnitId]
                 , j3.[Name] AS [BaseUnitName]
                 , t.[Code]
                 , t.[Name]
                 , t.[Specification]
                 , t.[StorageCondition]
                 , t.[IsExpiryManaged]
                 , t.[ShelfLifeDays]
                 , t.[MinimumStock]
                 , t.[SafetyStock]
                 , t.[Status]
                 , t.[Note]
                 , t.[CreatedDate]
            FROM [dbo].[Ingredient] AS t
            LEFT JOIN [dbo].[Company] AS j1 ON j1.[Id] = t.[CompanyId]
            LEFT JOIN [dbo].[IngredientCategory] AS j2 ON j2.[Id] = t.[IngredientCategoryId]
            LEFT JOIN [dbo].[UnitOfMeasure] AS j3 ON j3.[Id] = t.[BaseUnitId]
            WHERE t.[IsDeleted] = 0
              AND (ISNULL(@SearchText, N'') = N''
                OR t.[Code] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[Name] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[Specification] LIKE CONCAT(N'%', @SearchText, N'%')
                OR j2.[Name] LIKE CONCAT(N'%', @SearchText, N'%'))
        ),
        TempCount AS (SELECT COUNT(*) AS TotalRow FROM TempResult)
        SELECT r.*, c.TotalRow
        FROM TempResult AS r CROSS JOIN TempCount AS c
        ORDER BY
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'code' THEN r.[Code] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'name' THEN r.[Name] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'ingredientCategoryName' THEN r.[IngredientCategoryName] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'baseUnitName' THEN r.[BaseUnitName] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'storageCondition' THEN r.[StorageCondition] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'shelfLifeDays' THEN r.[ShelfLifeDays] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'isExpiryManaged' THEN r.[IsExpiryManaged] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'status' THEN r.[Status] END ASC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'code' THEN r.[Code] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'name' THEN r.[Name] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'ingredientCategoryName' THEN r.[IngredientCategoryName] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'baseUnitName' THEN r.[BaseUnitName] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'storageCondition' THEN r.[StorageCondition] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'shelfLifeDays' THEN r.[ShelfLifeDays] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'isExpiryManaged' THEN r.[IsExpiryManaged] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'status' THEN r.[Status] END DESC,
            r.[CreatedDate] DESC
        OFFSET (@PageSize * (@Page - 1)) ROWS FETCH NEXT @PageSize ROWS ONLY
        OPTION (RECOMPILE);
    END TRY
    BEGIN CATCH
        THROW;
    END CATCH;
END
GO

/****** uspDish_GetAll ******/
CREATE OR ALTER PROCEDURE [dbo].[uspDish_GetAll] (
    @SearchText NVARCHAR(MAX) = NULL,
    @DishCategoryId UNIQUEIDENTIFIER = NULL,
    @DefaultUnitId UNIQUEIDENTIFIER = NULL,
    @Code NVARCHAR(MAX) = NULL,
    @Name NVARCHAR(MAX) = NULL,
    @Description NVARCHAR(MAX) = NULL,
    @StandardServingSize DECIMAL(18, 4) = NULL,
    @Status NVARCHAR(MAX) = NULL,
    @Note NVARCHAR(MAX) = NULL,
    @Page INT = 1,
    @PageSize INT = 10,
    @SortField NVARCHAR(MAX) = NULL,
    @SortOrder NVARCHAR(MAX) = NULL)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        ;WITH TempResult AS (
            SELECT t.[Id]
                 , t.[DishCategoryId]
                 , j1.[Name] AS [DishCategoryName]
                 , t.[DefaultUnitId]
                 , j2.[Name] AS [DefaultUnitName]
                 , t.[Code]
                 , t.[Name]
                 , t.[Description]
                 , t.[StandardServingSize]
                 , t.[Status]
                 , t.[Note]
                 , t.[CreatedDate]
            FROM [dbo].[Dish] AS t
            LEFT JOIN [dbo].[DishCategory] AS j1 ON j1.[Id] = t.[DishCategoryId]
            LEFT JOIN [dbo].[UnitOfMeasure] AS j2 ON j2.[Id] = t.[DefaultUnitId]
            WHERE t.[IsDeleted] = 0
              AND (ISNULL(@SearchText, N'') = N''
                OR t.[Code] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[Name] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[Description] LIKE CONCAT(N'%', @SearchText, N'%'))
              AND (@DishCategoryId IS NULL OR @DishCategoryId = '00000000-0000-0000-0000-000000000000' OR t.[DishCategoryId] = @DishCategoryId)
              AND (@DefaultUnitId IS NULL OR @DefaultUnitId = '00000000-0000-0000-0000-000000000000' OR t.[DefaultUnitId] = @DefaultUnitId)
              AND (ISNULL(@Status, N'') = N'' OR t.[Status] = @Status)
        ),
        TempCount AS (SELECT COUNT(*) AS TotalRow FROM TempResult)
        SELECT r.*, c.TotalRow
        FROM TempResult AS r CROSS JOIN TempCount AS c
        ORDER BY
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'code' THEN r.[Code] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'name' THEN r.[Name] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'dishCategoryName' THEN r.[DishCategoryName] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'defaultUnitName' THEN r.[DefaultUnitName] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'standardServingSize' THEN r.[StandardServingSize] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'status' THEN r.[Status] END ASC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'code' THEN r.[Code] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'name' THEN r.[Name] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'dishCategoryName' THEN r.[DishCategoryName] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'defaultUnitName' THEN r.[DefaultUnitName] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'standardServingSize' THEN r.[StandardServingSize] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'status' THEN r.[Status] END DESC,
            r.[CreatedDate] DESC
        OFFSET (@PageSize * (@Page - 1)) ROWS FETCH NEXT @PageSize ROWS ONLY
        OPTION (RECOMPILE);
    END TRY
    BEGIN CATCH
        THROW;
    END CATCH;
END
GO

/****** uspWarehouse_GetAll ******/
CREATE OR ALTER PROCEDURE [dbo].[uspWarehouse_GetAll] (
    @SearchText NVARCHAR(MAX) = NULL,
    @CompanyId UNIQUEIDENTIFIER = NULL,
    @OrganizationUnitId UNIQUEIDENTIFIER = NULL,
    @WarehouseType NVARCHAR(MAX) = NULL,
    @Page INT = 1,
    @PageSize INT = 10,
    @SortField NVARCHAR(MAX) = NULL,
    @SortOrder NVARCHAR(MAX) = NULL)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        ;WITH TempResult AS (
            SELECT t.[Id]
                 , t.[CompanyId]
                 , j1.[Name] AS [CompanyName]
                 , t.[OrganizationUnitId]
                 , j2.[Name] AS [OrganizationUnitName]
                 , t.[Code]
                 , t.[Name]
                 , t.[WarehouseType]
                 , t.[Address]
                 , t.[Status]
                 , t.[CreatedDate]
            FROM [dbo].[Warehouse] AS t
            LEFT JOIN [dbo].[Company] AS j1 ON j1.[Id] = t.[CompanyId]
            LEFT JOIN [dbo].[OrganizationUnit] AS j2 ON j2.[Id] = t.[OrganizationUnitId]
            WHERE t.[IsDeleted] = 0
              AND (ISNULL(@SearchText, N'') = N''
                OR t.[Code] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[Name] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[Address] LIKE CONCAT(N'%', @SearchText, N'%'))
              AND (@CompanyId IS NULL OR @CompanyId = '00000000-0000-0000-0000-000000000000' OR t.[CompanyId] = @CompanyId)
              AND (@OrganizationUnitId IS NULL OR @OrganizationUnitId = '00000000-0000-0000-0000-000000000000' OR t.[OrganizationUnitId] = @OrganizationUnitId)
              AND (ISNULL(@WarehouseType, N'') = N'' OR t.[WarehouseType] = @WarehouseType)
        ),
        TempCount AS (SELECT COUNT(*) AS TotalRow FROM TempResult)
        SELECT r.*, c.TotalRow
        FROM TempResult AS r CROSS JOIN TempCount AS c
        ORDER BY
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'code' THEN r.[Code] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'name' THEN r.[Name] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'warehouseType' THEN r.[WarehouseType] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'organizationUnitName' THEN r.[OrganizationUnitName] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'address' THEN r.[Address] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'status' THEN r.[Status] END ASC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'code' THEN r.[Code] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'name' THEN r.[Name] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'warehouseType' THEN r.[WarehouseType] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'organizationUnitName' THEN r.[OrganizationUnitName] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'address' THEN r.[Address] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'status' THEN r.[Status] END DESC,
            r.[CreatedDate] DESC
        OFFSET (@PageSize * (@Page - 1)) ROWS FETCH NEXT @PageSize ROWS ONLY
        OPTION (RECOMPILE);
    END TRY
    BEGIN CATCH
        THROW;
    END CATCH;
END
GO

/****** uspStorageZone_GetAll ******/
CREATE OR ALTER PROCEDURE [dbo].[uspStorageZone_GetAll] (
    @SearchText NVARCHAR(MAX) = NULL,
    @WarehouseId UNIQUEIDENTIFIER = NULL,
    @Page INT = 1,
    @PageSize INT = 10,
    @SortField NVARCHAR(MAX) = NULL,
    @SortOrder NVARCHAR(MAX) = NULL)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        ;WITH TempResult AS (
            SELECT t.[Id]
                 , t.[WarehouseId]
                 , j1.[Name] AS [WarehouseName]
                 , t.[Code]
                 , t.[Name]
                 , t.[TemperatureRange]
                 , t.[Status]
                 , t.[CreatedDate]
            FROM [dbo].[StorageZone] AS t
            LEFT JOIN [dbo].[Warehouse] AS j1 ON j1.[Id] = t.[WarehouseId]
            WHERE t.[IsDeleted] = 0
              AND (ISNULL(@SearchText, N'') = N''
                OR t.[Code] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[Name] LIKE CONCAT(N'%', @SearchText, N'%')
                OR j1.[Name] LIKE CONCAT(N'%', @SearchText, N'%'))
              AND (@WarehouseId IS NULL OR @WarehouseId = '00000000-0000-0000-0000-000000000000' OR t.[WarehouseId] = @WarehouseId)
        ),
        TempCount AS (SELECT COUNT(*) AS TotalRow FROM TempResult)
        SELECT r.*, c.TotalRow
        FROM TempResult AS r CROSS JOIN TempCount AS c
        ORDER BY
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'warehouseName' THEN r.[WarehouseName] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'code' THEN r.[Code] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'name' THEN r.[Name] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'temperatureRange' THEN r.[TemperatureRange] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'status' THEN r.[Status] END ASC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'warehouseName' THEN r.[WarehouseName] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'code' THEN r.[Code] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'name' THEN r.[Name] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'temperatureRange' THEN r.[TemperatureRange] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'status' THEN r.[Status] END DESC,
            r.[CreatedDate] DESC
        OFFSET (@PageSize * (@Page - 1)) ROWS FETCH NEXT @PageSize ROWS ONLY
        OPTION (RECOMPILE);
    END TRY
    BEGIN CATCH
        THROW;
    END CATCH;
END
GO

/****** uspStorageLocation_GetAll ******/
CREATE OR ALTER PROCEDURE [dbo].[uspStorageLocation_GetAll] (
    @SearchText NVARCHAR(MAX) = NULL,
    @StorageZoneId UNIQUEIDENTIFIER = NULL,
    @Page INT = 1,
    @PageSize INT = 10,
    @SortField NVARCHAR(MAX) = NULL,
    @SortOrder NVARCHAR(MAX) = NULL)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        ;WITH TempResult AS (
            SELECT t.[Id]
                 , t.[WarehouseId]
                 , j1.[Name] AS [WarehouseName]
                 , t.[StorageZoneId]
                 , j2.[Name] AS [StorageZoneName]
                 , t.[Code]
                 , t.[Name]
                 , t.[LocationType]
                 , t.[Status]
                 , t.[CreatedDate]
            FROM [dbo].[StorageLocation] AS t
            LEFT JOIN [dbo].[Warehouse] AS j1 ON j1.[Id] = t.[WarehouseId]
            LEFT JOIN [dbo].[StorageZone] AS j2 ON j2.[Id] = t.[StorageZoneId]
            WHERE t.[IsDeleted] = 0
              AND (ISNULL(@SearchText, N'') = N''
                OR t.[Code] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[Name] LIKE CONCAT(N'%', @SearchText, N'%')
                OR j1.[Name] LIKE CONCAT(N'%', @SearchText, N'%')
                OR j2.[Name] LIKE CONCAT(N'%', @SearchText, N'%'))
              AND (@StorageZoneId IS NULL OR @StorageZoneId = '00000000-0000-0000-0000-000000000000' OR t.[StorageZoneId] = @StorageZoneId)
        ),
        TempCount AS (SELECT COUNT(*) AS TotalRow FROM TempResult)
        SELECT r.*, c.TotalRow
        FROM TempResult AS r CROSS JOIN TempCount AS c
        ORDER BY
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'warehouseName' THEN r.[WarehouseName] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'storageZoneName' THEN r.[StorageZoneName] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'code' THEN r.[Code] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'name' THEN r.[Name] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'locationType' THEN r.[LocationType] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'status' THEN r.[Status] END ASC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'warehouseName' THEN r.[WarehouseName] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'storageZoneName' THEN r.[StorageZoneName] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'code' THEN r.[Code] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'name' THEN r.[Name] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'locationType' THEN r.[LocationType] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'status' THEN r.[Status] END DESC,
            r.[CreatedDate] DESC
        OFFSET (@PageSize * (@Page - 1)) ROWS FETCH NEXT @PageSize ROWS ONLY
        OPTION (RECOMPILE);
    END TRY
    BEGIN CATCH
        THROW;
    END CATCH;
END
GO

/****** uspEmployee_GetAll ******/
CREATE OR ALTER PROCEDURE [dbo].[uspEmployee_GetAll] (
    @SearchText NVARCHAR(MAX) = NULL,
    @CompanyId UNIQUEIDENTIFIER = NULL,
    @OrganizationUnitId UNIQUEIDENTIFIER = NULL,
    @PositionId UNIQUEIDENTIFIER = NULL,
    @Code NVARCHAR(MAX) = NULL,
    @FullName NVARCHAR(MAX) = NULL,
    @DateOfBirth DATETIME = NULL,
    @Gender NVARCHAR(MAX) = NULL,
    @Phone NVARCHAR(MAX) = NULL,
    @Email NVARCHAR(MAX) = NULL,
    @Address NVARCHAR(MAX) = NULL,
    @HireDate DATETIME = NULL,
    @Status NVARCHAR(MAX) = NULL,
    @Page INT = 1,
    @PageSize INT = 10,
    @SortField NVARCHAR(MAX) = NULL,
    @SortOrder NVARCHAR(MAX) = NULL)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        ;WITH TempResult AS (
            SELECT t.[Id]
                 , t.[CompanyId]
                 , j1.[Name] AS [CompanyName]
                 , t.[OrganizationUnitId]
                 , j2.[Name] AS [OrganizationUnitName]
                 , t.[PositionId]
                 , j3.[Name] AS [PositionName]
                 , t.[Code]
                 , t.[FullName]
                 , t.[DateOfBirth]
                 , t.[Gender]
                 , t.[Phone]
                 , t.[Email]
                 , t.[Address]
                 , t.[HireDate]
                 , t.[Status]
                 , t.[CreatedDate]
            FROM [dbo].[Employee] AS t
            LEFT JOIN [dbo].[Company] AS j1 ON j1.[Id] = t.[CompanyId]
            LEFT JOIN [dbo].[OrganizationUnit] AS j2 ON j2.[Id] = t.[OrganizationUnitId]
            LEFT JOIN [dbo].[Position] AS j3 ON j3.[Id] = t.[PositionId]
            WHERE t.[IsDeleted] = 0
              AND (ISNULL(@SearchText, N'') = N''
                OR t.[Code] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[FullName] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[Phone] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[Email] LIKE CONCAT(N'%', @SearchText, N'%'))
              AND (@CompanyId IS NULL OR @CompanyId = '00000000-0000-0000-0000-000000000000' OR t.[CompanyId] = @CompanyId)
              AND (@OrganizationUnitId IS NULL OR @OrganizationUnitId = '00000000-0000-0000-0000-000000000000' OR t.[OrganizationUnitId] = @OrganizationUnitId)
              AND (@PositionId IS NULL OR @PositionId = '00000000-0000-0000-0000-000000000000' OR t.[PositionId] = @PositionId)
              AND (ISNULL(@Status, N'') = N'' OR t.[Status] = @Status)
        ),
        TempCount AS (SELECT COUNT(*) AS TotalRow FROM TempResult)
        SELECT r.*, c.TotalRow
        FROM TempResult AS r CROSS JOIN TempCount AS c
        ORDER BY
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'code' THEN r.[Code] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'fullName' THEN r.[FullName] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'positionName' THEN r.[PositionName] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'organizationUnitName' THEN r.[OrganizationUnitName] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'phone' THEN r.[Phone] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'hireDate' THEN r.[HireDate] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'status' THEN r.[Status] END ASC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'code' THEN r.[Code] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'fullName' THEN r.[FullName] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'positionName' THEN r.[PositionName] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'organizationUnitName' THEN r.[OrganizationUnitName] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'phone' THEN r.[Phone] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'hireDate' THEN r.[HireDate] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'status' THEN r.[Status] END DESC,
            r.[CreatedDate] DESC
        OFFSET (@PageSize * (@Page - 1)) ROWS FETCH NEXT @PageSize ROWS ONLY
        OPTION (RECOMPILE);
    END TRY
    BEGIN CATCH
        THROW;
    END CATCH;
END
GO

/****** uspVehicle_GetAll ******/
CREATE OR ALTER PROCEDURE [dbo].[uspVehicle_GetAll] (
    @SearchText NVARCHAR(MAX) = NULL,
    @CompanyId UNIQUEIDENTIFIER = NULL,
    @OrganizationUnitId UNIQUEIDENTIFIER = NULL,
    @VehicleType NVARCHAR(MAX) = NULL,
    @Status NVARCHAR(MAX) = NULL,
    @Page INT = 1,
    @PageSize INT = 10,
    @SortField NVARCHAR(MAX) = NULL,
    @SortOrder NVARCHAR(MAX) = NULL)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        ;WITH TempResult AS (
            SELECT t.[Id]
                 , t.[CompanyId]
                 , t.[OrganizationUnitId]
                 , j1.[Name] AS [CompanyName]
                 , j2.[Name] AS [OrganizationUnitName]
                 , j3.[FullName] AS [DefaultDriverName]
                 , t.[Code]
                 , t.[LicensePlate]
                 , t.[Name]
                 , t.[VehicleType]
                 , t.[Brand]
                 , t.[LoadCapacityKg]
                 , t.[VolumeM3]
                 , t.[IsRefrigerated]
                 , t.[DefaultDriverId]
                 , t.[RegistrationExpiryDate]
                 , t.[InsuranceExpiryDate]
                 , t.[Status]
                 , t.[Note]
                 , t.[CreatedDate]
            FROM [dbo].[Vehicle] AS t
            LEFT JOIN [dbo].[Company] AS j1 ON j1.[Id] = t.[CompanyId]
            LEFT JOIN [dbo].[OrganizationUnit] AS j2 ON j2.[Id] = t.[OrganizationUnitId]
            LEFT JOIN [dbo].[Employee] AS j3 ON j3.[Id] = t.[DefaultDriverId]
            WHERE t.[IsDeleted] = 0
              AND (ISNULL(@SearchText, N'') = N''
                OR t.[Code] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[LicensePlate] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[Name] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[Brand] LIKE CONCAT(N'%', @SearchText, N'%')
                OR j3.[FullName] LIKE CONCAT(N'%', @SearchText, N'%'))
              AND (@CompanyId IS NULL OR @CompanyId = '00000000-0000-0000-0000-000000000000' OR t.[CompanyId] = @CompanyId)
              AND (@OrganizationUnitId IS NULL OR @OrganizationUnitId = '00000000-0000-0000-0000-000000000000' OR t.[OrganizationUnitId] = @OrganizationUnitId)
              AND (ISNULL(@VehicleType, N'') = N'' OR t.[VehicleType] = @VehicleType)
              AND (ISNULL(@Status, N'') = N'' OR t.[Status] = @Status)
        ),
        TempCount AS (SELECT COUNT(*) AS TotalRow FROM TempResult)
        SELECT r.*, c.TotalRow
        FROM TempResult AS r CROSS JOIN TempCount AS c
        ORDER BY
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'code' THEN r.[Code] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'licensePlate' THEN r.[LicensePlate] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'name' THEN r.[Name] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'vehicleType' THEN r.[VehicleType] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'loadCapacityKg' THEN r.[LoadCapacityKg] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'isRefrigerated' THEN r.[IsRefrigerated] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'defaultDriverName' THEN r.[DefaultDriverName] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'registrationExpiryDate' THEN r.[RegistrationExpiryDate] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'status' THEN r.[Status] END ASC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'code' THEN r.[Code] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'licensePlate' THEN r.[LicensePlate] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'name' THEN r.[Name] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'vehicleType' THEN r.[VehicleType] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'loadCapacityKg' THEN r.[LoadCapacityKg] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'isRefrigerated' THEN r.[IsRefrigerated] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'defaultDriverName' THEN r.[DefaultDriverName] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'registrationExpiryDate' THEN r.[RegistrationExpiryDate] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'status' THEN r.[Status] END DESC,
            r.[CreatedDate] DESC
        OFFSET (@PageSize * (@Page - 1)) ROWS FETCH NEXT @PageSize ROWS ONLY
        OPTION (RECOMPILE);
    END TRY
    BEGIN CATCH
        THROW;
    END CATCH;
END
GO

/****** uspIngredientCategory_GetAll ******/
CREATE OR ALTER PROCEDURE [dbo].[uspIngredientCategory_GetAll] (
    @SearchText NVARCHAR(MAX) = NULL,
    @ParentId UNIQUEIDENTIFIER = NULL,
    @Page INT = 1,
    @PageSize INT = 10,
    @SortField NVARCHAR(MAX) = NULL,
    @SortOrder NVARCHAR(MAX) = NULL)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        ;WITH TempResult AS (
            SELECT t.[Id]
                 , t.[ParentId]
                 , j1.[Name] AS [ParentName]
                 , t.[Code]
                 , t.[Name]
                 , t.[Description]
                 , t.[Status]
                 , t.[CreatedDate]
            FROM [dbo].[IngredientCategory] AS t
            LEFT JOIN [dbo].[IngredientCategory] AS j1 ON j1.[Id] = t.[ParentId]
            WHERE t.[IsDeleted] = 0
              AND (ISNULL(@SearchText, N'') = N''
                OR t.[Code] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[Name] LIKE CONCAT(N'%', @SearchText, N'%'))
              AND (@ParentId IS NULL OR @ParentId = '00000000-0000-0000-0000-000000000000' OR t.[ParentId] = @ParentId)
        ),
        TempCount AS (SELECT COUNT(*) AS TotalRow FROM TempResult)
        SELECT r.*, c.TotalRow
        FROM TempResult AS r CROSS JOIN TempCount AS c
        ORDER BY
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'code' THEN r.[Code] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'name' THEN r.[Name] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'parentName' THEN r.[ParentName] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'description' THEN r.[Description] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'status' THEN r.[Status] END ASC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'code' THEN r.[Code] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'name' THEN r.[Name] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'parentName' THEN r.[ParentName] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'description' THEN r.[Description] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'status' THEN r.[Status] END DESC,
            r.[CreatedDate] DESC
        OFFSET (@PageSize * (@Page - 1)) ROWS FETCH NEXT @PageSize ROWS ONLY
        OPTION (RECOMPILE);
    END TRY
    BEGIN CATCH
        THROW;
    END CATCH;
END
GO

/****** uspUnitOfMeasure_GetAll ******/
CREATE OR ALTER PROCEDURE [dbo].[uspUnitOfMeasure_GetAll] (
    @SearchText NVARCHAR(MAX) = NULL,
    @Page INT = 1,
    @PageSize INT = 10,
    @SortField NVARCHAR(MAX) = NULL,
    @SortOrder NVARCHAR(MAX) = NULL)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        ;WITH TempResult AS (
            SELECT t.[Id]
                 , t.[Code]
                 , t.[Name]
                 , t.[Symbol]
                 , t.[UnitType]
                 , t.[Status]
                 , t.[CreatedDate]
            FROM [dbo].[UnitOfMeasure] AS t

            WHERE t.[IsDeleted] = 0
              AND (ISNULL(@SearchText, N'') = N''
                OR t.[Code] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[Name] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[Symbol] LIKE CONCAT(N'%', @SearchText, N'%'))
        ),
        TempCount AS (SELECT COUNT(*) AS TotalRow FROM TempResult)
        SELECT r.*, c.TotalRow
        FROM TempResult AS r CROSS JOIN TempCount AS c
        ORDER BY
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'code' THEN r.[Code] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'name' THEN r.[Name] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'symbol' THEN r.[Symbol] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'unitType' THEN r.[UnitType] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'status' THEN r.[Status] END ASC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'code' THEN r.[Code] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'name' THEN r.[Name] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'symbol' THEN r.[Symbol] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'unitType' THEN r.[UnitType] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'status' THEN r.[Status] END DESC,
            r.[CreatedDate] DESC
        OFFSET (@PageSize * (@Page - 1)) ROWS FETCH NEXT @PageSize ROWS ONLY
        OPTION (RECOMPILE);
    END TRY
    BEGIN CATCH
        THROW;
    END CATCH;
END
GO

/****** uspDishCategory_GetAll ******/
CREATE OR ALTER PROCEDURE [dbo].[uspDishCategory_GetAll] (
    @SearchText NVARCHAR(MAX) = NULL,
    @ParentId UNIQUEIDENTIFIER = NULL,
    @Code NVARCHAR(MAX) = NULL,
    @Name NVARCHAR(MAX) = NULL,
    @Description NVARCHAR(MAX) = NULL,
    @Status NVARCHAR(MAX) = NULL,
    @Page INT = 1,
    @PageSize INT = 10,
    @SortField NVARCHAR(MAX) = NULL,
    @SortOrder NVARCHAR(MAX) = NULL)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        ;WITH TempResult AS (
            SELECT t.[Id]
                 , t.[ParentId]
                 , j1.[Name] AS [ParentName]
                 , t.[Code]
                 , t.[Name]
                 , t.[Description]
                 , t.[Status]
                 , t.[CreatedDate]
            FROM [dbo].[DishCategory] AS t
            LEFT JOIN [dbo].[DishCategory] AS j1 ON j1.[Id] = t.[ParentId]
            WHERE t.[IsDeleted] = 0
              AND (ISNULL(@SearchText, N'') = N''
                OR t.[Code] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[Name] LIKE CONCAT(N'%', @SearchText, N'%'))
              AND (@ParentId IS NULL OR @ParentId = '00000000-0000-0000-0000-000000000000' OR t.[ParentId] = @ParentId)
              AND (ISNULL(@Status, N'') = N'' OR t.[Status] = @Status)
        ),
        TempCount AS (SELECT COUNT(*) AS TotalRow FROM TempResult)
        SELECT r.*, c.TotalRow
        FROM TempResult AS r CROSS JOIN TempCount AS c
        ORDER BY
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'code' THEN r.[Code] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'name' THEN r.[Name] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'parentName' THEN r.[ParentName] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'description' THEN r.[Description] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'status' THEN r.[Status] END ASC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'code' THEN r.[Code] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'name' THEN r.[Name] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'parentName' THEN r.[ParentName] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'description' THEN r.[Description] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'status' THEN r.[Status] END DESC,
            r.[CreatedDate] DESC
        OFFSET (@PageSize * (@Page - 1)) ROWS FETCH NEXT @PageSize ROWS ONLY
        OPTION (RECOMPILE);
    END TRY
    BEGIN CATCH
        THROW;
    END CATCH;
END
GO

/****** uspPosition_GetAll ******/
CREATE OR ALTER PROCEDURE [dbo].[uspPosition_GetAll] (
    @SearchText NVARCHAR(MAX) = NULL,
    @Page INT = 1,
    @PageSize INT = 10,
    @SortField NVARCHAR(MAX) = NULL,
    @SortOrder NVARCHAR(MAX) = NULL)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        ;WITH TempResult AS (
            SELECT t.[Id]
                 , t.[CompanyId]
                 , j1.[Name] AS [CompanyName]
                 , t.[Code]
                 , t.[Name]
                 , t.[Description]
                 , t.[Status]
                 , t.[CreatedDate]
            FROM [dbo].[Position] AS t
            LEFT JOIN [dbo].[Company] AS j1 ON j1.[Id] = t.[CompanyId]
            WHERE t.[IsDeleted] = 0
              AND (ISNULL(@SearchText, N'') = N''
                OR t.[Code] LIKE CONCAT(N'%', @SearchText, N'%')
                OR t.[Name] LIKE CONCAT(N'%', @SearchText, N'%'))
        ),
        TempCount AS (SELECT COUNT(*) AS TotalRow FROM TempResult)
        SELECT r.*, c.TotalRow
        FROM TempResult AS r CROSS JOIN TempCount AS c
        ORDER BY
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'code' THEN r.[Code] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'name' THEN r.[Name] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'companyName' THEN r.[CompanyName] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'description' THEN r.[Description] END ASC,
            CASE WHEN @SortOrder = 'ascend' AND @SortField = 'status' THEN r.[Status] END ASC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'code' THEN r.[Code] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'name' THEN r.[Name] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'companyName' THEN r.[CompanyName] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'description' THEN r.[Description] END DESC,
            CASE WHEN @SortOrder = 'descend' AND @SortField = 'status' THEN r.[Status] END DESC,
            r.[CreatedDate] DESC
        OFFSET (@PageSize * (@Page - 1)) ROWS FETCH NEXT @PageSize ROWS ONLY
        OPTION (RECOMPILE);
    END TRY
    BEGIN CATCH
        THROW;
    END CATCH;
END
GO

/****** Menu + phân quyền ******/
-- Menu cha cấp 1 + các menu con cấp 2. Quyền đầy đủ được cấp cho các nhóm người dùng
-- đang có quyền vào màn hình "quan-ly-chuc-nang" (nhóm quản trị). Nhóm khác: cấp trong màn hình Phân quyền.
DECLARE @ParentId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM [dbo].[ObjectMenu] WHERE [Name] = N'Danh mục Master Data' AND IsDeleted = 0);
IF @ParentId IS NULL
BEGIN
    SET @ParentId = NEWID();
    INSERT INTO [dbo].[ObjectMenu] (Id, [Name], ShortName, ParentId, MetaLink, IconName, LevelShow, OrderShow, IsActive, IsDefault, CreatedDate, IsDeleted)
    VALUES (@ParentId, N'Danh mục Master Data', N'Master Data', NULL, N'', N'fa-solid fa-database', 1, 90, 1, 0, GETDATE(), 0);
END

DECLARE @Menu TABLE ([Name] NVARCHAR(200), MetaLink NVARCHAR(200), OrderShow INT);
INSERT INTO @Menu ([Name], MetaLink, OrderShow) VALUES
    (N'Công ty', N'dm-cong-ty', 1),
    (N'Đơn vị tổ chức', N'dm-don-vi-to-chuc', 2),
    (N'Địa điểm vận hành', N'dm-dia-diem-van-hanh', 3),
    (N'Khách hàng', N'dm-khach-hang', 4),
    (N'Liên hệ khách hàng', N'dm-lien-he-khach-hang', 5),
    (N'Địa điểm giao hàng', N'dm-dia-diem-giao-hang', 6),
    (N'Nhà cung cấp', N'nha-cung-cap', 7),
    (N'Nguyên liệu nhà cung cấp', N'dm-nguyen-lieu-ncc', 8),
    (N'Nguyên liệu', N'dm-nguyen-lieu', 9),
    (N'Món ăn', N'dm-mon-an', 10),
    (N'Kho', N'dm-kho', 11),
    (N'Khu vực kho', N'dm-khu-vuc-kho', 12),
    (N'Vị trí lưu trữ', N'dm-vi-tri-luu-tru', 13),
    (N'Nhân viên', N'dm-nhan-vien', 14),
    (N'Phương tiện vận chuyển', N'dm-phuong-tien', 15),
    (N'Nhóm nguyên liệu', N'dm-nhom-nguyen-lieu', 16),
    (N'Đơn vị tính (UOM)', N'dm-don-vi-tinh', 17),
    (N'Nhóm món ăn', N'dm-nhom-mon-an', 18),
    (N'Chức vụ', N'dm-chuc-vu', 19);

INSERT INTO [dbo].[ObjectMenu] (Id, [Name], ShortName, ParentId, MetaLink, IconName, LevelShow, OrderShow, IsActive, IsDefault, CreatedDate, IsDeleted)
SELECT NEWID(), m.[Name], m.[Name], @ParentId, m.MetaLink, NULL, 2, m.OrderShow, 1, 0, GETDATE(), 0
FROM @Menu AS m
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[ObjectMenu] AS o WHERE o.MetaLink = m.MetaLink AND o.IsDeleted = 0);

DECLARE @AdminGroups TABLE (UserGroupId UNIQUEIDENTIFIER);
INSERT INTO @AdminGroups (UserGroupId)
SELECT DISTINCT ur.UserGroupId
FROM [dbo].[UserRole] AS ur
JOIN [dbo].[ObjectMenu] AS om ON om.Id = ur.ObjectMenuId AND om.IsDeleted = 0
WHERE om.MetaLink = N'quan-ly-chuc-nang' AND ur.AllowAccess = 1 AND ur.IsDeleted = 0;

INSERT INTO [dbo].[UserRole] (Id, UserGroupId, ObjectMenuId, AllowAccess, AllowAdd, AllowEdit, AllowDelete, AllowPrint, AllowExport, AllowImport, CreatedDate, IsDeleted)
SELECT NEWID(), g.UserGroupId, om.Id, 1, 1, 1, 1, 1, 1, 1, GETDATE(), 0
FROM @AdminGroups AS g
CROSS JOIN [dbo].[ObjectMenu] AS om
WHERE om.IsDeleted = 0
  AND (om.Id = @ParentId OR om.MetaLink IN (SELECT MetaLink FROM @Menu))
  AND NOT EXISTS (SELECT 1 FROM [dbo].[UserRole] AS ur
                  WHERE ur.UserGroupId = g.UserGroupId AND ur.ObjectMenuId = om.Id AND ur.IsDeleted = 0);
GO
