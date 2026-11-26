namespace AppDelicare.Model.Model
{
    /// <summary>ACC-8 (D-131 l): kết quả đối chiếu 1 chỉ tiêu.</summary>
    public static class AmisReconResult
    {
        public const string Match = "MATCH";
        public const string Missing = "MISSING_VOUCHER";   // lệch do còn chứng từ chưa lập / lỗi
        public const string Price = "PRICE_METHOD";        // lệch do cách tính giá xuất (AMIS bình quân ≠ giá lô) – chỉ để biết
        public const string Diff = "DIFF";                 // lệch chưa rõ nguyên nhân – kiểm tra
        public const string NoAmis = "NO_AMIS";            // chưa nhập số AMIS
        public const string WaitCount = "WAIT_COUNT";      // tồn cuối kỳ: chờ kiểm kê

        public static string Label(string? r) => r switch
        {
            Match => "Khớp", Missing => "Lệch – còn chứng từ", Price => "Lệch do giá xuất", Diff => "Lệch", NoAmis => "Chưa có số AMIS", WaitCount => "Chờ kiểm kê", _ => r ?? string.Empty,
        };
    }

    public sealed class AmisReconRowModel
    {
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        /// <summary>"amount" (đồng) | "count" (phiếu) | "qty" (suất).</summary>
        public string Kind { get; set; } = "amount";
        public string? UnitLabel { get; set; }
        public decimal Delicare { get; set; }
        public decimal? Amis { get; set; }
        public decimal? Difference { get; set; }
        public string Result { get; set; } = string.Empty;
        public string ResultName { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public string? Link { get; set; }
        public string? LinkLabel { get; set; }
    }

    public sealed class AmisReconModel
    {
        public string Month { get; set; } = string.Empty;
        public DateTime? AmisAt { get; set; }
        public string? AmisSource { get; set; }
        public string? AmisBy { get; set; }
        public int MatchCount { get; set; }
        public int MissingCount { get; set; }
        public int PriceCount { get; set; }
        public int Total { get; set; }
        public List<AmisReconRowModel> ListRow { get; set; } = [];
    }

    public sealed class AmisFigureInput
    {
        public string Code { get; set; } = string.Empty;
        public decimal? Value { get; set; }
    }

    public sealed class AmisReconSaveRequest
    {
        public string? Month { get; set; }
        public List<AmisFigureInput> ListFigure { get; set; } = [];
    }
}
