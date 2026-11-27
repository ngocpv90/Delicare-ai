using System.Globalization;
using System.Text.RegularExpressions;
using AppDelicare.Data.Connection;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Option;
using AppDelicare.Model.Request.Custom;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement.Retail
{
    /// <summary>
    /// RETAIL-1: quy tắc dùng chung của đơn khách lẻ (trang web + nhân viên lập). Máy chủ KIỂM LẠI mọi thứ, không tin giao diện:
    /// SĐT, ngày nhận theo giờ chốt, bữa, món còn bán, giá tính lại từ <c>RetailProduct.Price</c>. PROPOSED – BA/PO CONFIRM.
    /// </summary>
    public static partial class RetailRules
    {
        /// <summary>Dòng đã kiểm: sản phẩm + số lượng + giá chụp lúc đặt.</summary>
        public sealed record PricedLine(RetailProduct Product, decimal Quantity, decimal UnitPrice, decimal LineAmount);

        /// <summary>Chuẩn hóa SĐT di động VN về 0xxxxxxxxx; sai dạng → null.</summary>
        public static string? NormalizePhone(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }
            string s = Regex.Replace(raw.Trim(), @"[\s.\-()]", string.Empty);
            if (s.StartsWith("+84", StringComparison.Ordinal))
            {
                s = "0" + s[3..];
            }
            else if (s.StartsWith("84", StringComparison.Ordinal) && s.Length == 11)
            {
                s = "0" + s[2..];
            }
            return Phone().IsMatch(s) ? s : null;
        }

        [GeneratedRegex(@"^0(3|5|7|8|9)\d{8}$")]
        private static partial Regex Phone();

        public static string? Clean(string? s, int max)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return null;
            }
            string t = Regex.Replace(s.Trim(), @"\s+", " ");
            return t.Length > max ? t[..max] : t;
        }

        public static DateTime? ParseDate(string? s)
            => DateTime.TryParseExact(s?.Trim(), ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "dd/MM/yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d.Date : null;

        /// <summary>Ngày nhận sớm nhất: trước giờ chốt → ngày mai; từ giờ chốt → ngày kia.</summary>
        public static DateTime EarliestDate(DateTime now, RetailRulesOptions rules) => now.Date.AddDays(now.Hour < rules.OrderCutoffHour ? 1 : 2);

        /// <summary>Kiểm ngày nhận theo giờ chốt + số ngày đặt trước tối đa; trả lỗi hoặc null.</summary>
        public static string? CheckDate(DateTime date, DateTime now, RetailRulesOptions rules)
        {
            DateTime min = EarliestDate(now, rules), max = now.Date.AddDays(rules.MaxDaysAhead);
            if (date < min)
            {
                return string.Format(RetailMessage.DateTooEarly, min.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), rules.OrderCutoffHour);
            }
            if (date > max)
            {
                return string.Format(RetailMessage.DateTooLate, rules.MaxDaysAhead, max.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
            }
            return null;
        }

        /// <summary>
        /// Bữa: nhận nhãn (Trưa) hoặc mã (TRUA) của ReferenceLists:MealType → luôn lưu NHÃN (giao diện bán lẻ / trang khách dùng nhãn,
        /// RetailProduct.MealTypes nối nhãn bằng ';'). PROPOSED – BA/PO CONFIRM.
        /// </summary>
        public static string? ResolveMeal(string? input, ReferenceListsOptions lists)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return null;
            }
            string v = input.Trim();
            var items = Meals(lists);
            return items.FirstOrDefault(x => string.Equals(x.Label, v, StringComparison.OrdinalIgnoreCase))?.Label
                ?? items.FirstOrDefault(x => string.Equals(x.Code, v, StringComparison.OrdinalIgnoreCase))?.Label;
        }

        public static List<ReferenceItem> Meals(ReferenceListsOptions lists)
            => lists.Lists.TryGetValue(RetailCodes.MealTypeList, out var m) && m.Count > 0 ? m
                : [new() { Code = "SANG", Label = "Sáng" }, new() { Code = "TRUA", Label = "Trưa" }, new() { Code = "TOI", Label = "Tối" }];

        /// <summary>Danh sách bữa bán của sản phẩm (nhãn), đã chuẩn hóa theo ReferenceLists.</summary>
        public static List<string> MealsOf(string? mealTypes, ReferenceListsOptions lists)
            => (mealTypes ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(m => ResolveMeal(m, lists) ?? m).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        public static List<string> Tags(string? tags)
            => (tags ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        public static List<ReferenceItem> Categories(ReferenceListsOptions lists)
            => lists.Lists.TryGetValue(RetailCodes.CategoryList, out var c) ? c : [];

        /// <summary>
        /// Kiểm + tính tiền các dòng: sản phẩm có thật, đang bán, bán ở bữa này; số lượng nguyên 1…Max; giá lấy từ máy chủ.
        /// Cùng sản phẩm gửi 2 dòng → gộp. Trả (dòng, lỗi).
        /// </summary>
        public static async Task<(List<PricedLine> Lines, string? Error)> PriceLinesAsync(DatabaseContext db, IReadOnlyCollection<RetailOrderLineInput>? input,
            string meal, RetailRulesOptions rules, ReferenceListsOptions lists, ISet<Guid>? keepStoppedIds = null)
        {
            var raw = (input ?? []).Where(l => l != null && (l.ProductId is { } id && id != Guid.Empty || !string.IsNullOrWhiteSpace(l.ProductCode))).ToList();
            if (raw.Count == 0)
            {
                return ([], RetailMessage.LinesRequired);
            }
            if (raw.Count > rules.MaxLines)
            {
                return ([], string.Format(RetailMessage.TooManyLines, rules.MaxLines));
            }
            var ids = raw.Where(l => l.ProductId is { } g && g != Guid.Empty).Select(l => l.ProductId!.Value).Distinct().ToList();
            var codes = raw.Where(l => l.ProductId is not { } g || g == Guid.Empty).Select(l => l.ProductCode!.Trim()).Distinct().ToList();
            var products = await db.RetailProduct.Where(p => !p.IsDeleted && (ids.Contains(p.Id) || codes.Contains(p.Code!))).ToListAsync();
            var merged = new Dictionary<Guid, (RetailProduct Product, decimal Quantity)>();
            foreach (var l in raw)
            {
                var p = l.ProductId is { } g && g != Guid.Empty
                    ? products.FirstOrDefault(x => x.Id == g)
                    : products.FirstOrDefault(x => string.Equals(x.Code, l.ProductCode?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (p == null)
                {
                    return ([], RetailMessage.ProductNotFound);
                }
                if (l.Quantity != Math.Floor(l.Quantity) || l.Quantity < 1 || l.Quantity > rules.MaxQuantityPerLine)
                {
                    return ([], string.Format(RetailMessage.QuantityInvalid, p.Name, rules.MaxQuantityPerLine));
                }
                merged[p.Id] = merged.TryGetValue(p.Id, out var cur) ? (p, cur.Quantity + l.Quantity) : (p, l.Quantity);
            }
            // Sửa đơn: món đã có sẵn trong đơn mà sau đó ngừng bán vẫn giữ được (không bắt bỏ).
            var stopped = merged.Values.Where(x => x.Product.Status != RetailCodes.ProductActive && !(keepStoppedIds?.Contains(x.Product.Id) ?? false)).ToList();
            if (stopped.Count > 0)
            {
                return ([], string.Format(RetailMessage.ProductStopped, stopped[0].Product.Name));
            }
            var wrongMeal = merged.Values.Where(x => MealsOf(x.Product.MealTypes, lists) is { Count: > 0 } ms && !ms.Contains(meal, StringComparer.OrdinalIgnoreCase)).ToList();
            if (wrongMeal.Count > 0)
            {
                return ([], string.Format(RetailMessage.MealNotSold, meal.ToLowerInvariant(), string.Join(", ", wrongMeal.Select(x => x.Product.Name))));
            }
            foreach (var x in merged.Values.Where(x => x.Quantity > rules.MaxQuantityPerLine))
            {
                return ([], string.Format(RetailMessage.QuantityInvalid, x.Product.Name, rules.MaxQuantityPerLine));
            }
            return (merged.Values.Select(x => new PricedLine(x.Product, x.Quantity, x.Product.Price, x.Product.Price * x.Quantity)).ToList(), null);
        }

        public static RetailOrderLine ToEntity(PricedLine l, Guid orderId) => new()
        {
            Id = Guid.NewGuid(),
            RetailOrderId = orderId,
            RetailProductId = l.Product.Id,
            ProductName = l.Product.Name,
            Unit = l.Product.Unit,
            Quantity = l.Quantity,
            UnitPrice = l.UnitPrice,
            LineAmount = l.LineAmount,
        };

        public static string Day(DateTime d) => d.DayOfWeek switch
        {
            DayOfWeek.Monday => "Thứ Hai", DayOfWeek.Tuesday => "Thứ Ba", DayOfWeek.Wednesday => "Thứ Tư", DayOfWeek.Thursday => "Thứ Năm",
            DayOfWeek.Friday => "Thứ Sáu", DayOfWeek.Saturday => "Thứ Bảy", _ => "Chủ Nhật",
        };

        public static string Iso(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        public static string Money(decimal v) => v.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', '.') + "đ";
    }
}
