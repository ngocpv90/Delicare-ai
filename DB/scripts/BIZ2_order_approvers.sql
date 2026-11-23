/*
  BIZ-2.1 (D-77 – B6, anh chốt 2026-11-23) – script DỮ LIỆU một lần (không tạo / sửa bảng, cột; không DELETE).
  Người duyệt "Phê duyệt › Đơn đặt suất" (#/wf-inbox?type=crm-order – sửa / thêm đơn sau giờ chốt ORDER_CHANGE_AFTER_CUTOFF)
  = Trưởng KD (nhóm SAL-CON – Quản lý hợp đồng / đơn hàng) + COO. Seed mới đã bỏ CEO khỏi mặc định (DB/seed/UserRole_seed.sql).
    - Thêm quyền duyệt (AllowAccess + AllowEdit) cho SAL-CON và COO nếu chưa có dòng; có dòng mà chưa bật → bật.
    - CEO: chỉ tắt quyền duyệt khi truyền -v RemoveCeo=1 (mặc định 0 = giữ nguyên – anh quyết).
  Ghi ModifiedDate / ModifiedBy (tài khoản system). Chạy lại lần 2 không đổi gì (idempotent).

  Bắt buộc truyền -v DryRun=1 (chạy thử) hoặc -v DryRun=0 (chạy thật), và -v RemoveCeo=0 / 1.
  CÁCH CHẠY (chỉ sau khi backup):
    sqlcmd -S <server> -d <db> -E -f 65001 -v DryRun=1 -v RemoveCeo=0 -i DB/scripts/BIZ2_order_approvers.sql -s "," -W
    sqlcmd -S <server> -d <db> -E -f 65001 -v DryRun=0 -v RemoveCeo=0 -i DB/scripts/BIZ2_order_approvers.sql -s "," -W
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @DryRun BIT = $(DryRun);
DECLARE @RemoveCeo BIT = $(RemoveCeo);
DECLARE @SystemUser UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.[User] WHERE UserName = N'system' AND ISNULL(IsDeleted, 0) = 0);
IF @SystemUser IS NULL
    THROW 50101, N'Không có tài khoản "system" (seed T23) – không ghi được người thao tác.', 1;
DECLARE @Now DATETIME = DATEADD(HOUR, 7, SYSUTCDATETIME());
DECLARE @Menu UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.ObjectMenu WHERE MetaLink = N'#/wf-inbox?type=crm-order' AND ISNULL(IsDeleted, 0) = 0);
IF @Menu IS NULL
    THROW 50102, N'Không có mục #/wf-inbox?type=crm-order – chạy DB/seed/ObjectMenu_seed.sql trước.', 1;

DECLARE @Plan TABLE (GroupName NVARCHAR(100), GroupId UNIQUEIDENTIFIER, UserRoleId UNIQUEIDENTIFIER, Action NVARCHAR(30));
INSERT INTO @Plan (GroupName, GroupId, UserRoleId, Action)
SELECT g.Name, g.Id, ur.Id,
       CASE WHEN ur.Id IS NULL THEN N'THEM' WHEN ISNULL(ur.AllowAccess, 0) = 0 OR ISNULL(ur.AllowEdit, 0) = 0 THEN N'BAT' ELSE N'DA_CO' END
FROM dbo.UserGroup g
LEFT JOIN dbo.UserRole ur ON ur.UserGroupId = g.Id AND ur.ObjectMenuId = @Menu AND ISNULL(ur.IsDeleted, 0) = 0
WHERE g.Name IN (N'SAL-CON', N'COO') AND ISNULL(g.IsDeleted, 0) = 0;
INSERT INTO @Plan (GroupName, GroupId, UserRoleId, Action)
SELECT g.Name, g.Id, ur.Id, CASE WHEN @RemoveCeo = 1 AND ISNULL(ur.AllowEdit, 0) = 1 THEN N'TAT_CEO' ELSE N'GIU_CEO' END
FROM dbo.UserGroup g
JOIN dbo.UserRole ur ON ur.UserGroupId = g.Id AND ur.ObjectMenuId = @Menu AND ISNULL(ur.IsDeleted, 0) = 0
WHERE g.Name = N'CEO' AND ISNULL(g.IsDeleted, 0) = 0;

SELECT GroupName AS [Nhom], Action AS [Viec] FROM @Plan ORDER BY GroupName;

IF @DryRun = 1
BEGIN
    PRINT N'DryRun = 1: chỉ in danh sách, không ghi.';
    RETURN;
END

BEGIN TRANSACTION;
INSERT INTO dbo.UserRole (Id, UserGroupId, ObjectMenuId, AllowAccess, AllowAdd, AllowEdit, AllowDelete, AllowPrint, AllowExport, AllowImport, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), p.GroupId, @Menu, 1, 0, 1, 0, 0, 0, 0, @Now, @SystemUser, 0 FROM @Plan p WHERE p.Action = N'THEM';
UPDATE ur SET AllowAccess = 1, AllowEdit = 1, ModifiedDate = @Now, ModifiedBy = @SystemUser
FROM dbo.UserRole ur JOIN @Plan p ON p.UserRoleId = ur.Id WHERE p.Action = N'BAT';
UPDATE ur SET AllowEdit = 0, ModifiedDate = @Now, ModifiedBy = @SystemUser
FROM dbo.UserRole ur JOIN @Plan p ON p.UserRoleId = ur.Id WHERE p.Action = N'TAT_CEO';
COMMIT TRANSACTION;
PRINT N'Đã ghi.';
