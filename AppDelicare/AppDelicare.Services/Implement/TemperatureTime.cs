using AppDelicare.Data.Share;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// Rà soát BE (2026-11-17): giờ đo nhiệt độ nhập tay (lõi mẻ ≥ 70 °C, xuất phát / bàn giao ≥ 60 °C). Các cổng ATTP lấy "lần đo mới nhất"
    /// theo giờ đo → giờ đo do client gửi phải hợp lệ, nếu không 1 lần đo ghi giờ tương lai sẽ "che" lần đo thật sau đó.
    /// Bỏ trống → giờ hiện tại; giờ UTC ("Z") → đổi sang giờ nghiệp vụ; cho phép lệch tới tối đa <see cref="MaxFutureMinutes"/> phút
    /// về sau và ghi bù tối đa <see cref="MaxPastHours"/> giờ trước (cùng tinh thần IoT: MaxFutureMinutes / MaxAgeDays).
    /// </summary>
    public static class TemperatureTime
    {
        public const int MaxFutureMinutes = 5;
        public const int MaxPastHours = 24;

        /// <summary>Trả về lỗi (null = hợp lệ) và giờ đo đã chuẩn hóa.</summary>
        public static string? Resolve(DateTime? measuredAt, IClock clock, out DateTime value)
        {
            DateTime now = clock.Now;
            if (measuredAt == null)
            {
                value = now;
                return null;
            }
            DateTime at = measuredAt.Value;
            if (at.Kind == DateTimeKind.Utc)
            {
                TimeSpan offset = TimeSpan.FromMinutes(Math.Round((now - clock.UtcNow).TotalMinutes));
                at = DateTime.SpecifyKind(at + offset, DateTimeKind.Unspecified);
            }
            else if (at.Kind == DateTimeKind.Local)
            {
                at = DateTime.SpecifyKind(at, DateTimeKind.Unspecified);
            }
            value = at;
            if (at > now.AddMinutes(MaxFutureMinutes) || at < now.AddHours(-MaxPastHours))
            {
                return $"Giờ đo nhiệt độ không hợp lệ: chỉ nhận trong {MaxPastHours} giờ trước tới {MaxFutureMinutes} phút sau thời điểm hiện tại (để trống = giờ hiện tại).";
            }
            return null;
        }
    }
}
