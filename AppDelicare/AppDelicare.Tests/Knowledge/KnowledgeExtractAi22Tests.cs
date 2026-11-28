using System.Text;
using System.Text.Json.Nodes;
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
    /// AI-22.7 (D-141 k, §7.10): trích chữ lúc tải lên (PDF có lớp chữ – PdfPig) → tìm nội dung tài liệu gốc kèm trang / số hiệu; doc_tai_lieu đọc theo trang;
    /// quyền agent ∩ người hỏi (không quyền mục → không trích được; không người hỏi → không đọc vùng hạn chế); ACC ở chứng từ gốc chỉ phiếu mô tả;
    /// scan / ảnh → chờ KNOW đọc (ghi_ban_chu) – vùng cá nhân không gửi AI, bắt buộc tóm tắt; KNOW ghi tóm tắt, agent khác không ghi được.
    /// </summary>
    public class KnowledgeExtractAi22Tests
    {
        /// <summary>PDF tối thiểu nhiều trang có lớp chữ (font Helvetica, chữ ASCII) – xref đúng vị trí.</summary>
        internal static byte[] TextPdf(params string[] pages)
        {
            var objs = new List<string> { "<< /Type /Catalog /Pages 2 0 R >>", "" , "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>" };
            var kids = new List<int>();
            foreach (string p in pages)
            {
                string stream = $"BT /F1 12 Tf 72 720 Td ({p.Replace("(", "\\(").Replace(")", "\\)")}) Tj ET";
                objs.Add($"<< /Length {stream.Length} >>\nstream\n{stream}\nendstream");
                int contentNo = objs.Count;
                objs.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 3 0 R >> >> /Contents {contentNo} 0 R >>");
                kids.Add(objs.Count);
            }
            objs[1] = $"<< /Type /Pages /Kids [{string.Join(" ", kids.Select(k => k + " 0 R"))}] /Count {kids.Count} >>";
            var sb = new StringBuilder("%PDF-1.4\n");
            var offsets = new List<int>();
            for (int i = 0; i < objs.Count; i++)
            {
                offsets.Add(Encoding.ASCII.GetByteCount(sb.ToString()));
                sb.Append($"{i + 1} 0 obj\n{objs[i]}\nendobj\n");
            }
            int xref = Encoding.ASCII.GetByteCount(sb.ToString());
            sb.Append($"xref\n0 {objs.Count + 1}\n0000000000 65535 f \n");
            foreach (int o in offsets)
            {
                sb.Append($"{o:D10} 00000 n \n");
            }
            sb.Append($"trailer\n<< /Size {objs.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
            return Encoding.ASCII.GetBytes(sb.ToString());
        }

        private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

        private static async Task<Guid> UserAsync(InMemoryApiFactory f, string name, params string[] editFolders)
        {
            Guid u = Guid.NewGuid(), g = Guid.NewGuid();
            await f.SeedAsync(async db =>
            {
                db.User.Add(new User { Id = u, UserName = name, IsActive = true, AccountType = "INTERNAL" });
                db.UserGroup.Add(new UserGroup { Id = g, Name = name, Code = name.ToUpperInvariant() });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = u, UserGroupId = g });
                foreach (string folder in editFolders)
                {
                    string link = "#/knowledge?folder=" + folder.ToLowerInvariant();
                    var m = await db.ObjectMenu.FirstOrDefaultAsync(x => x.MetaLink == link) ?? db.ObjectMenu.Local.FirstOrDefault(x => x.MetaLink == link);
                    if (m == null)
                    {
                        m = new ObjectMenu { Id = Guid.NewGuid(), Name = folder, MetaLink = link, LevelShow = 2, IsActive = true };
                        db.ObjectMenu.Add(m);
                    }
                    db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = g, ObjectMenuId = m.Id, AllowAccess = true, AllowEdit = true, AllowAdd = true, AllowExport = true });
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

        // Agent gọi qua ToolGateway luôn trong phạm vi người thao tác = agent (ghi AuditChange).
        private static async Task<T> Svc<T>(InMemoryApiFactory f, Func<IKnowledgeService, Task<T>> run)
        {
            using var scope = f.Services.CreateScope();
            using var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForAgent(Guid.Parse("a3a3a3a3-0000-4000-8000-000000000022"), "KNOW"));
            return await run(scope.ServiceProvider.GetRequiredService<IKnowledgeService>());
        }

        private static Task<Data.Interface.IResult<KnowledgeSaveResultModel>> Up(IKnowledgeService s, string folder, string type, string title, string name, byte[] bytes, string? summary = null)
            => s.UploadFile(new KnowledgeUploadRequest { Folder = folder, Type = type, Title = title, SoHieu = "HD-2026-07", Ngay = "2026-01-02", Summary = summary }, name, new MemoryStream(bytes), bytes.Length);

        [Fact]
        public async Task Agents_read_document_content_with_source_within_asker_rights_and_scans_follow_ai_rules()
        {
            string vault = Path.Combine(Path.GetTempPath(), "dc-knowledge-" + Guid.NewGuid().ToString("N"));
            using var f = await AgentToolsAi3Tests.FactoryAsync(("Knowledge:VaultPath", vault));
            f.CreateAuthorizedClient().Dispose();
            Guid salesHead = await UserAsync(f, "kdt-a227", "20-HD");
            Guid sales = await UserAsync(f, "kd-a227", "20");
            Guid acc = await UserAsync(f, "kt-a227", "60-CT");
            Guid hr = await UserAsync(f, "ns-a227", "70-HS");

            // PDF có lớp chữ: trang 2 có điều khoản phạt.
            byte[] pdf = TextPdf("HOP DONG CUNG CAP SUAT AN - Vin Ocean", "Dieu 7: Phat giao tre 5 phan tram gia tri suat an cua ngay giao tre");
            var up = await As(f, salesHead, s => Up(s, "20-HD", "hop-dong-kh", "Hợp đồng suất ăn Vin Ocean 2026", "hd.pdf", pdf));
            up.ResponseCode.Should().Be(200, up.ResponseMessage);
            File.Exists(Path.Combine(vault, up.Data!.Path[..^3] + ".txt")).Should().BeTrue("bản chữ cạnh tệp gốc");
            File.ReadAllText(Path.Combine(vault, up.Data.Path)).Should().Contain("ban_chu: co");

            // Câu mẫu §7.10: "Hợp đồng khách A phạt giao trễ thế nào?" – SALES ∩ người hỏi có quyền 20 › Hợp đồng → đoạn trích + trang + số hiệu.
            var hits = (await Svc(f, s => s.SearchFor(new KnowledgeCaller(null, "SALES", salesHead), new KnowledgeSearchRequest { Keyword = "phat giao tre" }))).ListData!;
            var hit = hits.Should().ContainSingle().Subject;
            (hit.Page, hit.SoHieu, hit.DocDate).Should().Be((2, "HD-2026-07", "2026-01-02"));
            hit.Snippet.Should().Contain("Phat giao tre 5 phan tram");
            var page2 = (await Svc(f, s => s.ReadDocFor(new KnowledgeCaller(null, "SALES", salesHead), hit.Id, 2))).Data!;
            (page2.Pages, page2.Content).Should().Be((2, "Dieu 7: Phat giao tre 5 phan tram gia tri suat an cua ngay giao tre"));
            (await Svc(f, s => s.ReadDocFor(new KnowledgeCaller(null, "SALES", salesHead), hit.Id, 9))).ResponseCode.Should().Be(404);

            // Người hỏi không có quyền mục / không người hỏi → agent không trích được.
            (await Svc(f, s => s.SearchFor(new KnowledgeCaller(null, "SALES", sales), new KnowledgeSearchRequest { Keyword = "phat giao tre" }))).ListData!.Should().BeEmpty();
            (await Svc(f, s => s.SearchFor(new KnowledgeCaller(null, "SALES", null), new KnowledgeSearchRequest { Keyword = "phat giao tre" }))).ListData!.Should().BeEmpty("việc theo lịch không đọc vùng hạn chế");
            (await Svc(f, s => s.ReadDocFor(new KnowledgeCaller(null, "SALES", sales), hit.Id, null))).ResponseCode.Should().Be(404);

            // Chứng từ gốc: ACC chỉ phiếu mô tả; scan → chờ KNOW đọc ảnh; KNOW ghi bản chữ → tìm được.
            var ct = await As(f, acc, s => Up(s, "60-CT", "chung-tu", "Hóa đơn đầu vào T10 – NCC Hải Nam", "hd-scan.png", Png, "Hóa đơn GTGT thịt lợn T10"));
            ct.ResponseCode.Should().Be(200);
            File.ReadAllText(Path.Combine(vault, ct.Data!.Path)).Should().Contain("ocr: cho-ai");
            var pend = await Svc(f, s => s.PendingDocsFor(new KnowledgeCaller(null, "KNOW", acc), ocr: true));
            pend.Select(p => p.Path).Should().Equal(ct.Data.Path);
            (await Svc(f, s => s.WriteAgentTextFor(new KnowledgeCaller(null, "SALES", acc), ct.Data.Path, "x", summary: false))).ResponseCode.Should().Be(403, "chỉ KNOW");
            (await Svc(f, s => s.WriteAgentTextFor(new KnowledgeCaller(null, "KNOW", acc), ct.Data.Path, "HOA DON GTGT so 0001234 Thit lon 120kg", summary: false))).ResponseCode.Should().Be(200);
            File.ReadAllText(Path.Combine(vault, ct.Data.Path)).Should().Contain("ocr: ai");
            (await Svc(f, s => s.PendingDocsFor(new KnowledgeCaller(null, "KNOW", acc), ocr: true))).Should().BeEmpty();
            var accRead = (await Svc(f, s => s.ReadDocFor(new KnowledgeCaller(null, "ACC", acc), ct.Data.Path, null))).Data!;
            accRead.Content.Should().Contain("Hóa đơn GTGT thịt lợn T10").And.NotContain("0001234");
            accRead.Note.Should().Contain("chỉ đọc phiếu mô tả");
            (await Svc(f, s => s.ReadDocFor(new KnowledgeCaller(null, "VIVI", acc), ct.Data.Path, null))).Data!.Content.Should().Contain("0001234");

            // Vùng cá nhân: scan không gửi AI – thiếu tóm tắt bị chặn; có tóm tắt → không vào danh sách chờ đọc ảnh.
            (await As(f, hr, s => Up(s, "70-HS", "ho-so-nv", "CCCD NV001", "cccd.png", Png))).ResponseCode.Should().Be(400);
            var hs = await As(f, hr, s => Up(s, "70-HS", "ho-so-nv", "Hồ sơ NV001", "hs.png", Png, "Hồ sơ tuyển dụng NV001"));
            hs.ResponseCode.Should().Be(200);
            File.ReadAllText(Path.Combine(vault, hs.Data!.Path)).Should().Contain("ban_chu: khong-gui-ai").And.NotContain("ocr: cho-ai");
            (await Svc(f, s => s.PendingDocsFor(new KnowledgeCaller(null, "KNOW", hr), ocr: true))).Should().BeEmpty();

            // KNOW tóm tắt tài liệu mới (nhãn agent); tài liệu đã tóm tắt rời khỏi danh sách.
            (await Svc(f, s => s.WriteAgentTextFor(new KnowledgeCaller(null, "KNOW", salesHead), up.Data.Path, "Hợp đồng suất ăn với Vin Ocean 2026; phạt giao trễ 5% giá trị suất ăn ngày trễ.", summary: true))).ResponseCode.Should().Be(200);
            File.ReadAllText(Path.Combine(vault, up.Data.Path)).Should().Contain("tom_tat_boi: agent:know");
            (await Svc(f, s => s.PendingDocsFor(new KnowledgeCaller(null, "KNOW", salesHead), ocr: false))).Select(p => p.Path).Should().NotContain(up.Data.Path);

            // Công cụ qua ToolGateway: doc_tai_lieu (không người hỏi) đọc HDSD công khai nội bộ; vùng hạn chế → lỗi.
            File.WriteAllText(Path.Combine(vault, "80 Đào tạo", "Hướng dẫn sử dụng", "05_KHO.md"), "---\nid: hdsd-05_kho\ntype: hdsd\nsource: delicare-docs\nowner: Máy\nstatus: hiệu lực\nvalid_from: 2026-11-28\nreview_by: 2027-11-28\nconfidentiality: nội bộ\nrefs: []\n---\n# 05 – Kho\n\n## Nhận hàng – `scm-gr`\n\n1. **Bấm thẻ NCC** – mở form nhận.\n", new UTF8Encoding(false));
            var tool = await AgentToolsAi3Tests.Call(f, "WH", "doc_tai_lieu", new { id = "hdsd-05_kho" });
            tool.Status.Should().Be(200, tool.Message);
            ((JsonNode)tool.Data!)["noi_dung"]!.GetValue<string>().Should().Contain("Bấm thẻ NCC");
            (await AgentToolsAi3Tests.Call(f, "SALES", "doc_tai_lieu", new { id = up.Data.Path })).Status.Should().Be(400, "không người hỏi → vùng hạn chế");
        }
    }
}
