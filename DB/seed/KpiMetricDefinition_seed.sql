-- BE3.4 (spec 11.7, Catalog 221, BUSINESS_RULES §12): định nghĩa chỉ tiêu KPI phiên bản 1 – công thức tính nằm trong KpiCalculationService (theo MetricCode).
-- Công thức + mục tiêu: PROPOSED – BA/PO CONFIRM (DECISIONS "BE-3 – đề xuất" D-57 b, w–ab). Đổi công thức / mục tiêu = sửa trên màn #/kpi-definition
-- (hệ thống tạo phiên bản mới, đóng phiên bản cũ) – KHÔNG sửa seed này để đổi số.
-- Mục tiêu: KpiMetricDefinition không có cột mục tiêu (GAP-KPI-01) → thẻ "[TARGET >= x]" / "[TARGET <= x]" ở CUỐI FormulaDescription (đơn vị như giá trị: % = 0–100).
-- Mã theo spec 11.7; mã BUSINESS_RULES §12 tương ứng: ON_TIME_DELIVERY_PCT = DEL_ONTIME, INGREDIENT_COST_TO_REVENUE_PCT = FIN_FOOD_COST,
-- CONTAINER_LOSS_PCT = PKG_LOSS_RATE, COMPLAINT_SLA_MET_PCT = CS_COMPLAINT_SLA, CORE_TEMP_PASS_PCT = QC_CORE_TEMP_PASS, SAMPLE_RETAINED_PCT = QC_SAMPLE_RETAINED.
-- Dữ liệu cấu hình (không phải DEMO). Idempotent theo MetricCode: mã đã có (mọi phiên bản, kể cả đã xóa mềm) → KHÔNG thêm, KHÔNG ghi đè. Không DELETE / UPDATE.
-- Chạy: sqlcmd -S <server> -d AppDelicare -E -b -I -f 65001 -i DB/seed/KpiMetricDefinition_seed.sql
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @SeedBy UNIQUEIDENTIFIER = (SELECT TOP (1) Id FROM [dbo].[User] WHERE UserName = N'admin' AND IsDeleted = 0);
IF @SeedBy IS NULL THROW 50001, N'Không có tài khoản admin để ghi CreatedBy cho seed.', 1;
DECLARE @Now DATETIME2(0) = SYSDATETIME();
DECLARE @From DATE = '2026-01-01';

