using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace AppDelicare.Tests.Admin
{
    /// <summary>
    /// T45R.8 (D-38, D-39) – tạo người dùng 1 bước (Person/Insert → Person + đúng 1 User + nhóm), chặn tài khoản thứ 2 / trùng tên,
    /// mật khẩu tạm khi reset / tạo (12 ký tự, 24 giờ, PBKDF2T$), đăng nhập bằng mật khẩu tạm chỉ gọi được ChangePassword / GetCurrent,
    /// đổi xong dùng bình thường, quá hạn bị từ chối, mật khẩu tạm không nằm trong AuditChange / log.
    /// </summary>
    // T50R1: chạy KHÔNG song song – test reset thay Log.Logger (tĩnh) để bắt log; host test khác khởi động cùng lúc (UseSerilog) ghi đè Log.Logger → test chập chờn.
    [Collection(StaticLoggerCollection.Name)]
    public class UserAccountTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public UserAccountTests(InMemoryApiFactory factory) => _factory = factory;

        private sealed record LoginResult(int Code, string? Message, string? Token, bool MustChangePassword);

        private async Task<LoginResult> LoginAsync(string userName, string password)
        {
            using var client = _factory.CreateClient();
            var response = await client.PostAsJsonAsync("/api/Authentication/Login", new { userName, passWord = password });
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            string? token = null;
            bool must = false;
            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
            {
                token = data.GetProperty("token").GetString();
                must = data.GetProperty("mustChangePassword").GetBoolean();
            }
            return new LoginResult(root.GetProperty("code").GetInt32(), root.TryGetProperty("message", out var m) ? m.GetString() : null, token, must);
        }

        private HttpClient ClientWithToken(string token)
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        private async Task<Guid> SeedGroupAsync(string name)
        {
            var id = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.UserGroup.Add(new UserGroup { Id = id, Name = name });
                return Task.CompletedTask;
            });
            return id;
        }

        private async Task<Guid> SeedUserWithTempPasswordAsync(string userName, bool isAdmin)
        {
            var id = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.User.Add(new User { Id = id, UserName = userName, PassWord = EncriptFunctionsHelper.GeneratePassword("Cu-123456"), IsActive = true, IsAdmin = isAdmin });
                return Task.CompletedTask;
            });
            return id;
        }

        // ---------- D-38: tạo người dùng 1 bước ----------

        [Fact]
        public async Task Person_insert_creates_person_exactly_one_user_and_groups_in_one_step()
        {
            Guid g1 = await SeedGroupAsync("T45R8-NHOM-1"), g2 = await SeedGroupAsync("T45R8-NHOM-2");
            using var admin = _factory.CreateAuthorizedClient();

            var data = (await admin.PostFormAsync("/api/Person/Insert", ("FullName", "Nguyễn Văn Một Bước"), ("PhoneNumber", "0900000001"),
                ("UserName", " T45R8.One "), ("ListUserGroupId", $"{g1},{g2}"), ("IsActive", true))).Ok();

            Guid personId = data.GetProperty("personId").GetGuid();
            Guid userId = data.GetProperty("userId").GetGuid();
            data.GetProperty("userName").GetString().Should().Be("t45r8.one", "chữ thường, bỏ khoảng trắng đầu / cuối");
            data.GetProperty("tempPassword").GetString().Should().HaveLength(12);

            (await _factory.QueryAsync(db => db.Person.CountAsync(p => p.Id == personId && p.FullName == "Nguyễn Văn Một Bước"))).Should().Be(1);
            var users = await _factory.QueryAsync(db => db.User.AsNoTracking().Where(u => u.PersonId == personId && !u.IsDeleted).ToListAsync());
            users.Should().ContainSingle();
            users[0].Id.Should().Be(userId);
            users[0].IsActive.Should().BeTrue();
            users[0].PassWord.Should().StartWith("PBKDF2T$", "mật khẩu ban đầu là mật khẩu tạm (D-39)");
            var groups = await _factory.QueryAsync(db => db.UserClaim.Where(c => c.UserId == userId && !c.IsDeleted).Select(c => c.UserGroupId).ToListAsync());
            groups.Should().BeEquivalentTo(new Guid?[] { g1, g2 });

            // Tài khoản mới đăng nhập được bằng mật khẩu tạm, bị bắt đổi mật khẩu.
            var login = await LoginAsync("t45r8.one", data.GetProperty("tempPassword").GetString()!);
            login.Code.Should().Be(200);
            login.MustChangePassword.Should().BeTrue();
        }

        [Fact]
        public async Task Person_insert_without_username_generates_one_like_legacy_and_adds_suffix_when_taken()
        {
            using var admin = _factory.CreateAuthorizedClient();

            var first = (await admin.PostFormAsync("/api/Person/Insert", ("FullName", "Đỗ  Thị Hà"))).Ok();
            first.GetProperty("userName").GetString().Should().Be("hadt", "tên + chữ cái đầu họ / đệm, không dấu (Đ → d)");
            var second = (await admin.PostFormAsync("/api/Person/Insert", ("FullName", "Đinh Thị Hà"))).Ok();
            second.GetProperty("userName").GetString().Should().Be("hadt1");

            // Không gửi IsActive → giữ hành vi cũ (tài khoản tạo kèm hồ sơ chưa kích hoạt).
            Guid userId = first.GetProperty("userId").GetGuid();
            (await _factory.QueryAsync(db => db.User.Where(u => u.Id == userId).Select(u => u.IsActive).SingleAsync())).Should().BeFalse();
        }

        [Fact]
        public async Task Duplicate_username_and_second_user_for_same_person_are_blocked()
        {
            using var admin = _factory.CreateAuthorizedClient();
            var created = (await admin.PostFormAsync("/api/Person/Insert", ("FullName", "Trần Văn Trùng"), ("UserName", "t45r8-dup"), ("IsActive", true))).Ok();
            Guid personId = created.GetProperty("personId").GetGuid();
            int persons = await _factory.QueryAsync(db => db.Person.CountAsync());

            // Trùng tên đăng nhập (khác hoa thường) → chặn, không để lại Person mồ côi.
            (await admin.PostFormAsync("/api/Person/Insert", ("FullName", "Lê Văn Khác"), ("UserName", "T45R8-DUP"))).Fail("đã tồn tại");
            (await _factory.QueryAsync(db => db.Person.CountAsync())).Should().Be(persons);
            // Tên có dấu / khoảng trắng → chặn.
            (await admin.PostFormAsync("/api/Person/Insert", ("FullName", "Lê Văn Khác"), ("UserName", "lê văn"))).Fail("không dấu");
            // Nhóm không tồn tại → chặn.
            (await admin.PostFormAsync("/api/Person/Insert", ("FullName", "Lê Văn Khác"), ("ListUserGroupId", Guid.NewGuid().ToString()))).Fail("nhóm");
            (await _factory.QueryAsync(db => db.Person.CountAsync())).Should().Be(persons);

            // User/Insert cho Person đã có User → chặn (1 Person ↔ tối đa 1 User).
            (await admin.PostFormAsync("/api/User/Insert", ("UserName", "t45r8-second"), ("PersonId", personId), ("IsActive", true))).Fail("tối đa 1 tài khoản");
            // User/Insert trùng UserName → chặn.
            (await admin.PostFormAsync("/api/User/Insert", ("UserName", "T45R8-Dup"), ("IsActive", true))).Fail("đã tồn tại");
            (await _factory.QueryAsync(db => db.User.CountAsync(u => u.PersonId == personId && !u.IsDeleted))).Should().Be(1);

            // Hồ sơ chưa có tài khoản → User/Insert được, trả mật khẩu tạm.
            var bare = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Person.Add(new Person { Id = bare, FullName = "Hồ sơ có sẵn" });
                return Task.CompletedTask;
            });
            var attached = (await admin.PostFormAsync("/api/User/Insert", ("UserName", "t45r8-attach"), ("PersonId", bare), ("IsActive", true))).Ok();
            attached.GetProperty("personId").GetGuid().Should().Be(bare);
            attached.GetProperty("tempPassword").GetString().Should().HaveLength(12);
        }

        // ---------- D-39: mật khẩu tạm ----------

        [Fact]
        public void Temporary_password_is_12_chars_unambiguous_with_upper_lower_digit()
        {
            for (int i = 0; i < 300; i++)
            {
                string p = EncriptFunctionsHelper.GenerateTemporaryPassword();
                p.Should().HaveLength(12);
                p.Should().NotContainAny("0", "O", "1", "l", "I");
                p.Any(char.IsUpper).Should().BeTrue();
                p.Any(char.IsLower).Should().BeTrue();
                p.Any(char.IsDigit).Should().BeTrue();
                p.All(char.IsLetterOrDigit).Should().BeTrue();
            }
            string hash = EncriptFunctionsHelper.GenerateTemporaryPasswordHash("Abc234defGHJ", new DateTime(2026, 11, 17, 0, 0, 0, DateTimeKind.Utc));
            hash.Should().MatchRegex(@"^PBKDF2T\$\d+\$100000\$[^$]+\$[^$]+$");
            EncriptFunctionsHelper.VerifyPassword("Abc234defGHJ", hash).Should().BeTrue();
            EncriptFunctionsHelper.VerifyPassword("Abc234defGHK", hash).Should().BeFalse();
            EncriptFunctionsHelper.IsPbkdf2(hash).Should().BeFalse("mật khẩu tạm có dấu riêng");
            EncriptFunctionsHelper.IsTemporaryExpired(hash, new DateTime(2026, 11, 16, 23, 0, 0, DateTimeKind.Utc)).Should().BeFalse();
            EncriptFunctionsHelper.IsTemporaryExpired(hash, new DateTime(2026, 11, 17, 0, 0, 1, DateTimeKind.Utc)).Should().BeTrue();
        }

        [Fact]
        public async Task Reset_returns_temp_password_once_and_it_is_not_in_audit_change_or_log()
        {
            Guid userId = await SeedUserWithTempPasswordAsync("t45r8-reset", isAdmin: false);
            using var admin = _factory.CreateAuthorizedClient();

            var sink = new CollectingSink();
            var previous = Log.Logger;
            Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).WriteTo.Logger(previous).CreateLogger();
            string temp;
            try
            {
                var result = await admin.GetResultAsync($"/api/User/ResetPassword?id={userId}");
                temp = result.Ok().GetString()!;
                result.Message.Should().Be(ConstantHelper.Message.ResetPassword);
            }
            finally
            {
                Log.Logger = previous;
            }

            temp.Should().HaveLength(12);
            string stored = (await _factory.QueryAsync(db => db.User.AsNoTracking().Where(u => u.Id == userId).Select(u => u.PassWord).SingleAsync()))!;
            stored.Should().StartWith("PBKDF2T$");
            stored.Should().NotContain(temp, "không lưu bản rõ");

            // AuditChange: ghi ai (CreatedBy = admin) đặt lại cho ai (IdChange = user), không có mật khẩu / hash.
            var audits = await _factory.QueryAsync(db => db.AuditChange.AsNoTracking().Where(a => a.IdChange == userId && a.EntityName == nameof(User)).ToListAsync());
            audits.Should().Contain(a => a.CreatedBy == InMemoryApiFactory.UserId && a.Action == "Modified" && a.Changes!.Contains("PassWord"));
            var allChanges = await _factory.QueryAsync(db => db.AuditChange.AsNoTracking().Select(a => a.Changes).ToListAsync());
            allChanges.Should().NotContain(c => c != null && (c.Contains(temp) || c.Contains("PBKDF2T$")));

            // Log: có dòng ghi việc reset nhưng không có mật khẩu.
            sink.Messages.Should().Contain(m => m.Contains(userId.ToString()));
            sink.Messages.Should().NotContain(m => m.Contains(temp));

            // Mật khẩu cũ hết tác dụng; mật khẩu tạm đăng nhập được.
            (await LoginAsync("t45r8-reset", "Cu-123456")).Code.Should().NotBe(200);
            (await LoginAsync("t45r8-reset", temp)).Code.Should().Be(200);
        }

        [Fact]
        public async Task Temp_login_only_allows_change_password_and_get_current_then_works_normally()
        {
            Guid userId = await SeedUserWithTempPasswordAsync("t45r8-temp", isAdmin: true);
            using var admin = _factory.CreateAuthorizedClient();
            string temp = (await admin.GetResultAsync($"/api/User/ResetPassword?id={userId}")).Ok().GetString()!;

            var login = await LoginAsync("t45r8-temp", temp);
            login.Code.Should().Be(200);
            login.MustChangePassword.Should().BeTrue();
            new JwtSecurityTokenHandler().ReadJwtToken(login.Token).Claims.Should().Contain(c => c.Type == "pwd_temp");

            using var c = ClientWithToken(login.Token!);
            var current = (await c.GetResultAsync("/api/User/GetCurrent")).Ok();
            current.GetProperty("mustChangePassword").GetBoolean().Should().BeTrue();

            // Mọi API khác → 403 "Cần đổi mật khẩu" (kể cả admin, kể cả action tự phục vụ như GetProfile).
            foreach (string url in new[] { "/api/UserGroup/GetAllData", "/api/Person/GetProfile", $"/api/User/GetById?id={userId}" })
            {
                var blocked = await c.GetAsync(url);
                blocked.StatusCode.Should().Be(HttpStatusCode.Forbidden, url);
                (await blocked.Content.ReadAsStringAsync()).Should().Contain(ConstantHelper.Message.MustChangePassword);
            }

            // Đổi mật khẩu (được phép) → lưu PBKDF2 thường; token cũ hết bị chặn; đăng nhập lại bình thường.
            (await c.PutFormAsync("/api/User/ChangePassword", ("Id", userId), ("PassWordOld", temp), ("PassWordNew", "Moi-T45R8-ok"))).Ok();
            (await _factory.QueryAsync(db => db.User.AsNoTracking().Where(u => u.Id == userId).Select(u => u.PassWord).SingleAsync())).Should().StartWith("PBKDF2$");
            (await c.GetAsync("/api/UserGroup/GetAllData")).StatusCode.Should().Be(HttpStatusCode.OK);

            var normal = await LoginAsync("t45r8-temp", "Moi-T45R8-ok");
            normal.Code.Should().Be(200);
            normal.MustChangePassword.Should().BeFalse();
            new JwtSecurityTokenHandler().ReadJwtToken(normal.Token).Claims.Should().NotContain(x => x.Type == "pwd_temp");
            using var c2 = ClientWithToken(normal.Token!);
            (await c2.GetAsync("/api/UserGroup/GetAllData")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await c2.GetResultAsync("/api/User/GetCurrent")).Ok().GetProperty("mustChangePassword").GetBoolean().Should().BeFalse();
        }

        [Fact]
        public async Task Expired_temp_password_is_rejected_after_24_hours()
        {
            Guid userId = await SeedUserWithTempPasswordAsync("t45r8-expired", isAdmin: false);
            using var admin = _factory.CreateAuthorizedClient();
            var start = new DateTime(2026, 11, 16, 9, 0, 0);
            _factory.Clock.FixedNow = start;
            try
            {
                string temp = (await admin.GetResultAsync($"/api/User/ResetPassword?id={userId}")).Ok().GetString()!;

                _factory.Clock.FixedNow = start.AddHours(23);
                (await LoginAsync("t45r8-expired", temp)).Code.Should().Be(200, "còn hạn");

                _factory.Clock.FixedNow = start.AddHours(24).AddMinutes(1);
                var expired = await LoginAsync("t45r8-expired", temp);
                expired.Code.Should().Be(400);
                expired.Message.Should().Be("Mật khẩu tạm đã hết hạn, liên hệ quản trị");
                expired.Token.Should().BeNull();
                // Đổi mật khẩu bằng mật khẩu tạm đã hết hạn cũng bị chặn.
                (await _factory.CreateAuthorizedClient(userId).PutFormAsync("/api/User/ChangePassword", ("Id", userId), ("PassWordOld", temp), ("PassWordNew", "Moi-12345678"))).Fail("hết hạn");
                // Sai mật khẩu vẫn báo lỗi chung (không lộ việc tài khoản có mật khẩu tạm).
                (await LoginAsync("t45r8-expired", "sai-mat-khau")).Message.Should().Be(ConstantHelper.Message.UserInActiveMessage);
            }
            finally
            {
                _factory.Clock.FixedNow = null;
            }
        }

        private sealed class CollectingSink : ILogEventSink
        {
            private readonly ConcurrentQueue<string> _messages = new();
            public IReadOnlyCollection<string> Messages => [.. _messages];
            public void Emit(LogEvent logEvent) => _messages.Enqueue(logEvent.RenderMessage() + " " + logEvent.Exception);
        }
    }
}
