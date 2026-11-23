using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Option;
using AppDelicare.Tests.Integration;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AppDelicare.Tests.Purchasing
{
    /// <summary>
    /// T15.1 – quy tắc mua hàng anh chốt 2026-11-16: nhiệt độ nhận 3 vùng (CHILLED ≤ 5 / ≤ 8 / &gt; 8; FROZEN ≤ −18 / ≤ −15 / &gt; −15; DRY không kiểm),
    /// PO ≥ 50 triệu do Giám đốc duyệt (mục #/wf-inbox?type=scm-po-director), VAT phương án A (tính khi đọc / in, không lưu).
    /// Hôm nay (đồng hồ test) = 20/12/2026 10:00.
    /// </summary>
    public class PurchasingOwnerRulesTests : IClassFixture<InMemoryApiFactory>
    {
        private static readonly DateTime Today = new(2026, 12, 20);
        private readonly InMemoryApiFactory _factory;

        public PurchasingOwnerRulesTests(InMemoryApiFactory factory)
        {
            _factory = factory;
            _factory.Clock.FixedNow = Today.AddHours(10);
        }

        private sealed record Item(string Key, string? StorageType, decimal Quantity, decimal UnitPrice, Guid? CategoryId = null);

        private sealed record Seeded(Guid Warehouse, Guid Supplier, Guid PurchaseOrder, Dictionary<string, Guid> Ingredients, Dictionary<string, Guid> PoLines);

        /// <summary>1 NCC + nguyên liệu + 1 PO (trạng thái <paramref name="status"/>) với mỗi nguyên liệu 1 dòng; tổng tiền = Σ dòng.</summary>
        private async Task<Seeded> SeedPoAsync(string code, string status, params Item[] items)
        {
            Guid warehouse = Guid.NewGuid(), kg = Guid.NewGuid(), supplier = Guid.NewGuid(), po = Guid.NewGuid();
            var ingredients = items.ToDictionary(i => i.Key, _ => Guid.NewGuid());
            var lines = items.ToDictionary(i => i.Key, _ => Guid.NewGuid());
            await _factory.SeedAsync(db =>
            {
                db.Warehouse.Add(new Warehouse { Id = warehouse, Code = code, Name = "Kho " + code, Status = "Active" });
                db.UnitMeasure.Add(new UnitMeasure { Id = kg, Code = "KG-" + code, Name = "kg" });
                db.Supplier.Add(new Supplier { Id = supplier, Code = "S" + code, Name = "NCC " + code, Status = "Active" });
                db.PurchaseOrder.Add(new PurchaseOrder
                {
                    Id = po, PurchaseOrderNumber = "PO-" + code, PONo = "PO-" + code, SupplierId = supplier, OrderDate = Today, Status = status,
                    TotalAmount = items.Sum(i => i.Quantity * i.UnitPrice),
                });
                foreach (var i in items)
                {
                    db.Ingredient.Add(new Ingredient { Id = ingredients[i.Key], Code = i.Key + "-" + code, Name = i.Key + " " + code, BaseUnitId = kg, StorageType = i.StorageType, IngredientCategoryId = i.CategoryId });
                    db.PurchaseOrderLine.Add(new PurchaseOrderLine
                    {
                        Id = lines[i.Key], PurchaseOrderId = po, IngredientId = ingredients[i.Key], Quantity = i.Quantity, UnitId = kg, UomId = kg,
                        UnitPrice = i.UnitPrice, LineAmount = i.Quantity * i.UnitPrice, Amount = i.Quantity * i.UnitPrice,
                    });
                }
                return Task.CompletedTask;
            });
            return new Seeded(warehouse, supplier, po, ingredients, lines);
        }

        private static async Task<LegacyResult> PostJson(HttpClient c, string url, object body) => await LegacyResult.ReadAsync(await c.PostAsJsonAsync(url, body));

        private static JsonElement LineOf(JsonElement detail, Guid ingredient)
            => detail.GetProperty("listLine").EnumerateArray().Single(l => l.GetProperty("ingredientId").GetGuid() == ingredient);

        /// <summary>User thường có quyền Duyệt (AllowAccess + AllowEdit) trên các mục <c>#/wf-inbox?type=&lt;screen&gt;</c>.</summary>
        private async Task<Guid> ApproverAsync(params string[] screens)
        {
            Guid userId = Guid.NewGuid(), group = Guid.NewGuid();
            await _factory.SeedAsync(async db =>
            {
                db.User.Add(new User { Id = userId, UserName = "t151-" + userId.ToString("N")[..6], IsActive = true });
                db.UserGroup.Add(new UserGroup { Id = group, Name = "T151-" + group.ToString("N")[..6] });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = userId, UserGroupId = group });
                foreach (string screen in screens)
                {
                    string link = "#/wf-inbox?type=" + screen;
                    var menu = await db.ObjectMenu.FirstOrDefaultAsync(m => m.MetaLink == link);
                    if (menu == null)
                    {
                        menu = new ObjectMenu { Id = Guid.NewGuid(), Name = screen, MetaLink = link, IsActive = true, LevelShow = 2 };
                        db.ObjectMenu.Add(menu);
                        await db.SaveChangesAsync();
                    }
                    db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = group, ObjectMenuId = menu.Id, AllowAccess = true, AllowEdit = true });
                }
            });
            return userId;
        }

        [Fact]
        public async Task Receiving_temperature_three_zones_accept_conditional_reject_and_dry_unchecked()
        {
            // Mỗi dòng đặt 10 kg (C7 đặt 20 để nhận bù lần 2).
            var s = await SeedPoAsync("TMP1", "Approved",
                new Item("C4", "CHILLED", 10, 1000), new Item("C7", "CHILLED", 20, 1000), new Item("C9", "CHILLED", 10, 1000),
                new Item("F20", "FROZEN", 10, 1000), new Item("F16", "FROZEN", 10, 1000), new Item("F12", "FROZEN", 10, 1000),
                new Item("DRY", "DRY", 10, 1000), new Item("CMISS", "CHILLED", 10, 1000));
            using var admin = _factory.CreateAuthorizedClient();
            object Receive(string key, decimal quantity, decimal? temperature)
                => new { purchaseOrderLineId = s.PoLines[key], receivedQuantity = quantity, expiryDate = Today.AddDays(30), temperatureC = temperature };
            var created = (await PostJson(admin, "/api/GoodsReceipt/Create", new
            {
                purchaseOrderId = s.PurchaseOrder, warehouseId = s.Warehouse,
                listLine = new[]
                {
                    Receive("C4", 10, 4), Receive("C7", 10, 7), Receive("C9", 10, 9), Receive("F20", 10, -20), Receive("F16", 10, -16), Receive("F12", 10, -12),
                    Receive("DRY", 10, null), Receive("CMISS", 10, null),
                },
            })).Ok();
            string Zone(string key) => LineOf(created, s.Ingredients[key]).GetProperty("temperatureZone").GetString()!;
            Zone("C4").Should().Be("ACCEPT");
            Zone("C7").Should().Be("CONDITIONAL");
            Zone("C9").Should().Be("REJECT");
            Zone("F20").Should().Be("ACCEPT");
            Zone("F16").Should().Be("CONDITIONAL");
            Zone("F12").Should().Be("REJECT");
            Zone("DRY").Should().Be("NOT_CHECKED");
            Zone("CMISS").Should().Be("MISSING");
            LineOf(created, s.Ingredients["F16"]).GetProperty("noThawConfirmationRequired").GetBoolean().Should().BeTrue();
            LineOf(created, s.Ingredients["C7"]).GetProperty("noThawConfirmationRequired").GetBoolean().Should().BeFalse();
            LineOf(created, s.Ingredients["C9"]).GetProperty("suggestedResult").GetString().Should().Be("KHONG_DAT");
            LineOf(created, s.Ingredients["C7"]).GetProperty("conditionalMaxTemperature").GetDecimal().Should().Be(8);
            created.GetProperty("listWarning").EnumerateArray().Select(x => x.GetString()).Should()
                .Contain(w => w!.Contains("C7 TMP1") && w.Contains("có điều kiện"))
                .And.Contain(w => w!.Contains("C9 TMP1") && w.Contains("vùng loại"))
                .And.Contain(w => w!.Contains("CMISS TMP1") && w.Contains("chưa có nhiệt độ"));

            Guid inspection = created.GetProperty("qcInspectionId").GetGuid();
            Guid receiptId = created.GetProperty("goodsReceipt").GetProperty("id").GetGuid();
            Guid LineId(string key) => LineOf(created, s.Ingredients[key]).GetProperty("id").GetGuid();
            var ok = new Dictionary<string, object>
            {
                ["C4"] = new { goodsReceiptLineId = LineId("C4"), result = "DAT" },
                ["C7"] = new { goodsReceiptLineId = LineId("C7"), result = "DAT", remark = "Xe lạnh trễ, hàng còn lạnh – nhận có điều kiện" },
                ["C9"] = new { goodsReceiptLineId = LineId("C9"), result = "KHONG_DAT", remark = "9 °C > 8 °C – trả NCC" },
                ["F20"] = new { goodsReceiptLineId = LineId("F20"), result = "DAT" },
                ["F16"] = new { goodsReceiptLineId = LineId("F16"), result = "DAT", remark = "−16 °C, đá cứng", noThawConfirmed = true },
                ["F12"] = new { goodsReceiptLineId = LineId("F12"), result = "KHONG_DAT", remark = "Rã đông" },
                ["DRY"] = new { goodsReceiptLineId = LineId("DRY"), result = "DAT" },
                ["CMISS"] = new { goodsReceiptLineId = LineId("CMISS"), result = "DAT", temperature = 3m },
            };
            Task<LegacyResult> Conclude(string key, object replacement)
                => PostJson(admin, "/api/QcInspection/Conclude", new { id = inspection, listLine = ok.Select(kv => kv.Key == key ? replacement : kv.Value).ToArray() });

            // Mát 7 °C nhận mà không có lý do → lỗi.
            (await Conclude("C7", new { goodsReceiptLineId = LineId("C7"), result = "DAT" })).Fail("vượt ngưỡng");
            // Mát 9 °C: vùng loại – không nhận được kể cả có lý do (DAT / DAT_MOT_PHAN).
            (await Conclude("C9", new { goodsReceiptLineId = LineId("C9"), result = "DAT", remark = "Khách cần gấp" })).Fail("vùng loại");
            (await Conclude("C9", new { goodsReceiptLineId = LineId("C9"), result = "DAT_MOT_PHAN", acceptedQuantity = 5m, remark = "Nhận một nửa" })).Fail("vùng loại");
            // Đông lạnh −16 °C: có lý do nhưng chưa xác nhận không rã đông → lỗi.
            (await Conclude("F16", new { goodsReceiptLineId = LineId("F16"), result = "DAT", remark = "−16 °C" })).Fail("rã đông");
            // Đông lạnh −12 °C: vùng loại.
            (await Conclude("F12", new { goodsReceiptLineId = LineId("F12"), result = "DAT", remark = "Vẫn nhận" })).Fail("vùng loại");
            // Hàng mát chưa đo nhiệt độ → lỗi khi kết luận đạt.
            (await Conclude("CMISS", new { goodsReceiptLineId = LineId("CMISS"), result = "DAT" })).Fail("chưa có nhiệt độ");

            var concluded = (await PostJson(admin, "/api/QcInspection/Conclude", new { id = inspection, listLine = ok.Values.ToArray() })).Ok();
            concluded.GetProperty("goodsReceipt").GetProperty("status").GetString().Should().Be("PartiallyAccepted");
            LineOf(concluded, s.Ingredients["C7"]).GetProperty("useFirst").GetBoolean().Should().BeTrue();
            LineOf(concluded, s.Ingredients["C4"]).GetProperty("useFirst").GetBoolean().Should().BeFalse();
            LineOf(concluded, s.Ingredients["CMISS"]).GetProperty("temperatureZone").GetString().Should().Be("ACCEPT");
            var qcNotes = await _factory.QueryAsync(db => db.QcInspectionLine.AsNoTracking().Where(q => q.QCInspectionId == inspection).ToListAsync());
            qcNotes.Single(q => q.IngredientId == s.Ingredients["F16"]).Note.Should().Contain("[UU_TIEN_DUNG_TRUOC]").And.Contain("không có dấu hiệu rã đông");

            var posted = (await admin.PostFormAsync("/api/GoodsReceipt/Post", ("Id", receiptId))).Ok();
            var lots = await _factory.QueryAsync(db => db.MaterialLot.AsNoTracking().Where(l => l.SupplierId == s.Supplier).ToListAsync());
            MaterialLot Lot(string key) => lots.Single(l => l.IngredientId == s.Ingredients[key]);
            Lot("C7").Status.Should().Be("Available");
            Lot("C7").QcStatus.Should().Be("DAT");
            Lot("C7").Note.Should().Contain("[UU_TIEN_DUNG_TRUOC]").And.Contain("7 °C");
            Lot("F16").Note.Should().Contain("[UU_TIEN_DUNG_TRUOC]").And.Contain("rã đông");
            Lot("C4").Note.Should().NotContain("[UU_TIEN_DUNG_TRUOC]");
            foreach (string rejected in new[] { "C9", "F12" })
            {
                Lot(rejected).QcStatus.Should().Be("KHONG_DAT");
                Lot(rejected).Status.Should().Be("OnHold");
            }
            var txns = await _factory.QueryAsync(db => db.InventoryTransaction.AsNoTracking().Where(t => t.ReferenceId == receiptId).ToListAsync());
            txns.Select(t => t.IngredientId).Should().BeEquivalentTo(new[] { "C4", "C7", "F20", "F16", "DRY", "CMISS" }.Select(k => s.Ingredients[k]), "vùng loại không nhập kho");
            var issue = await _factory.QueryAsync(db => db.SupplierIssue.AsNoTracking().SingleAsync(i => i.GoodsReceiptId == receiptId));
            posted.GetProperty("supplierIssueId").GetGuid().Should().Be(issue.Id);
            issue.IssueType.Should().Be("TEMPERATURE");
            issue.Severity.Should().Be("High");
            issue.Description.Should().Contain(Lot("C7").LotNumber).And.Contain(Lot("C9").LotNumber).And.Contain(Lot("F16").LotNumber).And.NotContain(Lot("C4").LotNumber!);
            var temps = await _factory.QueryAsync(db => db.TemperatureLog.AsNoTracking().Where(t => lots.Select(l => l.Id).Contains(t.MaterialLotId!.Value)).ToListAsync());
            temps.Should().HaveCount(7, "DRY không có nhiệt độ");
            temps.Single(t => t.MaterialLotId == Lot("C7").Id).IsWithinRange.Should().BeFalse();
            temps.Single(t => t.MaterialLotId == Lot("C7").Id).MaxAllowedC.Should().Be(5);

            // Phiếu chỉ có 1 dòng nhận có điều kiện (DAT) → vẫn ghi sự cố NCC nhiệt độ, mức Medium.
            var second = (await PostJson(admin, "/api/GoodsReceipt/Create", new { purchaseOrderId = s.PurchaseOrder, warehouseId = s.Warehouse, listLine = new[] { Receive("C7", 10, 6) } })).Ok();
            (await PostJson(admin, "/api/QcInspection/Conclude", new
            {
                id = second.GetProperty("qcInspectionId").GetGuid(),
                listLine = new[] { new { goodsReceiptLineId = second.GetProperty("listLine")[0].GetProperty("id").GetGuid(), result = "DAT", remark = "6 °C – dùng ngay ca trưa" } },
            })).Ok().GetProperty("goodsReceipt").GetProperty("status").GetString().Should().Be("Accepted");
            Guid secondId = second.GetProperty("goodsReceipt").GetProperty("id").GetGuid();
            (await admin.PostFormAsync("/api/GoodsReceipt/Post", ("Id", secondId))).Ok().GetProperty("supplierIssueId").ValueKind.Should().Be(JsonValueKind.String);
            var issue2 = await _factory.QueryAsync(db => db.SupplierIssue.AsNoTracking().SingleAsync(i => i.GoodsReceiptId == secondId));
            issue2.IssueType.Should().Be("TEMPERATURE");
            issue2.Severity.Should().Be("Medium");
        }

        [Fact]
        public async Task Po_at_or_above_director_threshold_needs_director_approval_menu_right()
        {
            // PO lớn: 600 kg × 100 000 = 60 000 000 ≥ 50 000 000; PO nhỏ: 100 × 100 000 = 10 000 000; PO biên: 500 × 100 000 = 50 000 000 (= ngưỡng → GĐ).
            var big = await SeedPoAsync("DIR1", "Draft", new Item("THIT", "CHILLED", 600, 100_000));
            var small = await SeedPoAsync("DIR2", "Draft", new Item("THIT", "CHILLED", 100, 100_000));
            var edge = await SeedPoAsync("DIR3", "Draft", new Item("THIT", "CHILLED", 500, 100_000));
            using var admin = _factory.CreateAuthorizedClient();
            using var poApprover = _factory.CreateAuthorizedClient(await ApproverAsync("scm-po"));
            using var director = _factory.CreateAuthorizedClient(await ApproverAsync("scm-po-director"));

            var edgeDetail = (await admin.GetResultAsync($"/api/PurchaseOrder/GetDetail?id={edge.PurchaseOrder}")).Ok();
            edgeDetail.GetProperty("needsDirectorApproval").GetBoolean().Should().BeTrue("tổng = ngưỡng → GĐ duyệt (≥)");
            edgeDetail.GetProperty("approvalScreenId").GetString().Should().Be("scm-po-director");

            var bigSubmit = await admin.PostFormAsync("/api/PurchaseOrder/Submit", ("Id", big.PurchaseOrder));
            bigSubmit.Message.Should().Contain("Giám đốc");
            var bigDetail = bigSubmit.Ok();
            bigDetail.GetProperty("needsDirectorApproval").GetBoolean().Should().BeTrue();
            bigDetail.GetProperty("approvalScreenId").GetString().Should().Be("scm-po-director");
            (await admin.PostFormAsync("/api/PurchaseOrder/Submit", ("Id", small.PurchaseOrder))).Ok()
                .GetProperty("approvalScreenId").GetString().Should().Be("scm-po");
            var bigRequest = await _factory.QueryAsync(db => db.ApprovalRequest.AsNoTracking().SingleAsync(a => a.BusinessId == big.PurchaseOrder));
            var smallRequest = await _factory.QueryAsync(db => db.ApprovalRequest.AsNoTracking().SingleAsync(a => a.BusinessId == small.PurchaseOrder));

            async Task<List<Guid>> PendingIds(HttpClient c, string? type)
                => (await c.GetResultAsync("/api/ApprovalRequest/GetMyPending" + (type == null ? string.Empty : "?type=" + type))).ListData.EnumerateArray()
                    .Select(x => x.GetProperty("id").GetGuid()).ToList();

            // Người duyệt PO thường: không thấy PO lớn, không duyệt được (ApprovalNoRight) – qua PurchaseOrder/Approve và ApprovalRequest/Decide.
            (await PendingIds(poApprover, null)).Should().Contain(smallRequest.Id).And.NotContain(bigRequest.Id);
            (await PendingIds(poApprover, "scm-po")).Should().Contain(smallRequest.Id).And.NotContain(bigRequest.Id);
            (await PendingIds(poApprover, "scm-po-director")).Should().BeEmpty();
            (await poApprover.PostFormAsync("/api/PurchaseOrder/Approve", ("Id", big.PurchaseOrder))).Fail("không có quyền");
            (await poApprover.PostFormAsync("/api/ApprovalRequest/Decide", ("Id", bigRequest.Id), ("Approve", true))).Fail("không có quyền");
            (await poApprover.PostFormAsync("/api/PurchaseOrder/Reject", ("Id", big.PurchaseOrder), ("Note", "thử"))).Fail("không có quyền");

            // Giám đốc: thấy PO lớn trong hộp "scm-po-director" (không thấy PO nhỏ vì không có quyền scm-po), duyệt được.
            var directorPending = (await director.GetResultAsync("/api/ApprovalRequest/GetMyPending?type=scm-po-director")).ListData.EnumerateArray()
                .Single(x => x.GetProperty("id").GetGuid() == bigRequest.Id);
            directorPending.GetProperty("screenId").GetString().Should().Be("scm-po-director");
            directorPending.GetProperty("title").GetString().Should().Contain("PO-DIR1").And.Contain("60,000,000").And.Contain("Giám đốc");
            (await PendingIds(director, "scm-po")).Should().NotContain(bigRequest.Id);
            (await PendingIds(director, null)).Should().NotContain(smallRequest.Id);
            (await director.PostFormAsync("/api/PurchaseOrder/Approve", ("Id", small.PurchaseOrder))).Fail("không có quyền");
            (await director.PostFormAsync("/api/PurchaseOrder/Approve", ("Id", big.PurchaseOrder), ("Note", "GĐ đồng ý"))).Ok();
            (await _factory.QueryAsync(db => db.PurchaseOrder.AsNoTracking().SingleAsync(p => p.Id == big.PurchaseOrder))).Status.Should().Be("Approved");
            (await _factory.QueryAsync(db => db.ApprovalDecision.AsNoTracking().SingleAsync(d => d.ApprovalRequestId == bigRequest.Id))).DecisionCode.Should().Be("APPROVE");

            // PO nhỏ: người duyệt PO thường duyệt.
            (await poApprover.PostFormAsync("/api/PurchaseOrder/Approve", ("Id", small.PurchaseOrder))).Ok();
            (await _factory.QueryAsync(db => db.PurchaseOrder.AsNoTracking().SingleAsync(p => p.Id == small.PurchaseOrder))).Status.Should().Be("Approved");
        }

        [Fact]
        public async Task Vat_is_computed_on_detail_and_print_with_default_and_category_override()
        {
            // Nhóm cha "RAUTUOI-VAT1" = 5% (cấu hình tạm), nguyên liệu Rau thuộc nhóm con "RAULA-VAT1" → lấy theo nhóm cha; còn lại mặc định 8%.
            Guid parent = Guid.NewGuid(), child = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.IngredientCategory.AddRange(
                    new IngredientCategory { Id = parent, Code = "RAUTUOI-VAT1", Name = "Rau củ tươi" },
                    new IngredientCategory { Id = child, Code = "RAULA-VAT1", Name = "Rau lá", ParentId = parent });
                return Task.CompletedTask;
            });
            var rules = _factory.Services.GetRequiredService<IOptions<BusinessRulesOptions>>().Value;
            rules.PurchaseVat.ByIngredientCategoryCode["RAUTUOI-VAT1"] = 5m;
            try
            {
                // Thịt 10 × 100 000 = 1 000 000 → 8% = 80 000; Rau 20 × 20 000 = 400 000 → 5% = 20 000; Muối 3 × 12 345 = 37 035 → 8% = 2 962,8 → 2 963.
                // Tổng chưa VAT 1 437 035; VAT 102 963; gồm VAT 1 539 998.
                var s = await SeedPoAsync("VAT1", "Approved",
                    new Item("THIT", "CHILLED", 10, 100_000), new Item("RAU", "CHILLED", 20, 20_000, child), new Item("MUOI", "DRY", 3, 12_345));
                using var admin = _factory.CreateAuthorizedClient();
                var detail = (await admin.GetResultAsync($"/api/PurchaseOrder/GetDetail?id={s.PurchaseOrder}")).Ok();
                JsonElement L(string key) => LineOf(detail, s.Ingredients[key]);
                L("THIT").GetProperty("vatPct").GetDecimal().Should().Be(8);
                L("THIT").GetProperty("vatAmount").GetDecimal().Should().Be(80_000);
                L("RAU").GetProperty("vatPct").GetDecimal().Should().Be(5);
                L("RAU").GetProperty("vatAmount").GetDecimal().Should().Be(20_000);
                L("RAU").GetProperty("amountInclVat").GetDecimal().Should().Be(420_000);
                L("MUOI").GetProperty("vatAmount").GetDecimal().Should().Be(2_963);
                detail.GetProperty("subtotalAmount").GetDecimal().Should().Be(1_437_035);
                detail.GetProperty("vatAmount").GetDecimal().Should().Be(102_963);
                detail.GetProperty("totalAmountInclVat").GetDecimal().Should().Be(1_539_998);
                detail.GetProperty("purchaseOrder").GetProperty("totalAmount").GetDecimal().Should().Be(1_437_035, "không lưu VAT vào PO");

                var response = await admin.GetAsync($"/api/PurchaseOrder/ExportOrder?id={s.PurchaseOrder}");
                response.EnsureSuccessStatusCode();
                using var stream = new MemoryStream(await response.Content.ReadAsByteArrayAsync());
                using var wb = new XLWorkbook(stream);
                var ws = wb.Worksheets.First();
                ws.Cell(12, 8).GetString().Should().Be("% VAT");
                ws.Cell(12, 9).GetString().Should().Be("Tiền VAT");
                int Row(string text) => ws.CellsUsed().First(x => x.GetString() == text).Address.RowNumber;
                ws.Cell(Row("RAU VAT1"), 8).GetValue<decimal>().Should().Be(5);
                ws.Cell(Row("RAU VAT1"), 9).GetValue<decimal>().Should().Be(20_000);
                ws.Cell(Row("THIT VAT1"), 9).GetValue<decimal>().Should().Be(80_000);
                ws.Cell(Row("Tổng cộng (chưa VAT)"), 7).GetValue<decimal>().Should().Be(1_437_035);
                ws.Cell(Row("Tiền thuế VAT"), 7).GetValue<decimal>().Should().Be(102_963);
                ws.Cell(Row("Tổng cộng (gồm VAT)"), 7).GetValue<decimal>().Should().Be(1_539_998);
            }
            finally
            {
                rules.PurchaseVat.ByIngredientCategoryCode.Remove("RAUTUOI-VAT1");
            }
        }
    }
}
