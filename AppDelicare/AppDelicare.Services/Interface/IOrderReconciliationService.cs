using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IOrderReconciliationService
    {
        /// <summary>ACC-3 (D-131 g): yêu cầu mở lại đối soát Đã chốt (lý do bắt buộc) → duyệt RECON_REOPEN (COO).</summary>
        public Task<IResult<string>> RequestReopen(Guid id, string? reason);
        public Task<string?> ApplyReopenAsync(Guid id, string? proposalJson, string? note);
        public Task<string?> DescribeReopenAsync(Guid id);
        public Task<IResults<OrderReconciliationViewModel>> GetAll(OrderReconciliationSearchRequest request);
        /// <summary>BIZ-2.9: đơn vừa Đã giao → tự tạo đối soát (suất phục vụ = suất khách nhận). Trả câu thông báo (null = không tạo).</summary>
        public Task<string?> AutoCreateAsync(Guid orderId);
        /// <summary>BIZ-2.9: tự chốt đối soát Khớp, không phản ánh mở, quá ReconciliationAutoCloseHours. Trả số bản đã chốt.</summary>
        public Task<int> AutoCloseDueAsync();
        public Task<IResult<bool>> Save(OrderReconciliationRequest request, string typeSave);
        public Task<IResults<OrderReconciliation>> GetAllData();
        public Task<IResult<OrderReconciliation>> GetById(Guid id);

        // T19 (Pilot P8) – đối soát suất thực tế phục vụ.
        public Task<IResult<OrderReconciliationDetailModel>> Reconcile(OrderReconcileRequest request);
        public Task<IResult<OrderReconciliationDetailModel>> Close(ReconciliationCloseRequest request);
        public Task<IResult<OrderReconciliationDetailModel>> GetDetail(Guid id);
        public Task<IResult<OrderReconciliationDetailModel>> GetByOrder(Guid orderId);
        public Task<IResults<ReconciliationPendingModel>> GetPendingOrders(ReconciliationPendingRequest request);
        /// <summary>UI-6.8 (D-86 b, c): bảng xác nhận ngay trên trang + xác nhận / chốt theo lô + bot "Đã phục vụ đủ".</summary>
        public Task<IResult<AppDelicare.Model.Model.ReconBoardModel>> GetBoard(AppDelicare.Model.Model.ReconBoardRequest request);
        public Task<IResult<AppDelicare.Model.Model.ReconBatchResultModel>> ReconcileMany(AppDelicare.Model.Model.ReconcileManyRequest request);
        public Task<IResult<AppDelicare.Model.Model.ReconBatchResultModel>> CloseMany(AppDelicare.Model.Model.ReconCloseManyRequest request);
        public Task<IResult<OrderReconciliationDetailModel>> ConfirmServedFull(Guid orderId);
        /// <summary>BE3.1 (Catalog 100): danh sách đối soát + biểu đồ Đặt – Giao – Nhận theo ngày ăn.</summary>
        public Task<IResult<ReconciliationListModel>> GetList(ReconciliationListRequest request);
    }
}
