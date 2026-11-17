#nullable enable
using Microsoft.AspNetCore.Http;

namespace AppDelicare.Model.Request.Custom
{
    /// <summary>BE2.7 (Catalog 2, 8): bộ lọc chung của danh sách – thống kê trạng thái / xuất Excel.</summary>
    public class CommonListRequest
    {
        /// <summary>Tên danh sách (tên controller cũ, vd. Order, GoodsReceipt) – chỉ danh sách trong <c>Common/GetResources</c>.</summary>
        public string? Resource { get; set; }
        /// <summary>Tìm trên cột mã / số / tên của danh sách.</summary>
        public string? SearchText { get; set; }
        /// <summary>Lọc trạng thái (xuất Excel); thống kê bỏ qua tham số này.</summary>
        public string? Status { get; set; }
        /// <summary>Lọc theo cột ngày chính của danh sách (vd. Order.MealDate), tính cả ngày.</summary>
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        /// <summary>Xuất: xlsx (mặc định) / csv.</summary>
        public string? Format { get; set; }
    }

    /// <summary>BE2.7 (Catalog 19): tải lên 1 tệp đính kèm (multipart/form-data).</summary>
    public class DocumentFileUploadRequest
    {
        public IFormFile? File { get; set; }
        /// <summary>signature / photo / evidence / contract / other (mặc định other).</summary>
        public string? Category { get; set; }
        /// <summary>Danh sách chứa chứng từ (vd. ProofOfDelivery, GoodsReceipt); trống = tài liệu chung #/adm-document.</summary>
        public string? Resource { get; set; }
        /// <summary>Id chứng từ (bắt buộc khi có Resource).</summary>
        public Guid? RecordId { get; set; }
    }

    /// <summary>BE2.7 (Catalog 20): danh sách tệp đính kèm.</summary>
    public class DocumentFileListRequest
    {
        public string? SearchText { get; set; }
        /// <summary>Đuôi tệp: pdf / .pdf / png…</summary>
        public string? FileType { get; set; }
        public string? Category { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public Guid? CreatedBy { get; set; }
        /// <summary>Lọc theo chứng từ: Resource + RecordId (cần quyền Xem màn đó); trống = mọi tệp (cần quyền Xem #/adm-document).</summary>
        public string? Resource { get; set; }
        public Guid? RecordId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    /// <summary>BE2.7 (Catalog 30): in hàng loạt. JSON vì có danh sách phiếu.</summary>
    public class PrintBatchRequest
    {
        public List<PrintItemRequest>? Items { get; set; }
        /// <summary>Tùy chọn: thêm mọi phiếu giao (DELIVERY_ORDER) của chuyến theo thứ tự điểm dừng.</summary>
        public Guid? DeliveryRouteId { get; set; }
        /// <summary>xlsx (mặc định) / docx; pdf chưa hỗ trợ (D-56 b).</summary>
        public string? Format { get; set; }
    }

    public class PrintItemRequest
    {
        /// <summary>GOODS_RECEIPT / MATERIAL_ISSUE / STOCK_COUNT / DELIVERY_ORDER / POD / RECONCILIATION (nhận cả tên controller: GoodsReceipt…).</summary>
        public string? Type { get; set; }
        public Guid Id { get; set; }
    }
}
