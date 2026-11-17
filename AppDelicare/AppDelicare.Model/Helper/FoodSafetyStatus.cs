namespace AppDelicare.Model.Helper
{
    /// <summary>BE2.5 – mã dùng chung cho checklist ATTP, vệ sinh khay / hộp và sổ bao bì (PROPOSED – BA/PO CONFIRM, DECISIONS D-54).</summary>
    public static class FoodSafetyCode
    {
        /// <summary>FoodSafetyChecklistResult / CleaningChecklistResult.ResultCode của 1 tiêu chí (spec 9.2: PASS | FAIL).</summary>
        public const string ResultPass = "PASS";
        public const string ResultFail = "FAIL";

        /// <summary>Chuẩn hóa kết quả tiêu chí: PASS / ĐẠT / OK → PASS; FAIL / KHÔNG ĐẠT / NG → FAIL; còn lại null.</summary>
        public static string? NormalizeResult(string? value) => value?.Trim().ToUpperInvariant() switch
        {
            "PASS" or "OK" or "DAT" or "ĐẠT" or "Y" => ResultPass,
            "FAIL" or "NG" or "KHONG_DAT" or "KHÔNG ĐẠT" or "N" => ResultFail,
            _ => null,
        };

        public static string ResultLabel(string? code) => code switch
        {
            ResultPass => "Đạt",
            ResultFail => "Không đạt",
            _ => code ?? string.Empty,
        };
    }

    /// <summary>BE2.5 – trạng thái mẻ vệ sinh khay / hộp (STATUS_CATALOG chưa có – spec 9.4: InProgress → Completed, QcResult PASS / REJECT; PROPOSED D-54).</summary>
    public static class CleaningBatchStatus
    {
        public const string InProgress = "InProgress";
        public const string Completed = "Completed";

        public const string QcPass = "PASS";
        public const string QcReject = "REJECT";

        public static string Label(string? status) => status switch
        {
            InProgress => "Đang vệ sinh",
            Completed => "Hoàn tất",
            _ => status ?? string.Empty,
        };

        public static string QcLabel(string? qc) => qc switch
        {
            QcPass => "Đạt – sẵn sàng dùng lại",
            QcReject => "Không đạt – vệ sinh lại",
            _ => qc ?? string.Empty,
        };
    }

    /// <summary>BE2.5 – trạng thái thu hồi khay / hộp (STATUS_CATALOG: ContainerReturn.Status Expected → Returned → Inspected, PROPOSED).</summary>
    public static class ContainerReturnStatus
    {
        public const string Expected = "Expected";
        public const string Returned = "Returned";
        public const string Inspected = "Inspected";

        public static string Label(string? status) => status switch
        {
            Expected => "Chờ thu hồi",
            Returned => "Đã thu hồi",
            Inspected => "Đã kiểm tra",
            _ => status ?? string.Empty,
        };
    }

    /// <summary>BE2.5 – kết quả kiểm tra vật dụng thu hồi (STATUS_CATALOG: ReturnInspection.InspectionResult, theo nguồn).</summary>
    public static class ReturnInspectionResult
    {
        public const string Ok = "OK";
        public const string Shortage = "SHORTAGE";
        public const string Damaged = "DAMAGED";
        public const string Dirty = "DIRTY";
        public const string Rejected = "REJECTED";

        public static readonly string[] All = [Ok, Shortage, Damaged, Dirty, Rejected];

        public static string Label(string? code) => code switch
        {
            Ok => "Đủ",
            Shortage => "Thiếu",
            Damaged => "Hỏng",
            Dirty => "Bẩn",
            Rejected => "Không đạt",
            _ => code ?? string.Empty,
        };
    }

    /// <summary>
    /// BE2.5 – loại phát sinh của sổ bao bì <c>PackagingLedger.TransactionType</c> (sổ chỉ ghi thêm). PROPOSED – BA/PO CONFIRM (D-54 b, GAP-PKG-03):
    /// OUT = giao cho khách (lúc bàn giao), IN = khách trả (thu hồi), LOSS = thất thoát tại khách (ghi nhận / tính phí); AVAILABLE = khay sạch sẵn sàng dùng lại (mẻ vệ sinh đạt, không gắn khách).
    /// Số còn ở khách = Σ OUT − Σ IN − Σ LOSS.
    /// </summary>
    public static class PackagingTransactionType
    {
        public const string Out = "OUT";
        public const string In = "IN";
        public const string Loss = "LOSS";
        public const string Available = "AVAILABLE";

        /// <summary>Loại được ghi tay qua PackagingLedger/Record (điều chỉnh, tồn đầu kỳ, thất thoát).</summary>
        public static readonly string[] Manual = [Out, In, Loss];

        public static string Label(string? code) => code switch
        {
            Out => "Giao cho khách",
            In => "Khách trả",
            Loss => "Thất thoát",
            Available => "Sẵn sàng dùng lại",
            _ => code ?? string.Empty,
        };
    }

    /// <summary>BE2.5 – tình trạng của dòng sổ bao bì (<c>PackagingLedger.ConditionCode</c>).</summary>
    public static class PackagingCondition
    {
        public const string Good = "GOOD";
        public const string Damaged = "DAMAGED";
        public const string Clean = "CLEAN";
    }

    /// <summary>BE2.5 (spec 9.5, STATUS_CATALOG SupplierIssue.IssueStatus): Open → InProgress → Resolved → Closed (PROPOSED D-54 e).</summary>
    public static class SupplierIssueStatus
    {
        public const string Open = "Open";
        public const string InProgress = "InProgress";
        public const string Resolved = "Resolved";
        public const string Closed = "Closed";

        public static readonly string[] All = [Open, InProgress, Resolved, Closed];

        /// <summary>Chuyển hợp lệ: Mới → Đang xử lý / Đã xử lý; Đang xử lý → Đã xử lý; Đã xử lý → Đóng / mở lại Đang xử lý.</summary>
        public static bool CanMove(string? from, string to) => (from, to) switch
        {
            (Open, InProgress) => true,
            (Open, Resolved) => true,
            (InProgress, Resolved) => true,
            (Resolved, Closed) => true,
            (Resolved, InProgress) => true,
            _ => false,
        };

        public static string Label(string? status) => status switch
        {
            Open => "Mới",
            InProgress => "Đang xử lý",
            Resolved => "Đã xử lý",
            Closed => "Đóng",
            _ => status ?? string.Empty,
        };
    }
}
