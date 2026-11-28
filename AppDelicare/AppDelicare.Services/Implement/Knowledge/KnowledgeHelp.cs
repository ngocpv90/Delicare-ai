using System.Text;
using System.Text.RegularExpressions;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Services.Jobs;
using Newtonsoft.Json;
using static AppDelicare.Services.Implement.Knowledge.KnowledgeVault;

namespace AppDelicare.Services.Implement.Knowledge
{
    public sealed class KnowledgeHelpStepModel
    {
        public int No { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        /// <summary>Giá trị <c>data-help</c> của phần tử trên màn ("Chỉ cho tôi") – trống thì ẩn nút.</summary>
        public string? Target { get; set; }
    }

    public sealed class KnowledgeHelpFaqModel
    {
        public string Q { get; set; } = string.Empty;
        public string A { get; set; } = string.Empty;
    }

    /// <summary>AI-22.3 (D-141 l, §7.11): hướng dẫn của 1 màn cho ngăn "?" – mục HDSD có mã màn trong tiêu đề, các bước, ghi chú, câu hỏi thường gặp.</summary>
    public sealed class KnowledgeHelpModel
    {
        public string Screen { get; set; } = string.Empty;
        public bool Found { get; set; }
        public string? Path { get; set; }
        public string? Guide { get; set; }
        public string? Section { get; set; }
        public string? Anchor { get; set; }
        public string? Roles { get; set; }
        public DateTime? Updated { get; set; }
        public string? Intro { get; set; }
        public List<KnowledgeHelpStepModel> Steps { get; set; } = [];
        public List<string> Notes { get; set; } = [];
        public List<KnowledgeHelpFaqModel> Faq { get; set; } = [];
        /// <summary>Mục khác trong kho HDSD có nhắc màn này (tiêu đề + đường dẫn).</summary>
        public List<KnowledgeNoteHeadModel> Related { get; set; } = [];
    }

    /// <summary>AI-22.3: 1 HDSD trong thư viện (mục lục) – màn áp dụng, vai trò, ngày cập nhật.</summary>
    public sealed class KnowledgeGuideModel
    {
        public string Path { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public List<string> Screens { get; set; } = [];
        public string? Roles { get; set; }
        public DateTime? Updated { get; set; }
        public List<string> Sections { get; set; } = [];
    }

    public sealed class KnowledgeHelpFeedbackRequest
    {
        public string? Screen { get; set; }
        public string? Path { get; set; }
        public bool Helpful { get; set; }
        public string? Comment { get; set; }
    }

    /// <summary>
    /// AI-22.3 (D-141 l, §7.11): Hướng dẫn sử dụng trên giao diện. Nguồn = <c>docs/user-guide/*.md</c> (frontmatter <c>screens</c> / <c>roles</c> / <c>updated</c>; mục có mã màn
    /// trong tiêu đề, vd "### 4.1 Nhận hàng – `scm-gr`"; bước có <c>{target: …}</c> = phần tử <c>data-help</c> cho "Chỉ cho tôi"; mục "Câu hỏi thường gặp" / "Lỗi thường gặp").
    /// Exporter nhập vào <c>80 Đào tạo/Hướng dẫn sử dụng</c> (<c>source: delicare-docs</c>, chỉ đọc), chạy lại không nhân bản, đổi nội dung thì cập nhật.
    /// Phản hồi Có / Chưa: AuditChange <c>KnowledgeHelpFeedback</c> (KNOW tổng hợp mục cần sửa trong bảo trì tuần).
    /// </summary>
    public partial class KnowledgeService
    {
        public const string DocsSource = "delicare-docs";
        public const string HelpFeedbackEntity = "KnowledgeHelpFeedback";
        private static readonly Regex ScreenInHeading = new(@"`([a-z][a-z0-9-]*(?:\?[a-z]+=[a-z0-9-]+)?)`", RegexOptions.Compiled);
        private static readonly Regex TargetRx = new(@"\s*\{target:\s*([a-z0-9-]+)\s*\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>Thư mục nguồn HDSD: <c>Knowledge:UserGuidePath</c>; trống → tìm "docs/user-guide" đi ngược lên từ thư mục chạy (máy dev).</summary>
        public string? UserGuideSource()
        {
            if (!string.IsNullOrWhiteSpace(_o.UserGuidePath))
            {
                return Directory.Exists(_o.UserGuidePath) ? _o.UserGuidePath : null;
            }
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                string p = Path.Combine(dir.FullName, "docs", "user-guide");
                if (Directory.Exists(p))
                {
                    return p;
                }
                dir = dir.Parent;
            }
            return null;
        }

