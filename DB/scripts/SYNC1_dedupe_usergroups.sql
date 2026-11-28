/*
  DEPLOY-SYNC-1 (D-134): dọn nhóm quyền chuẩn bị TRÙNG mã. Nguyên nhân: sau UI-6.43 (Name nhóm đổi sang tiếng Việt, mã ở Code), seed cũ
  (UserRole_seed.sql, Integration_Telegram_seed.sql) tìm nhóm theo Name → tạo thêm nhóm Name = mã, Code trống (0 người dùng). Seed đã sửa khớp theo Code.
  Script DỮ LIỆU (không tạo / sửa bảng, cột; KHÔNG xóa cứng): với mỗi mã có > 1 nhóm, GIỮ nhóm có Code (nhiều người dùng nhất, cũ nhất);
  nhóm trùng KHÔNG có người dùng → IsDeleted = 1 cùng các dòng UserRole của nó; nhóm trùng CÓ người dùng → giữ nguyên, in ra để admin chuyển người.
  Chạy qua deploy/db/once.order (mọi DB 1 lần; DB không có trùng = 0 việc). Chạy tay: -v DryRun=1 (thử) / 0 (thật). Chạy lại = 0 việc.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
DECLARE @DryRun BIT = $(DryRun);
DECLARE @By UNIQUEIDENTIFIER = ISNULL((SELECT TOP 1 Id FROM dbo.[User] WHERE UserName = N'system' AND ISNULL(IsDeleted, 0) = 0),
                                      (SELECT TOP 1 Id FROM dbo.[User] WHERE UserName = N'admin' AND ISNULL(IsDeleted, 0) = 0));
DECLARE @Now DATETIME = DATEADD(HOUR, 7, SYSUTCDATETIME());
BEGIN TRANSACTION;
DECLARE @G TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY, RoleCode NVARCHAR(100), Users INT, HasCode BIT, CreatedDate DATETIME, Rn INT);
INSERT INTO @G
SELECT g.Id, ISNULL(NULLIF(g.Code, N''), g.Name),
       (SELECT COUNT(*) FROM dbo.UserClaim c WHERE c.UserGroupId = g.Id AND ISNULL(c.IsDeleted, 0) = 0),
       CASE WHEN NULLIF(g.Code, N'') IS NULL THEN 0 ELSE 1 END, g.CreatedDate, 0
FROM dbo.UserGroup g WHERE ISNULL(g.IsDeleted, 0) = 0;
;WITH x AS (SELECT Id, ROW_NUMBER() OVER (PARTITION BY RoleCode ORDER BY HasCode DESC, Users DESC, CreatedDate) AS r FROM @G) UPDATE g SET Rn = x.r FROM @G g JOIN x ON x.Id = g.Id;
DECLARE @Drop TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY, RoleCode NVARCHAR(100));
INSERT INTO @Drop SELECT Id, RoleCode FROM @G WHERE Rn > 1 AND Users = 0;

UPDATE r SET IsDeleted = 1, ModifiedDate = @Now, ModifiedBy = @By FROM dbo.UserRole r JOIN @Drop d ON d.Id = r.UserGroupId WHERE ISNULL(r.IsDeleted, 0) = 0;
DECLARE @Roles INT = @@ROWCOUNT;
UPDATE g SET IsDeleted = 1, ModifiedDate = @Now, ModifiedBy = @By FROM dbo.UserGroup g JOIN @Drop d ON d.Id = g.Id;
DECLARE @Groups INT = @@ROWCOUNT;
INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), d.Id, N'Deleted', N'UserGroup', CONCAT(N'- DEPLOY-SYNC-1 (D-134): nhóm trùng mã ', d.RoleCode, N' (không có người dùng) – xóa mềm cùng dòng quyền'), @Now, @By, 0 FROM @Drop d;

SELECT RoleCode AS [Mã trùng CÒN người dùng – admin chuyển người rồi chạy lại], Users AS [Người dùng] FROM @G WHERE Rn > 1 AND Users > 0;
SELECT @Groups AS [Nhóm trùng xóa mềm], @Roles AS [Dòng quyền xóa mềm], CASE WHEN @DryRun = 1 THEN N'CHẠY THỬ – đã hoàn tác' ELSE N'ĐÃ GHI' END AS [Kết quả];
IF @DryRun = 1 ROLLBACK TRANSACTION; ELSE COMMIT TRANSACTION;
