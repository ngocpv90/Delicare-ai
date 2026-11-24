namespace AppDelicare.Model.Model
{
    /// <summary>UI-6.31 (D-112): "Hôm nay" theo khu bảo quản (TemperatureLog/GetDayBoard).</summary>
    public class TemperatureDayBoardModel
    {
        public DateTime Date { get; set; }
        public DateTime Now { get; set; }
        public Guid? ShiftId { get; set; }
        public string? ShiftName { get; set; }
        public DateTime? ShiftStart { get; set; }
        public int ZonesOut { get; set; }
        public int DueNotMeasured { get; set; }
        public int SilentSensors { get; set; }
        public int Out7dResolved { get; set; }
        public int Out7dOpen { get; set; }
        public List<TemperatureZoneCardModel> ListZone { get; set; } = [];
    }

    public class TemperatureZoneCardModel
    {
        public Guid StorageZoneId { get; set; }
        public string? ZoneName { get; set; }
        public string? WarehouseName { get; set; }
        public decimal? TemperatureMin { get; set; }
        public decimal? TemperatureMax { get; set; }
        /// <summary>Loại đo tự theo loại khu (STORAGE / HOT_HOLDING / PACKING_AREA).</summary>
        public string MeasurementType { get; set; } = "STORAGE";
        public bool HasSensor { get; set; }
        public List<string> ListDevice { get; set; } = [];
        public decimal? LastTemperature { get; set; }
        public DateTime? LastAt { get; set; }
        public bool? LastWithin { get; set; }
        /// <summary>IOT | MANUAL.</summary>
        public string? LastSource { get; set; }
        public string? LastBy { get; set; }
        /// <summary>OK | OUT (ngoài ngưỡng chưa khắc phục) | DUE (đến giờ chưa đo) | SILENT (cảm biến mất tín hiệu) | NONE (chưa có số đo).</summary>
        public string Status { get; set; } = "NONE";
        public DateTime? SilentSince { get; set; }
        public List<TemperatureSparkPointModel> ListPoint { get; set; } = [];
    }

    public class TemperatureSparkPointModel
    {
        public DateTime At { get; set; }
        public decimal T { get; set; }
        public bool? Ok { get; set; }
    }

    /// <summary>UI-6.31: "Ghi một lượt" – mỗi khu một nhiệt độ; ngoài ngưỡng bắt buộc khắc phục.</summary>
    public class TemperatureRoundRequest
    {
        public List<TemperatureRoundItem> ListItem { get; set; } = [];
    }

    public class TemperatureRoundItem
    {
        public Guid StorageZoneId { get; set; }
        public decimal? TemperatureC { get; set; }
        public string? CorrectiveAction { get; set; }
    }

    public class TemperatureRoundResultModel
    {
        public int Saved { get; set; }
        public int OutOfRange { get; set; }
        public List<string> ListWarning { get; set; } = [];
    }

    /// <summary>UI-6.31: Sổ nhiệt độ – bộ lọc.</summary>
    public class TemperatureBookRequest
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public string? Keyword { get; set; }
        public Guid? StorageZoneId { get; set; }
        /// <summary>IOT | MANUAL.</summary>
        public string? Source { get; set; }
        public bool OnlyOutOfRange { get; set; }
        /// <summary>Gồm lõi mẻ / giao hàng / nhận hàng (mặc định tắt).</summary>
        public bool IncludeProcess { get; set; }
    }

    public class TemperatureBookModel
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public int Count { get; set; }
        public int OutOfRange { get; set; }
        public int OpenOutOfRange { get; set; }
        public List<TemperatureBookDayModel> ListDay { get; set; } = [];
    }

    public class TemperatureBookDayModel
    {
        public DateTime Date { get; set; }
        public int Count { get; set; }
        public int OutOfRange { get; set; }
        public List<TemperatureBookRowModel> ListRow { get; set; } = [];
    }

    public class TemperatureBookRowModel
    {
        public Guid Id { get; set; }
        public DateTime MeasuredAt { get; set; }
        public string? Target { get; set; }
        public Guid? StorageZoneId { get; set; }
        public Guid? ProductionBatchId { get; set; }
        public Guid? MaterialLotId { get; set; }
        public string? MeasurementType { get; set; }
        public string? MeasurementName { get; set; }
        public decimal TemperatureC { get; set; }
        public decimal? MinAllowedC { get; set; }
        public decimal? MaxAllowedC { get; set; }
        public bool? IsWithinRange { get; set; }
        public string? Source { get; set; }
        public string? By { get; set; }
        public string? CorrectiveAction { get; set; }
        /// <summary>Ngoài ngưỡng đã khắc phục (có khắc phục hoặc có lần đo sau trong ngưỡng của cùng khu).</summary>
        public bool Resolved { get; set; }
    }
}
