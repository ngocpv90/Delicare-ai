using AppDelicare.Data.Connection;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Share;
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
    /// BOT-1.1 (D-62) trên SQL Server thật (DB dev có seed Integration_Telegram_seed.sql – bot VH Active): claim READPAST song song không trùng dòng,
    /// update trùng gửi cùng lúc chỉ tạo 1 dòng (UPDLOCK, HOLDLOCK), outbox rollback cùng nghiệp vụ. Dữ liệu thử: rollback, hoặc xóa đúng các Id đã tạo.
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class BotChannelSqlServerTests(ApiFactory factory)
    {
        private async Task<(Guid VhChannel, Guid Admin)?> PrepareAsync(DatabaseContext db)
        {
            var bots = await RecipientResolver.ActiveBotsAsync(db);
            Guid admin = await db.User.Where(u => u.UserName == "admin").Select(u => u.Id).SingleAsync();
            return bots.TryGetValue("VH", out Guid vh) ? (vh, admin) : null;
        }

        [SkippableFact]
        public async Task Two_concurrent_claims_never_return_the_same_row()
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);
            using var scopeA = factory.Services.CreateScope();
            using var scopeB = factory.Services.CreateScope();
            var dbA = scopeA.ServiceProvider.GetRequiredService<DatabaseContext>();
            var ctx = await PrepareAsync(dbA);
            Skip.If(ctx == null, "DB dev chưa chạy DB/seed/Integration_Telegram_seed.sql (bot VH)");
            var (vh, admin) = ctx!.Value;
            string tag = "TEST-BOT11-" + Guid.NewGuid().ToString("N")[..8];
            var ids = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToList();
            var actor = scopeA.ServiceProvider.GetRequiredService<ICurrentActor>();
            using (actor.BeginScope(ActorInfo.ForUser(admin)))
            {
                dbA.InboundMessage.AddRange(ids.Select((id, i) => new InboundMessage
                {
                    Id = id, IntegrationChannelId = vh, ExternalUserId = tag, ReceivedAt = DateTime.Now.AddSeconds(-60 + i), ProcessingStatus = "Received",
                    IdempotencyKey = $"{tag}:{i}", PayloadJson = "{}",
                }));
                await dbA.SaveChangesAsync();
            }
            try
            {
                // A lấy 3 dòng trong transaction CHƯA commit (giữ khóa) → B (READPAST) không chờ và không nhận dòng nào A đã lấy
                // (B có thể bỏ qua thêm dòng A mới đọc – UPDLOCK giữ tới hết transaction; ở thật transaction lấy việc commit ngay). Sau khi A commit, C lấy phần còn lại.
                var uowA = scopeA.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var serviceA = scopeA.ServiceProvider.GetRequiredService<IBotChannelService>();
                var serviceB = scopeB.ServiceProvider.GetRequiredService<IBotChannelService>();
                List<Guid> fromA, fromB;
                await using (var tx = await dbA.Database.BeginTransactionAsync())
                {
                    fromA = (await serviceA.ClaimInbound(new BotClaimRequest { BotCode = "VH", Max = 3 })).ListData.Select(x => x.Id).Where(ids.Contains).ToList();
                    var claimB = Task.Run(async () => (await serviceB.ClaimInbound(new BotClaimRequest { BotCode = "VH", Max = 20 })).ListData.Select(x => x.Id).Where(ids.Contains).ToList());
                    (await Task.WhenAny(claimB, Task.Delay(TimeSpan.FromSeconds(10)))).Should().Be(claimB, "READPAST: B không chờ khóa của A");
                    fromB = await claimB;
                    await tx.CommitAsync();
                }
                var fromC = (await serviceB.ClaimInbound(new BotClaimRequest { BotCode = "VH", Max = 20 })).ListData.Select(x => x.Id).Where(ids.Contains).ToList();
                fromA.Should().HaveCount(3);
                fromB.Should().NotIntersectWith(fromA);
                fromC.Should().NotIntersectWith(fromA).And.NotIntersectWith(fromB);
                fromA.Concat(fromB).Concat(fromC).Should().OnlyHaveUniqueItems().And.BeEquivalentTo(ids, "mỗi dòng đúng 1 lần");
                _ = uowA;
            }
            finally
            {
                await dbA.Database.ExecuteSqlRawAsync("DELETE FROM dbo.InboundMessage WHERE ExternalUserId = {0}", tag);
            }
        }

        [SkippableFact]
        public async Task Same_update_sent_concurrently_creates_one_row()
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);
            using var scopeA = factory.Services.CreateScope();
            using var scopeB = factory.Services.CreateScope();
            var dbA = scopeA.ServiceProvider.GetRequiredService<DatabaseContext>();
            var ctx = await PrepareAsync(dbA);
            Skip.If(ctx == null, "DB dev chưa chạy DB/seed/Integration_Telegram_seed.sql (bot VH)");
            long updateId = 7_000_000_000L + Random.Shared.Next(1, 999_999_999);
            string key = $"TG:VH:{updateId}";
            var request = new BotUpdateRequest { BotCode = "VH", UpdateId = updateId, ExternalUserId = "TEST-BOT11", MessageType = "message", PayloadJson = "{}" };
            var serviceA = scopeA.ServiceProvider.GetRequiredService<IBotChannelService>();
            var serviceB = scopeB.ServiceProvider.GetRequiredService<IBotChannelService>();
            try
            {
                Task<AppDelicare.Data.Interface.IResult<BotUpdateResultModel>> b;
                await using (var tx = await dbA.Database.BeginTransactionAsync())
                {
                    var a = await serviceA.ReceiveUpdate(request);
                    a.Data!.Duplicate.Should().BeFalse();
                    b = Task.Run(() => serviceB.ReceiveUpdate(request));
                    (await Task.WhenAny(b, Task.Delay(TimeSpan.FromSeconds(2)))).Should().NotBe(b, "B phải chờ khóa UPDLOCK, HOLDLOCK của A");
                    await tx.CommitAsync();
                }
                var resultB = await b;
                resultB.Data!.Duplicate.Should().BeTrue();
                dbA.ChangeTracker.Clear();
                (await dbA.InboundMessage.CountAsync(m => m.IdempotencyKey == key)).Should().Be(1);
            }
            finally
            {
                await dbA.Database.ExecuteSqlRawAsync("DELETE FROM dbo.IntegrationLog WHERE IdempotencyKey = {0}", key);
                await dbA.Database.ExecuteSqlRawAsync("DELETE FROM dbo.InboundMessage WHERE IdempotencyKey = {0}", key);
            }
        }

        [SkippableFact]
        public async Task Business_rollback_leaves_no_outbound_message()
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            var ctx = await PrepareAsync(db);
            Skip.If(ctx == null, "DB dev chưa chạy DB/seed/Integration_Telegram_seed.sql (bot VH)");
            var (vh, admin) = ctx!.Value;
            var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>();
            var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
            Guid user = Guid.NewGuid();
            string key = "TEST-BOT11-ROLLBACK-" + Guid.NewGuid().ToString("N")[..8];
            using (actor.BeginScope(ActorInfo.ForUser(admin)))
            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                db.UserExternalIdentity.Add(new UserExternalIdentity
                {
                    Id = Guid.NewGuid(), UserId = user, IntegrationChannelId = vh, ExternalUserId = key, LinkedAt = DateTime.Now, IdentityStatus = "Active",
                });
                await db.SaveChangesAsync();
                var ids = await notifications.EnqueueAsync(new NotificationRequest
                {
                    Template = NotificationCatalog.ShiftConfirmed, UserIds = [user], Data = new { shift = "SANG_6H" }, IdempotencyKey = key,
                });
                ids.Should().HaveCount(1);
                (await db.OutboundMessage.CountAsync(m => m.RecipientExternalId == key)).Should().Be(1, "trong transaction thấy tin");
                // Gọi lại cùng khóa sự kiện → không nhân bản.
                (await notifications.EnqueueAsync(new NotificationRequest { Template = NotificationCatalog.ShiftConfirmed, UserIds = [user], IdempotencyKey = key }))
                    .Should().BeEmpty();
                await tx.RollbackAsync();
            }
            db.ChangeTracker.Clear();
            (await db.OutboundMessage.CountAsync(m => m.RecipientExternalId == key)).Should().Be(0, "nghiệp vụ rollback → outbox rollback");
        }
    }
}
