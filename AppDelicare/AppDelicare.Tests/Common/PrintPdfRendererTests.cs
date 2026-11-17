using AppDelicare.Model.Model;
using AppDelicare.Services.Implement;
using FluentAssertions;

namespace AppDelicare.Tests.Common
{
    /// <summary>
    /// BE2.7.1 – vẽ PDF (PDFsharp-MigraDoc + Noto Sans nhúng) từ PrintDocumentModel, không cần DB: tiếng Việt có dấu đọc lại được, font nhúng,
    /// A4 dọc / ngang (&gt; 8 cột), bảng dài sang nhiều trang, BẢN NHÁP, ghi chú nhiều dòng, in hàng loạt mỗi phiếu sang trang mới.
    /// </summary>
    public class PrintPdfRendererTests
    {
        private const double A4Short = 595.3, A4Long = 841.9;

        private static PrintDocumentModel Doc(string no, int rows = 2, int cols = 6, bool draft = false) => new()
        {
            Type = "GOODS_RECEIPT", Id = Guid.NewGuid(), Title = "PHIẾU NHẬP KHO", FormCode = "Mẫu số 01 - VT (tham khảo)", DocumentNo = no,
            DocumentDate = new DateTime(2026, 12, 3), IsDraft = draft,
            CompanyName = "[DEMO] CÔNG TY CỔ PHẦN SUẤT ĂN ĐỨC HẠNH", CompanyAddress = "[DEMO] Số 1 đường Nguyễn Trãi, Hà Nội", CompanyPhone = "0900000000", CompanyTaxCode = "0101234567",
            ListInfo = [new() { Label = "Nhà cung cấp", Value = "[DEMO] Hợp tác xã rau sạch Đà Lạt" }, new() { Label = "Kho nhận", Value = "[DEMO] Kho mát – nhiệt độ 0–4 °C" }],
            ListHeader = Enumerable.Range(0, cols).Select(c => c == 0 ? "STT" : c == 1 ? "Tên nguyên liệu" : "Cột số " + c).ToList(),
            ListRow = Enumerable.Range(1, rows).Select(r => Enumerable.Range(0, cols).Select(c => c == 0 ? r.ToString() : c == 1 ? $"Thịt gà ta dòng {r}" : "1.234,5").ToList()).ToList(),
            ListFooter = [Enumerable.Range(0, cols).Select(c => c == 1 ? "Tổng cộng" : c == cols - 1 ? "720.000" : string.Empty).ToList()],
            ListNote = ["Ghi chú: hàng đạt yêu cầu" + "\r\n" + "dòng hai\tcó tab"],
            ListSignature = ["Người lập phiếu", "Người giao hàng", "Thủ kho", "Kế toán trưởng"],
        };

        [Fact]
        public void Pdf_has_vietnamese_text_embedded_font_and_a4_portrait()
        {
            var bytes = PrintRenderer.Render([Doc("PN-261203-0001")], PrintRenderer.Pdf);
            PrintRenderer.ContentTypeOf(PrintRenderer.Pdf).Should().Be("application/pdf");
            var pdf = PdfText.Read(bytes);
            pdf.PageCount.Should().Be(1);
            pdf.PageSizes[0].Width.Should().BeApproximately(A4Short, 1);
            pdf.PageSizes[0].Height.Should().BeApproximately(A4Long, 1);
            pdf.FontsEmbedded.Should().BeTrue("font phải nhúng để in đúng trên máy không có font");
            pdf.Fonts.Should().OnlyContain(f => f.Contains("Noto Sans"));
            foreach (var text in new[] { "PHIẾU NHẬP KHO", "[DEMO] CÔNG TY CỔ PHẦN SUẤT ĂN ĐỨC HẠNH", "Mẫu số 01 - VT (tham khảo)", "Ngày 03 tháng 12 năm 2026", "Số: PN-261203-0001",
                "Nhà cung cấp:", "Hợp tác xã rau sạch Đà Lạt", "0–4 °C", "Tên nguyên liệu", "Thịt gà ta dòng 2", "1.234,5", "Tổng cộng", "720.000", "dòng hai có tab",
                "Kế toán trưởng", "(Ký, ghi rõ họ tên)", "Trang 1 / 1" })
            {
                pdf.Has(text).Should().BeTrue($"PDF phải có chữ \"{text}\"");
            }
            pdf.Has("BẢN NHÁP").Should().BeFalse();
        }

        [Fact]
        public void Long_wide_draft_and_batch_documents()
        {
            var wide = PdfText.Read(PrintRenderer.Render([Doc("KK-1", rows: 120, cols: 9, draft: true)], PrintRenderer.Pdf));
            wide.PageCount.Should().BeGreaterThan(1, "120 dòng phải sang trang");
            wide.PageSizes.Should().OnlyContain(s => s.Width > s.Height, "> 8 cột → A4 ngang");
            wide.Pages.Should().OnlyContain(p => p.Contains(PdfText.Squash("BẢN NHÁP")), "chữ chìm BẢN NHÁP mỗi trang");
            wide.Pages.Should().OnlyContain(p => p.Contains(PdfText.Squash("Tên nguyên liệu")), "tiêu đề cột lặp lại mỗi trang");
            wide.Has("BẢN NHÁP – CHƯA CÓ GIÁ TRỊ PHÁP LÝ").Should().BeTrue();
            wide.Has($"Trang {wide.PageCount} / {wide.PageCount}").Should().BeTrue();

            var batch = PdfText.Read(PrintRenderer.Render([Doc("PG-A"), Doc("PG-B"), Doc("PG-C", rows: 60)], PrintRenderer.Pdf));
            batch.PageCount.Should().BeGreaterThanOrEqualTo(3);
            batch.Pages[0].Should().Contain("PG-A");
            batch.Pages[1].Should().Contain("PG-B").And.NotContain("PG-A", "mỗi phiếu bắt đầu trang mới");
            batch.Pages[2].Should().Contain("PG-C").And.Contain(PdfText.Squash("Trang 1 /"), "số trang đếm lại theo từng phiếu");
        }
    }
}
