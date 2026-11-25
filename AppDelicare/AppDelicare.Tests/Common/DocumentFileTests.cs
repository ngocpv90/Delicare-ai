using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AppDelicare.Model.Entity;
using AppDelicare.Services.Implement;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Common
{
    /// <summary>
    /// BE2.7 – tệp đính kèm (spec 2.4, Catalog 19–21): lưu NGOÀI wwwroot trong thư mục cấu hình (thư mục tạm của test), tên sinh = SHA-256, gắn chứng từ qua khóa
    /// lưu trữ (GAP-DOC-01); chặn tệp chạy được / đổi đuôi / quá dung lượng / đường dẫn đi ra ngoài; quyền theo màn của chứng từ; tải ghi AuditChange.
    /// </summary>
    public sealed class DocumentFileTests : IDisposable
    {
        private readonly InMemoryApiFactory _factory = new();
        private readonly string _root = Path.Combine(Path.GetTempPath(), "delicare-be27-" + Guid.NewGuid().ToString("N")[..8]);

        public DocumentFileTests()
        {
            _factory.Settings["DocumentStorage:RootPath"] = _root;
            _factory.Settings["DocumentStorage:MaxFileSizeMb"] = "1";
        }

        public void Dispose()
        {
            _factory.Dispose();
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 0x49, 0x48, 0x44, 0x52, 1, 2, 3, 4];

        private static byte[] Xlsx(bool withMacro = false)
        {
            using var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var name in new[] { "[Content_Types].xml", "xl/workbook.xml" }.Concat(withMacro ? ["xl/vbaProject.bin"] : []))
                {
                    using var w = new StreamWriter(zip.CreateEntry(name).Open());
                    w.Write("<x/>");
                }
            }
            return ms.ToArray();
        }

        private static MultipartFormDataContent Form(byte[] content, string fileName, string? contentType = null, params (string Key, string Value)[] fields)
        {
            var form = new MultipartFormDataContent();
            var file = new ByteArrayContent(content);
            if (contentType != null)
            {
                file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            }
            form.Add(file, "File", fileName);
            foreach (var (k, v) in fields)
            {
                form.Add(new StringContent(v), k);
            }
            return form;
        }

        private async Task<Guid> PodAsync()
        {
            Guid pod = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.ProofOfDelivery.Add(new ProofOfDelivery { Id = pod, DeliveryOrderId = Guid.NewGuid(), ConfirmedAt = new DateTime(2026, 12, 8, 10, 0, 0), ReceiverName = "[DEMO]" });
                return Task.CompletedTask;
            });
            return pod;
        }

        [Fact]
        public async Task Upload_list_download_by_owner_record_with_dedupe_and_download_audit()
        {
            Guid pod = await PodAsync();
            var admin = _factory.CreateAuthorizedClient();
            var up = (await LegacyResult.ReadAsync(await admin.PostAsync("api/DocumentFile/Upload",
                Form(Png, "chu ky khach.png", "image/png", ("Category", "signature"), ("Resource", "ProofOfDelivery"), ("RecordId", pod.ToString()))))).Ok();
            Guid id = up.GetProperty("id").GetGuid();
            string sha = up.GetProperty("sha256").GetString()!;
            sha.Should().Be(DocumentStorageRules.Sha256Hex(Png));
            up.GetProperty("fileName").GetString().Should().Be("chu ky khach.png");
            up.GetProperty("resource").GetString().Should().Be("ProofOfDelivery");
            up.GetProperty("url").GetString().Should().Be("api/DocumentFile/Download?id=" + id);
            // Lưu ngoài wwwroot, trong thư mục cấu hình, tên sinh (không dùng tên người dùng).
            string stored = Path.Combine(_root, "ProofOfDelivery", pod.ToString("N"), "signature", sha + ".png");
            File.Exists(stored).Should().BeTrue();
            Directory.GetFiles(_root, "*chu ky*", SearchOption.AllDirectories).Should().BeEmpty();
            var row = await _factory.QueryAsync(db => db.DocumentFile.SingleAsync(f => f.Id == id));
            row.FileNameHas.Should().Be($"ProofOfDelivery/{pod:N}/signature/{sha}.png");
            row.FileType.Should().Be(".png");
            row.FileSize.Should().Be(Png.Length);
            (await _factory.QueryAsync(db => db.AuditChange.AnyAsync(a => a.IdChange == id && a.Action == "Added"))).Should().BeTrue("tải lên có người thao tác + audit");

            // Cùng nội dung, cùng chứng từ → trả tệp cũ, không thêm dòng.
            var again = (await LegacyResult.ReadAsync(await admin.PostAsync("api/DocumentFile/Upload",
                Form(Png, "ban sao.png", "image/png", ("Category", "signature"), ("Resource", "ProofOfDelivery"), ("RecordId", pod.ToString()))))).Ok();
            again.GetProperty("id").GetGuid().Should().Be(id);
            again.GetProperty("isDuplicate").GetBoolean().Should().BeTrue();
            var excel = (await LegacyResult.ReadAsync(await admin.PostAsync("api/DocumentFile/Upload",
                Form(Xlsx(), "bien ban.xlsx", null, ("Category", "evidence"), ("Resource", "ProofOfDelivery"), ("RecordId", pod.ToString()))))).Ok();

            var list = await admin.GetResultAsync($"api/DocumentFile/GetList?Resource=ProofOfDelivery&RecordId={pod}");
            list.ListData.GetArrayLength().Should().Be(2);
            (await admin.GetResultAsync($"api/DocumentFile/GetList?Resource=ProofOfDelivery&RecordId={pod}&FileType=xlsx")).ListData.GetArrayLength().Should().Be(1);
            (await admin.GetResultAsync($"api/DocumentFile/GetList?Category=signature")).ListData.GetArrayLength().Should().Be(1);

            var download = await admin.GetAsync($"api/DocumentFile/Download?id={id}");
            download.StatusCode.Should().Be(HttpStatusCode.OK);
            download.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
            download.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment");
            download.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
            (await download.Content.ReadAsByteArrayAsync()).Should().Equal(Png);
            (await _factory.QueryAsync(db => db.AuditChange.CountAsync(a => a.IdChange == id && a.Action == "Download" && a.CreatedBy == InMemoryApiFactory.UserId))).Should().Be(1);

            // Lịch sử của POD có mục "Đính kèm tệp".
            var history = (await admin.GetResultAsync($"api/Common/GetHistory?resource=ProofOfDelivery&id={pod}")).ListData.EnumerateArray().ToList();
            history.Count(h => h.GetProperty("source").GetString() == "FILE").Should().Be(2);

            // Quyền theo màn của chứng từ (#/log-execution – UI-6.35): Xem → được danh sách / tải; tải lên cần Sửa; không có quyền → 403.
            Guid viewer = await CommonTestUsers.UserAsync(_factory, ("log-execution", "V"));
            var v = _factory.CreateAuthorizedClient(viewer);
            (await v.GetAsync($"api/DocumentFile/Download?id={id}")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await v.GetResultAsync($"api/DocumentFile/GetList?Resource=ProofOfDelivery&RecordId={pod}")).ListData.GetArrayLength().Should().Be(2);
            (await v.GetAsync("api/DocumentFile/GetList")).StatusCode.Should().Be(HttpStatusCode.Forbidden, "mọi tệp cần quyền Xem #/adm-document");
            (await v.PostAsync("api/DocumentFile/Upload", Form(Png, "x.png", "image/png", ("Resource", "ProofOfDelivery"), ("RecordId", pod.ToString()))))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);
            Guid other = await CommonTestUsers.UserAsync(_factory, ("scm-gr", "VAE"));
            (await _factory.CreateAuthorizedClient(other).GetAsync($"api/DocumentFile/Download?id={id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await _factory.CreateClient().GetAsync($"api/DocumentFile/Download?id={id}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            // Tài liệu chung (#/adm-document) và tệp bị xóa mềm.
            var general = (await LegacyResult.ReadAsync(await admin.PostAsync("api/DocumentFile/Upload", Form(Png, "logo.png", "image/png")))).Ok();
            general.GetProperty("resource").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
            (await admin.DeleteResultAsync($"api/DocumentFile/Delete?id={excel.GetProperty("id").GetGuid()}")).Ok();
            (await admin.GetResultAsync($"api/DocumentFile/Download?id={excel.GetProperty("id").GetGuid()}")).Fail("Không tìm thấy tệp");
        }

        [Fact]
        public async Task Upload_rejects_executables_spoofed_content_oversize_and_bad_owner()
        {
            Guid pod = await PodAsync();
            var admin = _factory.CreateAuthorizedClient();
            async Task<LegacyResult> Up(byte[] content, string name, string? type = null, params (string, string)[] fields)
                => await LegacyResult.ReadAsync(await admin.PostAsync("api/DocumentFile/Upload", Form(content, name, type, fields)));

            byte[] exe = [0x4D, 0x5A, 0x90, 0x00, 1, 2, 3];
            (await Up(exe, "setup.exe")).Fail("Không nhận tệp");
            (await Up(Encoding.UTF8.GetBytes("<svg onload=alert(1)>"), "a.svg", "image/svg+xml")).Fail("Không nhận tệp");
            (await Up(Encoding.UTF8.GetBytes("<script>"), "trang.html")).Fail("Không nhận tệp");
            (await Up(exe, "hoa-don.pdf", "application/pdf")).Fail("không khớp đuôi");
            (await Up(exe, "anh.png", "image/png")).Fail("không khớp đuôi");
            (await Up(Png, "anh.png", "application/x-msdownload")).Fail("không khớp đuôi");
            (await Up(Xlsx(withMacro: true), "macro.xlsx")).Fail("không khớp đuôi");
            (await Up(Encoding.UTF8.GetBytes("%PDF-1.4"), "khong-duoi")).Fail("Không nhận tệp");
            byte[] big = new byte[1024 * 1024 + 10];
            Png.CopyTo(big, 0);
            (await Up(big, "to.png", "image/png")).Fail("vượt quá 1 MB");
            (await Up([], "rong.png", "image/png")).Fail();
            (await Up(Png, "a.png", "image/png", ("Category", "../../etc"))).Fail("Loại tài liệu");
            (await Up(Png, "a.png", "image/png", ("Resource", "../User"), ("RecordId", pod.ToString()))).Fail("không hỗ trợ");
            (await Up(Png, "a.png", "image/png", ("Resource", "User"), ("RecordId", InMemoryApiFactory.UserId.ToString()))).Fail("không hỗ trợ");
            (await Up(Png, "a.png", "image/png", ("Resource", "ProofOfDelivery"))).Fail("Resource và RecordId");
            (await Up(Png, "a.png", "image/png", ("Resource", "ProofOfDelivery"), ("RecordId", Guid.NewGuid().ToString()))).Fail("Không tìm thấy");

            // Tên tệp chứa đường dẫn đi ra ngoài: chỉ giữ tên hiển thị, tệp vẫn nằm trong thư mục gốc với tên sinh.
            var ok = (await Up(Png, "..\\..\\..\\windows\\evil.png", "image/png", ("Resource", "ProofOfDelivery"), ("RecordId", pod.ToString()))).Ok();
            ok.GetProperty("fileName").GetString().Should().Be("evil.png");
            Directory.GetFiles(_root, "*", SearchOption.AllDirectories).Should().OnlyContain(f => Path.GetFullPath(f).StartsWith(Path.GetFullPath(_root)));
            (await _factory.QueryAsync(db => db.DocumentFile.CountAsync())).Should().Be(1, "mọi lần bị từ chối không ghi DB");
        }

        [Fact]
        public void Storage_key_cannot_escape_root()
        {
            string root = Path.Combine(Path.GetTempPath(), "root-x");
            string sha = new('a', 64);
            DocumentStorageRules.Parse($"ProofOfDelivery/{Guid.NewGuid():N}/photo/{sha}.png").Should().NotBeNull();
            DocumentStorageRules.Parse($"_chung/other/{sha}.pdf")!.Resource.Should().BeNull();
            foreach (var bad in new[] { $"../{sha}.png", $"ProofOfDelivery/../../x/photo/{sha}.png", $"ProofOfDelivery/{Guid.NewGuid():N}/photo/../../{sha}.png",
                         $"C:\\Windows\\{sha}.png", $"/etc/{sha}.png", "Person_123.png", $"ProofOfDelivery/{Guid.NewGuid():N}/photo/{sha}.png/../../x" })
            {
                DocumentStorageRules.Parse(bad).Should().BeNull(bad);
                FluentActions.Invoking(() => DocumentStorageRules.FullPath(root, bad)).Should().Throw<InvalidOperationException>(bad);
            }
            DocumentStorageRules.SafeFileName("../../a\"b<c>.png").Should().Be("abc.png");
            DocumentStorageRules.SafeFileName("   ").Should().Be("tep");
            foreach (var ext in new[] { ".exe", ".js", ".html", ".svg", ".bat", ".ps1", ".xlsm" })
            {
                DocumentStorageRules.NeverAllowed.Should().Contain(ext);
            }
        }
    }
}
