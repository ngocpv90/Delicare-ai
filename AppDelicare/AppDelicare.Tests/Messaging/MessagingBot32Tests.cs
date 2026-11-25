using System.Text.Json;
using AppDelicare.Messaging.Channels.Telegram.Models;
using AppDelicare.Messaging.Core;
using AppDelicare.Messaging.Core.Contracts;
using FluentAssertions;

namespace AppDelicare.Tests.Messaging
{
    /// <summary>
    /// BOT-3.2 / 3.3 (D-72): thao tác ghi trên bot – điểm phát ký nhận, đổi xe / tài xế, nấu thêm / ghi thừa / chia suất xong, ai chưa vào (tổ),
    /// lập lại kế hoạch, đề xuất đổi món, lô vệ sinh, nhập file máy chấm công (trả lời bằng file).
    /// </summary>
    public class MessagingBot32Tests
    {
        private static long _u = 320000;
        private static readonly DateTime Today = VnTime.Today(TimeProvider.System);
        private static readonly Guid Route = Guid.Parse("33333333-0000-4000-8000-000000000001");
        private static readonly Guid Stop = Guid.Parse("33333333-0000-4000-8000-000000000002");
        private static readonly Guid Batch = Guid.Parse("33333333-0000-4000-8000-000000000003");
        private static readonly Guid V1 = Guid.Parse("33333333-0000-4000-8000-0000000000a1");
        private static readonly Guid V2 = Guid.Parse("33333333-0000-4000-8000-0000000000a2");
        private static readonly Guid Emp = Guid.Parse("33333333-0000-4000-8000-0000000000b1");
        private static readonly Guid Dish1 = Guid.Parse("33333333-0000-4000-8000-0000000000c1");
        private static readonly Guid Dish2 = Guid.Parse("33333333-0000-4000-8000-0000000000c2");
        private static readonly Guid Clean = Guid.Parse("33333333-0000-4000-8000-0000000000d1");

        private static string Text(string text, long from = 7901, long? replyTo = null)
        {
            long id = Interlocked.Increment(ref _u);
            string chat = "{\"id\":" + from + ",\"type\":\"private\"}";
            string reply = replyTo == null ? "" : ",\"reply_to_message\":{\"message_id\":" + replyTo + ",\"date\":1,\"chat\":" + chat + "}";
            return "{\"update_id\":" + id + ",\"message\":{\"message_id\":" + id + ",\"date\":" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ",\"from\":{\"id\":" + from
                + ",\"first_name\":\"An\"},\"chat\":" + chat + ",\"text\":" + JsonSerializer.Serialize(text) + reply + "}}";
        }

        private static string Doc(string fileId, string fileName, long replyTo, long from = 7901)
        {
            long id = Interlocked.Increment(ref _u);
            string chat = "{\"id\":" + from + ",\"type\":\"private\"}";
            return "{\"update_id\":" + id + ",\"message\":{\"message_id\":" + id + ",\"date\":" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ",\"from\":{\"id\":" + from
                + ",\"first_name\":\"An\"},\"chat\":" + chat + ",\"document\":{\"file_id\":\"" + fileId + "\",\"file_name\":\"" + fileName + "\"},\"reply_to_message\":{\"message_id\":" + replyTo + ",\"date\":1,\"chat\":" + chat + "}}}";
        }

        private static string Tap(string data, long from = 7901)
        {
            long id = Interlocked.Increment(ref _u);
            return "{\"update_id\":" + id + ",\"callback_query\":{\"id\":\"cb" + id + "\",\"from\":{\"id\":" + from + "},\"data\":" + JsonSerializer.Serialize(data)
                + ",\"message\":{\"message_id\":50,\"date\":1,\"chat\":{\"id\":" + from + ",\"type\":\"private\"}}}}";
        }

        private static MessagingTestHost Host(params string[] menus)
        {
            var h = new MessagingTestHost();
            h.Api.Identity = new BotIdentityModel { Linked = true, CanUseBot = true, HasWebAccount = true, UserId = Guid.NewGuid(), FullName = "Chị Lan", Status = "Active", Menus = [.. menus] };
            h.Api.Token = new ApiResult<UserTokenModel> { ResponseCode = 200, Data = new UserTokenModel { Token = "jwt-u" } };
            return h;
        }

        private static long LastSentId(MessagingTestHost h) => 100 + h.Sender.Sent.Count;

        private static (string Method, string Path, Dictionary<string, string> Form, string Token) Last(MessagingTestHost h, string path)
            => h.Web.Calls.Last(c => c.Path.Split('?')[0] == path);

        private static IEnumerable<TgInlineButton> Buttons(object? markup) => markup is TgInlineKeyboard k ? k.InlineKeyboard.SelectMany(r => r) : [];

