using System.Text.Json;
using AppDelicare.Messaging.Channels.Telegram.Models;
using AppDelicare.Messaging.Core.Contracts;
using FluentAssertions;

namespace AppDelicare.Tests.Messaging
{
    /// <summary>
    /// BOT-1.5b (D-62, BOT_TELEGRAM §3.2): tài xế – 🚚 Chuyến của tôi (GetMine) → thẻ chuyến, 🚀 Xuất phát kèm nhiệt độ (nút ≥ ngưỡng), thẻ điểm 📍 Đã đến / ✅ Giao
    /// (số suất điền sẵn = đã xếp → nhiệt độ → tên người nhận gõ tay) / ❌ Không giao được (lý do chọn sẵn), 📥 Nhận hộp về; điều phối – 🗺 Giao vận hôm nay;
    /// điểm phát – 🧮 Đối soát suất (điền sẵn số khách nhận POD), 🚚 Xe đến hôm nay. Mọi ghi qua api nghiệp vụ bằng JWT của người dùng; chức năng ẩn khi không có quyền màn.
    /// </summary>
    public class MessagingGiaoVanTests
    {
        private static readonly Guid Route = Guid.Parse("33333333-0000-4000-8000-0000000000a1");
        private static readonly Guid Stop1 = Guid.Parse("33333333-0000-4000-8000-0000000000b1");
        private static readonly Guid Stop2 = Guid.Parse("33333333-0000-4000-8000-0000000000b2");
        private static readonly Guid DeliveryOrder1 = Guid.Parse("33333333-0000-4000-8000-0000000000c1");
        private static readonly Guid Order = Guid.Parse("33333333-0000-4000-8000-0000000000d1");
        private static long _u = 80000;

        private static string Text(string text, long from = 7101, long? replyTo = null)
        {
            long id = Interlocked.Increment(ref _u);
            string reply = replyTo == null ? "" : ",\"reply_to_message\":{\"message_id\":" + replyTo + ",\"date\":1,\"chat\":{\"id\":" + from + ",\"type\":\"private\"}}";
            return "{\"update_id\":" + id + ",\"message\":{\"message_id\":" + id + ",\"date\":" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ",\"from\":{\"id\":" + from
                + ",\"first_name\":\"Tai\"},\"chat\":{\"id\":" + from + ",\"type\":\"private\"},\"text\":" + JsonSerializer.Serialize(text) + reply + "}}";
        }

        private static string Tap(string data, long messageId = 60, long from = 7101)
        {
            long id = Interlocked.Increment(ref _u);
            return "{\"update_id\":" + id + ",\"callback_query\":{\"id\":\"cb" + id + "\",\"from\":{\"id\":" + from + "},\"data\":" + JsonSerializer.Serialize(data)
                + ",\"message\":{\"message_id\":" + messageId + ",\"date\":1,\"chat\":{\"id\":" + from + ",\"type\":\"private\"}}}}";
        }

        private static long LastSentId(MessagingTestHost h) => 100 + h.Sender.Sent.Count;

        private static (string Method, string Path, Dictionary<string, string> Form, string Token) Last(MessagingTestHost h, string path)
            => h.Web.Calls.Last(c => c.Path.Split('?')[0] == path);

        private static List<string> Buttons(object? markup) => [.. ((TgInlineKeyboard)markup!).InlineKeyboard.SelectMany(r => r).Select(b => b.CallbackData!)];

        private static string Detail(string routeStatus, string[] routeActions, string stop1Status, string[] stop1Actions, decimal? departureTemp = null)
            => JsonSerializer.Serialize(new
            {
                route = new { id = Route, routeNo = "CX-261118-01", status = routeStatus, plannedStart = "2031-06-02T09:30:00" },
                statusName = routeStatus == "InTransit" ? "Đang giao" : "Đang xếp",
                licensePlate = "29C-123.45", driverName = "Tài xế Bình", loadMeals = 300m, deliveryTempMinC = 60m, departureTemperatureC = departureTemp,
                allowedActions = routeActions,
                listBlocker = Array.Empty<string>(),
                listStop = new object[]
                {
                    new { id = Stop1, sequenceNo = 1, stopStatus = stop1Status, statusName = stop1Status, customerName = "Trường A", locationName = "Cổng chính", address = "1 Lê Lợi",
                        plannedArrival = "2031-06-02T10:30:00", plannedMealQty = 100m, loadedQty = 98m, deliveryOrderId = DeliveryOrder1, allowedActions = stop1Actions },
                    new { id = Stop2, sequenceNo = 2, stopStatus = "Planned", statusName = "Kế hoạch", customerName = "Công ty B", plannedArrival = "2031-06-02T11:00:00",
                        plannedMealQty = 200m, allowedActions = Array.Empty<string>() },
                },
            });

