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
    public interface ICustomerComplaintService
    {
        public Task<IResults<CustomerComplaintViewModel>> GetAll(CustomerComplaintSearchRequest request);
        public Task<IResult<bool>> Save(CustomerComplaintRequest request, string typeSave);
        public IResults<CustomerComplaint> GetAllData();
        public Task<IResult<CustomerComplaint>> GetById(Guid id);
        /// <summary>BE2.1 (spec 10.5 GET complaints): danh sách đủ trường + isOverdue + statusCounts.</summary>
        public Task<IResult<AppDelicare.Model.Model.CustomerComplaintListModel>> GetList(AppDelicare.Model.Request.Custom.ComplaintListRequest request);

        // T19 (Pilot P8) – phản ánh PA- cơ bản: tạo / giao xử lý / đã xử lý / đóng / từ chối / ghi chú.
        public Task<IResult<CustomerComplaintDetailModel>> Create(ComplaintCreateRequest request);
        public Task<IResult<CustomerComplaintDetailModel>> Assign(ComplaintActionRequest request);
        /// <summary>BIZ-3.1: tạo đơn bù (Đã chốt, giá 0 khi lỗi Delicare) từ phản ánh thiếu suất / hỏng / không đạt nhiệt độ.</summary>
        public Task<IResult<AppDelicare.Model.Model.CustomerComplaintDetailModel>> CreateCompensationOrder(AppDelicare.Model.Request.Custom.ComplaintCompensationRequest request);
        public Task<IResult<CustomerComplaintDetailModel>> Resolve(ComplaintActionRequest request);
        public Task<IResult<CustomerComplaintDetailModel>> Close(ComplaintActionRequest request);
        public Task<IResult<CustomerComplaintDetailModel>> Reject(ComplaintActionRequest request);
        public Task<IResult<CustomerComplaintDetailModel>> AddComment(ComplaintActionRequest request);
        public Task<IResult<CustomerComplaintDetailModel>> GetDetail(Guid id);
    }
}
