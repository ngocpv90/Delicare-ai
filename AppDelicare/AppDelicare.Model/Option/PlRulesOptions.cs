namespace AppDelicare.Model.Option
{
    /// <summary>
    /// RPT-1.4 (D-140 h): ngưỡng nhận xét tự động của tab Kết quả kinh doanh – section <c>BusinessRules:Pl</c>. PROPOSED – anh / Kế toán chỉnh.
    /// </summary>
    public sealed class PlRulesOptions
    {
        /// <summary>Doanh thu đổi so tháng trước từ bao nhiêu % thì nêu (tăng / giảm).</summary>
        public decimal RevenueChangePct { get; set; } = 5m;

        /// <summary>Giá vốn tăng nhanh hơn doanh thu từ bao nhiêu điểm % thì cảnh báo.</summary>
        public decimal CostGapPct { get; set; } = 2m;

        /// <summary>Khoản chi phí tăng từ bao nhiêu % so tháng trước thì vào danh sách "tăng mạnh nhất".</summary>
        public decimal ItemIncreasePct { get; set; } = 20m;

        /// <summary>… và tăng ít nhất bao nhiêu đồng (bỏ khoản nhỏ).</summary>
        public decimal ItemMinAmount { get; set; } = 10_000_000m;

        /// <summary>Biên lợi nhuận trước thuế dưới bao nhiêu % doanh thu thì cảnh báo.</summary>
        public decimal MarginWarnPct { get; set; } = 5m;

        /// <summary>% NVL / doanh thu (lớp tạm tính) trên bao nhiêu thì cảnh báo.</summary>
        public decimal FoodCostWarnPct { get; set; } = 55m;
    }
}
