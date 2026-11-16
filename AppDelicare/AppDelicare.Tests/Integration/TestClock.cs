using AppDelicare.Data.Share;

namespace AppDelicare.Tests.Integration
{
    /// <summary>
    /// T13: đồng hồ điều khiển được cho test (giờ chốt đơn 20h D-1). <see cref="FixedNow"/> = null → giờ Việt Nam thật (như SystemClock).
    /// Đăng ký thay IClock trong <see cref="InMemoryApiFactory"/>; test nào đặt FixedNow nên dùng factory riêng (IClassFixture riêng).
    /// </summary>
    public sealed class TestClock : IClock
    {
        private static readonly TimeZoneInfo Zone = TimeZoneInfo.TryFindSystemTimeZoneById("Asia/Ho_Chi_Minh", out var z) ? z
            : TimeZoneInfo.TryFindSystemTimeZoneById("SE Asia Standard Time", out var w) ? w
            : TimeZoneInfo.CreateCustomTimeZone("ICT", TimeSpan.FromHours(7), "ICT", "ICT");

        public DateTime? FixedNow { get; set; }

        public DateTime Now => FixedNow ?? DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone), DateTimeKind.Unspecified);
        public DateTime Today => Now.Date;
        public DateTime UtcNow => FixedNow is { } f ? DateTime.SpecifyKind(f.AddHours(-7), DateTimeKind.Utc) : DateTime.UtcNow;
    }
}
