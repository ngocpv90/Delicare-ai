using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;

namespace AppDelicare.Tests.DetailPages
{
    /// <summary>
    /// UI-6 (D-80) – tab chuẩn của trang chi tiết: nhóm "Chứng từ liên quan" mới (CommonRelations – chỉ khóa ngoại có sẵn)
    /// và "Tài liệu" cho 6 danh sách mới đính kèm được (CommonRegistry attachable).
    /// </summary>
    public class DetailPageRelatedDocsTests(InMemoryApiFactory factory) : IClassFixture<InMemoryApiFactory>
    {
        private static readonly DateTime Day = new(2026, 12, 22);

        private static List<(string Key, int Count)> Groups(LegacyResult r)
            { r.Ok(); return r.ListData.EnumerateArray().Select(g => (g.GetProperty("key").GetString()!, g.GetProperty("count").GetInt32())).ToList(); }

        [Fact]
        public async Task Related_groups_cover_plan_order_batch_route_receipt_request_complaint()
        {
            string k = Guid.NewGuid().ToString("N")[..6];
            Guid plan = Guid.NewGuid(), order = Guid.NewGuid(), b1 = Guid.NewGuid(), b2 = Guid.NewGuid(), issue = Guid.NewGuid(), dish = Guid.NewGuid();
            Guid route = Guid.NewGuid(), loc = Guid.NewGuid(), cust = Guid.NewGuid(), sOrder = Guid.NewGuid(), dlv = Guid.NewGuid(), complaint = Guid.NewGuid();
            Guid pr = Guid.NewGuid(), po = Guid.NewGuid(), gr = Guid.NewGuid(), grLine = Guid.NewGuid(), lot = Guid.NewGuid(), ing = Guid.NewGuid();
            await factory.SeedAsync(db =>
            {
                db.Dish.Add(new Dish { Id = dish, Code = "MON-R" + k, Name = "Món " + k, Status = "Active" });
                db.ProductionPlan.Add(new ProductionPlan { Id = plan, PlanNumber = "KH-R" + k, PlanNo = "KH-R" + k, ProductionDate = Day, PlanDate = Day.AddDays(-1), Status = "Released" });
                db.ProductionOrder.Add(new ProductionOrder { Id = order, ProductionOrderNumber = "LSX-R" + k, ProductionOrderNo = "LSX-R" + k, ProductionPlanId = plan, DishId = dish, PlannedQty = 100, Status = "InProgress", PlannedStartDate = Day.AddHours(7) });
                db.ProductionBatch.AddRange(
                    new ProductionBatch { Id = b1, BatchNumber = "ME-R" + k + "-1", BatchNo = "ME-R" + k + "-1", ProductionOrderId = order, DishId = dish, PlannedQuantity = 50, StartDate = Day.AddHours(7), Status = "Cooking" },
                    new ProductionBatch { Id = b2, BatchNumber = "ME-R" + k + "-2", BatchNo = "ME-R" + k + "-2", ProductionOrderId = order, DishId = dish, PlannedQuantity = 50, StartDate = Day.AddHours(8), Status = "Planned" });
                db.MaterialIssue.Add(new MaterialIssue { Id = issue, IssueNumber = "PX-R" + k, IssueNo = "PX-R" + k, ProductionOrderId = order, IssueDate = Day, Status = "Posted" });

                db.Customer.Add(new Customer { Id = cust, CIF = Guid.NewGuid().ToString("N")[..10], CustomerType = "FACTORY", DisplayName = "KH", Code = "KH-R" + k, Name = "Khách " + k, BranchId = Guid.NewGuid(), Status = "Active" });
                db.Order.Add(new Order { Id = sOrder, OrderNumber = "DH-R" + k, OrderNo = "DH-R" + k, CustomerId = cust, OrderDate = Day, MealDate = Day, DeliveryDate = Day, Status = "Confirmed" });
                db.DeliveryRoute.Add(new DeliveryRoute { Id = route, RouteNo = "CX-R" + k, Code = "CX-R" + k, DeliveryDate = Day, Status = "InTransit" });
                db.DeliveryOrder.Add(new DeliveryOrder { Id = dlv, DeliveryNumber = "PG-R" + k, DeliveryNo = "PG-R" + k, OrderId = sOrder, CustomerId = cust, DeliveryDate = Day, DeliveryRouteId = route, CustomerDeliveryLocationId = loc, Status = "Delivered" });
                db.CustomerDeliveryLocation.Add(new CustomerDeliveryLocation { Id = loc, CustomerId = cust, Code = "DP-R" + k, Name = "Điểm " + k, Status = "Active" });
                db.CustomerComplaint.Add(new CustomerComplaint { Id = complaint, ComplaintNo = "PA-R" + k, CustomerId = cust, OrderId = sOrder, DeliveryOrderId = dlv, DeliveryRouteId = route, ReceivedAt = Day, ComplaintStatus = "Open", Description = "Thiếu suất" });

                db.Ingredient.Add(new Ingredient { Id = ing, Code = "NL-R" + k, Name = "NL " + k });
                db.PurchaseRequest.Add(new PurchaseRequest { Id = pr, RequestNumber = "DN-R" + k, RequestNo = "DN-R" + k, RequestDate = Day, Status = "Approved" });
                db.PurchaseOrder.Add(new PurchaseOrder { Id = po, PurchaseOrderNumber = "PO-R" + k, PONo = "PO-R" + k, PurchaseRequestId = pr, OrderDate = Day, Status = "Approved" });
                db.GoodsReceipt.Add(new GoodsReceipt { Id = gr, ReceiptNumber = "PN-R" + k, ReceiptNo = "PN-R" + k, PurchaseOrderId = po, ReceiptDate = Day, Status = "Posted" });
                db.GoodsReceiptLine.Add(new GoodsReceiptLine { Id = grLine, GoodsReceiptId = gr, IngredientId = ing });
                db.MaterialLot.Add(new MaterialLot { Id = lot, LotNumber = "LO-R" + k, LotNo = "LO-R" + k, IngredientId = ing, GoodsReceiptLineId = grLine, ReceivedDate = Day, Status = "Available" });
                return Task.CompletedTask;
            });
            using var admin = factory.CreateAuthorizedClient();
            async Task<List<(string Key, int Count)>> Rel(string res, Guid id) => Groups(await admin.GetResultAsync($"/api/Common/GetRelated?resource={res}&id={id}"));

            (await Rel("ProductionPlan", plan)).Should().Equal(("ProductionOrder", 1), ("ProductionBatch", 2));
            (await Rel("ProductionOrder", order)).Should().Equal(("ProductionBatch", 2), ("MaterialIssue", 1));
            (await Rel("ProductionBatch", b1)).Should().Equal(("ProductionOrder", 1), ("MaterialIssue", 1));
            (await Rel("DeliveryRoute", route)).Should().Equal(("DeliveryOrder", 1), ("CustomerComplaint", 1));
            (await Rel("CustomerComplaint", complaint)).Should().Equal(("Order", 1), ("DeliveryOrder", 1));
            (await Rel("CustomerDeliveryLocation", loc)).Should().Equal(("DeliveryOrder", 1));
            (await Rel("PurchaseRequest", pr)).Should().Equal(("PurchaseOrder", 1));
            (await Rel("GoodsReceipt", gr)).Should().Equal(("PurchaseOrder", 1), ("MaterialLot", 1), ("SupplierIssue", 0));
        }

