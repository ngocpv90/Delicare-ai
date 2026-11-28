using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Sales
{
    /// <summary>
    /// RETAIL-1 §8 (hoàn thiện): ảnh sản phẩm (tải bằng API tệp dùng chung → đặt ảnh → trang khách lấy ảnh công khai; ngừng bán → 404; tệp của sản phẩm khác → từ chối)
    /// và báo cáo doanh thu bán lẻ (đơn hoàn tất = doanh thu, đơn đang xử lý tách riêng, đơn hủy chỉ đếm; theo món / bữa / ngày).
    /// </summary>
    public class RetailImageRevenueTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public RetailImageRevenueTests(InMemoryApiFactory factory) => _factory = factory;

        private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

        private static async Task<Guid> UploadAsync(HttpClient c, Guid productId)
        {
            var f = new MultipartFormDataContent();
            var file = new ByteArrayContent(Png);
            file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            f.Add(file, "File", "mon.png");
            f.Add(new StringContent("RetailProduct"), "Resource");
            f.Add(new StringContent(productId.ToString()), "RecordId");
            f.Add(new StringContent("photo"), "Category");
            var j = JsonDocument.Parse(await (await c.PostAsync("/api/DocumentFile/Upload", f)).Content.ReadAsStringAsync()).RootElement;
            j.GetProperty("responseCode").GetInt32().Should().Be(200, j.ToString());
            return j.GetProperty("data").GetProperty("id").GetGuid();
        }

        private static async Task<JsonElement> SetImage(HttpClient c, Guid id, Guid file)
            => JsonDocument.Parse(await (await c.PostAsync("/api/RetailProduct/SetImage", new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = id.ToString(), ["documentFileId"] = file.ToString() }))).Content.ReadAsStringAsync()).RootElement;

        [Fact]
        public async Task Product_image_is_set_from_uploaded_photo_and_served_publicly_and_revenue_report_splits_statuses()
        {
            Guid a = Guid.NewGuid(), b = Guid.NewGuid();
            var day = new DateTime(2038, 3, 10);
            _factory.Clock.FixedNow = new DateTime(2038, 3, 12, 9, 0, 0);
            await _factory.SeedAsync(db =>
            {
                db.RetailProduct.AddRange(new RetailProduct { Id = a, Code = "SPL-IMG1", Name = "Cơm gà", CategoryCode = "com", MealTypes = "Trưa", Unit = "suất", Price = 50000, Status = RetailCodes.ProductActive },
                    new RetailProduct { Id = b, Code = "SPL-IMG2", Name = "Bánh mì", CategoryCode = "an-sang", MealTypes = "Sáng", Unit = "phần", Price = 20000, Status = RetailCodes.ProductActive });
                RetailOrder O(string no, string st, string meal, decimal total, int d) => new()
                {
                    Id = Guid.NewGuid(), OrderNumber = no, Channel = "WEB", CustomerName = "Khách", PhoneNumber = "0900000000", Address = "HN", DeliveryDate = day.AddDays(d), MealType = meal,
                    OrderStatus = st, SubTotal = total, ShippingFee = 0, TotalAmount = total, PaymentMethod = "COD", PaymentStatus = "Unpaid",
                };
                var o1 = O("DL-1", RetailOrderStatus.Completed, "Trưa", 100000, 0); var o2 = O("DL-2", RetailOrderStatus.Confirmed, "Sáng", 40000, 1); var o3 = O("DL-3", RetailOrderStatus.Cancelled, "Trưa", 50000, 1);
                db.RetailOrder.AddRange(o1, o2, o3);
                db.RetailOrderLine.AddRange(new RetailOrderLine { Id = Guid.NewGuid(), RetailOrderId = o1.Id, RetailProductId = a, ProductName = "Cơm gà", Unit = "suất", Quantity = 2, UnitPrice = 50000, LineAmount = 100000 },
                    new RetailOrderLine { Id = Guid.NewGuid(), RetailOrderId = o2.Id, RetailProductId = b, ProductName = "Bánh mì", Unit = "phần", Quantity = 2, UnitPrice = 20000, LineAmount = 40000 },
                    new RetailOrderLine { Id = Guid.NewGuid(), RetailOrderId = o3.Id, RetailProductId = a, ProductName = "Cơm gà", Unit = "suất", Quantity = 1, UnitPrice = 50000, LineAmount = 50000 });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();
            using var anon = _factory.CreateClient();

            (await anon.GetAsync($"/api/RetailPortal/ProductImage?id={a}")).StatusCode.Should().Be(HttpStatusCode.NotFound, "chưa có ảnh");
            Guid fa = await UploadAsync(c, a), fb = await UploadAsync(c, b);
            SetImage(c, a, fb).Result.GetProperty("responseCode").GetInt32().Should().Be(400, "ảnh của sản phẩm khác");
            var ok = await SetImage(c, a, fa);
            ok.GetProperty("responseCode").GetInt32().Should().Be(200);
            string url = ok.GetProperty("data").GetString()!;
            url.Should().StartWith($"/api/RetailPortal/ProductImage?id={a}&v=");
            var img = await anon.GetAsync(url);
            img.StatusCode.Should().Be(HttpStatusCode.OK, "trang khách (không đăng nhập) lấy được ảnh");
            img.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
            (await img.Content.ReadAsByteArrayAsync()).Should().Equal(Png);
            (await _factory.QueryAsync(db => db.RetailProduct.AsNoTracking().SingleAsync(p => p.Id == a))).ImageUrl.Should().Be(url);
            await _factory.SeedAsync(async db => { (await db.RetailProduct.SingleAsync(p => p.Id == a)).Status = RetailCodes.ProductInactive; });
            (await anon.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.NotFound, "ngừng bán thì không phát ảnh");

            var rev = JsonDocument.Parse(await c.GetStringAsync("/api/RetailOrder/GetRevenue?from=2038-03-10&to=2038-03-11")).RootElement.GetProperty("data");
            rev.GetProperty("revenue").GetDecimal().Should().Be(100000, "chỉ đơn hoàn tất");
            rev.GetProperty("pipeline").GetDecimal().Should().Be(40000);
            rev.GetProperty("cancelled").GetInt32().Should().Be(1);
            rev.GetProperty("cancelRatePct").GetDecimal().Should().Be(33.3m);
            var prod = rev.GetProperty("listProduct").EnumerateArray().ToDictionary(x => x.GetProperty("label").GetString()!, x => x);
            prod["Cơm gà"].GetProperty("quantity").GetDecimal().Should().Be(2, "dòng của đơn hủy không tính");
            prod["Bánh mì"].GetProperty("pipeline").GetDecimal().Should().Be(40000);
            rev.GetProperty("listDay").EnumerateArray().Should().HaveCount(2);
            JsonDocument.Parse(await c.GetStringAsync("/api/RetailOrder/GetRevenue?from=2038-01-01&to=2038-06-01")).RootElement.GetProperty("responseCode").GetInt32().Should().Be(400, "tối đa 92 ngày");
        }
    }
}
