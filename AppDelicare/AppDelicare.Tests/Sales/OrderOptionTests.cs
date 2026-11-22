using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Option;
using AppDelicare.Services.Implement;
using AppDelicare.Tests.Integration;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Sales
{
    /// <summary>
    /// ORD-OPT-1 (D-74): đăng ký suất theo lựa chọn + thực đơn chung – đơn theo lựa chọn (kiểm lựa chọn / trùng), thực đơn chung (tra + chặn chồng hiệu lực),
    /// suất món theo lựa chọn (không chia đều; dòng cũ NULL giữ cách cũ), đăng ký tuần (lưu / xác nhận / chép tuần trước), phiếu đăng ký Excel đọc lại.
    /// Tuần thử: thứ Hai 14/12/2026 (đồng hồ test 08/12 → mọi ngày chưa tới giờ chốt).
    /// </summary>
    public class OrderOptionTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public OrderOptionTests(InMemoryApiFactory factory)
        {
            _factory = factory;
            _factory.Clock.FixedNow = new DateTime(2026, 12, 8, 10, 0, 0);
        }

        private sealed record Scenario(Guid Customer, Guid LocA, Guid LocB, Guid MealSang, Guid MealTrua, Guid Menu, Guid BanhMi, Guid BanhBao, Guid BanhChung, Guid Sua, Guid Com);

        /// <summary>
        /// Khách + 2 điểm giao + hợp đồng (SANG 20 000, TRUA 40 000), KHÔNG phân công thực đơn; thực đơn chung Đã công bố tuần <paramref name="monday"/>:
        /// SANG 3 lựa chọn (LC1 bánh mì + sữa, LC2 bánh bao + sữa, LC3 bánh chưng + sữa), TRUA 1 lựa chọn (cơm) – mọi ngày.
        /// </summary>
        private async Task<Scenario> SeedAsync(string code, DateTime monday, int weeks = 1)
        {
            Guid customer = Guid.NewGuid(), locA = Guid.NewGuid(), locB = Guid.NewGuid(), contract = Guid.NewGuid(), sang = Guid.NewGuid(), trua = Guid.NewGuid(), menu = Guid.NewGuid();
            Guid banhMi = Guid.NewGuid(), banhBao = Guid.NewGuid(), banhChung = Guid.NewGuid(), sua = Guid.NewGuid(), com = Guid.NewGuid();
            await _factory.SeedAsync(async db =>
            {
                foreach (var shift in new[] { "TRUA", "SANG_6H" })
                {
                    if (!await db.Shift.AnyAsync(s => s.Code == shift))
                    {
                        db.Shift.Add(new Shift { Id = Guid.NewGuid(), Code = shift, Name = "Ca " + shift });
                    }
                }
                db.Customer.Add(new Customer { Id = customer, Code = code, Name = "Khách " + code, CIF = code, CustomerType = "BUSINESS", DisplayName = code, Status = "Active" });
                db.CustomerDeliveryLocation.AddRange(
                    new CustomerDeliveryLocation { Id = locA, CustomerId = customer, Code = code + "-A", Name = "Nhà máy A" },
                    new CustomerDeliveryLocation { Id = locB, CustomerId = customer, Code = code + "-B", Name = "Nhà máy B" });
                db.Contract.Add(new Contract { Id = contract, CustomerId = customer, ContractNumber = "HD-" + code, Status = "Active", EffectiveFrom = new DateTime(2026, 1, 1) });
                db.ContractMeal.AddRange(
                    new ContractMeal { Id = sang, ContractId = contract, MealType = "SANG", MealName = "Suất sáng", UnitPrice = 20000 },
                    new ContractMeal { Id = trua, ContractId = contract, MealType = "TRUA", MealName = "Suất trưa", UnitPrice = 40000 });
                foreach (var (id, name) in new[] { (banhMi, "Bánh mì Hữu Nghị"), (banhBao, "Bánh bao"), (banhChung, "Bánh chưng"), (sua, "Sữa Fami"), (com, "Cơm trắng") })
                {
                    db.Dish.Add(new Dish { Id = id, Code = code + "-" + name.GetHashCode().ToString("X"), Name = name });
                }
                db.Menu.Add(new Model.Entity.Menu
                {
                    Id = menu, Code = "NU-" + code, Name = "Thực đơn chung " + code, VersionNo = 1, Status = "Published", MenuType = "COMMON",
                    EffectiveFrom = monday, EffectiveTo = monday.AddDays(7 * weeks - 1),
                });
                for (int day = 1; day <= 7; day++)
                {
                    int sort = 0;
                    foreach (var (option, dish) in new[] { (1, banhMi), (1, sua), (2, banhBao), (2, sua), (3, banhChung), (3, sua) })
                    {
                        db.MenuLine.Add(new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = dish, MealType = "SANG", SequenceNo = option, DayOfWeek = day, SortOrder = sort++, PortionQty = 1 });
                    }
                    db.MenuLine.Add(new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = com, MealType = "TRUA", SequenceNo = 1, DayOfWeek = day, SortOrder = 0, PortionQty = 1 });
                }
            });
            return new Scenario(customer, locA, locB, sang, trua, menu, banhMi, banhBao, banhChung, sua, com);
        }

        private static async Task<LegacyResult> PostJson(HttpClient c, string url, object body) => await LegacyResult.ReadAsync(await c.PostAsJsonAsync(url, body));

        private Task<List<OrderLine>> LinesAsync(Guid orderId) => _factory.QueryAsync(db => db.OrderLine.AsNoTracking().Where(l => l.OrderId == orderId && !l.IsDeleted).ToListAsync());

        [Fact]
        public async Task Order_lines_by_option_use_common_menu_and_reject_unknown_or_duplicate_options()
        {
            var monday = new DateTime(2026, 12, 14);
            var s = await SeedAsync("OPT-ORD", monday);
            using var c = _factory.CreateAuthorizedClient();

            var resolved = (await c.GetResultAsync($"/api/MenuAssignment/Resolve?customerId={s.Customer}&deliveryLocationId={s.LocA}&mealType=SANG&date=2026-12-14")).Ok();
            resolved.GetProperty("level").GetString().Should().Be("COMMON", "khách không có phân công riêng → thực đơn chung");
            resolved.GetProperty("menuId").GetGuid().Should().Be(s.Menu);

            var template = (await c.GetResultAsync($"/api/Order/GetNewTemplate?customerId={s.Customer}&mealDate=2026-12-14")).Ok();
            var sangLine = template.GetProperty("listLocation")[0].GetProperty("listLine").EnumerateArray().Single(l => l.GetProperty("mealType").GetString() == "SANG");
            var options = sangLine.GetProperty("listOption").EnumerateArray().ToList();
            options.Select(o => o.GetProperty("optionNo").GetInt32()).Should().Equal(1, 2, 3);
            options[0].GetProperty("dishText").GetString().Should().Be("Bánh mì Hữu Nghị · Sữa Fami");

            var created = (await PostJson(c, "/api/Order/Create", new
            {
                customerId = s.Customer, deliveryLocationId = s.LocA, mealDate = monday,
                listLine = new object[]
                {
                    new { contractMealId = s.MealSang, menuOptionNo = 1, quantity = 2000 },
                    new { contractMealId = s.MealSang, menuOptionNo = 2, quantity = 1500 },
                    new { contractMealId = s.MealSang, menuOptionNo = 3, quantity = 1000 },
                    new { contractMealId = s.MealTrua, quantity = 4500 },
                },
            })).Ok();
            Guid orderId = created.GetProperty("order").GetProperty("id").GetGuid();
            created.GetProperty("order").GetProperty("totalQuantity").GetDecimal().Should().Be(9000);
            created.GetProperty("order").GetProperty("totalAmount").GetDecimal().Should().Be(4500 * 20000m + 4500 * 40000m, "mọi lựa chọn cùng buổi cùng giá");
            var lines = await LinesAsync(orderId);
            lines.Where(l => l.MealType == "SANG").Select(l => l.MenuOptionNo).Should().BeEquivalentTo(new int?[] { 1, 2, 3 });
            lines.Single(l => l.MealType == "TRUA").MenuOptionNo.Should().Be(1, "buổi chỉ 1 lựa chọn → 1");
            lines.Should().OnlyContain(l => l.MenuId == s.Menu);
            var detailLines = created.GetProperty("listLine").EnumerateArray().ToList();
            detailLines.First(l => l.GetProperty("menuOptionNo").ValueKind == JsonValueKind.Number && l.GetProperty("menuOptionNo").GetInt32() == 2)
                .GetProperty("optionDishText").GetString().Should().Be("Bánh bao · Sữa Fami");

            (await PostJson(c, "/api/Order/Create", new
            {
                customerId = s.Customer, deliveryLocationId = s.LocB, mealDate = monday,
                listLine = new object[] { new { contractMealId = s.MealSang, menuOptionNo = 5, quantity = 10 } },
            })).Fail("Lựa chọn 5 không có trong thực đơn");
            (await PostJson(c, "/api/Order/Create", new
            {
                customerId = s.Customer, deliveryLocationId = s.LocB, mealDate = monday,
                listLine = new object[] { new { contractMealId = s.MealSang, menuOptionNo = 1, quantity = 10 }, new { contractMealId = s.MealSang, menuOptionNo = 1, quantity = 5 } },
            })).Fail("Trùng dòng suất");
        }

        [Fact]
        public async Task Publishing_a_second_common_menu_with_overlapping_dates_is_blocked()
        {
            var monday = new DateTime(2027, 1, 4);
            var s = await SeedAsync("OPT-PUB", monday);
            Guid second = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Menu.Add(new Model.Entity.Menu { Id = second, Code = "NU-OPT-PUB-2", Name = "Chung 2", VersionNo = 1, Status = "Approved", MenuType = "COMMON", EffectiveFrom = monday.AddDays(3), EffectiveTo = monday.AddDays(9) });
                for (int day = 1; day <= 7; day++)
                {
                    db.MenuLine.Add(new MenuLine { Id = Guid.NewGuid(), MenuId = second, DishId = s.Com, MealType = "TRUA", SequenceNo = 1, DayOfWeek = day, PortionQty = 1 });
                }
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();
            (await c.PostFormAsync("/api/Menu/Publish", ("Id", second))).Fail("thực đơn chung NU-OPT-PUB");
        }

        [Fact]
        public void Option_line_uses_exact_dishes_of_the_option_and_null_line_keeps_even_split()
        {
            Guid menu = Guid.NewGuid(), a = Guid.NewGuid(), b = Guid.NewGuid(), milk = Guid.NewGuid();
            var menuLines = new List<MenuLine>
            {
                new() { MenuId = menu, MealType = "SANG", SequenceNo = 1, DishId = a, PortionQty = 2 },
                new() { MenuId = menu, MealType = "SANG", SequenceNo = 1, DishId = milk },
                new() { MenuId = menu, MealType = "SANG", SequenceNo = 2, DishId = b },
                new() { MenuId = menu, MealType = "SANG", SequenceNo = 2, DishId = milk },
            };
            var rules = new BusinessRulesOptions { MrpOptionBufferPct = 15 };
            var byOption = ProductionRules.LinePortions(new OrderLine { MenuId = menu, MealType = "SANG", MenuOptionNo = 1, Quantity = 2000 }, menuLines, rules, out bool missing);
            missing.Should().BeFalse();
            byOption.Should().HaveCount(2);
            byOption.Single(p => p.DishId == a).Quantity.Should().Be(4000, "LC1 2.000 suất × 2 cái / suất (PortionQty) → 4.000 bánh mì, không chia đều");
            byOption.Single(p => p.DishId == milk).Quantity.Should().Be(2000);

            var legacy = ProductionRules.LinePortions(new OrderLine { MenuId = menu, MealType = "SANG", Quantity = 1000 }, menuLines, rules, out _);
            legacy.Single(p => p.DishId == a).Quantity.Should().Be(575, "dòng cũ NULL: chia đều 500 + 15% dự phòng (GAP-MRP-03 không đổi)");
            legacy.Single(p => p.DishId == milk).Quantity.Should().Be(1000, "món chung mọi lựa chọn không vượt số suất dòng");

            rules.MrpOptionQtyBufferPct = 5;
            ProductionRules.LinePortions(new OrderLine { MenuId = menu, MealType = "SANG", MenuOptionNo = 2, Quantity = 1000 }, menuLines, rules, out _)
                .Single(p => p.DishId == b).Quantity.Should().Be(1050, "MrpOptionQtyBufferPct cấu hình được");

            ProductionRules.LinePortions(new OrderLine { MenuId = menu, MealType = "SANG", MenuOptionNo = 3, Quantity = 1000 }, menuLines, rules, out bool gone);
            gone.Should().BeTrue("lựa chọn không còn trong thực đơn → báo + tính cách cũ");
        }

        [Fact]
        public async Task Week_save_confirm_creates_seven_orders_and_production_demand_follows_options_then_copy_and_slip_round_trip()
        {
            var monday = new DateTime(2026, 12, 21);
            var s = await SeedAsync("OPT-WEEK", monday, weeks: 2);
            using var c = _factory.CreateAuthorizedClient();
            string query = $"customerId={s.Customer}&deliveryLocationId={s.LocA}&weekStart=2026-12-23";

            var week = (await c.GetResultAsync("/api/Order/GetWeek?" + query)).Ok();
            week.GetProperty("weekStart").GetDateTime().Should().Be(monday, "ngày bất kỳ trong tuần → thứ Hai");
            var rows = week.GetProperty("listRow").EnumerateArray().ToList();
            rows.Should().HaveCount(4, "Sáng 3 lựa chọn + Trưa 1");
            rows.Take(3).Select(r => r.GetProperty("menuOptionNo").GetInt32()).Should().Equal(1, 2, 3);
            rows[0].GetProperty("listCell")[0].GetProperty("dishText").GetString().Should().Be("Bánh mì Hữu Nghị · Sữa Fami");
            rows[0].GetProperty("listCell")[0].GetProperty("menuLevel").GetString().Should().Be("COMMON");

            var cells = new List<object>();
            for (int i = 0; i < 7; i++)
            {
                DateTime d = monday.AddDays(i);
                cells.Add(new { mealDate = d, contractMealId = s.MealSang, menuOptionNo = 1, quantity = 2000 });
                cells.Add(new { mealDate = d, contractMealId = s.MealSang, menuOptionNo = 2, quantity = 1500 });
                cells.Add(new { mealDate = d, contractMealId = s.MealSang, menuOptionNo = 3, quantity = i == 6 ? 0 : 1000 });
                cells.Add(new { mealDate = d, contractMealId = s.MealTrua, menuOptionNo = 1, quantity = 4500 });
            }
            var saved = (await PostJson(c, "/api/Order/SaveWeek", new { customerId = s.Customer, deliveryLocationId = s.LocA, weekStart = monday, confirm = true, listCell = cells })).Ok();
            saved.GetProperty("created").GetInt32().Should().Be(7, "mỗi ngày 1 đơn");
            saved.GetProperty("confirmed").GetInt32().Should().Be(7);
            var orders = await _factory.QueryAsync(db => db.Order.AsNoTracking().Where(o => o.CustomerId == s.Customer && o.DeliveryLocationId == s.LocA).ToListAsync());
            orders.Should().HaveCount(7).And.OnlyContain(o => o.Status == "Confirmed");
            orders.Single(o => o.MealDate == monday).TotalQuantity.Should().Be(9000);
            orders.Single(o => o.MealDate == monday.AddDays(6)).TotalQuantity.Should().Be(8000, "ô 0 = không đặt");

            // Sửa thứ Hai: LC3 1000 → 1100 (≤ 30% không cần duyệt) – chỉ ngày đó đổi.
            var update = (await PostJson(c, "/api/Order/SaveWeek", new
            {
                customerId = s.Customer, deliveryLocationId = s.LocA, weekStart = monday,
                listCell = new object[] { new { mealDate = monday, contractMealId = s.MealSang, menuOptionNo = 3, quantity = 1100 } },
            })).Ok();
            update.GetProperty("updated").GetInt32().Should().Be(1);
            (await LinesAsync(orders.Single(o => o.MealDate == monday).Id)).Single(l => l.MenuOptionNo == 3 && l.MealType == "SANG").Quantity.Should().Be(1100);

            // Kế hoạch SX: số phần món = suất lựa chọn × món trong set (không chia đều, không dự phòng).
            var demand = await _factory.QueryAsync(db => ProductionRules.DishDemandsAsync(db, monday, ["Confirmed"], new BusinessRulesOptions()));
            decimal Portions(Guid dish) => demand.Demands.Where(d => d.DishId == dish && demand.Orders.Any(o => o.Id == d.OrderId && o.CustomerId == s.Customer)).Sum(d => d.Quantity);
            Portions(s.BanhMi).Should().Be(2000, "2.000 suất LC1 → 2.000 bánh mì");
            Portions(s.BanhBao).Should().Be(1500);
            Portions(s.BanhChung).Should().Be(1100);
            Portions(s.Sua).Should().Be(4600, "sữa có trong mọi lựa chọn → cộng dồn");
            Portions(s.Com).Should().Be(4500);

            // Chép số tuần trước cho tuần sau (chỉ trả về).
            var next = (await c.GetResultAsync($"/api/Order/CopyPreviousWeek?customerId={s.Customer}&deliveryLocationId={s.LocA}&weekStart={monday.AddDays(7):yyyy-MM-dd}")).Ok();
            var nextRows = next.GetProperty("listRow").EnumerateArray().ToList();
            nextRows[0].GetProperty("listCell")[0].GetProperty("quantity").GetDecimal().Should().Be(2000, "chép LC1 thứ Hai tuần trước");
            nextRows[2].GetProperty("listCell")[0].GetProperty("quantity").GetDecimal().Should().Be(1100);
            (await _factory.QueryAsync(db => db.Order.CountAsync(o => o.CustomerId == s.Customer && o.MealDate >= monday.AddDays(7)))).Should().Be(0, "chép chỉ trả gợi ý, không lưu");

            // Phiếu đăng ký: tải xuống, điền LC2 thứ Ba = 1600, tải lên → đọc đúng ô, chưa lưu.
            var file = await c.GetByteArrayAsync("/api/Order/ExportWeekSlip?" + query);
            using var input = new MemoryStream(file);
            using var wb = new XLWorkbook(input);
            var ws = wb.Worksheet(1);
            ws.Cell(1, 1).GetString().Should().StartWith("DELICARE_SLIP|");
            int qtyRow = Enumerable.Range(3, ws.LastRowUsed()!.RowNumber()).First(r => ws.Cell(r, 10).GetString().StartsWith(s.MealSang.ToString("D")) && ws.Cell(r, 10).GetString().EndsWith("|2"));
            ws.Cell(qtyRow, 4).Value = 1600;
            using var output = new MemoryStream();
            wb.SaveAs(output);
            using var form = new MultipartFormDataContent
            {
                { new StringContent(s.Customer.ToString()), "CustomerId" },
                { new StringContent(s.LocA.ToString()), "DeliveryLocationId" },
                { new StringContent("2026-12-21"), "WeekStart" },
                { new ByteArrayContent(output.ToArray()), "File", "phieu.xlsx" },
            };
            var slip = (await LegacyResult.ReadAsync(await c.PostAsync("/api/Order/ImportWeekSlip", form))).Ok();
            var slipCells = slip.GetProperty("listCell").EnumerateArray().ToList();
            slipCells.Should().Contain(x => x.GetProperty("mealDate").GetDateTime() == monday.AddDays(1) && x.GetProperty("menuOptionNo").GetInt32() == 2 && x.GetProperty("quantity").GetDecimal() == 1600);
            slip.GetProperty("listError").GetArrayLength().Should().Be(0);
            (await LinesAsync(orders.Single(o => o.MealDate == monday.AddDays(1)).Id)).Single(l => l.MenuOptionNo == 2 && l.MealType == "SANG").Quantity.Should().Be(1500, "tải phiếu lên chưa lưu");

            // Phiếu của điểm giao khác → từ chối.
            using var wrong = new MultipartFormDataContent
            {
                { new StringContent(s.Customer.ToString()), "CustomerId" },
                { new StringContent(s.LocB.ToString()), "DeliveryLocationId" },
                { new StringContent("2026-12-21"), "WeekStart" },
                { new ByteArrayContent(output.ToArray()), "File", "phieu.xlsx" },
            };
            (await LegacyResult.ReadAsync(await c.PostAsync("/api/Order/ImportWeekSlip", wrong))).Fail("Phiếu không đúng");
        }
    }
}
