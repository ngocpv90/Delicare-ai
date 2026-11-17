#nullable enable
using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;

namespace AppDelicare.Services.Interface
{
    public interface IAnalyticsRefreshLogService
    {
        public Task<IResults<AnalyticsRefreshLogViewModel>> GetAll(AnalyticsRefreshLogSearchRequest request);
        public Task<IResult<bool>> Save(AnalyticsRefreshLogRequest request, string typeSave);
        public IResults<AnalyticsRefreshLog> GetAllData();
        public Task<IResult<AnalyticsRefreshLog>> GetById(Guid id);
        /// <summary>BE3.4 (Catalog 225): nhật ký làm mới KPI / chạy job – lọc jobCode, trạng thái, khoảng ngày bắt đầu; mới nhất trước, phân trang.</summary>
        public Task<IResults<AnalyticsRefreshLogItemModel>> GetList(AnalyticsRefreshLogListRequest request);
    }
}
