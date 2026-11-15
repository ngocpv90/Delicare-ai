using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.WebApi.Api;
using AppDelicare.WebApi.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Integration
{
    /// <summary>
    /// T03 – endpoint v1 mẫu <c>/api/v1/hr/shifts</c>: 200 envelope, 400 / 422 mã lỗi, 409 If-Match / DUPLICATE,
    /// Idempotency-Key gửi 2 lần chỉ ghi 1 lần, lỗi không body cũng là envelope.
    /// </summary>
    public class V1ShiftsTests : IClassFixture<InMemoryApiFactory>
    {
        private const string Url = "/api/v1/hr/shifts";
        private readonly InMemoryApiFactory _factory;

        public V1ShiftsTests(InMemoryApiFactory factory) => _factory = factory;

        private static object NewShift(string code, string start = "10:00:00", string end = "14:00:00", Guid? companyId = null)
            => new { code, name = "Ca " + code, startTime = start, endTime = end, companyId, note = "[DEMO] T03" };

        private static async Task<JsonElement> BodyAsync(HttpResponseMessage response)
        {
            string raw = await response.Content.ReadAsStringAsync();
            return JsonDocument.Parse(raw).RootElement.Clone();
        }

        private static string FirstErrorCode(JsonElement body) => body.GetProperty("errors")[0].GetProperty("code").GetString()!;

        [Fact]
        public async Task Create_returns_201_envelope_with_etag_and_get_list_returns_paged_envelope()
        {
            using var client = _factory.CreateAuthorizedClient();
            client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, "t03-create");

            var created = await client.PostAsJsonAsync(Url, NewShift("T03-A", "18:00:00", "02:00:00"));

            created.StatusCode.Should().Be(HttpStatusCode.Created);
            created.Headers.ETag.Should().NotBeNull();
            created.Headers.Location!.ToString().Should().Contain("/api/v1/hr/shifts/");
            var body = await BodyAsync(created);
            body.GetProperty("success").GetBoolean().Should().BeTrue();
            body.GetProperty("traceId").GetString().Should().Be("t03-create");
            body.GetProperty("errors").GetArrayLength().Should().Be(0);
            var data = body.GetProperty("data");
            data.GetProperty("code").GetString().Should().Be("T03-A");
            data.GetProperty("isOvernight").GetBoolean().Should().BeTrue("18:00–02:00 là ca qua đêm (Spec 4.7)");
            data.GetProperty("status").GetString().Should().Be("Active");

            var list = await client.GetAsync($"{Url}?page=1&pageSize=5&q=T03-A&sort=code:asc");
            list.StatusCode.Should().Be(HttpStatusCode.OK);
            var listBody = await BodyAsync(list);
            var page = listBody.GetProperty("data");
            page.GetProperty("total").GetInt32().Should().Be(1);
            page.GetProperty("page").GetInt32().Should().Be(1);
            page.GetProperty("pageSize").GetInt32().Should().Be(5);
            page.GetProperty("items")[0].GetProperty("code").GetString().Should().Be("T03-A");
        }

        [Fact]
        public async Task Missing_required_fields_returns_400_validation_failed_with_fields()
        {
            using var client = _factory.CreateAuthorizedClient();

            var response = await client.PostAsJsonAsync(Url, new { startTime = "08:00:00" });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var body = await BodyAsync(response);
            body.GetProperty("success").GetBoolean().Should().BeFalse();
            body.GetProperty("data").ValueKind.Should().Be(JsonValueKind.Null);
            var fields = body.GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("field").GetString()).ToList();
            fields.Should().Contain(["code", "name", "endTime"]);
            body.GetProperty("errors").EnumerateArray().Should().OnlyContain(e => e.GetProperty("code").GetString() == ErrorCodes.ValidationFailed);
        }

        [Fact]
        public async Task Bad_time_format_returns_400()
        {
            using var client = _factory.CreateAuthorizedClient();

            var response = await client.PostAsJsonAsync(Url, new { code = "T03-BAD", name = "x", startTime = "25:99", endTime = "10:00:00" });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            FirstErrorCode(await BodyAsync(response)).Should().Be(ErrorCodes.ValidationFailed);
        }

        [Fact]
        public async Task Unknown_company_returns_422_ref_not_found()
        {
            using var client = _factory.CreateAuthorizedClient();

            var response = await client.PostAsJsonAsync(Url, NewShift("T03-REF", companyId: Guid.NewGuid()));

            response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            var body = await BodyAsync(response);
            FirstErrorCode(body).Should().Be(ErrorCodes.RefNotFound);
            body.GetProperty("errors")[0].GetProperty("field").GetString().Should().Be("companyId");
        }

        [Fact]
        public async Task Existing_company_is_accepted()
        {
            var companyId = Guid.NewGuid();
            await _factory.SeedAsync(db => { db.Company.Add(new Company { Id = companyId }); return Task.CompletedTask; });
            using var client = _factory.CreateAuthorizedClient();

            var response = await client.PostAsJsonAsync(Url, NewShift("T03-COMP", companyId: companyId));

            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        [Fact]
        public async Task Same_start_and_end_returns_422_business_rule()
        {
            using var client = _factory.CreateAuthorizedClient();

            var response = await client.PostAsJsonAsync(Url, NewShift("T03-ZERO", "08:00:00", "08:00:00"));

            response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            FirstErrorCode(await BodyAsync(response)).Should().Be(ErrorCodes.BusinessRule);
        }

        [Fact]
        public async Task Duplicate_code_returns_409_duplicate()
        {
            using var client = _factory.CreateAuthorizedClient();
            (await client.PostAsJsonAsync(Url, NewShift("T03-DUP"))).StatusCode.Should().Be(HttpStatusCode.Created);

            var response = await client.PostAsJsonAsync(Url, NewShift("T03-DUP"));

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            FirstErrorCode(await BodyAsync(response)).Should().Be(ErrorCodes.Duplicate);
        }

        [Fact]
        public async Task Put_with_stale_if_match_returns_409_concurrency_conflict_and_fresh_etag_succeeds()
        {
            using var client = _factory.CreateAuthorizedClient();
            var created = await client.PostAsJsonAsync(Url, NewShift("T03-ETAG"));
            var id = (await BodyAsync(created)).GetProperty("data").GetProperty("id").GetGuid();
            string etag = created.Headers.ETag!.Tag;

            // Lần 1: If-Match đúng → 200, ETag đổi.
            using var put1 = new HttpRequestMessage(HttpMethod.Put, $"{Url}/{id}") { Content = JsonContent.Create(NewShift("T03-ETAG", "11:00:00", "15:00:00")) };
            put1.Headers.TryAddWithoutValidation("If-Match", etag);
            var ok = await client.SendAsync(put1);
            ok.StatusCode.Should().Be(HttpStatusCode.OK);
            ok.Headers.ETag!.Tag.Should().NotBe(etag);
            (await BodyAsync(ok)).GetProperty("data").GetProperty("startTime").GetString().Should().Be("11:00:00");

            // Lần 2: dùng lại ETag cũ → 409 CONCURRENCY_CONFLICT, dữ liệu không đổi.
            using var put2 = new HttpRequestMessage(HttpMethod.Put, $"{Url}/{id}") { Content = JsonContent.Create(NewShift("T03-ETAG", "12:00:00", "16:00:00")) };
            put2.Headers.TryAddWithoutValidation("If-Match", etag);
            var conflict = await client.SendAsync(put2);
            conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
            FirstErrorCode(await BodyAsync(conflict)).Should().Be(ErrorCodes.ConcurrencyConflict);
            var start = await _factory.QueryAsync(db => db.Shift.AsNoTracking().Where(x => x.Id == id).Select(x => x.StartTime).SingleAsync());
            start.Should().Be(TimeSpan.FromHours(11));
        }

        [Fact]
        public async Task Idempotency_key_sent_twice_writes_only_once()
        {
            using var client = _factory.CreateAuthorizedClient();
            const string key = "t03-idem-0001";

            async Task<HttpResponseMessage> SendAsync()
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, Url) { Content = JsonContent.Create(NewShift("T03-IDEM")) };
                request.Headers.Add(IdempotencyFilter.HeaderName, key);
                return await client.SendAsync(request);
            }

            var first = await SendAsync();
            var second = await SendAsync();

            first.StatusCode.Should().Be(HttpStatusCode.Created);
            second.StatusCode.Should().Be(HttpStatusCode.Created, "lần gửi lại trả lại mã HTTP của lần đầu");
            second.Headers.GetValues(IdempotencyFilter.ReplayedHeader).Single().Should().Be("true");
            (await BodyAsync(second)).GetProperty("success").GetBoolean().Should().BeTrue();

            int shifts = await _factory.QueryAsync(db => db.Shift.CountAsync(x => x.Code == "T03-IDEM" && !x.IsDeleted));
            shifts.Should().Be(1);
            int logs = await _factory.QueryAsync(db => db.IntegrationLog.CountAsync(x => x.IdempotencyKey == key));
            logs.Should().Be(1);
        }

        [Fact]
        public async Task Idempotency_key_reused_with_different_payload_returns_422()
        {
            using var client = _factory.CreateAuthorizedClient();
            const string key = "t03-idem-0002";
            using var r1 = new HttpRequestMessage(HttpMethod.Post, Url) { Content = JsonContent.Create(NewShift("T03-IDEM2")) };
            r1.Headers.Add(IdempotencyFilter.HeaderName, key);
            (await client.SendAsync(r1)).StatusCode.Should().Be(HttpStatusCode.Created);

            using var r2 = new HttpRequestMessage(HttpMethod.Post, Url) { Content = JsonContent.Create(NewShift("T03-IDEM3")) };
            r2.Headers.Add(IdempotencyFilter.HeaderName, key);
            var response = await client.SendAsync(r2);

            response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            int created = await _factory.QueryAsync(db => db.Shift.CountAsync(x => x.Code == "T03-IDEM3"));
            created.Should().Be(0);
        }

        [Fact]
        public async Task Failed_command_is_not_recorded_so_retry_with_same_key_works()
        {
            using var client = _factory.CreateAuthorizedClient();
            const string key = "t03-idem-0003";
            using var bad = new HttpRequestMessage(HttpMethod.Post, Url) { Content = JsonContent.Create(NewShift("T03-RETRY", "09:00:00", "09:00:00")) };
            bad.Headers.Add(IdempotencyFilter.HeaderName, key);
            (await client.SendAsync(bad)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

            using var good = new HttpRequestMessage(HttpMethod.Post, Url) { Content = JsonContent.Create(NewShift("T03-RETRY", "09:00:00", "17:00:00")) };
            good.Headers.Add(IdempotencyFilter.HeaderName, key);
            var response = await client.SendAsync(good);

            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        [Fact]
        public async Task Get_unknown_and_deleted_return_404_envelope()
        {
            using var client = _factory.CreateAuthorizedClient();
            var created = await client.PostAsJsonAsync(Url, NewShift("T03-DEL"));
            var id = (await BodyAsync(created)).GetProperty("data").GetProperty("id").GetGuid();

            (await client.DeleteAsync($"{Url}/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

            var afterDelete = await client.GetAsync($"{Url}/{id}");
            afterDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
            FirstErrorCode(await BodyAsync(afterDelete)).Should().Be(ErrorCodes.NotFound);
            var deletedBy = await _factory.QueryAsync(db => db.Shift.AsNoTracking().Where(x => x.Id == id).Select(x => x.DeleteddBy).SingleAsync());
            deletedBy.Should().Be(InMemoryApiFactory.UserId);
        }

        [Fact]
        public async Task Unauthenticated_v1_request_returns_401_envelope()
        {
            using var client = _factory.CreateClient();

            var response = await client.GetAsync(Url);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            var body = await BodyAsync(response);
            body.GetProperty("success").GetBoolean().Should().BeFalse();
            FirstErrorCode(body).Should().Be(ErrorCodes.Unauthorized);
        }

        [Fact]
        public async Task Unknown_v1_route_returns_404_envelope()
        {
            using var client = _factory.CreateAuthorizedClient();

            var response = await client.GetAsync("/api/v1/hr/does-not-exist");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            FirstErrorCode(await BodyAsync(response)).Should().Be(ErrorCodes.NotFound);
        }

        [Theory]
        [InlineData("sort=password:asc")]
        [InlineData("sort=code:sideways")]
        [InlineData("pageSize=500")]
        [InlineData("page=0")]
        public async Task Invalid_list_parameters_return_400(string query)
        {
            using var client = _factory.CreateAuthorizedClient();

            var response = await client.GetAsync($"{Url}?{query}");

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            FirstErrorCode(await BodyAsync(response)).Should().Be(ErrorCodes.ValidationFailed);
        }

        [Fact]
        public async Task Invalid_branch_header_returns_400_and_valid_one_is_accepted()
        {
            using var client = _factory.CreateAuthorizedClient();
            using var bad = new HttpRequestMessage(HttpMethod.Get, Url);
            bad.Headers.Add("X-Branch-Id", "not-a-guid");
            var badResponse = await client.SendAsync(bad);
            badResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await BodyAsync(badResponse)).GetProperty("errors")[0].GetProperty("field").GetString().Should().Be("X-Branch-Id");

            using var good = new HttpRequestMessage(HttpMethod.Get, Url);
            good.Headers.Add("X-Branch-Id", Guid.NewGuid().ToString());
            (await client.SendAsync(good)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public void Password_fields_are_never_serialized_but_still_deserialized()
        {
            var user = new User { Id = Guid.NewGuid(), UserName = "u1", PassWord = "secret-hash" };

            string json = JsonSerializer.Serialize(user, ApiJson.Options);
            var login = JsonSerializer.Deserialize<AppDelicare.Model.Request.LoginRequest>("{\"userName\":\"a\",\"passWord\":\"b\"}", ApiJson.Options);

            json.Should().NotContainEquivalentOf("password").And.NotContain("secret-hash");
            login!.PassWord.Should().Be("b");
        }
    }
}
