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
    /// AI-18 (D-133) – đồng bộ Obsidian máy cá nhân: danh sách theo quyền thư mục, KHÔNG BAO GIỜ BOD (kể cả người được cấp BOD), có băm khớp tệp;
    /// hai chiều chỉ thư mục 80 khi bật Knowledge:SyncTwoWayTraining VÀ người dùng có quyền Sửa 80; mỗi lần lấy ghi AuditChange KnowledgeSync.
    /// </summary>
    public class KnowledgeSyncAi18Tests
    {
        private static string Fm(string id, string conf = "nội bộ")
            => $"---\nid: {id}\ntype: huong-dan\nsource: human\nowner: HR\nstatus: hiệu lực\nvalid_from: 2026-11-01\nreview_by: 2027-11-01\nconfidentiality: {conf}\nrefs: []\n---\n# {id}\n";

        private static void Put(string vault, string rel, string content)
        {
            string full = Path.Combine(vault, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content, new UTF8Encoding(false));
        }

        private static async Task<Data.Interface.IResult<KnowledgeSyncManifestModel>> Manifest(InMemoryApiFactory f, Guid user)
        {
            using var scope = f.Services.CreateScope();
            using var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForUser(user));
            return await scope.ServiceProvider.GetRequiredService<IKnowledgeService>().GetSyncManifest();
        }

        private static async Task<Guid> UserWithFoldersAsync(InMemoryApiFactory f, string name, params (string Folder, bool Edit)[] folders)
        {
            Guid u = Guid.NewGuid(), g = Guid.NewGuid();
            await f.SeedAsync(async db =>
            {
                db.User.Add(new User { Id = u, UserName = name, IsActive = true, AccountType = "INTERNAL" });
                db.UserGroup.Add(new UserGroup { Id = g, Name = name, Code = name.ToUpperInvariant() });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = u, UserGroupId = g });
                foreach (var (folder, edit) in folders)
                {
                    string link = "#/knowledge?folder=" + folder.ToLowerInvariant();
                    var m = await db.ObjectMenu.FirstOrDefaultAsync(x => x.MetaLink == link) ?? db.ObjectMenu.Local.FirstOrDefault(x => x.MetaLink == link);
                    if (m == null)
                    {
                        m = new ObjectMenu { Id = Guid.NewGuid(), Name = folder, MetaLink = link, LevelShow = 2, IsActive = true };
                        db.ObjectMenu.Add(m);
                    }
                    db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = g, ObjectMenuId = m.Id, AllowAccess = true, AllowEdit = edit, AllowAdd = edit });
                }
            });
            return u;
        }

        [Fact]
        public async Task Manifest_follows_folder_rights_never_includes_bod_and_two_way_only_for_training_when_enabled()
        {
            string vault = Path.Combine(Path.GetTempPath(), "dc-knowledge-" + Guid.NewGuid().ToString("N"));
            using var f = await AgentToolsAi3Tests.FactoryAsync(("Knowledge:VaultPath", vault), ("Knowledge:SyncTwoWayTraining", "true"));
            f.CreateAuthorizedClient().Dispose();
            Put(vault, "80 Đào tạo/Hướng dẫn rửa khay.md", Fm("hd-rua-khay"));
            Put(vault, "45 ATTP/SOP lõi.md", Fm("sop-loi"));
            Put(vault, "BOD/Chiến lược.md", Fm("chien-luoc", "BOD"));
            Put(vault, "10 Ban GĐ/Mật.md", Fm("mat", "BOD"));
            Guid ceo = await UserWithFoldersAsync(f, "ceo-a18", ("BOD", true), ("10", true), ("45", false), ("80", false));
            Guid trainer = await UserWithFoldersAsync(f, "dt-a18", ("80", true));

            var m1 = (await Manifest(f, ceo)).Data!;
            m1.ListNote.Select(n => n.Path).Should().BeEquivalentTo(["45 ATTP/SOP lõi.md", "80 Đào tạo/Hướng dẫn rửa khay.md"], "BOD / ghi chú BOD không bao giờ rời máy chủ, kể cả người được cấp");
            m1.TwoWayFolders.Should().BeEmpty("CEO chỉ Xem thư mục 80");
            string text = File.ReadAllText(Path.Combine(vault, "45 ATTP/SOP lõi.md"), Encoding.UTF8);
            m1.ListNote.Single(n => n.Folder == "45").Hash.Should().Be(KnowledgeVault.Sha256(text), "băm khớp nội dung tệp – script so được bản trên máy");

            var m2 = (await Manifest(f, trainer)).Data!;
            m2.ListNote.Select(n => n.Folder).Should().Equal("80");
            m2.TwoWayFolders.Should().Equal("80");
            (await f.QueryAsync(db => db.AuditChange.CountAsync(a => a.EntityName == KnowledgeService.SyncEntity))).Should().Be(2, "mỗi lần lấy danh sách ghi lịch sử");

            using var off = await AgentToolsAi3Tests.FactoryAsync(("Knowledge:VaultPath", vault));
            off.CreateAuthorizedClient().Dispose();
            Guid trainer2 = await UserWithFoldersAsync(off, "dt2-a18", ("80", true));
            (await Manifest(off, trainer2)).Data!.TwoWayFolders.Should().BeEmpty("mặc định tắt – mọi thư mục một chiều");
        }
    }
}
