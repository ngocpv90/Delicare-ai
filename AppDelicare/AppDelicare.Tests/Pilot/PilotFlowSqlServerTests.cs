using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Data.Connection;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Pilot
{
    /// <summary>
    /// T50R1 – luồng Pilot P3 → P8 chạy QUA HTTP API thật (routing, model binding, PermissionFilter, controller → service) trên SQL Server dev,
    /// cùng thứ tự với <c>docs/api-tests/pilot-flow.http</c>, trong 1 transaction ngoài rồi ROLLBACK (không để lại dữ liệu).
    /// Cách giữ transaction qua nhiều request: mọi request dùng chung 1 scope DI (<see cref="SharedScope"/>) có DbContext đang mở transaction;
    /// UnitOfWork.OpenTransaction tham gia transaction ngoài (owner = false) như các *SqlServerTests khác. Dữ liệu thử gắn nhãn [DEMO], ngày ăn 21/12/2099.
    /// Kiểm luôn các chốt an toàn: lô hết hạn / KHONG_DAT, nhiệt độ nhận 3 vùng, Giám đốc duyệt PO ≥ 50 triệu, nhiệt độ lõi &lt; 70 °C,
    /// lưu mẫu trước khi giao (D-42), nhiệt độ giao &lt; 60 °C.
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class PilotFlowSqlServerTests(ApiFactory factory)
    {
        /// <summary>Giữ scope DI dùng chung cho mọi request (null = request dùng scope riêng như bình thường).</summary>
        private sealed class SharedScope
        {
            public IServiceProvider? Provider { get; set; }
        }

        private sealed class SharedScopeStartupFilter : IStartupFilter
        {
            public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
            {
                app.Use(async (context, nextMiddleware) =>
                {
                    if (context.RequestServices.GetService<SharedScope>()?.Provider is { } shared)
                    {
                        context.RequestServices = shared;
                    }
                    await nextMiddleware();
                });
                next(app);
            };
        }

        [SkippableFact]
        public async Task Pilot_flow_P3_to_P8_through_http_api_on_sql_server_inside_outer_transaction()
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);
            var shared = new SharedScope();
            using var app = factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
            {
                s.AddSingleton(shared);
                s.AddTransient<IStartupFilter, SharedScopeStartupFilter>();
            }));
            using var http = app.CreateClient();
            var login = await http.PostAsJsonAsync("/api/Authentication/Login", new
            {
                userName = Environment.GetEnvironmentVariable("DELICARE_TEST_USER") ?? "admin",
                passWord = Environment.GetEnvironmentVariable("DELICARE_TEST_PASSWORD") ?? "admin",
            });
            using (var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync()))
            {
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", doc.RootElement.GetProperty("data").GetProperty("token").GetString());
            }

            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            int ordersBefore = await db.Order.CountAsync();
            int lotsBefore = await db.MaterialLot.CountAsync();
            int txnBefore = await db.InventoryTransaction.CountAsync();
            int batchesBefore = await db.ProductionBatch.CountAsync();
            int routesBefore = await db.DeliveryRoute.CountAsync();
            int complaintsBefore = await db.CustomerComplaint.CountAsync();
            int tempBefore = await db.TemperatureLog.CountAsync();

            string k = Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
            DateTime day = new(2099, 12, 21); // Thứ 2
            var flow = new PilotFlow(http, db, k, day);

            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                shared.Provider = scope.ServiceProvider;
                try
                {
                    try
                    {
                        await flow.RunAsync();
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException("Luồng Pilot lỗi ở bước: " + flow.LastUrl, ex);
                    }
                }
                finally
                {
                    shared.Provider = null;
                    await tx.RollbackAsync();
                }
            }

            db.ChangeTracker.Clear();
            (await db.Order.CountAsync()).Should().Be(ordersBefore, "rollback không để lại dữ liệu");
            (await db.MaterialLot.CountAsync()).Should().Be(lotsBefore);
            (await db.InventoryTransaction.CountAsync()).Should().Be(txnBefore);
            (await db.ProductionBatch.CountAsync()).Should().Be(batchesBefore);
            (await db.DeliveryRoute.CountAsync()).Should().Be(routesBefore);
            (await db.CustomerComplaint.CountAsync()).Should().Be(complaintsBefore);
            (await db.TemperatureLog.CountAsync()).Should().Be(tempBefore);
        }

        /// <summary>Các bước của luồng – đúng thứ tự, tham số và kết quả mong đợi như pilot-flow.http / UAT_CHECKLIST mục PILOT.</summary>
        private sealed class PilotFlow(HttpClient http, DatabaseContext db, string k, DateTime day)
        {
            private static string D(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            public string? LastUrl { get; private set; }

            private async Task<LegacyResult> Get(string url) => await http.GetResultAsync(LastUrl = url);
            private async Task<LegacyResult> Post(string url, params (string, object?)[] f) => await http.PostFormAsync(LastUrl = url, f);
            private async Task<LegacyResult> PostJson(string url, object body) => await LegacyResult.ReadAsync(await http.PostAsJsonAsync(LastUrl = url, body));
            private async Task<LegacyResult> PutJson(string url, object body) => await LegacyResult.ReadAsync(await http.PutAsJsonAsync(url, body));

            /// <summary>Insert CRUD (form) trả bool → lấy Id theo mã vừa tạo.</summary>
            private async Task<Guid> Insert<T>(string controller, Func<IQueryable<T>, Task<Guid>> find, params (string, object?)[] f) where T : class
            {
                (await Post($"/api/{controller}/Insert", f)).Ok();
                db.ChangeTracker.Clear();
                return await find(db.Set<T>().AsNoTracking());
            }

            private static Guid Id(JsonElement e) => e.GetProperty("id").GetGuid();

            public async Task RunAsync()
            {
                string code = "DEMO" + k; // mã ngắn chỉ A–Z 0–9 (dùng trong số lô NL- / số chuyến CX-)
                DateTime today = DateTime.Today;
                int dow = day.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)day.DayOfWeek;

                // ===== 0. Danh mục tối thiểu (P1 / P2)
                Guid kg = await Insert<Model.Entity.UnitMeasure>("UnitMeasure", q => q.Where(x => x.Code == code + "KG").Select(x => x.Id).SingleAsync(),
                    ("Code", code + "KG"), ("Name", "[DEMO] kg " + k), ("Symbol", "kg"), ("Status", "Active"));
                Guid warehouse = await Insert<Model.Entity.Warehouse>("Warehouse", q => q.Where(x => x.Code == code + "K").Select(x => x.Id).SingleAsync(),
                    ("Code", code + "K"), ("Name", "[DEMO] Kho bếp " + k), ("WarehouseType", "KITCHEN"), ("Status", "Active"));
                Guid beef = await Insert<Model.Entity.Ingredient>("Ingredient", q => q.Where(x => x.Code == code + "BO").Select(x => x.Id).SingleAsync(),
                    ("Code", code + "BO"), ("Name", "[DEMO] Thịt bò " + k), ("BaseUnitId", kg), ("StorageType", "CHILLED"), ("ShelfLifeDays", 5),
                    ("IsExpiryManaged", true), ("QcRequired", true), ("Status", "Active"));
                Guid rice = await Insert<Model.Entity.Ingredient>("Ingredient", q => q.Where(x => x.Code == code + "GAO").Select(x => x.Id).SingleAsync(),
                    ("Code", code + "GAO"), ("Name", "[DEMO] Gạo " + k), ("BaseUnitId", kg), ("StorageType", "DRY"), ("ShelfLifeDays", 180),
                    ("IsExpiryManaged", true), ("QcRequired", true), ("Status", "Active"));
                Guid supplier = await Insert<Model.Entity.Supplier>("Supplier", q => q.Where(x => x.Code == code).Select(x => x.Id).SingleAsync(),
                    ("Code", code), ("Name", "[DEMO] NCC Pilot " + k), ("Status", "Active"));
                foreach (var (ingredient, price) in new[] { (beef, 300_000m), (rice, 20_000m) })
                {
                    (await Post("/api/SupplierMaterial/Insert", ("SupplierId", supplier), ("IngredientId", ingredient), ("UnitPrice", price), ("MinimumOrderQuantity", 0m),
                        ("LeadTimeDays", 1), ("EffectiveFrom", today.AddDays(-1)), ("IsPreferred", true), ("Status", "Active"))).Ok();
                }
                Guid dishBeef = await Insert<Model.Entity.Dish>("Dish", q => q.Where(x => x.Code == code + "BOKHO").Select(x => x.Id).SingleAsync(),
                    ("Code", code + "BOKHO"), ("Name", "[DEMO] Bò kho " + k), ("StandardServingSize", 150m), ("Status", "Active"));
                Guid dishRice = await Insert<Model.Entity.Dish>("Dish", q => q.Where(x => x.Code == code + "COM").Select(x => x.Id).SingleAsync(),
                    ("Code", code + "COM"), ("Name", "[DEMO] Cơm trắng " + k), ("StandardServingSize", 200m), ("Status", "Active"));
                // Công thức: định lượng cho 100 suất – bò 20 kg, gạo 12 kg → gửi duyệt → duyệt → khóa.
                foreach (var (dish, ingredient, qty) in new[] { (dishBeef, beef, 20m), (dishRice, rice, 12m) })
                {
                    Guid recipe = Id((await Post("/api/Recipe/CreateVersion", ("DishId", dish))).Ok());
                    (await Post("/api/RecipeLine/Insert", ("RecipeId", recipe), ("IngredientId", ingredient), ("Quantity", qty), ("UnitId", kg), ("SequenceNo", 1))).Ok();
                    (await Post("/api/Recipe/Submit", ("id", recipe))).Ok();
                    (await Post("/api/Recipe/Approve", ("Id", recipe), ("Note", "[DEMO]"))).Ok();
                    (await Post("/api/Recipe/Lock", ("Id", recipe), ("EffectiveFrom", today))).Ok();
                }
                Guid customer = await Insert<Model.Entity.Customer>("Customer", q => q.Where(x => x.Code == code).Select(x => x.Id).SingleAsync(),
                    ("Code", code), ("CIF", code), ("Name", "[DEMO] Khách Pilot " + k), ("DisplayName", "[DEMO] Khách Pilot " + k), ("CustomerType", "FACTORY"), ("Status", "Active"));
                Guid location = await Insert<Model.Entity.CustomerDeliveryLocation>("CustomerDeliveryLocation", q => q.Where(x => x.Code == code + "CT").Select(x => x.Id).SingleAsync(),
                    ("CustomerId", customer), ("Code", code + "CT"), ("Name", "[DEMO] Căng tin " + k), ("Address", "[DEMO] KCN"), ("DeliveryWindowFrom", "11:00"),
                    ("DeliveryWindowTo", "12:00"), ("Status", "Active"));
                Guid contract = await Insert<Model.Entity.Contract>("Contract", q => q.Where(x => x.ContractNumber == code + "HD").Select(x => x.Id).SingleAsync(),
                    ("CustomerId", customer), ("ContractNumber", code + "HD"), ("Name", "[DEMO] HĐ suất ăn " + k), ("EffectiveFrom", today.AddDays(-1)), ("Status", "Active"));
                Guid contractMeal = await Insert<Model.Entity.ContractMeal>("ContractMeal", q => q.Where(x => x.ContractId == contract).Select(x => x.Id).SingleAsync(),
                    ("ContractId", contract), ("MealType", "TRUA"), ("MealName", "[DEMO] Suất trưa"), ("UnitPrice", 35_000m), ("Status", "Active"));
                // BE2.2: hợp đồng tạo ra luôn Nháp → gửi duyệt → duyệt (engine CONTRACT) mới Hiệu lực.
                (await Post("/api/Contract/Submit", ("Id", contract))).Ok();
                (await Post("/api/Contract/Approve", ("Id", contract), ("Note", "[DEMO]"))).Ok();
                Guid vehicle = await Insert<Model.Entity.Vehicle>("Vehicle", q => q.Where(x => x.Code == code).Select(x => x.Id).SingleAsync(),
                    ("Code", code), ("LicensePlate", "[DEMO] 51C-" + k), ("VehicleType", "TRUCK"), ("Capacity", 1200m), ("Status", "Active"));
                Guid line = await Insert<Model.Entity.ProductionLine>("ProductionLine", q => q.Where(x => x.Code == code).Select(x => x.Id).SingleAsync(),
                    ("Code", code), ("Name", "[DEMO] Dây chuyền " + k), ("CapacityMealPerBatch", 1000m), ("MinutesPerBatch", 60m), ("Status", "Active"));

                // Lô bị chặn có sẵn trong kho (chốt an toàn): bò hết hạn hôm qua 50 kg, bò QC KHONG_DAT 30 kg.
                foreach (var (suffix, expiry, qc, qty) in new[] { ("HH", today.AddDays(-1), "DAT", 50m), ("KD", day.AddDays(5), "KHONG_DAT", 30m) })
                {
                    Guid lot = await Insert<Model.Entity.MaterialLot>("MaterialLot", q => q.Where(x => x.LotNumber == code + "-" + suffix).Select(x => x.Id).SingleAsync(),
                        ("IngredientId", beef), ("SupplierId", supplier), ("LotNumber", code + "-" + suffix), ("ExpiryDate", expiry), ("ReceivedDate", today.AddDays(-10)),
                        ("QcStatus", qc), ("UnitCost", 300_000m), ("Note", "[DEMO] lô bị chặn"));
                    (await Post("/api/InventoryTransaction/Adjust", ("WarehouseId", warehouse), ("IngredientId", beef), ("MaterialLotId", lot), ("Quantity", qty),
                        ("Reason", "[DEMO] Tồn đầu kỳ (lô bị chặn)"))).Ok();
                }

                // ===== P3. Thực đơn tuần → phân công → đơn hàng → chốt
                Guid menu = await Insert<Model.Entity.Menu>("Menu", q => q.Where(x => x.Code == code + "TD").Select(x => x.Id).SingleAsync(),
                    ("Code", code + "TD"), ("Name", "[DEMO] Thực đơn tuần " + k), ("EffectiveFrom", day), ("EffectiveTo", day.AddDays(6)));
                foreach (var (dish, sort) in new[] { (dishBeef, 1), (dishRice, 2) })
                {
                    (await Post("/api/MenuLine/Insert", ("MenuId", menu), ("DishId", dish), ("MealType", "TRUA"), ("DayOfWeek", dow), ("SequenceNo", 1), ("SortOrder", sort))).Ok();
                }
                (await Get($"/api/Menu/GetCalendar?id={menu}&mealType=TRUA&sellingPrice=35000")).Ok();
                (await Post("/api/Menu/Approve", ("Id", menu))).Ok();
                (await Post("/api/MenuAssignment/Insert", ("CustomerId", customer), ("ContractId", contract), ("MenuId", menu), ("MealType", "TRUA"),
                    ("EffectiveFrom", day), ("EffectiveTo", day.AddDays(6)))).Ok();
                (await Get($"/api/Order/GetNewTemplate?customerId={customer}&mealDate={D(day)}")).Ok();
                var order = (await PostJson("/api/Order/Create", new
                {
                    customerId = customer, deliveryLocationId = location, mealDate = D(day), note = "[DEMO] đơn Pilot",
                    listLine = new[] { new { contractMealId = contractMeal, quantity = 1000 } },
                })).Ok();
                Guid orderId = order.GetProperty("order").GetProperty("id").GetGuid();
                order.GetProperty("order").GetProperty("orderNumber").GetString().Should().MatchRegex(@"^PH-\d{6}-\d{4}$");
                order.GetProperty("order").GetProperty("totalAmount").GetDecimal().Should().Be(35_000_000m);
                (await Post("/api/Order/Submit", ("Id", orderId))).Ok();
                (await Post("/api/Order/Confirm", ("Id", orderId), ("Note", "[DEMO] khách xác nhận"))).Ok();
                (await Post("/api/Order/Lock", ("Id", orderId))).Ok();

                // ===== P5. MRP → đề nghị mua nháp
                var mrp = (await Post("/api/MaterialRequirement/Calculate", ("ProductionDate", day), ("WarehouseId", warehouse), ("Note", "[DEMO]"))).Ok();
                Guid planId = mrp.GetProperty("productionPlanId").GetGuid();
                Guid prId = Id(mrp.GetProperty("purchaseRequest"));
                var lines = mrp.GetProperty("listItem").EnumerateArray().ToList();
                var beefLine = lines.Single(l => l.GetProperty("ingredientId").GetGuid() == beef);
                beefLine.GetProperty("requiredQuantity").GetDecimal().Should().Be(200m, "1000 suất × 20 kg / 100 suất");
                beefLine.GetProperty("blockedQuantity").GetDecimal().Should().Be(80m, "lô hết hạn 50 + lô KHONG_DAT 30 không tính vào tồn khả dụng");
                beefLine.GetProperty("purchaseQuantity").GetDecimal().Should().Be(200m);
                lines.Single(l => l.GetProperty("ingredientId").GetGuid() == rice).GetProperty("purchaseQuantity").GetDecimal().Should().Be(120m);

                // ===== P6. Đề nghị → PO → duyệt Giám đốc (≥ 50 triệu) → nhận hàng + QC → nhập kho
                var converted = (await Post("/api/PurchaseRequest/ConvertToPurchaseOrder", ("Id", prId), ("Note", "[DEMO]"))).Ok();
                Guid poId = Id(converted.GetProperty("listPurchaseOrder")[0]);
                var po = (await Get($"/api/PurchaseOrder/GetDetail?id={poId}")).Ok();
                po.GetProperty("purchaseOrder").GetProperty("totalAmount").GetDecimal().Should().Be(62_400_000m, "200 × 300.000 + 120 × 20.000");
                po.GetProperty("needsDirectorApproval").GetBoolean().Should().BeTrue();
                (await Post("/api/PurchaseOrder/Submit", ("Id", poId), ("Note", "[DEMO] gửi duyệt"))).Ok();
                (await Get("/api/ApprovalRequest/GetMyPending?type=scm-po")).ListData.EnumerateArray()
                    .Should().NotContain(a => a.GetProperty("businessId").GetGuid() == poId, "PO ≥ 50 triệu chỉ nằm ở hộp duyệt Giám đốc");
                (await Get("/api/ApprovalRequest/GetMyPending?type=scm-po-director")).ListData.EnumerateArray()
                    .Should().Contain(a => a.GetProperty("businessId").GetGuid() == poId);
                (await Post("/api/PurchaseOrder/Approve", ("Id", poId), ("Note", "[DEMO] GĐ duyệt"))).Ok();
                (await Post("/api/PurchaseOrder/Send", ("Id", poId))).Ok();
                po = (await Get($"/api/PurchaseOrder/GetDetail?id={poId}")).Ok();
                var poLines = po.GetProperty("listLine").EnumerateArray().ToList();
                Guid poLineBeef = Id(poLines.Single(l => l.GetProperty("ingredientId").GetGuid() == beef));
                Guid poLineRice = Id(poLines.Single(l => l.GetProperty("ingredientId").GetGuid() == rice));
                var gr = (await PostJson("/api/GoodsReceipt/Create", new
                {
                    purchaseOrderId = poId, warehouseId = warehouse, note = "[DEMO] nhận hàng",
                    listLine = new object[]
                    {
                        new { purchaseOrderLineId = poLineBeef, receivedQuantity = 200, supplierLotNumber = "[DEMO]-BO-01", manufactureDate = D(today), expiryDate = D(day.AddDays(3)) },
                        new { purchaseOrderLineId = poLineRice, receivedQuantity = 120, supplierLotNumber = "[DEMO]-GAO-01", manufactureDate = D(today), expiryDate = D(day.AddDays(180)) },
                    },
                })).Ok();
                Guid grId = Id(gr.GetProperty("goodsReceipt"));
                Guid qcId = gr.GetProperty("qcInspectionId").GetGuid();
                var grLines = gr.GetProperty("listLine").EnumerateArray().ToList();
                Guid grBeef = Id(grLines.Single(l => l.GetProperty("ingredientId").GetGuid() == beef));
                Guid grRice = Id(grLines.Single(l => l.GetProperty("ingredientId").GetGuid() == rice));
                (await Post("/api/GoodsReceipt/Post", ("Id", grId))).Fail(); // chưa kết luận QC
                // Hàng mát 9 °C = vùng loại → không được kết luận DAT.
                (await PostJson("/api/QcInspection/Conclude", new
                {
                    id = qcId, listLine = new object[]
                    {
                        new { goodsReceiptLineId = grBeef, result = "DAT", temperature = 9 },
                        new { goodsReceiptLineId = grRice, result = "DAT" },
                    },
                })).Fail();
                (await PostJson("/api/QcInspection/Conclude", new
                {
                    id = qcId, note = "[DEMO] QC đầu vào", listLine = new object[]
                    {
                        new { goodsReceiptLineId = grBeef, result = "DAT", temperature = 3.5, appearance = "Tươi", packaging = "Nguyên" },
                        new { goodsReceiptLineId = grRice, result = "DAT", appearance = "Khô", packaging = "Nguyên" },
                    },
                })).Ok();
                (await Post("/api/GoodsReceipt/Post", ("Id", grId), ("Note", "[DEMO] ghi sổ"))).Ok();
                var grDetail = (await Get($"/api/GoodsReceipt/GetDetail?id={grId}")).Ok();
                System.Text.RegularExpressions.Regex.Matches(grDetail.GetRawText(), $"NL-\\d{{6}}-{code}-\\d{{2}}").Select(m => m.Value).Distinct()
                    .Should().HaveCount(2, "mỗi dòng phiếu nhập 1 lô NL- riêng, không trùng số (lỗi T50R1)");

                // ===== P7. Kế hoạch → duyệt → phát hành theo ca → mẻ ME- → xuất NL → nấu → nhiệt độ lõi → lưu mẫu → sản lượng → đạt → đóng gói
                (await Post("/api/ProductionPlan/Approve", ("Id", planId), ("Note", "[DEMO]"))).Ok();
                var released = (await Post("/api/ProductionPlan/Release", ("Id", planId), ("ProductionLineId", line), ("Note", "[DEMO]"))).Ok();
                var batches = released.GetProperty("listBatch").EnumerateArray().ToList();
                batches.Should().HaveCount(2, "1000 suất / dây chuyền 1000 suất / mẻ → 1 mẻ mỗi món");
                batches.Should().OnlyContain(b => System.Text.RegularExpressions.Regex.IsMatch(b.GetProperty("batchNumber").GetString()!, @"^ME-991221-[A-Z0-9]+-\d{2}$"));
                Guid batchBeef = Id(batches.Single(b => b.GetProperty("dishId").GetGuid() == dishBeef));
                Guid batchRice = Id(batches.Single(b => b.GetProperty("dishId").GetGuid() == dishRice));
                foreach (Guid batch in new[] { batchBeef, batchRice })
                {
                    var issue = (await Post("/api/ProductionBatch/CreateMaterialIssue", ("Id", batch), ("WarehouseId", warehouse), ("Post", true), ("Note", "[DEMO]"))).Ok();
                    issue.GetRawText().Should().NotContain(code + "-HH").And.NotContain(code + "-KD");
                    (await Post("/api/ProductionBatch/Start", ("Id", batch))).Ok();
                }
                // Bò kho: 65 °C (có khắc phục) → chặn đạt QC; 75,5 °C → đạt; lưu mẫu.
                (await Post("/api/ProductionBatch/RecordCoreTemperature", ("Id", batchBeef), ("TemperatureC", 65m))).Fail();
                (await Post("/api/ProductionBatch/RecordCoreTemperature", ("Id", batchBeef), ("TemperatureC", 65m), ("CorrectiveAction", "[DEMO] nấu thêm 10 phút"),
                    ("MeasuredAt", DateTime.Now.AddMinutes(-15).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)))).Ok();
                (await Post("/api/ProductionBatch/Finish", ("Id", batchBeef), ("ActualQuantity", 1000m))).Ok();
                (await Post("/api/ProductionBatch/RecordOutput", ("Id", batchBeef), ("ProducedQuantity", 1000m), ("RejectedQuantity", 0m))).Ok();
                (await Post("/api/ProductionBatch/Release", ("Id", batchBeef))).Fail("70");
                (await Post("/api/ProductionBatch/RecordCoreTemperature", ("Id", batchBeef), ("TemperatureC", 75.5m), ("DeviceCode", "[DEMO]-NK"))).Ok();
                (await Post("/api/ProductionBatch/RecordSampleRetained", ("Id", batchBeef), ("ActualValue", "[DEMO] tủ mẫu 150 g"))).Ok();
                (await Post("/api/ProductionBatch/Release", ("Id", batchBeef))).Ok();
                (await Post("/api/ProductionBatch/Pack", ("Id", batchBeef))).Ok();
                // Cơm: 82 °C, đạt + đóng gói nhưng CHƯA lưu mẫu (D-42: chặn ở bước xuất chuyến).
                (await Post("/api/ProductionBatch/RecordCoreTemperature", ("Id", batchRice), ("TemperatureC", 82m), ("DeviceCode", "[DEMO]-NK"))).Ok();
                (await Post("/api/ProductionBatch/Finish", ("Id", batchRice), ("ActualQuantity", 1000m))).Ok();
                (await Post("/api/ProductionBatch/RecordOutput", ("Id", batchRice), ("ProducedQuantity", 1000m), ("RejectedQuantity", 0m))).Ok();
                (await Post("/api/ProductionBatch/Release", ("Id", batchRice))).Ok();
                (await Post("/api/ProductionBatch/Pack", ("Id", batchRice))).Ok();
                (await Get($"/api/ProductionPlan/GetDetail?id={planId}")).Ok().GetProperty("productionPlan").GetProperty("status").GetString().Should().Be("Completed");

                // ===== P8. Chuyến CX- → nhiệt độ xuất phát → xuất phát → bàn giao + POD → đối soát → phản ánh
                (await Get($"/api/DeliveryRoute/GetReadyOrders?DeliveryDate={D(day)}")).Ok();
                var route = (await PostJson("/api/DeliveryRoute/Create", new
                {
                    deliveryDate = D(day), vehicleId = vehicle, description = "[DEMO] chuyến Pilot", listOrderId = new[] { orderId },
                })).Ok();
                Guid routeId = Id(route.GetProperty("route"));
                route.GetProperty("route").GetProperty("routeNo").GetString().Should().MatchRegex($@"^CX-991221-{code}-\d{{2}}$");
                Guid stopId = Id(route.GetProperty("listStop")[0]);
                (await Post("/api/DeliveryRoute/RecordTemperature", ("Id", routeId), ("TemperatureC", 55m), ("CorrectiveAction", "[DEMO] hâm lại thùng"), ("DeviceCode", "[DEMO]-NK"),
                    ("MeasuredAt", DateTime.Now.AddMinutes(-10).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)))).Ok(); // đo trước 10 phút
                (await Post("/api/DeliveryRoute/Dispatch", ("Id", routeId))).Fail("60");
                (await Post("/api/DeliveryRoute/Dispatch", ("Id", routeId), ("TemperatureC", 65m), ("DeviceCode", "[DEMO]-NK"))).Fail("ME-"); // cơm chưa lưu mẫu
                (await Post("/api/ProductionBatch/RecordSampleRetained", ("Id", batchRice), ("ActualValue", "[DEMO] tủ mẫu 150 g"))).Ok();
                (await PostJson("/api/DeliveryRoute/Load", new { id = routeId, note = "[DEMO]" })).Ok(); // BE2.1: xếp hàng lên xe
                (await Post("/api/DeliveryRoute/Dispatch", ("Id", routeId), ("TemperatureC", 65m), ("DeviceCode", "[DEMO]-NK"), ("Note", "[DEMO]"))).Ok();
                (await Post("/api/DeliveryStop/Arrive", ("Id", stopId))).Ok();
                (await Post("/api/DeliveryStop/Deliver", ("Id", stopId), ("TemperatureC", 58m), ("DeliveredQuantity", 1000m), ("AcceptedQuantity", 998m),
                    ("ReceiverName", "[DEMO] Chị Lan"))).Fail("60");
                var delivered = (await Post("/api/DeliveryStop/Deliver", ("Id", stopId), ("TemperatureC", 63m), ("DeliveredQuantity", 1000m), ("AcceptedQuantity", 998m),
                    ("ReceiverName", "[DEMO] Chị Lan"), ("Remark", "[DEMO] 2 suất móp hộp"))).Ok();
                delivered.GetProperty("route").GetProperty("status").GetString().Should().Be("Completed");
                (await Get($"/api/OrderReconciliation/GetPendingOrders?MealDate={D(day)}")).ListData.EnumerateArray()
                    .Should().Contain(o => o.GetProperty("orderId").GetGuid() == orderId);
                var rec = (await Post("/api/OrderReconciliation/Reconcile", ("OrderId", orderId), ("ServedQuantity", 990m), ("ReturnedQuantity", 5m), ("WastedQuantity", 3m),
                    ("Remark", "[DEMO] khách xác nhận"))).Ok();
                rec.GetProperty("reconciliation").GetProperty("finalAmount").GetDecimal().Should().Be(34_650_000m, "35.000.000 × 990 ÷ 1000");
                (await Post("/api/OrderReconciliation/Close", ("Id", Id(rec.GetProperty("reconciliation"))), ("Remark", "[DEMO] chốt"))).Ok();
                var complaint = (await Post("/api/CustomerComplaint/Create", ("CustomerId", customer), ("DeliveryStopId", stopId), ("Severity", "High"),
                    ("SourceChannel", "ZALO"), ("CategoryCode", "QUALITY"), ("Description", "[DEMO] Cơm hơi khô"))).Ok();
                Guid complaintId = Id(complaint.GetProperty("complaint"));
                complaint.GetProperty("complaint").GetProperty("complaintNo").GetString().Should().MatchRegex(@"^PA-\d{6}-\d{4}$");
                (await Post("/api/CustomerComplaint/Assign", ("Id", complaintId), ("Comment", "[DEMO] Bếp trưởng kiểm tra"))).Ok();
                (await Post("/api/CustomerComplaint/Resolve", ("Id", complaintId), ("Comment", "[DEMO] Điều chỉnh nước nấu"))).Ok();
                (await Post("/api/CustomerComplaint/Close", ("Id", complaintId))).Ok();

                db.ChangeTracker.Clear();
                (await db.Order.AsNoTracking().SingleAsync(o => o.Id == orderId)).Status.Should().Be("Reconciled");
                (await db.CustomerComplaint.AsNoTracking().CountAsync(c => c.CustomerId == customer)).Should().Be(2, "1 phản ánh tự động (2 suất từ chối khi bàn giao) + 1 tạo tay");
            }
        }
    }
}
