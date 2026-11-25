using System.Net.Http.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.HrAccount
{
    /// <summary>
    /// UI-6.42 (D-123): xóa tài khoản → lỗi; người không phải quản trị gọi User/Insert → lỗi; SetGroupsMany bỏ nhóm cuối → bỏ qua tài khoản đó, các tài khoản khác lưu;
    /// LockMany không khóa chính mình; GetList truy cập gần nhất + cờ nghỉ việc còn mở đúng; GetPendingRequests có yêu cầu cấp tài khoản.
    /// </summary>
    public class AccountsUi642Tests(InMemoryApiFactory factory) : HrAccountTestBase(factory), IClassFixture<InMemoryApiFactory>
    {
        private static async Task<string> PostJson(HttpClient c, string url, object body) => await (await c.PostAsJsonAsync(url, body)).Content.ReadAsStringAsync();

        [Fact]
        public async Task Delete_is_blocked_and_direct_create_needs_admin()
        {
            Guid it = await UserAsync("u642.it", ["#/adm-user"]);
            Guid victim = await UserAsync("u642.victim");
            using var admin = _factory.CreateAuthorizedClient();
            using var itc = _factory.CreateAuthorizedClient(it);
            (await admin.DeleteResultAsync("/api/User/Delete?id=" + victim)).Fail("không xóa được");
            (await itc.PostFormAsync("/api/User/Insert", ("UserName", "u642.sys"))).Fail("chỉ quản trị");
            (await _factory.QueryAsync(db => db.User.AsNoTracking().AnyAsync(u => u.UserName == "u642.sys"))).Should().BeFalse();
        }

        [Fact]
        public async Task Bulk_groups_skip_account_losing_last_group_and_lock_many_skips_self()
        {
            Guid it = await UserAsync("u642b.it", ["#/adm-user"]);
            Guid a = await UserAsync("u642b.a"), b = await UserAsync("u642b.b");
            await GroupsAsync("G642-EXTRA");
            Guid extra = await _factory.QueryAsync(db => db.UserGroup.Where(g => g.Name == "G642-EXTRA").Select(g => g.Id).SingleAsync());
            Guid aOnly = await _factory.QueryAsync(db => db.UserClaim.Where(c => c.UserId == a && !c.IsDeleted).Select(c => c.UserGroupId!.Value).SingleAsync());
            await _factory.SeedAsync(db => { db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = b, UserGroupId = aOnly }); return Task.CompletedTask; });
            using var itc = _factory.CreateAuthorizedClient(it);
            // Bỏ nhóm aOnly: a chỉ có nhóm đó → bỏ qua; b còn nhóm riêng → lưu.
            var r = await PostJson(itc, "/api/User/SetGroupsMany", new { userIds = new[] { a, b }, removeGroupIds = new[] { aOnly } });
            r.Should().Contain("\"done\":1").And.Contain("u642b.a");
            (await _factory.QueryAsync(db => db.UserClaim.CountAsync(c => c.UserId == a && !c.IsDeleted))).Should().Be(1);
            (await _factory.QueryAsync(db => db.UserClaim.CountAsync(c => c.UserId == b && c.UserGroupId == aOnly && !c.IsDeleted))).Should().Be(0);
            (await PostJson(itc, "/api/User/SetGroupsMany", new { userIds = new[] { a, b }, addGroupIds = new[] { extra } })).Should().Contain("\"done\":2");

            var lr = await PostJson(itc, "/api/User/LockMany", new { userIds = new[] { it, a } });
            lr.Should().Contain("\"done\":1");
            (await _factory.QueryAsync(db => db.User.AsNoTracking().Where(u => u.Id == it).Select(u => u.IsActive).SingleAsync())).Should().BeTrue("không tự khóa mình");
            (await _factory.QueryAsync(db => db.User.AsNoTracking().Where(u => u.Id == a).Select(u => u.IsActive).SingleAsync())).Should().BeFalse();
        }

        [Fact]
        public async Task Ui649_overview_page_has_account_access_bot_and_needs_account_screen()
        {
            Guid user = await UserAsync("u649.ov");
            await _factory.SeedAsync(db =>
            {
                db.LocationTracking.Add(new LocationTracking { Id = Guid.NewGuid(), CreatedBy = user, CreatedDate = Today.AddDays(-2), IPv4 = "10.0.0.9", PcName = "Chrome" });
                db.UserExternalIdentity.Add(new UserExternalIdentity { Id = Guid.NewGuid(), UserId = user, IntegrationChannelId = Guid.NewGuid(), ExternalUserId = "1", ExternalUsername = "ov649", LinkedAt = Today.AddDays(-5), IdentityStatus = "Active" });
                return Task.CompletedTask;
            });
            using var admin = _factory.CreateAuthorizedClient();
            var o = (await admin.GetResultAsync("/api/User/GetOverview?id=" + user)).Ok();
            o.GetProperty("account").GetProperty("userName").GetString().Should().Be("u649.ov");
            o.GetProperty("listAccess")[0].GetProperty("ip").GetString().Should().Be("10.0.0.9");
            o.GetProperty("listBot")[0].GetProperty("username").GetString().Should().Be("ov649");
            o.GetProperty("listGroupId").GetArrayLength().Should().Be(1);
            (await admin.GetResultAsync("/api/User/GetOverview?id=" + Guid.NewGuid())).Fail();
            using var nobody = _factory.CreateAuthorizedClient(await UserAsync("u649.none"));
            (await nobody.GetAsync("/api/User/GetOverview?id=" + user)).StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task List_shows_last_access_resigned_open_and_pending_requests()
        {
            Guid person = await PersonAsync("Người U642C");
            Guid emp = await EmployeeAsync("U642C-1", null, person, EmployeeStatus.Resigned);
            Guid user = await UserAsync("u642c.left", personId: person);
            await _factory.SeedAsync(db => { db.LocationTracking.Add(new LocationTracking { Id = Guid.NewGuid(), CreatedBy = user, CreatedDate = Today.AddDays(-70), IPv4 = "127.0.0.1" }); return Task.CompletedTask; });
            Guid fresh = await EmployeeAsync("U642C-2");
            await GroupsAsync("FIN-IT");
            using var admin = _factory.CreateAuthorizedClient();
            (await PostJson(admin, "/api/Employee/RequestAccount", new { employeeId = fresh })).Should().Contain("\"responseCode\":200");

            var list = (await admin.GetResultAsync("/api/User/GetList?keyword=u642c.left")).Ok();
            var row = list.GetProperty("listItem")[0];
            row.GetProperty("status").GetString().Should().Be("RESIGNED_OPEN");
            row.GetProperty("kind").GetString().Should().Be("EMPLOYEE");
            row.GetProperty("inactiveDays").GetInt32().Should().BeGreaterThanOrEqualTo(60);
            list.GetProperty("resignedOpen").GetInt32().Should().BeGreaterThan(0);
            (await admin.GetResultAsync("/api/User/GetList?flag=INACTIVE_LONG")).Ok().GetProperty("listItem").EnumerateArray().Select(x => x.GetProperty("userName").GetString()).Should().Contain("u642c.left");
            var pending = (await admin.GetResultAsync("/api/User/GetPendingRequests")).ListData.EnumerateArray().ToList();
            pending.Should().Contain(p => p.GetProperty("employeeId").GetGuid() == fresh && p.GetProperty("typeName").GetString() == "Tuyển mới");
        }
    }
}
