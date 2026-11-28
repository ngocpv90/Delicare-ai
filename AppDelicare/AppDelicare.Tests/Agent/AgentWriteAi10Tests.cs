using System.Text.Json.Nodes;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Implement;
using AppDelicare.Services.Implement.Agent;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Agent
{
    /// <summary>
    /// AI-10 (D-133 §4): 11 lệnh ghi 2 pha của WH / KITCHEN / QA / SALES / LOG / HR. Mỗi lệnh 1 test: đề xuất → bảng nghiệp vụ KHÔNG đổi; duyệt (admin) →
    /// dữ liệu đúng như thao tác tay trên web (cùng method Service).
    /// </summary>
    public class AgentWriteAi10Tests
    {
        private static readonly DateTime Today = DateTime.Today;
        private static readonly DateTime Now = Today.AddHours(10);

        private static async Task<IResult<bool>> DecideAsync(InMemoryApiFactory f, Guid requestId, Guid user, bool approve = true)
        {
            using var scope = f.Services.CreateScope();
            using var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForUser(user));
            return await scope.ServiceProvider.GetRequiredService<IApprovalRequestService>().Decide(new ApprovalDecideRequest { Id = requestId, Approve = approve, Note = "OK" });
        }

        private static Guid RequestIdOf(AgentCallResult r) => Guid.Parse(((JsonNode)r.Data!)["approvalRequestId"]!.GetValue<string>());

        private static async Task<InMemoryApiFactory> NewAsync()
        {
            var f = await AgentToolsAi3Tests.FactoryAsync();
            f.Clock.FixedNow = Now;
            return f;
        }

        /// <summary>Đề xuất (200) → kiểm bảng nghiệp vụ chưa đổi → duyệt bằng admin (200).</summary>
        private static async Task ProposeThenApproveAsync(InMemoryApiFactory f, string agent, string tool, object args, Func<Task> assertUnchanged)
        {
            f.CreateAuthorizedClient().Dispose(); // tạo admin test
            var r = await AgentToolsAi3Tests.Call(f, agent, tool, args);
            r.Status.Should().Be(200, r.Message);
            await assertUnchanged();
            var ok = await DecideAsync(f, RequestIdOf(r), InMemoryApiFactory.UserId);
            ok.ResponseCode.Should().Be(200, ok.ResponseMessage);
            (await f.QueryAsync(db => db.ApprovalRequest.SingleAsync(a => a.Id == RequestIdOf(r)))).RequestStatus.Should().Be("Approved");
        }

        private static async Task<T> AsAdminAsync<T>(InMemoryApiFactory f, Func<IServiceProvider, Task<T>> run)
        {
            f.CreateAuthorizedClient().Dispose();
            using var scope = f.Services.CreateScope();
            using var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForUser(InMemoryApiFactory.UserId));
            return await run(scope.ServiceProvider);
        }

        // ------------------------------------------------------------------ WH
        private sealed record Stock(Guid Warehouse, Guid Unit, Guid Ingredient, Guid Lot);

        /// <summary>Kho K10 + nguyên liệu NL10 + lô LO10 (40 kg nhập qua sổ cái, QC đạt).</summary>
        private static async Task<Stock> SeedStockAsync(InMemoryApiFactory f)
        {
            var s = new Stock(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            await f.SeedAsync(async db =>
            {
                db.Warehouse.Add(new Warehouse { Id = s.Warehouse, Code = "K10", Name = "Kho 10", Status = "Active" });
                db.UnitMeasure.Add(new UnitMeasure { Id = s.Unit, Code = "KG-10", Name = "kg" });
                db.Ingredient.Add(new Ingredient { Id = s.Ingredient, Code = "NL10", Name = "Thịt 10", BaseUnitId = s.Unit, IsExpiryManaged = true });
                db.MaterialLot.Add(new MaterialLot
                {
                    Id = s.Lot, IngredientId = s.Ingredient, LotNumber = "LO10", LotNo = "LO10", ExpiryDate = Today.AddDays(10), ReceivedDate = Today.AddDays(-1),
                    QcStatus = "DAT", QualityStatus = "DAT", Status = "Available", UnitCost = 1000,
                });
                await new InventoryLedgerService(db).PostAsync(new InventoryPostRequest
                {
                    TransactionType = "RECEIPT", TransactionNumber = "NK-LO10", WarehouseId = s.Warehouse, IngredientId = s.Ingredient, MaterialLotId = s.Lot, UnitId = s.Unit, QuantityIn = 40, UnitCost = 1000,
                });
            });
            return s;
        }

        private sealed record Po(Guid Warehouse, Guid Ingredient, Guid PurchaseOrder, Guid Line);

        /// <summary>Đơn mua PO-A10 Đã gửi NCC: 2 kg thịt (cần QC, hàng mát).</summary>
        private static async Task<Po> SeedPurchaseOrderAsync(InMemoryApiFactory f)
        {
            var p = new Po(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            Guid kg = Guid.NewGuid(), sup = Guid.NewGuid();
            await f.SeedAsync(db =>
            {
                db.Warehouse.Add(new Warehouse { Id = p.Warehouse, Code = "K10", Name = "Kho 10", Status = "Active" });
                db.UnitMeasure.Add(new UnitMeasure { Id = kg, Code = "KG-A10", Name = "kg" });
                db.Supplier.Add(new Supplier { Id = sup, Code = "S-A10", Name = "NCC A10", Status = "Active" });
                db.Ingredient.Add(new Ingredient { Id = p.Ingredient, Code = "THIT-A10", Name = "Thịt A10", BaseUnitId = kg, StorageType = "CHILLED", QcRequired = true });
                db.PurchaseOrder.Add(new PurchaseOrder { Id = p.PurchaseOrder, PurchaseOrderNumber = "PO-A10", PONo = "PO-A10", SupplierId = sup, OrderDate = Today.AddDays(-1), ExpectedDeliveryDate = Today, Status = "Sent", TotalAmount = 200_000 });
                db.PurchaseOrderLine.Add(new PurchaseOrderLine { Id = p.Line, PurchaseOrderId = p.PurchaseOrder, IngredientId = p.Ingredient, Quantity = 2, UnitId = kg, UomId = kg, UnitPrice = 100_000, LineAmount = 200_000, Amount = 200_000 });
                return Task.CompletedTask;
            });
            return p;
        }

        [Fact]
        public async Task Goods_receipt_proposal_creates_receipt_with_lines_after_approval()
        {
            using var f = await NewAsync();
            var p = await SeedPurchaseOrderAsync(f);
            await ProposeThenApproveAsync(f, "WH", "propose_goods_receipt", new
            {
                so_po = "PO-A10", ma_kho = "K10", ly_do = "Xe NCC đã tới",
                dong = new[] { new { ma_nl = "THIT-A10", so_luong = 2m, so_lo_ncc = "LOT-NCC-1", han_dung = Today.AddDays(5).ToString("yyyy-MM-dd"), nhiet_do = 3m } },
            }, async () => (await f.QueryAsync(db => db.GoodsReceipt.CountAsync())).Should().Be(0, "chưa duyệt chưa lập phiếu"));

            var gr = await f.QueryAsync(db => db.GoodsReceipt.AsNoTracking().SingleAsync());
            (gr.PurchaseOrderId, gr.WarehouseId).Should().Be((p.PurchaseOrder, p.Warehouse));
            gr.Status.Should().Be(GoodsReceiptStatus.QcPending, "thịt cần QC → chờ kết luận như Nhận hàng trên web");
            gr.Note.Should().Contain("[Agent WH]");
            var line = await f.QueryAsync(db => db.GoodsReceiptLine.AsNoTracking().SingleAsync(l => l.GoodsReceiptId == gr.Id));
            (line.IngredientId, line.ReceivedQuantity, line.ExpiryDate).Should().Be((p.Ingredient, 2m, Today.AddDays(5)));
            (await f.QueryAsync(db => db.QcInspection.CountAsync(q => q.GoodsReceiptId == gr.Id))).Should().Be(1);
        }

        [Fact]
        public async Task Issue_proposal_creates_draft_issue_for_production_order_after_approval()
        {
            using var f = await NewAsync();
            var s = await SeedStockAsync(f);
            Guid order = Guid.NewGuid();
            await f.SeedAsync(db =>
            {
                db.ProductionOrder.Add(new ProductionOrder { Id = order, ProductionPlanId = Guid.NewGuid(), DishId = Guid.NewGuid(), RecipeId = Guid.NewGuid(), PlannedQty = 50, Status = "Released", ProductionOrderNumber = "LSX-A10", ProductionOrderNo = "LSX-A10", PlannedStartDate = Now });
                return Task.CompletedTask;
            });
            await ProposeThenApproveAsync(f, "WH", "propose_issue", new
            {
                so_lenh = "LSX-A10", ma_kho = "K10", ly_do = "Bếp cần thịt ca trưa",
                dong = new[] { new { ma_nl = "NL10", so_luong = 5m, so_lo = "LO10" } },
            }, async () => (await f.QueryAsync(db => db.MaterialIssue.CountAsync())).Should().Be(0));

            var issue = await f.QueryAsync(db => db.MaterialIssue.AsNoTracking().SingleAsync());
            (issue.ProductionOrderId, issue.WarehouseId, issue.Status).Should().Be((order, s.Warehouse, MaterialIssueStatus.Draft));
            var line = await f.QueryAsync(db => db.MaterialIssueLine.AsNoTracking().SingleAsync(l => l.MaterialIssueId == issue.Id));
            (line.IngredientId, line.MaterialLotId, line.Quantity).Should().Be((s.Ingredient, s.Lot, 5m));
            (await f.QueryAsync(db => db.InventoryBalance.Where(b => b.IngredientId == s.Ingredient).SumAsync(b => b.OnHandQuantity))).Should().Be(40, "Nháp chưa ghi sổ – như web");
        }

        [Fact]
        public async Task Stock_count_proposal_creates_draft_count_for_one_ingredient_after_approval()
        {
            using var f = await NewAsync();
            var s = await SeedStockAsync(f);
            await ProposeThenApproveAsync(f, "WH", "propose_stock_count", new { ma_kho = "K10", ma_nl = "NL10", ly_do = "Lệch tồn thịt" },
                async () => (await f.QueryAsync(db => db.StockCount.CountAsync())).Should().Be(0));

            var count = await f.QueryAsync(db => db.StockCount.AsNoTracking().SingleAsync());
            (count.WarehouseId, count.Status, count.CountType, count.CountDate).Should().Be((s.Warehouse, StockCountStatus.Draft, "ADHOC", Today));
            var line = await f.QueryAsync(db => db.StockCountLine.AsNoTracking().SingleAsync(l => l.StockCountId == count.Id));
            (line.IngredientId, line.MaterialLotId, line.SystemQuantity).Should().Be((s.Ingredient, s.Lot, 40m));
        }

        // ------------------------------------------------------------------ KITCHEN
        [Fact]
        public async Task Batch_temperature_proposal_records_core_temperature_after_approval()
        {
            using var f = await NewAsync();
            Guid order = Guid.NewGuid(), batch = Guid.NewGuid(), dish = Guid.NewGuid();
            await f.SeedAsync(db =>
            {
                db.Dish.Add(new Dish { Id = dish, Code = "MON-A10", Name = "Cơm gà A10", Status = "Active" });
                db.ProductionOrder.Add(new ProductionOrder { Id = order, ProductionPlanId = Guid.NewGuid(), DishId = dish, RecipeId = Guid.NewGuid(), PlannedQty = 50, Status = "InProgress", ProductionOrderNumber = "LSX-B10", PlannedStartDate = Now.AddHours(-3) });
                db.ProductionBatch.Add(new ProductionBatch { Id = batch, ProductionOrderId = order, DishId = dish, BatchNumber = "ME-A10-01", BatchNo = "ME-A10-01", StartDate = Now.AddHours(-1), PlannedQuantity = 50, Status = ProductionBatchStatus.Cooking });
                return Task.CompletedTask;
            });
            await ProposeThenApproveAsync(f, "KITCHEN", "propose_batch_event", new { so_me = "ME-A10-01", su_kien = "nhiet_do", nhiet_do = 78m, ly_do = "Bếp đo lõi" },
                async () => (await f.QueryAsync(db => db.TemperatureLog.CountAsync(t => t.ProductionBatchId == batch))).Should().Be(0));

            var log = await f.QueryAsync(db => db.TemperatureLog.AsNoTracking().SingleAsync(t => t.ProductionBatchId == batch));
            (log.MeasurementType, log.TemperatureC, log.IsWithinRange, log.RecordedBy).Should().Be(("CORE", 78m, true, InMemoryApiFactory.UserId));
            log.MeasuredAt.Should().Be(Now);
        }

        // ------------------------------------------------------------------ QA
        [Fact]
        public async Task Qc_result_proposal_concludes_receipt_lines_after_approval()
        {
            using var f = await NewAsync();
            var p = await SeedPurchaseOrderAsync(f);
            var created = await AsAdminAsync(f, sp => sp.GetRequiredService<IGoodsReceiptService>().Create(new GoodsReceiptCreateRequest
            {
                PurchaseOrderId = p.PurchaseOrder, WarehouseId = p.Warehouse,
                ListLine = [new GoodsReceiptLineInput { PurchaseOrderLineId = p.Line, ReceivedQuantity = 2, ExpiryDate = Today.AddDays(5), TemperatureC = 3 }],
            }));
            created.ResponseCode.Should().Be(200, created.ResponseMessage);
            string no = created.Data!.GoodsReceipt!.ReceiptNumber!;
            Guid grId = created.Data.GoodsReceipt.Id;
            created.Data.GoodsReceipt.Status.Should().Be(GoodsReceiptStatus.QcPending);

            await ProposeThenApproveAsync(f, "QA", "propose_qc_result", new
            {
                so_phieu_nhap = no, ly_do = "Kiểm cảm quan đạt",
                dong = new[] { new { ma_nl = "THIT-A10", ket_qua = "DAT", sl_chap_nhan = 2m, nhiet_do = 3m, ghi_chu = "Màu đẹp" } },
            }, async () => (await f.QueryAsync(db => db.GoodsReceipt.AsNoTracking().SingleAsync(g => g.Id == grId))).Status.Should().Be(GoodsReceiptStatus.QcPending));

            var gr = await f.QueryAsync(db => db.GoodsReceipt.AsNoTracking().SingleAsync(g => g.Id == grId));
            gr.Status.Should().BeOneOf(GoodsReceiptStatus.Accepted, GoodsReceiptStatus.Posted);
            var line = await f.QueryAsync(db => db.GoodsReceiptLine.AsNoTracking().SingleAsync(l => l.GoodsReceiptId == grId));
            line.AcceptedQuantity.Should().Be(2);
            var qcLine = await f.QueryAsync(db => db.QcInspectionLine.AsNoTracking().SingleAsync(l => l.GoodsReceiptLineId == line.Id));
            qcLine.Result.Should().Be(IncomingQcResult.Dat);
        }

        // ------------------------------------------------------------------ SALES
        private sealed record Sale(Guid Order, Guid Line);

        private static async Task<Sale> SeedOrderAsync(InMemoryApiFactory f, DateTime mealDate, string status)
        {
            var s = new Sale(Guid.NewGuid(), Guid.NewGuid());
            Guid customer = Guid.NewGuid();
            await f.SeedAsync(db =>
            {
                db.Customer.Add(new Customer { Id = customer, Code = "KH-A10", Name = "Khách A10", CIF = "KH-A10", CustomerType = "FACTORY", DisplayName = "Khách A10", Status = "Active" });
                db.Order.Add(new Order { Id = s.Order, OrderNumber = "DH-A10", OrderNo = "DH-A10", CustomerId = customer, OrderDate = mealDate.AddDays(-3), MealDate = mealDate, DeliveryDate = mealDate, Status = status, TotalQuantity = 100, TotalAmount = 4_000_000 });
                db.OrderLine.Add(new OrderLine { Id = s.Line, OrderId = s.Order, MealType = "TRUA", Quantity = 100, UnitPrice = 40_000, LineAmount = 4_000_000, Amount = 4_000_000 });
                return Task.CompletedTask;
            });
            return s;
        }

        [Fact]
        public async Task Order_change_before_cutoff_updates_line_quantities_after_approval()
        {
            using var f = await NewAsync();
            var s = await SeedOrderAsync(f, Today.AddDays(3), OrderStatus.Confirmed);
            await ProposeThenApproveAsync(f, "SALES", "propose_order_change", new { so_don = "DH-A10", ly_do = "Khách báo tăng ca", dong = new[] { new { loai_suat = "trua", so_suat = 120m } } },
                async () => (await f.QueryAsync(db => db.OrderLine.AsNoTracking().SingleAsync(l => l.Id == s.Line))).Quantity.Should().Be(100));

            (await f.QueryAsync(db => db.OrderLine.AsNoTracking().SingleAsync(l => l.Id == s.Line))).Quantity.Should().Be(120);
            var order = await f.QueryAsync(db => db.Order.AsNoTracking().SingleAsync(o => o.Id == s.Order));
            (order.Status, order.TotalQuantity).Should().Be((OrderStatus.Confirmed, 120m));
            order.Note.Should().Contain("[Agent SALES]");
            (await f.QueryAsync(db => db.ApprovalRequest.CountAsync(a => a.BusinessId == s.Order))).Should().Be(0, "trước giờ chốt sửa thẳng như web");
        }

        [Fact]
        public async Task Order_change_on_locked_order_creates_change_after_cutoff_request_instead_of_changing_lines()
        {
            using var f = await NewAsync();
            var s = await SeedOrderAsync(f, Today, OrderStatus.Locked); // giờ chốt = 20:00 hôm qua
            await ProposeThenApproveAsync(f, "SALES", "propose_order_change", new { so_don = "DH-A10", ly_do = "Khách báo giảm ca", dong = new[] { new { loai_suat = "TRUA", so_suat = 90m } } },
                async () => (await f.QueryAsync(db => db.ApprovalRequest.CountAsync(a => a.BusinessId == s.Order))).Should().Be(0));

            (await f.QueryAsync(db => db.OrderLine.AsNoTracking().SingleAsync(l => l.Id == s.Line))).Quantity.Should().Be(100, "sau chốt chỉ tạo yêu cầu – chưa đổi số");
            var req = await f.QueryAsync(db => db.ApprovalRequest.AsNoTracking().SingleAsync(a => a.BusinessId == s.Order));
            (req.BusinessType, req.RequestStatus).Should().Be((OrderStatus.ChangeAfterCutoffBusinessType, "Pending"));
            (await f.QueryAsync(db => db.Order.AsNoTracking().SingleAsync(o => o.Id == s.Order))).Status.Should().Be(OrderStatus.Locked);
        }

        // ------------------------------------------------------------------ LOG
        private sealed record Route(Guid Route1, Guid Route2, Guid DeliveryOrder, Guid Stop);

        /// <summary>Đơn hàng Đang sản xuất + đơn giao GH-A10 ở điểm #1 của chuyến CX-A10-01; chuyến CX-A10-02 cùng ngày (chuyến 1 ở trạng thái <paramref name="route1Status"/>).</summary>
        private static async Task<Route> SeedRoutesAsync(InMemoryApiFactory f, string route1Status)
        {
            var r = new Route(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            Guid customer = Guid.NewGuid(), location = Guid.NewGuid(), order = Guid.NewGuid();
            await f.SeedAsync(db =>
            {
                db.Customer.Add(new Customer { Id = customer, Code = "KH-L10", Name = "Khách L10", CIF = "KH-L10", CustomerType = "FACTORY", DisplayName = "Khách L10", Status = "Active" });
                db.CustomerDeliveryLocation.Add(new CustomerDeliveryLocation { Id = location, CustomerId = customer, Code = "DL10", Name = "Căng tin L10", DeliveryWindowFrom = new TimeSpan(11, 0, 0) });
                db.Order.Add(new Order { Id = order, OrderNumber = "DH-L10", OrderNo = "DH-L10", CustomerId = customer, DeliveryLocationId = location, OrderDate = Today.AddDays(-2), MealDate = Today, DeliveryDate = Today, Status = OrderStatus.InProduction, TotalQuantity = 40 });
                db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = order, MealType = "TRUA", Quantity = 40, UnitPrice = 30_000, LineAmount = 1_200_000 });
                db.DeliveryRoute.AddRange(
                    new DeliveryRoute { Id = r.Route1, RouteNo = "CX-A10-01", Code = "CX-A10-01", DeliveryDate = Today, Status = route1Status, ActualStart = route1Status == DeliveryRouteStatus.InTransit ? Now.AddHours(-1) : null },
                    new DeliveryRoute { Id = r.Route2, RouteNo = "CX-A10-02", Code = "CX-A10-02", DeliveryDate = Today, Status = DeliveryRouteStatus.Planned });
                db.DeliveryOrder.Add(new DeliveryOrder
                {
                    Id = r.DeliveryOrder, DeliveryNumber = "GH-A10", DeliveryNo = "GH-A10", OrderId = order, CustomerId = customer, DeliveryLocationId = location, CustomerDeliveryLocationId = location,
                    DeliveryDate = Today, PlannedDeliveryTime = Today.AddHours(11), DeliveryRouteId = r.Route1,
                    Status = route1Status == DeliveryRouteStatus.InTransit ? DeliveryOrderStatus.InTransit : DeliveryOrderStatus.Planned,
                });
                db.DeliveryStop.Add(new DeliveryStop { Id = r.Stop, DeliveryRouteId = r.Route1, DeliveryOrderId = r.DeliveryOrder, CustomerId = customer, DeliveryLocationId = location, SequenceNo = 1, PlannedArrival = Today.AddHours(11), PlannedMealQty = 40, StopStatus = DeliveryStopStatus.Planned });
                return Task.CompletedTask;
            });
            return r;
        }

        [Fact]
        public async Task Route_proposal_moves_delivery_order_to_target_route_after_approval()
        {
            using var f = await NewAsync();
            var r = await SeedRoutesAsync(f, DeliveryRouteStatus.Planned);
            await ProposeThenApproveAsync(f, "LOG", "propose_route", new { so_don_giao = "GH-A10", so_chuyen = "CX-A10-02", ly_do = "Xe 1 quá tải" },
                async () => (await f.QueryAsync(db => db.DeliveryOrder.AsNoTracking().SingleAsync(d => d.Id == r.DeliveryOrder))).DeliveryRouteId.Should().Be(r.Route1));

            (await f.QueryAsync(db => db.DeliveryOrder.AsNoTracking().SingleAsync(d => d.Id == r.DeliveryOrder))).DeliveryRouteId.Should().Be(r.Route2);
            var stops = await f.QueryAsync(db => db.DeliveryStop.AsNoTracking().Where(s => s.DeliveryOrderId == r.DeliveryOrder && !s.IsDeleted).ToListAsync());
            stops.Should().ContainSingle();
            (stops[0].DeliveryRouteId, stops[0].SequenceNo, stops[0].PlannedMealQty, stops[0].StopStatus).Should().Be((r.Route2, 1, 40m, DeliveryStopStatus.Planned));
            (await f.QueryAsync(db => db.DeliveryStop.CountAsync(s => s.DeliveryRouteId == r.Route1 && !s.IsDeleted))).Should().Be(0, "điểm cũ gỡ khỏi chuyến 1");
        }

        [Fact]
        public async Task Delivery_arrive_proposal_marks_stop_arrived_and_records_temperature_after_approval()
        {
            using var f = await NewAsync();
            var r = await SeedRoutesAsync(f, DeliveryRouteStatus.InTransit);
            await ProposeThenApproveAsync(f, "LOG", "propose_delivery_event", new { so_chuyen = "CX-A10-01", thu_tu = 1, su_kien = "den", nhiet_do = 65m, ly_do = "Tài xế báo đã tới" },
                async () => (await f.QueryAsync(db => db.DeliveryStop.AsNoTracking().SingleAsync(s => s.Id == r.Stop))).StopStatus.Should().Be(DeliveryStopStatus.Planned));

            var stop = await f.QueryAsync(db => db.DeliveryStop.AsNoTracking().SingleAsync(s => s.Id == r.Stop));
            (stop.StopStatus, stop.ActualArrival).Should().Be((DeliveryStopStatus.Arrived, Now));
            stop.Note.Should().Contain("[Agent LOG]");
            (await f.QueryAsync(db => db.DeliveryEvent.CountAsync(e => e.DeliveryStopId == r.Stop && e.EventType == "ARRIVED"))).Should().Be(1);
            (await f.QueryAsync(db => db.TemperatureLog.CountAsync(t => t.MeasurementType == "DELIVERY" && t.TemperatureC == 65m))).Should().Be(1, "lần đo tại điểm như tài xế ghi trên web");
        }

        // ------------------------------------------------------------------ HR
        private sealed record Staff(Guid Shift, Guid E1, Guid E2, Guid A1, Guid A2);

        /// <summary>NV10-1 có ca ngày mai (A1); NV10-2 có ca ngày kia (A2).</summary>
        private static async Task<Staff> SeedStaffAsync(InMemoryApiFactory f)
        {
            var w = new Staff(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            DateTime d1 = Today.AddDays(1), d2 = Today.AddDays(2);
            await f.SeedAsync(db =>
            {
                Guid unit = Guid.NewGuid();
                db.OrganizationUnit.Add(new OrganizationUnit { Id = unit, Code = "T-A10", Name = "Tổ A10" });
                db.Shift.Add(new Shift { Id = w.Shift, Code = "S-A10", Name = "Sáng A10", StartTime = new TimeSpan(6, 0, 0), EndTime = new TimeSpan(14, 0, 0), Status = "Active" });
                db.Employee.Add(new Employee { Id = w.E1, Code = "NV10-1", EmployeeCode = "NV10-1", FullName = "NV A10 1", OrganizationUnitId = unit, Status = "Active" });
                db.Employee.Add(new Employee { Id = w.E2, Code = "NV10-2", EmployeeCode = "NV10-2", FullName = "NV A10 2", OrganizationUnitId = unit, Status = "Active" });
                db.ShiftAssignment.Add(new ShiftAssignment { Id = w.A1, EmployeeId = w.E1, ShiftId = w.Shift, WorkDate = d1, PlannedStart = d1.AddHours(6), PlannedEnd = d1.AddHours(14), AssignmentStatus = "Planned" });
                db.ShiftAssignment.Add(new ShiftAssignment { Id = w.A2, EmployeeId = w.E2, ShiftId = w.Shift, WorkDate = d2, PlannedStart = d2.AddHours(6), PlannedEnd = d2.AddHours(14), AssignmentStatus = "Planned" });
                return Task.CompletedTask;
            });
            return w;
        }

        [Fact]
        public async Task Leave_proposal_creates_leave_request_for_employee_after_approval()
        {
            using var f = await NewAsync();
            var w = await SeedStaffAsync(f);
            DateTime from = Today.AddDays(5), to = Today.AddDays(6);
            await ProposeThenApproveAsync(f, "HR", "propose_leave", new { ma_nv = "NV10-1", tu_ngay = from.ToString("yyyy-MM-dd"), den_ngay = to.ToString("yyyy-MM-dd"), loai_nghi = "annual", ly_do = "Việc gia đình" },
                async () => (await f.QueryAsync(db => db.LeaveRequest.CountAsync())).Should().Be(0));

            var leave = await f.QueryAsync(db => db.LeaveRequest.AsNoTracking().SingleAsync());
            (leave.EmployeeId, leave.FromDate, leave.ToDate, leave.LeaveTypeCode).Should().Be((w.E1, from, to, "ANNUAL"));
            leave.Reason.Should().Contain("[Agent HR]");
            leave.RequestStatus.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task Shift_swap_proposal_creates_swap_request_after_approval()
        {
            using var f = await NewAsync();
            var w = await SeedStaffAsync(f);
            await ProposeThenApproveAsync(f, "HR", "propose_shift_swap", new
            {
                ma_nv = "NV10-1", ngay = Today.AddDays(1).ToString("yyyy-MM-dd"), ma_nv_nhan = "NV10-2", ngay_doi = Today.AddDays(2).ToString("yyyy-MM-dd"), ly_do = "Bận việc nhà",
            }, async () => (await f.QueryAsync(db => db.ShiftSwapRequest.CountAsync())).Should().Be(0));

            var swap = await f.QueryAsync(db => db.ShiftSwapRequest.AsNoTracking().SingleAsync());
            (swap.RequesterEmployeeId, swap.TargetEmployeeId, swap.CurrentShiftAssignmentId, swap.ProposedShiftAssignmentId).Should().Be((w.E1, w.E2, w.A1, (Guid?)w.A2));
            swap.RequestStatus.Should().Be(ShiftSwapStatus.AwaitingConsent, "đi tiếp luồng người nhận đồng ý như web");
        }

        [Fact]
        public async Task Attendance_fix_proposal_creates_supplement_check_out_event_after_approval()
        {
            using var f = await NewAsync();
            Guid emp = Guid.NewGuid(), shift = Guid.NewGuid(), asg = Guid.NewGuid();
            DateTime y = Today.AddDays(-1);
            await f.SeedAsync(db =>
            {
                db.Employee.Add(new Employee { Id = emp, Code = "NV10-3", EmployeeCode = "NV10-3", FullName = "NV A10 3", Status = "Active" });
                db.Shift.Add(new Shift { Id = shift, Code = "C-A10", Name = "Chiều A10", StartTime = new TimeSpan(13, 0, 0), EndTime = new TimeSpan(21, 0, 0), Status = "Active" });
                db.ShiftAssignment.Add(new ShiftAssignment { Id = asg, EmployeeId = emp, ShiftId = shift, WorkDate = y, PlannedStart = y.AddHours(13), PlannedEnd = y.AddHours(21), AssignmentStatus = "CheckedIn" });
                db.AttendanceEvent.Add(new AttendanceEvent { Id = Guid.NewGuid(), EmployeeId = emp, ShiftAssignmentId = asg, EventType = "CHECK_IN", EventAt = y.AddHours(12.9), IsValid = true, SourceChannel = "GPS", DistanceMeters = 10 });
                return Task.CompletedTask;
            });
            await ProposeThenApproveAsync(f, "HR", "propose_attendance_fix", new { ma_nv = "NV10-3", ngay = y.ToString("yyyy-MM-dd"), loai = "RA", gio = "21:05", ma_ly_do = "FORGOT", ly_do = "Quên chấm ra" },
                async () => (await f.QueryAsync(db => db.AttendanceEvent.CountAsync(e => e.ShiftAssignmentId == asg && e.EventType == "CHECK_OUT"))).Should().Be(0));

            var ev = await f.QueryAsync(db => db.AttendanceEvent.AsNoTracking().SingleAsync(e => e.ShiftAssignmentId == asg && e.EventType == "CHECK_OUT"));
            (ev.EmployeeId, ev.EventAt).Should().Be((emp, y.AddHours(21).AddMinutes(5)));
            (await f.QueryAsync(db => db.ShiftAssignment.AsNoTracking().Where(a => a.Id == asg).Select(a => a.AssignmentStatus).SingleAsync())).Should().Be(ShiftAssignmentStatus.Completed);
        }
    }
}
