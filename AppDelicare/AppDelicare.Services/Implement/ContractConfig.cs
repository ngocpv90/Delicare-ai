using AppDelicare.Model.Helper;

namespace AppDelicare.Services.Implement
{
    /// <summary>D-78 (2) – GAP-BIZ-01: đọc / ghi <c>Contract.ConfigJson</c> (gộp khóa như Customer.ExternalData).</summary>
    public static class ContractConfig
    {
        public const string BillingBasisKey = "billingBasis";
        private static readonly HashSet<string> Valid = new(StringComparer.OrdinalIgnoreCase)
        {
            DeliveryCode.BillingReceived, DeliveryCode.BillingServed, DeliveryCode.BillingOrdered, DeliveryCode.BillingDelivered, DeliveryCode.BillingAccepted,
        };

        public static bool IsValid(string? basis) => string.IsNullOrWhiteSpace(basis) || Valid.Contains(basis.Trim());

        public static string? BillingBasisOf(string? configJson)
        {
            if (string.IsNullOrWhiteSpace(configJson) || !configJson.TrimStart().StartsWith('{'))
            {
                return null;
            }
            try
            {
                var token = Newtonsoft.Json.Linq.JObject.Parse(configJson)[BillingBasisKey];
                string? value = token?.Type == Newtonsoft.Json.Linq.JTokenType.String ? ((string?)token)?.Trim().ToUpperInvariant() : null;
                return value != null && Valid.Contains(value) ? value : null;
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return null;
            }
        }

        /// <summary>Ghi khóa billingBasis (trống = bỏ khóa → theo cấu hình chung), giữ các khóa khác.</summary>
        public static string? WithBillingBasis(string? configJson, string? basis)
        {
            string value = string.IsNullOrWhiteSpace(basis) ? "null" : Newtonsoft.Json.JsonConvert.SerializeObject(basis.Trim().ToUpperInvariant());
            return CustomerExternalData.Merge(configJson, "{\"" + BillingBasisKey + "\":" + value + "}");
        }
    }
}
