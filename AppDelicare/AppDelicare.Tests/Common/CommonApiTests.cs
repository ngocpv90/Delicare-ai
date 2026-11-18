using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Services.Implement;
using AppDelicare.Tests.Integration;
using AppDelicare.WebApi.Security;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Common
{
    /// <summary>
    /// BE2.7 – API dùng chung (spec 2.1 / 2.2): danh sách trắng (không nhận tên bảng tùy ý), thống kê theo trạng thái (Catalog 2), xuất Excel / CSV (8) kèm
    /// nhãn trạng thái / khóa ngoại + chống chèn công thức, lịch sử gộp AuditChange (+ dòng con) + ApprovalDecision + ComplaintHistory có che mật khẩu / GPS (15),
    /// quyền theo màn hình của danh sách (Xem / Xuất → 403).
    /// </summary>
    public class CommonApiTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public CommonApiTests(InMemoryApiFactory factory) => _factory = factory;

        [Fact]
        public void Registry_is_a_whitelist_of_real_columns_and_screens()
        {
            CommonRegistry.All.Should().NotBeEmpty();
            CommonRegistry.All.Select(r => r.Key).Should().OnlyHaveUniqueItems();
            foreach (var r in CommonRegistry.All)
            {
                ScreenMap.ByClass.Should().ContainKey(r.Key);
                ScreenMap.ByClass[r.Key].Should().Be(r.ScreenId, $"màn của {r.Key} phải khớp ScreenMap");
                var props = r.EntityType.GetProperties(BindingFlags.Public | BindingFlags.Instance).ToDictionary(p => p.Name);
                foreach (var c in r.SearchColumns)
                {
                    props.Should().ContainKey(c, $"{r.Key}.{c}");
                    props[c].PropertyType.Should().Be(typeof(string), $"{r.Key}.{c} là cột tìm chữ");
                }
                foreach (var c in r.ExportColumns)
                {
                    props.Should().ContainKey(c.Property, $"{r.Key}.{c.Property}");
                    CommonService.IsSensitive(c.Property).Should().BeFalse($"{r.Key}.{c.Property} không được xuất");
                    var type = Nullable.GetUnderlyingType(props[c.Property].PropertyType) ?? props[c.Property].PropertyType;
                    if (type == typeof(Guid))
                    {
                        CommonRegistry.Lookups.Should().ContainKey(c.Property, $"cột khóa ngoại {r.Key}.{c.Property} phải có nhãn");
                    }
                }
                if (r.StatusColumn != null)
                {
                    props[r.StatusColumn].PropertyType.Should().Be(typeof(string));
                }
                if (r.DateColumn != null)
                {
                    (Nullable.GetUnderlyingType(props[r.DateColumn].PropertyType) ?? props[r.DateColumn].PropertyType).Should().Be(typeof(DateTime));
                }
                foreach (var child in r.Children)
                {
                    child.EntityType.GetProperty(child.ForeignKey).Should().NotBeNull($"{child.EntityType.Name}.{child.ForeignKey}");
                }
            }
            // Bảng nhạy cảm / hệ thống không bao giờ nằm trong danh sách trắng.
            foreach (var blocked in new[] { "User", "UserRole", "UserClaim", "AuditChange", "IntegrationChannel", "BotConfiguration", "AttendanceEvent", "DocumentFile", "Order;DROP TABLE x", "../Order" })
            {
                CommonRegistry.Find(blocked).Should().BeNull(blocked);
            }
            // Màn chịu quyền In của từng loại phiếu khớp ScreenMap.
            var prints = new Dictionary<string, string> { ["GOODS_RECEIPT"] = "GoodsReceipt", ["MATERIAL_ISSUE"] = "MaterialIssue", ["STOCK_COUNT"] = "StockCount",
                ["DELIVERY_ORDER"] = "DeliveryOrder", ["POD"] = "ProofOfDelivery", ["RECONCILIATION"] = "OrderReconciliation" };
            using var scope = _factory.Services.CreateScope();
            var print = scope.ServiceProvider.GetRequiredService<AppDelicare.Services.Interface.IPrintService>();
            foreach (var (type, cls) in prints)
            {
                print.TypeScreens[type].Should().Be(ScreenMap.ByClass[cls]);
            }
            ScreenMap.ByClass["DocumentFile"].Should().Be(DocumentFileService.DocumentScreen);
        }

        private async Task<(Guid Customer, string Key, Guid[] Orders)> OrdersAsync()
        {
            string key = "B27" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
            Guid customer = Guid.NewGuid();
            var orders = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
            string[] statuses = ["Draft", "Draft", "Confirmed", "Cancelled", "Draft"];
            await _factory.SeedAsync(db =>
            {
                db.Customer.Add(new Customer { Id = customer, Code = key, Name = "[DEMO] Khách " + key, CIF = key, DisplayName = key, CustomerType = "FACTORY", Status = "Active" });
                for (int i = 0; i < orders.Length; i++)
                {
                    db.Order.Add(new Order
                    {
                        Id = orders[i], OrderNumber = $"PH-{key}-{i}", OrderNo = $"PH-{key}-{i}", CustomerId = customer, OrderDate = new DateTime(2026, 12, 1),
                        MealDate = i == 4 ? new DateTime(2026, 11, 1) : new DateTime(2026, 12, 2 + i), DeliveryDate = new DateTime(2026, 12, 2), Status = statuses[i],
                        TotalQuantity = 10 * (i + 1), Note = i == 1 ? "=HYPERLINK(\"http://x\",\"bấm\")" : "[DEMO]",
                    });
                }
                db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = orders[0], MealType = "TRUA", Quantity = 10 });
                return Task.CompletedTask;
            });
            return (customer, key, orders);
        }

        [Fact]
        public async Task Status_summary_counts_by_status_with_catalog_codes()
        {
            var (_, key, _) = await OrdersAsync();
            var client = _factory.CreateAuthorizedClient();
            var data = (await client.GetResultAsync($"api/Common/GetStatusSummary?Resource=Order&SearchText={key}")).Ok();
            data.GetProperty("total").GetInt32().Should().Be(5);
            var by = data.GetProperty("listByStatus").EnumerateArray().ToDictionary(x => x.GetProperty("status").GetString()!, x => x);
            by["Draft"].GetProperty("count").GetInt32().Should().Be(3);
            by["Draft"].GetProperty("statusName").GetString().Should().Be("Nháp");
            by["Confirmed"].GetProperty("count").GetInt32().Should().Be(1);
            by["Locked"].GetProperty("count").GetInt32().Should().Be(0, "đủ mọi mã của STATUS_CATALOG để vẽ chip lọc nhanh");
            // Cùng bộ lọc ngày (theo MealDate) với danh sách.
            var dated = (await client.GetResultAsync($"api/Common/GetStatusSummary?Resource=order&SearchText={key}&FromDate=2026-12-01&ToDate=2026-12-31")).Ok();
            dated.GetProperty("total").GetInt32().Should().Be(4);

            // Không có trong danh sách trắng / không có cột trạng thái → 400; không bao giờ đọc bảng tùy ý.
            (await client.GetResultAsync("api/Common/GetStatusSummary?Resource=User")).Fail("không hỗ trợ");
            (await client.GetResultAsync("api/Common/GetStatusSummary?Resource=" + Uri.EscapeDataString("Order;DROP TABLE [Order]"))).Fail("không hỗ trợ");
            (await client.GetResultAsync("api/Common/GetStatusSummary?Resource=ProofOfDelivery")).Fail("không có cột trạng thái");
        }

        [Fact]
        public async Task Export_list_xlsx_and_csv_with_labels_and_formula_guard()
        {
            var (_, key, _) = await OrdersAsync();
            var client = _factory.CreateAuthorizedClient();
            var response = await client.GetAsync($"api/Common/ExportList?Resource=Order&SearchText={key}&FromDate=2026-12-01");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            using (var wb = new XLWorkbook(new MemoryStream(await response.Content.ReadAsByteArrayAsync())))
            {
                var ws = wb.Worksheets.First();
                ws.Cell(1, 1).GetString().Should().Be("ĐƠN ĐẶT SUẤT ĂN");
                var headers = ws.Row(4).CellsUsed().Select(c => c.GetString()).ToList();
                headers.Should().StartWith(["STT", "Số đơn"]).And.Contain(["Khách hàng", "Trạng thái", "Ghi chú"]);
                int customerCol = headers.IndexOf("Khách hàng") + 1, statusCol = headers.IndexOf("Trạng thái") + 1, noteCol = headers.IndexOf("Ghi chú") + 1;
                var rows = Enumerable.Range(5, 4).Select(r => ws.Row(r)).ToList();
                rows.Should().OnlyContain(r => r.Cell(customerCol).GetString() == "[DEMO] Khách " + key, "khóa ngoại → tên khách");
                rows.Select(r => r.Cell(statusCol).GetString()).Should().Contain(["Nháp", "Đã xác nhận", "Hủy"]);
                var formulaCell = rows.Select(r => r.Cell(noteCol)).Single(c => c.GetString().StartsWith('='));
                formulaCell.HasFormula.Should().BeFalse("chuỗi do người dùng nhập không thành công thức");
                ws.Cell(9, 1).GetString().Should().BeEmpty("chỉ 4 đơn trong khoảng ngày");
            }
            var only = await client.GetAsync($"api/Common/ExportList?Resource=Order&SearchText={key}&Status=Draft&Format=csv");
            only.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
            byte[] csv = await only.Content.ReadAsByteArrayAsync();
            csv.Take(3).Should().Equal(Encoding.UTF8.GetPreamble());
            string text = Encoding.UTF8.GetString(csv[3..]);
            text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().HaveCount(4, "tiêu đề + 3 đơn Nháp");
            text.Should().Contain("\"'=HYPERLINK(").And.Contain("Nháp");

            (await client.GetResultAsync("api/Common/ExportList?Resource=Order&Format=pdf")).Fail("không hỗ trợ");
            (await client.GetResultAsync("api/Common/ExportList?Resource=UserRole")).Fail("không hỗ trợ");
        }

        [Fact]
        public async Task Common_api_requires_view_or_export_on_the_list_screen()
        {
            var (customer, key, orders) = await OrdersAsync();
            Guid viewer = await CommonTestUsers.UserAsync(_factory, ("crm-order", "V"));
            var client = _factory.CreateAuthorizedClient(viewer);
            (await client.GetResultAsync($"api/Common/GetStatusSummary?Resource=Order&SearchText={key}")).Ok().GetProperty("total").GetInt32().Should().Be(5);
            var resources = (await client.GetResultAsync("api/Common/GetResources")).ListData.EnumerateArray().Select(x => x.GetProperty("resource").GetString()).ToList();
            resources.Should().Equal("Order");

            var export = await client.GetAsync("api/Common/ExportList?Resource=Order");
            export.StatusCode.Should().Be(HttpStatusCode.Forbidden, "Xem nhưng không có quyền Xuất");
            (await export.Content.ReadAsStringAsync()).Should().Contain("Xuất").And.Contain("#/crm-order");
            (await client.GetAsync($"api/Common/GetHistory?resource=Customer&id={customer}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await client.GetAsync("api/Common/GetStatusSummary?Resource=PurchaseOrder")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await client.GetResultAsync($"api/Common/GetHistory?resource=Order&id={orders[0]}")).Code.Should().Be(200);

            Guid exporter = await CommonTestUsers.UserAsync(_factory, ("crm-order", "VX"));
            (await _factory.CreateAuthorizedClient(exporter).GetAsync($"api/Common/ExportList?Resource=Order&SearchText={key}")).StatusCode.Should().Be(HttpStatusCode.OK);
            Guid nobody = await CommonTestUsers.UserAsync(_factory);
            (await _factory.CreateAuthorizedClient(nobody).GetAsync("api/Common/GetStatusSummary?Resource=Order")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await _factory.CreateClient().GetAsync("api/Common/GetResources")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task History_merges_audit_lines_approvals_complaints_and_masks_secrets()
        {
            var (customer, _, orders) = await OrdersAsync();
            Guid order = orders[0], line = Guid.NewGuid(), approval = Guid.NewGuid(), step = Guid.NewGuid(), complaint = Guid.NewGuid();
            DateTime t0 = new(2026, 12, 1, 8, 0, 0);
            string header = $"- Actor: USER:{InMemoryApiFactory.UserId} | CorrelationId: x <br />\r\n";
            await _factory.SeedAsync(db =>
            {
                db.OrderLine.Add(new OrderLine { Id = line, OrderId = order, MealType = "TOI", Quantity = 5 });
                db.AuditChange.AddRange(
                    new AuditChange { Id = Guid.NewGuid(), IdChange = order, EntityName = "Order", Action = "Added", CreatedDate = t0, CreatedBy = InMemoryApiFactory.UserId,
                        Changes = header + "- OrderNumber: 'PH-1' <br />\r\n- CreatedDate: '01/12/2026' <br />\r\n- Status: 'Draft' <br />\r\n" },
                    // Dòng cũ (trước khi DbContext che) còn giá trị mật khẩu / tọa độ – khi đọc vẫn phải che.
                    new AuditChange { Id = Guid.NewGuid(), IdChange = order, EntityName = "Order", Action = "Modified", CreatedDate = t0.AddHours(1), CreatedBy = InMemoryApiFactory.UserId,
                        Changes = header + "- Status: From 'Draft' to 'Confirmed' <br />\r\n- PassWord: From 'mk-cu-qwz' to 'xyz' <br />\r\n- Latitude: From '10.1' to '10.2' <br />\r\n- BotToken: 'tk-123' <br />\r\n" },
                    new AuditChange { Id = Guid.NewGuid(), IdChange = line, EntityName = "OrderLine", Action = "Modified", CreatedDate = t0.AddHours(2), CreatedBy = InMemoryApiFactory.UserId,
                        Changes = header + "- Quantity: From '5' to '6' <br />\r\n" },
                    new AuditChange { Id = Guid.NewGuid(), IdChange = order, EntityName = "Order", Action = "Modified", CreatedDate = t0.AddHours(5), CreatedBy = InMemoryApiFactory.UserId,
                        Changes = header + "- IsDeleted: From 'False' to 'True' <br />\r\n- DeletedDate: From '' to '01/12/2026' <br />\r\n" },
                    // Bản ghi khác cùng Id nhưng khác bảng → không lẫn.
                    new AuditChange { Id = Guid.NewGuid(), IdChange = order, EntityName = "Customer", Action = "Added", CreatedDate = t0, CreatedBy = InMemoryApiFactory.UserId, Changes = header });
                db.ApprovalRequest.Add(new ApprovalRequest { Id = approval, RequestNo = "AR-B27", BusinessType = "ORDER_DEVIATION", BusinessId = order, RequestedBy = InMemoryApiFactory.UserId,
                    RequestedAt = t0.AddHours(3), RequestStatus = "Approved", CompletedAt = t0.AddHours(4) });
                db.ApprovalStep.Add(new ApprovalStep { Id = step, ApprovalRequestId = approval, StepNo = 1, DecisionStatus = "Approved" });
                db.ApprovalDecision.Add(new ApprovalDecision { Id = Guid.NewGuid(), ApprovalRequestId = approval, ApprovalStepId = step, DecisionCode = "APPROVED",
                    DecidedBy = InMemoryApiFactory.UserId, DecidedAt = t0.AddHours(4), Comment = "[DEMO] đồng ý" });
                db.CustomerComplaint.Add(new CustomerComplaint { Id = complaint, ComplaintNo = "PA-B27", CustomerId = customer, ReceivedAt = t0, ComplaintStatus = "InProgress", Description = "[DEMO]" });
                db.ComplaintHistory.Add(new ComplaintHistory { Id = Guid.NewGuid(), ComplaintId = complaint, EventType = "ASSIGN", OldStatus = "New", NewStatus = "InProgress", EventAt = t0.AddMinutes(30), ActorId = InMemoryApiFactory.UserId });
                return Task.CompletedTask;
            });
            var client = _factory.CreateAuthorizedClient();
            var result = await client.GetResultAsync($"api/Common/GetHistory?resource=Order&id={order}");
            var items = result.ListData.EnumerateArray().ToList();
            items.Select(i => i.GetProperty("actionName").GetString()).Should().Equal(
                "Xóa", "Duyệt (cấp 1)", "Gửi duyệt", "Cập nhật", "Cập nhật", "Tạo mới");
            items.Select(i => i.GetProperty("at").GetDateTime()).Should().BeInDescendingOrder();
            items[3].GetProperty("entityName").GetString().Should().Be("OrderLine", "gộp cả thay đổi của dòng đơn");
            var modified = items[4].GetProperty("listChange").EnumerateArray().ToDictionary(c => c.GetProperty("field").GetString()!);
            modified["Status"].GetProperty("from").GetString().Should().Be("Draft");
            modified["Status"].GetProperty("to").GetString().Should().Be("Confirmed");
            foreach (var secret in new[] { "PassWord", "Latitude", "BotToken" })
            {
                modified[secret].GetProperty("masked").GetBoolean().Should().BeTrue(secret);
                modified[secret].GetProperty("to").GetString().Should().Be("(đã ẩn)");
            }
            string raw = JsonSerializer.Serialize(result.ListData);
            // Không dùng chuỗi hex (vd. "abc") làm bí mật mẫu: Id ngẫu nhiên có thể chứa → test lúc đỏ lúc xanh.
            raw.Should().NotContain("mk-cu-qwz").And.NotContain("xyz").And.NotContain("10.2").And.NotContain("tk-123").And.NotContain("CorrelationId");
            items[5].GetProperty("listChange").EnumerateArray().Select(c => c.GetProperty("field").GetString()).Should().NotContain("CreatedDate");
            items[1].GetProperty("comment").GetString().Should().Be("[DEMO] đồng ý");
            items[1].GetProperty("byName").GetString().Should().Be("test-admin");

            var complaintHistory = (await client.GetResultAsync($"api/Common/GetHistory?resource=CustomerComplaint&id={complaint}")).ListData.EnumerateArray().ToList();
            complaintHistory.Should().ContainSingle(i => i.GetProperty("source").GetString() == "COMPLAINT")
                .Which.GetProperty("listChange")[0].GetProperty("to").GetString().Should().NotBeNullOrEmpty();
            (await client.GetResultAsync($"api/Common/GetHistory?resource=Order&id={Guid.NewGuid()}")).Fail("Không tìm thấy");
            (await client.GetResultAsync($"api/Common/GetHistory?resource=User&id={InMemoryApiFactory.UserId}")).Fail("không hỗ trợ");
        }
    }
}
