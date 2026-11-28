using System.Text;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Model;
using Newtonsoft.Json;

namespace AppDelicare.Services.Implement.Knowledge
{
    /// <summary>AI-22.2: phiếu mô tả khi tải file gốc lên kho (frontmatter §7.9).</summary>
    public sealed class KnowledgeUploadRequest
    {
        /// <summary>Mã vùng (05, 15, 20-HD, 40-HD, 60-CT, 70-HS…).</summary>
        public string? Folder { get; set; }
        /// <summary>cong-van-den / cong-van-di / bien-ban-hop / hop-dong-kh / hop-dong-ncc / hop-dong-ld / ho-so-nv / chung-tu / bao-cao / hdsd / tai-lieu.</summary>
        public string? Type { get; set; }
        public string? Title { get; set; }
        /// <summary>Số hiệu văn bản / hợp đồng / chứng từ.</summary>
        public string? SoHieu { get; set; }
        /// <summary>yyyy-MM-dd – ngày văn bản.</summary>
        public string? Ngay { get; set; }
        /// <summary>Mã đối tượng trong lõi / AMIS (mã khách, NCC, nhân viên, chứng từ) – thành refs.</summary>
        public string? DoiTuong { get; set; }
        public string? Confidentiality { get; set; }
        /// <summary>yyyy-MM-dd – hạn lưu giữ; hết hạn chỉ nhắc (không tự xóa).</summary>
        public string? RetainUntil { get; set; }
        /// <summary>Mô tả / tóm tắt (bắt buộc với scan vùng cá nhân / BOD – không gửi AI).</summary>
        public string? Summary { get; set; }
        /// <summary>AI-22.4 (sổ công văn 05): nơi gửi (đến) / nơi nhận (đi).</summary>
        public string? NoiGuiNhan { get; set; }
        /// <summary>AI-22.4: người xử lý (User.Id) + hạn xử lý yyyy-MM-dd → việc theo dõi 98.</summary>
        public Guid? HandlerId { get; set; }
        public string? HandlerDue { get; set; }
    }

    /// <summary>AI-22.2: 1 dòng nhật ký truy cập tài liệu hạn chế (AuditChange EntityName <c>KnowledgeAccess</c>).</summary>
    public sealed class KnowledgeAccessModel
    {
        public DateTime At { get; set; }
        public string? Action { get; set; }
        public Guid? By { get; set; }
        public string? Path { get; set; }
    }

    /// <summary>
    /// AI-22.2 (D-141 b, d, h, j; §7.9 luật 1, 4, 6, 7): tầng Lưu trữ của kho – file gốc + phiếu mô tả markdown cùng tên đặt cạnh (không bảng / cột mới, file KHÔNG lưu DB).
    /// Tải lên: quyền Sửa vùng; chỉ pdf / docx / xlsx / jpg / png (kiểm cả chữ ký đầu tệp), ≤ <c>Knowledge:MaxFileMb</c>; tên trên đĩa do máy chủ đặt (không dùng tên người gửi).
    /// Tải xuống: chỉ qua API, quyền Xuất vùng (BOD / hồ sơ cá nhân: cấp đích danh). Xóa = chuyển trạng thái Lưu trữ (quyền Xóa). Mỗi lần xem / tải ở vùng hạn chế
    /// (hợp đồng, chứng từ, hồ sơ cá nhân, BOD) ghi 1 dòng AuditChange <c>KnowledgeAccess</c> (Quyết định tự chọn: bảng nhật ký có sẵn, IdChange = Guid từ đường dẫn).
    /// </summary>
    public partial class KnowledgeService
    {
        public const string AccessEntity = "KnowledgeAccess";

        public static readonly string[] FileTypes = ["cong-van-den", "cong-van-di", "bien-ban-hop", "hop-dong-kh", "hop-dong-ncc", "hop-dong-ld", "ho-so-nv", "chung-tu", "bao-cao", "hdsd", "tai-lieu"];

        private static readonly Dictionary<string, (string Mime, byte[] Magic)> Allowed = new(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = ("application/pdf", "%PDF"u8.ToArray()),
            [".docx"] = ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", [0x50, 0x4B]),
            [".xlsx"] = ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", [0x50, 0x4B]),
            [".jpg"] = ("image/jpeg", [0xFF, 0xD8, 0xFF]),
            [".jpeg"] = ("image/jpeg", [0xFF, 0xD8, 0xFF]),
            [".png"] = ("image/png", [0x89, 0x50, 0x4E, 0x47]),
        };

