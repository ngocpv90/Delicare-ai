using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Common
{
    /// <summary>
    /// MENU-TAB (anh 2026-11-28): ai-agent-log / ai-flow / ai-tasks / ai-schedule / ai-cost là TAB của Trung tâm Agent (và kpi-daily / cost-meal / dash-pl là tab của Bảng điều hành),
    /// không phải menu – vẫn giữ dòng quyền riêng, nhưng không tính vào số màn của nhóm; tìm kiếm toàn cục hiện "Trung tâm Agent › …".
    /// </summary>
    public class MenuTabsTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public MenuTabsTests(InMemoryApiFactory factory) => _factory = factory;

        [Fact]
        public async Task Agent_tabs_keep_their_permission_rows_but_are_not_counted_as_screens_and_search_shows_parent()
        {
            Guid group = Guid.NewGuid(), root = Guid.NewGuid();
            await _factory.SeedAsync(async db =>
            {
                db.UserGroup.Add(new UserGroup { Id = group, Name = "Tab test", Code = "TABTEST" });
                db.ObjectMenu.Add(new ObjectMenu { Id = root, Name = "Điều hành (tab test)", MetaLink = "#/group/tabtest", LevelShow = 1, OrderShow = 1, IsActive = true });
                foreach (var (link, name) in new[] { ("#/ai-agents", "Trung tâm Agent"), ("#/ai-cost", "Sử dụng & chi phí"), ("#/ai-agent-log", "Nhật ký agent"), ("#/kb-tabtest", "Màn thường") })
                {
                    var m = await db.ObjectMenu.FirstOrDefaultAsync(x => x.MetaLink == link);
                    if (m == null)
                    {
                        m = new ObjectMenu { Id = Guid.NewGuid(), Name = name, MetaLink = link, ParentId = root, LevelShow = 2, OrderShow = 1, IsActive = true };
                        db.ObjectMenu.Add(m);
                    }
                    db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = group, ObjectMenuId = m.Id, AllowAccess = true });
                }
            });
            using var c = _factory.CreateAuthorizedClient();
            var pv = JsonDocument.Parse(await c.GetStringAsync($"/api/UserRole/GetPreview?userGroupId={group}")).RootElement.GetProperty("data");
            pv.GetProperty("screenCount").GetInt32().Should().Be(2, "Trung tâm Agent + Màn thường; 2 tab không tính là màn");
            static IEnumerable<JsonElement> All(JsonElement n) => new[] { n }.Concat(n.TryGetProperty("children", out var ch) && ch.ValueKind == JsonValueKind.Array ? ch.EnumerateArray().SelectMany(All) : []);
            var links = pv.GetProperty("listMenu").EnumerateArray().SelectMany(All).Select(x => x.GetProperty("metaLink").GetString()).ToList();
            links.Should().Contain(["#/ai-cost", "#/ai-agent-log"], "dòng quyền của tab vẫn còn (tab chi phí chỉ CEO / COO / FIN-IT)");

            var s = JsonDocument.Parse(await c.GetStringAsync("/api/Common/GlobalSearch?q=" + Uri.EscapeDataString("Sử dụng & chi phí"))).RootElement.GetProperty("data");
            s.GetProperty("screens").EnumerateArray().Select(x => x.GetProperty("title").GetString()).Should().Contain("Trung tâm Agent › Sử dụng & chi phí");
        }
    }
}
