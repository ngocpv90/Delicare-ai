using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;

namespace AppDelicare.Services.Interface
{
    public interface ICustomerService
    {
        public Task<IResults<CustomerViewModel>> GetAll(CustomerSearchRequest request);
        public Task<IResult<bool>> Save(CustomerRequest request, string typeSave);
        public IResults<Customer> GetAllData();
        public Task<IResult<Customer>> GetById(Guid id);
        // <summary>T45R: hồ sơ khách hàng 360°.</summary>
        public Task<IResult<CustomerOverviewModel>> GetOverview(Guid id);
        /// <summary>UI-1.2 (D-64): số liệu 4 thẻ của danh sách khách hàng theo chi nhánh / loại / tìm kiếm.</summary>
        public Task<IResult<CustomerSummaryModel>> GetSummary(Guid? branchId, string? customerType, string? searchText);
        /// <summary>UI-2 (D-66 f2): suất đặt / giao / nhận / phục vụ theo ngày ăn + cơ cấu loại suất; mặc định 30 ngày tới hôm nay.</summary>
        public Task<IResult<CustomerTrendModel>> GetTrend(Guid id, DateTime? from, DateTime? to);
        /// <summary>UI-5 (D-63 r): chi nhánh (loại BRANCH), NV phụ trách đang hoạt động, danh mục, cờ chi nhánh bắt buộc – cho form khách hàng.</summary>
        public Task<IResult<CustomerFormOptionsModel>> GetFormOptions();
    }
}
