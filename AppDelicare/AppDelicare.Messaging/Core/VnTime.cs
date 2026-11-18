namespace AppDelicare.Messaging.Core
{
    /// <summary>Giờ Việt Nam (D-18) cho hiển thị và tham số ngày gửi api – container có thể chạy UTC.</summary>
    public static class VnTime
    {
        public static readonly TimeZoneInfo Zone = Find("Asia/Ho_Chi_Minh") ?? Find("SE Asia Standard Time") ?? TimeZoneInfo.CreateCustomTimeZone("VN", TimeSpan.FromHours(7), "VN", "VN");

        public static DateTime Now(TimeProvider time) => TimeZoneInfo.ConvertTime(time.GetUtcNow(), Zone).DateTime;

        public static DateTime Today(TimeProvider time) => Now(time).Date;

        private static TimeZoneInfo? Find(string id)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
