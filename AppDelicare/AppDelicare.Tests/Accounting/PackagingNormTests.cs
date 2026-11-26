using System.Net.Http.Json;
using AppDelicare.Data.Connection;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Services.Implement;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Accounting
{
    /// <summary>
    /// ACC-6 (D-131 j): định mức bao bì = công thức món BB-&lt;buổi&gt; (lưu nháp → gửi duyệt RECIPE_VERSION → khóa áp dụng); đóng gói → phiếu xuất bao bì Nháp theo ca
    /// = suất đạt × định mức (mỗi đơn lấy món nhiều suất nhất), lần sau chỉ lập phần suất còn thiếu; thiếu tồn → không lập dòng đó; job ghi sổ khi hết ca;
    /// chứng từ "Xuất bao bì" (PACKAGING) đối tượng = thành phẩm của buổi.
    /// </summary>
    public class PackagingNormTests
    {
        private static readonly DateTime Day = new(2034, 5, 14);

        [Fact]
        public async Task Norm_needs_approval_then_packing_drafts_issue_and_job_posts_it()
        {
            using var f = new InMemoryApiFactory();
            f.Clock.FixedNow = Day.AddHours(11);
            Guid wh = Guid.NewGuid(), unit = Guid.NewGuid(), box = Guid.NewGuid(), spoon = Guid.NewGuid(), shift = Guid.NewGuid(), po = Guid.NewGuid();
            Guid dishA = Guid.NewGuid(), dishB = Guid.NewGuid(), batchA = Guid.NewGuid(), batchB = Guid.NewGuid(), order1 = Guid.NewGuid(), order2 = Guid.NewGuid();
            await f.SeedAsync(db =>
            {
                db.Warehouse.Add(new Warehouse { Id = wh, Code = "BKHO", Name = "Kho B", Status = "Active" });
                db.UnitMeasure.Add(new UnitMeasure { Id = unit, Code = "BCAI", Name = "cái", Status = "Active" });
                db.Ingredient.Add(new Ingredient { Id = box, Code = "CC0003", Name = "Hộp 5 ngăn", BaseUnitId = unit, Status = "Active" });
                db.Ingredient.Add(new Ingredient { Id = spoon, Code = "CC0002", Name = "Combo đũa thìa", BaseUnitId = unit, Status = "Active" });
                foreach (var (ing, qty) in new[] { (box, 1000m), (spoon, 10m) })
                {
                    db.InventoryTransaction.Add(new InventoryTransaction { Id = Guid.NewGuid(), TransactionNumber = "NK-B", TransactionType = "RECEIPT", WarehouseId = wh, IngredientId = ing, Quantity = qty, QuantityIn = qty, UomId = unit, UnitId = unit, UnitCost = 1500, TransactionDate = Day.AddDays(-2) });
                    db.InventoryBalance.Add(new InventoryBalance { Id = Guid.NewGuid(), WarehouseId = wh, IngredientId = ing, UnitId = unit, OnHandQuantity = qty, OnHandQty = qty, AvailableQuantity = qty, AvailableQty = qty, UpdatedDate = Day, LastCalculatedDate = Day });
                }
                db.Shift.Add(new Shift { Id = shift, Code = "TRUA_10H", Name = "Trưa", Status = "Active", StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(13, 0, 0) });
                db.ProductionOrder.Add(new ProductionOrder { Id = po, ProductionPlanId = Guid.NewGuid(), DishId = dishA, RecipeId = Guid.NewGuid(), ShiftId = shift, PlannedQty = 200, Status = "InProgress", PlannedStartDate = Day });
                db.ProductionBatch.Add(new ProductionBatch { Id = batchA, BatchNumber = "ME-A", ProductionOrderId = po, DishId = dishA, RecipeId = Guid.NewGuid(), StartDate = Day, Status = "Passed" });
                db.ProductionBatch.Add(new ProductionBatch { Id = batchB, BatchNumber = "ME-B", ProductionOrderId = po, DishId = dishB, RecipeId = Guid.NewGuid(), StartDate = Day, Status = "Passed" });
                // Đơn 1: 2 món cùng 100 suất (→ 100 suất, không cộng 200); đơn 2: 20 suất.
                AddPacking(db, batchA, order1, 100);
                AddPacking(db, batchB, order1, 100);
                AddPacking(db, batchA, order2, 20);
                return Task.CompletedTask;
            });
            using var c = f.CreateAuthorizedClient();
            async Task<LegacyResult> Post(string action, object body) => await LegacyResult.ReadAsync(await c.PostAsJsonAsync($"/api/PackagingNorm/{action}", body));

            (await Post("SaveMeal", new { meal = "XX", listLine = new[] { new { ingredientId = box, perHundred = 100m } } })).Fail("Loại suất");
            (await Post("SaveMeal", new { meal = "TRUA", listLine = new[] { new { ingredientId = box, perHundred = 100m }, new { ingredientId = spoon, perHundred = 50m } } })).Ok();
            var m = (await c.GetResultAsync("/api/PackagingNorm/GetMatrix")).Ok();
            var trua = m.GetProperty("listMeal").EnumerateArray().Single(x => x.GetProperty("code").GetString() == "TRUA");
            trua.GetProperty("workStatus").GetString().Should().Be("Draft");
            m.GetProperty("listRow").EnumerateArray().Single(r => r.GetProperty("code").GetString() == "CC0002").GetProperty("work").GetProperty("TRUA").GetDecimal().Should().Be(50);

            // Chưa duyệt → đóng gói không lập phiếu (chưa có định mức áp dụng).
            (await AfterPacking(f)).Should().Contain(w => w.Contains("Chưa có định mức"));
            (await Post("Submit", new { meal = "TRUA" })).Ok();
            (await Post("SaveMeal", new { meal = "TRUA", listLine = new[] { new { ingredientId = box, perHundred = 1m } } })).Fail("chờ duyệt");
            var req = await f.QueryAsync(db => db.ApprovalRequest.AsNoTracking().SingleAsync(r => r.BusinessType == RecipeStatus.BusinessType));
            (await c.PostFormAsync("/api/ApprovalRequest/Decide", ("Id", req.Id), ("Approve", true), ("Note", "đồng ý"))).Ok();
            m = (await c.GetResultAsync("/api/PackagingNorm/GetMatrix")).Ok();
            m.GetProperty("listRow").EnumerateArray().Single(r => r.GetProperty("code").GetString() == "CC0003").GetProperty("active").GetProperty("TRUA").GetDecimal().Should().Be(100);

            // 120 suất: hộp 120, đũa thìa 60 > tồn 10 → báo thiếu, phiếu chỉ có hộp.
            var w1 = await AfterPacking(f);
            w1.Should().Contain(w => w.Contains("Thiếu tồn 1 vật tư"));
            var issue = await f.QueryAsync(db => db.MaterialIssue.AsNoTracking().SingleAsync(i => i.Note!.Contains(PackagingNormService.TagPrefix)));
            issue.Status.Should().Be("Draft");
            PackagingNormService.ParseTag(issue.Note)!.Portions.Should().Be(120);
            var lines = await f.QueryAsync(db => db.MaterialIssueLine.AsNoTracking().Where(l => l.MaterialIssueId == issue.Id).ToListAsync());
            lines.Should().ContainSingle().Which.Quantity.Should().Be(120);

            // Chạy lại = không lập thêm; đóng thêm 30 suất → phiếu thứ 2 cho 30 suất.
            await AfterPacking(f);
            (await f.QueryAsync(db => db.MaterialIssue.CountAsync(i => i.Note!.Contains(PackagingNormService.TagPrefix)))).Should().Be(1);
            await f.SeedAsync(db => { AddPacking(db, batchA, Guid.NewGuid(), 30); return Task.CompletedTask; });
            await AfterPacking(f);
            (await f.QueryAsync(db => db.MaterialIssue.CountAsync(i => i.Note!.Contains(PackagingNormService.TagPrefix)))).Should().Be(2);

            // Nút 🛒 trên tin thiếu: đề nghị mua nháp phần thiếu (150 suất × 0,5 − tồn 10 = 65 đũa thìa); bấm lại → báo số cũ.
            var pr = await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/PackagingNorm/CreateShortagePr", new { day = Day, shiftCode = "TRUA_10H" }));
            pr.Ok();
            var prRow = await f.QueryAsync(db => db.PurchaseRequest.AsNoTracking().SingleAsync(p => p.Note!.Contains(PackagingNormService.PurchaseTagPrefix)));
            (await f.QueryAsync(db => db.PurchaseRequestLine.AsNoTracking().Where(l => l.PurchaseRequestId == prRow.Id).ToListAsync())).Should().ContainSingle(l => l.IngredientId == spoon).Which.Quantity.Should().Be(65);
            (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/PackagingNorm/CreateShortagePr", new { day = Day, shiftCode = "TRUA_10H" }))).Ok();
            (await f.QueryAsync(db => db.PurchaseRequest.CountAsync(p => p.Note!.Contains(PackagingNormService.PurchaseTagPrefix)))).Should().Be(1);

            // Món / nhóm bao bì không hiện ở danh mục món (chọn món thực đơn).
            (await c.GetResultAsync("/api/Dish/GetAllData")).ListData.EnumerateArray().Should().NotContain(d => d.GetProperty("code").GetString() == "BB-TRUA");

            // Chưa hết ca → không ghi sổ; sau 13:00 → ghi sổ cả 2 phiếu.
            (await Scoped(f, s => s.AutoPostAsync(Day.AddHours(12)))).Posted.Should().Be(0);
            (await Scoped(f, s => s.AutoPostAsync(Day.AddHours(14)))).Posted.Should().Be(2);
            (await f.QueryAsync(db => db.MaterialIssue.CountAsync(i => i.Status == "Issued" && i.Note!.Contains(PackagingNormService.TagPrefix)))).Should().Be(2);

            f.Clock.FixedNow = Day.AddDays(20);
            var vouchers = (await c.GetResultAsync("/api/AccountingVoucher/GetMonth?month=2034-05&tab=all")).Ok().GetProperty("listVoucher").EnumerateArray().ToList();
            vouchers.Where(v => v.GetProperty("type").GetString() == "PACKAGING").Should().HaveCount(2);
            vouchers.Should().NotContain(v => v.GetProperty("type").GetString() == "ISSUE");
            vouchers.First(v => v.GetProperty("type").GetString() == "PACKAGING").GetProperty("objectCode").GetString().Should().Be("TP0002");
        }

        private static void AddPacking(DatabaseContext db, Guid batch, Guid order, decimal qty)
        {
            var p = new PackingOrder { Id = Guid.NewGuid(), PackingOrderNumber = "DG-" + Guid.NewGuid().ToString("N")[..6], ProductionBatchId = batch, OrderId = order, PlannedQty = qty, PackingDate = Day.AddHours(10), Status = "Packed" };
            db.PackingOrder.Add(p);
            db.PackingResult.Add(new PackingResult { Id = Guid.NewGuid(), PackingOrderId = p.Id, PlannedQty = qty, PackedQty = qty, PackedQuantity = qty, AcceptedQty = qty, RejectedQuantity = 0 });
        }

        private static Task<List<string>> AfterPacking(InMemoryApiFactory f) => Scoped(f, s => s.AfterPackingAsync(Day));

        private static async Task<T> Scoped<T>(InMemoryApiFactory f, Func<IPackagingNormService, Task<T>> run)
        {
            using var scope = f.Services.CreateScope();
            var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>();
            using (actor.BeginScope(ActorInfo.ForSystem(InMemoryApiFactory.UserId)))
            {
                return await run(scope.ServiceProvider.GetRequiredService<IPackagingNormService>());
            }
        }
    }
}
