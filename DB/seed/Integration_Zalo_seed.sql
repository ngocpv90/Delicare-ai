-- AI-19 (D-133 đợt 4, D-06): kênh Zalo OA cho KHÁCH HÀNG – IntegrationChannel ZALO_OA + BotConfiguration ZALO_KH (bot khách, CSKH).
-- Idempotent, CHỈ THÊM (khóa ChannelCode / BotCode). Mặc định Inactive – IT bật trên màn #/int-channel khi đã có Zalo OA (app id / secret / OA secret ở biến môi trường).
-- KHÔNG chứa bí mật: SecretReference = TÊN biến môi trường (Zalo__AppSecret, Zalo__OaSecretKey, Zalo__RefreshToken của api / messaging).
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @SeedBy UNIQUEIDENTIFIER = (SELECT TOP (1) Id FROM [dbo].[User] WHERE UserName = N'admin' AND IsDeleted = 0);
IF @SeedBy IS NULL THROW 50001, N'Không có tài khoản admin để ghi CreatedBy cho seed.', 1;
DECLARE @Now DATETIME2 = SYSDATETIME();
BEGIN TRAN;
IF NOT EXISTS (SELECT 1 FROM [dbo].[IntegrationChannel] WHERE ChannelCode = N'ZALO_OA' AND IsDeleted = 0)
    INSERT INTO [dbo].[IntegrationChannel] (Id, ChannelCode, ChannelName, ProviderCode, EnvironmentCode, ChannelStatus, ConfigJson, SecretReference, Note, CreatedDate, CreatedBy, IsDeleted)
    VALUES (NEWID(), N'ZALO_OA', N'Zalo OA – Delicare (khách hàng)', N'ZALO', N'Development', N'Inactive', N'{}', N'Zalo__AppSecret', N'AI-19 seed (D-06) – token làm mới lưu mã hóa ở ConfigJson', @Now, @SeedBy, 0);
INSERT INTO [dbo].[BotConfiguration] (Id, IntegrationChannelId, BotCode, BotName, TargetAudience, WebhookPath, BotStatus, SecretReference, Note, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), c.Id, N'ZALO_KH', N'Delicare – Zalo khách hàng', N'CUSTOMER', N'/zalo/ZALO_KH', N'Inactive', N'Zalo__OaSecretKey', N'AI-19 seed (D-06) – bot khách: xem đơn / thực đơn, xác nhận suất, báo thiếu, phản ánh, đổi món', @Now, @SeedBy, 0
FROM [dbo].[IntegrationChannel] c
WHERE c.ChannelCode = N'ZALO_OA' AND c.IsDeleted = 0 AND NOT EXISTS (SELECT 1 FROM [dbo].[BotConfiguration] x WHERE x.BotCode = N'ZALO_KH' AND x.IsDeleted = 0);
COMMIT;
