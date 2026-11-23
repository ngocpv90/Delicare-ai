using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace AppDelicare.Tests.Admin
{
    /// <summary>
    /// D-78 (6): danh mục chỉ số (kpi-definition) chỉ admin / FIN-IT quản lý, nhưng người có quyền xem KPI ngày vẫn ĐỌC được danh mục (màn KPI cần tên chỉ số);
    /// không quyền màn nào → 403; có quyền xem KPI ngày vẫn KHÔNG sửa được danh mục.
    /// </summary>
    public class TechScreenPermissionTests(InMemoryApiFactory factory) : IClassFixture<InMemoryApiFactory>
    {
        private async Task<Guid> UserAsync(string? viewScreen)
        {
            Guid user = Guid.NewGuid(), group = Guid.NewGuid();
            await factory.SeedAsync(async db =>
            {
                db.User.Add(new User { Id = user, UserName = "tech-" + user.ToString("N")[..6], IsActive = true });
                db.UserGroup.Add(new UserGroup { Id = group, Name = "TG-" + group.ToString("N")[..6] });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = user, UserGroupId = group });
                foreach (string link in new[] { "#/kpi-daily", "#/kpi-definition" })
                {
                    if (!await db.ObjectMenu.AnyAsync(m => m.MetaLink == link))
                    {
                        db.ObjectMenu.Add(new ObjectMenu { Id = Guid.NewGuid(), Name = link, MetaLink = link, IsActive = true, LevelShow = 2 });
                        await db.SaveChangesAsync();
                    }
                }
                if (viewScreen != null)
                {
                    var menu = await db.ObjectMenu.FirstAsync(m => m.MetaLink == viewScreen);
                    db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = group, ObjectMenuId = menu.Id, AllowAccess = true });
                }
            });
            return user;
        }

        [Fact]
        public async Task Kpi_viewer_reads_metric_catalog_but_cannot_edit_and_others_are_denied()
        {
            using var viewer = factory.CreateAuthorizedClient(await UserAsync("#/kpi-daily"));
            (await viewer.GetAsync("/api/KpiMetricDefinition/GetAllData")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await viewer.PostAsync("/api/KpiMetricDefinition/Insert", new FormUrlEncodedContent(new Dictionary<string, string> { ["MetricCode"] = "X", ["MetricName"] = "X" }))).StatusCode.Should().Be(HttpStatusCode.Forbidden);

            using var nobody = factory.CreateAuthorizedClient(await UserAsync(null));
            (await nobody.GetAsync("/api/KpiMetricDefinition/GetAllData")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }
}
