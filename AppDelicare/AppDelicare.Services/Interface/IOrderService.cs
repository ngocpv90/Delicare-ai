using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IOrderService
    {
        public Task<IResults<OrderViewModel>> GetAll(OrderSearchRequest request);
        public Task<IResult<bool>> Save(OrderRequest request, string typeSave);
        public IResults<Order> GetAllData();
        public Task<IResult<Order>> GetById(Guid id);
        // <summary>T13 (spec 6.6): khung tạo đơn, tạo, sửa số suất, máy trạng thái, giờ chốt 20h D-1, lệch suất > 30%.</summary>
        public Task<IResult<AppDelicare.Model.Model.OrderTemplateModel>> GetNewTemplate(Guid customerId, DateTime mealDate);
        public Task<IResult<AppDelicare.Model.Model.OrderDetailModel>> GetDetail(Guid id);
        public Task<IResult<AppDelicare.Model.Model.OrderDetailModel>> Create(AppDelicare.Model.Request.Custom.OrderCreateRequest request);
        public Task<IResult<AppDelicare.Model.Model.OrderDetailModel>> UpdateLines(AppDelicare.Model.Request.Custom.OrderUpdateLinesRequest request);
        public Task<IResult<AppDelicare.Model.Model.OrderDetailModel>> Submit(AppDelicare.Model.Request.Custom.OrderActionRequest request);
        public Task<IResult<AppDelicare.Model.Model.OrderDetailModel>> Confirm(AppDelicare.Model.Request.Custom.OrderActionRequest request);
        public Task<IResult<AppDelicare.Model.Model.OrderDetailModel>> Lock(AppDelicare.Model.Request.Custom.OrderActionRequest request);
        public Task<IResult<AppDelicare.Model.Model.OrderDetailModel>> Cancel(AppDelicare.Model.Request.Custom.OrderCancelRequest request);
        public Task<IResult<AppDelicare.Model.Model.OrderLockSummaryModel>> LockByMealDate(AppDelicare.Model.Request.Custom.OrderLockByMealDateRequest request);
        /// <summary>Giờ chốt đơn của ngày ăn (20h ngày D-1, BusinessRules).</summary>
        public DateTime CutoffOf(DateTime mealDate);
        /// <summary>Nội bộ (duyệt ORDER_DEVIATION): ghi số suất mới, chạy trong transaction của người gọi; trả lỗi hoặc null.</summary>
        public Task<string?> ApplyLinesAsync(Guid orderId, IReadOnlyCollection<AppDelicare.Model.Request.Custom.OrderLineQuantityRequest> changes);
        /// <summary>Nội bộ: nội dung được duyệt của ORDER_DEVIATION (trạng thái + số suất từng dòng).</summary>
        public Task<string?> BuildDeviationStateAsync(Guid orderId);
    }
}
