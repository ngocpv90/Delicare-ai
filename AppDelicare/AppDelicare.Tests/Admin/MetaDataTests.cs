using System.Net;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;

namespace AppDelicare.Tests.Admin
{
    /// <summary>T45B.5 – MetaData/GetStatuses (STATUS_CATALOG) và MetaData/GetReferenceLists (appsettings ReferenceLists).</summary>
    public class MetaDataTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public MetaDataTests(InMemoryApiFactory factory) => _factory = factory;

        [Fact]
        public async Task Statuses_cover_the_reference_catalog_plus_code_status_classes_and_filter_by_key()
        {
            using var c = _factory.CreateAuthorizedClient();

            var all = await c.GetResultAsync("/api/MetaData/GetStatuses");
            // UG-1 C-10: 47 bộ của STATUS_CATALOG.md + cột có lớp *Status.cs mà tài liệu chưa có (DeliveryOrder, CleaningBatche…).
            all.ListData.GetArrayLength().Should().Be(AppDelicare.Model.Helper.StatusCatalogEffective.All.Count).And.BeGreaterThan(47);

            var recipe = (await c.GetResultAsync("/api/MetaData/GetStatuses?key=Recipe.Status")).ListData[0];
            recipe.GetProperty("listItem").EnumerateArray().Select(x => x.GetProperty("code").GetString())
                .Should().Equal("Draft", "PendingApproval", "Approved", "Locked", "Obsolete");
            recipe.GetProperty("listItem")[0].GetProperty("label").GetString().Should().Be("Nháp");
        }

        [Fact]
        public async Task Reference_lists_come_from_configuration()
        {
            using var c = _factory.CreateAuthorizedClient();

            var all = await c.GetResultAsync("/api/MetaData/GetReferenceLists");
            all.ListData.GetArrayLength().Should().BeGreaterThanOrEqualTo(9);
            var meal = (await c.GetResultAsync("/api/MetaData/GetReferenceLists?name=MealType")).ListData[0];
            meal.GetProperty("listItem").EnumerateArray().Select(x => x.GetProperty("code").GetString()).Should().Contain("TRUA");
        }

        [Fact]
        public async Task Any_logged_in_user_can_read_meta_data()
        {
            var userId = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.User.Add(new User { Id = userId, UserName = "meta-no-rights", IsActive = true });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient(userId);

            (await c.GetAsync("/api/MetaData/GetStatuses")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await c.GetAsync("/api/MetaData/GetReferenceLists")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await _factory.CreateClient().GetAsync("/api/MetaData/GetStatuses")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }
}