        /// <summary>Ghi nhật ký truy cập (luật 4) – chỉ vùng hạn chế; người (không agent).</summary>
        private void LogAccess(string path, string folder, string action, Guid? by)
        {
            if (!KnowledgeVault.Restricted.Contains(folder))
            {
                return;
            }
            unitOfWork.DbContext.AuditChange.Add(new AuditChange
            {
                Id = Guid.NewGuid(), IdChange = KnowledgeVault.KeyGuid(path), EntityName = AccessEntity, Action = action,
                Changes = JsonConvert.SerializeObject(new { path, folder }), CreatedDate = clock.Now, CreatedBy = by, IsDeleted = false,
            });
        }

        public async Task<IResult<KnowledgeSaveResultModel>> UploadFile(KnowledgeUploadRequest request, string fileName, Stream stream, long length)
        {
            var caller = WebCaller();
            if (caller.AgentCode != null)
            {
                return Status<KnowledgeSaveResultModel>(403, "Agent không tải file lên kho");
            }
            var folder = KnowledgeVault.FolderByCode(request.Folder);
            if (folder == null || folder.AgentWritable)
            {
                return Result<KnowledgeSaveResultModel>.Fail("Chọn vùng tài liệu hợp lệ");
            }
            if (folder.Code == KnowledgeVault.UserGuide)
            {
                return Status<KnowledgeSaveResultModel>(403, "Hướng dẫn sử dụng do máy tự nhập từ tài liệu phát hành – không tải tay");
            }
            var rights = await RightsAsync(caller);
            if (!rights.CanEdit(folder.Code))
            {
                return Status<KnowledgeSaveResultModel>(403, $"Không có quyền Sửa / tải lên vùng {folder.Dir}");
            }
            string ext = Path.GetExtension(fileName ?? string.Empty);
            if (!Allowed.TryGetValue(ext, out var kind))
            {
                return Result<KnowledgeSaveResultModel>.Fail("Chỉ nhận tệp PDF, Word (.docx), Excel (.xlsx), ảnh JPG / PNG");
            }
            long max = Math.Max(1, _o.MaxFileMb) * 1024L * 1024L;
            if (length <= 0 || length > max)
            {
                return Result<KnowledgeSaveResultModel>.Fail($"Tệp rỗng hoặc vượt {_o.MaxFileMb} MB");
            }
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            byte[] bytes = ms.ToArray();
            if (bytes.Length > max || bytes.Length < kind.Magic.Length || !bytes.AsSpan(0, kind.Magic.Length).SequenceEqual(kind.Magic))
            {
                return Result<KnowledgeSaveResultModel>.Fail("Nội dung tệp không đúng định dạng " + ext.TrimStart('.').ToUpperInvariant());
            }
            string type = KnowledgeVault.Slug(request.Type);
            if (!FileTypes.Contains(type))
            {
                return Result<KnowledgeSaveResultModel>.Fail("Loại tài liệu phải là " + string.Join(" / ", FileTypes));
            }
            string title = (request.Title ?? string.Empty).Trim();
            if (title.Length == 0)
            {
                return Result<KnowledgeSaveResultModel>.Fail("Nhập tên tài liệu (trích yếu)");
            }
            DateTime? Date(string? s) => DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d) ? d.Date : null;
            DateTime day = Date(request.Ngay) ?? clock.Today;
            DateTime? retain = Date(request.RetainUntil);
            if (!string.IsNullOrWhiteSpace(request.RetainUntil) && retain == null)
            {
                return Result<KnowledgeSaveResultModel>.Fail("Hạn lưu giữ không đúng ngày (yyyy-MM-dd)");
            }
            string conf = KnowledgeVault.Slug(request.Confidentiality);
            if (!KnowledgeVault.Confidentialities.Contains(conf))
            {
                conf = KnowledgeVault.Restricted.Contains(folder.Code) ? "han-che" : "noi-bo";
            }
            // AI-22.4: công văn vào sổ 05 – số vào sổ máy chủ cấp (khóa vùng chống trùng), người xử lý + hạn → việc theo dõi 98.
            bool dispatch = folder.Code == DispatchFolder && type is "cong-van-den" or "cong-van-di";
            SemaphoreSlim? gate = dispatch ? KnowledgeVault.LockFor(Root, "#register-" + DispatchFolder) : null;
            if (gate != null)
            {
                await gate.WaitAsync();
            }
            try
            {
            string? regNo = null;
            if (dispatch)
            {
                var (no, err) = await DispatchNumberAsync(request, type, day);
                if (err != null)
                {
                    return Result<KnowledgeSaveResultModel>.Fail(err);
                }
                regNo = no;
            }
            string? handlerName = await UserNameAsync(request.HandlerId);
            DateTime? handlerDue = KnowledgeVault.ParseDate(request.HandlerDue);
            // Tên trên đĩa do máy chủ đặt (luật 7): <loại>-<yyyyMMddHHmmss>-<8 ký tự>; tên gốc chỉ để hiển thị.
            string stem = $"{type}-{clock.Now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..(type.Length + 24)];
            string notePath = $"{folder.Dir}/{stem}.md", filePath = $"{folder.Dir}/{stem}{ext.ToLowerInvariant()}";
            var actor = currentActor.RequireActor();
            string userName = unitOfWork.DbContext.User.Where(u => u.Id == actor.UserId).Select(u => u.UserName).FirstOrDefault() ?? "người dùng";
            string Q(string? s) => (s ?? string.Empty).Replace("\n", " ").Replace("\r", " ").Trim();
            string fm = KnowledgeVault.Frontmatter([
                ("id", stem), ("type", type), ("source", KnowledgeVault.HumanSource), ("owner", Q(userName)), ("status", "hiệu lực"),
                ("valid_from", day.ToString("yyyy-MM-dd")), ("review_by", (retain ?? day.AddYears(1)).ToString("yyyy-MM-dd")), ("confidentiality", conf),
                ("refs", string.IsNullOrWhiteSpace(request.DoiTuong) ? "[]" : "[" + KnowledgeVault.FileSafe(Q(request.DoiTuong)) + "]"), ("title", Q(title)),
                ("so_hieu", Q(request.SoHieu)), ("ngay", day.ToString("yyyy-MM-dd")), ("doi_tuong", Q(request.DoiTuong)), ("retain_until", retain?.ToString("yyyy-MM-dd") ?? string.Empty),
                ("file", stem + ext.ToLowerInvariant()), ("ten_goc", Q(Path.GetFileName(fileName))), ("kich_thuoc", bytes.Length.ToString()),
                .. (regNo != null ? new[] { ("so_vao_so", regNo), ("noi_gui_nhan", Q(request.NoiGuiNhan)), ("nguoi_xu_ly", handlerName ?? string.Empty), ("han_xu_ly", handlerDue?.ToString("yyyy-MM-dd") ?? string.Empty) } : []),
            ]);
            string body = $"\n# {Q(title)}\n\n{(string.IsNullOrWhiteSpace(request.Summary) ? "(chưa có mô tả)" : request.Summary!.Trim())}\n\n📎 Tệp gốc: {Q(Path.GetFileName(fileName))} – tải bằng nút **Tải file gốc** (cần quyền Xuất của vùng).\n";
            string full = Path.Combine(Root, filePath);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            await File.WriteAllBytesAsync(full, bytes);
            var r = await WriteAsync(notePath, fm + body, null, actor.UserId, guardGenerated: true, note: "upload " + filePath);
            if (r.ResponseCode != 200)
            {
                File.Delete(full);
            }
            else if (regNo != null)
            {
                if (request.HandlerId is { } h && handlerName != null && handlerDue != null)
                {
                    await CreateFollowTaskAsync("CONG-VAN", $"Xử lý công văn {regNo}: {Q(title)}", $"Công văn {(type == "cong-van-di" ? "đi" : "đến")} {regNo}{(string.IsNullOrWhiteSpace(request.SoHieu) ? string.Empty : " (số " + Q(request.SoHieu) + ")")} – {Q(request.NoiGuiNhan)}.", h, handlerName, handlerDue.Value, notePath, actor.UserId);
                }
                r.ResponseMessage = $"Đã vào sổ {regNo}" + (handlerName != null ? $" · giao {handlerName} xử lý trước {handlerDue:dd/MM/yyyy}" : string.Empty);
            }
            return r;
            }
            finally
            {
                gate?.Release();
            }
        }

