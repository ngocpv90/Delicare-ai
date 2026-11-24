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
    }
}
