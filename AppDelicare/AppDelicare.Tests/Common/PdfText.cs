using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace AppDelicare.Tests.Common
{
    /// <summary>
    /// BE2.7.1: đọc lại PDF mẫu in bằng chính PDFsharp (không thêm thư viện): số trang, khổ trang, font nhúng, và chữ của từng trang
    /// (giải mã glyph qua bảng ToUnicode của font nhúng) để kiểm tiếng Việt có dấu thật sự nằm trong tệp.
    /// </summary>
    public sealed class PdfText
    {
        public int PageCount { get; private init; }
        /// <summary>Chữ của từng trang, đã bỏ mọi khoảng trắng (MigraDoc vẽ từng từ riêng) – so bằng <see cref="Has"/>.</summary>
        public IReadOnlyList<string> Pages { get; private init; } = [];
        public IReadOnlyList<(double Width, double Height)> PageSizes { get; private init; } = [];
        public IReadOnlyList<string> Fonts { get; private init; } = [];
        public bool FontsEmbedded { get; private init; }

        public static readonly byte[] Magic = "%PDF-"u8.ToArray();

        // Tf (đổi font) | <hex> (font Type0 – glyph 2 byte) | (chuỗi) (font TrueType WinAnsi – PDFsharp dùng cho từ chỉ có ký tự ANSI).
        private static readonly Regex Token = new(@"/(?<font>[A-Za-z0-9_.+-]+)\s+[-0-9.]+\s+Tf|<(?<hex>[0-9A-Fa-f]+)>|\((?<lit>(?:\\.|[^\\)])*)\)", RegexOptions.Compiled);

        /// <summary>WinAnsiEncoding 0x80–0x9F (khác Latin-1), vd. 0x96 = "–".</summary>
        private const string WinAnsi80 = "€\u0081‚ƒ„…†‡ˆ‰Š‹Œ\u008DŽ\u008F\u0090‘’“”•–—˜™š›œ\u009DžŸ";

        private static string Literal(string raw)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                if (c == '\\' && i + 1 < raw.Length)
                {
                    char n = raw[++i];
                    if (n >= '0' && n <= '7')
                    {
                        int len = 1;
                        while (len < 3 && i + len < raw.Length && raw[i + len] >= '0' && raw[i + len] <= '7') len++;
                        c = (char)Convert.ToInt32(raw.Substring(i, len), 8);
                        i += len - 1;
                    }
                    else
                    {
                        c = n switch { 'n' => '\n', 'r' => '\r', 't' => '\t', 'b' => '\b', 'f' => '\f', _ => n };
                    }
                }
                sb.Append(c >= 0x80 && c <= 0x9F ? WinAnsi80[c - 0x80] : c);
            }
            return sb.ToString();
        }
        private static readonly Regex BfChar = new(@"<(?<src>[0-9A-Fa-f]{4})>\s*<(?<dst>[0-9A-Fa-f]+)>", RegexOptions.Compiled);
        private static readonly Regex BfRange = new(@"<(?<lo>[0-9A-Fa-f]{4})>\s*<(?<hi>[0-9A-Fa-f]{4})>\s*<(?<dst>[0-9A-Fa-f]+)>", RegexOptions.Compiled);

        public static string Squash(string text) => new(text.Where(c => !char.IsWhiteSpace(c)).ToArray());

        public bool Has(string text) => Pages.Any(p => p.Contains(Squash(text), StringComparison.Ordinal));

        public static PdfText Read(byte[] bytes)
        {
            bytes.Take(5).Should().Equal(Magic, "tệp PDF phải bắt đầu bằng %PDF-");
            using var document = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);
            var pages = new List<string>();
            var sizes = new List<(double, double)>();
            var fonts = new HashSet<string>();
            bool embedded = true;
            foreach (PdfPage page in document.Pages)
            {
                sizes.Add((page.Width.Point, page.Height.Point));
                var maps = new Dictionary<string, Dictionary<int, string>>();
                var fontDict = page.Resources.Elements.GetDictionary("/Font");
                foreach (string key in fontDict?.Elements.Keys ?? Enumerable.Empty<string>())
                {
                    var font = fontDict!.Elements.GetDictionary(key)!;
                    fonts.Add(font.Elements.GetName("/BaseFont"));
                    var descendant = font.Elements.GetArray("/DescendantFonts")?.Elements.GetDictionary(0);
                    var descriptor = (descendant ?? font).Elements.GetDictionary("/FontDescriptor");
                    embedded &= descriptor?.Elements.GetDictionary("/FontFile2") != null;
                    var toUnicode = font.Elements.GetDictionary("/ToUnicode");
                    maps[key.TrimStart('/')] = toUnicode?.Stream != null ? ParseCMap(Encoding.Latin1.GetString(toUnicode.Stream.UnfilteredValue)) : [];
                }
                var content = new StringBuilder();
                for (int i = 0; i < page.Contents.Elements.Count; i++)
                {
                    var stream = page.Contents.Elements.GetDictionary(i)?.Stream;
                    if (stream != null)
                    {
                        content.Append(Encoding.Latin1.GetString(stream.UnfilteredValue)).Append('\n');
                    }
                }
                var text = new StringBuilder();
                Dictionary<int, string> map = [];
                foreach (Match m in Token.Matches(content.ToString()))
                {
                    if (m.Groups["font"].Success)
                    {
                        map = maps.GetValueOrDefault(m.Groups["font"].Value) ?? [];
                        continue;
                    }
                    if (m.Groups["lit"].Success)
                    {
                        text.Append(Literal(m.Groups["lit"].Value));
                        continue;
                    }
                    string hex = m.Groups["hex"].Value;
                    for (int i = 0; i + 4 <= hex.Length; i += 4)
                    {
                        int glyph = int.Parse(hex.AsSpan(i, 4), NumberStyles.HexNumber);
                        text.Append(map.GetValueOrDefault(glyph, string.Empty));
                    }
                }
                pages.Add(Squash(text.ToString()));
            }
            return new PdfText { PageCount = document.PageCount, Pages = pages, PageSizes = sizes, Fonts = fonts.ToList(), FontsEmbedded = embedded && fonts.Count > 0 };
        }

        private static Dictionary<int, string> ParseCMap(string cmap)
        {
            var map = new Dictionary<int, string>();
            foreach (Match block in Regex.Matches(cmap, @"beginbfrange(?<b>.*?)endbfrange", RegexOptions.Singleline))
            {
                foreach (Match m in BfRange.Matches(block.Groups["b"].Value))
                {
                    int lo = int.Parse(m.Groups["lo"].Value, NumberStyles.HexNumber), hi = int.Parse(m.Groups["hi"].Value, NumberStyles.HexNumber);
                    int dst = int.Parse(m.Groups["dst"].Value, NumberStyles.HexNumber);
                    for (int g = lo; g <= hi; g++)
                    {
                        map[g] = char.ConvertFromUtf32(dst + g - lo);
                    }
                }
            }
            foreach (Match block in Regex.Matches(cmap, @"beginbfchar(?<b>.*?)endbfchar", RegexOptions.Singleline))
            {
                foreach (Match m in BfChar.Matches(block.Groups["b"].Value))
                {
                    map[int.Parse(m.Groups["src"].Value, NumberStyles.HexNumber)] = Utf16(m.Groups["dst"].Value);
                }
            }
            return map;
        }

        private static string Utf16(string hex)
        {
            var sb = new StringBuilder();
            for (int i = 0; i + 4 <= hex.Length; i += 4)
            {
                sb.Append((char)int.Parse(hex.AsSpan(i, 4), NumberStyles.HexNumber));
            }
            return sb.ToString();
        }
    }
}
