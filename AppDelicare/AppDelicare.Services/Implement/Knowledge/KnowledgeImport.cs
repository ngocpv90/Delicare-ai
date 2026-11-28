using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace AppDelicare.Services.Implement.Knowledge
{
    /// <summary>
    /// AI-12 (D-133): nhập tài liệu → markdown cho kho tri thức. Word (.docx) đọc trực tiếp gói OpenXML bằng thư viện chuẩn (.NET ZipArchive + XML):
    /// tiêu đề (Heading 1–4 / Title) → #, đoạn danh sách → "- " / "1. ", bảng → bảng markdown, chữ đậm / nghiêng. Syncfusion trong solution chưa có license
    /// (bản thử chèn dòng quảng cáo vào văn bản) nên không dùng. PDF: chưa có thư viện tách chữ đủ tin cậy → GAP-KB-PDF (báo người dùng lưu Word rồi nhập).
    /// </summary>
    public static class KnowledgeImport
    {
        private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

        public static string WordToMarkdown(Stream docx)
        {
            using var zip = new ZipArchive(docx, ZipArchiveMode.Read, leaveOpen: true);
            var entry = zip.GetEntry("word/document.xml") ?? throw new InvalidDataException("Tệp không phải Word .docx");
            XDocument doc;
            using (var s = entry.Open())
            {
                doc = XDocument.Load(s);
            }
            var styles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (zip.GetEntry("word/styles.xml") is { } st)
            {
                using var s = st.Open();
                foreach (var e in XDocument.Load(s).Descendants(W + "style"))
                {
                    string? id = (string?)e.Attribute(W + "styleId");
                    string? name = (string?)e.Element(W + "name")?.Attribute(W + "val");
                    if (id != null && name != null)
                    {
                        styles[id] = name;
                    }
                }
            }
            var body = doc.Root?.Element(W + "body") ?? throw new InvalidDataException("Tệp Word rỗng");
            var sb = new StringBuilder();
            foreach (var el in body.Elements())
            {
                if (el.Name == W + "p")
                {
                    string line = Paragraph(el, styles);
                    if (line.Length > 0)
                    {
                        sb.Append(line).Append('\n');
                        if (!line.StartsWith("- ", StringComparison.Ordinal) && !char.IsDigit(line[0]))
                        {
                            sb.Append('\n');
                        }
                    }
                }
                else if (el.Name == W + "tbl")
                {
                    sb.Append('\n').Append(Table(el, styles)).Append('\n');
                }
            }
            string md = sb.ToString().Replace("\n\n\n", "\n\n").Trim();
            if (md.Length == 0)
            {
                throw new InvalidDataException("Tệp Word không có chữ");
            }
            return md + "\n";
        }

        public static string PdfToMarkdown(Stream pdf)
            => throw new InvalidDataException("Nhập PDF chưa hỗ trợ (GAP-KB-PDF – chưa có thư viện tách chữ PDF có license) – lưu tài liệu dạng Word .docx rồi nhập, hoặc dán nội dung vào ghi chú");

        private static string Paragraph(XElement p, Dictionary<string, string> styles)
        {
            string text = Runs(p).Trim();
            if (text.Length == 0)
            {
                return string.Empty;
            }
            var pPr = p.Element(W + "pPr");
            string styleId = (string?)pPr?.Element(W + "pStyle")?.Attribute(W + "val") ?? string.Empty;
            string style = styles.GetValueOrDefault(styleId) ?? styleId;
            string s = style.Replace(" ", string.Empty).ToLowerInvariant();
            if (s is "title")
            {
                return "# " + Plain(text);
            }
            if (s.StartsWith("heading", StringComparison.Ordinal) && int.TryParse(s["heading".Length..], out int level))
            {
                return new string('#', Math.Clamp(level, 1, 3) + 1) + " " + Plain(text);
            }
            if (pPr?.Element(W + "numPr") != null || s.Contains("list", StringComparison.Ordinal))
            {
                return "- " + text;
            }
            return text;
        }

        private static string Plain(string md) => md.Replace("**", string.Empty).Replace("*", string.Empty);

        private static string Runs(XElement p)
        {
            var sb = new StringBuilder();
            foreach (var r in p.Descendants(W + "r"))
            {
                string t = string.Concat(r.Elements().Select(e => e.Name == W + "t" ? e.Value : e.Name == W + "tab" ? " " : e.Name == W + "br" ? " " : string.Empty));
                if (t.Length == 0)
                {
                    continue;
                }
                var rPr = r.Element(W + "rPr");
                bool b = On(rPr?.Element(W + "b")), i = On(rPr?.Element(W + "i"));
                string core = t.Trim();
                if (core.Length > 0 && (b || i))
                {
                    string mark = b ? "**" : "*";
                    t = t.Replace(core, mark + core + mark);
                }
                sb.Append(t);
            }
            return sb.ToString().Replace("****", string.Empty);
        }

        private static bool On(XElement? e) => e != null && (string?)e.Attribute(W + "val") is null or "1" or "true" or "on";

        private static string Table(XElement tbl, Dictionary<string, string> styles)
        {
            var rows = tbl.Elements(W + "tr").Select(tr => tr.Elements(W + "tc").Select(tc => string.Join(" ", tc.Elements(W + "p").Select(p => Paragraph(p, styles)).Where(x => x.Length > 0)).Replace("|", "/").Replace("\n", " ")).ToList()).Where(r => r.Count > 0).ToList();
            if (rows.Count == 0)
            {
                return string.Empty;
            }
            int cols = rows.Max(r => r.Count);
            string Row(List<string> r) => "| " + string.Join(" | ", Enumerable.Range(0, cols).Select(i => i < r.Count ? r[i] : string.Empty)) + " |";
            var sb = new StringBuilder(Row(rows[0])).Append('\n').Append("| " + string.Join(" | ", Enumerable.Repeat("---", cols)) + " |").Append('\n');
            foreach (var r in rows.Skip(1))
            {
                sb.Append(Row(r)).Append('\n');
            }
            return sb.ToString();
        }
    }
}
