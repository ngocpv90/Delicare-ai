/*
  D-68 (PROPOSED – làm theo quy tắc ROADMAP: mục PROPOSED chưa chốt thì làm theo đề xuất) – script DỮ LIỆU một lần cho DB đang chạy
  (không tạo / sửa bảng, cột; không DELETE).
  Màn tích hợp bot chỉ cho quản trị:
    int-channel, int-bot, int-identity, int-inbound, int-log : FIN-IT Truy cập + Thêm + Sửa + In + Xuất; nhóm khác = không quyền
    int-outbound                                             : như trên, thêm CEO / COO chỉ Truy cập (để thấy tin gửi lỗi)
  Chỉ đụng 16 nhóm vai trò chuẩn (ORG_STRUCTURE §2) – nhóm tự tạo khác (Administrator, nhóm thử…) giữ nguyên. Tài khoản IsAdmin luôn đủ quyền (không qua ma trận).
  Nguồn giống tools/AppDelicare.CodeGen Seeds.RestrictedScreens. Chạy lại lần 2 không đổi gì (idempotent). Người sửa = tài khoản "system".
  Sau khi chạy, admin vẫn chỉnh lại được từng ô trên màn ma trận quyền.

  Bắt buộc truyền -v DryRun=1 (chạy thử) hoặc -v DryRun=0 (chạy thật) – không có giá trị mặc định để tránh ghi nhầm.
  CÁCH CHẠY (chỉ sau khi backup; SQL Server 2022, mức tương thích 160):
    sqlcmd -S <server> -d <db> -E -f 65001 -v DryRun=1 -i DB/scripts/D68_revoke_int_screens.sql -W
    sqlcmd -S <server> -d <db> -E -f 65001 -v DryRun=0 -i DB/scripts/D68_revoke_int_screens.sql -W
  Người dùng đang đăng nhập cần đăng nhập lại để menu / quyền mới có hiệu lực.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @DryRun BIT = $(DryRun);

DECLARE @SystemUser UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.[User] WHERE UserName = N'system' AND ISNULL(IsDeleted, 0) = 0);
IF @SystemUser IS NULL
    THROW 50101, N'Không có tài khoản "system" (seed T23) – không ghi được người thao tác.', 1;
DECLARE @Now DATETIME = DATEADD(HOUR, 7, SYSUTCDATETIME()); -- giờ Việt Nam như ứng dụng

DECLARE @Roles TABLE (Name NVARCHAR(100));
INSERT INTO @Roles VALUES (N'CEO'), (N'CEO-PMO'), (N'COO'), (N'OPS-PLAN'), (N'OPS-SCM'), (N'OPS-KIT'), (N'OPS-LOG'), (N'OPS-POS'), (N'OPS-MNT'),
    (N'SAL-SALES'), (N'SAL-CON'), (N'SAL-CS'), (N'FIN-ACC'), (N'FIN-HR'), (N'FIN-IT'), (N'QA');

DECLARE @Screens TABLE (MetaLink NVARCHAR(200));
INSERT INTO @Screens VALUES (N'#/int-channel'), (N'#/int-bot'), (N'#/int-identity'), (N'#/int-inbound'), (N'#/int-outbound'), (N'#/int-log');

-- Quyền đích (nhóm, màn) – không có dòng ở đây = không quyền.
DECLARE @Allow TABLE (GroupName NVARCHAR(100), MetaLink NVARCHAR(200), A BIT, Ad BIT, E BIT, D BIT, P BIT, X BIT, I BIT);
INSERT INTO @Allow SELECT N'FIN-IT', MetaLink, 1, 1, 1, 0, 1, 1, 0 FROM @Screens;
INSERT INTO @Allow VALUES (N'CEO', N'#/int-outbound', 1, 0, 0, 0, 0, 0, 0), (N'COO', N'#/int-outbound', 1, 0, 0, 0, 0, 0, 0);

DECLARE @T TABLE (UserGroupId UNIQUEIDENTIFIER, GroupName NVARCHAR(100), ObjectMenuId UNIQUEIDENTIFIER, MetaLink NVARCHAR(200),
                  A BIT, Ad BIT, E BIT, D BIT, P BIT, X BIT, I BIT, UserRoleId UNIQUEIDENTIFIER NULL,
                  OA BIT NULL, OAd BIT NULL, OE BIT NULL, OD BIT NULL, OP BIT NULL, OX BIT NULL, OI BIT NULL);
INSERT INTO @T
SELECT g.Id, g.Name, m.Id, m.MetaLink,
       ISNULL(a.A, 0), ISNULL(a.Ad, 0), ISNULL(a.E, 0), ISNULL(a.D, 0), ISNULL(a.P, 0), ISNULL(a.X, 0), ISNULL(a.I, 0),
       ur.Id, ur.AllowAccess, ur.AllowAdd, ur.AllowEdit, ur.AllowDelete, ur.AllowPrint, ur.AllowExport, ur.AllowImport
FROM dbo.UserGroup g
JOIN @Roles r ON r.Name = g.Name
CROSS JOIN @Screens s
JOIN dbo.ObjectMenu m ON m.MetaLink = s.MetaLink AND ISNULL(m.IsDeleted, 0) = 0
LEFT JOIN @Allow a ON a.GroupName = g.Name AND a.MetaLink = s.MetaLink
OUTER APPLY (SELECT TOP 1 x.* FROM dbo.UserRole x WHERE x.UserGroupId = g.Id AND x.ObjectMenuId = m.Id AND ISNULL(x.IsDeleted, 0) = 0 ORDER BY x.CreatedDate) ur
WHERE ISNULL(g.IsDeleted, 0) = 0;

-- Dòng thay đổi: đã có mà khác quyền đích, hoặc chưa có mà được cấp.
DECLARE @Diff TABLE (UserGroupId UNIQUEIDENTIFIER, ObjectMenuId UNIQUEIDENTIFIER);
INSERT INTO @Diff
SELECT UserGroupId, ObjectMenuId FROM @T
WHERE (UserRoleId IS NULL AND A = 1)
   OR (UserRoleId IS NOT NULL AND (ISNULL(OA, 0) <> A OR ISNULL(OAd, 0) <> Ad OR ISNULL(OE, 0) <> E OR ISNULL(OD, 0) <> D OR ISNULL(OP, 0) <> P OR ISNULL(OX, 0) <> X OR ISNULL(OI, 0) <> I));

SELECT t.GroupName AS Nhom, t.MetaLink AS Man,
       CASE WHEN t.UserRoleId IS NULL THEN N'THÊM DÒNG' WHEN t.A = 0 THEN N'THU HỒI' ELSE N'ĐẶT LẠI' END AS ThaoTac,
       CONCAT(ISNULL(CAST(t.OA AS INT), 0), ISNULL(CAST(t.OAd AS INT), 0), ISNULL(CAST(t.OE AS INT), 0), ISNULL(CAST(t.OD AS INT), 0), ISNULL(CAST(t.OP AS INT), 0), ISNULL(CAST(t.OX AS INT), 0), ISNULL(CAST(t.OI AS INT), 0)) AS Cu_XemThemSuaXoaInXuatNhap,
       CONCAT(CAST(t.A AS INT), CAST(t.Ad AS INT), CAST(t.E AS INT), CAST(t.D AS INT), CAST(t.P AS INT), CAST(t.X AS INT), CAST(t.I AS INT)) AS Moi
FROM @T t JOIN @Diff d ON d.UserGroupId = t.UserGroupId AND d.ObjectMenuId = t.ObjectMenuId
ORDER BY t.MetaLink, t.GroupName;

IF @DryRun = 1
BEGIN
    PRINT N'Chạy thử (DryRun=1) – không ghi gì.';
    RETURN;
END

BEGIN TRANSACTION;

UPDATE ur SET AllowAccess = t.A, AllowAdd = t.Ad, AllowEdit = t.E, AllowDelete = t.D, AllowPrint = t.P, AllowExport = t.X, AllowImport = t.I,
    ModifiedDate = @Now, ModifiedBy = @SystemUser
FROM dbo.UserRole ur
JOIN @T t ON t.UserRoleId = ur.Id
JOIN @Diff d ON d.UserGroupId = t.UserGroupId AND d.ObjectMenuId = t.ObjectMenuId;
PRINT CONCAT(N'Cập nhật: ', @@ROWCOUNT, N' dòng.');

INSERT INTO dbo.UserRole (Id, UserGroupId, ObjectMenuId, AllowAccess, AllowAdd, AllowEdit, AllowDelete, AllowPrint, AllowExport, AllowImport, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), t.UserGroupId, t.ObjectMenuId, t.A, t.Ad, t.E, t.D, t.P, t.X, t.I, @Now, @SystemUser, 0
FROM @T t
WHERE t.UserRoleId IS NULL AND t.A = 1;
PRINT CONCAT(N'Thêm dòng: ', @@ROWCOUNT, N' dòng.');

COMMIT;
