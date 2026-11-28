using System.Text;
using System.Text.RegularExpressions;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Model;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using static AppDelicare.Services.Implement.Knowledge.KnowledgeVault;

namespace AppDelicare.Services.Implement.Knowledge
{
    public sealed class KnowledgePathRequest
    {
        public string? Path { get; set; }
        public string? PrevHash { get; set; }
        public string? Note { get; set; }
    }

    public sealed class KnowledgeTaskModel
    {
        public string Path { get; set; } = string.Empty;
        public string Agent { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Content { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? Due { get; set; }
        public string? Owner { get; set; }
        /// <summary>dang-theo-doi / xong.</summary>
        public string Status { get; set; } = "dang-theo-doi";
        public bool Overdue { get; set; }
        public List<string> Refs { get; set; } = [];
    }

    public sealed class KnowledgeTaskSetRequest
    {
        public string? Path { get; set; }
        /// <summary>xong / dang-theo-doi.</summary>
        public string? Status { get; set; }
        public string? Owner { get; set; }
        public string? Note { get; set; }
    }

    /// <summary>
    /// AI-12 (D-133): kho tri thức – người viết + duyệt SOP.
    /// <list type="bullet">
    /// <item><b>Gửi duyệt</b> (SOP / hướng dẫn / quyết định): Nháp → Chờ duyệt + ApprovalRequest <c>KB_SOP</c> (mục <c>#/wf-inbox?type=kb-sop</c>), nội dung được duyệt = mã băm tệp.
    ///   Duyệt (người có quyền mục duyệt VÀ quyền Sửa thư mục) → Hiệu lực; bản Hiệu lực cũ cùng <c>id</c> → Lưu trữ (không xóa). Từ chối → Nháp.</item>
    /// <item><b>Phiên bản mới</b>: chép bản Hiệu lực sang tệp mới cùng <c>id</c>, trạng thái Nháp – sửa rồi gửi duyệt.</item>
    /// <item><b>Nhập Word / PDF</b> → markdown (Syncfusion DocIO / PDF đang có trong solution) thành ghi chú Nháp.</item>
    /// <item><b>Việc agent theo dõi</b> (thư mục 98): danh sách + đánh dấu Xong / giao lại (ghi dòng trạng thái của tệp, có lịch sử).</item>
    /// </list>
    /// </summary>
    public partial class KnowledgeService
    {
        public const string SopBusinessType = "KB_SOP";
        public const string SopScreen = "kb-sop";
        private static readonly string[] Approvable = ["sop", "huong-dan", "quyet-dinh"];

        /// <summary>Sửa 1 khóa frontmatter (thêm nếu chưa có). Nội dung khác giữ nguyên.</summary>
        public static string SetMeta(string content, string key, string value)
        {
            string text = content.Replace("\r\n", "\n");
            if (!text.StartsWith("---\n", StringComparison.Ordinal))
            {
                return text;
            }
            int end = text.IndexOf("\n---", 4, StringComparison.Ordinal);
            string head = text[4..end], rest = text[end..];
            var rx = new Regex("^" + Regex.Escape(key) + @"\s*:.*$", RegexOptions.Multiline);
            head = rx.IsMatch(head) ? rx.Replace(head, key + ": " + value, 1) : head + "\n" + key + ": " + value;
            return "---\n" + head + rest;
        }

        private IApprovalRequestService Approvals => serviceProvider.GetRequiredService<IApprovalRequestService>();

        public async Task<IResult<KnowledgeSaveResultModel>> SubmitSop(KnowledgePathRequest request)
        {
            var caller = WebCaller();
            var rights = await RightsAsync(caller);
            var n = All().FirstOrDefault(x => string.Equals(x.Path, request.Path, StringComparison.OrdinalIgnoreCase));
            if (n == null || !rights.NoteVisible(n))
            {
                return Status<KnowledgeSaveResultModel>(404, "Không có ghi chú này hoặc không có quyền xem");
            }
            if (!rights.CanEdit(n.Folder))
            {
                return Status<KnowledgeSaveResultModel>(403, "Không có quyền Sửa thư mục này");
            }
            if (!Approvable.Contains(n.Type))
            {
                return Result<KnowledgeSaveResultModel>.Fail("Chỉ SOP / hướng dẫn / quyết định cần duyệt – ghi chú khác lưu là dùng được");
            }
            if (n.Status != "nhap")
            {
                return Result<KnowledgeSaveResultModel>.Fail($"Chỉ gửi duyệt bản Nháp (đang «{n.Meta.GetValueOrDefault("status")}»)");
            }
            if (request.PrevHash != null && !string.Equals(request.PrevHash, n.Hash, StringComparison.OrdinalIgnoreCase))
            {
                return Status<KnowledgeSaveResultModel>(409, "Ghi chú đã được người khác sửa – tải lại rồi gửi duyệt");
            }
            string raw = File.ReadAllText(Path.Combine(Root, n.Path), Encoding.UTF8);
            var w = await WriteAsync(n.Path, SetMeta(raw, "status", "chờ duyệt"), n.Hash, caller.UserId, guardGenerated: true, note: "gửi duyệt SOP");
            if (w.ResponseCode != 200)
            {
                return w;
            }
            using var tran = await unitOfWork.OpenTransaction();
            var (req, error) = await Approvals.CreateAsync(SopBusinessType, KeyGuid(n.Path), JsonConvert.SerializeObject(new { path = n.Path, title = n.Title, note = request.Note }));
            if (error != null || req == null)
            {
                unitOfWork.RollbackTransaction();
                await WriteAsync(n.Path, raw, w.Data!.Hash, caller.UserId, guardGenerated: false, note: "hoàn tác gửi duyệt");
                return Result<KnowledgeSaveResultModel>.Fail(error ?? "Không gửi duyệt được");
            }
            unitOfWork.CommitTransaction();
            return Result<KnowledgeSaveResultModel>.Success(w.Data, $"Đã gửi duyệt ({req.RequestNo}) – mục Phê duyệt › Duyệt SOP");
        }

        /// <summary>Nội dung được duyệt: đường dẫn + mã băm tệp (sửa sau khi gửi → không duyệt được).</summary>
        public Task<string?> SopStateAsync(Guid key)
        {
            var n = All().FirstOrDefault(x => KeyGuid(x.Path) == key);
            return Task.FromResult(n == null ? null : JsonConvert.SerializeObject(new { n.Path, n.Hash, status = n.Status }));
        }

        public (string? Path, string? Title, string? Folder, string? Status) SopInfo(Guid key)
        {
            var n = All().FirstOrDefault(x => KeyGuid(x.Path) == key);
            return (n?.Path, n?.Title, n?.Folder, n?.Status);
        }

        /// <summary>Duyệt → Hiệu lực (bản Hiệu lực cũ cùng id → Lưu trữ); từ chối → Nháp. Người duyệt phải có quyền Sửa thư mục.</summary>
        public async Task<string?> ApplySopDecisionAsync(Guid key, bool approved, Guid actorUserId, string? note)
        {
            var notes = All();
            var n = notes.FirstOrDefault(x => KeyGuid(x.Path) == key);
            if (n == null)
            {
                return "Ghi chú không còn trong kho";
            }
            var rights = await RightsAsync(new KnowledgeCaller(actorUserId, null, null));
            if (!rights.CanEdit(n.Folder))
            {
                return $"Người duyệt cần quyền Sửa thư mục {FolderByCode(n.Folder)?.Dir}";
            }
            string raw = File.ReadAllText(Path.Combine(Root, n.Path), Encoding.UTF8);
            if (!approved)
            {
                var r = await WriteAsync(n.Path, SetMeta(raw, "status", "nháp"), n.Hash, actorUserId, guardGenerated: false, note: "từ chối duyệt: " + note);
                return r.ResponseCode == 200 ? null : r.ResponseMessage;
            }
            string today = clock.Today.ToString("yyyy-MM-dd");
            string text = SetMeta(raw, "status", "hiệu lực");
            if (n.ValidFrom is not { } vf || vf < clock.Today)
            {
                text = SetMeta(text, "valid_from", today);
            }
            var w = await WriteAsync(n.Path, text, n.Hash, actorUserId, guardGenerated: false, note: "duyệt SOP" + (string.IsNullOrWhiteSpace(note) ? string.Empty : ": " + note));
            if (w.ResponseCode != 200)
            {
                return w.ResponseMessage;
            }
            foreach (var old in notes.Where(x => x.Path != n.Path && string.Equals(x.Id, n.Id, StringComparison.OrdinalIgnoreCase) && x.Status == "hieu-luc"))
            {
                string o = File.ReadAllText(Path.Combine(Root, old.Path), Encoding.UTF8);
                await WriteAsync(old.Path, SetMeta(o, "status", "lưu trữ"), old.Hash, actorUserId, guardGenerated: false, note: "thay bằng " + n.Path);
            }
            return null;
        }

        /// <summary>Phiên bản mới của SOP đang Hiệu lực: tệp mới "… (bản N).md", cùng id, Nháp, người soạn.</summary>
        public async Task<IResult<KnowledgeSaveResultModel>> NewVersion(KnowledgePathRequest request)
        {
            var caller = WebCaller();
            var rights = await RightsAsync(caller);
            var notes = All();
            var n = notes.FirstOrDefault(x => string.Equals(x.Path, request.Path, StringComparison.OrdinalIgnoreCase));
            if (n == null || !rights.NoteVisible(n))
            {
                return Status<KnowledgeSaveResultModel>(404, "Không có ghi chú này hoặc không có quyền xem");
            }
            if (!rights.CanEdit(n.Folder) || n.Generated)
            {
                return Status<KnowledgeSaveResultModel>(403, "Không có quyền Sửa thư mục này / ghi chú tự sinh chỉ đọc");
            }
            if (notes.Any(x => string.Equals(x.Id, n.Id, StringComparison.OrdinalIgnoreCase) && x.Status is "nhap" or "cho-duyet"))
            {
                return Result<KnowledgeSaveResultModel>.Fail("Đã có phiên bản Nháp / Chờ duyệt của ghi chú này – sửa bản đó");
            }
            string dir = Path.GetDirectoryName(n.Path)!.Replace('\\', '/');
            string name = Regex.Replace(Path.GetFileNameWithoutExtension(n.Path), @"\s*\(bản \d+\)$", string.Empty);
            int v = notes.Count(x => string.Equals(x.Id, n.Id, StringComparison.OrdinalIgnoreCase)) + 1;
            string path = $"{dir}/{name} (bản {v}).md";
            string raw = File.ReadAllText(Path.Combine(Root, n.Path), Encoding.UTF8);
            string text = SetMeta(SetMeta(SetMeta(raw, "status", "nháp"), "source", HumanSource), "valid_from", clock.Today.ToString("yyyy-MM-dd"));
            return await WriteAsync(path, text, null, caller.UserId, guardGenerated: true, note: "phiên bản mới của " + n.Path);
        }

        /// <summary>Nhập Word (.docx) / PDF → markdown, thành ghi chú Nháp trong thư mục (có frontmatter chuẩn). Quét dữ liệu nhạy cảm như lưu tay.</summary>
        public async Task<IResult<KnowledgeSaveResultModel>> Import(string? folderCode, string? type, string fileName, Stream file)
        {
            var caller = WebCaller();
            var folder = FolderByCode(folderCode);
            if (folder == null || folder.AgentWritable)
            {
                return Result<KnowledgeSaveResultModel>.Fail("Chọn thư mục nghiệp vụ để nhập");
            }
            if (!(await RightsAsync(caller)).CanEdit(folder.Code))
            {
                return Status<KnowledgeSaveResultModel>(403, $"Không có quyền Sửa thư mục {folder.Dir}");
            }
            string ext = Path.GetExtension(fileName).ToLowerInvariant();
            string md;
            try
            {
                md = ext switch
                {
                    ".docx" or ".doc" => KnowledgeImport.WordToMarkdown(file),
                    ".pdf" => KnowledgeImport.PdfToMarkdown(file),
                    ".md" or ".txt" => await new StreamReader(file, Encoding.UTF8).ReadToEndAsync(),
                    _ => throw new InvalidDataException("Chỉ nhận .docx, .pdf, .md, .txt"),
                };
            }
            catch (InvalidDataException ex)
            {
                return Result<KnowledgeSaveResultModel>.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "[Knowledge] Import {File}", fileName);
                return Result<KnowledgeSaveResultModel>.Fail("Không đọc được tệp – lưu lại dạng .docx / .pdf có chữ (không phải ảnh quét) rồi thử lại");
            }
            string title = Path.GetFileNameWithoutExtension(fileName).Trim();
            string t = Types.Contains(Slug(type)) ? Slug(type) : "sop";
            string body = md.TrimStart().StartsWith("---", StringComparison.Ordinal) ? md : Frontmatter([("id", $"{t}-{FileSafe(title).ToLowerInvariant().Replace(' ', '-')}"), ("type", t), ("source", HumanSource), ("owner", folder.Owner),
                ("status", "nháp"), ("valid_from", clock.Today.ToString("yyyy-MM-dd")), ("review_by", clock.Today.AddMonths(12).ToString("yyyy-MM-dd")), ("confidentiality", folder.Code == Bod ? "bod" : "nội bộ"), ("refs", "[]")])
                + (md.Contains("\n# ", StringComparison.Ordinal) || md.StartsWith("# ", StringComparison.Ordinal) ? "\n" : $"\n# {title}\n\n") + md.Trim() + "\n";
            if (ScanSensitive(body) is { } pii)
            {
                return Result<KnowledgeSaveResultModel>.Fail($"Không nhập: {pii} – xóa dữ liệu nhạy cảm khỏi tệp rồi nhập lại (§7.5-8)");
            }
            string path = $"{folder.Dir}/{FileSafe(title)}.md";
            if (File.Exists(Path.Combine(Root, path)))
            {
                path = $"{folder.Dir}/{FileSafe(title)} {clock.Now:yyyyMMddHHmm}.md";
            }
            return await WriteAsync(path, body, null, caller.UserId, guardGenerated: true, note: "nhập " + ext);
        }

        // ---------------- việc agent theo dõi (98)
        private static readonly Regex TaskLine = new(@"^- (Hạn|Người phụ trách|Trạng thái):\s*(.*)$", RegexOptions.Multiline);

        public Task<IResults<KnowledgeTaskModel>> GetTasks(bool includeDone) => GetTasksCore(includeDone, mine: false);

        /// <summary>AI-22.4: người được giao (frontmatter assignee_id) thấy việc của mình kể cả không có quyền thư mục 98.</summary>
        private static bool AssignedTo(Note n, Guid? user) => user is { } u && Guid.TryParse(n.Meta.GetValueOrDefault("assignee_id"), out var a) && a == u;

        private async Task<IResults<KnowledgeTaskModel>> GetTasksCore(bool includeDone, bool mine)
        {
            var caller = WebCaller();
            var rights = await RightsAsync(caller);
            var list = All().Where(n => n.Folder == "98" && !IsTaskLedger(n.Path) && (mine ? AssignedTo(n, caller.UserId) : rights.NoteVisible(n) || AssignedTo(n, caller.UserId))).Select(n =>
            {
                var lines = TaskLine.Matches(n.Body).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value.Trim());
                string st = Slug(lines.GetValueOrDefault("Trạng thái") ?? "đang theo dõi");
                var parts = n.Path.Split('/');
                string body = string.Join("\n", n.Body.Split('\n').Where(l => !l.StartsWith("# ", StringComparison.Ordinal) && !TaskLine.IsMatch(l))).Trim();
                return new KnowledgeTaskModel
                {
                    Path = n.Path, Agent = parts.Length > 2 ? parts[1] : (n.Source.Replace("agent:", string.Empty).ToUpperInvariant()), Title = n.Title, Content = body,
                    CreatedAt = n.ValidFrom ?? DateTime.MinValue, Due = n.ReviewBy, Owner = lines.GetValueOrDefault("Người phụ trách"), Status = st == "xong" ? "xong" : "dang-theo-doi",
                    Overdue = st != "xong" && n.ReviewBy is { } d && d < clock.Today, Refs = n.Refs,
                };
            }).Where(t => includeDone || t.Status != "xong").OrderBy(t => t.Status == "xong").ThenBy(t => t.Due ?? DateTime.MaxValue).ToList();
            return new Results<KnowledgeTaskModel>(list, list.Count) { ResponseCode = 200, ResponseMessage = "OK" };
        }

