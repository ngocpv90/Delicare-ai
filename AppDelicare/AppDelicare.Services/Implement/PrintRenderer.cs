#nullable enable
using System.Globalization;
using AppDelicare.Model.Model;
using ClosedXML.Excel;
using GemBox.Document;
using GemBox.Document.Tables;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// BE2.7 (spec 2.5, Catalog 24–30): vẽ phiếu in từ <see cref="PrintDocumentModel"/> – Excel bằng ClosedXML (như các mẫu in T15 / T17 / T19),
    /// Word bằng GemBox.Document (thư viện đã có, license <c>Licenses:GemBox</c>; thiếu license → bản dùng thử), PDF bằng PDFsharp-MigraDoc
    /// (<see cref="PrintPdfRenderer"/>, BE2.7.1 – anh chốt 2026-11-17; GemBox.Document trong <c>Library/</c> là bản .NET Framework, lưu PDF lỗi trên .NET 8).
    /// Định dạng mặc định: <c>BusinessRules:Common:PrintDefaultFormat</c> (pdf – PROPOSED D-56 k).
    /// Nhiều phiếu → 1 tệp (Excel: mỗi phiếu 1 sheet; Word / PDF: mỗi phiếu 1 section / trang mới).
    /// Bố cục: đầu trang pháp nhân (bảng Company) + ký hiệu mẫu, tiêu đề, số / ngày, thông tin chung, bảng dòng, dòng tổng, ghi chú, ô ký.
    /// </summary>
    public static class PrintRenderer
    {
        public const string Docx = "docx";
        public const string Xlsx = "xlsx";
        public const string Pdf = "pdf";
        public const string DocxContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        public const string PdfContentType = "application/pdf";
        public static readonly string[] Formats = [Pdf, Xlsx, Docx];
        private const string Brand = "#91AF42";
        private const string FontName = "Arial";

        public static string ContentTypeOf(string format) => format switch
        {
            Docx => DocxContentType,
            Pdf => PdfContentType,
            _ => AppDelicare.Model.Helper.ConstantHelper.DownloadExcel,
        };

        public static byte[] Render(IReadOnlyList<PrintDocumentModel> docs, string format) => format switch
        {
            Docx => ToDocx(docs),
            Pdf => PrintPdfRenderer.ToPdf(docs),
            _ => ToXlsx(docs),
        };

        public static string DateLine(DateTime? date)
            => date is { } d ? $"Ngày {d:dd} tháng {d:MM} năm {d:yyyy}" : "Ngày ...... tháng ...... năm ......";

        // ---------------------------------------------------------------- Word (GemBox.Document, khung cũ TestDemo / MedicineWarehouse)

        public static byte[] ToDocx(IReadOnlyList<PrintDocumentModel> docs)
        {
            var document = new DocumentModel();
            document.DefaultCharacterFormat.FontName = FontName;
            document.DefaultCharacterFormat.Size = 10;
            foreach (var doc in docs)
            {
                var section = new Section(document);
                section.PageSetup.PaperType = PaperType.A4;
                section.PageSetup.Orientation = doc.ListHeader.Count > 8 ? Orientation.Landscape : Orientation.Portrait;
                section.PageSetup.PageMargins.Left = section.PageSetup.PageMargins.Right = 36;
                section.PageSetup.PageMargins.Top = section.PageSetup.PageMargins.Bottom = 36;
                document.Sections.Add(section);
                WriteSection(document, section, doc);
            }
            using var stream = new MemoryStream();
            document.Save(stream, GemBox.Document.SaveOptions.DocxDefault);
            return stream.ToArray();
        }

        private static Paragraph Text(DocumentModel d, string? text, bool bold = false, bool italic = false, double size = 10,
            HorizontalAlignment align = HorizontalAlignment.Left, Color? color = null)
        {
            var run = new Run(d, OneLine(text));
            run.CharacterFormat.Bold = bold;
            run.CharacterFormat.Italic = italic;
            run.CharacterFormat.Size = size;
            if (color is { } c)
            {
                run.CharacterFormat.FontColor = c;
            }
            var p = new Paragraph(d, run);
            p.ParagraphFormat.Alignment = align;
            p.ParagraphFormat.SpaceAfter = 2;
            return p;
        }

        private static Table Grid(DocumentModel d, int rows, int cols, Func<int, int, TableCell> cell, bool borders)
        {
            var table = new Table(d);
            for (int r = 0; r < rows; r++)
            {
                var row = new TableRow(d);
                for (int c = 0; c < cols; c++)
                {
                    row.Cells.Add(cell(r, c));
                }
                table.Rows.Add(row);
            }
            table.TableFormat.PreferredWidth = new TableWidth(100, TableWidthUnit.Percentage);
            if (borders)
            {
                table.TableFormat.Borders.SetBorders(MultipleBorderTypes.All, BorderStyle.Single, Color.Black, 0.5);
            }
            else
            {
                table.TableFormat.Borders.ClearBorders();
            }
            return table;
        }

        private static void WriteSection(DocumentModel d, Section s, PrintDocumentModel doc)
        {
            var brand = new Color(0x91, 0xAF, 0x42);
            // Đầu trang: pháp nhân (trái) + ký hiệu mẫu (phải).
            s.Blocks.Add(Grid(d, 1, 2, (r, c) =>
            {
                var cell = new TableCell(d);
                if (c == 0)
                {
                    cell.Blocks.Add(Text(d, doc.CompanyName, bold: true, size: 11, color: brand));
                    if (!string.IsNullOrEmpty(doc.CompanyAddress)) cell.Blocks.Add(Text(d, "Địa chỉ: " + doc.CompanyAddress, size: 9));
                    if (!string.IsNullOrEmpty(doc.CompanyPhone)) cell.Blocks.Add(Text(d, "Điện thoại: " + doc.CompanyPhone, size: 9));
                    if (!string.IsNullOrEmpty(doc.CompanyTaxCode)) cell.Blocks.Add(Text(d, "MST: " + doc.CompanyTaxCode, size: 9));
                }
                else
                {
                    cell.Blocks.Add(Text(d, doc.FormCode, bold: true, size: 9, align: HorizontalAlignment.Right));
                }
                return cell;
            }, borders: false));
            s.Blocks.Add(Text(d, doc.Title, bold: true, size: 16, align: HorizontalAlignment.Center));
            if (doc.IsDraft)
            {
                s.Blocks.Add(Text(d, "BẢN NHÁP – CHƯA CÓ GIÁ TRỊ PHÁP LÝ", bold: true, size: 12, align: HorizontalAlignment.Center, color: Color.Red));
            }
            s.Blocks.Add(Text(d, DateLine(doc.DocumentDate) + (string.IsNullOrEmpty(doc.DocumentNo) ? string.Empty : "    –    Số: " + doc.DocumentNo),
                italic: true, align: HorizontalAlignment.Center));

            if (doc.ListInfo.Count > 0)
            {
                int rows = (doc.ListInfo.Count + 1) / 2;
                s.Blocks.Add(Grid(d, rows, 2, (r, c) =>
                {
                    int i = r * 2 + c;
                    var cell = new TableCell(d);
                    if (i < doc.ListInfo.Count)
                    {
                        var p = new Paragraph(d, new Run(d, OneLine(doc.ListInfo[i].Label) + ": ") { CharacterFormat = { Bold = true } }, new Run(d, OneLine(doc.ListInfo[i].Value)));
                        p.ParagraphFormat.SpaceAfter = 2;
                        cell.Blocks.Add(p);
                    }
                    else
                    {
                        cell.Blocks.Add(new Paragraph(d));
                    }
                    return cell;
                }, borders: false));
            }

            if (doc.ListHeader.Count > 0)
            {
                var body = new List<(List<string> Cells, bool Head, bool Foot)> { (doc.ListHeader, true, false) };
                body.AddRange(doc.ListRow.Select(r => (r, false, false)));
                body.AddRange(doc.ListFooter.Select(r => (r, false, true)));
                int cols = doc.ListHeader.Count;
                var table = Grid(d, body.Count, cols, (r, c) =>
                {
                    var (cells, head, foot) = body[r];
                    string value = c < cells.Count ? cells[c] : string.Empty;
                    var cell = new TableCell(d, Text(d, value, bold: head || foot, size: 9,
                        align: head ? HorizontalAlignment.Center : IsNumber(value) ? HorizontalAlignment.Right : HorizontalAlignment.Left, color: head ? Color.White : null));
                    if (head)
                    {
                        cell.CellFormat.BackgroundColor = brand;
                    }
                    return cell;
                }, borders: true);
                table.Rows[0].RowFormat.RepeatOnEachPage = true;
                s.Blocks.Add(new Paragraph(d));
                s.Blocks.Add(table);
            }

            foreach (var note in doc.ListNote)
            {
                s.Blocks.Add(Text(d, note, italic: true, size: 9));
            }

            if (doc.ListSignature.Count > 0)
            {
                s.Blocks.Add(new Paragraph(d));
                s.Blocks.Add(Grid(d, 1, doc.ListSignature.Count, (r, c) =>
                {
                    var cell = new TableCell(d);
                    cell.Blocks.Add(Text(d, doc.ListSignature[c], bold: true, align: HorizontalAlignment.Center));
                    cell.Blocks.Add(Text(d, "(Ký, ghi rõ họ tên)", italic: true, size: 8, align: HorizontalAlignment.Center));
                    for (int i = 0; i < 4; i++)
                    {
                        cell.Blocks.Add(new Paragraph(d));
                    }
                    return cell;
                }, borders: false));
            }
        }

        /// <summary>GemBox Run không nhận xuống dòng / tab (ghi chú nhập tay có thể có) → thay bằng khoảng trắng.</summary>
        private static string OneLine(string? text) => string.Join(" ", (text ?? string.Empty).Split(new[] { (char)13, (char)10, (char)9 }, StringSplitOptions.RemoveEmptyEntries));

        private static bool IsNumber(string value)
            => value.Length > 0 && decimal.TryParse(value.Replace(" ", string.Empty), NumberStyles.Number, CultureInfo.GetCultureInfo("vi-VN"), out _);

        // ---------------------------------------------------------------- Excel (ClosedXML)

        public static byte[] ToXlsx(IReadOnlyList<PrintDocumentModel> docs)
        {
            using var wb = new XLWorkbook();
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var doc in docs)
            {
                var ws = wb.Worksheets.Add(SheetName(doc, used));
                WriteSheet(ws, doc);
            }
            using var stream = new MemoryStream();
            wb.SaveAs(stream);
            return stream.ToArray();
        }

        public static string SheetName(PrintDocumentModel doc, ISet<string> used)
        {
            string raw = new string((doc.DocumentNo ?? doc.Type).Where(ch => !"[]:*?/\\".Contains(ch)).ToArray()).Trim();
            if (raw.Length == 0) raw = "Phieu";
            if (raw.Length > 28) raw = raw[..28];
            string name = raw;
            for (int i = 2; used.Contains(name); i++)
            {
                name = $"{raw}-{i}";
            }
            used.Add(name);
            return name;
        }

        private static void WriteSheet(IXLWorksheet ws, PrintDocumentModel doc)
        {
            int cols = Math.Max(6, doc.ListHeader.Count);
            ws.Cell(1, 1).Value = doc.CompanyName ?? string.Empty;
            ws.Cell(1, 1).Style.Font.SetBold().Font.SetFontColor(XLColor.FromHtml(Brand));
            ws.Cell(2, 1).Value = string.Join(" · ", new[] { doc.CompanyAddress, doc.CompanyPhone is { Length: > 0 } ph ? "ĐT " + ph : null }.Where(x => !string.IsNullOrEmpty(x)));
            ws.Cell(1, cols).Value = doc.FormCode ?? string.Empty;
            ws.Cell(1, cols).Style.Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
            int row = 4;
            ws.Range(row, 1, row, cols).Merge().Value = doc.Title;
            ws.Range(row, 1, row, cols).Style.Font.SetBold().Font.SetFontSize(16).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            if (doc.IsDraft)
            {
                row++;
                ws.Range(row, 1, row, cols).Merge().Value = "BẢN NHÁP – CHƯA CÓ GIÁ TRỊ PHÁP LÝ";
                ws.Range(row, 1, row, cols).Style.Font.SetBold().Font.SetFontColor(XLColor.Red).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            }
            row++;
            ws.Range(row, 1, row, cols).Merge().Value = DateLine(doc.DocumentDate) + (string.IsNullOrEmpty(doc.DocumentNo) ? string.Empty : "  –  Số: " + doc.DocumentNo);
            ws.Range(row, 1, row, cols).Style.Font.SetItalic().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            row++;
            int half = Math.Max(1, cols / 2);
            for (int i = 0; i < doc.ListInfo.Count; i++)
            {
                if (i % 2 == 0) row++;
                int col = i % 2 == 0 ? 1 : half + 1;
                ws.Cell(row, col).Value = doc.ListInfo[i].Label + ":";
                ws.Cell(row, col).Style.Font.Bold = true;
                ws.Cell(row, col + 1).Value = doc.ListInfo[i].Value ?? string.Empty;
            }
            if (doc.ListHeader.Count > 0)
            {
                row += 2;
                int head = row;
                for (int c = 0; c < doc.ListHeader.Count; c++)
                {
                    ws.Cell(head, c + 1).Value = doc.ListHeader[c];
                }
                ws.Range(head, 1, head, doc.ListHeader.Count).Style.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(XLColor.FromHtml(Brand))
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center).Alignment.SetWrapText(true);
                foreach (var r in doc.ListRow.Concat(doc.ListFooter))
                {
                    row++;
                    for (int c = 0; c < r.Count && c < doc.ListHeader.Count; c++)
                    {
                        ws.Cell(row, c + 1).Value = r[c];
                    }
                }
                for (int f = 0; f < doc.ListFooter.Count; f++)
                {
                    ws.Range(row - f, 1, row - f, doc.ListHeader.Count).Style.Font.Bold = true;
                }
                ws.Range(head, 1, row, doc.ListHeader.Count).Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin).Border.SetInsideBorder(XLBorderStyleValues.Thin);
            }
            foreach (var note in doc.ListNote)
            {
                row++;
                ws.Cell(row, 1).Value = note;
                ws.Cell(row, 1).Style.Font.Italic = true;
            }
            if (doc.ListSignature.Count > 0)
            {
                row += 2;
                int step = Math.Max(1, cols / doc.ListSignature.Count);
                for (int i = 0; i < doc.ListSignature.Count; i++)
                {
                    ws.Cell(row, 1 + i * step).Value = doc.ListSignature[i];
                    ws.Cell(row, 1 + i * step).Style.Font.Bold = true;
                    ws.Cell(row + 1, 1 + i * step).Value = "(Ký, ghi rõ họ tên)";
                    ws.Cell(row + 1, 1 + i * step).Style.Font.Italic = true;
                }
            }
            ws.Columns(1, cols).AdjustToContents(6.0, 40.0);
            ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
            ws.PageSetup.PageOrientation = doc.ListHeader.Count > 8 ? XLPageOrientation.Landscape : XLPageOrientation.Portrait;
            ws.PageSetup.FitToPages(1, 0);
        }
    }
}