        private static MessagingTestHost DriverHost(string detail, params string[] menus)
        {
            var h = new MessagingTestHost();
            h.Api.Identity = new BotIdentityModel { Linked = true, CanUseBot = true, HasWebAccount = true, UserId = Guid.NewGuid(), FullName = "Tài xế Bình", Status = "Active", Menus = [.. menus] };
            h.Api.Token = new ApiResult<UserTokenModel> { ResponseCode = 200, Data = new UserTokenModel { Token = "jwt-driver" } };
            h.Web.Routes["api/DeliveryRoute/GetMine"] = (_, _) => (200, null, "{\"listRoute\":[" + detail + "]}", null);
            h.Web.Routes["api/DeliveryRoute/GetBoard"] = (_, _) => (200, null, "{\"listRoute\":[" + detail + "]}", null);
            h.Web.Routes["api/DeliveryRoute/GetDetail"] = (_, _) => (200, null, detail, null);
            h.Web.Routes["api/DeliveryStop/GetById"] = (_, _) => (200, null, "{\"id\":\"" + Stop1 + "\",\"deliveryRouteId\":\"" + Route + "\"}", null);
            foreach (var a in new[] { "DeliveryRoute/Dispatch", "DeliveryStop/Arrive", "DeliveryStop/Deliver", "DeliveryStop/Fail", "ContainerReturn/Record" })
            {
                h.Web.Routes["api/" + a] = (_, _) => (200, "Đã ghi " + a, "{}", null);
            }
            return h;
        }

        [Fact]
        public async Task Driver_my_trip_card_then_dispatch_with_temperature_buttons()
        {
            await using var h = DriverHost(Detail("Loading", ["recordTemperature", "load", "dispatch"], "Planned", []), "#/log-stop", "#/log-route");
            await h.SendAsync(Text("/chuyen"));
            Last(h, "api/DeliveryRoute/GetMine").Token.Should().Be("jwt-driver");
            var card = h.Sender.Sent.Last().Message;
            card.Text.Should().Contain("CX-261118-01").And.Contain("29C-123.45").And.Contain("#1 Trường A").And.Contain("nhiệt độ xuất phát: chưa đo");
            string r = Route.ToString("N");
            Buttons(card.ReplyMarkup).Should().Contain("dlv:dsp:" + r);
            await h.SendAsync(Tap("dlv:dsp:" + r));
            var temps = Buttons(h.Sender.Edits.Last().Markup);
            temps.Should().Contain(["dlv:dt:" + r + ".60", "dlv:dt:" + r + ".65", "dlv:dtn:" + r], "nhiệt độ 1 chạm + màn số cho nhiệt độ khác (không gõ tay)");
            temps.Should().NotContain(b => b.EndsWith(".-"), "chưa đo thì không có xuất phát 1 chạm");
            await h.SendAsync(Tap("dlv:dt:" + r + ".65"));
            var call = Last(h, "api/DeliveryRoute/Dispatch");
            call.Form["Id"].Should().Be(Route.ToString());
            call.Form["TemperatureC"].Should().Be("65");
            h.Sender.Edits.Last().Text.Should().Contain("Đã ghi DeliveryRoute/Dispatch");
        }

        [Fact]
        public async Task Dispatch_one_tap_when_departure_temperature_already_ok_and_typed_temperature()
        {
            await using var h = DriverHost(Detail("Loading", ["dispatch"], "Planned", [], departureTemp: 66m), "#/log-stop");
            string r = Route.ToString("N");
            await h.SendAsync(Tap("dlv:dsp:" + r));
            Buttons(h.Sender.Edits.Last().Markup).Should().Contain("dlv:dt:" + r + ".-");
            await h.SendAsync(Tap("dlv:dt:" + r + ".-"));
            Last(h, "api/DeliveryRoute/Dispatch").Form.Should().NotContainKey("TemperatureC");
            await h.SendAsync(Tap("dlv:dtx:" + r));
            h.Sender.Sent.Last().Message.ReplyMarkup.Should().BeOfType<TgForceReply>();
            await h.SendAsync(Text("66,5", replyTo: LastSentId(h)));
            Last(h, "api/DeliveryRoute/Dispatch").Form["TemperatureC"].Should().Be("66.5");
        }

