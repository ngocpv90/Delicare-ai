using AppDelicare.Data.Share;
using AppDelicare.WebApi.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace AppDelicare.Tests.Actor
{
    /// <summary>T02 – log có CorrelationId; header vào / ra.</summary>
    public class CorrelationIdMiddlewareTests
    {
        private sealed class CollectingSink : ILogEventSink
        {
            public List<LogEvent> Events { get; } = [];
            public void Emit(LogEvent logEvent) => Events.Add(logEvent);
        }

        private sealed class CapturingResponseFeature : HttpResponseFeature
        {
            private readonly List<(Func<object, Task> Callback, object State)> _onStarting = [];
            public override void OnStarting(Func<object, Task> callback, object state) => _onStarting.Add((callback, state));
            public async Task FireOnStartingAsync()
            {
                foreach (var (callback, state) in _onStarting)
                {
                    await callback(state);
                }
            }
        }

        private static async Task<(HttpContext Ctx, List<LogEvent> Logs, string? SeenInside)> RunAsync(string? incomingHeader)
        {
            var sink = new CollectingSink();
            var logger = new LoggerConfiguration().Enrich.FromLogContext().WriteTo.Sink(sink).CreateLogger();
            var responseFeature = new CapturingResponseFeature();
            var ctx = new DefaultHttpContext();
            ctx.Features.Set<IHttpResponseFeature>(responseFeature);
            if (incomingHeader != null)
            {
                ctx.Request.Headers[CorrelationIdMiddleware.HeaderName] = incomingHeader;
            }
            string? seenInside = null;
            var middleware = new CorrelationIdMiddleware(c =>
            {
                seenInside = new CurrentActor(new HttpContextAccessor { HttpContext = c }).CorrelationId;
                logger.Information("inside request");
                return Task.CompletedTask;
            });

            await middleware.InvokeAsync(ctx);
            await responseFeature.FireOnStartingAsync();
            return (ctx, sink.Events, seenInside);
        }

        [Fact]
        public async Task Uses_valid_incoming_header_everywhere()
        {
            var (ctx, logs, seenInside) = await RunAsync("abc-123_X.y");

            seenInside.Should().Be("abc-123_X.y");
            ctx.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString().Should().Be("abc-123_X.y");
            logs.Should().ContainSingle();
            logs[0].Properties[CorrelationIdMiddleware.LogProperty].ToString().Should().Contain("abc-123_X.y");
        }

        [Fact]
        public async Task Generates_id_when_header_missing()
        {
            var (ctx, logs, seenInside) = await RunAsync(null);

            seenInside.Should().MatchRegex("^[0-9a-f]{32}$");
            ctx.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString().Should().Be(seenInside);
            logs[0].Properties.Should().ContainKey(CorrelationIdMiddleware.LogProperty);
        }

        [Theory]
        [InlineData("bad value with spaces")]
        [InlineData("x\r\nInjected: header")]
        [InlineData("0123456789012345678901234567890123456789012345678901234567890123456789")]
        public async Task Rejects_unsafe_header_and_generates_new_id(string header)
        {
            var (_, _, seenInside) = await RunAsync(header);

            seenInside.Should().NotBe(header).And.MatchRegex("^[0-9a-f]{32}$");
        }
    }
}
