using System.Text;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Services.Implement.Knowledge;
using AppDelicare.Tests.Agent;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Knowledge
{
    /// <summary>
    /// AI-17 (D-133) – tab "Tri thức" trên trang chi tiết: Knowledge/GetByEntity lấy mã của bản ghi (lô: số lô; khách: mã + CIF…), trả ghi chú có mã trong refs (gắn mã, xếp trước)
    /// hoặc nhắc trong nội dung (nhắc tới), theo quyền thư mục của người xem; BOD không hiện cho người không được cấp rõ; loại sai → 400; bản ghi không tồn tại → rỗng.
    /// </summary>
    public class KnowledgeEntityAi17Tests
    {
        private static string Fm(string id, string refs, string status = "hiệu lực", string conf = "nội bộ")
            => $"---\nid: {id}\ntype: bai-hoc\nsource: human\nowner: QA\nstatus: {status}\nvalid_from: 2026-11-01\nreview_by: 2027-11-01\nconfidentiality: {conf}\nrefs: [{refs}]\n---\n";

        private static void Put(string vault, string rel, string content)
        {
            string full = Path.Combine(vault, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content, new UTF8Encoding(false));
        }

        private static async Task<List<KnowledgeEntityNoteModel>> AsUser(InMemoryApiFactory f, Guid user, string type, Guid id, int expect = 200)
        {
            using var scope = f.Services.CreateScope();
            using var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForUser(user));
            var r = await scope.ServiceProvider.GetRequiredService<IKnowledgeService>().GetByEntity(type, id);
            r.ResponseCode.Should().Be(expect, r.ResponseMessage);
            return r.ListData ?? [];
        }

        [Fact]
        public async Task Entity_tab_lists_ref_notes_first_then_mentions_within_reader_folder_rights()
        {
            string vault = Path.Combine(Path.GetTempPath(), "dc-knowledge-" + Guid.NewGuid().ToString("N"));
            using var f = await AgentToolsAi3Tests.FactoryAsync(("Knowledge:VaultPath", vault));
            f.CreateAuthorizedClient().Dispose();
            Guid lot = Guid.NewGuid(), customer = Guid.NewGuid(), qaUser = Guid.NewGuid(), g = Guid.NewGuid(), menu45 = Guid.NewGuid();
            await f.SeedAsync(db =>
            {
                db.MaterialLot.Add(new MaterialLot { Id = lot, IngredientId = Guid.NewGuid(), LotNumber = "LO-261128-A17", LotNo = "LO-261128-A17", Status = "Available" });
                db.Customer.Add(new Customer { Id = customer, Code = "KH-A17", CIF = "CIF-A17", Name = "Khách A17", DisplayName = "Khách A17", CustomerType = "FACTORY", Status = "Active" });
                db.User.Add(new User { Id = qaUser, UserName = "qa-a17", IsActive = true, AccountType = "INTERNAL" });
                db.UserGroup.Add(new UserGroup { Id = g, Name = "QA-A17", Code = "QA-A17" });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = qaUser, UserGroupId = g });
                db.ObjectMenu.Add(new ObjectMenu { Id = menu45, Name = "45 ATTP", MetaLink = "#/knowledge?folder=45", LevelShow = 2, IsActive = true });
                db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = g, ObjectMenuId = menu45, AllowAccess = true });
                return Task.CompletedTask;
            });
            Put(vault, "45 ATTP/Bài học lô thịt nhiễm.md", Fm("bh-lo-a17", "LO-261128-A17") + "# Bài học lô thịt nhiễm\n\nLô bị giữ do nhiệt độ xe 9°C.\n");
            Put(vault, "45 ATTP/Sự cố tháng 11.md", Fm("su-co-11", "") + "# Sự cố tháng 11\n\nNgày 28 giữ lô LO-261128-A17 và 2 lô khác của NCC.\n");
            Put(vault, "40 Kho & Truy xuất/Ghi chú kho A17.md", Fm("kho-a17", "LO-261128-A17") + "# Ghi chú kho A17\n\nLô xếp kệ lạnh B.\n");
            Put(vault, "BOD/Đánh giá NCC A17.md", Fm("bod-a17", "LO-261128-A17", conf: "BOD") + "# Đánh giá NCC A17\n\nTạm dừng NCC.\n");
            Put(vault, "45 ATTP/Không liên quan.md", Fm("khac", "LO-999") + "# Không liên quan\n\nLô khác.\n");
            Put(vault, "20 Kinh doanh & Hợp đồng/Khách A17.md", Fm("kh-a17", "CIF-A17") + "# Khách A17 – lưu ý giao\n\nGiao cổng sau.\n");

            // Admin: thấy 45 + 40 (không BOD – BOD cần cấp rõ); gắn mã trước, nhắc tới sau, có đoạn trích.
            var all = await AsUser(f, InMemoryApiFactory.UserId, "lot", lot);
            all.Select(x => (x.Note.Id, x.ByRef)).Should().BeEquivalentTo([("bh-lo-a17", true), ("kho-a17", true), ("su-co-11", false)]);
            all.Last().ByRef.Should().BeFalse("gắn mã xếp trước");
            all.Single(x => x.Note.Id == "su-co-11").Snippet.Should().Contain("LO-261128-A17");

            // Người chỉ có quyền thư mục 45: không thấy ghi chú 40.
            (await AsUser(f, qaUser, "lot", lot)).Select(x => x.Note.Id).Should().BeEquivalentTo(["bh-lo-a17", "su-co-11"]);

            // Khách: khớp theo CIF trong refs; loại sai → 400; Id không tồn tại → rỗng.
            (await AsUser(f, InMemoryApiFactory.UserId, "customer", customer)).Select(x => x.Note.Id).Should().Equal("kh-a17");
            await AsUser(f, InMemoryApiFactory.UserId, "employee", customer, 400);
            (await AsUser(f, InMemoryApiFactory.UserId, "lot", Guid.NewGuid())).Should().BeEmpty();
        }
    }
}