        [Fact]
        public async Task Stop_card_arrive_then_deliver_prefilled_loaded_quantity_temperature_and_receiver()
        {
            await using var h = DriverHost(Detail("InTransit", ["exportDeliveryNote"], "Arrived", ["recordTemperature", "deliver", "fail"]), "#/log-stop");
            string s = Stop1.ToString("N");
            await h.SendAsync(Tap("dlv:s:" + s));
            var card = h.Sender.Edits.Last();
            card.Text.Should().Contain("Trường A").And.Contain("đã xếp 98");
            Buttons(card.Markup).Should().Contain(["dlv:dl:" + s, "dlv:fl:" + s, "dlv:r:" + Route.ToString("N")]).And.NotContain("dlv:arr:" + s);
            // BOT-UX-2: [✅ Giao đủ 98] có sẵn trên thẻ (số đã xếp); người nhận điền sẵn, đổi bằng 👤 Người nhận khác… (chữ tự do – gõ tay).
            Buttons(card.Markup).Should().Contain(["dlv:full:" + s, "dlv:rcv:" + s]);
            card.Text.Should().Contain("người nhận: Đại diện Trường A");
            await h.SendAsync(Tap("dlv:rcv:" + s));
            await h.SendAsync(Text("Cô Lan", replyTo: LastSentId(h)));
            h.Sender.Sent.Last().Message.Text.Should().Contain("người nhận: Cô Lan");
            h.Web.Calls.Should().NotContain(c => c.Path == "api/DeliveryStop/Deliver", "đổi người nhận chưa ghi gì");
            await h.SendAsync(Tap("dlv:dl:" + s));
            var qty = h.Sender.Edits.Last();
            ((TgInlineKeyboard)qty.Markup!).InlineKeyboard.SelectMany(x => x).Should().Contain(b => b.Text == "✅ Xác nhận 98 suất" && b.CallbackData == "dlv:dq:" + s + ".98");
            await h.SendAsync(Tap("dlv:dq:" + s + ".98"));
            Buttons(h.Sender.Edits.Last().Markup).Should().Contain(["dlv:dv:" + s + ".98.65", "dlv:dv:" + s + ".98.-"]);
            await h.SendAsync(Tap("dlv:dv:" + s + ".98.65"));
            var call = Last(h, "api/DeliveryStop/Deliver");
            call.Form["Id"].Should().Be(Stop1.ToString());
            call.Form["DeliveredQuantity"].Should().Be("98");
            call.Form["TemperatureC"].Should().Be("65");
            call.Form["ReceiverName"].Should().Be("Cô Lan");
            call.Token.Should().Be("jwt-driver");
        }

