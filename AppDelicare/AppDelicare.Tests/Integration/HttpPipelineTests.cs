using System.Net;
using System.Text.Json;
using AppDelicare.WebApi.Infrastructure;
using FluentAssertions;

namespace AppDelicare.Tests.Integration
{
    /// <summary>T03 – pipeline HTTP: route cũ giữ nguyên dạng, route v1 trả ProblemDetails, CORS theo cấu hình, Swagger.</summary>
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
        public async Task V1_invalid_route_value_returns_400_envelope()
        {
            Skip.If(_factory.SkipReason != null, _factory.SkipReason);
            using var client = _factory.CreateAuthorizedClient();
            client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, "t03-bad-table");

            var response = await client.GetAsync($"/api/v1/audit/Bad-Table;DROP/{Guid.NewGuid()}/history");

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
            var error = doc.RootElement.GetProperty("errors")[0];
            error.GetProperty("code").GetString().Should().Be("VALIDATION_FAILED");
            error.GetProperty("field").GetString().Should().Be("table");
            doc.RootElement.GetProperty("traceId").GetString().Should().Be("t03-bad-table");
        }

        [SkippableFact]
        public async Task V1_out_of_range_query_returns_400_envelope()
        {
            Skip.If(_factory.SkipReason != null, _factory.SkipReason);
            using var client = _factory.CreateAuthorizedClient();

            var response = await client.GetAsync($"/api/v1/audit/TestDemo/{Guid.NewGuid()}/history?take=0");

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            doc.RootElement.GetProperty("errors")[0].GetProperty("field").GetString().Should().Be("take");
        }

        [SkippableFact]
        public async Task V1_valid_request_returns_200_with_list()
        {
            Skip.If(_factory.SkipReason != null, _factory.SkipReason);
            using var client = _factory.CreateAuthorizedClient();

            var response = await client.GetAsync($"/api/v1/audit/TestDemo/{Guid.NewGuid()}/history?take=5");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
            doc.RootElement.GetProperty("data").GetArrayLength().Should().Be(0);
        }

        [Fact]
        public async Task V1_without_token_is_401()
        {
            using var client = _factory.CreateClient();

            var response = await client.GetAsync($"/api/v1/audit/TestDemo/{Guid.NewGuid()}/history");

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
        public async Task Swagger_json_contains_v1_endpoint_and_bearer_scheme()
        {
            using var client = _factory.CreateClient();

            var response = await client.GetAsync("/swagger/v1/swagger.json");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            string json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            doc.RootElement.GetProperty("paths").TryGetProperty("/api/v1/audit/{table}/{id}/history", out var path).Should().BeTrue();
            path.GetProperty("get").GetProperty("summary").GetString().Should().Contain("Lịch sử");
            path.GetProperty("get").GetProperty("responses").TryGetProperty("400", out _).Should().BeTrue();
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
