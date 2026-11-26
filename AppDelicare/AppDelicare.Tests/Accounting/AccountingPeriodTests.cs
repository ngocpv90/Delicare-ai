using System.Net.Http.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Services.Implement;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Accounting
{
    /// <summary>
    /// ACC-4 (D-131 h): khóa kỳ – 5 điều kiện (đơn chờ đối soát chặn khóa), kỳ chưa hết không khóa, khóa → chặn đảo bút toán kho trong kỳ,
    /// xin mở khóa: lý do bắt buộc, 1 yêu cầu chờ, COO duyệt → mở; hạn khóa ngày 10 tháng sau.
    /// </summary>
    public class AccountingPeriodTests
    {
        [Fact]
        public async Task Lock_needs_conditions_blocks_changes_and_unlock_needs_approval()
        {
            using var f = new InMemoryApiFactory();
            f.Clock.FixedNow = new DateTime(2033, 7, 6, 9, 0, 0);
            Guid wh = Guid.NewGuid(), ing = Guid.NewGuid(), unit = Guid.NewGuid(), txn = Guid.NewGuid(), cus = Guid.NewGuid(), order = Guid.NewGuid();
            await f.SeedAsync(db =>
            {
                db.Warehouse.Add(new Warehouse { Id = wh, Code = "PKHO", Name = "Kho P", Status = "Active" });
                db.StockCount.Add(new StockCount { Id = Guid.NewGuid(), CountNumber = "KK-P-0630", WarehouseId = wh, CountDate = new DateTime(2033, 6, 30), Status = "Posted" });
                db.InventoryTransaction.Add(new InventoryTransaction
                {
                    Id = txn, TransactionNumber = "PK-P-1", TransactionType = "ISSUE", WarehouseId = wh, IngredientId = ing, Quantity = 1, QuantityOut = 1, UomId = unit, UnitId = unit, UnitCost = 1000, TransactionDate = new DateTime(2033, 6, 15),
                });
                db.Customer.Add(new Customer { Id = cus, Code = "PKH", CIF = "PKH", Name = "Khách P", DisplayName = "Khách P", CustomerType = "BUSINESS", Status = "Active" });
                db.Order.Add(new Order { Id = order, OrderNumber = "DH-P-1", CustomerId = cus, OrderDate = new DateTime(2033, 6, 19), MealDate = new DateTime(2033, 6, 20), DeliveryDate = new DateTime(2033, 6, 20), Status = "Delivered", TotalAmount = 100000 });
                return Task.CompletedTask;
            });
            using var c = f.CreateAuthorizedClient();
            var o = (await c.GetResultAsync("/api/AccountingPeriod/GetOverview?month=2033-06")).Ok();
            o.GetProperty("dueDate").GetDateTime().Should().Be(new DateTime(2033, 7, 10));
            o.GetProperty("canLock").GetBoolean().Should().BeFalse("còn 1 đơn chờ đối soát");
            var recon = o.GetProperty("listCondition").EnumerateArray().Single(x => x.GetProperty("code").GetString() == "RECON");
            recon.GetProperty("ok").GetBoolean().Should().BeFalse();
            (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/AccountingPeriod/Lock", new { month = "2033-06" }))).Fail("chưa xong");

            await f.SeedAsync(async db => { (await db.Order.SingleAsync(x => x.Id == order)).Status = "Cancelled"; });
            (await c.GetResultAsync("/api/AccountingPeriod/GetOverview?month=2033-06")).Ok().GetProperty("canLock").GetBoolean().Should().BeTrue();
            (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/AccountingPeriod/Lock", new { month = "2033-07" }))).Fail("kết thúc");
            (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/AccountingPeriod/Lock", new { month = "2033-06", reason = "đủ điều kiện" }))).Ok();
            (await c.GetResultAsync("/api/AccountingPeriod/GetOverview?month=2033-06")).Ok().GetProperty("locked").GetBoolean().Should().BeTrue();

            // Kỳ đã khóa: đảo bút toán kho có ngày trong kỳ → chặn.
            (await c.PostFormAsync("/api/InventoryTransaction/Reverse", ("Id", txn), ("Reason", "nhập nhầm"))).Fail("đã khóa");

            // Xin mở khóa → COO duyệt → mở.
            (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/AccountingPeriod/RequestUnlock", new { month = "2033-06" }))).Fail("lý do");
            (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/AccountingPeriod/RequestUnlock", new { month = "2033-06", reason = "sửa phiếu xuất" }))).Ok();
            (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/AccountingPeriod/RequestUnlock", new { month = "2033-06", reason = "lần 2" }))).Fail("chờ duyệt");
            var req = await f.QueryAsync(db => db.ApprovalRequest.AsNoTracking().SingleAsync(r => r.BusinessType == AccountingPeriodService.UnlockBusinessType));
            req.BusinessId.Should().Be(AccountingPeriodRules.PeriodId(2033, 6));
            (await c.PostFormAsync("/api/ApprovalRequest/Decide", ("Id", req.Id), ("Approve", true), ("Note", "đồng ý"))).Ok();
            (await c.GetResultAsync("/api/AccountingPeriod/GetOverview?month=2033-06")).Ok().GetProperty("locked").GetBoolean().Should().BeFalse();
        }
    }
}