        /// <summary>Frontmatter đơn giản của file HDSD nguồn (khóa: giá trị) + phần thân.</summary>
        private static (Dictionary<string, string> Meta, string Body) SplitFrontmatter(string text)
        {
            var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string t = text.Replace("\r\n", "\n");
            if (t.StartsWith("---\n", StringComparison.Ordinal) && t.IndexOf("\n---", 4, StringComparison.Ordinal) is var end and > 0)
            {
                foreach (string line in t[4..end].Split('\n'))
                {
                    int i = line.IndexOf(':');
                    if (i > 0)
                    {
                        meta[line[..i].Trim()] = line[(i + 1)..].Trim();
                    }
                }
                t = t[(end + 4)..].TrimStart('\n');
            }
            return (meta, t);
        }

        public static List<string> ListOf(string? inline) => [.. (inline ?? string.Empty).Trim().TrimStart('[').TrimEnd(']').Split(',').Select(x => x.Trim().Trim('"', '\'')).Where(x => x.Length > 0)];

        /// <summary>Nhập <c>docs/user-guide/NN_*.md</c> vào vùng 80 › Hướng dẫn sử dụng. Trả số ghi chú thêm / cập nhật.</summary>
        public async Task<int> ExportUserGuideAsync()
        {
            string? src = UserGuideSource();
            if (src == null)
            {
                return 0;
            }
            var folder = KnowledgeVault.FolderByCode(KnowledgeVault.UserGuide)!;
            int n = 0;
            foreach (string file in Directory.EnumerateFiles(src, "*.md").Where(f => Regex.IsMatch(Path.GetFileName(f), @"^\d{2}_")).OrderBy(f => f, StringComparer.Ordinal))
            {
                string stem = Path.GetFileNameWithoutExtension(file);
                var (meta, body) = SplitFrontmatter(await File.ReadAllTextAsync(file, Encoding.UTF8));
                string title = Regex.Match(body, @"^#\s+(.+)$", RegexOptions.Multiline) is { Success: true } h ? h.Groups[1].Value.Trim() : stem;
                DateTime updated = KnowledgeVault.ParseDate(meta.GetValueOrDefault("updated")) ?? File.GetLastWriteTime(file).Date;
                var screens = ListOf(meta.GetValueOrDefault("screens"));
                if (screens.Count == 0)
                {
                    screens = [.. ScreenInHeading.Matches(string.Join('\n', body.Split('\n').Where(l => l.StartsWith('#')))).Select(m => m.Groups[1].Value).Distinct()];
                }
                string text = KnowledgeVault.Frontmatter([
                    ("id", "hdsd-" + stem.ToLowerInvariant()), ("type", "hdsd"), ("source", DocsSource), ("owner", folder.Owner), ("status", "hiệu lực"),
                    ("valid_from", updated.ToString("yyyy-MM-dd")), ("review_by", updated.AddYears(1).ToString("yyyy-MM-dd")), ("confidentiality", "nội bộ"), ("refs", "[]"),
                    ("title", title), ("screens", "[" + string.Join(", ", screens) + "]"), ("roles", meta.GetValueOrDefault("roles") ?? string.Empty), ("updated", updated.ToString("yyyy-MM-dd")),
                ]) + "\n" + body.TrimEnd() + "\n";
                string path = $"{folder.Dir}/{stem}.md";
                string full = Path.Combine(Root, path);
                if (File.Exists(full) && KnowledgeVault.Sha256(await File.ReadAllTextAsync(full, Encoding.UTF8)) == KnowledgeVault.Sha256(text))
                {
                    continue;
                }
                var r = await WriteAsync(path, text, null, null, guardGenerated: false, note: "user-guide");
                if (r.ResponseCode == 200)
                {
                    n++;
                }
            }
            return n;
        }

        private sealed record Section(string Title, int Level, string Text, string Anchor);

        private static List<Section> Sections(string body)
        {
            var list = new List<Section>();
            string? title = null;
            int level = 0;
            var sb = new StringBuilder();
            void Flush()
            {
                if (title != null)
                {
                    list.Add(new Section(title, level, sb.ToString().Trim(), Anchor(title)));
                }
                sb.Clear();
            }
            foreach (string line in body.Replace("\r\n", "\n").Split('\n'))
            {
                var m = Regex.Match(line, @"^(#{2,4})\s+(.+)$");
                if (m.Success)
                {
                    Flush();
                    title = m.Groups[2].Value.Trim();
                    level = m.Groups[1].Value.Length;
                    continue;
                }
                sb.Append(line).Append('\n');
            }
            Flush();
            return list;
        }

        public static string Anchor(string title) => "h-" + KnowledgeVault.Slug(Regex.Replace(title, @"`[^`]*`", string.Empty)).Trim('-');

