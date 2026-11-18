using System.Text.Json;
using AppDelicare.Messaging.Channels.Telegram.Models;
using AppDelicare.Messaging.Core.Contracts;
using FluentAssertions;

namespace AppDelicare.Tests.Messaging
{
    /// <summary>
    /// BOT-1.6 (BOT_TELEGRAM §3.3): bot Điều hành – 📊 tổng quan (Dashboard/GetExecutive), 🎯 KPI (KpiSnapshot/GetDaily), 🚨 việc đang mở (mọi cảnh báo người dùng được xem),
    /// 📈 tiến độ bếp / 🗺 giao vận / 📦 tồn & thiếu hụt dùng lại chức năng VH; chức năng DH chỉ ở bot DH (bot VH không có), ⏳ chờ tôi duyệt + phần chung có ở cả hai.
    /// </summary>
    public class MessagingDieuHanhTests
    {
        private static long _u = 99000;

        private static string Text(string text, long from = 7501)
        {
            long id = Interlocked.Increment(ref _u);
            return "{\"update_id\":" + id + ",\"message\":{\"message_id\":" + id + ",\"date\":" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ",\"from\":{\"id\":" + from
                + ",\"first_name\":\"Ceo\"},\"chat\":{\"id\":" + from + ",\"type\":\"private\"},\"text\":" + JsonSerializer.Serialize(text) + "}}";
        }

        private static MessagingTestHost Host(params string[] menus)
        {
            var h = new MessagingTestHost();
            h.Api.Identity = new BotIdentityModel
            {
                Linked = true, CanUseBot = true, CanUseDieuHanh = true, HasWebAccount = true, UserId = Guid.NewGuid(), FullName = "CEO", Status = "Active", Menus = [.. menus],
                ApproveScreens = ["wf-inbox"],
            };
            h.Api.Token = new ApiResult<UserTokenModel> { ResponseCode = 200, Data = new UserTokenModel { Token = "jwt-ceo" } };
            return h;
        }

        [Fact]
        public async Task Overview_shows_production_delivery_staffing_and_alerts()
        {
            await using var h = Host("#/dash-exec");
            h.Web.Routes["api/Dashboard/GetExecutive"] = (_, _) => (200, null, JsonSerializer.Serialize(new
            {
                kpi = new
                {
                    plannedMeals = 3200m, orders = new { total = 12 }, productionProgress = new { percent = 62.5m, donePortions = 2000m, plannedPortions = 3200m, batchCount = 9 },
                    delivery = new { delivered = 4, total = 10 }, staffing = new { checkedIn = 40, assigned = 45 }, qcDetail = new { batchHold = 1, batchReject = 0, lotOnHold = 2 },
                    stockAlerts = 3, openComplaints = 5,
                },
                shiftProgress = new[] { new { shiftId = Guid.NewGuid(), code = "SANG", name = "Ca sáng", plannedPortions = 1600m, donePortions = 1600m } },
                alerts = new[] { new { code = "STOCK_BELOW_MIN", severity = "High", title = "Tồn dưới mức", count = 3 } },
            }), null);
            await h.SendAsync(Text("/tongquan"), "DH");
            var call = h.Web.Calls.Last(c => c.Path.StartsWith("api/Dashboard/GetExecutive"));
            call.Token.Should().Be("jwt-ceo");
            string text = h.Sender.Sent.Last().Message.Text;
            text.Should().Contain("Sản xuất: 2,000 / 3,200 suất (62.5%) · 9 mẻ").And.Contain("Giao: 4 / 10 điểm").And.Contain("Vào ca: 40 / 45")
                .And.Contain("Ca sáng: KH 1600 · xong 1600").And.Contain("🔴 Tồn dưới mức: 3");
        }

        [Fact]
        public async Task Kpi_daily_marks_met_and_missed_targets()
        {
            await using var h = Host("#/kpi-daily");
            h.Web.Routes["api/KpiSnapshot/GetDaily"] = (_, _) => (200, null, JsonSerializer.Serialize(new
            {
                metrics = new object[]
                {
                    new { definition = new { metricCode = "ONTIME", metricName = "Giao đúng giờ", unitCode = "%" }, latest = new { value = 97.5m, target = 95m, met = true } },
                    new { definition = new { metricCode = "WASTE", metricName = "Tỷ lệ hao hụt", unitCode = "%", targetValue = 3m }, latest = new { value = 4.2m, target = 3m, met = false } },
                    new { definition = new { metricCode = "NEW", metricName = "Chỉ tiêu mới" }, latest = (object?)null },
                },
            }), null);
            await h.SendAsync(Text("/kpi"), "DH");
            h.Web.Calls.Last(c => c.Path.StartsWith("api/KpiSnapshot/GetDaily")).Path.Should().Contain("From=").And.Contain("To=");
            string text = h.Sender.Sent.Last().Message.Text;
            text.Should().Contain("✅ Giao đúng giờ: 97.5 / mục tiêu 95 %").And.Contain("⚠️ Tỷ lệ hao hụt: 4.2 / mục tiêu 3 %").And.Contain("⚪ Chỉ tiêu mới: chưa có");
        }

        [Fact]
        public async Task Open_items_show_every_alert_and_dh_functions_only_on_dh_bot()
        {
            await using var h = Host("#/dash-exec", "#/kpi-daily", "#/prod-batch");
            h.Web.Routes["api/Common/GetAlerts"] = (_, _) => (200, null, null, JsonSerializer.Serialize(new object[]
            {
                new { code = "COMPLAINT_OVERDUE", severity = "High", title = "Phản ánh quá hạn", count = 2 },
                new { code = "APPROVAL_PENDING", severity = "Medium", title = "Chờ duyệt", count = 4 },
            }));
            await h.SendAsync(Text("/viecmo"), "DH");
            h.Sender.Sent.Last().Message.Text.Should().Contain("🔴 Phản ánh quá hạn: 2").And.Contain("🟡 Chờ duyệt: 4");

            await h.SendAsync(Text("/menu"), "DH");
            var modules = ((TgInlineKeyboard)h.Sender.Sent.Last().Message.ReplyMarkup!).InlineKeyboard.SelectMany(r => r).Select(b => b.CallbackData).ToList();
            modules.Should().Contain(["menu:mod:DIEUHANH", "menu:mod:DUYET", "menu:mod:CHUNG"]).And.NotContain("menu:mod:BEP");

            int before = h.Web.Calls.Count;
            await h.SendAsync(Text("/tongquan"), "VH");
            h.Web.Calls.Count.Should().Be(before, "chức năng DH không có trên bot VH");
        }
    }
}
