/*
  AI-6 (D-133): quyền cho nhóm menu "Trợ lý AI" (#/ai-agents, #/ai-agent-log, #/ai-schedule) và "Tri thức" (#/knowledge + mục quyền từng thư mục
  #/knowledge?folder=<mã> – Xem = đọc, Sửa = soạn; BOD chỉ Ban GĐ, CEO Sửa). Cờ lấy từ ma trận CodeGen (Seeds.Menu5 – MENU5_MATRIX.md), CHỈ các mục mới
  (DB dev chưa áp toàn bộ D79_permission_matrix – không đụng quyền cũ). Mục menu thêm bằng D79_menu_regroup.sql (chạy trước).
  Script DỮ LIỆU (không tạo bảng / cột, không DELETE): chưa có dòng → thêm; đã có → chỉ OR thêm cờ. Ghi CreatedBy (system) + AuditChange. Chạy lại = 0 việc.
    sqlcmd -S <server> -d <db> -E -I -b -f 65001 -v DryRun=1 -i DB/scripts/AI6_agent_knowledge_rights.sql -s "|" -W
  144 dòng quyền (nhóm × mục). Sinh lại: chạy D79_permission_matrix.sql DryRun=1 rồi node gen-ai6.cjs.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
DECLARE @DryRun BIT = $(DryRun);
DECLARE @SystemUser UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.[User] WHERE UserName = N'system' AND ISNULL(IsDeleted, 0) = 0);
IF @SystemUser IS NULL
    THROW 50101, N'Không có tài khoản "system" (seed T23) – không ghi được người thao tác.', 1;
DECLARE @Now DATETIME = DATEADD(HOUR, 7, SYSUTCDATETIME());

BEGIN TRANSACTION;
DECLARE @Want TABLE (Code NVARCHAR(50), Link NVARCHAR(200), A BIT, Ad BIT, E BIT, D BIT, P BIT, X BIT, I BIT, PRIMARY KEY (Code, Link));
INSERT INTO @Want VALUES
    (N'CEO', N'#/ai-agent-log', 1, 0, 0, 0, 0, 0, 0),
    (N'CEO', N'#/ai-agents', 1, 1, 1, 0, 0, 0, 0),
    (N'CEO', N'#/ai-schedule', 1, 0, 0, 0, 0, 0, 0),
    (N'CEO', N'#/knowledge', 1, 0, 0, 0, 0, 0, 0),
    (N'CEO', N'#/knowledge?folder=10', 1, 1, 1, 0, 0, 0, 0),
    (N'CEO', N'#/knowledge?folder=20', 1, 0, 0, 0, 0, 0, 0),
    (N'CEO', N'#/knowledge?folder=30', 1, 0, 0, 0, 0, 0, 0),
    (N'CEO', N'#/knowledge?folder=40', 1, 0, 0, 0, 0, 0, 0),
    (N'CEO', N'#/knowledge?folder=45', 1, 0, 0, 0, 0, 0, 0),
    (N'CEO', N'#/knowledge?folder=50', 1, 0, 0, 0, 0, 0, 0),
    (N'CEO', N'#/knowledge?folder=55', 1, 0, 0, 0, 0, 0, 0),
    (N'CEO', N'#/knowledge?folder=60', 1, 0, 0, 0, 0, 0, 0),
    (N'CEO', N'#/knowledge?folder=70', 1, 0, 0, 0, 0, 0, 0),
    (N'CEO', N'#/knowledge?folder=80', 1, 0, 0, 0, 0, 0, 0),
    (N'CEO', N'#/knowledge?folder=95', 1, 0, 0, 0, 0, 0, 0),
    (N'CEO', N'#/knowledge?folder=96', 1, 0, 0, 0, 0, 0, 0),
    (N'CEO', N'#/knowledge?folder=97', 1, 0, 0, 0, 0, 0, 0),
    (N'CEO', N'#/knowledge?folder=98', 1, 0, 0, 0, 0, 0, 0),
    (N'CEO', N'#/knowledge?folder=99', 1, 0, 0, 0, 0, 0, 0),
    (N'CEO', N'#/knowledge?folder=bod', 1, 1, 1, 0, 0, 0, 0),
    (N'CEO-PMO', N'#/knowledge', 1, 0, 0, 0, 0, 0, 0),
    (N'CEO-PMO', N'#/knowledge?folder=10', 1, 0, 0, 0, 0, 0, 0),
    (N'CEO-PMO', N'#/knowledge?folder=20', 1, 0, 0, 0, 0, 0, 0),
    (N'CEO-PMO', N'#/knowledge?folder=30', 1, 0, 0, 0, 0, 0, 0),
    (N'CEO-PMO', N'#/knowledge?folder=40', 1, 0, 0, 0, 0, 0, 0),
    (N'CEO-PMO', N'#/knowledge?folder=45', 1, 0, 0, 0, 0, 0, 0),
    (N'CEO-PMO', N'#/knowledge?folder=50', 1, 0, 0, 0, 0, 0, 0),
    (N'CEO-PMO', N'#/knowledge?folder=55', 1, 0, 0, 0, 0, 0, 0),
    (N'CEO-PMO', N'#/knowledge?folder=60', 1, 0, 0, 0, 0, 0, 0),
    (N'CEO-PMO', N'#/knowledge?folder=70', 1, 0, 0, 0, 0, 0, 0),
    (N'CEO-PMO', N'#/knowledge?folder=80', 1, 0, 0, 0, 0, 0, 0),
    (N'CEO-PMO', N'#/knowledge?folder=95', 1, 0, 0, 0, 0, 0, 0),
    (N'CEO-PMO', N'#/knowledge?folder=96', 1, 0, 0, 0, 0, 0, 0),
    (N'CEO-PMO', N'#/knowledge?folder=97', 1, 0, 0, 0, 0, 0, 0),
    (N'CEO-PMO', N'#/knowledge?folder=98', 1, 0, 0, 0, 0, 0, 0),
    (N'CEO-PMO', N'#/knowledge?folder=99', 1, 0, 0, 0, 0, 0, 0),
    (N'CEO-PMO', N'#/knowledge?folder=bod', 1, 0, 0, 0, 0, 0, 0),
    (N'COO', N'#/ai-agent-log', 1, 0, 0, 0, 0, 0, 0),
    (N'COO', N'#/ai-agents', 1, 1, 1, 0, 0, 0, 0),
    (N'COO', N'#/ai-schedule', 1, 0, 0, 0, 0, 0, 0),
    (N'COO', N'#/knowledge', 1, 0, 0, 0, 0, 0, 0),
    (N'COO', N'#/knowledge?folder=10', 1, 1, 1, 0, 0, 0, 0),
    (N'COO', N'#/knowledge?folder=20', 1, 0, 0, 0, 0, 0, 0),
    (N'COO', N'#/knowledge?folder=30', 1, 0, 0, 0, 0, 0, 0),
    (N'COO', N'#/knowledge?folder=40', 1, 0, 0, 0, 0, 0, 0),
    (N'COO', N'#/knowledge?folder=45', 1, 0, 0, 0, 0, 0, 0),
    (N'COO', N'#/knowledge?folder=50', 1, 0, 0, 0, 0, 0, 0),
    (N'COO', N'#/knowledge?folder=55', 1, 0, 0, 0, 0, 0, 0),
    (N'COO', N'#/knowledge?folder=60', 1, 0, 0, 0, 0, 0, 0),
    (N'COO', N'#/knowledge?folder=70', 1, 0, 0, 0, 0, 0, 0),
    (N'COO', N'#/knowledge?folder=80', 1, 1, 1, 0, 0, 0, 0),
    (N'COO', N'#/knowledge?folder=95', 1, 0, 0, 0, 0, 0, 0),
    (N'COO', N'#/knowledge?folder=96', 1, 0, 0, 0, 0, 0, 0),
    (N'COO', N'#/knowledge?folder=97', 1, 0, 0, 0, 0, 0, 0),
    (N'COO', N'#/knowledge?folder=98', 1, 0, 0, 0, 0, 0, 0),
    (N'COO', N'#/knowledge?folder=99', 1, 0, 0, 0, 0, 0, 0),
    (N'COO', N'#/knowledge?folder=bod', 1, 0, 0, 0, 0, 0, 0),
    (N'OPS-PLAN', N'#/knowledge', 1, 0, 0, 0, 0, 0, 0),
    (N'OPS-PLAN', N'#/knowledge?folder=30', 1, 0, 0, 0, 0, 0, 0),
    (N'OPS-PLAN', N'#/knowledge?folder=40', 1, 0, 0, 0, 0, 0, 0),
    (N'OPS-PLAN', N'#/knowledge?folder=50', 1, 0, 0, 0, 0, 0, 0),
    (N'OPS-PLAN', N'#/knowledge?folder=80', 1, 0, 0, 0, 0, 0, 0),
    (N'OPS-PLAN', N'#/knowledge?folder=99', 1, 0, 0, 0, 0, 0, 0),
    (N'OPS-SCM', N'#/knowledge', 1, 0, 0, 0, 0, 0, 0),
    (N'OPS-SCM', N'#/knowledge?folder=30', 1, 0, 0, 0, 0, 0, 0),
    (N'OPS-SCM', N'#/knowledge?folder=40', 1, 0, 0, 0, 0, 0, 0),
    (N'OPS-SCM', N'#/knowledge?folder=80', 1, 0, 0, 0, 0, 0, 0),
    (N'OPS-SCM', N'#/knowledge?folder=99', 1, 0, 0, 0, 0, 0, 0),
    (N'OPS-WH', N'#/knowledge', 1, 0, 0, 0, 0, 0, 0),
    (N'OPS-WH', N'#/knowledge?folder=40', 1, 1, 1, 0, 0, 0, 0),
    (N'OPS-WH', N'#/knowledge?folder=45', 1, 0, 0, 0, 0, 0, 0),
    (N'OPS-WH', N'#/knowledge?folder=80', 1, 0, 0, 0, 0, 0, 0),
    (N'OPS-WH', N'#/knowledge?folder=99', 1, 0, 0, 0, 0, 0, 0),
    (N'OPS-SCM-HEAD', N'#/knowledge', 1, 0, 0, 0, 0, 0, 0),
    (N'OPS-SCM-HEAD', N'#/knowledge?folder=30', 1, 0, 0, 0, 0, 0, 0),
    (N'OPS-SCM-HEAD', N'#/knowledge?folder=40', 1, 1, 1, 0, 0, 0, 0),
    (N'OPS-SCM-HEAD', N'#/knowledge?folder=60', 1, 0, 0, 0, 0, 0, 0),
    (N'OPS-SCM-HEAD', N'#/knowledge?folder=80', 1, 0, 0, 0, 0, 0, 0),
    (N'OPS-SCM-HEAD', N'#/knowledge?folder=99', 1, 0, 0, 0, 0, 0, 0),
    (N'OPS-KIT', N'#/knowledge', 1, 0, 0, 0, 0, 0, 0),
    (N'OPS-KIT', N'#/knowledge?folder=30', 1, 1, 1, 0, 0, 0, 0),
    (N'OPS-KIT', N'#/knowledge?folder=45', 1, 0, 0, 0, 0, 0, 0),
    (N'OPS-KIT', N'#/knowledge?folder=50', 1, 1, 1, 0, 0, 0, 0),
    (N'OPS-KIT', N'#/knowledge?folder=80', 1, 0, 0, 0, 0, 0, 0),
    (N'OPS-KIT', N'#/knowledge?folder=99', 1, 0, 0, 0, 0, 0, 0),
    (N'OPS-LOG', N'#/knowledge', 1, 0, 0, 0, 0, 0, 0),
    (N'OPS-LOG', N'#/knowledge?folder=20', 1, 0, 0, 0, 0, 0, 0),
    (N'OPS-LOG', N'#/knowledge?folder=45', 1, 0, 0, 0, 0, 0, 0),
    (N'OPS-LOG', N'#/knowledge?folder=55', 1, 1, 1, 0, 0, 0, 0),
    (N'OPS-LOG', N'#/knowledge?folder=80', 1, 0, 0, 0, 0, 0, 0),
    (N'OPS-LOG', N'#/knowledge?folder=99', 1, 0, 0, 0, 0, 0, 0),
    (N'OPS-POS', N'#/knowledge', 1, 0, 0, 0, 0, 0, 0),
    (N'OPS-POS', N'#/knowledge?folder=80', 1, 0, 0, 0, 0, 0, 0),
    (N'OPS-POS', N'#/knowledge?folder=99', 1, 0, 0, 0, 0, 0, 0),
    (N'OPS-MNT', N'#/knowledge', 1, 0, 0, 0, 0, 0, 0),
    (N'OPS-MNT', N'#/knowledge?folder=80', 1, 0, 0, 0, 0, 0, 0),
    (N'OPS-MNT', N'#/knowledge?folder=99', 1, 0, 0, 0, 0, 0, 0),
    (N'SAL-SALES', N'#/knowledge', 1, 0, 0, 0, 0, 0, 0),
    (N'SAL-SALES', N'#/knowledge?folder=20', 1, 0, 0, 0, 0, 0, 0),
    (N'SAL-SALES', N'#/knowledge?folder=80', 1, 0, 0, 0, 0, 0, 0),
    (N'SAL-SALES', N'#/knowledge?folder=99', 1, 0, 0, 0, 0, 0, 0),
    (N'SAL-CON', N'#/knowledge', 1, 0, 0, 0, 0, 0, 0),
    (N'SAL-CON', N'#/knowledge?folder=20', 1, 0, 0, 0, 0, 0, 0),
    (N'SAL-CON', N'#/knowledge?folder=80', 1, 0, 0, 0, 0, 0, 0),
    (N'SAL-CON', N'#/knowledge?folder=99', 1, 0, 0, 0, 0, 0, 0),
    (N'SAL-HEAD', N'#/knowledge', 1, 0, 0, 0, 0, 0, 0),
    (N'SAL-HEAD', N'#/knowledge?folder=20', 1, 1, 1, 0, 0, 0, 0),
    (N'SAL-HEAD', N'#/knowledge?folder=30', 1, 0, 0, 0, 0, 0, 0),
    (N'SAL-HEAD', N'#/knowledge?folder=55', 1, 0, 0, 0, 0, 0, 0),
    (N'SAL-HEAD', N'#/knowledge?folder=80', 1, 0, 0, 0, 0, 0, 0),
    (N'SAL-HEAD', N'#/knowledge?folder=99', 1, 0, 0, 0, 0, 0, 0),
    (N'SAL-CS', N'#/knowledge', 1, 0, 0, 0, 0, 0, 0),
    (N'SAL-CS', N'#/knowledge?folder=20', 1, 0, 0, 0, 0, 0, 0),
    (N'SAL-CS', N'#/knowledge?folder=55', 1, 0, 0, 0, 0, 0, 0),
    (N'SAL-CS', N'#/knowledge?folder=80', 1, 0, 0, 0, 0, 0, 0),
    (N'SAL-CS', N'#/knowledge?folder=99', 1, 0, 0, 0, 0, 0, 0),
    (N'FIN-ACC', N'#/knowledge', 1, 0, 0, 0, 0, 0, 0),
    (N'FIN-ACC', N'#/knowledge?folder=20', 1, 0, 0, 0, 0, 0, 0),
    (N'FIN-ACC', N'#/knowledge?folder=40', 1, 0, 0, 0, 0, 0, 0),
    (N'FIN-ACC', N'#/knowledge?folder=60', 1, 1, 1, 0, 0, 0, 0),
    (N'FIN-ACC', N'#/knowledge?folder=80', 1, 0, 0, 0, 0, 0, 0),
    (N'FIN-ACC', N'#/knowledge?folder=99', 1, 0, 0, 0, 0, 0, 0),
    (N'FIN-HR', N'#/knowledge', 1, 0, 0, 0, 0, 0, 0),
    (N'FIN-HR', N'#/knowledge?folder=70', 1, 1, 1, 0, 0, 0, 0),
    (N'FIN-HR', N'#/knowledge?folder=80', 1, 0, 0, 0, 0, 0, 0),
    (N'FIN-HR', N'#/knowledge?folder=99', 1, 0, 0, 0, 0, 0, 0),
    (N'FIN-IT', N'#/ai-agent-log', 1, 0, 0, 0, 0, 0, 0),
    (N'FIN-IT', N'#/ai-agents', 1, 1, 1, 0, 0, 0, 0),
    (N'FIN-IT', N'#/ai-schedule', 1, 0, 0, 0, 0, 0, 0),
    (N'FIN-IT', N'#/knowledge', 1, 0, 0, 0, 0, 0, 0),
    (N'FIN-IT', N'#/knowledge?folder=80', 1, 0, 0, 0, 0, 0, 0),
    (N'FIN-IT', N'#/knowledge?folder=95', 1, 1, 1, 0, 0, 0, 0),
    (N'FIN-IT', N'#/knowledge?folder=96', 1, 0, 0, 0, 0, 0, 0),
    (N'FIN-IT', N'#/knowledge?folder=97', 1, 0, 0, 0, 0, 0, 0),
    (N'FIN-IT', N'#/knowledge?folder=98', 1, 0, 0, 0, 0, 0, 0),
    (N'FIN-IT', N'#/knowledge?folder=99', 1, 1, 1, 0, 0, 0, 0),
    (N'QA', N'#/knowledge', 1, 0, 0, 0, 0, 0, 0),
    (N'QA', N'#/knowledge?folder=30', 1, 0, 0, 0, 0, 0, 0),
    (N'QA', N'#/knowledge?folder=40', 1, 0, 0, 0, 0, 0, 0),
    (N'QA', N'#/knowledge?folder=45', 1, 1, 1, 0, 0, 0, 0),
    (N'QA', N'#/knowledge?folder=50', 1, 0, 0, 0, 0, 0, 0),
    (N'QA', N'#/knowledge?folder=55', 1, 0, 0, 0, 0, 0, 0),
    (N'QA', N'#/knowledge?folder=80', 1, 0, 0, 0, 0, 0, 0),
    (N'QA', N'#/knowledge?folder=99', 1, 0, 0, 0, 0, 0, 0);

IF EXISTS (SELECT 1 FROM @Want AS w WHERE NOT EXISTS (SELECT 1 FROM dbo.ObjectMenu AS m WHERE m.MetaLink = w.Link AND ISNULL(m.IsDeleted, 0) = 0))
    THROW 50104, N'Thiếu mục menu AI-6 – chạy D79_menu_regroup.sql trước.', 1;

DECLARE @Map TABLE (UserGroupId UNIQUEIDENTIFIER, Code NVARCHAR(50), MenuId UNIQUEIDENTIFIER, Link NVARCHAR(200), A BIT, Ad BIT, E BIT, D BIT, P BIT, X BIT, I BIT);
INSERT INTO @Map
SELECT g.Id, g.Code, m.Id, w.Link, w.A, w.Ad, w.E, w.D, w.P, w.X, w.I
FROM @Want AS w
JOIN dbo.UserGroup AS g ON g.Code = w.Code AND ISNULL(g.IsDeleted, 0) = 0
CROSS APPLY (SELECT TOP 1 Id FROM dbo.ObjectMenu WHERE MetaLink = w.Link AND ISNULL(IsDeleted, 0) = 0) AS m;

DECLARE @Or TABLE (UserGroupId UNIQUEIDENTIFIER);
UPDATE r SET AllowAccess = CASE WHEN x.A = 1 THEN 1 ELSE r.AllowAccess END, AllowAdd = CASE WHEN x.Ad = 1 THEN 1 ELSE r.AllowAdd END,
             AllowEdit = CASE WHEN x.E = 1 THEN 1 ELSE r.AllowEdit END, AllowDelete = CASE WHEN x.D = 1 THEN 1 ELSE r.AllowDelete END,
             AllowPrint = CASE WHEN x.P = 1 THEN 1 ELSE r.AllowPrint END, AllowExport = CASE WHEN x.X = 1 THEN 1 ELSE r.AllowExport END,
             AllowImport = CASE WHEN x.I = 1 THEN 1 ELSE r.AllowImport END, ModifiedDate = @Now, ModifiedBy = @SystemUser
OUTPUT inserted.UserGroupId INTO @Or
FROM dbo.UserRole AS r JOIN @Map AS x ON x.UserGroupId = r.UserGroupId AND x.MenuId = r.ObjectMenuId
WHERE ISNULL(r.IsDeleted, 0) = 0
  AND ((x.A = 1 AND ISNULL(r.AllowAccess, 0) = 0) OR (x.Ad = 1 AND ISNULL(r.AllowAdd, 0) = 0) OR (x.E = 1 AND ISNULL(r.AllowEdit, 0) = 0) OR (x.D = 1 AND ISNULL(r.AllowDelete, 0) = 0)
    OR (x.P = 1 AND ISNULL(r.AllowPrint, 0) = 0) OR (x.X = 1 AND ISNULL(r.AllowExport, 0) = 0) OR (x.I = 1 AND ISNULL(r.AllowImport, 0) = 0));
DECLARE @RoleOr INT = @@ROWCOUNT;

DECLARE @Added TABLE (UserGroupId UNIQUEIDENTIFIER);
INSERT INTO dbo.UserRole (Id, UserGroupId, ObjectMenuId, AllowAccess, AllowAdd, AllowEdit, AllowDelete, AllowPrint, AllowExport, AllowImport, CreatedDate, CreatedBy, IsDeleted)
OUTPUT inserted.UserGroupId INTO @Added
SELECT NEWID(), x.UserGroupId, x.MenuId, x.A, x.Ad, x.E, x.D, x.P, x.X, x.I, @Now, @SystemUser, 0
FROM @Map AS x
WHERE NOT EXISTS (SELECT 1 FROM dbo.UserRole AS r WHERE r.UserGroupId = x.UserGroupId AND r.ObjectMenuId = x.MenuId AND ISNULL(r.IsDeleted, 0) = 0);
DECLARE @RoleAdded INT = @@ROWCOUNT;

INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), u.UserGroupId, N'RoleSaveMany', N'UserGroup', N'- AI-6 (D-133): cấp quyền Trợ lý AI / Tri thức', @Now, @SystemUser, 0
FROM (SELECT UserGroupId FROM @Added UNION SELECT UserGroupId FROM @Or) AS u;

SELECT (SELECT COUNT(*) FROM @Want) AS [Dòng ma trận], (SELECT COUNT(*) FROM @Map) AS [Khớp nhóm có trên DB], @RoleAdded AS [Dòng quyền thêm], @RoleOr AS [Dòng quyền bổ sung cờ],
       CASE WHEN @DryRun = 1 THEN N'CHẠY THỬ – đã hoàn tác' ELSE N'ĐÃ GHI' END AS [Kết quả];
IF @DryRun = 1 ROLLBACK TRANSACTION; ELSE COMMIT TRANSACTION;
