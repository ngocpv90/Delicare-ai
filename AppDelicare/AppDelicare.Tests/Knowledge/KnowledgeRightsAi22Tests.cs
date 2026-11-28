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
    /// AI-22.1 (D-141, §7.9) – kho tài liệu doanh nghiệp phân quyền theo từng vùng (mỗi vùng 1 mục quyền): mặc định từ chối, vùng con không thừa kế thư mục cha,
    /// tìm không lộ tiêu đề, admin không tự thấy vùng cá nhân / BOD, agent = quyền agent ∩ người hỏi (vùng cá nhân chỉ HR, BOD chặn), việc không người hỏi không đọc vùng hạn chế,
    /// đồng bộ Obsidian không gồm hợp đồng / chứng từ / hồ sơ cá nhân.
    /// </summary>
    public class KnowledgeRightsAi22Tests
    {
        private static string Fm(string id, string conf = "nội bộ")
            => $"---\nid: {id}\ntype: ho-so\nsource: human\nowner: Test\nstatus: hiệu lực\nvalid_from: 2026-11-01\nreview_by: 2027-11-01\nconfidentiality: {conf}\nrefs: []\n---\n# {id}\n";

        private static void Put(string vault, string rel, string body)
        {
            string full = Path.Combine(vault, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, body, new UTF8Encoding(false));
        }

        private static async Task<Guid> UserAsync(InMemoryApiFactory f, string name, bool admin, params string[] folders)
        {
            Guid u = Guid.NewGuid(), g = Guid.NewGuid();
            await f.SeedAsync(async db =>
            {
                db.User.Add(new User { Id = u, UserName = name, IsActive = true, IsAdmin = admin, AccountType = "INTERNAL" });
                db.UserGroup.Add(new UserGroup { Id = g, Name = name, Code = name.ToUpperInvariant() });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = u, UserGroupId = g });
                foreach (string folder in folders)
                {
                    string link = "#/knowledge?folder=" + folder.ToLowerInvariant();
                    var m = await db.ObjectMenu.FirstOrDefaultAsync(x => x.MetaLink == link) ?? db.ObjectMenu.Local.FirstOrDefault(x => x.MetaLink == link);
                    if (m == null)
                    {
                        m = new ObjectMenu { Id = Guid.NewGuid(), Name = folder, MetaLink = link, LevelShow = 2, IsActive = true };
                        db.ObjectMenu.Add(m);
                    }
                    db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = g, ObjectMenuId = m.Id, AllowAccess = true });
                }
            });
            return u;
        }

        private static async Task<T> As<T>(InMemoryApiFactory f, Guid user, Func<IKnowledgeService, Task<T>> run)
        {
            using var scope = f.Services.CreateScope();
            using var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForUser(user));
            return await run(scope.ServiceProvider.GetRequiredService<IKnowledgeService>());
        }

        private static async Task<T> Svc<T>(InMemoryApiFactory f, Func<IKnowledgeService, Task<T>> run)
        {
            using var scope = f.Services.CreateScope();
            return await run(scope.ServiceProvider.GetRequiredService<IKnowledgeService>());
        }

        private const string Contract = "20 Kinh doanh & Hợp đồng/Hợp đồng khách hàng/HĐ khách Vinpearl.md";
        private const string Personal = "70 Nhân sự/Hồ sơ cá nhân/Hồ sơ NV001.md";
        private const string Voucher = "60 Kế toán/Chứng từ gốc/Hóa đơn đầu vào T10.md";

        [Fact]
        public async Task Each_area_has_its_own_right_default_deny_no_title_leak_admin_and_agents_limited()
        {
            string vault = Path.Combine(Path.GetTempPath(), "dc-knowledge-" + Guid.NewGuid().ToString("N"));
            using var f = await AgentToolsAi3Tests.FactoryAsync(("Knowledge:VaultPath", vault));
            f.CreateAuthorizedClient().Dispose();
            Put(vault, Contract, Fm("hd-vinpearl") + "Phạt giao trễ 5% giá trị suất ăn.\n");
            Put(vault, "20 Kinh doanh & Hợp đồng/Ghi chú khách Vinpearl.md", Fm("gc-vinpearl") + "Khách thích món cá.\n");
            Put(vault, Personal, Fm("hs-nv001") + "Hợp đồng lao động NV001 hết hạn 2027-03-01.\n");
            Put(vault, "70 Nhân sự/Nội quy.md", Fm("noi-quy") + "Giờ làm việc 7:00.\n");
            Put(vault, Voucher, Fm("ct-t10") + "Hóa đơn đầu vào tháng 10 Vinpearl.\n");
            Put(vault, "80 Đào tạo/Hướng dẫn sử dụng/Nhận hàng.md", Fm("hdsd-nhan-hang") + "Bấm Nhận hàng.\n");
            Put(vault, "BOD Bảo mật/Chiến lược Vinpearl.md", Fm("bod-vinpearl", "BOD") + "Đàm phán Vinpearl.\n");

            Guid sales = await UserAsync(f, "kd-a22", false, "20", "70", "80", "80-HDSD");
            Guid hr = await UserAsync(f, "ns-a22", false, "70", "70-HS", "80");
            Guid acc = await UserAsync(f, "kt-a22", false, "60", "60-CT", "20-HD", "70");
            Guid admin = await UserAsync(f, "it-a22", true);

            // Luật 1: vùng con có quyền riêng – Kinh doanh (thư mục 20) KHÔNG thấy 20 › Hợp đồng khách hàng.
            var tree = (await As(f, sales, s => s.GetTree())).ListData!;
            tree.Select(x => x.Code).Should().Contain(["20", "70", "80", "80-HDSD"]).And.NotContain(["20-HD", "60-CT", "70-HS", "BOD"]);
            tree.Single(x => x.Code == "20").ListNote.Select(n => n.Id).Should().Equal("gc-vinpearl");
            (await As(f, sales, s => s.GetNote(Contract))).ResponseCode.Should().Be(404, "mở link trực tiếp không có quyền");

            // Luật 2: tìm không lộ cả tiêu đề tài liệu không có quyền.
            var hits = (await As(f, sales, s => s.Search(new KnowledgeSearchRequest { Keyword = "vinpearl" }))).ListData!;
            hits.Select(h => h.Id).Should().Equal("gc-vinpearl");
            var accHits = (await As(f, acc, s => s.Search(new KnowledgeSearchRequest { Keyword = "vinpearl" }))).ListData!;
            accHits.Select(h => h.Id).Should().BeEquivalentTo(["hd-vinpearl", "ct-t10"], "Kế toán có 20 › Hợp đồng + 60 › Chứng từ, không có thư mục 20 / BOD");

            // Vùng cá nhân: Nhân sự xem được, Kế toán thì không; admin IT không tự thấy vùng cá nhân / BOD (thấy hợp đồng).
            (await As(f, hr, s => s.GetNote(Personal))).ResponseCode.Should().Be(200);
            (await As(f, acc, s => s.GetNote(Personal))).ResponseCode.Should().Be(404);
            (await As(f, admin, s => s.GetNote(Personal))).ResponseCode.Should().Be(404, "admin không tự xem hồ sơ cá nhân (PROPOSED §7.9)");
            (await As(f, admin, s => s.GetNote("BOD Bảo mật/Chiến lược Vinpearl.md"))).ResponseCode.Should().Be(404);
            (await As(f, admin, s => s.GetNote(Contract))).ResponseCode.Should().Be(200);
            (await As(f, admin, s => s.GetTree())).ListData!.Select(x => x.Code).Should().NotContain(["70-HS", "BOD"]);

            // Luật 3: agent = agent ∩ người hỏi; vùng cá nhân chỉ agent HR khi người hỏi có quyền; BOD chặn mặc định.
            (await Svc(f, s => s.ReadFor(new KnowledgeCaller(null, "HR", hr), "hs-nv001"))).ResponseCode.Should().Be(200);
            (await Svc(f, s => s.ReadFor(new KnowledgeCaller(null, "HR", acc), "hs-nv001"))).ResponseCode.Should().Be(404, "người hỏi không có quyền vùng cá nhân");
            (await Svc(f, s => s.ReadFor(new KnowledgeCaller(null, "VIVI", hr), "hs-nv001"))).ResponseCode.Should().Be(404, "chỉ agent HR đọc vùng cá nhân");
            (await Svc(f, s => s.ReadFor(new KnowledgeCaller(null, "HR", null), "hs-nv001"))).ResponseCode.Should().Be(404, "không người hỏi");
            (await Svc(f, s => s.SearchFor(new KnowledgeCaller(null, "VIVI", admin), new KnowledgeSearchRequest { Keyword = "vinpearl" }))).ListData!.Select(h => h.Id)
                .Should().NotContain("bod-vinpearl", "Knowledge:AgentCanReadBod = false");
            (await Svc(f, s => s.ReadFor(new KnowledgeCaller(null, "SALES", acc), "hd-vinpearl"))).ResponseCode.Should().Be(200, "SALES ∩ Kế toán có 20 › Hợp đồng");
            (await Svc(f, s => s.ReadFor(new KnowledgeCaller(null, "SALES", sales), "hd-vinpearl"))).ResponseCode.Should().Be(404, "người hỏi không có 20 › Hợp đồng");
            (await Svc(f, s => s.ReadFor(new KnowledgeCaller(null, "SALES", null), "hd-vinpearl"))).ResponseCode.Should().Be(404, "việc không người hỏi không đọc vùng hạn chế");
            (await Svc(f, s => s.ReadFor(new KnowledgeCaller(null, "WH", null), "hdsd-nhan-hang"))).ResponseCode.Should().Be(200, "HDSD – mọi agent nội bộ");
            (await Svc(f, s => s.ReadFor(new KnowledgeCaller(null, "SALES", null), "gc-vinpearl"))).ResponseCode.Should().Be(200, "phạm vi cũ của agent giữ nguyên");

            // Luật 5: đồng bộ Obsidian không gồm hợp đồng / chứng từ / hồ sơ cá nhân kể cả người được cấp.
            var man = (await As(f, acc, s => s.GetSyncManifest())).Data!;
            man.ListNote.Select(n => n.Folder).Should().NotContain(["20-HD", "60-CT", "70-HS"]).And.Contain("70");
            (await As(f, hr, s => s.GetSyncManifest())).Data!.ListNote.Should().NotContain(n => n.Path == Personal);
        }
    }
}
