using System.Net.Http.Json;
using AppDelicare.Data.Share;
using AppDelicare.Model.Helper;
using AppDelicare.Services.Implement;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

namespace AppDelicare.Tests.Security
{
    /// <summary>
    /// RÃ  soÃ¡t backend 2026-11-17: (1) khÃ´ng nÃ¢ng quyá»n admin qua User/Insert / User/Update; (2) upload áº£nh há»“ sÆ¡ chá»‰ nháº­n PNG / JPEG tháº­t,
    /// tÃªn tá»‡p do server sinh (khÃ´ng "../", khÃ´ng .html); (3) giá» Ä‘o nhiá»‡t Ä‘á»™ nháº­p tay pháº£i trong khoáº£ng há»£p lá»‡.
    /// </summary>
    public class BackendReviewFixTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public BackendReviewFixTests(InMemoryApiFactory factory) => _factory = factory;

        [Fact]
        public async Task Login_is_throttled_after_repeated_failures_for_same_user()
        {
            using var client = _factory.CreateClient();
            string user = "review-throttle-" + Guid.NewGuid().ToString("N")[..6];
            for (int i = 0; i < AppDelicare.WebApi.Controllers.AuthenticationController.MaxFailuresPerUser; i++)
            {
                var r = await client.PostAsJsonAsync("/api/Authentication/Login", new { userName = user, passWord = "sai-" + i });
                ((int)r.StatusCode).Should().Be(200, "lần sai thứ {0} vẫn trả kết quả bình thường", i + 1);
            }
            var blocked = await client.PostAsJsonAsync("/api/Authentication/Login", new { userName = user, passWord = "bat-ky" });
            ((int)blocked.StatusCode).Should().Be(429);
            var other = await client.PostAsJsonAsync("/api/Authentication/Login", new { userName = user + "x", passWord = "bat-ky" });
            ((int)other.StatusCode).Should().Be(200, "tên đăng nhập khác không bị ảnh hưởng");
        }

        [Fact]
        public async Task User_insert_and_update_ignore_IsAdmin_from_form()
        {
            using var admin = _factory.CreateAuthorizedClient();
            (await admin.PostFormAsync("/api/User/Insert", ("UserName", "review-noadmin"), ("IsActive", true), ("IsAdmin", true))).Ok();
            var created = await _factory.QueryAsync(db => db.User.AsNoTracking().SingleAsync(u => u.UserName == "review-noadmin"));
            created.IsAdmin.Should().BeFalse("Insert khÃ´ng nháº­n IsAdmin tá»« form");

            await admin.PutFormAsync("/api/User/Update", ("Id", created.Id), ("IsActive", true), ("IsAdmin", true));
            (await _factory.QueryAsync(db => db.User.AsNoTracking().Where(u => u.Id == created.Id).Select(u => u.IsAdmin).SingleAsync()))
                .Should().BeFalse("Update khÃ´ng nháº­n IsAdmin tá»« form â€“ chá»‰ User/SaveIsAdmin (AdminOnly)");
        }

        [Fact]
        public void Profile_image_upload_accepts_only_real_png_jpeg_with_server_generated_name()
        {
            string root = Path.Combine(Path.GetTempPath(), "delicare-review-" + Guid.NewGuid().ToString("N"));
            try
            {
                var service = new ImportService(null!, new FakeEnv(root));
                byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];
                byte[] html = "<script>alert(1)</script>"u8.ToArray();

                var ok = service.UploadFileForInsertUpdate([File(png, "../../../anh.png")], ConstantHelper.FolderName.PERSON_IMAGE);
                ok.Result.Should().BeTrue();
                string saved = ok.ListFile!.Single().FileNameHas!;
                saved.Should().MatchRegex("^[0-9a-f]{32}\\.png$", "tÃªn trÃªn Ä‘Ä©a do server sinh, khÃ´ng chá»©a Ä‘Æ°á»ng dáº«n client");
                System.IO.File.Exists(Path.Combine(root, ConstantHelper.Import, ConstantHelper.FolderName.PERSON_IMAGE, saved)).Should().BeTrue();

                service.UploadFileForInsertUpdate([File(html, "x.html")], ConstantHelper.FolderName.PERSON_IMAGE).Result.Should().BeFalse(".html bá»‹ cháº·n");
                service.UploadFileForInsertUpdate([File(html, "fake.png")], ConstantHelper.FolderName.PERSON_IMAGE).Result.Should().BeFalse("Ä‘uÃ´i .png nhÆ°ng ná»™i dung khÃ´ng pháº£i PNG");
                service.UploadFileForInsertUpdate([File(png, "a.png")], "UNKNOWN").Result.Should().BeFalse("loáº¡i thÆ° má»¥c láº¡");
                Directory.GetFiles(root, "*", SearchOption.AllDirectories).Should().HaveCount(1, "chá»‰ tá»‡p há»£p lá»‡ Ä‘Æ°á»£c ghi");
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Fact]
        public void Manual_temperature_time_must_be_recent()
        {
            var clock = new FixedClock(new DateTime(2026, 11, 17, 12, 0, 0));
            TemperatureTime.Resolve(null, clock, out var at).Should().BeNull();
            at.Should().Be(clock.Now);
            TemperatureTime.Resolve(clock.Now.AddMinutes(-30), clock, out at).Should().BeNull();
            TemperatureTime.Resolve(clock.Now.AddDays(1), clock, out _).Should().NotBeNull("giá» tÆ°Æ¡ng lai sáº½ 'che' láº§n Ä‘o tháº­t");
            TemperatureTime.Resolve(clock.Now.AddDays(-3), clock, out _).Should().NotBeNull();
            TemperatureTime.Resolve(DateTime.SpecifyKind(new DateTime(2026, 11, 17, 4, 50, 0), DateTimeKind.Utc), clock, out at).Should().BeNull();
            at.Should().Be(new DateTime(2026, 11, 17, 11, 50, 0), "giá» UTC Ä‘á»•i sang giá» VN");
        }

        private static IFormFile File(byte[] content, string name)
            => new FormFile(new MemoryStream(content), 0, content.Length, "File", name);

        private sealed class FixedClock(DateTime now) : IClock
        {
            public DateTime Now => now;
            public DateTime Today => now.Date;
            public DateTime UtcNow => DateTime.SpecifyKind(now.AddHours(-7), DateTimeKind.Utc);
        }

        private sealed class FakeEnv(string root) : IWebHostEnvironment
        {
            public string WebRootPath { get; set; } = root;
            public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
            public string ApplicationName { get; set; } = "test";
            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
            public string ContentRootPath { get; set; } = root;
            public string EnvironmentName { get; set; } = "Test";
        }
    }
}
