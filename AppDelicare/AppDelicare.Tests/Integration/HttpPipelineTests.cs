using System.Net;
using System.Text.Json;
using AppDelicare.WebApi.Infrastructure;
using FluentAssertions;

namespace AppDelicare.Tests.Integration
{
    /// <summary>T03 / T45R – pipeline HTTP theo khung cũ: route api/[controller]/<Action>, 401 khi thiếu token, CORS theo cấu hình, Swagger.</summary>
    [Collection(ApiCollection.Name)]
    public class HttpPipelineTests(ApiFactory factory)
    {
        private readonly ApiFactory _factory = factory;

        [SkippableFact]
        public async Task Legacy_Supplier_GetAll_keeps_old_shape_and_http_200()
        {
            Skip.If(_factory.SkipReason != null, _factory.SkipReason);
            using var client = _factory.CreateAuthorizedClient();

            var response = await client.GetAsync("/api/Supplier/GetAll?PageIndex=1&PageSize=5");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            root.TryGetProperty("totalRecords", out _).Should().BeTrue();
            root.GetProperty("listData").ValueKind.Should().Be(JsonValueKind.Array);
            root.GetProperty("responseCode").GetInt32().Should().Be(200);
            root.TryGetProperty("responseMessage", out _).Should().BeTrue();
        }

        [Fact]
        public async Task Legacy_route_without_token_is_401()
        {
            using var client = _factory.CreateClient();

            var response = await client.GetAsync("/api/Supplier/GetAll?PageIndex=1&PageSize=5");

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [SkippableFact]
        public async Task Record_history_uses_legacy_AuditChange_GetAll()
        {
            Skip.If(_factory.SkipReason != null, _factory.SkipReason);
            using var client = _factory.CreateAuthorizedClient();

            var response = await client.GetAsync($"/api/AuditChange/GetAll?EntityName=TestDemo&IdChange={Guid.NewGuid()}&Page=1&PageSize=5");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            doc.RootElement.GetProperty("responseCode").GetInt32().Should().Be(200);
            doc.RootElement.GetProperty("listData").GetArrayLength().Should().Be(0);
        }

        [SkippableFact]
        public async Task New_legacy_actions_run_on_sql_server()
        {
            Skip.If(_factory.SkipReason != null, _factory.SkipReason);
            using var client = _factory.CreateAuthorizedClient();

            // SP đã sinh lại có @EffectiveOn; SP mới cho bảng Id bigint; ma trận quyền và người dùng hiện tại (EF).
            (await client.GetResultAsync("/api/SupplierMaterial/GetAll?Page=1&PageSize=5&EffectiveOn=2026-11-15")).Code.Should().Be(200);
            (await client.GetResultAsync("/api/CoreDataMergeConflict/GetAll?Page=1&PageSize=5&IsResolved=false")).Code.Should().Be(200);
            (await client.GetResultAsync("/api/PricePolicy/GetAll?Page=1&PageSize=5&EffectiveOn=2026-11-15")).Code.Should().Be(200);
            var me = (await client.GetResultAsync("/api/User/GetCurrent")).Ok();
            me.GetProperty("user").GetProperty("userName").GetString().Should().Be("admin");
            (await client.GetResultAsync("/api/ObjectMenu/GetTree?includeInactive=false")).Ok().GetArrayLength().Should().BeGreaterThan(5);
            (await client.GetResultAsync("/api/OrganizationUnit/GetBranches")).Code.Should().Be(200);
        }

        [Fact]
        public async Task Business_action_without_token_is_401()
        {
            using var client = _factory.CreateClient();

            var response = await client.GetAsync($"/api/Recipe/CalculateCost?id={Guid.NewGuid()}");

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Theory]
        [InlineData("http://localhost:4200", true)]
        [InlineData("http://evil.example.com", false)]
        public async Task Cors_only_allows_configured_origins(string origin, bool allowed)
        {
            using var client = _factory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Options, "/api/Supplier/GetAll");
            request.Headers.Add("Origin", origin);
            request.Headers.Add("Access-Control-Request-Method", "GET");

            var response = await client.SendAsync(request);

            response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values).Should().Be(allowed);
            if (allowed)
            {
                values!.Single().Should().Be(origin);
            }
        }

        [Fact]
        public async Task Swagger_json_has_legacy_business_actions_no_v1_and_bearer_scheme()
        {
            using var client = _factory.CreateClient();

            var response = await client.GetAsync("/swagger/v1/swagger.json");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            string json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var paths = doc.RootElement.GetProperty("paths");
            paths.TryGetProperty("/api/Recipe/Lock", out var path).Should().BeTrue();
            path.GetProperty("post").GetProperty("summary").GetString().Should().Contain("khóa");
            paths.EnumerateObject().Should().NotContain(p => p.Name.StartsWith("/api/v1", StringComparison.OrdinalIgnoreCase));
            doc.RootElement.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer")
                .GetProperty("scheme").GetString().Should().Be("bearer");
        }

        [Fact]
        public async Task Every_response_has_correlation_header()
        {
            using var client = _factory.CreateClient();

            var response = await client.GetAsync("/api/Supplier/GetAll");

            response.Headers.Contains(CorrelationIdMiddleware.HeaderName).Should().BeTrue();
        }
    }
}