        private static string RouteDetail(string status) => JsonSerializer.Serialize(new
        {
            route = new { id = Route, routeNo = "CX-1", status, plannedStart = Today.AddHours(9).ToString("yyyy-MM-ddTHH:mm:ss") },
            statusName = status, licensePlate = "51C-111", driverName = "Tài xế Một", loadMeals = 100m, deliveryTempMinC = 60m,
            allowedActions = Array.Empty<string>(), listBlocker = Array.Empty<string>(),
            listStop = new[] { new { id = Stop, sequenceNo = 1, stopStatus = "Arrived", statusName = "Đã đến", customerName = "Trường A", plannedMealQty = 100m, allowedActions = Array.Empty<string>() } },
        });

        [Fact]
        public async Task Pickup_point_receives_with_prefilled_quantity_and_own_name()
        {
            await using var h = Host("#/crm-reconciliation");
            h.Web.Routes["api/DeliveryRoute/GetBoard"] = (_, _) => (200, null, "{\"listRoute\":[" + RouteDetail("InTransit") + "]}", null);
            h.Web.Routes["api/DeliveryStop/Deliver"] = (_, _) => (200, "Đã bàn giao", "{}", null);
            await h.SendAsync(Text("/xeden"));
            Buttons(h.Sender.Sent.Last().Message.ReplyMarkup).Should().Contain(b => b.CallbackData == "rcn:rv:" + Stop.ToString("N"));
            await h.SendAsync(Tap("rcn:rv:" + Stop.ToString("N")));
            // BOT-UX-2 (mục 6): màn số điền sẵn số kế hoạch – [✅ Ký nhận 100 suất] 1 chạm, chỉnh bằng −10 −1 +1 +10 / hàng chọn nhanh.
            Buttons(h.Sender.Edits.Last().Markup).Should().Contain(b => b.Text == "✅ Ký nhận 100 suất" && b.CallbackData == "rcn:rq:" + Stop.ToString("N") + ".100")
                .And.Contain(b => b.CallbackData == "rcn:rvn:" + Stop.ToString("N") + ".98");
            await h.SendAsync(Tap("rcn:rq:" + Stop.ToString("N") + ".98"));
            var form = Last(h, "api/DeliveryStop/Deliver").Form;
            form["Id"].Should().Be(Stop.ToString());
            form["DeliveredQuantity"].Should().Be("98");
            form["AcceptedQuantity"].Should().Be("98");
            form["ReceiverName"].Should().Be("Chị Lan");
            h.Sender.Sent.Last().Message.Text.Should().Contain("98 suất, người nhận Chị Lan");
        }

        [Fact]
        public async Task Dispatcher_changes_vehicle_and_driver_before_departure()
        {
            await using var h = Host("#/log-route");
            string detail = RouteDetail("Planned");
            h.Web.Routes["api/DeliveryRoute/GetDetail"] = (_, _) => (200, null, detail, null);
            h.Web.Routes["api/Vehicle/GetAll"] = (_, _) => (200, null, null, JsonSerializer.Serialize(new[] { new { id = V1, licensePlate = "51C-111", capacity = 1500m }, new { id = V2, licensePlate = "51C-222", capacity = 1000m } }));
            h.Web.Routes["api/Employee/GetAll"] = (_, _) => (200, null, null, JsonSerializer.Serialize(new[] { new { id = Emp, fullName = "Tài xế Hùng", code = "NV-9" } }));
            h.Web.Routes["api/DeliveryRoute/Reassign"] = (_, _) => (200, "Đã đổi xe của chuyến CX-1", JsonSerializer.Serialize(new { listWarning = new[] { "Xe 51C-222 đã có chuyến khác" } }), null);
            await h.SendAsync(Tap("dlv:r:" + Route.ToString("N")));
            Buttons(h.Sender.Edits.Last().Markup).Select(b => b.CallbackData).Should().Contain(["dlv:rv:" + Route.ToString("N"), "dlv:rd:" + Route.ToString("N")]);
            await h.SendAsync(Tap("dlv:rv:" + Route.ToString("N")));
            Buttons(h.Sender.Edits.Last().Markup).Should().Contain(b => b.Text.Contains("51C-222") && b.CallbackData == "dlv:pv:" + Route.ToString("N") + ".1");
            await h.SendAsync(Tap("dlv:pv:" + Route.ToString("N") + ".1"));
            Last(h, "api/DeliveryRoute/Reassign").Form["VehicleId"].Should().Be(V2.ToString());
            h.Sender.Edits.Last().Text.Should().Contain("Đã đổi xe").And.Contain("⚠️ Xe 51C-222");
            await h.SendAsync(Tap("dlv:rd:" + Route.ToString("N")));
            await h.SendAsync(Text("Hùng", replyTo: LastSentId(h)));
            Last(h, "api/Employee/GetAll").Path.Should().Contain("SearchText=H");
            await h.SendAsync(Tap("dlv:pd:" + Route.ToString("N") + ".0"));
            Last(h, "api/DeliveryRoute/Reassign").Form["DriverId"].Should().Be(Emp.ToString());
            // Chuyến đang giao: không có nút đổi.
            h.Web.Routes["api/DeliveryRoute/GetDetail"] = (_, _) => (200, null, RouteDetail("InTransit"), null);
            await h.SendAsync(Tap("dlv:r:" + Route.ToString("N")));
            Buttons(h.Sender.Edits.Last().Markup).Should().NotContain(b => b.CallbackData.StartsWith("dlv:rv:"));
        }

