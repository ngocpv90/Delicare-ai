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
    }
}
