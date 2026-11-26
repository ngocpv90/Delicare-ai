using System.Text.Json;
using AppDelicare.Messaging.Audiences.Internal.Chung;
using AppDelicare.Messaging.Core.Contracts;
using FluentAssertions;

namespace AppDelicare.Tests.Messaging
{
    /// <summary>
    /// UI-6.50 (D-132 h): QR <c>DOC_PO_</c> trên đơn mua in → bot mở gói nhận gộp của NCC (JWT người quét); [✓ Nhận đủ – Đạt tất cả] hỏi nhiệt độ thùng xe (có hàng mát)
    /// → GoodsReceipt/CreateMany đủ số còn phải nhận của đơn hẹn ≤ hôm nay + đơn quét; người không có quyền nhận hàng → chỉ xem thông tin đơn.
    /// </summary>
    public class MessagingReceiveUi650Tests
    {
        private static readonly Guid Po = Guid.Parse("65065065-0000-4000-8000-0000000000a1");
        private static readonly Guid Sup = Guid.Parse("65065065-0000-4000-8000-0000000000b1");
        private static readonly Guid Wh = Guid.Parse("65065065-0000-4000-8000-0000000000c1");
        private static long _u = 96500;

        private static string Text(string text, long from = 7650, long? replyTo = null)
        {
            long id = Interlocked.Increment(ref _u);
            string chat = "{\"id\":" + from + ",\"type\":\"private\"}";
            string reply = replyTo == null ? "" : ",\"reply_to_message\":{\"message_id\":" + replyTo + ",\"date\":1,\"chat\":" + chat + "}";
            return "{\"update_id\":" + id + ",\"message\":{\"message_id\":" + id + ",\"date\":" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ",\"from\":{\"id\":" + from
                + ",\"first_name\":\"Kho\"},\"chat\":" + chat + ",\"text\":" + JsonSerializer.Serialize(text) + reply + "}}";
        }

        private static string Tap(string data, long from = 7650)
        {
            long id = Interlocked.Increment(ref _u);
            return "{\"update_id\":" + id + ",\"callback_query\":{\"id\":\"cb" + id + "\",\"from\":{\"id\":" + from + "},\"data\":" + JsonSerializer.Serialize(data)
                + ",\"message\":{\"message_id\":50,\"date\":1,\"chat\":{\"id\":" + from + ",\"type\":\"private\"}}}}";
        }

        private static MessagingTestHost Host()
        {
            var h = new MessagingTestHost();
            h.Api.Identity = new BotIdentityModel { Linked = true, CanUseBot = true, HasWebAccount = true, UserId = Guid.NewGuid(), FullName = "Thủ kho", Status = "Active", Menus = ["#/scm-gr"] };
            h.Api.Token = new ApiResult<UserTokenModel> { ResponseCode = 200, Data = new UserTokenModel { Token = "jwt-kho" } };
            return h;
        }

        private static object Bundle() => new
        {
            supplierId = Sup, supplierName = "NCC Rau Sạch", defaultWarehouseId = Wh, needsVehicleTemperature = true,
            listOrder = new object[]
            {
                new { purchaseOrderId = Po, purchaseOrderNumber = "PO-0041", due = true, matched = true },
                new { purchaseOrderId = Guid.Parse("65065065-0000-4000-8000-0000000000a2"), purchaseOrderNumber = "PO-0046", due = true, matched = false },
                new { purchaseOrderId = Guid.Parse("65065065-0000-4000-8000-0000000000a3"), purchaseOrderNumber = "PO-0050", due = false, matched = false },
            },
            listLine = new object[]
            {
                new
                {
                    ingredientId = Guid.Parse("65065065-0000-4000-8000-0000000000d1"), ingredientName = "Rau cải", unitName = "kg", lotControl = false,
                    listPart = new object[]
                    {
                        new { purchaseOrderId = Po, purchaseOrderLineId = Guid.Parse("65065065-0000-4000-8000-0000000000e1"), remaining = 8m },
                        new { purchaseOrderId = Guid.Parse("65065065-0000-4000-8000-0000000000a2"), purchaseOrderLineId = Guid.Parse("65065065-0000-4000-8000-0000000000e2"), remaining = 4m },
                        new { purchaseOrderId = Guid.Parse("65065065-0000-4000-8000-0000000000a3"), purchaseOrderLineId = Guid.Parse("65065065-0000-4000-8000-0000000000e3"), remaining = 9m },
                    },
                },
            },
        };

