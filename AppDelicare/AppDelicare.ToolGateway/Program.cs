using System.Text.Json;
using System.Text.RegularExpressions;
using AppDelicare.Model.Helper;
using AppDelicare.Services.Implement.Agent;
using AppDelicare.Services.Ioc;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using Serilog;
using Serilog.Context;

// AI-1 (D-133): AppDelicare.ToolGateway – cửa vào thứ ba (cạnh WebApi, Job) cho agent Hermes.
//   MCP  POST /mcp            (Streamable HTTP, không phiên – SDK ModelContextProtocol.AspNetCore 2.4.0)
//   REST POST /tools/{name}   (cùng bộ công cụ – Trung tâm agent, test, tools/agent-eval)
//   GET  /health              (Docker HEALTHCHECK)
// Header: X-Agent-Code, X-Agent-Key (AI-2), X-On-Behalf-Of (mã định danh ngoài của người hỏi), X-Correlation-Id.
// KHÔNG truy cập CSDL riêng (không ngữ cảnh EF, không kết nối SQL): mọi công cụ gọi method Service (IAgentGatewayService). Chỉ mở trong mạng nội bộ Docker.
namespace AppDelicare.ToolGateway
{
    public class Program
    {
        public static void Main(string[] args)
        {
            string environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory, EnvironmentName = environment });
            builder.Configuration.Sources.Clear();
            builder.Configuration.SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.shared.json", optional: false)
                .AddJsonFile($"appsettings.shared.{environment}.json", optional: true)
                .AddJsonFile("toolgateway.json", optional: false)
                .AddJsonFile($"toolgateway.{environment}.json", optional: true);
            if (builder.Environment.IsDevelopment())
            {
                builder.Configuration.AddUserSecrets(typeof(Program).Assembly, optional: true);
            }
            builder.Configuration.AddEnvironmentVariables();
            builder.Configuration.AddCommandLine(args);
            builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory())
                .ConfigureContainer<ContainerBuilder>(b => b.RegisterModule<DelicareServicesModule>())
                .UseSerilog((context, logger) => SerilogSetup.Configure(logger, context.Configuration));
            builder.Services.AddDelicareCore(builder.Configuration, builder.Environment.ContentRootPath);
            builder.Services.AddHealthChecks();
            builder.Services.AddMcpServer(o => o.ServerInfo = new Implementation { Name = "delicare-toolgateway", Version = AgentToolRegistry.Version })
                .WithHttpTransport(o => o.Stateless = true)
                .WithListToolsHandler(async (ctx, ct) =>
                {
                    var gateway = ctx.Services!.GetRequiredService<IAgentGatewayService>();
                    var (error, tools) = await gateway.ListToolsAsync(GatewayHttp.Request(ctx.Services!, null, default));
                    if (error.Status != 200)
                    {
                        throw new ModelContextProtocol.McpException(error.Message ?? "Từ chối");
                    }
                    return new ListToolsResult
                    {
                        Tools = [.. tools.Select(t => new Tool { Name = t.Name, Description = t.Description, InputSchema = JsonDocument.Parse(t.InputSchemaJson).RootElement.Clone() })],
                    };
                })
                .WithCallToolHandler(async (ctx, ct) =>
                {
                    var gateway = ctx.Services!.GetRequiredService<IAgentGatewayService>();
                    var p = ctx.Params!;
                    var args = p.Arguments == null ? default : JsonSerializer.SerializeToElement(p.Arguments);
                    var result = await gateway.CallAsync(GatewayHttp.Request(ctx.Services!, p.Name, args));
                    return new CallToolResult
                    {
                        IsError = result.Status != 200,
                        Content = [new TextContentBlock { Text = result.Status == 200 ? AgentToolRegistry.Serialize(result.Data) : $"[{result.Status}] {result.Message}" }],
                    };
                });

            var app = builder.Build();
            if (string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString(ConstantHelper.MainConnectionString)))
            {
                throw new InvalidOperationException("Thiếu ConnectionStrings:MainConnectionString (biến môi trường ConnectionStrings__MainConnectionString).");
            }
            app.Use(async (http, next) =>
            {
                string id = http.Request.Headers.TryGetValue(GatewayHttp.CorrelationHeader, out var v) && GatewayHttp.SafeId.IsMatch(v.ToString()) ? v.ToString() : Guid.NewGuid().ToString("N");
                http.Items[GatewayHttp.CorrelationHeader] = id;
                http.Response.Headers[GatewayHttp.CorrelationHeader] = id;
                using (LogContext.PushProperty("CorrelationId", id))
                {
                    await next();
                }
            });
            app.MapHealthChecks("/health");
            app.MapMcp("/mcp");
            app.MapPost("/tools/{name}", async (string name, HttpContext http, IAgentGatewayService gateway) =>
            {
                JsonElement args = default;
                if (http.Request.ContentLength is > 0 || http.Request.Headers.ContentType.ToString().Contains("json", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        args = (await JsonDocument.ParseAsync(http.Request.Body)).RootElement.Clone();
                    }
                    catch (JsonException)
                    {
                        return Results.Json(new { status = 400, message = "Tham số không phải JSON" }, statusCode: 400);
                    }
                }
                var result = await gateway.CallAsync(GatewayHttp.Request(http.RequestServices, name, args));
                return Results.Json(new { status = result.Status, message = result.Message, data = result.Data }, statusCode: result.Status);
            });
            app.MapGet("/tools", async (HttpContext http, IAgentGatewayService gateway) =>
            {
                var (error, tools) = await gateway.ListToolsAsync(GatewayHttp.Request(http.RequestServices, null, default));
                return error.Status != 200 ? Results.Json(new { status = error.Status, message = error.Message }, statusCode: error.Status)
                    : Results.Json(tools.Select(t => new { t.Name, t.Description, t.ScreenId, inputSchema = JsonDocument.Parse(t.InputSchemaJson).RootElement }));
            });
            app.Run();
        }
    }

    /// <summary>Đọc header của lời gọi (MCP hoặc REST) thành <see cref="AgentCallRequest"/>.</summary>
    public static partial class GatewayHttp
    {
        public const string CorrelationHeader = "X-Correlation-Id";
        public static readonly Regex SafeId = MakeSafeId();

        public static AgentCallRequest Request(IServiceProvider services, string? tool, JsonElement args)
        {
            var http = services.GetService<IHttpContextAccessor>()?.HttpContext;
            string? H(string name) => http != null && http.Request.Headers.TryGetValue(name, out var v) && v.Count > 0 ? v.ToString().Trim() : null;
            return new AgentCallRequest
            {
                AgentCode = H("X-Agent-Code"), AgentKey = H("X-Agent-Key"), OnBehalfOf = H("X-On-Behalf-Of"), Tool = tool ?? string.Empty, Arguments = args,
                CorrelationId = http?.Items[CorrelationHeader] as string,
            };
        }

        [GeneratedRegex("^[A-Za-z0-9._-]{1,64}$")]
        private static partial Regex MakeSafeId();
    }
}
