using System.Net;
using System.Text.Json;
using AppDelicare.Application;
using AppDelicare.Application.Common;
using AppDelicare.Application.Common.Lookups;
using AppDelicare.Data.Connection;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AppDelicare.Tests.Application
{
    // ---- Lệnh / sự kiện mẫu chỉ dùng trong test --------------------------------------------------
    public sealed record PingCommand(string Text, bool Fail = false) : ICommand<string>;
    public sealed record Pinged(string Text) : IDomainEvent;

    public sealed class PingValidator : AbstractValidator<PingCommand>
    {
        public PingValidator() => RuleFor(x => x.Text).NotEmpty().WithMessage("Text bắt buộc.");
    }

    public sealed class PingHandler(IDomainEventCollector events) : IRequestHandler<PingCommand, string>
    {
        public Task<string> HandleAsync(PingCommand request, CancellationToken cancellationToken)
        {
            events.Raise(new Pinged(request.Text));
            if (request.Fail)
            {
                throw new BusinessRuleException(422, "BUSINESS_RULE", "lỗi giả lập");
            }
            return Task.FromResult("pong:" + request.Text);
        }
    }

    public sealed class PingedLog
    {
        public List<string> Items { get; } = [];
    }

    public sealed class PingedHandler(PingedLog log) : IDomainEventHandler<Pinged>
    {
        public Task HandleAsync(Pinged domainEvent, CancellationToken cancellationToken)
        {
            log.Items.Add(domainEvent.Text);
            return Task.CompletedTask;
        }
    }

    /// <summary>T10 – khung Application: dispatcher + validator + sự kiện sau commit, IClock, BusinessRules, LookupQuery.</summary>
    public class ApplicationCoreTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public ApplicationCoreTests(InMemoryApiFactory factory) => _factory = factory;

        private static ServiceProvider BuildServices(Action<IServiceCollection>? extra = null)
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BusinessRules:CoreTempMinC"] = "72.5",
            }).Build();
            var services = new ServiceCollection();
            services.AddApplication(config);
            services.AddScoped<IRequestHandler<PingCommand, string>, PingHandler>();
            services.AddScoped<IValidator<PingCommand>, PingValidator>();
            services.AddScoped<IDomainEventHandler<Pinged>, PingedHandler>();
            services.AddSingleton<PingedLog>();
            extra?.Invoke(services);
            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task Dispatcher_runs_handler_and_publishes_events_after_success()
        {
            using var sp = BuildServices();
            using var scope = sp.CreateScope();

            string result = await scope.ServiceProvider.GetRequiredService<IDispatcher>().SendAsync(new PingCommand("a"));

            result.Should().Be("pong:a");
            sp.GetRequiredService<PingedLog>().Items.Should().Equal("a");
        }

        [Fact]
        public async Task Failed_command_discards_events()
        {
            using var sp = BuildServices();
            using var scope = sp.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

            Func<Task> act = () => dispatcher.SendAsync(new PingCommand("x", Fail: true));

            await act.Should().ThrowAsync<BusinessRuleException>();
            sp.GetRequiredService<PingedLog>().Items.Should().BeEmpty();
            await dispatcher.SendAsync(new PingCommand("y"));
            sp.GetRequiredService<PingedLog>().Items.Should().Equal(new[] { "y" }, "sự kiện của lệnh lỗi trước không bị phát lại");
        }

        [Fact]
        public async Task Validator_failure_throws_with_camel_case_fields_and_skips_handler()
        {
            using var sp = BuildServices();
            using var scope = sp.CreateScope();

            Func<Task> act = () => scope.ServiceProvider.GetRequiredService<IDispatcher>().SendAsync(new PingCommand(""));

            var ex = (await act.Should().ThrowAsync<ValidationFailedException>()).Which;
            ex.Errors.Should().ContainSingle(e => e.Field == "text" && e.Message == "Text bắt buộc.");
            sp.GetRequiredService<PingedLog>().Items.Should().BeEmpty();
        }

        [Fact]
        public void Clock_is_vietnam_time_and_business_rules_bind_from_configuration()
        {
            using var sp = BuildServices();
            var clock = sp.GetRequiredService<IClock>();
            var rules = sp.GetRequiredService<IOptions<BusinessRulesOptions>>().Value;

            (clock.Now - clock.UtcNow).TotalHours.Should().BeApproximately(7, 0.01);
            clock.Now.Kind.Should().Be(DateTimeKind.Unspecified);
            rules.CoreTempMinC.Should().Be(72.5m);
            rules.DeliveryTempMinC.Should().Be(60m, "giá trị mặc định khi cấu hình không ghi đè");
            rules.ComplaintSlaHours["HIGH"].Should().Be(4);
        }

        // ---- LookupQuery ----------------------------------------------------------------------------

        private static (IDispatcher Dispatcher, DatabaseContext Db, IServiceScope Scope, IDisposable Actor) LookupEnv()
        {
            string name = "t10-" + Guid.NewGuid();
            var sp = BuildServices(s =>
            {
                s.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
                s.AddScoped<ICurrentActor, CurrentActor>();
                s.AddDbContext<DatabaseContext>(o => o.UseInMemoryDatabase(name));
            });
            var scope = sp.CreateScope();
            var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForSystem(Guid.NewGuid()));
            return (scope.ServiceProvider.GetRequiredService<IDispatcher>(), scope.ServiceProvider.GetRequiredService<DatabaseContext>(), scope, actor);
        }

        [Fact]
        public async Task Lookup_uses_label_priority_filters_inactive_and_deleted_and_searches_code()
        {
            var (dispatcher, db, scope, actor) = LookupEnv();
            using (scope)
            using (actor)
            {
                db.Customer.AddRange(
                    new Customer { Id = Guid.NewGuid(), CIF = "C1", CustomerType = "B2B", Code = "KH01", Name = "Tên", DisplayName = "Ánh Dương", Status = "Active" },
                    new Customer { Id = Guid.NewGuid(), CIF = "C2", CustomerType = "B2B", Code = "KH02", DisplayName = "Bình Minh", Status = "Inactive" },
                    new Customer { Id = Guid.NewGuid(), CIF = "C3", CustomerType = "B2B", Code = "KH03", DisplayName = "Cầu Vồng", Status = "Active", IsDeleted = true },
                    new Customer { Id = Guid.NewGuid(), CIF = "C4", CustomerType = "B2B", Code = "KH04", DisplayName = "Đông Á", Status = "Active" });
                await db.SaveChangesAsync();

                var all = await dispatcher.SendAsync(new LookupQuery("Customer"));
                var byCode = await dispatcher.SendAsync(new LookupQuery("Customer", Q: "KH04"));

                all.Select(x => x.Label).Should().Equal("Ánh Dương", "Đông Á");
                all[0].Code.Should().Be("KH01");
                byCode.Should().ContainSingle(x => x.Label == "Đông Á");
            }
        }

        [Fact]
        public async Task Lookup_by_ids_returns_selected_values_even_if_inactive_and_maps_legacy_table_names()
        {
            var (dispatcher, db, scope, actor) = LookupEnv();
            using (scope)
            using (actor)
            {
                var inactive = Guid.NewGuid();
                db.Customer.Add(new Customer { Id = inactive, CIF = "C9", CustomerType = "B2B", Code = "KH09", DisplayName = "Cũ", Status = "Inactive" });
                db.Dish.Add(new Dish { Id = Guid.NewGuid(), Code = "M01", Name = "Cơm gà", Status = "Active" });
                await db.SaveChangesAsync();

                var byIds = await dispatcher.SendAsync(new LookupQuery("Customer", Ids: [inactive]));
                var dishesByTable = await dispatcher.SendAsync(new LookupQuery("Dishe"));
                var dishesByClass = await dispatcher.SendAsync(new LookupQuery("Dish"));

                byIds.Should().ContainSingle(x => x.Id == inactive && x.Status == "Inactive");
                dishesByTable.Should().ContainSingle(x => x.Label == "Cơm gà");
                dishesByClass.Should().BeEquivalentTo(dishesByTable);
            }
        }

        [Theory]
        [InlineData("User")]
        [InlineData("AuditChange")]
        [InlineData("Patient")]
        [InlineData("NoSuchTable")]
        [InlineData("UnitsOfMeasure_Legacy_20261113")]
        public async Task Lookup_rejects_blocked_or_unknown_tables(string table)
        {
            var (dispatcher, _, scope, actor) = LookupEnv();
            using (scope)
            using (actor)
            {
                Func<Task> act = () => dispatcher.SendAsync(new LookupQuery(table));
                (await act.Should().ThrowAsync<BusinessRuleException>()).Which.StatusCode.Should().Be(404);
            }
        }

        [Fact]
        public async Task Lookup_endpoint_end_to_end()
        {
            await _factory.SeedAsync(db =>
            {
                db.Warehouse.Add(new Warehouse { Id = Guid.NewGuid(), Code = "K-T10", Name = "Kho T10", Status = "Active" });
                return Task.CompletedTask;
            });
            using var client = _factory.CreateAuthorizedClient();

            var ok = await client.GetAsync("/api/v1/meta/lookups/Warehouse?q=T10");
            var bad = await client.GetAsync("/api/v1/meta/lookups/Warehouse?take=0");
            var blocked = await client.GetAsync("/api/v1/meta/lookups/User");

            ok.StatusCode.Should().Be(HttpStatusCode.OK);
            using (var body = JsonDocument.Parse(await ok.Content.ReadAsStringAsync()))
            {
                var item = body.RootElement.GetProperty("data")[0];
                item.GetProperty("label").GetString().Should().Be("Kho T10");
                item.GetProperty("code").GetString().Should().Be("K-T10");
            }
            bad.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            using (var body = JsonDocument.Parse(await bad.Content.ReadAsStringAsync()))
            {
                body.RootElement.GetProperty("errors")[0].GetProperty("field").GetString().Should().Be("take");
            }
            blocked.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }
}
