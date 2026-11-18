using System.Globalization;
using System.Text.Json;
using AppDelicare.Messaging.Audiences.Internal.Chung;
using AppDelicare.Messaging.Audiences.Internal.VanHanh;
using AppDelicare.Messaging.Channels.Telegram.Models;
using AppDelicare.Messaging.Core.Contracts;
using FluentAssertions;

namespace AppDelicare.Tests.Messaging
{
    /// <summary>
    /// BOT-2.4 (BOT_TELEGRAM §1.3, §6): QR vật thể mở đúng việc bằng JWT của người quét (LINE_ → mẻ của dây chuyền / bàn nấu, VEH_ → chuyến của xe,
    /// LOC_ → tồn tại kệ, người chưa liên kết → đăng ký); vị trí trực tiếp tài xế: rời bếp → tự ghi xuất phát (đã đo nhiệt độ), vào bán kính điểm giao → tự ghi Đã đến
    /// đúng 1 lần kèm tọa độ, gửi thẻ điểm; vị trí gửi 1 lần vẫn là chấm công.
    /// </summary>
    public class MessagingQrLiveTests
    {
        private static readonly Guid Route = Guid.Parse("44444444-0000-4000-8000-0000000000a1");
        private static readonly Guid Stop1 = Guid.Parse("44444444-0000-4000-8000-0000000000b1");
        private static readonly Guid Batch = Guid.Parse("44444444-0000-4000-8000-0000000000c1");
        private const double KitchenLat = 21.0000, KitchenLng = 105.8000, StopLat = 21.0300, StopLng = 105.8300;
        private static long _u = 91000;

        private static string Text(string text, long from = 7401)
        {
            long id = Interlocked.Increment(ref _u);
            return "{\"update_id\":" + id + ",\"message\":{\"message_id\":" + id + ",\"date\":" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ",\"from\":{\"id\":" + from
                + ",\"first_name\":\"Tai\"},\"chat\":{\"id\":" + from + ",\"type\":\"private\"},\"text\":" + JsonSerializer.Serialize(text) + "}}";
        }

        /// <summary>Vị trí trực tiếp: tin đầu (message + live_period) hoặc cập nhật (edited_message, giữ message_id + date gốc).</summary>
        private static string Live(double lat, double lng, bool edited, long from = 7401)
        {
            long id = Interlocked.Increment(ref _u);
            string loc = "{\"latitude\":" + lat.ToString(CultureInfo.InvariantCulture) + ",\"longitude\":" + lng.ToString(CultureInfo.InvariantCulture) + ",\"live_period\":3600}";
            return "{\"update_id\":" + id + ",\"" + (edited ? "edited_message" : "message") + "\":{\"message_id\":555,\"date\":1700000000,\"edit_date\":" + DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                + ",\"from\":{\"id\":" + from + ",\"first_name\":\"Tai\"},\"chat\":{\"id\":" + from + ",\"type\":\"private\"},\"location\":" + loc + "}}";
        }

        private static string Board(string routeStatus, decimal? departureTemp, string stopStatus = "Planned")
            => JsonSerializer.Serialize(new
            {
                listRoute = new object[]
                {
                    new
                    {
                        route = new { id = Route, routeNo = "CX-24", status = routeStatus, plannedStart = "2031-06-02T09:30:00" },
                        statusName = routeStatus, vehicleCode = "XE01", licensePlate = "29C-123.45", deliveryTempMinC = 60m, departureTemperatureC = departureTemp,
                        allowedActions = routeStatus == "InTransit" ? Array.Empty<string>() : ["dispatch"], listBlocker = Array.Empty<string>(),
                        listStop = new object[]
                        {
                            new { id = Stop1, sequenceNo = 1, stopStatus, statusName = stopStatus, customerName = "Trường A", latitude = (decimal)StopLat, longitude = (decimal)StopLng,
                                plannedArrival = "2031-06-02T10:30:00", plannedMealQty = 100m, allowedActions = stopStatus == "Planned" ? ["arrive"] : new[] { "deliver", "fail" } },
                        },
                    },
                },
                listKitchen = new[] { new { code = "BEP1", latitude = (decimal)KitchenLat, longitude = (decimal)KitchenLng, radiusMeters = 0m } },
            });