        [Fact]
        public void Po_qr_is_a_known_document_type()
        {
            QrActions.Parse("DOC_PO_" + Po.ToString("N")).Should().Be(("DOC_", "PO_" + Po.ToString("N")));
            QrActions.DocOf("PO_" + Po.ToString("N")).Should().Be(("PO", Po));
        }

        [Fact]
        public async Task Po_qr_opens_supplier_bundle_and_receive_all_asks_vehicle_temperature()
        {
            await using var h = Host();
            h.Web.Routes["api/PurchaseOrder/GetReceivingBundle"] = (_, _) => (200, null, JsonSerializer.Serialize(Bundle()), null);
            h.Web.Routes["api/GoodsReceipt/CreateMany"] = (_, _) => (200, "Đã lập 2 phiếu nhập: 2 đã nhập kho, 0 chờ QA", JsonSerializer.Serialize(new { listReceipt = Array.Empty<object>(), listWarning = Array.Empty<string>() }), null);
            await h.SendAsync(Text("/start DOC_PO_" + Po.ToString("N")));
            h.Web.Calls.Should().Contain(c => c.Path == "api/PurchaseOrder/GetReceivingBundle?purchaseOrderId=" + Po && c.Token == "jwt-kho");
            var card = h.Sender.Sent.Last().Message.Text;
            card.Should().Contain("NCC Rau Sạch").And.Contain("Gộp 2 đơn: PO-0041, PO-0046").And.Contain("Rau cải: 12 kg (2 đơn)");

            await h.SendAsync(Tap("op:rca:P" + Po.ToString("N")));
            h.Sender.Sent.Last().Message.Text.Should().Contain("Nhiệt độ thùng xe");
            await h.SendAsync(Text("4,5", replyTo: 100 + h.Sender.Sent.Count));
            var post = h.Web.Calls.Single(c => c.Path == "api/GoodsReceipt/CreateMany");
            using var body = JsonDocument.Parse(post.Form["__json"]);
            body.RootElement.GetProperty("warehouseId").GetGuid().Should().Be(Wh);
            body.RootElement.GetProperty("vehicleTemperatureC").GetDecimal().Should().Be(4.5m);
            var line = body.RootElement.GetProperty("listLine")[0];
            line.GetProperty("receivedQuantity").GetDecimal().Should().Be(12m, "chỉ đơn hẹn ≤ hôm nay + đơn quét");
            line.GetProperty("listPurchaseOrderLineId").GetArrayLength().Should().Be(2);
            h.Sender.Sent.Last().Message.Text.Should().Contain("Đã lập 2 phiếu nhập");
        }

        [Fact]
        public async Task Po_qr_without_receiving_right_shows_order_info_only()
        {
            await using var h = Host();
            h.Web.Routes["api/PurchaseOrder/GetReceivingBundle"] = (_, _) => (403, "Không có quyền", null, null);
            h.Web.Routes["api/Common/GetPrintDocument"] = (_, _) => (200, null, JsonSerializer.Serialize(new { title = "ĐƠN ĐẶT HÀNG", documentNo = "PO-0041", listInfo = Array.Empty<object>(), listRow = Array.Empty<object>() }), null);
            await h.SendAsync(Text("/start DOC_PO_" + Po.ToString("N")));
            h.Web.Calls.Should().Contain(c => c.Path == $"api/Common/GetPrintDocument?type=PO&id={Po}");
            h.Sender.Sent.Last().Message.Text.Should().Contain("ĐƠN ĐẶT HÀNG").And.Contain("PO-0041");
            h.Web.Calls.Should().NotContain(c => c.Path == "api/GoodsReceipt/CreateMany");
        }
    }
}
