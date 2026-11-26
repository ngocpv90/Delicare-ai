using System.Net.Http.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Menu
{
    /// <summary>
    /// UI-6.11 (D-89): thực đơn tuần – tự lập nháp = nhân bản tuần N − 4; lưới buổi → lựa chọn → nhóm món × 7 ngày + đánh dấu (ô trống, thiếu công thức, FC vượt);
    /// gửi duyệt bị khóa tới khi hết đánh dấu; gợi ý món: cùng nhóm → có công thức → chưa dùng 14 ngày; điền cả hàng; chép ngày; thực đơn đã duyệt chỉ đọc.
    /// </summary>
    public class MenuWeekTests(InMemoryApiFactory factory) : IClassFixture<InMemoryApiFactory>
    {
        private static readonly DateTime Week = new(2034, 3, 13);   // thứ Hai

        [Fact]
        public async Task Auto_draft_clones_cycle_week_marks_cells_blocks_submit_and_suggests_dishes()
        {
            string k = Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
            factory.Clock.FixedNow = Week.AddDays(-7).AddHours(6);
            Guid cat = Guid.NewGuid(), dA = Guid.NewGuid(), dB = Guid.NewGuid(), dC = Guid.NewGuid(), dNoRecipe = Guid.NewGuid(), source = Guid.NewGuid(), cus = Guid.NewGuid();
            await factory.SeedAsync(db =>
            {
                db.DishCategory.Add(new DishCategory { Id = cat, Code = "MC" + k, Name = "Món chính " + k });
                foreach (var (id, name, cost) in new[] { (dA, "Gà kho " + k, 15000m), (dB, "Cá chiên " + k, 12000m), (dC, "Thịt rim " + k, 14000m), (dNoRecipe, "Bò mới " + k, 16000m) })
                {
                    db.Dish.Add(new Dish { Id = id, Code = "D" + id.ToString("N")[..6], Name = name, DishCategoryId = cat, StandardCost = cost, Status = "Active" });
                    if (id != dNoRecipe)
                    {
                        db.Recipe.Add(new Recipe { Id = Guid.NewGuid(), DishId = id, VersionNo = 1, Status = "Locked", IsActive = true, YieldQuantity = 1 });
                    }
                }
                // Giá bán TRUA 40.000 (hợp đồng hiệu lực) → FC = giá vốn / 40.000.
                db.Customer.Add(new Customer { Id = cus, Code = "MW" + k, Name = "Khách " + k, DisplayName = "Khách " + k, CIF = "MW" + k, CustomerType = "BUSINESS", Status = "Active" });
                var ct = new Contract { Id = Guid.NewGuid(), CustomerId = cus, ContractNumber = "HD-MW" + k, Status = "Active", EffectiveFrom = new DateTime(2034, 1, 1) };
                db.Contract.Add(ct);
                db.ContractMeal.Add(new ContractMeal { Id = Guid.NewGuid(), ContractId = ct.Id, MealType = "TRUA", MealName = "Suất trưa", UnitPrice = 40000 });
                // Tuần N − 4: TRUA lựa chọn 1, món A thứ 2–6 (thứ 7, CN trống), món chưa có công thức ở thứ 2 dòng khác không có → chỉ A.
                db.Menu.Add(new Model.Entity.Menu { Id = source, Code = "NU-SRC-" + k, Name = "Nguồn", VersionNo = 1, Status = "Published", MenuType = "COMMON", EffectiveFrom = Week.AddDays(-28), EffectiveTo = Week.AddDays(-22) });
                for (int day = 1; day <= 5; day++)
                {
                    db.MenuLine.Add(new MenuLine { Id = Guid.NewGuid(), MenuId = source, DishId = day == 3 ? dNoRecipe : dA, MealType = "TRUA", SequenceNo = 1, DayOfWeek = day, SortOrder = 0, PortionQty = 1 });
                }
                return Task.CompletedTask;
            });
            using var c = factory.CreateAuthorizedClient();
            var draft = (await c.PostFormAsync("/api/Menu/CreateWeek", ("weekStart", Week.ToString("yyyy-MM-dd")))).Ok();
            draft.GetProperty("created").GetBoolean().Should().BeTrue();
            draft.GetProperty("sourceCode").GetString().Should().Be("NU-SRC-" + k, "nhân bản tuần N − 4");
            Guid menuId = draft.GetProperty("menuId").GetGuid();
            (await c.PostFormAsync("/api/Menu/CreateWeek", ("weekStart", Week.ToString("yyyy-MM-dd")))).Ok().GetProperty("created").GetBoolean().Should().BeFalse("tuần đã có thực đơn → bỏ qua");

            var w = (await c.GetResultAsync($"/api/Menu/GetWeek?WeekStart={Week:yyyy-MM-dd}")).Ok();
            w.GetProperty("menuId").GetGuid().Should().Be(menuId);
            var trua = w.GetProperty("listMeal").EnumerateArray().Single(m => m.GetProperty("mealType").GetString() == "TRUA");
            trua.GetProperty("price").GetDecimal().Should().Be(40000);
            var row = trua.GetProperty("listOption")[0].GetProperty("listRow").EnumerateArray().Single(r => r.GetProperty("categoryId").ValueKind != System.Text.Json.JsonValueKind.Null);
            var cells = row.GetProperty("listCell").EnumerateArray().ToList();
            cells[2].GetProperty("flag").GetString().Should().Be("NO_RECIPE");
            cells[5].GetProperty("flag").GetString().Should().Be("EMPTY");
            trua.GetProperty("listOption")[0].GetProperty("listFcPct")[0].GetDecimal().Should().Be(37.5m, "15.000 / 40.000");
            w.GetProperty("canSubmit").GetBoolean().Should().BeFalse();
            (await c.PostFormAsync("/api/Menu/SubmitWeek", ("Id", menuId))).Fail("Chưa gửi duyệt được");

            // Gợi ý cho thứ 3 (ô thiếu công thức): cùng nhóm, có công thức trước; A đã dùng trong tuần nguồn (> 14 ngày) nên không bị trừ.
            var sug = await c.GetResultAsync($"/api/Menu/SuggestDish?MenuId={menuId}&MealType=TRUA&OptionNo=1&CategoryId={cat}&Day=3");
            var names = sug.ListData.EnumerateArray().Select(x => x.GetProperty("dishName").GetString()).ToList();
            names.Should().HaveCountLessOrEqualTo(5);
            names.Take(3).Should().OnlyContain(n => n!.EndsWith(k) && !n.StartsWith("Bò mới"), "có công thức khóa xếp trước");
            sug.ListData.EnumerateArray().First().GetProperty("newFcPct").ValueKind.Should().NotBe(System.Text.Json.JsonValueKind.Null);

            // Sửa ô thứ 3 → B; điền cả hàng C cho dòng; chép ngày 1 sang 7.
            (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/Menu/SaveCell", new { menuId, mealType = "TRUA", optionNo = 1, categoryId = cat, day = 3, dishId = dB }))).Ok();
            var filled = (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/Menu/FillRow", new { menuId, mealType = "TRUA", optionNo = 1, categoryId = cat, dishId = dC }))).Ok();
            var frow = filled.GetProperty("listMeal").EnumerateArray().Single(m => m.GetProperty("mealType").GetString() == "TRUA").GetProperty("listOption")[0].GetProperty("listRow").EnumerateArray()
                .Single(r => r.GetProperty("categoryId").ValueKind != System.Text.Json.JsonValueKind.Null);
            frow.GetProperty("listCell").EnumerateArray().Select(x => x.GetProperty("dishName").GetString()).Should().OnlyContain(n => n == "Thịt rim " + k);
            (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/Menu/CopyDay", new { menuId, fromDay = 1, toDay = 7 }))).Ok();
            (await factory.QueryAsync(db => db.MenuLine.CountAsync(l => l.MenuId == menuId && !l.IsDeleted && l.DayOfWeek == 7 && l.MealType == "TRUA"))).Should().Be(1);

            w = (await c.GetResultAsync($"/api/Menu/GetWeek?WeekStart={Week:yyyy-MM-dd}")).Ok();
            w.GetProperty("emptyCells").GetInt32().Should().Be(0);
            w.GetProperty("missingRecipe").GetInt32().Should().Be(0);
            w.GetProperty("canSubmit").GetBoolean().Should().BeTrue();
            (await c.PostFormAsync("/api/Menu/SubmitWeek", ("Id", menuId))).Ok();

            // Đã duyệt → chỉ đọc.
            (await c.PostFormAsync("/api/Menu/Approve", ("Id", menuId))).Ok();
            (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/Menu/SaveCell", new { menuId, mealType = "TRUA", optionNo = 1, categoryId = cat, day = 3, dishId = dA }))).Fail("không còn ở Nháp");
        }

        [Fact]
        public async Task Excel_template_round_trip_matches_names_without_accents_suggests_and_writes_picks()
        {
            string k = Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
            DateTime week = Week.AddDays(21);
            factory.Clock.FixedNow = week.AddDays(-7);
            Guid dGa = Guid.NewGuid(), dCa = Guid.NewGuid();
            await factory.SeedAsync(db =>
            {
                db.Dish.Add(new Dish { Id = dGa, Code = "EX1" + k, Name = "Gà kho gừng " + k, Status = "Active" });
                db.Dish.Add(new Dish { Id = dCa, Code = "EX2" + k, Name = "Cá thu sốt cà " + k, Status = "Active" });
                return Task.CompletedTask;
            });
            using var c = factory.CreateAuthorizedClient();
            var created = (await c.PostFormAsync("/api/Menu/CreateWeek", ("weekStart", week.ToString("yyyy-MM-dd")), ("blank", true))).Ok();
            Guid menuId = created.GetProperty("menuId").GetGuid();
            var bytes = await c.GetByteArrayAsync($"/api/Menu/GetWeekTemplate?WeekStart={week:yyyy-MM-dd}");
            using var input = new MemoryStream(bytes);
            using var wb = new ClosedXML.Excel.XLWorkbook(input);
            var ws = wb.Worksheet("1. Menu");
            int row = Enumerable.Range(2, ws.LastRowUsed()!.RowNumber()).First(r => ws.Cell(r, 1).GetString() == "TRUA");
            ws.Cell(row, 4).Value = "ga kho gung " + k.ToLowerInvariant();   // không dấu → vẫn khớp
            ws.Cell(row, 5).Value = "Cá thu " + k;                          // không khớp → gợi ý
            using var output = new MemoryStream();
            wb.SaveAs(output);
            async Task<LegacyResult> Send(bool apply, string? picks)
            {
                using var form = new MultipartFormDataContent { { new StringContent(menuId.ToString()), "menuId" }, { new StringContent(apply ? "true" : "false"), "apply" }, { new ByteArrayContent(output.ToArray()), "file", "menu.xlsx" } };
                if (picks != null) form.Add(new StringContent(picks), "picks");
                return await LegacyResult.ReadAsync(await c.PostAsync("/api/Menu/ImportWeek", form));
            }
            var pre = (await Send(false, null)).Ok();
            pre.GetProperty("matched").GetInt32().Should().Be(1);
            pre.GetProperty("unmatched").GetInt32().Should().Be(1);
            var miss = pre.GetProperty("listCell").EnumerateArray().Single(x => x.GetProperty("dishId").ValueKind == System.Text.Json.JsonValueKind.Null);
            miss.GetProperty("listSuggestion").EnumerateArray().Select(x => x.GetProperty("dishId").GetGuid()).Should().Contain(dCa);
            var done = (await Send(true, $$"""{"{{miss.GetProperty("key").GetString()}}":"{{dCa}}"}""")).Ok();
            done.GetProperty("written").GetInt32().Should().Be(2);
            var lines = await factory.QueryAsync(db => db.MenuLine.AsNoTracking().Where(l => l.MenuId == menuId && !l.IsDeleted).ToListAsync());
            lines.Select(l => (l.DishId, l.DayOfWeek)).Should().BeEquivalentTo(new[] { (dGa, (int?)1), (dCa, (int?)2) });
        }

        /// <summary>REAL-1 (D-129): thực đơn thật có nhiều món cùng nhóm (trưa 2 món chính) – file 2 hàng cùng nhóm → 2 dòng Slot 0 / 1, không ghi đè; sửa ô theo Slot.</summary>
        [Fact]
        public async Task Import_keeps_several_rows_of_the_same_category_as_slots()
        {
            string k = Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
            DateTime week = Week.AddDays(42);
            factory.Clock.FixedNow = week.AddDays(-7);
            Guid cat = Guid.NewGuid(), d1 = Guid.NewGuid(), d2 = Guid.NewGuid(), d3 = Guid.NewGuid();
            await factory.SeedAsync(db =>
            {
                db.DishCategory.Add(new DishCategory { Id = cat, Code = "SL" + k, Name = "Món chính SL " + k });
                db.Dish.Add(new Dish { Id = d1, Code = "SL1" + k, Name = "Thịt kho SL " + k, DishCategoryId = cat, Status = "Active" });
                db.Dish.Add(new Dish { Id = d2, Code = "SL2" + k, Name = "Tôm chiên SL " + k, DishCategoryId = cat, Status = "Active" });
                db.Dish.Add(new Dish { Id = d3, Code = "SL3" + k, Name = "Gà rang SL " + k, DishCategoryId = cat, Status = "Active" });
                return Task.CompletedTask;
            });
            using var c = factory.CreateAuthorizedClient();
            Guid menuId = (await c.PostFormAsync("/api/Menu/CreateWeek", ("weekStart", week.ToString("yyyy-MM-dd")), ("blank", true))).Ok().GetProperty("menuId").GetGuid();
            using var wb = new ClosedXML.Excel.XLWorkbook();
            var ws = wb.AddWorksheet("1. Menu");
            ws.Cell(1, 1).Value = "Buổi"; ws.Cell(1, 2).Value = "Lựa chọn"; ws.Cell(1, 3).Value = "Nhóm món";
            ws.Cell(2, 1).Value = "TRUA"; ws.Cell(2, 2).Value = 1; ws.Cell(2, 4).Value = "Thịt kho SL " + k;
            ws.Cell(3, 1).Value = "TRUA"; ws.Cell(3, 2).Value = 1; ws.Cell(3, 4).Value = "Tôm chiên SL " + k;
            using var output = new MemoryStream();
            wb.SaveAs(output);
            using var form = new MultipartFormDataContent { { new StringContent(menuId.ToString()), "menuId" }, { new StringContent("true"), "apply" }, { new ByteArrayContent(output.ToArray()), "file", "menu.xlsx" } };
            (await LegacyResult.ReadAsync(await c.PostAsync("/api/Menu/ImportWeek", form))).Ok().GetProperty("written").GetInt32().Should().Be(2);
            (await factory.QueryAsync(db => db.MenuLine.CountAsync(l => l.MenuId == menuId && !l.IsDeleted))).Should().Be(2, "2 món chính cùng ngày không ghi đè nhau");

            async Task<List<(int Slot, Guid? Dish)>> RowsAsync()
            {
                var w = (await c.GetResultAsync($"/api/Menu/GetWeek?WeekStart={week:yyyy-MM-dd}")).Ok();
                return [.. w.GetProperty("listMeal").EnumerateArray().Where(m => m.GetProperty("mealType").GetString() == "TRUA")
                    .SelectMany(m => m.GetProperty("listOption").EnumerateArray()).SelectMany(o => o.GetProperty("listRow").EnumerateArray())
                    .Where(r => r.GetProperty("categoryId").ValueKind == System.Text.Json.JsonValueKind.String && r.GetProperty("categoryId").GetGuid() == cat)
                    .Select(r => (r.GetProperty("slot").GetInt32(), r.GetProperty("listCell")[0].GetProperty("dishId") is { ValueKind: System.Text.Json.JsonValueKind.String } x ? x.GetGuid() : (Guid?)null))];
            }
            (await RowsAsync()).Should().Equal((0, d1), (1, d2));
            var week1 = (await c.GetResultAsync($"/api/Menu/GetWeek?WeekStart={week:yyyy-MM-dd}")).Ok();
            var trua = week1.GetProperty("listMeal").EnumerateArray().Single(m => m.GetProperty("mealType").GetString() == "TRUA").GetProperty("listOption")[0].GetProperty("listRow").EnumerateArray()
                .Where(r => r.GetProperty("categoryId").ValueKind == System.Text.Json.JsonValueKind.String && r.GetProperty("categoryId").GetGuid() == cat).ToList();
            trua[0].GetProperty("listCell")[1].GetProperty("flag").GetString().Should().Be("EMPTY", "dòng đầu của nhóm trống → ô trống");
            trua[1].GetProperty("listCell")[1].GetProperty("flag").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null, "dòng thêm (Slot 1) trống không tính ô trống");

            // Đổi món dòng 2 (Slot 1) – dòng 1 giữ nguyên.
            (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/Menu/SaveCell", new { menuId, mealType = "TRUA", optionNo = 1, categoryId = cat, slot = 1, day = 1, dishId = d3 }))).Ok();
            (await RowsAsync()).Should().Equal((0, d1), (1, d3));
        }
    }
}
