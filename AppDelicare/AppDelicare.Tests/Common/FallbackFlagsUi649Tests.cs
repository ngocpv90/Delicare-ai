using System.Text.RegularExpressions;
using AppDelicare.Model.Option;
using FluentAssertions;

namespace AppDelicare.Tests.Common
{
    /// <summary>
    /// UI-6.49 (4) (D-127 d): cờ "đường lùi" của đợt UI-6 – mặc định trong code = hành vi MỚI; không bản appsettings nào (dev, Staging, Production, Job) ghi đè về hành vi cũ.
    /// Danh sách cờ: docs/claude/CONFIG_KEYS.md mục "Cờ đường lùi" – bỏ cờ sau UAT.
    /// </summary>
    public class FallbackFlagsUi649Tests
    {
        /// <summary>Cờ → giá trị hành vi mới.</summary>
        public static readonly (string Key, bool New)[] Flags =
        [
            ("PurchaseAutoCreatePoOnApprove", true), ("PurchaseOrderAutoApproveFromApprovedRequest", true), ("ProductionAutoCloseOrder", true),
            ("ProductionAutoRecordOverProduction", true), ("PackingManualCreate", false), ("DeliveryAutoPlanAfterLock", true), ("PackagingManualOutIn", false),
            ("EmployeeStatusViaUpdate", false), ("SwapConsentRequired", true), ("AutoMarkAbsent", true), ("ForecastAutoSuggest", true), ("ForecastAutoPublish", true),
        ];

        [Fact]
        public void Code_defaults_are_the_new_behaviour()
        {
            var o = new BusinessRulesOptions();
            object Of(string key) => (typeof(BusinessRulesOptions).GetProperty(key)?.GetValue(o)) ?? typeof(HrRulesOptions).GetProperty(key)!.GetValue(o.Hr)!;
            foreach (var (key, value) in Flags)
            {
                Of(key).Should().Be(value, $"{key} mặc định phải là hành vi mới");
            }
        }

        [Fact]
        public void No_appsettings_file_sets_a_flag_back_to_old_behaviour()
        {
            string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
            var files = new[] { "AppDelicare.WebApi", "AppDelicare.Job" }
                .SelectMany(p => Directory.Exists(Path.Combine(root, p)) ? Directory.GetFiles(Path.Combine(root, p), "appsettings*.json") : []).ToList();
            files.Should().NotBeEmpty();
            foreach (var file in files)
            {
                string text = File.ReadAllText(file);
                foreach (var (key, value) in Flags)
                {
                    Regex.IsMatch(text, $"\"{key}\"\\s*:\\s*{(!value).ToString().ToLowerInvariant()}\\b").Should().BeFalse($"{Path.GetFileName(file)} đặt {key} về hành vi cũ");
                }
            }
        }
    }
}
