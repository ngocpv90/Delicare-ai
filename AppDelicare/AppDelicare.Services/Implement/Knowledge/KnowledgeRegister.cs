using System.Text;
using System.Text.RegularExpressions;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Model;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Services.Interface;
using AppDelicare.Services.Jobs;
using Microsoft.EntityFrameworkCore;
using static AppDelicare.Services.Implement.Knowledge.KnowledgeVault;

namespace AppDelicare.Services.Implement.Knowledge
{
    /// <summary>AI-22.4: 1 dòng sổ công văn / biên bản họp.</summary>
    public sealed class KnowledgeRegisterItemModel
    {
        public string Path { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Type { get; set; }
        /// <summary>Số vào sổ do máy chủ cấp (VD-2026-0001 / VI-2026-0001 / BH-2026-001).</summary>
        public string? RegisterNo { get; set; }
        public DateTime? Date { get; set; }
        /// <summary>Số / ký hiệu của văn bản gốc.</summary>
        public string? SoHieu { get; set; }
        public string? Party { get; set; }
        public string? Handler { get; set; }
        public DateTime? Due { get; set; }
        /// <summary>dang-theo-doi / xong / (trống – không giao việc).</summary>
        public string? TaskStatus { get; set; }
        public int TaskCount { get; set; }
        public int TaskDone { get; set; }
        public bool Overdue { get; set; }
        public bool HasFile { get; set; }
    }

    public sealed class KnowledgeAssigneeModel
    {
        public Guid Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? FullName { get; set; }
    }

    public sealed class KnowledgeMeetingTask
    {
        public string? Task { get; set; }
        public Guid? AssigneeId { get; set; }
        public string? Due { get; set; }
    }

    /// <summary>AI-22.4: biên bản họp giao ban (mẫu 99) – mỗi việc giao thành việc theo dõi 98.</summary>
    public sealed class KnowledgeMeetingRequest
    {
        public string? Title { get; set; }
        public string? Date { get; set; }
        public string? Chair { get; set; }
        public string? Attendees { get; set; }
        public string? Content { get; set; }
        public string? Conclusion { get; set; }
        public List<KnowledgeMeetingTask> Tasks { get; set; } = [];
    }

    /// <summary>
    /// AI-22.4 (D-141 i, §7.9 tính năng đi kèm): <b>Sổ công văn đến / đi</b> (vùng 05) – số vào sổ máy chủ tự cấp theo năm <c>VD-YYYY-NNNN</c> (đến) / <c>VI-YYYY-NNNN</c> (đi),
    /// đếm từ phiếu mô tả trong kho (không bảng đếm – Quyết định tự chọn QĐ-AI224-1, tiền tố đăng ký BUSINESS_RULES §11), khóa theo vùng chống trùng;
    /// giao người xử lý + hạn → việc theo dõi 98 (<c>98 Công việc/CONG-VAN</c>). <b>Biên bản họp</b> (vùng 15, mẫu 99) số <c>BH-YYYY-NNN</c>, mỗi việc giao → việc 98
    /// (<c>98 Công việc/HOP</c>). Người được giao thấy / bấm Xong việc của mình kể cả không có quyền thư mục 98; job 07:00 nhắc việc sắp / quá hạn qua bot.
    /// </summary>
    public partial class KnowledgeService
    {
        public const string DispatchFolder = "05";
        public const string MeetingFolder = "15";
        private static readonly Regex NoRx = new(@"^(VD|VI|BH)-(\d{4})-(\d+)$", RegexOptions.Compiled);

        /// <summary>Số kế tiếp trong năm, đếm từ frontmatter <c>so_vao_so</c> của các phiếu trong vùng (gọi trong khóa vùng).</summary>
        private string NextRegisterNo(string prefix, int year, int digits)
        {
            int max = All().Where(n => n.Folder is DispatchFolder or MeetingFolder).Select(n => NoRx.Match(n.Meta.GetValueOrDefault("so_vao_so") ?? string.Empty))
                .Where(m => m.Success && m.Groups[1].Value == prefix && m.Groups[2].Value == year.ToString()).Select(m => int.Parse(m.Groups[3].Value)).DefaultIfEmpty(0).Max();
            return $"{prefix}-{year}-{(max + 1).ToString(new string('0', digits))}";
        }

        private async Task<string?> UserNameAsync(Guid? id) => id is { } u
            ? await unitOfWork.DbContext.User.AsNoTracking().Where(x => x.Id == u && x.IsActive && !x.IsDeleted).Select(x => x.UserName).FirstOrDefaultAsync()
            : null;

