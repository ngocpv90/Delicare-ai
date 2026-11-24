namespace AppDelicare.Model.Model
{
    /// <summary>
    /// APR-UI-1 (D-81 e): khuôn tóm tắt CHUNG của mọi loại yêu cầu duyệt – web (danh sách + xem nhanh), điện thoại, bot dùng 1 bộ vẽ.
    /// Máy chủ tính (chỉ đọc dữ liệu có sẵn); loại chưa viết tóm tắt riêng → khuôn mặc định (tiêu đề + số chứng từ).
    /// </summary>
    public class ApprovalSummaryModel
    {
        /// <summary>Tóm tắt 1 dòng (vd. "NCC Rau sạch Đà Lạt · 38.450.000 đ · 12 dòng").</summary>
        public string? Title { get; set; }
        /// <summary>Tên loại tiếng Việt (vd. "Đơn mua hàng", "Đề nghị mua – cấp 2").</summary>
        public string? TypeName { get; set; }
        /// <summary>Phân hệ để lọc nhanh: Kinh doanh · Thực đơn · Mua hàng · Kho · Sản xuất · Nhân sự.</summary>
        public string? ModuleName { get; set; }
        /// <summary>Số chứng từ nghiệp vụ (PO-…, DH-…).</summary>
        public string? DocumentNo { get; set; }
        /// <summary>Đối tác / người liên quan (khách, NCC, nhân viên).</summary>
        public string? Counterparty { get; set; }
        /// <summary>Giá trị chính (tiền, số suất, số ngày…).</summary>
        public string? MainValue { get; set; }
        /// <summary>"Vì sao cần duyệt".</summary>
        public string? Reason { get; set; }
        /// <summary>Cảnh báo (tối đa 3) – rỗng = Bình thường.</summary>
        public List<ApprovalFlagModel> Flags { get; set; } = [];
        public List<ApprovalInfoModel> Info { get; set; } = [];
        public List<string> LineHeaders { get; set; } = [];
        public List<ApprovalLineModel> Lines { get; set; } = [];
        /// <summary>Tổng số dòng chứng từ (Lines chỉ gửi tối đa 20 dòng đầu).</summary>
        public int LineCount { get; set; }
        public List<ApprovalChangeModel> Changes { get; set; } = [];
        /// <summary>Đường dẫn màn chứng từ gốc (screen-id[?id=…]).</summary>
        public string? OpenLink { get; set; }
    }

    public class ApprovalFlagModel
    {
        public string Text { get; set; } = string.Empty;
        /// <summary>warn (cam) / high (đỏ).</summary>
        public string Severity { get; set; } = "warn";
    }

    public class ApprovalInfoModel
    {
        public string Label { get; set; } = string.Empty;
        public string? Value { get; set; }
    }

    public class ApprovalLineModel
    {
        public List<string?> Cells { get; set; } = [];
        /// <summary>Ô bất thường (tô màu).</summary>
        public bool Highlight { get; set; }
    }

    public class ApprovalChangeModel
    {
        public string Label { get; set; } = string.Empty;
        public string? Before { get; set; }
        public string? After { get; set; }
    }

    /// <summary>APR-1.3: yêu cầu tôi đã gửi còn đang mở (tab "Tôi đã gửi").</summary>
    public class ApprovalSentModel
    {
        public Guid Id { get; set; }
        public string? RequestNo { get; set; }
        public string? BusinessType { get; set; }
        public Guid? BusinessId { get; set; }
        public ApprovalSummaryModel Summary { get; set; } = new();
        public int StepNo { get; set; } = 1;
        public int StepCount { get; set; } = 1;
        public string? StepName { get; set; }
        /// <summary>Người / nhóm đang giữ cấp hiện tại (tên người có quyền duyệt cấp đó, tối đa 3 + "…").</summary>
        public string? HolderNames { get; set; }
        public DateTime RequestedAt { get; set; }
        public double WaitingHours { get; set; }
        public DateTime? DueAt { get; set; }
        public bool IsOverdue { get; set; }
        public DateTime? LastRemindAt { get; set; }
        /// <summary>Được bấm Nhắc (lần nhắc gần nhất đã ≥ ApprovalRemindHours).</summary>
        public bool CanRemind { get; set; }
    }

    /// <summary>APR-1.3: yêu cầu tôi đã quyết định (tab "Tôi đã duyệt").</summary>
    public class ApprovalDecidedModel
    {
        public Guid Id { get; set; }
        public string? RequestNo { get; set; }
        public string? BusinessType { get; set; }
        public string? TypeName { get; set; }
        public string? Title { get; set; }
        public string? DecisionCode { get; set; }
        public string? DecisionName { get; set; }
        public DateTime DecidedAt { get; set; }
        public string? Comment { get; set; }
        public int StepNo { get; set; }
        public string? RequestStatus { get; set; }
    }

    /// <summary>APR-1.3: kết quả từng yêu cầu khi duyệt nhiều.</summary>
    public class ApprovalBulkResultModel
    {
        public Guid Id { get; set; }
        public string? RequestNo { get; set; }
        public bool Ok { get; set; }
        public string? Message { get; set; }
    }

    /// <summary>APR-1.3 / D-81 k: thống kê theo loại – đo UAT.</summary>
    public class ApprovalStatsModel
    {
        public string? BusinessType { get; set; }
        public string? TypeName { get; set; }
        public int RequestCount { get; set; }
        public int ApprovedCount { get; set; }
        public int RejectedCount { get; set; }
        public int OpenCount { get; set; }
        /// <summary>% duyệt nguyên trạng = duyệt ngay lần gửi đầu (chứng từ chưa từng bị từ chối trước đó) / số đã quyết.</summary>
        public decimal? ApprovedAsIsPct { get; set; }
        /// <summary>Thời gian chờ trung bình (giờ) từ gửi tới quyết định cuối.</summary>
        public decimal? AvgWaitHours { get; set; }
    }
}
