using AppDelicare.Data.Interface;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Services.Interface;

namespace AppDelicare.Services.Jobs
{
    // T23 (D-48, AGENTS.md §2, spec 6.1 / 6.6 / 7.1 / 7.5 / 11.2): mỗi job = 1 lớp mỏng gọi Service cũ – cùng code với nút bấm tay.
    // Giờ mặc định ghi ở DefaultAt; đổi giờ / tắt bằng cấu hình Jobs:<Code> (PROPOSED D-59).

    /// <summary>20:00 – khóa đơn ngày mai (Confirmed → Locked). Đơn chưa xác nhận / chờ duyệt được liệt kê, không khóa.</summary>
    public sealed class LockOrdersJob(IOrderService orderService, IUnitOfWork unitOfWork, INotificationService notifications, IOrderReconciliationService reconciliationService) : IScheduledJob
    {
        public string Code => "LockOrders";
        public string Name => "Khóa đơn ngày mai";
        public string Description => "Khóa mọi đơn Đã xác nhận của ngày ăn D+1 (= nút Order/LockByMealDate). Đơn chưa xác nhận / chờ duyệt: chỉ liệt kê.";
        public string DefaultAt => "20:00";

        public async Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            DateTime mealDate = now.Date.AddDays(1);
            // BIZ-2.9: tự chốt đối soát Khớp quá hạn (cùng hàm khi mở màn đối soát).
            await reconciliationService.AutoCloseDueAsync();
            var result = await orderService.LockByMealDate(new OrderLockByMealDateRequest { MealDate = mealDate });
            if (result.ResponseCode == 200 && result.Data is { } locked)
            {
                // BOT-1.7: PLAN_READY → kế hoạch vận hành, điều phối (quyền Thêm) – đơn đã khóa, việc tiếp theo là kế hoạch / lịch xe.
                await BusinessNotifier.ToMenusAsync(notifications, unitOfWork.DbContext, NotificationCatalog.PlanReady, ["#/prod-plan", "#/log-route"],
                    new
                    {
                        title = $"Đơn ngày {mealDate:dd/MM} đã khóa: {locked.LockedCount} đơn",
                        lines = new[]
                        {
                            locked.ListNotConfirmedNumber.Count > 0 ? "Chưa xác nhận: " + string.Join(", ", locked.ListNotConfirmedNumber.Take(10)) : null,
                            locked.ListPendingApprovalNumber.Count > 0 ? "Chờ duyệt: " + string.Join(", ", locked.ListPendingApprovalNumber.Take(10)) : null,
                        }.Where(x => x != null).ToList(),
                    },
                    $"{NotificationCatalog.PlanReady}:{mealDate:yyyyMMdd}", requireAdd: true);
            }
            return JobOutcome.From(result, mealDate, r => r.LockedCount,
                r => $"Khóa {r.LockedCount} đơn ngày {mealDate:dd/MM/yyyy}"
                     + (r.ListNotConfirmedNumber.Count > 0 ? $"; chưa xác nhận: {string.Join(", ", r.ListNotConfirmedNumber)}" : string.Empty)
                     + (r.ListPendingApprovalNumber.Count > 0 ? $"; chờ duyệt: {string.Join(", ", r.ListPendingApprovalNumber)}" : string.Empty));
        }
    }

    /// <summary>
    /// 20:30 – tính nhu cầu vật tư ngày mai từ đơn đã khóa, thiếu hụt → đề nghị mua Nháp + gợi ý NCC.
    /// Gộp luôn PurchaseProposalJob 21:00 của AGENTS.md vì <c>MaterialRequirement/Calculate</c> đã tạo đề nghị mua (T14).
    /// </summary>
    public sealed class MrpJob(IMaterialRequirementService materialRequirementService, IUnitOfWork unitOfWork, INotificationService notifications) : IScheduledJob
    {
        public string Code => "Mrp";
        public string Name => "Tính nhu cầu vật tư + đề nghị mua";
        public string Description => "Tính MRP cho ngày sản xuất D+1 (= nút MaterialRequirement/Calculate, toàn công ty): kế hoạch KH-, thiếu hụt, đề nghị mua Nháp + NCC gợi ý.";
        public string DefaultAt => "20:30";

        public async Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            DateTime productionDate = now.Date.AddDays(1);
            var result = await materialRequirementService.Calculate(new MaterialRequirementCalculateRequest
            {
                ProductionDate = productionDate,
                Note = "Job tự động",
            });
            if (result.ResponseCode == 200 && result.Data is { ShortageCount: > 0 } mrp)
            {
                // BOT-1.7: SHORTAGE_MRP → mua hàng, thủ kho (quyền Thêm trên nhu cầu vật tư / đề nghị mua).
                await BusinessNotifier.ToMenusAsync(notifications, unitOfWork.DbContext, NotificationCatalog.ShortageMrp, ["#/scm-requirement", "#/scm-pr"],
                    new
                    {
                        title = $"Kế hoạch {mrp.PlanNumber} ngày {productionDate:dd/MM}: thiếu {mrp.ShortageCount} nguyên liệu",
                        detail = mrp.PurchaseRequest is { } pr ? $"Đã lập đề nghị mua {pr.RequestNumber} (Nháp)" : null,
                        lines = mrp.ListItem.Where(i => i.ShortageQuantity > 0).OrderByDescending(i => i.ShortageQuantity).Take(10)
                            .Select(i => $"⚠️ {i.IngredientName}: thiếu {i.ShortageQuantity:0.##} {i.UnitName}").ToList(),
                    },
                    $"{NotificationCatalog.ShortageMrp}:{mrp.ProductionPlanId:N}:{productionDate:yyyyMMdd}", requireAdd: true);
            }
            return JobOutcome.From(result, productionDate, r => r.OrderCount,
                r => $"Kế hoạch {r.PlanNumber}: {r.OrderCount} đơn, {r.TotalMeals:0.##} suất, {r.DishCount} món, thiếu {r.ShortageCount} nguyên liệu");
        }
    }

    /// <summary>00:15 – lô quá hạn → Hết hạn + ghi hao hụt toàn bộ tồn (spec 7.5 LotExpiryJob).</summary>
    public sealed class LotExpiryJob(IMaterialLotService materialLotService) : IScheduledJob
    {
        public string Code => "LotExpiry";
        public string Name => "Xử lý lô hết hạn";
        public string Description => "Lô Khả dụng có hạn < hôm nay → Hết hạn + ghi hao hụt tồn (= nút MaterialLot/ProcessExpired). Lô tạm giữ bỏ qua.";
        public string DefaultAt => "00:15";

        public async Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            var result = await materialLotService.ProcessExpired(new MaterialLotExpireRequest { Preview = false, Note = "Job tự động" });
            return JobOutcome.From(result, now.Date, r => r.ExpiredLotCount,
                r => $"{r.ExpiredLotCount} lô hết hạn" + (r.TransactionNumber is { Length: > 0 } t ? $" (phiếu {t})" : string.Empty)
                     + (r.ListExpiring.Count > 0 ? $"; {r.ListExpiring.Count} lô sắp hết hạn" : string.Empty));
        }
    }

    /// <summary>00:30 – hợp đồng Hiệu lực quá ngày hết hiệu lực → Hết hạn (spec 6.1 ContractExpiryJob, Catalog 81).</summary>
    public sealed class ContractExpiryJob(IContractService contractService) : IScheduledJob
    {
        public string Code => "ContractExpiry";
        public string Name => "Hợp đồng hết hạn";
        public string Description => "Hợp đồng Hiệu lực có ngày hết hiệu lực < hôm nay → Hết hạn (= nút Contract/ExpireOverdue).";
        public string DefaultAt => "00:30";

        public async Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            var result = await contractService.ExpireOverdue(new ContractExpireRequest { Preview = false });
            return JobOutcome.From(result, now.Date, r => r.ExpiredCount,
                r => r.ExpiredCount == 0 ? "Không có hợp đồng hết hạn" : $"{r.ExpiredCount} hợp đồng hết hạn: {string.Join(", ", r.ListContractNumber)}");
        }
    }

    /// <summary>
    /// 08:00 – chốt Vắng cho ca của <b>hôm qua</b> đã hết giờ mà không vào ca (spec 11.2 "job cuối ngày đánh Absent", D-55 j).
    /// Chạy sáng hôm sau để ca đêm (DEM_23H kết thúc sáng nay) cũng đã hết giờ.
    /// </summary>
    public sealed class MarkAbsentJob(IShiftAssignmentService shiftAssignmentService) : IScheduledJob
    {
        public string Code => "MarkAbsent";
        public string Name => "Chốt vắng ca hôm qua";
        public string Description => "Ca Kế hoạch / Xác nhận của hôm qua đã hết giờ mà không vào ca → Vắng (= nút ShiftAssignment/MarkAbsent, toàn công ty).";
        public string DefaultAt => "08:00";

        public async Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            DateTime workDate = now.Date.AddDays(-1);
            var result = await shiftAssignmentService.MarkAbsent(new ShiftMarkAbsentRequest { WorkDate = workDate });
            return JobOutcome.From(result, workDate, r => r.Marked, r => $"Đánh vắng {r.Marked} ca ngày {workDate:dd/MM/yyyy}");
        }
    }
}
