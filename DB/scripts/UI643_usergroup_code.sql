/*
  UI-6.43 (D-124 – anh chốt 2026-11-24, GAP-PERM-03 APPROVED): điền mã / tên tiếng Việt / mô tả / khối cho nhóm quyền.
  Chạy SAU migration DB/migrations/20261124_02_usergroup_code_block.sql. Script DỮ LIỆU (không DELETE; UserRole, UserClaim, ApprovalStep KHÔNG đổi):
    (a) Code = UPPER(Name) cho nhóm đang dùng chưa có Code (mã cũ giữ nguyên – luồng duyệt, nhóm gợi ý chức vụ, bot tìm theo Code).
    (b) 19 nhóm chuẩn (ORG_STRUCTURE §2): Name → tên tiếng Việt, Description → mô tả vai trò, GroupBlock → khối – CHỈ khi Name còn đúng bằng mã
        (không ghi đè tên quản trị đã sửa); Description / GroupBlock chỉ điền khi đang trống.
    (c) Nhóm ngoài 19 nhóm chuẩn: chỉ điền Code, liệt kê để dọn tay.
  Ghi ModifiedDate / ModifiedBy (system) + AuditChange. Chạy lại = 0 việc.
  -v DryRun=1 (thử) / 0 (thật). DB dev chạy thật (D-92 a).
    sqlcmd -S <server> -d <db> -E -b -f 65001 -v DryRun=1 -i DB/scripts/UI643_usergroup_code.sql -s "|" -W
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
DECLARE @DryRun BIT = $(DryRun);
DECLARE @SystemUser UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.[User] WHERE UserName = N'system' AND ISNULL(IsDeleted, 0) = 0);
IF @SystemUser IS NULL
    THROW 50101, N'Không có tài khoản "system" (seed T23) – không ghi được người thao tác.', 1;
IF COL_LENGTH(N'dbo.UserGroup', N'Code') IS NULL
    THROW 50103, N'Chưa chạy migration 20261124_02_usergroup_code_block.sql.', 1;
DECLARE @Now DATETIME = DATEADD(HOUR, 7, SYSUTCDATETIME());
DECLARE @RolesBefore INT = (SELECT COUNT(*) FROM dbo.UserRole), @ClaimsBefore INT = (SELECT COUNT(*) FROM dbo.UserClaim);

BEGIN TRANSACTION;
DECLARE @Map TABLE (Code NVARCHAR(50) PRIMARY KEY, Name NVARCHAR(200), Description NVARCHAR(500), Block NVARCHAR(30));
INSERT INTO @Map VALUES
    (N'CEO', N'Tổng giám đốc', N'Điều hành chung, xem mọi số liệu, duyệt cấp cao', N'BOARD'),
    (N'CEO-PMO', N'Trợ lý TGĐ / PMO', N'Điều phối, báo cáo, theo dõi dự án cho Tổng giám đốc', N'BOARD'),
    (N'COO', N'Giám đốc vận hành', N'Điều hành sản xuất – kho – giao hàng, duyệt vận hành', N'BOARD'),
    (N'OPS-PLAN', N'Kế hoạch vận hành', N'Dự báo, kế hoạch sản xuất, nhu cầu nguyên liệu', N'OPS'),
    (N'OPS-SCM', N'Mua hàng', N'Đề nghị mua, đơn mua, nhà cung cấp', N'OPS'),
    (N'OPS-WH', N'Thủ kho', N'Nhận hàng, xuất kho, tồn kho, kiểm kê', N'OPS'),
    (N'OPS-SCM-HEAD', N'Trưởng mua hàng & kho', N'Duyệt mua hàng, quản lý kho', N'OPS'),
    (N'OPS-KIT', N'Bếp', N'Sản xuất, mẻ nấu, chia suất', N'OPS'),
    (N'OPS-LOG', N'Điều phối giao hàng', N'Chuyến giao, theo dõi giao hàng, khay hộp', N'OPS'),
    (N'OPS-POS', N'Điểm phát suất', N'Nhận suất, phát suất, đối soát tại điểm', N'OPS'),
    (N'OPS-MNT', N'Bảo trì', N'Bảo trì thiết bị, xe', N'OPS'),
    (N'SAL-SALES', N'Kinh doanh', N'Khách hàng, báo giá, đơn hàng', N'SAL'),
    (N'SAL-CON', N'Hợp đồng & đơn hàng', N'Hợp đồng, đơn hàng theo ngày', N'SAL'),
    (N'SAL-HEAD', N'Trưởng kinh doanh', N'Duyệt giá, hợp đồng, theo dõi doanh số', N'SAL'),
    (N'SAL-CS', N'Chăm sóc khách hàng', N'Phản ánh khách hàng, chăm sóc sau bán', N'SAL'),
    (N'FIN-ACC', N'Kế toán', N'Công nợ, đối soát, giá vốn', N'FIN'),
    (N'FIN-HR', N'Nhân sự', N'Nhân viên, phân ca, chấm công, nghỉ phép', N'FIN'),
    (N'FIN-IT', N'Quản trị hệ thống', N'Tài khoản, phân quyền, cấu hình hệ thống', N'FIN'),
    (N'QA', N'QA / ATTP', N'Kiểm tra chất lượng, an toàn thực phẩm', N'QA');

DECLARE @Change TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY, OldName NVARCHAR(200), NewCode NVARCHAR(50) NULL, NewName NVARCHAR(200) NULL, NewDesc NVARCHAR(500) NULL, NewBlock NVARCHAR(30) NULL);
INSERT INTO @Change
SELECT g.Id, g.Name,
       CASE WHEN g.Code IS NULL THEN UPPER(LTRIM(RTRIM(g.Name))) END,
       CASE WHEN m.Code IS NOT NULL AND g.Name = m.Code THEN m.Name END,
       CASE WHEN m.Code IS NOT NULL AND g.Description IS NULL THEN m.Description END,
       CASE WHEN m.Code IS NOT NULL AND g.GroupBlock IS NULL THEN m.Block END
FROM dbo.UserGroup AS g
LEFT JOIN @Map AS m ON m.Code = ISNULL(g.Code, UPPER(LTRIM(RTRIM(g.Name))))
WHERE ISNULL(g.IsDeleted, 0) = 0 AND g.Name IS NOT NULL
  AND (g.Code IS NULL OR (m.Code IS NOT NULL AND (g.Name = m.Code OR g.Description IS NULL OR g.GroupBlock IS NULL)));
DELETE FROM @Change WHERE NewCode IS NULL AND NewName IS NULL AND NewDesc IS NULL AND NewBlock IS NULL;

SELECT c.OldName AS [Nhóm], ISNULL(c.NewCode, N'') AS [Mã mới], ISNULL(c.NewName, N'') AS [Tên mới], ISNULL(c.NewBlock, N'') AS [Khối] FROM @Change AS c ORDER BY c.OldName;

UPDATE g SET g.Code = ISNULL(c.NewCode, g.Code), g.Name = ISNULL(c.NewName, g.Name), g.Description = ISNULL(c.NewDesc, g.Description),
       g.GroupBlock = ISNULL(c.NewBlock, g.GroupBlock), g.ModifiedDate = @Now, g.ModifiedBy = @SystemUser
FROM dbo.UserGroup AS g JOIN @Change AS c ON c.Id = g.Id;
INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), c.Id, N'Modified', N'UserGroup', N'- UI-6.43 (D-124): ' + ISNULL(N'Code => ''' + c.NewCode + N'''; ', N'') + ISNULL(N'Name => ''' + c.NewName + N'''; ', N'') + ISNULL(N'GroupBlock => ''' + c.NewBlock + N'''', N''),
       @Now, @SystemUser, 0 FROM @Change AS c;

-- (c) Nhóm ngoài 19 nhóm chuẩn – để anh dọn tay.
SELECT g.Name AS [Nhóm ngoài chuẩn (dọn tay)], g.Code AS [Mã],
       (SELECT COUNT(*) FROM dbo.UserClaim AS uc WHERE uc.UserGroupId = g.Id AND ISNULL(uc.IsDeleted, 0) = 0) AS [Tài khoản]
FROM dbo.UserGroup AS g WHERE ISNULL(g.IsDeleted, 0) = 0 AND NOT EXISTS (SELECT 1 FROM @Map AS m WHERE m.Code = g.Code) ORDER BY g.Name;

IF (SELECT COUNT(*) FROM dbo.UserRole) <> @RolesBefore OR (SELECT COUNT(*) FROM dbo.UserClaim) <> @ClaimsBefore
    THROW 50102, N'UserRole / UserClaim thay đổi – dừng.', 1;
SELECT (SELECT COUNT(*) FROM @Change) AS [Nhóm cập nhật], CASE WHEN @DryRun = 1 THEN N'CHẠY THỬ – đã hoàn tác' ELSE N'ĐÃ GHI' END AS [Kết quả];
IF @DryRun = 1 ROLLBACK TRANSACTION; ELSE COMMIT TRANSACTION;
