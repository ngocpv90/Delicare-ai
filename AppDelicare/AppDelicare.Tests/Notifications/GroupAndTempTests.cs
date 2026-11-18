using System.Text.Json;
using AppDelicare.Data.Share;
using AppDelicare.Messaging.Presentation;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Notifications
{
    /// <summary>
    /// BOT-1.7d – phía api: nhóm chat theo ConfigJson.groups (replace: 1 tin vào nhóm thay tin riêng trên cùng bot; copy: cả hai; tin duyệt / cá nhân không bao giờ vào nhóm;
    /// gọi lại không trùng); DELIVERY_TEMP_LOW khi nhiệt độ giao dưới ngưỡng (kèm khắc phục) → điều phối, sự việc có nút Nhận xử lý; đạt ngưỡng → không báo.
    /// </summary>
    public class GroupAndTempTests
    {
        private static readonly Guid Vh = Guid.Parse("66666666-0000-4000-8000-0000000000a1");
        private static readonly Guid Dh = Guid.Parse("66666666-0000-4000-8000-0000000000a2");

        private static async Task<(Guid Worker, Guid Dispatcher)> SeedAsync(InMemoryApiFactory f, string vhConfig)
        {
            Guid worker = Guid.NewGuid(), dispatcher = Guid.NewGuid(), group = Guid.NewGuid();
            await f.SeedAsync(async db =>
            {
                db.IntegrationChannel.AddRange(
                    new IntegrationChannel { Id = Vh, ChannelCode = "TELEGRAM_VH", ChannelName = "VH", ProviderCode = "TELEGRAM", EnvironmentCode = "Development", ChannelStatus = "Active", ConfigJson = vhConfig },
                    new IntegrationChannel { Id = Dh, ChannelCode = "TELEGRAM_DH", ChannelName = "DH", ProviderCode = "TELEGRAM", EnvironmentCode = "Development", ChannelStatus = "Active", ConfigJson = "{}" });
                db.BotConfiguration.AddRange(
                    new BotConfiguration { Id = Guid.NewGuid(), IntegrationChannelId = Vh, BotCode = "VH", BotName = "Vận hành", BotStatus = "Active" },
                    new BotConfiguration { Id = Guid.NewGuid(), IntegrationChannelId = Dh, BotCode = "DH", BotName = "Điều hành", BotStatus = "Active" });
                db.User.AddRange(new User { Id = worker, UserName = "g-worker", IsActive = true }, new User { Id = dispatcher, UserName = "g-dispatch", IsActive = true });
                db.UserGroup.Add(new UserGroup { Id = group, Name = "G-17D" });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = dispatcher, UserGroupId = group });
                var menu = await db.ObjectMenu.FirstOrDefaultAsync(m => m.MetaLink == "#/log-route") ?? new ObjectMenu { Id = Guid.NewGuid(), Name = "Chuyến", MetaLink = "#/log-route", IsActive = true, LevelShow = 2 };
                if (db.Entry(menu).State == EntityState.Detached)
                {
                    db.ObjectMenu.Add(menu);
                }
                db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = group, ObjectMenuId = menu.Id, AllowAccess = true, AllowAdd = true, AllowEdit = true });
                db.UserExternalIdentity.AddRange(
                    new UserExternalIdentity { Id = Guid.NewGuid(), UserId = worker, IntegrationChannelId = Vh, ExternalUserId = "1701", LinkedAt = DateTime.Today, IdentityStatus = "Active" },
                    new UserExternalIdentity { Id = Guid.NewGuid(), UserId = dispatcher, IntegrationChannelId = Vh, ExternalUserId = "1702", LinkedAt = DateTime.Today, IdentityStatus = "Active" });
            });
            return (worker, dispatcher);
        }

        private static async Task EnqueueAsync(InMemoryApiFactory f, string template, string key, params Guid[] users)
        {
            using var scope = f.Services.CreateScope();
            using var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForSystem(users[0]));
            await scope.ServiceProvider.GetRequiredService<INotificationService>().EnqueueAsync(new NotificationRequest { Template = template, UserIds = [.. users], Data = new { title = "Thử" }, IdempotencyKey = key });
        }

        private static Task<List<string>> RecipientsAsync(InMemoryApiFactory f, string key)
            => f.QueryAsync(db => db.OutboundMessage.Where(m => m.IdempotencyKey!.StartsWith(key + ":")).Select(m => m.RecipientExternalId!).ToListAsync());

        private static string Config(string mode, params string[] templates)
            => JsonSerializer.Serialize(new { groups = new[] { new { code = "BEP", chatId = "-100500", templates, mode } } });

        [Fact]
        public async Task Group_replaces_or_copies_department_messages_but_never_takes_approvals()
        {
            using var f = new InMemoryApiFactory();
            var (worker, dispatcher) = await SeedAsync(f, Config("replace", NotificationCatalog.ChecklistDue, NotificationCatalog.ApprovalPending));
            await EnqueueAsync(f, NotificationCatalog.ChecklistDue, "G1", worker, dispatcher);
            (await RecipientsAsync(f, "G1")).Should().Equal(["-100500"], "replace: 1 tin vào nhóm, không gửi riêng trên bot VH");
            await EnqueueAsync(f, NotificationCatalog.ChecklistDue, "G1", worker, dispatcher);
            (await RecipientsAsync(f, "G1")).Should().HaveCount(1, "gọi lại không trùng");
            await EnqueueAsync(f, NotificationCatalog.ApprovalPending, "G2", worker);
            (await RecipientsAsync(f, "G2")).Should().Equal(["1701"], "tin duyệt không bao giờ vào nhóm");

            await f.SeedAsync(async db => (await db.IntegrationChannel.SingleAsync(c => c.Id == Vh)).ConfigJson = Config("copy", NotificationCatalog.ChecklistDue));
            await EnqueueAsync(f, NotificationCatalog.ChecklistDue, "G3", worker);
            (await RecipientsAsync(f, "G3")).Should().BeEquivalentTo(["-100500", "1701"], "copy: vừa nhóm vừa riêng");
        }

        [Fact]
        public async Task Low_delivery_temperature_alerts_dispatchers_as_an_incident()
        {
            using var f = new InMemoryApiFactory();
            var (worker, _) = await SeedAsync(f, "{}");
            Guid route = Guid.NewGuid();
            await f.SeedAsync(db =>
            {
                db.DeliveryRoute.Add(new DeliveryRoute { Id = route, RouteNo = "CX-17D", DeliveryDate = DateTime.Today, PlannedStart = DateTime.Today.AddHours(9), Status = DeliveryRouteStatus.Planned });
                return Task.CompletedTask;
            });
            using (var scope = f.Services.CreateScope())
            using (scope.ServiceProvider.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForUser(worker)))
            {
                var routes = scope.ServiceProvider.GetRequiredService<IDeliveryRouteService>();
                (await routes.RecordTemperature(new DeliveryTemperatureRequest { Id = route, TemperatureC = 65m })).ResponseCode.Should().Be(200);
                var low = await routes.RecordTemperature(new DeliveryTemperatureRequest { Id = route, TemperatureC = 55m, CorrectiveAction = "Hâm lại trước khi xuất" });
                low.ResponseCode.Should().Be(200, low.ResponseMessage);
            }
            var alerts = await f.QueryAsync(db => db.OutboundMessage.Where(m => m.MessageType == NotificationCatalog.DeliveryTempLow).ToListAsync());
            alerts.Should().ContainSingle("chỉ lần đo dưới ngưỡng");
            alerts[0].RecipientExternalId.Should().Be("1702", "điều phối (quyền Thêm chuyến xe), không phải người vừa đo");
            var text = MessageTemplates.Render(alerts[0].PayloadJson);
            text.Lines.Should().Contain(l => l.Contains("55")).And.Contain(l => l.Contains("Hâm lại"));
            text.Buttons.Select(b => b.Callback).Should().Contain("inc:claim");
        }
    }
}