DECLARE @K TABLE (MetricCode NVARCHAR(100), MetricName NVARCHAR(255), DomainCode NVARCHAR(50), UnitCode NVARCHAR(30), AggregationCode NVARCHAR(30), FormulaDescription NVARCHAR(2000));
INSERT INTO @K VALUES
    (N'PROD_ACHIEVEMENT', N'Hoàn thành kế hoạch sản xuất', N'PROD', N'%', N'RATIO',
        N'Σ suất đạt (ProductionOutput.AcceptedQuantity) ÷ Σ suất kế hoạch (PlannedQuantity) của mẻ nấu bắt đầu trong kỳ, trừ mẻ Hủy × 100. Còn mẻ chưa ghi sản lượng → thiếu dữ liệu. PROPOSED – BA/PO CONFIRM. [TARGET >= 98]'),
    (N'WASTE_RATE', N'Tỷ lệ hao hụt suất ăn', N'PROD', N'%', N'RATIO',
        N'Σ suất hao hụt (MealWasteRecord.QuantityMeals, theo giờ phát sinh) ÷ Σ suất sản xuất (ProductionOutput.ProducedQuantity của mẻ trong kỳ) × 100. PROPOSED – BA/PO CONFIRM. [TARGET <= 2]'),
    (N'QC_REJECT_RATE', N'Tỷ lệ mẻ bị QC từ chối', N'QA', N'%', N'RATIO',
        N'Số mẻ QC Từ chối ÷ số mẻ đã có kết luận QC (Đạt / Từ chối; Tạm giữ chưa tính) trong kỳ × 100. PROPOSED – BA/PO CONFIRM. [TARGET <= 1]'),
    (N'QC_CORE_TEMP_PASS', N'Mẻ đạt nhiệt độ lõi', N'QA', N'%', N'RATIO',
        N'Mẻ đã nấu xong có nhiệt độ lõi đo gần nhất ≥ BusinessRules:CoreTempMinC (70 °C) ÷ số mẻ đã nấu xong × 100 (mẻ chưa đo = không đạt). BUSINESS_RULES §12 CORE_TEMP_PASS_PCT. PROPOSED – BA/PO CONFIRM. [TARGET >= 100]'),
    (N'QC_SAMPLE_RETAINED', N'Mẻ đã lưu mẫu', N'QA', N'%', N'RATIO',
        N'Mẻ Đạt / Đã đóng gói có checklist SAMPLE_RETAINED = Đạt ÷ số mẻ Đạt / Đã đóng gói × 100. BUSINESS_RULES §12 SAMPLE_RETAINED_PCT. PROPOSED – BA/PO CONFIRM. [TARGET >= 100]'),
    (N'DEL_ONTIME', N'Giao đúng giờ', N'DEL', N'%', N'RATIO',
        N'Đơn giao Đã giao trước giờ giao dự kiến + BusinessRules:Kpi:OnTimeToleranceMinutes (15 phút; không có giờ dự kiến = trong ngày giao) ÷ đơn giao Đã giao + Thất bại theo ngày giao × 100. BUSINESS_RULES §12 ON_TIME_DELIVERY_PCT. PROPOSED – BA/PO CONFIRM. [TARGET >= 95]'),
    (N'INV_ACCURACY', N'Độ chính xác tồn kho', N'INV', N'%', N'RATIO',
        N'(1 − Σ |giá trị chênh lệch| ÷ Σ giá trị tồn hệ thống) × 100 của phiếu kiểm kê Đã ghi sổ theo ngày kiểm kê; giá trị = số lượng × đơn giá lô (MaterialLot.UnitCost), dòng không có đơn giá không tính. PROPOSED – BA/PO CONFIRM. [TARGET >= 98]'),
    (N'FIN_COST_MEAL', N'Giá vốn bình quân / suất', N'FIN', N'VND', N'RATIO',
        N'Σ giá vốn thực tế (MealCost.TotalActualCost) ÷ Σ suất nhận (AcceptedMealQuantity, 1 lần / dòng đơn) của đơn Đã giao / Đã đối soát theo ngày ăn. Đơn chưa tính giá vốn suất → thiếu dữ liệu. Chưa có mục tiêu – BA/PO CONFIRM.'),
    (N'FIN_FOOD_COST', N'Giá vốn / doanh thu (food cost)', N'FIN', N'%', N'RATIO',
        N'Σ giá vốn suất (MealCost.TotalActualCost) ÷ Σ doanh thu chưa VAT (MealCost.Revenue) của đơn Đã giao / Đã đối soát theo ngày ăn × 100. BUSINESS_RULES §12 INGREDIENT_COST_TO_REVENUE_PCT. PROPOSED – BA/PO CONFIRM. [TARGET <= 55]'),
    (N'PKG_LOSS_RATE', N'Thất thoát hộp / khay', N'PKG', N'%', N'RATIO',
        N'Σ hộp thất thoát (PackagingLedger LOSS) ÷ Σ hộp giao cho khách (OUT) theo ngày phát sinh × 100. BUSINESS_RULES §12 CONTAINER_LOSS_PCT. PROPOSED – BA/PO CONFIRM. [TARGET <= 3]'),
    (N'CS_COMPLAINT_SLA', N'Phản ánh xử lý đúng hạn', N'CS', N'%', N'RATIO',
        N'Phản ánh có hạn SLA trong kỳ (trừ Từ chối): xử lý xong trước hạn ÷ (đã xong + quá hạn chưa xong) × 100; chưa tới hạn chưa xong thì chưa tính. BUSINESS_RULES §12 COMPLAINT_SLA_MET_PCT. PROPOSED – BA/PO CONFIRM. [TARGET >= 95]');

INSERT INTO [dbo].[KpiMetricDefinition] (Id, MetricCode, MetricName, DomainCode, UnitCode, FormulaDescription, AggregationCode, VersionNo, EffectiveFrom, EffectiveTo, IsActive, CreatedDate, CreatedBy, IsDeleted)
SELECT NEWID(), k.MetricCode, k.MetricName, k.DomainCode, k.UnitCode, k.FormulaDescription, k.AggregationCode, 1, @From, NULL, 1, @Now, @SeedBy, 0
FROM @K AS k
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[KpiMetricDefinition] AS d WHERE d.MetricCode = k.MetricCode);
DECLARE @Added INT = @@ROWCOUNT;

PRINT CONCAT(N'KpiMetricDefinition_seed: thêm ', @Added, N' chỉ tiêu (mã đã có giữ nguyên).');
