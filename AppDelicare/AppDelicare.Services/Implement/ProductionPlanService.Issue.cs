using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// BIZ-2.5 (D-77 – C4): phát hành lệnh → tự tạo phiếu xuất kho <b>Nháp</b> cho mọi lệnh của ca (số = giữ chỗ, lô FEFO phân bổ sẵn khi tạo phiếu).
    /// Phiếu xuất chỉ gắn 1 lệnh SX (giữ chỗ trừ theo lệnh) → "phiếu xuất ca" = nhóm phiếu Nháp cùng thẻ <c>[XUAT_CA:&lt;số KH&gt;|&lt;mã ca&gt;]</c> trong Note
    /// (không thêm bảng / cột – GAP-BIZ-02); kho <b>Ghi sổ cả ca</b> một lần (<c>MaterialIssue/PostShift</c>), vẫn sửa số / lô từng phiếu được trước khi ghi.
    /// Kho xuất = kho có nhiều tồn khả dụng nhất cho nguyên liệu của lệnh. Lỗi không làm hỏng việc phát hành – ghi cảnh báo.
    /// </summary>
    public partial class ProductionPlanService
    {
        public static string ShiftIssueTag(string? planNumber, string? shiftCode) => $"[XUAT_CA:{planNumber}|{shiftCode}]";

        private async Task<List<string>> CreateShiftIssueDraftsAsync(string? planNumber, IReadOnlyCollection<(Guid OrderId, Guid ShiftId, string? ShiftCode, string? OrderNumber)> orders)
        {
            var warnings = new List<string>();
            if (!_rules.ProductionAutoDraftIssueOnRelease || orders.Count == 0)
            {
                return warnings;
            }
            var db = _unitOfWork.DbContext;
            var issues = _services.GetRequiredService<IMaterialIssueService>();
            var created = new Dictionary<string, int>();
            foreach (var order in orders)
            {
                try
                {
                    var reservations = await ReservationRules.Open(db).AsNoTracking().Where(r => r.ProductionOrderId == order.OrderId && r.ReservedQuantity > 0).ToListAsync();
                    if (reservations.Count == 0)
                    {
                        continue;
                    }
                    var ingredientIds = reservations.Select(r => r.IngredientId).Distinct().ToList();
                    var warehouseId = await db.InventoryBalance.AsNoTracking().Where(b => ingredientIds.Contains(b.IngredientId) && b.AvailableQuantity > 0)
                        .GroupBy(b => b.WarehouseId).OrderByDescending(g => g.Sum(b => b.AvailableQuantity)).Select(g => (Guid?)g.Key).FirstOrDefaultAsync();
                    if (warehouseId == null)
                    {
                        continue;
                    }
                    string tag = ShiftIssueTag(planNumber, order.ShiftCode);
                    var result = await issues.Create(new MaterialIssueCreateRequest
                    {
                        WarehouseId = warehouseId.Value, ProductionOrderId = order.OrderId, IssueDate = _clock.Now,
                        Note = $"{tag} Phiếu xuất ca {order.ShiftCode} – lệnh {order.OrderNumber} – hệ thống tạo nháp khi phát hành, kho kiểm rồi Ghi sổ cả ca",
                        ListLine = [.. reservations.GroupBy(r => (r.IngredientId, r.UnitId)).Select(g => new MaterialIssueCreateLineRequest { IngredientId = g.Key.IngredientId, UnitId = g.Key.UnitId, Quantity = g.Sum(r => r.ReservedQuantity) })],
                    });
                    if (result.ResponseCode != 200)
                    {
                        warnings.Add(string.Format(ConstantHelper.Message.ShiftIssueDraftFailed, order.OrderNumber, result.ResponseMessage));
                        continue;
                    }
                    created[order.ShiftCode ?? string.Empty] = created.GetValueOrDefault(order.ShiftCode ?? string.Empty) + 1;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[ProductionPlanService] phiếu xuất nháp cho lệnh {Order}", order.OrderNumber);
                    warnings.Add(string.Format(ConstantHelper.Message.ShiftIssueDraftFailed, order.OrderNumber, ex.Message));
                }
            }
            foreach (var (shiftCode, count) in created)
            {
                await BusinessNotifier.ToMenusAsync(_notifications, db, NotificationCatalog.PlanReady, ["#/scm-issue"],
                    new { title = $"📦 Phiếu xuất ca {shiftCode} – kế hoạch {planNumber} sẵn sàng: {count} phiếu Nháp, kiểm rồi Ghi sổ cả ca", lines = new List<string>() },
                    $"{NotificationCatalog.PlanReady}:ISSUE:{planNumber}:{shiftCode}", requireAdd: true);
            }
            return warnings;
        }
    }
}
