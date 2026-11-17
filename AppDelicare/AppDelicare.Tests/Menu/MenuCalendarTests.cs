using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Menu
{
    /// <summary>
    /// T13 + T46B.1 (Pilot P3) – thực đơn tuần: dòng thực đơn (thứ 1–7, chỉ khi Nháp / Đã duyệt, cảnh báo món chưa có công thức khóa),
    /// lịch tuần + giá vốn + FC%, Approve / Publish / Expire / Clone, phân công + Resolve (điểm giao trước khách), in Thực đơn gửi khách.
    /// </summary>
    public class MenuCalendarTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public MenuCalendarTests(InMemoryApiFactory factory) => _factory = factory;

        /// <summary>Món có công thức đang khóa: 100 suất dùng <paramref name="kg"/> kg nguyên liệu giá <paramref name="pricePerKg"/> → giá vốn / suất = kg × giá / 100.</summary>
        private async Task<Guid> DishWithRecipeAsync(string code, decimal kg, decimal pricePerKg, Guid? category = null)
        {
            Guid dish = Guid.NewGuid(), ing = Guid.NewGuid(), unit = Guid.NewGuid(), recipe = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Dish.Add(new Dish { Id = dish, Code = code, Name = "Món " + code, Status = "Active", DishCategoryId = category, StandardServingSize = 120 });
                db.UnitMeasure.Add(new UnitMeasure { Id = unit, Name = "kg-" + code });
                db.Ingredient.Add(new Ingredient { Id = ing, Code = code + "-NL", Name = "NL " + code });
                db.SupplierMaterial.Add(new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = Guid.NewGuid(), IngredientId = ing, UnitPrice = pricePerKg, PurchaseUomId = unit, IsPreferred = true });
                db.Recipe.Add(new Recipe { Id = recipe, DishId = dish, VersionNo = 1, VersionCode = code + "-V1", Status = "Locked", IsActive = true, StandardOutputQty = 100, EffectiveFrom = new DateTime(2026, 1, 1) });
                db.RecipeLine.Add(new RecipeLine { Id = Guid.NewGuid(), RecipeId = recipe, IngredientId = ing, Quantity = kg, UnitId = unit, UomId = unit });
                return Task.CompletedTask;
            });
            return dish;
        }

        private async Task<Guid> DishWithoutRecipeAsync(string code)
        {
            Guid dish = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Dish.Add(new Dish { Id = dish, Code = code, Name = "Món " + code, Status = "Active" });
                return Task.CompletedTask;
            });
            return dish;
        }

        private static async Task<Guid> MenuAsync(HttpClient c, string code, DateTime from, DateTime? to = null)
        {
            (await c.PostFormAsync("/api/Menu/Insert", ("Code", code), ("Name", "Thực đơn " + code), ("EffectiveFrom", from), ("EffectiveTo", to), ("Status", "Published"))).Ok();
            return Guid.Empty;
        }

        private Task<Model.Entity.Menu> MenuByCodeAsync(string code) => _factory.QueryAsync(db => db.Menu.AsNoTracking().SingleAsync(m => m.Code == code && !m.IsDeleted));

        private static Task<LegacyResult> AddLine(HttpClient c, Guid menu, Guid dish, int day, int option = 1, string meal = "TRUA", int sort = 0)
            => c.PostFormAsync("/api/MenuLine/Insert", ("MenuId", menu), ("DishId", dish), ("MealType", meal), ("DayOfWeek", day), ("SequenceNo", option), ("SortOrder", sort));

        [Fact]
        public async Task Calendar_pivots_day_by_option_and_category_with_cost_and_food_cost_percent()
        {
            Guid category = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.DishCategory.Add(new DishCategory { Id = category, Code = "MC-CAL", Name = "Món chính" });
                return Task.CompletedTask;
            });
            Guid meat = await DishWithRecipeAsync("CAL-THIT", kg: 10, pricePerKg: 20000, category);   // 2 000 đ / suất
            Guid fish = await DishWithRecipeAsync("CAL-CA", kg: 5, pricePerKg: 40000, category);      // 2 000 đ / suất
            Guid veg = await DishWithoutRecipeAsync("CAL-RAU");
            using var c = _factory.CreateAuthorizedClient();
            await MenuAsync(c, "TD-CAL", new DateTime(2026, 12, 7));
            var menu = await MenuByCodeAsync("TD-CAL");
            menu.Status.Should().Be("Draft", "thực đơn mới luôn là Nháp (không nhận Status từ form)");

            (await AddLine(c, menu.Id, meat, day: 1, sort: 1)).Ok();
            (await AddLine(c, menu.Id, fish, day: 1, sort: 2)).Ok();
            var warn = await AddLine(c, menu.Id, veg, day: 1, option: 2);
            warn.Ok();
            warn.Message.Should().Contain("Cảnh báo").And.Contain("CAL-RAU");
            (await AddLine(c, menu.Id, meat, day: 8)).Fail("Thứ phải trong khoảng");
            (await AddLine(c, menu.Id, meat, day: 0)).Fail("Thứ phải trong khoảng");

            var cal = (await c.GetResultAsync($"/api/Menu/GetCalendar?id={menu.Id}&mealType=TRUA&sellingPrice=40000")).Ok();
            cal.GetProperty("sellingPrice").GetDecimal().Should().Be(40000);
            var rows = cal.GetProperty("listRow").EnumerateArray().ToList();
            var main = rows.Single(r => r.GetProperty("optionNo").GetInt32() == 1);
            main.GetProperty("dishCategoryName").GetString().Should().Be("Món chính");
            var monday = main.GetProperty("cells").GetProperty("1").EnumerateArray().ToList();
            monday.Select(d => d.GetProperty("dishCode").GetString()).Should().Equal("CAL-THIT", "CAL-CA");
            monday[0].GetProperty("costPerMeal").GetDecimal().Should().Be(2000);

            var totals = cal.GetProperty("listTotal").EnumerateArray().ToList();
            var option1 = totals.Single(t => t.GetProperty("optionNo").GetInt32() == 1 && t.GetProperty("dayOfWeek").GetInt32() == 1);
            option1.GetProperty("costPerMeal").GetDecimal().Should().Be(4000);
            option1.GetProperty("foodCostPercent").GetDecimal().Should().Be(10);
            option1.GetProperty("costComplete").GetBoolean().Should().BeTrue();
            var option2 = totals.Single(t => t.GetProperty("optionNo").GetInt32() == 2);
            option2.GetProperty("costComplete").GetBoolean().Should().BeFalse("món chưa có công thức khóa");

            // Không có giá bán → không tính FC%.
            var noPrice = (await c.GetResultAsync($"/api/Menu/GetCalendar?id={menu.Id}")).Ok();
            noPrice.GetProperty("listTotal")[0].GetProperty("foodCostPercent").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        }

        [Fact]
        public async Task Approve_publish_expire_state_machine_and_lines_locked_after_publish()
        {
            Guid dish = await DishWithRecipeAsync("ST-MON", 10, 10000);
            using var c = _factory.CreateAuthorizedClient();
            // Hiệu lực 1 ngày (Thứ 2, 07/12/2026) → công bố chỉ cần món của Thứ 2.
            await MenuAsync(c, "TD-ST", new DateTime(2026, 12, 7), new DateTime(2026, 12, 7));
            var menu = await MenuByCodeAsync("TD-ST");

            (await c.PostFormAsync("/api/Menu/Approve", ("Id", menu.Id))).Fail("ít nhất 1 món");
            (await AddLine(c, menu.Id, dish, day: 2)).Ok();
            (await c.PostFormAsync("/api/Menu/Publish", ("Id", menu.Id))).Fail("trạng thái");
            (await c.PostFormAsync("/api/Menu/Approve", ("Id", menu.Id))).Ok();
            (await c.PostFormAsync("/api/Menu/Publish", ("Id", menu.Id))).Fail("Thứ 2");
            (await AddLine(c, menu.Id, dish, day: 1)).Ok();
            (await c.PostFormAsync("/api/Menu/Publish", ("Id", menu.Id))).Ok();
            (await MenuByCodeAsync("TD-ST")).Status.Should().Be("Published");

            (await AddLine(c, menu.Id, dish, day: 3)).Fail("Nháp / Đã duyệt");
            (await c.PostFormAsync("/api/Menu/Approve", ("Id", menu.Id))).Fail("trạng thái");

            var clone = (await c.PostFormAsync("/api/Menu/Clone", ("Id", menu.Id), ("Code", "TD-ST-2"), ("EffectiveFrom", new DateTime(2026, 12, 14)))).Ok();
            clone.GetProperty("status").GetString().Should().Be("Draft");
            Guid cloneId = clone.GetProperty("id").GetGuid();
            (await _factory.QueryAsync(db => db.MenuLine.CountAsync(l => l.MenuId == cloneId && !l.IsDeleted))).Should().Be(2);
            (await c.PostFormAsync("/api/Menu/Clone", ("Id", menu.Id), ("Code", "TD-ST-2"), ("EffectiveFrom", new DateTime(2026, 12, 14)))).Fail("đã tồn tại");

            (await c.PostFormAsync("/api/Menu/Expire", ("Id", menu.Id))).Ok();
            (await MenuByCodeAsync("TD-ST")).Status.Should().Be("Expired");
            (await c.PostFormAsync("/api/Menu/Expire", ("Id", menu.Id))).Fail("trạng thái");
        }

        [Fact]
        public async Task Assignment_resolve_prefers_location_level_and_rejects_overlap()
        {
            Guid customer = Guid.NewGuid(), locA = Guid.NewGuid(), locB = Guid.NewGuid(), contract = Guid.NewGuid(), otherCustomer = Guid.NewGuid();
            Guid menuCustomer = Guid.NewGuid(), menuLocation = Guid.NewGuid(), menuDraft = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Customer.AddRange(new Customer { Id = customer, Code = "ASG", Name = "Khách ASG", CIF = "ASG", CustomerType = "FACTORY", DisplayName = "ASG", Status = "Active" }, new Customer { Id = otherCustomer, Code = "ASG-2", Name = "Khách khác", CIF = "ASG-2", CustomerType = "FACTORY", DisplayName = "ASG-2", Status = "Active" });
                db.CustomerDeliveryLocation.AddRange(
                    new CustomerDeliveryLocation { Id = locA, CustomerId = customer, Code = "ASG-A", Name = "A" },
                    new CustomerDeliveryLocation { Id = locB, CustomerId = customer, Code = "ASG-B", Name = "B" });
                db.Contract.Add(new Contract { Id = contract, CustomerId = customer, ContractNumber = "HD-ASG", Status = "Active", EffectiveFrom = new DateTime(2026, 1, 1) });
                db.ContractMeal.Add(new ContractMeal { Id = Guid.NewGuid(), ContractId = contract, MealType = "TRUA", UnitPrice = 40000 });
                db.Menu.AddRange(
                    new Model.Entity.Menu { Id = menuCustomer, Code = "ASG-KH", VersionNo = 1, Status = "Published" },
                    new Model.Entity.Menu { Id = menuLocation, Code = "ASG-DG", VersionNo = 1, Status = "Approved" },
                    new Model.Entity.Menu { Id = menuDraft, Code = "ASG-NHAP", VersionNo = 1, Status = "Draft" });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();
            Task<LegacyResult> Assign(Guid menu, Guid? location, DateTime from, DateTime? to = null, Guid? contractId = null, string meal = "TRUA", Guid? cust = null)
                => c.PostFormAsync("/api/MenuAssignment/Insert", ("CustomerId", cust ?? customer), ("MenuId", menu), ("DeliveryLocationId", location), ("MealType", meal),
                    ("EffectiveFrom", from), ("EffectiveTo", to), ("ContractId", contractId));

            (await Assign(menuCustomer, null, new DateTime(2026, 12, 1), contractId: contract)).Ok();
            (await Assign(menuLocation, locA, new DateTime(2026, 12, 1), new DateTime(2026, 12, 31))).Ok();
            (await Assign(menuDraft, locA, new DateTime(2026, 12, 15), new DateTime(2027, 1, 15))).Fail("Chồng thời gian");
            (await Assign(menuDraft, locB, new DateTime(2026, 12, 1), contractId: contract, meal: "TOI")).Fail("TOI");
            (await Assign(menuDraft, locB, new DateTime(2026, 12, 1), cust: otherCustomer)).Fail("Điểm giao không thuộc");
            (await Assign(menuDraft, null, new DateTime(2026, 12, 1), contractId: contract, cust: otherCustomer)).Fail("Hợp đồng không thuộc");
            (await Assign(menuDraft, locB, new DateTime(2026, 12, 20), new DateTime(2026, 12, 10))).Fail("hiệu lực từ");

            var atA = (await c.GetResultAsync($"/api/MenuAssignment/Resolve?customerId={customer}&deliveryLocationId={locA}&mealType=TRUA&date=2026-12-10")).Ok();
            atA.GetProperty("menuId").GetGuid().Should().Be(menuLocation);
            atA.GetProperty("level").GetString().Should().Be("LOCATION");
            var atB = (await c.GetResultAsync($"/api/MenuAssignment/Resolve?customerId={customer}&deliveryLocationId={locB}&mealType=TRUA&date=2026-12-10")).Ok();
            atB.GetProperty("menuId").GetGuid().Should().Be(menuCustomer);
            atB.GetProperty("level").GetString().Should().Be("CUSTOMER");
            var afterLocation = (await c.GetResultAsync($"/api/MenuAssignment/Resolve?customerId={customer}&deliveryLocationId={locA}&mealType=TRUA&date=2027-01-05")).Ok();
            afterLocation.GetProperty("menuId").GetGuid().Should().Be(menuCustomer, "phân công điểm giao hết hiệu lực → về cấp khách");
            (await c.GetResultAsync($"/api/MenuAssignment/Resolve?customerId={customer}&deliveryLocationId={locA}&mealType=SANG&date=2026-12-10")).Fail("Không có thực đơn");
        }

        [Fact]
        public async Task Export_for_customer_produces_an_xlsx_that_opens_with_header_days_and_dishes()
        {
            Guid company = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Company.Add(new Company { Id = company, Code = "DLC-EXP", Name = "[DEMO] CÔNG TY THỬ IN", Address = "[DEMO] Địa chỉ" });
                return Task.CompletedTask;
            });
            Guid dish = await DishWithRecipeAsync("EXP-MON", 10, 10000);
            Guid breakfast = await DishWithoutRecipeAsync("EXP-SANG");
            using var c = _factory.CreateAuthorizedClient();
            (await c.PostFormAsync("/api/Menu/Insert", ("Code", "TD-EXP"), ("Name", "Thực đơn in"), ("EffectiveFrom", new DateTime(2026, 12, 7)), ("CompanyId", company))).Ok();
            var menu = await MenuByCodeAsync("TD-EXP");
            (await AddLine(c, menu.Id, dish, day: 1)).Ok();
            (await AddLine(c, menu.Id, dish, day: 7, option: 2)).Ok();
            (await AddLine(c, menu.Id, breakfast, day: 7, meal: "SANG")).Ok();

            var response = await c.GetAsync($"/api/Menu/ExportForCustomer?id={menu.Id}&project=Nhà máy thử");
            response.EnsureSuccessStatusCode();
            response.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            using var stream = new MemoryStream(await response.Content.ReadAsByteArrayAsync());
            using var wb = new XLWorkbook(stream);
            var ws = wb.Worksheets.First();
            ws.Cell(1, 1).GetString().Should().Be("[DEMO] CÔNG TY THỬ IN");
            ws.Cell(5, 1).GetString().Should().Be("THỰC ĐƠN SUẤT ĂN");
            ws.Cell(6, 1).GetString().Should().Contain("Nhà máy thử");
            ws.Cell(9, 2).GetString().Should().Be("SÁNG");
            ws.Cell(9, 4).GetString().Should().Be("TRƯA");
            ws.Cell(10, 3).GetString().Should().Be("Trọng lượng thành phẩm");
            var text = ws.CellsUsed().Select(x => x.GetString()).ToList();
            text.Should().Contain("Thứ 2").And.Contain("Chủ nhật");
            text.Should().Contain(t => t.Contains("Món EXP-MON"));
            text.Should().Contain("120 g");

            // Thực đơn không có món → lỗi nghiệp vụ dạng JSON.
            (await c.PostFormAsync("/api/Menu/Insert", ("Code", "TD-EXP-EMPTY"), ("EffectiveFrom", new DateTime(2026, 12, 7)))).Ok();
            var empty = await MenuByCodeAsync("TD-EXP-EMPTY");
            (await c.GetResultAsync($"/api/Menu/ExportForCustomer?id={empty.Id}")).Fail("ít nhất 1 món");
        }
    }
}
