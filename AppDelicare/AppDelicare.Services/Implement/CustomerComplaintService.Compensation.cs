using AppDelicare.Data.Repositories;
using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// BIZ-3.1 (D-77 – A6, C14): <c>CustomerComplaint/CreateCompensationOrder</c> – từ phản ánh loại thiếu suất / hỏng / không đạt nhiệt độ (kể cả phản ánh tự tạo từ POD)
    /// có đơn gốc → đơn bù cùng ngày ăn, cùng điểm giao, loại suất / lựa chọn theo đơn gốc (chia theo tỷ lệ dòng), số suất = số khách từ chối ở POD (sửa được),
    /// <b>Đã chốt ngay</b> (tự duyệt – lý do = phản ánh), <b>giá 0</b> (lỗi Delicare) trừ khi chọn "Khách trả tiền" (giá dòng đơn gốc).
    /// Liên kết bằng Note <c>[DON_BU:&lt;số đơn gốc&gt;|&lt;số PA&gt;]</c> (không có cột liên kết). Phản ánh Mới → Đang xử lý + lịch sử; bot 🔴 Kế hoạch + điều phối.
    /// Mỗi phản ánh 1 đơn bù. Bảng kê thanh toán bỏ đơn bù giá 0; giá cố định lúc chốt không áp cho đơn bù.
    /// </summary>
    public partial class CustomerComplaintService
    {
        private static readonly HashSet<string> CompensableCategories = new(StringComparer.OrdinalIgnoreCase) { "SHORTAGE", "DAMAGED", "TEMPERATURE" };

        public async Task<IResult<CustomerComplaintDetailModel>> CreateCompensationOrder(ComplaintCompensationRequest request)
        {
            string? orderNumber = null;
            var result = await RunAsync(request.Id, "tạo đơn bù", [ComplaintStatus.Open, ComplaintStatus.InProgress], async (complaint, actor, at) =>
            {
                var db = _unitOfWork.DbContext;
                if (!CompensableCategories.Contains(complaint.CategoryCode ?? string.Empty))
                {
                    return ConstantHelper.Message.CompensationCategoryInvalid;
                }
                var source = complaint.OrderId is { } oid ? await db.Order.AsNoTracking().FirstOrDefaultAsync(o => o.Id == oid && !o.IsDeleted) : null;
                if (source == null || source.Status == OrderStatus.Cancelled)
                {
                    return ConstantHelper.Message.CompensationNoSourceOrder;
                }
                string sourceNo = source.OrderNumber ?? source.OrderNo ?? string.Empty;
                string tag = $"[DON_BU:{sourceNo}|{complaint.ComplaintNo}]";
                var existing = await db.Order.AsNoTracking().FirstOrDefaultAsync(o => !o.IsDeleted && o.Status != OrderStatus.Cancelled && o.Note != null && o.Note.Contains(tag));
                if (existing != null)
                {
                    return string.Format(ConstantHelper.Message.CompensationExists, existing.OrderNumber ?? existing.OrderNo);
                }
                decimal quantity = request.Quantity ?? 0m;
                if (request.Quantity == null)
                {
                    var deliveryIds = await db.DeliveryOrder.AsNoTracking().Where(d => d.OrderId == source.Id && !d.IsDeleted).Select(d => d.Id).ToListAsync();
                    quantity = await db.ProofOfDelivery.AsNoTracking().Where(p => deliveryIds.Contains(p.DeliveryOrderId) && !p.IsDeleted)
                        .SumAsync(p => p.RejectedQuantity ?? p.RejectedQty ?? 0m);
                }
                if (quantity <= 0)
                {
                    return ConstantHelper.Message.CompensationQuantityRequired;
                }
                var lines = await db.OrderLine.AsNoTracking().Where(l => l.OrderId == source.Id && !l.IsDeleted && l.Quantity > 0).OrderByDescending(l => l.Quantity).ToListAsync();
                if (lines.Count == 0)
                {
                    return ConstantHelper.Message.OrderNeedsLine;
                }
                decimal total = lines.Sum(l => l.Quantity), left = Math.Ceiling(quantity);
                string number = await _documentNumberService.NextAsync(DocumentKind.Order, _clock.Today);
                var order = new Order
                {
                    Id = Guid.NewGuid(), OrderNumber = number, OrderNo = number, CustomerId = source.CustomerId, ContractId = source.ContractId, DeliveryLocationId = source.DeliveryLocationId,
                    OrderDate = at, MealDate = source.MealDate, DeliveryDate = source.DeliveryDate, Status = OrderStatus.Locked,
                    Note = $"{tag} Đơn bù phản ánh {complaint.ComplaintNo} ({(DeliveryCode.FaultReasons.GetValueOrDefault(complaint.CategoryCode ?? string.Empty) ?? complaint.CategoryCode)}) cho đơn {sourceNo} – hệ thống tự duyệt"
                        + (request.ChargeCustomer ? " – khách trả tiền" : " – lỗi Delicare, giá 0") + (string.IsNullOrWhiteSpace(request.Note) ? string.Empty : " – " + request.Note.Trim()),
                };
                decimal amount = 0m, qtyTotal = 0m;
                for (int i = 0; i < lines.Count && left > 0; i++)
                {
                    var l = lines[i];
                    decimal q = i == lines.Count - 1 ? left : Math.Min(left, Math.Round(Math.Ceiling(quantity) * l.Quantity / total, 0, MidpointRounding.AwayFromZero));
                    if (q <= 0)
                    {
                        continue;
                    }
                    left -= q;
                    decimal price = request.ChargeCustomer ? l.UnitPrice ?? 0m : 0m;
                    decimal lineAmount = Math.Round(q * price, 2);
                    _unitOfWork.OrderLineRepository.Add(new OrderLine
                    {
                        Id = Guid.NewGuid(), OrderId = order.Id, MenuId = l.MenuId, MealType = l.MealType, ShiftId = l.ShiftId, ContractMealId = l.ContractMealId, MenuOptionNo = l.MenuOptionNo,
                        Quantity = q, UnitPrice = price, LineAmount = lineAmount, Amount = lineAmount, Note = "Đơn bù " + complaint.ComplaintNo,
                    });
                    amount += lineAmount;
                    qtyTotal += q;
                }
                order.TotalQuantity = qtyTotal;
                order.TotalAmount = amount;
                _unitOfWork.OrderRepository.Add(order);
                orderNumber = number;
                if (complaint.ComplaintStatus == ComplaintStatus.Open)
                {
                    complaint.AssignedTo ??= actor.UserId;
                    Transition(complaint, actor, at, DeliveryCode.ComplaintAssigned, ComplaintStatus.InProgress, string.Format(ConstantHelper.Message.CompensationCreated, number, DeliveryRules.Num(qtyTotal)));
                }
                else
                {
                    DeliveryRules.AddHistory(_unitOfWork, actor, at, complaint.Id, DeliveryCode.ComplaintComment, complaint.ComplaintStatus, complaint.ComplaintStatus,
                        string.Format(ConstantHelper.Message.CompensationCreated, number, DeliveryRules.Num(qtyTotal)));
                }
                return null;
            });
            if (result.ResponseCode == 200 && orderNumber != null)
            {
                result.ResponseMessage = string.Format(ConstantHelper.Message.CompensationCreated, orderNumber, string.Empty).TrimEnd(' ', '–') + " – đã chốt";
                // D-78 (1): kế hoạch ngày đã duyệt / phát hành → tự sinh lệnh + mẻ bổ sung ưu tiên (bot 🔴 báo bếp + điều phối trong đó).
                Guid newOrderId = await _unitOfWork.DbContext.Order.AsNoTracking().Where(o => o.OrderNumber == orderNumber).Select(o => o.Id).FirstOrDefaultAsync();
                var sup = await _services.GetRequiredService<IProductionPlanService>().SupplementForOrderAsync(newOrderId, "phản ánh " + result.Data?.Complaint?.ComplaintNo);
                result.ResponseMessage += sup.ProductionOrderCount > 0 ? " – " + string.Format(ConstantHelper.Message.SupplementCreated, sup.ProductionOrderCount)
                    : sup.ListReason.Count != 0 ? " – " + string.Join("; ", sup.ListReason) : " – cần sản xuất bổ sung";
            }
            return result;
        }
    }
}
