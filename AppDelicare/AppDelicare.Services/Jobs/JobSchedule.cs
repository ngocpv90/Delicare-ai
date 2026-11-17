using System.Globalization;

namespace AppDelicare.Services.Jobs
{
    /// <summary>T23: tính mốc chạy hằng ngày "HH:mm" theo giờ VN (IClock).</summary>
    public static class JobSchedule
    {
        /// <summary>"20:00" → 20:00:00. Sai định dạng → dùng <paramref name="fallback"/>.</summary>
        public static TimeSpan ParseAt(string? at, string fallback)
            => TryParse(at, out TimeSpan value) ? value
                : TryParse(fallback, out value) ? value
                : throw new FormatException($"Giờ chạy job không hợp lệ: '{fallback}' (cần HH:mm).");

        public static bool TryParse(string? at, out TimeSpan value)
            => TimeSpan.TryParseExact(at?.Trim(), [@"hh\:mm", @"h\:mm"], CultureInfo.InvariantCulture, out value)
               && value >= TimeSpan.Zero && value < TimeSpan.FromDays(1);

        /// <summary>Mốc của hôm nay nếu đã tới giờ (job lỡ giờ trong ngày vẫn chạy bù tới hết ngày), ngược lại null.</summary>
        public static DateTime? DueSlot(DateTime now, TimeSpan at)
        {
            DateTime slot = now.Date + at;
            return now >= slot ? slot : null;
        }

        /// <summary>Lần chạy kế tiếp tính từ <paramref name="now"/> (hôm nay nếu chưa tới giờ, ngược lại ngày mai).</summary>
        public static DateTime NextRun(DateTime now, TimeSpan at)
        {
            DateTime slot = now.Date + at;
            return now < slot ? slot : slot.AddDays(1);
        }
    }
}
