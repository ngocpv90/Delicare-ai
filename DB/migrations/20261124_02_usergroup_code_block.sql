-- delicare-migration: normal
-- UI-6.43 (D-124, GAP-PERM-03 APPROVED – anh duyệt 2026-11-24, cách A): mã nhóm quyền tách khỏi tên.
-- Thêm đúng 3 cột dbo.UserGroup: Code nvarchar(50) NULL (bắt buộc ở tầng dịch vụ sau khi dọn dữ liệu – UNIQUE lọc IsDeleted = 0),
-- Description nvarchar(500) NULL, GroupBlock nvarchar(30) NULL (mã khối – ReferenceLists.UserGroupBlock). Không thêm bảng, không FK.
-- Điền dữ liệu: DB/scripts/UI643_usergroup_code.sql. Chạy lại được (kiểm cột / chỉ mục đã có).
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
IF COL_LENGTH(N'dbo.UserGroup', N'Code') IS NULL
    ALTER TABLE [dbo].[UserGroup] ADD [Code] [nvarchar](50) NULL;
GO
IF COL_LENGTH(N'dbo.UserGroup', N'Description') IS NULL
    ALTER TABLE [dbo].[UserGroup] ADD [Description] [nvarchar](500) NULL;
GO
IF COL_LENGTH(N'dbo.UserGroup', N'GroupBlock') IS NULL
    ALTER TABLE [dbo].[UserGroup] ADD [GroupBlock] [nvarchar](30) NULL;
GO
-- Mã nhóm không trùng giữa các nhóm đang dùng (bỏ qua dòng đã xóa mềm và dòng chưa điền mã).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_UserGroup_Code' AND object_id = OBJECT_ID(N'dbo.UserGroup'))
    CREATE UNIQUE NONCLUSTERED INDEX [UX_UserGroup_Code] ON [dbo].[UserGroup] ([Code]) WHERE [IsDeleted] = 0 AND [Code] IS NOT NULL;
GO
