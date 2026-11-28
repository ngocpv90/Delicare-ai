-- delicare-migration: normal
-- UI-6.51 (2026-11-28, D-137 – anh duyệt GAP-PUR-SUB / GAP-PUR-STD / GAP-PUR-SPLIT): kế hoạch mua tự động.
--   RecipeLine.SubstituteGroup / MaxSharePct : dòng thay thế cùng nhóm với dòng chuẩn (định lượng 0), % tối đa được thay (50 = trộn, 100 = thay hẳn).
--   Contract.StandardIngredientOnly          : khách chỉ dùng nguyên liệu chuẩn – phần suất của khách này không bao giờ thay thế.
--   SupplierMaterial.AllocationPct            : tỷ lệ chia đơn cho nhiều NCC của một nguyên liệu (tổng các NCC có tỷ lệ = 100; trống = chọn 1 NCC như cũ).
-- Chỉ 4 cột đã duyệt, không thêm bảng. Chạy lại được. Cú pháp SQL Server 2022 (D-61).
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH(N'dbo.RecipeLine', N'SubstituteGroup') IS NULL
    ALTER TABLE [dbo].[RecipeLine] ADD [SubstituteGroup] [nvarchar](30) NULL;
GO
IF COL_LENGTH(N'dbo.RecipeLine', N'MaxSharePct') IS NULL
    ALTER TABLE [dbo].[RecipeLine] ADD [MaxSharePct] [decimal](5,2) NULL;
GO
IF COL_LENGTH(N'dbo.Contract', N'StandardIngredientOnly') IS NULL
    ALTER TABLE [dbo].[Contract] ADD [StandardIngredientOnly] [bit] NOT NULL CONSTRAINT [DF_Contract_StandardIngredientOnly] DEFAULT (0);
GO
IF COL_LENGTH(N'dbo.SupplierMaterial', N'AllocationPct') IS NULL
    ALTER TABLE [dbo].[SupplierMaterial] ADD [AllocationPct] [decimal](5,2) NULL;
GO
