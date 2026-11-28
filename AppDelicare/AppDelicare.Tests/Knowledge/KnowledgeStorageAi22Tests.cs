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
    /// AI-22.2 (D-141, §7.9 luật 1, 4, 6, 7): tầng Lưu trữ – tải file gốc + phiếu mô tả (quyền Sửa, đúng loại / chữ ký tệp, ≤ MaxFileMb, tên trên đĩa do máy chủ đặt),
    /// tải xuống chỉ qua API với quyền Xuất, "Lưu trữ" thay xóa (quyền Xóa, file giữ nguyên), nhật ký xem / tải vùng hạn chế, nhắc hết hạn lưu giữ không tự xóa.
    /// </summary>
    public class KnowledgeStorageAi22Tests
    {
        private static async Task<Guid> UserAsync(InMemoryApiFactory f, string name, string folder, bool edit, bool export, bool delete)
        {
            Guid u = Guid.NewGuid(), g = Guid.NewGuid();
            await f.SeedAsync(async db =>
            {
                db.User.Add(new User { Id = u, UserName = name, IsActive = true, AccountType = "INTERNAL" });
                db.UserGroup.Add(new UserGroup { Id = g, Name = name, Code = name.ToUpperInvariant() });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = u, UserGroupId = g });
                string link = "#/knowledge?folder=" + folder.ToLowerInvariant();
                var m = await db.ObjectMenu.FirstOrDefaultAsync(x => x.MetaLink == link) ?? db.ObjectMenu.Local.FirstOrDefault(x => x.MetaLink == link);
                if (m == null)
                {
                    m = new ObjectMenu { Id = Guid.NewGuid(), Name = folder, MetaLink = link, LevelShow = 2, IsActive = true };
                    db.ObjectMenu.Add(m);
                }
                db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = g, ObjectMenuId = m.Id, AllowAccess = true, AllowEdit = edit, AllowAdd = edit, AllowExport = export, AllowDelete = delete });
            });
            return u;
        }

        private static async Task<T> As<T>(InMemoryApiFactory f, Guid user, Func<IKnowledgeService, Task<T>> run)
        {
            using var scope = f.Services.CreateScope();
            using var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForUser(user));
            return await run(scope.ServiceProvider.GetRequiredService<IKnowledgeService>());
        }

        private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF\n");

        private static Task<Data.Interface.IResult<KnowledgeSaveResultModel>> Upload(IKnowledgeService s, string name, byte[] bytes, string folder = "60-CT", string? retain = null)
            => s.UploadFile(new KnowledgeUploadRequest { Folder = folder, Type = "chung-tu", Title = "Hóa đơn đầu vào T10 – NCC Hải Nam", SoHieu = "HD-0001234", Ngay = "2026-10-31", DoiTuong = "NCC00012", RetainUntil = retain, Summary = "Hóa đơn GTGT thịt lợn tháng 10." },
                name, new MemoryStream(bytes), bytes.Length);

        [Fact]
        public async Task Upload_download_archive_follow_rights_and_restricted_access_is_logged()
        {
            string vault = Path.Combine(Path.GetTempPath(), "dc-knowledge-" + Guid.NewGuid().ToString("N"));
            using var f = await AgentToolsAi3Tests.FactoryAsync(("Knowledge:VaultPath", vault), ("Knowledge:MaxFileMb", "1"));
            f.Clock.FixedNow = new DateTime(2026, 11, 28, 9, 0, 0);
            f.CreateAuthorizedClient().Dispose();
            Guid acc = await UserAsync(f, "kt-a222", "60-CT", edit: true, export: true, delete: false);
            Guid boss = await UserAsync(f, "kt-truong-a222", "60-CT", edit: true, export: true, delete: true);
            Guid viewer = await UserAsync(f, "gd-a222", "60-CT", edit: false, export: false, delete: false);

            // Luật 7: loại tệp, chữ ký đầu tệp, dung lượng; người chỉ Xem không tải lên.
            (await As(f, acc, s => Upload(s, "virus.exe", Pdf))).ResponseCode.Should().Be(400);
            (await As(f, acc, s => Upload(s, "gia.pdf", [0x89, 0x50, 0x4E, 0x47, 1, 2, 3]))).ResponseCode.Should().Be(400, "đuôi pdf nhưng nội dung là PNG");
            (await As(f, acc, s => Upload(s, "to.pdf", [.. Pdf, .. new byte[2 * 1024 * 1024]]))).ResponseCode.Should().Be(400, "vượt Knowledge:MaxFileMb");
            (await As(f, viewer, s => Upload(s, "hd.pdf", Pdf))).ResponseCode.Should().Be(403);

            var up = await As(f, acc, s => Upload(s, "../../Hoa don T10 (ban scan).pdf", Pdf, retain: "2026-11-01"));
            up.ResponseCode.Should().Be(200, up.ResponseMessage);
            string note = up.Data!.Path;
            note.Should().StartWith("60 Kế toán/Chứng từ gốc/chung-tu-20261128090000-").And.EndWith(".md");
            string onDisk = Path.Combine(vault, note[..^3] + ".pdf");
            File.ReadAllBytes(onDisk).Should().Equal(Pdf, "tên trên đĩa do máy chủ đặt, không dùng tên người gửi");
            Directory.EnumerateFiles(vault, "*Hoa don*", SearchOption.AllDirectories).Should().BeEmpty();

            // Xem: phiếu mô tả + được tải; người không có quyền Xuất → 403; tải ghi nhật ký.
            var seen = (await As(f, viewer, s => s.GetNote(note))).Data!;
            seen.FileName.Should().Be("Hoa don T10 (ban scan).pdf");
            seen.Meta["so_hieu"].Should().Be("HD-0001234");
            seen.CanDownload.Should().BeFalse();
            (await As(f, viewer, s => s.DownloadFile(note))).ResponseCode.Should().Be(403);
            var dl = await As(f, acc, s => s.DownloadFile(note));
            dl.ResponseCode.Should().Be(200);
            dl.Data!.Content.Should().Equal(Pdf);
            dl.Data.ContentType.Should().Be("application/pdf");
            dl.Data.FileName.Should().Be("Hoa don T10 (ban scan).pdf");
            var log = await f.QueryAsync(db => db.AuditChange.AsNoTracking().Where(a => a.EntityName == KnowledgeService.AccessEntity).Select(a => new { a.Action, a.CreatedBy }).ToListAsync());
            log.Should().BeEquivalentTo([new { Action = (string?)"Xem", CreatedBy = (Guid?)viewer }, new { Action = (string?)"Tải", CreatedBy = (Guid?)acc }], "luật 4: mỗi lần xem / tải vùng hạn chế 1 dòng");
            (await As(f, boss, s => s.GetAccessLog(note))).ListData!.Should().HaveCount(2);
            (await As(f, viewer, s => s.GetAccessLog(note))).ResponseCode.Should().Be(403);

            // Xóa = Lưu trữ (quyền Xóa), file gốc giữ nguyên.
            (await As(f, acc, s => s.Archive(new KnowledgePathRequest { Path = note }))).ResponseCode.Should().Be(403);
            (await As(f, boss, s => s.Archive(new KnowledgePathRequest { Path = note }))).ResponseCode.Should().Be(200);
            File.ReadAllText(Path.Combine(vault, note)).Should().Contain("status: lưu trữ");
            File.Exists(onDisk).Should().BeTrue();

            // Luật 6: quá hạn lưu giữ → nhắc, không xóa (tài liệu đã Lưu trữ thì thôi nhắc).
            var up2 = await As(f, acc, s => Upload(s, "hd2.pdf", Pdf, retain: "2026-11-20"));
            using (var scope = f.Services.CreateScope())
            {
                var r = await scope.ServiceProvider.GetRequiredService<IKnowledgeService>().MaintainAsync();
                r.RetainDueByFolder.Should().ContainKey("60-CT").WhoseValue.Should().ContainSingle().Which.Should().Contain("20/11/2026");
            }
            File.Exists(Path.Combine(vault, up2.Data!.Path)).Should().BeTrue();
        }
    }
}
