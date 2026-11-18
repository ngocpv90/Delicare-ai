using AppDelicare.Messaging.Audiences.Internal.VanHanh;
using AppDelicare.Messaging.Core;

namespace AppDelicare.Messaging.Audiences.Internal.Chung
{
    /// <summary>
    /// BOT-2.4 (BOT_TELEGRAM §1.3): QR vật thể – link <c>t.me/&lt;bot VH&gt;?start=&lt;tham số&gt;</c> mở đúng việc tại chỗ, quyền như web (JWT của người quét):
    /// <c>LINE_&lt;mã&gt;</c> dây chuyền / bàn nấu → mẻ đang nấu / chờ nấu; <c>VEH_&lt;mã&gt;</c> xe → chuyến hôm nay [🚀 Xuất phát];
    /// <c>LOC_&lt;mã&gt;</c> kệ / vị trí kho → tồn theo lô, lô cận date; <c>CLEAN</c> khu vệ sinh hộp khay → lô vệ sinh hôm nay.
    /// Mã: <c>[A-Za-z0-9_-]</c>, ≤ 50 ký tự (tham số start ≤ 64). Người chưa liên kết quét → đăng ký trước (OnboardingActions).
    /// </summary>
    public sealed class QrActions(BepActions bep, GiaoVanActions giaoVan, XemNhanhActions xem)
    {
        public const string Clean = "CLEAN";
        private static readonly string[] Prefixes = ["LINE_", "VEH_", "LOC_"];

        /// <summary>Tách tham số QR → (loại, mã); không phải QR vật thể → null.</summary>
        public static (string Kind, string Code)? Parse(string? arg)
        {
            if (string.IsNullOrWhiteSpace(arg))
            {
                return null;
            }
            arg = arg.Trim();
            if (arg.Equals(Clean, StringComparison.OrdinalIgnoreCase))
            {
                return (Clean, string.Empty);
            }
            foreach (string prefix in Prefixes)
            {
                if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    string code = arg[prefix.Length..];
                    return code.Length is > 0 and <= 50 && code.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_') ? (prefix, code) : null;
                }
            }
            return null;
        }

        public static bool IsQr(string? arg) => Parse(arg) != null;

        public Task HandleAsync(BotActionContext ctx, string arg, CancellationToken ct) => Parse(arg) switch
        {
            ("LINE_", var code) => bep.LineAsync(ctx, code, ct),
            ("VEH_", var code) => giaoVan.VehicleAsync(ctx, code, ct),
            ("LOC_", var code) => xem.LocationStockAsync(ctx, code, ct),
            (Clean, _) => xem.CleaningTodayAsync(ctx, ct),
            _ => Task.CompletedTask,
        };
    }
}
