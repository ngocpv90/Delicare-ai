/*
  UI-6.26 (D-107 – anh chốt 2026-11-24, GAP-PRD-10 APPROVED): dây chuyền bếp gắn theo nhóm món → ra lệnh chia mẻ đúng dây chuyền.
  1. Migration (idempotent): thêm cột DishCategorie.ProductionLineId uniqueidentifier NULL (tham chiếu logic → ProductionLine, KHÔNG tạo FK vật lý – D-30).
     Không thêm bảng, không thêm cột nào khác. Chạy thử (DryRun=1) chỉ báo "sẽ thêm cột", không đổi cấu trúc.
  2. Dữ liệu ban đầu: gán dây chuyền cho nhóm món CHƯA gán chỉ khi tên khớp rõ – nhóm có từ khóa (mặn · rau · canh · cơm · tráng miệng · phụ) và đúng MỘT
     dây chuyền đang hoạt động có cùng từ khóa trong tên. Nhóm không khớp / khớp nhiều dây chuyền → để trống (màn Dây chuyền cảnh báo) và liệt kê để Bếp trưởng xác nhận.
  Ghi ModifiedDate / ModifiedBy (tài khoản system) + AuditChange. Chạy lại lần 2 = 0 việc.
  Bắt buộc truyền -v DryRun=1 (chạy thử) hoặc -v DryRun=0 (chạy thật). DB dev: chạy thật ngay trong task (D-92 a).
    sqlcmd -S <server> -d <db> -E -f 65001 -v DryRun=1 -i DB/scripts/UI626_dishcategory_productionline.sql -s "|" -W
  Sau khi thêm cột: chạy lại DB/sp/uspDishCategory_GetAll.sql (danh sách có cột ProductionLineId).
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
GO
DECLARE @DryRun BIT = $(DryRun);
IF COL_LENGTH(N'dbo.DishCategorie', N'ProductionLineId') IS NULL
BEGIN
    IF @DryRun = 1
        SELECT N'DishCategorie.ProductionLineId' AS [Cột], N'CHẠY THỬ – sẽ thêm (uniqueidentifier NULL, không FK)' AS [Kết quả];
    ELSE
    BEGIN
        ALTER TABLE dbo.DishCategorie ADD ProductionLineId uniqueidentifier NULL;
        SELECT N'DishCategorie.ProductionLineId' AS [Cột], N'ĐÃ THÊM' AS [Kết quả];
    END
END
ELSE
    SELECT N'DishCategorie.ProductionLineId' AS [Cột], N'Đã có – bỏ qua' AS [Kết quả];
GO
DECLARE @DryRun BIT = $(DryRun);
IF COL_LENGTH(N'dbo.DishCategorie', N'ProductionLineId') IS NULL
BEGIN
    SELECT N'Chưa có cột (chạy thử) – bỏ qua bước gán' AS [Gán dây chuyền];
    RETURN;
END
DECLARE @SystemUser UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.[User] WHERE UserName = N'system' AND ISNULL(IsDeleted, 0) = 0);
IF @SystemUser IS NULL
    THROW 50101, N'Không có tài khoản "system" (seed T23) – không ghi được người thao tác.', 1;
DECLARE @Now DATETIME = DATEADD(HOUR, 7, SYSUTCDATETIME());
DECLARE @sql NVARCHAR(MAX) = N'
DECLARE @Kw TABLE (Kw NVARCHAR(50) PRIMARY KEY);
INSERT INTO @Kw (Kw) VALUES (N''mặn''), (N''rau''), (N''canh''), (N''cơm''), (N''tráng miệng''), (N''phụ'');
DECLARE @Cand TABLE (CategoryId UNIQUEIDENTIFIER, CategoryName NVARCHAR(500), Kw NVARCHAR(50), LineId UNIQUEIDENTIFIER NULL, LineName NVARCHAR(500) NULL, Hits INT);
INSERT INTO @Cand (CategoryId, CategoryName, Kw, LineId, LineName, Hits)
SELECT c.Id, c.Name, k.Kw,
       (SELECT TOP 1 l.Id FROM dbo.ProductionLine AS l WHERE ISNULL(l.IsDeleted, 0) = 0 AND ISNULL(l.Status, N''Active'') <> N''Inactive'' AND l.Name LIKE N''%'' + k.Kw + N''%'' COLLATE Vietnamese_CI_AI),
       (SELECT TOP 1 l.Name FROM dbo.ProductionLine AS l WHERE ISNULL(l.IsDeleted, 0) = 0 AND ISNULL(l.Status, N''Active'') <> N''Inactive'' AND l.Name LIKE N''%'' + k.Kw + N''%'' COLLATE Vietnamese_CI_AI),
       (SELECT COUNT(*) FROM dbo.ProductionLine AS l WHERE ISNULL(l.IsDeleted, 0) = 0 AND ISNULL(l.Status, N''Active'') <> N''Inactive'' AND l.Name LIKE N''%'' + k.Kw + N''%'' COLLATE Vietnamese_CI_AI)
FROM dbo.DishCategorie AS c CROSS JOIN @Kw AS k
WHERE ISNULL(c.IsDeleted, 0) = 0 AND c.ProductionLineId IS NULL AND c.Name LIKE N''%'' + k.Kw + N''%'' COLLATE Vietnamese_CI_AI;
-- Một nhóm chỉ gán khi đúng 1 từ khóa khớp và từ khóa đó khớp đúng 1 dây chuyền.
DECLARE @Assign TABLE (CategoryId UNIQUEIDENTIFIER PRIMARY KEY, CategoryName NVARCHAR(500), LineId UNIQUEIDENTIFIER, LineName NVARCHAR(500));
INSERT INTO @Assign SELECT CategoryId, MAX(CategoryName), MAX(LineId), MAX(LineName) FROM @Cand GROUP BY CategoryId HAVING COUNT(*) = 1 AND MAX(Hits) = 1;

SELECT a.CategoryName AS [Nhóm món], a.LineName AS [Dây chuyền], N''Gán'' AS [Xử lý] FROM @Assign AS a
UNION ALL
SELECT c.Name, NULL, N''Để trống – chưa khớp rõ, Bếp trưởng gán ở màn Dây chuyền & trạm bếp''
FROM dbo.DishCategorie AS c WHERE ISNULL(c.IsDeleted, 0) = 0 AND c.ProductionLineId IS NULL AND ISNULL(c.Status, N''Active'') <> N''Inactive'' AND c.Id NOT IN (SELECT CategoryId FROM @Assign)
ORDER BY 3, 1;

BEGIN TRANSACTION;
UPDATE c SET c.ProductionLineId = a.LineId, c.ModifiedDate = @Now, c.ModifiedBy = @SystemUser
FROM dbo.DishCategorie AS c JOIN @Assign AS a ON a.CategoryId = c.Id;
INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), a.CategoryId, N''Modified'', N''DishCategory'', N''- ProductionLineId: '''''''' => '''''' + CONVERT(NVARCHAR(36), a.LineId) + N'''''' (UI-6.26 D-107: gán theo tên khớp rõ – '' + a.LineName + N'')'', @Now, @SystemUser, 0
FROM @Assign AS a;
SELECT (SELECT COUNT(*) FROM @Assign) AS [Nhóm được gán],
       (SELECT COUNT(*) FROM dbo.DishCategorie AS c WHERE ISNULL(c.IsDeleted, 0) = 0 AND c.ProductionLineId IS NULL AND ISNULL(c.Status, N''Active'') <> N''Inactive'') AS [Nhóm còn trống],
       CASE WHEN @DryRun = 1 THEN N''CHẠY THỬ – đã hoàn tác'' ELSE N''ĐÃ GHI'' END AS [Kết quả];
IF @DryRun = 1 ROLLBACK TRANSACTION; ELSE COMMIT TRANSACTION;';
EXEC sp_executesql @sql, N'@DryRun BIT, @SystemUser UNIQUEIDENTIFIER, @Now DATETIME', @DryRun = @DryRun, @SystemUser = @SystemUser, @Now = @Now;
