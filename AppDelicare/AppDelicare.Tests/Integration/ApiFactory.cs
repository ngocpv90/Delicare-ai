using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.WebApi;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace AppDelicare.Tests.Integration
{
    /// <summary>
    /// Chạy WebApi thật trong bộ nhớ (môi trường Development → đọc user-secrets của WebApi: JWT key, connection string DB dev).
    /// Không có secret / DB thì <see cref="SkipReason"/> có giá trị và test DB được bỏ qua (Skippable).
    /// Tài khoản đăng nhập test: biến môi trường DELICARE_TEST_USER / DELICARE_TEST_PASSWORD, mặc định tài khoản dev (LOCAL_DEV.local.md).
    /// </summary>
    public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private string? _token;

        /// <summary>
        /// T09: nếu có <c>ConnectionStrings:TestDb</c> (user-secrets của WebApi hoặc biến môi trường <c>ConnectionStrings__TestDb</c>)
        /// thì test dùng DB đó thay cho <c>MainConnectionString</c>. Không có → dùng DB dev hiện tại.
        /// </summary>
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                string? testDb = config.Build().GetConnectionString("TestDb");
                if (!string.IsNullOrWhiteSpace(testDb))
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:MainConnectionString"] = testDb });
                }
            });
        }

        public string? SkipReason { get; private set; }

        public async Task InitializeAsync()
        {
            try
            {
                using var client = CreateClient();
                var response = await client.PostAsJsonAsync("/api/Authentication/Login", new
                {
                    userName = Environment.GetEnvironmentVariable("DELICARE_TEST_USER") ?? "admin",
                    passWord = Environment.GetEnvironmentVariable("DELICARE_TEST_PASSWORD") ?? "admin",
                });
                string body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    SkipReason = $"Không đăng nhập được DB dev (HTTP {(int)response.StatusCode}).";
                    return;
                }
                using var doc = JsonDocument.Parse(body);
                _token = doc.RootElement.TryGetProperty("data", out var data) && data.TryGetProperty("token", out var t) ? t.GetString() : null;
                if (string.IsNullOrEmpty(_token))
                {
                    SkipReason = "Đăng nhập không trả token (sai tài khoản test?).";
                }
            }
            catch (Exception ex)
            {
                SkipReason = "Không khởi động được API / DB dev: " + ex.GetBaseException().Message;
            }
        }

        public HttpClient CreateAuthorizedClient()
        {
            var client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            return client;
        }

        Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;
    }

    [CollectionDefinition(Name)]
    public sealed class ApiCollection : ICollectionFixture<ApiFactory>
    {
        public const string Name = "api";
    }
}

namespace AppDelicare.Tests.Integration
{
    /// <summary>T50R1: test thay <c>Serilog.Log.Logger</c> (tĩnh) chạy riêng, không song song với test khác (host test khởi động sẽ ghi đè Log.Logger).</summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class StaticLoggerCollection
    {
        public const string Name = "static-logger";
    }
}
