using AppDelicare.Data.Connection;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Common
{
    /// <summary>T11 – mã chứng từ: định dạng mọi loại, tăng dần theo dữ liệu đã có (cả cột cũ), hậu tố bắt buộc.</summary>
    public class DocumentNumberTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public DocumentNumberTests(InMemoryApiFactory factory) => _factory = factory;

        private async Task<string> NextAsync(DocumentKind kind, DateTime date, string? suffix = null)
        {
            using var scope = _factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IDocumentNumberService>().NextAsync(kind, date, suffix);
        }

        [Theory]
        [InlineData(DocumentKind.Order, null, "DH-261116-0001")]
        [InlineData(DocumentKind.MenuChange, null, "DM-261116-0001")]
        [InlineData(DocumentKind.PurchaseRequest, null, "MH-261116-0001")]
        [InlineData(DocumentKind.GoodsReceipt, null, "PN-261116-0001")]
        [InlineData(DocumentKind.MaterialIssue, null, "PX-261116-0001")]
        [InlineData(DocumentKind.StockCount, null, "KK-261116-0001")]
        [InlineData(DocumentKind.InventoryTransaction, null, "GD-261116-0001")]
        [InlineData(DocumentKind.ProductionOrder, null, "LS-261116-0001")]
        [InlineData(DocumentKind.PackingOrder, null, "DG-261116-0001")]
        [InlineData(DocumentKind.DeliveryOrder, null, "GH-261116-0001")]
        [InlineData(DocumentKind.ShiftSwap, null, "DC-261116-0001")]
        [InlineData(DocumentKind.Menu, null, "NU-261116-001")]
        [InlineData(DocumentKind.QcInspection, null, "QC-261116-0001")]
        [InlineData(DocumentKind.MaterialRequirement, null, "MR-261116-001")]
        [InlineData(DocumentKind.Contract, null, "HD-261116-001")]
        [InlineData(DocumentKind.Approval, null, "XN-261116-001")]
        [InlineData(DocumentKind.Complaint, null, "PA-261116-0001")]
        [InlineData(DocumentKind.PurchaseOrder, null, "PO-261116-0001")]
        [InlineData(DocumentKind.Leave, null, "DN-261116-0001")]
        [InlineData(DocumentKind.MaterialLot, "ncc-01", "LO-261116-NCC01-01")]
        [InlineData(DocumentKind.ProductionBatch, "TRUA", "ME-261116-TRUA-01")]
        [InlineData(DocumentKind.DeliveryRoute, "29C 123.45", "CX-261116-29C12345-01")]
        [InlineData(DocumentKind.ProductionPlan, null, "KS-261116")]
        [InlineData(DocumentKind.CleaningBatch, "Tối", "VS-261116-TOI")]
        public async Task First_number_of_the_day_has_the_right_format(DocumentKind kind, string? suffix, string expected)
            => (await NextAsync(kind, new DateTime(2026, 11, 16), suffix)).Should().Be(expected);

        [Fact]
        public async Task Next_number_continues_from_existing_rows_including_deprecated_column_and_ignores_pre_D63_prefixes()
        {
            var day = new DateTime(2026, 11, 17);
            await _factory.SeedAsync(db =>
            {
                db.Order.Add(new Order { Id = Guid.NewGuid(), OrderNumber = "DH-261117-0007", CustomerId = Guid.NewGuid(), MealDate = day, DeliveryDate = day });
                db.Order.Add(new Order { Id = Guid.NewGuid(), OrderNumber = "PH-261117-0050", CustomerId = Guid.NewGuid(), MealDate = day, DeliveryDate = day }); // số cũ trước D-63
                db.GoodsReceipt.Add(new GoodsReceipt { Id = Guid.NewGuid(), ReceiptNo = "PN-261117-0012", SupplierId = Guid.NewGuid(), ReceiptDate = day }); // cột cũ
                db.ProductionPlan.Add(new ProductionPlan { Id = Guid.NewGuid(), PlanNumber = "KS-261117" });
                db.ProductionPlan.Add(new ProductionPlan { Id = Guid.NewGuid(), PlanNumber = "KS-261117-2" });
                db.MaterialLot.Add(new MaterialLot { Id = Guid.NewGuid(), LotNumber = "LO-261117-NCC01-09", LotNo = "LO-261117-NCC01-09" });
                return Task.CompletedTask;
            });

            (await NextAsync(DocumentKind.Order, day)).Should().Be("DH-261117-0008", "số cũ PH- không tính vào dãy DH-");
            (await NextAsync(DocumentKind.GoodsReceipt, day)).Should().Be("PN-261117-0013", "đọc cả cột cũ ReceiptNo");
            (await NextAsync(DocumentKind.ProductionPlan, day)).Should().Be("KS-261117-3");
            (await NextAsync(DocumentKind.MaterialLot, day, "NCC01")).Should().Be("LO-261117-NCC01-10");
            (await NextAsync(DocumentKind.MaterialLot, day, "NCC02")).Should().Be("LO-261117-NCC02-01", "đếm riêng theo hậu tố");
            (await NextAsync(DocumentKind.Order, day.AddDays(1))).Should().Be("DH-261118-0001", "đếm lại theo ngày");
        }

        [Theory]
        [InlineData(DocumentKind.CustomerBusiness, "KHB-00001")]
        [InlineData(DocumentKind.CustomerIndividual, "KHC-00001")]
        [InlineData(DocumentKind.Supplier, "NC-00001")]
        [InlineData(DocumentKind.Ingredient, "NL-00001")]
        [InlineData(DocumentKind.Dish, "MA-00001")]
        [InlineData(DocumentKind.Warehouse, "KO-00001")]
        [InlineData(DocumentKind.Vehicle, "XE-00001")]
        [InlineData(DocumentKind.Employee, "NV-00001")]
        [InlineData(DocumentKind.DeliveryLocation, "DP-00001")]
        [InlineData(DocumentKind.PricePolicy, "BG-00001")]
        public async Task First_catalog_code_has_the_right_format(DocumentKind kind, string expected)
        {
            using var scope = _factory.Services.CreateScope();
            (await scope.ServiceProvider.GetRequiredService<IDocumentNumberService>().NextCodeAsync(kind)).Should().Be(expected);
        }

        [Fact]
        public async Task Suffix_is_required_for_lot_batch_route_and_cleaning()
        {
            Func<Task> lot = () => NextAsync(DocumentKind.MaterialLot, DateTime.Today);
            await lot.Should().ThrowAsync<ArgumentException>();
            Func<Task> batch = () => NextAsync(DocumentKind.ProductionBatch, DateTime.Today, "  ");
            await batch.Should().ThrowAsync<ArgumentException>();
        }
    }

    /// <summary>
    /// T11 – SQL Server dev: 50 transaction song song cùng xin mã XN cho 1 ngày và ghi ApprovalRequest → 50 mã khác nhau, liên tục 001..050.
    /// Dùng ngày 2099-12-31 (không đụng dữ liệu thật); cuối test xóa đúng 50 dòng test vừa tạo (theo Id).
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class DocumentNumberSqlServerTests(ApiFactory factory)
    {
        [SkippableFact]
        public async Task Fifty_parallel_transactions_never_get_the_same_number()
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);
            var day = new DateTime(2099, 12, 31);
            Guid admin;
            using (var s = factory.Services.CreateScope())
            {
                admin = await s.ServiceProvider.GetRequiredService<DatabaseContext>().User.Where(u => u.UserName == "admin").Select(u => u.Id).SingleAsync();
            }
            var ids = new System.Collections.Concurrent.ConcurrentBag<Guid>();
            try
            {
                var tasks = Enumerable.Range(0, 50).Select(_ => Task.Run(async () =>
                {
                    using var scope = factory.Services.CreateScope();
                    var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>();
                    using (actor.BeginScope(ActorInfo.ForUser(admin)))
                    {
                        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                        var numbers = scope.ServiceProvider.GetRequiredService<IDocumentNumberService>();
                        using var tran = await unitOfWork.OpenTransaction();
                        string no = await numbers.NextAsync(DocumentKind.Approval, day);
                        var id = Guid.NewGuid();
                        unitOfWork.ApprovalRequestRepository.Add(new ApprovalRequest
                        {
                            Id = id, RequestNo = no, BusinessType = "[DEMO]-T11", RequestStatus = "Pending", RequestedAt = DateTime.Now, RequestedBy = admin,
                        });
                        await unitOfWork.ApprovalRequestRepository.CommitAsync("Insert");
                        unitOfWork.CommitTransaction();
                        ids.Add(id);
                    }
                })).ToArray();
                await Task.WhenAll(tasks);

                using var check = factory.Services.CreateScope();
                var numbersSaved = await check.ServiceProvider.GetRequiredService<DatabaseContext>().ApprovalRequest.AsNoTracking()
                    .Where(a => ids.Contains(a.Id)).Select(a => a.RequestNo!).ToListAsync();
                numbersSaved.Should().HaveCount(50).And.OnlyHaveUniqueItems();
                numbersSaved.OrderBy(x => x).Should().Equal(Enumerable.Range(1, 50).Select(i => $"XN-991231-{i:000}"));
            }
            finally
            {
                // Dọn đúng các dòng test vừa tạo (DB dev) – không xóa hàng loạt.
                using var cleanup = factory.Services.CreateScope();
                var db = cleanup.ServiceProvider.GetRequiredService<DatabaseContext>();
                var list = ids.ToList();
                await db.ApprovalRequest.Where(a => list.Contains(a.Id) && a.BusinessType == "[DEMO]-T11").ExecuteDeleteAsync();
            }
        }

        /// <summary>
        /// T50R1 (lỗi tìm thấy khi chạy luồng Pilot trên DB dev): phiếu nhập 2 dòng ghi sổ → 2 lô cùng số NL-…-01 vì mã thứ 2 sinh trước khi lô thứ 1 được lưu.
        /// Nay mã đã gán cho bản ghi đang theo dõi (chưa lưu) cũng được tính. Chạy trong transaction rồi ROLLBACK.
        /// </summary>
        [SkippableFact]
        public async Task Numbers_generated_in_a_loop_before_saving_are_distinct_on_sql_server()
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);
            var day = new DateTime(2099, 12, 30);
            using var scope = factory.Services.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var numbers = scope.ServiceProvider.GetRequiredService<IDocumentNumberService>();
            await using var tx = await unitOfWork.DbContext.Database.BeginTransactionAsync();
            var lots = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                string no = await numbers.NextAsync(DocumentKind.MaterialLot, day, "T50R1");
                lots.Add(no);
                unitOfWork.MaterialLotRepository.Add(new MaterialLot { Id = Guid.NewGuid(), LotNumber = no, LotNo = no, IngredientId = Guid.NewGuid(), Status = "Available", Note = "[DEMO]" });
            }
            lots.Should().Equal("LO-991230-T50R1-01", "LO-991230-T50R1-02", "LO-991230-T50R1-03");
            await tx.RollbackAsync();
        }
    }
}
