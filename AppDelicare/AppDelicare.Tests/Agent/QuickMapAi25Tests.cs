using System.Text.RegularExpressions;
using AppDelicare.Services.Implement.Agent;
using FluentAssertions;

namespace AppDelicare.Tests.Agent
{
    /// <summary>AI-25.1 (D-143): bảng ánh xạ một chỗ – mọi cột / chỗ trống / bộ lọc trong 72 câu nhanh ánh xạ được sang trường thật (QuickSpecs); nhãn không có dữ liệu phải khai trong Dropped.</summary>
    public class QuickMapAi25Tests
    {
        [Fact]
        public void Every_column_placeholder_and_filter_maps_to_a_real_field()
        {
            var (qs, _) = QuickCatalog.Load(QuickCatalog.Root());
            var errs = new List<string>();
            var ph = new Regex(@"\{([a-z_]+)(?:\.([a-z_]+))?(?:=([^}]+))?\}");
            foreach (var q in qs)
            {
                if (!QuickSpecs.Tools.TryGetValue(q.Tool, out var spec))
                {
                    errs.Add($"{q.Id}: chưa có ánh xạ cho công cụ {q.Tool}");
                    continue;
                }
                var dropped = QuickSpecs.Dropped.GetValueOrDefault(q.Tool) ?? [];
                errs.AddRange(q.View.Cot.Where(c => !spec.Cols.ContainsKey(c) && !dropped.Contains(c)).Select(c => $"{q.Id}: cột «{c}» chưa ánh xạ"));
                foreach (string text in new[] { q.View.TieuDe, q.View.TomTat, q.View.Rong }.Where(t => t != null))
                {
                    foreach (Match m in ph.Matches(text!))
                    {
                        string a = m.Groups[1].Value, b = m.Groups[2].Value;
                        bool ok = a is "so_dong" or "ngay" || (a == "args" && (q.Args.ContainsKey(b) || q.Nhap.ContainsKey(b))) || (b.Length == 0 && spec.Root.ContainsKey(a))
                            || (a == "dem" && spec.Fields.ContainsKey(b)) || (a is "tong" or "tb" or "min" or "pct" && (spec.Root.ContainsKey(b) || spec.Fields.ContainsKey(b)));
                        if (!ok) errs.Add($"{q.Id}: chỗ trống {m.Value} chưa ánh xạ");
                    }
                }
                if (q.Loc != null && !QuickSpecs.Filters.ContainsKey(q.Id)) errs.Add($"{q.Id}: bộ lọc «{q.Loc}» chưa có");
            }
            errs.AddRange(QuickSpecs.Filters.Keys.Where(k => qs.All(q => q.Id != k || q.Loc == null)).Select(k => $"bộ lọc thừa {k}"));
            errs.Should().BeEmpty(string.Join(" ¦ ", errs));
        }
    }
}
