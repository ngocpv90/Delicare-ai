using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Purchasing
{
    /// <summary>
    /// T15 (Pilot P6) – đề nghị mua → PO (duyệt 1 cấp qua engine T12) → phiếu nhập (PH-) → QC đầu vào DAT / DAT_MOT_PHAN / KHONG_DAT → lô NL- → nhập kho.
    /// Hôm nay (đồng hồ test) = 20/12/2026 10:00.
    /// <para>Bộ dữ liệu: NCC A (mã "SA-&lt;code&gt;") bán Thịt (mát ≤ 5 °C, 100 000/kg), Rau (mát, 20 000/kg), Cá (đông lạnh, 150 000/kg); NCC B bán Gạo
    /// (15 000/kg). Đề nghị mua Nháp: Thịt 10, Rau 20, Cá 5, Gạo 30 → PO A = 10 × 100 000 + 20 × 20 000 + 5 × 150 000 = 2 150 000; PO B = 450 000.</para>
    /// </summary>
    public class PurchasingTests : IClassFixture<InMemoryApiFactory>
    {
        private static readonly DateTime Today = new(2026, 12, 20);
        private readonly InMemoryApiFactory _factory;

        public PurchasingTests(InMemoryApiFactory factory)
        {
            _factory = factory;
            _factory.Clock.FixedNow = Today.AddHours(10);
        }

        private sealed record Scenario(string Code, Guid Warehouse, Guid Kg, Guid Meat, Guid Veg, Guid Fish, Guid Rice, Guid SupplierA, Guid SupplierB, Guid Request);

        private async Task<Scenario> SeedAsync(string code)
        {
            Guid warehouse = Guid.NewGuid(), kg = Guid.NewGuid(), meat = Guid.NewGuid(), veg = Guid.NewGuid(), fish = Guid.NewGuid(), rice = Guid.NewGuid();
            Guid a = Guid.NewGuid(), b = Guid.NewGuid(), pr = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Warehouse.Add(new Warehouse { Id = warehouse, Code = code, Name = "Kho " + code, Status = "Active" });
                db.UnitMeasure.Add(new UnitMeasure { Id = kg, Code = "KG-" + code, Name = "kg" });
                db.Ingredient.AddRange(
                    new Ingredient { Id = meat, Code = "THIT-" + code, Name = "Thịt " + code, BaseUnitId = kg, StorageType = "CHILLED" },
                    new Ingredient { Id = veg, Code = "RAU-" + code, Name = "Rau " + code, BaseUnitId = kg, StorageType = "CHILLED" },
                    new Ingredient { Id = fish, Code = "CA-" + code, Name = "Cá " + code, BaseUnitId = kg, StorageType = "FROZEN", ShelfLifeDays = 90 },
                    new Ingredient { Id = rice, Code = "GAO-" + code, Name = "Gạo " + code, BaseUnitId = kg, StorageType = "DRY" });
                db.Supplier.AddRange(
                    new Supplier { Id = a, Code = "SA-" + code, Name = "NCC A " + code, Address = "[DEMO] Hà Nội", Status = "Active" },
                    new Supplier { Id = b, Code = "SB-" + code, Name = "NCC B " + code, Status = "Active" });
                db.SupplierMaterial.AddRange(
                    new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = a, IngredientId = meat, IsPreferred = true, UnitPrice = 100000, PurchaseUomId = kg, MinimumOrderQuantity = 20, Status = "Active" },
                    new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = b, IngredientId = meat, IsPreferred = false, UnitPrice = 90000, PurchaseUomId = kg, Status = "Active" },
                    new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = a, IngredientId = veg, IsPreferred = true, UnitPrice = 20000, PurchaseUomId = kg, Status = "Active" },
                    new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = a, IngredientId = fish, IsPreferred = true, UnitPrice = 150000, PurchaseUomId = kg, Status = "Active" },
                    new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = b, IngredientId = rice, IsPreferred = true, UnitPrice = 15000, PurchaseUomId = kg, Status = "Active" });
                db.PurchaseRequest.Add(new PurchaseRequest { Id = pr, RequestNumber = "PH-" + code, RequestNo = "PH-" + code, RequestDate = Today, RequiredDate = Today.AddDays(2), Status = "Draft" });
                foreach (var (ingredient, quantity) in new[] { (meat, 10m), (veg, 20m), (fish, 5m), (rice, 30m) })
                {
                    db.PurchaseRequestLine.Add(new PurchaseRequestLine { Id = Guid.NewGuid(), PurchaseRequestId = pr, IngredientId = ingredient, Quantity = quantity, UnitId = kg, UomId = kg });
                }
                return Task.CompletedTask;
            });
            return new Scenario(code, warehouse, kg, meat, veg, fish, rice, a, b, pr);
        }

        /// <summary>User thường; withApprove = có quyền trên mục "Duyệt – Đơn mua hàng" (#/wf-inbox?type=scm-po).</summary>
        private async Task<Guid> UserAsync(bool withApprove)
        {
            Guid userId = Guid.NewGuid(), group = Guid.NewGuid();
            await _factory.SeedAsync(async db =>
            {
                db.User.Add(new User { Id = userId, UserName = "po-" + userId.ToString("N")[..6], IsActive = true });
                db.UserGroup.Add(new UserGroup { Id = group, Name = "PO-" + group.ToString("N")[..6] });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = userId, UserGroupId = group });
                if (withApprove)
                {
                    var menu = await db.ObjectMenu.FirstOrDefaultAsync(m => m.MetaLink == "#/wf-inbox?type=scm-po");
                    if (menu == null)
                    {
                        menu = new ObjectMenu { Id = Guid.NewGuid(), Name = "Duyệt – Đơn mua hàng", MetaLink = "#/wf-inbox?type=scm-po", IsActive = true, LevelShow = 2 };
                        db.ObjectMenu.Add(menu);
                    }
                    db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = group, ObjectMenuId = menu.Id, AllowAccess = true, AllowEdit = true });
                }
            });
            return userId;
        }

        private static async Task<LegacyResult> PostJson(HttpClient c, string url, object body) => await LegacyResult.ReadAsync(await c.PostAsJsonAsync(url, body));
        private static async Task<LegacyResult> PutJson(HttpClient c, string url, object body) => await LegacyResult.ReadAsync(await c.PutAsJsonAsync(url, body));

        private Task<PurchaseOrder> PoOfAsync(Guid supplier, Guid request)
            => _factory.QueryAsync(db => db.PurchaseOrder.AsNoTracking().SingleAsync(p => p.SupplierId == supplier && p.PurchaseRequestId == request));

        private static JsonElement Line(JsonElement detail, Guid ingredient)
            => detail.GetProperty("listLine").EnumerateArray().Single(l => l.GetProperty("ingredientId").GetGuid() == ingredient);

        /// <summary>PR → PO (2 NCC) → gửi duyệt → duyệt qua engine → trả về Scenario + PO của NCC A đã duyệt.</summary>
        private async Task<(Scenario S, PurchaseOrder PoA, PurchaseOrder PoB)> ApprovedPoAsync(HttpClient admin, string code)
        {
            var s = await SeedAsync(code);
            await admin.ApprovePurchaseRequestAsync(s.Request); // BE2.3 (D-46): chỉ đề nghị Đã duyệt 2 cấp mới lập PO.
            var convert = (await admin.PostFormAsync("/api/PurchaseRequest/ConvertToPurchaseOrder", ("Id", s.Request))).Ok();
            convert.GetProperty("listPurchaseOrder").GetArrayLength().Should().Be(2);
            var poA = await PoOfAsync(s.SupplierA, s.Request);
            var poB = await PoOfAsync(s.SupplierB, s.Request);
            (await admin.PostFormAsync("/api/PurchaseOrder/Submit", ("Id", poA.Id))).Ok();
            using var approver = _factory.CreateAuthorizedClient(await UserAsync(withApprove: true));
            (await approver.PostFormAsync("/api/PurchaseOrder/Approve", ("Id", poA.Id), ("Note", "ok"))).Ok();
            return (s, await PoOfAsync(s.SupplierA, s.Request), poB);
        }

        [Fact]
        public async Task Convert_request_groups_lines_by_preferred_supplier_with_po_numbers_and_prices()
        {
            var s = await SeedAsync("PUR1");
            using var c = _factory.CreateAuthorizedClient();
            var detail = (await c.GetResultAsync($"/api/PurchaseRequest/GetDetail?id={s.Request}")).Ok();
            detail.GetProperty("estimatedAmount").GetDecimal().Should().Be(2_600_000m);
            detail.GetProperty("allowedActions").EnumerateArray().Select(x => x.GetString()).Should().Contain("submit").And.NotContain("convertToPurchaseOrder");
            // BE2.3 (D-46): Nháp không lập PO được – phải duyệt 2 cấp trước.
            (await c.PostFormAsync("/api/PurchaseRequest/ConvertToPurchaseOrder", ("Id", s.Request))).Fail("Đã duyệt");
            await c.ApprovePurchaseRequestAsync(s.Request);
            (await c.GetResultAsync($"/api/PurchaseRequest/GetDetail?id={s.Request}")).Ok()
                .GetProperty("allowedActions").EnumerateArray().Select(x => x.GetString()).Should().Contain("convertToPurchaseOrder");

            var result = await c.PostFormAsync("/api/PurchaseRequest/ConvertToPurchaseOrder", ("Id", s.Request), ("Note", "[DEMO] lập PO"));
            var data = result.Ok();
            data.GetProperty("listWarning").EnumerateArray().Select(x => x.GetString()).Should().Contain(w => w!.Contains("MOQ"));
            var poA = await PoOfAsync(s.SupplierA, s.Request);
            var poB = await PoOfAsync(s.SupplierB, s.Request);
            poA.PurchaseOrderNumber.Should().MatchRegex(@"^PO-261220-\d{4}$").And.Be(poA.PONo);
            poB.PurchaseOrderNumber.Should().MatchRegex(@"^PO-261220-\d{4}$").And.NotBe(poA.PurchaseOrderNumber);
            poA.Status.Should().Be("Draft");
            poA.TotalAmount.Should().Be(2_150_000m);
            poB.TotalAmount.Should().Be(450_000m);
            poA.ExpectedDeliveryDate.Should().Be(Today.AddDays(2));
            var lines = await _factory.QueryAsync(db => db.PurchaseOrderLine.AsNoTracking().Where(l => l.PurchaseOrderId == poA.Id).ToListAsync());
            lines.Should().HaveCount(3);
            lines.Single(l => l.IngredientId == s.Meat).UnitPrice.Should().Be(100000, "NCC ưu tiên (A) thắng NCC rẻ hơn (B)");
            lines.Should().OnlyContain(l => l.UomId == l.UnitId && l.Amount == l.LineAmount);
            (await _factory.QueryAsync(db => db.PurchaseRequest.AsNoTracking().SingleAsync(p => p.Id == s.Request))).Status.Should().Be("Converted");

            // Đã lập PO → không lập lại, không sửa dòng đề nghị.
            (await c.PostFormAsync("/api/PurchaseRequest/ConvertToPurchaseOrder", ("Id", s.Request))).Fail("Đã duyệt");
            (await c.PostFormAsync("/api/PurchaseRequestLine/Insert", ("PurchaseRequestId", s.Request), ("IngredientId", s.Meat), ("Quantity", 1m), ("UnitId", s.Kg))).Fail("Nháp");

            // PO lập từ đề nghị: không xóa (Cancel), sửa dòng → tổng tính lại.
            (await c.DeleteResultAsync($"/api/PurchaseOrder/Delete?id={poB.Id}")).Fail("Cancel");
            var updated = (await PutJson(c, "/api/PurchaseOrder/UpdateLines", new { id = poB.Id, listLine = new[] { new { ingredientId = s.Rice, quantity = 40m } } })).Ok();
            updated.GetProperty("purchaseOrder").GetProperty("totalAmount").GetDecimal().Should().Be(600_000m);
            (await PutJson(c, "/api/PurchaseOrder/UpdateLines", new { id = poB.Id, listLine = new[] { new { ingredientId = s.Rice, quantity = 1m }, new { ingredientId = s.Rice, quantity = 2m } } })).Fail("trùng");

            // Nguyên liệu chưa có NCC → lỗi kèm tên.
            Guid salt = Guid.NewGuid(), pr2 = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Ingredient.Add(new Ingredient { Id = salt, Code = "MUOI-PUR1", Name = "Muối PUR1", BaseUnitId = s.Kg });
                db.PurchaseRequest.Add(new PurchaseRequest { Id = pr2, RequestNumber = "PH-PUR1-2", RequestDate = Today, Status = "Draft" });
                db.PurchaseRequestLine.Add(new PurchaseRequestLine { Id = Guid.NewGuid(), PurchaseRequestId = pr2, IngredientId = salt, Quantity = 1, UnitId = s.Kg, UomId = s.Kg });
                return Task.CompletedTask;
            });
            await c.ApprovePurchaseRequestAsync(pr2);
            (await c.PostFormAsync("/api/PurchaseRequest/ConvertToPurchaseOrder", ("Id", pr2))).Fail("Muối PUR1");
        }

        [Fact]
        public async Task Po_approval_goes_through_engine_with_menu_right_and_payload_hash()
        {
            var s = await SeedAsync("PUR2");
            using var admin = _factory.CreateAuthorizedClient();
            await admin.ApprovePurchaseRequestAsync(s.Request);
            (await admin.PostFormAsync("/api/PurchaseRequest/ConvertToPurchaseOrder", ("Id", s.Request))).Ok();
            var poA = await PoOfAsync(s.SupplierA, s.Request);
            var poB = await PoOfAsync(s.SupplierB, s.Request);

            // Phiếu nhập trên PO chưa duyệt → chặn.
            var poLine = await _factory.QueryAsync(db => db.PurchaseOrderLine.AsNoTracking().FirstAsync(l => l.PurchaseOrderId == poA.Id));
            (await PostJson(admin, "/api/GoodsReceipt/Create", new { purchaseOrderId = poA.Id, warehouseId = s.Warehouse, listLine = new[] { new { purchaseOrderLineId = poLine.Id, receivedQuantity = 1m } } }))
                .Fail("Chỉ nhận hàng theo PO");

            var submitted = (await admin.PostFormAsync("/api/PurchaseOrder/Submit", ("Id", poA.Id))).Ok();
            submitted.GetProperty("purchaseOrder").GetProperty("status").GetString().Should().Be("PendingApproval");
            var request = await _factory.QueryAsync(db => db.ApprovalRequest.AsNoTracking().SingleAsync(a => a.BusinessId == poA.Id));
            request.BusinessType.Should().Be("PURCHASE_ORDER");
            request.RequestNo.Should().MatchRegex(@"^XN-\d{6}-\d{3}$");
            (await PostJson(admin, "/api/GoodsReceipt/Create", new { purchaseOrderId = poA.Id, warehouseId = s.Warehouse, listLine = new[] { new { purchaseOrderLineId = poLine.Id, receivedQuantity = 1m } } }))
                .Fail("Chờ duyệt");
            (await PutJson(admin, "/api/PurchaseOrder/UpdateLines", new { id = poA.Id, listLine = new[] { new { ingredientId = s.Meat, quantity = 1m } } })).Fail("Nháp");

            // Người không có quyền: không thấy trong hộp duyệt, không duyệt được.
            using var other = _factory.CreateAuthorizedClient(await UserAsync(withApprove: false));
            (await other.GetResultAsync("/api/ApprovalRequest/GetMyPending?type=scm-po")).ListData.GetArrayLength().Should().Be(0);
            (await other.PostFormAsync("/api/ApprovalRequest/Decide", ("Id", request.Id), ("Approve", true))).Fail("không có quyền");
            // T15.1: quyền duyệt PO kiểm trong engine theo tổng tiền (scm-po / scm-po-director) → lỗi nghiệp vụ thay vì 403 ở filter.
            (await other.PostFormAsync("/api/PurchaseOrder/Approve", ("Id", poA.Id))).Fail("không có quyền");

            // Người có quyền: thấy (tiêu đề có số PO + NCC), duyệt → Approved + ApprovalDecision.
            using var approver = _factory.CreateAuthorizedClient(await UserAsync(withApprove: true));
            var pending = (await approver.GetResultAsync("/api/ApprovalRequest/GetMyPending?type=scm-po")).ListData.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == request.Id);
            pending.GetProperty("title").GetString().Should().Contain(poA.PurchaseOrderNumber!).And.Contain("NCC A PUR2").And.Contain("2,150,000");
            (await approver.PostFormAsync("/api/PurchaseOrder/Approve", ("Id", poA.Id), ("Note", "Đồng ý"))).Ok();
            (await PoOfAsync(s.SupplierA, s.Request)).Status.Should().Be("Approved");
            var decision = await _factory.QueryAsync(db => db.ApprovalDecision.AsNoTracking().SingleAsync(d => d.ApprovalRequestId == request.Id));
            decision.DecisionCode.Should().Be("APPROVE");
            decision.PayloadHash.Should().HaveLength(64);
            (await approver.PostFormAsync("/api/PurchaseOrder/Approve", ("Id", poA.Id))).Fail("không có yêu cầu duyệt");

            // PO B: gửi duyệt rồi bị sửa thẳng trong DB → hash khác → không duyệt được, không ghi gì; từ chối (lý do) → về Nháp.
            (await admin.PostFormAsync("/api/PurchaseOrder/Submit", ("Id", poB.Id))).Ok();
            await _factory.SeedAsync(async db =>
            {
                var line = await db.PurchaseOrderLine.SingleAsync(l => l.PurchaseOrderId == poB.Id);
                line.Quantity = 999;
            });
            (await approver.PostFormAsync("/api/PurchaseOrder/Approve", ("Id", poB.Id))).Fail("thay đổi sau khi gửi duyệt");
            (await PoOfAsync(s.SupplierB, s.Request)).Status.Should().Be("PendingApproval");
            (await approver.PostFormAsync("/api/PurchaseOrder/Reject", ("Id", poB.Id))).Fail("lý do");
            (await approver.PostFormAsync("/api/PurchaseOrder/Reject", ("Id", poB.Id), ("Note", "Sai số lượng"))).Ok();
            var rejected = await PoOfAsync(s.SupplierB, s.Request);
            rejected.Status.Should().Be("Draft");
            rejected.Note.Should().Contain("Từ chối: Sai số lượng");

            // Đã duyệt → gửi NCC → hủy được khi chưa có phiếu nhập; in Đơn đặt hàng mở được bằng ClosedXML.
            var response = await admin.GetAsync($"/api/PurchaseOrder/ExportOrder?id={poA.Id}");
            response.EnsureSuccessStatusCode();
            using (var stream = new MemoryStream(await response.Content.ReadAsByteArrayAsync()))
            using (var wb = new XLWorkbook(stream))
            {
                var ws = wb.Worksheets.First();
                ws.Cell(5, 1).GetString().Should().Be("ĐƠN ĐẶT HÀNG");
                ws.Cell(8, 1).GetString().Should().Contain("NCC A PUR2");
                ws.Cell(12, 6).GetString().Should().Contain("Đơn giá");
                var text = ws.CellsUsed().Select(x => x.GetString()).ToList();
                text.Should().Contain("Thịt PUR2").And.Contain("Tổng cộng (chưa VAT)");
            }
            (await admin.PostFormAsync("/api/PurchaseOrder/Send", ("Id", poA.Id))).Ok().GetProperty("purchaseOrder").GetProperty("status").GetString().Should().Be("Sent");
            (await admin.PostFormAsync("/api/PurchaseOrder/Cancel", ("Id", poA.Id))).Fail("lý do");
            (await admin.PostFormAsync("/api/PurchaseOrder/Cancel", ("Id", poA.Id), ("Reason", "[DEMO] NCC hết hàng"))).Ok();
            (await PoOfAsync(s.SupplierA, s.Request)).Status.Should().Be("Cancelled");
        }

        [Fact]
        public async Task Receive_qc_and_post_puts_only_accepted_quantity_in_stock_and_blocks_failed_lot()
        {
            using var admin = _factory.CreateAuthorizedClient();
            var (s, poA, _) = await ApprovedPoAsync(admin, "PUR3");
            poA.Status.Should().Be("Approved");
            var poLines = await _factory.QueryAsync(db => db.PurchaseOrderLine.AsNoTracking().Where(l => l.PurchaseOrderId == poA.Id).ToDictionaryAsync(l => l.IngredientId, l => l.Id));
            object ReceiveLine(Guid ingredient, decimal quantity, decimal temperature, int expiryDays)
                => new { purchaseOrderLineId = poLines[ingredient], receivedQuantity = quantity, supplierLotNumber = "NCC-LOT-" + quantity, manufactureDate = Today.AddDays(-1), expiryDate = Today.AddDays(expiryDays), temperatureC = temperature };

            // Nhận vượt số đặt quá 10% → chặn (Thịt đặt 10, cho tối đa 11).
            (await PostJson(admin, "/api/GoodsReceipt/Create", new { purchaseOrderId = poA.Id, warehouseId = s.Warehouse, listLine = new[] { ReceiveLine(s.Meat, 12m, 3m, 5) } }))
                .Fail("vượt số còn được nhận");

            var created = (await PostJson(admin, "/api/GoodsReceipt/Create", new
            {
                purchaseOrderId = poA.Id, warehouseId = s.Warehouse, note = "[DEMO] nhận hàng",
                listLine = new[] { ReceiveLine(s.Meat, 10m, 3m, 5), ReceiveLine(s.Veg, 20m, 8m, 4), ReceiveLine(s.Fish, 5m, -10m, 60) },
            })).Ok();
            Guid receiptId = created.GetProperty("goodsReceipt").GetProperty("id").GetGuid();
            created.GetProperty("goodsReceipt").GetProperty("receiptNumber").GetString().Should().MatchRegex(@"^PH-261220-\d{4}$");
            created.GetProperty("goodsReceipt").GetProperty("status").GetString().Should().Be("QCPending");
            created.GetProperty("inspectionNumber").GetString().Should().Be(created.GetProperty("goodsReceipt").GetProperty("receiptNumber").GetString());
            created.GetProperty("listWarning").EnumerateArray().Select(x => x.GetString()).Should().Contain(w => w!.Contains("Rau PUR3") && w.Contains("8 °C"));
            // T15.1: mát 8 °C = vùng nhận có điều kiện (5 < t ≤ 8) → gợi ý DAT kèm lý do; đông lạnh −10 °C > −15 °C = vùng loại.
            Line(created, s.Veg).GetProperty("suggestedResult").GetString().Should().Be("DAT");
            Line(created, s.Veg).GetProperty("temperatureZone").GetString().Should().Be("CONDITIONAL");
            Line(created, s.Fish).GetProperty("suggestedResult").GetString().Should().Be("KHONG_DAT", "đông lạnh −10 °C > −15 °C – vùng loại");
            Line(created, s.Meat).GetProperty("suggestedResult").GetString().Should().Be("DAT");
            Guid inspectionId = created.GetProperty("qcInspectionId").GetGuid();
            Guid LineId(Guid ingredient) => Line(created, ingredient).GetProperty("id").GetGuid();

            // Ghi sổ trước khi kết luận QC → chặn; PO hiện "đang chờ".
            (await admin.PostFormAsync("/api/GoodsReceipt/Post", ("Id", receiptId))).Fail("chưa kết luận QC");
            var poDetail = (await admin.GetResultAsync($"/api/PurchaseOrder/GetDetail?id={poA.Id}")).Ok();
            Line(poDetail, s.Meat).GetProperty("pendingQuantity").GetDecimal().Should().Be(10);

            object Qc(Guid ingredient, string result, decimal? accepted = null, string? remark = null) => new { goodsReceiptLineId = LineId(ingredient), result, acceptedQuantity = accepted, remark };
            async Task<LegacyResult> Conclude(params object[] lines) => await PostJson(admin, "/api/QcInspection/Conclude", new { id = inspectionId, note = "[DEMO] QC", listLine = lines });
            (await Conclude(Qc(s.Meat, "DAT"), Qc(s.Veg, "DAT_MOT_PHAN", remark: "Dập 5 kg"))).Fail("Chưa có kết quả QC");
            (await Conclude(Qc(s.Meat, "DAT"), Qc(s.Veg, "DAT_MOT_PHAN", remark: "Dập 5 kg"), Qc(s.Fish, "KHONG_DAT", remark: "Rã đông"))).Fail("số lượng đạt");
            (await Conclude(Qc(s.Meat, "DAT"), Qc(s.Veg, "DAT_MOT_PHAN", 15m, "Dập 5 kg"), Qc(s.Fish, "KHONG_DAT"))).Fail("nhận xét");
            (await Conclude(Qc(s.Meat, "DAT"), Qc(s.Veg, "DAT", null, null), Qc(s.Fish, "KHONG_DAT", remark: "Rã đông"))).Fail("vượt ngưỡng");
            (await Conclude(Qc(s.Meat, "OK"), Qc(s.Veg, "DAT_MOT_PHAN", 15m, "Dập 5 kg"), Qc(s.Fish, "KHONG_DAT", remark: "Rã đông"))).Fail("DAT / DAT_MOT_PHAN / KHONG_DAT");
            var concluded = (await Conclude(Qc(s.Meat, "PASS"), Qc(s.Veg, "DAT_MOT_PHAN", 15m, "Dập 5 kg – nhận phần đạt"), Qc(s.Fish, "REJECT", remark: "Rã đông, nhiệt độ −10 °C"))).Ok();
            concluded.GetProperty("goodsReceipt").GetProperty("status").GetString().Should().Be("PartiallyAccepted");
            concluded.GetProperty("qcResult").GetString().Should().Be("DAT_MOT_PHAN");
            Line(concluded, s.Meat).GetProperty("qualityStatus").GetString().Should().Be("DAT");
            Line(concluded, s.Veg).GetProperty("rejectedQuantity").GetDecimal().Should().Be(5);
            Line(concluded, s.Fish).GetProperty("acceptedQuantity").GetDecimal().Should().Be(0);

            var posted = (await admin.PostFormAsync("/api/GoodsReceipt/Post", ("Id", receiptId))).Ok();
            posted.GetProperty("goodsReceipt").GetProperty("status").GetString().Should().Be("Posted");
            posted.GetProperty("supplierIssueId").ValueKind.Should().Be(JsonValueKind.String);

            // Lô NL-YYMMDD-<mã NCC>-NN cho cả 3 dòng; KHONG_DAT tạm giữ; số lô NCC ghi trong Note.
            var lots = await _factory.QueryAsync(db => db.MaterialLot.AsNoTracking().Where(l => l.SupplierId == s.SupplierA).ToListAsync());
            lots.Should().HaveCount(3);
            lots.Should().OnlyContain(l => System.Text.RegularExpressions.Regex.IsMatch(l.LotNumber!, @"^NL-261220-SAPUR3-\d{2}$") && l.LotNo == l.LotNumber && l.QualityStatus == l.QcStatus);
            var meatLot = lots.Single(l => l.IngredientId == s.Meat);
            var vegLot = lots.Single(l => l.IngredientId == s.Veg);
            var fishLot = lots.Single(l => l.IngredientId == s.Fish);
            meatLot.QcStatus.Should().Be("DAT");
            meatLot.Status.Should().Be("Available");
            meatLot.Note.Should().Contain("Lô NCC: NCC-LOT-10");
            vegLot.QcStatus.Should().Be("DAT_MOT_PHAN");
            fishLot.QcStatus.Should().Be("KHONG_DAT");
            fishLot.Status.Should().Be("OnHold");

            // Sổ kho: chỉ nhập phần đạt (Thịt 10, Rau 15), Cá không nhập.
            var txns = await _factory.QueryAsync(db => db.InventoryTransaction.AsNoTracking().Where(t => t.ReferenceId == receiptId).ToListAsync());
            txns.Should().HaveCount(2);
            txns.Should().OnlyContain(t => t.TransactionType == "RECEIPT" && t.ReferenceType == "GoodsReceipt" && t.WarehouseId == s.Warehouse);
            txns.Single(t => t.MaterialLotId == meatLot.Id).QuantityIn.Should().Be(10);
            txns.Single(t => t.MaterialLotId == vegLot.Id).QuantityIn.Should().Be(15);
            (await _factory.QueryAsync(db => db.InventoryBalance.AsNoTracking().Where(b => b.MaterialLotId == vegLot.Id).SumAsync(b => b.OnHandQuantity))).Should().Be(15);
            (await _factory.QueryAsync(db => db.InventoryBalance.AsNoTracking().AnyAsync(b => b.MaterialLotId == fishLot.Id))).Should().BeFalse();

            // Nhiệt độ nhận hàng (RECEIVING) gắn lô, Rau 8 °C ngoài ngưỡng; sự cố NCC mức High.
            var temps = await _factory.QueryAsync(db => db.TemperatureLog.AsNoTracking().Where(t => t.MeasurementType == "RECEIVING" && lots.Select(l => l.Id).Contains(t.MaterialLotId!.Value)).ToListAsync());
            temps.Should().HaveCount(3);
            temps.Single(t => t.MaterialLotId == vegLot.Id).IsWithinRange.Should().BeFalse();
            temps.Single(t => t.MaterialLotId == vegLot.Id).CorrectiveAction.Should().Contain("Dập 5 kg");
            temps.Single(t => t.MaterialLotId == meatLot.Id).IsWithinRange.Should().BeTrue();
            temps.Single(t => t.MaterialLotId == meatLot.Id).MaxAllowedC.Should().Be(5);
            var issue = await _factory.QueryAsync(db => db.SupplierIssue.AsNoTracking().SingleAsync(i => i.GoodsReceiptId == receiptId));
            issue.Severity.Should().Be("High");
            issue.IssueType.Should().Be("TEMPERATURE");
            issue.IssueStatus.Should().Be("Open");
            issue.DueAt.Should().Be(Today.AddHours(10).AddDays(2));
            issue.Description.Should().Contain(fishLot.LotNumber).And.Contain("Rã đông");

            // PO: Thịt nhận đủ, Rau 15 / 20, Cá 0 / 5 → Nhận một phần; ghi sổ lần 2 bị chặn.
            poDetail = (await admin.GetResultAsync($"/api/PurchaseOrder/GetDetail?id={poA.Id}")).Ok();
            poDetail.GetProperty("purchaseOrder").GetProperty("status").GetString().Should().Be("PartiallyReceived");
            Line(poDetail, s.Veg).GetProperty("receivedQuantity").GetDecimal().Should().Be(15);
            Line(poDetail, s.Veg).GetProperty("remainingQuantity").GetDecimal().Should().Be(5);
            Line(poDetail, s.Fish).GetProperty("remainingQuantity").GetDecimal().Should().Be(5);
            (await admin.PostFormAsync("/api/GoodsReceipt/Post", ("Id", receiptId))).Fail();
            (await admin.PostFormAsync("/api/PurchaseOrder/Cancel", ("Id", poA.Id), ("Reason", "[DEMO] thử hủy"))).Fail();

            // Xuất kho: lô KHONG_DAT bị chặn; lô DAT_MOT_PHAN chỉ xuất được phần đạt (15).
            object Issue(Guid ingredient, Guid lot, decimal quantity) => new { warehouseId = s.Warehouse, note = "[DEMO] xuất thử", listLine = new[] { new { ingredientId = ingredient, materialLotId = lot, quantity } } };
            (await PostJson(admin, "/api/MaterialIssue/Create", Issue(s.Fish, fishLot.Id, 1m))).Fail("KHONG_DAT");
            (await PostJson(admin, "/api/MaterialIssue/Create", Issue(s.Veg, vegLot.Id, 16m))).Fail("Không đủ tồn");
            (await PostJson(admin, "/api/MaterialIssue/Create", Issue(s.Veg, vegLot.Id, 15m))).Ok();
            // Lô KHONG_DAT không "sửa" thành đạt qua danh mục.
            (await admin.PutFormAsync("/api/MaterialLot/Update", ("Id", fishLot.Id), ("IngredientId", s.Fish), ("LotNumber", fishLot.LotNumber), ("QcStatus", "DAT"))).Fail("KHONG_DAT");

            // Nhận bù phần còn lại (Rau 5, Cá 5) → DAT → PO Đã nhận đủ → Đóng.
            var second = (await PostJson(admin, "/api/GoodsReceipt/Create", new
            {
                purchaseOrderId = poA.Id, warehouseId = s.Warehouse,
                listLine = new[] { ReceiveLine(s.Veg, 5m, 4m, 4), ReceiveLine(s.Fish, 5m, -20m, 60) },
            })).Ok();
            Guid secondId = second.GetProperty("goodsReceipt").GetProperty("id").GetGuid();
            Guid secondInspection = second.GetProperty("qcInspectionId").GetGuid();
            (await PostJson(admin, "/api/QcInspection/Conclude", new
            {
                id = secondInspection,
                listLine = second.GetProperty("listLine").EnumerateArray().Select(l => new { goodsReceiptLineId = l.GetProperty("id").GetGuid(), result = "DAT" }).ToArray(),
            })).Ok().GetProperty("goodsReceipt").GetProperty("status").GetString().Should().Be("Accepted");
            (await admin.PostFormAsync("/api/GoodsReceipt/Post", ("Id", secondId))).Ok().GetProperty("supplierIssueId").ValueKind.Should().Be(JsonValueKind.Null);
            (await PoOfAsync(s.SupplierA, s.Request)).Status.Should().Be("Received");
            (await admin.PostFormAsync("/api/PurchaseOrder/Close", ("Id", poA.Id))).Ok().GetProperty("purchaseOrder").GetProperty("status").GetString().Should().Be("Closed");
            (await PostJson(admin, "/api/GoodsReceipt/Create", new { purchaseOrderId = poA.Id, warehouseId = s.Warehouse, listLine = new[] { ReceiveLine(s.Meat, 1m, 3m, 5) } })).Fail("Đóng");
        }

        [Fact]
        public async Task Legacy_crud_cannot_bypass_purchasing_state_machine()
        {
            using var admin = _factory.CreateAuthorizedClient();
            var (s, poA, poB) = await ApprovedPoAsync(admin, "PUR4");

            // PO: CRUD Insert = Nháp + số PO- + tổng 0 (bỏ qua trạng thái gửi lên); Update PO đã duyệt / đổi trạng thái → chặn.
            (await admin.PostFormAsync("/api/PurchaseOrder/Insert", ("SupplierId", s.SupplierB), ("OrderDate", Today), ("Status", "Approved"), ("TotalAmount", 1m), ("Note", "[DEMO] PO tay PUR4"))).Ok();
            var manual = await _factory.QueryAsync(db => db.PurchaseOrder.AsNoTracking().SingleAsync(p => p.Note == "[DEMO] PO tay PUR4"));
            manual.Status.Should().Be("Draft");
            manual.TotalAmount.Should().Be(0);
            manual.PurchaseOrderNumber.Should().MatchRegex(@"^PO-261220-\d{4}$");
            (await admin.PutFormAsync("/api/PurchaseOrder/Update", ("Id", poA.Id), ("SupplierId", s.SupplierA), ("OrderDate", Today), ("Note", "x"))).Fail("Nháp");
            (await admin.PutFormAsync("/api/PurchaseOrder/Update", ("Id", manual.Id), ("SupplierId", s.SupplierB), ("OrderDate", Today), ("Status", "Approved"))).Fail("thao tác nghiệp vụ");
            (await admin.PostFormAsync("/api/PurchaseOrder/Submit", ("Id", manual.Id))).Fail("chưa có dòng");
            (await admin.DeleteResultAsync($"/api/PurchaseOrder/Delete?id={manual.Id}")).Ok();
            (await admin.PostFormAsync("/api/PurchaseOrder/Insert", ("SupplierId", Guid.NewGuid()), ("OrderDate", Today))).Fail("nhà cung cấp");

            // Dòng PO / phiếu nhập / QC: CRUD không ghi.
            (await admin.PostFormAsync("/api/PurchaseOrderLine/Insert", ("PurchaseOrderId", poB.Id), ("IngredientId", s.Rice), ("Quantity", 1m), ("UnitId", s.Kg), ("UnitPrice", 1m))).Fail("UpdateLines");
            (await admin.PostFormAsync("/api/GoodsReceipt/Insert", ("SupplierId", s.SupplierA), ("WarehouseId", s.Warehouse), ("ReceiptDate", Today))).Fail("GoodsReceipt/Create");
            (await admin.PostFormAsync("/api/GoodsReceiptLine/Insert", ("GoodsReceiptId", Guid.NewGuid()), ("IngredientId", s.Meat), ("Quantity", 1m), ("UnitId", s.Kg))).Fail("GoodsReceipt/Create");
            (await admin.PostFormAsync("/api/QcInspection/Insert", ("GoodsReceiptId", Guid.NewGuid()), ("InspectionDate", Today))).Fail("Conclude");
            (await admin.PostFormAsync("/api/QcInspectionLine/Insert", ("QCInspectionId", Guid.NewGuid()), ("IngredientId", s.Meat))).Fail("Conclude");

            // Phiếu nhập chưa ghi sổ: hủy được (lý do bắt buộc), không xóa; PO hủy được khi chỉ có phiếu đã hủy.
            var line = await _factory.QueryAsync(db => db.PurchaseOrderLine.AsNoTracking().FirstAsync(l => l.PurchaseOrderId == poA.Id && l.IngredientId == s.Meat));
            var receipt = (await PostJson(admin, "/api/GoodsReceipt/Create", new { purchaseOrderId = poA.Id, warehouseId = s.Warehouse, listLine = new[] { new { purchaseOrderLineId = line.Id, receivedQuantity = 2m } } })).Ok();
            Guid receiptId = receipt.GetProperty("goodsReceipt").GetProperty("id").GetGuid();
            (await admin.DeleteResultAsync($"/api/GoodsReceipt/Delete?id={receiptId}")).Fail("Cancel");
            (await admin.PostFormAsync("/api/PurchaseOrder/Cancel", ("Id", poA.Id), ("Reason", "[DEMO] hủy"))).Fail("phiếu nhập");
            (await admin.PostFormAsync("/api/GoodsReceipt/Cancel", ("Id", receiptId))).Fail("lý do");
            (await admin.PostFormAsync("/api/GoodsReceipt/Cancel", ("Id", receiptId), ("Reason", "[DEMO] nhập nhầm"))).Ok().GetProperty("goodsReceipt").GetProperty("status").GetString().Should().Be("Cancelled");
            (await admin.PostFormAsync("/api/GoodsReceipt/Post", ("Id", receiptId))).Fail();
            (await admin.PostFormAsync("/api/PurchaseOrder/Cancel", ("Id", poA.Id), ("Reason", "[DEMO] hủy"))).Ok();
        }

        [Fact]
        public async Task Mrp_purchase_request_converts_to_po_and_pre_production_sheet_prints()
        {
            // Bộ nhỏ: 1 đơn Đã chốt 200 suất × công thức 20 kg / 100 suất → cần 40 kg, tồn 0 → đề nghị mua 40 kg → PO 40 × 50 000.
            DateTime date = Today.AddDays(2);
            int day = date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;
            Guid warehouse = Guid.NewGuid(), kg = Guid.NewGuid(), ingredient = Guid.NewGuid(), dish = Guid.NewGuid(), menu = Guid.NewGuid(), customer = Guid.NewGuid(), order = Guid.NewGuid(), supplier = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Warehouse.Add(new Warehouse { Id = warehouse, Code = "PUR5", Name = "Kho PUR5", Status = "Active" });
                db.UnitMeasure.Add(new UnitMeasure { Id = kg, Code = "KG-PUR5", Name = "kg" });
                db.Ingredient.Add(new Ingredient { Id = ingredient, Code = "GA-PUR5", Name = "Gà PUR5", BaseUnitId = kg });
                db.Dish.Add(new Dish { Id = dish, Code = "GA-PUR5", Name = "Gà luộc PUR5" });
                Guid recipe = Guid.NewGuid();
                db.Recipe.Add(new Recipe { Id = recipe, DishId = dish, VersionNo = 1, Status = "Locked", IsActive = true, StandardOutputQty = 100, EffectiveFrom = Today.AddDays(-30), LockedAt = Today.AddDays(-30) });
                db.RecipeLine.Add(new RecipeLine { Id = Guid.NewGuid(), RecipeId = recipe, IngredientId = ingredient, Quantity = 20, UnitId = kg, UomId = kg });
                db.Menu.Add(new Model.Entity.Menu { Id = menu, Code = "TD-PUR5", Name = "Thực đơn PUR5", VersionNo = 1, Status = "Published", EffectiveFrom = Today.AddDays(-30) });
                db.MenuLine.Add(new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = dish, MealType = "TRUA", DayOfWeek = day, SequenceNo = 1 });
                db.Customer.Add(new Customer { Id = customer, Code = "PUR5", Name = "Khách PUR5", CIF = "PUR5", CustomerType = "FACTORY", DisplayName = "PUR5", Status = "Active" });
                db.Order.Add(new Order { Id = order, OrderNumber = "PH-PUR5", OrderNo = "PH-PUR5", CustomerId = customer, OrderDate = Today, MealDate = date, DeliveryDate = date, Status = "Locked", TotalQuantity = 200 });
                db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = order, MenuId = menu, MealType = "TRUA", Quantity = 200 });
                db.Supplier.Add(new Supplier { Id = supplier, Code = "SC-PUR5", Name = "NCC gà PUR5", Status = "Active" });
                db.SupplierMaterial.Add(new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = supplier, IngredientId = ingredient, IsPreferred = true, UnitPrice = 50000, PurchaseUomId = kg, Status = "Active" });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();
            var mrp = (await c.PostFormAsync("/api/MaterialRequirement/Calculate", ("ProductionDate", date), ("WarehouseId", warehouse))).Ok();
            Guid planId = mrp.GetProperty("productionPlanId").GetGuid();
            Guid prId = mrp.GetProperty("purchaseRequest").GetProperty("id").GetGuid();

            var response = await c.GetAsync($"/api/MaterialRequirement/ExportPreProduction?id={planId}");
            response.EnsureSuccessStatusCode();
            response.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            using (var stream = new MemoryStream(await response.Content.ReadAsByteArrayAsync()))
            using (var wb = new XLWorkbook(stream))
            {
                var ws = wb.Worksheets.First();
                ws.Cell(3, 1).GetString().Should().Be("PHIẾU TRƯỚC SẢN XUẤT");
                ws.Cell(1, 13).GetValue<decimal>().Should().Be(200);
                var text = ws.CellsUsed().Select(x => x.GetString()).ToList();
                text.Should().Contain("Gà luộc PUR5").And.Contain("Gà PUR5").And.Contain("Tình trạng hàng").And.Contain("NCC gà PUR5");
                var row = ws.CellsUsed().First(x => x.GetString() == "Gà PUR5").Address.RowNumber;
                ws.Cell(row, 5).GetValue<decimal>().Should().Be(40);
                ws.Cell(row, 10).GetValue<decimal>().Should().Be(40);
            }
            (await c.GetAsync($"/api/MaterialRequirement/ExportPreProduction?id={Guid.NewGuid()}")).EnsureSuccessStatusCode();

            await c.ApprovePurchaseRequestAsync(prId); // BE2.3 (D-46): đề nghị do MRP lập cũng phải duyệt 2 cấp.
            var converted = (await c.PostFormAsync("/api/PurchaseRequest/ConvertToPurchaseOrder", ("Id", prId))).Ok();
            var po = converted.GetProperty("listPurchaseOrder").EnumerateArray().Single();
            po.GetProperty("totalAmount").GetDecimal().Should().Be(2_000_000m);
            // Đề nghị đã lập PO → MRP không tính lại (GAP-MRP-06).
            (await c.PostFormAsync("/api/MaterialRequirement/Calculate", ("ProductionDate", date), ("WarehouseId", warehouse))).Fail("Đã lập PO");
        }
    }
}
