using System.Globalization;
using AppDelicare.Application.Common;
using AppDelicare.Application.Common.Crud;
using AppDelicare.Data.Connection;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Application.MasterData
{
    /// <summary>
    /// T45B.3 – logic riêng của màn P1 (spec mục 4) gắn vào CRUD chuẩn qua hook (Enrich / Rules / AfterSave / CustomFilters),
    /// và các query riêng (overview, layout, quy đổi đơn vị).
    /// </summary>
    public static class P1Hooks
    {
        /// <summary>Trạng thái đơn đã xong (để tính suất 7 ngày) – PROPOSED theo STATUS_CATALOG (Order.Status).</summary>
        public static readonly string[] CompletedOrderStatuses = ["Delivered", "Reconciled"];

        /// <summary>Trạng thái đơn giao đã kết thúc (không còn trỏ "đang giao" tới điểm giao) – PROPOSED.</summary>
        public static readonly string[] ClosedDeliveryStatuses = ["DELIVERED", "Delivered", "FAILED", "Failed", "Cancelled", "CANCELLED", "Completed", "COMPLETED"];

        public const string ActiveContractStatus = "Active";
        public const string InactiveStatus = "Inactive";

        // ---------------------------------------------------------------- Khách hàng (spec 4.1)
        /// <summary>activeContracts, locationCount, avgMealsPerDay7d, branchName.</summary>
        public static async Task CustomerEnrich(CrudEnrichContext c)
        {
            var ids = c.Ids;
            var contracts = await c.Db.Contract.AsNoTracking()
                .Where(x => !x.IsDeleted && ids.Contains(x.CustomerId) && x.Status == ActiveContractStatus)
                .Select(x => new { x.CustomerId, x.ContractNumber, x.Status }).ToListAsync(c.CancellationToken);
            var locations = await c.Db.CustomerDeliveryLocation.AsNoTracking()
                .Where(x => !x.IsDeleted && ids.Contains(x.CustomerId))
                .GroupBy(x => x.CustomerId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, c.CancellationToken);
            DateTime from = DateTime.Today.AddDays(-7);
            var meals = await c.Db.Order.AsNoTracking()
                .Where(x => !x.IsDeleted && ids.Contains(x.CustomerId) && x.MealDate >= from && CompletedOrderStatuses.Contains(x.Status))
                .GroupBy(x => x.CustomerId).Select(g => new { g.Key, Total = g.Sum(o => o.TotalQuantity) })
                .ToDictionaryAsync(x => x.Key, x => x.Total, c.CancellationToken);
            var branchIds = c.Items.Select(i => i.TryGetValue("branchId", out var b) ? b as Guid? : null).Where(b => b != null).Select(b => b!.Value).Distinct().ToList();
            var branches = await c.Db.OrganizationUnit.AsNoTracking().Where(o => branchIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.Name, c.CancellationToken);
            foreach (var item in c.Items)
            {
                var id = (Guid)item["id"]!;
                item["activeContracts"] = contracts.Where(x => x.CustomerId == id).Select(x => new { x.ContractNumber, x.Status }).ToList();
                item["locationCount"] = locations.GetValueOrDefault(id);
                item["avgMealsPerDay7d"] = Math.Round((meals.GetValueOrDefault(id) ?? 0m) / 7m, 1);
                item["branchName"] = item.TryGetValue("branchId", out var b) && b is Guid bid ? branches.GetValueOrDefault(bid) : null;
            }
        }

        /// <summary>Spec 4.1: kiểm tra trùng CIF ở service (Code đã kiểm ở CRUD chuẩn).</summary>
        public static async Task CustomerRules(CrudRuleContext c)
        {
            string? cif = c.Get<string>("CIF");
            if (!string.IsNullOrWhiteSpace(cif)
                && await c.Db.Customer.AnyAsync(x => !x.IsDeleted && x.CIF == cif && x.Id != c.Id, c.CancellationToken))
            {
                throw new BusinessRuleException(409, "DUPLICATE", $"CIF '{cif}' đã tồn tại.", "cif");
            }
        }

        // ---------------------------------------------------------------- Điểm giao (spec 4.2)
        /// <summary>Không cho ngừng hoạt động (Status = Inactive) khi còn DeliveryOrder chưa giao trỏ tới điểm đó.</summary>
        public static async Task DeliveryLocationInactiveGuard(CrudRuleContext c)
        {
            if (c.Id is { } id && c.Get<string>("Status") == InactiveStatus
                && await c.Db.DeliveryOrder.AnyAsync(d => !d.IsDeleted && (d.CustomerDeliveryLocationId == id || d.DeliveryLocationId == id)
                    && !ClosedDeliveryStatuses.Contains(d.Status!), c.CancellationToken))
            {
                throw new BusinessRuleException(409, "IN_USE", "Còn đơn giao chưa hoàn tất tại điểm này – không ngừng hoạt động được.", "status");
            }
        }

        // ---------------------------------------------------------------- Giá NCC (spec 4.3)
        /// <summary><c>effectiveOn=YYYY-MM-DD</c>: EffectiveFrom ≤ ngày ≤ EffectiveTo (NULL = không giới hạn).</summary>
        public static IQueryable SupplierMaterialEffectiveOn(IQueryable query, string raw)
        {
            if (!DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            {
                throw new ValidationFailedException([("effectiveOn", "Phải là ngày YYYY-MM-DD.")]);
            }
            return ((IQueryable<SupplierMaterial>)query).Where(x => (x.EffectiveFrom == null || x.EffectiveFrom <= day) && (x.EffectiveTo == null || x.EffectiveTo >= day));
        }

        /// <summary>Mỗi nguyên liệu chỉ một dòng IsPreferred đang hiệu lực: đặt dòng mới là ưu tiên → tắt dòng cũ.</summary>
        public static async Task SupplierMaterialPreferred(CrudRuleContext c)
        {
            if (c.Values.GetValueOrDefault("IsPreferred") is not true || c.Values.GetValueOrDefault("IngredientId") is not Guid ingredient)
            {
                return;
            }
            var others = await c.Db.SupplierMaterial.Where(x => !x.IsDeleted && x.IngredientId == ingredient && x.IsPreferred && x.Id != c.Id).ToListAsync(c.CancellationToken);
            if (others.Count == 0)
            {
                return;
            }
            others.ForEach(o => o.IsPreferred = false);
            await c.Db.SaveChangesAsync(ConstantHelper.TypeSave.Update, c.CancellationToken);
        }

        // ---------------------------------------------------------------- Nguyên liệu (spec 4.4)
        /// <summary>availableQuantity = Σ InventoryBalance.AvailableQuantity; belowMin = available &lt; MinimumStock.</summary>
        public static async Task IngredientEnrich(CrudEnrichContext c)
        {
            var ids = c.Ids;
            var stock = await c.Db.InventoryBalance.AsNoTracking().Where(b => !b.IsDeleted && ids.Contains(b.IngredientId))
                .GroupBy(b => b.IngredientId).Select(g => new { g.Key, Qty = g.Sum(b => b.AvailableQuantity) })
                .ToDictionaryAsync(x => x.Key, x => x.Qty, c.CancellationToken);
            foreach (var item in c.Items)
            {
                decimal available = stock.GetValueOrDefault((Guid)item["id"]!);
                item["availableQuantity"] = available;
                item["belowMin"] = item.TryGetValue("minimumStock", out var m) && m is decimal min && available < min;
            }
        }
    }

    // ================================================================ GET /sales/customers/{id}/overview (spec 4.1)
    public sealed record CustomerKpi(int OrderCount, decimal ReconciledRevenue, int ComplaintCount);

    public sealed record CustomerOverview(IDictionary<string, object?> Customer, IReadOnlyList<CustomerContact> Contacts, IReadOnlyList<CustomerDeliveryLocation> Locations,
        IReadOnlyList<object> Contracts, IReadOnlyList<object> RecentOrders, CustomerKpi Kpi);

    public sealed record CustomerOverviewQuery(Guid CustomerId) : IQuery<CustomerOverview>;

    /// <summary>Một lần gọi cho drawer 360°. reconciledRevenue = Σ OrderReconciliation.FinalAmount của các đơn của khách.</summary>
    public sealed class CustomerOverviewHandler(DatabaseContext db, ICrudService crud) : IRequestHandler<CustomerOverviewQuery, CustomerOverview>
    {
        public async Task<CustomerOverview> HandleAsync(CustomerOverviewQuery q, CancellationToken ct)
        {
            var resource = ResourceRegistry.Find("sales", "customers")!;
            var customer = (await crud.GetAsync(resource, q.CustomerId, ct)).Data;
            var id = q.CustomerId;
            var contacts = await db.CustomerContact.AsNoTracking().Where(x => !x.IsDeleted && x.CustomerId == id).ToListAsync(ct);
            var locations = await db.CustomerDeliveryLocation.AsNoTracking().Where(x => !x.IsDeleted && x.CustomerId == id).ToListAsync(ct);
            var contracts = await db.Contract.AsNoTracking().Where(x => !x.IsDeleted && x.CustomerId == id).OrderByDescending(x => x.StartDate).ToListAsync(ct);
            var contractIds = contracts.Select(x => x.Id).ToList();
            var meals = await db.ContractMeal.AsNoTracking().Where(m => !m.IsDeleted && contractIds.Contains(m.ContractId)).ToListAsync(ct);
            var orders = db.Order.AsNoTracking().Where(o => !o.IsDeleted && o.CustomerId == id);
            var recent = await orders.OrderByDescending(o => o.MealDate).Take(15)
                .Select(o => new { o.Id, o.OrderNumber, o.MealDate, o.DeliveryDate, o.TotalQuantity, o.TotalAmount, o.Status }).ToListAsync(ct);
            int orderCount = await orders.CountAsync(ct);
            decimal revenue = await (from r in db.OrderReconciliation.AsNoTracking()
                                     join o in db.Order.AsNoTracking() on r.OrderId equals o.Id
                                     where !r.IsDeleted && !o.IsDeleted && o.CustomerId == id
                                     select (decimal?)r.FinalAmount).SumAsync(ct) ?? 0m;
            int complaints = await db.CustomerComplaint.AsNoTracking().CountAsync(x => !x.IsDeleted && x.CustomerId == id, ct);
            return new CustomerOverview(customer, contacts, locations,
                contracts.Select(x => (object)new { x.Id, x.ContractNumber, x.Name, x.StartDate, x.EndDate, x.EffectiveFrom, x.EffectiveTo, x.Status, x.ContractValue,
                    Meals = meals.Where(m => m.ContractId == x.Id).Select(m => new { m.Id, m.MealType, m.MealName, m.ServiceType, m.UnitPrice, m.Status }).ToList() }).ToList(),
                recent.Cast<object>().ToList(), new CustomerKpi(orderCount, revenue, complaints));
        }
    }

    // ================================================================ GET /scm/suppliers/{id}/overview (spec 4.3)
    public sealed record SupplierKpi(decimal? OnTimeRate, decimal? RejectRate);

    public sealed record SupplierOverview(IDictionary<string, object?> Supplier, IReadOnlyList<object> Materials, IReadOnlyList<object> OpenPurchaseOrders,
        IReadOnlyList<object> Issues, SupplierKpi Kpi);

    public sealed record SupplierOverviewQuery(Guid SupplierId) : IQuery<SupplierOverview>;

    /// <summary>
    /// rejectRate = Σ RejectedQuantity ÷ Σ ReceivedQuantity của phiếu nhập 90 ngày. onTimeRate (PROPOSED) = tỷ lệ phiếu nhập có ReceiptDate ≤ ngày dự kiến của PO
    /// (ExpectedDeliveryDate, thiếu thì ExpectedDate) trong 90 ngày. Không có dữ liệu → null.
    /// </summary>
    public sealed class SupplierOverviewHandler(DatabaseContext db, ICrudService crud) : IRequestHandler<SupplierOverviewQuery, SupplierOverview>
    {
        public static readonly string[] ClosedPoStatuses = ["Received", "Closed", "Cancelled"];

        public async Task<SupplierOverview> HandleAsync(SupplierOverviewQuery q, CancellationToken ct)
        {
            var supplier = (await crud.GetAsync(ResourceRegistry.Find("scm", "suppliers")!, q.SupplierId, ct)).Data;
            var id = q.SupplierId;
            var materials = await (from m in db.SupplierMaterial.AsNoTracking()
                                   join i in db.Ingredient.AsNoTracking() on m.IngredientId equals i.Id into ig
                                   from i in ig.DefaultIfEmpty()
                                   where !m.IsDeleted && m.SupplierId == id
                                   orderby i.Name
                                   select (object)new { m.Id, m.IngredientId, IngredientName = i.Name, m.UnitPrice, m.MinimumOrderQuantity, m.LeadTimeDays, m.IsPreferred, m.EffectiveFrom, m.EffectiveTo, m.Status })
                                  .ToListAsync(ct);
            var openPos = await db.PurchaseOrder.AsNoTracking().Where(p => !p.IsDeleted && p.SupplierId == id && !ClosedPoStatuses.Contains(p.Status!))
                .OrderBy(p => p.ExpectedDeliveryDate).Select(p => (object)new { p.Id, p.PurchaseOrderNumber, p.OrderDate, p.ExpectedDeliveryDate, p.TotalAmount, p.Status }).ToListAsync(ct);
            var issues = await db.SupplierIssue.AsNoTracking().Where(x => !x.IsDeleted && x.SupplierId == id).OrderByDescending(x => x.ReportedAt).Take(20)
                .Select(x => (object)new { x.Id, x.IssueType, x.Severity, x.IssueStatus, x.ReportedAt, x.DueAt, x.ClosedAt, x.Description }).ToListAsync(ct);

            DateTime since = DateTime.Today.AddDays(-90);
            var lines = await (from l in db.GoodsReceiptLine.AsNoTracking()
                               join r in db.GoodsReceipt.AsNoTracking() on l.GoodsReceiptId equals r.Id
                               where !l.IsDeleted && !r.IsDeleted && r.SupplierId == id && r.ReceiptDate >= since
                               select new { l.ReceivedQuantity, l.RejectedQuantity }).ToListAsync(ct);
            decimal received = lines.Sum(l => (decimal?)l.ReceivedQuantity ?? 0m);
            decimal? rejectRate = received > 0 ? Math.Round(lines.Sum(l => (decimal?)l.RejectedQuantity ?? 0m) / received * 100, 2) : null;
            var receipts = await (from r in db.GoodsReceipt.AsNoTracking()
                                  join p in db.PurchaseOrder.AsNoTracking() on r.PurchaseOrderId equals p.Id
                                  where !r.IsDeleted && r.SupplierId == id && r.ReceiptDate >= since
                                  select new { r.ReceiptDate, Expected = p.ExpectedDeliveryDate ?? p.ExpectedDate }).ToListAsync(ct);
            var withExpected = receipts.Where(r => r.Expected != null).ToList();
            decimal? onTime = withExpected.Count > 0 ? Math.Round(withExpected.Count(r => r.ReceiptDate.Date <= r.Expected!.Value.Date) * 100m / withExpected.Count, 2) : null;
            return new SupplierOverview(supplier, materials, openPos, issues, new SupplierKpi(onTime, rejectRate));
        }
    }

    // ================================================================ GET /scm/warehouses/{id}/layout (spec 4.6)
    public sealed record ZoneLayout(Guid Id, string? Code, string? Name, decimal? TemperatureMin, decimal? TemperatureMax, string? Status, IReadOnlyList<object> Locations);

    public sealed record WarehouseLayout(IDictionary<string, object?> Warehouse, IReadOnlyList<ZoneLayout> Zones);

    public sealed record WarehouseLayoutQuery(Guid WarehouseId) : IQuery<WarehouseLayout>;

    /// <summary>Dựng từ StorageZone + StorageLocation (dùng StorageZoneId, bỏ ZoneId deprecated).</summary>
    public sealed class WarehouseLayoutHandler(DatabaseContext db, ICrudService crud) : IRequestHandler<WarehouseLayoutQuery, WarehouseLayout>
    {
        public async Task<WarehouseLayout> HandleAsync(WarehouseLayoutQuery q, CancellationToken ct)
        {
            var warehouse = (await crud.GetAsync(ResourceRegistry.Find("scm", "warehouses")!, q.WarehouseId, ct)).Data;
            var zones = await db.StorageZone.AsNoTracking().Where(z => !z.IsDeleted && z.WarehouseId == q.WarehouseId).OrderBy(z => z.Code).ToListAsync(ct);
            var zoneIds = zones.Select(z => z.Id).ToList();
            var locations = await db.StorageLocation.AsNoTracking()
                .Where(l => !l.IsDeleted && l.StorageZoneId != null && zoneIds.Contains(l.StorageZoneId.Value)).OrderBy(l => l.Code).ToListAsync(ct);
            return new WarehouseLayout(warehouse, zones.Select(z => new ZoneLayout(z.Id, z.Code, z.Name, z.TemperatureMin, z.TemperatureMax, z.Status,
                locations.Where(l => l.StorageZoneId == z.Id).Select(l => (object)new { l.Id, l.Code, l.Name, l.LocationType, l.Status }).ToList())).ToList());
        }
    }

    // ================================================================ UomService.Convert (spec 4.5)
    public interface IUomService
    {
        /// <summary>Quy đổi số lượng giữa 2 đơn vị: chiều xuôi (×factor), chiều ngược (÷factor), hoặc qua một đơn vị trung gian. Không có → 422 UOM_CONVERSION_MISSING.</summary>
        Task<decimal> ConvertAsync(decimal quantity, Guid fromUnitId, Guid toUnitId, DateTime? on = null, CancellationToken cancellationToken = default);
    }

    public sealed class UomService(DatabaseContext db) : IUomService
    {
        public async Task<decimal> ConvertAsync(decimal quantity, Guid fromUnitId, Guid toUnitId, DateTime? on = null, CancellationToken ct = default)
        {
            if (fromUnitId == toUnitId)
            {
                return quantity;
            }
            DateTime day = (on ?? DateTime.Today).Date;
            var rules = await db.UomConversion.AsNoTracking()
                .Where(x => !x.IsDeleted && x.FromUnitId != Guid.Empty && x.ToUnitId != Guid.Empty && x.ConversionFactor > 0
                    && (x.EffectiveFrom == null || x.EffectiveFrom <= day) && (x.EffectiveTo == null || x.EffectiveTo >= day))
                .Select(x => new { From = x.FromUnitId, To = x.ToUnitId, x.ConversionFactor }).ToListAsync(ct);
            // Đồ thị 2 chiều: from → to (×k), to → from (÷k). BFS tìm đường ngắn nhất.
            var edges = rules.SelectMany(r => new[] { (r.From, r.To, Factor: r.ConversionFactor), (From: r.To, To: r.From, Factor: 1 / r.ConversionFactor) })
                .GroupBy(e => e.From).ToDictionary(g => g.Key, g => g.ToList());
            var best = new Dictionary<Guid, decimal> { [fromUnitId] = 1m };
            var queue = new Queue<Guid>([fromUnitId]);
            while (queue.Count > 0)
            {
                var u = queue.Dequeue();
                if (u == toUnitId)
                {
                    return quantity * best[u];
                }
                foreach (var e in edges.GetValueOrDefault(u) ?? [])
                {
                    if (!best.ContainsKey(e.To))
                    {
                        best[e.To] = best[u] * e.Factor;
                        queue.Enqueue(e.To);
                    }
                }
            }
            throw new BusinessRuleException(422, "UOM_CONVERSION_MISSING", "Chưa có quy đổi giữa 2 đơn vị này.", "toUnitId");
        }
    }

    public sealed record ConvertUomQuery(decimal Quantity, Guid FromUnitId, Guid ToUnitId, DateTime? On) : IQuery<decimal>;

    public sealed class ConvertUomHandler(IUomService uom) : IRequestHandler<ConvertUomQuery, decimal>
    {
        public Task<decimal> HandleAsync(ConvertUomQuery q, CancellationToken ct) => uom.ConvertAsync(q.Quantity, q.FromUnitId, q.ToUnitId, q.On, ct);
    }
}
