using System.Net;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;

namespace AppDelicare.Tests.DetailPages
{
    /// <summary>
    /// UI-6.3 (kiểm tra toàn đợt): mọi trang chi tiết dùng <c>GetOverview</c> – id không có → báo lỗi gọn (không 500, không lộ chi tiết kỹ thuật);
    /// tài khoản không có quyền màn → 403; id rỗng → báo lỗi.
    /// </summary>
    public class Ui63OverviewTests(InMemoryApiFactory factory) : IClassFixture<InMemoryApiFactory>
    {
        public static TheoryData<string> Controllers => new()
        {
            "Contract", "Customer", "CustomerComplaint", "CustomerDeliveryLocation", "DeliveryRoute", "Dish", "Employee", "GoodsReceipt", "Ingredient", "MaterialLot",
            "Order", "ProductionBatch", "ProductionLine", "ProductionOrder", "ProductionPlan", "PurchaseOrder", "PurchaseRequest", "StockCount", "Supplier",
        };

        [Theory]
        [MemberData(nameof(Controllers))]
        public async Task Unknown_id_fails_cleanly_and_user_without_screen_gets_403(string controller)
        {
            using var admin = factory.CreateAuthorizedClient();
            var res = await admin.GetAsync($"/api/{controller}/GetOverview?id={Guid.NewGuid()}");
            res.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError, $"{controller}: id không có không được lỗi máy chủ");
            string body = await res.Content.ReadAsStringAsync();
            body.Should().NotContain("Exception").And.NotContain("dbo.");
            if (controller != "ProductionLine") // ProductionLine/GetOverview = tổng quan cả màn Dây chuyền (không theo id)
            {
                body.Should().NotContain("\"responseCode\":200", $"{controller}: id không có phải báo lỗi");
            }
            var empty = await admin.GetAsync($"/api/{controller}/GetOverview?id={Guid.Empty}");
            empty.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);

            Guid nobody = Guid.NewGuid();
            await factory.SeedAsync(db => { db.User.Add(new User { Id = nobody, UserName = "u63." + controller.ToLowerInvariant(), IsActive = true }); return Task.CompletedTask; });
            using var none = factory.CreateAuthorizedClient(nobody);
            (await none.GetAsync($"/api/{controller}/GetOverview?id={Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.Forbidden, $"{controller}: không có quyền màn");
        }
    }
}
