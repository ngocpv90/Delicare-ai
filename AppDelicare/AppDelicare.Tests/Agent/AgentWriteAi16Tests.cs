using System.Text.Json;
using System.Text.Json.Nodes;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
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
    /// AI-16 (D-133 đợt 3): 5 lệnh ghi 2 pha của PLAN / PKG / ACC. Mỗi lệnh: đề xuất → bảng nghiệp vụ KHÔNG đổi; duyệt (admin) → đúng như thao tác web (cùng method Service).
    /// Kèm công cụ đọc mới (hop_khay, lo_ve_sinh, chung_tu_ke_toan, doi_chieu_amis, gia_von_lai_gop) trả 200; ACC không có lệnh nào ngoài ghi thất thoát (D-23: không thay MISA).
    /// </summary>
    public class AgentWriteAi16Tests
    {
        private static async Task<IResult<bool>> DecideAsync(InMemoryApiFactory f, AgentCallResult r)
        {
            using var scope = f.Services.CreateScope();
            using var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForUser(InMemoryApiFactory.UserId));
            return await scope.ServiceProvider.GetRequiredService<IApprovalRequestService>().Decide(new ApprovalDecideRequest
            {
                Id = Guid.Parse(((JsonNode)r.Data!)["approvalRequestId"]!.GetValue<string>()), Approve = true, Note = "OK",
            });
        }

        private static async Task ApproveAsync(InMemoryApiFactory f, AgentCallResult r)
        {
            r.Status.Should().Be(200, r.Message);
            var ok = await DecideAsync(f, r);
            ok.ResponseCode.Should().Be(200, ok.ResponseMessage);
        }

        [Fact]
        public async Task Plan_recalculates_day_plan_and_changes_published_menu_only_after_approval()
        {
            using var f = await AgentToolsAi3Tests.FactoryAsync();
            DateTime day = new(2034, 6, 7); // thứ Tư; tuần bắt đầu 05/06/2034
            f.Clock.FixedNow = day.AddDays(-5).AddHours(9);
            f.CreateAuthorizedClient().Dispose();
            Guid shift = Guid.NewGuid(), cat = Guid.NewGuid(), dOld = Guid.NewGuid(), dNew = Guid.NewGuid(), menu = Guid.NewGuid(), customer = Guid.NewGuid(), order = Guid.NewGuid();
            Guid kg = Guid.NewGuid(), ing = Guid.NewGuid(), wh = Guid.NewGuid(), lot = Guid.NewGuid();
            await f.SeedAsync(async db =>
            {
                db.Shift.Add(new Shift { Id = shift, Code = "T16", Name = "Trưa 16", StartTime = new TimeSpan(10, 30, 0), Status = "Active" });
                db.DishCategory.Add(new DishCategory { Id = cat, Code = "MC16", Name = "Món chính 16" });
                db.UnitMeasure.Add(new UnitMeasure { Id = kg, Code = "KG16", Name = "kg" });
                db.Ingredient.Add(new Ingredient { Id = ing, Code = "NL16", Name = "NL 16", BaseUnitId = kg });
                foreach (var (id, code, name) in new[] { (dOld, "MON16A", "Gà kho 16"), (dNew, "MON16B", "Cá kho 16") })
                {
                    db.Dish.Add(new Dish { Id = id, Code = code, Name = name, DishCategoryId = cat, StandardCost = 12000, Status = "Active" });
                    db.Recipe.Add(new Recipe { Id = Guid.NewGuid(), DishId = id, VersionNo = 1, Status = "Locked", IsActive = true, StandardOutputQty = 100, YieldQuantity = 1, EffectiveFrom = day.AddDays(-30),
                        LockedAt = day.AddDays(-30), SnapshotJson = JsonSerializer.Serialize(new[] { new { IngredientId = ing, Quantity = 10m, UnitId = kg, SequenceNo = 1 } }) });
                }
                db.Warehouse.Add(new Warehouse { Id = wh, Code = "W16", Name = "Kho 16", Status = "Active" });
                db.MaterialLot.Add(new MaterialLot { Id = lot, IngredientId = ing, LotNumber = "L16", LotNo = "L16", ExpiryDate = day.AddDays(30), ReceivedDate = day.AddDays(-2), QcStatus = "DAT", QualityStatus = "DAT", Status = "Available" });
                await new InventoryLedgerService(db).PostAsync(new InventoryPostRequest { TransactionType = "RECEIPT", TransactionNumber = "NK16", WarehouseId = wh, IngredientId = ing, MaterialLotId = lot, UnitId = kg, QuantityIn = 30 });
                db.Menu.Add(new Model.Entity.Menu { Id = menu, Code = "TD16", Name = "TĐ 16", VersionNo = 1, Status = "Published", MenuType = "COMMON", EffectiveFrom = day.AddDays(-2), EffectiveTo = day.AddDays(4) });
                db.MenuLine.Add(new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = dOld, MealType = "TRUA", DayOfWeek = 3, SequenceNo = 1, SortOrder = 0, PortionQty = 1 });
                db.Customer.Add(new Customer { Id = customer, Code = "K16", Name = "Khách 16", DisplayName = "Khách 16", CIF = "K16", CustomerType = "FACTORY", Status = "Active" });
                var ct = new Contract { Id = Guid.NewGuid(), CustomerId = customer, ContractNumber = "HD-16", Status = "Active", EffectiveFrom = new DateTime(2034, 1, 1) };
                db.Contract.Add(ct);
                db.ContractMeal.Add(new ContractMeal { Id = Guid.NewGuid(), ContractId = ct.Id, MealType = "TRUA", MealName = "Trưa", UnitPrice = 40000 });
                db.Order.Add(new Order { Id = order, OrderNumber = "DH-16", CustomerId = customer, OrderDate = day.AddDays(-3), MealDate = day, DeliveryDate = day, Status = "Locked", TotalQuantity = 100 });
                db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = order, MenuId = menu, MealType = "TRUA", MenuOptionNo = 1, ShiftId = shift, Quantity = 100, LineAmount = 0 });
            });

            // Đọc: kế hoạch / thực đơn / giá vốn – PLAN có công cụ.
            (await AgentToolsAi3Tests.Call(f, "PLAN", "ke_hoach_ngay", new { tu_ngay = "2034-06-07", den_ngay = "2034-06-07" })).Status.Should().Be(200);
            (await AgentToolsAi3Tests.Call(f, "PLAN", "thuc_don_tuan", new { tuan = "2034-06-07" })).Status.Should().Be(200);

            // Đổi món: ô không có món cũ → 400; mã lý do sai → 400; đúng → chưa có yêu cầu đổi món tới khi duyệt.
            (await AgentToolsAi3Tests.Call(f, "PLAN", "propose_menu_change", new { ngay = "2034-06-08", buoi = "TRUA", mon_cu = "MON16A", mon_moi = "MON16B", ma_ly_do = "SHORTAGE", ly_do = "x" }))
                .Message.Should().Contain("không có món MON16A");
            (await AgentToolsAi3Tests.Call(f, "PLAN", "propose_menu_change", new { ngay = "2034-06-07", buoi = "TRUA", mon_cu = "MON16A", mon_moi = "MON16B", ma_ly_do = "KHAC", ly_do = "x" })).Status.Should().Be(400);
            var change = await AgentToolsAi3Tests.Call(f, "PLAN", "propose_menu_change", new { ngay = "2034-06-07", buoi = "TRUA", mon_cu = "MON16A", mon_moi = "MON16B", ma_ly_do = "SHORTAGE", ly_do = "Hết gà, NCC báo giao bù thứ Năm" });
            change.Status.Should().Be(200, change.Message);
            ((JsonNode)change.Data!)["summary"]!.GetValue<string>().Should().Contain("100 suất").And.Contain("tự duyệt");
            (await f.QueryAsync(db => db.MenuChangeRequest.CountAsync(r => r.MenuId == menu))).Should().Be(0, "đề xuất chưa ghi");
            await ApproveAsync(f, change);
            var req = await f.QueryAsync(db => db.MenuChangeRequest.AsNoTracking().SingleAsync(r => r.MenuId == menu));
            (req.OriginalDishId, req.RequestedDishId, req.Quantity).Should().Be((dOld, (Guid?)dNew, (decimal?)100m));

            // Kế hoạch: ngày không có đơn chốt → 400; ngày có → chưa có kế hoạch tới khi duyệt; duyệt = Tính lại trên web.
            (await AgentToolsAi3Tests.Call(f, "PLAN", "propose_production_plan", new { ngay = "2034-06-09", ly_do = "x" })).Message.Should().Contain("chưa có đơn đã chốt");
            var plan = await AgentToolsAi3Tests.Call(f, "PLAN", "propose_production_plan", new { ngay = "2034-06-07", ly_do = "Đơn DH-16 đã chốt 100 suất" });
            plan.Status.Should().Be(200, plan.Message);
            (await f.QueryAsync(db => db.ProductionPlan.CountAsync(p => p.ProductionDate == day && !p.IsDeleted))).Should().Be(0, "đề xuất chưa ghi");
            await ApproveAsync(f, plan);
            (await f.QueryAsync(db => db.ProductionPlan.CountAsync(p => p.ProductionDate == day && !p.IsDeleted))).Should().Be(1);
        }

        [Fact]
        public async Task Pkg_and_acc_record_return_cleaning_step_and_loss_only_after_approval()
        {
            using var f = await AgentToolsAi3Tests.FactoryAsync();
            DateTime day = new(2026, 12, 21);
            f.Clock.FixedNow = day.AddHours(10);
            Guid customer = Guid.NewGuid(), location = Guid.NewGuid(), order = Guid.NewGuid(), route = Guid.NewGuid(), dlv = Guid.NewGuid(), stop = Guid.NewGuid(), packing = Guid.NewGuid(), emp = Guid.NewGuid();
            const string Number = "PH-261221-916", Type = "KHAY-T16";
            await f.SeedAsync(db =>
            {
                db.Customer.Add(new Customer { Id = customer, Code = "KP16", Name = "Khách P16", CIF = "KP16", CustomerType = "FACTORY", DisplayName = "Khách P16", Status = "Active", ExternalData = "{\"containerLossMaxPct\": 2}" });
                db.CustomerDeliveryLocation.Add(new CustomerDeliveryLocation { Id = location, CustomerId = customer, Code = "DP16", Name = "Căng tin P16", DeliveryWindowFrom = new TimeSpan(11, 0, 0) });
                db.Order.Add(new Order { Id = order, OrderNumber = "PH-OP16", OrderNo = "PH-OP16", CustomerId = customer, DeliveryLocationId = location, OrderDate = day.AddDays(-2), MealDate = day, DeliveryDate = day, Status = "InProduction", TotalQuantity = 60, TotalAmount = 1_800_000 });
                db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = order, MealType = "TRUA", Quantity = 60, UnitPrice = 30_000, LineAmount = 1_800_000 });
                db.PackingOrder.Add(new PackingOrder { Id = packing, ProductionBatchId = Guid.NewGuid(), OrderId = order, PlannedQty = 60, Status = "Packed", PackingOrderNumber = "PH-PP16", PackingDate = day });
                db.PackingResult.Add(new PackingResult { Id = Guid.NewGuid(), PackingOrderId = packing, PlannedQty = 60, PackedQuantity = 60, AcceptedQty = 60, PackagingType = Type });
                db.DeliveryRoute.Add(new DeliveryRoute { Id = route, RouteNo = "CX-261221-XP16-01", Code = "CXP16", DeliveryDate = day, Status = "InTransit", ActualStart = day.AddHours(9) });
                db.DeliveryOrder.Add(new DeliveryOrder { Id = dlv, OrderId = order, CustomerId = customer, CustomerDeliveryLocationId = location, DeliveryLocationId = location, DeliveryRouteId = route, DeliveryNumber = Number, DeliveryNo = Number,
                    DeliveryDate = day, PlannedDeliveryTime = day.AddHours(11), Status = "InTransit" });
                db.DeliveryStop.Add(new DeliveryStop { Id = stop, DeliveryRouteId = route, DeliveryOrderId = dlv, CustomerId = customer, DeliveryLocationId = location, SequenceNo = 1, PlannedArrival = day.AddHours(11), PlannedMealQty = 60, StopStatus = "Planned" });
                db.Employee.Add(new Employee { Id = emp, Code = "NVP16", FullName = "Nhân viên rửa P16", Status = "Active" });
                Guid cl = Guid.NewGuid();
                db.FoodSafetyChecklist.Add(new FoodSafetyChecklist { Id = cl, Code = "VS-KHAY", Name = "Vệ sinh khay", VersionNo = 1, Status = "Active", EffectiveFrom = day.AddDays(-30) });
                int i = 0;
                foreach (var (code, name, crit) in new[] { ("NHAN_QR", "1. Nhận / quét QR", false), ("PHAN_LOAI", "2. Phân loại", false), ("RUA_SO", "3. Rửa sơ", false), ("RUA_MAY", "4. Rửa máy", false), ("KHU_KHUAN", "5. Khử khuẩn", true), ("KIEM_TRA_NHAP_KHO", "6. Kiểm tra và nhập kho sạch", true) })
                {
                    db.FoodSafetyChecklistItem.Add(new FoodSafetyChecklistItem { Id = Guid.NewGuid(), ChecklistId = cl, ItemCode = code, ItemName = name, SortOrder = ++i, IsCritical = crit, IsRequired = true });
                }
                return Task.CompletedTask;
            });
            using var c = f.CreateAuthorizedClient();
            (await c.PostFormAsync("/api/DeliveryStop/Deliver", ("Id", stop), ("TemperatureC", 63m), ("DeliveredQuantity", 60m), ("AcceptedQuantity", 58m), ("ReceiverName", "Chị Hoa"))).Ok();

            var bal = await AgentToolsAi3Tests.Call(f, "PKG", "hop_khay", new { ma_khach = "KP16" });
            bal.Status.Should().Be(200, bal.Message);
            JsonSerializer.Serialize(bal.Data).Should().Contain("\"outstanding\":58");

            // Trả hộp: vượt số khách giữ → cảnh báo trong xem trước; đúng → chưa ghi tới khi duyệt.
            var over = await AgentToolsAi3Tests.Call(f, "PKG", "propose_container_return", new { so_phieu_giao = Number, so_tra = 70, ly_do = "thử" });
            ((JsonNode)over.Data!)["warnings"]!.AsArray().Select(w => w!.GetValue<string>()).Should().Contain("Số trả 70 > số khách đang giữ 58 – xác nhận sẽ bị từ chối");
            var ret = await AgentToolsAi3Tests.Call(f, "PKG", "propose_container_return", new { so_phieu_giao = Number, so_tra = 55, so_hong = 3, ly_do = "Tài xế báo thu 55 khay, 3 móp" });
            ret.Status.Should().Be(200, ret.Message);
            (await f.QueryAsync(db => db.PackagingLedger.CountAsync(l => l.CustomerId == customer && l.TransactionType == "IN"))).Should().Be(0, "đề xuất chưa ghi");
            await ApproveAsync(f, ret);
            var ins = await f.QueryAsync(db => db.PackagingLedger.AsNoTracking().Where(l => l.CustomerId == customer && l.TransactionType == "IN").OrderBy(l => l.Quantity).Select(l => l.Quantity).ToListAsync());
            ins.Should().Equal(3m, 52m);

            // Kiểm hộp trả (web) → lô vệ sinh; bước 3 trước bước 1 → 400; bước 1 PASS → chưa ghi tới khi duyệt.
            Guid returnId = await f.QueryAsync(db => db.ContainerReturn.Where(r => r.DeliveryOrderId == dlv).Select(r => r.Id).SingleAsync());
            (await c.PostFormAsync("/api/ContainerReturn/Inspect", ("Id", returnId), ("InspectionResult", "OK"), ("InspectorEmployeeId", emp), ("ResponsibleEmployeeId", emp))).Ok();
            var batch = await f.QueryAsync(db => db.CleaningBatch.AsNoTracking().SingleAsync());
            (await AgentToolsAi3Tests.Call(f, "PKG", "lo_ve_sinh", new { ngay = "2026-12-21" })).Status.Should().Be(200);
            (await AgentToolsAi3Tests.Call(f, "PKG", "propose_cleaning_step", new { so_lo_ve_sinh = batch.CleaningBatchNumber, ma_buoc = "RUA_SO", ket_qua = "PASS", ly_do = "x" })).Message.Should().Contain("Bước trước chưa PASS");
            (await AgentToolsAi3Tests.Call(f, "PKG", "propose_cleaning_step", new { so_lo_ve_sinh = batch.CleaningBatchNumber, ma_buoc = "NHAN_QR", ket_qua = "FAIL", ly_do = "x" })).Message.Should().Contain("khắc phục");
            var step = await AgentToolsAi3Tests.Call(f, "PKG", "propose_cleaning_step", new { so_lo_ve_sinh = batch.CleaningBatchNumber, ma_buoc = "NHAN_QR", ket_qua = "PASS", ly_do = "Tổ vệ sinh báo đã quét QR đủ khay" });
            step.Status.Should().Be(200, step.Message);
            (await f.QueryAsync(db => db.CleaningChecklistResult.CountAsync(r => r.CleaningBatchId == batch.Id))).Should().Be(0, "đề xuất chưa ghi");
            await ApproveAsync(f, step);
            (await f.QueryAsync(db => db.CleaningChecklistResult.Where(r => r.CleaningBatchId == batch.Id).Select(r => r.ResultCode).ToListAsync())).Should().Equal("PASS");

            // ACC: đọc chứng từ / AMIS / lãi gộp; ghi thất thoát vượt số giữ → 400; 2 khay → chưa ghi tới khi duyệt.
            foreach (string tool in new[] { "chung_tu_ke_toan", "doi_chieu_amis", "gia_von_lai_gop" })
            {
                var r = await AgentToolsAi3Tests.Call(f, "ACC", tool, new { thang = "2026-12" });
                r.Status.Should().Be(200, $"{tool}: {r.Message}");
            }
            (await AgentToolsAi3Tests.Call(f, "ACC", "propose_container_loss_charge", new { ma_khach = "KP16", loai_hop = Type, so_luong = 10, ly_do = "x" })).Message.Should().Contain("đang giữ 3");
            var loss = await AgentToolsAi3Tests.Call(f, "ACC", "propose_container_loss_charge", new { ma_khach = "KP16", loai_hop = Type, so_luong = 2, so_phieu_giao = Number, ly_do = "Khách xác nhận mất 2 khay" });
            loss.Status.Should().Be(200, loss.Message);
            (await f.QueryAsync(db => db.PackagingLedger.CountAsync(l => l.CustomerId == customer && l.TransactionType == "LOSS"))).Should().Be(0, "đề xuất chưa ghi");
            await ApproveAsync(f, loss);
            (await f.QueryAsync(db => db.PackagingLedger.Where(l => l.CustomerId == customer && l.TransactionType == "LOSS").Select(l => l.Quantity).SingleAsync())).Should().Be(2m);
        }

        [Fact]
        public void Acc_has_no_write_except_loss_and_new_agents_reach_every_wave3_tool()
        {
            AgentCommandRegistry.All.Where(c => c.Agents.Contains("ACC")).Select(c => c.Code).Should().BeEquivalentTo(["propose_container_loss_charge", "propose_note_update"], "ACC không thay MISA (D-23)");
            AgentToolRegistry.All.Where(t => t.AllowedFor("PKG")).Select(t => t.Name).Should().Contain(["hop_khay", "lo_ve_sinh", "propose_container_return", "propose_cleaning_step", "propose_container_loss_charge"]);
            AgentToolRegistry.All.Where(t => t.AllowedFor("PLAN")).Select(t => t.Name).Should().Contain(["ke_hoach_ngay", "thuc_don_tuan", "du_bao_thieu_hut", "propose_production_plan", "propose_menu_change"]);
        }
    }
}
