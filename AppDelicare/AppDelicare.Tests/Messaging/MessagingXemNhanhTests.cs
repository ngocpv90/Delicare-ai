using System.Text.Json;
using System.Text.RegularExpressions;
using AppDelicare.Messaging.Audiences.Internal;
using AppDelicare.Messaging.Core;
using AppDelicare.Messaging.Core.Contracts;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Messaging
{
    /// <summary>
    /// BOT-1.5c-2: chức năng chỉ xem (Kho, Kế hoạch, Kinh doanh, Kế toán, QA, Quản trị) – gọi đúng API bằng JWT người dùng, tóm tắt ≤ 15 dòng, ẩn khi không có quyền màn;
    /// toàn bộ danh mục chức năng: mã / lệnh không trùng, lệnh hợp lệ với Telegram, phân hệ có trong danh sách.
    /// </summary>
    public class MessagingXemNhanhTests
    {
        private static long _u = 97000;

        private static string Text(string text, long from = 7401)
        {
            long id = Interlocked.Increment(ref _u);
            return "{\"update_id\":" + id + ",\"message\":{\"message_id\":" + id + ",\"date\":" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ",\"from\":{\"id\":" + from
                + ",\"first_name\":\"Kho\"},\"chat\":{\"id\":" + from + ",\"type\":\"private\"},\"text\":" + JsonSerializer.Serialize(text) + "}}";
        }

        private static MessagingTestHost Host(bool admin = false, params string[] menus)
        {
            var h = new MessagingTestHost();
            h.Api.Identity = new BotIdentityModel { Linked = true, CanUseBot = true, HasWebAccount = true, IsAdmin = admin, UserId = Guid.NewGuid(), FullName = "Thủ kho", Status = "Active", Menus = [.. menus] };
            h.Api.Token = new ApiResult<UserTokenModel> { ResponseCode = 200, Data = new UserTokenModel { Token = "jwt-kho" } };
            return h;
        }

        [Fact]
        public async Task Action_catalog_codes_and_commands_are_unique_and_valid()
        {
            await using var h = new MessagingTestHost();
            var all = h.App.Services.GetRequiredService<BotActionRegistry>().All;
            all.Select(a => a.Code).Should().OnlyHaveUniqueItems();
            all.Where(a => a.Command != null).Select(a => a.Command).Should().OnlyHaveUniqueItems().And.OnlyContain(c => Regex.IsMatch(c!, "^[a-z0-9_]{1,32}$"));
            all.Select(a => a.Module).Should().OnlyContain(m => BotActionRegistry.Modules.Any(x => x.Code == m));
            all.Should().Contain(a => a.Module == "KHO").And.Contain(a => a.Module == "KEHOACH").And.Contain(a => a.Module == "KINHDOANH").And.Contain(a => a.Module == "KETOAN").And.Contain(a => a.Module == "QUANTRI");
        }

        [Fact]
        public async Task Shortage_tomorrow_lists_shortage_first_and_caps_lines()
        {
            await using var h = Host(false, "#/scm-requirement");
            var items = Enumerable.Range(1, 20).Select(i => new { ingredientName = "NL" + i, unitName = "kg", requiredQuantity = 10m, availableQuantity = 12m, shortageQuantity = 0m }).ToList();
            items.Insert(7, new { ingredientName = "Thịt heo", unitName = "kg", requiredQuantity = 50m, availableQuantity = 30m, shortageQuantity = 20m });
            h.Web.Routes["api/MaterialRequirement/GetByDate"] = (_, _) => (200, null, JsonSerializer.Serialize(new { planNumber = "KH-1", totalMeals = 900, shortageCount = 1, listItem = items }), null);
            await h.SendAsync(Text("/nlmai"));
            var call = h.Web.Calls.Last(c => c.Path.StartsWith("api/MaterialRequirement/GetByDate"));
            call.Path.Should().Contain("productionDate=" + VnTime.Today(TimeProvider.System).AddDays(1).ToString("yyyy-MM-dd"));
            call.Token.Should().Be("jwt-kho");
            string text = h.Sender.Sent.Last().Message.Text;
            text.Should().Contain("⚠️ Thịt heo: cần 50 kg · có 30 · thiếu 20").And.Contain("KH-1 · 900 suất").And.Contain("dòng nữa");
            text.IndexOf("Thịt heo", StringComparison.Ordinal).Should().BeLessThan(text.IndexOf("NL1:", StringComparison.Ordinal));
        }

        [Fact]
        public async Task Orders_not_confirmed_and_po_overdue()
        {
            await using var h = Host(false, "#/crm-order", "#/scm-po");
            h.Web.Routes["api/Order/GetBoard"] = (_, _) => (200, null, JsonSerializer.Serialize(new
            {
                listColumn = new object[]
                {
                    new { status = "Draft", statusName = "Nháp", count = 1, totalQuantity = 100m, listItem = new[] { new { orderNumber = "DH-1", customerName = "Trường A", totalQuantity = 100m } } },
                    new { status = "Confirmed", statusName = "Đã xác nhận", count = 2, totalQuantity = 300m, listItem = new[] { new { orderNumber = "DH-2", customerName = "Công ty B", totalQuantity = 150m } } },
                },
            }), null);
            DateTime today = VnTime.Today(TimeProvider.System);
            h.Web.Routes["api/PurchaseOrder/GetAll"] = (path, _) => (200, null, null, path.Contains("status=Sent")
                ? JsonSerializer.Serialize(new object[]
                {
                    new { purchaseOrderNumber = "PO-1", supplierName = "NCC Rau", expectedDeliveryDate = today.AddDays(-1).ToString("yyyy-MM-ddT00:00:00"), status = "Sent" },
                    new { purchaseOrderNumber = "PO-9", supplierName = "NCC Xa", expectedDeliveryDate = today.AddDays(9).ToString("yyyy-MM-ddT00:00:00"), status = "Sent" },
                })
                : "[]");
            await h.SendAsync(Text("/chuachot"));
            h.Sender.Sent.Last().Message.Text.Should().Contain("DH-1 Trường A").And.NotContain("DH-2");
            await h.SendAsync(Text("/donmai"));
            h.Sender.Sent.Last().Message.Text.Should().Contain("Nháp: 1 đơn · 100 suất").And.Contain("Đã xác nhận: 2 đơn · 300 suất");
            await h.SendAsync(Text("/po"));
            h.Sender.Sent.Last().Message.Text.Should().Contain("🔴 trễ PO-1 NCC Rau").And.NotContain("PO-9");
        }

        [Fact]
        public async Task System_status_admin_only_and_views_hidden_without_menu()
        {
            await using var h = Host(true);
            h.Web.Routes["api/Job/GetAll"] = (_, _) => (200, null, null, JsonSerializer.Serialize(new[] { new { jobCode = "MRP", jobName = "Tính MRP", enabled = true, lastRun = new { runStatus = "Failed", startedAt = "2031-06-02T20:30:00" } } }));
            h.Web.Routes["api/OutboundMessage/GetAll"] = (_, _) => (200, null, null, "[]");
            await h.SendAsync(Text("/hethong"));
            h.Sender.Sent.Last().Message.Text.Should().Contain("🔴 Tính MRP · Failed");

            await using var plain = Host(false);
            await plain.SendAsync(Text("/hethong"));
            await plain.SendAsync(Text("/ton"));
            plain.Web.Calls.Should().BeEmpty("không phải admin / không có quyền màn tồn kho");
        }
    }
}
