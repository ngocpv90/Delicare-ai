using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Services.Implement.Identity;
using AppDelicare.Tests.Integration;
using AppDelicare.WebApi.Security;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Notifications
{
    /// <summary>
    /// BOT-1.2 (D-62, BOT_TELEGRAM §2): mã liên kết (HMAC, hạn, 1 lần), khớp số điện thoại duy nhất, người duyệt chỉ khớp số → token hạn chế (không duyệt),
    /// JWT 10′ của chính user (User bị khóa → không cấp, IntegrationLog TOKEN), chặn / mở bot, admin liên kết / gỡ, API nhân viên chỉ có EmployeeId.
    /// </summary>
    public class BotIdentityTests : IClassFixture<InMemoryApiFactory>
    {
        private const string ClientKey = "test-messaging-client-key-bot12-0123456789";
        private const string SigningKey = "test-link-signing-key-0123456789-abcdefghij";
        private static readonly Guid Vh = Guid.Parse("b0720000-0000-4000-8000-000000000001");
        private static readonly Guid Dh = Guid.Parse("b0720000-0000-4000-8000-000000000002");
        private static readonly Guid Approver = Guid.Parse("b0720000-0000-4000-8000-0000000000a1");
        private static readonly Guid Staff = Guid.Parse("b0720000-0000-4000-8000-0000000000a2");
        private static readonly Guid Locked = Guid.Parse("b0720000-0000-4000-8000-0000000000a3");
        private static readonly Guid Worker = Guid.Parse("b0720000-0000-4000-8000-0000000000e1");
        private readonly InMemoryApiFactory _factory;

        public BotIdentityTests(InMemoryApiFactory factory)
        {
            _factory = factory;
            _factory.Clock.FixedNow = new DateTime(2031, 6, 2, 9, 0, 0);
            _factory.Settings["Clients:MESSAGING:KeyHash"] = ClientKeyFilter.HashKey(ClientKey);
            _factory.Settings["Messaging:LinkSigningKey"] = SigningKey;
        }

        private async Task SeedAsync()
        {
            await _factory.SeedAsync(async db =>
            {
                if (await db.IntegrationChannel.AnyAsync(c => c.Id == Vh))
                {
                    return;
                }
                db.IntegrationChannel.AddRange(
                    new IntegrationChannel { Id = Vh, ChannelCode = "TG_VH_12", ChannelName = "VH", ProviderCode = "TELEGRAM", EnvironmentCode = "Development", ChannelStatus = "Active" },
                    new IntegrationChannel { Id = Dh, ChannelCode = "TG_DH_12", ChannelName = "DH", ProviderCode = "TELEGRAM", EnvironmentCode = "Development", ChannelStatus = "Active" });
                db.BotConfiguration.AddRange(
                    new BotConfiguration { Id = Guid.NewGuid(), IntegrationChannelId = Vh, BotCode = "VH", BotName = "VH", BotUsername = "delicare_vh_dev_bot", BotStatus = "Active" },
                    new BotConfiguration { Id = Guid.NewGuid(), IntegrationChannelId = Dh, BotCode = "DH", BotName = "DH", BotStatus = "Active" });
                // Nhóm: DUYET (vào bot VH + duyệt kế hoạch SX), NV (chỉ vào bot VH).
                Guid gApprove = Guid.NewGuid(), gStaff = Guid.NewGuid(), mVh = Guid.NewGuid(), mApprove = Guid.NewGuid(), mDh = Guid.NewGuid();
                db.UserGroup.AddRange(new UserGroup { Id = gApprove, Name = "BOT12-DUYET" }, new UserGroup { Id = gStaff, Name = "BOT12-NV" });
                db.ObjectMenu.AddRange(
                    new ObjectMenu { Id = mVh, Name = "Bot VH", MetaLink = MessagingCode.MenuBotVanHanh, IsActive = true },
                    new ObjectMenu { Id = mDh, Name = "Bot DH", MetaLink = MessagingCode.MenuBotDieuHanh, IsActive = true },
                    new ObjectMenu { Id = mApprove, Name = "Duyệt KH SX", MetaLink = "#/wf-inbox?type=prod-plan", IsActive = true });
                db.UserRole.AddRange(
                    new UserRole { Id = Guid.NewGuid(), UserGroupId = gApprove, ObjectMenuId = mVh, AllowAccess = true },
                    new UserRole { Id = Guid.NewGuid(), UserGroupId = gApprove, ObjectMenuId = mDh, AllowAccess = true },
                    new UserRole { Id = Guid.NewGuid(), UserGroupId = gApprove, ObjectMenuId = mApprove, AllowAccess = true, AllowEdit = true },
                    new UserRole { Id = Guid.NewGuid(), UserGroupId = gStaff, ObjectMenuId = mVh, AllowAccess = true });
                Guid pA = Guid.NewGuid(), pS = Guid.NewGuid(), pL = Guid.NewGuid(), pDup1 = Guid.NewGuid(), pDup2 = Guid.NewGuid();
                db.Person.AddRange(
                    new Person { Id = pA, FullName = "Trưởng ca Duyệt", PhoneNumber = "0901 234 567" },
                    new Person { Id = pS, FullName = "Nhân viên Văn phòng", PhoneNumber = "+84 902 000 111" },
                    new Person { Id = pL, FullName = "Đã khóa", PhoneNumber = "0903000222" },
                    new Person { Id = pDup1, FullName = "Trùng 1", PhoneNumber = "0909999888" },
                    new Person { Id = pDup2, FullName = "Trùng 2", PhoneNumber = "84909999888" });
                db.User.AddRange(
                    new User { Id = Approver, UserName = "bot12-duyet", PersonId = pA, IsActive = true },
                    new User { Id = Staff, UserName = "bot12-nv", PersonId = pS, IsActive = true },
                    new User { Id = Locked, UserName = "bot12-khoa", PersonId = pL, IsActive = true },
                    new User { Id = Guid.NewGuid(), UserName = "bot12-dup1", PersonId = pDup1, IsActive = true },
                    new User { Id = Guid.NewGuid(), UserName = "bot12-dup2", PersonId = pDup2, IsActive = true });
                db.UserClaim.AddRange(
                    new UserClaim { Id = Guid.NewGuid(), UserId = Approver, UserGroupId = gApprove },
                    new UserClaim { Id = Guid.NewGuid(), UserId = Staff, UserGroupId = gStaff },
                    new UserClaim { Id = Guid.NewGuid(), UserId = Locked, UserGroupId = gStaff });
                db.Employee.Add(new Employee { Id = Worker, FullName = "Công nhân Bếp", Code = "NV-B12", Phone = "0911.222.333", Status = "Active" });
            });
        }

        private HttpClient Bot()
        {
            var c = _factory.CreateClient();
            c.DefaultRequestHeaders.Add(ClientKeyFilter.HeaderName, ClientKey);
            return c;
        }

        private static async Task<LegacyResult> Post(HttpClient c, string action, object body)
            => await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/BotChannel/" + action, body));

        private static object Who(string tg, string bot = "VH") => new { botCode = bot, externalUserId = tg };

        [Fact]
        public void Link_code_round_trip_tamper_and_expiry()
        {
            var exp = new DateTime(2031, 6, 2, 3, 0, 0, DateTimeKind.Utc);
            string code = LinkCodeSigner.Create('U', Approver, exp, SigningKey);
            code.Length.Should().BeLessThanOrEqualTo(64);
            code.Should().MatchRegex("^[A-Za-z0-9_-]+$");
            LinkCodeSigner.Verify(code, SigningKey, exp.AddMinutes(-1), out char kind, out Guid id, out _).Should().BeNull();
            kind.Should().Be('U');
            id.Should().Be(Approver);
            LinkCodeSigner.Verify(code, SigningKey, exp.AddSeconds(1), out _, out _, out _).Should().Be(ConstantHelper.Message.BotLinkCodeExpired);
            string tampered = code[..10] + (code[10] == 'A' ? 'B' : 'A') + code[11..];
            LinkCodeSigner.Verify(tampered, SigningKey, exp.AddMinutes(-1), out _, out _, out _).Should().Be(ConstantHelper.Message.BotLinkCodeInvalid);
            LinkCodeSigner.Verify(code, "khoa-khac-0123456789-0123456789-xx", exp.AddMinutes(-1), out _, out _, out _).Should().Be(ConstantHelper.Message.BotLinkCodeInvalid);
            LinkCodeSigner.Verify("abc", SigningKey, exp, out _, out _, out _).Should().Be(ConstantHelper.Message.BotLinkCodeInvalid);
        }

        [Theory]
        [InlineData("0901 234 567", "84901234567")]
        [InlineData("+84 901-234-567", "84901234567")]
        [InlineData("84901234567", "84901234567")]
        [InlineData("0084901234567", "84901234567")]
        [InlineData("901234567", "84901234567")]
        [InlineData("+1 415 555 0100", "14155550100")]
        [InlineData("123", null)]
        public void Phone_is_normalized(string input, string? expected) => PhoneMatcher.Normalize(input).Should().Be(expected);

        [Fact]
        public async Task Phone_link_unique_match_only_and_employee_only_flow()
        {
            await SeedAsync();
            using var bot = Bot();
            (await Post(bot, "LinkByPhone", new { botCode = "VH", externalUserId = "9001", contactUserId = "9999", phone = "0911222333" })).Fail("của chính tài khoản");
            (await Post(bot, "LinkByPhone", new { botCode = "VH", externalUserId = "9001", contactUserId = "9001", phone = "0900000000" })).Fail("Nhân sự");
            (await Post(bot, "LinkByPhone", new { botCode = "VH", externalUserId = "9001", contactUserId = "9001", phone = "0909 999 888" })).Fail("trùng nhiều");

            var linked = (await Post(bot, "LinkByPhone", new { botCode = "VH", externalUserId = "9001", contactUserId = "9001", phone = "+84911222333", displayName = "CN Bếp" })).Ok();
            linked.GetProperty("linked").GetBoolean().Should().BeTrue();
            linked.GetProperty("employeeId").GetGuid().Should().Be(Worker);
            linked.GetProperty("hasWebAccount").GetBoolean().Should().BeFalse();
            linked.GetProperty("status").GetString().Should().Be("Active");
            (await Post(bot, "IssueUserToken", Who("9001"))).Fail("chưa có tài khoản web");
            (await Post(bot, "EmployeeGetMyShifts", Who("9001"))).Ok();
            (await Post(bot, "EmployeeGetMyRequests", Who("9001"))).Ok();
            (await Post(bot, "EmployeeGetToday", Who("9001"))).Ok().GetProperty("employeeId").GetGuid().Should().Be(Worker);
            (await Post(bot, "EmployeeGetToday", Who("chua-lien-ket"))).Fail("chưa liên kết");
            (await Post(bot, "EmployeeCheckIn", new { botCode = "VH", externalUserId = "9001", latitude = 10.8, longitude = 106.6 })).Fail(); // không có ca → 400 (quy tắc cũ)
        }

        [Fact]
        public async Task Approver_phone_only_gets_limited_token_until_web_code()
        {
            await SeedAsync();
            using var bot = Bot();
            var r = (await Post(bot, "LinkByPhone", new { botCode = "VH", externalUserId = "9101", contactUserId = "9101", phone = "0901234567" })).Ok();
            r.GetProperty("status").GetString().Should().Be(MessagingCode.Identity.PendingCode);
            r.GetProperty("needsCode").GetBoolean().Should().BeTrue();
            r.GetProperty("canUseDieuHanh").GetBoolean().Should().BeTrue();

            var limited = (await Post(bot, "IssueUserToken", Who("9101"))).Ok();
            limited.GetProperty("limited").GetBoolean().Should().BeTrue();
            string limitedToken = limited.GetProperty("token").GetString()!;
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(limitedToken);
            jwt.Claims.Should().Contain(c => c.Type == MessagingCode.Claims.Client && c.Value == "MESSAGING");
            jwt.Claims.Should().Contain(c => c.Type == MessagingCode.Claims.BotLimited && c.Value == "1");
            (jwt.ValidTo - jwt.ValidFrom).TotalMinutes.Should().BeApproximately(10, 1.5);
            (await ApproveStatus(limitedToken)).Should().Be(HttpStatusCode.Forbidden, "token hạn chế không được duyệt");

            // Lấy mã trên web (chính user) → /start <mã> → Active, token đầy đủ duyệt được.
            using var web = _factory.CreateAuthorizedClient(Approver);
            var code = (await web.PostFormAsync("/api/TelegramLink/CreateCode")).Ok();
            string c = code.GetProperty("code").GetString()!;
            code.GetProperty("deepLinks").GetProperty("VH").GetString().Should().Be("https://t.me/delicare_vh_dev_bot?start=" + c);
            (await Post(bot, "LinkByCode", new { botCode = "VH", externalUserId = "9101", code = c })).Ok().GetProperty("status").GetString().Should().Be("Active");
            (await Post(bot, "LinkByCode", new { botCode = "VH", externalUserId = "9101", code = c })).Fail("đã được dùng");
            var full = (await Post(bot, "IssueUserToken", Who("9101"))).Ok();
            full.GetProperty("limited").GetBoolean().Should().BeFalse();
            (await ApproveStatus(full.GetProperty("token").GetString()!)).Should().NotBe(HttpStatusCode.Forbidden);

            // Chặn bot → không cấp token; /start lại → Active (đã xác nhận mã trước đó).
            (await Post(bot, "TouchIdentity", new { botCode = "VH", externalUserId = "9101", blocked = true })).Ok().GetProperty("linked").GetBoolean().Should().BeFalse();
            (await Post(bot, "IssueUserToken", Who("9101"))).Fail("chưa liên kết");
            (await Post(bot, "TouchIdentity", new { botCode = "VH", externalUserId = "9101", blocked = false })).Ok().GetProperty("status").GetString().Should().Be("Active");

            (await _factory.QueryAsync(db => db.IntegrationLog.CountAsync(l => l.DirectionCode == "TOKEN" && l.OperationCode == "ISSUE_USER_TOKEN"))).Should().BeGreaterThanOrEqualTo(3);
        }

        private async Task<HttpStatusCode> ApproveStatus(string token)
        {
            using var c = _factory.CreateClient();
            c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return (await c.PostAsync("/api/ProductionPlan/Approve", LegacyHttp.Form(("Id", Guid.NewGuid())))).StatusCode;
        }

        /// <summary>BOT-1.5a: người chỉ có EmployeeId xin nghỉ qua bot (quy tắc LeaveRequest/Create, ghi dưới system); loại nghỉ; ShiftAssignment/GetMine của tài khoản web.</summary>
        [Fact]
        public async Task Employee_only_leave_via_bot_and_my_shifts_for_web_user()
        {
            await SeedAsync();
            using var bot = Bot();
            (await Post(bot, "LinkByPhone", new { botCode = "VH", externalUserId = "9701", contactUserId = "9701", phone = "0911222333" })).Ok();
            var types = await Post(bot, "GetLeaveTypes", new { });
            types.Ok();
            string type = types.ListData.EnumerateArray().First().GetProperty("code").GetString()!;
            DateTime from = new(2031, 6, 5);
            var created = await Post(bot, "EmployeeCreateLeave", new { botCode = "VH", externalUserId = "9701", fromDate = from, toDate = from, leaveTypeCode = type, reason = "Việc gia đình" });
            created.Ok().GetProperty("request").GetProperty("requestNo").GetString().Should().StartWith("DN-");
            var leave = await _factory.QueryAsync(db => db.LeaveRequest.AsNoTracking().SingleAsync(l => l.EmployeeId == Worker && l.FromDate == from));
            leave.Reason.Should().Be("[Telegram] Việc gia đình");
            leave.RequestStatus.Should().NotBeNullOrEmpty();
            (await Post(bot, "EmployeeCreateLeave", new { botCode = "VH", externalUserId = "chua-lien-ket", fromDate = from, leaveTypeCode = type, reason = "x" })).Fail("chưa liên kết");

            using var staff = _factory.CreateAuthorizedClient(Staff);
            (await staff.GetResultAsync("/api/ShiftAssignment/GetMine")).Fail();
        }

        [Fact]
        public async Task Code_expired_tampered_locked_user_and_admin_link_unlink()
        {
            await SeedAsync();
            using var bot = Bot();
            using var webStaff = _factory.CreateAuthorizedClient(Staff);
            string code = (await webStaff.PostFormAsync("/api/TelegramLink/CreateCode")).Ok().GetProperty("code").GetString()!;
            string tampered = code[..5] + (code[5] == 'x' ? 'y' : 'x') + code[6..];
            (await Post(bot, "LinkByCode", new { botCode = "VH", externalUserId = "9201", code = tampered })).Fail("không hợp lệ");
            _factory.Clock.FixedNow = _factory.Clock.FixedNow!.Value.AddMinutes(16);
            (await Post(bot, "LinkByCode", new { botCode = "VH", externalUserId = "9201", code })).Fail("hết hạn");
            _factory.Clock.FixedNow = new DateTime(2031, 6, 2, 9, 0, 0);

            // Nhân viên văn phòng (không quyền duyệt) khớp số → Active ngay, token đầy đủ; bot DH không có quyền → từ chối.
            (await Post(bot, "LinkByPhone", new { botCode = "VH", externalUserId = "9201", contactUserId = "9201", phone = "0902000111" })).Ok().GetProperty("status").GetString().Should().Be("Active");
            (await Post(bot, "IssueUserToken", Who("9201"))).Ok().GetProperty("limited").GetBoolean().Should().BeFalse();
            (await Post(bot, "LinkByPhone", new { botCode = "DH", externalUserId = "9201", contactUserId = "9201", phone = "0902000111" })).Ok();
            (await Post(bot, "IssueUserToken", Who("9201", "DH"))).Fail("quyền dùng bot DH");

            // Tài khoản bị khóa → không cấp token.
            (await Post(bot, "LinkByPhone", new { botCode = "VH", externalUserId = "9301", contactUserId = "9301", phone = "0903000222" })).Ok();
            await _factory.SeedAsync(async db => (await db.User.SingleAsync(u => u.Id == Locked)).IsActive = false);
            (await Post(bot, "IssueUserToken", Who("9301"))).Fail("không còn hoạt động");
            (await _factory.QueryAsync(db => db.IntegrationLog.CountAsync(l => l.DirectionCode == "TOKEN" && l.LogStatus == "Failed"))).Should().BeGreaterThan(0);

            // Admin: liên kết tay nhân viên + gỡ; non-admin gọi → 403.
            using var admin = _factory.CreateAuthorizedClient();
            (await webStaff.PostAsync("/api/UserExternalIdentity/Link", LegacyHttp.Form(("BotCode", "VH"), ("EmployeeId", Worker), ("ExternalUserId", "9401"))))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);
            var link = (await admin.PostFormAsync("/api/UserExternalIdentity/Link", ("BotCode", "VH"), ("EmployeeId", Worker), ("ExternalUserId", "9401"))).Ok();
            Guid identityId = link.GetProperty("identityId").GetGuid();
            (await Post(bot, "ResolveIdentity", Who("9401"))).Ok().GetProperty("linked").GetBoolean().Should().BeTrue();
            (await admin.PostFormAsync("/api/UserExternalIdentity/Unlink", ("id", identityId))).Ok();
            (await Post(bot, "ResolveIdentity", Who("9401"))).Ok().GetProperty("linked").GetBoolean().Should().BeFalse();

            // HR cấp mã cho nhân viên: cần quyền Sửa #/adm-employee (nhân viên thường → 403; admin → được).
            (await webStaff.PostAsync("/api/TelegramLink/CreateCodeForEmployee", LegacyHttp.Form(("EmployeeId", Worker)))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            string empCode = (await admin.PostFormAsync("/api/TelegramLink/CreateCodeForEmployee", ("EmployeeId", Worker))).Ok().GetProperty("code").GetString()!;
            (await Post(bot, "LinkByCode", new { botCode = "VH", externalUserId = "9501", code = empCode })).Ok().GetProperty("employeeId").GetGuid().Should().Be(Worker);

            // Web: tài khoản xem / gỡ liên kết của mình.
            var mine = await webStaff.GetResultAsync("/api/TelegramLink/GetMine");
            mine.Ok();
            var first = mine.ListData.EnumerateArray().First(x => x.GetProperty("botCode").GetString() == "VH");
            (await webStaff.PostFormAsync("/api/TelegramLink/Unlink", ("id", first.GetProperty("id").GetGuid()))).Ok();
            (await Post(bot, "IssueUserToken", Who("9201"))).Fail("chưa liên kết");
        }
    }
}
