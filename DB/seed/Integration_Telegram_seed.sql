-- BOT-1.1 (D-62): kênh tin nhắn Telegram nội bộ – IntegrationChannel + BotConfiguration (mỗi bot 1 dòng) và quyền vào bot
-- (ObjectMenu #/bot-van-hanh, #/bot-dieu-hanh + UserRole – ma trận PROPOSED D-62, admin chỉnh trên màn quyền).
-- Idempotent, CHỈ THÊM (khóa theo ChannelCode / BotCode / MetaLink / nhóm × menu) – không ghi đè chỉnh sửa trên màn hình.
-- KHÔNG chứa bí mật: SecretReference = TÊN biến môi trường giữ token (token thật nằm ở env / user-secrets của container messaging).
-- EnvironmentCode = Development (PROPOSED): staging / production sửa trên màn #/int-channel sau khi chạy seed (cột chỉ để hiển thị).
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @SeedBy UNIQUEIDENTIFIER = (SELECT TOP (1) Id FROM [dbo].[User] WHERE UserName = N'admin' AND IsDeleted = 0);
IF @SeedBy IS NULL THROW 50001, N'Không có tài khoản admin để ghi CreatedBy cho seed.', 1;
DECLARE @Now DATETIME2 = SYSDATETIME();
BEGIN TRAN;

DECLARE @B TABLE (ChannelCode NVARCHAR(50), ChannelName NVARCHAR(255), BotCode NVARCHAR(50), BotName NVARCHAR(255), Audience NVARCHAR(100), Status NVARCHAR(30), OrderNo INT);
INSERT INTO @B VALUES
    (N'TELEGRAM_VH', N'Telegram – Delicare Vận hành', N'VH', N'Delicare Vận hành', N'INTERNAL', N'Active', 1),
    (N'TELEGRAM_DH', N'Telegram – Delicare Điều hành', N'DH', N'Delicare Điều hành', N'MANAGER', N'Active', 2),
    -- Bot Trợ lý (Hermes) không chạy trong container messaging – để Inactive tới đợt agent.
    (N'TELEGRAM_TRO_LY', N'Telegram – Delicare Trợ lý', N'TRO_LY', N'Delicare Trợ lý', N'BOARD', N'Inactive', 3);

INSERT INTO [dbo].[IntegrationChannel] (Id, ChannelCode, ChannelName, ProviderCode, EnvironmentCode, ChannelStatus, ConfigJson, SecretReference, Note, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), b.ChannelCode, b.ChannelName, N'TELEGRAM', N'Development', b.Status, N'{}',
       N'Telegram__Bots__' + b.BotCode + N'__Token', N'BOT-1.1 seed (D-62)', @Now, @SeedBy, 0
FROM @B b
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[IntegrationChannel] c WHERE c.ChannelCode = b.ChannelCode AND c.IsDeleted = 0);

INSERT INTO [dbo].[BotConfiguration] (Id, IntegrationChannelId, BotCode, BotName, TargetAudience, WebhookPath, BotStatus, SecretReference, Note, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), c.Id, b.BotCode, b.BotName, b.Audience, N'/tg/' + b.BotCode, b.Status,
       N'Telegram__Bots__' + b.BotCode + N'__Token', N'BOT-1.1 seed (D-62) – webhook secret: Telegram__Bots__' + b.BotCode + N'__WebhookSecret', @Now, @SeedBy, 0
FROM @B b
JOIN [dbo].[IntegrationChannel] c ON c.ChannelCode = b.ChannelCode AND c.IsDeleted = 0
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[BotConfiguration] x WHERE x.BotCode = b.BotCode AND x.IsDeleted = 0);

