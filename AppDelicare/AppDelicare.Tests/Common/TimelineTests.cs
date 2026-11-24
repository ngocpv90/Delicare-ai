using System.Net;
using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;

namespace AppDelicare.Tests.Common
{
    /// <summary>UI-6.14 (D-95): Common/GetTimeline – gộp nhật ký bản ghi + chứng từ liên quan, phân loại, lọc nhóm / người, tải dần, quyền.</summary>
    public class TimelineTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public TimelineTests(InMemoryApiFactory factory) => _factory = factory;

        private static string Header => $"- Actor: USER:{InMemoryApiFactory.UserId} | CorrelationId: x <br />\r\n";

        private async Task<(Guid Customer, Guid Order, Guid Contract)> SeedAsync(int extraEdits = 0)
        {
            string key = "TL" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
            Guid customer = Guid.NewGuid(), order = Guid.NewGuid(), contract = Guid.NewGuid(), approval = Guid.NewGuid(), step = Guid.NewGuid();
            DateTime t0 = new(2026, 11, 20, 8, 0, 0);
            await _factory.SeedAsync(db =>
            {
                db.Customer.Add(new Customer { Id = customer, Code = key, Name = "[DEMO] Khách " + key, CIF = key, DisplayName = key, CustomerType = "FACTORY", Status = "Active" });
                db.Order.Add(new Order { Id = order, OrderNumber = "DH-" + key, OrderNo = "DH-" + key, CustomerId = customer, OrderDate = t0, MealDate = t0.AddDays(1), DeliveryDate = t0.AddDays(1), Status = "Confirmed", TotalQuantity = 10 });
                db.Contract.Add(new Contract { Id = contract, ContractNo = "HD-" + key, CustomerId = customer, Status = "Active", StartDate = t0, EndDate = t0.AddYears(1) });
                db.AuditChange.AddRange(
                    new AuditChange { Id = Guid.NewGuid(), IdChange = customer, EntityName = "Customer", Action = "Added", CreatedDate = t0, CreatedBy = InMemoryApiFactory.UserId, Changes = Header + "- Name: 'A' <br />\r\n" },
                    new AuditChange { Id = Guid.NewGuid(), IdChange = customer, EntityName = "Customer", Action = "Modified", CreatedDate = t0.AddHours(1), CreatedBy = InMemoryApiFactory.UserId, Changes = Header + "- Phone: From '1' to '2' <br />\r\n" },
                    new AuditChange { Id = Guid.NewGuid(), IdChange = order, EntityName = "Order", Action = "Modified", CreatedDate = t0.AddHours(2), CreatedBy = InMemoryApiFactory.UserId, Changes = Header + "- Status: From 'Draft' to 'Confirmed' <br />\r\n" },
                    new AuditChange { Id = Guid.NewGuid(), IdChange = contract, EntityName = "Contract", Action = "Added", CreatedDate = t0.AddHours(3), CreatedBy = null, Changes = Header + "- ContractNo: 'HD' <br />\r\n" });
                for (int i = 0; i < extraEdits; i++)
                {
                    db.AuditChange.Add(new AuditChange { Id = Guid.NewGuid(), IdChange = customer, EntityName = "Customer", Action = "Modified", CreatedDate = t0.AddMinutes(-i - 1), CreatedBy = InMemoryApiFactory.UserId, Changes = Header + $"- Note: From '{i}' to '{i + 1}' <br />\r\n" });
                }
                db.ApprovalRequest.Add(new ApprovalRequest { Id = approval, RequestNo = "AR-" + key, BusinessType = "CONTRACT", BusinessId = contract, RequestedBy = InMemoryApiFactory.UserId, RequestedAt = t0.AddHours(4), RequestStatus = "Approved", CompletedAt = t0.AddHours(5) });
                db.ApprovalStep.Add(new ApprovalStep { Id = step, ApprovalRequestId = approval, StepNo = 1, DecisionStatus = "Approved" });
                db.ApprovalDecision.Add(new ApprovalDecision { Id = Guid.NewGuid(), ApprovalRequestId = approval, ApprovalStepId = step, DecisionCode = "APPROVED", DecidedBy = InMemoryApiFactory.UserId, DecidedAt = t0.AddHours(5), Comment = "ok giá" });
                return Task.CompletedTask;
            });
            return (customer, order, contract);
        }

        private static List<JsonElement> Items(JsonElement data) => [.. data.GetProperty("listItem").EnumerateArray()];

