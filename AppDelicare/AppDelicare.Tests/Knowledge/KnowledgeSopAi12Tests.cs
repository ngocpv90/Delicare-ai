using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using AppDelicare.Data.Share;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Implement.Agent;
using AppDelicare.Services.Implement.Knowledge;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Agent;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Knowledge
{
    /// <summary>
    /// AI-12 (D-133) – Kho tri thức: người viết + duyệt SOP. SOP Nháp → Gửi duyệt (KB_SOP) → Duyệt → Hiệu lực; phiên bản mới → duyệt → bản cũ Lưu trữ (không xóa);
    /// nhập Word .docx → markdown Nháp; agent propose_note_update không ghi trước khi duyệt; việc agent theo dõi (98) xong / giao lại; bảo trì gộp nhật ký 90 ngày, lưu trữ bản tin 13 tháng.
    /// </summary>
    public class KnowledgeSopAi12Tests
    {
        private static string NewVault() => Path.Combine(Path.GetTempPath(), "dc-knowledge-" + Guid.NewGuid().ToString("N"));

        private static string Fm(string id, string type = "sop", string status = "nháp", string validFrom = "2026-11-01")
            => $"---\nid: {id}\ntype: {type}\nsource: human\nowner: QA\nstatus: {status}\nvalid_from: {validFrom}\nreview_by: 2027-11-01\nconfidentiality: nội bộ\nrefs: []\n---\n";

        private static void Put(string vault, string rel, string content)
        {
            string full = Path.Combine(vault, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content, new UTF8Encoding(false));
        }

        private static string StatusOf(string vault, string rel) => KnowledgeVault.Parse(rel, File.ReadAllText(Path.Combine(vault, rel))).Status;

        private static async Task<T> AsAdmin<T>(InMemoryApiFactory f, Func<IServiceProvider, Task<T>> run)
        {
            using var scope = f.Services.CreateScope();
            using var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForUser(InMemoryApiFactory.UserId));
            return await run(scope.ServiceProvider);
        }

        private static async Task ApproveAsync(InMemoryApiFactory f, string path)
        {
            Guid key = KnowledgeVault.KeyGuid(path);
            var req = await f.QueryAsync(db => db.ApprovalRequest.AsNoTracking().SingleAsync(a => a.BusinessId == key && a.BusinessType == KnowledgeService.SopBusinessType && a.RequestStatus == "Pending"));
            var r = await AsAdmin(f, sp => sp.GetRequiredService<IApprovalRequestService>().Decide(new ApprovalDecideRequest { Id = req.Id, Approve = true, Note = "Đồng ý" }));
            r.ResponseCode.Should().Be(200, r.ResponseMessage);
        }

        [Fact]
        public async Task Sop_goes_draft_pending_effective_and_new_version_archives_old_one()
        {
            string vault = NewVault();
            using var f = await AgentToolsAi3Tests.FactoryAsync(("Knowledge:VaultPath", vault));
            f.CreateAuthorizedClient().Dispose();
            const string p1 = "45 ATTP/SOP nhiệt độ lõi.md";
            Put(vault, p1, Fm("sop-nhiet-do") + "# SOP nhiệt độ lõi\n\nĐo lõi ≥ 70°C.\n");

            var sub = await AsAdmin(f, sp => sp.GetRequiredService<IKnowledgeService>().SubmitSop(new KnowledgePathRequest { Path = p1 }));
            sub.ResponseCode.Should().Be(200, sub.ResponseMessage);
            StatusOf(vault, p1).Should().Be("cho-duyet");
            (await AsAdmin(f, sp => sp.GetRequiredService<IKnowledgeService>().SubmitSop(new KnowledgePathRequest { Path = p1 }))).ResponseCode.Should().NotBe(200, "chỉ gửi duyệt bản Nháp");
            await ApproveAsync(f, p1);
            StatusOf(vault, p1).Should().Be("hieu-luc");

            var ver = await AsAdmin(f, sp => sp.GetRequiredService<IKnowledgeService>().NewVersion(new KnowledgePathRequest { Path = p1 }));
            ver.ResponseCode.Should().Be(200, ver.ResponseMessage);
            string p2 = ver.Data!.Path;
            p2.Should().Be("45 ATTP/SOP nhiệt độ lõi (bản 2).md");
            StatusOf(vault, p2).Should().Be("nhap");
            StatusOf(vault, p1).Should().Be("hieu-luc", "bản cũ còn dùng tới khi bản mới được duyệt");
            (await AsAdmin(f, sp => sp.GetRequiredService<IKnowledgeService>().SubmitSop(new KnowledgePathRequest { Path = p2 }))).ResponseCode.Should().Be(200);
            await ApproveAsync(f, p2);
            StatusOf(vault, p2).Should().Be("hieu-luc");
            StatusOf(vault, p1).Should().Be("luu-tru", "bản Hiệu lực cũ cùng id → Lưu trữ, không xóa");
            File.Exists(Path.Combine(vault, p1)).Should().BeTrue();
            var read = await AgentToolsAi3Tests.Call(f, "QA", "doc_ghi_chu", new { id = "sop-nhiet-do" });
            read.Status.Should().Be(200, read.Message);
            ((JsonNode)read.Data!)["duong_dan"]!.GetValue<string>().Should().Be(p2, "đọc theo id → bản Hiệu lực");
        }

        [Fact]
        public async Task Word_import_becomes_markdown_draft()
        {
            string vault = NewVault();
            using var f = await AgentToolsAi3Tests.FactoryAsync(("Knowledge:VaultPath", vault));
            f.CreateAuthorizedClient().Dispose();
            using var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                const string w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                using var s = new StreamWriter(zip.CreateEntry("word/document.xml").Open());
                s.Write($"<w:document xmlns:w=\"{w}\"><w:body>"
                    + "<w:p><w:pPr><w:pStyle w:val=\"Heading1\"/></w:pPr><w:r><w:t>Nhận hàng lạnh</w:t></w:r></w:p>"
                    + "<w:p><w:r><w:rPr><w:b/></w:rPr><w:t>Bắt buộc</w:t></w:r><w:r><w:t xml:space=\"preserve\"> đo nhiệt độ thùng xe.</w:t></w:r></w:p>"
                    + "<w:p><w:pPr><w:numPr><w:ilvl w:val=\"0\"/></w:numPr></w:pPr><w:r><w:t>Thịt ≤ 4°C</w:t></w:r></w:p>"
                    + "<w:tbl><w:tr><w:tc><w:p><w:r><w:t>Hàng</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>Ngưỡng</w:t></w:r></w:p></w:tc></w:tr><w:tr><w:tc><w:p><w:r><w:t>Cá</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>≤ 2°C</w:t></w:r></w:p></w:tc></w:tr></w:tbl>"
                    + "</w:body></w:document>");
            }
            ms.Position = 0;
            var r = await AsAdmin(f, sp => sp.GetRequiredService<IKnowledgeService>().Import("45", "sop", "Nhận hàng lạnh.docx", ms));
            r.ResponseCode.Should().Be(200, r.ResponseMessage);
            string text = File.ReadAllText(Path.Combine(vault, r.Data!.Path));
            text.Should().Contain("status: nháp").And.Contain("## Nhận hàng lạnh").And.Contain("**Bắt buộc** đo nhiệt độ").And.Contain("- Thịt ≤ 4°C").And.Contain("| Cá | ≤ 2°C |");
            var pdf = await AsAdmin(f, sp => sp.GetRequiredService<IKnowledgeService>().Import("45", "sop", "a.pdf", new MemoryStream([1, 2, 3])));
            pdf.ResponseMessage.Should().Contain("GAP-KB-PDF");
        }

        [Fact]
        public async Task Agent_note_update_writes_nothing_until_folder_owner_approves()
        {
            string vault = NewVault();
            using var f = await AgentToolsAi3Tests.FactoryAsync(("Knowledge:VaultPath", vault));
            f.CreateAuthorizedClient().Dispose();
            const string p = "45 ATTP/Hướng dẫn rửa tay.md";
            Put(vault, p, Fm("hd-rua-tay", "huong-dan", "hiệu lực") + "# Hướng dẫn rửa tay\n\nRửa 20 giây.\n");
            string before = File.ReadAllText(Path.Combine(vault, p));
            var r = await AgentToolsAi3Tests.Call(f, "QA", "propose_note_update", new { duong_dan = p, noi_dung = Fm("hd-rua-tay", "huong-dan", "hiệu lực").Replace("source: human", "source: agent:qa") + "# Hướng dẫn rửa tay\n\nRửa 30 giây, lau khô bằng khăn giấy.\n", ly_do = "Sự cố SC-12: rửa tay chưa đủ thời gian" });
            r.Status.Should().Be(200, r.Message);
            File.ReadAllText(Path.Combine(vault, p)).Should().Be(before, "đề xuất không ghi");
            Guid id = Guid.Parse(((JsonNode)r.Data!)["approvalRequestId"]!.GetValue<string>());
            var ok = await AsAdmin(f, sp => sp.GetRequiredService<IApprovalRequestService>().Decide(new ApprovalDecideRequest { Id = id, Approve = true, Note = "OK" }));
            ok.ResponseCode.Should().Be(200, ok.ResponseMessage);
            string after = File.ReadAllText(Path.Combine(vault, p));
            after.Should().Contain("Rửa 30 giây").And.Contain("source: human");
            (await AgentToolsAi3Tests.Call(f, "QA", "propose_note_update", new { duong_dan = "96 Nhật ký/QA/x.md", noi_dung = "x", ly_do = "x" })).Status.Should().Be(400);
        }

        [Fact]
        public async Task Agent_tasks_list_done_and_reassign()
        {
            string vault = NewVault();
            using var f = await AgentToolsAi3Tests.FactoryAsync(("Knowledge:VaultPath", vault));
            f.CreateAuthorizedClient().Dispose();
            (await AgentToolsAi3Tests.Call(f, "WH", "ghi_nhat_ky", new { loai = "cong_viec", tieu_de = "Theo dõi NCC giao bù thịt", noi_dung = "NCC Sơn Lâm hẹn giao bù 20 kg", han = "2026-12-01", nguoi_phu_trach = "Thủ kho ca sáng" })).Status.Should().Be(200);
            var list = await AsAdmin(f, sp => sp.GetRequiredService<IKnowledgeService>().GetTasks(false));
            var t = list.ListData.Should().ContainSingle().Subject;
            t.Agent.Should().Be("WH");
            t.Status.Should().Be("dang-theo-doi");
            (await AsAdmin(f, sp => sp.GetRequiredService<IKnowledgeService>().SetTask(new KnowledgeTaskSetRequest { Path = t.Path, Owner = "Trưởng kho" }))).ResponseCode.Should().Be(200);
            (await AsAdmin(f, sp => sp.GetRequiredService<IKnowledgeService>().SetTask(new KnowledgeTaskSetRequest { Path = t.Path, Status = "xong" }))).ResponseCode.Should().Be(200);
            (await AsAdmin(f, sp => sp.GetRequiredService<IKnowledgeService>().GetTasks(false))).ListData.Should().BeEmpty();
            var all = (await AsAdmin(f, sp => sp.GetRequiredService<IKnowledgeService>().GetTasks(true))).ListData.Single();
            (all.Status, all.Owner).Should().Be(("xong", "Trưởng kho"));
        }

        [Fact]
        public async Task Maintenance_merges_old_journals_and_archives_old_briefs()
        {
            string vault = NewVault();
            using var f = await AgentToolsAi3Tests.FactoryAsync(("Knowledge:VaultPath", vault));
            f.CreateAuthorizedClient().Dispose();
            DateTime today = f.Clock.Today;
            DateTime old = today.AddDays(-120), recent = today.AddDays(-5);
            string Journal(DateTime d, string line) => $"---\nid: nk-wh-{d:yyyyMMdd}\ntype: nhat-ky\nsource: agent:wh\nowner: KNOW\nstatus: hiệu lực\nvalid_from: {d:yyyy-MM-dd}\nreview_by: {d.AddDays(90):yyyy-MM-dd}\nconfidentiality: nội bộ\nrefs: []\n---\n\n# Nhật ký WH\n\n- 08:00 · {line}\n";
            Put(vault, $"96 Nhật ký/WH/{old:yyyy-MM-dd}.md", Journal(old, "Đã cảnh báo lô cận hạn"));
            Put(vault, $"96 Nhật ký/WH/{old.AddDays(1):yyyy-MM-dd}.md", Journal(old.AddDays(1), "Đã báo tồn dưới tối thiểu"));
            Put(vault, $"96 Nhật ký/WH/{recent:yyyy-MM-dd}.md", Journal(recent, "Gần đây"));
            DateTime briefDay = today.AddMonths(-14);
            Put(vault, $"10 Ban GĐ/Bản tin/{briefDay:yyyy}/{briefDay:yyyy-MM-dd} sáng.md", Fm("ban-tin-x", "bao-cao", "hiệu lực", briefDay.ToString("yyyy-MM-dd")) + "# Bản tin\n");
            Put(vault, "45 ATTP/SOP cũ.md", Fm("sop-cu", "sop", "hiệu lực").Replace("review_by: 2027-11-01", $"review_by: {today.AddDays(-3):yyyy-MM-dd}") + "# SOP cũ\n");

            var r = await AsAdmin(f, sp => sp.GetRequiredService<IKnowledgeService>().MaintainAsync());
            r.MergedLogs.Should().Be(2);
            r.ArchivedBriefs.Should().Be(1);
            r.OverdueByFolder.Should().ContainKey("45");
            File.Exists(Path.Combine(vault, $"96 Nhật ký/WH/{old:yyyy-MM-dd}.md")).Should().BeFalse();
            File.Exists(Path.Combine(vault, $"96 Nhật ký/WH/{recent:yyyy-MM-dd}.md")).Should().BeTrue("chưa quá 90 ngày");
            string month = File.ReadAllText(Path.Combine(vault, $"96 Nhật ký/WH/Tóm tắt {old:yyyy-MM}.md"));
            month.Should().Contain("Đã cảnh báo lô cận hạn");
            File.Exists(Path.Combine(vault, $"10 Ban GĐ/Lưu trữ bản tin/{briefDay:yyyy}/{briefDay:yyyy-MM-dd} sáng.md")).Should().BeTrue("chuyển lưu trữ, không xóa");
            (await AsAdmin(f, sp => sp.GetRequiredService<IKnowledgeService>().MaintainAsync())).MergedLogs.Should().Be(0, "chạy lại không gộp thêm");
        }
    }
}
