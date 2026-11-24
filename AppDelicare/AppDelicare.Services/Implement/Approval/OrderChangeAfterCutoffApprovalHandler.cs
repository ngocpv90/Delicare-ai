using AppDelicare.Data.Interface;
using AppDelicare.Model.Helper;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement.Approval
{
    /// <summary>
    /// BE2.2 – thêm / sửa đơn sau giờ chốt (BusinessType ORDER_CHANGE_AFTER_CUTOFF, BUSINESS_RULES §2 "chỉ ghi khi đã được duyệt").
    /// Quyền duyệt = mục menu <c>#/wf-inbox?type=crm-order</c> "Phê duyệt › Đơn đặt suất ăn" (dùng chung với lệch suất ORDER_DEVIATION – không thêm mục menu, PROPOSED D-51).
    /// Engine 1 cấp T12; BE2.3 có thể chuyển nhiều cấp (handler không đổi). Đề xuất (số suất mới / dòng mới) lưu trong PayloadJson.proposal.
    /// </summary>
    public class OrderChangeAfterCutoffApprovalHandler(IUnitOfWork unitOfWork, IOrderService orderService) : IApprovalHandler
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IOrderService _orderService = orderService;

        public string BusinessType => OrderStatus.ChangeAfterCutoffBusinessType;

        public string ScreenId => "crm-order";

        public Task<string?> BuildPayloadAsync(Guid businessId) => _orderService.BuildChangeAfterCutoffStateAsync(businessId);

        public async Task<string?> DescribeAsync(Guid businessId)
        {
            var row = await (from o in _unitOfWork.OrderRepository.GetAll()
                             join c in _unitOfWork.CustomerRepository.GetAll() on o.CustomerId equals c.Id
                             where o.Id == businessId
                             select new { Number = o.OrderNumber ?? o.OrderNo, o.MealDate, o.Status, c.Name }).FirstOrDefaultAsync();
            return row == null ? null
                : $"{(row.Status == OrderStatus.Draft ? "Thêm" : "Sửa")} đơn sau giờ chốt {row.Number} – {row.Name} – ngày ăn {row.MealDate:dd/MM/yyyy}";
        }

        public Task<string?> ApproveAsync(Guid businessId, Guid actorUserId, string? note) => _orderService.ApplyChangeAfterCutoffAsync(businessId, null, note);

        public Task<string?> ApproveAsync(Guid businessId, Guid actorUserId, string? note, string? proposalJson)
            => _orderService.ApplyChangeAfterCutoffAsync(businessId, proposalJson, note);

        public Task<string?> RejectAsync(Guid businessId, Guid actorUserId, string note) => _orderService.RejectChangeAfterCutoffAsync(businessId, note);

        /// <summary>APR-1.2 (D-81 e): khuôn tóm tắt chung – ApprovalSummaries.</summary>
        public Task<AppDelicare.Model.Model.ApprovalSummaryModel?> GetSummaryAsync(Guid businessId, ApprovalSummaryContext ctx) => ApprovalSummaries.OrderChangeAsync(businessId, ctx, true);
    }
}
