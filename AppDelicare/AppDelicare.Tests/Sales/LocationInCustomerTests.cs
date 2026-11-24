using System.Net;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Common;
using AppDelicare.Tests.Integration;
using AppDelicare.WebApi.Security;
using FluentAssertions;

namespace AppDelicare.Tests.Sales
{
    /// <summary>UI-6.16 (D-97): mục menu Điểm giao hàng ẩn – quyền API điểm giao theo màn Khách hàng; ngừng điểm còn đơn chưa giao bị chặn (báo số đơn).</summary>
    public class LocationInCustomerTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public LocationInCustomerTests(InMemoryApiFactory factory)
        {
            _factory = factory;
            _factory.Clock.FixedNow = new DateTime(2026, 12, 8, 10, 0, 0);
        }

        private static FormUrlEncodedContent Form(params (string, string)[] kv) => new(kv.Select(x => new KeyValuePair<string, string>(x.Item1, x.Item2)));

        [Fact]
        public async Task Location_api_uses_customer_screen_and_blocks_deactivation_with_open_orders()
        {
            ScreenMap.ParentByClass["CustomerDeliveryLocation"].Screen.Should().Be("crm-customer");
            ScreenMap.HiddenScreenParent["crm-location"].Should().Be("crm-customer");
            Guid customer = Guid.NewGuid(), loc = Guid.NewGuid();
            string code = "LIC" + customer.ToString("N")[..5].ToUpperInvariant();
            await _factory.SeedAsync(db =>
            {
                db.Customer.Add(new Customer { Id = customer, Code = code, Name = "Khách " + code, CIF = code, CustomerType = "BUSINESS", DisplayName = code, Status = "Active" });
                db.CustomerDeliveryLocation.Add(new CustomerDeliveryLocation { Id = loc, CustomerId = customer, Code = code + "-A", Name = "Cổng A", Status = "Active" });
                db.Order.Add(new Order { Id = Guid.NewGuid(), CustomerId = customer, DeliveryLocationId = loc, MealDate = new DateTime(2026, 12, 10), Status = "Confirmed", OrderNumber = "DH-" + code });
                db.Order.Add(new Order { Id = Guid.NewGuid(), CustomerId = customer, DeliveryLocationId = loc, MealDate = new DateTime(2026, 12, 1), Status = "Confirmed", OrderNumber = "DH-CU-" + code });
                return Task.CompletedTask;
            });
            using var admin = _factory.CreateAuthorizedClient();
            var res = await LegacyResult.ReadAsync(await admin.PutAsync("/api/CustomerDeliveryLocation/Update",
                Form(("Id", loc.ToString()), ("CustomerId", customer.ToString()), ("Code", code + "-A"), ("Name", "Cổng A"), ("Status", "Inactive"))));
            res.Fail("1 đơn hàng chưa giao");
            res.Message.Should().Contain("DH-" + code).And.NotContain("DH-CU-", "đơn ngày đã qua không tính");

            // Chỉ Xem Khách hàng: xem được điểm giao, không thêm được; Sửa Khách hàng: thêm được.
            Guid viewer = await CommonTestUsers.UserAsync(_factory, ("crm-customer", "V"));
            using var v = _factory.CreateAuthorizedClient(viewer);
            (await v.GetAsync($"/api/CustomerDeliveryLocation/GetOverview?id={loc}")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await v.PostAsync("/api/CustomerDeliveryLocation/Insert", Form(("CustomerId", customer.ToString()), ("Name", "Cổng B")))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            Guid editor = await CommonTestUsers.UserAsync(_factory, ("crm-customer", "VE"));
            using var e = _factory.CreateAuthorizedClient(editor);
            (await e.PostAsync("/api/CustomerDeliveryLocation/Insert", Form(("CustomerId", customer.ToString()), ("Name", "Cổng B"), ("Status", "Active")))).StatusCode.Should().Be(HttpStatusCode.OK, "qua lớp quyền (kết quả nghiệp vụ do luật điểm giao quyết định)");
            Guid other = await CommonTestUsers.UserAsync(_factory, ("crm-order", "V"));
            (await _factory.CreateAuthorizedClient(other).GetAsync($"/api/CustomerDeliveryLocation/GetOverview?id={loc}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }
}
