using AppDelicare.Tests.Integration;

namespace AppDelicare.Tests.Delivery
{
    /// <summary>UI-6.34 (D-115): lập tay lệnh đóng gói bị chặn mặc định (lệnh tự sinh khi mẻ đạt).</summary>
    public class PackingCreateBlockedUi634Tests(InMemoryApiFactory factory) : IClassFixture<InMemoryApiFactory>
    {
        [Fact]
        public async Task Manual_packing_order_is_blocked_by_default()
        {
            using var c = factory.CreateAuthorizedClient();
            (await c.PostFormAsync("/api/PackingOrder/Create", ("ProductionBatchId", Guid.NewGuid()), ("OrderId", Guid.NewGuid()), ("PlannedQty", 1m))).Fail("tự sinh khi mẻ đạt");
        }
    }
}
