using System.Text;
using System.Text.RegularExpressions;

namespace AppDelicare.Services.Implement.Knowledge
{
    /// <summary>AI-21 (D-138 d): việc đang mở do Trung tâm Agent tính ra (cảnh báo / bước luồng kẹt) – khóa ổn định theo ngày.</summary>
    public sealed record KnowledgeTaskOpen(string Key, string Title, string Agent);

    /// <summary>AI-21: người bấm "Đã xử lý" 1 việc.</summary>
    public sealed record KnowledgeTaskDone(string Key, string Title, string Agent, string UserName, string? Note);

    /// <summary>AI-21: trạng thái 1 việc trong sổ việc của ngày – mo (đang mở) / tu-dong (nguồn hết điều kiện) / xu-ly (người bấm Đã xử lý).</summary>
    public sealed class KnowledgeTaskMark
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Agent { get; set; } = string.Empty;
        public string State { get; set; } = "mo";
        public DateTime? OpenedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public string? By { get; set; }
        public string? Note { get; set; }
    }

    /// <summary>
    /// AI-21 (D-138 d – không bảng mới): <b>sổ việc theo ngày</b> trong thư mục 98 (<c>98 Công việc/_so-viec/yyyy-MM-dd.md</c>, mẫu cố định, chỉ nối dòng):
    /// <c>- [mo] KHÓA · HH:mm · agent · tiêu đề</c>, <c>- [tu-dong] KHÓA · HH:mm</c>, <c>- [xu-ly] KHÓA · HH:mm · người · ghi chú</c>.
    /// Việc tự đóng khi nguồn hết điều kiện (người xem có quyền kiểm nguồn đó mà việc không còn mở); mở lại nếu nguồn lại có điều kiện; "Đã xử lý" ghi người + ghi chú.
    /// Tệp không hiện ở danh sách "việc agent theo dõi" (id <c>so-viec-…</c>); đọc / ghi qua service (không theo quyền thư mục – nội dung chỉ là khóa + tiêu đề việc người xem đã thấy).
    /// </summary>
    public partial class KnowledgeService
    {
        public const string TaskLedgerDir = "98 Công việc/_so-viec";
        private static readonly Regex LedgerLine = new(@"^- \[(mo|tu-dong|xu-ly)\] (\S+) · (\d{2}:\d{2})(?: · (.*))?$", RegexOptions.Multiline);

        public static bool IsTaskLedger(string path) => path.Replace('\\', '/').StartsWith(TaskLedgerDir + "/", StringComparison.OrdinalIgnoreCase);

        private static string LedgerPath(DateTime day) => $"{TaskLedgerDir}/{day:yyyy-MM-dd}.md";

        private Dictionary<string, KnowledgeTaskMark> ParseLedger(string? text, DateTime day)
        {
            var map = new Dictionary<string, KnowledgeTaskMark>(StringComparer.Ordinal);
            foreach (Match m in LedgerLine.Matches((text ?? string.Empty).Replace("\r\n", "\n")))
            {
                string state = m.Groups[1].Value, key = m.Groups[2].Value;
                DateTime at = day.Date + (TimeSpan.TryParse(m.Groups[3].Value, out var t) ? t : TimeSpan.Zero);
                string[] rest = m.Groups[4].Success ? m.Groups[4].Value.Split(" · ", 2) : [];
                if (!map.TryGetValue(key, out var mark))
                {
                    map[key] = mark = new KnowledgeTaskMark { Key = key };
                }
                if (state == "mo")
                {
                    mark.State = "mo"; mark.OpenedAt ??= at; mark.ClosedAt = null; mark.By = null; mark.Note = null;
                    if (rest.Length > 0) mark.Agent = rest[0];
                    if (rest.Length > 1) mark.Title = rest[1];
                }
                else
                {
                    mark.State = state; mark.ClosedAt = at;
                    mark.By = state == "xu-ly" && rest.Length > 0 ? rest[0] : null;
                    mark.Note = state == "xu-ly" && rest.Length > 1 ? rest[1] : null;
                }
            }
            return map;
        }

        private static string OneLine(string? s, int max)
        {
            string v = Regex.Replace(s ?? string.Empty, @"\s+", " ").Replace(" · ", " – ").Trim();
            return v.Length <= max ? v : v[..max].TrimEnd() + "…";
        }

        /// <summary>
        /// Cập nhật sổ việc của <paramref name="day"/>: việc mới mở → [mo]; việc đã ghi mở, <paramref name="canJudge"/>(khóa) = true mà không còn trong <paramref name="open"/> → [tu-dong];
        /// việc đã đóng lại xuất hiện → [mo] (mở lại); <paramref name="done"/> → [xu-ly]. Không đổi gì → không ghi tệp. Trả trạng thái mọi việc trong sổ.
        /// </summary>
        public async Task<Dictionary<string, KnowledgeTaskMark>> UpdateTaskLedger(DateTime day, IReadOnlyCollection<KnowledgeTaskOpen> open, Func<string, bool> canJudge, KnowledgeTaskDone? done)
        {
            EnsureSkeleton();
            string path = LedgerPath(day), full = Path.Combine(Root, path);
            string? text = File.Exists(full) ? await File.ReadAllTextAsync(full, Encoding.UTF8) : null;
            var map = ParseLedger(text, day);
            var add = new List<string>();
            string now = clock.Now.ToString("HH:mm");
            var openKeys = open.Select(o => o.Key).ToHashSet(StringComparer.Ordinal);
            foreach (var o in open)
            {
                if (!map.TryGetValue(o.Key, out var mark) || mark.State != "mo")
                {
                    // việc đã bấm Đã xử lý trong ngày: không mở lại (người phụ trách đã xác nhận); đã tự đóng mà nguồn lại có điều kiện → mở lại.
                    if (mark?.State == "xu-ly")
                    {
                        continue;
                    }
                    add.Add($"- [mo] {o.Key} · {now} · {OneLine(o.Agent, 20)} · {OneLine(o.Title, 160)}");
                }
            }
            foreach (var mark in map.Values.Where(m => m.State == "mo" && !openKeys.Contains(m.Key) && canJudge(m.Key)))
            {
                if (done?.Key != mark.Key)
                {
                    add.Add($"- [tu-dong] {mark.Key} · {now}");
                }
            }
            if (done != null && (!map.TryGetValue(done.Key, out var dm) || dm.State != "xu-ly"))
            {
                if (dm == null && !openKeys.Contains(done.Key))
                {
                    add.Add($"- [mo] {done.Key} · {now} · {OneLine(done.Agent, 20)} · {OneLine(done.Title, 160)}");
                }
                add.Add($"- [xu-ly] {done.Key} · {now} · {OneLine(done.UserName, 60)} · {OneLine(done.Note, 300)}");
            }
            if (add.Count == 0)
            {
                return map;
            }
            string head = text ?? KnowledgeVault.Frontmatter([("id", $"so-viec-{day:yyyyMMdd}"), ("type", "nhat-ky"), ("source", "agent:vivi"), ("owner", "KNOW"), ("status", "hiệu lực"),
                    ("valid_from", day.ToString("yyyy-MM-dd")), ("review_by", day.AddDays(90).ToString("yyyy-MM-dd")), ("confidentiality", "nội bộ"), ("refs", "[]")])
                + $"\n# Sổ việc Trung tâm Agent {day:dd/MM/yyyy}\n\nViệc agent phát hiện (cảnh báo, bước luồng kẹt): mở / tự đóng khi nguồn đã xử lý / người xử lý ghi chú. Tệp do hệ thống ghi – không sửa tay.\n\n";
            string body = head.Replace("\r\n", "\n").TrimEnd('\n') + "\n" + string.Join("\n", add) + "\n";
            var res = await WriteAsync(path, body, null, currentActor.Actor?.UserId, guardGenerated: false, note: done != null ? "việc: đã xử lý " + done.Key : "sổ việc");
            return res.ResponseCode == 200 ? ParseLedger(body, day) : map;
        }
    }
}
