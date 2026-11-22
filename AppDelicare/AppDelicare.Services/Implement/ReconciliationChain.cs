using AppDelicare.Data.Connection;
using AppDelicare.Model.Helper;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UAT 2026-11-22 (GAP-DLV-05 làm tiếp): suất <b>sản xuất</b> / <b>đóng gói</b> của 1 đơn cho chuỗi đối soát Đặt → SX → Đóng → Giao → Nhận.
    /// <para>Liên kết có sẵn: <c>ProductionPlanLine.OrderLineId</c> (dòng kế hoạch theo dòng đơn × món), <c>ProductionOrder</c> theo kế hoạch × món,
    /// <c>PackingOrder.OrderId</c> + mẻ (món).</para>
    /// <list type="bullet">
    /// <item>Mẻ nấu chung nhiều đơn → phần của đơn = tổng suất đạt của mẻ (món, kế hoạch) × suất đơn trên kế hoạch ÷ tổng suất kế hoạch món đó, tối đa bằng suất đơn.</item>
    /// <item>Đóng gói: Σ suất đạt của lệnh đóng gói gắn đơn theo món (chia cho các dòng đơn cùng món theo suất kế hoạch) + mẻ đóng gói thẳng
    /// (<c>ProductionBatch/Pack</c>, mẻ Đã đóng gói không có lệnh đóng gói) chia theo tỷ lệ như sản xuất; tối đa bằng suất đơn.</item>
    /// <item>1 suất = đủ các món của loại suất → suất của dòng đơn = món ít nhất; suất của đơn = Σ các dòng đơn.</item>
    /// <item>Đơn chưa lên kế hoạch sản xuất → để trống (null).</item>
    /// </list>
    /// Mẻ / lệnh đóng gói đã hủy không tính. Suất đạt của mẻ: <c>AcceptedQty</c> (sau QC), chưa có thì suất nấu thực tế.
    /// </summary>
    public static class ReconciliationChain
    {
        public static async Task<(decimal? Produced, decimal? Packed)> ComputeAsync(DatabaseContext db, Guid orderId, CancellationToken ct = default)
        {
            var lines = await (from l in db.ProductionPlanLine.AsNoTracking()
                               join ol in db.OrderLine.AsNoTracking() on l.OrderLineId equals ol.Id
                               where !l.IsDeleted && !ol.IsDeleted && ol.OrderId == orderId
                               select new { OrderLineId = ol.Id, l.ProductionPlanId, l.DishId, l.PlannedQuantity }).ToListAsync(ct);
            if (lines.Count == 0)
            {
                return (null, null);
            }
            var planIds = lines.Select(x => x.ProductionPlanId).Distinct().ToList();
            var dishIds = lines.Select(x => x.DishId).Distinct().ToList();

            var planTotals = (await db.ProductionPlanLine.AsNoTracking()
                    .Where(l => !l.IsDeleted && planIds.Contains(l.ProductionPlanId) && dishIds.Contains(l.DishId))
                    .Select(l => new { l.ProductionPlanId, l.DishId, l.PlannedQuantity }).ToListAsync(ct))
                .GroupBy(x => (x.ProductionPlanId, x.DishId)).ToDictionary(g => g.Key, g => g.Sum(x => x.PlannedQuantity));

            var batches = await (from b in db.ProductionBatch.AsNoTracking()
                                 join po in db.ProductionOrder.AsNoTracking() on b.ProductionOrderId equals po.Id
                                 where !b.IsDeleted && !po.IsDeleted && planIds.Contains(po.ProductionPlanId) && dishIds.Contains(po.DishId)
                                     && b.Status != ProductionBatchStatus.Cancelled && b.Status != ProductionBatchStatus.Failed
                                 select new
                                 {
                                     b.Id, po.ProductionPlanId, po.DishId, b.Status,
                                     Qty = b.AcceptedQty ?? b.ActualQuantity ?? b.ActualQty ?? 0m,
                                     HasPacking = db.PackingOrder.Any(p => !p.IsDeleted && p.ProductionBatchId == b.Id && p.Status != PackingOrderStatus.Cancelled),
                                 }).ToListAsync(ct);
            var produced = batches.GroupBy(x => (x.ProductionPlanId, x.DishId)).ToDictionary(g => g.Key, g => g.Sum(x => x.Qty));
            var packedDirect = batches.Where(x => x.Status == ProductionBatchStatus.Packed && !x.HasPacking)
                .GroupBy(x => (x.ProductionPlanId, x.DishId)).ToDictionary(g => g.Key, g => g.Sum(x => x.Qty));

            var packed = (await (from p in db.PackingOrder.AsNoTracking()
                                 join b in db.ProductionBatch.AsNoTracking() on p.ProductionBatchId equals b.Id
                                 join r in db.PackingResult.AsNoTracking() on p.Id equals r.PackingOrderId
                                 where !p.IsDeleted && !r.IsDeleted && p.OrderId == orderId && p.Status != PackingOrderStatus.Cancelled
                                 select new { b.DishId, Qty = r.AcceptedQty ?? (r.PackedQuantity - r.RejectedQuantity) }).ToListAsync(ct))
                .GroupBy(x => x.DishId).ToDictionary(g => g.Key, g => g.Sum(x => x.Qty));
            var orderDishTotals = lines.GroupBy(x => x.DishId).ToDictionary(g => g.Key, g => g.Sum(x => x.PlannedQuantity));

            decimal producedSum = 0m, packedSum = 0m;
            foreach (var orderLine in lines.GroupBy(x => x.OrderLineId))
            {
                decimal? lineProduced = null, linePacked = null;
                foreach (var dish in orderLine.GroupBy(x => (x.ProductionPlanId, x.DishId)))
                {
                    decimal need = dish.Sum(x => x.PlannedQuantity);
                    decimal planTotal = planTotals.GetValueOrDefault(dish.Key);
                    decimal made = planTotal > 0 ? produced.GetValueOrDefault(dish.Key) * need / planTotal : 0m;
                    decimal orderDishTotal = orderDishTotals.GetValueOrDefault(dish.Key.DishId);
                    decimal pack = (orderDishTotal > 0 ? packed.GetValueOrDefault(dish.Key.DishId) * need / orderDishTotal : 0m)
                        + (planTotal > 0 ? packedDirect.GetValueOrDefault(dish.Key) * need / planTotal : 0m);
                    lineProduced = Math.Min(lineProduced ?? decimal.MaxValue, Math.Min(made, need));
                    linePacked = Math.Min(linePacked ?? decimal.MaxValue, Math.Min(pack, need));
                }
                producedSum += Math.Max(0m, lineProduced ?? 0m);
                packedSum += Math.Max(0m, linePacked ?? 0m);
            }
            return (Math.Round(producedSum, 2, MidpointRounding.AwayFromZero), Math.Round(packedSum, 2, MidpointRounding.AwayFromZero));
        }
    }
}
