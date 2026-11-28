using System.Collections.Concurrent;

namespace AppDelicare.Services.Implement.Agent
{
    public sealed class QuickView
    {
        /// <summary>bang · the · danh_sach · bieu_do · tom_tat.</summary>
        public string Kieu { get; init; } = "bang";
        public string TieuDe { get; init; } = string.Empty;
        public string? TomTat { get; init; }
        public List<string> Cot { get; init; } = [];
        public string? SapXep { get; init; }
        public string? Rong { get; init; }
    }

    /// <summary>AI-25 (D-143): 1 câu hỏi nhanh trong <c>quick.yaml</c> – chạy công cụ + trình bày theo mẫu, KHÔNG qua AI.</summary>
    public sealed class QuickQuestion
    {
        public string Id { get; init; } = string.Empty;
        public string Agent { get; init; } = string.Empty;
        public string Hoi { get; init; } = string.Empty;
        public string Tool { get; init; } = string.Empty;
        public Dictionary<string, string> Args { get; init; } = [];
        public Dictionary<string, string> Nhap { get; init; } = [];
        public string? Loc { get; init; }
        public QuickView View { get; init; } = new();
        public string? MoMan { get; init; }
        public string? PhanTich { get; init; }
        public List<string> Kenh { get; init; } = [];
    }

    public sealed record QuickBlock(string Cau, int Dong, bool AnKhiRong);

    /// <summary>AI-25.4: 1 bản tin trong <c>ban-tin.yaml</c> dựng từ câu nhanh.</summary>
    public sealed class QuickBrief
    {
        public string Id { get; init; } = string.Empty;
        public string Ten { get; init; } = string.Empty;
        public string Gio { get; init; } = string.Empty;
        public string MucQuyen { get; init; } = string.Empty;
        public string Kenh { get; init; } = string.Empty;
        public List<QuickBlock> Khoi { get; init; } = [];
    }

    /// <summary>
    /// AI-25.1: nạp <c>app-delicare-agents/*/quick.yaml</c> + <c>ban-tin.yaml</c> (cùng chỗ tìm thư mục agent như schedules.json – <see cref="AgentAdmin"/>); nạp lại khi tệp đổi (theo thời điểm ghi).
    /// </summary>
    public static class QuickCatalog
    {
        public static readonly string[] Channels = ["web", "VH", "DH", "ZALO"];
        private static readonly ConcurrentDictionary<string, (DateTime Stamp, List<QuickQuestion> Questions, List<QuickBrief> Briefs)> Cache = new();

        /// <summary>Thư mục app-delicare-agents (đi ngược lên từ thư mục chạy; Docker: /app/app-delicare-agents nếu mount).</summary>
        public static string? Root(string? configured = null)
        {
            if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
            {
                return configured;
            }
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                string p = Path.Combine(dir.FullName, "app-delicare-agents");
                if (Directory.Exists(p))
                {
                    return p;
                }
                dir = dir.Parent;
            }
            return null;
        }

        public static (List<QuickQuestion> Questions, List<QuickBrief> Briefs) Load(string? root)
        {
            if (root == null || !Directory.Exists(root))
            {
                return ([], []);
            }
            var files = Directory.EnumerateFiles(root, "quick.yaml", SearchOption.AllDirectories).Append(Path.Combine(root, "ban-tin.yaml")).Where(File.Exists).ToList();
            DateTime stamp = files.Select(File.GetLastWriteTimeUtc).DefaultIfEmpty(DateTime.MinValue).Max();
            if (Cache.TryGetValue(root, out var c) && c.Stamp == stamp)
            {
                return (c.Questions, c.Briefs);
            }
            var qs = new List<QuickQuestion>();
            foreach (string f in files.Where(f => Path.GetFileName(f) == "quick.yaml").OrderBy(f => f, StringComparer.Ordinal))
            {
                if (QuickYaml.Parse(File.ReadAllText(f)) is not Dictionary<string, object?> doc)
                {
                    continue;
                }
                string agent = doc.Str("agent") ?? Path.GetFileName(Path.GetDirectoryName(f)!);
                foreach (var o in doc.Arr("questions").OfType<Dictionary<string, object?>>())
                {
                    var v = o.Obj("hien_thi") ?? [];
                    qs.Add(new QuickQuestion
                    {
                        Id = o.Str("id") ?? string.Empty, Agent = agent, Hoi = o.Str("hoi") ?? string.Empty, Tool = o.Str("tool") ?? string.Empty,
                        Args = (o.Obj("args") ?? []).ToDictionary(x => x.Key, x => x.Value?.ToString() ?? string.Empty), Nhap = (o.Obj("nhap") ?? []).ToDictionary(x => x.Key, x => x.Value?.ToString() ?? string.Empty),
                        Loc = o.Str("loc"), MoMan = o.Str("mo_man"), PhanTich = o.Str("phan_tich"), Kenh = [.. o.Arr("kenh").Select(x => x?.ToString() ?? string.Empty)],
                        View = new QuickView
                        {
                            Kieu = v.Str("kieu") ?? "bang", TieuDe = v.Str("tieu_de") ?? string.Empty, TomTat = v.Str("tom_tat"), Cot = [.. v.Arr("cot").Select(x => x?.ToString() ?? string.Empty)],
                            SapXep = v.Str("sap_xep"), Rong = v.Str("rong"),
                        },
                    });
                }
            }
            var briefs = new List<QuickBrief>();
            string bt = Path.Combine(root, "ban-tin.yaml");
            if (File.Exists(bt) && QuickYaml.Parse(File.ReadAllText(bt)) is Dictionary<string, object?> bd)
            {
                foreach (var o in bd.Arr("ban_tin").OfType<Dictionary<string, object?>>())
                {
                    briefs.Add(new QuickBrief
                    {
                        Id = o.Str("id") ?? string.Empty, Ten = o.Str("ten") ?? string.Empty, Gio = o.Str("gio") ?? string.Empty, MucQuyen = o.Str("muc_quyen") ?? string.Empty, Kenh = o.Str("kenh") ?? string.Empty,
                        Khoi = [.. o.Arr("khoi").OfType<Dictionary<string, object?>>().Select(k => new QuickBlock(k.Str("cau") ?? string.Empty, k.Int("dong") ?? 0, k.Bool("an_khi_rong")))],
                    });
                }
            }
            Cache[root] = (stamp, qs, briefs);
            return (qs, briefs);
        }
    }
}
