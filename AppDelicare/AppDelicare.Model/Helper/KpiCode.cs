using System.Globalization;
using System.Text.RegularExpressions;

namespace AppDelicare.Model.Helper
{
    /// <summary>
    /// BE3.4 (spec 11.7, Catalog 221–225): mã dùng chung của KPI. Công thức / mục tiêu: PROPOSED – BA/PO CONFIRM (DECISIONS "BE-3 – đề xuất" w–ab).
    /// </summary>
    public static class KpiCode
    {
        /// <summary>Mã job làm mới = <c>AnalyticsRefreshLog.JobCode</c> = khóa <c>sp_getapplock</c> (DELICARE_JOB_&lt;mã&gt;, dùng chung với AppDelicare.Job).</summary>
        public const string JobDaily = "KPI_DAILY";
        public const string JobMonthly = "KPI_MONTHLY";
        /// <summary>Hồi quy đợt 2 (D-88): tên tiếng Việt của job cho thông báo – không hiện mã kỹ thuật.</summary>
        public static string JobLabel(string? jobCode) => jobCode switch { JobDaily => "chỉ số ngày", JobMonthly => "chỉ số tháng", _ => jobCode ?? string.Empty };
        /// <summary>Spec 11.7 có INVENTORY_BALANCE – R1 không làm (tồn tính trực tiếp từ sổ kho) → 400.</summary>
        public const string JobInventoryBalance = "INVENTORY_BALANCE";

        /// <summary><c>DataQualityStatus</c>: đủ dữ liệu / thiếu dữ liệu (mẫu số 0, thiếu nguồn, chưa có công thức).</summary>
        public const string QualityOk = "OK";
        public const string QualityIncomplete = "INCOMPLETE";

        /// <summary>Chiều mục tiêu: càng cao càng tốt (≥) / càng thấp càng tốt (≤).</summary>
        public const string HigherBetter = "HIGHER";
        public const string LowerBetter = "LOWER";

        /// <summary><c>KpiDailySnapshot.CalculationVersion</c> – đổi khi đổi cách tính trong code.</summary>
        public const string CalculationVersion = "BE3.4-v1";

        // Mã chỉ tiêu có công thức trong KpiCalculationService (seed DB/seed/KpiMetricDefinition_seed.sql).
        public const string ProdAchievement = "PROD_ACHIEVEMENT";
        public const string WasteRate = "WASTE_RATE";
        public const string QcRejectRate = "QC_REJECT_RATE";
        public const string QcCoreTempPass = "QC_CORE_TEMP_PASS";
        public const string QcSampleRetained = "QC_SAMPLE_RETAINED";
        public const string DelOnTime = "DEL_ONTIME";
        public const string InvAccuracy = "INV_ACCURACY";
        public const string FinCostMeal = "FIN_COST_MEAL";
        public const string FinFoodCost = "FIN_FOOD_COST";
        public const string PkgLossRate = "PKG_LOSS_RATE";
        public const string CsComplaintSla = "CS_COMPLAINT_SLA";

        /// <summary>Spec 11.7: chỉ tiêu "càng thấp càng tốt" nhận theo từ khóa trong mã (WASTE, REJECT, EXPIRY, COST; thêm LOSS – thất thoát hộp).</summary>
        private static readonly string[] LowerKeywords = ["WASTE", "REJECT", "EXPIRY", "COST", "LOSS"];

        public static string DefaultDirection(string? metricCode)
            => LowerKeywords.Any(k => (metricCode ?? string.Empty).Contains(k, StringComparison.OrdinalIgnoreCase)) ? LowerBetter : HigherBetter;

        public static string? NormalizeDirection(string? direction) => direction?.Trim().ToUpperInvariant() switch
        {
            HigherBetter or ">=" or "≥" => HigherBetter,
            LowerBetter or "<=" or "≤" => LowerBetter,
            _ => null,
        };

        public static string DirectionLabel(string? direction) => direction == LowerBetter ? "Càng thấp càng tốt (≤ mục tiêu)" : "Càng cao càng tốt (≥ mục tiêu)";

        // GAP-KPI-01: KpiMetricDefinition không có cột mục tiêu → ghi thẻ máy đọc được ở CUỐI FormulaDescription: "[TARGET >= 95]" / "[TARGET <= 55]".
        private static readonly Regex TargetTag = new(@"\s*\[TARGET\s*(>=|<=)\s*(-?\d+(?:\.\d+)?)\]\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>Tách FormulaDescription → (công thức chữ, mục tiêu, chiều). Không có thẻ → mục tiêu null, chiều theo mã.</summary>
        public static (string Formula, decimal? Target, string Direction) ParseFormula(string? formulaDescription, string? metricCode)
        {
            string text = formulaDescription ?? string.Empty;
            var match = TargetTag.Match(text);
            if (!match.Success)
            {
                return (text.Trim(), null, DefaultDirection(metricCode));
            }
            decimal target = decimal.Parse(match.Groups[2].Value, NumberStyles.Number, CultureInfo.InvariantCulture);
            return (text[..match.Index].Trim(), target, match.Groups[1].Value == "<=" ? LowerBetter : HigherBetter);
        }

        /// <summary>Công thức chữ + mục tiêu → FormulaDescription (thẻ ở cuối). Không có mục tiêu → chỉ công thức.</summary>
        public static string ComposeFormula(string? formula, decimal? target, string direction)
        {
            string text = ParseFormula(formula, null).Formula;
            return target is { } t
                ? $"{text} [TARGET {(direction == LowerBetter ? "<=" : ">=")} {t.ToString("0.######", CultureInfo.InvariantCulture)}]"
                : text;
        }

        /// <summary>Spec 11.7: met = value ≥ target (≤ khi càng thấp càng tốt). Thiếu giá trị / mục tiêu → null.</summary>
        public static bool? Met(decimal? value, decimal? target, string direction)
            => value is { } v && target is { } t ? (direction == LowerBetter ? v <= t : v >= t) : null;
    }
}
