using AppDelicare.Data.Interface;
using AppDelicare.Model.Model;

namespace AppDelicare.Services.Interface
{
    public interface IDashboardService
    {
        public IResult<DashboardModel> GetCount();
        public IResult<DashboardModel> GetListChartOptionsBar();
        public IResult<DashboardModel> GetListChartOptionsPolarArea();
        /// <summary>BE3.5 – Catalog 231 (spec 12.1): dashboard điều hành (ngày, ca?, chi nhánh?) – cache 60 giây phần chung, cảnh báo theo người.</summary>
        public Task<IResult<DashboardExecutiveModel>> GetExecutive(DateTime? date, Guid? shiftId, Guid? branchId);
        /// <summary>BE3.5 – Catalog 232 (spec 12.2): hành trình 1 đơn hàng qua các chặng (màn hình + số chứng từ, không tên bảng / cột – D-58).</summary>
        public Task<IResult<OrderJourneyModel>> GetOrderJourney(Guid orderId);
    }
}
