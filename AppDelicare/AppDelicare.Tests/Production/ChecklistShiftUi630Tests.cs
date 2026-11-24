using System.Net.Http.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Services.Implement;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Production
{
    /// <summary>
    /// UI-6.30 (D-111): ma trận Bộ kiểm tra × Ca (bộ ghi qua thao tác riêng không xuất hiện, quá hạn theo giờ xuất bếp), tiêu chí trọng yếu Không đạt
    /// ở lượt chung giữ đúng các mẻ được chọn (Đạt → Chờ QC + giữ; Đã đóng gói → chặn xuất), kiểm lại Đạt mở chặn; Sổ kiểm tra gộp dòng theo mẻ, lọc đúng; tỷ lệ đạt.
    /// </summary>
    public class ChecklistShiftUi630Tests : IClassFixture<InMemoryApiFactory>
    {
        private static readonly DateTime Day = new(2035, 4, 10);
        private readonly InMemoryApiFactory _factory;

        public ChecklistShiftUi630Tests(InMemoryApiFactory factory)
        {
            _factory = factory;
            _factory.Clock.FixedNow = Day.AddHours(10);
        }

        private static async Task<LegacyResult> PostJson(HttpClient c, string url, object body) => await LegacyResult.ReadAsync(await c.PostAsJsonAsync(url, body));

        [Fact]
        public async Task Day_board_critical_fail_holds_chosen_batches_and_book_lists_merged_rows()
        {
            Guid shift = Guid.NewGuid(), plan = Guid.NewGuid(), order = Guid.NewGuid(), dish = Guid.NewGuid(), chill = Guid.NewGuid(), stage = Guid.NewGuid();
            Guid bPassed = Guid.NewGuid(), bCooking = Guid.NewGuid(), bPacked = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Shift.Add(new Shift { Id = shift, Code = "T630", Name = "Trưa 630", StartTime = new TimeSpan(10, 30, 0), EndTime = new TimeSpan(13, 30, 0), Status = "Active" });
                db.Dish.Add(new Dish { Id = dish, Code = "M630", Name = "Gà luộc 630", Status = "Active" });
                db.ProductionPlan.Add(new ProductionPlan { Id = plan, PlanNumber = "KH-630", PlanNo = "KH-630", ProductionDate = Day, PlanDate = Day.AddDays(-1), Status = "Released" });
                db.ProductionOrder.Add(new ProductionOrder { Id = order, ProductionPlanId = plan, DishId = dish, RecipeId = Guid.NewGuid(), ShiftId = shift, PlannedQty = 300, Status = "InProgress", ProductionOrderNumber = "LS-630" });
                foreach (var (id, no, st) in new[] { (bPassed, "ME-630-01", "Passed"), (bCooking, "ME-630-02", "Cooking"), (bPacked, "ME-630-03", "Packed") })
                {
                    db.ProductionBatch.Add(new ProductionBatch { Id = id, ProductionOrderId = order, DishId = dish, BatchNumber = no, BatchNo = no, StartDate = Day.AddHours(7), PlannedQuantity = 100, Status = st, QcStatus = st == "Cooking" ? null : "PASS" });
                }
                db.FoodSafetyChecklist.AddRange(
                    new FoodSafetyChecklist { Id = chill, Code = "BLAST_CHILL", Name = "Làm lạnh nhanh", VersionNo = 1, Status = "Active", EffectiveFrom = Day.AddDays(-30) },
                    new FoodSafetyChecklist { Id = stage, Code = "PRD_STAGE", Name = "Công đoạn", VersionNo = 99, Status = "Active", EffectiveFrom = Day.AddDays(-30) });
                db.FoodSafetyChecklistItem.AddRange(
                    new FoodSafetyChecklistItem { Id = Guid.NewGuid(), ChecklistId = chill, ItemCode = "LANH_LOI", ItemName = "Nhiệt độ lõi sau làm lạnh", SortOrder = 1, IsCritical = true, IsRequired = true, ExpectedValue = "≤ 5", UnitCode = "°C" },
                    new FoodSafetyChecklistItem { Id = Guid.NewGuid(), ChecklistId = chill, ItemCode = "VE_SINH_TU", ItemName = "Tủ làm lạnh sạch", SortOrder = 2, IsCritical = false, IsRequired = true },
                    new FoodSafetyChecklistItem { Id = Guid.NewGuid(), ChecklistId = stage, ItemCode = "SO_CHE", ItemName = "Sơ chế", SortOrder = 1, IsRequired = true });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();

            // Ma trận: bộ công đoạn (ghi ở màn Mẻ nấu) không xuất hiện; hạn = giờ xuất bếp (10:30 − 60′ = 09:30) − 20′ = 09:10 → 10:00 quá hạn.
            var board = (await c.GetResultAsync($"/api/FoodSafetyChecklistResult/GetDayBoard?date={Day:yyyy-MM-dd}")).Ok();
            var rows = board.GetProperty("listChecklist").EnumerateArray().ToList();
            rows.Select(r => r.GetProperty("code").GetString()).Should().Contain("BLAST_CHILL").And.NotContain("PRD_STAGE");
            var cell = rows.Single(r => r.GetProperty("code").GetString() == "BLAST_CHILL").GetProperty("listCell").EnumerateArray().Single(x => x.GetProperty("shiftId").GetGuid() == shift);
            (cell.GetProperty("status").GetString(), cell.GetProperty("total").GetInt32(), cell.GetProperty("dueAt").GetDateTime()).Should().Be(("OVERDUE", 2, Day.AddHours(9).AddMinutes(10)));
            board.GetProperty("listShift").EnumerateArray().Single(s => s.GetProperty("id").GetGuid() == shift).GetProperty("listBatch").GetArrayLength().Should().Be(3);

            // Trọng yếu Không đạt ở lượt chung, không chọn mẻ, không lý do → lỗi; chọn 2 mẻ → giữ đúng 2 mẻ.
            object Items(string lanh) => new object[]
            {
                new { itemCode = "LANH_LOI", resultCode = lanh, actualValue = lanh == "FAIL" ? "9 °C" : "4 °C", correctiveAction = lanh == "FAIL" ? "Chuyển sang tủ khác, đo lại" : null },
                new { itemCode = "VE_SINH_TU", resultCode = "PASS" },
            };
            (await PostJson(c, "/api/FoodSafetyChecklistResult/Record", new { checklistId = chill, shiftId = shift, checkDate = Day, listItem = Items("FAIL") })).Fail("chọn mẻ bị ảnh hưởng");
            var rec = (await PostJson(c, "/api/FoodSafetyChecklistResult/Record", new { checklistId = chill, shiftId = shift, checkDate = Day, listProductionBatchId = new[] { bPassed, bPacked }, listItem = Items("FAIL") })).Ok();
            rec.GetProperty("listHeldBatch").GetArrayLength().Should().Be(2);
            var batches = await _factory.QueryAsync(db => db.ProductionBatch.AsNoTracking().Where(b => b.ProductionOrderId == order).ToDictionaryAsync(b => b.Id));
            (batches[bPassed].Status, batches[bPassed].QcStatus).Should().Be(("QCPending", "HOLD"));
            batches[bPacked].Status.Should().Be("Packed");
            batches[bCooking].QcStatus.Should().BeNull("mẻ không chọn không bị giữ");
            var open = await _factory.QueryAsync(db => FoodSafetyRules.OpenCriticalFailuresAsync(db, [bPassed, bCooking, bPacked]));
            open.Keys.Should().BeEquivalentTo([bPassed, bPacked]);

            cell = (await c.GetResultAsync($"/api/FoodSafetyChecklistResult/GetDayBoard?date={Day:yyyy-MM-dd}")).Ok().GetProperty("listChecklist").EnumerateArray()
                .Single(r => r.GetProperty("code").GetString() == "BLAST_CHILL").GetProperty("listCell").EnumerateArray().Single(x => x.GetProperty("shiftId").GetGuid() == shift);
            cell.GetProperty("status").GetString().Should().Be("CRITICAL");
            cell.GetProperty("listHeldBatch").EnumerateArray().Select(x => x.GetString()).Should().Equal("ME-630-01", "ME-630-03");

            // Kiểm lại Đạt ở lượt chung → mẻ đang bị giữ vì tiêu chí này được mở chặn.
            (await PostJson(c, "/api/FoodSafetyChecklistResult/Record", new { checklistId = chill, shiftId = shift, checkDate = Day, listItem = new[] { new { itemCode = "LANH_LOI", resultCode = "PASS", actualValue = "4 °C" } } })).Ok();
            (await _factory.QueryAsync(db => FoodSafetyRules.OpenCriticalFailuresAsync(db, [bPassed, bPacked]))).Should().BeEmpty();

            // Sổ: dòng Không đạt nhân bản theo mẻ gộp thành 1 (2 mẻ); kiểm lại ghi "kiểm lại"; lọc trọng yếu.
            var book = (await c.GetResultAsync($"/api/FoodSafetyChecklistResult/GetBook?From={Day:yyyy-MM-dd}&To={Day:yyyy-MM-dd}&ChecklistId={chill}")).Ok();
            var lines = book.GetProperty("listDay")[0].GetProperty("listRow").EnumerateArray().ToList();
            lines.Should().HaveCount(3, "2 tiêu chí lần đầu (dòng gắn mẻ gộp) + kiểm lại (dòng mở chặn của 2 mẻ gộp vào)");
            var fail = lines.Single(l => l.GetProperty("resultCode").GetString() == "FAIL");
            fail.GetProperty("listBatch").EnumerateArray().Select(x => x.GetString()).Should().Equal("ME-630-01", "ME-630-03");
            fail.GetProperty("expectedValue").GetString().Should().Be("≤ 5 °C");
            lines.Where(l => l.GetProperty("isRecheck").GetBoolean()).Should().NotBeEmpty();
            (await c.GetResultAsync($"/api/FoodSafetyChecklistResult/GetBook?From={Day:yyyy-MM-dd}&To={Day:yyyy-MM-dd}&OnlyCritical=true")).Ok().GetProperty("lines").GetInt32().Should().Be(1);

            var rate = (await c.GetResultAsync($"/api/FoodSafetyChecklistResult/GetPassRate?from={Day:yyyy-MM-dd}&to={Day:yyyy-MM-dd}")).Ok();
            rate.GetProperty("listSeries").EnumerateArray().Single(x => x.GetProperty("checklistId").GetGuid() == chill).GetProperty("ratePct").GetDecimal().Should().Be(100m, "kiểm lại Đạt thay lần trước");
            rate.GetProperty("listTopFail")[0].GetProperty("itemName").GetString().Should().Be("Nhiệt độ lõi sau làm lạnh");

            // CRUD vẫn chặn.
            (await c.PostFormAsync("/api/FoodSafetyChecklistResult/Insert", ("ChecklistId", chill), ("ChecklistItemId", Guid.NewGuid()), ("CheckDate", Day), ("ResultCode", "PASS"))).Fail("không");
        }
    }
}
