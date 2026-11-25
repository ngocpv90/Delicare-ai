using System.Net.Http.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Services.Implement;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Production
{
    /// <summary>
    /// UI-6.32 (D-113): Kiểm tra không gửi người kiểm → người đăng nhập; PassAll trên mẻ 4/6 bước → đủ 6 bước Đạt + kết luận Đạt + sổ bao bì sẵn sàng;
    /// thiếu hóa chất → lỗi, không ghi gì; mẻ có bước Không đạt → chặn; bảng 3 cột; khay sạch cho ngày mai khớp sổ bao bì + suất đã chốt; Sổ vệ sinh.
    /// </summary>
    public class CleaningBoardUi632Tests : IClassFixture<InMemoryApiFactory>
    {
        private static readonly DateTime Day = new(2037, 3, 9);
        private readonly InMemoryApiFactory _factory;

        public CleaningBoardUi632Tests(InMemoryApiFactory factory)
        {
            _factory = factory;
            _factory.Clock.FixedNow = Day.AddHours(14);
        }

        private static async Task<LegacyResult> PostJson(HttpClient c, string url, object body) => await LegacyResult.ReadAsync(await c.PostAsJsonAsync(url, body));

        private static object Steps(params (string Code, string Result, string? Chemical, string? Corrective)[] steps)
            => steps.Select(x => new { itemCode = x.Code, resultCode = x.Result, chemicalUsed = x.Chemical, correctiveAction = x.Corrective }).ToArray();

        [Fact]
        public async Task Pass_all_board_clean_stock_and_book()
        {
            Guid customer = Guid.NewGuid(), dOrder = Guid.NewGuid(), ret1 = Guid.NewGuid(), ret2 = Guid.NewGuid(), person = Guid.NewGuid(), employee = Guid.NewGuid(), tomorrow = Guid.NewGuid();
            const string type = "KHAY-632";
            using var c = _factory.CreateAuthorizedClient();
            await _factory.SeedAsync(async db =>
            {
                db.Customer.Add(new Customer { Id = customer, Code = "K632", Name = "Khách 632", DisplayName = "Khách 632", CIF = "K632", CustomerType = "FACTORY", Status = "Active" });
                db.DeliveryOrder.Add(new DeliveryOrder { Id = dOrder, OrderId = Guid.NewGuid(), CustomerId = customer, DeliveryNumber = "GH-632", DeliveryNo = "GH-632", DeliveryDate = Day, Status = "Delivered" });
                db.ContainerReturn.AddRange(
                    new ContainerReturn { Id = ret1, DeliveryOrderId = dOrder, CustomerId = customer, ReturnDate = Day.AddHours(13), Status = "Returned", ExpectedQuantity = 40, ReturnedQuantity = 40, DamagedQuantity = 0, Note = PackagingRules.WithType(null, type) },
                    new ContainerReturn { Id = ret2, DeliveryOrderId = dOrder, CustomerId = customer, ReturnDate = Day.AddHours(13), Status = "Returned", ExpectedQuantity = 20, ReturnedQuantity = 18, DamagedQuantity = 1, Note = PackagingRules.WithType(null, type) });
                db.Employee.Add(new Employee { Id = employee, Code = "NV632", FullName = "Người kiểm 632", PersonId = person, Status = "Active" });
                (await db.User.SingleAsync(u => u.Id == InMemoryApiFactory.UserId)).PersonId = person;
                if (!db.FoodSafetyChecklist.Any(x => x.Code == "VS-KHAY"))
                {
                    Guid cl = Guid.NewGuid();
                    db.FoodSafetyChecklist.Add(new FoodSafetyChecklist { Id = cl, Code = "VS-KHAY", Name = "Vệ sinh khay", VersionNo = 1, Status = "Active", EffectiveFrom = Day.AddDays(-30) });
                    int i = 0;
                    foreach (var (code, name) in new[] { ("NHAN_QR", "1. Nhận"), ("PHAN_LOAI", "2. Phân loại"), ("RUA_SO", "3. Rửa sơ"), ("RUA_MAY", "4. Rửa máy"), ("KHU_KHUAN", "5. Khử khuẩn"), ("KIEM_TRA_NHAP_KHO", "6. Kiểm tra") })
                    {
                        db.FoodSafetyChecklistItem.Add(new FoodSafetyChecklistItem { Id = Guid.NewGuid(), ChecklistId = cl, ItemCode = code, ItemName = name, SortOrder = ++i, IsRequired = true });
                    }
                }
                // Sổ bao bì: sẵn sàng 100, giao 70 → sạch sẵn sàng 30; ngày mai 50 suất đã chốt.
                db.PackagingLedger.AddRange(
                    new PackagingLedger { Id = Guid.NewGuid(), TransactionType = "AVAILABLE", PackagingType = type, Quantity = 100, TransactionAt = Day.AddDays(-3) },
                    new PackagingLedger { Id = Guid.NewGuid(), TransactionType = "OUT", PackagingType = type, Quantity = 70, TransactionAt = Day.AddDays(-1) });
                db.Order.Add(new Order { Id = tomorrow, OrderNumber = "DH-632", OrderNo = "DH-632", CustomerId = customer, OrderDate = Day, MealDate = Day.AddDays(1), DeliveryDate = Day.AddDays(1), Status = "Confirmed", TotalQuantity = 50 });
                db.PackingOrder.Add(new PackingOrder { Id = Guid.NewGuid(), ProductionBatchId = Guid.NewGuid(), OrderId = tomorrow, PlannedQty = 50, Status = "Planned", PackingOrderNumber = "DG-632", PackingDate = Day.AddDays(1) });
            });
            await _factory.SeedAsync(async db =>
            {
                var po = await db.PackingOrder.SingleAsync(p => p.OrderId == tomorrow);
                db.PackingResult.Add(new PackingResult { Id = Guid.NewGuid(), PackingOrderId = po.Id, PlannedQty = 50, PackedQuantity = 0, PackagingType = type });
            });

            var board = (await c.GetResultAsync($"/api/CleaningBatch/GetDayBoard?date={Day:yyyy-MM-dd}")).Ok();
            var waiting = board.GetProperty("listWaiting").EnumerateArray().Where(w => w.GetProperty("customerName").GetString() == "Khách 632").ToList();
            waiting.Single(w => w.GetProperty("containerReturnId").GetGuid() == ret2).GetProperty("suggestedResult").GetString().Should().Be("SHORTAGE");
            var stock = (await c.GetResultAsync($"/api/PackagingLedger/GetCleanStock?date={Day:yyyy-MM-dd}")).Ok().GetProperty("listType").EnumerateArray().Single(t => t.GetProperty("packagingType").GetString() == type);
            (stock.GetProperty("ready").GetDecimal(), stock.GetProperty("needed").GetDecimal(), stock.GetProperty("shortage").GetDecimal(), stock.GetProperty("waitingInspection").GetDecimal())
                .Should().Be((30m, 50m, 20m, 57m));

            // Kiểm tra không gửi người kiểm → người đăng nhập; tự lập mẻ 40 khay.
            var inspected = (await c.PostFormAsync("/api/ContainerReturn/Inspect", ("Id", ret1), ("InspectionResult", "OK"))).Ok();
            Guid batch = inspected.GetProperty("listCleaningBatch")[0].GetProperty("id").GetGuid();
            (await _factory.QueryAsync(db => db.ReturnInspection.AsNoTracking().SingleAsync(r => r.ContainerReturnId == ret1))).InspectorEmployeeId.Should().Be(employee);

            (await PostJson(c, "/api/CleaningBatch/RecordResults", new { id = batch, listItem = Steps(("NHAN_QR", "PASS", null, null), ("PHAN_LOAI", "PASS", null, null), ("RUA_SO", "PASS", null, null), ("RUA_MAY", "PASS", null, null)) })).Ok();
            (await c.PostFormAsync("/api/CleaningBatch/PassAll", ("Id", batch))).Fail("phải ghi hóa chất");
            (await _factory.QueryAsync(db => db.CleaningChecklistResult.AsNoTracking().CountAsync(r => r.CleaningBatchId == batch))).Should().Be(4, "lỗi → không ghi gì");
            var detail = (await c.GetResultAsync($"/api/CleaningBatch/GetDetail?id={batch}")).Ok();
            var done = (await c.PostFormAsync("/api/CleaningBatch/PassAll", ("Id", batch), ("ChemicalUsed", "Chlorine 200 ppm"))).Ok();
            (done.GetProperty("batch").GetProperty("status").GetString(), done.GetProperty("batch").GetProperty("qcResult").GetString(), done.GetProperty("passedStepCount").GetInt32())
                .Should().Be(("Completed", "PASS", 6));
            (await _factory.QueryAsync(db => db.PackagingLedger.AsNoTracking().Where(l => l.PackagingType == type && l.TransactionType == "AVAILABLE").SumAsync(l => l.Quantity))).Should().Be(140m);
            // Lần sau: hóa chất gợi ý = lần ghi gần nhất.
            var i2 = (await c.PostFormAsync("/api/ContainerReturn/Inspect", ("Id", ret2), ("InspectionResult", "SHORTAGE"), ("Note", "Thiếu 2 khay"))).Ok();
            Guid batch2 = i2.GetProperty("listCleaningBatch")[0].GetProperty("id").GetGuid();
            (await c.GetResultAsync($"/api/CleaningBatch/GetDetail?id={batch2}")).Ok().GetProperty("suggestedChemical").GetString().Should().Be("Chlorine 200 ppm");
            (await PostJson(c, "/api/CleaningBatch/RecordResults", new { id = batch2, listItem = Steps(("NHAN_QR", "FAIL", null, "Quét lại QR")) })).Ok();
            (await c.PostFormAsync("/api/CleaningBatch/PassAll", ("Id", batch2), ("ChemicalUsed", "Chlorine"))).Fail("có bước Không đạt");

            board = (await c.GetResultAsync($"/api/CleaningBatch/GetDayBoard?date={Day:yyyy-MM-dd}")).Ok();
            board.GetProperty("listDone").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == batch).GetProperty("qcResult").GetString().Should().Be("PASS");
            board.GetProperty("listInProgress").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == batch2).GetProperty("hasFailedStep").GetBoolean().Should().BeTrue();

            var book = (await c.GetResultAsync($"/api/CleaningBatch/GetBook?From={Day:yyyy-MM-dd}&To={Day:yyyy-MM-dd}&CustomerId={customer}")).Ok();
            var row = book.GetProperty("listDay")[0].GetProperty("listRow").EnumerateArray().Single();
            (row.GetProperty("containerQuantity").GetInt32(), row.GetProperty("chemical").GetString(), row.GetProperty("qcResult").GetString()).Should().Be((40, "Chlorine 200 ppm", "PASS"));
        }
    }
}
