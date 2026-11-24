using System.Globalization;
using AppDelicare.Messaging.Audiences.Internal;
using AppDelicare.Messaging.Callback;
using AppDelicare.Messaging.Presentation;

namespace AppDelicare.Messaging.Experience
{
    /// <summary>
    /// BOT-UX-2 (mục 10, 11): danh sách tối đa <see cref="PageSize"/> mục / trang + hàng [◀ Trước] [Sau ▶] (callback <c>pg:&lt;mã chức năng&gt;:&lt;trang&gt;[.&lt;lọc&gt;]</c> –
    /// chạy lại chức năng CHỈ ĐỌC ở trang đó, không gọi api ghi). Danh sách có lọc mặc định (ca hiện tại…) thêm [Xem tất cả] / [Chỉ …] – lọc <see cref="All"/> = tất cả.
    /// Việc gấp (quá giờ, cảnh báo) do chức năng xếp lên đầu trước khi chia trang.
    /// </summary>
    public static class ListPager
    {
        public const int PageSize = 6;
        public const string All = "all";
        public const string Row = "pager";

        /// <summary>Trang (0 = đầu) và bộ lọc từ callback <c>pg:*</c> (null / không đọc được → trang 0, lọc mặc định).</summary>
        public static (int Page, string? Filter) Parse(string? callback)
        {
            var d = CallbackData.Parse(callback);
            if (d is not { Kind: CallbackData.Kinds.Page })
            {
                return (0, null);
            }
            string[] parts = (d.Id ?? "0").Split('.', 2);
            int page = int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int p) && p > 0 ? p : 0;
            return (page, parts.Length > 1 && parts[1].Length > 0 ? parts[1] : null);
        }

        /// <summary>
        /// Đổ 1 trang danh sách vào <paramref name="m"/>: dòng + nút của các mục trong trang, tiêu đề thêm "· trang x/y", hàng [◀ Trước] [Sau ▶].
        /// <paramref name="filterToggle"/> = (nhãn, lọc) của nút đổi bộ lọc (vd. ("📋 Xem tất cả", "all")) – null = không có.
        /// </summary>
        public static void Fill<T>(NeutralMessage m, BotActionContext ctx, string actionCode, IReadOnlyList<T> items, Func<T, string>? line, Func<T, NeutralButton?>? button,
            (string Label, string Filter)? filterToggle = null)
        {
            int pages = Math.Max(1, (items.Count + PageSize - 1) / PageSize);
            int page = Math.Clamp(ctx.Page, 0, pages - 1);
            var slice = items.Skip(page * PageSize).Take(PageSize).ToList();
            if (line != null)
            {
                m.Lines.AddRange(slice.Select(line));
            }
            if (button != null)
            {
                m.Buttons.AddRange(slice.Select(button).Where(b => b != null).Select(b => b!));
            }
            if (pages > 1)
            {
                m.Title = (m.Title ?? string.Empty) + $" · trang {page + 1}/{pages}";
                string? f = ctx.Filter;
                if (page > 0)
                {
                    m.Buttons.Add(new NeutralButton("◀ Trước", Callback(actionCode, page - 1, f), Row: Row));
                }
                if (page < pages - 1)
                {
                    m.Buttons.Add(new NeutralButton("Sau ▶", Callback(actionCode, page + 1, f), Row: Row));
                }
            }
            if (filterToggle is { } t)
            {
                m.Buttons.Add(new NeutralButton(t.Label, Callback(actionCode, 0, t.Filter)));
            }
        }

        public static string Callback(string actionCode, int page, string? filter)
            => CallbackData.Build(CallbackData.Kinds.Page, actionCode, page.ToString(CultureInfo.InvariantCulture) + (string.IsNullOrEmpty(filter) ? string.Empty : "." + filter));
    }
}