        private static (List<KnowledgeHelpStepModel> Steps, List<string> Notes, string? Intro) Steps(string text)
        {
            var steps = new List<KnowledgeHelpStepModel>();
            var notes = new List<string>();
            string? intro = null;
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.TrimEnd();
                var m = Regex.Match(line, @"^\s*(?:\d+\.|[-*])\s+(.+)$");
                if (m.Success && !line.StartsWith("  ", StringComparison.Ordinal))
                {
                    string s = m.Groups[1].Value;
                    string? target = TargetRx.Match(s) is { Success: true } t ? t.Groups[1].Value.ToLowerInvariant() : null;
                    s = TargetRx.Replace(s, string.Empty).Trim();
                    var b = Regex.Match(s, @"^\*\*(.+?)\*\*\s*[:–-]?\s*(.*)$");
                    steps.Add(new KnowledgeHelpStepModel { No = steps.Count + 1, Title = b.Success ? b.Groups[1].Value : s, Text = b.Success ? b.Groups[2].Value : string.Empty, Target = target });
                }
                else if (line.StartsWith('>'))
                {
                    notes.Add(line.TrimStart('>', ' '));
                }
                else if (line.Trim().Length > 0 && !line.StartsWith('|') && steps.Count == 0 && intro == null)
                {
                    intro = line.Trim();
                }
            }
            return (steps, notes, intro);
        }

        private static List<KnowledgeHelpFaqModel> Faq(List<Section> sections)
        {
            var faq = new List<KnowledgeHelpFaqModel>();
            foreach (var s in sections.Where(x => Regex.IsMatch(KnowledgeVault.Fold(x.Title), @"cau hoi thuong gap|loi.*thuong gap|canh bao thuong gap")))
            {
                foreach (string line in s.Text.Split('\n'))
                {
                    var b = Regex.Match(line.Trim(), @"^(?:[-*]|\d+\.)\s+\*\*(.+?)\*\*\s*[:–-]?\s*(.+)$");
                    var row = Regex.Match(line.Trim(), @"^\|\s*([^|]+?)\s*\|\s*([^|]+?)\s*\|");
                    if (b.Success)
                    {
                        faq.Add(new KnowledgeHelpFaqModel { Q = b.Groups[1].Value.Trim(), A = b.Groups[2].Value.Trim() });
                    }
                    else if (row.Success && !Regex.IsMatch(row.Groups[1].Value, @"^[-: ]+$") && !Regex.IsMatch(KnowledgeVault.Fold(row.Groups[1].Value), @"^\s*(thong bao|loi|canh bao|cau hoi|hien tuong)\s*$"))
                    {
                        faq.Add(new KnowledgeHelpFaqModel { Q = row.Groups[1].Value.Trim(), A = row.Groups[2].Value.Trim() });
                    }
                }
            }
            return faq;
        }

        private async Task<(Rights Rights, List<Note> Guides)> GuidesAsync()
        {
            var rights = await RightsAsync(WebCaller());
            var guides = All().Where(n => n.Folder == KnowledgeVault.UserGuide && rights.NoteVisible(n) && n.Status != "luu-tru").ToList();
            if (guides.Count == 0 && rights.CanRead(KnowledgeVault.UserGuide) && await ExportUserGuideAsync() > 0)
            {
                guides = [.. All().Where(n => n.Folder == KnowledgeVault.UserGuide && rights.NoteVisible(n) && n.Status != "luu-tru")];
            }
            return (rights, guides);
        }