        [Fact]
        public async Task Arrive_fail_with_reason_and_container_return_typed_count()
        {
            await using var h = DriverHost(Detail("InTransit", [], "Planned", ["arrive", "recordTemperature", "deliver", "fail"]), "#/log-stop", "#/log-packaging-ledger");
            string s = Stop1.ToString("N");
            await h.SendAsync(Tap("dlv:arr:" + s));
            Last(h, "api/DeliveryStop/Arrive").Form["Id"].Should().Be(Stop1.ToString());
            await h.SendAsync(Tap("dlv:fl:" + s));
            Buttons(h.Sender.Edits.Last().Markup).Should().Contain("dlv:fr:" + s + ".1");
            await h.SendAsync(Tap("dlv:fr:" + s + ".1"));
            Last(h, "api/DeliveryStop/Fail").Form["Reason"].Should().Be("Không liên lạc được người nhận (Telegram)");

            await using var h2 = DriverHost(Detail("InTransit", [], "Delivered", []), "#/log-stop", "#/log-packaging-ledger");
            await h2.SendAsync(Text("📥 Nhận hộp về"));
            string d = DeliveryOrder1.ToString("N");
            Buttons(h2.Sender.Sent.Last().Message.ReplyMarkup).Should().ContainSingle().Which.Should().Be("dlv:bx:" + d);
            // BOT-UX-2 (mục 6): số hộp = màn số (+10, +1, +1 → ✅ Ghi 12) – không gõ.
            await h2.SendAsync(Tap("dlv:bx:" + d));
            Buttons(h2.Sender.Edits.Last().Markup).Should().Contain(["dlv:bxn:" + d + ".10", "dlv:bxq:" + d + ".0"]);
            await h2.SendAsync(Tap("dlv:bxn:" + d + ".10"));
            await h2.SendAsync(Tap("dlv:bxn:" + d + ".11"));
            Buttons(h2.Sender.Edits.Last().Markup).Should().Contain(["dlv:bxn:" + d + ".12", "dlv:bxq:" + d + ".11"]);
            await h2.SendAsync(Tap("dlv:bxn:" + d + ".12"));
            h2.Web.Calls.Should().NotContain(c => c.Path == "api/ContainerReturn/Record", "đổi số trên màn số không ghi gì");
            await h2.SendAsync(Tap("dlv:bxq:" + d + ".12"));
            var call = Last(h2, "api/ContainerReturn/Record");
            call.Form["DeliveryOrderId"].Should().Be(DeliveryOrder1.ToString());
            call.Form["ReturnedQuantity"].Should().Be("12");
            h2.Sender.Sent.Last().Message.Text.Should().Contain("12 hộp");
        }

        [Fact]
        public async Task Dispatcher_board_and_hidden_functions_without_screen_permission()
        {
            await using var h = DriverHost(Detail("InTransit", [], "Planned", []), "#/log-route");
            await h.SendAsync(Text("/giaovan"));
            var board = h.Sender.Sent.Last().Message;
            board.Text.Should().Contain("1 chuyến").And.Contain("Tài xế Bình").And.Contain("0/2 điểm");
            Buttons(board.ReplyMarkup).Should().Contain("dlv:r:" + Route.ToString("N"));
            int before = h.Web.Calls.Count;
            await h.SendAsync(Text("/doisoat"));
            await h.SendAsync(Text("/hop"));
            h.Web.Calls.Count.Should().Be(before, "không có quyền màn đối soát / hộp khay → không gọi api");
        }

        [Fact]
        public async Task Point_of_service_reconcile_prefilled_with_pod_accepted_quantity()
        {
            await using var h = DriverHost(Detail("InTransit", [], "Delivered", []), "#/crm-reconciliation");
            string pending = JsonSerializer.Serialize(new[] { new { orderId = Order, orderNumber = "DH-261118-0007", customerName = "Trường A", orderedQuantity = 100m, deliveredQuantity = 98m, podAcceptedQuantity = 96m } });
            h.Web.Routes["api/OrderReconciliation/GetPendingOrders"] = (_, _) => (200, null, null, pending);
            h.Web.Routes["api/OrderReconciliation/Reconcile"] = (_, _) => (200, "Đã đối soát", "{\"orderNumber\":\"DH-261118-0007\",\"customerName\":\"Trường A\",\"billingQuantity\":96,\"billingBasis\":\"ACCEPTED\"}", null);
            await h.SendAsync(Text("/xeden"));
            h.Sender.Sent.Last().Message.Text.Should().Contain("10:30 Trường A").And.Contain("11:00 Công ty B");
            await h.SendAsync(Text("🧮 Đối soát suất"));
            string o = Order.ToString("N");
            Buttons(h.Sender.Sent.Last().Message.ReplyMarkup).Should().Contain("rcn:o:" + o);
            await h.SendAsync(Tap("rcn:o:" + o));
            ((TgInlineKeyboard)h.Sender.Edits.Last().Markup!).InlineKeyboard.SelectMany(x => x).Should().Contain(b => b.Text == "✅ Đối soát 96 suất" && b.CallbackData == "rcn:q:" + o + ".96");
            await h.SendAsync(Tap("rcn:q:" + o + ".96"));
            var call = Last(h, "api/OrderReconciliation/Reconcile");
            call.Form["OrderId"].Should().Be(Order.ToString());
            call.Form["ServedQuantity"].Should().Be("96");
            h.Sender.Sent.Last().Message.Text.Should().Contain("Đã đối soát").And.Contain("tính tiền 96 suất");
        }
    }
}
