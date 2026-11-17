using AppDelicare.Data.Interface;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;

namespace AppDelicare.Services.Interface
{
    /// <summary>BE3.4 (Catalog 222 / 223): đọc KPI ngày / tháng cho màn <c>#/kpi-daily</c>, <c>#/kpi-monthly</c>.</summary>
    public interface IKpiSnapshotService
    {
        Task<IResult<KpiDashboardModel>> GetDaily(KpiDailyRequest request);
        Task<IResult<KpiDashboardModel>> GetMonthly(KpiMonthlyRequest request);
    }
}