        /// <summary>Đánh dấu Xong / mở lại / giao lại một việc agent theo dõi – sửa dòng "- Trạng thái" / "- Người phụ trách" của tệp 98, ghi lịch sử.</summary>
        public async Task<IResult<KnowledgeSaveResultModel>> SetTask(KnowledgeTaskSetRequest request)
        {
            var caller = WebCaller();
            var rights = await RightsAsync(caller);
            var n = All().FirstOrDefault(x => x.Folder == "98" && string.Equals(x.Path, request.Path, StringComparison.OrdinalIgnoreCase));
            if (n == null || !(rights.NoteVisible(n) || AssignedTo(n, caller.UserId))) // AI-22.4: người được giao bấm Xong việc của mình
            {
                return Status<KnowledgeSaveResultModel>(404, "Không có việc này");
            }
            string raw = File.ReadAllText(Path.Combine(Root, n.Path), Encoding.UTF8).Replace("\r\n", "\n");
            string Set(string text, string label, string value)
            {
                var rx = new Regex("^- " + Regex.Escape(label) + @":.*$", RegexOptions.Multiline);
                return rx.IsMatch(text) ? rx.Replace(text, $"- {label}: {value}", 1) : text.TrimEnd('\n') + $"\n- {label}: {value}\n";
            }
            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                raw = Set(raw, "Trạng thái", Slug(request.Status) == "xong" ? "xong" : "đang theo dõi");
            }
            if (!string.IsNullOrWhiteSpace(request.Owner))
            {
                if (ScanSensitive(request.Owner) is { } pii)
                {
                    return Result<KnowledgeSaveResultModel>.Fail($"Không lưu: {pii}");
                }
                raw = Set(raw, "Người phụ trách", request.Owner.Trim());
            }
            return await WriteAsync(n.Path, raw, n.Hash, caller.UserId, guardGenerated: false, note: "việc agent: " + (request.Note ?? request.Status ?? "giao lại"));
        }
    }
}
