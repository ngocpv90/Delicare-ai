using System.Text;
using AppDelicare.Data.Share;
using AppDelicare.Model.Option;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Services.Interface;
using AppDelicare.Services.Jobs;
using Microsoft.Extensions.Options;
using static AppDelicare.Services.Implement.Knowledge.KnowledgeVault;

namespace AppDelicare.Services.Implement.Knowledge
{
    public sealed record KnowledgeMaintenanceResult(int MergedLogs, int MonthFiles, int ArchivedBriefs, Dictionary<string, List<string>> OverdueByFolder)
    {
        /// <summary>AI-22.2 (§7.9 luật 6): tài liệu quá <c>retain_until</c> theo vùng – chỉ nhắc chủ vùng, KHÔNG tự xóa.</summary>
        public Dictionary<string, List<string>> RetainDueByFolder { get; init; } = [];
    }

    /// <summary>
    /// AI-12 (D-133 §7.8): bảo trì kho của KNOW – (1) ghi chú nghiệp vụ Hiệu lực quá <c>review_by</c> → nhắc chủ thư mục (1 tin / thư mục / tuần);
    /// (2) nhật ký 96 quá <c>Knowledge:LogRetentionDays</c> (90) → gộp vào tệp "Tóm tắt yyyy-MM" của agent (nội dung giữ nguyên từng dòng kèm ngày), bỏ tệp ngày đã gộp;
    /// (3) bản tin ngày (10 Ban GĐ/Bản tin) quá <c>Knowledge:BriefKeepMonths</c> (13) → chuyển sang "10 Ban GĐ/Lưu trữ bản tin" trạng thái Lưu trữ (không xóa).
    /// </summary>
    public partial class KnowledgeService
    {
        public async Task<KnowledgeMaintenanceResult> MaintainAsync()
        {
            EnsureSkeleton();
            var notes = All();
            DateTime today = clock.Today;
            // (1) quá hạn rà soát
            var overdue = notes.Where(n => !n.Generated && n.Status == "hieu-luc" && n.ReviewBy is { } r && r < today && FolderByCode(n.Folder) is { AgentWritable: false } && n.Folder != Bod)
                .GroupBy(n => n.Folder).ToDictionary(g => g.Key, g => g.OrderBy(n => n.ReviewBy).Select(n => $"{n.Title} (hạn rà {n.ReviewBy:dd/MM/yyyy})").ToList());

            // (2) gộp nhật ký 96
            int merged = 0;
            var months = new HashSet<string>();
            DateTime cutoff = today.AddDays(-Math.Max(7, _o.LogRetentionDays));
            foreach (var g in notes.Where(n => n.Folder == "96" && n.Type == "nhat-ky" && n.ValidFrom is { } d && d < cutoff && !n.Path.Contains("/Tóm tắt ", StringComparison.Ordinal))
                         .GroupBy(n => (Dir: n.Path[..n.Path.LastIndexOf('/')],Month: n.ValidFrom!.Value.ToString("yyyy-MM"))))
            {
                string monthPath = $"{g.Key.Dir}/Tóm tắt {g.Key.Month}.md";
                string full = Path.Combine(Root, monthPath);
                string agent = g.Key.Dir.Split('/').Last();
                var sb = new StringBuilder(File.Exists(full) ? File.ReadAllText(full, Encoding.UTF8).TrimEnd('\n') + "\n"
                    : Frontmatter([("id", $"nk-{agent.ToLowerInvariant()}-{g.Key.Month.Replace("-", string.Empty)}"), ("type", "nhat-ky"), ("source", "agent:know"), ("owner", "KNOW"), ("status", "lưu trữ"),
                        ("valid_from", g.Key.Month + "-01"), ("review_by", DateTime.Parse(g.Key.Month + "-01").AddMonths(13).ToString("yyyy-MM-dd")), ("confidentiality", "nội bộ"), ("refs", "[]")])
                      + $"\n# Tóm tắt nhật ký {agent} tháng {g.Key.Month[5..]}/{g.Key.Month[..4]}\n\n_Gộp tự động từ nhật ký ngày quá {_o.LogRetentionDays} ngày (KNOW)._\n");
                foreach (var n in g.OrderBy(n => n.ValidFrom))
                {
                    sb.Append($"\n## {n.ValidFrom:dd/MM/yyyy}\n\n");
                    foreach (string line in n.Body.Split('\n').Where(l => l.StartsWith("- ", StringComparison.Ordinal)))
                    {
                        sb.Append(line).Append('\n');
                    }
                }
                var w = await WriteAsync(monthPath, sb.ToString(), File.Exists(full) ? Sha256(File.ReadAllText(full, Encoding.UTF8)) : null, null, guardGenerated: false, note: "gộp nhật ký tháng");
                if (w.ResponseCode != 200)
                {
                    continue;
                }
                months.Add(monthPath);
                foreach (var n in g)
                {
                    File.Delete(Path.Combine(Root, n.Path));
                    Audit(n.Path, "Merged", n.Hash, w.Data!.Hash, null, "đã gộp vào " + monthPath);
                    merged++;
                }
                await unitOfWork.DbContext.SaveChangesAsync();
            }

            // (3) bản tin ngày quá hạn giữ → Lưu trữ bản tin
            int archived = 0;
            DateTime keep = today.AddMonths(-Math.Max(1, _o.BriefKeepMonths));
            foreach (var n in notes.Where(n => n.Path.StartsWith("10 Ban GĐ/Bản tin/", StringComparison.Ordinal) && n.ValidFrom is { } d && d < keep))
            {
                string target = "10 Ban GĐ/Lưu trữ bản tin/" + n.Path["10 Ban GĐ/Bản tin/".Length..];
                string text = SetMeta(File.ReadAllText(Path.Combine(Root, n.Path), Encoding.UTF8), "status", "lưu trữ");
                Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(Root, target))!);
                WriteAtomic(Path.Combine(Root, target), text);
                File.Delete(Path.Combine(Root, n.Path));
                Audit(n.Path, "Archived", n.Hash, Sha256(text), null, "chuyển " + target);
                archived++;
            }
            if (archived > 0)
            {
                await unitOfWork.DbContext.SaveChangesAsync();
            }
            // (4) AI-22.2 (§7.9 luật 6): quá hạn lưu giữ → nhắc chủ vùng, không tự xóa.
            var retain = All().Where(n => n.Status != "luu-tru" && ParseDate(n.Meta.GetValueOrDefault("retain_until")) is { } ru && ru <= today && FolderByCode(n.Folder) is { AgentWritable: false })
                .GroupBy(n => n.Folder).ToDictionary(g => g.Key, g => g.Select(n => $"{n.Title} (hạn lưu giữ {ParseDate(n.Meta.GetValueOrDefault("retain_until")):dd/MM/yyyy})").ToList());
            return new KnowledgeMaintenanceResult(merged, months.Count, archived, overdue) { RetainDueByFolder = retain };
        }
    }

    /// <summary>AI-12: job bảo trì kho tri thức (thứ Hai 22:30 – sau lịch rà của KNOW): nhắc quá hạn rà soát, gộp nhật ký, lưu trữ bản tin cũ.</summary>
    public sealed class KnowledgeMaintenanceJob(IKnowledgeService knowledge, AppDelicare.Data.Interface.IUnitOfWork unitOfWork, INotificationService notifications, IClock clock) : IScheduledJob
    {
        public string Code => "KnowledgeMaintenance";
        public string Name => "Bảo trì kho tri thức";
        public string Description => "Thứ Hai: nhắc chủ thư mục ghi chú quá hạn rà soát; gộp nhật ký agent quá 90 ngày thành tóm tắt tháng; chuyển bản tin ngày quá 13 tháng vào Lưu trữ bản tin (không xóa).";
        public string DefaultAt => "22:30";

        public async Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            if (now.DayOfWeek != DayOfWeek.Monday)
            {
                return new JobOutcome(JobRunStatus.Skipped, 0, now.Date, "Chỉ chạy thứ Hai");
            }
            var r = await knowledge.MaintainAsync();
            foreach (var (folder, items) in r.OverdueByFolder)
            {
                var f = FolderByCode(folder)!;
                await BusinessNotifier.ToMenusAsync(notifications, unitOfWork.DbContext, NotificationCatalog.KnowledgeReviewDue, ["#/" + f.Screen], new
                {
                    title = $"{items.Count} ghi chú {f.Dir} quá hạn rà soát", lines = items.Take(10).Select(x => "📄 " + x).ToList(),
                }, $"{NotificationCatalog.KnowledgeReviewDue}:{folder}:{clock.Today:yyyyMMdd}", requireAdd: true);
            }
            foreach (var (folder, items) in r.RetainDueByFolder)
            {
                var f = FolderByCode(folder)!;
                await BusinessNotifier.ToMenusAsync(notifications, unitOfWork.DbContext, NotificationCatalog.KnowledgeReviewDue, ["#/" + f.Screen], new
                {
                    title = $"{items.Count} tài liệu {f.Dir} hết hạn lưu giữ – xem xét lưu trữ (không tự xóa)", lines = items.Take(10).Select(x => "🗄 " + x).ToList(),
                }, $"{NotificationCatalog.KnowledgeReviewDue}:retain:{folder}:{clock.Today:yyyyMMdd}", requireAdd: true);
            }
            int overdue = r.OverdueByFolder.Sum(x => x.Value.Count);
            return new JobOutcome(JobRunStatus.Succeeded, r.MergedLogs + r.ArchivedBriefs + overdue, now.Date,
                $"Quá hạn rà soát {overdue} ghi chú · gộp {r.MergedLogs} nhật ký ngày vào {r.MonthFiles} tóm tắt tháng · lưu trữ {r.ArchivedBriefs} bản tin");
        }
    }
}