        public async Task<IResult<FileContentModel>> DownloadFile(string? path)
        {
            var caller = WebCaller();
            var rights = await RightsAsync(caller);
            var n = All().FirstOrDefault(x => string.Equals(x.Path, (path ?? string.Empty).Replace('\\', '/').Trim(), StringComparison.OrdinalIgnoreCase));
            if (n == null || !rights.NoteVisible(n) || n.Meta.GetValueOrDefault("file") is not { Length: > 0 } file)
            {
                return Status<FileContentModel>(404, "Không có tài liệu này hoặc không có quyền xem");
            }
            if (caller.AgentCode != null || !rights.CanExport(n.Folder))
            {
                return Status<FileContentModel>(403, "Cần quyền Xuất (tải file gốc) của vùng tài liệu này");
            }
            string dir = Path.GetDirectoryName(n.Path)!.Replace('\\', '/');
            string full = Path.Combine(Root, dir, Path.GetFileName(file));
            if (!File.Exists(full) || !Allowed.TryGetValue(Path.GetExtension(full), out var kind))
            {
                return Status<FileContentModel>(404, "Không tìm thấy tệp gốc trên máy chủ");
            }
            LogAccess(n.Path, n.Folder, "Tải", caller.UserId);
            await unitOfWork.DbContext.SaveChangesAsync();
            string shown = n.Meta.GetValueOrDefault("ten_goc") is { Length: > 0 } g ? g : Path.GetFileName(full);
            return Result<FileContentModel>.Success(new FileContentModel { Content = await File.ReadAllBytesAsync(full), ContentType = kind.Mime, FileName = shown }, "OK");
        }