        private static MessagingTestHost Host(Func<string> board)
        {
            var h = new MessagingTestHost();
            h.Api.Identity = new BotIdentityModel { Linked = true, CanUseBot = true, HasWebAccount = true, UserId = Guid.NewGuid(), FullName = "Tài xế", Status = "Active", Menus = ["#/log-stop", "#/log-route", "#/prod-batch", "#/scm-inv-balance"] };
            h.Api.Token = new ApiResult<UserTokenModel> { ResponseCode = 200, Data = new UserTokenModel { Token = "jwt-qr" } };
            h.Web.Routes["api/DeliveryRoute/GetMine"] = (_, _) => (200, null, board(), null);
            h.Web.Routes["api/DeliveryRoute/GetBoard"] = (_, _) => (200, null, board(), null);
            h.Web.Routes["api/DeliveryRoute/GetDetail"] = (_, _) => (200, null, JsonDocument.Parse(board()).RootElement.GetProperty("listRoute")[0].GetRawText(), null);
            h.Web.Routes["api/DeliveryStop/GetById"] = (_, _) => (200, null, "{\"id\":\"" + Stop1 + "\",\"deliveryRouteId\":\"" + Route + "\"}", null);
            h.Web.Routes["api/DeliveryRoute/Dispatch"] = (_, _) => (200, "Đã xuất phát", "{}", null);
            h.Web.Routes["api/DeliveryStop/Arrive"] = (_, _) => (200, "Đã đến", "{}", null);
            return h;
        }

        private static List<string> Buttons(object? markup) => [.. ((TgInlineKeyboard)markup!).InlineKeyboard.SelectMany(r => r).Select(b => b.CallbackData ?? b.Url ?? "")];

        [Fact]
        public void Qr_parameters_are_parsed_strictly()
        {
            QrActions.Parse("LINE_BEP-01").Should().Be(("LINE_", "BEP-01"));
            QrActions.Parse("veh_XE01").Should().Be(("VEH_", "XE01"));
            QrActions.Parse("CLEAN").Should().Be(("CLEAN", ""));
            QrActions.Parse("LOC_").Should().BeNull();
            QrActions.Parse("LOC_K1;DROP").Should().BeNull("chỉ chữ / số / - _");
            QrActions.Parse("CHAMCONG").Should().BeNull();
            GiaoVanActions.Distance(KitchenLat, KitchenLng, (decimal)KitchenLat, (decimal)(KitchenLng + 0.001)).Should().BeApproximately(103.8, 1, "0.001° kinh độ ở vĩ độ 21 ≈ 104 m");
        }

        [Fact]
        public async Task Line_qr_opens_the_running_batch_of_that_line_with_start_button()
        {
            await using var h = Host(() => Board("Planned", null));
            string batch = "{\"listBatch\":[{\"id\":\"" + Batch + "\",\"batchNumber\":\"ME-24\",\"dishName\":\"Gà kho\",\"status\":\"Planned\",\"statusName\":\"Kế hoạch\",\"plannedStart\":\"2031-06-02T08:00:00\",\"plannedQuantity\":100}]}";
            h.Web.Routes["api/ProductionBatch/GetBoard"] = (_, _) => (200, null, batch, null);
            await h.SendAsync(Text("/start LINE_BEP1"));
            h.Web.Calls.Should().Contain(c => c.Path.StartsWith("api/ProductionBatch/GetBoard") && c.Path.Contains("LineCode=BEP1") && c.Token == "jwt-qr");
            var card = h.Sender.Sent.Last().Message;
            card.Text.Should().Contain("ME-24").And.Contain("BEP1");
            Buttons(card.ReplyMarkup).Should().Contain("bat:start:" + Batch.ToString("N"));
            h.Api.Calls.OfType<BotLinkByCodeRequest>().Should().BeEmpty("tham số QR không bị hiểu nhầm là mã liên kết");
        }

