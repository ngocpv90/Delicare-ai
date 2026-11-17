using AppDelicare.Data.Connection;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// BE2.1 (spec 7.5 trace, Catalog 118 / 119) – truy xuất nguồn gốc 2 chiều theo quan hệ logic (lớp tĩnh, chỉ đọc, dùng DbContext của service gọi):
    /// <list type="bullet">
    /// <item>Xuôi (lô NL → khách): lô → dòng phiếu xuất (MaterialIssueLine.MaterialLotId / LotId) → phiếu xuất (không Hủy) → lệnh SX → mẻ (số mẻ trong MaterialIssue.Note
    /// – GAP-PRD-04; không khớp → mọi mẻ của lệnh, đánh dấu suy luận) → lệnh đóng gói → đơn hàng → đơn giao → khách.</item>
    /// <item>Ngược (mẻ → NCC): mẻ → phiếu xuất của mẻ → dòng → lô → dòng phiếu nhập → phiếu nhập → PO → NCC (+ nhiệt độ nhận, nhiệt độ lõi).</item>
    /// <item>Mẻ chưa có lệnh đóng gói (đóng gói trực tiếp ProductionBatch/Pack): đơn hàng suy ra = đơn (Đã chốt / Đang SX / Đã giao / Đã đối soát) của ngày + ca của lệnh SX có món
    /// trong thực đơn – <c>Inferred = true</c> (GAP-TRC-01, PROPOSED – BA/PO CONFIRM). Chưa có mã QR (spec GAP-F-10).</item>
    /// </list>
    /// </summary>
    public static class TraceRules
    {
        private static readonly string[] TracedOrderStatus = [OrderStatus.Locked, OrderStatus.InProduction, OrderStatus.Delivered, OrderStatus.Reconciled];

        private static TraceNodeModel Node(Guid id, string type, string? number, string? name = null, string? status = null, DateTime? date = null, decimal? qty = null, bool inferred = false, string? note = null)
            => new() { Id = id, Type = type, Number = number, Name = name, Status = status, Date = date, Quantity = qty, Inferred = inferred, Note = note };

        private static void AddDistinct(List<TraceNodeModel> list, TraceNodeModel node)
        {
            var existing = list.FirstOrDefault(x => x.Id == node.Id);
            if (existing == null)
            {
                list.Add(node);
            }
            else if (!node.Inferred)
            {
                existing.Inferred = false;
            }
        }

        /// <summary>Xuôi: lô nguyên liệu → … → khách hàng (+ nguồn gốc của chính lô: phiếu nhập, PO, NCC).</summary>
        public static async Task<TraceModel?> ForwardFromLotAsync(DatabaseContext db, Guid lotId, BusinessRulesOptions rules)
        {
            var lot = await db.MaterialLot.AsNoTracking().FirstOrDefaultAsync(l => l.Id == lotId && !l.IsDeleted);
            if (lot == null)
            {
                return null;
            }
            var ingredient = await db.Ingredient.AsNoTracking().FirstOrDefaultAsync(i => i.Id == lot.IngredientId);
            var model = new TraceModel
            {
                Direction = "FORWARD",
                Root = Node(lot.Id, "MaterialLot", lot.LotNumber ?? lot.LotNo, ingredient?.Name, lot.Status, lot.ReceivedDate, null, note: "QC " + (lot.QcStatus ?? "-") + (lot.ExpiryDate is { } e ? " – HSD " + ProductionRules.Day(e) : string.Empty)),
            };
            AddDistinct(model.ListLot, model.Root);
            await SourcesAsync(db, model, [lot]);

            var issueLines = await db.MaterialIssueLine.AsNoTracking().Where(l => !l.IsDeleted && (l.MaterialLotId == lot.Id || l.LotId == lot.Id)).ToListAsync();
            var issueIds = issueLines.Select(l => l.MaterialIssueId).Distinct().ToList();
            var issues = await db.MaterialIssue.AsNoTracking().Where(i => issueIds.Contains(i.Id) && !i.IsDeleted && i.Status != MaterialIssueStatus.Cancelled).ToListAsync();
            var batches = new List<(ProductionBatch Batch, bool Inferred)>();
            foreach (var issue in issues)
            {
                model.ListIssue.Add(Node(issue.Id, "MaterialIssue", issue.IssueNumber ?? issue.IssueNo, null, issue.Status, issue.IssueDate,
                    issueLines.Where(l => l.MaterialIssueId == issue.Id).Sum(l => l.Quantity), note: issue.Note));
                var orderBatches = await db.ProductionBatch.AsNoTracking().Where(b => b.ProductionOrderId == issue.ProductionOrderId && !b.IsDeleted).ToListAsync();
                var matched = orderBatches.Where(b => issue.Note != null && issue.Note.Contains(b.BatchNumber ?? b.BatchNo ?? "\u0001")).ToList();
                if (matched.Count != 0)
                {
                    batches.AddRange(matched.Select(b => (b, false)));
                }
                else
                {
                    batches.AddRange(orderBatches.Select(b => (b, true)));
                }
            }
            await DownstreamAsync(db, model, batches, rules);
            if (issues.Count == 0)
            {
                model.ListWarning.Add("Lô chưa được xuất cho sản xuất");
            }
            return model;
        }

        /// <summary>Ngược: mẻ → phiếu xuất → lô → phiếu nhập → PO → NCC; kèm xuôi: mẻ → lệnh đóng gói → đơn → đơn giao → khách.</summary>
        public static async Task<TraceModel?> FromBatchAsync(DatabaseContext db, Guid batchId, BusinessRulesOptions rules)
        {
            var batch = await db.ProductionBatch.AsNoTracking().FirstOrDefaultAsync(b => b.Id == batchId && !b.IsDeleted);
            if (batch == null)
            {
                return null;
            }
            var dish = await db.Dish.AsNoTracking().FirstOrDefaultAsync(d => d.Id == batch.DishId);
            var model = new TraceModel
            {
                Direction = "BACKWARD",
                Root = Node(batch.Id, "ProductionBatch", batch.BatchNumber ?? batch.BatchNo, dish?.Name, batch.Status, batch.StartTime ?? batch.StartDate,
                    batch.AcceptedQty ?? batch.ActualQuantity ?? batch.PlannedQuantity, note: "QC " + (batch.QcStatus ?? "-")),
            };
            var issues = await ProductionRules.IssuesOf(db, batch).Where(i => i.Status != MaterialIssueStatus.Cancelled).ToListAsync();
            var issueIds = issues.Select(i => i.Id).ToList();
            var lines = await db.MaterialIssueLine.AsNoTracking().Where(l => !l.IsDeleted && issueIds.Contains(l.MaterialIssueId)).ToListAsync();
            foreach (var issue in issues)
            {
                model.ListIssue.Add(Node(issue.Id, "MaterialIssue", issue.IssueNumber ?? issue.IssueNo, null, issue.Status, issue.IssueDate,
                    lines.Where(l => l.MaterialIssueId == issue.Id).Sum(l => l.Quantity), note: issue.Note));
            }
            var lotIds = lines.Select(l => l.MaterialLotId ?? l.LotId).Where(x => x != null).Select(x => x!.Value).Distinct().ToList();
            var lots = await db.MaterialLot.AsNoTracking().Where(l => lotIds.Contains(l.Id)).ToListAsync();
            var ingredientIds = lots.Select(l => l.IngredientId).Distinct().ToList();
            var ingredients = await db.Ingredient.AsNoTracking().Where(i => ingredientIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Name);
            foreach (var lot in lots.OrderBy(l => l.LotNumber, StringComparer.Ordinal))
            {
                model.ListLot.Add(Node(lot.Id, "MaterialLot", lot.LotNumber ?? lot.LotNo, ingredients.GetValueOrDefault(lot.IngredientId), lot.Status, lot.ReceivedDate,
                    lines.Where(l => (l.MaterialLotId ?? l.LotId) == lot.Id).Sum(l => l.Quantity), note: "QC " + (lot.QcStatus ?? "-")));
            }
            if (lines.Any(l => (l.MaterialLotId ?? l.LotId) == null))
            {
                model.ListWarning.Add("Có dòng xuất không gắn lô – không truy được nguồn gốc của dòng đó");
            }
            await SourcesAsync(db, model, lots);
            var cores = await db.TemperatureLog.AsNoTracking().Where(t => t.ProductionBatchId == batch.Id).OrderBy(t => t.MeasuredAt).ToListAsync();
            model.ListTemperature.AddRange(cores.Select(t => Node(t.Id, "TemperatureLog", t.MeasurementType, null, t.IsWithinRange == false ? "OUT_OF_RANGE" : "OK", t.MeasuredAt, t.TemperatureC)));
            await DownstreamAsync(db, model, [(batch, false)], rules);
            if (issues.Count == 0)
            {
                model.ListWarning.Add("Mẻ chưa có phiếu xuất nguyên liệu");
            }
            return model;
        }

        /// <summary>Nguồn gốc của các lô: dòng phiếu nhập → phiếu nhập → PO → NCC (NCC của lô nếu không có phiếu nhập) + nhiệt độ nhận.</summary>
        private static async Task SourcesAsync(DatabaseContext db, TraceModel model, List<MaterialLot> lots)
        {
            var grLineIds = lots.Where(l => l.GoodsReceiptLineId != null).Select(l => l.GoodsReceiptLineId!.Value).ToList();
            var grLines = await db.GoodsReceiptLine.AsNoTracking().Where(l => grLineIds.Contains(l.Id)).ToListAsync();
            var grIds = grLines.Select(l => l.GoodsReceiptId).Distinct().ToList();
            var receipts = await db.GoodsReceipt.AsNoTracking().Where(r => grIds.Contains(r.Id)).ToListAsync();
            foreach (var r in receipts)
            {
                AddDistinct(model.ListGoodsReceipt, Node(r.Id, "GoodsReceipt", r.ReceiptNumber ?? r.ReceiptNo, null, r.Status, r.ReceiptDate,
                    grLines.Where(l => l.GoodsReceiptId == r.Id).Sum(l => l.AcceptedQuantity)));
            }
            var poIds = receipts.Where(r => r.PurchaseOrderId != null).Select(r => r.PurchaseOrderId!.Value).Distinct().ToList();
            var pos = await db.PurchaseOrder.AsNoTracking().Where(p => poIds.Contains(p.Id)).ToListAsync();
            foreach (var p in pos)
            {
                AddDistinct(model.ListPurchaseOrder, Node(p.Id, "PurchaseOrder", p.PurchaseOrderNumber ?? p.PONo, null, p.Status, p.OrderDate, p.TotalAmount));
            }
            var supplierIds = receipts.Select(r => r.SupplierId).Concat(lots.Where(l => l.SupplierId != null).Select(l => l.SupplierId!.Value)).Distinct().ToList();
            var suppliers = await db.Supplier.AsNoTracking().Where(s => supplierIds.Contains(s.Id)).ToListAsync();
            foreach (var s in suppliers)
            {
                AddDistinct(model.ListSupplier, Node(s.Id, "Supplier", s.Code, s.Name, s.Status));
            }
            var lotIds = lots.Select(l => l.Id).ToList();
            var temps = await db.TemperatureLog.AsNoTracking().Where(t => t.MaterialLotId != null && lotIds.Contains(t.MaterialLotId.Value)).OrderBy(t => t.MeasuredAt).ToListAsync();
            model.ListTemperature.AddRange(temps.Select(t => Node(t.Id, "TemperatureLog", t.MeasurementType, null, t.IsWithinRange == false ? "OUT_OF_RANGE" : "OK", t.MeasuredAt, t.TemperatureC)));
        }

        /// <summary>Phía sau mẻ: lệnh SX, mẻ, lệnh đóng gói → đơn hàng (trực tiếp) hoặc suy luận theo ngày + ca + món → đơn giao → khách.</summary>
        private static async Task DownstreamAsync(DatabaseContext db, TraceModel model, List<(ProductionBatch Batch, bool Inferred)> batches, BusinessRulesOptions rules)
        {
            var orderIdsDirect = new HashSet<Guid>();
            var orderIdsInferred = new HashSet<Guid>();
            var productionOrderIds = batches.Select(b => b.Batch.ProductionOrderId).Distinct().ToList();
            var productionOrders = await db.ProductionOrder.AsNoTracking().Where(o => productionOrderIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id);
            var dishIds = batches.Select(b => b.Batch.DishId).Distinct().ToList();
            var dishes = await db.Dish.AsNoTracking().Where(d => dishIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name);
            foreach (var po in productionOrders.Values)
            {
                AddDistinct(model.ListProductionOrder, Node(po.Id, "ProductionOrder", po.ProductionOrderNumber ?? po.ProductionOrderNo, dishes.GetValueOrDefault(po.DishId), po.Status,
                    po.PlannedStartDate, po.ActualQty ?? po.PlannedQty));
            }
            var batchIds = batches.Select(b => b.Batch.Id).Distinct().ToList();
            var packings = await db.PackingOrder.AsNoTracking()
                .Where(p => !p.IsDeleted && batchIds.Contains(p.ProductionBatchId) && p.Status != PackingOrderStatus.Cancelled).ToListAsync();
            foreach (var (batch, inferred) in batches.GroupBy(b => b.Batch.Id).Select(g => (g.First().Batch, g.All(x => x.Inferred))))
            {
                if (model.Root.Id != batch.Id)
                {
                    AddDistinct(model.ListBatch, Node(batch.Id, "ProductionBatch", batch.BatchNumber ?? batch.BatchNo, dishes.GetValueOrDefault(batch.DishId), batch.Status,
                        batch.StartTime ?? batch.StartDate, batch.AcceptedQty ?? batch.ActualQuantity ?? batch.PlannedQuantity, inferred));
                }
                var mine = packings.Where(p => p.ProductionBatchId == batch.Id).ToList();
                foreach (var p in mine)
                {
                    AddDistinct(model.ListPackingOrder, Node(p.Id, "PackingOrder", p.PackingOrderNumber ?? p.PackingNo, null, p.Status, p.PackingDate, p.PlannedQty, inferred));
                    if (p.OrderId is { } oid)
                    {
                        (inferred ? orderIdsInferred : orderIdsDirect).Add(oid);
                    }
                }
                if (mine.Count == 0 && productionOrders.TryGetValue(batch.ProductionOrderId, out var po))
                {
                    // Đóng gói trực tiếp (không có lệnh đóng gói) → suy ra đơn theo ngày + ca + món (GAP-TRC-01).
                    var plan = await db.ProductionPlan.AsNoTracking().FirstOrDefaultAsync(x => x.Id == po.ProductionPlanId);
                    DateTime date = (plan?.ProductionDate ?? batch.StartDate).Date;
                    var (demands, _, _, _) = await ProductionRules.DishDemandsAsync(db, date, TracedOrderStatus, rules);
                    foreach (var d in demands.Where(d => d.DishId == batch.DishId && (po.ShiftId == null || d.ShiftId == po.ShiftId)))
                    {
                        orderIdsInferred.Add(d.OrderId);
                    }
                }
            }
            orderIdsInferred.ExceptWith(orderIdsDirect);
            var allOrderIds = orderIdsDirect.Concat(orderIdsInferred).ToList();
            var orders = await db.Order.AsNoTracking().Where(o => allOrderIds.Contains(o.Id)).ToListAsync();
            foreach (var o in orders.OrderBy(o => o.OrderNumber, StringComparer.Ordinal))
            {
                AddDistinct(model.ListOrder, Node(o.Id, "Order", o.OrderNumber ?? o.OrderNo, null, o.Status, o.MealDate, o.TotalQuantity, orderIdsInferred.Contains(o.Id)));
            }
            var deliveryOrders = await db.DeliveryOrder.AsNoTracking()
                .Where(d => !d.IsDeleted && allOrderIds.Contains(d.OrderId) && d.Status != DeliveryOrderStatus.Cancelled).ToListAsync();
            var meals = await DeliveryRules.DeliveryOrderMealsAsync(db, deliveryOrders);
            foreach (var d in deliveryOrders.OrderBy(d => d.DeliveryNumber, StringComparer.Ordinal))
            {
                AddDistinct(model.ListDeliveryOrder, Node(d.Id, "DeliveryOrder", d.DeliveryNumber ?? d.DeliveryNo, null, d.Status, d.ActualDeliveryTime ?? d.PlannedDeliveryTime,
                    meals[d.Id], orderIdsInferred.Contains(d.OrderId)));
            }
            var customerIds = orders.Select(o => o.CustomerId).Distinct().ToList();
            var customers = await db.Customer.AsNoTracking().Where(c => customerIds.Contains(c.Id)).ToListAsync();
            foreach (var c in customers.OrderBy(c => c.Code, StringComparer.Ordinal))
            {
                bool inferred = orders.Where(o => o.CustomerId == c.Id).All(o => orderIdsInferred.Contains(o.Id));
                AddDistinct(model.ListCustomer, Node(c.Id, "Customer", c.Code, c.DisplayName ?? c.Name, c.Status, inferred: inferred));
            }
            if (orderIdsInferred.Count != 0)
            {
                model.ListWarning.Add("Một số đơn / khách suy ra theo ngày + ca + món (mẻ chưa có lệnh đóng gói) – Inferred = true");
            }
        }
    }
}
