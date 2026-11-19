using AppDelicare.Data.Connection;
using AppDelicare.Model.Helper;
using Microsoft.EntityFrameworkCore;
using QRCoder;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// FE-E.2 (D-65, anh chọn 2026-11-19: backend vẽ QR bằng QRCoder 1.6.0 – MIT, thuần .NET, chạy được trong Docker Linux).
    /// Dùng cho API Common/GetQrBatch, Common/GetFieldQrList (giao diện hiện SVG) và ảnh QR trên PDF phiếu in (BMP).
    /// Mức sửa lỗi M; nội dung ≤ <see cref="MaxLength"/> ký tự (link t.me ≈ 60–110 ký tự).
    /// </summary>
    public static class QrCodeRenderer
    {
        public const int MaxLength = 512;

        /// <summary>SVG (viewBox, co giãn theo khung chứa) – <paramref name="pixelsPerModule"/> chỉ ảnh hưởng kích thước gốc.</summary>
        public static string Svg(string text, int pixelsPerModule = 4)
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
            return new SvgQRCode(data).GetGraphic(pixelsPerModule, "#000000", "#FFFFFF", drawQuietZones: true, sizingMode: SvgQRCode.SizingMode.ViewBoxAttribute);
        }

        /// <summary>BMP đen trắng – cho PDF (PDFsharp 6 bản Core không đọc PNG, đọc được BMP – đã thử).</summary>
        public static byte[] Bmp(string text, int pixelsPerModule = 6)
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
            return new BitmapByteQRCode(data).GetGraphic(pixelsPerModule);
        }

        /// <summary>
        /// Username bot Vận hành đang hoạt động (BotConfiguration VH + kênh Active, có BotUsername) – dùng làm link <c>https://t.me/&lt;bot&gt;?start=…</c>.
        /// Chưa khai báo → null (không in QR / không lập link).
        /// </summary>
        public static Task<string?> VanHanhBotAsync(DatabaseContext db)
            => (from b in db.BotConfiguration.AsNoTracking()
                join c in db.IntegrationChannel.AsNoTracking() on b.IntegrationChannelId equals c.Id
                where !b.IsDeleted && !c.IsDeleted && b.BotCode == MessagingCode.BotVanHanh && b.BotStatus == MessagingCode.StatusActive
                      && c.ChannelStatus == MessagingCode.StatusActive && b.BotUsername != null && b.BotUsername != ""
                select b.BotUsername).FirstOrDefaultAsync();

        public static string StartLink(string botUsername, string payload) => $"https://t.me/{botUsername.Trim().TrimStart('@')}?start={payload}";
    }
}
