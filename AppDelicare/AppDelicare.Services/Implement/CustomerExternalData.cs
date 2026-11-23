using AppDelicare.Model.Helper;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// BIZ-3.6 (D-77 – A10): cấu hình theo khách lưu trong <c>Customer.ExternalData</c> (JSON có sẵn – không thêm cột): <c>containerLossMaxPct</c> = ngưỡng thất thoát khay %
    /// (trống = <c>KpiContainerLossMaxPct</c>). Ghi theo kiểu gộp khóa để không làm mất khóa khác.
    /// </summary>
    public static class CustomerExternalData
    {
        public const string ContainerLossMaxPct = "containerLossMaxPct";

        private static JObject Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith('{'))
            {
                return [];
            }
            try
            {
                return JObject.Parse(json);
            }
            catch (JsonException)
            {
                return [];
            }
        }

        /// <summary>Gộp <paramref name="incoming"/> vào <paramref name="current"/>: incoming trống → giữ; khóa null → bỏ; khóa có giá trị → ghi.</summary>
        public static string? Merge(string? current, string? incoming)
        {
            if (string.IsNullOrWhiteSpace(incoming))
            {
                return current;
            }
            if (!incoming.TrimStart().StartsWith('{'))
            {
                return current;
            }
            var result = Parse(current);
            foreach (var p in Parse(incoming).Properties())
            {
                if (p.Value.Type == JTokenType.Null)
                {
                    result.Remove(p.Name);
                }
                else
                {
                    result[p.Name] = p.Value;
                }
            }
            return result.HasValues ? result.ToString(Formatting.None) : null;
        }

        public static string? Validate(string? incoming)
        {
            var token = Parse(incoming)[ContainerLossMaxPct];
            if (token == null || token.Type == JTokenType.Null)
            {
                return null;
            }
            return token.Type is JTokenType.Integer or JTokenType.Float && token.Value<decimal>() is >= 0 and <= 100 ? null : ConstantHelper.Message.ContainerLossPctInvalid;
        }
    }
}
