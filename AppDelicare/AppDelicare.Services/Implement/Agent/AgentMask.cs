using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AppDelicare.Services.Implement.Agent
{
    /// <summary>
    /// AI-3 (D-133): hàm che dữ liệu cá nhân DÙNG CHUNG cho mọi kết quả công cụ agent: số điện thoại, email, MST, CCCD, số tài khoản (chuỗi 9–16 chữ số),
    /// và mọi trường có tên nhạy cảm (phone, email, taxCode, identity…, bankAccount…). Đồng thời cắt mảng dài hơn <c>Agent:MaxRows</c>
    /// (ghi <c>&lt;tên&gt;_total</c> / <c>&lt;tên&gt;_truncated</c> cạnh mảng). Số (number) không bị che – chỉ chuỗi.
    /// </summary>
    public static partial class AgentMask
    {
        public const string Hidden = "[ẩn]";
        private static readonly string[] SensitiveKeys = ["phone", "mobile", "email", "taxcode", "mst", "identity", "cccd", "cmnd", "idcard", "passport", "bankaccount", "accountnumber", "password", "token", "secret"];

        [GeneratedRegex(@"[\w.+-]+@[\w-]+(\.[\w-]+)+", RegexOptions.CultureInvariant)]
        private static partial Regex EmailRx();

        [GeneratedRegex(@"(?<![\w-])(\+?84|0)([\s.]?\d){8,10}(?![\w-])", RegexOptions.CultureInvariant)]
        private static partial Regex PhoneRx();

        [GeneratedRegex(@"(?<![\w-])\d{9,16}(?![\w-])", RegexOptions.CultureInvariant)]
        private static partial Regex LongNumberRx();

        /// <summary>Ngày giờ ISO (yyyy-MM-ddTHH:mm:ss.fffffff) – không che (phần giây lẻ giống số điện thoại).</summary>
        [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}([T ]\d{2}:\d{2}(:\d{2}(\.\d+)?)?)?(Z|[+-]\d{2}:\d{2})?$", RegexOptions.CultureInvariant)]
        private static partial Regex IsoDateRx();

        /// <summary>Che 1 chuỗi tự do.</summary>
        public static string Text(string? s)
        {
            if (string.IsNullOrEmpty(s) || IsoDateRx().IsMatch(s))
            {
                return s ?? string.Empty;
            }
            s = EmailRx().Replace(s, Hidden);
            s = PhoneRx().Replace(s, Hidden);
            return LongNumberRx().Replace(s, Hidden);
        }

        private static bool SensitiveKey(string key)
        {
            string k = key.Replace("_", string.Empty).ToLowerInvariant();
            return SensitiveKeys.Any(x => k.Contains(x, StringComparison.Ordinal));
        }

        /// <summary>Đổi kết quả công cụ thành JSON đã che + cắt mảng; trả kèm số dòng của mảng lớn nhất (ghi nhật ký).</summary>
        public static (JsonNode? Node, int Rows) Apply(object? data, int maxRows, JsonSerializerOptions options)
        {
            if (data == null)
            {
                return (null, 0);
            }
            var node = data as JsonNode ?? JsonSerializer.SerializeToNode(data, options);
            int rows = 0;
            Walk(node, maxRows, ref rows);
            if (node is JsonArray top && top.Count > maxRows)
            {
                int total = top.Count;
                while (top.Count > maxRows)
                {
                    top.RemoveAt(top.Count - 1);
                }
                node = new JsonObject { ["items"] = top, ["total"] = total, ["truncated"] = true };
            }
            return (node, rows);
        }

        private static void Walk(JsonNode? node, int maxRows, ref int rows)
        {
            switch (node)
            {
                case JsonObject o:
                    foreach (var key in o.Select(kv => kv.Key).ToList())
                    {
                        var child = o[key];
                        if (child is JsonValue v && v.TryGetValue(out string? str))
                        {
                            o[key] = SensitiveKey(key) && !string.IsNullOrEmpty(str) ? Hidden : Text(str);
                        }
                        else if (child is JsonArray a)
                        {
                            rows = Math.Max(rows, a.Count);
                            if (a.Count > maxRows)
                            {
                                o[key + "_total"] = a.Count;
                                o[key + "_truncated"] = true;
                                while (a.Count > maxRows)
                                {
                                    a.RemoveAt(a.Count - 1);
                                }
                            }
                            Walk(a, maxRows, ref rows);
                        }
                        else
                        {
                            Walk(child, maxRows, ref rows);
                        }
                    }
                    break;
                case JsonArray arr:
                    rows = Math.Max(rows, arr.Count);
                    for (int i = 0; i < arr.Count; i++)
                    {
                        if (arr[i] is JsonValue v && v.TryGetValue(out string? str))
                        {
                            arr[i] = Text(str);
                        }
                        else
                        {
                            Walk(arr[i], maxRows, ref rows);
                        }
                    }
                    break;
            }
        }
    }
}