        [Fact]
        public async Task Vehicle_and_location_qr_open_the_trip_and_the_shelf_stock()
        {
            await using var h = Host(() => Board("Loading", null));
            await h.SendAsync(Text("/start VEH_29C12345"));
            var trip = h.Sender.Sent.Last().Message;
            trip.Text.Should().Contain("CX-24");
            Buttons(trip.ReplyMarkup).Should().Contain("dlv:dsp:" + Route.ToString("N"), "biển số khớp khi bỏ dấu");

            h.Web.Routes["api/InventoryBalance/GetByLot"] = (_, _) => (200, null,
                "[{\"ingredientName\":\"Thịt gà\",\"lotNumber\":\"L1\",\"expiryDate\":\"2031-06-03\",\"availableQuantity\":12,\"unitName\":\"kg\",\"isExpiring\":true}]", null);
            await h.SendAsync(Text("/start LOC_K1"));
            h.Web.Calls.Should().Contain(c => c.Path == "api/InventoryBalance/GetByLot?storageLocationCode=K1");
            h.Sender.Sent.Last().Message.Text.Should().Contain("Vị trí K1").And.Contain("Thịt gà");
        }

        [Fact]
        public async Task Unlinked_user_scanning_a_qr_is_asked_to_register_first()
        {
            await using var h = new MessagingTestHost();
            h.Api.Identity = new BotIdentityModel { Linked = false };
            await h.SendAsync(Text("/start VEH_XE01"));
            h.Api.Calls.OfType<BotLinkByCodeRequest>().Should().BeEmpty();
            h.Sender.Sent.Last().Message.Text.Should().Contain("liên kết");
        }

        [Fact]
        public async Task Live_location_departs_on_leaving_kitchen_and_arrives_once_inside_stop_radius()
        {
            string state = "Loading";
            bool arrived = false;
            await using var h = Host(() => Board(state, 65m, state == "InTransit" && arrived ? "Arrived" : "Planned"));
            await h.SendAsync(Live(KitchenLat, KitchenLng, edited: false));
            h.Sender.Sent.Last().Message.Text.Should().Contain("Đã nhận vị trí trực tiếp");
            h.Web.Calls.Should().NotContain(c => c.Path.StartsWith("api/DeliveryRoute/Dispatch"), "còn trong bếp");

            await h.SendAsync(Live(KitchenLat + 0.01, KitchenLng, edited: true)); // ~1,1 km – đã rời bếp
            var dispatch = h.Web.Calls.Single(c => c.Path == "api/DeliveryRoute/Dispatch");
            dispatch.Form["Id"].Should().Be(Route.ToString());
            dispatch.Form.Should().ContainKey("Latitude");
            state = "InTransit";

            await h.SendAsync(Live(StopLat + 0.0005, StopLng, edited: true)); // ~55 m tới điểm giao
            var arrive = h.Web.Calls.Single(c => c.Path == "api/DeliveryStop/Arrive");
            arrive.Form["Id"].Should().Be(Stop1.ToString());
            h.Sender.Sent.Last().Message.Text.Should().Contain("Đã tự ghi đến điểm");
            arrived = true;
            await h.SendAsync(Live(StopLat, StopLng, edited: true));
            h.Web.Calls.Count(c => c.Path == "api/DeliveryStop/Arrive").Should().Be(1, "vào bán kính chỉ ghi Đã đến 1 lần");
            h.Api.Calls.Should().NotContain(c => c.GetType().Name.Contains("Attendance"), "vị trí trực tiếp không phải chấm công");
        }

    }
}
