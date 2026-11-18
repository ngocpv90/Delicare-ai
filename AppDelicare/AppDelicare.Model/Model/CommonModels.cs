#nullable enable
namespace AppDelicare.Model.Model
{
    /// <summary>BE2.7: danh sách dùng được với API dùng chung (thống kê trạng thái / xuất / lịch sử / đính kèm).</summary>
    public class CommonResourceModel
    {
        /// <summary>Tên danh sách = tên controller cũ (vd. Order, GoodsReceipt).</summary>
        public string Resource { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string ScreenId { get; set; } = string.Empty;
        public string? StatusColumn { get; set; }
        public string? DateColumn { get; set; }
        public List<string> ListSearchColumn { get; set; } = [];
        public List<string> ListExportColumn { get; set; } = [];
        public bool Attachable { get; set; }
    }

    /// <summary>BE2.7 (Catalog 2): số bản ghi theo trạng thái – thẻ KPI / chip "Lọc nhanh".</summary>
    public class StatusSummaryModel
    {
        public string Resource { get; set; } = string.Empty;
        public string? StatusColumn { get; set; }
        public int Total { get; set; }
        public List<StatusCountModel> ListByStatus { get; set; } = [];
    }

    public class StatusCountModel
    {
        /// <summary>Mã trạng thái; null = bản ghi chưa có trạng thái.</summary>
        public string? Status { get; set; }
        public string? StatusName { get; set; }
        public int Count { get; set; }
    }

    /// <summary>BE2.7 (Catalog 15): 1 mục lịch sử của bản ghi (AuditChange / ApprovalDecision / ComplaintHistory / tệp đính kèm).</summary>
    public class RecordHistoryItemModel
    {
        public DateTime At { get; set; }
        public Guid? By { get; set; }
        public string? ByName { get; set; }
        /// <summary>AUDIT / APPROVAL / COMPLAINT / FILE.</summary>
        public string Source { get; set; } = string.Empty;
        /// <summary>AUDIT: Added / Modified / Deleted / Download; APPROVAL: SUBMITTED / APPROVED / REJECTED…; COMPLAINT: EventType; FILE: UPLOAD.</summary>
        public string? Action { get; set; }
        public string? ActionName { get; set; }
        /// <summary>Bảng của dòng thay đổi (bản ghi chính hoặc dòng con, vd. GoodsReceiptLine).</summary>
        public string? EntityName { get; set; }
        public Guid? EntityId { get; set; }
        public string? Comment { get; set; }
        public string? ReferenceNo { get; set; }
        public List<RecordChangeModel> ListChange { get; set; } = [];
    }

    public class RecordChangeModel
    {
        public string Field { get; set; } = string.Empty;
        public string? From { get; set; }
        public string? To { get; set; }
        /// <summary>true = giá trị bị che (mật khẩu, khóa bí mật, tọa độ GPS).</summary>
        public bool Masked { get; set; }
    }

    /// <summary>BE2.7 (Catalog 19–21): tệp đính kèm (bảng DocumentFile).</summary>
    public class DocumentFileItemModel
    {
        public Guid Id { get; set; }
        /// <summary>Tên tệp gốc (kèm đuôi).</summary>
        public string? FileName { get; set; }
        /// <summary>Đuôi tệp (.pdf, .png…) – cột DocumentFile.FileType.</summary>
        public string? FileType { get; set; }
        public string? ContentType { get; set; }
        public int? FileSize { get; set; }
        public string? Category { get; set; }
        /// <summary>Danh sách chứa chứng từ (tên controller, vd. ProofOfDelivery); null = tài liệu chung #/adm-document.</summary>
        public string? Resource { get; set; }
        public Guid? RecordId { get; set; }
        /// <summary>SHA-256 nội dung (phát hiện trùng).</summary>
        public string? Sha256 { get; set; }
        public DateTime CreatedDate { get; set; }
        public Guid? CreatedBy { get; set; }
        public string? CreatedByName { get; set; }
        /// <summary>Đường dẫn tải (API nội bộ, cần đăng nhập + quyền Xem màn chứa chứng từ) – không phải đường dẫn công khai.</summary>
        public string? Url { get; set; }
        /// <summary>true = tệp cùng nội dung đã có trên cùng chứng từ → trả lại tệp cũ, không lưu thêm.</summary>
        public bool IsDuplicate { get; set; }
    }

    /// <summary>BE2.7: tệp tải về (nội dung + kiểu + tên).</summary>
    public class FileContentModel
    {
        public byte[] Content { get; set; } = [];
        public string ContentType { get; set; } = "application/octet-stream";
        public string FileName { get; set; } = "file";
    }

    /// <summary>BE2.7 (Catalog 23–30): nội dung 1 phiếu in (dựng 1 lần, xuất PDF hoặc Excel).</summary>
    public class PrintDocumentModel
    {
        /// <summary>Loại phiếu (GOODS_RECEIPT, MATERIAL_ISSUE, STOCK_COUNT, DELIVERY_ORDER, POD, RECONCILIATION).</summary>
        public string Type { get; set; } = string.Empty;
        public Guid Id { get; set; }
        /// <summary>Ký hiệu mẫu tham khảo (vd. "Mẫu số 01 - VT").</summary>
        public string? FormCode { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? DocumentNo { get; set; }
        public DateTime? DocumentDate { get; set; }
        /// <summary>Bản chưa chốt (vd. kiểm kê chưa ghi sổ) → in chữ "BẢN NHÁP".</summary>
        public bool IsDraft { get; set; }
        public string? CompanyName { get; set; }
        public string? CompanyAddress { get; set; }
        public string? CompanyPhone { get; set; }
        public string? CompanyTaxCode { get; set; }
        public List<PrintInfoRowModel> ListInfo { get; set; } = [];
        public List<string> ListHeader { get; set; } = [];
        public List<List<string>> ListRow { get; set; } = [];
        /// <summary>Dòng tổng (cùng số cột với tiêu đề; ô trống = "").</summary>
        public List<List<string>> ListFooter { get; set; } = [];
        public List<string> ListNote { get; set; } = [];
        public List<string> ListSignature { get; set; } = [];
        /// <summary>BOT-1.7d (BOT_TELEGRAM §1.3): link mở đúng phiếu trên bot Vận hành – <c>https://t.me/&lt;bot&gt;?start=DOC_&lt;mã loại&gt;_&lt;Id N&gt;</c>; FE vẽ QR từ link. Null = chưa cấu hình bot (BotUsername).</summary>
        public string? BotLink { get; set; }
    }

    public class PrintInfoRowModel
    {
        public string Label { get; set; } = string.Empty;
        public string? Value { get; set; }
    }
}
