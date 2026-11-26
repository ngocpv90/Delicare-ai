namespace AppDelicare.Model.Model
{
    /// <summary>ACC-2 (D-131 d, e): loại chứng từ gửi MISA AMIS (tính khi đọc từ nghiệp vụ đã xong – không bảng mới).</summary>
    public static class VoucherType
    {
        public const string Sale = "SALE";               // Bán hàng tháng × khách × loại suất (bảng kê khách đã xác nhận)
        public const string Purchase = "PURCHASE";       // Nhập mua qua kho (phiếu nhận đã ghi sổ: số đạt × giá PO)
        public const string PurchaseReturn = "PURCHASE_RETURN"; // Trả lại hàng mua – phiếu trả NCC đã ghi sổ (ACC-5, D-131 i), đối tượng = NCC
        public const string Issue = "ISSUE";             // Xuất NVL cho sản xuất theo phiếu xuất (ca → buổi → thành phẩm = đối tượng THCP)
        public const string Packaging = "PACKAGING";     // Xuất bao bì theo buổi (ACC-6)
        public const string FinishedIn = "FG_IN";        // Nhập kho thành phẩm theo ngày × buổi
        public const string FinishedOut = "FG_OUT";      // Xuất thành phẩm khác (suất bù, hủy / dư) theo ngày × buổi
        public const string StockOther = "STOCK_OTHER";  // Kho khác: điều chỉnh / kiểm kê đã duyệt, hủy lô, trả hàng NCC (ACC-5)
        public const string Timesheet = "TIMESHEET";     // Bảng công tháng (chỉ Excel)

        public static string Label(string? t) => t switch
        {
            Sale => "Bán hàng (tháng)", Purchase => "Nhập mua", PurchaseReturn => "Trả lại hàng mua", Issue => "Xuất NVL", Packaging => "Xuất bao bì", FinishedIn => "Nhập kho thành phẩm",
            FinishedOut => "Xuất thành phẩm khác", StockOther => "Kho khác", Timesheet => "Bảng công", _ => t ?? string.Empty,
        };

        /// <summary>Tab màn Chứng từ kế toán.</summary>
        public static string Tab(string t) => t switch { Sale => "sale", Purchase or PurchaseReturn => "purchase", Timesheet => "timesheet", _ => "stock" };
    }

    public static class VoucherStatus
    {
        public const string Ready = "READY";
        public const string Pending = "PENDING_SOURCE";   // nghiệp vụ gốc chưa xong (vd. bảng kê chưa được khách xác nhận)
        public const string Waiting = "WAITING_AMIS";     // đã gửi / tải Excel, kế toán chưa lập trên AMIS
        public const string Created = "CREATED_ON_AMIS";
        public const string Error = "ERROR";
        public const string Changed = "CHANGED";          // nghiệp vụ gốc đổi / hủy sau khi gửi – cần điều chỉnh

        public static string Label(string? s) => s switch
        {
            Ready => "Sẵn sàng", Pending => "Chờ nghiệp vụ gốc", Waiting => "Chờ lập trên AMIS", Created => "Đã lập trên AMIS", Error => "Lỗi",
            Changed => "Đã sửa – cần điều chỉnh", _ => s ?? string.Empty,
        };
    }

    public sealed class VoucherLineModel
    {
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? Unit { get; set; }
        public decimal Quantity { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? Amount { get; set; }
        public decimal? VatPct { get; set; }
        /// <summary>Đối tượng tập hợp chi phí / kho / lý do (theo loại chứng từ).</summary>
        public string? Target { get; set; }
        public string? Note { get; set; }
    }

    public sealed class VoucherModel
    {
        /// <summary>Khóa ổn định của chứng từ (loại + nguồn) – theo dõi trạng thái gửi qua AuditChange.</summary>
        public string Key { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public string Tab { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string? ObjectCode { get; set; }
        public string? ObjectName { get; set; }
        public string? Source { get; set; }
        public decimal? Amount { get; set; }
        public decimal? Quantity { get; set; }
        public string Status { get; set; } = string.Empty;
        public string StatusName { get; set; } = string.Empty;
        public string? Note { get; set; }
        /// <summary>Đi tới chỗ sửa (hash màn FE) khi lỗi / chờ nghiệp vụ gốc.</summary>
        public string? FixLink { get; set; }
        public string? FixLabel { get; set; }
        public DateTime? SentAt { get; set; }
        public string? Hash { get; set; }
        public List<VoucherLineModel> ListLine { get; set; } = [];
    }

    public sealed class VoucherMonthModel
    {
        public string Month { get; set; } = string.Empty;
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public bool HasAmisCatalog { get; set; }
        public decimal SaleAmount { get; set; }
        public int SaleCount { get; set; }
        public decimal IssueAmount { get; set; }
        public int IssueCount { get; set; }
        public int CreatedCount { get; set; }
        public int Total { get; set; }
        public int NeedsAction { get; set; }
        public int ErrorCount { get; set; }
        public int ChangedCount { get; set; }
        public int ReadyCount { get; set; }
        public Dictionary<string, int> TabCount { get; set; } = [];
        public List<VoucherModel> ListVoucher { get; set; } = [];
    }

    public sealed class VoucherActionRequest
    {
        public string? Month { get; set; }
        public List<string> ListKey { get; set; } = [];
    }
}
