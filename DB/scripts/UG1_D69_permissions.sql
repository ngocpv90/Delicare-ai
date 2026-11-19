/*
  D-69 (UG-1 C-17, C-23 – anh chốt 2026-11-19) – script DỮ LIỆU một lần cho DB đang chạy (không tạo / sửa bảng, cột; không DELETE).
  Seed UserRole_seed.sql chỉ THÊM dòng chưa có nên không nâng được quyền của dòng đã tồn tại → script này:
    C-17  OPS-KIT  : log-packing-order, log-packing-result → Truy cập + Thêm + Sửa
    (C-18 tự chấm công / lập đơn của mình: KHÔNG cấp quyền hr-* – làm trên màn "Việc của tôi" hr-my, không cần quyền màn.)
    C-23  FIN-HR   : adm-employee → Truy cập + Thêm + Sửa
  CHỈ NÂNG quyền (cờ 0 → 1), không hạ quyền nào; dòng chưa có thì thêm. Nguồn giống tools/AppDelicare.CodeGen Seeds.RightOverrides.
  Chạy lại lần 2 không đổi gì (idempotent). Người sửa = tài khoản "system".

  Bắt buộc truyền -v DryRun=1 (chạy thử) hoặc -v DryRun=0 (chạy thật) – không có giá trị mặc định để tránh ghi nhầm.
  CÁCH CHẠY (chỉ sau khi backup; SQL Server 2022, mức tương thích 160):
    sqlcmd -S <server> -d <db> -E -f 65001 -v DryRun=1 -i DB/scripts/UG1_D69_permissions.sql -W
    sqlcmd -S <server> -d <db> -E -f 65001 -v DryRun=0 -i DB/scripts/UG1_D69_permissions.sql -W
  Người dùng đang đăng nhập cần đăng nhập lại để menu / quyền mới có hiệu lực.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @DryRun BIT = $(DryRun);

DECLARE @SystemUser UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.[User] WHERE UserName = N'system' AND ISNULL(IsDeleted, 0) = 0);
IF @SystemUser IS NULL
    THROW 50101, N'Không có tài khoản "system" (seed T23) – không ghi được người thao tác.', 1;
DECLARE @Now DATETIME = DATEADD(HOUR, 7, SYSUTCDATETIME()); -- giờ Việt Nam như ứng dụng

-- 16 nhóm vai trò chuẩn (ORG_STRUCTURE §2). Nhóm '*' bên dưới = các nhóm này – nhóm tự tạo khác (thử nghiệm, Administrator…) không bị đụng.
DECLARE @Roles TABLE (Name NVARCHAR(100));
INSERT INTO @Roles VALUES (N'CEO'), (N'CEO-PMO'), (N'COO'), (N'OPS-PLAN'), (N'OPS-SCM'), (N'OPS-KIT'), (N'OPS-LOG'), (N'OPS-POS'), (N'OPS-MNT'),
    (N'SAL-SALES'), (N'SAL-CON'), (N'SAL-CS'), (N'FIN-ACC'), (N'FIN-HR'), (N'FIN-IT'), (N'QA');

-- (Nhóm, màn, Thêm, Sửa); Nhóm = '*' → 16 nhóm trên. Truy cập luôn = 1.
DECLARE @O TABLE (GroupName NVARCHAR(100), MetaLink NVARCHAR(200), Ad BIT, E BIT);
INSERT INTO @O VALUES
    (N'OPS-KIT', N'#/log-packing-order', 1, 1),
    (N'OPS-KIT', N'#/log-packing-result', 1, 1),
    (N'FIN-HR', N'#/adm-employee', 1, 1);

DECLARE @T TABLE (UserGroupId UNIQUEIDENTIFIER, GroupName NVARCHAR(100), ObjectMenuId UNIQUEIDENTIFIER, MetaLink NVARCHAR(200), Ad BIT, E BIT,
                  UserRoleId UNIQUEIDENTIFIER NULL, OldAccess BIT NULL, OldAdd BIT NULL, OldEdit BIT NULL);
INSERT INTO @T (UserGroupId, GroupName, ObjectMenuId, MetaLink, Ad, E, UserRoleId, OldAccess, OldAdd, OldEdit)
SELECT g.Id, g.Name, m.Id, m.MetaLink, o.Ad, o.E, ur.Id, ur.AllowAccess, ur.AllowAdd, ur.AllowEdit
FROM @O o
JOIN dbo.UserGroup g ON (o.GroupName = N'*' OR g.Name = o.GroupName) AND ISNULL(g.IsDeleted, 0) = 0 AND g.Name IN (SELECT Name FROM @Roles)
JOIN dbo.ObjectMenu m ON m.MetaLink = o.MetaLink AND ISNULL(m.IsDeleted, 0) = 0
OUTER APPLY (SELECT TOP 1 x.Id, x.AllowAccess, x.AllowAdd, x.AllowEdit FROM dbo.UserRole x
             WHERE x.UserGroupId = g.Id AND x.ObjectMenuId = m.Id AND ISNULL(x.IsDeleted, 0) = 0 ORDER BY x.CreatedDate) ur;

-- Bảng đối chiếu: chỉ các dòng sẽ thay đổi.
SELECT t.GroupName AS Nhom, t.MetaLink AS Man,
       CASE WHEN t.UserRoleId IS NULL THEN N'THÊM DÒNG' ELSE N'NÂNG QUYỀN' END AS ThaoTac,
       CONCAT(ISNULL(CAST(t.OldAccess AS INT), 0), '/', ISNULL(CAST(t.OldAdd AS INT), 0), '/', ISNULL(CAST(t.OldEdit AS INT), 0)) AS Cu_TruyCap_Them_Sua,
       CONCAT(1, '/', CASE WHEN t.Ad = 1 OR t.OldAdd = 1 THEN 1 ELSE 0 END, '/', CASE WHEN t.E = 1 OR t.OldEdit = 1 THEN 1 ELSE 0 END) AS Moi_TruyCap_Them_Sua
FROM @T t
WHERE t.UserRoleId IS NULL OR ISNULL(t.OldAccess, 0) = 0 OR (t.Ad = 1 AND ISNULL(t.OldAdd, 0) = 0) OR (t.E = 1 AND ISNULL(t.OldEdit, 0) = 0)
ORDER BY t.MetaLink, t.GroupName;

IF @DryRun = 1
BEGIN
    PRINT N'Chạy thử (DryRun=1) – không ghi gì.';
    RETURN;
END

BEGIN TRANSACTION;

UPDATE ur SET
    AllowAccess = 1,
    AllowAdd = CASE WHEN t.Ad = 1 THEN 1 ELSE ur.AllowAdd END,
    AllowEdit = CASE WHEN t.E = 1 THEN 1 ELSE ur.AllowEdit END,
    ModifiedDate = @Now, ModifiedBy = @SystemUser
FROM dbo.UserRole ur
JOIN @T t ON t.UserRoleId = ur.Id
WHERE ISNULL(ur.AllowAccess, 0) = 0 OR (t.Ad = 1 AND ISNULL(ur.AllowAdd, 0) = 0) OR (t.E = 1 AND ISNULL(ur.AllowEdit, 0) = 0);
PRINT CONCAT(N'Nâng quyền: ', @@ROWCOUNT, N' dòng.');

INSERT INTO dbo.UserRole (Id, UserGroupId, ObjectMenuId, AllowAccess, AllowAdd, AllowEdit, AllowDelete, AllowPrint, AllowExport, AllowImport, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), t.UserGroupId, t.ObjectMenuId, 1, t.Ad, t.E, 0, 0, 0, 0, @Now, @SystemUser, 0
FROM @T t
WHERE t.UserRoleId IS NULL;
PRINT CONCAT(N'Thêm dòng: ', @@ROWCOUNT, N' dòng.');

COMMIT;
