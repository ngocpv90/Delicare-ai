#nullable enable
using System.Collections.Concurrent;
using PdfSharp.Fonts;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// BE2.7.1: font cho PDF (PDFsharp-MigraDoc bản Core). Mọi họ font trả về <b>Noto Sans</b> (SIL OFL 1.1, notofonts/latin-greek-cyrillic) nhúng trong DLL
    /// (<c>AppDelicare.Services/Fonts</c>, EmbeddedResource) – đủ dấu tiếng Việt, không đọc font của Windows / Linux nên chạy như nhau trên IIS và Docker.
    /// Chỉ có Regular + Bold: nghiêng do PDFsharp mô phỏng (<c>mustSimulateItalic</c>).
    /// </summary>
    public sealed class PdfFontResolver : IFontResolver
    {
        public const string FamilyName = "Noto Sans";
        private const string RegularFace = "NotoSans-Regular";
        private const string BoldFace = "NotoSans-Bold";
        private static readonly object Gate = new();
        private static readonly ConcurrentDictionary<string, byte[]> Cache = new();

        /// <summary>Gắn resolver 1 lần cho cả tiến trình (PDFsharp không cho đổi resolver sau khi đã dùng font).</summary>
        public static void EnsureRegistered()
        {
            if (GlobalFontSettings.FontResolver is PdfFontResolver)
            {
                return;
            }
            lock (Gate)
            {
                if (GlobalFontSettings.FontResolver is not PdfFontResolver)
                {
                    GlobalFontSettings.FontResolver = new PdfFontResolver();
                }
            }
        }

        public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
            => new(isBold ? BoldFace : RegularFace, false, isItalic);

        public byte[]? GetFont(string faceName) => Cache.GetOrAdd(faceName, Load);

        private static byte[] Load(string faceName)
        {
            string resource = "AppDelicare.Fonts." + (faceName == BoldFace ? BoldFace : RegularFace) + ".ttf";
            using var stream = typeof(PdfFontResolver).Assembly.GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException("Thiếu font nhúng " + resource + " (AppDelicare.Services/Fonts)");
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }
    }
}
