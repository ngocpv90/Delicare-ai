using Microsoft.Extensions.Options;

namespace AppDelicare.Application.Common
{
    /// <summary>
    /// Thời gian nghiệp vụ theo giờ Việt Nam (CLAUDE.md mục 4, D-18). Code nghiệp vụ mới KHÔNG gọi <c>DateTime.Now</c> trực tiếp.
    /// </summary>
    public interface IClock
    {
        /// <summary>Giờ hiện tại theo <c>BusinessRules:TimeZone</c> (mặc định Asia/Ho_Chi_Minh), Kind = Unspecified – ghi thẳng vào cột datetime.</summary>
        DateTime Now { get; }
        DateTime Today { get; }
        DateTime UtcNow { get; }
    }

    public sealed class SystemClock(IOptions<BusinessRulesOptions> options) : IClock
    {
        private readonly TimeZoneInfo _zone = Resolve(options.Value.TimeZone);

        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime Now => DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _zone), DateTimeKind.Unspecified);
        public DateTime Today => Now.Date;

        private static TimeZoneInfo Resolve(string id)
        {
            foreach (string candidate in new[] { id, "Asia/Ho_Chi_Minh", "SE Asia Standard Time" })
            {
                if (TimeZoneInfo.TryFindSystemTimeZoneById(candidate, out var zone))
                {
                    return zone;
                }
            }
            return TimeZoneInfo.CreateCustomTimeZone("ICT", TimeSpan.FromHours(7), "Indochina Time", "Indochina Time");
        }
    }

    /// <summary>Section <c>BusinessRules</c> của appsettings (BUSINESS_RULES.md). Đổi ngưỡng không cần sửa code.</summary>
    public sealed class BusinessRulesOptions
    {
        public const string Section = "BusinessRules";

        public string TimeZone { get; set; } = "Asia/Ho_Chi_Minh";
        public int OrderCutoffHourPreviousDay { get; set; } = 20;
        public string OrderLockJobTime { get; set; } = "20:00";
        public string MrpJobTime { get; set; } = "20:30";
        public string PurchaseProposalJobTime { get; set; } = "21:00";
        public decimal StockCountVariancePctNeedApproval { get; set; } = 2.0m;
        public decimal PortionDeviationPctNeedApproval { get; set; } = 30.0m;
        public int TransportMinutesBeforeCutoff { get; set; } = 60;
        public decimal CoreTempMinC { get; set; } = 70.0m;
        public decimal DeliveryTempMinC { get; set; } = 60.0m;
        public decimal KpiOnTimeDeliveryTargetPct { get; set; } = 95.0m;
        public decimal KpiIngredientToRevenueMaxPct { get; set; } = 55.0m;
        public decimal KpiContainerLossMaxPct { get; set; } = 3.0m;
        public Dictionary<string, int> ComplaintSlaHours { get; set; } = new() { ["HIGH"] = 4, ["MEDIUM"] = 24, ["LOW"] = 72 };
        public Dictionary<string, int> ChecklistOffsetsMinutes { get; set; } = new()
        {
            ["PREP"] = -150, ["PREP_AREA_CLEAN"] = -90, ["BLAST_CHILL"] = -20, ["KITCHEN_CLEAN"] = 90,
        };
        public int ExpiringLotDays { get; set; } = 7;
        public int ShortageForecastDays { get; set; } = 3;
        /// <summary>Spec 5.2 bước 4: chi phí nhân công / suất khi khóa công thức (PROPOSED – BA/PO CONFIRM).</summary>
        public decimal RecipeLaborCostPerServing { get; set; }
        /// <summary>Spec 5.2 bước 4: chi phí chung / suất (PROPOSED – BA/PO CONFIRM).</summary>
        public decimal RecipeOverheadCostPerServing { get; set; }
        /// <summary>Số suất chuẩn của định lượng công thức khi Recipe.StandardOutputQty trống (spec 5.2: 100 suất).</summary>
        public decimal RecipeStandardServings { get; set; } = 100;
    }
}
