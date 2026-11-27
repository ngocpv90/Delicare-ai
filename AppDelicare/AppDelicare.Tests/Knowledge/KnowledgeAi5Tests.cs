using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Services.Implement.Agent;
using AppDelicare.Services.Implement.Knowledge;
using AppDelicare.Tests.Agent;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Knowledge
{
    /// <summary>
    /// AI-5 (D-133) – Kho tri thức: mỗi luật §7.5–7.8 một test. Agent ghi ngoài 96–98 bị từ chối; không agent nào (kể cả CSKH) đọc BOD; thiếu frontmatter không lưu;
    /// ghi chú tự sinh không sửa qua API; hai người lưu đè → người sau 409; export chạy 2 lần = 0 file mới; tìm "thit heo" ra "thịt heo"; quét SĐT; ngưỡng từ cấu hình.
    /// </summary>
    public class KnowledgeAi5Tests
    {
        private static string NewVault() => Path.Combine(Path.GetTempPath(), "dc-knowledge-" + Guid.NewGuid().ToString("N"));

        private static string Fm(string id, string type = "huong-dan", string status = "hiệu lực", string conf = "nội bộ", string source = "human", string extra = "")
            => $"---\nid: {id}\ntype: {type}\nsource: {source}\nowner: QA\nstatus: {status}\nvalid_from: 2026-11-01\nreview_by: 2027-05-01\nconfidentiality: {conf}\nrefs: []\n{extra}---\n";

        private static void Put(string vault, string rel, string content)
        {
            string full = Path.Combine(vault, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content, new UTF8Encoding(false));
        }

        private static async Task<(int Code, JsonNode? Body)> Post(HttpClient c, string url, object body)
        {
            var res = await c.PostAsJsonAsync(url, body);
            var node = JsonNode.Parse(await res.Content.ReadAsStringAsync());
            return (node?["responseCode"]?.GetValue<int>() ?? (int)res.StatusCode, node);
        }

        private static async Task<JsonNode?> Get(HttpClient c, string url) => JsonNode.Parse(await (await c.GetAsync(url)).Content.ReadAsStringAsync());

        [Fact]
        public async Task Agent_writes_only_96_97_98()
        {
            string vault = NewVault();
            using var f = await AgentToolsAi3Tests.FactoryAsync(("Knowledge:VaultPath", vault));
            using (var scope = f.Services.CreateScope())
            {
                var sp = scope.ServiceProvider;
                using var actor = sp.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForAgent(Guid.NewGuid(), "QA"));
                var r = await sp.GetRequiredService<IKnowledgeService>().Save(new KnowledgeSaveRequest { Path = "45 ATTP/SOP mới.md", Content = Fm("x", "sop", "nháp") + "# x\n" });
                r.ResponseCode.Should().Be(403, "agent ghi vào 45 → từ chối");
                (await sp.GetRequiredService<IKnowledgeService>().AppendJournal("QA", new KnowledgeJournalRequest { Kind = "sop", Content = "abc" })).ResponseCode.Should().Be(403);
            }
            var ok = await AgentToolsAi3Tests.Call(f, "WH", "ghi_nhat_ky", new { loai = "nhat_ky", noi_dung = "Đã gửi cảnh báo lô cận hạn", ma_chung_tu = "LO-261124-NC00026-01" });
            ok.Status.Should().Be(200, ok.Message);
            string day = f.Clock.Today.ToString("yyyy-MM-dd");
            File.ReadAllText(Path.Combine(vault, "96 Nhật ký", "WH", day + ".md")).Should().Contain("source: agent:wh").And.Contain("Đã gửi cảnh báo lô cận hạn").And.Contain("LO-261124-NC00026-01");
            (await AgentToolsAi3Tests.Call(f, "WH", "ghi_nhat_ky", new { loai = "cong_viec", noi_dung = "Theo dõi NCC giao bù", han = "2026-12-01" })).Status.Should().Be(200);
            (await AgentToolsAi3Tests.Call(f, "WH", "ghi_nhat_ky", new { loai = "tuong_tac", noi_dung = "gọi 0912345678" })).Status.Should().Be(400, "quét SĐT");
            var audits = await f.QueryAsync(db => db.AuditChange.AsNoTracking().Where(a => a.EntityName == KnowledgeService.Entity).ToListAsync());
            audits.Should().HaveCount(2).And.OnlyContain(a => a.Changes!.Contains("AGENT:WH"));
        }

        [Fact]
        public async Task No_agent_ever_reads_bod_and_cskh_only_public()
        {
            string vault = NewVault();
            using var f = await AgentToolsAi3Tests.FactoryAsync(("Knowledge:VaultPath", vault));
            Put(vault, "BOD Bảo mật/Chiến lược 2027.md", Fm("bod-1", conf: "BOD") + "# Chiến lược giá vốn\nchiến lược đàm phán giá vốn\n");
            Put(vault, "45 ATTP/Ghi chú mật.md", Fm("mat-1", conf: "BOD") + "# Mật\nchiến lược đàm phán giá vốn\n");
            Put(vault, "45 ATTP/Nhận hàng lạnh.md", Fm("sop-lanh", conf: "công khai") + "# Nhận hàng lạnh\nkiểm nhiệt độ thùng xe, chiến lược nhận hàng\n");
            var vivi = await AgentToolsAi3Tests.Call(f, "VIVI", "tim_tri_thuc", new { cau_hoi = "chien luoc gia von" });
            vivi.Status.Should().Be(200);
            string json = ((JsonNode)vivi.Data!).ToJsonString();
            json.Should().NotContain("bod-1").And.NotContain("mat-1");
            (await AgentToolsAi3Tests.Call(f, "VIVI", "doc_ghi_chu", new { id = "BOD Bảo mật/Chiến lược 2027.md" })).Status.Should().Be(400);
            (await AgentToolsAi3Tests.Call(f, "QA", "doc_ghi_chu", new { id = "mat-1" })).Status.Should().Be(400);

            using var scope = f.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IKnowledgeService>();
            var cskh = await svc.SearchFor(new KnowledgeCaller(Guid.NewGuid(), "CSKH", null), new KnowledgeSearchRequest { Keyword = "chien luoc" });
            cskh.ListData.Should().BeEmpty("CSKH chỉ đọc 20 / 30 / 55 / 80 và chỉ ghi chú công khai");
            Put(vault, "80 Đào tạo/Nội bộ.md", Fm("noi-bo-80") + "# Hướng dẫn nội bộ\nchiến lược đào tạo\n");
            Put(vault, "80 Đào tạo/Công khai.md", Fm("cong-khai-80", conf: "công khai") + "# Hướng dẫn công khai\nchiến lược đào tạo\n");
            (await svc.SearchFor(new KnowledgeCaller(Guid.NewGuid(), "CSKH", null), new KnowledgeSearchRequest { Keyword = "chien luoc dao tao" })).ListData.Select(h => h.Id).Should().Equal("cong-khai-80");

            // admin web cũng KHÔNG thấy BOD nếu không được cấp rõ mục BOD
            using var client = f.CreateAuthorizedClient();
            var tree = await Get(client, "/api/Knowledge/GetTree");
            tree!.ToJsonString().Should().NotContain("BOD Bảo mật").And.Contain("45 ATTP");
        }

        [Fact]
        public async Task Save_requires_frontmatter_scans_pii_and_blocks_unapproved_sop()
        {
            string vault = NewVault();
            using var f = new InMemoryApiFactory();
            f.Settings["Knowledge:VaultPath"] = vault;
            using var c = f.CreateAuthorizedClient();
            (await Post(c, "/api/Knowledge/Save", new { path = "80 Đào tạo/a.md", content = "# Không có frontmatter\n" })).Code.Should().Be(400);
            var (code, body) = await Post(c, "/api/Knowledge/Save", new { path = "80 Đào tạo/a.md", content = "---\nid: a\ntype: huong-dan\nsource: human\nowner: x\nstatus: nháp\nvalid_from: 2026-11-01\nconfidentiality: nội bộ\nrefs: []\n---\n# a\n" });
            code.Should().Be(400);
            body!["responseMessage"]!.GetValue<string>().Should().Contain("review_by");
            (await Post(c, "/api/Knowledge/Save", new { path = "80 Đào tạo/b.md", content = Fm("b") + "# b\nLiên hệ 0912 345 678\n" })).Code.Should().Be(400, "quét SĐT");
            (await Post(c, "/api/Knowledge/Save", new { path = "45 ATTP/SOP.md", content = Fm("sop-x", "sop", "hiệu lực") + "# SOP\n" })).Code.Should().Be(400, "SOP hiệu lực phải qua Duyệt SOP");
            (await Post(c, "/api/Knowledge/Save", new { path = "45 ATTP/SOP.md", content = Fm("sop-x", "sop", "chờ duyệt") + "# SOP\n" })).Code.Should().Be(200);
            (await Post(c, "/api/Knowledge/Save", new { path = "../ngoai.md", content = Fm("n") })).Code.Should().Be(400);
            (await Post(c, "/api/Knowledge/Save", new { path = "96 Nhật ký/tay.md", content = Fm("t") })).Code.Should().Be(403, "96–98 chỉ agent ghi");
            var hist = await Get(c, "/api/Knowledge/GetHistory?path=" + Uri.EscapeDataString("45 ATTP/SOP.md"));
            hist!["listData"]!.AsArray().Should().ContainSingle();
        }

        [Fact]
        public async Task Generated_note_is_read_only_and_concurrent_save_conflicts()
        {
            string vault = NewVault();
            using var f = new InMemoryApiFactory();
            f.Settings["Knowledge:VaultPath"] = vault;
            using var c = f.CreateAuthorizedClient();
            using (var scope = f.Services.CreateScope())
            {
                using var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForSystem(InMemoryApiFactory.UserId));
                (await scope.ServiceProvider.GetRequiredService<IKnowledgeService>().ExportNote("10 Ban GĐ/Bản tin/2026/x.md", "bt-x", "bao-cao", "CEO", new DateTime(2026, 11, 27), 13, "nội bộ", [], "# Bản tin\n")).Should().Be(1);
            }
            var note = await Get(c, "/api/Knowledge/GetNote?path=" + Uri.EscapeDataString("10 Ban GĐ/Bản tin/2026/x.md"));
            note!["data"]!["readOnly"]!.GetValue<bool>().Should().BeTrue();
            note["data"]!["canEdit"]!.GetValue<bool>().Should().BeFalse();
            string h = note["data"]!["hash"]!.GetValue<string>();
            (await Post(c, "/api/Knowledge/Save", new { path = "10 Ban GĐ/Bản tin/2026/x.md", content = Fm("bt-x") + "# sửa\n", prevHash = h })).Code.Should().Be(403, "ghi chú tự sinh chỉ đọc");

            var (c1, b1) = await Post(c, "/api/Knowledge/Save", new { path = "50 Sản xuất/Mẻ trễ.md", content = Fm("me-tre") + "# Mẻ trễ\nbản 1\n" });
            c1.Should().Be(200);
            string h1 = b1!["data"]!["hash"]!.GetValue<string>();
            (await Post(c, "/api/Knowledge/Save", new { path = "50 Sản xuất/Mẻ trễ.md", content = Fm("me-tre") + "# Mẻ trễ\nbản A\n", prevHash = h1 })).Code.Should().Be(200);
            var (c3, b3) = await Post(c, "/api/Knowledge/Save", new { path = "50 Sản xuất/Mẻ trễ.md", content = Fm("me-tre") + "# Mẻ trễ\nbản B\n", prevHash = h1 });
            c3.Should().Be(409, "người sau lưu trên bản cũ");
            b3!["responseMessage"]!.GetValue<string>().Should().Contain("người khác sửa");
            File.ReadAllText(Path.Combine(vault, "50 Sản xuất", "Mẻ trễ.md")).Should().Contain("bản A");
        }

        [Fact]
        public async Task Export_twice_creates_no_new_files()
        {
            string vault = NewVault();
            using var f = new InMemoryApiFactory();
            f.Settings["Knowledge:VaultPath"] = vault;
            DateTime now = f.Clock.Now;
            await f.SeedAsync(db =>
            {
                Guid cust = Guid.NewGuid(), sup = Guid.NewGuid();
                db.Customer.Add(new Customer { Id = cust, Code = "KH-AI5", Name = "Công ty A", CIF = "KH-AI5", CustomerType = "B2B", DisplayName = "Công ty A", Status = "Active" });
                db.Supplier.Add(new Supplier { Id = sup, Code = "NCC-AI5", Name = "NCC rau" });
                db.CustomerComplaint.Add(new CustomerComplaint { Id = Guid.NewGuid(), ComplaintNo = "PA-AI5-01", CustomerId = cust, Description = "Canh nguội, gọi 0912345678", ComplaintStatus = "Resolved", ReceivedAt = now.AddHours(-5), ResolvedAt = now.AddHours(-1), Resolution = "Đổi suất" });
                db.SupplierIssue.Add(new SupplierIssue { Id = Guid.NewGuid(), SupplierId = sup, IssueType = "QUALITY", Description = "Rau dập", IssueStatus = "Closed", ReportedAt = now.AddDays(-1), ClosedAt = now.AddHours(-2), Resolution = "NCC đổi hàng" });
                return Task.CompletedTask;
            });
            using var scope = f.Services.CreateScope();
            using var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForSystem(InMemoryApiFactory.UserId));
            var ex = scope.ServiceProvider.GetRequiredService<IKnowledgeExportService>();
            int first = await ex.ExportAll(now);
            first.Should().BeGreaterThanOrEqualTo(3, "bản tin + phản ánh + sự cố NCC");
            int files = Directory.EnumerateFiles(vault, "*.md", SearchOption.AllDirectories).Count();
            (await ex.ExportAll(now)).Should().Be(0, "chạy lại không nhân bản");
            Directory.EnumerateFiles(vault, "*.md", SearchOption.AllDirectories).Count().Should().Be(files);
            string pa = Directory.EnumerateFiles(Path.Combine(vault, "20 Kinh doanh & Hợp đồng"), "PA-AI5-01.md", SearchOption.AllDirectories).Single();
            File.ReadAllText(pa).Should().Contain("source: delicare-core").And.NotContain("0912345678");
            Directory.EnumerateFiles(Path.Combine(vault, "45 ATTP"), "SC-*.md", SearchOption.AllDirectories).Should().ContainSingle();
            Directory.EnumerateFiles(Path.Combine(vault, "BOD Bảo mật"), "*", SearchOption.AllDirectories).Should().BeEmpty();
        }

        [Fact]
        public async Task Search_without_diacritics_ranking_rules_and_thresholds()
        {
            string vault = NewVault();
            using var f = await AgentToolsAi3Tests.FactoryAsync(("Knowledge:VaultPath", vault), ("BusinessRules:PurchaseOrderDirectorApprovalAmount", "50000000"));
            Put(vault, "40 Kho & Truy xuất/Nhật ký agent.md", Fm("agent-note", source: "agent:wh") + "# Ghi chú thịt heo của agent\nthịt heo nhập kho\n");
            Put(vault, "40 Kho & Truy xuất/SOP nhận thịt heo.md", Fm("sop-heo", "sop") + "# SOP nhận thịt heo\nKiểm nhiệt độ thịt heo ≤ 4°C. Duyệt khi vượt {{BusinessRules:PurchaseOrderDirectorApprovalAmount}} – {{ConnectionStrings:MainConnectionString}}\n");
            Put(vault, "40 Kho & Truy xuất/Nháp.md", Fm("nhap-heo", status: "nháp") + "# Nháp thịt heo\nthịt heo\n");
            Put(vault, "60 Kế toán/Thịt heo kế toán.md", Fm("kt-heo") + "# Thịt heo kế toán\nthịt heo\n");
            var r = await AgentToolsAi3Tests.Call(f, "WH", "tim_tri_thuc", new { cau_hoi = "thit heo" });
            r.Status.Should().Be(200, r.Message);
            var ids = ((JsonNode)r.Data!)["ghi_chu"]!.AsArray().Select(x => x!["id"]!.GetValue<string>()).ToList();
            ids.Should().Equal(["sop-heo", "agent-note"], "SOP hiệu lực trước ghi chú agent; bỏ nháp; WH không đọc 60 Kế toán");
            var doc = await AgentToolsAi3Tests.Call(f, "WH", "doc_ghi_chu", new { id = "sop-heo" });
            string body = ((JsonNode)doc.Data!)["noi_dung"]!.GetValue<string>();
            body.Should().Contain("50000000").And.Contain("{{ConnectionStrings:MainConnectionString}}", "chỉ thay nhánh BusinessRules");
            (await AgentToolsAi3Tests.Call(f, "WH", "doc_ghi_chu", new { id = "kt-heo" })).Status.Should().Be(400, "ngoài phạm vi đọc của WH");
            var hist = await AgentToolsAi3Tests.Call(f, "WH", "tim_tri_thuc", new { cau_hoi = "thit heo", lich_su = true });
            ((JsonNode)hist.Data!)["ghi_chu"]!.AsArray().Select(x => x!["id"]!.GetValue<string>()).Should().Contain("nhap-heo");
            KnowledgeVault.Fold("Thịt heo Đà Nẵng").Should().Be("thit heo da nang");
        }
    }
}
