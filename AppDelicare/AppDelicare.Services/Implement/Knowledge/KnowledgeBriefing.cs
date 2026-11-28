using System.Text;
using AppDelicare.Data.Interface;

namespace AppDelicare.Services.Implement.Knowledge
{
    /// <summary>
    /// AI-25.4 (D-143): bản tin Ban GĐ dựng bằng mẫu → 1 ghi chú / bản tin / ngày ở <c>10 Ban GĐ/Bản tin/yyyy/</c> (source delicare-core – chỉ đọc; chạy lại cùng ngày = ghi đè bản mới).
    /// </summary>
    public partial class KnowledgeService
    {
        public const string BriefingDir = "Bản tin";

        public async Task<IResult<KnowledgeSaveResultModel>> WriteBriefingAsync(string id, string title, DateTime day, IReadOnlyList<string> lines)
        {
            var folder = KnowledgeVault.FolderByCode("10")!;
            string path = $"{folder.Dir}/{BriefingDir}/{day:yyyy}/{day:yyyy-MM-dd} {id}.md";
            string text = KnowledgeVault.Frontmatter([
                ("id", $"ban-tin-{id}-{day:yyyyMMdd}"), ("type", "ban-tin"), ("source", KnowledgeVault.CoreSource), ("owner", folder.Owner), ("status", "hiệu lực"),
                ("valid_from", day.ToString("yyyy-MM-dd")), ("review_by", day.AddYears(1).ToString("yyyy-MM-dd")), ("confidentiality", "nội bộ"), ("refs", "[]"),
                ("title", $"{title} {day:dd/MM/yyyy}"),
            ]) + "\n" + new StringBuilder().AppendLine($"# {title} – {day:dd/MM/yyyy}").AppendLine()
                .AppendLine("_Dựng bằng mẫu từ câu hỏi nhanh (không dùng AI) – bản đầy đủ nhất trong số người nhận, lúc " + day.ToString("HH:mm") + "._").AppendLine()
                .AppendJoin('\n', lines.Select(l => l.StartsWith("▪ ", StringComparison.Ordinal) ? "\n**" + l[2..] + "**" : "- " + l.Trim().TrimStart('•').Trim()))
                .AppendLine().ToString();
            string full = Path.Combine(Root, path);
            string? prev = File.Exists(full) ? KnowledgeVault.Sha256(await File.ReadAllTextAsync(full, Encoding.UTF8)) : null;
            return await WriteAsync(path, text, prev, null, guardGenerated: false, note: "bản tin " + id);
        }
    }
}
