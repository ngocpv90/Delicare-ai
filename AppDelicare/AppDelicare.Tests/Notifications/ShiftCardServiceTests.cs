using System.Text.Json;
using AppDelicare.Data.Interface;
using AppDelicare.Messaging.Presentation;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Model;
using AppDelicare.Services.Interface;
using AppDelicare.Services.Jobs;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Notifications
{
    /// <summary>
    /// BOT-2.1 (BOT_TELEGRAM §1.1) – phía api, đồng hồ giả: Thẻ ca xếp vào outbox đúng 15′ trước ca (chưa tới hạn → không), 1 thẻ / người / ca (gọi lại không trùng),
    /// nút theo giai đoạn ∩ quyền menu; thẻ đã gửi → trả nội dung mới (giai đoạn đổi, đã vào ca) để messaging sửa tin; ⚡ Mine trả thẻ + tin đã ghim;
    /// Thẻ ngày 07:00 tới quản lý có bot DH (chạy lại không gửi trùng), người không có mục dashboard không nhận.
    /// </summary>
    public class ShiftCardServiceTests
    {
        private static readonly DateTime Day = new(2026, 12, 14);
        private static readonly Guid Vh = Guid.Parse("77777777-0000-4000-8000-0000000000a1");
        private static readonly Guid Dh = Guid.Parse("77777777-0000-4000-8000-0000000000a2");

        private static readonly Guid SystemUser = Guid.Parse("77777777-0000-4000-8000-0000000000ff");

        private static Task SeedAsync(InMemoryApiFactory f, Guid employee, Guid user, Guid assignment)
            => f.SeedAsync(async db =>
            {
                db.User.Add(new User { Id = SystemUser, UserName = "system-sc", IsActive = true });
                db.IntegrationChannel.AddRange(
                    new IntegrationChannel { Id = Vh, ChannelCode = "TELEGRAM_VH", ChannelName = "VH", ProviderCode = "TELEGRAM", EnvironmentCode = "Development", ChannelStatus = "Active", ConfigJson = "{}" },
                    new IntegrationChannel { Id = Dh, ChannelCode = "TELEGRAM_DH", ChannelName = "DH", ProviderCode = "TELEGRAM", EnvironmentCode = "Development", ChannelStatus = "Active", ConfigJson = "{}" });
                db.BotConfiguration.AddRange(
                    new BotConfiguration { Id = Guid.NewGuid(), IntegrationChannelId = Vh, BotCode = "VH", BotName = "Vận hành", BotStatus = "Active" },
                    new BotConfiguration { Id = Guid.NewGuid(), IntegrationChannelId = Dh, BotCode = "DH", BotName = "Điều hành", BotStatus = "Active" });
                Guid shift = Guid.NewGuid(), group = Guid.NewGuid();
                db.Shift.Add(new Shift { Id = shift, Code = "SANG-SC", Name = "Ca sáng", StartTime = new TimeSpan(6, 0, 0), EndTime = new TimeSpan(14, 0, 0) });
                db.Employee.Add(new Employee { Id = employee, Code = "NV-SC", FullName = "Tổ trưởng SC", Status = "Active" });
                db.ShiftAssignment.Add(new ShiftAssignment { Id = assignment, EmployeeId = employee, ShiftId = shift, WorkDate = Day, PlannedStart = Day.AddHours(6), PlannedEnd = Day.AddHours(14), AssignmentStatus = "Confirmed" });
                db.User.Add(new User { Id = user, UserName = "sc-user", IsActive = true });
                db.UserGroup.Add(new UserGroup { Id = group, Name = "SC-KIT" });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = user, UserGroupId = group });
                foreach (string link in new[] { "#/prod-batch", "#/dash-exec" })
                {
                    var menu = await db.ObjectMenu.FirstOrDefaultAsync(m => m.MetaLink == link) ?? new ObjectMenu { Id = Guid.NewGuid(), Name = link, MetaLink = link, IsActive = true, LevelShow = 2 };
                    if (db.Entry(menu).State == EntityState.Detached)
                    {
                        db.ObjectMenu.Add(menu);
                    }
                    db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = group, ObjectMenuId = menu.Id, AllowAccess = true, AllowAdd = true, AllowEdit = true });
                }
                // Cùng 1 người: định danh VH (khớp số điện thoại → có cả UserId + EmployeeId) + DH.
                db.UserExternalIdentity.AddRange(
                    new UserExternalIdentity { Id = Guid.NewGuid(), UserId = user, EmployeeId = employee, IntegrationChannelId = Vh, ExternalUserId = "8801", LinkedAt = Day, IdentityStatus = "Active" },
                    new UserExternalIdentity { Id = Guid.NewGuid(), UserId = user, IntegrationChannelId = Dh, ExternalUserId = "8802", LinkedAt = Day, IdentityStatus = "Active" });
            });

        private static async Task<IResults<ShiftCardModel>> SyncAsync(InMemoryApiFactory f)
        {
            using var scope = f.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IShiftCardService>().Sync();
        }

        private static Task<List<OutboundMessage>> CardsAsync(InMemoryApiFactory f, string template = "SHIFT_CARD")
            => f.QueryAsync(db => db.OutboundMessage.Where(m => m.MessageType == template).ToListAsync());

        [Fact]
        public async Task Shift_card_is_queued_fifteen_minutes_before_once_then_refreshed_with_new_phase()
        {
            using var f = new InMemoryApiFactory();
            f.Settings["SystemUsers:JobUserId"] = SystemUser.ToString();
            Guid employee = Guid.NewGuid(), user = Guid.NewGuid(), assignment = Guid.NewGuid();
            await SeedAsync(f, employee, user, assignment);

            f.Clock.FixedNow = Day.AddHours(5).AddMinutes(40);
            (await SyncAsync(f)).ListData.Should().BeEmpty();
            (await CardsAsync(f)).Should().BeEmpty("còn 20′ – chưa tới 15′ trước ca");

            f.Clock.FixedNow = Day.AddHours(5).AddMinutes(46);
            await SyncAsync(f);
            var card = (await CardsAsync(f)).Should().ContainSingle().Subject;
            card.RecipientExternalId.Should().Be("8801", "thẻ ca là tin cá nhân → bot VH, 1 tin / người");
            card.CorrelationId.Should().Be("SHIFTCARD:" + assignment.ToString("N"));
            var m = MessageTemplates.Render(card.PayloadJson);
            m.Title.Should().Contain("Ca sáng").And.Contain("Tổ trưởng SC");
            m.Lines.Should().Contain(l => l.StartsWith("⏳ Vào ca lúc 06:00")).And.Contain("Giai đoạn: trước ca");
            m.Buttons.Select(b => b.Callback).Should().Equal("sc:run:check_in", "sc:run:bep_batches", "sc:refresh");
            await SyncAsync(f);
            (await CardsAsync(f)).Should().HaveCount(1, "gọi lại không gửi thẻ thứ 2");

            // Messaging đã gửi thẻ (Sent + mã tin) → Sync trả nội dung mới để sửa: trong ca, đã vào ca.
            await f.SeedAsync(async db =>
            {
                var sent = await db.OutboundMessage.SingleAsync(x => x.MessageType == "SHIFT_CARD");
                sent.MessageStatus = "Sent";
                sent.ProviderMessageId = "555";
                var a = await db.ShiftAssignment.SingleAsync(x => x.Id == assignment);
                a.AssignmentStatus = "CheckedIn";
            });
            f.Clock.FixedNow = Day.AddHours(9);
            var refresh = (await SyncAsync(f)).ListData.Should().ContainSingle().Subject;
            refresh.ProviderMessageId.Should().Be("555");
            refresh.ChatId.Should().Be("8801");
            var updated = MessageTemplates.Render(refresh.PayloadJson);
            updated.Lines.Should().Contain("✅ Đã vào ca").And.Contain("Giai đoạn: trong ca");
            updated.Buttons.Select(b => b.Callback).Should().Equal("sc:run:bep_batches", "sc:refresh");
            (await CardsAsync(f)).Should().HaveCount(1, "cập nhật = sửa tin, không thêm tin");

            using var scope = f.Services.CreateScope();
            var mine = await scope.ServiceProvider.GetRequiredService<IShiftCardService>().Mine(new BotIdentityRequest { BotCode = "VH", ExternalUserId = "8801" });
            mine.Data!.ProviderMessageId.Should().Be("555");

            f.Clock.FixedNow = Day.AddHours(15);
            (await SyncAsync(f)).ListData.Should().BeEmpty("hết ca + 30′ → không làm mới nữa");
        }

        [Fact]
        public async Task Day_card_goes_to_managers_on_dh_once_per_day()
        {
            using var f = new InMemoryApiFactory();
            Guid employee = Guid.NewGuid(), user = Guid.NewGuid();
            await SeedAsync(f, employee, user, Guid.NewGuid());
            f.Clock.FixedNow = Day.AddHours(7);
            using var scope = f.Services.CreateScope();
            var job = scope.ServiceProvider.GetServices<IScheduledJob>().Single(j => j.Code == "DayCard");
            using var actor = scope.ServiceProvider.GetRequiredService<AppDelicare.Data.Share.ICurrentActor>().BeginScope(AppDelicare.Data.Share.ActorInfo.ForSystem(SystemUser)); // như JobRunService
            (await job.RunAsync(Day.AddHours(7), default)).RowsProcessed.Should().Be(1);
            (await job.RunAsync(Day.AddHours(7), default)).RowsProcessed.Should().Be(0, "chạy lại không gửi trùng");
            var card = (await CardsAsync(f, "DAY_CARD")).Should().ContainSingle().Subject;
            card.RecipientExternalId.Should().Be("8802", "Thẻ ngày → bot Điều hành");
            var m = MessageTemplates.Render(card.PayloadJson);
            m.Title.Should().Be("☀️ Thẻ ngày 14/12");
            m.Lines.Should().Contain(l => l.StartsWith("⏳"));
            m.Buttons.Select(b => b.Callback).Should().Contain("sc:run:dh_overview");
        }
    }
}
