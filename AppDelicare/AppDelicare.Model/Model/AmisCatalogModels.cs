namespace AppDelicare.Model.Model
{
    /// <summary>ACC-1 (D-131 c): loại danh mục khớp mã với MISA AMIS.</summary>
    public static class AmisKind
    {
        public const string Material = "material";   // vật tư hàng hóa ↔ Nguyên liệu (gồm vật tư bao bì)
        public const string Product = "product";     // thành phẩm theo buổi / loại suất (cấu hình Integration:Misa:ProductByMeal)
        public const string Unit = "unit";           // đơn vị tính
        public const string Customer = "customer";
        public const string Supplier = "supplier";
        public const string Warehouse = "warehouse"; // kho (cấu hình Integration:Misa:WarehouseMap)

        public static readonly string[] All = [Material, Product, Unit, Customer, Supplier, Warehouse];
        /// <summary>Loại nhận được từ tệp AMIS (thành phẩm nằm trong danh sách vật tư hàng hóa).</summary>
        public static readonly string[] Importable = [Material, Unit, Customer, Supplier, Warehouse];
        /// <summary>Loại ghép mã được (đổi mã Delicare theo mã AMIS).</summary>
        public static readonly string[] Remappable = [Material, Customer, Supplier];

        public static string Label(string? kind) => kind switch
        {
            Material => "Vật tư hàng hóa", Product => "Thành phẩm theo loại suất", Unit => "Đơn vị tính",
            Customer => "Khách hàng", Supplier => "Nhà cung cấp", Warehouse => "Kho", _ => kind ?? string.Empty,
        };
    }

    /// <summary>Kết quả khớp 1 dòng.</summary>
    public static class AmisMatchStatus
    {
        public const string Matched = "MATCHED";
        public const string MissingOnAmis = "MISSING_ON_AMIS";
        public const string NameMismatch = "NAME_MISMATCH";
        public const string UnitMismatch = "UNIT_MISMATCH";
        public const string OnlyOnAmis = "ONLY_ON_AMIS";
        public const string NotConfigured = "NOT_CONFIGURED";

        public static string Label(string? s) => s switch
        {
            Matched => "Đã khớp", MissingOnAmis => "Chưa có trên AMIS", NameMismatch => "Cùng mã khác tên", UnitMismatch => "Khác ĐVT",
            OnlyOnAmis => "Chỉ có trên AMIS", NotConfigured => "Chưa khai mã AMIS", _ => s ?? string.Empty,
        };

        /// <summary>Dòng cần kế toán xử lý (mặc định mở "Cần xử lý").</summary>
        public static bool NeedsAction(string? s) => s is MissingOnAmis or NameMismatch or UnitMismatch or NotConfigured;
    }

    /// <summary>1 bản ghi danh mục AMIS trong bản chụp.</summary>
    public sealed class AmisCatalogItem
    {
        public string Code { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? Unit { get; set; }
    }

    /// <summary>Bản chụp danh mục AMIS (lưu JSON ở tài liệu đính kèm – không bảng mới).</summary>
    public sealed class AmisCatalogSnapshot
    {
        public DateTime ReceivedAt { get; set; }
        public string? Source { get; set; }
        public Dictionary<string, string?> SourceFiles { get; set; } = [];
        public Dictionary<string, DateTime> ReceivedByKind { get; set; } = [];
        public Dictionary<string, List<AmisCatalogItem>> Items { get; set; } = [];
    }

    public sealed class AmisImportResultModel
    {
        public string? Kind { get; set; }
        public string? KindName { get; set; }
        public string? Sheet { get; set; }
        public int Received { get; set; }
        public int SkippedRows { get; set; }
        public DateTime ReceivedAt { get; set; }
        public List<string> ListWarning { get; set; } = [];
    }

    public sealed class AmisMatchRowModel
    {
        public string Kind { get; set; } = string.Empty;
        public Guid? Id { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? Unit { get; set; }
        public string? AmisCode { get; set; }
        public string? AmisName { get; set; }
        public string? AmisUnit { get; set; }
        public string Status { get; set; } = string.Empty;
        public string StatusName { get; set; } = string.Empty;
        public string? Note { get; set; }
        public bool CanRemap { get; set; }
        public List<AmisCatalogItem> ListSuggestion { get; set; } = [];
    }

    public sealed class AmisKindCountModel
    {
        public string Kind { get; set; } = string.Empty;
        public string KindName { get; set; } = string.Empty;
        public int Total { get; set; }
        public int NeedsAction { get; set; }
        public DateTime? ReceivedAt { get; set; }
    }

    public sealed class AmisMatchModel
    {
        public string Kind { get; set; } = string.Empty;
        public string KindName { get; set; } = string.Empty;
        public bool HasSnapshot { get; set; }
        public DateTime? ReceivedAt { get; set; }
        public int AmisRecords { get; set; }
        /// <summary>Bản ghi Delicare đang dùng (mọi loại) / đã khớp / chưa có trên AMIS / lệch thông tin.</summary>
        public int Total { get; set; }
        public int Matched { get; set; }
        public int Missing { get; set; }
        public int Mismatch { get; set; }
        public int OnlyOnAmis { get; set; }
        public List<AmisKindCountModel> ListKind { get; set; } = [];
        public List<AmisMatchRowModel> ListRow { get; set; } = [];
    }

    public sealed class AmisRemapRequest
    {
        public string? Kind { get; set; }
        public Guid Id { get; set; }
        public string? AmisCode { get; set; }
    }
}
