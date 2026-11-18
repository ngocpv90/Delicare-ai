using System.Text;

namespace AppDelicare.Messaging.Callback
{
    /// <summary>
    /// BOT-1.4 (BOT_TELEGRAM §6): <c>callback_data</c> = <c>&lt;loại&gt;:&lt;hành động&gt;:&lt;id&gt;</c> ≤ 64 byte (giới hạn Telegram), Guid dạng N (32 ký tự).
    /// Loại: <c>apv</c> duyệt · <c>inc</c> sự việc · <c>bat</c> mẻ · <c>stp</c> điểm giao · <c>chk</c> checklist · <c>menu</c> điều hướng · <c>num</c> nhập số · <c>undo</c> hoàn tác.
    /// Nút nào cũng kiểm lại quyền ở api khi bấm – callback không phải bằng chứng quyền.
    /// </summary>
    public sealed record CallbackData(string Kind, string Action, string? Id = null)
    {
        public const int MaxBytes = 64;

        public static class Kinds
        {
            public const string Approval = "apv";
            public const string Incident = "inc";
            public const string Batch = "bat";
            public const string Stop = "stp";
            public const string Checklist = "chk";
            public const string Menu = "menu";
            public const string Number = "num";
            public const string Undo = "undo";
            /// <summary>BOT-1.5b: chuyến / điểm dừng / hộp khay (GiaoVanActions).</summary>
            public const string Delivery = "dlv";
            /// <summary>BOT-1.5b: đối soát suất tại điểm phát (DiemPhatActions).</summary>
            public const string Reconciliation = "rcn";
            /// <summary>BOT-1.5c: phản ánh khách hàng (PhanAnhActions).</summary>
            public const string Complaint = "cmp";
            /// <summary>BOT-1.5c: nhân sự – chấm hộ (NhanSuActions).</summary>
            public const string Hr = "hr";
        }

        public static string Build(string kind, string action, string? id = null)
        {
            string s = id == null ? $"{kind}:{action}" : $"{kind}:{action}:{id}";
            if (Encoding.UTF8.GetByteCount(s) > MaxBytes)
            {
                throw new ArgumentException($"callback_data dài quá {MaxBytes} byte: {s}");
            }
            return s;
        }

        public static string Build(string kind, string action, Guid id) => Build(kind, action, id.ToString("N"));

        public static CallbackData? Parse(string? data)
        {
            if (string.IsNullOrWhiteSpace(data) || Encoding.UTF8.GetByteCount(data) > MaxBytes)
            {
                return null;
            }
            var parts = data.Split(':', 3);
            return parts.Length < 2 || parts[0].Length == 0 || parts[1].Length == 0 ? null : new CallbackData(parts[0], parts[1], parts.Length == 3 ? parts[2] : null);
        }

        public Guid? Guid => System.Guid.TryParseExact(Id, "N", out var g) ? g : null;
    }
}
