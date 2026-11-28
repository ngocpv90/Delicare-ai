using System.Text;
using System.Text.RegularExpressions;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using static AppDelicare.Services.Implement.Knowledge.KnowledgeVault;

namespace AppDelicare.Services.Implement.Knowledge
{
    /// <summary>
    /// AI-22.7 (D-141 k, §7.10): trích chữ tài liệu gốc lúc tải lên → <c>&lt;tệp&gt;.txt</c> cạnh tệp gốc (ẩn, chỉ máy đọc; không phục vụ qua API tải xuống), chia trang bằng dòng <c>[Trang N]</c>.
    /// Word: đọc gói OpenXML (KnowledgeImport); Excel: ClosedXML; PDF có lớp chữ: PdfPig 0.1.16 (Apache 2.0 – anh duyệt 2026-11-28). Scan / ảnh / PDF không có chữ → <c>ocr: cho-ai</c>
    /// (KNOW đọc một lần qua Hermes, ghi bản chữ <c>ocr: ai</c>) – TRỪ vùng cá nhân / BOD: không gửi AI, người tải lên phải nhập tóm tắt.
    /// </summary>
    public static class KnowledgeExtract
    {
        public const int MaxChars = 400_000;
        public static readonly Regex PageMark = new(@"^\[Trang (\d+)\]$", RegexOptions.Multiline | RegexOptions.Compiled);

        /// <summary>(Bản chữ, số trang, cần đọc ảnh). Lỗi đọc tệp → coi như cần đọc ảnh (không chặn tải lên).</summary>
        public static (string Text, int Pages, bool NeedsOcr) Extract(byte[] bytes, string ext)
        {
            try
            {
                switch (ext.ToLowerInvariant())
                {
                    case ".pdf":
                    {
                        using var doc = UglyToad.PdfPig.PdfDocument.Open(bytes);
                        var sb = new StringBuilder();
                        int pages = 0, chars = 0;
                        foreach (var page in doc.GetPages())
                        {
                            pages++;
                            string t = Regex.Replace(page.Text ?? string.Empty, @"[ \t]+", " ").Trim();
                            chars += t.Length;
                            sb.Append($"[Trang {page.Number}]\n").Append(t).Append("\n\n");
                            if (sb.Length > MaxChars)
                            {
                                break;
                            }
                        }
                        return chars < 20 ? (string.Empty, pages, true) : (Cut(sb.ToString()), pages, false);
                    }
                    case ".docx":
                    {
                        using var ms = new MemoryStream(bytes);
                        string md = KnowledgeImport.WordToMarkdown(ms);
                        return md.Trim().Length < 5 ? (string.Empty, 1, true) : ("[Trang 1]\n" + Cut(md), 1, false);
                    }
                    case ".xlsx":
                    {
                        using var ms = new MemoryStream(bytes);
                        using var wb = new ClosedXML.Excel.XLWorkbook(ms);
                        var sb = new StringBuilder();
                        int n = 0;
                        foreach (var ws in wb.Worksheets)
                        {
                            n++;
                            sb.Append($"[Trang {n}]\n## {ws.Name}\n");
                            foreach (var row in ws.RowsUsed())
                            {
                                sb.Append(string.Join(" | ", row.CellsUsed().Select(c => { try { return c.CachedValue.ToString(); } catch { return c.GetString(); } }))).Append('\n');
                                if (sb.Length > MaxChars)
                                {
                                    break;
                                }
                            }
                        }
                        return (Cut(sb.ToString()), n, false);
                    }
                    default:
                        return (string.Empty, 1, true); // jpg / png
                }
            }
            catch (Exception)
            {
                return (string.Empty, 0, true);
            }
        }

        private static string Cut(string s) => s.Length > MaxChars ? s[..MaxChars] : s;

