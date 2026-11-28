using System.Text.Json;
using System.Text.Json.Nodes;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Share;
using AppDelicare.Messaging.Channels.Zalo;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Implement.Agent;
using AppDelicare.Services.Implement.CustomerChannel;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Agent
{
    /// <summary>
    /// AI-19 (D-133 đợt 4, D-06): bot Zalo khách. KD cấp mã → tài khoản CUSTOMER gắn CustomerContact.UserId → khách nhắn mã vào OA → liên kết; khách chỉ thấy / chạm đơn của chính mình;
    /// xác nhận suất / báo thiếu / phản ánh = đề xuất CSKH chờ KD / điều phối (không ghi trước khi duyệt), duyệt xong nhắn lại khách qua Zalo; cùng msg_id không xử lý 2 lần.
    /// Cổng công cụ: người hỏi "zalo:&lt;id&gt;" chỉ dùng công cụ dành cho khách, _khach do máy chủ đặt (agent không giả mạo được). Chữ ký webhook Zalo.
    /// </summary>
    public class CustomerZaloAi19Tests
    {
        private const string Key = "khoa-ky-ma-lien-ket-test-ai19-0123456789abcdef";
        private static readonly Guid System = Guid.Parse("19191919-0000-4000-8000-0000000000ff");
        private const string Zid = "8123456789012345";

        private static async Task<IResult<CustomerChannelReplyModel>> SayAsync(InMemoryApiFactory f, string text, string? ev = null, string ext = Zid)
        {
            using var scope = f.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ICustomerChannelService>().Handle(new CustomerChannelRequest { BotCode = "ZALO_KH", ExternalUserId = ext, EventId = ev ?? Guid.NewGuid().ToString("N"), Text = text });
        }

        private static string Reply(IResult<CustomerChannelReplyModel> r)
        {
            r.ResponseCode.Should().Be(200, r.ResponseMessage);
            return string.Join("\n", r.Data!.Replies);
        }

        private static async Task<Guid> LastCskhRequestAsync(InMemoryApiFactory f)
            => await f.QueryAsync(db => db.ApprovalRequest.Where(a => a.BusinessType == AgentCommandService.BusinessType && a.RequestStatus == "Pending").OrderByDescending(a => a.RequestedAt).ThenByDescending(a => a.RequestNo).Select(a => a.Id).FirstAsync());

        private static async Task ApproveAsync(InMemoryApiFactory f, Guid id)
        {
            using var scope = f.Services.CreateScope();
            using var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForUser(InMemoryApiFactory.UserId));
            var r = await scope.ServiceProvider.GetRequiredService<IApprovalRequestService>().Decide(new ApprovalDecideRequest { Id = id, Approve = true, Note = "OK" });
            r.ResponseCode.Should().Be(200, r.ResponseMessage);
        }

        [Fact]
        public async Task Customer_links_by_code_sees_only_own_orders_and_every_write_waits_for_staff()
        {
            using var f = await AgentToolsAi3Tests.FactoryAsync(("Messaging:LinkSigningKey", Key), ("SystemUsers:JobUserId", System.ToString()));
            DateTime today = DateTime.Today;
            f.Clock.FixedNow = today.AddHours(9);
            f.CreateAuthorizedClient().Dispose();
            Guid customer = Guid.NewGuid(), other = Guid.NewGuid(), contact = Guid.NewGuid(), order = Guid.NewGuid(), shift = Guid.NewGuid(), menu = Guid.NewGuid(), dish = Guid.NewGuid();
            await f.SeedAsync(db =>
            {
                db.User.Add(new User { Id = System, UserName = "system-a19", PassWord = "!NO-LOGIN!", IsActive = true });
                Guid ch = Guid.NewGuid();
                db.IntegrationChannel.Add(new IntegrationChannel { Id = ch, ChannelCode = "ZALO_OA", ChannelName = "Zalo", ProviderCode = "ZALO", ChannelStatus = "Active", ConfigJson = "{}" });
                db.BotConfiguration.Add(new BotConfiguration { Id = Guid.NewGuid(), IntegrationChannelId = ch, BotCode = "ZALO_KH", BotName = "Zalo KH", BotStatus = "Active" });
                db.Customer.Add(new Customer { Id = customer, Code = "K19", CIF = "K19", Name = "Công ty A19", DisplayName = "Công ty A19", CustomerType = "FACTORY", Status = "Active" });
                db.Customer.Add(new Customer { Id = other, Code = "K19B", CIF = "K19B", Name = "Công ty B19", DisplayName = "Công ty B19", CustomerType = "FACTORY", Status = "Active" });
                db.CustomerContact.Add(new CustomerContact { Id = contact, CustomerId = customer, FullName = "Chị Lan", Phone = "0900000019", Status = "Active", IsPrimary = true });
                db.Shift.Add(new Shift { Id = shift, Code = "T19", Name = "Trưa 19", StartTime = new TimeSpan(10, 30, 0), Status = "Active" });
                db.Dish.Add(new Dish { Id = dish, Code = "M19", Name = "Gà kho gừng", Status = "Active" });
                int dow = today.AddDays(1).DayOfWeek == DayOfWeek.Sunday ? 7 : (int)today.AddDays(1).DayOfWeek;
                db.Menu.Add(new Model.Entity.Menu { Id = menu, Code = "TD19", Name = "TĐ 19", VersionNo = 1, Status = "Published" });
                db.MenuLine.Add(new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = dish, MealType = "TRUA", DayOfWeek = dow, SequenceNo = 1, SortOrder = 1 });
                db.Order.Add(new Order { Id = order, OrderNumber = "DH-19A", CustomerId = customer, OrderDate = today, MealDate = today.AddDays(1), DeliveryDate = today.AddDays(1), Status = "Draft", TotalQuantity = 100 });
                db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = order, MenuId = menu, MenuOptionNo = 1, MealType = "TRUA", ShiftId = shift, Quantity = 100, LineAmount = 0 });
                db.Order.Add(new Order { Id = Guid.NewGuid(), OrderNumber = "DH-19B", CustomerId = other, OrderDate = today, MealDate = today.AddDays(1), Status = "Draft", TotalQuantity = 50 });
                return Task.CompletedTask;
            });

            // KD cấp mã → tài khoản CUSTOMER gắn người liên hệ.
            CustomerLinkCodeModel code;
            using (var scope = f.Services.CreateScope())
            using (scope.ServiceProvider.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForUser(InMemoryApiFactory.UserId)))
            {
                var r = await scope.ServiceProvider.GetRequiredService<ICustomerChannelService>().CreateLinkCode(new CustomerLinkCodeRequest { ContactId = contact });
                r.ResponseCode.Should().Be(200, r.ResponseMessage);
                code = r.Data!;
            }
            var cc = await f.QueryAsync(db => db.CustomerContact.AsNoTracking().SingleAsync(c => c.Id == contact));
            (await f.QueryAsync(db => db.User.AsNoTracking().SingleAsync(u => u.Id == cc.UserId))).AccountType.Should().Be("CUSTOMER");

            Reply(await SayAsync(f, "xin chào")).Should().Contain("MÃ LIÊN KẾT", "chưa liên kết → chỉ hướng dẫn, không lộ dữ liệu");
            Reply(await SayAsync(f, code.Code, "ev-link")).Should().Contain("Đã liên kết Zalo với Chị Lan");
            (await SayAsync(f, code.Code, "ev-link")).Data!.Duplicate.Should().BeTrue("cùng msg_id không xử lý 2 lần");
            Reply(await SayAsync(f, code.Code, ext: "khac-999")).Should().Contain("đã được dùng", "mã dùng 1 lần");

            // Chỉ đơn / thực đơn của chính khách.
            string orders = Reply(await SayAsync(f, "1"));
            orders.Should().Contain("DH-19A").And.NotContain("DH-19B");
            Reply(await SayAsync(f, "2")).Should().Contain("Gà kho gừng");
            Reply(await SayAsync(f, "3 DH-19B")).Should().Contain("Không tìm thấy đơn «DH-19B» của quý khách");

            // Xác nhận đơn → đề xuất CSKH, đơn chưa đổi; KD duyệt → Đã chốt + tin Zalo cho khách.
            Reply(await SayAsync(f, "3 DH-19A")).Should().Contain("Đã gửi yêu cầu");
            (await f.QueryAsync(db => db.Order.AsNoTracking().SingleAsync(o => o.Id == order))).Status.Should().Be("Draft");
            var req = await f.QueryAsync(db => db.ApprovalRequest.AsNoTracking().SingleAsync(a => a.Id == db.ApprovalRequest.Where(x => x.BusinessType == AgentCommandService.BusinessType).OrderByDescending(x => x.RequestNo).Select(x => x.Id).First()));
            req.PayloadJson.Should().Contain("CSKH").And.Contain(cc.UserId!.Value.ToString());
            await ApproveAsync(f, req.Id);
            (await f.QueryAsync(db => db.Order.AsNoTracking().SingleAsync(o => o.Id == order))).Status.Should().Be("Confirmed");
            var notice = await f.QueryAsync(db => db.OutboundMessage.AsNoTracking().SingleAsync(m => m.MessageType == CustomerNotifier.Template));
            notice.RecipientExternalId.Should().Be(Zid);
            ZaloOutboundPump.TextOf(notice.PayloadJson).Should().Contain("đã xác nhận yêu cầu");

            // Báo thiếu suất → chưa có phản ánh tới khi duyệt; duyệt → phản ánh SHORTAGE nguồn ZALO.
            Reply(await SayAsync(f, "4 DH-19A 5 thiếu canh")).Should().Contain("THIẾU 5 suất");
            (await f.QueryAsync(db => db.CustomerComplaint.CountAsync(c => c.CustomerId == customer))).Should().Be(0);
            await ApproveAsync(f, await LastCskhRequestAsync(f));
            var cpl = await f.QueryAsync(db => db.CustomerComplaint.AsNoTracking().SingleAsync(c => c.CustomerId == customer));
            (cpl.CategoryCode, cpl.SourceChannel).Should().Be(("SHORTAGE", "ZALO"));

            // Phản ánh tự do → phân loại theo từ khóa (giao trễ → LATE).
            Reply(await SayAsync(f, "5 Cơm trưa giao trễ 30 phút")).Should().Contain("(LATE)");

            // Cổng công cụ: người hỏi zalo:<id> – chỉ công cụ dành cho khách; _khach giả mạo bị bỏ.
            var mine = await CallAsZaloAsync(f, "khach_don_hang", new { });
            mine.Status.Should().Be(200, mine.Message);
            JsonSerializer.Serialize(mine.Data).Should().Contain("DH-19A").And.NotContain("DH-19B");
            (await CallAsZaloAsync(f, "ghi_nhat_ky", new { loai = "nhat_ky", noi_dung = "x" })).Message.Should().Contain("Khách hàng chỉ dùng được công cụ dành cho khách");
            (await CallAsZaloAsync(f, "tim_tri_thuc", new { cau_hoi = "giờ giao" })).Status.Should().NotBe(403, "khách hỏi được kho – chỉ ghi chú công khai (phạm vi CSKH)");
            (await CallAsZaloAsync(f, "propose_portion_confirm", new { so_don = "DH-19B", ly_do = "x", _khach = other.ToString() })).Message.Should().Contain("Không tìm thấy đơn «DH-19B» của quý khách");
            (await CallAsZaloAsync(f, "khach_don_hang", new { }, "zalo:chua-lien-ket")).Status.Should().Be(403);
            (await AgentToolsAi3Tests.Call(f, "CSKH", "propose_complaint", new { noi_dung = "x", ly_do = "x" })).Message.Should().Contain("chỉ dùng khi khách hàng hỏi qua Zalo");
        }

        private static async Task<AgentCallResult> CallAsZaloAsync(InMemoryApiFactory f, string tool, object args, string obo = "zalo:" + Zid)
        {
            using var scope = f.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IAgentGatewayService>().CallAsync(new AgentCallRequest
            {
                AgentCode = "CSKH", AgentKey = AgentToolsAi3Tests.Key, Tool = tool, Arguments = JsonSerializer.SerializeToElement(args), OnBehalfOf = obo,
            });
        }

        [Fact]
        public void Zalo_webhook_signature_and_event_parsing()
        {
            string body = "{\"app_id\":\"123\",\"event_name\":\"user_send_text\",\"timestamp\":\"1764300000000\",\"sender\":{\"id\":\"999\"},\"message\":{\"text\":\"1\",\"msg_id\":\"m-1\"}}";
            string mac = ZaloSignature.Mac("123", body, "1764300000000", "oa-secret");
            ZaloSignature.Verify("mac=" + mac, "123", body, "1764300000000", "oa-secret").Should().BeTrue();
            ZaloSignature.Verify("mac=" + mac, "123", body.Replace("\"1\"", "\"2\""), "1764300000000", "oa-secret").Should().BeFalse("sửa nội dung → sai chữ ký");
            ZaloSignature.Verify("mac=" + mac, "123", body, "1764300000000", null).Should().BeFalse("thiếu khóa OA → từ chối");
            var ev = ZaloEventParser.Parse(body)!;
            (ev.EventName, ev.UserId, ev.Text, ev.MessageId).Should().Be(("user_send_text", "999", "1", "m-1"));
            ZaloEventParser.Parse("{\"event_name\":\"user_seen_message\",\"sender\":{\"id\":\"1\"}}").Should().BeNull("chỉ xử lý tin chữ / quan tâm OA");
            ZaloEventParser.Parse("{\"event_name\":\"follow\",\"timestamp\":\"1\",\"follower\":{\"id\":\"77\"}}")!.Text.Should().BeEmpty("quan tâm OA → gửi hướng dẫn");
        }
    }
}
