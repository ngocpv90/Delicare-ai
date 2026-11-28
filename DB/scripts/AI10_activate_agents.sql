/*
  AI-10 (D-133): bật agent đợt 2 – SALES, PROC, LOG, HR – trên DB đã có seed đợt 1 (Agent_seed.sql chỉ THÊM, không ghi đè công tắc BotStatus).
  Script DỮ LIỆU (không tạo / sửa bảng, cột; không xóa): BotConfiguration kênh HERMES, BotCode SALES / PROC / LOG / HR đang Inactive → Active + AuditChange.
  Agent bị anh tắt SAU khi chạy script này không bị bật lại (once.order: mỗi DB 1 lần). Khóa agent vẫn ở biến môi trường AGENT_KEY_<MÃ> của ToolGateway.
  Chạy tay: -v DryRun=1 (thử) / 0 (thật). Chạy lại = 0 việc.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
DECLARE @DryRun BIT = $(DryRun);
DECLARE @By UNIQUEIDENTIFIER = ISNULL((SELECT TOP 1 Id FROM dbo.[User] WHERE UserName = N'system' AND ISNULL(IsDeleted, 0) = 0),
                                      (SELECT TOP 1 Id FROM dbo.[User] WHERE UserName = N'admin' AND ISNULL(IsDeleted, 0) = 0));
DECLARE @Now DATETIME = DATEADD(HOUR, 7, SYSUTCDATETIME());
BEGIN TRANSACTION;
DECLARE @On TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY, BotCode NVARCHAR(50));
UPDATE b SET BotStatus = N'Active', ModifiedDate = @Now, ModifiedBy = @By
OUTPUT inserted.Id, inserted.BotCode INTO @On
FROM dbo.BotConfiguration b JOIN dbo.IntegrationChannel c ON c.Id = b.IntegrationChannelId AND c.ChannelCode = N'HERMES' AND ISNULL(c.IsDeleted, 0) = 0
WHERE ISNULL(b.IsDeleted, 0) = 0 AND b.BotCode IN (N'SALES', N'PROC', N'LOG', N'HR') AND ISNULL(b.BotStatus, N'Inactive') <> N'Active';
INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), o.Id, N'Updated', N'BotConfiguration', CONCAT(N'- AI-10 (D-133): bật agent ', o.BotCode, N' (đợt 2)'), @Now, @By, 0 FROM @On o;
SELECT (SELECT COUNT(*) FROM @On) AS [Agent bật], CASE WHEN @DryRun = 1 THEN N'CHẠY THỬ – đã hoàn tác' ELSE N'ĐÃ GHI' END AS [Kết quả];
IF @DryRun = 1 ROLLBACK TRANSACTION; ELSE COMMIT TRANSACTION;
