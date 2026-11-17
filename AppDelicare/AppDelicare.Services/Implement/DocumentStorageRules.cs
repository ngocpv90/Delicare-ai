#nullable enable
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// BE2.7 (spec 2.4, GAP-F-09 → GAP-DOC-01, D-56 a): quy ước lưu tệp đính kèm khi <c>DocumentFile</c> không có cột liên kết chứng từ.
    /// <c>FileNameHas</c> (cột cũ = "tên tệp đã băm" trên đĩa) giữ <b>khóa lưu trữ</b> do máy chủ sinh, không có chữ nào của người dùng:
    /// <c>&lt;Resource&gt;/&lt;RecordId:N&gt;/&lt;category&gt;/&lt;sha256&gt;&lt;.ext&gt;</c> (tài liệu chung: <c>_chung/&lt;category&gt;/&lt;sha256&gt;&lt;.ext&gt;</c>).
    /// Resource lấy từ danh sách trắng <see cref="CommonRegistry"/>, category từ cấu hình, ext từ danh sách đuôi được nhận → không thể đi ra ngoài thư mục gốc.
    /// </summary>
    public static partial class DocumentStorageRules
    {
        public const string GeneralFolder = "_chung";

        /// <summary>Đuôi luôn bị chặn dù cấu hình có (tệp chạy được / script / trang web).</summary>
        public static readonly IReadOnlySet<string> NeverAllowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".dll", ".bat", ".cmd", ".com", ".msi", ".ps1", ".psm1", ".vbs", ".vbe", ".js", ".jse", ".wsf", ".wsh", ".scr", ".pif", ".cpl", ".jar",
            ".sh", ".py", ".php", ".asp", ".aspx", ".cshtml", ".html", ".htm", ".svg", ".xhtml", ".hta", ".lnk", ".reg", ".docm", ".xlsm", ".pptm", ".iso",
        };

        public static readonly IReadOnlyDictionary<string, string> ContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        };

        [GeneratedRegex(@"^(?<res>[A-Za-z][A-Za-z0-9]{1,63})/(?<id>[0-9a-f]{32})/(?<cat>[a-z]{1,30})/(?<sha>[0-9a-f]{64})(?<ext>\.[a-z0-9]{2,5})$")]
        private static partial Regex OwnedKey();

        [GeneratedRegex(@"^_chung/(?<cat>[a-z]{1,30})/(?<sha>[0-9a-f]{64})(?<ext>\.[a-z0-9]{2,5})$")]
        private static partial Regex GeneralKey();

        public sealed record StorageKey(string? Resource, Guid? RecordId, string Category, string Sha256, string Extension);

        public static string BuildKey(string? resource, Guid? recordId, string category, string sha256, string extension)
            => resource != null && recordId != null
                ? $"{resource}/{recordId.Value:N}/{category}/{sha256}{extension}"
                : $"{GeneralFolder}/{category}/{sha256}{extension}";

        /// <summary>Tiền tố khóa của mọi tệp thuộc 1 chứng từ (lọc danh sách: <c>FileNameHas LIKE 'prefix%'</c>).</summary>
        public static string OwnerPrefix(string resource, Guid recordId) => $"{resource}/{recordId:N}/";

        /// <summary>Đọc khóa lưu trữ; khóa không đúng mẫu (tệp cũ lưu trong wwwroot, dữ liệu sửa tay) → null.</summary>
        public static StorageKey? Parse(string? key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }
            var m = OwnedKey().Match(key);
            if (m.Success)
            {
                return new StorageKey(m.Groups["res"].Value, Guid.ParseExact(m.Groups["id"].Value, "N"), m.Groups["cat"].Value, m.Groups["sha"].Value, m.Groups["ext"].Value);
            }
            m = GeneralKey().Match(key);
            return m.Success ? new StorageKey(null, null, m.Groups["cat"].Value, m.Groups["sha"].Value, m.Groups["ext"].Value) : null;
        }

        /// <summary>Đường dẫn tuyệt đối của khóa trong thư mục gốc; chặn mọi đường đi ra ngoài gốc (path traversal).</summary>
        public static string FullPath(string root, string key)
        {
            if (Parse(key) == null)
            {
                throw new InvalidOperationException("Khóa lưu trữ không hợp lệ");
            }
            string rootFull = Path.GetFullPath(root);
            string full = Path.GetFullPath(Path.Combine(rootFull, key.Replace('/', Path.DirectorySeparatorChar)));
            string prefix = rootFull.EndsWith(Path.DirectorySeparatorChar) ? rootFull : rootFull + Path.DirectorySeparatorChar;
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Đường dẫn nằm ngoài thư mục lưu trữ");
            }
            return full;
        }

        /// <summary>Tên tệp gốc an toàn để hiển thị / Content-Disposition: bỏ thư mục, ký tự điều khiển và ký tự cấm, tối đa 150 ký tự.</summary>
        public static string SafeFileName(string? name)
        {
            string file = Path.GetFileName((name ?? string.Empty).Replace('\\', '/').Split('/').Last());
            var chars = file.Where(c => !char.IsControl(c) && !Path.GetInvalidFileNameChars().Contains(c) && "\"<>:*?|".IndexOf(c) < 0).ToArray();
            string clean = new string(chars).Trim().Trim('.');
            if (clean.Length > 150)
            {
                string ext = Path.GetExtension(clean);
                clean = clean[..(150 - ext.Length)] + ext;
            }
            return string.IsNullOrWhiteSpace(clean) ? "tep" : clean;
        }

        public static string Sha256Hex(byte[] content) => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

        /// <summary>Kiểm "chữ ký" đầu tệp khớp đuôi (chống đổi đuôi tệp chạy được thành .pdf / .png…).</summary>
        public static bool ContentMatches(string extension, byte[] content)
        {
            static bool StartsWith(byte[] data, params byte[] magic) => data.Length >= magic.Length && data.AsSpan(0, magic.Length).SequenceEqual(magic);
            switch (extension.ToLowerInvariant())
            {
                case ".pdf":
                    return StartsWith(content, 0x25, 0x50, 0x44, 0x46, 0x2D); // %PDF-
                case ".png":
                    return StartsWith(content, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A);
                case ".jpg":
                case ".jpeg":
                    return StartsWith(content, 0xFF, 0xD8, 0xFF);
                case ".xlsx":
                    return IsOpenXml(content, "xl/");
                case ".docx":
                    return IsOpenXml(content, "word/");
                default:
                    return false; // đuôi cấu hình thêm mà chưa có cách kiểm nội dung → không nhận.
            }
        }

        /// <summary>Tệp Office mở (zip) có [Content_Types].xml + thư mục đúng loại, không chứa macro (vbaProject.bin).</summary>
        private static bool IsOpenXml(byte[] content, string folder)
        {
            if (content.Length < 4 || content[0] != 0x50 || content[1] != 0x4B || content[2] != 0x03 || content[3] != 0x04)
            {
                return false;
            }
            try
            {
                using var zip = new ZipArchive(new MemoryStream(content), ZipArchiveMode.Read);
                var names = zip.Entries.Select(e => e.FullName).ToList();
                return names.Contains("[Content_Types].xml")
                    && names.Any(n => n.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
                    && !names.Any(n => n.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase));
            }
            catch (InvalidDataException)
            {
                return false;
            }
        }
    }
}
