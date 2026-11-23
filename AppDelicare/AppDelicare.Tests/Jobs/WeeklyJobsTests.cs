using AppDelicare.Model.Entity;
using AppDelicare.Services.Jobs;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Jobs
{
    /// <summary>BIZ-3.4 (D-77 – C11, C12): thực đơn tự hết hiệu lực; thứ Tư nhắc thực đơn chung tuần sau; thứ Sáu nhắc khách chưa đăng ký suất tuần sau.</summary>
    public class WeeklyJobsTests(InMemoryApiFactory factory) : IClassFixture<InMemoryApiFactory>
    {
        private async Task<JobOutcome> RunAsync(string code, DateTime now)
        {
            using var scope = factory.Services.CreateScope();
            var job = scope.ServiceProvider.GetServices<IScheduledJob>().Single(j => j.Code == code);
            using (scope.ServiceProvider.GetRequiredService<AppDelicare.Data.Share.ICurrentActor>().BeginScope(AppDelicare.Data.Share.ActorInfo.ForSystem(InMemoryApiFactory.UserId)))
            {
                return await job.RunAsync(now, CancellationToken.None);
            }
        }

        [Fact]
        public async Task Published_menu_past_last_effective_day_expires()
        {
            string key = Guid.NewGuid().ToString("N")[..5];
            Guid old = Guid.NewGuid(), open = Guid.NewGuid(), current = Guid.NewGuid();
            await factory.SeedAsync(db =>
            {
                db.Menu.AddRange(
                    new Model.Entity.Menu { Id = old, Code = "TD-OLD-" + key, Name = "Cũ", VersionNo = 1, Status = "Published", EffectiveFrom = new DateTime(2032, 1, 5), EffectiveTo = new DateTime(2032, 1, 11) },
                    new Model.Entity.Menu { Id = open, Code = "TD-OPEN-" + key, Name = "Không ngày đến", VersionNo = 1, Status = "Published", EffectiveFrom = new DateTime(2032, 1, 6) },
                    new Model.Entity.Menu { Id = current, Code = "TD-CUR-" + key, Name = "Đang dùng", VersionNo = 1, Status = "Published", EffectiveFrom = new DateTime(2032, 1, 12), EffectiveTo = new DateTime(2032, 1, 18) });
                return Task.CompletedTask;
            });
            var outcome = await RunAsync("MenuExpiry", new DateTime(2032, 1, 13, 0, 30, 0));
            outcome.Message.Should().Contain("TD-OLD-" + key).And.Contain("TD-OPEN-" + key, "không ngày đến → hiệu lực từ + 6 = 12/01 < 13/01");
            var statuses = await factory.QueryAsync(db => db.Menu.AsNoTracking().Where(m => m.Code!.EndsWith(key)).ToDictionaryAsync(m => m.Id, m => m.Status));
            (statuses[old], statuses[open], statuses[current]).Should().Be(("Expired", "Expired", "Published"));
        }

        [Fact]
        public async Task Reminders_run_only_on_their_weekday_and_detect_missing_menu_and_registrations()
        {
            // 07/01/2032 = thứ Tư; tuần sau 12/01 – 18/01.
            (await RunAsync("MenuWeekReminder", new DateTime(2032, 1, 6, 9, 0, 0))).Message.Should().Contain("Không phải ngày nhắc");
            (await RunAsync("MenuWeekReminder", new DateTime(2032, 1, 7, 9, 0, 0))).Message.Should().Contain("Chưa có thực đơn chung tuần 12/01");
            await factory.SeedAsync(db =>
            {
                db.Menu.Add(new Model.Entity.Menu { Id = Guid.NewGuid(), Code = "TD-CHUNG-" + Guid.NewGuid().ToString("N")[..4], Name = "Chung", VersionNo = 1, Status = "Published", MenuType = "COMMON", EffectiveFrom = new DateTime(2032, 1, 12) });
                return Task.CompletedTask;
            });
            (await RunAsync("MenuWeekReminder", new DateTime(2032, 1, 7, 9, 0, 0))).Message.Should().Contain("Đã có thực đơn chung");

            // 09/01/2032 = thứ Sáu: khách A có hợp đồng, chưa có đơn tuần sau → nhắc; khách B đã có đơn.
            string key = Guid.NewGuid().ToString("N")[..5];
            Guid a = Guid.NewGuid(), b = Guid.NewGuid();
            await factory.SeedAsync(db =>
            {
                foreach (var (id, code) in new[] { (a, "A"), (b, "B") })
                {
                    db.Customer.Add(new Customer { Id = id, Code = code + key, Name = "Khách " + code + key, DisplayName = "Khách " + code + key, CIF = code + key, CustomerType = "FACTORY", Status = "Active" });
                    db.Contract.Add(new Contract { Id = Guid.NewGuid(), CustomerId = id, ContractNumber = "HD-" + code + key, Status = "Active", EffectiveFrom = new DateTime(2031, 1, 1) });
                }
                db.Order.Add(new Order { Id = Guid.NewGuid(), OrderNumber = "DH-" + key, CustomerId = b, OrderDate = new DateTime(2032, 1, 9), MealDate = new DateTime(2032, 1, 13), DeliveryDate = new DateTime(2032, 1, 13), Status = "Draft" });
                return Task.CompletedTask;
            });
            var week = await RunAsync("OrderWeekReminder", new DateTime(2032, 1, 9, 16, 0, 0));
            week.Message.Should().Contain("chưa đăng ký tuần 12/01");
            (await RunAsync("OrderWeekReminder", new DateTime(2032, 1, 8, 16, 0, 0))).Message.Should().Contain("Không phải ngày nhắc");
        }
    }
}
