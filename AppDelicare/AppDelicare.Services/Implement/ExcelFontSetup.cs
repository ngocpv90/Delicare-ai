#nullable enable
using System.Runtime.CompilerServices;
using ClosedXML.Excel;
using ClosedXML.Graphics;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// T48.4 (D-60, Linux + Docker): ClosedXML cần 1 font để đo độ rộng cột khi ghi .xlsx. Image Linux không có Calibri / Microsoft Sans Serif
    /// → mọi lệnh xuất Excel (Common/ExportList, biên bản giao hàng, phiếu sản xuất…) lỗi "Unable to find font Calibri or fallback font".
    /// Dùng font hệ thống nếu có (Windows giữ nguyên Calibri), thiếu thì rơi về <b>Noto Sans</b> nhúng trong DLL (cùng font với PDF – <see cref="PdfFontResolver"/>),
    /// nên không cần cài gói font trong image. Chạy 1 lần khi nạp AppDelicare.Services (WebApi, Job, test đều áp dụng).
    /// </summary>
    internal static class ExcelFontSetup
    {
        private const string Regular = "AppDelicare.Fonts.NotoSans-Regular.ttf";
        private const string Bold = "AppDelicare.Fonts.NotoSans-Bold.ttf";

        [ModuleInitializer]
        internal static void Configure()
        {
            try
            {
                LoadOptions.DefaultGraphicEngine = DefaultGraphicEngine.CreateWithFontsAndSystemFonts(Open(Regular), Open(Bold));
            }
            catch (Exception ex)
            {
                // Không chặn khởi động: lỗi font chỉ ảnh hưởng xuất Excel và sẽ được ghi log lại ở chính lệnh xuất.
                Log.Warning(string.Concat("[ExcelFontSetup] Configure: ", ex.ToString()));
            }
        }

        private static Stream Open(string resource)
        {
            using var stream = typeof(ExcelFontSetup).Assembly.GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException("Thiếu font nhúng " + resource + " (AppDelicare.Services/Fonts)");
            var ms = new MemoryStream();
            stream.CopyTo(ms);
            ms.Position = 0;
            return ms;
        }
    }
}
