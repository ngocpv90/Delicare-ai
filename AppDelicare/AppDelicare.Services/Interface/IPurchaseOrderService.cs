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
    public interface IPurchaseOrderService
    {
        public Task<IResults<PurchaseOrderViewModel>> GetAll(PurchaseOrderSearchRequest request);
        public Task<IResult<bool>> Save(PurchaseOrderRequest request, string typeSave);
        public IResults<PurchaseOrder> GetAllData();
        public Task<IResult<PurchaseOrder>> GetById(Guid id);
        /// <summary>T15: chi tiết PO (dòng + đã nhận / đang chờ / còn lại, phiếu nhập, yêu cầu duyệt, thao tác được phép).</summary>
        public Task<IResult<PurchaseOrderDetailModel>> GetDetail(Guid id);
        /// <summary>T15: sửa toàn bộ dòng PO Nháp, tính lại tổng tiền.</summary>
        public Task<IResult<PurchaseOrderDetailModel>> UpdateLines(PurchaseOrderUpdateLinesRequest request);
        /// <summary>T15: Nháp → Chờ duyệt (ApprovalRequest PURCHASE_ORDER) hoặc tự duyệt dưới ngưỡng cấu hình.</summary>
        public Task<IResult<PurchaseOrderDetailModel>> Submit(PurchaseActionRequest request);
        /// <summary>T15: duyệt / từ chối qua engine T12.</summary>
        public Task<IResult<bool>> Decide(PurchaseActionRequest request, bool approve);
        public Task<IResult<PurchaseOrderDetailModel>> Send(PurchaseActionRequest request);
        public Task<IResult<PurchaseOrderDetailModel>> Cancel(PurchaseCancelRequest request);
        public Task<IResult<PurchaseOrderDetailModel>> Close(PurchaseActionRequest request);
        /// <summary>T15: in Đơn đặt hàng NCC (.xlsx).</summary>
        public Task<IResult<byte[]>> ExportOrder(Guid id);
        /// <summary>T15 (handler duyệt): nội dung được duyệt (hash), tiêu đề hộp duyệt, thực thi quyết định (trong transaction của engine).</summary>
        public Task<string?> BuildApprovalStateAsync(Guid id);
        public Task<string?> DescribeAsync(Guid id);
        /// <summary>T15.1: mục quyền duyệt của PO – scm-po-director nếu tổng ≥ ngưỡng GĐ, còn lại scm-po.</summary>
        public Task<string> ApprovalScreenIdAsync(Guid id);
        /// <summary>BE2.3: các cấp duyệt của PO theo ngưỡng (D-11 mặc định 1 cấp; <c>PurchaseOrderDirectorTwoLevel</c> = scm-po → scm-po-director).</summary>
        public Task<IReadOnlyList<string>> ApprovalStepScreenIdsAsync(Guid id);
        public Task<string?> ApplyDecisionAsync(Guid id, bool approved, string? note);
        /// <summary>T15: cập nhật trạng thái nhận hàng sau khi ghi sổ phiếu nhập (trong transaction của GoodsReceipt/Post).</summary>
        public Task RefreshReceiptStatusAsync(Guid id);
    }
}
