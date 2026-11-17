namespace AppDelicare.Model.Model
{
    /// <summary>BE3.2 (Catalog 166): 1 lần đo cảm biến gửi về (JSON mảng).</summary>
    public class IotTemperatureItem
    {
        public string? DeviceCode { get; set; }
        /// <summary>Giờ đo: không ghi múi giờ = giờ Việt Nam; có múi giờ (Z / +hh:mm, ISO 8601) → đổi về giờ Việt Nam.</summary>
        public DateTime? MeasuredAt { get; set; }
        public decimal? TemperatureC { get; set; }
    }

    /// <summary>BE3.2: 1 lần đo bị từ chối (vị trí trong mảng gửi lên, bắt đầu 0).</summary>
    public class IotRejectedItemModel
    {
        public int Index { get; set; }
        public string? DeviceCode { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>BE3.2: kết quả nhận dữ liệu cảm biến.</summary>
    public class IotReceiveResultModel
    {
        public int Received { get; set; }
        /// <summary>Số lần đo đã ghi TemperatureLog.</summary>
        public int Accepted { get; set; }
        /// <summary>Đã có (cùng DeviceCode + MeasuredAt) – bỏ qua, không lỗi (gửi lại an toàn).</summary>
        public int Duplicate { get; set; }
        /// <summary>Trong số đã ghi: vượt ngưỡng khu vực (hiện ở chuông cảnh báo – BE3.3, không gửi tin).</summary>
        public int OutOfRange { get; set; }
        public List<IotRejectedItemModel> Rejected { get; set; } = [];
    }
}