        [Fact]
        public async Task PurchaseOrder_received_percent_is_value_weighted_across_units()
        {
            // 10 kg thịt × 100.000 đã nhận đủ; 1.000 g gia vị × 1.000 chưa nhận → theo giá trị 50 % (cộng số lượng khác đơn vị sẽ ra ~1 %).
            string k = Guid.NewGuid().ToString("N")[..6];
            Guid wh = Guid.NewGuid(), kg = Guid.NewGuid(), g = Guid.NewGuid(), meat = Guid.NewGuid(), spice = Guid.NewGuid(), sup = Guid.NewGuid(), po = Guid.NewGuid(), gr = Guid.NewGuid();
            await factory.SeedAsync(db =>
            {
                db.Warehouse.Add(new Warehouse { Id = wh, Code = "KHO-P" + k, Name = "Kho " + k });
                db.UnitMeasure.AddRange(new UnitMeasure { Id = kg, Code = "KG-P" + k, Name = "kg" }, new UnitMeasure { Id = g, Code = "G-P" + k, Name = "g" });
                db.Ingredient.AddRange(new Ingredient { Id = meat, Code = "NL-P1" + k, Name = "Thịt " + k, BaseUnitId = kg }, new Ingredient { Id = spice, Code = "NL-P2" + k, Name = "Tiêu " + k, BaseUnitId = g });
                db.Supplier.Add(new Supplier { Id = sup, Code = "NCC-P" + k, Name = "NCC " + k, Status = "Active" });
                db.PurchaseOrder.Add(new PurchaseOrder { Id = po, PurchaseOrderNumber = "PO-P" + k, SupplierId = sup, OrderDate = Day, Status = "PartiallyReceived", TotalAmount = 2_000_000 });
                db.PurchaseOrderLine.AddRange(
                    new PurchaseOrderLine { Id = Guid.NewGuid(), PurchaseOrderId = po, IngredientId = meat, Quantity = 10, UnitId = kg, UomId = kg, UnitPrice = 100_000, LineAmount = 1_000_000 },
                    new PurchaseOrderLine { Id = Guid.NewGuid(), PurchaseOrderId = po, IngredientId = spice, Quantity = 1000, UnitId = g, UomId = g, UnitPrice = 1_000, LineAmount = 1_000_000 });
                db.GoodsReceipt.Add(new GoodsReceipt { Id = gr, ReceiptNumber = "PN-P" + k, PurchaseOrderId = po, SupplierId = sup, WarehouseId = wh, ReceiptDate = Day, Status = "Posted" });
                db.GoodsReceiptLine.Add(new GoodsReceiptLine { Id = Guid.NewGuid(), GoodsReceiptId = gr, IngredientId = meat, UnitId = kg, UomId = kg, Quantity = 10, ReceivedQuantity = 10, AcceptedQuantity = 10, UnitCost = 100_000 });
                return Task.CompletedTask;
            });
            using var admin = factory.CreateAuthorizedClient();
            var o = (await admin.GetResultAsync($"/api/PurchaseOrder/GetOverview?id={po}")).Ok();
            o.GetProperty("receivedPercent").GetDecimal().Should().Be(50);
        }

