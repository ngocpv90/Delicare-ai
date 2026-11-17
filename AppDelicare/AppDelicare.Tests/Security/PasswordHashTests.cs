using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Security
{
    /// <summary>
    /// T48.0 (D-29): mật khẩu băm PBKDF2; hash MD5 cũ vẫn đăng nhập được và tự nâng lên PBKDF2; tạo / reset / đổi mật khẩu ghi PBKDF2.
    /// Gọi qua route cũ (Authentication/Login, User/*) – API không đổi.
    /// </summary>
    public class PasswordHashTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public PasswordHashTests(InMemoryApiFactory factory) => _factory = factory;

        private async Task<Guid> SeedUserAsync(string userName, string storedHash)
        {
            var id = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.User.Add(new User { Id = id, UserName = userName, PassWord = storedHash, IsActive = true });
                return Task.CompletedTask;
            });
            return id;
        }

        /// <summary>Đăng nhập: trả code của Response cũ (200 = thành công) và có token hay không.</summary>
        private async Task<(int Code, bool HasToken)> LoginAsync(string userName, string password)
        {
            using var client = _factory.CreateClient();
            var response = await client.PostAsJsonAsync("/api/Authentication/Login", new { userName, passWord = password });
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            int code = doc.RootElement.GetProperty("code").GetInt32();
            bool hasToken = doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object
                && data.TryGetProperty("token", out var token) && !string.IsNullOrEmpty(token.GetString());
            return (code, hasToken);
        }

        private Task<string?> StoredAsync(Guid id) => _factory.QueryAsync(db => db.User.AsNoTracking().Where(u => u.Id == id).Select(u => u.PassWord).SingleAsync());

        [Fact]
        public async Task Legacy_md5_user_logs_in_and_is_upgraded_to_pbkdf2()
        {
            var id = await SeedUserAsync("t480-legacy", EncriptFunctionsHelper.GenerateMd5String("Mat-khau-cu-1"));

            (await LoginAsync("t480-legacy", "Mat-khau-cu-1")).Should().Be((200, true));
            (await StoredAsync(id)).Should().StartWith("PBKDF2$100000$", "hash cũ được nâng ngay khi đăng nhập đúng");
            (await LoginAsync("t480-legacy", "Mat-khau-cu-1")).Should().Be((200, true), "lần 2 đăng nhập bằng hash mới");
            (await LoginAsync("t480-legacy", "sai-mat-khau")).Code.Should().NotBe(200);
        }

        [Fact]
        public async Task Legacy_change_password_hash_md5_password_plus_username_also_logs_in()
        {
            // ChangePassword cũ lưu MD5(mật khẩu + tên) nên trước T48.0 user đó không đăng nhập lại được.
            var id = await SeedUserAsync("t480-changed", EncriptFunctionsHelper.GenerateMd5String("Moi-12345" + "t480-changed"));

            (await LoginAsync("t480-changed", "Moi-12345")).Should().Be((200, true));
            (await StoredAsync(id)).Should().StartWith("PBKDF2$");
        }

        [Fact]
        public async Task Wrong_password_is_rejected_for_old_and_new_hashes()
        {
            await SeedUserAsync("t480-md5", EncriptFunctionsHelper.GenerateMd5String("Dung-12345"));
            await SeedUserAsync("t480-new", EncriptFunctionsHelper.GeneratePassword("Dung-12345"));

            (await LoginAsync("t480-md5", "Sai-12345")).Code.Should().NotBe(200);
            (await LoginAsync("t480-new", "Sai-12345")).Code.Should().NotBe(200);
            (await LoginAsync("t480-new", "Dung-12345")).Should().Be((200, true));
        }

        [Fact]
        public void Same_password_gives_different_hashes_and_powershell_deploy_hash_verifies()
        {
            string a = EncriptFunctionsHelper.GeneratePassword("Cung-mat-khau");
            string b = EncriptFunctionsHelper.GeneratePassword("Cung-mat-khau");

            a.Should().NotBe(b, "salt ngẫu nhiên");
            EncriptFunctionsHelper.VerifyPassword("Cung-mat-khau", a).Should().BeTrue();
            EncriptFunctionsHelper.VerifyPassword("Cung-mat-khau", b).Should().BeTrue();
            EncriptFunctionsHelper.VerifyPassword("cung-mat-khau", a).Should().BeFalse();
            EncriptFunctionsHelper.VerifyPassword("x", "PBKDF2$abc$không-hợp-lệ$").Should().BeFalse();

            // Sinh bởi deploy/db/Deploy-Database.ps1 (PowerShell 5.1, .NET Framework) cho mật khẩu demo "Demo-12345".
            const string fromPowerShell = "PBKDF2$100000$Q9NsMrjO2PgA1w/QP9l7Og==$WnyGfte//pqAmqvTM+fYPZ/S/WSd8QAA9A5LzVVLp/w=";
            EncriptFunctionsHelper.VerifyPassword("Demo-12345", fromPowerShell).Should().BeTrue();
            EncriptFunctionsHelper.VerifyPassword("Demo-12346", fromPowerShell).Should().BeFalse();
        }

        [Fact]
        public async Task Create_reset_and_change_password_write_pbkdf2_and_new_password_logs_in()
        {
            using var admin = _factory.CreateAuthorizedClient();

            // T45R.8 (D-39): mật khẩu ban đầu = mật khẩu tạm trả một lần (trước đây = tên đăng nhập), lưu PBKDF2T$.
            string initial = (await admin.PostFormAsync("/api/User/Insert", ("UserName", "t480-created"), ("IsActive", true))).Ok().GetProperty("tempPassword").GetString()!;
            var created = await _factory.QueryAsync(db => db.User.AsNoTracking().SingleAsync(u => u.UserName == "t480-created"));
            created.PassWord.Should().StartWith("PBKDF2T$");
            (await LoginAsync("t480-created", "t480-created")).Code.Should().NotBe(200, "không còn mật khẩu = tên đăng nhập");
            (await LoginAsync("t480-created", initial)).Should().Be((200, true));

            // Rà soát BE: chỉ đổi mật khẩu của chính mình – admin gọi cho user khác bị chặn (admin dùng ResetPassword).
            (await admin.PutFormAsync("/api/User/ChangePassword", ("Id", created.Id), ("PassWordOld", initial), ("PassWordNew", "Mat-khau-moi-8"))).Fail("chính tài khoản");
            using var self = _factory.CreateAuthorizedClient(created.Id);
            (await self.PutFormAsync("/api/User/ChangePassword", ("Id", created.Id), ("PassWordOld", initial), ("PassWordNew", "Mat-khau-moi-8"))).Ok();
            (await StoredAsync(created.Id)).Should().StartWith("PBKDF2$");
            (await LoginAsync("t480-created", "Mat-khau-moi-8")).Should().Be((200, true), "trước T48.0 đổi mật khẩu xong không đăng nhập lại được");
            (await LoginAsync("t480-created", initial)).Code.Should().NotBe(200);
            (await self.PutFormAsync("/api/User/ChangePassword", ("Id", created.Id), ("PassWordOld", "Mat-khau-moi-8"), ("PassWordNew", "1234567"))).Fail("8 ký tự");

            string reset = (await admin.GetResultAsync($"/api/User/ResetPassword?id={created.Id}")).Ok().GetString()!;
            (await StoredAsync(created.Id)).Should().StartWith("PBKDF2T$");
            (await LoginAsync("t480-created", "Mat-khau-moi-8")).Code.Should().NotBe(200, "reset thay mật khẩu cũ");
            (await LoginAsync("t480-created", reset)).Should().Be((200, true));
        }
    }
}
