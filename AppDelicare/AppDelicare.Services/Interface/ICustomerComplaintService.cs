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

        // T19 (Pilot P8) – phản ánh PA- cơ bản: tạo / giao xử lý / đã xử lý / đóng / từ chối / ghi chú.
        public Task<IResult<CustomerComplaintDetailModel>> Create(ComplaintCreateRequest request);
        public Task<IResult<CustomerComplaintDetailModel>> Assign(ComplaintActionRequest request);
        public Task<IResult<CustomerComplaintDetailModel>> Resolve(ComplaintActionRequest request);
        public Task<IResult<CustomerComplaintDetailModel>> Close(ComplaintActionRequest request);
        public Task<IResult<CustomerComplaintDetailModel>> Reject(ComplaintActionRequest request);
        public Task<IResult<CustomerComplaintDetailModel>> AddComment(ComplaintActionRequest request);
        public Task<IResult<CustomerComplaintDetailModel>> GetDetail(Guid id);
    }
}