        [Fact]
        public async Task Kitchen_extra_batch_waste_pack_and_team_absent()
        {
            await using var h = Host("#/prod-batch", "#/hr-shift-assign");
            h.Web.Routes["api/ProductionBatch/GetBoard"] = (_, _) => (200, null, JsonSerializer.Serialize(new
            {
                listBatch = new[] { new { id = Batch, batchNumber = "ME-1", dishName = "Canh", status = "Passed", statusName = "Đạt", plannedQuantity = 100m, sampleRetained = true } },
            }), null);
            foreach (var a in new[] { "Pack", "RecordWaste" })
            {
                h.Web.Routes["api/ProductionBatch/" + a] = (_, _) => (200, "Đã ghi " + a, "{}", null);
            }
            h.Web.Routes["api/ProductionBatch/CreateExtra"] = (_, _) => (200, "Đã tạo mẻ nấu thêm ME-2 (20 suất)", JsonSerializer.Serialize(new { batch = new { batchNumber = "ME-2" }, listWarning = new[] { "Mẻ ME-2: lập phiếu xuất nguyên liệu" } }), null);
            string n = Batch.ToString("N");
            await h.SendAsync(Tap("bat:view:" + n));
            Buttons(h.Sender.Edits.Last().Markup).Select(b => b.CallbackData).Should().Contain(["bat:pk:" + n, "bat:wx:" + n, "bat:ex:" + n]);
            await h.SendAsync(Tap("bat:pk:" + n));
            Last(h, "api/ProductionBatch/Pack").Form["Id"].Should().Be(Batch.ToString());
            // BOT-UX-2 (mục 6, 7): số suất = màn số (−10 −1 +1 +10, ✅ Xác nhận), lý do = nút + [✏️ Khác…] – không gõ số.
            await h.SendAsync(Tap("bat:wx:" + n));
            Buttons(h.Sender.Edits.Last().Markup).Select(b => b.CallbackData).Should().Contain(["bat:wxn:" + n + ".11", "bat:wxn:" + n + ".5", "bat:wq:" + n + ".1"]);
            await h.SendAsync(Tap("bat:wxn:" + n + ".5"));
            await h.SendAsync(Tap("bat:wq:" + n + ".5"));
            Last(h, "api/ProductionBatch/RecordWaste").Form["QuantityMeals"].Should().Be("5");
            await h.SendAsync(Tap("bat:ex:" + n));
            await h.SendAsync(Tap("bat:exq:" + n + ".20"));
            h.Web.Calls.Should().NotContain(c => c.Path == "api/ProductionBatch/CreateExtra", "mới chọn số – chưa có lý do");
            await h.SendAsync(Tap("bat:exx:" + n + ".20"));
            await h.SendAsync(Text("Thiếu 20 suất khách A", replyTo: LastSentId(h)));
            var extra = Last(h, "api/ProductionBatch/CreateExtra").Form;
            extra["Quantity"].Should().Be("20");
            extra["Reason"].Should().Be("Thiếu 20 suất khách A");
            h.Sender.Sent.Last().Message.Text.Should().Contain("ME-2").And.Contain("phiếu xuất nguyên liệu");

            h.Web.Routes["api/ShiftAssignment/GetGrid"] = (_, _) => (200, null, "{\"employees\":[]}", null);
            await h.SendAsync(Text("👥 Ai chưa vào (tổ)"));
            Last(h, "api/ShiftAssignment/GetGrid").Path.Should().Contain("myUnit=true");
            h.Sender.Sent.Last().Message.Text.Should().Contain("tổ tôi");
        }

