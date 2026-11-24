/*
  UI-6.27 (D-108): giá thành chuẩn CHỈ tự tính (khóa công thức / đổi giá NCC / Tính lại) – trước đây màn Giá thành chuẩn cho thêm / sửa tay tự do → bản gõ tay song song.
  Script DỮ LIỆU (không tạo / sửa bảng, cột; không DELETE): mỗi món chỉ giữ 1 bản Đang hiệu lực = bản mới nhất (EffectiveFrom, rồi CreatedDate);
  các bản Active khác → Inactive (EffectiveTo = hôm nay nếu trống). Liệt kê món có bản bị ngừng lệch > 5 % so với bản giữ lại (ghi PROGRESS để anh xem).
  Ghi ModifiedDate / ModifiedBy (system) + AuditChange. Chạy lại lần 2 = 0 việc. -v DryRun=1 (thử) / 0 (thật). DB dev chạy thật (D-92 a).
    sqlcmd -S <server> -d <db> -E -f 65001 -v DryRun=1 -i DB/scripts/UI627_standardcost_cleanup.sql -s "|" -W
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
DECLARE @Rank TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY, DishId UNIQUEIDENTIFIER, Cost DECIMAL(18,4), Rn INT);
INSERT INTO @Rank SELECT s.Id, s.DishId, s.CostPerServing,
    ROW_NUMBER() OVER (PARTITION BY s.DishId ORDER BY ISNULL(s.EffectiveFrom, '19000101') DESC, s.CreatedDate DESC, s.Id)
FROM dbo.StandardCost AS s WHERE ISNULL(s.IsDeleted, 0) = 0 AND s.Status = N'Active';
DECLARE @Off TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY, DishId UNIQUEIDENTIFIER, Cost DECIMAL(18,4), KeepCost DECIMAL(18,4));
INSERT INTO @Off SELECT r.Id, r.DishId, r.Cost, k.Cost FROM @Rank AS r JOIN @Rank AS k ON k.DishId = r.DishId AND k.Rn = 1 WHERE r.Rn > 1;

SELECT d.Name AS [Món], o.Cost AS [Bản ngừng (đ/suất)], o.KeepCost AS [Bản giữ (đ/suất)],
       CASE WHEN o.KeepCost > 0 AND ABS(o.Cost - o.KeepCost) / o.KeepCost > 0.05 THEN N'Lệch > 5 % – anh xem' ELSE N'' END AS [Ghi chú]
FROM @Off AS o LEFT JOIN dbo.Dishe AS d ON d.Id = o.DishId ORDER BY d.Name;

UPDATE s SET s.Status = N'Inactive', s.EffectiveTo = ISNULL(s.EffectiveTo, CAST(@Now AS DATE)), s.ModifiedDate = @Now, s.ModifiedBy = @SystemUser
FROM dbo.StandardCost AS s JOIN @Off AS o ON o.Id = s.Id;
INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), o.Id, N'Modified', N'StandardCost', N'- Status: ''Active'' => ''Inactive'' (UI-6.27 D-108: mỗi món 1 bản giá thành chuẩn đang hiệu lực)', @Now, @SystemUser, 0 FROM @Off AS o;
SELECT (SELECT COUNT(*) FROM @Off) AS [Bản ngừng], (SELECT COUNT(DISTINCT DishId) FROM @Off) AS [Món],
       CASE WHEN @DryRun = 1 THEN N'CHẠY THỬ – đã hoàn tác' ELSE N'ĐÃ GHI' END AS [Kết quả];
IF @DryRun = 1 ROLLBACK TRANSACTION; ELSE COMMIT TRANSACTION;
