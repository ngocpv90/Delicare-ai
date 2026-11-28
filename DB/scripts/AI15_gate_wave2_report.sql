-- AI-15 (D-133): SỐ LIỆU CỬA ĐỢT 2 – chỉ đọc, không ghi gì (không nằm trong once.order). Chạy trên server thật sau ≥ 2 tuần dùng đợt 2:
--   sqlcmd -S <server> -d <db> -E -I -f 65001 -i DB/scripts/AI15_gate_wave2_report.sql -W -s "|" -v Tu=2026-12-01 Den=2026-12-15
-- (1) Tỷ lệ duyệt KHÔNG sửa theo agent / lệnh: Đã duyệt ÷ (Đã duyệt + Từ chối). Đề xuất của agent không sửa được ở bước duyệt (AgentWriteApprovalHandler không là
--     IApprovalProposalEditor) → "sửa" = từ chối rồi agent đề xuất lại. Hết hạn (Đã hủy, quyết định "Hết hạn (agent)") tính riêng, không vào mẫu số.
--     Ngưỡng qua cửa do anh đặt (D-133) – script chỉ báo số.
-- (2) Ghi nghiệp vụ không qua duyệt = 0: mọi AuditChange 'AgentCommand' phải gắn 1 yêu cầu AGENT_WRITE Đã duyệt (test AgentWriteAi10Tests / AgentWriteAi9Tests chứng minh trên code).
-- (3) Chi phí tháng (bao_chi_phi, AI-13) so ngân sách Agent:MonthlyBudget – xem thêm thẻ Chi phí ở Trợ lý AI › Trung tâm agent.
SET NOCOUNT ON;
DECLARE @Tu DATE = TRY_CONVERT(DATE, N'$(Tu)'), @Den DATE = TRY_CONVERT(DATE, N'$(Den)');
IF @Tu IS NULL SET @Tu = DATEADD(DAY, -14, CAST(GETDATE() AS DATE));
IF @Den IS NULL SET @Den = CAST(GETDATE() AS DATE);

;WITH R AS (
    SELECT r.Id, r.RequestStatus,
           JSON_VALUE(JSON_VALUE(r.PayloadJson, '$.proposal'), '$.agent') AS Agent,
           JSON_VALUE(JSON_VALUE(r.PayloadJson, '$.proposal'), '$.command') AS Command,
           CASE WHEN EXISTS (SELECT 1 FROM dbo.ApprovalDecision d WHERE d.ApprovalRequestId = r.Id AND d.Comment LIKE N'Hết hạn%') THEN 1 ELSE 0 END AS Expired
    FROM dbo.ApprovalRequest r
    WHERE r.BusinessType = N'AGENT_WRITE' AND ISNULL(r.IsDeleted, 0) = 0
      AND r.RequestedAt >= @Tu AND r.RequestedAt < DATEADD(DAY, 1, @Den)
)
SELECT N'1-Duyệt không sửa' AS Muc, ISNULL(Agent, N'?') AS Agent, ISNULL(Command, N'?') AS Lenh,
       COUNT(*) AS DeXuat,
       SUM(CASE WHEN RequestStatus = N'Approved' THEN 1 ELSE 0 END) AS DaDuyet,
       SUM(CASE WHEN RequestStatus = N'Rejected' THEN 1 ELSE 0 END) AS TuChoi,
       SUM(Expired) AS HetHan,
       SUM(CASE WHEN RequestStatus = N'Pending' THEN 1 ELSE 0 END) AS ChoDuyet,
       CAST(100.0 * SUM(CASE WHEN RequestStatus = N'Approved' THEN 1 ELSE 0 END)
            / NULLIF(SUM(CASE WHEN RequestStatus IN (N'Approved', N'Rejected') THEN 1 ELSE 0 END), 0) AS DECIMAL(5, 1)) AS TyLeDuyetKhongSua
FROM R
GROUP BY ROLLUP(Agent, Command)
ORDER BY CASE WHEN Agent IS NULL THEN 1 ELSE 0 END, Agent, CASE WHEN Command IS NULL THEN 1 ELSE 0 END, Command;

-- (2) Lệnh agent đã chạy (AuditChange AgentCommand) mà không có yêu cầu AGENT_WRITE Đã duyệt cùng mã lệnh → phải 0 dòng.
SELECT N'2-Ghi không qua duyệt' AS Muc, a.Id, a.CreatedDate, a.IdChange
FROM dbo.AuditChange a
WHERE a.EntityName = N'AgentCommand' AND a.Action = N'Executed' AND ISNULL(a.IsDeleted, 0) = 0 AND a.CreatedDate >= @Tu AND a.CreatedDate < DATEADD(DAY, 1, @Den)
  AND NOT EXISTS (SELECT 1 FROM dbo.ApprovalRequest r WHERE r.BusinessType = N'AGENT_WRITE' AND r.BusinessId = a.IdChange AND r.RequestStatus = N'Approved');

-- (3) Chi phí theo tháng (VND) từ bao_chi_phi.
SELECT N'3-Chi phí' AS Muc, FORMAT(l.RequestAt, 'yyyy-MM') AS Thang,
       JSON_VALUE(l.ErrorMessage, '$.Agent') AS Agent, COUNT(*) AS Luot,
       SUM(TRY_CONVERT(DECIMAL(18, 0), JSON_VALUE(l.ErrorMessage, '$.Vnd'))) AS ChiPhiVnd
FROM dbo.IntegrationLog l
WHERE l.DirectionCode = N'COST' AND l.OperationCode = N'agent_cost' AND ISNULL(l.IsDeleted, 0) = 0
  AND l.RequestAt >= @Tu AND l.RequestAt < DATEADD(DAY, 1, @Den)
GROUP BY FORMAT(l.RequestAt, 'yyyy-MM'), JSON_VALUE(l.ErrorMessage, '$.Agent')
ORDER BY Thang, ChiPhiVnd DESC;
