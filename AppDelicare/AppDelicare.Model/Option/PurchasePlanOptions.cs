namespace AppDelicare.Model.Option
{
    /// <summary>
    /// UI-6.51 (D-137): kế hoạch mua tự động. Mục cấu hình <c>PurchasePlan</c> (appsettings – không phải cột).
    /// Ngưỡng tiền dùng chung <c>BusinessRules</c> (PurchaseOrderAutoApproveMaxAmount, PurchaseOrderDirectorApprovalAmount, ApprovalFlagPricePct).
    /// </summary>
    public sealed class PurchasePlanOptions
    {
        public const string Section = "PurchasePlan";

        /// <summary>Tắt cả job lập đề nghị tự động (màn vẫn xem được, bấm tay vẫn chạy).</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Giờ job lập / cập nhật đề nghị mua cho phần đến hạn đặt (lần 1 = job PurchasePlan, lần 2 = job PurchasePlanEvening – trước MrpJob 20:30).</summary>
        public string[] RunTimes { get; set; } = [];

        /// <summary>Giờ chạy lần <paramref name="index"/> (0 / 1) – trống dùng mặc định 14:00 / 20:15 (mảng cấu hình không đặt sẵn giá trị: binder nối thêm phần tử).</summary>
        public string RunTime(int index) => RunTimes.Length > index && !string.IsNullOrWhiteSpace(RunTimes[index]) ? RunTimes[index].Trim() : index == 0 ? "14:00" : "20:15";

        /// <summary>Giờ tự gửi đơn mua đã duyệt cho NCC (đơn duyệt sau giờ này gửi ngay khi duyệt).</summary>
        public string SendTime { get; set; } = "15:00";

        /// <summary>Tự gửi đơn mua cho NCC qua kênh tích hợp (false = chỉ đánh dấu chờ gửi tay).</summary>
        public bool AutoSend { get; set; } = true;

        /// <summary>Thay thế rẻ hơn (giá trên 1 kg dùng được) ít nhất % này mới dùng; chênh ít hơn giữ nguyên liệu chuẩn.</summary>
        public decimal SubstituteMinSavingPct { get; set; } = 5m;

        /// <summary>Đề nghị do hệ thống lập: số lượng ≤ nhu cầu tính × (1 + % này) mới coi là đúng định mức.</summary>
        public decimal TolerancePct { get; set; } = 10m;

        /// <summary>Chia NCC: tỷ lệ lần này = mục tiêu + (mục tiêu − lũy kế tháng), chặn trong ± số điểm này.</summary>
        public decimal SplitMaxCorrectionPct { get; set; } = 15m;

        /// <summary>Đơn đã gửi mà NCC chưa xác nhận trước giờ giao ít hơn số giờ này → bot báo Mua hàng.</summary>
        public int SupplierConfirmHours { get; set; } = 12;

        /// <summary>Tự gửi duyệt đề nghị do job lập (false = để Nháp cho người mua gửi).</summary>
        public bool AutoSubmit { get; set; } = true;
    }
}