        /// <summary>Trang chứa vị trí <paramref name="at"/> trong bản chữ (dòng [Trang N] gần nhất phía trước).</summary>
        public static int? PageAt(string text, int at)
        {
            int? page = null;
            foreach (Match m in PageMark.Matches(text))
            {
                if (m.Index > at)
                {
                    break;
                }
                page = int.Parse(m.Groups[1].Value);
            }
            return page;
        }

        /// <summary>Nội dung 1 trang (hoặc cả bản chữ khi không chia trang).</summary>
        public static string PageText(string text, int page)
        {
            var marks = PageMark.Matches(text).ToList();
            var m = marks.FirstOrDefault(x => x.Groups[1].Value == page.ToString());
            if (m == null)
            {
                return string.Empty;
            }
            int start = m.Index + m.Length, end = marks.FirstOrDefault(x => x.Index > m.Index)?.Index ?? text.Length;
            return text[start..end].Trim();
        }
    }

    /// <summary>AI-22.7: đọc phần dài của 1 tài liệu cho agent (doc_tai_lieu) – trả kèm nguồn.</summary>
    public sealed class KnowledgeDocReadModel
    {
        public string Id { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Folder { get; set; } = string.Empty;
        public string? SoHieu { get; set; }
        public string? Date { get; set; }
        public int? Page { get; set; }
        public int Pages { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? Summary { get; set; }
        public string? Note { get; set; }
    }

    public partial class KnowledgeService
    {
        private string TxtPathOf(Note n)
        {
            string dir = Path.GetDirectoryName(n.Path)!.Replace('\\', '/');
            return Path.Combine(Root, dir, Path.GetFileNameWithoutExtension(n.Meta.GetValueOrDefault("file") ?? string.Empty) + ".txt");
        }

        /// <summary>doc_tai_lieu: nội dung 1 trang (hoặc 6.000 ký tự đầu) – theo quyền agent ∩ người hỏi; ACC ở 60 › Chứng từ gốc chỉ đọc phiếu mô tả (§7.9).</summary>
        public async Task<IResult<KnowledgeDocReadModel>> ReadDocFor(KnowledgeCaller caller, string? idOrPath, int? page)
        {
            var rights = await RightsAsync(caller);
            string key = (idOrPath ?? string.Empty).Trim();
            var n = All().FirstOrDefault(x => (string.Equals(x.Path, key, StringComparison.OrdinalIgnoreCase) || string.Equals(x.Id, key, StringComparison.OrdinalIgnoreCase)) && rights.NoteVisible(x));
            if (n == null)
            {
                return Status<KnowledgeDocReadModel>(404, "Kho chưa có tài liệu này hoặc người hỏi không có quyền xem");
            }
            var m = new KnowledgeDocReadModel
            {
                Id = n.Id, Path = n.Path, Title = n.Title, Folder = n.Folder, SoHieu = n.Meta.GetValueOrDefault("so_hieu"), Date = n.Meta.GetValueOrDefault("ngay") ?? n.Meta.GetValueOrDefault("valid_from"),
                Summary = n.Meta.GetValueOrDefault("tom_tat"),
            };
            bool descOnly = caller.AgentCode?.ToUpperInvariant() == "ACC" && n.Folder == "60-CT";
            string text = n.Extract;
            if (descOnly || text.Length == 0)
            {
                m.Content = n.Body.Trim();
                m.Note = descOnly ? "ACC chỉ đọc phiếu mô tả chứng từ gốc (§7.9)" : n.Meta.GetValueOrDefault("ocr") == "cho-ai" ? "Tài liệu là bản scan chưa đọc chữ – chỉ có phiếu mô tả" : null;
                return Result<KnowledgeDocReadModel>.Success(m, "OK");
            }
            m.Pages = Math.Max(1, KnowledgeExtract.PageMark.Matches(text).Count);
            if (page is { } p && p > 0)
            {
                m.Page = p;
                m.Content = KnowledgeExtract.PageText(text, p);
                if (m.Content.Length == 0)
                {
                    return Status<KnowledgeDocReadModel>(404, $"Tài liệu có {m.Pages} trang – không có trang {p}");
                }
            }
            else
            {
                m.Content = text.Length > 6000 ? text[..6000] + "\n… (dài hơn – gọi lại với trang cụ thể)" : text;
            }
            return Result<KnowledgeDocReadModel>.Success(m, "OK");
        }

        /// <summary>KNOW: tài liệu cần đọc ảnh (ocr: cho-ai) hoặc chưa có tóm tắt – KHÔNG gồm vùng cá nhân / BOD.</summary>
        public async Task<List<KnowledgeDocReadModel>> PendingDocsFor(KnowledgeCaller caller, bool ocr)
        {
            var rights = await RightsAsync(caller);
            return [.. All().Where(n => n.Meta.ContainsKey("file") && rights.NoteVisible(n) && n.Folder != Personal && !n.IsBod && n.Status != "luu-tru"
                    && (ocr ? n.Meta.GetValueOrDefault("ocr") == "cho-ai" : !n.Meta.ContainsKey("tom_tat")))
                .Take(20).Select(n => new KnowledgeDocReadModel { Id = n.Id, Path = n.Path, Title = n.Title, Folder = n.Folder, SoHieu = n.Meta.GetValueOrDefault("so_hieu"), Date = n.Meta.GetValueOrDefault("ngay") })];
        }

        /// <summary>KNOW ghi bản chữ đọc từ ảnh (ocr: ai) hoặc tóm tắt (tom_tat, nhãn "tóm tắt do agent") – chỉ agent KNOW, không vùng cá nhân / BOD.</summary>
        public async Task<IResult<KnowledgeSaveResultModel>> WriteAgentTextFor(KnowledgeCaller caller, string? idOrPath, string? text, bool summary)
        {
            if (caller.AgentCode?.ToUpperInvariant() != "KNOW")
            {
                return Status<KnowledgeSaveResultModel>(403, "Chỉ agent KNOW ghi bản chữ / tóm tắt tài liệu");
            }
            string body = (text ?? string.Empty).Trim();
            if (body.Length == 0)
            {
                return Result<KnowledgeSaveResultModel>.Fail("Thiếu nội dung");
            }
            var rights = await RightsAsync(caller);
            string key = (idOrPath ?? string.Empty).Trim();
            var n = All().FirstOrDefault(x => (string.Equals(x.Path, key, StringComparison.OrdinalIgnoreCase) || string.Equals(x.Id, key, StringComparison.OrdinalIgnoreCase)) && x.Meta.ContainsKey("file") && rights.NoteVisible(x));
            if (n == null || n.Folder == Personal || n.IsBod)
            {
                return Status<KnowledgeSaveResultModel>(404, "Không có tài liệu này trong phạm vi KNOW");
            }
            if (ScanSensitive(body) is { } pii && summary)
            {
                return Result<KnowledgeSaveResultModel>.Fail($"Không ghi: {pii}");
            }
            string raw = await File.ReadAllTextAsync(Path.Combine(Root, n.Path), Encoding.UTF8);
            string updated;
            if (summary)
            {
                string one = Regex.Replace(body, @"\s+", " ");
                updated = SetMeta(SetMeta(raw, "tom_tat", one.Length > 600 ? one[..600] : one), "tom_tat_boi", "agent:know");
            }
            else
            {
                await File.WriteAllTextAsync(TxtPathOf(n), "[Trang 1]\n" + (body.Length > KnowledgeExtract.MaxChars ? body[..KnowledgeExtract.MaxChars] : body), new UTF8Encoding(false));
                updated = SetMeta(raw, "ocr", "ai");
            }
            return await WriteAsync(n.Path, updated, KnowledgeVault.Sha256(raw), caller.UserId, guardGenerated: true, note: summary ? "KNOW tóm tắt" : "KNOW đọc ảnh");
        }
    }
}
