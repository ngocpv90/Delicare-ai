-- GAP-INT-02 (D-62 p1) – PROPOSED, KHÔNG TỰ CHẠY (không nằm trong deploy/db/scripts.order; DBA / anh duyệt trước – đây là thay đổi lược đồ: thêm index).
-- Mục đích: chống trùng update / tin ra ở mức DB và để khóa UPDLOCK, HOLDLOCK của BotChannelService chỉ khóa 1 khoảng khóa (không quét cả bảng).
-- Hiện tại (R1, chưa có index): BotChannelService đọc khóa UPDLOCK, HOLDLOCK theo IdempotencyKey trong transaction – đúng nhưng các lần nhận update chạy lần lượt.
-- Chạy lại được (kiểm tồn tại trước). Index có lọc: chỉ dòng có khóa và chưa xóa mềm.
SET NOCOUNT ON;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_InboundMessage_IdempotencyKey' AND object_id = OBJECT_ID(N'dbo.InboundMessage'))
    CREATE UNIQUE NONCLUSTERED INDEX [UX_InboundMessage_IdempotencyKey] ON [dbo].[InboundMessage] ([IdempotencyKey])
    WHERE [IdempotencyKey] IS NOT NULL AND [IsDeleted] = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_OutboundMessage_IdempotencyKey' AND object_id = OBJECT_ID(N'dbo.OutboundMessage'))
    CREATE UNIQUE NONCLUSTERED INDEX [UX_OutboundMessage_IdempotencyKey] ON [dbo].[OutboundMessage] ([IdempotencyKey])
    WHERE [IdempotencyKey] IS NOT NULL AND [IsDeleted] = 0;
