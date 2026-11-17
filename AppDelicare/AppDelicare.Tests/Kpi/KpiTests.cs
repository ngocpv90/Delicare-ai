using System.Net;
using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Tests.Common;
using AppDelicare.Tests.Integration;
using Dapper;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Kpi
{
    /// <summary>
    /// BE3.4 – KPI (spec 11.7, Catalog 221–225): phiên bản định nghĩa, công thức trên dữ liệu mẫu, upsert chạy lại không nhân đôi, INCOMPLETE,
    /// chiều "đạt", KPI ngày / tháng, chi nhánh, nhật ký làm mới, chỉ admin được làm mới. Mỗi test dùng tháng riêng (factory dùng chung).
    /// </summary>
    public class KpiTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public KpiTests(InMemoryApiFactory factory)
        {
            _factory = factory;
            _factory.Clock.FixedNow = new DateTime(2033, 12, 20, 10, 0, 0);
        }

        private static string Key() => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        /// <summary>11 chỉ tiêu như seed DB/seed/KpiMetricDefinition_seed.sql (chỉ thêm mã chưa có).</summary>
        private async Task SeedDefinitionsAsync()
        {
            var defs = new (string Code, string Domain, string Unit, string Tag)[]
            {
                (KpiCode.ProdAchievement, "PROD", "%", "[TARGET >= 98]"),
                (KpiCode.WasteRate, "PROD", "%", "[TARGET <= 2]"),
                (KpiCode.QcRejectRate, "QA", "%", "[TARGET <= 1]"),
                (KpiCode.QcCoreTempPass, "QA", "%", "[TARGET >= 100]"),
                (KpiCode.QcSampleRetained, "QA", "%", "[TARGET >= 100]"),
                (KpiCode.DelOnTime, "DEL", "%", "[TARGET >= 95]"),
                (KpiCode.InvAccuracy, "INV", "%", "[TARGET >= 98]"),
                (KpiCode.FinCostMeal, "FIN", "VND", ""),
                (KpiCode.FinFoodCost, "FIN", "%", "[TARGET <= 55]"),
                (KpiCode.PkgLossRate, "PKG", "%", "[TARGET <= 3]"),
                (KpiCode.CsComplaintSla, "CS", "%", "[TARGET >= 95]"),
            };
            var existing = await _factory.QueryAsync(db => db.KpiMetricDefinition.Select(d => d.MetricCode).ToListAsync());
            await _factory.SeedAsync(db =>
            {
                foreach (var d in defs.Where(d => !existing.Contains(d.Code)))
                {
                    db.KpiMetricDefinition.Add(new KpiMetricDefinition
                    {
                        Id = Guid.NewGuid(), MetricCode = d.Code, MetricName = "KPI " + d.Code, DomainCode = d.Domain, UnitCode = d.Unit, AggregationCode = "RATIO",
                        FormulaDescription = ("Công thức " + d.Code + " " + d.Tag).Trim(), VersionNo = 1, EffectiveFrom = new DateTime(2026, 1, 1), IsActive = true,
                    });
                }
                return Task.CompletedTask;
            });
        }

        private static Dictionary<string, JsonElement> Metrics(JsonElement refresh)
            => refresh.GetProperty("metrics").EnumerateArray().ToDictionary(m => m.GetProperty("metricCode").GetString()!);

        private static decimal? Dec(JsonElement e, string name) => e.GetProperty(name).ValueKind == JsonValueKind.Null ? null : e.GetProperty(name).GetDecimal();

        [Fact]
        public async Task Definition_change_of_formula_or_target_creates_new_version_and_closes_old()
        {
            string code = "TST_" + Key();
            using var c = _factory.CreateAuthorizedClient();
            (await c.PostFormAsync("/api/KpiMetricDefinition/Insert", ("MetricCode", code.ToLowerInvariant()), ("MetricName", "Thử"), ("DomainCode", "prod"),
                ("UnitCode", "%"), ("FormulaDescription", "A ÷ B"), ("EffectiveFrom", new DateTime(2033, 1, 1)), ("TargetValue", 90m))).Ok();
            var v1 = await _factory.QueryAsync(db => db.KpiMetricDefinition.AsNoTracking().SingleAsync(d => d.MetricCode == code));
            v1.VersionNo.Should().Be(1);
            v1.DomainCode.Should().Be("PROD");
            v1.FormulaDescription.Should().Be("A ÷ B [TARGET >= 90]", "mục tiêu ghi thẻ cuối FormulaDescription (GAP-KPI-01)");

            (await c.PostFormAsync("/api/KpiMetricDefinition/Insert", ("MetricCode", code), ("MetricName", "Trùng"), ("DomainCode", "PROD"),
                ("FormulaDescription", "X"), ("EffectiveFrom", new DateTime(2033, 1, 1)))).Fail(code);
            (await c.PostFormAsync("/api/KpiMetricDefinition/Insert", ("MetricCode", "TST_" + Key()), ("MetricName", "Sai chiều"), ("DomainCode", "PROD"),
                ("FormulaDescription", "X"), ("EffectiveFrom", new DateTime(2033, 1, 1)), ("TargetDirection", "UP"))).Fail("HIGHER");

            // Chỉ đổi tên → sửa tại chỗ, không tạo phiên bản.
            (await c.PutFormAsync("/api/KpiMetricDefinition/Update", ("Id", v1.Id), ("MetricCode", code), ("MetricName", "Thử – đổi tên"), ("DomainCode", "PROD"),
                ("UnitCode", "%"), ("FormulaDescription", "A ÷ B [TARGET >= 90]"), ("EffectiveFrom", new DateTime(2033, 1, 1)))).Ok();
            (await _factory.QueryAsync(db => db.KpiMetricDefinition.CountAsync(d => d.MetricCode == code))).Should().Be(1);

            // Đổi mục tiêu → phiên bản 2 (hiệu lực hôm nay 20/12/2033), bản 1 đóng 19/12/2033, công thức bản 1 giữ nguyên.
            var changed = await c.PutFormAsync("/api/KpiMetricDefinition/Update", ("Id", v1.Id), ("MetricCode", code), ("MetricName", "Thử – đổi tên"), ("DomainCode", "PROD"),
                ("UnitCode", "%"), ("FormulaDescription", "A ÷ B"), ("EffectiveFrom", new DateTime(2033, 1, 1)), ("TargetValue", 2m), ("TargetDirection", "LOWER"));
            changed.Ok();
            changed.Message.Should().Contain("phiên bản 2");
            var versions = await _factory.QueryAsync(db => db.KpiMetricDefinition.AsNoTracking().Where(d => d.MetricCode == code).OrderBy(d => d.VersionNo).ToListAsync());
            versions.Should().HaveCount(2);
            versions[0].EffectiveTo.Should().Be(new DateTime(2033, 12, 19));
            versions[0].FormulaDescription.Should().Be("A ÷ B [TARGET >= 90]", "không sửa phiên bản cũ");
            versions[1].VersionNo.Should().Be(2);
            versions[1].EffectiveFrom.Should().Be(new DateTime(2033, 12, 20));
            versions[1].FormulaDescription.Should().Be("A ÷ B [TARGET <= 2]");

            var byId = (await c.GetResultAsync($"/api/KpiMetricDefinition/GetById?id={versions[1].Id}")).Ok();
            byId.GetProperty("targetValue").GetDecimal().Should().Be(2m);
            byId.GetProperty("targetDirection").GetString().Should().Be(KpiCode.LowerBetter);
            byId.GetProperty("formula").GetString().Should().Be("A ÷ B");
            byId.GetProperty("hasCalculator").GetBoolean().Should().BeFalse();

            // Bản cũ không sửa được; không đổi mã.
            (await c.PutFormAsync("/api/KpiMetricDefinition/Update", ("Id", v1.Id), ("MetricCode", code), ("MetricName", "Sửa bản cũ"), ("DomainCode", "PROD"),
                ("FormulaDescription", "C"), ("EffectiveFrom", new DateTime(2033, 1, 1)))).Fail("phiên bản mới nhất");
            (await c.PutFormAsync("/api/KpiMetricDefinition/Update", ("Id", versions[1].Id), ("MetricCode", code + "X"), ("MetricName", "Đổi mã"), ("DomainCode", "PROD"),
                ("FormulaDescription", "A ÷ B"), ("EffectiveFrom", new DateTime(2033, 12, 20)))).Fail("mã chỉ tiêu");

            // Xóa bản mới nhất chưa có số liệu → bản 1 mở lại hiệu lực; xóa bản cũ khi còn bản mới → 400.
            (await c.DeleteResultAsync($"/api/KpiMetricDefinition/Delete?id={versions[1].Id}")).Ok();
            (await _factory.QueryAsync(db => db.KpiMetricDefinition.AsNoTracking().SingleAsync(d => d.Id == v1.Id))).EffectiveTo.Should().BeNull();
            // Mã có bản đã xóa mềm: phiên bản kế tiếp đánh số sau bản đã xóa (UNIQUE gồm cả dòng xóa mềm).
            (await c.PutFormAsync("/api/KpiMetricDefinition/Update", ("Id", v1.Id), ("MetricCode", code), ("MetricName", "Thử"), ("DomainCode", "PROD"),
                ("FormulaDescription", "A ÷ C"), ("EffectiveFrom", new DateTime(2033, 12, 25)))).Ok();
            var v3 = await _factory.QueryAsync(db => db.KpiMetricDefinition.AsNoTracking().SingleAsync(d => d.MetricCode == code && !d.IsDeleted && d.VersionNo > 1));
            v3.VersionNo.Should().Be(3);
            v3.EffectiveFrom.Should().Be(new DateTime(2033, 12, 25), "ngày hiệu lực gửi lên sau bản đang dùng → dùng ngày đó");
            (await c.DeleteResultAsync($"/api/KpiMetricDefinition/Delete?id={v1.Id}")).Fail("phiên bản mới nhất");
        }

        [Fact]
        public async Task Daily_and_monthly_refresh_compute_spec_formulas_on_seeded_data_with_met_direction_and_branch()
        {
            await SeedDefinitionsAsync();
            string key = Key();
            DateTime day = new(2033, 5, 10);
            Guid branch = Guid.NewGuid(), customerA = Guid.NewGuid(), customerB = Guid.NewGuid(), po = Guid.NewGuid();
            Guid b1 = Guid.NewGuid(), b2 = Guid.NewGuid(), b3 = Guid.NewGuid(), lot = Guid.NewGuid(), count = Guid.NewGuid(), order = Guid.NewGuid(), line = Guid.NewGuid();
            Guid checklist = Guid.NewGuid(), sampleItem = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.OrganizationUnit.Add(new OrganizationUnit { Id = branch, CompanyId = Guid.NewGuid(), Code = "CN" + key, Name = "Chi nhánh " + key, UnitType = "BRANCH", Status = "Active" });
                db.Customer.AddRange(
                    new Customer { Id = customerA, Code = "A" + key, Name = "Khách A " + key, CIF = "A" + key, DisplayName = "A" + key, CustomerType = "FACTORY", Status = "Active", BranchId = branch },
                    new Customer { Id = customerB, Code = "B" + key, Name = "Khách B " + key, CIF = "B" + key, DisplayName = "B" + key, CustomerType = "FACTORY", Status = "Active" });
                // Sản xuất: b1 Đạt (kế hoạch 100, đạt 95), b2 Không đạt (kế hoạch 100, đạt 0), b3 Hủy (không tính).
                db.ProductionBatch.AddRange(
                    new ProductionBatch { Id = b1, ProductionOrderId = po, DishId = Guid.NewGuid(), RecipeId = Guid.NewGuid(), BatchNumber = "ME-" + key + "-1", StartDate = day.AddHours(6), PlannedQuantity = 100, Status = ProductionBatchStatus.Passed, QcStatus = BatchQcStatus.Pass },
                    new ProductionBatch { Id = b2, ProductionOrderId = po, DishId = Guid.NewGuid(), RecipeId = Guid.NewGuid(), BatchNumber = "ME-" + key + "-2", StartDate = day.AddHours(7), PlannedQuantity = 100, Status = ProductionBatchStatus.Failed, QcStatus = BatchQcStatus.Reject },
                    new ProductionBatch { Id = b3, ProductionOrderId = po, DishId = Guid.NewGuid(), RecipeId = Guid.NewGuid(), BatchNumber = "ME-" + key + "-3", StartDate = day.AddHours(8), PlannedQuantity = 50, Status = ProductionBatchStatus.Cancelled });
                db.ProductionOutput.AddRange(
                    new ProductionOutput { Id = Guid.NewGuid(), ProductionBatchId = b1, DishId = Guid.NewGuid(), ProducedQuantity = 100, AcceptedQuantity = 95, RejectedQuantity = 5 },
                    new ProductionOutput { Id = Guid.NewGuid(), ProductionBatchId = b2, DishId = Guid.NewGuid(), ProducedQuantity = 100, AcceptedQuantity = 0, RejectedQuantity = 100 });
                db.MealWasteRecord.Add(new MealWasteRecord { Id = Guid.NewGuid(), ProductionBatchId = b2, WasteType = "QC_REJECT", QuantityMeals = 10, QuantityKg = 3, OccurredAt = day.AddHours(10) });
                db.TemperatureLog.AddRange(
                    new TemperatureLog { Id = Guid.NewGuid(), ProductionBatchId = b1, MeasurementType = ProductionCode.CoreMeasurement, TemperatureC = 75, MeasuredAt = day.AddHours(9) },
                    new TemperatureLog { Id = Guid.NewGuid(), ProductionBatchId = b2, MeasurementType = ProductionCode.CoreMeasurement, TemperatureC = 80, MeasuredAt = day.AddHours(8) },
                    new TemperatureLog { Id = Guid.NewGuid(), ProductionBatchId = b2, MeasurementType = ProductionCode.CoreMeasurement, TemperatureC = 65, MeasuredAt = day.AddHours(9) });
                db.FoodSafetyChecklist.Add(new FoodSafetyChecklist { Id = checklist, Code = "PRD_" + key, Name = "Mẻ " + key, AreaCode = "KITCHEN", FrequencyCode = "BATCH" });
                db.FoodSafetyChecklistItem.Add(new FoodSafetyChecklistItem { Id = sampleItem, ChecklistId = checklist, ItemCode = ProductionCode.SampleRetainedItem, ItemName = "Lưu mẫu", SortOrder = 1 });
                db.FoodSafetyChecklistResult.Add(new FoodSafetyChecklistResult { Id = Guid.NewGuid(), ChecklistId = checklist, ChecklistItemId = sampleItem, ProductionBatchId = b1, ResultCode = ProductionCode.ResultPass, CheckDate = day, CheckedAt = day.AddHours(11) });
                // Giao: 11:10 (trễ 10′ ≤ 15′ → đúng giờ, khách A thuộc chi nhánh), 11:30 (trễ), Thất bại, Kế hoạch (không tính).
                DeliveryOrder Dlv(int i, Guid customer, string status, DateTime? actual) => new()
                {
                    Id = Guid.NewGuid(), DeliveryNumber = $"PH-{key}-{i}", OrderId = Guid.NewGuid(), CustomerId = customer, DeliveryDate = day, PlannedDeliveryTime = day.AddHours(11),
                    ActualDeliveryTime = actual, Status = status,
                };
                db.DeliveryOrder.AddRange(
                    Dlv(1, customerA, DeliveryOrderStatus.Delivered, day.AddHours(11).AddMinutes(10)),
                    Dlv(2, customerB, DeliveryOrderStatus.Delivered, day.AddHours(11).AddMinutes(30)),
                    Dlv(3, customerB, DeliveryOrderStatus.Failed, null),
                    Dlv(4, customerB, DeliveryOrderStatus.Planned, null));
                // Kiểm kê đã ghi sổ: tồn 100 × 10 000 = 1 000 000, lệch −2 × 10 000 → độ chính xác 98%.
                db.MaterialLot.Add(new MaterialLot { Id = lot, LotNo = "NL-" + key, LotNumber = "NL-" + key, IngredientId = Guid.NewGuid(), UnitCost = 10_000m, Status = "Available" });
                db.StockCount.Add(new StockCount { Id = count, CountNumber = "KK-" + key, WarehouseId = Guid.NewGuid(), CountDate = day.AddHours(16), Status = StockCountStatus.Posted });
                db.StockCountLine.Add(new StockCountLine { Id = Guid.NewGuid(), StockCountId = count, IngredientId = Guid.NewGuid(), MaterialLotId = lot, UnitId = Guid.NewGuid(), SystemQuantity = 100, PhysicalQuantity = 98, VarianceQuantity = -2, VarianceValue = -20_000m });
                // Giá vốn suất: 1 dòng đơn 10 suất nhận, 2 món: giá vốn 300 000, doanh thu 500 000 → 30 000 / suất, food cost 60%.
                db.Order.Add(new Order { Id = order, OrderNumber = "DH-" + key, OrderNo = "DH-" + key, CustomerId = customerA, OrderDate = day.AddDays(-2), MealDate = day, DeliveryDate = day, Status = OrderStatus.Delivered });
                db.MealCost.AddRange(
                    new MealCost { Id = Guid.NewGuid(), OrderId = order, OrderLineId = line, DishId = Guid.NewGuid(), AcceptedMealQuantity = 10, TotalActualCost = 200_000m, Revenue = 300_000m, CalculatedAt = day },
                    new MealCost { Id = Guid.NewGuid(), OrderId = order, OrderLineId = line, DishId = Guid.NewGuid(), AcceptedMealQuantity = 10, TotalActualCost = 100_000m, Revenue = 200_000m, CalculatedAt = day });
                // Hộp: giao 100, thất thoát 2 → 2%.
                db.PackagingLedger.AddRange(
                    new PackagingLedger { Id = Guid.NewGuid(), CustomerId = customerB, TransactionType = PackagingTransactionType.Out, PackagingType = "KHAY", Quantity = 100, TransactionAt = day.AddHours(11) },
                    new PackagingLedger { Id = Guid.NewGuid(), CustomerId = customerB, TransactionType = PackagingTransactionType.Loss, PackagingType = "KHAY", Quantity = 2, TransactionAt = day.AddHours(18) });
                // Phản ánh hạn SLA trong ngày: xong trước hạn, xong sau hạn, quá hạn chưa xong (giờ hiện tại 20/12/2033) → 1/3; Từ chối không tính.
                CustomerComplaint Cpl(int i, int dueHour, DateTime? resolved, string status) => new()
                {
                    Id = Guid.NewGuid(), ComplaintNo = $"PA-{key}-{i}", CustomerId = customerB, Severity = "MEDIUM", Description = "Thử", ReceivedAt = day.AddHours(1),
                    SlaDueAt = day.AddHours(dueHour), ResolvedAt = resolved, ComplaintStatus = status,
                };
                db.CustomerComplaint.AddRange(
                    Cpl(1, 10, day.AddHours(9), ComplaintStatus.Resolved),
                    Cpl(2, 12, day.AddHours(13), ComplaintStatus.Closed),
                    Cpl(3, 14, null, ComplaintStatus.InProgress),
                    Cpl(4, 15, null, ComplaintStatus.Rejected));
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();

            var refresh = (await c.PostFormAsync("/api/KpiSnapshot/Refresh", ("JobCode", "kpi_daily"), ("Date", day))).Ok();
            refresh.GetProperty("runStatus").GetString().Should().Be("Succeeded");
            refresh.GetProperty("sourceWatermark").GetString().Should().Be("MANUAL|2033-05-10");
            var m = Metrics(refresh);
            Dec(m[KpiCode.ProdAchievement], "value").Should().Be(47.5m, "95 ÷ 200 (mẻ Hủy không tính)");
            Dec(m[KpiCode.WasteRate], "value").Should().Be(5m, "10 ÷ 200");
            Dec(m[KpiCode.QcRejectRate], "value").Should().Be(50m);
            Dec(m[KpiCode.QcCoreTempPass], "value").Should().Be(50m, "mẻ 2 lần đo gần nhất 65 °C < 70");
            Dec(m[KpiCode.QcSampleRetained], "value").Should().Be(100m);
            Dec(m[KpiCode.DelOnTime], "value").Should().Be(33.3333m, "1 đúng giờ ÷ (2 đã giao + 1 thất bại)");
            Dec(m[KpiCode.InvAccuracy], "value").Should().Be(98m);
            Dec(m[KpiCode.FinCostMeal], "value").Should().Be(30_000m, "300 000 ÷ 10 suất (suất nhận tính 1 lần / dòng đơn)");
            Dec(m[KpiCode.FinFoodCost], "value").Should().Be(60m);
            Dec(m[KpiCode.PkgLossRate], "value").Should().Be(2m);
            Dec(m[KpiCode.CsComplaintSla], "value").Should().Be(33.3333m);
            m[KpiCode.DelOnTime].GetProperty("numerator").GetDecimal().Should().Be(1m);
            m[KpiCode.DelOnTime].GetProperty("denominator").GetDecimal().Should().Be(3m);
            m[KpiCode.DelOnTime].GetProperty("dataQualityStatus").GetString().Should().Be(KpiCode.QualityOk);
            // Chiều "đạt": cao tốt (≥) / thấp tốt (≤).
            m[KpiCode.WasteRate].GetProperty("met").GetBoolean().Should().BeFalse("5% > mục tiêu ≤ 2%");
            m[KpiCode.PkgLossRate].GetProperty("met").GetBoolean().Should().BeTrue("2% ≤ 3%");
            m[KpiCode.QcSampleRetained].GetProperty("met").GetBoolean().Should().BeTrue("100% ≥ 100%");
            m[KpiCode.DelOnTime].GetProperty("met").GetBoolean().Should().BeFalse();
            m[KpiCode.FinFoodCost].GetProperty("met").GetBoolean().Should().BeFalse("60% > 55% (COST: càng thấp càng tốt)");
            m[KpiCode.FinCostMeal].GetProperty("met").ValueKind.Should().Be(JsonValueKind.Null, "chưa có mục tiêu");
            Dec(m[KpiCode.PkgLossRate], "variance").Should().Be(-1m);

            // KPI ngày: definition + latest + series; lọc nhóm.
            var daily = (await c.GetResultAsync("/api/KpiSnapshot/GetDaily?From=2033-05-01&To=2033-05-31")).Ok();
            var list = daily.GetProperty("metrics").EnumerateArray().ToList();
            var del = list.Single(x => x.GetProperty("definition").GetProperty("metricCode").GetString() == KpiCode.DelOnTime);
            del.GetProperty("definition").GetProperty("targetValue").GetDecimal().Should().Be(95m);
            del.GetProperty("definition").GetProperty("targetDirection").GetString().Should().Be(KpiCode.HigherBetter);
            del.GetProperty("latest").GetProperty("value").GetDecimal().Should().Be(33.3333m);
            del.GetProperty("latest").GetProperty("target").GetDecimal().Should().Be(95m);
            del.GetProperty("latest").GetProperty("met").GetBoolean().Should().BeFalse();
            del.GetProperty("series").EnumerateArray().Single().GetProperty("date").GetDateTime().Should().Be(day);
            var qa = (await c.GetResultAsync("/api/KpiSnapshot/GetDaily?From=2033-05-01&To=2033-05-31&DomainCode=QA")).Ok();
            qa.GetProperty("metrics").EnumerateArray().Select(x => x.GetProperty("definition").GetProperty("domainCode").GetString()).Should().OnlyContain(d => d == "QA").And.HaveCount(3);

            // Chi nhánh: chỉ khách A (1 đơn giao đúng giờ) → 100%; dòng snapshot riêng (BranchId), không đè số toàn công ty.
            var byBranch = Metrics((await c.PostFormAsync("/api/KpiSnapshot/Refresh", ("JobCode", "KPI_DAILY"), ("Date", day), ("BranchId", branch))).Ok());
            Dec(byBranch[KpiCode.DelOnTime], "value").Should().Be(100m);
            Dec(byBranch[KpiCode.FinCostMeal], "value").Should().Be(30_000m, "đơn của khách A");
            Dec(byBranch[KpiCode.PkgLossRate], "value").Should().BeNull("khách B ngoài chi nhánh");
            var rows = await _factory.QueryAsync(db => db.KpiDailySnapshot.AsNoTracking().Where(x => x.SnapshotDate == day && x.MetricCode == KpiCode.DelOnTime).ToListAsync());
            rows.Should().HaveCount(2);
            rows.Single(x => x.BranchId == null).MetricValue.Should().Be(33.3333m);
            var branchDaily = (await c.GetResultAsync($"/api/KpiSnapshot/GetDaily?From=2033-05-01&To=2033-05-31&BranchId={branch}")).Ok();
            branchDaily.GetProperty("metrics").EnumerateArray().Single(x => x.GetProperty("definition").GetProperty("metricCode").GetString() == KpiCode.DelOnTime)
                .GetProperty("latest").GetProperty("value").GetDecimal().Should().Be(100m);

            // KPI tháng: tính thẳng trên cả tháng (cùng dữ liệu) → 1 điểm ngày 01/05/2033.
            var monthly = (await c.PostFormAsync("/api/KpiSnapshot/Refresh", ("JobCode", "KPI_MONTHLY"), ("Month", "2033-05"))).Ok();
            monthly.GetProperty("periodDate").GetDateTime().Should().Be(new DateTime(2033, 5, 1));
            monthly.GetProperty("to").GetDateTime().Should().Be(new DateTime(2033, 5, 31));
            Dec(Metrics(monthly)[KpiCode.ProdAchievement], "value").Should().Be(47.5m);
            var monthView = (await c.GetResultAsync("/api/KpiSnapshot/GetMonthly?FromMonth=2033-05&ToMonth=2033-05")).Ok();
            monthView.GetProperty("period").GetString().Should().Be("MONTHLY");
            var prod = monthView.GetProperty("metrics").EnumerateArray().Single(x => x.GetProperty("definition").GetProperty("metricCode").GetString() == KpiCode.ProdAchievement);
            prod.GetProperty("series").EnumerateArray().Single().GetProperty("date").GetDateTime().Should().Be(new DateTime(2033, 5, 1));
            prod.GetProperty("latest").GetProperty("met").GetBoolean().Should().BeFalse("47,5% < 98%");
        }

        [Fact]
        public async Task Refresh_twice_upserts_without_duplicates_and_logs_each_run()
        {
            await SeedDefinitionsAsync();
            DateTime day = new(2033, 7, 15);
            using var c = _factory.CreateAuthorizedClient();
            var first = (await c.PostFormAsync("/api/KpiSnapshot/Refresh", ("JobCode", "KPI_DAILY"), ("Date", day))).Ok();
            var second = (await c.PostFormAsync("/api/KpiSnapshot/Refresh", ("JobCode", "KPI_DAILY"), ("Date", day))).Ok();
            int definitions = await _factory.QueryAsync(db => db.KpiMetricDefinition.CountAsync(d => !d.IsDeleted && d.IsActive && d.EffectiveFrom <= day && (d.EffectiveTo == null || d.EffectiveTo >= day)));
            second.GetProperty("rowsProcessed").GetInt64().Should().Be(first.GetProperty("rowsProcessed").GetInt64());
            var rows = await _factory.QueryAsync(db => db.KpiDailySnapshot.AsNoTracking().Where(x => x.SnapshotDate == day && x.BranchId == null).ToListAsync());
            rows.Should().HaveCount(definitions, "chạy lại không nhân đôi (upsert theo ngày, chi nhánh, mã, phiên bản)");
            rows.Select(r => (r.MetricCode, r.MetricVersion)).Should().OnlyHaveUniqueItems();

            // Dòng đã xóa mềm được dùng lại (UNIQUE gồm cả dòng xóa mềm).
            await _factory.SeedAsync(async db =>
            {
                var row = await db.KpiDailySnapshot.FirstAsync(x => x.SnapshotDate == day && x.MetricCode == KpiCode.WasteRate);
                row.IsDeleted = true;
            });
            (await c.PostFormAsync("/api/KpiSnapshot/Refresh", ("JobCode", "KPI_DAILY"), ("Date", day))).Ok();
            (await _factory.QueryAsync(db => db.KpiDailySnapshot.CountAsync(x => x.SnapshotDate == day && x.MetricCode == KpiCode.WasteRate))).Should().Be(1);
            (await _factory.QueryAsync(db => db.KpiDailySnapshot.CountAsync(x => x.SnapshotDate == day && x.MetricCode == KpiCode.WasteRate && !x.IsDeleted))).Should().Be(1);

            var logs = await _factory.QueryAsync(db => db.AnalyticsRefreshLog.AsNoTracking().Where(x => x.SourceWatermark == "MANUAL|2033-07-15").ToListAsync());
            logs.Should().HaveCount(3);
            logs.Should().OnlyContain(x => x.JobCode == KpiCode.JobDaily && x.RunStatus == "Succeeded" && x.CompletedAt != null && x.RowsProcessed == definitions);
            logs.Should().OnlyContain(x => x.CreatedBy == InMemoryApiFactory.UserId);

            // Catalog 225: nhật ký (lọc mã + trạng thái + ngày bắt đầu).
            var list = await c.GetResultAsync("/api/AnalyticsRefreshLog/GetList?JobCode=KPI_DAILY&Status=Succeeded&From=2033-12-20&To=2033-12-20&PageSize=2");
            list.Code.Should().Be(200);
            list.ListData.GetArrayLength().Should().Be(2, "trang 2 dòng");
            var item = list.ListData[0];
            item.GetProperty("trigger").GetString().Should().Be("MANUAL");
            item.GetProperty("runStatusName").GetString().Should().Be("Thành công");
            item.GetProperty("createdByName").GetString().Should().Be("test-admin");
            (await c.GetResultAsync("/api/AnalyticsRefreshLog/GetList?From=2033-12-20&To=2033-01-01")).Fail("Khoảng thời gian");
        }

        [Fact]
        public async Task Day_without_data_is_incomplete_with_null_value_and_met()
        {
            await SeedDefinitionsAsync();
            DateTime day = new(2033, 6, 3);
            using var c = _factory.CreateAuthorizedClient();
            var m = Metrics((await c.PostFormAsync("/api/KpiSnapshot/Refresh", ("JobCode", "KPI_DAILY"), ("Date", day))).Ok());
            m.Values.Should().OnlyContain(x => x.GetProperty("dataQualityStatus").GetString() == KpiCode.QualityIncomplete);
            m.Values.Should().OnlyContain(x => x.GetProperty("value").ValueKind == JsonValueKind.Null && x.GetProperty("met").ValueKind == JsonValueKind.Null);
            m[KpiCode.DelOnTime].GetProperty("note").GetString().Should().Contain("Chưa có đơn giao");
            var rows = await _factory.QueryAsync(db => db.KpiDailySnapshot.AsNoTracking().Where(x => x.SnapshotDate == day).ToListAsync());
            rows.Should().OnlyContain(x => x.DataQualityStatus == KpiCode.QualityIncomplete && x.MetricValue == null && x.CalculationVersion == KpiCode.CalculationVersion);

            // Mã không có công thức → INCOMPLETE "chưa có công thức".
            string code = "TST_" + Key();
            await _factory.SeedAsync(db =>
            {
                db.KpiMetricDefinition.Add(new KpiMetricDefinition { Id = Guid.NewGuid(), MetricCode = code, MetricName = "Không công thức", DomainCode = "ZZ", FormulaDescription = "?", VersionNo = 1, EffectiveFrom = day, EffectiveTo = day, IsActive = true });
                return Task.CompletedTask;
            });
            var again = Metrics((await c.PostFormAsync("/api/KpiSnapshot/Refresh", ("JobCode", "KPI_DAILY"), ("Date", day))).Ok());
            again[code].GetProperty("note").GetString().Should().Be(ConstantHelper.Message.KpiNoFormula);
        }

        [Fact]
        public async Task Refresh_and_views_validate_input()
        {
            using var c = _factory.CreateAuthorizedClient();
            (await c.PostFormAsync("/api/KpiSnapshot/Refresh", ("JobCode", "ABC"))).Fail("KPI_DAILY");
            (await c.PostFormAsync("/api/KpiSnapshot/Refresh", ("JobCode", "INVENTORY_BALANCE"))).Fail("INVENTORY_BALANCE");
            (await c.PostFormAsync("/api/KpiSnapshot/Refresh", ("JobCode", "KPI_DAILY"), ("Date", new DateTime(2033, 12, 21)))).Fail("tương lai");
            (await c.PostFormAsync("/api/KpiSnapshot/Refresh", ("JobCode", "KPI_MONTHLY"), ("Month", "2034-01"))).Fail("tương lai");
            (await c.PostFormAsync("/api/KpiSnapshot/Refresh", ("JobCode", "KPI_MONTHLY"), ("Month", "2033-13"))).Fail("yyyy-MM");
            (await c.PostFormAsync("/api/KpiSnapshot/Refresh", ("JobCode", "KPI_DAILY"), ("BranchId", Guid.NewGuid()))).Fail("chi nhánh");
            (await c.GetResultAsync("/api/KpiSnapshot/GetDaily?From=2033-05-10&To=2033-05-01")).Fail("Khoảng thời gian");
            (await c.GetResultAsync("/api/KpiSnapshot/GetMonthly?FromMonth=2030-01&ToMonth=2033-12")).Fail("36 tháng");
            (await c.GetResultAsync("/api/KpiSnapshot/GetMonthly?FromMonth=2033-5x")).Fail("yyyy-MM");
            var defaults = (await c.GetResultAsync("/api/KpiSnapshot/GetMonthly")).Ok();
            defaults.GetProperty("from").GetDateTime().Should().Be(new DateTime(2033, 1, 1), "trống = 12 tháng gần nhất");
            defaults.GetProperty("to").GetDateTime().Should().Be(new DateTime(2033, 12, 1));
        }

        [Fact]
        public async Task Refresh_is_admin_only_and_views_need_screen_permission()
        {
            Guid viewer = await CommonTestUsers.UserAsync(_factory, ("kpi-daily", "V"), ("kpi-refresh", "VAE"));
            using var user = _factory.CreateAuthorizedClient(viewer);
            (await user.PostAsync("/api/KpiSnapshot/Refresh", LegacyHttp.Form(("JobCode", "KPI_DAILY")))).StatusCode.Should().Be(HttpStatusCode.Forbidden, "chỉ admin bấm làm mới");
            (await user.GetAsync("/api/KpiSnapshot/GetDaily")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await user.GetAsync("/api/KpiSnapshot/GetMonthly")).StatusCode.Should().Be(HttpStatusCode.Forbidden, "không có quyền Xem #/kpi-monthly");
            (await user.GetAsync("/api/AnalyticsRefreshLog/GetList")).StatusCode.Should().Be(HttpStatusCode.OK, "Xem #/kpi-refresh");
            (await user.PostAsync("/api/KpiMetricDefinition/Insert", LegacyHttp.Form(("MetricCode", "X")))).StatusCode.Should().Be(HttpStatusCode.Forbidden, "không có quyền Thêm #/kpi-definition");
        }
    }

    /// <summary>
    /// BE3.4 trên SQL Server dev (bỏ qua nếu không có DB): KpiSnapshot/Refresh khóa sp_getapplock theo jobCode (DELICARE_JOB_KPI_DAILY – dùng chung với job),
    /// ghi AnalyticsRefreshLog Running → Succeeded / Locked, upsert không nhân đôi. Dùng 1 chỉ tiêu thử riêng (mã có tag) hiệu lực đúng 1 ngày năm 2000
    /// → chỉ đụng dòng của test; cuối test xóa đúng các dòng đó (theo mã / RunId).
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class KpiRefreshSqlServerTests(ApiFactory factory)
    {
        [SkippableFact]
        public async Task Refresh_locks_logs_and_upserts_on_sql_server()
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);
            using var scope = factory.Services.CreateScope();
            string connectionString = scope.ServiceProvider.GetRequiredService<IConfiguration>().GetConnectionString(ConstantHelper.MainConnectionString)!;
            string code = "ZZTEST_KPI_" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
            DateTime day = new(2000, 1, 3);
            var runIds = new List<Guid>();
            await using var db = new SqlConnection(connectionString);
            await db.OpenAsync();
            Guid admin = await db.ExecuteScalarAsync<Guid>("SELECT TOP (1) Id FROM dbo.[User] WHERE UserName = N'admin' AND IsDeleted = 0");
            await db.ExecuteAsync(
                "INSERT INTO dbo.KpiMetricDefinition (Id, MetricCode, MetricName, DomainCode, UnitCode, FormulaDescription, AggregationCode, VersionNo, EffectiveFrom, EffectiveTo, IsActive, CreatedDate, CreatedBy, IsDeleted)"
                + " VALUES (NEWID(), @Code, N'[DEMO] chỉ tiêu thử của test', N'ZZ', N'%', N'Không có công thức [TARGET >= 50]', N'RATIO', 1, @Day, @Day, 1, SYSDATETIME(), @Admin, 0);",
                new { Code = code, Day = day, Admin = admin });
            try
            {
                using var c = factory.CreateAuthorizedClient();
                for (int i = 0; i < 2; i++)
                {
                    var data = (await c.PostFormAsync("/api/KpiSnapshot/Refresh", ("JobCode", "KPI_DAILY"), ("Date", day))).Ok();
                    runIds.Add(data.GetProperty("runId").GetGuid());
                    var metric = data.GetProperty("metrics").EnumerateArray().Single(m => m.GetProperty("metricCode").GetString() == code);
                    metric.GetProperty("dataQualityStatus").GetString().Should().Be(KpiCode.QualityIncomplete);
                }
                (await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.KpiDailySnapshot WHERE MetricCode = @Code", new { Code = code })).Should().Be(1, "chạy lại không nhân đôi");
                var logs = (await db.QueryAsync<(string RunStatus, string SourceWatermark, long RowsProcessed)>(
                    "SELECT RunStatus, SourceWatermark, ISNULL(RowsProcessed, 0) FROM dbo.AnalyticsRefreshLog WHERE RunId IN @Ids", new { Ids = runIds })).ToList();
                logs.Should().HaveCount(2).And.OnlyContain(l => l.RunStatus == "Succeeded" && l.SourceWatermark == "MANUAL|2000-01-03" && l.RowsProcessed >= 1);

                // Kết nối khác đang giữ khóa KPI_DAILY (vd. job đang chạy) → 400, ghi 1 dòng Locked, không tính.
                await using (var holder = new SqlConnection(connectionString))
                {
                    await holder.OpenAsync();
                    (await holder.ExecuteScalarAsync<int>(
                        "DECLARE @r int; EXEC @r = sp_getapplock @Resource = N'DELICARE_JOB_KPI_DAILY', @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 5000; SELECT @r;"))
                        .Should().BeGreaterThanOrEqualTo(0);
                    (await c.PostFormAsync("/api/KpiSnapshot/Refresh", ("JobCode", "KPI_DAILY"), ("Date", day))).Fail("Đang có lần làm mới");
                }
                var locked = (await db.QueryAsync<Guid>(
                    "SELECT RunId FROM dbo.AnalyticsRefreshLog WHERE JobCode = N'KPI_DAILY' AND RunStatus = N'Locked' AND SourceWatermark = N'MANUAL|2000-01-03'")).ToList();
                locked.Should().NotBeEmpty();
                runIds.AddRange(locked);
            }
            finally
            {
                // Chỉ xóa dòng của test: snapshot + định nghĩa theo mã có tag, nhật ký theo RunId của test.
                await db.ExecuteAsync("DELETE FROM dbo.KpiDailySnapshot WHERE MetricCode = @Code", new { Code = code });
                await db.ExecuteAsync("DELETE FROM dbo.KpiMetricDefinition WHERE MetricCode = @Code", new { Code = code });
                if (runIds.Count > 0)
                {
                    await db.ExecuteAsync("DELETE FROM dbo.AnalyticsRefreshLog WHERE RunId IN @Ids", new { Ids = runIds });
                }
            }
        }
    }
}