        [Fact]
        public async Task Route_overview_shows_shift_derived_from_its_orders()
        {
            string k = Guid.NewGuid().ToString("N")[..6];
            Guid shift = Guid.NewGuid(), cust = Guid.NewGuid(), order = Guid.NewGuid(), route = Guid.NewGuid();
            await factory.SeedAsync(db =>
            {
                db.Shift.Add(new Shift { Id = shift, Code = "CA-S" + k, Name = "Ca trưa " + k, StartTime = new TimeSpan(10, 30, 0), EndTime = new TimeSpan(13, 30, 0), Status = "Active" });
                db.Customer.Add(new Customer { Id = cust, CIF = Guid.NewGuid().ToString("N")[..10], CustomerType = "FACTORY", DisplayName = "KH", Code = "KH-S" + k, Name = "Khách " + k, BranchId = Guid.NewGuid(), Status = "Active" });
                db.Order.Add(new Order { Id = order, OrderNumber = "DH-S" + k, OrderNo = "DH-S" + k, CustomerId = cust, OrderDate = Day, MealDate = Day, DeliveryDate = Day, Status = "Confirmed" });
                db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = order, ShiftId = shift, MealType = "LUNCH", Quantity = 100 });
                db.DeliveryRoute.Add(new DeliveryRoute { Id = route, RouteNo = "CX-S" + k, Code = "CX-S" + k, DeliveryDate = Day, Status = "Planned" });
                db.DeliveryOrder.Add(new DeliveryOrder { Id = Guid.NewGuid(), DeliveryNumber = "PG-S" + k, DeliveryNo = "PG-S" + k, OrderId = order, CustomerId = cust, DeliveryDate = Day, DeliveryRouteId = route, Status = "Planned" });
                return Task.CompletedTask;
            });
            using var admin = factory.CreateAuthorizedClient();
            var o = (await admin.GetResultAsync($"/api/DeliveryRoute/GetOverview?id={route}")).Ok();
            o.GetProperty("shiftNames").EnumerateArray().Select(x => x.GetString()).Should().Equal("Ca trưa " + k);
        }

        [Theory]
        [InlineData("Employee")]
        [InlineData("Ingredient")]
        [InlineData("MaterialLot")]
        [InlineData("ProductionOrder")]
        [InlineData("ProductionPlan")]
        [InlineData("Dish")]
        [InlineData("CustomerDeliveryLocation")]
        public async Task Documents_tab_is_supported(string resource)
        {
            using var admin = factory.CreateAuthorizedClient();
            var r = await admin.GetResultAsync($"api/DocumentFile/GetList?Resource={resource}&RecordId={Guid.NewGuid()}");
            r.Ok();
        }
    }
}