        [Fact]
        public async Task Regenerate_plan_menu_change_and_cleaning()
        {
            await using var h = Host("#/prod-plan", "#/menu-menu", "#/qc-cleaning");
            h.Web.Routes["api/ProductionPlan/Generate"] = (_, _) => (200, "Đã lập kế hoạch", JsonSerializer.Serialize(new { plan = new { planNumber = "KH-9" }, statusName = "Nháp" }), null);
            await h.SendAsync(Text("/laplai"));
            string day = Today.AddDays(1).ToString("yyyyMMdd");
            Buttons(h.Sender.Sent.Last().Message.ReplyMarkup).Should().Contain(b => b.CallbackData == "op:pg:" + day);
            await h.SendAsync(Tap("op:pg:" + day));
            Last(h, "api/ProductionPlan/Generate").Form["ProductionDate"].Should().Be(Today.AddDays(1).ToString("yyyy-MM-dd"));
            h.Sender.Sent.Last().Message.Text.Should().Contain("KH-9");

            h.Web.Routes["api/Dish/GetAll"] = (path, _) => (200, null, null, JsonSerializer.Serialize(path.Contains("Canh")
                ? new[] { new { id = Dish1, name = "Canh chua" } } : new[] { new { id = Dish2, name = "Canh bí" } }));
            h.Web.Routes["api/MenuChangeRequest/Create"] = (_, _) => (200, "Đã gửi đề xuất", "{}", null);
            await h.SendAsync(Text("/doimon"));
            await h.SendAsync(Text("Canh", replyTo: LastSentId(h)));
            await h.SendAsync(Tap("op:df:0"));
            await h.SendAsync(Text("bí", replyTo: LastSentId(h)));
            await h.SendAsync(Tap("op:dt:0"));
            string meal = Today.AddDays(2).ToString("yyyyMMdd");
            await h.SendAsync(Tap("op:cd:" + meal));
            // BOT-UX-2 (mục 7): lý do đổi món = nút lý do hay gặp (bấm, không gõ).
            await h.SendAsync(Tap("op:chr:k.0"));
            var change = Last(h, "api/MenuChangeRequest/Create").Form;
            change["OriginalDishId"].Should().Be(Dish1.ToString());
            change["RequestedDishId"].Should().Be(Dish2.ToString());
            change["MealDate"].Should().Be(Today.AddDays(2).ToString("yyyy-MM-dd"));
            change["Reason"].Should().Be("Khách đề nghị");

            h.Web.Routes["api/CleaningBatch/GetAll"] = (_, _) => (200, null, null, JsonSerializer.Serialize(new[] { new { id = Clean, cleaningBatchNumber = "VS-1", containerQuantity = 120 } }));
            h.Web.Routes["api/CleaningBatch/Complete"] = (_, form) => (200, "Đã kết luận", "{}", null);
            h.Web.Routes["api/CleaningBatch/PassAll"] = (_, _) => (200, "Đã đạt tất cả và kết luận", "{}", null);
            await h.SendAsync(Text("/vesinh"));
            h.Sender.Sent.Last().Message.Text.Should().Contain("VS-1 · 120 khay");
            // UI-6.45 (UI-6.32): ✅ Đạt = Đạt tất cả & kết luận (PassAll) – không còn bị chặn vì chưa ghi từng bước.
            await h.SendAsync(Tap("op:cp:0"));
            Last(h, "api/CleaningBatch/PassAll").Form["Id"].Should().Be(Clean.ToString());
            h.Sender.Sent.Last().Message.Text.Should().Contain("Đạt, sẵn sàng dùng lại");
            await h.SendAsync(Tap("op:cr:0"));
            await h.SendAsync(Tap("op:crx:0"));
            await h.SendAsync(Text("Khay còn bẩn", replyTo: LastSentId(h)));
            var reject = Last(h, "api/CleaningBatch/Complete").Form;
            reject["QcResult"].Should().Be("REJECT");
            reject["Note"].Should().Be("Khay còn bẩn");
        }

        [Fact]
        public async Task Hr_imports_attendance_machine_file_by_replying_with_document()
        {
            await using var h = Host("#/hr-attendance");
            h.Sender.Files["FILE-1"] = [1, 2, 3, 4];
            h.Web.Routes["api/ImportExcel/Import"] = (_, _) => (200, "Nhập thành công", JsonSerializer.Serialize(new { totalRows = 3, inserted = 2, skipped = 0, listError = new[] { new { row = 4, message = "Không có ca" } } }), null);
            await h.SendAsync(Text("/nhapmcc"));
            h.Sender.Sent.Last().Message.Text.Should().Contain("Gửi file Excel");
            await h.SendAsync(Doc("FILE-X", "anh.png", LastSentId(h)));
            h.Sender.Sent.Last().Message.Text.Should().Contain("Chỉ nhận file .xlsx");
            await h.SendAsync(Doc("FILE-1", "mcc-thang11.xlsx", LastSentId(h)));
            var call = h.Web.Calls.Last(c => c.Path == "api/ImportExcel/Import");
            call.Method.Should().Be("FILE");
            call.Form["TableName"].Should().Be("AttendanceMachine");
            call.Form["__file"].Should().Be("mcc-thang11.xlsx");
            call.Token.Should().Be("jwt-u");
            h.Sender.Sent.Last().Message.Text.Should().Contain("Đọc 3 dòng · thêm 2").And.Contain("Dòng 4: Không có ca");
        }
    }
}