-- Quyền vào bot: mục menu cấp 2 trong nhóm "Tích hợp & KPI" (#/group/09). IsActive = 1 vì PermissionService bỏ qua menu tắt; FE không có màn cho 2 mục này (chỉ dùng cho ma trận quyền).
DECLARE @Parent UNIQUEIDENTIFIER = (SELECT TOP (1) Id FROM [dbo].[ObjectMenu] WHERE MetaLink = N'#/group/09' AND IsDeleted = 0);
IF NOT EXISTS (SELECT 1 FROM [dbo].[ObjectMenu] WHERE MetaLink = N'#/bot-van-hanh' AND IsDeleted = 0)
    INSERT INTO [dbo].[ObjectMenu] (Id, Name, ShortName, MetaLink, ParentId, LevelShow, OrderShow, IsActive, IsDefault, CreatedDate, CreatedBy, IsDeleted)
    VALUES (NEWID(), N'Bot Telegram – Vận hành', N'Bot VH', N'#/bot-van-hanh', @Parent, 2, 911, 1, 0, @Now, @SeedBy, 0);
IF NOT EXISTS (SELECT 1 FROM [dbo].[ObjectMenu] WHERE MetaLink = N'#/bot-dieu-hanh' AND IsDeleted = 0)
    INSERT INTO [dbo].[ObjectMenu] (Id, Name, ShortName, MetaLink, ParentId, LevelShow, OrderShow, IsActive, IsDefault, CreatedDate, CreatedBy, IsDeleted)
    VALUES (NEWID(), N'Bot Telegram – Điều hành', N'Bot ĐH', N'#/bot-dieu-hanh', @Parent, 2, 912, 1, 0, @Now, @SeedBy, 0);

-- Ma trận PROPOSED (D-62, BOT_TELEGRAM §0): bot VH – mọi nhóm vai trò nghiệp vụ; bot DH – Ban GĐ (CEO, CEO-PMO), COO, kế toán (FIN-ACC).
-- Trưởng phòng dùng chung nhóm với nhân viên → admin cấp thêm #/bot-dieu-hanh trên ma trận quyền (không tự đoán theo nhóm).
DECLARE @R TABLE (GroupName NVARCHAR(100), MetaLink NVARCHAR(200));
INSERT INTO @R (GroupName, MetaLink)
SELECT g.Name, N'#/bot-van-hanh' FROM (VALUES (N'CEO'), (N'CEO-PMO'), (N'COO'), (N'FIN-ACC'), (N'FIN-HR'), (N'FIN-IT'), (N'OPS-KIT'), (N'OPS-LOG'),
    (N'OPS-MNT'), (N'OPS-PLAN'), (N'OPS-POS'), (N'OPS-SCM'), (N'QA'), (N'SAL-CON'), (N'SAL-CS'), (N'SAL-SALES')) g(Name)
UNION ALL
SELECT g.Name, N'#/bot-dieu-hanh' FROM (VALUES (N'CEO'), (N'CEO-PMO'), (N'COO'), (N'FIN-ACC')) g(Name);
INSERT INTO [dbo].[UserRole] (Id, UserGroupId, ObjectMenuId, AllowAccess, AllowAdd, AllowEdit, AllowDelete, AllowPrint, AllowExport, AllowImport, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), g.Id, m.Id, 1, 0, 0, 0, 0, 0, 0, @Now, @SeedBy, 0
FROM @R r
JOIN [dbo].[UserGroup] g ON ISNULL(NULLIF(g.Code, N''), g.Name) = r.GroupName AND g.IsDeleted = 0 -- D-134: theo Code (UI-6.43 đổi Name sang tiếng Việt)
JOIN [dbo].[ObjectMenu] m ON m.MetaLink = r.MetaLink AND m.IsDeleted = 0
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[UserRole] ur WHERE ur.UserGroupId = g.Id AND ur.ObjectMenuId = m.Id AND ur.IsDeleted = 0);

COMMIT;
DECLARE @nc INT = (SELECT COUNT(*) FROM [dbo].[IntegrationChannel] WHERE ProviderCode = N'TELEGRAM' AND IsDeleted = 0);
DECLARE @nb INT = (SELECT COUNT(*) FROM [dbo].[BotConfiguration] WHERE IsDeleted = 0);
PRINT N'Integration_Telegram_seed: kênh Telegram ' + CAST(@nc AS NVARCHAR(10)) + N', bot ' + CAST(@nb AS NVARCHAR(10)) + N'.';
