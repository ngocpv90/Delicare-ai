using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Services.Jobs;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Jobs
{
    /// <summary>
    /// UI-6.49 (3) (QĐ-26, D-127 c): job đóng lệnh 22:00 tự ghi Nấu dư = suất đạt − đóng gói − hỏng − loại − nấu dư đã ghi; mẻ chưa chia suất không đoán;
    /// chạy lại = 0 (không ghi trùng).
    /// </summary>
    public class OverProductionUi649Tests
    {
        private static readonly DateTime Day = new(2032, 8, 2);

        [Fact]
        public async Task Over_production_is_recorded_once_from_packing_results()
        {
            using var f = new InMemoryApiFactory();
            f.Clock.FixedNow = Day.AddHours(22);
            Guid plan = Guid.NewGuid(), order = Guid.NewGuid(), packed = Guid.NewGuid(), notPacked = Guid.NewGuid(), po = Guid.NewGuid(), dish = Guid.NewGuid();
            await f.SeedAsync(db =>
            {
                db.ProductionPlan.Add(new ProductionPlan { Id = plan, PlanNumber = "KH-OV", PlanNo = "KH-OV", ProductionDate = Day, PlanDate = Day.AddDays(-1), Status = "Released" });
                db.ProductionOrder.Add(new ProductionOrder { Id = order, ProductionPlanId = plan, DishId = dish, RecipeId = Guid.NewGuid(), PlannedQty = 200, Status = ProductionOrderStatus.Closed, ProductionOrderNumber = "LS-OV", ProductionOrderNo = "LS-OV" });
                db.ProductionBatch.Add(new ProductionBatch { Id = packed, ProductionOrderId = order, DishId = dish, BatchNumber = "ME-OV-1", BatchNo = "ME-OV-1", StartDate = Day.AddHours(7), PlannedQuantity = 100, Status = ProductionBatchStatus.Packed });
                db.ProductionBatch.Add(new ProductionBatch { Id = notPacked, ProductionOrderId = order, DishId = dish, BatchNumber = "ME-OV-2", BatchNo = "ME-OV-2", StartDate = Day.AddHours(8), PlannedQuantity = 100, Status = ProductionBatchStatus.Passed });
                db.ProductionOutput.Add(new ProductionOutput { Id = Guid.NewGuid(), ProductionBatchId = packed, ProducedQuantity = 100, AcceptedQuantity = 100 });
                db.ProductionOutput.Add(new ProductionOutput { Id = Guid.NewGuid(), ProductionBatchId = notPacked, ProducedQuantity = 100, AcceptedQuantity = 100 });
                db.PackingOrder.Add(new PackingOrder { Id = po, ProductionBatchId = packed, PackingNo = "DG-OV", PackingOrderNumber = "DG-OV", PackingDate = Day, PlannedQty = 93, Status = "Packed" });
                db.PackingResult.Add(new PackingResult { Id = Guid.NewGuid(), PackingOrderId = po, PackedQuantity = 90, PackedQty = 90, DamagedQty = 2, RejectedQuantity = 1, RejectedQty = 1 });
                // Nấu dư bếp đã ghi tay 2 suất → không ghi trùng.
                db.MealWasteRecord.Add(new MealWasteRecord { Id = Guid.NewGuid(), ProductionBatchId = packed, ProductionOrderId = order, DishId = dish, WasteType = ProductionCode.WasteOverProduction, QuantityMeals = 2, OccurredAt = Day.AddHours(13) });
                return Task.CompletedTask;
            });
            using var sc = f.Services.CreateScope();
            using var asSystem = sc.ServiceProvider.GetRequiredService<AppDelicare.Data.Share.ICurrentActor>().BeginScope(AppDelicare.Data.Share.ActorInfo.ForSystem(Guid.NewGuid()));
            int Run() => f.QueryAsync(db => ProductionAutoCloseJob.RecordOverProductionAsync(db, Day, Day.AddHours(22), null, CancellationToken.None)).GetAwaiter().GetResult();
            Run().Should().Be(1, "chỉ mẻ đã chia suất");
            var rows = await f.QueryAsync(db => db.MealWasteRecord.AsNoTracking().Where(w => w.ProductionBatchId == packed && w.WasteType == ProductionCode.WasteOverProduction).OrderBy(w => w.OccurredAt).ToListAsync());
            rows.Should().HaveCount(2);
            rows[1].QuantityMeals.Should().Be(5, "100 − 90 − 2 − 1 − 2 đã ghi");
            rows[1].Reason.Should().StartWith(ProductionAutoCloseJob.OverProductionReason);
            (await f.QueryAsync(db => db.MealWasteRecord.AnyAsync(w => w.ProductionBatchId == notPacked)))
                .Should().BeFalse("mẻ chưa có kết quả đóng gói – không đoán");
            Run().Should().Be(0, "chạy lại không ghi trùng");
        }
    }
}
