using System.Net.Http.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Delivery
{
    /// <summary>
    /// UI-6.8 (D-86 b, c): màn Đối soát – bảng theo kỳ (tab Chờ / Có chênh lệch / Đã chốt, nguồn số nhận), xác nhận nhiều dòng một lần
    /// (phục vụ mặc định = khách nhận; dòng lỗi Delicare thiếu lý do bị chặn riêng), chốt nhiều bản, bot "Đã phục vụ đủ".
    /// </summary>
    public class ReconBoardTests(InMemoryApiFactory factory) : IClassFixture<InMemoryApiFactory>
    {
        private static readonly DateTime Day = new(2032, 9, 6);

        private async Task<(Guid Customer, Guid Match, Guid Short, Guid Third)> SeedAsync(string k)
        {
            Guid customer = Guid.NewGuid(), match = Guid.NewGuid(), shrt = Guid.NewGuid(), third = Guid.NewGuid();
            await factory.SeedAsync(db =>
            {
                db.Customer.Add(new Customer { Id = customer, Code = "RB" + k, Name = "Khách " + k, DisplayName = "Khách " + k, CIF = "RB" + k, CustomerType = "FACTORY", Status = "Active" });
                foreach (var (id, accepted) in new[] { (match, 100m), (shrt, 96m), (third, 100m) })
                {
                    Guid delivery = Guid.NewGuid();
                    db.Order.Add(new Order { Id = id, OrderNumber = "DH-" + k + "-" + accepted + "-" + id.ToString("N")[..3], CustomerId = customer, OrderDate = Day.AddDays(-1), MealDate = Day, DeliveryDate = Day, Status = "Delivered", TotalQuantity = 100, TotalAmount = 4_000_000 });
                    db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = id, MealType = "TRUA", Quantity = 100, UnitPrice = 40000, LineAmount = 4_000_000 });
                    db.DeliveryOrder.Add(new DeliveryOrder { Id = delivery, OrderId = id, CustomerId = customer, DeliveryDate = Day, PlannedDeliveryTime = Day.AddHours(11), Status = "Delivered" });
                    db.ProofOfDelivery.Add(new ProofOfDelivery { Id = Guid.NewGuid(), DeliveryOrderId = delivery, DeliveredQuantity = 100, AcceptedQuantity = accepted, ConfirmedAt = Day.AddHours(11) });
                }
                return Task.CompletedTask;
            });
            return (customer, match, shrt, third);
        }

        [Fact]
        public async Task Board_groups_rows_then_confirm_many_close_many_and_bot_served_full()
        {
            factory.Clock.FixedNow = Day.AddHours(15);
            var s = await SeedAsync(Guid.NewGuid().ToString("N")[..5]);
            using var c = factory.CreateAuthorizedClient();
            async Task<List<System.Text.Json.JsonElement>> Rows()
            {
                var b = (await c.GetResultAsync($"/api/OrderReconciliation/GetBoard?From={Day:yyyy-MM-dd}&To={Day:yyyy-MM-dd}&CustomerId={s.Customer}")).Ok();
                return b.GetProperty("listRow").EnumerateArray().ToList();
            }
            var board = (await c.GetResultAsync($"/api/OrderReconciliation/GetBoard?From={Day:yyyy-MM-dd}&To={Day:yyyy-MM-dd}&CustomerId={s.Customer}")).Ok();
            board.GetProperty("ordered").GetDecimal().Should().Be(300);
            board.GetProperty("accepted").GetDecimal().Should().Be(296);
            board.GetProperty("listChain").GetArrayLength().Should().Be(5);
            board.GetProperty("matchedPendingCount").GetInt32().Should().Be(2, "2 đơn nhận = đặt, chưa xác nhận");
            board.GetProperty("varianceCount").GetInt32().Should().Be(1);
            var rows = board.GetProperty("listRow").EnumerateArray().ToList();
            System.Text.Json.JsonElement Row(List<System.Text.Json.JsonElement> list, Guid id) => list.Single(r => r.GetProperty("orderId").GetGuid() == id);
            Row(rows, s.Short).GetProperty("tab").GetString().Should().Be("VARIANCE");
            Row(rows, s.Match).GetProperty("source").GetString().Should().Be("POD", "chưa có đối soát → số nhận từ bàn giao");
            Row(rows, s.Short).GetProperty("amount").GetDecimal().Should().Be(3_840_000, "96 × 40.000 (nhận − lỗi)");

            // Xác nhận: 1 dòng khớp (phục vụ trống = nhận) + dòng lỗi thiếu lý do (bị chặn riêng, không chặn dòng khác).
            var many = (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/OrderReconciliation/ReconcileMany", new
            {
                listItem = new object[]
                {
                    new { orderId = s.Match },
                    new { orderId = s.Short, delicareFaultQuantity = 2 },
                },
            }))).Ok();
            many.GetProperty("ok").GetInt32().Should().Be(1);
            many.GetProperty("failed").GetInt32().Should().Be(1);
            many.GetProperty("listError")[0].GetProperty("id").GetGuid().Should().Be(s.Short);
            (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/OrderReconciliation/ReconcileMany", new
            {
                listItem = new object[] { new { orderId = s.Short, delicareFaultQuantity = 2, faultReason = "DAMAGED" } },
            }))).Ok().GetProperty("ok").GetInt32().Should().Be(1);

            rows = await Rows();
            Row(rows, s.Match).GetProperty("tab").GetString().Should().Be("CONFIRMED");
            Row(rows, s.Match).GetProperty("source").GetString().Should().Be("WEB");
            Row(rows, s.Short).GetProperty("billed").GetDecimal().Should().Be(94, "nhận 96 − lỗi 2");
            Row(rows, s.Short).GetProperty("faultReason").GetString().Should().Be("DAMAGED");

            // Bot "Đã phục vụ đủ": phục vụ = khách nhận, nguồn bot.
            var bot = (await c.PostFormAsync("/api/OrderReconciliation/ConfirmServedFull", ("orderId", s.Third))).Ok();
            bot.GetProperty("servedQuantity").GetDecimal().Should().Be(100);
            rows = await Rows();
            Row(rows, s.Third).GetProperty("source").GetString().Should().Be("BOT");

            // Chốt nhiều bản: cả 3 đã xác nhận → Đóng, đơn → Đã đối soát.
            var ids = rows.Select(r => r.GetProperty("reconciliationId").GetGuid()).ToArray();
            var closed = (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/OrderReconciliation/CloseMany", new { listReconciliationId = ids, remark = "Chốt cuối ngày" }))).Ok();
            closed.GetProperty("ok").GetInt32().Should().Be(3);
            rows = await Rows();
            rows.Should().OnlyContain(r => r.GetProperty("tab").GetString() == "CLOSED");
            (await factory.QueryAsync(db => db.Order.Where(o => o.CustomerId == s.Customer).Select(o => o.Status).ToListAsync())).Should().OnlyContain(x => x == "Reconciled");
            (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/OrderReconciliation/ReconcileMany", new { listItem = Array.Empty<object>() }))).Fail("Chưa chọn");
        }
    }
}
