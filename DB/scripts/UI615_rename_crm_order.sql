/*
  UI-6.15 (D-96 – anh chốt 2026-11-24): mục menu #/crm-order đổi tên "Đơn đặt suất" → "Đơn hàng theo ngày" (giữ id màn, quyền, link cũ).
  Script DỮ LIỆU một lần (không tạo / sửa bảng, cột; không DELETE): chỉ đổi ObjectMenu.Name khi tên đang là tên cũ ("Đơn đặt suất" / "Đơn đặt suất ăn"),
  không ghi đè tên admin tự sửa. Mục duyệt "Phê duyệt › Đơn đặt suất" (#/wf-inbox?type=crm-order) giữ nguyên (chứng từ vẫn gọi là đơn đặt suất).
  Ghi ModifiedDate / ModifiedBy (tài khoản system) + AuditChange. Chạy lại lần 2 không đổi gì. Số dòng UserRole không đổi.
  Bắt buộc truyền -v DryRun=1 (chạy thử) hoặc -v DryRun=0 (chạy thật). DB dev: chạy thật ngay trong task (D-92 a).
    sqlcmd -S <server> -d <db> -E -f 65001 -v DryRun=1 -i DB/scripts/UI615_rename_crm_order.sql -s "|" -W
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
DECLARE @DryRun BIT = $(DryRun);
DECLARE @SystemUser UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.[User] WHERE UserName = N'system' AND ISNULL(IsDeleted, 0) = 0);
IF @SystemUser IS NULL
    THROW 50101, N'Không có tài khoản "system" (seed T23) – không ghi được người thao tác.', 1;
DECLARE @Now DATETIME = DATEADD(HOUR, 7, SYSUTCDATETIME());
DECLARE @New NVARCHAR(200) = N'Đơn hàng theo ngày';

BEGIN TRANSACTION;
DECLARE @RolesBefore INT = (SELECT COUNT(*) FROM dbo.UserRole);
DECLARE @Menu TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY, OldName NVARCHAR(500));
INSERT INTO @Menu (Id, OldName)
SELECT m.Id, m.Name FROM dbo.ObjectMenu AS m
WHERE ISNULL(m.IsDeleted, 0) = 0 AND m.MetaLink = N'#/crm-order' AND m.Name IN (N'Đơn đặt suất', N'Đơn đặt suất ăn');

SELECT OldName AS [Tên cũ], @New AS [Tên mới] FROM @Menu;

UPDATE m SET m.Name = @New, m.ModifiedDate = @Now, m.ModifiedBy = @SystemUser
FROM dbo.ObjectMenu AS m JOIN @Menu AS x ON x.Id = m.Id;

INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), x.Id, N'Modified', N'ObjectMenu', N'- Name: ''' + x.OldName + N''' → ''' + @New + N''' (UI-6.15 D-96)', @Now, @SystemUser, 0
FROM @Menu AS x;

DECLARE @RolesAfter INT = (SELECT COUNT(*) FROM dbo.UserRole);
SELECT (SELECT COUNT(*) FROM @Menu) AS [Mục menu đổi], @RolesBefore AS [UserRole trước], @RolesAfter AS [UserRole sau],
       CASE WHEN @DryRun = 1 THEN N'CHẠY THỬ – đã hoàn tác' ELSE N'ĐÃ GHI' END AS [Kết quả];
IF @RolesAfter <> @RolesBefore
    THROW 50102, N'Số dòng UserRole thay đổi – dừng.', 1;
IF @DryRun = 1
    ROLLBACK TRANSACTION;
ELSE
    COMMIT TRANSACTION;
