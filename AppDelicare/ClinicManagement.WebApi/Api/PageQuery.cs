using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;

namespace AppDelicare.WebApi.Api
{
    /// <summary>Tham số danh sách chuẩn (Spec 1.2): <c>page</c>, <c>pageSize</c> (≤ 200), <c>q</c>, <c>sort=field:asc,field2:desc</c>.</summary>
    public class PageQuery
    {
        public const int MaxPageSize = 200;

        /// <summary>Trang (bắt đầu từ 1).</summary>
        /// <example>1</example>
        [FromQuery(Name = "page"), Range(1, int.MaxValue, ErrorMessage = "page phải ≥ 1.")]
        public int Page { get; set; } = 1;

        /// <summary>Số dòng mỗi trang (1–200, mặc định 20).</summary>
        /// <example>20</example>
        [FromQuery(Name = "pageSize"), Range(1, MaxPageSize, ErrorMessage = "pageSize phải từ 1 đến 200.")]
        public int PageSize { get; set; } = 20;

        /// <summary>Từ khóa tìm trên cột mã / tên.</summary>
        /// <example>ca 1</example>
        [FromQuery(Name = "q"), StringLength(200)]
        public string? Q { get; set; }

        /// <summary>Sắp xếp, vd. <c>code:asc,startTime:desc</c>. Chỉ nhận cột trong danh sách cho phép của từng API.</summary>
        /// <example>code:asc</example>
        [FromQuery(Name = "sort"), StringLength(200)]
        public string? Sort { get; set; }
    }

    public static class QueryableExtensions
    {
        /// <summary>
        /// Áp sort theo whitelist <paramref name="allowed"/> (tên camelCase → biểu thức). Cột lạ → 400 VALIDATION_FAILED.
        /// Không có sort thì dùng <paramref name="defaultSort"/>.
        /// </summary>
        public static IQueryable<T> ApplySort<T>(this IQueryable<T> source, string? sort,
            IReadOnlyDictionary<string, Expression<Func<T, object?>>> allowed,
            Func<IQueryable<T>, IOrderedQueryable<T>> defaultSort)
        {
            if (string.IsNullOrWhiteSpace(sort))
            {
                return defaultSort(source);
            }
            IOrderedQueryable<T>? ordered = null;
            foreach (string part in sort.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string[] pieces = part.Split(':', StringSplitOptions.TrimEntries);
                string field = pieces[0];
                bool desc = pieces.Length > 1 && pieces[1].Equals("desc", StringComparison.OrdinalIgnoreCase);
                if (pieces.Length > 2 || (pieces.Length == 2 && !desc && !pieces[1].Equals("asc", StringComparison.OrdinalIgnoreCase)))
                {
                    throw ApiException.Validation("sort", $"Sort '{part}' sai định dạng (field:asc|desc).");
                }
                var match = allowed.FirstOrDefault(kv => kv.Key.Equals(field, StringComparison.OrdinalIgnoreCase));
                if (match.Value is null)
                {
                    throw ApiException.Validation("sort", $"Không sắp xếp được theo '{field}'. Cho phép: {string.Join(", ", allowed.Keys)}.");
                }
                ordered = ordered is null
                    ? (desc ? source.OrderByDescending(match.Value) : source.OrderBy(match.Value))
                    : (desc ? ordered.ThenByDescending(match.Value) : ordered.ThenBy(match.Value));
            }
            return ordered ?? defaultSort(source);
        }

        public static IQueryable<T> Page<T>(this IQueryable<T> source, PageQuery query)
            => source.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize);
    }
}
