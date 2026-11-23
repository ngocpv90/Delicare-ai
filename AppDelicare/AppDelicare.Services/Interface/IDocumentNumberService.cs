namespace AppDelicare.Services.Interface
{
    /// <summary>
    /// Loại mã (BUSINESS_RULES §11, D-63). Hai dạng:
    /// <list type="bullet">
    /// <item><b>Danh mục</b> (dạng A): <c>TIỀNTỐ-NNNNN</c> – 5 chữ số, tăng liên tục toàn hệ thống, không phần ngày; quá 99.999 tự sang 6 chữ số.</item>
    /// <item><b>Chứng từ</b> (dạng B): <c>TIỀNTỐ-YYMMDD-NNNN</c> (số quay về 1 mỗi ngày); có hậu tố: <c>TIỀNTỐ-YYMMDD-&lt;HẬU TỐ&gt;-NN</c>.</item>
    /// </list>
    /// Tiền tố đăng ký một chỗ (<c>DocumentNumberService.Specs</c>), không tiền tố nào là phần đầu của tiền tố khác.
    /// </summary>
    public enum DocumentKind
    {
        // ---------- Chứng từ (dạng B) ----------
        /// <summary>Đơn hàng – <c>DH-YYMMDD-NNNN</c> (trước D-63: PH).</summary>
        Order,
        /// <summary>Hợp đồng – <c>HD-YYMMDD-NNN</c>.</summary>
        Contract,
        /// <summary>Thực đơn – <c>NU-YYMMDD-NNN</c> (ngày = ngày bắt đầu áp dụng).</summary>
        Menu,
        /// <summary>Đề xuất đổi món – <c>DM-YYMMDD-NNNN</c> (trước D-63: PH).</summary>
        MenuChange,
        /// <summary>Đề nghị mua – <c>MH-YYMMDD-NNNN</c> (trước D-63: PH).</summary>
        PurchaseRequest,
        /// <summary>Đơn mua NCC – <c>PO-YYMMDD-NNNN</c>.</summary>
        PurchaseOrder,
        /// <summary>Phiếu nhập – <c>PN-YYMMDD-NNNN</c> (trước D-63: PH).</summary>
        GoodsReceipt,
        /// <summary>Phiếu xuất – <c>PX-YYMMDD-NNNN</c> (trước D-63: PH).</summary>
        MaterialIssue,
        /// <summary>Kiểm kê – <c>KK-YYMMDD-NNNN</c> (trước D-63: PH).</summary>
        StockCount,
        /// <summary>Giao dịch kho tay (điều chỉnh, chuyển kho, hủy lô hết hạn) – <c>GD-YYMMDD-NNNN</c> (trước D-63: PH).</summary>
        InventoryTransaction,
        /// <summary>Lô nguyên liệu – <c>LO-YYMMDD-&lt;NCC&gt;-NN</c> (hậu tố = mã NCC, bắt buộc; trước D-63: NL).</summary>
        MaterialLot,
        /// <summary>Kế hoạch sản xuất – <c>KS-YYMMDD</c>, phiên bản sau: <c>KS-YYMMDD-2</c>, <c>-3</c>… (trước D-63: KH).</summary>
        ProductionPlan,
        /// <summary>Lệnh sản xuất – <c>LS-YYMMDD-NNNN</c> (trước D-63: PH).</summary>
        ProductionOrder,
        /// <summary>Mẻ nấu – <c>ME-YYMMDD-&lt;CA&gt;-NN</c> (hậu tố = mã ca, bắt buộc).</summary>
        ProductionBatch,
        /// <summary>Lệnh đóng gói – <c>DG-YYMMDD-NNNN</c> (trước D-63: PH).</summary>
        PackingOrder,
        /// <summary>Đơn giao hàng – <c>GH-YYMMDD-NNNN</c> (trước D-63: PH).</summary>
        DeliveryOrder,
        /// <summary>Chuyến xe – <c>CX-YYMMDD-&lt;XE&gt;-NN</c> (hậu tố = mã xe, bắt buộc).</summary>
        DeliveryRoute,
        /// <summary>Lô vệ sinh – <c>VS-YYMMDD-&lt;CA&gt;</c>, lần sau cùng ca: <c>-2</c>… (hậu tố = mã ca, bắt buộc).</summary>
        CleaningBatch,
        /// <summary>Phản ánh khách hàng – <c>PA-YYMMDD-NNNN</c>.</summary>
        Complaint,
        /// <summary>Xác nhận / yêu cầu duyệt – <c>XN-YYMMDD-NNN</c> (ApprovalRequest.RequestNo).</summary>
        Approval,
        /// <summary>Đơn nghỉ – <c>DN-YYMMDD-NNNN</c>.</summary>
        Leave,
        /// <summary>Đơn đổi ca – <c>DC-YYMMDD-NNNN</c> (trước D-63: PH).</summary>
        ShiftSwap,
        /// <summary>Phiếu QC – <c>QC-YYMMDD-NNNN</c> (QCInspection.InspectionNumber).</summary>
        QcInspection,
        /// <summary>Lần tính nhu cầu nguyên liệu – <c>MR-YYMMDD-NNN</c> (MaterialRequirement.RequirementNo).</summary>
        MaterialRequirement,

        // ---------- Danh mục (dạng A) ----------
        /// <summary>Khách hàng doanh nghiệp – <c>KHB-NNNNN</c> (Customer.Code, CIF = Code).</summary>
        CustomerBusiness,
        /// <summary>Khách hàng cá nhân – <c>KHC-NNNNN</c> (Customer.Code, CIF = Code).</summary>
        CustomerIndividual,
        /// <summary>Nhà cung cấp – <c>NC-NNNNN</c>.</summary>
        Supplier,
        /// <summary>Nguyên liệu – <c>NL-NNNNN</c>.</summary>
        Ingredient,
        /// <summary>Món ăn – <c>MA-NNNNN</c>.</summary>
        Dish,
        /// <summary>Kho – <c>KO-NNNNN</c>.</summary>
        Warehouse,
        /// <summary>Phương tiện – <c>XE-NNNNN</c>.</summary>
        Vehicle,
        /// <summary>Nhân viên mới – <c>NV-NNNNN</c> (Employee.EmployeeCode / Code = tên đăng nhập).</summary>
        Employee,
        /// <summary>Điểm giao / điểm phát – <c>DP-NNNNN</c> (CustomerDeliveryLocation.Code).</summary>
        DeliveryLocation,
        /// <summary>Bảng giá – <c>BG-NNNNN</c> (PricePolicie.Code).</summary>
        PricePolicy,
        /// <summary>Nhóm món – <c>NM-NNNNN</c> (DishCategorie.Code; anh chốt 2026-11-22: máy chủ tự sinh, trước đây người dùng đặt).</summary>
        DishCategory,
        /// <summary>Nhóm nguyên liệu – <c>NN-NNNNN</c> (IngredientCategorie.Code; anh chốt 2026-11-22).</summary>
        IngredientCategory,
    }

    /// <summary>
    /// T11 / D-63 – sinh mã chứng từ / danh mục, không trùng, an toàn khi chạy đồng thời (BUSINESS_RULES §11).
    /// Không bảng đếm, không SEQUENCE: <c>sp_getapplock</c> theo mã gốc + MAX số đang có ở cột đích (kể cả bản ghi đã xóa mềm – không cấp lại số đã dùng).
    /// PHẢI gọi trong transaction đang mở (<c>IUnitOfWork.OpenTransaction</c>) và ghi bản ghi trong cùng transaction đó – khóa giữ tới khi commit.
    /// </summary>
    public interface IDocumentNumberService
    {
        /// <summary>Mã chứng từ (dạng B). Loại danh mục bỏ qua <paramref name="date"/> / <paramref name="suffix"/>.</summary>
        Task<string> NextAsync(DocumentKind kind, DateTime date, string? suffix = null, CancellationToken cancellationToken = default);

        /// <summary>Mã danh mục (dạng A) – <c>TIỀNTỐ-NNNNN</c>.</summary>
        Task<string> NextCodeAsync(DocumentKind kind, CancellationToken cancellationToken = default);
    }
}
