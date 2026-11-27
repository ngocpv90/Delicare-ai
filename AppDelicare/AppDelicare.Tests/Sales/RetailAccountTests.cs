using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Sales
{
    /// <summary>
    /// RETAIL-2 (D-136): tài khoản khách lẻ (bảng RetailCustomer, tách tài khoản nhân viên) + Đơn của tôi + gắn đơn cũ + tiến độ.
    /// Phiên khách ở header X-Retail-Token – không bao giờ vào được API nội bộ; mật khẩu không bao giờ trả ra.
    /// </summary>
    public class RetailAccountTests
    {
        private const string TokenHeader = "X-Retail-Token";
        private static readonly Guid SystemUser = Guid.Parse("00000000-0000-0000-0000-00000000a001");
        private static readonly Guid Lunch = Guid.Parse("00000000-0000-0000-0000-00000000b001");
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
                db.RetailProduct.Add(new RetailProduct
                {
                    Id = Lunch, Code = "SPL-00001", Name = "Suất cơm trưa", CategoryCode = "com-van-phong", MealTypes = "Trưa;Tối", Unit = "suất", Price = 45000,
                    Status = RetailCodes.ProductActive, SortOrder = 1,
                });
                return Task.CompletedTask;
            });
            return f;
        }

        private static object Order(string phone = "0912345678")
            => new
            {
                customerName = "Nguyễn Văn A", phoneNumber = phone, address = "Số 1 Phố Huế, Hà Nội", deliveryDate = "2026-11-28", mealType = "Trưa", note = "",
                website = "", clientRequestId = Guid.NewGuid().ToString(),
                lines = new[] { new { productId = Lunch, productCode = "", quantity = 1m, unitPrice = 0 } },
            };

        private static async Task<(HttpStatusCode Status, JsonElement Body)> PostAsync(HttpClient c, string url, object body, string? token = null)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
            if (token != null)
            {
                req.Headers.Add(TokenHeader, token);
            }
            var r = await c.SendAsync(req);
            return (r.StatusCode, await r.Content.ReadFromJsonAsync<JsonElement>());
        }

        private static async Task<(HttpStatusCode Status, JsonElement Body)> GetAsync(HttpClient c, string url, string? token)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            if (token != null)
            {
                req.Headers.Add(TokenHeader, token);
            }
            var r = await c.SendAsync(req);
            string text = await r.Content.ReadAsStringAsync();
            return (r.StatusCode, string.IsNullOrWhiteSpace(text) ? default : JsonDocument.Parse(text).RootElement.Clone());
        }

        private static int Code(JsonElement b) => b.GetProperty("responseCode").GetInt32();
        private static string Msg(JsonElement b) => b.GetProperty("responseMessage").GetString() ?? string.Empty;

        private static async Task<string> RegisterAsync(HttpClient c, string phone = "0912345678", string password = "matkhau1")
        {
            var r = await PostAsync(c, "/api/RetailPortal/Register", new { fullName = "Nguyễn Văn A", phoneNumber = phone, password, address = "Số 1 Phố Huế" });
            Code(r.Body).Should().Be(200, Msg(r.Body));
            return r.Body.GetProperty("data").GetProperty("token").GetString()!;
        }

        [Fact]
        public async Task Register_then_login_never_returns_password_and_phone_must_be_unique()
        {
            using var f = await FactoryAsync();
            var c = f.CreateClient();
            var reg = await PostAsync(c, "/api/RetailPortal/Register", new { fullName = "Nguyễn Văn A", phoneNumber = "0912 345 678", password = "matkhau1" });
            Code(reg.Body).Should().Be(200, Msg(reg.Body));
            reg.Body.GetRawText().Should().NotContainAny("PBKDF2", "passwordHash", "matkhau1");
            reg.Body.GetProperty("data").GetProperty("profile").GetProperty("phoneNumber").GetString().Should().Be("0912345678");

            var again = await PostAsync(c, "/api/RetailPortal/Register", new { fullName = "Người khác", phoneNumber = "0912345678", password = "matkhau2" });
            Code(again.Body).Should().Be(400);

            var login = await PostAsync(c, "/api/RetailPortal/Login", new { phoneNumber = "0912345678", password = "matkhau1" });
            Code(login.Body).Should().Be(200, Msg(login.Body));
            string token = login.Body.GetProperty("data").GetProperty("token").GetString()!;
            var me = await GetAsync(c, "/api/RetailPortal/GetProfile", token);
            Code(me.Body).Should().Be(200);
            me.Body.GetProperty("data").GetProperty("fullName").GetString().Should().Be("Nguyễn Văn A");

            var stored = await f.QueryAsync(db => db.RetailCustomer.AsNoTracking().SingleAsync());
            stored.PasswordHash.Should().StartWith("PBKDF2$").And.NotContain("matkhau1");
            stored.CreatedBy.Should().Be(SystemUser);
        }

        [Fact]
        public async Task Wrong_phone_or_password_same_message_and_lock_after_limit()
        {
            using var f = await FactoryAsync(s => s["BusinessRules:Retail:LoginFailLimit"] = "3");
            var c = f.CreateClient();
            await RegisterAsync(c);
            var wrongPass = await PostAsync(c, "/api/RetailPortal/Login", new { phoneNumber = "0912345678", password = "sai-roi" });
            var wrongPhone = await PostAsync(c, "/api/RetailPortal/Login", new { phoneNumber = "0987654321", password = "matkhau1" });
            Code(wrongPass.Body).Should().Be(400);
            Msg(wrongPass.Body).Should().Be(Msg(wrongPhone.Body), "sai SĐT hay sai mật khẩu báo như nhau");

            await PostAsync(c, "/api/RetailPortal/Login", new { phoneNumber = "0912345678", password = "sai-roi" });
            await PostAsync(c, "/api/RetailPortal/Login", new { phoneNumber = "0912345678", password = "sai-roi" });
            var locked = await PostAsync(c, "/api/RetailPortal/Login", new { phoneNumber = "0912345678", password = "matkhau1" });
            locked.Status.Should().Be(HttpStatusCode.TooManyRequests, "đủ số lần sai thì khóa tạm, kể cả mật khẩu đúng");
        }

        [Fact]
        public async Task Tampered_or_missing_token_is_401_and_change_password_kills_old_sessions()
        {
            using var f = await FactoryAsync();
            var c = f.CreateClient();
            string token = await RegisterAsync(c);

            (await GetAsync(c, "/api/RetailPortal/GetMyOrders", null)).Status.Should().Be(HttpStatusCode.Unauthorized);
            string tampered = token[..^2] + (token[^1] == 'A' ? "BB" : "AA");
            (await GetAsync(c, "/api/RetailPortal/GetMyOrders", tampered)).Status.Should().Be(HttpStatusCode.Unauthorized);

            var wrongCurrent = await PostAsync(c, "/api/RetailPortal/ChangePassword", new { currentPassword = "khong-dung", newPassword = "matkhau-moi" }, token);
            Code(wrongCurrent.Body).Should().Be(400);
            var changed = await PostAsync(c, "/api/RetailPortal/ChangePassword", new { currentPassword = "matkhau1", newPassword = "matkhau-moi" }, token);
            Code(changed.Body).Should().Be(200, Msg(changed.Body));
            string fresh = changed.Body.GetProperty("data").GetProperty("token").GetString()!;

            (await GetAsync(c, "/api/RetailPortal/GetProfile", token)).Status.Should().Be(HttpStatusCode.Unauthorized, "phiên cũ hết hiệu lực sau khi đổi mật khẩu");
            (await GetAsync(c, "/api/RetailPortal/GetProfile", fresh)).Status.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Logged_in_order_goes_to_my_orders_others_cannot_see_it_and_guest_order_needs_same_phone_to_claim()
        {
            using var f = await FactoryAsync();
            var c = f.CreateClient();
            string mine = await RegisterAsync(c, "0912345678");
            string other = await RegisterAsync(c, "0987654321");

            var sent = await PostAsync(c, "/api/RetailPortal/SubmitOrder", Order("0912345678"), mine);
            Code(sent.Body).Should().Be(200, Msg(sent.Body));
            string number = sent.Body.GetProperty("data").GetProperty("orderNumber").GetString()!;

            var list = await GetAsync(c, "/api/RetailPortal/GetMyOrders?group=active", mine);
            list.Body.GetProperty("data").GetProperty("listItem").EnumerateArray().Select(i => i.GetProperty("orderNumber").GetString()).Should().Equal(number);
            var detail = await GetAsync(c, $"/api/RetailPortal/GetMyOrder?orderNumber={number}", mine);
            Code(detail.Body).Should().Be(200);
            detail.Body.GetProperty("data").GetProperty("address").GetString().Should().Be("Số 1 Phố Huế, Hà Nội", "chủ đơn đã đăng nhập thì thấy địa chỉ");
            detail.Body.GetProperty("data").GetProperty("listStep").GetArrayLength().Should().BeGreaterThan(0);

            (await GetAsync(c, "/api/RetailPortal/GetMyOrders?group=all", other)).Body.GetProperty("data").GetProperty("listItem").GetArrayLength().Should().Be(0);
            Code((await GetAsync(c, $"/api/RetailPortal/GetMyOrder?orderNumber={number}", other)).Body).Should().Be(400, "không xem được đơn của tài khoản khác");
            Code((await GetAsync(c, $"/api/RetailPortal/GetOrderTrace?orderNumber={number}", other)).Body).Should().Be(400);

            // Đơn khách vãng lai (không đăng nhập) → chỉ tài khoản có đúng SĐT của đơn mới gắn được.
            var guest = await PostAsync(c, "/api/RetailPortal/SubmitOrder", Order("0987654321"));
            string guestNumber = guest.Body.GetProperty("data").GetProperty("orderNumber").GetString()!;
            Code((await PostAsync(c, "/api/RetailPortal/ClaimOrder", new { orderNumber = guestNumber }, mine)).Body).Should().Be(400, "SĐT của đơn khác SĐT tài khoản");
            Code((await PostAsync(c, "/api/RetailPortal/ClaimOrder", new { orderNumber = guestNumber.ToLowerInvariant() }, other)).Body).Should().Be(200);
            (await GetAsync(c, "/api/RetailPortal/GetMyOrders?group=all", other)).Body.GetProperty("data").GetProperty("listItem").GetArrayLength().Should().Be(1);
        }

        [Fact]
        public async Task Business_account_needs_company_tax_code_and_email_tax_code_unique()
        {
            using var f = await FactoryAsync();
            var c = f.CreateClient();
            object Biz(string phone, string? company = "Công ty TNHH Thử", string? tax = "0101234567", string? email = "ketoan@thu.vn")
                => new { accountType = "BUSINESS", fullName = "Người liên hệ", phoneNumber = phone, password = "matkhau1", companyName = company, taxCode = tax, email };

            Msg((await PostAsync(c, "/api/RetailPortal/Register", Biz("0912000001", company: ""))).Body).Should().Be(RetailMessage.CompanyNameRequired);
            Msg((await PostAsync(c, "/api/RetailPortal/Register", Biz("0912000001", tax: "12345"))).Body).Should().Be(RetailMessage.TaxCodeInvalid);
            Msg((await PostAsync(c, "/api/RetailPortal/Register", Biz("0912000001", email: null))).Body).Should().Be(RetailMessage.BusinessEmailRequired);
            Msg((await PostAsync(c, "/api/RetailPortal/Register", new { accountType = "OTHER", fullName = "A B", phoneNumber = "0912000001", password = "matkhau1" })).Body)
                .Should().Be(RetailMessage.AccountTypeInvalid);

            var ok = await PostAsync(c, "/api/RetailPortal/Register", Biz("0912000001", tax: "0101 234 567-001"));
            Code(ok.Body).Should().Be(200, Msg(ok.Body));
            var profile = ok.Body.GetProperty("data").GetProperty("profile");
            profile.GetProperty("accountType").GetString().Should().Be("BUSINESS");
            profile.GetProperty("taxCode").GetString().Should().Be("0101234567-001", "bỏ khoảng trắng khi lưu");
            Msg((await PostAsync(c, "/api/RetailPortal/Register", Biz("0912000002", tax: "0101234567-001"))).Body).Should().Be(RetailMessage.TaxCodeTaken);

            // Cập nhật hồ sơ: doanh nghiệp không được bỏ email / MST.
            string token = ok.Body.GetProperty("data").GetProperty("token").GetString()!;
            Msg((await PostAsync(c, "/api/RetailPortal/UpdateProfile", new { fullName = "Người liên hệ", email = "", companyName = "Công ty TNHH Thử" }, token)).Body)
                .Should().Be(RetailMessage.BusinessEmailRequired);

            // Cá nhân: không cần MST / email; trường doanh nghiệp gửi lên bị bỏ qua.
            var person = await PostAsync(c, "/api/RetailPortal/Register", new { fullName = "Cá Nhân", phoneNumber = "0912000003", password = "matkhau1", taxCode = "0101234567" });
            Code(person.Body).Should().Be(200, Msg(person.Body));
            person.Body.GetProperty("data").GetProperty("profile").GetProperty("accountType").GetString().Should().Be("INDIVIDUAL");
            person.Body.GetProperty("data").GetProperty("profile").GetProperty("taxCode").ValueKind.Should().Be(JsonValueKind.Null);
        }

        [Fact]
        public async Task Customer_token_never_opens_internal_api()
        {
            using var f = await FactoryAsync();
            var c = f.CreateClient();
            string token = await RegisterAsync(c);
            (await GetAsync(c, "/api/RetailOrder/GetList", token)).Status.Should().Be(HttpStatusCode.Unauthorized);
            using var req = new HttpRequestMessage(HttpMethod.Get, "/api/RetailOrder/GetList");
            req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
            (await c.SendAsync(req)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }
}
