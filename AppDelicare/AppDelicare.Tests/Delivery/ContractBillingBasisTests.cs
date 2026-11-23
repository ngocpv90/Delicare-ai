using System.Net.Http.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Services.Implement;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Delivery
{
    /// <summary>
    /// D-78 (2) – GAP-BIZ-01: cách tính tiền theo hợp đồng (Contract.ConfigJson). Hợp đồng "Theo suất ăn thực tế" (SERVED): khách nhận 100, phục vụ 95 → tính 95;
    /// hợp đồng không cấu hình → theo cấu hình chung RECEIVED (100 − lỗi). Tạo hợp đồng kèm cách tính, đổi cách tính, giá trị sai bị chặn.
    /// </summary>
    public class ContractBillingBasisTests(InMemoryApiFactory factory) : IClassFixture<InMemoryApiFactory>
    {
        [Fact]
        public void Config_json_reads_and_writes_billing_basis_keeping_other_keys()
        {
            ContractConfig.BillingBasisOf(null).Should().BeNull();
            ContractConfig.WithBillingBasis(null, "served").Should().Be("{\"billingBasis\":\"SERVED\"}");
            ContractConfig.WithBillingBasis("{\"x\":1,\"billingBasis\":\"SERVED\"}", null).Should().Be("{\"x\":1}");
            ContractConfig.BillingBasisOf("{\"billingBasis\":\"ORDERED\"}").Should().Be("ORDERED");
            ContractConfig.BillingBasisOf("{\"billingBasis\":\"ABC\"}").Should().BeNull();
        }

        private async Task<(Guid Order, Guid Contract)> SeedAsync(string k, string? config)
        {
            Guid customer = Guid.NewGuid(), contract = Guid.NewGuid(), order = Guid.NewGuid(), delivery = Guid.NewGuid();
            DateTime day = new(2032, 8, 3);
            await factory.SeedAsync(db =>
            {
                db.Customer.Add(new Customer { Id = customer, Code = "CBB" + k, Name = "Khách " + k, DisplayName = "Khách " + k, CIF = "CBB" + k, CustomerType = "FACTORY", Status = "Active" });
                db.Contract.Add(new Contract { Id = contract, CustomerId = customer, ContractNumber = "HD-" + k, Status = "Active", EffectiveFrom = new DateTime(2032, 1, 1), ConfigJson = config });
                db.Order.Add(new Order { Id = order, OrderNumber = "DH-" + k, CustomerId = customer, ContractId = contract, OrderDate = day.AddDays(-1), MealDate = day, DeliveryDate = day, Status = "Delivered", TotalQuantity = 100, TotalAmount = 4_000_000 });
                db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = order, MealType = "TRUA", Quantity = 100, UnitPrice = 40000, LineAmount = 4_000_000 });
                db.DeliveryOrder.Add(new DeliveryOrder { Id = delivery, OrderId = order, CustomerId = customer, DeliveryDate = day, PlannedDeliveryTime = day.AddHours(11), Status = "Delivered" });
                db.ProofOfDelivery.Add(new ProofOfDelivery { Id = Guid.NewGuid(), DeliveryOrderId = delivery, DeliveredQuantity = 100, AcceptedQuantity = 100, ConfirmedAt = day.AddHours(11) });
                return Task.CompletedTask;
            });
            return (order, contract);
        }

        [Fact]
        public async Task Reconciliation_uses_contract_billing_basis_and_contract_api_updates_it()
        {
            factory.Clock.FixedNow = new DateTime(2032, 8, 3, 15, 0, 0);
            using var c = factory.CreateAuthorizedClient();
            var served = await SeedAsync(Guid.NewGuid().ToString("N")[..5], "{\"billingBasis\":\"SERVED\"}");
            var r1 = (await c.PostFormAsync("/api/OrderReconciliation/Reconcile", ("OrderId", served.Order), ("ServedQuantity", 95m))).Ok();
            (r1.GetProperty("billingBasis").GetString(), r1.GetProperty("billingQuantity").GetDecimal()).Should().Be(("SERVED", 95m));

            var global = await SeedAsync(Guid.NewGuid().ToString("N")[..5], null);
            var r2 = (await c.PostFormAsync("/api/OrderReconciliation/Reconcile", ("OrderId", global.Order), ("ServedQuantity", 95m))).Ok();
            (r2.GetProperty("billingBasis").GetString(), r2.GetProperty("billingQuantity").GetDecimal()).Should().Be(("RECEIVED", 100m), "không cấu hình → theo cấu hình chung");

            // Đổi cách tính của hợp đồng; giá trị sai bị chặn; để trống = theo cấu hình chung.
            var updated = (await c.PutFormAsync("/api/Contract/UpdateBillingBasis", ("Id", global.Contract), ("BillingBasis", "ORDERED"))).Ok();
            updated.GetProperty("billingBasis").GetString().Should().Be("ORDERED");
            (await c.PutFormAsync("/api/Contract/UpdateBillingBasis", ("Id", global.Contract), ("BillingBasis", "ABC"))).Fail("không hợp lệ");
            (await c.PutFormAsync("/api/Contract/UpdateBillingBasis", ("Id", global.Contract), ("BillingBasis", ""))).Ok().GetProperty("billingBasisName").GetString().Should().Be("Theo cấu hình chung");
            (await factory.QueryAsync(db => db.Contract.AsNoTracking().SingleAsync(x => x.Id == global.Contract))).ConfigJson.Should().BeNull();
        }
    }
}