        [Fact]
        public async Task Timeline_merges_record_and_related_documents_with_kinds_and_groups()
        {
            var (customer, order, contract) = await SeedAsync();
            var client = _factory.CreateAuthorizedClient();
            var data = (await client.GetResultAsync($"api/Common/GetTimeline?resource=Customer&id={customer}")).Ok();
            var items = Items(data);
            items.Select(i => i.GetProperty("at").GetDateTime()).Should().BeInDescendingOrder();
            items.Select(i => i.GetProperty("kind").GetString()).Should().Equal(["approve", "submit", "system", "approve", "update", "create"], "đơn chuyển Đã xác nhận = chấm duyệt");
            items[0].GetProperty("docId").GetGuid().Should().Be(contract);
            items[0].GetProperty("group").GetString().Should().Be("docs");
            items[0].GetProperty("comment").GetString().Should().Be("ok giá");
            items[1].GetProperty("comment").ValueKind.Should().Be(JsonValueKind.Null, "mã loại duyệt không hiện");
            items[2].GetProperty("isSystem").GetBoolean().Should().BeTrue();
            items[2].GetProperty("byName").GetString().Should().Be("Hệ thống");
            var orderItem = items[3];
            orderItem.GetProperty("docId").GetGuid().Should().Be(order);
            orderItem.GetProperty("docLabel").GetString().Should().StartWith("DH-");
            var st = orderItem.GetProperty("listChange")[0];
            st.GetProperty("from").GetString().Should().Be("Nháp", "trạng thái đổi ra chữ");
            items[4].GetProperty("isSelf").GetBoolean().Should().BeTrue();
            items[4].GetProperty("group").GetString().Should().Be("info");
            data.GetProperty("groupCount").GetProperty("all").GetInt32().Should().Be(6);
            data.GetProperty("groupCount").GetProperty("info").GetInt32().Should().Be(2);
            data.GetProperty("groupCount").GetProperty("docs").GetInt32().Should().Be(4);
            data.GetProperty("listRelated").EnumerateArray().Select(g => g.GetProperty("key").GetString()).Should().Contain(["Contract", "Order"]);
            data.GetProperty("listActor").EnumerateArray().Should().Contain(a => a.GetProperty("isSystem").GetBoolean());

            // Lọc nhóm / người ở máy chủ.
            Items((await client.GetResultAsync($"api/Common/GetTimeline?resource=Customer&id={customer}&group=info")).Ok()).Should().HaveCount(2);
            Items((await client.GetResultAsync($"api/Common/GetTimeline?resource=Customer&id={customer}&bySystem=true")).Ok()).Should().ContainSingle();
            Items((await client.GetResultAsync($"api/Common/GetTimeline?resource=Customer&id={customer}&by={InMemoryApiFactory.UserId}")).Ok()).Should().HaveCount(5);
        }

        [Fact]
        public void Script_arrow_lines_split_into_old_and_new_values()
        {
            var list = AppDelicare.Services.Implement.CommonService.ParseChanges("- Code: 'DEMO1' → 'KHB-00069' <br />\r\n- Name: From 'A' to 'B' <br />\r\n- Note: 'x' <br />\r\n");
            list[0].From.Should().Be("DEMO1");
            list[0].To.Should().Be("KHB-00069");
            list[1].From.Should().Be("A");
            list[2].From.Should().BeNull();
            list[2].To.Should().Be("x");
        }

        [Fact]
        public async Task Timeline_pages_by_20_and_hides_related_groups_without_view_and_checks_access()
        {
            var (customer, _, _) = await SeedAsync(extraEdits: 25);
            var admin = _factory.CreateAuthorizedClient();
            var p1 = (await admin.GetResultAsync($"api/Common/GetTimeline?resource=Customer&id={customer}")).Ok();
            Items(p1).Should().HaveCount(20);
            p1.GetProperty("hasMore").GetBoolean().Should().BeTrue();
            p1.GetProperty("total").GetInt32().Should().Be(31);
            var p2 = (await admin.GetResultAsync($"api/Common/GetTimeline?resource=Customer&id={customer}&page=2")).Ok();
            Items(p2).Should().HaveCount(11);
            p2.GetProperty("hasMore").GetBoolean().Should().BeFalse();

            // Chỉ quyền Xem khách hàng: không thấy hoạt động của hợp đồng / đơn.
            Guid viewer = await CommonTestUsers.UserAsync(_factory, ("crm-customer", "V"));
            var only = (await _factory.CreateAuthorizedClient(viewer).GetResultAsync($"api/Common/GetTimeline?resource=Customer&id={customer}&pageSize=100")).Ok();
            Items(only).Should().OnlyContain(i => i.GetProperty("isSelf").GetBoolean());
            only.GetProperty("listRelated").GetArrayLength().Should().Be(0);

            Guid nobody = await CommonTestUsers.UserAsync(_factory);
            (await _factory.CreateAuthorizedClient(nobody).GetAsync($"api/Common/GetTimeline?resource=Customer&id={customer}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await admin.GetResultAsync($"api/Common/GetTimeline?resource=Customer&id={Guid.NewGuid()}")).Fail("Không tìm thấy");
            (await admin.GetResultAsync($"api/Common/GetTimeline?resource=User&id={customer}")).Fail("không hỗ trợ");

            // Bản ghi không có nhật ký → danh sách rỗng.
            Guid quiet = Guid.NewGuid();
            await _factory.SeedAsync(db => { db.Customer.Add(new Customer { Id = quiet, Code = "Q" + quiet.ToString("N")[..6], Name = "[DEMO] Q", CIF = "Q", DisplayName = "Q", CustomerType = "FACTORY", Status = "Active" }); return Task.CompletedTask; });
            var empty = (await admin.GetResultAsync($"api/Common/GetTimeline?resource=Customer&id={quiet}")).Ok();
            Items(empty).Should().BeEmpty();
            empty.GetProperty("total").GetInt32().Should().Be(0);
        }
    }
}
