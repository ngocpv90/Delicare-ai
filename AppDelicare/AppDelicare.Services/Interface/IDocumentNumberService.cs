namespace AppDelicare.Services.Interface
{
    /// <summary>Loại mã chứng từ (BUSINESS_RULES §11).</summary>
    public enum DocumentKind
    {
        /// <summary>Phiếu: đơn hàng, nhập, xuất, kiểm kê, đề nghị mua, lệnh SX, đóng gói, giao – <c>PH-YYMMDD-NNNN</c>.</summary>
        Voucher,
        /// <summary>Xác nhận / yêu cầu duyệt – <c>XN-YYMMDD-NNN</c> (ApprovalRequest.RequestNo).</summary>
        Approval,
        /// <summary>Phản ánh khách hàng – <c>PA-YYMMDD-NNNN</c>.</summary>
        Complaint,
        /// <summary>Đơn mua NCC – <c>PO-YYMMDD-NNNN</c>.</summary>
        PurchaseOrder,
        /// <summary>Đơn nghỉ – <c>DN-YYMMDD-NNNN</c>.</summary>
        Leave,
        /// <summary>Lô nhập – <c>NL-YYMMDD-&lt;NCC&gt;-NN</c> (hậu tố = mã NCC, bắt buộc).</summary>
        MaterialLot,
        /// <summary>Mẻ nấu – <c>ME-YYMMDD-&lt;CA&gt;-NN</c> (hậu tố = mã ca, bắt buộc).</summary>
        ProductionBatch,
        /// <summary>Chuyến xe – <c>CX-YYMMDD-&lt;XE&gt;-NN</c> (hậu tố = mã xe, bắt buộc).</summary>
        DeliveryRoute,
        /// <summary>Kế hoạch sản xuất – <c>KH-YYMMDD</c>, phiên bản sau: <c>KH-YYMMDD-2</c>, <c>-3</c>…</summary>
        ProductionPlan,
        /// <summary>Lô vệ sinh – <c>VS-YYMMDD-&lt;CA&gt;</c>, lần sau cùng ca: <c>-2</c>… (hậu tố = mã ca, bắt buộc).</summary>
        CleaningBatch,
        /// <summary>BE2.2: hợp đồng – <c>HD-YYMMDD-NNN</c> (PROPOSED – BA/PO CONFIRM, D-51; BUSINESS_RULES §11 chưa có mẫu cho hợp đồng).</summary>
        Contract,
    }

    /// <summary>
    /// T11 – sinh mã chứng từ theo ngày, không trùng, an toàn khi chạy đồng thời (BUSINESS_RULES §11).
    /// Không bảng đếm, không SEQUENCE: <c>sp_getapplock</c> theo (loại, ngày, hậu tố) + MAX số đang có ở cột đích.
    /// PHẢI gọi trong transaction đang mở (<c>IUnitOfWork.OpenTransaction</c>) và ghi bản ghi trong cùng transaction đó – khóa giữ tới khi commit.
    /// </summary>
    public interface IDocumentNumberService
    {
        Task<string> NextAsync(DocumentKind kind, DateTime date, string? suffix = null, CancellationToken cancellationToken = default);
    }
}
