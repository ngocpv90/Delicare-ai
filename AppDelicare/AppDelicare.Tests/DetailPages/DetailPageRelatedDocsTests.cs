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
