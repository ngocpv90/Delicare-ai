using System.Net;
using System.Net.Http.Json;
using System.Text;
using AppDelicare.Model.Entity;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Common
{
    /// <summary>
    /// BE2.7 – mẫu in (spec 2.5, Catalog 24–30): phiếu nhập (chỉ khi QC đã kết luận), phiếu xuất (chỉ Đã xuất), kiểm kê (chưa ghi sổ → BẢN NHÁP), phiếu giao,
    /// biên bản POD, đối soát (không in khi Mở) – PDF (mặc định, BE2.7.1 – PDFsharp-MigraDoc + Noto Sans, D-56 k) / xlsx / docx (GemBox); in hàng loạt theo danh sách + theo chuyến, tối đa 100, quyền In từng loại phiếu.
    /// </summary>
    public class PrintTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public PrintTests(InMemoryApiFactory factory) => _factory = factory;

        private static readonly byte[] ZipMagic = [0x50, 0x4B, 0x03, 0x04];
        private const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        private const string DocxType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        private const string PdfType = "application/pdf";

        /// <summary>BE2.7.1: tải PDF, kiểm Content-Type + %PDF- + font nhúng, đọc lại chữ.</summary>
        private static async Task<PdfText> PdfAsync(HttpResponseMessage response, string because)
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK, because);
            response.Content.Headers.ContentType!.MediaType.Should().Be(PdfType, because);
            var pdf = PdfText.Read(await response.Content.ReadAsByteArrayAsync());
            pdf.PageCount.Should().BeGreaterThan(0, because);
            pdf.FontsEmbedded.Should().BeTrue(because);
            return pdf;
        }

        private async Task<IPrintService> PrintAsync(Func<IPrintService, Task> use)
        {
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPrintService>();
            await use(service);
            return service;
        }

        private async Task<(Guid Supplier, Guid Warehouse, Guid Ingredient, Guid Unit)> MasterAsync(string key)
        {
            Guid supplier = Guid.NewGuid(), warehouse = Guid.NewGuid(), ingredient = Guid.NewGuid(), unit = Guid.NewGuid();
            await _factory.SeedAsync(async db =>
            {
                if (!await db.Company.AnyAsync())
                {
                    db.Company.Add(new Company { Id = Guid.NewGuid(), Code = "DLC-B27", Name = "[DEMO] CÔNG TY THỬ IN B27", Address = "[DEMO] Địa chỉ", Phone = "0900000000" });
                }
                db.Supplier.Add(new Supplier { Id = supplier, Code = "NCC" + key, Name = "[DEMO] NCC " + key, Status = "Active" });
                db.Warehouse.Add(new Warehouse { Id = warehouse, Code = "K" + key, Name = "[DEMO] Kho " + key, Status = "Active" });
                db.UnitMeasure.Add(new UnitMeasure { Id = unit, Code = "KG" + key, Name = "kg" });
                db.Ingredient.Add(new Ingredient { Id = ingredient, Code = "NL" + key, Name = "[DEMO] Thịt gà " + key, StorageType = "CHILLED", Status = "Active" });
            });
            return (supplier, warehouse, ingredient, unit);
        }

        [Fact]
        public async Task Goods_receipt_prints_only_after_qc_and_renders_pdf_and_xlsx()
        {
            string key = Guid.NewGuid().ToString("N")[..6];
            var (supplier, warehouse, ingredient, unit) = await MasterAsync(key);
            Guid gr = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.GoodsReceipt.Add(new GoodsReceipt { Id = gr, ReceiptNumber = "PN-" + key, ReceiptNo = "PN-" + key, SupplierId = supplier, WarehouseId = warehouse, ReceiptDate = new DateTime(2026, 12, 3), Status = "QCPending", Note = "[DEMO] dòng 1" + "\r\n" + "dòng 2" + "\t" + "cột" });
                db.GoodsReceiptLine.Add(new GoodsReceiptLine { Id = Guid.NewGuid(), GoodsReceiptId = gr, IngredientId = ingredient, UomId = unit, UnitId = unit, Quantity = 10, ReceivedQuantity = 10,
                    AcceptedQuantity = 9, RejectedQuantity = 1, UnitCost = 80000, LotNumber = "LOT-" + key, ExpiryDate = new DateTime(2026, 12, 10) });
                return Task.CompletedTask;
            });
            var admin = _factory.CreateAuthorizedClient();
            (await admin.GetResultAsync($"api/GoodsReceipt/Print?id={gr}")).Fail("chưa có kết luận QC");

            await _factory.SeedAsync(async db =>
            {
                db.QcInspection.Add(new QcInspection { Id = Guid.NewGuid(), GoodsReceiptId = gr, InspectionDate = new DateTime(2026, 12, 3), Result = "DAT_MOT_PHAN", InspectionNumber = "QC-" + key });
                (await db.GoodsReceipt.SingleAsync(x => x.Id == gr)).Status = "PartiallyAccepted";
            });
            var word = await admin.GetAsync($"api/GoodsReceipt/Print?id={gr}&format=docx");
            word.StatusCode.Should().Be(HttpStatusCode.OK);
            word.Content.Headers.ContentType!.MediaType.Should().Be(DocxType);
            (await word.Content.ReadAsByteArrayAsync()).Take(4).Should().Equal(ZipMagic);

            // BE2.7.1: không gửi format → PDF (spec 2.5; BusinessRules:Common:PrintDefaultFormat – D-56 k).
            var pdf = await PdfAsync(await admin.GetAsync($"api/GoodsReceipt/Print?id={gr}"), "mặc định PDF");
            foreach (var text in new[] { "PHIẾU NHẬP KHO", "[DEMO] CÔNG TY THỬ IN B27", "Mẫu số 01 - VT", "[DEMO] Thịt gà " + key, "LOT-" + key, "720.000", "Thủ kho", "PN-" + key, "[DEMO] dòng 1" })
            {
                pdf.Has(text).Should().BeTrue($"PDF phiếu nhập phải có \"{text}\"");
            }
            (await PdfAsync(await admin.GetAsync($"api/GoodsReceipt/Print?id={gr}&format=PDF"), "format=PDF")).Has("PHIẾU NHẬP KHO").Should().BeTrue();

            var xlsx = await admin.GetAsync($"api/GoodsReceipt/Print?id={gr}&format=xlsx");
            xlsx.Content.Headers.ContentType!.MediaType.Should().Be(XlsxType);
            using (var wb = new XLWorkbook(new MemoryStream(await xlsx.Content.ReadAsByteArrayAsync())))
            {
                var ws = wb.Worksheets.Single();
                ws.Name.Should().Be("PN-" + key);
                ws.CellsUsed().Select(c => c.GetString()).Should().Contain(["PHIẾU NHẬP KHO", "[DEMO] CÔNG TY THỬ IN B27", "[DEMO] Thịt gà " + key, "720.000", "Thủ kho"]);
            }
            (await admin.GetResultAsync($"api/GoodsReceipt/Print?id={gr}&format=html")).Fail("không hỗ trợ");

            await PrintAsync(async p =>
            {
                var doc = (await p.Build("GoodsReceipt", gr)).Data!;
                doc.FormCode.Should().StartWith("Mẫu số 01 - VT");
                doc.ListInfo.Should().Contain(i => i.Label == "Kết luận QC" && i.Value!.Contains("QC-" + key));
                doc.ListRow.Single().Should().Contain(["LOT-" + key, "10", "9", "1", "80.000", "720.000"]);
            });

            // Quyền In theo màn #/scm-gr (PermissionConvention: Print*).
            Guid viewer = await CommonTestUsers.UserAsync(_factory, ("scm-gr", "V"));
            (await _factory.CreateAuthorizedClient(viewer).GetAsync($"api/GoodsReceipt/Print?id={gr}&format=pdf")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            Guid printer = await CommonTestUsers.UserAsync(_factory, ("scm-gr", "VP"));
            (await _factory.CreateAuthorizedClient(printer).GetAsync($"api/GoodsReceipt/Print?id={gr}")).StatusCode.Should().Be(HttpStatusCode.OK);

            // BOT-1.7d: bot Vận hành có username → phiếu kèm link mở trên bot (QR DOC_); bot đọc phiếu qua Common/GetPrintDocument (quyền Xem màn của loại phiếu, mã ngắn GR).
            await _factory.SeedAsync(async db =>
            {
                if (!await db.BotConfiguration.AnyAsync(b => b.BotCode == "VH"))
                {
                    var channel = Guid.NewGuid();
                    db.IntegrationChannel.Add(new IntegrationChannel { Id = channel, ChannelCode = "TELEGRAM_VH_PRINT", ChannelName = "VH", ProviderCode = "TELEGRAM", EnvironmentCode = "Development", ChannelStatus = "Active", ConfigJson = "{}" });
                    db.BotConfiguration.Add(new BotConfiguration { Id = Guid.NewGuid(), IntegrationChannelId = channel, BotCode = "VH", BotName = "Vận hành", BotUsername = "@delicare_vh_bot", BotStatus = "Active" });
                }
            });
            await PrintAsync(async p =>
            {
                var doc = (await p.Build("GoodsReceipt", gr)).Data!;
                doc.BotLink.Should().Be($"https://t.me/delicare_vh_bot?start=DOC_GR_{gr:N}");
                doc.ListNote.Should().Contain(n => n.Contains(doc.BotLink!), "in kèm link (ảnh QR do FE vẽ)");
            });
            var viewed = (await _factory.CreateAuthorizedClient(viewer).GetResultAsync($"api/Common/GetPrintDocument?type=GR&id={gr}")).Ok();
            viewed.GetProperty("documentNo").GetString().Should().Be("PN-" + key);
            Guid stranger = await CommonTestUsers.UserAsync(_factory, ("scm-issue", "V"));
            (await _factory.CreateAuthorizedClient(stranger).GetAsync($"api/Common/GetPrintDocument?type=GR&id={gr}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

            // FE-E.2 (D-65 – backend vẽ QR, QRCoder): PDF phiếu có ảnh QR của link bot.
            var qrPdf = await _factory.CreateAuthorizedClient(printer).GetAsync($"api/GoodsReceipt/Print?id={gr}&format=pdf");
            qrPdf.StatusCode.Should().Be(HttpStatusCode.OK);
            var qrBytes = await qrPdf.Content.ReadAsByteArrayAsync();
            System.Text.Encoding.Latin1.GetString(qrBytes).Should().MatchRegex(@"/Subtype\s*/Image", "PDF nhúng ảnh QR");

            // Common/GetQrBatch: SVG cho từng nội dung; mục lỗi không làm hỏng cả lô.
            using var qrAdmin = _factory.CreateAuthorizedClient();
            var batch = await LegacyResult.ReadAsync(await qrAdmin.PostAsJsonAsync("/api/Common/GetQrBatch", new { listText = new[] { "https://t.me/delicare_vh_bot?start=CLEAN", "" } }));
            var items = batch.ListData.EnumerateArray().ToList();
            items[0].GetProperty("svg").GetString().Should().Contain("<svg");
            items[1].GetProperty("error").GetString().Should().NotBeNullOrEmpty();
            (await LegacyResult.ReadAsync(await qrAdmin.PostAsJsonAsync("/api/Common/GetQrBatch", new { listText = Array.Empty<string>() }))).Fail("1–200");

            // Common/GetFieldQrList: QR hiện trường mở bot Vận hành theo quyền xem màn.
            await _factory.SeedAsync(db =>
            {
                db.ProductionLine.Add(new ProductionLine { Id = Guid.NewGuid(), Code = "DC-" + key.ToUpperInvariant(), Name = "[DEMO] Dây chuyền " + key, Status = "Active" });
                return Task.CompletedTask;
            });
            var field = (await qrAdmin.GetResultAsync("/api/Common/GetFieldQrList?group=LINE")).ListData.EnumerateArray().ToList();
            var line = field.Single(x => x.GetProperty("code").GetString() == "DC-" + key.ToUpperInvariant());
            line.GetProperty("link").GetString().Should().Be($"https://t.me/delicare_vh_bot?start=LINE_DC-{key.ToUpperInvariant()}");
            line.GetProperty("svg").GetString().Should().Contain("<svg");
            var all = (await qrAdmin.GetResultAsync("/api/Common/GetFieldQrList?withSvg=false")).ListData.EnumerateArray().Select(x => x.GetProperty("group").GetString()).ToHashSet();
            all.Should().Contain(["CHAMCONG", "LINE", "CLEAN"]);
            (await _factory.CreateAuthorizedClient(stranger).GetResultAsync("/api/Common/GetFieldQrList?withSvg=false")).ListData.GetArrayLength().Should().Be(0, "không có quyền xem màn nào của QR hiện trường");
        }

        [Fact]
        public async Task Issue_stock_count_and_reconciliation_follow_print_conditions()
        {
            string key = Guid.NewGuid().ToString("N")[..6];
            var (_, warehouse, ingredient, unit) = await MasterAsync(key);
            Guid issue = Guid.NewGuid(), po = Guid.NewGuid(), dish = Guid.NewGuid(), count = Guid.NewGuid(), customer = Guid.NewGuid(), order = Guid.NewGuid(), rec = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Dish.Add(new Dish { Id = dish, Code = "M" + key, Name = "[DEMO] Gà kho " + key, Status = "Active" });
                db.ProductionOrder.Add(new ProductionOrder { Id = po, ProductionOrderNumber = "LSX-" + key, ProductionPlanId = Guid.NewGuid(), DishId = dish, RecipeId = Guid.NewGuid(), PlannedQty = 100,
                    PlannedStartDate = new DateTime(2026, 12, 4), Status = "Released" });
                db.MaterialIssue.Add(new MaterialIssue { Id = issue, IssueNumber = "PX-" + key, IssueNo = "PX-" + key, ProductionOrderId = po, WarehouseId = warehouse, IssueDate = new DateTime(2026, 12, 4), Status = "Draft" });
                db.MaterialIssueLine.Add(new MaterialIssueLine { Id = Guid.NewGuid(), MaterialIssueId = issue, IngredientId = ingredient, UomId = unit, UnitId = unit, Quantity = 12.5m });
                db.StockCount.Add(new StockCount { Id = count, CountNumber = "KK-" + key, CountNo = "KK-" + key, WarehouseId = warehouse, CountDate = new DateTime(2026, 12, 5), Status = "Counting", CountType = "FULL" });
                db.StockCountLine.Add(new StockCountLine { Id = Guid.NewGuid(), StockCountId = count, IngredientId = ingredient, UnitId = unit, SystemQuantity = 10, PhysicalQuantity = 9, VarianceQuantity = -1, VarianceReason = "[DEMO] hao hụt" });
                db.Customer.Add(new Customer { Id = customer, Code = "KH" + key, Name = "[DEMO] Khách " + key, CIF = key, DisplayName = key, CustomerType = "FACTORY", Status = "Active" });
                db.Order.Add(new Order { Id = order, OrderNumber = "DH-" + key, OrderNo = "DH-" + key, CustomerId = customer, OrderDate = new DateTime(2026, 12, 1), MealDate = new DateTime(2026, 12, 4),
                    DeliveryDate = new DateTime(2026, 12, 4), Status = "Delivered", TotalQuantity = 50, TotalAmount = 2000000 });
                db.OrderReconciliation.Add(new OrderReconciliation { Id = rec, OrderId = order, Status = "Open", ReconciliationDate = new DateTime(2026, 12, 4), OrderedQuantity = 50, OrderedQty = 50,
                    DeliveredQuantity = 50, AcceptedQuantity = 48, ProducedQty = 52, PackedQty = 50, FinalAmount = 1920000 });
                return Task.CompletedTask;
            });
            var admin = _factory.CreateAuthorizedClient();
            (await admin.GetResultAsync($"api/MaterialIssue/Print?id={issue}")).Fail("chưa ở trạng thái Đã xuất");
            (await admin.GetResultAsync($"api/OrderReconciliation/Print?id={rec}")).Fail("còn Mở");
            await _factory.SeedAsync(async db =>
            {
                (await db.MaterialIssue.SingleAsync(x => x.Id == issue)).Status = "Issued";
                (await db.OrderReconciliation.SingleAsync(x => x.Id == rec)).Status = "Closed";
            });
            foreach (var url in new[] { $"api/MaterialIssue/Print?id={issue}&format=xlsx", $"api/StockCount/Print?id={count}&format=xlsx", $"api/OrderReconciliation/Print?id={rec}&format=xlsx" })
            {
                var r = await admin.GetAsync(url);
                r.Content.Headers.ContentType!.MediaType.Should().Be(XlsxType, url);
                (await r.Content.ReadAsByteArrayAsync()).Take(4).Should().Equal(ZipMagic);
            }
            // BE2.7.1: PDF (mặc định) của 3 phiếu – tiêu đề, số chứng từ, nội dung dòng tiếng Việt; kiểm kê chưa ghi sổ có BẢN NHÁP.
            var issuePdf = await PdfAsync(await admin.GetAsync($"api/MaterialIssue/Print?id={issue}"), "phiếu xuất");
            issuePdf.Has("PHIẾU XUẤT KHO").Should().BeTrue();
            issuePdf.Has("LSX-" + key + " – [DEMO] Gà kho " + key).Should().BeTrue();
            issuePdf.Has("12,5").Should().BeTrue();
            var countPdf = await PdfAsync(await admin.GetAsync($"api/StockCount/Print?id={count}&format=pdf"), "kiểm kê");
            countPdf.Has("BIÊN BẢN KIỂM KÊ KHO").Should().BeTrue();
            countPdf.Has("BẢN NHÁP – CHƯA CÓ GIÁ TRỊ PHÁP LÝ").Should().BeTrue("chưa ghi sổ → BẢN NHÁP");
            countPdf.Has("[DEMO] hao hụt").Should().BeTrue();
            var recPdf = await PdfAsync(await admin.GetAsync($"api/OrderReconciliation/Print?id={rec}"), "đối soát");
            recPdf.Has("BIÊN BẢN ĐỐI SOÁT SUẤT ĂN").Should().BeTrue();
            recPdf.Has("1.920.000").Should().BeTrue();
            recPdf.Has("% chênh lệch").Should().BeTrue();
            await PrintAsync(async p =>
            {
                var issueDoc = (await p.Build("MATERIAL_ISSUE", issue)).Data!;
                issueDoc.ListInfo.Should().Contain(i => i.Label == "Lệnh sản xuất" && i.Value == "LSX-" + key + " – [DEMO] Gà kho " + key);
                issueDoc.ListRow.Single().Should().Contain("12,5");
                var countDoc = (await p.Build("STOCK_COUNT", count)).Data!;
                countDoc.IsDraft.Should().BeTrue("chưa ghi sổ (Posted) → BẢN NHÁP");
                countDoc.ListRow.Single().Should().Contain(["10", "9", "-1", "[DEMO] hao hụt"]);
                var recDoc = (await p.Build("RECONCILIATION", rec)).Data!;
                recDoc.ListHeader.Should().Equal("Đặt", "Sản xuất", "Đóng gói", "Giao", "Nhận (POD)", "Chênh lệch", "% chênh lệch", "Giá trị chốt");
                recDoc.ListRow.Single().Should().Equal("50", "52", "50", "50", "48", "-2", "", "1.920.000");
                (await p.Build("INVOICE", rec)).ResponseCode.Should().Be(400);
            });
            await _factory.SeedAsync(async db => (await db.StockCount.SingleAsync(x => x.Id == count)).Status = "Posted");
            await PrintAsync(async p => (await p.Build("STOCK_COUNT", count)).Data!.IsDraft.Should().BeFalse());
            (await PdfAsync(await admin.GetAsync($"api/StockCount/Print?id={count}"), "kiểm kê đã ghi sổ")).Has("BẢN NHÁP").Should().BeFalse("đã ghi sổ → không còn BẢN NHÁP");
        }

        private async Task<(Guid Route, Guid[] DeliveryOrders, Guid Pod)> RouteAsync(string key)
        {
            Guid customer = Guid.NewGuid(), location = Guid.NewGuid(), order = Guid.NewGuid(), vehicle = Guid.NewGuid(), driver = Guid.NewGuid(), route = Guid.NewGuid(), pod = Guid.NewGuid();
            Guid[] dos = [Guid.NewGuid(), Guid.NewGuid()];
            var day = new DateTime(2026, 12, 8);
            await _factory.SeedAsync(db =>
            {
                db.Customer.Add(new Customer { Id = customer, Code = "KH" + key, Name = "[DEMO] Khách giao " + key, CIF = key, DisplayName = key, CustomerType = "FACTORY", Status = "Active" });
                db.CustomerDeliveryLocation.Add(new CustomerDeliveryLocation { Id = location, CustomerId = customer, Code = "D" + key, Name = "[DEMO] Cổng A " + key, Address = "[DEMO] KCN",
                    ContactName = "[DEMO] Chị Lan", ContactPhone = "0900000001", DeliveryWindowFrom = new TimeSpan(10, 30, 0), DeliveryWindowTo = new TimeSpan(11, 15, 0), Status = "Active" });
                db.Order.Add(new Order { Id = order, OrderNumber = "DH-" + key, OrderNo = "DH-" + key, CustomerId = customer, OrderDate = day.AddDays(-2), MealDate = day, DeliveryDate = day, Status = "InProduction" });
                db.OrderLine.AddRange(
                    new OrderLine { Id = Guid.NewGuid(), OrderId = order, MealType = "TRUA", Quantity = 80 },
                    new OrderLine { Id = Guid.NewGuid(), OrderId = order, MealType = "TOI", Quantity = 20 });
                db.Vehicle.Add(new Vehicle { Id = vehicle, Code = "XE" + key, LicensePlate = "29C-" + key, Capacity = 200, Status = "Active" });
                db.Employee.Add(new Employee { Id = driver, Code = "TX" + key, FullName = "[DEMO] Tài xế " + key, Status = "Active" });
                db.DeliveryRoute.Add(new DeliveryRoute { Id = route, RouteNo = "CX-" + key, Code = "CX-" + key, DeliveryDate = day, VehicleId = vehicle, DriverId = driver, Status = "InTransit" });
                for (int i = 0; i < 2; i++)
                {
                    db.DeliveryOrder.Add(new DeliveryOrder { Id = dos[i], DeliveryNumber = $"PG-{key}-{i}", DeliveryNo = $"PG-{key}-{i}", OrderId = order, CustomerId = customer, CustomerDeliveryLocationId = location,
                        DeliveryDate = day, DeliveryRouteId = route, PlannedDeliveryTime = day.AddHours(10.5 + i), Status = "Delivered" });
                    db.DeliveryStop.Add(new DeliveryStop { Id = Guid.NewGuid(), DeliveryRouteId = route, DeliveryOrderId = dos[i], CustomerId = customer, SequenceNo = 2 - i, PlannedMealQty = 50 });
                }
                db.ProofOfDelivery.Add(new ProofOfDelivery { Id = pod, DeliveryOrderId = dos[0], DeliveredQuantity = 50, AcceptedQuantity = 48, RejectedQuantity = 2, ReceiverName = "[DEMO] Anh Nam",
                    ReceiverPhone = "0900000002", ReceivedAt = day.AddHours(10.6), ConfirmedAt = day.AddHours(10.6), SignatureUrl = "api/DocumentFile/Download?id=" + Guid.Empty });
                return Task.CompletedTask;
            });
            return (route, dos, pod);
        }

        [Fact]
        public async Task Delivery_note_pod_and_batch_print()
        {
            string key = Guid.NewGuid().ToString("N")[..6];
            await MasterAsync(key);
            var (route, dos, pod) = await RouteAsync(key);
            var admin = _factory.CreateAuthorizedClient();
            foreach (var url in new[] { $"api/DeliveryOrder/Print?id={dos[0]}&format=xlsx", $"api/ProofOfDelivery/Print?id={pod}&format=xlsx" })
            {
                var r = await admin.GetAsync(url);
                r.Content.Headers.ContentType!.MediaType.Should().Be(XlsxType, url);
            }
            // BE2.7.1: PDF (mặc định) phiếu giao + POD.
            var notePdf = await PdfAsync(await admin.GetAsync($"api/DeliveryOrder/Print?id={dos[0]}"), "phiếu giao");
            notePdf.Has("PHIẾU GIAO HÀNG").Should().BeTrue();
            notePdf.Has("10:30 – 11:15").Should().BeTrue();
            notePdf.Has("[DEMO] Tài xế " + key).Should().BeTrue();
            notePdf.Has("Trưa").Should().BeTrue();
            var podPdf = await PdfAsync(await admin.GetAsync($"api/ProofOfDelivery/Print?id={pod}"), "POD");
            podPdf.Has("BIÊN BẢN GIAO NHẬN SUẤT ĂN").Should().BeTrue();
            podPdf.Has("[DEMO] Anh Nam – 0900000002").Should().BeTrue();
            await PrintAsync(async p =>
            {
                var note = (await p.Build("DELIVERY_ORDER", dos[0])).Data!;
                note.ListInfo.Should().Contain(i => i.Label == "Khung giờ nhận" && i.Value == "10:30 – 11:15");
                note.ListInfo.Should().Contain(i => i.Label == "Tài xế" && i.Value == "[DEMO] Tài xế " + key);
                note.ListRow.Select(r => r[1]).Should().Equal("Tối", "Trưa");
                note.ListFooter.Last().Should().Contain("50", "số suất của đơn giao này (điểm dừng)");
                var podDoc = (await p.Build("ProofOfDelivery", pod)).Data!;
                podDoc.ListRow.Single().Should().Contain(["50", "48", "2"]);
                podDoc.ListInfo.Should().Contain(i => i.Label == "Người nhận" && i.Value == "[DEMO] Anh Nam – 0900000002");
            });

            // In hàng loạt: POD + mọi phiếu giao của chuyến (theo thứ tự điểm dừng) → 1 tệp Excel 3 sheet.
            var batch = await admin.PostAsJsonAsync("api/Common/PrintBatch", new { items = new[] { new { type = "POD", id = pod } }, deliveryRouteId = route, format = "xlsx" });
            batch.StatusCode.Should().Be(HttpStatusCode.OK);
            using (var wb = new XLWorkbook(new MemoryStream(await batch.Content.ReadAsByteArrayAsync())))
            {
                wb.Worksheets.Select(w => w.Name).Should().Equal($"PG-{key}-0", $"PG-{key}-1", $"PG-{key}-0-2");
            }
            var docx = await admin.PostAsJsonAsync("api/Common/PrintBatch", new { deliveryRouteId = route, format = "docx" });
            docx.Content.Headers.ContentType!.MediaType.Should().Be(DocxType);
            // BE2.7.1: cùng lô in ra 1 PDF (mặc định) – mỗi phiếu bắt đầu trang mới, số trang đếm lại theo phiếu ("Trang 1 / …" đúng 3 lần), đúng thứ tự.
            var batchPdf = await PdfAsync(await admin.PostAsJsonAsync("api/Common/PrintBatch", new { items = new[] { new { type = "POD", id = pod } }, deliveryRouteId = route }), "in hàng loạt PDF");
            batchPdf.PageCount.Should().BeGreaterThanOrEqualTo(3, "≥ số phiếu");
            var starts = batchPdf.Pages.Where(p => p.Contains(PdfText.Squash("Trang 1 /"))).ToList();
            starts.Should().HaveCount(3);
            // Thứ tự như Excel: phiếu chọn (POD) → phiếu giao của chuyến theo điểm dừng (PG-1 dừng 1, PG-0 dừng 2).
            starts[0].Should().Contain(PdfText.Squash("BIÊN BẢN GIAO NHẬN SUẤT ĂN")).And.Contain(PdfText.Squash("[DEMO] Anh Nam"));
            starts[1].Should().Contain(PdfText.Squash("PHIẾU GIAO HÀNG")).And.Contain($"PG-{key}-1").And.NotContain($"PG-{key}-0");
            starts[2].Should().Contain(PdfText.Squash("PHIẾU GIAO HÀNG")).And.Contain($"PG-{key}-0").And.NotContain($"PG-{key}-1");
            (await PdfAsync(await admin.PostAsJsonAsync("api/Common/PrintBatch", new { deliveryRouteId = route, format = "pdf" }), "format=pdf")).PageCount.Should().BeGreaterThanOrEqualTo(2);
            (await LegacyResult.ReadAsync(await admin.PostAsJsonAsync("api/Common/PrintBatch", new { deliveryRouteId = route, format = "html" }))).Fail("không hỗ trợ");

            var tooMany = Enumerable.Range(0, 101).Select(_ => new { type = "DELIVERY_ORDER", id = Guid.NewGuid() }).ToArray();
            (await LegacyResult.ReadAsync(await admin.PostAsJsonAsync("api/Common/PrintBatch", new { items = tooMany }))).Fail("tối đa 100");
            (await LegacyResult.ReadAsync(await admin.PostAsJsonAsync("api/Common/PrintBatch", new { items = new[] { new { type = "User", id = pod } } }))).Fail("không hợp lệ");
            (await LegacyResult.ReadAsync(await admin.PostAsJsonAsync("api/Common/PrintBatch", new { items = Array.Empty<object>() }))).Fail("Chưa chọn");
            (await LegacyResult.ReadAsync(await admin.PostAsJsonAsync("api/Common/PrintBatch", new { items = new[] { new { type = "POD", id = Guid.NewGuid() } } }))).Fail("Không tìm thấy");

            // Quyền In theo từng loại phiếu: có In phiếu giao nhưng không In POD → 403.
            Guid dispatcher = await CommonTestUsers.UserAsync(_factory, ("log-delivery-order", "VP"), ("log-pod", "V"));
            var client = _factory.CreateAuthorizedClient(dispatcher);
            (await client.PostAsJsonAsync("api/Common/PrintBatch", new { deliveryRouteId = route })).StatusCode.Should().Be(HttpStatusCode.OK);
            var denied = await client.PostAsJsonAsync("api/Common/PrintBatch", new { items = new[] { new { type = "POD", id = pod } }, deliveryRouteId = route });
            denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await denied.Content.ReadAsStringAsync()).Should().Contain("#/log-pod");
            (await client.GetAsync($"api/ProofOfDelivery/Print?id={pod}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }
}
