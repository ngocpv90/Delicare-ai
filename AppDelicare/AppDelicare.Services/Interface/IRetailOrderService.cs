using AppDelicare.Data.Interface;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;

namespace AppDelicare.Services.Interface
{
    /// <summary>
    /// RETAIL-1: đơn khách lẻ – màn nội bộ "Bán lẻ › Đơn khách lẻ" (#/sal-retail-order). Không có CRUD chung:
    /// đơn chỉ tạo qua trang web / Create, đổi trạng thái qua Transition (máy trạng thái), sửa qua UpdateLines.
    /// </summary>
    public interface IRetailOrderService
    {
        Task<IResult<RetailOrderListModel>> GetList(RetailOrderListRequest request);
        Task<IResult<RetailOrderDetailModel>> GetDetail(Guid id);
        Task<IResult<RetailPrepSummaryModel>> GetPrepSummary(string? date);
        Task<IResult<RetailCreatedModel>> Create(RetailOrderCreateRequest request);
        Task<IResult<bool>> UpdateLines(RetailOrderUpdateLinesRequest request);
        Task<IResult<bool>> Transition(RetailOrderTransitionRequest request);
        /// <summary>RETAIL-2: gán / bỏ chuyến giao cho đơn.</summary>
        Task<IResult<bool>> AssignRoute(RetailOrderAssignRouteRequest request);
        Task<IResult<FileContentModel>> Export(RetailOrderListRequest request);
    }
}
