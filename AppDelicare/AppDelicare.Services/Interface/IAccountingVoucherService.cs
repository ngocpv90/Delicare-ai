using AppDelicare.Data.Interface;
using AppDelicare.Model.Model;

namespace AppDelicare.Services.Interface
{
    /// <summary>ACC-2 (D-131 d, e): Kế toán › Chứng từ kế toán – chứng từ gửi MISA AMIS tính khi đọc từ nghiệp vụ đã xong.</summary>
    public interface IAccountingVoucherService
    {
        /// <summary>Chứng từ của kỳ tháng (yyyy-MM, trống = tháng này) theo tab: need (cần xử lý) · sale · purchase · stock · timesheet · all.</summary>
        Task<IResult<VoucherMonthModel>> GetMonth(string? month, string? tab);

        /// <summary>Tải Excel theo mẫu AMIS cho các chứng từ chọn (bỏ dòng lỗi / chờ nghiệp vụ gốc); ghi lịch sử gửi.</summary>
        Task<IResult<FileContentModel>> ExportExcel(VoucherActionRequest request);

        /// <summary>Đánh dấu đã lập trên AMIS (luồng Excel) cho chứng từ đang chờ lập.</summary>
        Task<IResult<int>> MarkCreated(VoucherActionRequest request);
    }
}
