using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Interface;

namespace AppDelicare.Services.Implement.Agent
{
    /// <summary>AI-10 (D-133): công cụ đọc cho SALES, PROC, LOG, HR (bật ở đợt 2) – mỗi công cụ gọi đúng method Service của màn web tương ứng, quyền Xem màn đó.</summary>
    public static partial class AgentToolRegistry
    {
        private static IEnumerable<AgentToolDef> OpsReadTools() =>
        [
            new AgentToolDef
            {
                Name = "don_hang_ngay", ScreenId = "crm-order", Agents = ["VIVI", "SALES"],
                Description = "Đơn suất ăn của 1 ngày ăn (mặc định ngày mai): theo khách / điểm giao / loại suất, trạng thái (chưa chốt / đã chốt), tổng suất – như màn Đơn hàng theo ngày.",
                InputSchemaJson = Schema(("ngay", "string", "Ngày ăn yyyy-MM-dd (trống = ngày mai)")),
                RunAsync = async (ctx, a) => Data(await Svc<IOrderService>(ctx).GetDay(new OrderDayRequest { MealDate = Date(ctx, a, "ngay", 1) })),
            },
            new AgentToolDef
            {
                Name = "ke_hoach_mua", ScreenId = "scm-requirement", Agents = ["VIVI", "PROC"],
                Description = "Kế hoạch mua 7 ngày (UI-6.51): 5 chỉ số + nguyên liệu phải đặt hôm nay / quá hạn / chờ duyệt / chưa gửi NCC (theo ngày cần, số lượng, NCC) – như màn Kế hoạch mua.",
                InputSchemaJson = Schema(("tu_ngay", "string", "Từ ngày (trống = hôm nay)")),
                RunAsync = async (ctx, a) =>
                {
                    var b = Data(await Svc<IPurchasePlanService>(ctx).GetBoard(new PurchasePlanBoardRequest { From = Date(ctx, a, "tu_ngay") })) as PurchasePlanBoardModel;
                    return b == null ? null : new
                    {
                        b.Kpi, b.DueCount, b.LastRunAt, b.LastRunSummary,
                        ViecCanLam = b.ListRow.SelectMany(r => r.ListCell.Where(c => c.Status is "DUE_TODAY" or "LATE" or "PENDING" or "DRAFT" or "NEED_PICK" or "APPROVED")
                            .Select(c => new { r.IngredientCode, r.IngredientName, r.UnitName, c.Date, c.Quantity, c.StatusName, c.Sub })).Take(MaxRows(ctx)).ToList(),
                    };
                },
            },
            new AgentToolDef
            {
                Name = "chuyen_giao_ngay", ScreenId = "log-route", Agents = ["VIVI", "LOG"],
                Description = "Bảng điều phối giao hàng 1 ngày (mặc định hôm nay): chuyến, xe, tài xế, điểm giao, đơn chưa xếp chuyến – như màn Điều phối giao vận.",
                InputSchemaJson = Schema(("ngay", "string", "Ngày giao yyyy-MM-dd (trống = hôm nay)")),
                RunAsync = async (ctx, a) => Data(await Svc<IDeliveryRouteService>(ctx).GetDispatchBoard(Date(ctx, a, "ngay"))),
            },
            new AgentToolDef
            {
                Name = "cham_cong_ngay", ScreenId = "hr-attendance", Agents = ["VIVI", "HR"],
                Description = "Chấm công 1 ngày (mặc định hôm nay): theo ca – đã vào, chưa vào ca, đi muộn, về sớm, vắng – như màn Chấm công ngày.",
                InputSchemaJson = Schema(("ngay", "string", "Ngày yyyy-MM-dd (trống = hôm nay)")),
                RunAsync = async (ctx, a) => Data(await Svc<IAttendanceEventService>(ctx).GetDayBoard(Date(ctx, a, "ngay"), null, null)),
            },
        ];
    }
}
