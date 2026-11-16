using AppDelicare.Tests.Integration;
using FluentAssertions;

namespace AppDelicare.Tests.Admin
{
    /// <summary>T45R.8 (FE-1): <c>User/GetAll</c> lọc theo IsActive trong SP <c>uspUser_GetAll</c> (chỉ đọc DB dev; không có DB thì Skip).</summary>
    [Collection(ApiCollection.Name)]
    public class UserAccountSqlServerTests(ApiFactory factory)
    {
        [SkippableFact]
        public async Task User_get_all_honours_is_active_filter()
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);
            using var c = factory.CreateAuthorizedClient();

            var all = (await c.GetResultAsync("/api/User/GetAll?Page=1&PageSize=500")).ListData.EnumerateArray().ToList();
            var active = (await c.GetResultAsync("/api/User/GetAll?Page=1&PageSize=500&IsActive=true")).ListData.EnumerateArray().ToList();
            var locked = (await c.GetResultAsync("/api/User/GetAll?Page=1&PageSize=500&IsActive=false")).ListData.EnumerateArray().ToList();

            active.Should().NotContain(u => !u.GetProperty("isActive").GetBoolean());
            locked.Should().NotContain(u => u.GetProperty("isActive").GetBoolean());
            (active.Count + locked.Count).Should().Be(all.Count);
        }
    }
}
