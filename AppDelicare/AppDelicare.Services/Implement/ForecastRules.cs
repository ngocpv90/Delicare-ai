using AppDelicare.Data.Connection;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// BE2.2 (spec 6.5 / 6.6) – đọc dự báo dùng chung cho Forecast/GetMatrix, mẫu tạo đơn (suggestedQuantity), chi tiết đơn (forecastQuantity) và lệch dự báo khi xác nhận.
    /// Khóa dự báo = (khách, điểm giao, ngày, ca, loại suất); "bản hiệu lực" = bản Đã công bố có VersionNo lớn nhất của khóa (không cột "bản hiện hành" – PROPOSED).
    /// </summary>
    public static class ForecastRules
    {
        public sealed record Key(Guid CustomerId, Guid? DeliveryLocationId, DateTime Date, Guid? ShiftId, string MealType);

        public static Key KeyOf(Forecast f) => new(f.CustomerId, f.DeliveryLocationId, f.ForecastDate.Date, f.ShiftId, Norm(f.MealType));

        public static string Norm(string? mealType) => (mealType ?? string.Empty).Trim().ToUpperInvariant();

        /// <summary>Bản Đã công bố mới nhất (VersionNo lớn nhất) mỗi khóa, trong khoảng ngày [from, to] (theo ngày).</summary>
        public static async Task<List<Forecast>> LatestPublishedAsync(DatabaseContext db, DateTime from, DateTime to, IReadOnlyCollection<Guid>? customerIds = null)
        {
            DateTime start = from.Date, end = to.Date.AddDays(1);
            var query = db.Forecast.AsNoTracking().Where(f => !f.IsDeleted && f.Status == ForecastStatus.Published && f.ForecastDate >= start && f.ForecastDate < end);
            if (customerIds is { Count: > 0 })
            {
                query = query.Where(f => customerIds.Contains(f.CustomerId));
            }
            var rows = await query.ToListAsync();
            return [.. rows.GroupBy(KeyOf).Select(g => g.OrderByDescending(f => f.VersionNo).ThenByDescending(f => f.CreatedDate).First())];
        }

        /// <summary>
        /// Dự báo cho 1 điểm giao / loại suất (/ ca) trong ngày: dòng dự báo của chính điểm giao; điểm chưa có dự báo riêng cho loại suất đó thì nhận phần chia đều
        /// của dự báo cấp khách (DeliveryLocationId trống) cho các điểm giao chưa có dự báo riêng – phần nguyên, điểm đầu danh sách nhận phần dư (PROPOSED – BA/PO CONFIRM, D-51).
        /// Dòng dự báo có ca chỉ khớp đúng ca; dòng không ca khớp mọi ca. Không có dự báo → null.
        /// </summary>
        public static decimal? ForLocation(IReadOnlyCollection<Forecast> rows, Guid customerId, Guid locationId, IReadOnlyList<Guid> customerLocationIds, string? mealType, Guid? shiftId)
        {
            string meal = Norm(mealType);
            bool Match(Forecast f) => f.CustomerId == customerId && Norm(f.MealType) == meal && (f.ShiftId == null || shiftId == null || f.ShiftId == shiftId);
            var own = rows.Where(f => f.DeliveryLocationId == locationId && Match(f)).ToList();
            if (own.Count != 0)
            {
                return own.Sum(f => f.ForecastQuantity);
            }
            var customerLevel = rows.Where(f => f.DeliveryLocationId == null && Match(f)).ToList();
            if (customerLevel.Count == 0)
            {
                return null;
            }
            var withoutOwn = customerLocationIds.Where(id => !rows.Any(f => f.DeliveryLocationId == id && Match(f))).ToList();
            if (!withoutOwn.Contains(locationId))
            {
                return null;
            }
            decimal total = customerLevel.Sum(f => f.ForecastQuantity);
            decimal share = Math.Floor(total / withoutOwn.Count);
            decimal remainder = total - share * withoutOwn.Count;
            return withoutOwn[0] == locationId ? share + remainder : share;
        }

        /// <summary>(đặt − dự báo) / dự báo × 100, 2 số lẻ; dự báo null / 0 → null.</summary>
        public static decimal? VariancePercent(decimal ordered, decimal? forecast)
            => forecast is { } f && f > 0 ? Math.Round((ordered - f) / f * 100m, 2, MidpointRounding.AwayFromZero) : null;
    }
}
