namespace AppDelicare.Model.Option
{
    /// <summary>
    /// BE3.2 (Catalog 166, spec 9.3 integration/iot/temperature – PROPOSED D-57 f): cảm biến nhiệt độ gửi về – section <c>Iot</c>.
    /// <c>ApiKeys</c> là BÍ MẬT: đặt bằng biến môi trường / user-secrets (<c>Iot__ApiKeys__0</c>, <c>Iot__ApiKeys__1</c>…), KHÔNG ghi vào appsettings trong repo.
    /// Danh sách trống = tắt API IoT (mọi request → 401). Không có bảng thiết bị (không tạo bảng) → ánh xạ thiết bị nằm ở <c>Iot:Devices</c>.
    /// </summary>
    public sealed class IotOptions
    {
        public const string Section = "Iot";

        /// <summary>Khóa API hợp lệ (header <c>X-Api-Key</c>) – mỗi cổng / thiết bị 1 khóa được. BÍ MẬT.</summary>
        public List<string> ApiKeys { get; set; } = [];

        /// <summary>Tối đa N lần đo / request (spec: 500).</summary>
        public int MaxItemsPerRequest { get; set; } = 500;

        /// <summary>Tối đa N request / phút / khóa (quá → HTTP 429).</summary>
        public int RateLimitPerMinute { get; set; } = 60;

        /// <summary>Lần đo cũ hơn N ngày → từ chối (gửi bù khi mất mạng vẫn nhận trong N ngày).</summary>
        public int MaxAgeDays { get; set; } = 7;

        /// <summary>Lần đo ở tương lai quá N phút (lệch đồng hồ thiết bị) → từ chối.</summary>
        public int MaxFutureMinutes { get; set; } = 5;

        /// <summary>Khoảng nhiệt độ hợp lý của cảm biến – ngoài khoảng coi là lỗi cảm biến, từ chối.</summary>
        public decimal MinTemperatureC { get; set; } = -60m;
        public decimal MaxTemperatureC { get; set; } = 150m;

        /// <summary>TemperatureLog.MeasurementType mặc định (ReferenceLists TemperatureMeasurementType).</summary>
        public string MeasurementType { get; set; } = "STORAGE";

        /// <summary>Mã thiết bị (DeviceCode, không phân biệt hoa thường) → khu vực / vị trí kho.</summary>
        public Dictionary<string, IotDeviceOptions> Devices { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>BE3.2: 1 thiết bị – khu vực kho theo Id hoặc mã (StorageZone.Code); vị trí kho tùy chọn.</summary>
    public sealed class IotDeviceOptions
    {
        public string? Name { get; set; }
        public Guid? StorageZoneId { get; set; }
        /// <summary>Dùng khi không ghi StorageZoneId; mã trùng ở nhiều kho → thiết bị bị từ chối (ghi rõ lý do).</summary>
        public string? StorageZoneCode { get; set; }
        public Guid? StorageLocationId { get; set; }
        /// <summary>Ghi đè MeasurementType mặc định (vd. BLAST_CHILL cho tủ làm lạnh nhanh).</summary>
        public string? MeasurementType { get; set; }
        /// <summary>false = tạm ngưng thiết bị (lần đo bị từ chối).</summary>
        public bool Enabled { get; set; } = true;
    }
}