        /// <summary>Tạo việc theo dõi 98 cho người được giao (frontmatter assignee_id – người đó thấy / bấm Xong việc của mình).</summary>
        private async Task<string?> CreateFollowTaskAsync(string group, string title, string body, Guid assignee, string assigneeName, DateTime due, string refPath, Guid? by)
        {
            DateTime now = clock.Now;
            string path = $"98 Công việc/{group}/{now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..4]} {FileSafe(title.Length > 80 ? title[..80] : title)}.md";
            string text = Frontmatter([("id", $"cv-{group.ToLowerInvariant()}-{now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..4]}"), ("type", "nhat-ky"), ("source", HumanSource), ("owner", assigneeName),
                    ("status", "hiệu lực"), ("valid_from", now.ToString("yyyy-MM-dd")), ("review_by", due.ToString("yyyy-MM-dd")), ("confidentiality", "nội bộ"), ("refs", "[]"),
                    ("assignee_id", assignee.ToString()), ("nguon", refPath)])
                + $"\n# {title}\n\n{body}\n\nNguồn: [[{Path.GetFileNameWithoutExtension(refPath)}]]\n\n- Hạn: {due:yyyy-MM-dd}\n- Người phụ trách: {assigneeName}\n- Trạng thái: đang theo dõi\n";
            var r = await WriteAsync(path, text, null, by, guardGenerated: false, note: "giao việc");
            return r.ResponseCode == 200 ? path : null;
        }

        /// <summary>Người dùng nội bộ đang hoạt động để chọn người xử lý / người được giao – chỉ người có quyền Sửa vùng 05 hoặc 15.</summary>
        public async Task<IResults<KnowledgeAssigneeModel>> GetAssignees(string? keyword)
        {
            var rights = await RightsAsync(WebCaller());
            if (!rights.CanEdit(DispatchFolder) && !rights.CanEdit(MeetingFolder))
            {
                return StatusList<KnowledgeAssigneeModel>(403, "Cần quyền Sửa sổ công văn hoặc biên bản họp");
            }
            string k = (keyword ?? string.Empty).Trim();
            var db = unitOfWork.DbContext;
            var q = from u in db.User.AsNoTracking()
                    where u.IsActive && !u.IsDeleted && u.AccountType == "INTERNAL" && !u.UserName!.StartsWith("agent.")
                    join p in db.Person.AsNoTracking() on u.PersonId equals p.Id into pp
                    from p in pp.DefaultIfEmpty()
                    select new KnowledgeAssigneeModel { Id = u.Id, UserName = u.UserName!, FullName = p != null ? p.FullName : null };
            if (k.Length > 0)
            {
                q = q.Where(x => x.UserName.Contains(k) || (x.FullName != null && x.FullName.Contains(k)));
            }
            var list = await q.OrderBy(x => x.UserName).Take(30).ToListAsync();
            return Results<KnowledgeAssigneeModel>.Success(list, "OK");
        }

        /// <summary>Sổ công văn (05) hoặc biên bản họp (15) – theo quyền vùng; kèm trạng thái việc theo dõi đã giao.</summary>
        public async Task<IResults<KnowledgeRegisterItemModel>> GetRegister(string? folder, int? year)
        {
            string code = folder == MeetingFolder ? MeetingFolder : DispatchFolder;
            var rights = await RightsAsync(WebCaller());
            if (!rights.CanRead(code))
            {
                return StatusList<KnowledgeRegisterItemModel>(403, "Không có quyền xem vùng này");
            }
            var notes = All();
            var tasks = notes.Where(n => n.Folder == "98" && n.Meta.ContainsKey("nguon")).ToLookup(n => n.Meta["nguon"], StringComparer.OrdinalIgnoreCase);
            var list = notes.Where(n => n.Folder == code && rights.NoteVisible(n) && n.Status != "luu-tru" && n.Meta.ContainsKey("so_vao_so"))
                .Select(n =>
                {
                    var ts = tasks[n.Path].Select(t => (Done: Slug(Regex.Match(t.Body, @"^- Trạng thái:\s*(.*)$", RegexOptions.Multiline).Groups[1].Value) == "xong", Due: t.ReviewBy)).ToList();
                    return new KnowledgeRegisterItemModel
                    {
                        Path = n.Path, Title = n.Title, Type = n.Type, RegisterNo = n.Meta.GetValueOrDefault("so_vao_so"), Date = ParseDate(n.Meta.GetValueOrDefault("ngay")),
                        SoHieu = n.Meta.GetValueOrDefault("so_hieu"), Party = n.Meta.GetValueOrDefault("noi_gui_nhan") ?? n.Meta.GetValueOrDefault("chu_tri"),
                        Handler = n.Meta.GetValueOrDefault("nguoi_xu_ly"), Due = ParseDate(n.Meta.GetValueOrDefault("han_xu_ly")), HasFile = n.Meta.ContainsKey("file"),
                        TaskCount = ts.Count, TaskDone = ts.Count(t => t.Done), TaskStatus = ts.Count == 0 ? null : ts.All(t => t.Done) ? "xong" : "dang-theo-doi",
                        Overdue = ts.Any(t => !t.Done && t.Due is { } d && d < clock.Today),
                    };
                })
                .Where(x => year == null || x.Date?.Year == year)
                .OrderByDescending(x => x.Date).ThenByDescending(x => x.RegisterNo, StringComparer.Ordinal).ToList();
            return Results<KnowledgeRegisterItemModel>.Success(list, "OK");
        }

