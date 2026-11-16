using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace AppDelicare.WebApi.Infrastructure
{
    /// <summary>
    /// Cấu hình JSON dùng chung: camelCase (Spec 1.2) và KHÔNG BAO GIỜ trả trường mật khẩu ra ngoài
    /// (chỉ chặn khi ghi JSON; vẫn nhận được khi đọc, vd. LoginRequest.PassWord).
    /// </summary>
    public static class ApiJson
    {
        private static readonly HashSet<string> SensitiveNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "PassWord", "Password", "PasswordHash", "PassWordHash",
        };

        public static readonly JsonSerializerOptions Options = Configure(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        public static JsonSerializerOptions Configure(JsonSerializerOptions options)
        {
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            var resolver = options.TypeInfoResolver as DefaultJsonTypeInfoResolver ?? new DefaultJsonTypeInfoResolver();
            resolver.Modifiers.Add(HideSensitive);
            options.TypeInfoResolver = resolver;
            return options;
        }

        private static void HideSensitive(JsonTypeInfo typeInfo)
        {
            if (typeInfo.Kind != JsonTypeInfoKind.Object)
            {
                return;
            }
            foreach (var property in typeInfo.Properties)
            {
                if (property.AttributeProvider is System.Reflection.MemberInfo member && SensitiveNames.Contains(member.Name))
                {
                    property.ShouldSerialize = static (_, _) => false;
                }
            }
        }
    }
}
