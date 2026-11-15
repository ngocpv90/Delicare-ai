using AppDelicare.Model.Option;
using Microsoft.Extensions.Options;

namespace AppDelicare.Data.Share
{
    /// <summary>
    /// Thời gian nghiệp vụ theo giờ Việt Nam (CLAUDE.md mục 4, D-18). Code nghiệp vụ mới KHÔNG gọi <c>DateTime.Now</c> trực tiếp.
    /// </summary>
    public interface IClock
    {
        /// <summary>Giờ hiện tại theo <c>BusinessRules:TimeZone</c> (mặc định Asia/Ho_Chi_Minh), Kind = Unspecified – ghi thẳng vào cột datetime.</summary>
        DateTime Now { get; }
        DateTime Today { get; }
        DateTime UtcNow { get; }
    }

    public sealed class SystemClock(IOptions<BusinessRulesOptions> options) : IClock
    {
        private readonly TimeZoneInfo _zone = Resolve(options.Value.TimeZone);

        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime Now => DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _zone), DateTimeKind.Unspecified);
        public DateTime Today => Now.Date;

        private static TimeZoneInfo Resolve(string id)
        {
            foreach (string candidate in new[] { id, "Asia/Ho_Chi_Minh", "SE Asia Standard Time" })
            {
                if (TimeZoneInfo.TryFindSystemTimeZoneById(candidate, out var zone))
                {
                    return zone;
                }
            }
            return TimeZoneInfo.CreateCustomTimeZone("ICT", TimeSpan.FromHours(7), "Indochina Time", "Indochina Time");
        }
    }
}
