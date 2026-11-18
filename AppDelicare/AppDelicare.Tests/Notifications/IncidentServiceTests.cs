using AppDelicare.Data.Share;
using AppDelicare.Messaging.Presentation;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Model;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Notifications
{
    /// <summary>
    /// BOT-2.2 (BOT_TELEGRAM §5.2) – phía api, đồng hồ giả: tin sự việc có nút Nhận xử lý / Ghi chú / Đã xử lý; người bấm trước được ghi, người sau thấy tên;
    /// ghi chú + đã xử lý hiện trên tin của MỌI người liên quan (sửa tin, không thêm tin); chưa ai nhận → leo thang đúng mốc (10′ → COO ở bot DH), không trùng; đã nhận → dừng.
    /// </summary>
    public class IncidentServiceTests
    {
        private static readonly DateTime T0 = new(2026, 12, 15, 9, 0, 0);
        private static readonly Guid Vh = Guid.Parse("88888888-0000-4000-8000-0000000000a1");
        private static readonly Guid Dh = Guid.Parse("88888888-0000-4000-8000-0000000000a2");
        private static readonly Guid SystemUser = Guid.Parse("88888888-0000-4000-8000-0000000000ff");

        private static async Task<(Guid QaA, Guid QaB, Guid Coo)> SeedAsync(InMemoryApiFactory f)
        {
            Guid qaA = Guid.NewGuid(), qaB = Guid.NewGuid(), coo = Guid.NewGuid(), qaGroup = Guid.NewGuid(), cooGroup = Guid.NewGuid();
            await f.SeedAsync(async db =>
            {
                db.IntegrationChannel.AddRange(
                    new IntegrationChannel { Id = Vh, ChannelCode = "TELEGRAM_VH", ChannelName = "VH", ProviderCode = "TELEGRAM", EnvironmentCode = "Development", ChannelStatus = "Active", ConfigJson = "{}" },
                    new IntegrationChannel { Id = Dh, ChannelCode = "TELEGRAM_DH", ChannelName = "DH", ProviderCode = "TELEGRAM", EnvironmentCode = "Development", ChannelStatus = "Active", ConfigJson = "{}" });
                db.BotConfiguration.AddRange(
                    new BotConfiguration { Id = Guid.NewGuid(), IntegrationChannelId = Vh, BotCode = "VH", BotName = "Vận hành", BotStatus = "Active" },
                    new BotConfiguration { Id = Guid.NewGuid(), IntegrationChannelId = Dh, BotCode = "DH", BotName = "Điều hành", BotStatus = "Active" });
                db.User.Add(new User { Id = SystemUser, UserName = "system-inc", IsActive = true });
                var person = Guid.NewGuid();
                db.Person.Add(new Person { Id = person, FullName = "Chị Hoa QA" });
                db.User.AddRange(new User { Id = qaA, UserName = "qa-a", IsActive = true, PersonId = person }, new User { Id = qaB, UserName = "qa-b", IsActive = true },
                    new User { Id = coo, UserName = "coo-inc", IsActive = true });
                db.UserGroup.AddRange(new UserGroup { Id = qaGroup, Name = "QA-INC" }, new UserGroup { Id = cooGroup, Name = "COO" });
                db.UserClaim.AddRange(new UserClaim { Id = Guid.NewGuid(), UserId = qaA, UserGroupId = qaGroup }, new UserClaim { Id = Guid.NewGuid(), UserId = qaB, UserGroupId = qaGroup },
                    new UserClaim { Id = Guid.NewGuid(), UserId = coo, UserGroupId = cooGroup });
                var menu = await db.ObjectMenu.FirstOrDefaultAsync(m => m.MetaLink == "#/qc-checklist-result") ?? new ObjectMenu { Id = Guid.NewGuid(), Name = "QC", MetaLink = "#/qc-checklist-result", IsActive = true, LevelShow = 2 };
                if (db.Entry(menu).State == EntityState.Detached)
                {
                    db.ObjectMenu.Add(menu);
                }
                db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = qaGroup, ObjectMenuId = menu.Id, AllowAccess = true, AllowAdd = true });
                db.UserExternalIdentity.AddRange(
                    new UserExternalIdentity { Id = Guid.NewGuid(), UserId = qaA, IntegrationChannelId = Vh, ExternalUserId = "9301", LinkedAt = T0, IdentityStatus = "Active" },
                    new UserExternalIdentity { Id = Guid.NewGuid(), UserId = qaB, IntegrationChannelId = Vh, ExternalUserId = "9302", LinkedAt = T0, IdentityStatus = "Active" },
                    new UserExternalIdentity { Id = Guid.NewGuid(), UserId = coo, IntegrationChannelId = Dh, ExternalUserId = "9303", LinkedAt = T0, IdentityStatus = "Active" });
            });
            return (qaA, qaB, coo);
        }

        /// <summary>Phát 1 sự việc FOODSAFETY_CRITICAL (như checklist ATTP) rồi đánh dấu đã gửi (mã tin = 600 + thứ tự).</summary>
        private static async Task RaiseAsync(InMemoryApiFactory f)
        {
            using (var scope = f.Services.CreateScope())
            using (scope.ServiceProvider.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForSystem(SystemUser)))
            {
                await BusinessNotifier.ToMenusAsync(scope.ServiceProvider.GetRequiredService<INotificationService>(), scope.ServiceProvider.GetRequiredService<AppDelicare.Data.Connection.DatabaseContext>(),
                    NotificationCatalog.FoodSafetyCritical, ["#/qc-checklist-result"], new { title = "Checklist bếp nóng", lines = new[] { "❗ Nhiệt độ tủ mát" } }, "FOODSAFETY_CRITICAL:TEST-1");
            }
            await f.SeedAsync(async db =>
            {
                int n = 600;
                foreach (var m in await db.OutboundMessage.Where(x => x.MessageType == NotificationCatalog.FoodSafetyCritical).OrderBy(x => x.RecipientExternalId).ToListAsync())
                {
                    m.MessageStatus = "Sent";
                    m.ProviderMessageId = (++n).ToString();
                    m.QueuedAt = T0;
                }
            });
        }

        private static async Task<IncidentResultModel> ActAsync(InMemoryApiFactory f, string chat, string messageId, string action, string? text = null)
        {
            using var scope = f.Services.CreateScope();
            var r = await scope.ServiceProvider.GetRequiredService<IIncidentService>().Act(new IncidentActionRequest
            {
                BotCode = "VH", ExternalUserId = chat, ChatId = chat, ProviderMessageId = messageId, Action = action, Text = text,
            });
            r.ResponseCode.Should().Be(200, r.ResponseMessage);
            return r.Data!;
        }

        [Fact]
        public async Task First_claim_wins_notes_and_done_show_on_every_related_message()
        {
            using var f = new InMemoryApiFactory();
            f.Settings["SystemUsers:JobUserId"] = SystemUser.ToString();
            f.Clock.FixedNow = T0;
            await SeedAsync(f);
            await RaiseAsync(f);
            var sent = await f.QueryAsync(db => db.OutboundMessage.Where(m => m.MessageType == NotificationCatalog.FoodSafetyCritical).OrderBy(m => m.RecipientExternalId).ToListAsync());
            sent.Select(m => m.RecipientExternalId).Should().Equal("9301", "9302");
            MessageTemplates.Render(sent[0].PayloadJson).Buttons.Select(b => b.Callback).Should().Equal("inc:claim", "inc:note", "inc:done");

            var claim = await ActAsync(f, "9301", "601", "claim");
            claim.Changed.Should().BeTrue();
            claim.ListEdit.Select(e => e.ChatId).Should().BeEquivalentTo(["9301", "9302"], "sửa thẻ của mọi người liên quan");
            var seenByB = MessageTemplates.Render(claim.ListEdit.Single(e => e.ChatId == "9302").PayloadJson);
            seenByB.Lines.Should().Contain("🙋 Chị Hoa QA đã nhận xử lý lúc 09:00");
            seenByB.Buttons.Select(b => b.Callback).Should().Equal("inc:note", "inc:done");

            var late = await ActAsync(f, "9302", "602", "claim");
            late.Changed.Should().BeFalse();
            late.Note.Should().Contain("Chị Hoa QA").And.Contain("09:00");
            late.ListEdit.Should().BeEmpty();

            f.Clock.FixedNow = T0.AddMinutes(5);
            var note = await ActAsync(f, "9302", "602", "note", "Đã chuyển hàng sang tủ 2");
            MessageTemplates.Render(note.ListEdit.Single(e => e.ChatId == "9301").PayloadJson).Lines.Should().Contain("💬 09:05 qa-b: Đã chuyển hàng sang tủ 2");
            var done = await ActAsync(f, "9302", "602", "done");
            var final = MessageTemplates.Render(done.ListEdit.Single(e => e.ChatId == "9301").PayloadJson);
            final.Lines.Should().Contain("✅ Đã xử lý – qa-b lúc 09:05");
            final.Buttons.Should().BeEmpty();
            (await f.QueryAsync(db => db.OutboundMessage.CountAsync())).Should().Be(2, "không thêm tin mới");
        }

        [Fact]
        public async Task Unclaimed_incident_escalates_on_time_once_and_stops_after_claim()
        {
            using var f = new InMemoryApiFactory();
            f.Settings["SystemUsers:JobUserId"] = SystemUser.ToString();
            f.Clock.FixedNow = T0;
            await SeedAsync(f);
            await RaiseAsync(f);
            async Task<int> RunAsync()
            {
                using var scope = f.Services.CreateScope();
                return (await scope.ServiceProvider.GetRequiredService<IIncidentService>().RunEscalation()).Data;
            }
            Task<List<OutboundMessage>> EscalatedAsync() => f.QueryAsync(db => db.OutboundMessage.Where(m => m.MessageType == NotificationCatalog.IncidentUnclaimed).ToListAsync());

            f.Clock.FixedNow = T0.AddMinutes(9);
            (await RunAsync()).Should().Be(0, "chưa đủ 10′");
            f.Clock.FixedNow = T0.AddMinutes(11);
            (await RunAsync()).Should().Be(1);
            var esc = (await EscalatedAsync()).Should().ContainSingle().Subject;
            esc.RecipientExternalId.Should().Be("9303", "bậc 2 = COO ở bot DH");
            esc.IntegrationChannelId.Should().Be(Dh);
            esc.CorrelationId.Should().Be("FOODSAFETY_CRITICAL:TEST-1", "cùng sự việc – nhận / ghi chú đồng bộ");
            var m = MessageTemplates.Render(esc.PayloadJson);
            m.Title.Should().Be("🚨 Sự việc chưa ai nhận");
            m.Lines.Should().Contain("Checklist bếp nóng").And.Contain(l => l.Contains("Chưa ai nhận xử lý sau 10′"));
            (await RunAsync()).Should().Be(0, "không gửi trùng bậc");

            await ActAsync(f, "9301", "601", "claim");
            f.Clock.FixedNow = T0.AddMinutes(40);
            (await RunAsync()).Should().Be(0, "đã có người nhận → dừng leo thang");
            (await EscalatedAsync()).Should().HaveCount(1);
        }
    }
}
