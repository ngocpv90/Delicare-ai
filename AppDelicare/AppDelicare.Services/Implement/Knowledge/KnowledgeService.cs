using System.Text;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Option;
using AppDelicare.Services.Interface;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Note = AppDelicare.Services.Implement.Knowledge.KnowledgeVault.Note;

namespace AppDelicare.Services.Implement.Knowledge
{
    public sealed class KnowledgeFolderModel
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public bool CanEdit { get; set; }
        public int NoteCount { get; set; }
        public List<KnowledgeNoteHeadModel> ListNote { get; set; } = [];
    }

    public class KnowledgeNoteHeadModel
    {
        public string Path { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Folder { get; set; } = string.Empty;
        public string? Type { get; set; }
        public string? Status { get; set; }
        public string? Source { get; set; }
        public string? Confidentiality { get; set; }
        public DateTime? ValidFrom { get; set; }
        public DateTime? ReviewBy { get; set; }
        /// <summary>Quá hạn rà soát (§7.5-9) – agent trích dẫn phải gắn "cần rà soát".</summary>
        public bool NeedsReview { get; set; }
        /// <summary>Ghi chú tự sinh (source delicare-core) – chỉ đọc (§7.5-7).</summary>
        public bool ReadOnly { get; set; }
    }

    public sealed class KnowledgeNoteModel : KnowledgeNoteHeadModel
    {
        public Dictionary<string, string> Meta { get; set; } = [];
        public List<string> Refs { get; set; } = [];
        /// <summary>Nội dung (đã thay <c>{{BusinessRules:…}}</c> bằng giá trị cấu hình hiện tại).</summary>
        public string Body { get; set; } = string.Empty;
        /// <summary>Toàn văn gốc (cả frontmatter) – để sửa; agent không nhận.</summary>
        public string? Raw { get; set; }
        public string Hash { get; set; } = string.Empty;
        public bool CanEdit { get; set; }
        public List<KnowledgeNoteHeadModel> ListBacklink { get; set; } = [];
    }

    public sealed class KnowledgeHitModel : KnowledgeNoteHeadModel
    {
        public string Snippet { get; set; } = string.Empty;
        public int Score { get; set; }
    }

    public sealed class KnowledgeHistoryModel
    {
        public DateTime At { get; set; }
        public string? Action { get; set; }
        public Guid? By { get; set; }
        public string? Changes { get; set; }
    }

    public sealed class KnowledgeSaveRequest
    {
        public string? Path { get; set; }
        public string? Content { get; set; }
        /// <summary>Băm SHA-256 bản đang sửa (từ GetNote) – khác bản trên đĩa → 409 xung đột. Ghi chú mới: để trống.</summary>
        public string? PrevHash { get; set; }
    }

    public sealed class KnowledgeSearchRequest
    {
        public string? Keyword { get; set; }
        public string? Folder { get; set; }
        /// <summary>Gồm cả bản nháp / lưu trữ (câu hỏi lịch sử).</summary>
        public bool IncludeHistory { get; set; }
        public int? Max { get; set; }
    }

    public sealed class KnowledgeJournalRequest
    {
        /// <summary>nhat_ky (96) / tuong_tac (97) / cong_viec (98).</summary>
        public string? Kind { get; set; }
        public string? Content { get; set; }
        public string? Title { get; set; }
        public string? Due { get; set; }
        public string? Owner { get; set; }
        public string? Refs { get; set; }
    }

    public sealed class KnowledgeSaveResultModel
    {
        public string Path { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public bool Created { get; set; }
    }

    /// <summary>Người gọi kho: người dùng web (quyền menu <c>knowledge?folder=…</c>) hoặc agent (phạm vi §7.8 ∩ quyền người hỏi; KHÔNG BAO GIỜ BOD).</summary>
    public sealed record KnowledgeCaller(Guid? UserId, string? AgentCode, Guid? AskingUserId);

    /// <summary>
    /// AI-5 (D-133) – Kho tri thức (§7): file markdown trên <c>Knowledge:VaultPath</c>; chỉ đi qua service này (web, job, ToolGateway).
    /// Đọc theo quyền thư mục (menu <c>knowledge?folder=&lt;mã&gt;</c>, admin thấy hết); agent theo bảng phạm vi §7.8 ∩ quyền người hỏi, không bao giờ BOD /
    /// <c>confidentiality: BOD</c>, CSKH chỉ ghi chú công khai. Ghi (người): frontmatter bắt buộc, ghi chú tự sinh chỉ đọc, khóa theo file + kiểm băm (409),
    /// quét SĐT / CCCD / số tài khoản, SOP / quyết định hiệu lực phải qua Duyệt SOP (AI-12); agent chỉ ghi 96 / 97 / 98 bằng mẫu cố định.
    /// Mỗi lần ghi 1 dòng <c>AuditChange</c> (EntityName <c>KnowledgeNote</c>, IdChange = Guid từ đường dẫn).
    /// </summary>
    public interface IKnowledgeService
    {
        Task<IResults<KnowledgeFolderModel>> GetTree();
        Task<IResult<KnowledgeNoteModel>> GetNote(string? path);
        Task<IResults<KnowledgeHitModel>> Search(KnowledgeSearchRequest request);
        Task<IResults<KnowledgeNoteHeadModel>> GetBacklinks(string? path);
        Task<IResults<KnowledgeHistoryModel>> GetHistory(string? path);
        Task<IResult<KnowledgeSaveResultModel>> Save(KnowledgeSaveRequest request);

        Task<IResults<KnowledgeHitModel>> SearchFor(KnowledgeCaller caller, KnowledgeSearchRequest request);
        Task<IResult<KnowledgeNoteModel>> ReadFor(KnowledgeCaller caller, string? idOrPath);
        Task<IResult<KnowledgeSaveResultModel>> AppendJournal(string agentCode, KnowledgeJournalRequest request);

        /// <summary>Exporter (ghi chú tự sinh): ghi / cập nhật 1 ghi chú <c>source: delicare-core</c>; nội dung không đổi → 0 (chạy lại không nhân bản).</summary>
        Task<int> ExportNote(string path, string id, string type, string owner, DateTime validFrom, int reviewMonths, string confidentiality, IEnumerable<string> refs, string body);
        // AI-12 (D-133): duyệt SOP, phiên bản, nhập tệp, việc agent theo dõi, bảo trì kho.
        Task<IResult<KnowledgeSaveResultModel>> SubmitSop(KnowledgePathRequest request);
        Task<IResult<KnowledgeSaveResultModel>> NewVersion(KnowledgePathRequest request);
        Task<IResult<KnowledgeSaveResultModel>> Import(string? folderCode, string? type, string fileName, Stream file);
        Task<IResults<KnowledgeTaskModel>> GetTasks(bool includeDone);
        Task<IResult<KnowledgeSaveResultModel>> SetTask(KnowledgeTaskSetRequest request);
        Task<string?> SopStateAsync(Guid key);
        (string? Path, string? Title, string? Folder, string? Status) SopInfo(Guid key);
        Task<string?> ApplySopDecisionAsync(Guid key, bool approved, Guid actorUserId, string? note);
        Task<KnowledgeMaintenanceResult> MaintainAsync();
        void EnsureSkeleton();
    }

    public partial class KnowledgeService(IUnitOfWork unitOfWork, IClock clock, ICurrentActor currentActor, IPermissionService permissions,
        IOptions<KnowledgeOptions> options, IConfiguration configuration, IServiceProvider serviceProvider) : IKnowledgeService
    {
        public const string Entity = "KnowledgeNote";
        private readonly KnowledgeOptions _o = options.Value;
        private string Root => _o.VaultPath;

        private static Result<T> Status<T>(int code, string message) => new() { ResponseCode = code, ResponseMessage = message };

        private static Results<T> StatusList<T>(int code, string message) => new([], 0) { ResponseCode = code, ResponseMessage = message };

        // ---------------- quyền
        private KnowledgeCaller WebCaller()
        {
            var a = currentActor.RequireActor();
            return a.Type == ActorType.Agent ? new KnowledgeCaller(a.UserId, a.AgentCode, null) : new KnowledgeCaller(a.UserId, null, null);
        }

        private sealed record Rights(Func<string, bool> CanRead, Func<string, bool> CanEdit, Func<Note, bool> NoteVisible);

        private async Task<Rights> RightsAsync(KnowledgeCaller c)
        {
            UserPermissions? asking = c.AskingUserId is { } u ? await permissions.GetAsync(u) : null;
            if (c.AgentCode is { Length: > 0 } agent)
            {
                string code = agent.ToUpperInvariant();
                string[]? scope = KnowledgeVault.AgentScope(code);
                bool Read(string folder) => folder != KnowledgeVault.Bod
                    && (scope == null || scope.Contains(folder) || folder is "96" or "97" or "98")
                    && (asking == null || permissions.IsAllowed(asking, KnowledgeVault.FolderByCode(folder)!.Screen, PermissionAction.View));
                bool Visible(Note n)
                {
                    if (n.IsBod || !Read(n.Folder))
                    {
                        return false;
                    }
                    if (code == "CSKH" && n.Confidentiality != "cong-khai")
                    {
                        return false;
                    }
                    // 96–98: agent đọc nhật ký của chính mình; VIVI / KNOW đọc hết.
                    if (n.Folder is "96" or "97" or "98" && scope != null)
                    {
                        return n.Source == "agent:" + code.ToLowerInvariant() || n.Path.Split('/').ElementAtOrDefault(1)?.Equals(code, StringComparison.OrdinalIgnoreCase) == true;
                    }
                    return true;
                }
                return new Rights(Read, _ => false, Visible);
            }
            var me = c.UserId is { } id ? await permissions.GetAsync(id) : UserPermissions.None;
            // BOD: chỉ người được cấp RÕ mục "knowledge?folder=bod" (Ban GĐ) – quyền admin KHÔNG tự mở BOD (§7.2).
            bool Explicit(string folder, bool edit) => me.Menus.TryGetValue("#/" + KnowledgeVault.FolderByCode(folder)!.Screen, out var m) && m.Access && (!edit || m.Edit);
            bool R(string folder) => folder == KnowledgeVault.Bod ? Explicit(folder, false) : permissions.IsAllowed(me, KnowledgeVault.FolderByCode(folder)!.Screen, PermissionAction.View);
            bool E(string folder) => folder == KnowledgeVault.Bod ? Explicit(folder, true) : permissions.IsAllowed(me, KnowledgeVault.FolderByCode(folder)!.Screen, PermissionAction.Edit);
            return new Rights(R, E, n => n.Folder.Length > 0 && R(n.Folder) && (n.Confidentiality != "bod" || R(KnowledgeVault.Bod)));
        }

        // ---------------- khung kho
        public void EnsureSkeleton()
        {
            foreach (var f in KnowledgeVault.Folders)
            {
                Directory.CreateDirectory(Path.Combine(Root, f.Dir));
            }
            string tplDir = Path.Combine(Root, "99 Mẫu");
            foreach (var (name, type, title, hint) in Templates)
            {
                string file = Path.Combine(tplDir, name + ".md");
                if (File.Exists(file))
                {
                    continue;
                }
                string fm = KnowledgeVault.Frontmatter([
                    ("id", "mau-" + type), ("type", type), ("source", KnowledgeVault.HumanSource), ("owner", "Admin"), ("status", "hiệu lực"),
                    ("valid_from", "2026-11-27"), ("review_by", "2027-11-27"), ("confidentiality", "nội bộ"), ("refs", "[]"), ("title", title)]);
                KnowledgeVault.WriteAtomic(file, fm + $"\n# {title}\n\n{hint}\n");
            }
        }

        private static readonly (string Name, string Type, string Title, string Hint)[] Templates =
        [
            ("Mẫu SOP", "sop", "Mẫu SOP", "## Mục đích\n\n## Phạm vi\n\n## Các bước\n1. …\n\n## Ngưỡng (dẫn mã cấu hình, không viết cứng)\nVí dụ: duyệt PO khi vượt {{BusinessRules:PurchaseOrderDirectorApprovalAmount}}.\n\n## Hồ sơ / biểu mẫu\n"),
            ("Mẫu sự cố", "su-co", "Mẫu hồ sơ sự cố", "## Diễn biến\n\n## Nguyên nhân\n\n## Xử lý\n\n## Chứng từ liên quan\n(mã chứng từ ghi vào refs)\n"),
            ("Mẫu bài học", "bai-hoc", "Mẫu bài học", "## Bối cảnh\n\n## Bài học\n\n## Áp dụng\n"),
            ("Mẫu báo cáo", "bao-cao", "Mẫu báo cáo", "## Số liệu chính (bản chụp có ngày – số hiện tại luôn hỏi lõi)\n\n## Nhận xét\n"),
            ("Mẫu quyết định", "quyet-dinh", "Mẫu quyết định", "## Quyết định\n\n## Người chốt / thời điểm\n\n## Lý do\n"),
            ("Mẫu hướng dẫn", "huong-dan", "Mẫu hướng dẫn đào tạo", "## Dành cho vai trò\n\n## Các bước\n\n## Câu hỏi thường gặp\n"),
        ];

        // ---------------- đọc
        private IReadOnlyList<Note> All()
        {
            EnsureSkeleton();
            return KnowledgeVault.Scan(Root);
        }

        private KnowledgeNoteHeadModel Head(Note n, KnowledgeNoteHeadModel? into = null)
        {
            var h = into ?? new KnowledgeNoteHeadModel();
            h.Path = n.Path; h.Id = n.Id; h.Title = n.Title; h.Folder = n.Folder; h.Type = n.Meta.GetValueOrDefault("type"); h.Status = n.Meta.GetValueOrDefault("status");
            h.Source = n.Meta.GetValueOrDefault("source"); h.Confidentiality = n.Meta.GetValueOrDefault("confidentiality"); h.ValidFrom = n.ValidFrom; h.ReviewBy = n.ReviewBy;
            h.NeedsReview = n.ReviewBy is { } r && r < clock.Today; h.ReadOnly = n.Generated;
            return h;
        }

        public async Task<IResults<KnowledgeFolderModel>> GetTree()
        {
            var rights = await RightsAsync(WebCaller());
            var notes = All();
            var list = KnowledgeVault.Folders.Where(f => rights.CanRead(f.Code)).Select(f =>
            {
                var items = notes.Where(n => n.Folder == f.Code && rights.NoteVisible(n)).OrderBy(n => n.Path, StringComparer.OrdinalIgnoreCase).Select(n => Head(n)).ToList();
                return new KnowledgeFolderModel { Code = f.Code, Name = f.Dir, Owner = f.Owner, CanEdit = rights.CanEdit(f.Code) && !f.AgentWritable, NoteCount = items.Count, ListNote = items };
            }).ToList();
            return Results<KnowledgeFolderModel>.Success(list, "OK");
        }

        public Task<IResult<KnowledgeNoteModel>> GetNote(string? path) => ReadFor(WebCaller(), path, includeRaw: true);

        public Task<IResult<KnowledgeNoteModel>> ReadFor(KnowledgeCaller caller, string? idOrPath) => ReadFor(caller, idOrPath, includeRaw: caller.AgentCode == null);

        private async Task<IResult<KnowledgeNoteModel>> ReadFor(KnowledgeCaller caller, string? idOrPath, bool includeRaw)
        {
            var rights = await RightsAsync(caller);
            var notes = All();
            string key = (idOrPath ?? string.Empty).Replace('\\', '/').Trim();
            var n = notes.FirstOrDefault(x => string.Equals(x.Path, key, StringComparison.OrdinalIgnoreCase) || string.Equals(x.Path, key + ".md", StringComparison.OrdinalIgnoreCase))
                ?? notes.Where(x => string.Equals(x.Id, key, StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.Status == "hieu-luc" ? 0 : x.Status == "luu-tru" ? 2 : 1).FirstOrDefault(); // AI-12: nhiều phiên bản cùng id → bản Hiệu lực
            if (n == null || !rights.NoteVisible(n))
            {
                return Status<KnowledgeNoteModel>(404, "Không có ghi chú này hoặc không có quyền xem");
            }
            var m = (KnowledgeNoteModel)Head(n, new KnowledgeNoteModel());
            m.Meta = new Dictionary<string, string>(n.Meta);
            m.Refs = n.Refs;
            m.Body = ApplyRules(n.Body);
            m.Hash = n.Hash;
            m.CanEdit = caller.AgentCode == null && !n.Generated && rights.CanEdit(n.Folder) && !(KnowledgeVault.FolderByCode(n.Folder)?.AgentWritable ?? false);
            if (includeRaw)
            {
                m.Raw = File.ReadAllText(Path.Combine(Root, n.Path), Encoding.UTF8);
            }
            m.ListBacklink = Backlinks(notes, n).Where(rights.NoteVisible).Select(x => Head(x)).ToList();
            return Result<KnowledgeNoteModel>.Success(m, "OK");
        }

        /// <summary>§7.5-2: <c>{{BusinessRules:X}}</c> → giá trị cấu hình hiện tại (CHỈ nhánh BusinessRules – không lộ cấu hình khác).</summary>
        public string ApplyRules(string body) => KnowledgeVault.RuleRx().Replace(body, m => configuration[m.Groups[1].Value] is { Length: > 0 } v ? v : m.Value + " (chưa cấu hình)");

        private static IEnumerable<Note> Backlinks(IEnumerable<Note> notes, Note target)
        {
            string name = Path.GetFileNameWithoutExtension(target.Path);
            return notes.Where(x => x.Path != target.Path && KnowledgeVault.LinkRx().Matches(x.Body).Any(m =>
            {
                string link = m.Groups[1].Value.Trim();
                return link.Equals(name, StringComparison.OrdinalIgnoreCase) || link.Equals(target.Title, StringComparison.OrdinalIgnoreCase)
                    || link.Equals(target.Id, StringComparison.OrdinalIgnoreCase) || (target.Path.StartsWith(link, StringComparison.OrdinalIgnoreCase) && target.Path.Length == link.Length + 3);
            }));
        }

        public async Task<IResults<KnowledgeNoteHeadModel>> GetBacklinks(string? path)
        {
            var rights = await RightsAsync(WebCaller());
            var notes = All();
            var n = notes.FirstOrDefault(x => string.Equals(x.Path, path?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (n == null || !rights.NoteVisible(n))
            {
                return StatusList<KnowledgeNoteHeadModel>(404, "Không có ghi chú này hoặc không có quyền xem");
            }
            return Results<KnowledgeNoteHeadModel>.Success(Backlinks(notes, n).Where(rights.NoteVisible).Select(x => Head(x)).ToList(), "OK");
        }

        public async Task<IResults<KnowledgeHistoryModel>> GetHistory(string? path)
        {
            var rights = await RightsAsync(WebCaller());
            string? p = KnowledgeVault.NormalizePath(path, out string? err);
            var folder = p == null ? null : KnowledgeVault.FolderOfPath(p);
            if (p == null || folder == null || !rights.CanRead(folder.Code))
            {
                return StatusList<KnowledgeHistoryModel>(404, err ?? "Không có quyền xem");
            }
            Guid key = KnowledgeVault.KeyGuid(p);
            var rows = unitOfWork.DbContext.AuditChange.Where(a => a.EntityName == Entity && a.IdChange == key && !a.IsDeleted).OrderByDescending(a => a.CreatedDate)
                .Select(a => new KnowledgeHistoryModel { At = a.CreatedDate, Action = a.Action, By = a.CreatedBy, Changes = a.Changes }).ToList();
            return Results<KnowledgeHistoryModel>.Success(rows, "OK");
        }

        // ---------------- tìm (§7.3, §7.8)
        public async Task<IResults<KnowledgeHitModel>> Search(KnowledgeSearchRequest request) => await SearchFor(WebCaller(), request);

        public async Task<IResults<KnowledgeHitModel>> SearchFor(KnowledgeCaller caller, KnowledgeSearchRequest request)
        {
            string[] tokens = KnowledgeVault.Tokens(request.Keyword);
            if (tokens.Length == 0)
            {
                return StatusList<KnowledgeHitModel>(400, "Nhập từ khóa cần tìm");
            }
            var rights = await RightsAsync(caller);
            var folder = request.Folder is { Length: > 0 } fc ? KnowledgeVault.FolderByCode(fc) ?? KnowledgeVault.Folders.FirstOrDefault(f => f.Dir.Equals(fc, StringComparison.OrdinalIgnoreCase)) : null;
            int max = Math.Clamp(request.Max ?? (caller.AgentCode != null ? _o.MaxNotesPerQuery : 20), 1, caller.AgentCode != null ? _o.MaxNotesPerQuery : 100);
            var hits = new List<(Note N, int Score, int Rank)>();
            foreach (var n in All())
            {
                if (!rights.NoteVisible(n) || (folder != null && n.Folder != folder.Code))
                {
                    continue;
                }
                if (!request.IncludeHistory && n.Status is "nhap" or "luu-tru")
                {
                    continue; // §7.8-1
                }
                int score = 0, matched = 0;
                foreach (string t in tokens)
                {
                    int inTitle = Count(n.FoldedTitle, t), inBody = Count(n.FoldedBody, t);
                    if (inTitle + inBody > 0)
                    {
                        matched++;
                        score += inTitle * 5 + Math.Min(inBody, 10);
                    }
                }
                if (matched * 2 < tokens.Length || score == 0)
                {
                    continue;
                }
                if (matched == tokens.Length)
                {
                    score += 10;
                }
                // §7.8-2: SOP hiệu lực → ghi chú tự sinh / người viết → ghi chú agent
                int rank = n.Type == "sop" && n.Status == "hieu-luc" ? 0 : n.Source.StartsWith("agent:", StringComparison.Ordinal) ? 2 : 1;
                hits.Add((n, score, rank));
            }
            var list = hits.OrderBy(h => h.Rank).ThenByDescending(h => h.Score).ThenByDescending(h => h.N.ValidFrom).Take(max).Select(h =>
            {
                var m = (KnowledgeHitModel)Head(h.N, new KnowledgeHitModel());
                m.Score = h.Score;
                m.Snippet = ApplyRules(Snippet(h.N, tokens, _o.SnippetLength));
                return m;
            }).ToList();
            return Results<KnowledgeHitModel>.Success(list, list.Count == 0 ? "Kho chưa có ghi chú phù hợp" : "OK");
        }

        private static int Count(string hay, string token)
        {
            int n = 0, i = 0;
            while ((i = hay.IndexOf(token, i, StringComparison.Ordinal)) >= 0)
            {
                n++;
                i += token.Length;
            }
            return n;
        }

        private static string Snippet(Note n, string[] tokens, int length)
        {
            int at = tokens.Select(t => n.FoldedBody.IndexOf(t, StringComparison.Ordinal)).Where(i => i >= 0).DefaultIfEmpty(0).Min();
            int start = Math.Max(0, at - length / 4);
            int len = Math.Min(length, n.Body.Length - start);
            string s = n.Body.Substring(start, Math.Max(0, len)).Trim();
            return (start > 0 ? "…" : string.Empty) + s + (start + len < n.Body.Length ? "…" : string.Empty);
        }

        // ---------------- ghi (người)
        public async Task<IResult<KnowledgeSaveResultModel>> Save(KnowledgeSaveRequest request)
        {
            var caller = WebCaller();
            string? path = KnowledgeVault.NormalizePath(request.Path, out string? err);
            if (path == null)
            {
                return Result<KnowledgeSaveResultModel>.Fail(err!);
            }
            var folder = KnowledgeVault.FolderOfPath(path)!;
            if (caller.AgentCode != null)
            {
                return Status<KnowledgeSaveResultModel>(403, $"Agent chỉ ghi trực tiếp vào 96 / 97 / 98 bằng ghi_nhat_ky; thư mục {folder.Dir} phải qua đề xuất (propose_note_update)");
            }
            var rights = await RightsAsync(caller);
            if (!rights.CanEdit(folder.Code))
            {
                return Status<KnowledgeSaveResultModel>(403, $"Không có quyền Sửa thư mục {folder.Dir}");
            }
            if (folder.AgentWritable)
            {
                return Status<KnowledgeSaveResultModel>(403, $"{folder.Dir} do agent ghi (theo mẫu) – không sửa tay");
            }
            string content = (request.Content ?? string.Empty).Replace("\r\n", "\n");
            var note = KnowledgeVault.Parse(path, content);
            if (KnowledgeVault.ValidateFrontmatter(note) is { } fmErr)
            {
                return Result<KnowledgeSaveResultModel>.Fail(fmErr);
            }
            if (note.Source != KnowledgeVault.HumanSource)
            {
                return Result<KnowledgeSaveResultModel>.Fail("Ghi chú soạn trên web phải có source: human");
            }
            if (note.Type is "sop" or "quyet-dinh" && note.Status == "hieu-luc")
            {
                return Result<KnowledgeSaveResultModel>.Fail("SOP / quyết định chỉ có hiệu lực sau khi duyệt (mục Duyệt SOP) – lưu ở trạng thái nháp hoặc chờ duyệt");
            }
            if (KnowledgeVault.ScanSensitive(content) is { } pii)
            {
                return Result<KnowledgeSaveResultModel>.Fail($"Không lưu: {pii} – kho không chứa dữ liệu cá nhân nhạy cảm / bí mật (§7.5-8)");
            }
            if (note.Confidentiality == "bod" && folder.Code != KnowledgeVault.Bod)
            {
                return Result<KnowledgeSaveResultModel>.Fail("Ghi chú mật BOD phải nằm trong thư mục BOD Bảo mật");
            }
            return await WriteAsync(path, content, request.PrevHash, caller.UserId, guardGenerated: true);
        }

        private async Task<IResult<KnowledgeSaveResultModel>> WriteAsync(string path, string content, string? prevHash, Guid? by, bool guardGenerated, string? note = null)
        {
            EnsureSkeleton();
            string full = Path.Combine(Root, path);
            var gate = KnowledgeVault.LockFor(Root, path);
            await gate.WaitAsync();
            try
            {
                bool exists = File.Exists(full);
                string? current = exists ? File.ReadAllText(full, Encoding.UTF8) : null;
                string? currentHash = current == null ? null : KnowledgeVault.Sha256(current);
                if (exists && guardGenerated && KnowledgeVault.Parse(path, current!).Generated)
                {
                    return Status<KnowledgeSaveResultModel>(403, "Ghi chú tự sinh (delicare-core) chỉ đọc – viết ghi chú riêng và liên kết [[…]] tới nó");
                }
                if (prevHash != null && !string.Equals(prevHash, currentHash ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    return Status<KnowledgeSaveResultModel>(409, "Ghi chú đã được người khác sửa sau khi anh/chị mở – tải lại để xem bản mới rồi sửa tiếp");
                }
                if (prevHash == null && exists && guardGenerated)
                {
                    return Status<KnowledgeSaveResultModel>(409, "Ghi chú đã tồn tại – mở ghi chú rồi sửa (cần mã băm bản đang sửa)");
                }
                string hash = KnowledgeVault.Sha256(content);
                if (hash == currentHash)
                {
                    return Result<KnowledgeSaveResultModel>.Success(new KnowledgeSaveResultModel { Path = path, Hash = hash }, "Không có thay đổi");
                }
                KnowledgeVault.WriteAtomic(full, content);
                Audit(path, exists ? "Updated" : "Created", currentHash, hash, by, note);
                await unitOfWork.DbContext.SaveChangesAsync();
                return Result<KnowledgeSaveResultModel>.Success(new KnowledgeSaveResultModel { Path = path, Hash = hash, Created = !exists }, "Đã lưu");
            }
            finally
            {
                gate.Release();
            }
        }

        private void Audit(string path, string action, string? prevSha, string sha, Guid? by, string? note)
        {
            var actor = currentActor.Actor;
            Guid? who = by ?? actor?.UserId;
            unitOfWork.DbContext.AuditChange.Add(new AuditChange
            {
                Id = Guid.NewGuid(), IdChange = KnowledgeVault.KeyGuid(path), EntityName = Entity, Action = action,
                Changes = JsonConvert.SerializeObject(new { path, sha256 = sha, prevSha, actor = actor?.ToString(), correlationId = currentActor.CorrelationId, note }),
                CreatedDate = clock.Now, CreatedBy = who, IsDeleted = false,
            });
        }

        // ---------------- ghi (agent – 96 / 97 / 98, mẫu cố định)
        public async Task<IResult<KnowledgeSaveResultModel>> AppendJournal(string agentCode, KnowledgeJournalRequest request)
        {
            string code = (agentCode ?? string.Empty).Trim().ToUpperInvariant();
            string kind = KnowledgeVault.Slug(request.Kind);
            string content = (request.Content ?? string.Empty).Replace("\r", string.Empty).Trim();
            if (code.Length == 0 || content.Length == 0)
            {
                return Result<KnowledgeSaveResultModel>.Fail("Thiếu agent hoặc nội dung");
            }
            if (content.Length > 2000)
            {
                return Result<KnowledgeSaveResultModel>.Fail("Nội dung tối đa 2.000 ký tự – ghi ngắn: việc, kết quả, mã chứng từ");
            }
            if (KnowledgeVault.ScanSensitive(content + " " + request.Title + " " + request.Owner) is { } pii)
            {
                return Result<KnowledgeSaveResultModel>.Fail($"Không lưu: {pii} (§7.5-8)");
            }
            // nội dung là DỮ LIỆU: bỏ dòng frontmatter / tiêu đề cấp 1 để không phá mẫu
            content = string.Join("\n", content.Split('\n').Select(l => l.TrimEnd()).Where(l => !l.StartsWith("---", StringComparison.Ordinal)).Select(l => l.StartsWith("# ", StringComparison.Ordinal) ? "## " + l[2..] : l));
            DateTime now = clock.Now;
            string source = "agent:" + code.ToLowerInvariant();
            string refs = "[" + string.Join(", ", (request.Refs ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(KnowledgeVault.FileSafe)) + "]";
            Guid? by = currentActor.Actor?.UserId;
            switch (kind)
            {
                case "nhat-ky":
                {
                    string path = $"96 Nhật ký/{code}/{now:yyyy-MM-dd}.md";
                    var gate = KnowledgeVault.LockFor(Root, path);
                    string full = Path.Combine(Root, path);
                    string text;
                    await gate.WaitAsync();
                    try
                    {
                        text = File.Exists(full) ? File.ReadAllText(full, Encoding.UTF8)
                            : KnowledgeVault.Frontmatter([("id", $"nk-{code.ToLowerInvariant()}-{now:yyyyMMdd}"), ("type", "nhat-ky"), ("source", source), ("owner", "KNOW"), ("status", "hiệu lực"),
                                ("valid_from", now.ToString("yyyy-MM-dd")), ("review_by", now.AddDays(90).ToString("yyyy-MM-dd")), ("confidentiality", "nội bộ"), ("refs", "[]")])
                              + $"\n# Nhật ký {code} {now:dd/MM/yyyy}\n\n";
                        text = text.TrimEnd('\n') + "\n" + $"- {now:HH:mm} · {content.Replace("\n", " ")}" + (refs != "[]" ? $" ({refs.Trim('[', ']')})" : string.Empty) + "\n";
                    }
                    finally
                    {
                        gate.Release();
                    }
                    return await WriteAsync(path, text, null, by, guardGenerated: false, note: "ghi_nhat_ky");
                }
                case "tuong-tac":
                {
                    string title = string.IsNullOrWhiteSpace(request.Title) ? "Quyết định / cam kết" : request.Title.Trim();
                    string path = $"97 Tương tác/{now:yyyy-MM}/{now:yyyyMMdd-HHmmss}-{code} {KnowledgeVault.FileSafe(title)}.md";
                    string text = KnowledgeVault.Frontmatter([("id", $"tt-{code.ToLowerInvariant()}-{now:yyyyMMddHHmmss}"), ("type", "quyet-dinh"), ("source", source), ("owner", "KNOW"), ("status", "hiệu lực"),
                            ("valid_from", now.ToString("yyyy-MM-dd")), ("review_by", now.AddMonths(12).ToString("yyyy-MM-dd")), ("confidentiality", "nội bộ"), ("refs", refs)])
                        + $"\n# {title}\n\n{content}\n\n- Người chốt: {request.Owner?.Trim() ?? "(chưa ghi)"}\n- Thời điểm: {now:dd/MM/yyyy HH:mm}\n- Agent ghi: {code}\n";
                    return await WriteAsync(path, text, null, by, guardGenerated: false, note: "ghi_nhat_ky");
                }
                case "cong-viec":
                {
                    string title = string.IsNullOrWhiteSpace(request.Title) ? content.Split('\n')[0] : request.Title.Trim();
                    string path = $"98 Công việc/{code}/{now:yyyyMMdd-HHmmss} {KnowledgeVault.FileSafe(title)}.md";
                    string text = KnowledgeVault.Frontmatter([("id", $"cv-{code.ToLowerInvariant()}-{now:yyyyMMddHHmmss}"), ("type", "nhat-ky"), ("source", source), ("owner", "KNOW"), ("status", "hiệu lực"),
                            ("valid_from", now.ToString("yyyy-MM-dd")), ("review_by", (KnowledgeVault.ParseDate(request.Due) ?? now.AddDays(30)).ToString("yyyy-MM-dd")), ("confidentiality", "nội bộ"), ("refs", refs)])
                        + $"\n# {title}\n\n{content}\n\n- Hạn: {request.Due?.Trim() ?? "(chưa ghi)"}\n- Người phụ trách: {request.Owner?.Trim() ?? "(chưa ghi)"}\n- Trạng thái: đang theo dõi\n";
                    return await WriteAsync(path, text, null, by, guardGenerated: false, note: "ghi_nhat_ky");
                }
                default:
                    return Status<KnowledgeSaveResultModel>(403, "Agent chỉ ghi được nhat_ky (96), tuong_tac (97), cong_viec (98)");
            }
        }

        // ---------------- exporter (ghi chú tự sinh)
        public async Task<int> ExportNote(string path, string id, string type, string owner, DateTime validFrom, int reviewMonths, string confidentiality, IEnumerable<string> refs, string body)
        {
            string? p = KnowledgeVault.NormalizePath(path, out string? err) ?? throw new ArgumentException(err);
            if (KnowledgeVault.FolderOfPath(p)!.Code == KnowledgeVault.Bod)
            {
                throw new InvalidOperationException("Exporter không bao giờ ghi vào BOD Bảo mật");
            }
            string text = KnowledgeVault.Frontmatter([("id", id), ("type", type), ("source", KnowledgeVault.CoreSource), ("owner", owner), ("status", "hiệu lực"),
                ("valid_from", validFrom.ToString("yyyy-MM-dd")), ("review_by", validFrom.AddMonths(reviewMonths).ToString("yyyy-MM-dd")), ("confidentiality", confidentiality),
                ("refs", "[" + string.Join(", ", refs.Where(r => !string.IsNullOrWhiteSpace(r)).Select(KnowledgeVault.FileSafe)) + "]")]) + "\n" + body.Replace("\r\n", "\n").TrimEnd() + "\n";
            string full = Path.Combine(Root, p);
            if (File.Exists(full) && KnowledgeVault.Sha256(File.ReadAllText(full, Encoding.UTF8)) == KnowledgeVault.Sha256(text))
            {
                return 0;
            }
            var r = await WriteAsync(p, text, null, null, guardGenerated: false, note: "export");
            return r.ResponseCode == 200 ? 1 : 0;
        }
    }
}
