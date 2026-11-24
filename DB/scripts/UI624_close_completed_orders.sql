/*
  UI-6.24 (D-105 – anh chốt 2026-11-24): lệnh sản xuất Hoàn thành chưa Đóng (trước khi có tự đóng) → nguyên liệu giữ chỗ dư còn treo, chưa có chênh lệch / giá thành.
  Script CHỈ LIỆT KÊ (không ghi gì – chạy DryRun 0 hay 1 đều như nhau): lệnh Hoàn thành chưa đóng + số dòng / số lượng giữ chỗ còn hiệu lực của lệnh.
  Việc ĐÓNG chạy bằng job "Đóng lệnh sản xuất đã xong" (ProductionAutoClose – Job/Run hoặc chờ 22:00) / API ProductionOrder/Close để tính chênh lệch tiêu hao,
  giá thành thực tế và giải phóng + phân bổ lại giữ chỗ đúng logic máy chủ. Chạy lại sau khi job chạy = 0 lệnh.
    sqlcmd -S <server> -d <db> -E -f 65001 -v DryRun=1 -i DB/scripts/UI624_close_completed_orders.sql -s "|" -W
*/
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
DECLARE @DryRun BIT = $(DryRun);

SELECT COALESCE(o.ProductionOrderNumber, o.ProductionOrderNo) AS [Số lệnh], CONVERT(NVARCHAR(10), p.ProductionDate, 103) AS [Ngày SX], d.Name AS [Món],
       CONVERT(NVARCHAR(16), o.ActualEndDate, 120) AS [Hoàn thành lúc],
       (SELECT COUNT(*) FROM dbo.MaterialReservation AS r WHERE r.ProductionOrderId = o.Id AND ISNULL(r.IsDeleted, 0) = 0 AND r.Status IN (N'Reserved', N'PartiallyIssued')) AS [Dòng giữ chỗ còn treo],
       (SELECT ISNULL(SUM(r.ReservedQuantity), 0) FROM dbo.MaterialReservation AS r WHERE r.ProductionOrderId = o.Id AND ISNULL(r.IsDeleted, 0) = 0 AND r.Status IN (N'Reserved', N'PartiallyIssued')) AS [SL giữ chỗ còn treo]
FROM dbo.ProductionOrder AS o
JOIN dbo.ProductionPlan AS p ON p.Id = o.ProductionPlanId
LEFT JOIN dbo.Dishe AS d ON d.Id = o.DishId
WHERE ISNULL(o.IsDeleted, 0) = 0 AND o.Status = N'Completed'
ORDER BY p.ProductionDate, [Số lệnh];

SELECT COUNT(*) AS [Lệnh Hoàn thành chưa đóng],
       (SELECT COUNT(*) FROM dbo.MaterialReservation AS r JOIN dbo.ProductionOrder AS o ON o.Id = r.ProductionOrderId
         WHERE ISNULL(o.IsDeleted, 0) = 0 AND o.Status = N'Completed' AND ISNULL(r.IsDeleted, 0) = 0 AND r.Status IN (N'Reserved', N'PartiallyIssued')) AS [Dòng giữ chỗ còn treo],
       N'CHỈ LIỆT KÊ – đóng bằng job ProductionAutoClose / ProductionOrder/Close' AS [Kết quả]
FROM dbo.ProductionOrder AS o WHERE ISNULL(o.IsDeleted, 0) = 0 AND o.Status = N'Completed';
