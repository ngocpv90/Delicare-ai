-- delicare-migration: normal
-- D-82 (c) – HR-ACC-1.1 (anh duyệt 2026-11-23): tài khoản ↔ con người ↔ nhân viên. Đúng 2 cột, không đổi bảng khác:
--   (1) dbo.Employee.PersonId uniqueidentifier NULL + FK → dbo.Person(Id) + chỉ mục duy nhất lọc (PersonId IS NOT NULL AND IsDeleted = 0):
--       1 Person ↔ tối đa 1 Employee (chưa xóa). KHÔNG lọc theo Status: nghỉ việc rồi quay lại dùng lại chính dòng Employee cũ
--       (D-82 h – "Mở lại tài khoản"), nên không cần cho 2 dòng Employee cùng 1 Person.
--   (2) dbo.[User].AccountType nvarchar(20) NOT NULL DEFAULT 'INTERNAL' + CHECK ('INTERNAL', 'CUSTOMER', 'SUPPLIER'). Mọi tài khoản hiện có = INTERNAL.
-- Chạy lại được (kiểm COL_LENGTH / sys.foreign_keys / sys.indexes / sys.check_constraints). Gỡ (rollback) – xem PROGRESS HR-ACC-1.1.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
IF COL_LENGTH(N'dbo.Employee', N'PersonId') IS NULL
    ALTER TABLE [dbo].[Employee] ADD [PersonId] [uniqueidentifier] NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Employee_Person' AND parent_object_id = OBJECT_ID(N'dbo.Employee'))
    ALTER TABLE [dbo].[Employee] WITH CHECK ADD CONSTRAINT [FK_Employee_Person] FOREIGN KEY ([PersonId]) REFERENCES [dbo].[Person] ([Id]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Employee_PersonId' AND object_id = OBJECT_ID(N'dbo.Employee'))
    CREATE UNIQUE NONCLUSTERED INDEX [UX_Employee_PersonId] ON [dbo].[Employee] ([PersonId]) WHERE [PersonId] IS NOT NULL AND [IsDeleted] = 0;
GO
IF COL_LENGTH(N'dbo.User', N'AccountType') IS NULL
    ALTER TABLE [dbo].[User] ADD [AccountType] [nvarchar](20) NOT NULL CONSTRAINT [DF_User_AccountType] DEFAULT (N'INTERNAL');
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_User_AccountType' AND parent_object_id = OBJECT_ID(N'dbo.User'))
    ALTER TABLE [dbo].[User] WITH CHECK ADD CONSTRAINT [CK_User_AccountType] CHECK ([AccountType] IN (N'INTERNAL', N'CUSTOMER', N'SUPPLIER'));
GO
-- Seed: mọi dòng = INTERNAL (DEFAULT đã điền khi thêm cột; câu này cho trường hợp cột đã được thêm tay mà còn rỗng).
UPDATE [dbo].[User] SET [AccountType] = N'INTERNAL' WHERE [AccountType] IS NULL OR LTRIM(RTRIM([AccountType])) = N'';
GO
