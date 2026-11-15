using System.Text.Json;
using AppDelicare.Data.Share;
using AppDelicare.WebApi.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace AppDelicare.Tests.Integration
{
    /// <summary>T03 – lỗi chưa bắt: 500 ProblemDetails, không lộ message / stack trace.</summary>
    public class ProblemDetailsExceptionHandlerTests
    {
        private sealed class ExceptionFeature(Exception error) : IExceptionHandlerFeature
        {
            public Exception Error { get; } = error;
        }

        private static async Task<(int Status, string ContentType, JsonElement Body, string Raw)> RunAsync(Exception ex)
        {
            var ctx = new DefaultHttpContext();
            ctx.Response.Body = new MemoryStream();
            ctx.Request.Path = "/api/v1/x";
            ctx.Items[CurrentActor.CorrelationIdItemKey] = "corr-500";
            ctx.Features.Set<IExceptionHandlerFeature>(new ExceptionFeature(ex));

            await ProblemDetailsExceptionHandler.HandleAsync(ctx);

            ctx.Response.Body.Position = 0;
            string raw = await new StreamReader(ctx.Response.Body).ReadToEndAsync();
            return (ctx.Response.StatusCode, ctx.Response.ContentType!, JsonDocument.Parse(raw).RootElement.Clone(), raw);
        }

        [Fact]
        public async Task Unhandled_exception_returns_500_without_leaking_details()
        {
            var (status, contentType, body, raw) = await RunAsync(new InvalidOperationException("SECRET connection string Server=prod;Password=x"));

            status.Should().Be(500);
            contentType.Should().StartWith("application/problem+json");
            body.GetProperty("status").GetInt32().Should().Be(500);
            body.GetProperty("traceId").GetString().Should().Be("corr-500");
            raw.Should().NotContain("SECRET").And.NotContain("Password").And.NotContain("InvalidOperationException").And.NotContain(" at ");
        }

        [Fact]
        public async Task Missing_actor_maps_to_401()
        {
            var (status, _, body, _) = await RunAsync(new UnauthorizedActorException("x"));

            status.Should().Be(401);
            body.GetProperty("status").GetInt32().Should().Be(401);
        }
    }
}
