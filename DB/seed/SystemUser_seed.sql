-- T23 (D-48): tài khoản dịch vụ "system" cho job tự động (AppDelicare.Job) – CreatedBy / ModifiedBy / AuditChange của job = user này.
-- Id CỐ ĐỊNH 061877ad-396c-4386-8868-25d4940bf467 = SystemUsers:JobUserId trong appsettings.json (WebApi, dùng chung cho Job).
-- KHÔNG đăng nhập được: PassWord không phải hash hợp lệ (không PBKDF2, không MD5 hex) → VerifyPassword luôn sai. Không cấp nhóm quyền.
-- Chỉ thêm khi chưa có (idempotent); không đổi gì nếu đã có. Không tạo bảng / cột.
SET NOCOUNT ON;
DECLARE @SystemUserId UNIQUEIDENTIFIER = '061877ad-396c-4386-8868-25d4940bf467';
IF NOT EXISTS (SELECT 1 FROM [dbo].[User] WHERE Id = @SystemUserId)
BEGIN
    IF EXISTS (SELECT 1 FROM [dbo].[User] WHERE UserName = N'system' AND IsDeleted = 0)
        PRINT N'Đã có user tên "system" khác Id cố định – KHÔNG tạo. Đặt SystemUsers:JobUserId = Id của user đó hoặc đổi tên user kia.';
    ELSE
    BEGIN
        INSERT INTO [dbo].[User] (Id, UserName, PassWord, IsActive, IsAdmin, IsDefault, CreatedDate, CreatedBy, IsDeleted)
        VALUES (@SystemUserId, N'system', N'!NO-LOGIN!', 1, 0, 0, GETDATE(), @SystemUserId, 0);
        PRINT N'Đã tạo tài khoản dịch vụ system (job tự động).';
    END
END
ELSE
    PRINT N'Tài khoản system đã có – giữ nguyên.';