        /// <summary>Xóa = chuyển trạng thái Lưu trữ (luật 1: AllowDelete); file gốc giữ nguyên.</summary>
        public async Task<IResult<KnowledgeSaveResultModel>> Archive(KnowledgePathRequest request)
        {
            var caller = WebCaller();
            var rights = await RightsAsync(caller);
            var n = All().FirstOrDefault(x => string.Equals(x.Path, (request.Path ?? string.Empty).Replace('\\', '/').Trim(), StringComparison.OrdinalIgnoreCase));
            if (n == null || !rights.NoteVisible(n))
            {
                return Status<KnowledgeSaveResultModel>(404, "Không có tài liệu này hoặc không có quyền xem");
            }
            if (caller.AgentCode != null || n.Generated || !rights.CanArchive(n.Folder))
            {
                return Status<KnowledgeSaveResultModel>(403, "Cần quyền Xóa (chuyển Lưu trữ) của vùng tài liệu này");
            }
            if (n.Status == "luu-tru")
            {
                return Result<KnowledgeSaveResultModel>.Success(new KnowledgeSaveResultModel { Path = n.Path, Hash = n.Hash }, "Tài liệu đã ở trạng thái Lưu trữ");
            }
            string raw = await File.ReadAllTextAsync(Path.Combine(Root, n.Path), Encoding.UTF8);
            string text = SetMeta(raw, "status", "lưu trữ");
            return await WriteAsync(n.Path, text, KnowledgeVault.Sha256(raw), caller.UserId, guardGenerated: true, note: "archive");
        }

        /// <summary>Nhật ký truy cập 1 tài liệu hạn chế – người có quyền Sửa vùng xem được.</summary>
        public async Task<IResults<KnowledgeAccessModel>> GetAccessLog(string? path)
        {
            var rights = await RightsAsync(WebCaller());
            var n = All().FirstOrDefault(x => string.Equals(x.Path, (path ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));
            if (n == null || !rights.NoteVisible(n) || !rights.CanEdit(n.Folder))
            {
                return StatusList<KnowledgeAccessModel>(403, "Cần quyền Sửa vùng để xem nhật ký truy cập");
            }
            Guid key = KnowledgeVault.KeyGuid(n.Path);
            var rows = unitOfWork.DbContext.AuditChange.Where(a => a.EntityName == AccessEntity && a.IdChange == key && !a.IsDeleted).OrderByDescending(a => a.CreatedDate)
                .Select(a => new KnowledgeAccessModel { At = a.CreatedDate, Action = a.Action, By = a.CreatedBy, Path = n.Path }).ToList();
            return Results<KnowledgeAccessModel>.Success(rows, "OK");
        }
    }
}
