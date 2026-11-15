using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Data.Connection;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Services.Implement;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using AppDelicare.WebApi.Api;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Ledger
{
    /// <summary>T08 – sổ chỉ ghi thêm bị chặn sửa / xóa; bút toán đảo đưa tồn về đúng.</summary>
    public class AppendOnlyAndLedgerTests : IClassFixture<InMemoryApiFactory>
    {
        private static readonly Guid Actor = Guid.NewGuid();
        private static readonly Guid Warehouse = Guid.NewGuid();
        private static readonly Guid Unit = Guid.NewGuid();

        private readonly InMemoryApiFactory _factory;

        public AppendOnlyAndLedgerTests(InMemoryApiFactory factory) => _factory = factory;

        private static (DatabaseContext Db, IDisposable Scope) NewDb()
        {
            var actor = new CurrentActor(new HttpContextAccessor());
            var scope = actor.BeginScope(ActorInfo.ForSystem(Actor));
            var options = new DbContextOptionsBuilder<DatabaseContext>().UseInMemoryDatabase("t08-" + Guid.NewGuid()).Options;
            return (new DatabaseContext(options, actor), scope);
        }

        private static InventoryPostRequest Receipt(Guid ingredient, decimal qty) => new()
        {
            TransactionType = "RECEIPT", TransactionNumber = "PN-" + qty, WarehouseId = Warehouse, IngredientId = ingredient, UnitId = Unit, QuantityIn = qty, UnitCost = 1000,
        };

        private static InventoryPostRequest Issue(Guid ingredient, decimal qty) => new()
        {
            TransactionType = "ISSUE", TransactionNumber = "PX-" + qty, WarehouseId = Warehouse, IngredientId = ingredient, UnitId = Unit, QuantityOut = qty,
        };

        private static Task<InventoryBalance> BalanceAsync(DatabaseContext db, Guid ingredient)
            => db.InventoryBalance.AsNoTracking().SingleAsync(b => b.IngredientId == ingredient);

        [Fact]
        public void Every_ledger_in_claude_md_is_append_only()
        {
            DatabaseContext.AppendOnlyTypes.Select(t => t.Name).Should().BeEquivalentTo(
                ["InventoryTransaction", "PackagingLedger", "TemperatureLog", "AuditChange", "IntegrationLog",
                 "ApprovalDecision", "AttendanceEvent", "DeliveryEvent", "ComplaintHistory"]);
        }

        [Fact]
        public async Task Updating_a_ledger_row_is_blocked_on_every_save_path()
        {
            var (db, scope) = NewDb();
            using (scope)
            {
                var ledger = new InventoryLedgerService(db);
                var txn = await ledger.PostAsync(Receipt(Guid.NewGuid(), 10));

                txn.Note = "sửa lén";
                Func<Task> typed = () => db.SaveChangesAsync(ConstantHelper.TypeSave.Update);
                await typed.Should().ThrowAsync<AppendOnlyViolationException>();
                Action plain = () => db.SaveChanges();
                plain.Should().Throw<AppendOnlyViolationException>();
                Func<Task> plainAsync = () => db.SaveChangesAsync();
                await plainAsync.Should().ThrowAsync<AppendOnlyViolationException>();
            }
        }

        [Fact]
        public async Task Deleting_or_soft_deleting_a_ledger_row_is_blocked()
        {
            var (db, scope) = NewDb();
            using (scope)
            {
                var log = new AuditChange { Id = Guid.NewGuid(), EntityName = "X", Action = "Added" };
                db.AuditChange.Add(log);
                await db.SaveChangesAsync();

                db.Entry(log).State = EntityState.Modified; // xóa mềm đi qua Modified
                Func<Task> soft = () => db.SaveChangesAsync(ConstantHelper.TypeSave.Delete);
                await soft.Should().ThrowAsync<AppendOnlyViolationException>();

                db.Entry(log).State = EntityState.Deleted;
                Func<Task> hard = () => db.SaveChangesAsync();
                await hard.Should().ThrowAsync<AppendOnlyViolationException>();
            }
        }

        [Fact]
        public async Task Non_ledger_tables_can_still_be_updated()
        {
            var (db, scope) = NewDb();
            using (scope)
            {
                var shift = new Shift { Id = Guid.NewGuid(), Code = "S", Name = "S" };
                db.Shift.Add(shift);
                await db.SaveChangesAsync(ConstantHelper.TypeSave.Insert);
                shift.Name = "S2";
                await db.SaveChangesAsync(ConstantHelper.TypeSave.Update);
                (await db.Shift.AsNoTracking().SingleAsync()).Name.Should().Be("S2");
            }
        }

        [Fact]
        public async Task Reversal_returns_stock_to_the_previous_level_and_links_to_the_original()
        {
            var (db, scope) = NewDb();
            using (scope)
            {
                var ledger = new InventoryLedgerService(db);
                var ingredient = Guid.NewGuid();
                await ledger.PostAsync(Receipt(ingredient, 120));
                var issue = await ledger.PostAsync(Issue(ingredient, 20));
                (await BalanceAsync(db, ingredient)).OnHandQuantity.Should().Be(100);

                var reversal = await ledger.ReverseAsync(issue.Id, "Xuất nhầm");

                var balance = await BalanceAsync(db, ingredient);
                balance.OnHandQuantity.Should().Be(120);
                balance.OnHandQty.Should().Be(120, "cột deprecated NOT NULL ghi song song");
                balance.AvailableQuantity.Should().Be(120);
                reversal.Quantity.Should().Be(20);
                reversal.QuantityIn.Should().Be(20);
                reversal.ReferenceType.Should().Be(InventoryLedgerService.ReversalReferenceType);
                reversal.ReferenceId.Should().Be(issue.Id);
                reversal.Reason.Should().Be("Xuất nhầm");
                reversal.UomId.Should().Be(Unit);
                reversal.CreatedBy.Should().Be(Actor);
                (await db.InventoryTransaction.CountAsync()).Should().Be(3, "dòng gốc giữ nguyên, chỉ thêm dòng đảo");
                (await db.InventoryTransaction.AsNoTracking().SingleAsync(x => x.Id == issue.Id)).Quantity.Should().Be(-20);
            }
        }

        [Fact]
        public async Task Cannot_reverse_twice_or_reverse_a_reversal()
        {
            var (db, scope) = NewDb();
            using (scope)
            {
                var ledger = new InventoryLedgerService(db);
                var ingredient = Guid.NewGuid();
                var receipt = await ledger.PostAsync(Receipt(ingredient, 50));
                var reversal = await ledger.ReverseAsync(receipt.Id, "Nhập nhầm");
                (await BalanceAsync(db, ingredient)).OnHandQuantity.Should().Be(0);

                Func<Task> twice = () => ledger.ReverseAsync(receipt.Id, "lần 2");
                (await twice.Should().ThrowAsync<BusinessRuleException>()).Which.StatusCode.Should().Be(409);
                Func<Task> ofReversal = () => ledger.ReverseAsync(reversal.Id, "đảo của đảo");
                (await ofReversal.Should().ThrowAsync<BusinessRuleException>()).Which.Code.Should().Be("INVALID_TRANSITION");
            }
        }

        [Fact]
        public async Task Reversing_a_receipt_already_consumed_is_insufficient_stock_and_issue_beyond_stock_is_blocked()
        {
            var (db, scope) = NewDb();
            using (scope)
            {
                var ledger = new InventoryLedgerService(db);
                var ingredient = Guid.NewGuid();
                var receipt = await ledger.PostAsync(Receipt(ingredient, 30));
                await ledger.PostAsync(Issue(ingredient, 10));

                Func<Task> reverse = () => ledger.ReverseAsync(receipt.Id, "Nhập nhầm");
                (await reverse.Should().ThrowAsync<BusinessRuleException>()).Which.Code.Should().Be("INSUFFICIENT_STOCK");
                Func<Task> overIssue = () => ledger.PostAsync(Issue(ingredient, 21));
                (await overIssue.Should().ThrowAsync<BusinessRuleException>()).Which.StatusCode.Should().Be(422);
                (await BalanceAsync(db, ingredient)).OnHandQuantity.Should().Be(20);
            }
        }

        [Fact]
        public async Task V1_reverse_endpoint_returns_201_then_409_on_second_call()
        {
            var ingredient = Guid.NewGuid();
            Guid receiptId = Guid.Empty;
            await _factory.SeedAsync(async db =>
            {
                var txn = await new InventoryLedgerService(db).PostAsync(Receipt(ingredient, 40));
                receiptId = txn.Id;
            });
            using var client = _factory.CreateAuthorizedClient();

            var first = await client.PostAsJsonAsync($"/api/v1/scm/inventory-transactions/{receiptId}/reverse", new { reason = "Nhập nhầm phiếu" });
            var second = await client.PostAsJsonAsync($"/api/v1/scm/inventory-transactions/{receiptId}/reverse", new { reason = "Lần 2" });
            var noReason = await client.PostAsJsonAsync($"/api/v1/scm/inventory-transactions/{receiptId}/reverse", new { });

            first.StatusCode.Should().Be(HttpStatusCode.Created);
            using (var body = JsonDocument.Parse(await first.Content.ReadAsStringAsync()))
            {
                body.RootElement.GetProperty("data").GetProperty("referenceId").GetGuid().Should().Be(receiptId);
                body.RootElement.GetProperty("data").GetProperty("quantity").GetDecimal().Should().Be(-40);
            }
            second.StatusCode.Should().Be(HttpStatusCode.Conflict);
            using (var body = JsonDocument.Parse(await second.Content.ReadAsStringAsync()))
            {
                body.RootElement.GetProperty("errors")[0].GetProperty("code").GetString().Should().Be(ErrorCodes.InvalidTransition);
            }
            noReason.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Legacy_ledger_controller_has_no_update_or_delete()
        {
            using var client = _factory.CreateAuthorizedClient();

            (await client.PutAsync("/api/InventoryTransaction/Update", new FormUrlEncodedContent([]))).StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
            (await client.DeleteAsync($"/api/InventoryTransaction/Delete?id={Guid.NewGuid()}")).StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
        }
    }
}
