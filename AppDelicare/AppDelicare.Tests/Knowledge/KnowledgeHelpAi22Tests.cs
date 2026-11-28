using System.Text;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Services.Implement.Knowledge;
using AppDelicare.Tests.Agent;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Knowledge
{
    /// <summary>
    /// AI-22.3 (D-141 l, §7.11): exporter nhập docs/user-guide vào 80 › Hướng dẫn sử dụng (chạy lại không nhân bản, đổi nội dung thì cập nhật, chỉ đọc);
    /// GetHelp của 1 màn = mục có mã màn trong tiêu đề → các bước (target cho "Chỉ cho tôi"), ghi chú, câu hỏi thường gặp; màn không có → Found = false;
    /// thư viện theo màn; phản hồi Có / Chưa ghi nhận; HDSD đọc được bởi mọi tài khoản nội bộ.
    /// </summary>
    public class KnowledgeHelpAi22Tests
    {
        private static void Put(string dir, string name, string text)
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, name), text, new UTF8Encoding(false));
        }

        private const string Guide = """
            ---
            screens: [scm-uom, scm-warehouse]
            roles: [FIN-IT, OPS-WH]
            updated: 2026-11-28
            ---
            # 01 – Quản trị hệ thống

            ## Thử quy đổi đơn vị – `scm-uom`

            Kiểm nhanh một quy đổi.

            1. **Mở Thử quy đổi** – nút trên đầu màn. {target: uom-try}
            2. **Chọn Từ / Sang đơn vị** → Quy đổi.
            3. **Đọc kết quả** – chưa có quy đổi thì báo. {target: uom-list}

            > Quy đổi theo đơn vị gốc.

            ## Câu hỏi thường gặp

            - **Không thấy đơn vị thùng?** – Thêm đơn vị đóng gói trước.
            - **Kết quả lẻ nhiều số?** – Làm tròn 4 chữ số.

            | Thông báo | Cách xử lý |
            |---|---|
            | Chưa có quy đổi | Thêm quy đổi ở cột trái |
            """;

        [Fact]
        public async Task User_guide_is_imported_once_and_help_returns_steps_targets_faq_for_the_screen()
        {
            string vault = Path.Combine(Path.GetTempPath(), "dc-knowledge-" + Guid.NewGuid().ToString("N"));
            string src = Path.Combine(Path.GetTempPath(), "dc-guide-" + Guid.NewGuid().ToString("N"));
            Put(src, "01_QUAN_TRI.md", Guide);
            Put(src, "README.md", "# không nhập\n");
            using var f = await AgentToolsAi3Tests.FactoryAsync(("Knowledge:VaultPath", vault), ("Knowledge:UserGuidePath", src));
            using var c = f.CreateAuthorizedClient();
            Guid plain = Guid.NewGuid(), g = Guid.NewGuid();
            await f.SeedAsync(async db =>
            {
                db.User.Add(new User { Id = plain, UserName = "kho-a223", IsActive = true, AccountType = "INTERNAL" });
                db.UserGroup.Add(new UserGroup { Id = g, Name = "kho-a223", Code = "KHO-A223" });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = plain, UserGroupId = g });
                var m = await db.ObjectMenu.FirstOrDefaultAsync(x => x.MetaLink == "#/knowledge?folder=80-hdsd");
                if (m == null)
                {
                    m = new ObjectMenu { Id = Guid.NewGuid(), Name = "80-hdsd", MetaLink = "#/knowledge?folder=80-hdsd", LevelShow = 2, IsActive = true };
                    db.ObjectMenu.Add(m);
                }
                db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = g, ObjectMenuId = m.Id, AllowAccess = true });
            });
            async Task<T> As<T>(Func<IKnowledgeService, Task<T>> run)
            {
                using var scope = f.Services.CreateScope();
                using var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForUser(plain));
                return await run(scope.ServiceProvider.GetRequiredService<IKnowledgeService>());
            }

            (await As(s => s.ExportUserGuideAsync())).Should().Be(1, "README không nhập");
            (await As(s => s.ExportUserGuideAsync())).Should().Be(0, "chạy lại không nhân bản");
            string note = Path.Combine(vault, "80 Đào tạo", "Hướng dẫn sử dụng", "01_QUAN_TRI.md");
            File.ReadAllText(note).Should().Contain("source: delicare-docs").And.Contain("screens: [scm-uom, scm-warehouse]");

            var h = (await As(s => s.GetHelp("scm-uom"))).Data!;
            h.Found.Should().BeTrue();
            h.Section.Should().Be("Thử quy đổi đơn vị");
            h.Roles.Should().Be("[FIN-IT, OPS-WH]");
            h.Updated.Should().Be(new DateTime(2026, 11, 28));
            h.Steps.Select(x => (x.Title, x.Target)).Should().Equal(("Mở Thử quy đổi", "uom-try"), ("Chọn Từ / Sang đơn vị", (string?)null), ("Đọc kết quả", "uom-list"));
            h.Steps[0].Text.Should().Be("nút trên đầu màn.", "{target} không hiện trong chữ");
            h.Notes.Should().Equal("Quy đổi theo đơn vị gốc.");
            h.Faq.Select(x => x.Q).Should().Equal("Không thấy đơn vị thùng?", "Kết quả lẻ nhiều số?", "Chưa có quy đổi");
            (await As(s => s.GetHelp("scm-zone"))).Data!.Found.Should().BeFalse("màn không có mục HDSD → mở mục lục");
            (await As(s => s.GetGuides())).ListData!.Single().Screens.Should().Equal("scm-uom", "scm-warehouse");

            // HDSD tự nhập chỉ đọc; sửa file nguồn → lần nhập sau cập nhật.
            (await As(s => s.GetNote("80 Đào tạo/Hướng dẫn sử dụng/01_QUAN_TRI.md"))).Data!.CanEdit.Should().BeFalse();
            Put(src, "01_QUAN_TRI.md", Guide.Replace("Kiểm nhanh một quy đổi.", "Kiểm nhanh quy đổi trước khi dùng."));
            (await As(s => s.ExportUserGuideAsync())).Should().Be(1);
            (await As(s => s.GetHelp("scm-uom"))).Data!.Intro.Should().Be("Kiểm nhanh quy đổi trước khi dùng.");

            (await As(s => s.HelpFeedback(new KnowledgeHelpFeedbackRequest { Screen = "scm-uom", Path = h.Path, Helpful = false, Comment = "Thiếu ví dụ thùng" }))).ResponseCode.Should().Be(200);
            var fb = await f.QueryAsync(db => db.AuditChange.AsNoTracking().Where(a => a.EntityName == KnowledgeService.HelpFeedbackEntity).ToListAsync());
            fb.Should().ContainSingle(a => a.Action == "Chưa" && a.CreatedBy == plain && a.Changes!.Contains("Thiếu ví dụ thùng"));
        }
    }
}
