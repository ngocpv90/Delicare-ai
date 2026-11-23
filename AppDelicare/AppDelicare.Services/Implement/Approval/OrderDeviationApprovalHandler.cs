using AppDelicare.Data.Interface;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace AppDelicare.Services.Implement.Approval
{
    /// <summary>
    /// T13 – duyệt sửa số suất của đơn đã xác nhận lệch quá ngưỡng (BusinessType ORDER_DEVIATION, màn crm-order;
    /// quyền duyệt = mục menu <c>#/wf-inbox?type=crm-order</c> "Phê duyệt › Đơn hàng / lệch suất", D-31).
    /// Nội dung được duyệt = trạng thái đơn + số suất hiện tại từng dòng; đề xuất (số suất mới) lưu kèm trong PayloadJson.
    /// Duyệt → ghi số mới (đơn phải còn Confirmed); từ chối → không đổi gì (ghi chú vào đơn).
    /// </summary>
    public class OrderDeviationApprovalHandler(IUnitOfWork unitOfWork, IOrderService orderService) : IApprovalHandler
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IOrderService _orderService = orderService;

        public string BusinessType => OrderStatus.DeviationBusinessType;

        public string ScreenId => "crm-order";

        public Task<string?> BuildPayloadAsync(Guid businessId) => _orderService.BuildDeviationStateAsync(businessId);

        public async Task<string?> DescribeAsync(Guid businessId)
        {
            var row = await (from o in _unitOfWork.OrderRepository.GetAll()
                             join c in _unitOfWork.CustomerRepository.GetAll() on o.CustomerId equals c.Id
                             where o.Id == businessId
                             select new { Number = o.OrderNumber ?? o.OrderNo, o.MealDate, c.Name }).FirstOrDefaultAsync();
            return row == null ? null : $"Lệch suất đơn {row.Number} – {row.Name} – ngày ăn {row.MealDate:dd/MM/yyyy}";
        }

        public Task<string?> ApproveAsync(Guid businessId, Guid actorUserId, string? note)
            => Task.FromResult<string?>("Thiếu nội dung đề xuất số suất – không duyệt được");

        public async Task<string?> ApproveAsync(Guid businessId, Guid actorUserId, string? note, string? proposalJson)
        {
            var order = _unitOfWork.OrderRepository.GetSingle(o => o.Id == businessId && !o.IsDeleted);
            if (order == null || order.Status != OrderStatus.Confirmed)
            {
                return string.Format(ConstantHelper.Message.StatusActionInvalid, "duyệt lệch suất", "đơn", OrderStatus.Label(OrderStatus.Confirmed), OrderStatus.Label(order?.Status));
            }
            var changes = string.IsNullOrEmpty(proposalJson) ? null : JsonConvert.DeserializeObject<List<OrderLineQuantityRequest>>(proposalJson);
            if (changes == null || changes.Count == 0)
            {
                return await ApproveAsync(businessId, actorUserId, note);
            }
            if (!string.IsNullOrWhiteSpace(note))
            {
                order.Note = string.IsNullOrWhiteSpace(order.Note) ? $"Duyệt lệch suất: {note}" : $"{order.Note}\nDuyệt lệch suất: {note}";
                _unitOfWork.OrderRepository.Update(order);
            }
            return await _orderService.ApplyLinesAsync(businessId, changes);
        }

        public Task<string?> RejectAsync(Guid businessId, Guid actorUserId, string note)
        {
            var order = _unitOfWork.OrderRepository.GetSingle(o => o.Id == businessId && !o.IsDeleted);
            if (order != null)
            {
                order.Note = string.IsNullOrWhiteSpace(order.Note) ? $"Từ chối lệch suất: {note}" : $"{order.Note}\nTừ chối lệch suất: {note}";
                _unitOfWork.OrderRepository.Update(order);
            }
            return Task.FromResult<string?>(null);
        }
    }
}