        /// <summary>Gọi trong UploadFile khi tải công văn vào vùng 05: cấp số vào sổ + (nếu có) giao người xử lý.</summary>
        private async Task<(string? No, string? Error)> DispatchNumberAsync(KnowledgeUploadRequest request, string type, DateTime day)
        {
            string prefix = type == "cong-van-di" ? "VI" : "VD";
            string no = NextRegisterNo(prefix, day.Year, 4);
            if (request.HandlerId != null && await UserNameAsync(request.HandlerId) == null)
            {
                return (null, "Người xử lý không hợp lệ / đã ngừng hoạt động");
            }
            if (request.HandlerId != null && ParseDate(request.HandlerDue) == null)
            {
                return (null, "Giao người xử lý cần hạn xử lý (yyyy-MM-dd)");
            }
            return (no, null);
        }

        public async Task<IResult<KnowledgeSaveResultModel>> CreateMeeting(KnowledgeMeetingRequest request)
        {
            var caller = WebCaller();
            if (caller.AgentCode != null)
            {
                return Status<KnowledgeSaveResultModel>(403, "Agent không lập biên bản họp");
            }
            var rights = await RightsAsync(caller);
            if (!rights.CanEdit(MeetingFolder))
            {
                return Status<KnowledgeSaveResultModel>(403, "Cần quyền Sửa vùng 15 Họp & giao ban");
            }
            string title = (request.Title ?? string.Empty).Trim();
            DateTime? day = ParseDate(request.Date);
            if (title.Length == 0 || day == null)
            {
                return Result<KnowledgeSaveResultModel>.Fail("Nhập tên cuộc họp và ngày họp");
            }
            var items = (request.Tasks ?? []).Where(t => !string.IsNullOrWhiteSpace(t.Task)).ToList();
            var named = new List<(KnowledgeMeetingTask T, string Name, DateTime Due)>();
            foreach (var t in items)
            {
                string? name = await UserNameAsync(t.AssigneeId);
                DateTime? due = ParseDate(t.Due);
                if (name == null || due == null)
                {
                    return Result<KnowledgeSaveResultModel>.Fail($"Việc «{t.Task!.Trim()}»: chọn người được giao và hạn");
                }
                named.Add((t, name, due.Value));
            }
            var folder = FolderByCode(MeetingFolder)!;
            var actor = currentActor.RequireActor();
            string Q(string? s) => (s ?? string.Empty).Replace("\r", string.Empty).Trim();
            var gate = LockFor(Root, "#register-" + MeetingFolder);
            await gate.WaitAsync();
            try
            {
                string no = NextRegisterNo("BH", day.Value.Year, 3);
                string path = $"{folder.Dir}/{day:yyyy-MM-dd} {no} {FileSafe(title.Length > 70 ? title[..70] : title)}.md";
                var sb = new StringBuilder();
                sb.Append($"\n# {title.Replace("\n", " ")}\n\n- Số: {no}\n- Ngày họp: {day:dd/MM/yyyy}\n- Chủ trì: {Q(request.Chair).Replace("\n", " ")}\n- Thành phần: {Q(request.Attendees).Replace("\n", " ")}\n\n");
                sb.Append($"## Nội dung\n\n{(Q(request.Content) is { Length: > 0 } c ? c : "(chưa ghi)")}\n\n## Kết luận\n\n{(Q(request.Conclusion) is { Length: > 0 } k ? k : "(chưa ghi)")}\n\n## Việc giao\n\n");
                sb.Append(named.Count == 0 ? "(không có)\n" : "| # | Việc | Người được giao | Hạn |\n|---|---|---|---|\n" + string.Join("\n", named.Select((x, i) => $"| {i + 1} | {x.T.Task!.Trim().Replace("|", "/").Replace("\n", " ")} | {x.Name} | {x.Due:dd/MM/yyyy} |")) + "\n");
                string fm = Frontmatter([("id", no.ToLowerInvariant()), ("type", "bien-ban-hop"), ("source", HumanSource), ("owner", Q(await UserNameAsync(actor.UserId) ?? "người lập")),
                    ("status", "hiệu lực"), ("valid_from", day.Value.ToString("yyyy-MM-dd")), ("review_by", day.Value.AddYears(1).ToString("yyyy-MM-dd")), ("confidentiality", "nội bộ"), ("refs", "[]"),
                    ("title", title.Replace("\n", " ")), ("so_vao_so", no), ("ngay", day.Value.ToString("yyyy-MM-dd")), ("chu_tri", Q(request.Chair).Replace("\n", " "))]);
                var r = await WriteAsync(path, fm + sb, null, actor.UserId, guardGenerated: true, note: "biên bản họp " + no);
                if (r.ResponseCode != 200)
                {
                    return r;
                }
                foreach (var (t, name, due) in named)
                {
                    await CreateFollowTaskAsync("HOP", t.Task!.Trim(), $"Việc giao trong cuộc họp {no} – {title} ({day:dd/MM/yyyy}).", t.AssigneeId!.Value, name, due, path, actor.UserId);
                }
                r.ResponseMessage = $"Đã lập biên bản {no}" + (named.Count > 0 ? $" · giao {named.Count} việc theo dõi" : string.Empty);
                return r;
            }
            finally
            {
                gate.Release();
            }
        }

