using AppDelicare.Tests.Integration;
using FluentAssertions;

namespace AppDelicare.Tests.Pilot
{
    /// <summary>
    /// BE2.9 (lỗi tìm được khi smoke test staging): các bảng điều hành theo ngày gọi thiếu tham số ngày (FE mở màn hình chưa chọn ngày) trước đây trả HTTP 500
    /// (DateTime.MinValue → SqlDateTime overflow). Nay trả responseCode 400 kèm thông báo tiếng Việt như <c>MaterialRequirement/GetByDate</c>.
    /// </summary>
    public class MissingDateParameterTests(InMemoryApiFactory factory) : IClassFixture<InMemoryApiFactory>
    {
        [Theory]
        [InlineData("/api/DeliveryExecution/GetBoard", "Chọn ngày giao")]
        [InlineData("/api/DeliveryRoute/GetBoard", "Chọn ngày giao")]
        [InlineData("/api/DeliveryRoute/GetReadyOrders", "Chọn ngày giao")]
        [InlineData("/api/ProductionBatch/GetBoard", "Chọn ngày sản xuất")]
        [InlineData("/api/ProductionBatch/GetStageBoard", "Chọn ngày sản xuất")]
        [InlineData("/api/Forecast/GetMatrix", "Khoảng ngày không hợp lệ")]
        [InlineData("/api/Forecast/GetMatrix?To=2026-12-01", "Khoảng ngày không hợp lệ")]
        public async Task Board_without_date_returns_400_with_message_instead_of_500(string url, string message)
        {
            using var client = factory.CreateAuthorizedClient();
            (await client.GetResultAsync(url)).Fail(message);
        }

        [Fact]
        public async Task Board_with_date_still_works()
        {
            using var client = factory.CreateAuthorizedClient();
            (await client.GetResultAsync("/api/DeliveryExecution/GetBoard?DeliveryDate=2026-12-01")).Ok();
            (await client.GetResultAsync("/api/DeliveryRoute/GetBoard?DeliveryDate=2026-12-01")).Ok();
            (await client.GetResultAsync("/api/ProductionBatch/GetStageBoard?ProductionDate=2026-12-01")).Ok();
            (await client.GetResultAsync("/api/Forecast/GetMatrix?From=2026-12-01&To=2026-12-07")).Ok();
        }
    }
}
