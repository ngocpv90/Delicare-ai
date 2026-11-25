using System.Collections;
using System.Reflection;
using AppDelicare.Model.Option;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace AppDelicare.Services.Ioc
{
    /// <summary>
    /// UI-6.47 (D-125 d): khi khởi động, liệt kê các khóa <c>BusinessRules:*</c> KHÔNG có trong cấu hình (appsettings / biến môi trường) – chúng dùng giá trị mặc định
    /// trong code (docs/claude/CONFIG_KEYS.md). Chỉ ghi MỘT dòng cảnh báo, không chặn khởi động.
    /// </summary>
    public static class ConfigDefaultsCheck
    {
        public static List<string> MissingKeys(IConfiguration configuration)
        {
            var missing = new List<string>();
            Walk(typeof(BusinessRulesOptions), BusinessRulesOptions.Section, configuration, missing, 0);
            return missing;
        }

        private static void Walk(Type type, string prefix, IConfiguration configuration, List<string> missing, int depth)
        {
            foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite))
            {
                string key = prefix + ":" + p.Name;
                bool exists = configuration.GetSection(key).Exists();
                bool nested = p.PropertyType.IsClass && p.PropertyType != typeof(string) && !typeof(IEnumerable).IsAssignableFrom(p.PropertyType)
                    && p.PropertyType.Namespace == typeof(BusinessRulesOptions).Namespace;
                if (nested && depth < 3)
                {
                    Walk(p.PropertyType, key, configuration, missing, depth + 1);
                }
                else if (!exists)
                {
                    missing.Add(key);
                }
            }
        }

        public static void Warn(IConfiguration configuration, string host)
        {
            try
            {
                var missing = MissingKeys(configuration);
                if (missing.Count != 0)
                {
                    Log.Warning("[{Host}] {Count} khóa cấu hình BusinessRules dùng giá trị mặc định (xem docs/claude/CONFIG_KEYS.md): {Keys}", host, missing.Count, string.Join(", ", missing));
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[{Host}] Không kiểm được khóa cấu hình mặc định", host);
            }
        }
    }
}
