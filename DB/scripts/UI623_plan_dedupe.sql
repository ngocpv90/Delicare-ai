/*
  UI-6.23 (D-104 – anh chốt 2026-11-24): MỘT kế hoạch sản xuất / ngày (khóa = ngày, OrganizationUnitId để trống).
  Lỗi cũ: hộp "Lập từ đơn hàng" gửi Đơn vị → Generate sinh kế hoạch thứ 2 đủ món cả ngày → ra lệnh = nấu + giữ chỗ nguyên liệu gấp đôi.
  Script DỮ LIỆU một lần (không tạo / sửa bảng, cột; không DELETE):
    1. Mỗi ngày có > 1 kế hoạch (không ca) → giữ 1 bản: ưu tiên bản đã có lệnh SX, rồi bản không đơn vị, rồi bản lập sớm nhất.
    2. Bản thừa Nháp, CHƯA có lệnh SX và CHƯA có nhu cầu nguyên liệu (MaterialRequirement) tham chiếu → xóa mềm kế hoạch + dòng (ghi lý do vào Note).
    3. Bản thừa đã có lệnh SX / nhu cầu NL / không còn Nháp → CHỈ LIỆT KÊ (số kế hoạch, ngày, số lệnh, giữ chỗ) để anh quyết – không sửa.
    4. Bản giữ lại có OrganizationUnitId → đặt NULL (Quyết định tự chọn QĐ-20: không xóa kế hoạch duy nhất của ngày, chỉ đưa về khóa ngày để màn 7 ngày tới /
       tự lập sau chốt tìm thấy).
  Ghi ModifiedDate / ModifiedBy (tài khoản system) + AuditChange. Chạy lại lần 2 = 0 việc.
  Bắt buộc truyền -v DryRun=1 (chạy thử) hoặc -v DryRun=0 (chạy thật). DB dev: chạy thật ngay trong task (D-92 a).
    sqlcmd -S <server> -d <db> -E -f 65001 -v DryRun=1 -i DB/scripts/UI623_plan_dedupe.sql -s "|" -W
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
DECLARE @DryRun BIT = $(DryRun);
DECLARE @SystemUser UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.[User] WHERE UserName = N'system' AND ISNULL(IsDeleted, 0) = 0);
IF @SystemUser IS NULL
    THROW 50101, N'Không có tài khoản "system" (seed T23) – không ghi được người thao tác.', 1;
DECLARE @Now DATETIME = DATEADD(HOUR, 7, SYSUTCDATETIME());
DECLARE @Reason NVARCHAR(200) = N'UI-6.23 (D-104): kế hoạch trùng ngày – một kế hoạch / ngày, xóa mềm bản thừa chưa có lệnh SX';

BEGIN TRANSACTION;
DECLARE @Plan TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY, PlanNumber NVARCHAR(100), ProductionDate DATE, OrganizationUnitId UNIQUEIDENTIFIER NULL, Status NVARCHAR(50),
    Orders INT, Reservations INT, Mrp INT, Rn INT, DayCount INT);
INSERT INTO @Plan (Id, PlanNumber, ProductionDate, OrganizationUnitId, Status, Orders, Reservations, Mrp, Rn, DayCount)
SELECT x.Id, x.PlanNumber, x.D, x.OrganizationUnitId, x.Status, x.Orders, x.Reservations, x.Mrp,
       ROW_NUMBER() OVER (PARTITION BY x.D ORDER BY CASE WHEN x.Orders > 0 THEN 0 ELSE 1 END, CASE WHEN x.OrganizationUnitId IS NULL THEN 0 ELSE 1 END, x.CreatedDate, x.Id),
       COUNT(*) OVER (PARTITION BY x.D)
FROM (
    SELECT p.Id, COALESCE(p.PlanNumber, p.PlanNo) AS PlanNumber, CAST(p.ProductionDate AS DATE) AS D, p.OrganizationUnitId, p.Status, p.CreatedDate,
           (SELECT COUNT(*) FROM dbo.ProductionOrder AS o WHERE o.ProductionPlanId = p.Id AND ISNULL(o.IsDeleted, 0) = 0 AND ISNULL(o.Status, N'') <> N'Cancelled') AS Orders,
           (SELECT COUNT(*) FROM dbo.MaterialReservation AS r JOIN dbo.ProductionOrder AS o ON o.Id = r.ProductionOrderId
             WHERE o.ProductionPlanId = p.Id AND ISNULL(r.IsDeleted, 0) = 0) AS Reservations,
           (SELECT COUNT(*) FROM dbo.MaterialRequirement AS m WHERE m.ProductionPlanId = p.Id AND ISNULL(m.IsDeleted, 0) = 0) AS Mrp
    FROM dbo.ProductionPlan AS p
    WHERE ISNULL(p.IsDeleted, 0) = 0 AND p.ShiftId IS NULL
) AS x;

-- Bản thừa xóa mềm được / chỉ liệt kê.
DECLARE @Drop TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY);
INSERT INTO @Drop (Id) SELECT Id FROM @Plan WHERE Rn > 1 AND Status = N'Draft' AND Orders = 0 AND Mrp = 0;

SELECT p.PlanNumber AS [Số kế hoạch], CONVERT(NVARCHAR(10), p.ProductionDate, 103) AS [Ngày SX], p.Status AS [Trạng thái], p.Orders AS [Lệnh SX], p.Reservations AS [Giữ chỗ],
       CASE WHEN d.Id IS NOT NULL THEN N'Xóa mềm (bản thừa Nháp)' WHEN p.Rn > 1 THEN N'CHỈ LIỆT KÊ – bản thừa đã có lệnh SX / nhu cầu NL, anh quyết'
            ELSE N'Giữ – bỏ đơn vị (một kế hoạch / ngày)' END AS [Xử lý]
FROM @Plan AS p LEFT JOIN @Drop AS d ON d.Id = p.Id
WHERE p.Rn > 1 OR (p.Rn = 1 AND p.OrganizationUnitId IS NOT NULL)
ORDER BY p.ProductionDate, p.Rn;

-- 2. Xóa mềm bản thừa + dòng.
UPDATE pl SET pl.IsDeleted = 1, pl.ModifiedDate = @Now, pl.ModifiedBy = @SystemUser
FROM dbo.ProductionPlanLine AS pl JOIN @Drop AS d ON d.Id = pl.ProductionPlanId WHERE ISNULL(pl.IsDeleted, 0) = 0;
UPDATE p SET p.IsDeleted = 1, p.ModifiedDate = @Now, p.ModifiedBy = @SystemUser,
    p.Note = CASE WHEN ISJSON(p.Note) = 1 THEN JSON_MODIFY(p.Note, '$.DeletedReason', @Reason) ELSE LEFT(CONCAT(p.Note, CASE WHEN p.Note IS NULL THEN N'' ELSE N' | ' END, @Reason), 4000) END
FROM dbo.ProductionPlan AS p JOIN @Drop AS d ON d.Id = p.Id;
INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), d.Id, N'Deleted', N'ProductionPlan', N'- IsDeleted: ''False'' => ''True'' (' + @Reason + N')', @Now, @SystemUser, 0 FROM @Drop AS d;

-- 4. Bản giữ lại có đơn vị → khóa ngày.
DECLARE @Norm TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY, OldUnit UNIQUEIDENTIFIER);
INSERT INTO @Norm (Id, OldUnit) SELECT Id, OrganizationUnitId FROM @Plan WHERE Rn = 1 AND OrganizationUnitId IS NOT NULL;
UPDATE p SET p.OrganizationUnitId = NULL, p.ModifiedDate = @Now, p.ModifiedBy = @SystemUser
FROM dbo.ProductionPlan AS p JOIN @Norm AS n ON n.Id = p.Id;
INSERT INTO dbo.AuditChange (Id, IdChange, Action, EntityName, Changes, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), n.Id, N'Modified', N'ProductionPlan', N'- OrganizationUnitId: ''' + CONVERT(NVARCHAR(36), n.OldUnit) + N''' => '''' (UI-6.23 D-104: một kế hoạch / ngày)', @Now, @SystemUser, 0
FROM @Norm AS n;

SELECT (SELECT COUNT(*) FROM @Plan) AS [Kế hoạch đang có], (SELECT COUNT(*) FROM @Drop) AS [Xóa mềm], (SELECT COUNT(*) FROM @Norm) AS [Bỏ đơn vị],
       (SELECT COUNT(*) FROM @Plan WHERE Rn > 1) - (SELECT COUNT(*) FROM @Drop) AS [Chỉ liệt kê],
       CASE WHEN @DryRun = 1 THEN N'CHẠY THỬ – đã hoàn tác' ELSE N'ĐÃ GHI' END AS [Kết quả];
IF @DryRun = 1
    ROLLBACK TRANSACTION;
ELSE
    COMMIT TRANSACTION;
