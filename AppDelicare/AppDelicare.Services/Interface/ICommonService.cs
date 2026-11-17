using AppDelicare.Data.Interface;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;

namespace AppDelicare.Services.Interface
{
    /// <summary>
    /// BE2.7 (spec 2.1 / 2.2, Catalog 2, 8, 15): API dùng chung cho mọi màn hình danh sách – chỉ danh sách trong danh sách trắng
    /// (<c>CommonRegistry</c>), kiểm quyền theo màn hình của danh sách (Xem / Xuất). Không ghi DB.
    /// </summary>
    public interface ICommonService
    {
        /// <summary>Danh sách được hỗ trợ (chỉ những màn người dùng có quyền Xem; admin thấy hết).</summary>
        public Task<IResults<CommonResourceModel>> GetResources();
        /// <summary>Catalog 2: tổng + số bản ghi theo trạng thái (cùng bộ lọc với danh sách). Quyền Xem.</summary>
        public Task<IResult<StatusSummaryModel>> GetStatusSummary(CommonListRequest request);
        /// <summary>Catalog 8: xuất danh sách ra xlsx / csv (cột cố định theo danh sách). Quyền Xuất.</summary>
        public Task<IResult<FileContentModel>> ExportList(CommonListRequest request);
        /// <summary>Catalog 15: lịch sử bản ghi gộp AuditChange (+ dòng con) + ApprovalDecision + ComplaintHistory + tệp đính kèm, mới nhất trước. Quyền Xem.</summary>
        public Task<IResults<RecordHistoryItemModel>> GetHistory(string? resource, Guid id);
        /// <summary>BE3.3 – Catalog 18: chuông cảnh báo (spec 12.1) – chỉ cảnh báo có số > 0 thuộc màn người dùng được Xem; cache 60 giây theo (người, chi nhánh).</summary>
        public Task<IResults<AlertModel>> GetAlerts(Guid? branchId);
        /// <summary>BE3.3 – Catalog 17: tìm kiếm toàn cục – màn hình theo menu được Xem + bản ghi của 8 loại danh sách trắng (không trả tên bảng / cột – D-58).</summary>
        public Task<IResult<GlobalSearchModel>> GlobalSearch(string? q);
        /// <summary>BE3.3 – Catalog 16: tab Liên quan – nhóm chứng từ theo quan hệ viết cứng, mỗi nhóm ≤ 8 dòng + tổng; bỏ nhóm không được Xem.</summary>
        public Task<IResults<RelatedGroupModel>> GetRelated(string? resource, Guid id);
    }
}
