using AppDelicare.Services.Implement.Agent;
using FluentAssertions;

namespace AppDelicare.Tests.Agent
{
    /// <summary>
    /// AI-25.6 (D-143) – kiểm tệp câu nhanh: đọc đủ 72 câu / 13 agent + 5 bản tin; id không trùng; tool có trong tools.txt của agent; không propose_*; kenh hợp lệ;
    /// mo_man là màn có trong catalog.js; câu trong bản tin tồn tại.
    /// </summary>
    public class QuickCatalogAi25Tests
    {
        private static string Root() => QuickCatalog.Root() ?? throw new InvalidOperationException("Không tìm thấy app-delicare-agents");

        [Fact]
        public void Quick_files_parse_and_follow_the_rules()
        {
            string root = Root();
            var (qs, briefs) = QuickCatalog.Load(root);
            qs.Should().HaveCount(72);
            qs.Select(q => q.Agent).Distinct().Should().HaveCount(13);
            briefs.Should().HaveCount(5);
            qs.Select(q => q.Id).Should().OnlyHaveUniqueItems();
            string catalog = File.ReadAllText(Path.Combine(root, "..", "app-delicare-client", "src", "assets", "delicare", "js", "data", "catalog.js"));
            var errs = new List<string>();
            foreach (var q in qs)
            {
                var tools = File.ReadAllLines(Path.Combine(root, q.Agent, "tools.txt")).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#')).ToHashSet();
                if (!tools.Contains(q.Tool)) errs.Add($"{q.Id}: {q.Tool} không có trong tools.txt của {q.Agent}");
                if (q.Tool.StartsWith("propose_", StringComparison.Ordinal)) errs.Add($"{q.Id}: câu nhanh không được là lệnh ghi");
                if (q.Kenh.Count == 0 || q.Kenh.Any(k => !QuickCatalog.Channels.Contains(k))) errs.Add($"{q.Id}: kenh không hợp lệ ({string.Join(",", q.Kenh)})");
                if (q.Hoi.Length == 0 || q.View.TieuDe.Length == 0) errs.Add($"{q.Id}: thiếu hoi / tieu_de");
                if (q.MoMan is { Length: > 0 } m)
                {
                    string screen = m.TrimStart('#', '/').Split('?')[0];
                    if (!catalog.Contains($"'{screen}'", StringComparison.Ordinal) && !catalog.Contains($"\"{screen}\"", StringComparison.Ordinal) && !catalog.Contains($"id:'{screen}'", StringComparison.Ordinal))
                    {
                        errs.Add($"{q.Id}: mo_man {m} không có trong catalog.js");
                    }
                }
            }
            foreach (var b in briefs)
            {
                foreach (var k in b.Khoi.Where(k => qs.All(q => q.Id != k.Cau)))
                {
                    errs.Add($"bản tin {b.Id}: câu {k.Cau} không tồn tại");
                }
            }
            errs.Should().BeEmpty(string.Join(" ¦ ", errs));
        }
    }
}
