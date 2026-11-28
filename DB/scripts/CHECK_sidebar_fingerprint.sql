/*
  DEPLOY-SYNC-1.6 (D-134): "dấu vân tay" sidebar theo vai trò – để so 2 DB (DB thử sau cập nhật vs DB dev của anh) có menu GIỐNG HỆT nhau.
  Mỗi dòng = 1 vai trò: số mục được thấy + băm SHA-256 của danh sách (nhóm cha thứ tự / tên → mục thứ tự / tên / link) đang bật và có quyền Truy cập.
  ADMIN = mọi mục đang bật. Chỉ đọc, không ghi. Chạy trên 2 DB rồi so cột Băm.
    sqlcmd -S <server> -d <db> -E -I -f 65001 -i DB/scripts/CHECK_sidebar_fingerprint.sql -W -s "|"
*/
SET NOCOUNT ON;
DECLARE @Roles TABLE (Code NVARCHAR(50) PRIMARY KEY);
INSERT INTO @Roles VALUES (N'ADMIN'), (N'OPS-SCM'), (N'OPS-WH'), (N'OPS-KIT'), (N'SAL-SALES'), (N'FIN-HR'), (N'CEO'), (N'COO'), (N'QA'), (N'FIN-IT');
;WITH Items AS (
    SELECT r.Code, CONCAT(p.OrderShow, N'|', p.Name, N'>', m.OrderShow, N'|', m.Name, N'|', m.MetaLink) AS Line
    FROM @Roles r
    JOIN dbo.ObjectMenu m ON ISNULL(m.IsDeleted, 0) = 0 AND m.IsActive = 1 AND m.LevelShow = 2
    JOIN dbo.ObjectMenu p ON p.Id = m.ParentId AND ISNULL(p.IsDeleted, 0) = 0 AND p.IsActive = 1
    WHERE r.Code = N'ADMIN'
       OR EXISTS (SELECT 1 FROM dbo.UserRole ur JOIN dbo.UserGroup g ON g.Id = ur.UserGroupId
                  WHERE ur.ObjectMenuId = m.Id AND ISNULL(ur.IsDeleted, 0) = 0 AND ur.AllowAccess = 1 AND ISNULL(g.IsDeleted, 0) = 0
                    AND ISNULL(NULLIF(g.Code, N''), g.Name) = r.Code)
)
SELECT Code AS [Vai trò], COUNT(*) AS [Số mục],
       CONVERT(NVARCHAR(64), HASHBYTES('SHA2_256', STRING_AGG(CAST(Line AS NVARCHAR(MAX)), N';') WITHIN GROUP (ORDER BY Line)), 2) AS [Băm]
FROM Items GROUP BY Code ORDER BY Code;
