#nullable enable
using System.Globalization;
using AppDelicare.Model.Model;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Shapes;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// BE2.7.1 (spec 2.5, Catalog 24–30; anh chốt 2026-11-17 – GAP-PRINT-01): vẽ phiếu in PDF từ cùng <see cref="PrintDocumentModel"/> như Excel / Word
    /// bằng PDFsharp-MigraDoc 6.2.4 (MIT, bản Core đa nền tảng) + font Noto Sans nhúng (<see cref="PdfFontResolver"/>).
    /// Bố cục giống bản Word: đầu trang pháp nhân (Company) + ký hiệu mẫu, tiêu đề, BẢN NHÁP (chữ đỏ + chữ chìm mỗi trang), số / ngày, thông tin chung,
    /// bảng dòng (tiêu đề lặp lại mỗi trang), dòng tổng, ghi chú, ô ký, chân trang "Trang x / y". A4 (dọc; &gt; 8 cột → ngang).
    /// Nhiều phiếu → 1 tệp, mỗi phiếu 1 section bắt đầu trang mới.
    /// </summary>
    public static class PrintPdfRenderer
    {
        private static readonly Color Brand = new(0x91, 0xAF, 0x42);
        private static readonly Color Watermark = new(0xF4, 0xC7, 0xC3);
        private static readonly Color Muted = new(0x60, 0x6B, 0x70);
        private static readonly Unit Margin = Unit.FromPoint(36);

        public static byte[] ToPdf(IReadOnlyList<PrintDocumentModel> docs)
        {
            PdfFontResolver.EnsureRegistered();
            var document = new Document();
            document.Info.Title = docs.Count == 1 ? Join(docs[0].Title, docs[0].DocumentNo) : $"In hàng loạt {docs.Count} phiếu";
            document.Info.Author = docs.FirstOrDefault()?.CompanyName ?? "DELICARE";
            var normal = document.Styles[StyleNames.Normal]!;
            normal.Font.Name = PdfFontResolver.FamilyName;
            normal.Font.Size = 9.5;
            normal.ParagraphFormat.SpaceAfter = 1;
            foreach (var doc in docs)
            {
                WriteSection(document, document.AddSection(), doc);
            }
            var renderer = new PdfDocumentRenderer { Document = document };
            renderer.RenderDocument();
            using var stream = new MemoryStream();
            renderer.PdfDocument.Save(stream, false);
            return stream.ToArray();
        }

        private static string Join(string a, string? b) => string.IsNullOrEmpty(b) ? a : a + " " + b;

        /// <summary>Thêm chữ, giữ xuống dòng của ghi chú nhập tay (tab → khoảng trắng).</summary>
        private static void AddLines(Paragraph p, string? text)
        {
            var lines = (text ?? string.Empty).Replace((char)9, ' ').Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                {
                    p.AddLineBreak();
                }
                if (lines[i].Length > 0)
                {
                    p.AddText(lines[i]); // kế thừa định dạng đoạn (đậm / nghiêng / màu)
                }
            }
        }

        private static Paragraph Text(DocumentElements blocks, string? text, bool bold = false, bool italic = false, double size = 9.5,
            ParagraphAlignment align = ParagraphAlignment.Left, Color? color = null)
        {
            var p = new Paragraph();
            p.Format.Alignment = align;
            p.Format.Font.Size = size;
            p.Format.Font.Bold = bold;
            p.Format.Font.Italic = italic;
            if (color is { } c)
            {
                p.Format.Font.Color = c;
            }
            AddLines(p, text);
            blocks.Add(p);
            return p;
        }

        private static Table NewTable(Section s, IReadOnlyList<double> widths, bool borders)
        {
            var table = s.AddTable();
            table.LeftPadding = table.RightPadding = 3;
            table.TopPadding = table.BottomPadding = 1.5;
            foreach (double w in widths)
            {
                table.AddColumn(Unit.FromPoint(w));
            }
            if (borders)
            {
                table.Borders.Width = 0.5;
                table.Borders.Color = Colors.Black;
            }
            return table;
        }

        private static void WriteSection(Document document, Section s, PrintDocumentModel doc)
        {
            bool landscape = doc.ListHeader.Count > 8;
            s.PageSetup = document.DefaultPageSetup.Clone();
            // A4 ghi thẳng kích thước (MigraDoc 6: Orientation.Landscape với PageFormat của DefaultPageSetup vẫn ra trang dọc – đã thử).
            s.PageSetup.Orientation = Orientation.Portrait;
            s.PageSetup.PageWidth = Unit.FromMillimeter(landscape ? 297 : 210);
            s.PageSetup.PageHeight = Unit.FromMillimeter(landscape ? 210 : 297);
            s.PageSetup.LeftMargin = s.PageSetup.RightMargin = s.PageSetup.TopMargin = Margin;
            s.PageSetup.BottomMargin = Unit.FromPoint(44);
            s.PageSetup.FooterDistance = Unit.FromPoint(18);
            s.PageSetup.StartingNumber = 1;
            double pageWidth = Unit.FromMillimeter(landscape ? 297 : 210).Point;
            double width = pageWidth - 2 * Margin.Point;

            if (doc.IsDraft)
            {
                // Chữ chìm "BẢN NHÁP" giữa mỗi trang (header vẽ trước thân trang → nằm dưới nội dung).
                var frame = s.Headers.Primary.AddTextFrame();
                frame.Width = Unit.FromPoint(width);
                frame.Height = Unit.FromPoint(120);
                frame.RelativeHorizontal = RelativeHorizontal.Page;
                frame.RelativeVertical = RelativeVertical.Page;
                frame.Left = ShapePosition.Center;
                frame.Top = ShapePosition.Center;
                frame.WrapFormat.Style = WrapStyle.Through;
                var mark = frame.AddParagraph("BẢN NHÁP");
                mark.Format.Alignment = ParagraphAlignment.Center;
                mark.Format.Font.Size = 80;
                mark.Format.Font.Bold = true;
                mark.Format.Font.Color = Watermark;
            }

            // Chân trang: số phiếu + "Trang x / y" (đếm trong phiếu – in hàng loạt mỗi phiếu đánh số riêng).
            var footer = s.Footers.Primary.AddParagraph();
            footer.Format.Font.Size = 7.5;
            footer.Format.Font.Color = Muted;
            footer.Format.TabStops.AddTabStop(Unit.FromPoint(width), TabAlignment.Right);
            footer.AddText(Join(doc.Title, doc.DocumentNo));
            footer.AddTab();
            footer.AddText("Trang ");
            footer.AddPageField();
            footer.AddText(" / ");
            footer.AddSectionPagesField();

            // Đầu trang: pháp nhân (trái) + ký hiệu mẫu (phải).
            var head = NewTable(s, [width * 0.68, width * 0.32], borders: false);
            var headRow = head.AddRow();
            var left = headRow.Cells[0].Elements;
            Text(left, doc.CompanyName, bold: true, size: 11, color: Brand);
            if (!string.IsNullOrEmpty(doc.CompanyAddress)) Text(left, "Địa chỉ: " + doc.CompanyAddress, size: 8.5);
            if (!string.IsNullOrEmpty(doc.CompanyPhone)) Text(left, "Điện thoại: " + doc.CompanyPhone, size: 8.5);
            if (!string.IsNullOrEmpty(doc.CompanyTaxCode)) Text(left, "MST: " + doc.CompanyTaxCode, size: 8.5);
            Text(headRow.Cells[1].Elements, doc.FormCode, bold: true, size: 8.5, align: ParagraphAlignment.Right);

            Text(s.Elements, doc.Title, bold: true, size: 15, align: ParagraphAlignment.Center).Format.SpaceBefore = 10;
            if (doc.IsDraft)
            {
                Text(s.Elements, "BẢN NHÁP – CHƯA CÓ GIÁ TRỊ PHÁP LÝ", bold: true, size: 12, align: ParagraphAlignment.Center, color: Colors.Red);
            }
            Text(s.Elements, PrintRenderer.DateLine(doc.DocumentDate) + (string.IsNullOrEmpty(doc.DocumentNo) ? string.Empty : "    –    Số: " + doc.DocumentNo),
                italic: true, align: ParagraphAlignment.Center).Format.SpaceAfter = 8;

            if (doc.ListInfo.Count > 0)
            {
                var info = NewTable(s, [width / 2, width / 2], borders: false);
                for (int i = 0; i < doc.ListInfo.Count; i += 2)
                {
                    var row = info.AddRow();
                    for (int c = 0; c < 2 && i + c < doc.ListInfo.Count; c++)
                    {
                        var p = row.Cells[c].AddParagraph();
                        p.AddFormattedText(doc.ListInfo[i + c].Label + ": ", TextFormat.Bold);
                        AddLines(p, doc.ListInfo[i + c].Value);
                    }
                }
            }

            if (doc.ListHeader.Count > 0)
            {
                s.AddParagraph().Format.SpaceAfter = 4;
                int cols = doc.ListHeader.Count;
                var table = NewTable(s, ColumnWidths(doc, width), borders: true);
                var headerRow = table.AddRow();
                headerRow.HeadingFormat = true; // lặp lại tiêu đề cột mỗi trang
                headerRow.Shading.Color = Brand;
                headerRow.VerticalAlignment = VerticalAlignment.Center;
                for (int c = 0; c < cols; c++)
                {
                    Text(headerRow.Cells[c].Elements, doc.ListHeader[c], bold: true, size: 8.5, align: ParagraphAlignment.Center, color: Colors.White);
                }
                foreach (var (cells, foot) in doc.ListRow.Select(r => (r, false)).Concat(doc.ListFooter.Select(r => (r, true))))
                {
                    var row = table.AddRow();
                    for (int c = 0; c < cols; c++)
                    {
                        string value = c < cells.Count ? cells[c] : string.Empty;
                        Text(row.Cells[c].Elements, value, bold: foot, size: 8.5, align: IsNumber(value) ? ParagraphAlignment.Right : ParagraphAlignment.Left);
                    }
                }
            }

            foreach (var note in doc.ListNote)
            {
                Text(s.Elements, note, italic: true, size: 8.5).Format.SpaceBefore = 3;
            }

            if (doc.ListSignature.Count > 0)
            {
                s.AddParagraph().Format.SpaceAfter = 10;
                int n = doc.ListSignature.Count;
                var sign = NewTable(s, Enumerable.Repeat(width / n, n).ToList(), borders: false);
                var row = sign.AddRow();
                row.Height = Unit.FromPoint(80); // chỗ ký tay
                for (int c = 0; c < n; c++)
                {
                    Text(row.Cells[c].Elements, doc.ListSignature[c], bold: true, align: ParagraphAlignment.Center);
                    Text(row.Cells[c].Elements, "(Ký, ghi rõ họ tên)", italic: true, size: 8, align: ParagraphAlignment.Center);
                }
            }
        }

        /// <summary>
        /// Độ rộng cột: mỗi cột tối thiểu đủ cho từ dài nhất (mã NL / số lô không ngắt dòng được – ~5,6 pt / ký tự ở cỡ 8,5), phần còn lại chia theo
        /// độ dài nội dung (4–30 ký tự); tổng tối thiểu vượt bề rộng trang → co theo tỉ lệ.
        /// </summary>
        private static List<double> ColumnWidths(PrintDocumentModel doc, double width)
        {
            static int LongestWord(string? s) => (s ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => w.Length).DefaultIfEmpty(0).Max();
            var rows = doc.ListRow.Concat(doc.ListFooter).ToList();
            var minimum = new List<double>();
            var weights = new List<double>();
            for (int c = 0; c < doc.ListHeader.Count; c++)
            {
                var cells = rows.Select(r => c < r.Count ? r[c] ?? string.Empty : string.Empty).ToList();
                int word = Math.Max(LongestWord(doc.ListHeader[c]), cells.Select(LongestWord).DefaultIfEmpty(0).Max());
                minimum.Add(Math.Min(word * 5.6 + 7, width * 0.4));
                int longest = cells.Select(x => x.Length).DefaultIfEmpty(0).Max();
                weights.Add(Math.Clamp(Math.Max(longest, doc.ListHeader[c].Length / 2), 4, 30));
            }
            double sumMin = minimum.Sum(), total = weights.Sum();
            return sumMin >= width
                ? minimum.Select(m => width * m / sumMin).ToList()
                : minimum.Select((m, c) => m + (width - sumMin) * weights[c] / total).ToList();
        }

        private static bool IsNumber(string value)
            => value.Length > 0 && decimal.TryParse(value.Replace(" ", string.Empty), NumberStyles.Number, CultureInfo.GetCultureInfo("vi-VN"), out _);
    }
}
