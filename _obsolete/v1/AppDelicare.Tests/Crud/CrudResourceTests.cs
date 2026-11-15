using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using AppDelicare.WebApi.Api;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Crud
{
    /// <summary>T45B.1 – CRUD chuẩn v1 theo metadata DDL (Spec 2.1).</summary>
    public class CrudResourceTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public CrudResourceTests(InMemoryApiFactory factory) => _factory = factory;

        private static async Task<JsonElement> Body(HttpResponseMessage r) => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.Clone();
        private static string Code(JsonElement b) => b.GetProperty("errors")[0].GetProperty("code").GetString()!;
        private static string Field(JsonElement b) => b.GetProperty("errors")[0].GetProperty("field").GetString()!;

        private async Task<Guid> CreateWarehouseAsync(HttpClient c, string code, string status = "Active")
        {
            var r = await c.PostAsJsonAsync("/api/v1/scm/warehouses", new { code, name = "Kho " + code, status });
            r.StatusCode.Should().Be(HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
            return (await Body(r)).GetProperty("data").GetProperty("id").GetGuid();
        }

        [Fact]
        public async Task Create_get_list_summary_roundtrip()
        {
            using var c = _factory.CreateAuthorizedClient();
            var id = await CreateWarehouseAsync(c, "WH-RT-A");
            await CreateWarehouseAsync(c, "WH-RT-B", "Inactive");

            var get = await c.GetAsync($"/api/v1/scm/warehouses/{id}");
            get.StatusCode.Should().Be(HttpStatusCode.OK);
            get.Headers.ETag.Should().NotBeNull();
            var data = (await Body(get)).GetProperty("data");
            data.GetProperty("code").GetString().Should().Be("WH-RT-A");
            data.TryGetProperty("isDeleted", out _).Should().BeFalse("cột xóa mềm không trả ra API");
            data.TryGetProperty("deleteddBy", out _).Should().BeFalse();

            var list = await Body(await c.GetAsync("/api/v1/scm/warehouses?q=WH-RT-&status=Active&sort=code:asc&pageSize=5"));
            list.GetProperty("data").GetProperty("total").GetInt32().Should().Be(1);
            list.GetProperty("data").GetProperty("items")[0].GetProperty("code").GetString().Should().Be("WH-RT-A");

            var summary = await Body(await c.GetAsync("/api/v1/scm/warehouses/summary?q=WH-RT-"));
            summary.GetProperty("data").GetProperty("total").GetInt32().Should().Be(2);
            summary.GetProperty("data").GetProperty("byStatus").GetArrayLength().Should().Be(2);
        }

        [Fact]
        public async Task Validation_errors_cover_unknown_deprecated_type_and_length()
        {
            using var c = _factory.CreateAuthorizedClient();

            var unknown = await c.PostAsJsonAsync("/api/v1/scm/warehouses", new { code = "X1", hacker = 1 });
            var badType = await c.PostAsJsonAsync("/api/v1/scm/warehouses", new { code = 123 });
            var tooLong = await c.PostAsJsonAsync("/api/v1/scm/warehouses", new { code = new string('x', 300) });
            var deprecated = await c.PostAsJsonAsync("/api/v1/scm/uom-conversions", new { fromUomId = Guid.NewGuid(), toUnitId = Guid.NewGuid(), fromUnitId = Guid.NewGuid(), conversionFactor = 1 });
            var missing = await c.PostAsJsonAsync("/api/v1/scm/uom-conversions", new { conversionFactor = 2 });

            unknown.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            Field(await Body(unknown)).Should().Be("hacker");
            badType.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            tooLong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            deprecated.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await Body(deprecated)).GetProperty("errors")[0].GetProperty("message").GetString().Should().Contain("deprecated");
            var missingFields = (await Body(missing)).GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("field").GetString()).ToList();
            missingFields.Should().Contain(["fromUnitId", "toUnitId"], "cột thay thế của cột cũ NOT NULL là bắt buộc");
        }

        [Fact]
        public async Task Duplicate_code_is_409_and_ref_not_found_is_422()
        {
            using var c = _factory.CreateAuthorizedClient();
            await CreateWarehouseAsync(c, "WH-T45-DUP");

            var dup = await c.PostAsJsonAsync("/api/v1/scm/warehouses", new { code = "WH-T45-DUP", name = "x" });
            var badRef = await c.PostAsJsonAsync("/api/v1/scm/storage-zones", new { code = "Z1", name = "Khu 1", warehouseId = Guid.NewGuid() });

            dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
            Code(await Body(dup)).Should().Be(ErrorCodes.Duplicate);
            badRef.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            var b = await Body(badRef);
            Code(b).Should().Be(ErrorCodes.RefNotFound);
            Field(b).Should().Be("warehouseId");
        }

        [Fact]
        public async Task Reference_label_if_match_and_in_use_delete()
        {
            using var c = _factory.CreateAuthorizedClient();
            var wh = await CreateWarehouseAsync(c, "WH-T45-REF");
            var zone = await c.PostAsJsonAsync("/api/v1/scm/storage-zones", new { code = "Z-REF", name = "Khu lạnh", warehouseId = wh });
            zone.StatusCode.Should().Be(HttpStatusCode.Created);
            (await Body(zone)).GetProperty("data").GetProperty("warehouseIdLabel").GetString().Should().Be("Kho WH-T45-REF");

            var get = await c.GetAsync($"/api/v1/scm/warehouses/{wh}");
            string etag = get.Headers.ETag!.Tag;
            using (var put = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/scm/warehouses/{wh}") { Content = JsonContent.Create(new { name = "Kho mới" }) })
            {
                put.Headers.TryAddWithoutValidation("If-Match", etag);
                (await c.SendAsync(put)).StatusCode.Should().Be(HttpStatusCode.OK);
            }
            using (var stale = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/scm/warehouses/{wh}") { Content = JsonContent.Create(new { name = "Kho cũ" }) })
            {
                stale.Headers.TryAddWithoutValidation("If-Match", etag);
                var r = await c.SendAsync(stale);
                r.StatusCode.Should().Be(HttpStatusCode.Conflict);
                Code(await Body(r)).Should().Be(ErrorCodes.ConcurrencyConflict);
            }

            var inUse = await c.DeleteAsync($"/api/v1/scm/warehouses/{wh}");
            inUse.StatusCode.Should().Be(HttpStatusCode.Conflict);
            Code(await Body(inUse)).Should().Be(ErrorCodes.InUse);
        }

        [Fact]
        public async Task Deprecated_columns_are_written_in_parallel_and_rules_apply()
        {
            using var c = _factory.CreateAuthorizedClient();
            Guid kg = Guid.NewGuid(), g = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.UnitMeasure.AddRange(new UnitMeasure { Id = kg, Name = "kg" }, new UnitMeasure { Id = g, Name = "g" });
                return Task.CompletedTask;
            });

            var zero = await c.PostAsJsonAsync("/api/v1/scm/uom-conversions", new { fromUnitId = kg, toUnitId = g, conversionFactor = 0 });
            var same = await c.PostAsJsonAsync("/api/v1/scm/uom-conversions", new { fromUnitId = kg, toUnitId = kg, conversionFactor = 1000 });
            var ok = await c.PostAsJsonAsync("/api/v1/scm/uom-conversions", new { fromUnitId = kg, toUnitId = g, conversionFactor = 1000 });
            var dupPair = await c.PostAsJsonAsync("/api/v1/scm/uom-conversions", new { fromUnitId = kg, toUnitId = g, conversionFactor = 999 });

            zero.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            same.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            ok.StatusCode.Should().Be(HttpStatusCode.Created);
            dupPair.StatusCode.Should().Be(HttpStatusCode.Conflict);
            var id = (await Body(ok)).GetProperty("data").GetProperty("id").GetGuid();
            var stored = await _factory.QueryAsync(db => db.UomConversion.AsNoTracking().SingleAsync(x => x.Id == id));
            stored.FromUomId.Should().Be(kg, "cột cũ FromUomId ghi song song từ FromUnitId");
            stored.ToUomId.Should().Be(g);
        }

        [Fact]
        public async Task Read_only_resource_rejects_writes_with_405()
        {
            using var c = _factory.CreateAuthorizedClient();

            var r = await c.PostAsJsonAsync("/api/v1/production/standard-costs", new { });

            r.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
            (await c.GetAsync("/api/v1/production/standard-costs")).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Permission_is_enforced_per_screen()
        {
            var userId = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                var group = Guid.NewGuid();
                var menu = Guid.NewGuid();
                db.User.Add(new User { Id = userId, UserName = "t45-viewer", IsActive = true });
                db.UserGroup.Add(new UserGroup { Id = group, Name = "T45-VIEW" });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = userId, UserGroupId = group });
                db.ObjectMenu.Add(new ObjectMenu { Id = menu, Name = "Kho", MetaLink = "#/scm-warehouse", IsActive = true });
                db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = group, ObjectMenuId = menu, AllowAccess = true });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient(userId);

            (await c.GetAsync("/api/v1/scm/warehouses")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await c.PostAsJsonAsync("/api/v1/scm/warehouses", new { code = "NOPE" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await c.GetAsync("/api/v1/scm/suppliers")).StatusCode.Should().Be(HttpStatusCode.Forbidden, "không có quyền màn #/scm-supplier");
        }

        [Fact]
        public async Task Schema_and_resource_list_describe_fields_for_frontend()
        {
            using var c = _factory.CreateAuthorizedClient();

            var schema = (await Body(await c.GetAsync("/api/v1/meta/schema/scm/storage-zones"))).GetProperty("data");
            var resources = (await Body(await c.GetAsync("/api/v1/meta/resources"))).GetProperty("data");

            schema.GetProperty("screen").GetString().Should().Be("scm-zone");
            var warehouseField = schema.GetProperty("fields").EnumerateArray().Single(f => f.GetProperty("field").GetString() == "warehouseId");
            warehouseField.GetProperty("required").GetBoolean().Should().BeTrue();
            warehouseField.GetProperty("refTable").GetString().Should().Be("Warehouse");
            schema.GetProperty("fields").EnumerateArray().Should().NotContain(f => f.GetProperty("field").GetString() == "zoneId", "cột cũ không có trong schema");
            resources.GetArrayLength().Should().BeGreaterThanOrEqualTo(39);
            (await c.GetAsync("/api/v1/meta/schema/scm/nothing")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }
}