        /// <summary>Việc theo dõi giao cho chính người dùng (assignee_id) – không cần quyền thư mục 98.</summary>
        public Task<IResults<KnowledgeTaskModel>> GetMyTasks(bool includeDone) => GetTasksCore(includeDone, mine: true);

        /// <summary>Người được giao bấm Xong / mở lại việc CỦA MÌNH (không cần quyền mục Giao việc ai-tasks).</summary>
        public async Task<IResult<KnowledgeSaveResultModel>> SetMyTask(KnowledgeTaskSetRequest request)
        {
            var caller = WebCaller();
            var n = All().FirstOrDefault(x => x.Folder == "98" && string.Equals(x.Path, request.Path, StringComparison.OrdinalIgnoreCase));
            if (n == null || !AssignedTo(n, caller.UserId))
            {
                return Status<KnowledgeSaveResultModel>(404, "Không có việc này trong danh sách việc được giao cho anh/chị");
            }
            return await SetTask(new KnowledgeTaskSetRequest { Path = n.Path, Status = request.Status, Note = request.Note });
        }

        /// <summary>Job 07:00: việc theo dõi có người được giao, chưa xong, hạn ≤ ngày mai → nhắc người được giao qua bot (1 tin / người / ngày).</summary>
        public async Task<Dictionary<Guid, List<string>>> DueTasksByAssigneeAsync()
        {
            DateTime limit = clock.Today.AddDays(1);
            return All().Where(n => n.Folder == "98" && Guid.TryParse(n.Meta.GetValueOrDefault("assignee_id"), out _) && n.ReviewBy is { } d && d <= limit
                    && Slug(Regex.Match(n.Body, @"^- Trạng thái:\s*(.*)$", RegexOptions.Multiline).Groups[1].Value) != "xong")
                .GroupBy(n => Guid.Parse(n.Meta["assignee_id"]))
                .ToDictionary(g => g.Key, g => g.OrderBy(n => n.ReviewBy).Select(n => $"{n.Title} – hạn {n.ReviewBy:dd/MM}{(n.ReviewBy < clock.Today ? " (quá hạn)" : string.Empty)}").ToList());
        }
    }

    /// <summary>AI-22.4: 07:00 nhắc người được giao việc theo dõi (công văn, biên bản họp) sắp / quá hạn.</summary>
    public sealed class KnowledgeTaskDueJob(IKnowledgeService knowledge, INotificationService notifications, IClock clock) : IScheduledJob
    {
        public string Code => "KnowledgeTaskDue";
        public string Name => "Nhắc việc giao (công văn, họp)";
        public string Description => "07:00 – việc theo dõi từ sổ công văn / biên bản họp có hạn đến ngày mai hoặc đã quá hạn → nhắc người được giao qua bot.";
        public string DefaultAt => "07:00";

        public async Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            var due = await knowledge.DueTasksByAssigneeAsync();
            int sent = 0;
            foreach (var (user, items) in due)
            {
                sent += await BusinessNotifier.SafeEnqueueAsync(notifications, new NotificationRequest
                {
                    Template = NotificationCatalog.KnowledgeTaskDue, UserIds = [user], IdempotencyKey = $"{NotificationCatalog.KnowledgeTaskDue}:{user:N}:{clock.Today:yyyyMMdd}",
                    Data = new { title = $"{items.Count} việc được giao sắp / quá hạn", lines = items.Take(10).Select(x => "📌 " + x).ToList(), link = "#/knowledge?view=my-tasks" },
                });
            }
            return new JobOutcome(JobRunStatus.Succeeded, due.Sum(x => x.Value.Count), now.Date, $"{due.Count} người có việc sắp / quá hạn · {sent} tin đã xếp");
        }
    }
}
