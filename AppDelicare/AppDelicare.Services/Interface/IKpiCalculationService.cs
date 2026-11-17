using AppDelicare.Data.Interface;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;

namespace AppDelicare.Services.Interface
{
    /// <summary>
    /// BE3.4 (spec 11.7, Catalog 224, D-48): tính KPI từ bảng giao dịch rồi upsert <c>KpiDailySnapshot</c> / <c>KpiMonthlySnapshot</c>.
    /// Dùng chung cho nút <c>KpiSnapshot/Refresh</c> (khóa + nhật ký) và job KPI_DAILY / KPI_MONTHLY của AppDelicare.Job (JobRunService khóa + ghi nhật ký).
    /// </summary>
    public interface IKpiCalculationService
    {
        /// <summary>Bấm tay: kiểm tra → khóa <c>sp_getapplock</c> theo jobCode → AnalyticsRefreshLog Running → tính + upsert → Succeeded / Failed.</summary>
        Task<IResult<KpiRefreshResultModel>> Refresh(KpiRefreshRequest request);

        /// <summary>
        /// Tính + upsert theo (ngày / tháng, chi nhánh, mã, phiên bản) trong 1 transaction – chạy lại không nhân đôi. Không khóa / không ghi nhật ký
        /// (nơi gọi lo). <paramref name="periodDate"/> = ngày (KPI_DAILY) hoặc ngày bất kỳ trong tháng (KPI_MONTHLY).
        /// </summary>
        Task<KpiRefreshResultModel> Calculate(string jobCode, DateTime periodDate, Guid? branchId, string trigger, CancellationToken cancellationToken = default);
    }
}
