using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Option;
using AppDelicare.Services.Implement;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Production
{
    /// <summary>
    /// BE2.5 – ATTP, vệ sinh, hộp / khay (Catalog 161, 162, 167–170, 190, 194–196). Đồng hồ test = 21/12/2026 10:00.
    /// <para>Bộ số tính tay: bàn giao 60 suất, khách nhận 58 → sổ bao bì OUT 58 khay (1 khay / suất, loại = loại của kết quả đóng gói "KHAY-T{code}");
    /// thu hồi 55 (hỏng 3) → IN 52 tốt + 3 hỏng, còn ở khách 3; kiểm tra hỏng 4 → dùng được 51 → mẻ vệ sinh 51 khay; ghi thất thoát 2 → còn 1,
    /// tỷ lệ thất thoát 2 ÷ 58 = 3,45% &gt; ngưỡng riêng của khách 2% (Customer.ExternalData).</para>
    /// </summary>
    public class FoodSafetyPackagingTests : IClassFixture<InMemoryApiFactory>
    {
        private static readonly DateTime Day = new(2026, 12, 21);
        private readonly InMemoryApiFactory _factory;

        public FoodSafetyPackagingTests(InMemoryApiFactory factory)
        {
            _factory = factory;
            _factory.Clock.FixedNow = Day.AddHours(10);
        }

        private static async Task<LegacyResult> PostJson(HttpClient c, string url, object body) => await LegacyResult.ReadAsync(await c.PostAsJsonAsync(url, body));

        private static IEnumerable<string?> Warnings(JsonElement data) => data.GetProperty("listWarning").EnumerateArray().Select(w => w.GetString());

        /// <summary>Bộ checklist + tiêu chí (mã bộ riêng theo test, trừ bộ cần mã cố định như PREP / VS-KHAY).</summary>
        private static Guid AddChecklist(Data.Connection.DatabaseContext db, string code, params (string Code, string Name, bool Critical, bool Required)[] items)
        {
            Guid id = Guid.NewGuid();
            db.FoodSafetyChecklist.Add(new FoodSafetyChecklist { Id = id, Code = code, Name = "Bộ " + code, VersionNo = 1, Status = "Active", EffectiveFrom = Day.AddDays(-30) });
            int i = 0;
            foreach (var (itemCode, name, critical, required) in items)
            {
                db.FoodSafetyChecklistItem.Add(new FoodSafetyChecklistItem
                {
                    Id = Guid.NewGuid(), ChecklistId = id, ItemCode = itemCode, ItemName = name, SortOrder = ++i, IsCritical = critical, IsRequired = required,
                });
            }
            return id;
        }

        private sealed record BatchScenario(Guid Shift, Guid Batch, Guid Checklist, string BatchNumber);

        /// <summary>Mẻ Đạt QC (nhiệt độ lõi 75 °C, đã ghi sản lượng) của lệnh SX ca <paramref name="code"/> + bộ kiểm soát thành phẩm 3 tiêu chí (2 trọng yếu).</summary>
        private async Task<BatchScenario> SeedBatchAsync(string code, string status = "Passed")
        {
            Guid shift = Guid.NewGuid(), plan = Guid.NewGuid(), order = Guid.NewGuid(), batch = Guid.NewGuid(), dish = Guid.NewGuid(), checklist = Guid.Empty;
            string number = $"ME-261221-{code}-01";
            await _factory.SeedAsync(db =>
            {
                db.Shift.Add(new Shift { Id = shift, Code = "S" + code, Name = "Trưa " + code, StartTime = new TimeSpan(10, 30, 0), EndTime = new TimeSpan(13, 30, 0), Status = "Active" });
                db.Dish.Add(new Dish { Id = dish, Code = "M" + code, Name = "Cơm gà " + code, Status = "Active" });
                db.ProductionPlan.Add(new ProductionPlan { Id = plan, PlanNumber = "KH-" + code, PlanNo = "KH-" + code, ProductionDate = Day, PlanDate = Day.AddDays(-1), Status = "Released" });
                db.ProductionOrder.Add(new ProductionOrder { Id = order, ProductionPlanId = plan, DishId = dish, RecipeId = Guid.NewGuid(), ShiftId = shift, PlannedQty = 100, Status = "InProgress", ProductionOrderNumber = "PH-SX" + code });
                db.ProductionBatch.Add(new ProductionBatch
                {
                    Id = batch, ProductionOrderId = order, DishId = dish, BatchNumber = number, BatchNo = number, StartDate = Day.AddHours(7), PlannedQuantity = 100,
                    Status = status, QcStatus = status == "Passed" || status == "Packed" ? "PASS" : null,
                });
                db.TemperatureLog.Add(new TemperatureLog { Id = Guid.NewGuid(), ProductionBatchId = batch, MeasurementType = "CORE", TemperatureC = 75, MinAllowedC = 70, IsWithinRange = true, MeasuredAt = Day.AddHours(8), SourceChannel = "MANUAL" });
                db.ProductionOutput.Add(new ProductionOutput { Id = Guid.NewGuid(), ProductionBatchId = batch, DishId = dish, ProducedQuantity = 100, AcceptedQuantity = 100, QualityStatus = "PASS" });
                checklist = AddChecklist(db, "FG" + code,
                    ("CAM_QUAN", "Cảm quan", true, true), ("TRONG_LUONG_SUAT", "Trọng lượng suất", true, true), ("LUU_MAU_KY", "Lưu mẫu + người ký", false, true));
                return Task.CompletedTask;
            });
            return new BatchScenario(shift, batch, checklist, number);
        }

        [Fact]
        public async Task Checklist_run_record_and_critical_failure_hold_the_batch_until_rechecked()
        {
            var s = await SeedBatchAsync("C1");
            using var c = _factory.CreateAuthorizedClient();

            // Lần chạy chưa có kết quả: 3 tiêu chí theo SortOrder, ca lấy theo lệnh SX của mẻ.
            var run = (await c.GetResultAsync($"/api/FoodSafetyChecklistResult/GetRun?ChecklistId={s.Checklist}&ProductionBatchId={s.Batch}")).Ok();
            run.GetProperty("shiftId").GetGuid().Should().Be(s.Shift);
            run.GetProperty("checkDate").GetDateTime().Should().Be(Day);
            run.GetProperty("batchNumber").GetString().Should().Be(s.BatchNumber);
            run.GetProperty("listItem").EnumerateArray().Select(x => x.GetProperty("item").GetProperty("itemCode").GetString()).Should().Equal("CAM_QUAN", "TRONG_LUONG_SUAT", "LUU_MAU_KY");
            run.GetProperty("isComplete").GetBoolean().Should().BeFalse();

            // Chốt: thiếu tiêu chí bắt buộc; FAIL không có khắc phục; kết quả sai mã; bộ công đoạn / lưu mẫu ghi qua thao tác riêng.
            (await PostJson(c, "/api/FoodSafetyChecklistResult/Record", new { checklistId = s.Checklist, productionBatchId = s.Batch,
                listItem = new[] { new { itemCode = "CAM_QUAN", resultCode = "PASS" } } })).Fail("chưa có kết quả: Trọng lượng suất, Lưu mẫu + người ký");
            (await PostJson(c, "/api/FoodSafetyChecklistResult/Record", new { checklistId = s.Checklist, productionBatchId = s.Batch, listItem = new object[] {
                new { itemCode = "CAM_QUAN", resultCode = "FAIL" }, new { itemCode = "TRONG_LUONG_SUAT", resultCode = "PASS" }, new { itemCode = "LUU_MAU_KY", resultCode = "PASS" } } }))
                .Fail("bắt buộc ghi hành động khắc phục");
            (await PostJson(c, "/api/FoodSafetyChecklistResult/Record", new { checklistId = s.Checklist, listItem = new[] { new { itemCode = "CAM_QUAN", resultCode = "MAYBE" } } }))
                .Fail("phải là PASS hoặc FAIL");
            Guid stage = await _factory.QueryAsync(db => db.FoodSafetyChecklist.Where(x => x.Code == "PRD_STAGE").Select(x => x.Id).FirstOrDefaultAsync());
            if (stage == Guid.Empty)
            {
                await _factory.SeedAsync(db => { stage = AddChecklist(db, "PRD_STAGE", ("SO_CHE", "Sơ chế", false, true)); return Task.CompletedTask; });
            }
            (await PostJson(c, "/api/FoodSafetyChecklistResult/Record", new { checklistId = stage, productionBatchId = s.Batch,
                listItem = new[] { new { itemCode = "SO_CHE", resultCode = "PASS" } } })).Fail("ProductionBatch/StartStage");

            // Cảm quan KHÔNG ĐẠT (trọng yếu) → mẻ Đạt → Chờ QC, tạm giữ; trả criticalFailures; cảnh báo Telegram để R2.
            var recorded = (await PostJson(c, "/api/FoodSafetyChecklistResult/Record", new { checklistId = s.Checklist, productionBatchId = s.Batch, listItem = new object[] {
                new { itemCode = "CAM_QUAN", resultCode = "FAIL", actualValue = "Có mùi lạ", correctiveAction = "Tách mẻ, báo bếp trưởng nếm lại" },
                new { itemCode = "TRONG_LUONG_SUAT", resultCode = "PASS", actualValue = "352 g" },
                new { itemCode = "LUU_MAU_KY", resultCode = "PASS" } } })).Ok();
            recorded.GetProperty("saved").GetInt32().Should().Be(3);
            var failure = recorded.GetProperty("listCriticalFailure").EnumerateArray().Should().ContainSingle().Subject;
            failure.GetProperty("itemCode").GetString().Should().Be("CAM_QUAN");
            failure.GetProperty("batchAction").GetString().Should().Be("Đạt → Chờ QC, tạm giữ");
            Warnings(recorded).Should().Contain(w => w!.StartsWith($"Mẻ {s.BatchNumber}: tiêu chí trọng yếu KHÔNG ĐẠT (Cảm quan)"));
            Warnings(recorded).Should().Contain(w => w!.Contains("Release 2"));
            recorded.GetProperty("run").GetProperty("listCriticalFailure").EnumerateArray().Select(x => x.GetString()).Should().Equal("Cảm quan");
            var held = await _factory.QueryAsync(db => db.ProductionBatch.AsNoTracking().SingleAsync(b => b.Id == s.Batch));
            (held.Status, held.QcStatus).Should().Be(("QCPending", "HOLD"));
            (await _factory.QueryAsync(db => db.ProductionOutput.AsNoTracking().SingleAsync(o => o.ProductionBatchId == s.Batch))).QualityStatus.Should().Be("HOLD");
            var stored = await _factory.QueryAsync(db => db.FoodSafetyChecklistResult.AsNoTracking().Where(r => r.ChecklistId == s.Checklist).ToListAsync());
            stored.Should().HaveCount(3).And.OnlyContain(r => r.ShiftId == s.Shift && r.CheckDate == Day && r.CheckedBy == InMemoryApiFactory.UserId && r.ProductionBatchId == s.Batch);

            // Đạt QC / đóng gói bị chặn tới khi kiểm lại ĐẠT.
            (await c.PostFormAsync("/api/ProductionBatch/Release", ("Id", s.Batch))).Fail("Tiêu chí ATTP trọng yếu chưa đạt (kiểm lại sau khắc phục): Cảm quan");

            // Kiểm lại cùng giây: chỉ ghi tiêu chí đã sửa (2 tiêu chí kia đã có kết quả) → dòng mới (giữ lịch sử), kết quả hiện tại ĐẠT.
            var recheck = (await PostJson(c, "/api/FoodSafetyChecklistResult/Record", new { checklistId = s.Checklist, productionBatchId = s.Batch,
                listItem = new[] { new { itemCode = "CAM_QUAN", resultCode = "PASS", actualValue = "Nếm lại đạt" } } })).Ok();
            recheck.GetProperty("listCriticalFailure").GetArrayLength().Should().Be(0);
            var camQuan = recheck.GetProperty("run").GetProperty("listItem")[0];
            camQuan.GetProperty("result").GetProperty("resultCode").GetString().Should().Be("PASS");
            camQuan.GetProperty("historyCount").GetInt32().Should().Be(2);
            recheck.GetProperty("run").GetProperty("isComplete").GetBoolean().Should().BeTrue();
            var rate = recheck.GetProperty("run").GetProperty("listPassRate7d").EnumerateArray().ToList();
            rate.Should().HaveCount(7);
            (rate[6].GetProperty("total").GetInt32(), rate[6].GetProperty("pass").GetInt32(), rate[6].GetProperty("rate").GetDecimal()).Should().Be((3, 3, 100m));
            rate[0].GetProperty("rate").ValueKind.Should().Be(JsonValueKind.Null);
            (await c.PostFormAsync("/api/ProductionBatch/Release", ("Id", s.Batch))).Ok().GetProperty("batch").GetProperty("status").GetString().Should().Be("Passed");

            // CRUD kết quả checklist bị chặn (bằng chứng ATTP).
            (await c.PostFormAsync("/api/FoodSafetyChecklistResult/Insert", ("ChecklistId", s.Checklist), ("ChecklistItemId", Guid.NewGuid()), ("CheckDate", Day), ("ResultCode", "PASS")))
                .Fail("FoodSafetyChecklistResult/Record");
        }

        [Fact]
        public async Task Shift_checklist_due_time_follows_kitchen_dispatch_offset_and_critical_fail_on_packed_batch_blocks_dispatch()
        {
            var s = await SeedBatchAsync("C2", status: "Packed");
            Guid prep = Guid.Empty;
            await _factory.SeedAsync(db =>
            {
                prep = AddChecklist(db, "PREP", ("NGUYEN_LIEU_DUNG_LO", "Nguyên liệu đúng lô", true, true), ("RA_DONG", "Rã đông đúng cách", false, false));
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();

            // Hạn = giờ xuất bếp (ca 10:30, không có đơn → giờ bắt đầu ca − 60′ = 09:30) − 150′ = 07:00; 10:00 chưa làm → quá hạn.
            var run = (await c.GetResultAsync($"/api/FoodSafetyChecklistResult/GetRun?ChecklistId={prep}&ShiftId={s.Shift}&CheckDate=2026-12-21")).Ok();
            run.GetProperty("dueAt").GetDateTime().Should().Be(Day.AddHours(7));
            run.GetProperty("isOverdue").GetBoolean().Should().BeTrue();
            run.GetProperty("requiredCount").GetInt32().Should().Be(1);
            // Tiêu chí không bắt buộc không cần ghi; FAIL trọng yếu không gắn mẻ → chỉ cảnh báo QA.
            var noBatch = (await PostJson(c, "/api/FoodSafetyChecklistResult/Record", new { checklistId = prep, shiftId = s.Shift,
                listItem = new[] { new { itemCode = "NGUYEN_LIEU_DUNG_LO", resultCode = "FAIL", correctiveAction = "Đổi lô gà còn hạn" } } })).Ok();
            Warnings(noBatch).Should().Contain(w => w!.StartsWith("Tiêu chí trọng yếu KHÔNG ĐẠT: Nguyên liệu đúng lô"));
            noBatch.GetProperty("run").GetProperty("isOverdue").GetBoolean().Should().BeFalse("đã đủ tiêu chí bắt buộc");

            // Mẻ Đã đóng gói: tiêu chí trọng yếu FAIL → không đổi trạng thái mẻ, chặn xuất chuyến (DeliveryRules.ProductionBlockers) tới khi kiểm lại ĐẠT.
            var packed = (await PostJson(c, "/api/FoodSafetyChecklistResult/Record", new { checklistId = s.Checklist, productionBatchId = s.Batch, listItem = new object[] {
                new { itemCode = "CAM_QUAN", resultCode = "PASS" }, new { itemCode = "TRONG_LUONG_SUAT", resultCode = "FAIL", actualValue = "310 g", correctiveAction = "Chia lại suất" },
                new { itemCode = "LUU_MAU_KY", resultCode = "PASS" } } })).Ok();
            packed.GetProperty("listCriticalFailure")[0].GetProperty("batchAction").GetString().Should().Be("Đã đóng gói – chặn xuất chuyến");
            (await _factory.QueryAsync(db => db.ProductionBatch.AsNoTracking().SingleAsync(b => b.Id == s.Batch))).Status.Should().Be("Packed");
            var rules = new BusinessRulesOptions();
            var blockers = await _factory.QueryAsync(async db => DeliveryRules.ProductionBlockers(await DeliveryRules.BatchesAsync(db, Day, [s.Shift]), rules));
            blockers.Should().Contain($"Mẻ có tiêu chí ATTP trọng yếu chưa đạt: {s.BatchNumber} (Trọng lượng suất)");
            (await PostJson(c, "/api/FoodSafetyChecklistResult/Record", new { checklistId = s.Checklist, productionBatchId = s.Batch,
                listItem = new[] { new { itemCode = "TRONG_LUONG_SUAT", resultCode = "PASS", actualValue = "350 g" } } })).Ok();
            blockers = await _factory.QueryAsync(async db => DeliveryRules.ProductionBlockers(await DeliveryRules.BatchesAsync(db, Day, [s.Shift]), rules));
            blockers.Should().NotContain(b => b.Contains("trọng yếu"));
        }

        private sealed record DeliveryScenario(Guid Customer, Guid Order, Guid DeliveryOrder, Guid Stop, string DeliveryNumber, string Type, Guid Employee);

        /// <summary>Chuyến Đang giao 1 điểm (đơn 60 suất), kết quả đóng gói của đơn ghi loại khay; khách có ngưỡng thất thoát riêng 2%.</summary>
        private async Task<DeliveryScenario> SeedDeliveryAsync(string code)
        {
            Guid customer = Guid.NewGuid(), location = Guid.NewGuid(), order = Guid.NewGuid(), route = Guid.NewGuid(), deliveryOrder = Guid.NewGuid(), stop = Guid.NewGuid();
            Guid packing = Guid.NewGuid(), employee = Guid.NewGuid();
            string number = "PH-261221-9" + code[^3..], type = "KHAY-T" + code;
            await _factory.SeedAsync(db =>
            {
                db.Customer.Add(new Customer { Id = customer, Code = "K" + code, Name = "[DEMO] Khách " + code, CIF = "K" + code, CustomerType = "FACTORY", DisplayName = "Khách " + code,
                    Status = "Active", ExternalData = "{\"containerLossMaxPct\": 2}" });
                db.CustomerDeliveryLocation.Add(new CustomerDeliveryLocation { Id = location, CustomerId = customer, Code = "D" + code, Name = "Căng tin " + code, DeliveryWindowFrom = new TimeSpan(11, 0, 0) });
                db.Order.Add(new Order { Id = order, OrderNumber = "PH-O" + code, OrderNo = "PH-O" + code, CustomerId = customer, DeliveryLocationId = location, OrderDate = Day.AddDays(-2),
                    MealDate = Day, DeliveryDate = Day, Status = "InProduction", TotalQuantity = 60, TotalAmount = 1_800_000 });
                db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = order, MealType = "TRUA", Quantity = 60, UnitPrice = 30_000, LineAmount = 1_800_000 });
                db.PackingOrder.Add(new PackingOrder { Id = packing, ProductionBatchId = Guid.NewGuid(), OrderId = order, PlannedQty = 60, Status = "Packed", PackingOrderNumber = "PH-P" + code, PackingDate = Day });
                db.PackingResult.Add(new PackingResult { Id = Guid.NewGuid(), PackingOrderId = packing, PlannedQty = 60, PackedQuantity = 60, AcceptedQty = 60, PackagingType = type });
                db.DeliveryRoute.Add(new DeliveryRoute { Id = route, RouteNo = "CX-261221-X" + code + "-01", Code = "CX" + code, DeliveryDate = Day, Status = "InTransit", ActualStart = Day.AddHours(9) });
                db.DeliveryOrder.Add(new DeliveryOrder { Id = deliveryOrder, OrderId = order, CustomerId = customer, CustomerDeliveryLocationId = location, DeliveryLocationId = location,
                    DeliveryRouteId = route, DeliveryNumber = number, DeliveryNo = number, DeliveryDate = Day, PlannedDeliveryTime = Day.AddHours(11), Status = "InTransit" });
                db.DeliveryStop.Add(new DeliveryStop { Id = stop, DeliveryRouteId = route, DeliveryOrderId = deliveryOrder, CustomerId = customer, DeliveryLocationId = location,
                    SequenceNo = 1, PlannedArrival = Day.AddHours(11), PlannedMealQty = 60, StopStatus = "Planned" });
                db.Employee.Add(new Employee { Id = employee, Code = "NV" + code, FullName = "Nhân viên rửa " + code, Status = "Active" });
                if (!db.FoodSafetyChecklist.Any(x => x.Code == "VS-KHAY"))
                {
                    AddChecklist(db, "VS-KHAY", ("NHAN_QR", "1. Nhận / quét QR", false, true), ("PHAN_LOAI", "2. Phân loại", false, true), ("RUA_SO", "3. Rửa sơ", false, true),
                        ("RUA_MAY", "4. Rửa máy", false, true), ("KHU_KHUAN", "5. Khử khuẩn", true, true), ("KIEM_TRA_NHAP_KHO", "6. Kiểm tra và nhập kho sạch", true, true));
                }
                return Task.CompletedTask;
            });
            return new DeliveryScenario(customer, order, deliveryOrder, stop, number, type, employee);
        }

        private static object Steps(params (string Code, string Result, string? Chemical, string? Corrective)[] steps)
            => steps.Select(x => new { itemCode = x.Code, resultCode = x.Result, chemicalUsed = x.Chemical, correctiveAction = x.Corrective }).ToArray();

        [Fact]
        public async Task Delivery_out_return_inspection_cleaning_six_steps_and_loss_rate_follow_hand_checked_numbers()
        {
            var s = await SeedDeliveryAsync("P1A");
            using var c = _factory.CreateAuthorizedClient();

            // Bàn giao 60 / nhận 58 (63 °C) → sổ bao bì OUT 58 khay loại của kết quả đóng gói + phiếu thu hồi Chờ thu hồi.
            var delivered = (await c.PostFormAsync("/api/DeliveryStop/Deliver", ("Id", s.Stop), ("TemperatureC", 63m), ("DeliveredQuantity", 60m), ("AcceptedQuantity", 58m),
                ("ReceiverName", "Chị Hoa"))).Ok();
            Warnings(delivered).Should().Contain($"Sổ bao bì: giao 58 {s.Type} cho khách (OUT), chờ thu hồi");
            var outRow = await _factory.QueryAsync(db => db.PackagingLedger.AsNoTracking().SingleAsync(l => l.CustomerId == s.Customer));
            (outRow.TransactionType, outRow.PackagingType, outRow.Quantity, outRow.ReferenceNo, outRow.OrderId, outRow.RecordedBy)
                .Should().Be(("OUT", s.Type, 58m, s.DeliveryNumber, s.Order, InMemoryApiFactory.UserId));
            var expected = await _factory.QueryAsync(db => db.ContainerReturn.AsNoTracking().SingleAsync(r => r.DeliveryOrderId == s.DeliveryOrder));
            (expected.Status, expected.ExpectedQuantity, expected.CustomerId).Should().Be(("Expected", 58, (Guid?)s.Customer));
            var list = (await c.GetResultAsync($"/api/ContainerReturn/GetList?CustomerId={s.Customer}")).Ok();
            list.GetProperty("statusCounts").GetProperty("Expected").GetInt32().Should().Be(1);
            list.GetProperty("listItem")[0].GetProperty("packagingType").GetString().Should().Be(s.Type);

            // Thu hồi: vượt số còn ở khách → 400; hỏng > trả về → 400; 55 (hỏng 3) → IN 52 tốt + 3 hỏng, phiếu Đã thu hồi, còn 3 ở khách.
            (await c.PostFormAsync("/api/ContainerReturn/Record", ("DeliveryOrderId", s.DeliveryOrder), ("ReturnedQuantity", 60))).Fail($"chỉ còn 58 {s.Type}");
            (await c.PostFormAsync("/api/ContainerReturn/Record", ("DeliveryOrderId", s.DeliveryOrder), ("ReturnedQuantity", 5), ("DamagedQuantity", 6))).Fail("0 ≤ hỏng ≤ trả về");
            var returned = (await c.PostFormAsync("/api/ContainerReturn/Record", ("DeliveryOrderId", s.DeliveryOrder), ("ReturnedQuantity", 55), ("DamagedQuantity", 3),
                ("Note", "Thu cuối ca"))).Ok();
            Guid returnId = returned.GetProperty("return").GetProperty("id").GetGuid();
            returnId.Should().Be(expected.Id, "phiếu Chờ thu hồi được cập nhật, không lập phiếu mới");
            returned.GetProperty("return").GetProperty("status").GetString().Should().Be("Returned");
            returned.GetProperty("outstandingOfDelivery").GetDecimal().Should().Be(3);
            returned.GetProperty("allowedActions").EnumerateArray().Select(a => a.GetString()).Should().Equal("inspect");
            Warnings(returned).Should().Contain($"Còn 3 {s.Type} của đơn giao {s.DeliveryNumber} ở khách – thu hồi tiếp hoặc ghi thất thoát (PackagingLedger/Record LOSS)");
            var ins = await _factory.QueryAsync(db => db.PackagingLedger.AsNoTracking().Where(l => l.CustomerId == s.Customer && l.TransactionType == "IN")
                .OrderBy(l => l.Quantity).Select(l => new { l.Quantity, l.ConditionCode }).ToListAsync());
            ins.Select(x => (x.Quantity, x.ConditionCode)).Should().Equal((3m, "DAMAGED"), (52m, "GOOD"));

            // Kiểm tra: kết quả khác OK phải ghi chú; hỏng 4 (thêm 1 khi kiểm) → dùng được 51 → tự lập mẻ vệ sinh VS-261221-<ca> 51 khay.
            (await c.PostFormAsync("/api/ContainerReturn/Inspect", ("Id", returnId), ("InspectionResult", "DAMAGED"), ("DamagedQty", 4m))).Fail("phải ghi chú");
            (await c.PostFormAsync("/api/ContainerReturn/Inspect", ("Id", returnId), ("InspectionResult", "BROKEN"), ("Note", "x"))).Fail("OK, SHORTAGE, DAMAGED, DIRTY, REJECTED");
            var inspected = (await c.PostFormAsync("/api/ContainerReturn/Inspect", ("Id", returnId), ("InspectionResult", "DAMAGED"), ("DamagedQty", 4m),
                ("InspectorEmployeeId", s.Employee), ("ResponsibleEmployeeId", s.Employee), ("Note", "4 khay móp, thiếu 3"))).Ok();
            inspected.GetProperty("return").GetProperty("status").GetString().Should().Be("Inspected");
            var inspection = inspected.GetProperty("listInspection").EnumerateArray().Should().ContainSingle().Subject;
            (inspection.GetProperty("damagedQty").GetDecimal(), inspection.GetProperty("missingQty").GetDecimal()).Should().Be((4m, 3m));
            Guid inspectionId = inspection.GetProperty("id").GetGuid();
            var cleaning = inspected.GetProperty("listCleaningBatch").EnumerateArray().Should().ContainSingle().Subject;
            cleaning.GetProperty("containerQuantity").GetInt32().Should().Be(51);
            cleaning.GetProperty("cleaningBatchNumber").GetString().Should().MatchRegex(@"^VS-261221-[A-Z0-9_]+$");
            Guid batch1 = cleaning.GetProperty("id").GetGuid();
            (await c.PostFormAsync("/api/ContainerReturn/Inspect", ("Id", returnId), ("InspectionResult", "OK"))).Fail("Đã thu hồi");
            (await c.PostFormAsync("/api/CleaningBatch/Create", ("ReturnInspectionId", inspectionId))).Fail("không còn khay / hộp dùng được");

            // Vệ sinh đi lần lượt: bước 3 trước bước 1, 2 → 400; khử khuẩn ĐẠT thiếu hóa chất → 400.
            (await PostJson(c, "/api/CleaningBatch/RecordResults", new { id = batch1, listItem = Steps(("RUA_SO", "PASS", null, null)) })).Fail("bước trước 1. Nhận / quét QR chưa ĐẠT");
            var four = (await PostJson(c, "/api/CleaningBatch/RecordResults", new { id = batch1, listItem = Steps(("NHAN_QR", "PASS", null, null), ("PHAN_LOAI", "PASS", null, null),
                ("RUA_SO", "PASS", null, null), ("RUA_MAY", "PASS", null, null)) })).Ok();
            four.GetProperty("passedStepCount").GetInt32().Should().Be(4);
            (await PostJson(c, "/api/CleaningBatch/RecordResults", new { id = batch1, listItem = Steps(("KHU_KHUAN", "PASS", null, null)) })).Fail("phải ghi hóa chất");
            (await PostJson(c, "/api/CleaningBatch/RecordResults", new { id = batch1, listItem = Steps(("KIEM_TRA_NHAP_KHO", "PASS", null, null)) })).Fail("bước trước 5. Khử khuẩn chưa ĐẠT");
            var failed = (await PostJson(c, "/api/CleaningBatch/RecordResults", new { id = batch1, listItem = Steps(("KHU_KHUAN", "FAIL", null, "Pha lại dung dịch Chlorine 200 ppm")) })).Ok();
            Warnings(failed).Should().Contain(w => w!.Contains("5. Khử khuẩn KHÔNG ĐẠT"));
            (await c.PostFormAsync("/api/CleaningBatch/Complete", ("Id", batch1), ("QcResult", "PASS"))).Fail("chưa ĐẠT đủ các bước: 5. Khử khuẩn, 6. Kiểm tra và nhập kho sạch");

            // QC loại mẻ (lý do bắt buộc) → Hoàn tất / REJECT, dòng KHÔNG ĐẠT ở bước cuối; 51 khay chờ vệ sinh lại → mẻ mới VS-…-2.
            (await c.PostFormAsync("/api/CleaningBatch/Complete", ("Id", batch1), ("QcResult", "REJECT"))).Fail("phải ghi lý do");
            var rejected = (await c.PostFormAsync("/api/CleaningBatch/Complete", ("Id", batch1), ("QcResult", "REJECT"), ("Note", "Còn vết dầu, rửa lại"))).Ok();
            (rejected.GetProperty("batch").GetProperty("status").GetString(), rejected.GetProperty("batch").GetProperty("qcResult").GetString()).Should().Be(("Completed", "REJECT"));
            rejected.GetProperty("listStep")[5].GetProperty("result").GetProperty("correctiveAction").GetString().Should().Be("Còn vết dầu, rửa lại");
            (await PostJson(c, "/api/CleaningBatch/RecordResults", new { id = batch1, listItem = Steps(("NHAN_QR", "PASS", null, null)) })).Fail("Đang vệ sinh");
            var second = (await c.PostFormAsync("/api/CleaningBatch/Create", ("ReturnInspectionId", inspectionId), ("ResponsibleEmployeeId", s.Employee))).Ok();
            second.GetProperty("batch").GetProperty("containerQuantity").GetInt32().Should().Be(51);
            second.GetProperty("batch").GetProperty("cleaningBatchNumber").GetString().Should().Be(cleaning.GetProperty("cleaningBatchNumber").GetString() + "-2");
            Guid batch2 = second.GetProperty("batch").GetProperty("id").GetGuid();
            var all = (await PostJson(c, "/api/CleaningBatch/RecordResults", new { id = batch2, listItem = Steps(("NHAN_QR", "PASS", null, null), ("PHAN_LOAI", "PASS", null, null),
                ("RUA_SO", "PASS", null, null), ("RUA_MAY", "PASS", null, null), ("KHU_KHUAN", "PASS", "Chlorine 200 ppm, 5 phút", null), ("KIEM_TRA_NHAP_KHO", "PASS", null, null)) })).Ok();
            all.GetProperty("canPass").GetBoolean().Should().BeTrue();
            all.GetProperty("responsibleName").GetString().Should().Be("Nhân viên rửa P1A");
            var done = (await c.PostFormAsync("/api/CleaningBatch/Complete", ("Id", batch2), ("QcResult", "PASS"))).Ok();
            done.GetProperty("batch").GetProperty("qcResult").GetString().Should().Be("PASS");
            Warnings(done).Should().Contain($"Sổ bao bì: 51 {s.Type} sạch, sẵn sàng dùng lại (AVAILABLE)");
            var available = await _factory.QueryAsync(db => db.PackagingLedger.AsNoTracking().SingleAsync(l => l.TransactionType == "AVAILABLE" && l.PackagingType == s.Type));
            (available.Quantity, available.CustomerId, available.ConditionCode, available.ReferenceNo).Should().Be((51m, (Guid?)null, "CLEAN", second.GetProperty("batch").GetProperty("cleaningBatchNumber").GetString()));

            // Số dư: OUT 58, IN 55 (hỏng 3), còn 3. Thất thoát 4 > còn 3 → 400; ghi thất thoát 2 → còn 1, tỷ lệ 3,45% > ngưỡng khách 2% → cảnh báo.
            var balance = (await c.GetResultAsync($"/api/PackagingLedger/GetBalance?CustomerId={s.Customer}")).Ok().GetProperty("listItem").EnumerateArray().Single();
            (balance.GetProperty("outQuantity").GetDecimal(), balance.GetProperty("inQuantity").GetDecimal(), balance.GetProperty("damagedInQuantity").GetDecimal(),
                balance.GetProperty("outstanding").GetDecimal()).Should().Be((58m, 55m, 3m, 3m));
            (await c.PostFormAsync("/api/PackagingLedger/Record", ("CustomerId", s.Customer), ("TransactionType", "LOSS"), ("PackagingType", s.Type), ("Quantity", 4m),
                ("Reason", "Khách làm mất"))).Fail("chỉ còn 3");
            (await c.PostFormAsync("/api/PackagingLedger/Record", ("CustomerId", s.Customer), ("TransactionType", "AVAILABLE"), ("PackagingType", s.Type), ("Quantity", 1m),
                ("Reason", "x"))).Fail("OUT, IN, LOSS");
            var loss = (await c.PostFormAsync("/api/PackagingLedger/Record", ("CustomerId", s.Customer), ("TransactionType", "LOSS"), ("PackagingType", s.Type), ("Quantity", 2m),
                ("DeliveryOrderId", s.DeliveryOrder), ("Reason", "Khách báo mất 2 khay"))).Ok();
            loss.GetProperty("ledger").GetProperty("referenceNo").GetString().Should().Be(s.DeliveryNumber);
            var after = loss.GetProperty("balance");
            (after.GetProperty("lossQuantity").GetDecimal(), after.GetProperty("outstanding").GetDecimal(), after.GetProperty("lossRatePct").GetDecimal(),
                after.GetProperty("lossThresholdPct").GetDecimal(), after.GetProperty("isOverThreshold").GetBoolean()).Should().Be((2m, 1m, 3.45m, 2m, true));
            Warnings(loss).Should().Contain($"Khách Khách P1A ({s.Type}): tỷ lệ thất thoát 3.45% vượt ngưỡng 2% – báo khách và chuyển kế toán tính phí");
            var over = (await c.GetResultAsync("/api/PackagingLedger/GetBalance?OverThresholdOnly=true&From=2026-12-01&To=2026-12-31")).Ok();
            over.GetProperty("listItem").EnumerateArray().Should().Contain(x => x.GetProperty("customerId").GetGuid() == s.Customer);

            // Chốt: sổ bao bì / phiếu thu hồi / kiểm tra / mẻ vệ sinh không ghi qua CRUD; phiếu đã ghi sổ không xóa.
            (await c.PostFormAsync("/api/PackagingLedger/Insert", ("CustomerId", s.Customer), ("TransactionType", "IN"), ("PackagingType", s.Type), ("Quantity", 1m))).Fail("PackagingLedger/Record");
            (await c.PostFormAsync("/api/ContainerReturn/Insert", ("DeliveryOrderId", s.DeliveryOrder), ("ReturnDate", Day))).Fail("ContainerReturn/Record");
            (await c.DeleteResultAsync($"/api/ContainerReturn/Delete?id={returnId}")).Fail("đã ghi sổ bao bì");
            (await c.PostFormAsync("/api/CleaningBatch/Insert", ("ReturnInspectionId", inspectionId), ("StartDate", Day))).Fail("CleaningBatch/Create");
            (await c.PostFormAsync("/api/ReturnInspection/Insert", ("ContainerReturnId", returnId), ("InspectionDate", Day))).Fail("ContainerReturn/Inspect");
        }

        [Fact]
        public async Task Delivery_with_zero_packaging_skips_ledger_and_return_needs_delivered_order_with_known_packaging()
        {
            var s = await SeedDeliveryAsync("P2B");
            using var c = _factory.CreateAuthorizedClient();
            // Chưa giao → không thu hồi.
            (await c.PostFormAsync("/api/ContainerReturn/Record", ("DeliveryOrderId", s.DeliveryOrder), ("ReturnedQuantity", 1))).Fail("Đã giao (hiện tại: Đang giao)");
            // Hộp dùng 1 lần: PackagingQuantity = 0 → không ghi sổ bao bì, không phiếu thu hồi.
            (await c.PostFormAsync("/api/DeliveryStop/Deliver", ("Id", s.Stop), ("TemperatureC", 65m), ("DeliveredQuantity", 60m), ("ReceiverName", "Anh Nam"),
                ("PackagingQuantity", 0m))).Ok();
            (await _factory.QueryAsync(db => db.PackagingLedger.CountAsync(l => l.CustomerId == s.Customer))).Should().Be(0);
            (await _factory.QueryAsync(db => db.ContainerReturn.CountAsync(r => r.DeliveryOrderId == s.DeliveryOrder))).Should().Be(0);
            (await c.PostFormAsync("/api/ContainerReturn/Record", ("DeliveryOrderId", s.DeliveryOrder), ("ReturnedQuantity", 1))).Fail("chưa ghi sổ bao bì giao cho khách");
            // Tồn đầu kỳ khay đang ở khách (ghi tay OUT) → thu hồi được theo loại chọn.
            (await c.PostFormAsync("/api/PackagingLedger/Record", ("CustomerId", s.Customer), ("TransactionType", "OUT"), ("PackagingType", "THUNG"), ("Quantity", 5m),
                ("Reason", "Tồn đầu kỳ"))).Ok();
            var r = (await c.PostFormAsync("/api/ContainerReturn/Record", ("DeliveryOrderId", s.DeliveryOrder), ("ReturnedQuantity", 2), ("PackagingType", "THUNG"))).Ok();
            r.GetProperty("return").GetProperty("expectedQuantity").GetInt32().Should().Be(0, "đơn giao này không giao thùng");
            Warnings(r).Should().Contain("Thu hồi 2 nhiều hơn số phải thu 0 của đơn giao (trả khay của chuyến trước)");
            r.GetProperty("outstandingOfCustomer").GetDecimal().Should().Be(3);
        }

        [Fact]
        public async Task Supplier_issue_transitions_follow_status_machine_with_resolution_owner_and_overdue_flag()
        {
            Guid supplier = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Supplier.Add(new Supplier { Id = supplier, Code = "NCC-SI", Name = "[DEMO] NCC sự cố", Status = "Active" });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();
            // CRUD Insert: luôn Mới (trạng thái gửi lên bị bỏ), hạn mặc định +2 ngày, mức độ chuẩn hóa.
            (await c.PostFormAsync("/api/SupplierIssue/Insert", ("SupplierId", supplier), ("IssueType", "QUALITY"), ("Severity", "Critical"), ("Description", "x"))).Fail("High / Medium / Low");
            (await c.PostFormAsync("/api/SupplierIssue/Insert", ("SupplierId", supplier), ("IssueType", "QUALITY"), ("Severity", "high"), ("Description", "Rau dập 10%"),
                ("IssueStatus", "Closed"))).Ok();
            var issue = await _factory.QueryAsync(db => db.SupplierIssue.AsNoTracking().SingleAsync(i => i.SupplierId == supplier));
            (issue.IssueStatus, issue.Severity, issue.ReportedAt, issue.DueAt, issue.ClosedAt).Should().Be(("Open", "High", Day.AddHours(10), Day.AddDays(2).AddHours(10), (DateTime?)null));

            (await c.PostFormAsync("/api/SupplierIssue/Transition", ("Id", issue.Id), ("To", "Closed"))).Fail("từ Mới sang Đóng");
            (await c.PostFormAsync("/api/SupplierIssue/Transition", ("Id", issue.Id), ("To", "Open"))).Fail("InProgress, Resolved hoặc Closed");
            var progress = (await c.PostFormAsync("/api/SupplierIssue/Transition", ("Id", issue.Id), ("To", "InProgress"))).Ok();
            progress.GetProperty("issue").GetProperty("ownerId").GetGuid().Should().Be(InMemoryApiFactory.UserId, "người nhận xử lý = người thao tác");
            progress.GetProperty("allowedTransitions").EnumerateArray().Select(x => x.GetString()).Should().Equal("Resolved");
            (await c.PostFormAsync("/api/SupplierIssue/Transition", ("Id", issue.Id), ("To", "Resolved"))).Fail("cách xử lý");
            (await c.PostFormAsync("/api/SupplierIssue/Transition", ("Id", issue.Id), ("To", "Resolved"), ("Resolution", "NCC đổi 5 kg rau"))).Ok()
                .GetProperty("statusName").GetString().Should().Be("Đã xử lý");
            // CRUD Update không đổi trạng thái / cách xử lý.
            (await c.PutFormAsync("/api/SupplierIssue/Update", ("Id", issue.Id), ("SupplierId", supplier), ("IssueType", "QUALITY"), ("Description", "Rau dập 10% (sửa)"),
                ("IssueStatus", "Open"), ("Resolution", "xóa"))).Ok();
            var edited = await _factory.QueryAsync(db => db.SupplierIssue.AsNoTracking().SingleAsync(i => i.Id == issue.Id));
            (edited.IssueStatus, edited.Resolution, edited.Description).Should().Be(("Resolved", "NCC đổi 5 kg rau", "Rau dập 10% (sửa)"));
            var closed = (await c.PostFormAsync("/api/SupplierIssue/Transition", ("Id", issue.Id), ("To", "Closed"))).Ok();
            closed.GetProperty("issue").GetProperty("closedAt").GetDateTime().Should().Be(Day.AddHours(10));
            (await c.PutFormAsync("/api/SupplierIssue/Update", ("Id", issue.Id), ("SupplierId", supplier), ("IssueType", "QUALITY"), ("Description", "y"))).Fail("đã Đóng");

            // Sự cố quá hạn (hạn hôm qua, chưa xử lý) lên đầu danh sách, isOverdue.
            (await c.PostFormAsync("/api/SupplierIssue/Insert", ("SupplierId", supplier), ("IssueType", "TEMPERATURE"), ("Description", "Thịt 9 °C"),
                ("ReportedAt", Day.AddDays(-3)), ("DueAt", Day.AddDays(-1)))).Ok();
            var list = (await c.GetResultAsync($"/api/SupplierIssue/GetList?SupplierId={supplier}")).Ok();
            list.GetProperty("overdueCount").GetInt32().Should().Be(1);
            var first = list.GetProperty("listItem")[0];
            (first.GetProperty("isOverdue").GetBoolean(), first.GetProperty("issue").GetProperty("issueType").GetString()).Should().Be((true, "TEMPERATURE"));
            first.GetProperty("supplierName").GetString().Should().Be("[DEMO] NCC sự cố");
            list.GetProperty("statusCounts").GetProperty("Closed").GetInt32().Should().Be(1);
        }
    }
}
