/*
  UI-6.17 (D-98 – anh chốt 2026-11-24): form Món ăn còn 5 ô – 2 cột chữ tự do trùng nghĩa ẩn khỏi giao diện, máy chủ tự ghi:
    Dish.MealCategory  = mã nhóm món (DishCategory.Code theo DishCategoryId, thiếu thì CategoryId)
    Dish.StandardPortion = Dish.StandardServingSize (khi có định lượng 1 suất)
  Script DỮ LIỆU một lần (không tạo / sửa bảng, cột; không DELETE): chỉ sửa dòng đang lệch / rỗng. Ghi ModifiedDate / ModifiedBy (system) + AuditChange.
  Chạy lại lần 2 không đổi gì. Bắt buộc -v DryRun=1 (thử) hoặc -v DryRun=0 (thật). DB dev: chạy thật ngay trong task (D-92 a).
    sqlcmd -S <server> -d <db> -E -f 65001 -v DryRun=1 -i DB/scripts/UI617_dish_cleanup.sql -s "|" -W
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
DECLARE @Fix TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY, OldCat NVARCHAR(200), NewCat NVARCHAR(200), OldPortion DECIMAL(18, 4), NewPortion DECIMAL(18, 4));
INSERT INTO @Fix (Id, OldCat, NewCat, OldPortion, NewPortion)
SELECT d.Id, d.MealCategory, c.Code, d.StandardPortion, ISNULL(d.StandardServingSize, d.StandardPortion)
FROM dbo.Dishe AS d
LEFT JOIN dbo.DishCategorie AS c ON c.Id = ISNULL(d.DishCategoryId, d.CategoryId) AND ISNULL(c.IsDeleted, 0) = 0
WHERE ISNULL(d.IsDeleted, 0) = 0
  AND ((c.Code IS NOT NULL AND ISNULL(d.MealCategory, N'') <> c.Code)
    OR (d.StandardServingSize IS NOT NULL AND ISNULL(d.StandardPortion, -1) <> d.StandardServingSize));

SELECT SUM(CASE WHEN NewCat IS NOT NULL AND ISNULL(OldCat, N'') <> NewCat THEN 1 ELSE 0 END) AS [Sửa phân loại],
       SUM(CASE WHEN ISNULL(OldPortion, -1) <> ISNULL(NewPortion, -1) THEN 1 ELSE 0 END) AS [Sửa khẩu phần] FROM @Fix;

UPDATE d SET d.MealCategory = ISNULL(x.NewCat, d.MealCategory), d.StandardPortion = x.NewPortion, d.ModifiedDate = @Now, d.ModifiedBy = @SystemUser
FROM dbo.Dishe AS d JOIN @Fix AS x ON x.Id = d.Id;

INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), x.Id, N'Modified', N'Dish',
       CONCAT(CASE WHEN x.NewCat IS NOT NULL AND ISNULL(x.OldCat, N'') <> x.NewCat THEN N'- MealCategory: ''' + ISNULL(x.OldCat, N'') + N''' → ''' + x.NewCat + N''' ' ELSE N'' END,
              CASE WHEN ISNULL(x.OldPortion, -1) <> ISNULL(x.NewPortion, -1) THEN N'- StandardPortion: ''' + ISNULL(CAST(x.OldPortion AS NVARCHAR(40)), N'') + N''' → ''' + CAST(x.NewPortion AS NVARCHAR(40)) + N''' ' ELSE N'' END,
              N'(UI-6.17 D-98)'), @Now, @SystemUser, 0
FROM @Fix AS x;

SELECT (SELECT COUNT(*) FROM @Fix) AS [Món sửa], CASE WHEN @DryRun = 1 THEN N'CHẠY THỬ – đã hoàn tác' ELSE N'ĐÃ GHI' END AS [Kết quả];
IF @DryRun = 1
    ROLLBACK TRANSACTION;
ELSE
    COMMIT TRANSACTION;
