using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using AppDelicare.Data.Connection;
using AppDelicare.Data.Share;
using AppDelicare.WebApi;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace AppDelicare.Tests.Integration
{
    /// <summary>
    /// WebApi chạy trong bộ nhớ với EF InMemory + khóa JWT riêng cho test (KHÔNG đụng DB dev qua EF).
    /// Dùng cho route v1 chỉ đi qua EF. Token tự ký bằng cùng khóa (không cần đăng nhập).
    /// </summary>
    public sealed class InMemoryApiFactory : WebApplicationFactory<Program>
    {
        public const string SigningKey = "test-only-signing-key-0123456789-abcdefghijklmnopqrstuvwxyz";
        public const string Issuer = "webApi";
        public const string Audience = "http://localhost:5125/";
        public static readonly Guid UserId = Guid.Parse("11111111-2222-3333-4444-555555555555");

        private readonly string _databaseName = "v1-" + Guid.NewGuid();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Development: cấu hình giống host DB dev (user-secrets nếu có) để các lớp cấu hình tĩnh cũ (ConfigurationCommon,
            // DapperRepository) nhận cùng giá trị khi 2 factory chạy song song. EF bị thay bằng InMemory nên không đụng DB dev.
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                // Ưu tiên thấp nhất: chỉ dùng khi máy không có user-secrets (CI) – để qua StartupSecrets.Validate.
                config.Sources.Insert(0, new MemoryConfigurationSource
                {
                    InitialData = new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:MainConnectionString"] = "Server=unused-in-tests;Database=none;Trusted_Connection=True;",
                    },
                });
                // Ưu tiên cao nhất: khóa JWT riêng của test.
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:SigningKey"] = SigningKey,
                    ["Jwt:AccessTokenMinutes"] = "30",
                    ["JwtIssuerOptions:Issuer"] = Issuer,
                    ["JwtIssuerOptions:Audience"] = Audience,
                });
                // BE2.3: cấu hình riêng của 1 test (vd. BusinessRules:ApprovalSteps:CONTRACT:0) – đặt trước lần gọi CreateClient đầu tiên.
                if (Settings.Count > 0)
                {
                    config.AddInMemoryCollection(Settings);
                }
            });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<DatabaseContext>>();
                services.AddDbContext<DatabaseContext>(o => o.UseInMemoryDatabase(_databaseName));
                // T13: đồng hồ điều khiển được (mặc định = giờ VN thật, không đổi hành vi test cũ).
                services.RemoveAll<IClock>();
                services.AddSingleton<IClock>(Clock);
            });
        }

        /// <summary>BE2.3: ghi đè cấu hình cho factory riêng của 1 test (không dùng với fixture chung đã khởi động).</summary>
        public Dictionary<string, string?> Settings { get; } = [];

        /// <summary>T13: đặt <c>Clock.FixedNow</c> để thử giờ chốt đơn.</summary>
        public TestClock Clock { get; } = new();

        private readonly object _seedGate = new();
        private bool _seeded;

        /// <summary>T07: user mặc định của test là admin (bỏ qua kiểm tra quyền); test phân quyền tự tạo user thường.</summary>
        private void EnsureAdminUser()
        {
            lock (_seedGate)
            {
                if (_seeded)
                {
                    return;
                }
                SeedAsync(db =>
                {
                    db.User.Add(new AppDelicare.Model.Entity.User { Id = UserId, UserName = "test-admin", IsAdmin = true, IsActive = true });
                    return Task.CompletedTask;
                }).GetAwaiter().GetResult();
                _seeded = true;
            }
        }

        public HttpClient CreateAuthorizedClient(Guid? userId = null)
        {
            EnsureAdminUser();
            var client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(userId ?? UserId));
            return client;
        }

        public static string CreateToken(Guid userId, DateTime? expires = null)
        {
            var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(Issuer, Audience,
                [new Claim(CurrentActor.UserIdClaim, userId.ToString())],
                notBefore: DateTime.UtcNow.AddMinutes(-1),
                expires: expires ?? DateTime.UtcNow.AddMinutes(30),
                signingCredentials: credentials);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>Ghi dữ liệu mẫu trực tiếp vào DB InMemory (người thao tác: SYSTEM).</summary>
        public async Task SeedAsync(Func<DatabaseContext, Task> seed)
        {
            using var scope = Services.CreateScope();
            var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            using (actor.BeginScope(ActorInfo.ForSystem(UserId)))
            {
                await seed(db);
                await db.SaveChangesAsync();
            }
        }

        public async Task<T> QueryAsync<T>(Func<DatabaseContext, Task<T>> query)
        {
            using var scope = Services.CreateScope();
            return await query(scope.ServiceProvider.GetRequiredService<DatabaseContext>());
        }
    }
}
