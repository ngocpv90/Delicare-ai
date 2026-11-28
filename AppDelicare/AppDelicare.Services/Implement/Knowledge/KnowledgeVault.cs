using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace AppDelicare.Services.Implement.Knowledge
{
    /// <summary>1 thư mục lớn của kho (khung 10–99 + BOD, §7.1). <see cref="Screen"/> = mục menu cấp quyền (AI-6 seed).</summary>
    public sealed record KnowledgeFolder(string Code, string Dir, string Owner, bool AgentWritable = false)
    {
        public string Screen => "knowledge?folder=" + Code.ToLowerInvariant();
    }

    /// <summary>AI-5 (D-133): khung kho + luật cứng §7.5 dạng hằng / hàm thuần (không I/O) – test được độc lập.</summary>
    public static partial class KnowledgeVault
    {
        public const string Bod = "BOD";
        public const string CoreSource = "delicare-core";
        public const string HumanSource = "human";

        public static readonly IReadOnlyList<KnowledgeFolder> Folders =
        [
            new("10", "10 Ban GĐ", "CEO / COO"),
            new("20", "20 Kinh doanh & Hợp đồng", "TP Kinh doanh"),
            new("30", "30 Thực đơn & Công thức", "Trưởng bếp"),
            new("40", "40 Kho & Truy xuất", "Thủ kho / Mua hàng"),
            new("45", "45 ATTP", "Trưởng QA"),
            new("50", "50 Sản xuất", "Trưởng bếp"),
            new("55", "55 Giao vận", "Điều phối giao vận"),
            new("60", "60 Kế toán", "Kế toán trưởng"),
            new("70", "70 Nhân sự", "TP Nhân sự"),
            new("80", "80 Đào tạo", "TP Vận hành"),
            new("95", "95 Nguồn BOT", "Admin"),
            new("96", "96 Nhật ký", "KNOW", AgentWritable: true),
            new("97", "97 Tương tác", "KNOW", AgentWritable: true),
            new("98", "98 Công việc", "KNOW", AgentWritable: true),
            new("99", "99 Mẫu", "Admin"),
            new(Bod, "BOD Bảo mật", "CEO"),
            // AI-22.1 (D-141, §7.9): kho tài liệu doanh nghiệp – mỗi vùng 1 mục quyền riêng; vùng con = thư mục con của thư mục lớn (quyền riêng, không thừa kế thư mục cha).
            new("05", "05 Công văn", "Hành chính"),
            new("15", "15 Họp & giao ban", "Người lập biên bản"),
            new("20-HD", "20 Kinh doanh & Hợp đồng/Hợp đồng khách hàng", "TP Kinh doanh"),
            new("40-HD", "40 Kho & Truy xuất/Hợp đồng nhà cung cấp", "Mua hàng"),
            new("60-CT", "60 Kế toán/Chứng từ gốc", "Kế toán"),
            new(Personal, "70 Nhân sự/Hồ sơ cá nhân", "TP Nhân sự"),
            new(UserGuide, "80 Đào tạo/Hướng dẫn sử dụng", "Máy (tự sinh từ docs/user-guide)"),
        ];

        /// <summary>AI-22.1: vùng cá nhân (hồ sơ nhân viên, HĐLĐ) – chỉ người được cấp đích danh (admin không tự thấy); agent chỉ HR khi người hỏi có quyền.</summary>
        public const string Personal = "70-HS";
        /// <summary>AI-22.3: Hướng dẫn sử dụng (máy tự nhập) – mọi tài khoản nội bộ.</summary>
        public const string UserGuide = "80-HDSD";
        /// <summary>Mục công khai nội bộ (mọi tài khoản nội bộ xem).</summary>
        public static readonly string[] PublicInternal = ["70", "80", UserGuide, "99"];
        /// <summary>§7.9 luật 3 (Quyết định tự chọn AI-22.1): việc theo lịch / không người hỏi KHÔNG đọc vùng tài liệu hạn chế + công văn + biên bản họp (và ghi chú confidentiality hạn chế); phần còn lại theo phạm vi agent như AI-3…AI-16.</summary>
        public static readonly string[] NoAskerBlocked = ["05", "15", "20-HD", "40-HD", "60-CT", Personal, Bod];
        /// <summary>§7.9 luật 4 / 5: mục hạn chế – ghi nhật ký mỗi lần xem / tải file; KHÔNG đồng bộ Obsidian máy cá nhân.</summary>
        public static readonly string[] Restricted = ["20-HD", "40-HD", "60-CT", Personal, Bod];

        public static KnowledgeFolder? FolderByCode(string? code) => Folders.FirstOrDefault(f => string.Equals(f.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase));

        /// <summary>Thư mục của đường dẫn: khớp dài nhất (vùng con "20 Kinh doanh &amp; Hợp đồng/Hợp đồng khách hàng" thắng thư mục cha).</summary>
        public static KnowledgeFolder? FolderOfPath(string relPath)
        {
            string p = relPath.Replace('\\', '/');
            return Folders.Where(f => p.Equals(f.Dir, StringComparison.OrdinalIgnoreCase) || p.StartsWith(f.Dir + "/", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => f.Dir.Length).FirstOrDefault();
        }

        /// <summary>Phạm vi đọc của agent (§7.8). null = mọi thư mục trừ BOD. CSKH: chỉ ghi chú công khai (lọc ở ghi chú).</summary>
        public static string[]? AgentScope(string agent) => agent.ToUpperInvariant() switch
        {
            "VIVI" or "KNOW" => null,
            "SALES" => ["20", "20-HD", "30", "55", "80"],
            "PLAN" => ["30", "40", "50", "80"],
            "PROC" => ["30", "40", "40-HD", "60", "80"],
            "WH" => ["40", "45", "80"],
            "KITCHEN" => ["30", "45", "50", "80"],
            "QA" => ["40", "45", "50", "55", "80"],
            "LOG" => ["20", "55", "80"],
            "PKG" => ["20", "45", "55", "80"],
            "HR" => ["70", Personal, "80"],
            "ACC" => ["20", "40", "60", "60-CT", "80"],
            "CSKH" => ["20", "30", "55", "80"],
            _ => [],
        };

        // ---------------- frontmatter (§7.5-3)
        public static readonly string[] Types = ["sop", "su-co", "bai-hoc", "bao-cao", "quyet-dinh", "ho-so", "nhat-ky", "huong-dan"];
        public static readonly string[] Statuses = ["nhap", "cho-duyet", "hieu-luc", "luu-tru"];
        public static readonly string[] Confidentialities = ["cong-khai", "noi-bo", "han-che", "bod"];
        public static readonly string[] Required = ["id", "type", "source", "owner", "status", "valid_from", "review_by", "confidentiality", "refs"];

        public sealed class Note
        {
            public string Path { get; init; } = string.Empty;
            public string Folder { get; init; } = string.Empty;
            public Dictionary<string, string> Meta { get; init; } = new(StringComparer.OrdinalIgnoreCase);
            public List<string> Refs { get; init; } = [];
            public string Body { get; init; } = string.Empty;
            public string Title { get; init; } = string.Empty;
            public string Hash { get; init; } = string.Empty;
            public bool HasFrontmatter { get; init; }
            public string Id => Meta.GetValueOrDefault("id") ?? Path;
            public string Type => Slug(Meta.GetValueOrDefault("type"));
            public string Status => Slug(Meta.GetValueOrDefault("status"));
            public string Source => (Meta.GetValueOrDefault("source") ?? string.Empty).Trim().ToLowerInvariant();
            public string Confidentiality => Slug(Meta.GetValueOrDefault("confidentiality"));
            public DateTime? ValidFrom => ParseDate(Meta.GetValueOrDefault("valid_from"));
            public DateTime? ReviewBy => ParseDate(Meta.GetValueOrDefault("review_by"));
            public bool Generated => Source == CoreSource;
            public bool IsBod => Folder == Bod || Confidentiality == "bod";
            /// <summary>Dạng gấp (không dấu, thường) của tiêu đề + nội dung – chỉ mục tìm.</summary>
            public string FoldedTitle { get; init; } = string.Empty;
            public string FoldedBody { get; init; } = string.Empty;
        }

        public static string Sha256(string content) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

        public static Note Parse(string relPath, string content)
        {
            var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var refs = new List<string>();
            string body = content;
            bool has = false;
            string text = content.Replace("\r\n", "\n").Normalize(NormalizationForm.FormC);
            if (text.StartsWith("---\n", StringComparison.Ordinal))
            {
                int end = text.IndexOf("\n---", 4, StringComparison.Ordinal);
                if (end > 0)
                {
                    has = true;
                    string? listKey = null;
                    foreach (string raw in text[4..end].Split('\n'))
                    {
                        string line = raw.TrimEnd();
                        if (line.Length == 0)
                        {
                            continue;
                        }
                        if (listKey != null && line.TrimStart().StartsWith("- ", StringComparison.Ordinal))
                        {
                            if (listKey == "refs")
                            {
                                refs.Add(Unquote(line.TrimStart()[2..]));
                            }
                            continue;
                        }
                        int c = line.IndexOf(':');
                        if (c <= 0)
                        {
                            continue;
                        }
                        string key = line[..c].Trim().ToLowerInvariant(), val = line[(c + 1)..].Trim();
                        meta[key] = Unquote(val);
                        listKey = val.Length == 0 ? key : null;
                        if (key == "refs" && val.StartsWith('[') && val.EndsWith(']'))
                        {
                            refs.AddRange(val[1..^1].Split(',').Select(Unquote).Where(x => x.Length > 0));
                        }
                    }
                    int after = text.IndexOf('\n', end + 4);
                    body = after < 0 ? string.Empty : text[(after + 1)..];
                }
            }
            string title = meta.GetValueOrDefault("title") is { Length: > 0 } t ? t
                : body.Split('\n').FirstOrDefault(l => l.StartsWith("# ", StringComparison.Ordinal))?[2..].Trim()
                ?? System.IO.Path.GetFileNameWithoutExtension(relPath);
            var folder = FolderOfPath(relPath);
            return new Note
            {
                Path = relPath, Folder = folder?.Code ?? string.Empty, Meta = meta, Refs = refs, Body = body, Title = title, Hash = Sha256(content), HasFrontmatter = has,
                FoldedTitle = Fold(title), FoldedBody = Fold(body),
            };
        }

        private static string Unquote(string v)
        {
            v = v.Trim();
            return v.Length >= 2 && (v[0] == '"' && v[^1] == '"' || v[0] == '\'' && v[^1] == '\'') ? v[1..^1] : v;
        }

        public static DateTime? ParseDate(string? s)
            => DateTime.TryParseExact(s?.Trim(), ["yyyy-MM-dd", "dd/MM/yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

        /// <summary>Lỗi frontmatter (null = hợp lệ) – thiếu trường / giá trị ngoài danh mục.</summary>
        public static string? ValidateFrontmatter(Note n)
        {
            if (!n.HasFrontmatter)
            {
                return "Thiếu frontmatter (khối --- đầu ghi chú)";
            }
            var missing = Required.Where(k => !n.Meta.ContainsKey(k) || (k != "refs" && string.IsNullOrWhiteSpace(n.Meta[k]))).ToList();
            if (missing.Count > 0)
            {
                return "Thiếu trường frontmatter: " + string.Join(", ", missing);
            }
            if (!Types.Contains(n.Type))
            {
                return $"type «{n.Meta["type"]}» không hợp lệ (" + string.Join(" / ", Types) + ")";
            }
            if (!Statuses.Contains(n.Status))
            {
                return $"status «{n.Meta["status"]}» không hợp lệ (nháp / chờ duyệt / hiệu lực / lưu trữ)";
            }
            if (!Confidentialities.Contains(n.Confidentiality))
            {
                return $"confidentiality «{n.Meta["confidentiality"]}» không hợp lệ (công khai / nội bộ / hạn chế / BOD)";
            }
            if (n.Source != CoreSource && n.Source != HumanSource && !n.Source.StartsWith("agent:", StringComparison.Ordinal))
            {
                return "source phải là delicare-core / human / agent:<MÃ>";
            }
            if (n.ValidFrom == null || n.ReviewBy == null)
            {
                return "valid_from / review_by phải là ngày yyyy-MM-dd";
            }
            return null;
        }

        // ---------------- tìm không dấu
        /// <summary>Gấp 1 ký tự: bỏ dấu, thường, đ → d (giữ nguyên độ dài chuỗi để cắt đoạn trích đúng chỗ).</summary>
        public static char Fold(char c)
        {
            if (c is 'đ' or 'Đ')
            {
                return 'd';
            }
            if (c < 128)
            {
                return char.ToLowerInvariant(c);
            }
            string d = c.ToString().Normalize(NormalizationForm.FormD);
            return char.ToLowerInvariant(d[0]);
        }

        public static string Fold(string? s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }
            var chars = new char[s.Length];
            for (int i = 0; i < s.Length; i++)
            {
                chars[i] = Fold(s[i]);
            }
            return new string(chars);
        }

        public static string Slug(string? s)
        {
            string f = Fold(s).Trim();
            return SlugRx().Replace(f, "-").Trim('-');
        }

        [GeneratedRegex(@"[^a-z0-9]+")]
        private static partial Regex SlugRx();

        public static string[] Tokens(string? q) => SlugRx().Split(Fold(q)).Where(t => t.Length >= 2).Distinct().ToArray();

        // ---------------- quét dữ liệu cá nhân khi lưu (§7.5-8)
        [GeneratedRegex(@"(?<![\w-])(\+?84|0)([\s.]?\d){8,10}(?![\w-])")]
        private static partial Regex PhoneRx();

        [GeneratedRegex(@"(?<![\w-])\d{9,16}(?![\w-])")]
        private static partial Regex LongNumberRx();

        [GeneratedRegex(@"\b(cccd|cmnd|stk|so tai khoan|tai khoan ngan hang|mat khau|password|token)\b\s*[:=]?\s*\S+", RegexOptions.IgnoreCase)]
        private static partial Regex SecretWordRx();

        /// <summary>Lý do từ chối (null = sạch): số điện thoại, CCCD / số tài khoản (chuỗi 9–16 số), "CCCD: …", mật khẩu / token.</summary>
        public static string? ScanSensitive(string content)
        {
            if (PhoneRx().IsMatch(content))
            {
                return "Có số điện thoại";
            }
            if (LongNumberRx().IsMatch(content))
            {
                return "Có chuỗi số giống CCCD / số tài khoản";
            }
            if (SecretWordRx().IsMatch(Fold(content)))
            {
                return "Có CCCD / số tài khoản / mật khẩu / token";
            }
            return null;
        }

        // ---------------- {{BusinessRules:...}} (§7.5-2)
        [GeneratedRegex(@"\{\{\s*(BusinessRules:[A-Za-z0-9:_.]+)\s*\}\}")]
        public static partial Regex RuleRx();

        [GeneratedRegex(@"\[\[([^\]|#]+)(?:[#|][^\]]*)?\]\]")]
        public static partial Regex LinkRx();

        /// <summary>Đường dẫn tương đối hợp lệ trong kho ("45 ATTP/abc.md") – chặn "..", tuyệt đối, ngoài khung.</summary>
        public static string? NormalizePath(string? path, out string? error)
        {
            error = null;
            string p = (path ?? string.Empty).Replace('\\', '/').Trim().Trim('/');
            if (p.Length == 0 || p.Contains("..", StringComparison.Ordinal) || p.Contains(':') || p.StartsWith('/'))
            {
                error = "Đường dẫn ghi chú không hợp lệ";
                return null;
            }
            if (!p.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                p += ".md";
            }
            if (FolderOfPath(p) == null)
            {
                error = "Ghi chú phải nằm trong một thư mục của khung kho (10 … 99, BOD)";
                return null;
            }
            if (p.Split('/').Any(seg => seg.Length == 0 || seg.StartsWith('.')))
            {
                error = "Đường dẫn ghi chú không hợp lệ";
                return null;
            }
            return p;
        }

        public static Guid KeyGuid(string relPath) => new(MD5.HashData(Encoding.UTF8.GetBytes("knowledge:" + relPath.ToLowerInvariant())));

        // ---------------- chỉ mục trong bộ nhớ (dựng lại khi file đổi)
        private sealed record Entry(DateTime WriteUtc, long Length, Note Note);

        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Entry>> Indexes = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new(StringComparer.OrdinalIgnoreCase);

        public static SemaphoreSlim LockFor(string root, string relPath) => Locks.GetOrAdd(root + "|" + relPath.ToLowerInvariant(), _ => new SemaphoreSlim(1, 1));

        /// <summary>Đồng bộ chỉ mục với đĩa (đọc lại file đổi theo thời điểm ghi / kích thước) và trả danh sách ghi chú.</summary>
        public static IReadOnlyList<Note> Scan(string root)
        {
            var index = Indexes.GetOrAdd(root, _ => new ConcurrentDictionary<string, Entry>(StringComparer.OrdinalIgnoreCase));
            if (!Directory.Exists(root))
            {
                index.Clear();
                return [];
            }
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string file in Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories))
            {
                string rel = System.IO.Path.GetRelativePath(root, file).Replace('\\', '/');
                if (FolderOfPath(rel) == null)
                {
                    continue;
                }
                seen.Add(rel);
                var info = new FileInfo(file);
                if (index.TryGetValue(rel, out var e) && e.WriteUtc == info.LastWriteTimeUtc && e.Length == info.Length)
                {
                    continue;
                }
                try
                {
                    index[rel] = new Entry(info.LastWriteTimeUtc, info.Length, Parse(rel, File.ReadAllText(file, Encoding.UTF8)));
                }
                catch (IOException)
                {
                    // file đang được ghi – lần sau đọc lại
                }
            }
            foreach (string gone in index.Keys.Where(k => !seen.Contains(k)).ToList())
            {
                index.TryRemove(gone, out _);
            }
            return [.. index.Values.Select(v => v.Note)];
        }

        /// <summary>Ghi nguyên tử (tệp tạm → đổi tên) – không để file dở khi máy dừng giữa chừng.</summary>
        public static void WriteAtomic(string fullPath, string content)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
            string tmp = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
            File.WriteAllText(tmp, content, new UTF8Encoding(false));
            File.Move(tmp, fullPath, overwrite: true);
        }

        public static string Frontmatter(IEnumerable<(string Key, string Value)> fields)
        {
            var sb = new StringBuilder("---\n");
            foreach (var (k, v) in fields)
            {
                sb.Append(k).Append(": ").Append(v).Append('\n');
            }
            return sb.Append("---\n").ToString();
        }

        public static string FileSafe(string s)
        {
            var bad = System.IO.Path.GetInvalidFileNameChars().Concat(['/', '\\', ':', '#', '[', ']', '|', '^']).ToHashSet();
            string r = new([.. s.Trim().Select(c => bad.Contains(c) ? '-' : c)]);
            return r.Length > 80 ? r[..80].Trim() : r;
        }
    }
}
