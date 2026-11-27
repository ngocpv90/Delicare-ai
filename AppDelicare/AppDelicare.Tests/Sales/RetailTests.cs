using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Sales
{
    /// <summary>
    /// RETAIL-1 (docs/frontend-handoff/RETAIL-1.md mục 7): trang khách lẻ gửi đơn KHÔNG cần đăng nhập, máy chủ kiểm lại mọi thứ
    /// (giá, giờ chốt, bữa, món ngừng bán, ô bẫy, giới hạn 429, chống trùng), tra cứu không lộ địa chỉ; màn nội bộ theo máy trạng thái.
    /// </summary>
    public class RetailTests
    {
        private static readonly Guid SystemUser = Guid.Parse("00000000-0000-0000-0000-00000000a001");
        private static readonly Guid Lunch = Guid.Parse("00000000-0000-0000-0000-00000000b001");
        private static readonly Guid Breakfast = Guid.Parse("00000000-0000-0000-0000-00000000b002");
        private static readonly Guid Stopped = Guid.Parse("00000000-0000-0000-0000-00000000b003");
        /// <summary>Thứ Sáu 27/11/2026 10:00 → nhận sớm nhất 28/11.</summary>
        private static readonly DateTime Now = new(2026, 11, 27, 10, 0, 0);

        private static async Task<InMemoryApiFactory> FactoryAsync(Action<Dictionary<string, string?>>? settings = null)
        {
            var f = new InMemoryApiFactory();
            f.Settings["SystemUsers:JobUserId"] = SystemUser.ToString();
            settings?.Invoke(f.Settings);
            f.Clock.FixedNow = Now;
            await f.SeedAsync(db =>
            {
                db.RetailProduct.AddRange(
                    new RetailProduct { Id = Lunch, Code = "SPL-00001", Name = "Suất cơm trưa", CategoryCode = "com-van-phong", MealTypes = "Trưa;Tối", Unit = "suất", Price = 45000, Status = RetailCodes.ProductActive, SortOrder = 1 },
                    new RetailProduct { Id = Breakfast, Code = "SPL-00002", Name = "Bánh mì sáng", CategoryCode = "an-sang", MealTypes = "Sáng", Unit = "phần", Price = 25000, Status = RetailCodes.ProductActive, SortOrder = 2 },
                    new RetailProduct { Id = Stopped, Code = "SPL-00003", Name = "Món đã ngừng", CategoryCode = "combo", MealTypes = "Trưa", Unit = "suất", Price = 99000, Status = RetailCodes.ProductInactive, SortOrder = 3 });
                return Task.CompletedTask;
            });
            return f;
        }

        private static object Order(string phone = "0912345678", string date = "2026-11-28", string meal = "Trưa", string? requestId = null, Guid? product = null,
            decimal qty = 2, string website = "")
            => new
            {
                customerName = "Nguyễn Văn A", phoneNumber = phone, address = "Số 1 Phố Huế, Hà Nội", deliveryDate = date, mealType = meal, note = "Ít cay",
                website, clientRequestId = requestId ?? Guid.NewGuid().ToString(),
                // Giá gửi lên (nếu có) bị bỏ qua – máy chủ tính theo RetailProduct.Price.
                lines = new[] { new { productId = product ?? Lunch, productCode = "", quantity = qty, unitPrice = 1 } },
            };

        private static async Task<(HttpStatusCode Status, JsonElement Body)> PostAsync(HttpClient c, string url, object body)
        {
            var r = await c.PostAsJsonAsync(url, body);
            return (r.StatusCode, await r.Content.ReadFromJsonAsync<JsonElement>());
        }

        private static int Code(JsonElement b) => b.GetProperty("responseCode").GetInt32();
        private static string Msg(JsonElement b) => b.GetProperty("responseMessage").GetString() ?? string.Empty;

        [Fact]
        public async Task Web_order_needs_no_token_recomputes_price_and_resend_returns_same_order()
        {
            using var f = await FactoryAsync();
            var anon = f.CreateClient();
            string rid = Guid.NewGuid().ToString();
            var (st, body) = await PostAsync(anon, "/api/RetailPortal/SubmitOrder", Order(requestId: rid));
            st.Should().Be(HttpStatusCode.OK);
            Code(body).Should().Be(200, Msg(body));
            string number = body.GetProperty("data").GetProperty("orderNumber").GetString()!;
            number.Should().MatchRegex(@"^BL-261127-\d{4}$");
            body.GetProperty("data").GetProperty("totalAmount").GetDecimal().Should().Be(90000m, "2 × 45.000 – giá máy chủ, không theo giá gửi lên");

            var again = await PostAsync(anon, "/api/RetailPortal/SubmitOrder", Order(requestId: rid));
            again.Body.GetProperty("data").GetProperty("orderNumber").GetString().Should().Be(number, "gửi lại cùng mã lần gửi → trả đơn cũ");

            var orders = await f.QueryAsync(db => db.RetailOrder.AsNoTracking().ToListAsync());
            orders.Should().ContainSingle();
            var o = orders[0];
            o.Channel.Should().Be(RetailCodes.ChannelWeb);
            o.OrderStatus.Should().Be(RetailOrderStatus.New);
            o.CreatedBy.Should().Be(SystemUser, "đơn WEB ghi dưới tài khoản system");
            o.MealType.Should().Be("Trưa");
            (await f.QueryAsync(db => db.RetailOrderLine.CountAsync(l => l.RetailOrderId == o.Id))).Should().Be(1);
            (await f.QueryAsync(db => db.Order.CountAsync())).Should().Be(0, "đơn lẻ tách hẳn đơn doanh nghiệp");
        }

        [Fact]
        public async Task Web_order_rejects_wrong_meal_past_cutoff_stopped_product_and_bad_phone_and_honeypot_writes_nothing()
        {
            using var f = await FactoryAsync();
            var anon = f.CreateClient();
            var meal = await PostAsync(anon, "/api/RetailPortal/SubmitOrder", Order(product: Breakfast));
            Code(meal.Body).Should().Be(400);
            Msg(meal.Body).Should().Contain("Bữa trưa không bán").And.Contain("Bánh mì sáng");

            var today = await PostAsync(anon, "/api/RetailPortal/SubmitOrder", Order(date: "2026-11-27"));
            Code(today.Body).Should().Be(400);
            Msg(today.Body).Should().Contain("28/11/2026");

            var stopped = await PostAsync(anon, "/api/RetailPortal/SubmitOrder", Order(product: Stopped));
            Msg(stopped.Body).Should().Contain("đã ngừng bán");

            var phone = await PostAsync(anon, "/api/RetailPortal/SubmitOrder", Order(phone: "12345"));
            Msg(phone.Body).Should().Contain("Số điện thoại");

            var far = await PostAsync(anon, "/api/RetailPortal/SubmitOrder", Order(date: "2026-12-31"));
            Msg(far.Body).Should().Contain("tối đa 14 ngày");

            var bot = await PostAsync(anon, "/api/RetailPortal/SubmitOrder", Order(website: "http://spam"));
            Code(bot.Body).Should().Be(200);
            (await f.QueryAsync(db => db.RetailOrder.CountAsync())).Should().Be(0, "ô bẫy / đơn sai không ghi gì");
        }

        [Fact]
        public async Task After_cutoff_hour_earliest_date_is_the_day_after_tomorrow()
        {
            using var f = await FactoryAsync();
            f.Clock.FixedNow = new DateTime(2026, 11, 27, 20, 30, 0);
            var anon = f.CreateClient();
            var tomorrow = await PostAsync(anon, "/api/RetailPortal/SubmitOrder", Order(date: "2026-11-28"));
            Msg(tomorrow.Body).Should().Contain("29/11/2026");
            var ok = await PostAsync(anon, "/api/RetailPortal/SubmitOrder", Order(date: "2026-11-29"));
            Code(ok.Body).Should().Be(200, Msg(ok.Body));
        }

        [Fact]
        public async Task Too_many_orders_from_one_phone_get_429()
        {
            using var f = await FactoryAsync();
            var anon = f.CreateClient();
            for (int i = 0; i < 3; i++)
            {
                var ok = await PostAsync(anon, "/api/RetailPortal/SubmitOrder", Order());
                Code(ok.Body).Should().Be(200, Msg(ok.Body));
            }
            var (st, body) = await PostAsync(anon, "/api/RetailPortal/SubmitOrder", Order());
            st.Should().Be(HttpStatusCode.TooManyRequests);
            Msg(body).Should().Contain("nhiều đơn");
        }

        [Fact]
        public async Task Track_needs_number_and_phone_and_never_returns_address()
        {
            using var f = await FactoryAsync();
            var anon = f.CreateClient();
            var sent = await PostAsync(anon, "/api/RetailPortal/SubmitOrder", Order());
            string number = sent.Body.GetProperty("data").GetProperty("orderNumber").GetString()!;

            var ok = await anon.GetFromJsonAsync<JsonElement>($"/api/RetailPortal/TrackOrder?orderNumber={number}&phoneNumber=0912%20345%20678");
            Code(ok).Should().Be(200, Msg(ok));
            var data = ok.GetProperty("data");
            data.GetProperty("orderStatus").GetString().Should().Be("New");
            data.GetProperty("lines").GetArrayLength().Should().Be(1);
            data.TryGetProperty("address", out _).Should().BeFalse("tra cứu không trả địa chỉ");

            var wrongPhone = await anon.GetFromJsonAsync<JsonElement>($"/api/RetailPortal/TrackOrder?orderNumber={number}&phoneNumber=0999999999");
            var wrongNumber = await anon.GetFromJsonAsync<JsonElement>("/api/RetailPortal/TrackOrder?orderNumber=BL-000000-0000&phoneNumber=0912345678");
            Msg(wrongPhone).Should().Be(Msg(wrongNumber), "sai SĐT hay sai số đơn báo như nhau");
        }

        [Fact]
        public async Task Catalog_is_public_and_lists_only_products_on_sale()
        {
            using var f = await FactoryAsync();
            var body = await f.CreateClient().GetFromJsonAsync<JsonElement>("/api/RetailPortal/GetCatalog");
            var products = body.GetProperty("data").GetProperty("products").EnumerateArray().Select(p => p.GetProperty("code").GetString()).ToList();
            products.Should().BeEquivalentTo(["SPL-00001", "SPL-00002"]);
            body.GetProperty("data").GetProperty("categories").EnumerateArray().Select(c => c.GetProperty("name").GetString()).Should().Contain("Cơm văn phòng");
        }

        [Fact]
        public async Task Transitions_follow_the_state_machine_cancel_needs_reason_and_complete_marks_paid()
        {
            using var f = await FactoryAsync();
            var sent = await PostAsync(f.CreateClient(), "/api/RetailPortal/SubmitOrder", Order());
            Guid id = sent.Body.GetProperty("data").GetProperty("id").GetGuid();
            var staff = f.CreateAuthorizedClient();

            async Task<JsonElement> Move(string to, Dictionary<string, string>? extra = null)
            {
                var form = new Dictionary<string, string> { ["Id"] = id.ToString(), ["To"] = to };
                foreach (var kv in extra ?? [])
                {
                    form[kv.Key] = kv.Value;
                }
                var r = await staff.PostAsync("/api/RetailOrder/Transition", new FormUrlEncodedContent(form));
                return await r.Content.ReadFromJsonAsync<JsonElement>();
            }

            Msg(await Move("Delivering")).Should().Contain("không chuyển sang");
            Code(await Move("Confirmed", new() { ["ShippingFee"] = "15000", ["PaymentMethod"] = "BANK" })).Should().Be(200);
            var detail = await staff.GetFromJsonAsync<JsonElement>($"/api/RetailOrder/GetDetail?id={id}");
            var order = detail.GetProperty("data").GetProperty("order");
            order.GetProperty("totalAmount").GetDecimal().Should().Be(105000m);
            order.GetProperty("paymentMethod").GetString().Should().Be("BANK");
            detail.GetProperty("data").GetProperty("allowedTransitions").EnumerateArray().Select(x => x.GetString()).Should().BeEquivalentTo(["Delivering", "Cancelled"]);

            Msg(await Move("Cancelled")).Should().Contain("lý do");
            Code(await Move("Delivering")).Should().Be(200);
            Code(await Move("Completed")).Should().Be(200);
            var done = await f.QueryAsync(db => db.RetailOrder.AsNoTracking().SingleAsync(o => o.Id == id));
            done.PaymentStatus.Should().Be(RetailCodes.Paid);
            done.ConfirmedBy.Should().Be(InMemoryApiFactory.UserId, "người xác nhận = người thao tác");
            done.CompletedAt.Should().NotBeNull();
            Msg(await Move("Cancelled", new() { ["Reason"] = "Khách hủy" })).Should().Contain("không chuyển sang");
        }

        [Fact]
        public async Task Staff_create_is_confirmed_update_recomputes_and_list_filters_many_statuses()
        {
            using var f = await FactoryAsync();
            using var scope = f.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IRetailOrderService>();
            using var who = scope.ServiceProvider.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForUser(InMemoryApiFactory.UserId), "test-retail");

            var created = await svc.Create(new RetailOrderCreateRequest
            {
                Channel = "PHONE", CustomerName = "Chị B", PhoneNumber = "+84 987 654 321", Address = "12 Láng Hạ, Hà Nội", DeliveryDate = "2026-11-28",
                MealType = "TRUA", ShippingFee = 10000, PaymentMethod = "COD", Lines = [new() { ProductCode = "SPL-00001", Quantity = 1 }],
            });
            created.ResponseCode.Should().Be(200, created.ResponseMessage);
            var o = await f.QueryAsync(db => db.RetailOrder.AsNoTracking().SingleAsync(x => x.Id == created.Data!.Id));
            o.OrderStatus.Should().Be(RetailOrderStatus.Confirmed);
            o.PhoneNumber.Should().Be("0987654321");
            o.MealType.Should().Be("Trưa", "mã TRUA chuẩn hóa về nhãn");
            o.TotalAmount.Should().Be(55000m);

            var upd = await svc.UpdateLines(new RetailOrderUpdateLinesRequest
            {
                Id = o.Id, Address = "12 Láng Hạ, Hà Nội", DeliveryDate = "2026-11-28", MealType = "Tối", ShippingFee = 0,
                Lines = [new() { ProductId = Lunch, Quantity = 3 }],
            });
            upd.ResponseCode.Should().Be(200, upd.ResponseMessage);
            (await f.QueryAsync(db => db.RetailOrder.AsNoTracking().SingleAsync(x => x.Id == o.Id))).TotalAmount.Should().Be(135000m);
            (await f.QueryAsync(db => db.RetailOrderLine.CountAsync(l => l.RetailOrderId == o.Id && !l.IsDeleted))).Should().Be(1);

            (await svc.Create(new RetailOrderCreateRequest { Channel = "WEB", CustomerName = "X", PhoneNumber = "0912345678" })).ResponseMessage.Should().Contain("Kênh");

            var web = await PostAsync(f.CreateClient(), "/api/RetailPortal/SubmitOrder", Order(phone: "0909000111"));
            Code(web.Body).Should().Be(200, Msg(web.Body));
            var list = await svc.GetList(new RetailOrderListRequest { OrderStatus = "New,Delivering", PageSize = 50 });
            list.Data!.ListItem.Should().ContainSingle().Which.Channel.Should().Be("WEB");
            list.Data.StatusCounts["New"].Should().Be(1);
            list.Data.StatusCounts["Confirmed"].Should().Be(1);
            list.Data.ListItem[0].LineSummary.Should().Contain("Suất cơm trưa × 2");

            await svc.Transition(new RetailOrderTransitionRequest { Id = o.Id, To = "Delivering" });
            (await svc.UpdateLines(new RetailOrderUpdateLinesRequest { Id = o.Id, Address = "x x x x x", DeliveryDate = "2026-11-28", MealType = "Trưa", Lines = [new() { ProductId = Lunch, Quantity = 1 }] }))
                .ResponseMessage.Should().Contain("Chỉ sửa được đơn Mới hoặc Đã xác nhận");

            var prep = await svc.GetPrepSummary("2026-11-28");
            prep.Data!.Meals.Should().ContainSingle(m => m.MealType == "Tối").Which.Items.Single().Quantity.Should().Be(3m);
        }

        [Fact]
        public async Task Internal_api_needs_login_and_screen_permission()
        {
            using var f = await FactoryAsync();
            (await f.CreateClient().GetAsync("/api/RetailOrder/GetList")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            var plain = Guid.NewGuid();
            await f.SeedAsync(db =>
            {
                db.User.Add(new User { Id = plain, UserName = "retail-no-right", IsAdmin = false, IsActive = true });
                return Task.CompletedTask;
            });
            var client = f.CreateAuthorizedClient(plain);
            (await client.GetAsync("/api/RetailOrder/GetList")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await client.PostAsync("/api/RetailOrder/Transition", new FormUrlEncodedContent(new Dictionary<string, string> { ["Id"] = Guid.NewGuid().ToString(), ["To"] = "Confirmed" })))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }
}
