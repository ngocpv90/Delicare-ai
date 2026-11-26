using System.Net.Http.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Services.Implement;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Accounting
{
    /// <summary>
    /// ACC-5 (D-131 i): trả hàng NCC – số trả ≤ tồn còn trả được của lô, lý do bắt buộc, lô tạm giữ; Mua hàng xác nhận (không nhận → ghi chú bắt buộc,
    /// lô bỏ giữ); ghi sổ RETURN (giá lô) → tồn giảm, lô bỏ giữ, chứng từ "Trả lại hàng mua" đối tượng NCC; màn Sự cố NCC không đổi trạng thái phiếu trả;
    /// kỳ kế toán đã khóa chặn ghi sổ.
    /// </summary>
    public class SupplierReturnTests
    {
        [Fact]
        public async Task Return_holds_lot_needs_supplier_confirm_and_posts_ledger_and_voucher()
        {
            using var f = new InMemoryApiFactory();
            f.Clock.FixedNow = new DateTime(2034, 3, 12, 9, 0, 0);
            Guid wh = Guid.NewGuid(), ing = Guid.NewGuid(), unit = Guid.NewGuid(), sup = Guid.NewGuid(), lot = Guid.NewGuid();
            await f.SeedAsync(db =>
            {
                db.Warehouse.Add(new Warehouse { Id = wh, Code = "RKHO", Name = "Kho R", Status = "Active" });
                db.UnitMeasure.Add(new UnitMeasure { Id = unit, Code = "RKG", Name = "kg", Status = "Active" });
                db.Ingredient.Add(new Ingredient { Id = ing, Code = "RNL1", Name = "Cá basa", BaseUnitId = unit, Status = "Active" });
                db.Supplier.Add(new Supplier { Id = sup, Code = "NCC-R1", Name = "NCC cá", Status = "Active" });
                db.MaterialLot.Add(new MaterialLot { Id = lot, LotNo = "LOT-R1", LotNumber = "LOT-R1", IngredientId = ing, SupplierId = sup, UnitCost = 20000, Status = "Available", ReceivedDate = new DateTime(2034, 3, 10) });
                db.InventoryTransaction.Add(new InventoryTransaction
                {
                    Id = Guid.NewGuid(), TransactionNumber = "NK-R-1", TransactionType = "RECEIPT", WarehouseId = wh, IngredientId = ing, MaterialLotId = lot, LotId = lot, Quantity = 10, QuantityIn = 10,
                    UomId = unit, UnitId = unit, UnitCost = 20000, TransactionDate = new DateTime(2034, 3, 10),
                });
                db.InventoryBalance.Add(new InventoryBalance
                {
                    Id = Guid.NewGuid(), WarehouseId = wh, IngredientId = ing, MaterialLotId = lot, LotId = lot, UnitId = unit, OnHandQuantity = 10, OnHandQty = 10, AvailableQuantity = 10, AvailableQty = 10,
                    UpdatedDate = new DateTime(2034, 3, 10), LastCalculatedDate = new DateTime(2034, 3, 10),
                });
                return Task.CompletedTask;
            });
            using var c = f.CreateAuthorizedClient();
            async Task<LegacyResult> Post(string action, object body) => await LegacyResult.ReadAsync(await c.PostAsJsonAsync($"/api/SupplierReturn/{action}", body));
            Task<string?> LotStatus() => f.QueryAsync(db => db.MaterialLot.AsNoTracking().Where(l => l.Id == lot).Select(l => l.Status).SingleAsync());

            var lots = (await c.GetResultAsync("/api/SupplierReturn/GetLots?search=LOT-R1")).Ok().EnumerateArray().ToList();
            lots.Should().ContainSingle().Which.GetProperty("onHand").GetDecimal().Should().Be(10);

            (await Post("Create", new { materialLotId = lot, quantity = 4 })).Fail("lý do");
            (await Post("Create", new { materialLotId = lot, quantity = 12, reason = "DAMAGED" })).Fail("vượt tồn");
            var created = (await Post("Create", new { materialLotId = lot, quantity = 4, reason = "DAMAGED", note = "cá ươn" })).Ok();
            Guid id = created.GetProperty("id").GetGuid();
            created.GetProperty("state").GetString().Should().Be("RETURN_PENDING");
            created.GetProperty("number").GetString().Should().StartWith("TH-340312-");
            created.GetProperty("amount").GetDecimal().Should().Be(80000);
            (await LotStatus()).Should().Be("OnHold");
            (await Post("Create", new { materialLotId = lot, quantity = 7, reason = "SPEC" })).Fail("tối đa 6");

            // Màn Sự cố NCC không được đổi trạng thái phiếu trả.
            (await c.PostFormAsync("/api/SupplierIssue/Transition", ("Id", id), ("To", "InProgress"))).Fail("Trả hàng NCC");

            (await Post("Post", new { id })).Fail("Chờ NCC xác nhận");
            (await Post("Confirm", new { id })).Fail("đồng ý");
            (await Post("Confirm", new { id, agree = true })).Ok().GetProperty("state").GetString().Should().Be("RETURN_AGREED");
            var posted = (await Post("Post", new { id, note = "NCC đã nhận" })).Ok();
            posted.GetProperty("state").GetString().Should().Be("RETURN_POSTED");
            posted.GetProperty("listHistory").GetArrayLength().Should().Be(3);
            (await f.QueryAsync(db => db.InventoryBalance.AsNoTracking().Where(b => b.MaterialLotId == lot).SumAsync(b => b.OnHandQuantity))).Should().Be(6);
            var txn = await f.QueryAsync(db => db.InventoryTransaction.AsNoTracking().SingleAsync(t => t.TransactionType == "RETURN"));
            txn.ReferenceType.Should().Be(SupplierReturnService.ReferenceType);
            txn.QuantityOut.Should().Be(4);
            txn.UnitCost.Should().Be(20000);
            (await LotStatus()).Should().Be("Available");

            var vouchers = (await c.GetResultAsync("/api/AccountingVoucher/GetMonth?month=2034-03&tab=purchase")).Ok().GetProperty("listVoucher").EnumerateArray().ToList();
            var v = vouchers.Single(x => x.GetProperty("type").GetString() == "PURCHASE_RETURN");
            v.GetProperty("objectCode").GetString().Should().Be("NCC-R1");
            v.GetProperty("amount").GetDecimal().Should().Be(80000);
            (await c.GetResultAsync("/api/AccountingVoucher/GetMonth?month=2034-03&tab=stock")).Ok().GetProperty("listVoucher").EnumerateArray()
                .Should().NotContain(x => x.GetProperty("type").GetString() == "STOCK_OTHER", "trả hàng NCC không lặp ở Kho khác");

            // NCC không nhận → ghi chú bắt buộc, lô bỏ giữ.
            Guid id2 = (await Post("Create", new { materialLotId = lot, quantity = 2, reason = "EXPIRY" })).Ok().GetProperty("id").GetGuid();
            (await LotStatus()).Should().Be("OnHold");
            (await Post("Confirm", new { id = id2, agree = false })).Fail("lý do");
            (await Post("Confirm", new { id = id2, agree = false, note = "quá 3 ngày" })).Ok().GetProperty("state").GetString().Should().Be("RETURN_REFUSED");
            (await LotStatus()).Should().Be("Available");
            (await Post("Cancel", new { id = id2, note = "x" })).Fail("NCC không nhận");

            // Kỳ đã khóa → chặn ghi sổ.
            Guid id3 = (await Post("Create", new { materialLotId = lot, quantity = 1, reason = "EXCESS" })).Ok().GetProperty("id").GetGuid();
            (await Post("Confirm", new { id = id3, agree = true })).Ok();
            await f.SeedAsync(db =>
            {
                db.AuditChange.Add(new AuditChange { Id = Guid.NewGuid(), IdChange = AccountingPeriodRules.PeriodId(2034, 3), EntityName = AccountingPeriodRules.Entity, Action = AccountingPeriodRules.ActionLock, CreatedDate = f.Clock.FixedNow!.Value });
                return Task.CompletedTask;
            });
            (await Post("Post", new { id = id3 })).Fail("đã khóa");
            var list = (await c.GetResultAsync("/api/SupplierReturn/GetList?state=all&month=2034-03")).Ok();
            list.GetProperty("agreed").GetInt32().Should().Be(1);
            list.GetProperty("postedThisMonth").GetInt32().Should().Be(1);
            list.GetProperty("listData").GetArrayLength().Should().Be(3);
        }
    }
}