        public async Task<IResult<KnowledgeHelpModel>> GetHelp(string? screen)
        {
            string id = (screen ?? string.Empty).Trim().TrimStart('#', '/');
            var model = new KnowledgeHelpModel { Screen = id };
            if (id.Length == 0)
            {
                return Result<KnowledgeHelpModel>.Fail("Thiếu mã màn");
            }
            var (_, guides) = await GuidesAsync();
            string baseId = id.Split('?')[0];
            string Tick(string s) => "`" + s + "`";
            // Mục ứng viên = tiêu đề có `mã màn` (khớp đủ ?tab= trước); chọn mục có nhiều bước "Chỉ cho tôi", rồi nhiều bước, rồi HDSD khai màn trong frontmatter, rồi theo tên tệp.
            var cands = guides.OrderBy(g => g.Path, StringComparer.Ordinal).SelectMany(g => Sections(g.Body)
                    .Where(x => x.Title.Contains(Tick(id), StringComparison.Ordinal) || x.Title.Contains(Tick(baseId), StringComparison.Ordinal))
                    .Select(x => (G: g, S: x, St: Steps(x.Text).Steps, Exact: x.Title.Contains(Tick(id), StringComparison.Ordinal), Listed: ListOf(g.Meta.GetValueOrDefault("screens")).Any(s => s == id || s == baseId))))
                .ToList();
            var pick = cands.OrderByDescending(c => c.Exact).ThenByDescending(c => c.St.Count(x => x.Target != null)).ThenByDescending(c => c.St.Count).ThenByDescending(c => c.Listed).FirstOrDefault();
            Note? best = pick.G;
            Section? hit = pick.S;
            if (best == null || hit == null)
            {
                return Result<KnowledgeHelpModel>.Success(model, "Màn này chưa có hướng dẫn – mở mục lục Hướng dẫn sử dụng");
            }
            var all = Sections(best.Body);
            var (steps, notes, intro) = Steps(hit.Text);
            model.Found = true;
            model.Path = best.Path;
            model.Guide = best.Title;
            model.Section = Regex.Replace(Regex.Replace(hit.Title, @"\s*[–-]\s*`[^`]+`.*$", string.Empty), @"\s*\((?:[A-Z]+-\d[^)]*|D-\d+[^)]*)\)", string.Empty).Trim(); // bỏ mã task "(FE-2, …)" / "(UI-6.27, D-108)"
            model.Anchor = hit.Anchor;
            model.Roles = best.Meta.GetValueOrDefault("roles");
            model.Updated = KnowledgeVault.ParseDate(best.Meta.GetValueOrDefault("updated"));
            model.Intro = intro;
            model.Steps = steps;
            model.Notes = notes;
            model.Faq = Faq(all);
            model.Related = [.. guides.Where(g => g != best && g.Body.Contains(Tick(baseId), StringComparison.Ordinal)).Take(5).Select(g => Head(g))];
            return Result<KnowledgeHelpModel>.Success(model, "OK");
        }

        public async Task<IResults<KnowledgeGuideModel>> GetGuides()
        {
            var (_, guides) = await GuidesAsync();
            var list = guides.OrderBy(g => g.Path, StringComparer.Ordinal).Select(g => new KnowledgeGuideModel
            {
                Path = g.Path, Title = g.Title, Screens = ListOf(g.Meta.GetValueOrDefault("screens")), Roles = g.Meta.GetValueOrDefault("roles"),
                Updated = KnowledgeVault.ParseDate(g.Meta.GetValueOrDefault("updated")), Sections = [.. Sections(g.Body).Where(s => s.Level == 2).Select(s => s.Title)],
            }).ToList();
            return Results<KnowledgeGuideModel>.Success(list, "OK");
        }

        public async Task<IResult<bool>> HelpFeedback(KnowledgeHelpFeedbackRequest request)
        {
            var actor = currentActor.RequireActor();
            string screen = (request.Screen ?? string.Empty).Trim();
            if (screen.Length == 0)
            {
                return Result<bool>.Fail("Thiếu mã màn");
            }
            unitOfWork.DbContext.AuditChange.Add(new AuditChange
            {
                Id = Guid.NewGuid(), IdChange = KnowledgeVault.KeyGuid("help:" + screen), EntityName = HelpFeedbackEntity, Action = request.Helpful ? "Có" : "Chưa",
                Changes = JsonConvert.SerializeObject(new { screen, path = request.Path, comment = (request.Comment ?? string.Empty).Trim() is { Length: > 0 } c ? (c.Length > 500 ? c[..500] : c) : null }),
                CreatedDate = clock.Now, CreatedBy = actor.UserId, IsDeleted = false,
            });
            await unitOfWork.DbContext.SaveChangesAsync();
            return Result<bool>.Success(true, request.Helpful ? "Cảm ơn anh/chị!" : "Đã ghi nhận – hướng dẫn sẽ được bổ sung");
        }
    }

    /// <summary>AI-22.3: nhập lại Hướng dẫn sử dụng (docs/user-guide) vào kho mỗi sáng – chạy lại không nhân bản, file đổi thì cập nhật.</summary>
    public sealed class UserGuideExportJob(IKnowledgeService knowledge) : IScheduledJob
    {
        public string Code => "UserGuideExport";
        public string Name => "Nhập Hướng dẫn sử dụng vào kho";
        public string Description => "05:30 – nhập docs/user-guide (bản phát hành) vào Kho tài liệu › 80 › Hướng dẫn sử dụng; nội dung không đổi thì bỏ qua.";
        public string DefaultAt => "05:30";

        public async Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            int n = await knowledge.ExportUserGuideAsync();
            return new JobOutcome(JobRunStatus.Succeeded, n, now.Date, n == 0 ? "Hướng dẫn sử dụng không đổi" : $"Đã cập nhật {n} hướng dẫn sử dụng");
        }
    }
}
