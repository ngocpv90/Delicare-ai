#nullable enable
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Reflection;
using AppDelicare.Data.Connection;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// BE3.3 (Catalog 16, 17 – D-57 i, D-58): DANH SÁCH TRẮNG viết cứng trong code cho tìm kiếm toàn cục (8 loại bản ghi của spec) và tab "Liên quan"
    /// (cặp nghiệp vụ lấy từ <c>docs/reference/LOGICAL_RELATIONS.md</c>). Client chỉ gửi tên danh sách (<see cref="CommonRegistry"/>) + Id / từ khóa;
    /// không bao giờ nhận / trả tên bảng, tên cột, quan hệ (D-58). Truy vấn EF LINQ (tham số hóa). Màn hình (screen-id) khớp <c>ScreenMap.ByClass</c> (test kiểm).
    /// </summary>
    public static class CommonRelations
    {
        /// <summary>Tìm kiếm toàn cục: 8 loại (spec 2.x global-search) – thứ tự trả về.</summary>
        public static readonly IReadOnlyList<CommonSearchType> SearchTypes =
        [
            S<Customer>("Customer", (db, q) => db.Customer.Where(x => !x.IsDeleted
                    && ((x.Code != null && x.Code.Contains(q)) || (x.Name != null && x.Name.Contains(q)) || (x.DisplayName != null && x.DisplayName.Contains(q))))
                .Select(x => new CommonRow { Id = x.Id, Code = x.Code, Name = x.Name ?? x.DisplayName, Status = x.Status })),
            S<Order>("Order", (db, q) => db.Order.Where(x => !x.IsDeleted
                    && ((x.OrderNumber != null && x.OrderNumber.Contains(q)) || (x.OrderNo != null && x.OrderNo.Contains(q))))
                .Select(x => new CommonRow
                {
                    Id = x.Id, Code = x.OrderNumber ?? x.OrderNo, Status = x.Status, At = x.MealDate,
                    Name = db.Customer.Where(c => c.Id == x.CustomerId).Select(c => c.Name).FirstOrDefault(),
                }), withDate: true),
            S<Ingredient>("Ingredient", (db, q) => db.Ingredient.Where(x => !x.IsDeleted
                    && ((x.Code != null && x.Code.Contains(q)) || (x.Name != null && x.Name.Contains(q))))
                .Select(x => new CommonRow { Id = x.Id, Code = x.Code, Name = x.Name, Status = x.Status })),
            S<Dish>("Dish", (db, q) => db.Dish.Where(x => !x.IsDeleted
                    && ((x.Code != null && x.Code.Contains(q)) || (x.Name != null && x.Name.Contains(q))))
                .Select(x => new CommonRow { Id = x.Id, Code = x.Code, Name = x.Name, Status = x.Status })),
            S<Supplier>("Supplier", (db, q) => db.Supplier.Where(x => !x.IsDeleted
                    && ((x.Code != null && x.Code.Contains(q)) || (x.Name != null && x.Name.Contains(q)) || (x.TaxCode != null && x.TaxCode.Contains(q))))
                .Select(x => new CommonRow { Id = x.Id, Code = x.Code, Name = x.Name, Status = x.Status })),
            S<MaterialLot>("MaterialLot", (db, q) => db.MaterialLot.Where(x => !x.IsDeleted
                    && ((x.LotNumber != null && x.LotNumber.Contains(q)) || (x.LotNo != null && x.LotNo.Contains(q))))
                .Select(x => new CommonRow
                {
                    Id = x.Id, Code = x.LotNumber ?? x.LotNo, Status = x.Status, At = x.ExpiryDate,
                    Name = db.Ingredient.Where(i => i.Id == x.IngredientId).Select(i => i.Name).FirstOrDefault(),
                })),
            S<Employee>("Employee", (db, q) => db.Employee.Where(x => !x.IsDeleted
                    && ((x.Code != null && x.Code.Contains(q)) || (x.EmployeeCode != null && x.EmployeeCode.Contains(q)) || (x.FullName != null && x.FullName.Contains(q))))
                .Select(x => new CommonRow { Id = x.Id, Code = x.Code ?? x.EmployeeCode, Name = x.FullName, Status = x.Status })),
            S<PurchaseOrder>("PurchaseOrder", (db, q) => db.PurchaseOrder.Where(x => !x.IsDeleted
                    && ((x.PurchaseOrderNumber != null && x.PurchaseOrderNumber.Contains(q)) || (x.PONo != null && x.PONo.Contains(q))))
                .Select(x => new CommonRow
                {
                    Id = x.Id, Code = x.PurchaseOrderNumber ?? x.PONo, Status = x.Status, At = x.OrderDate,
                    Name = db.Supplier.Where(s => s.Id == x.SupplierId).Select(s => s.Name).FirstOrDefault(),
                }), withDate: true),
        ];

        // ------------------------------------------------------------------ Nhóm "Liên quan" (đích) – dùng lại giữa các nguồn
        private static CommonRelatedGroup Contracts(Func<DatabaseContext, Guid, IQueryable<Contract>> where) => G<Contract>("Contract", "Status",
            (db, id) => where(db, id).Where(x => !x.IsDeleted).Select(x => new CommonRow { Id = x.Id, Code = x.ContractNumber ?? x.ContractNo, Name = x.Name, Status = x.Status, At = x.EffectiveFrom }));

        private static CommonRelatedGroup Orders(Func<DatabaseContext, Guid, IQueryable<Order>> where) => G<Order>("Order", "Status",
            (db, id) => where(db, id).Where(x => !x.IsDeleted).Select(x => new CommonRow { Id = x.Id, Code = x.OrderNumber ?? x.OrderNo, Status = x.Status, At = x.MealDate }), withDate: true);

        private static CommonRelatedGroup Complaints(Func<DatabaseContext, Guid, IQueryable<CustomerComplaint>> where) => G<CustomerComplaint>("CustomerComplaint", "ComplaintStatus",
            (db, id) => where(db, id).Where(x => !x.IsDeleted).Select(x => new CommonRow { Id = x.Id, Code = x.ComplaintNo, Name = x.Description, Status = x.ComplaintStatus, At = x.ReceivedAt }));

        private static CommonRelatedGroup DeliveryOrders(Func<DatabaseContext, Guid, IQueryable<DeliveryOrder>> where) => G<DeliveryOrder>("DeliveryOrder", "Status",
            (db, id) => where(db, id).Where(x => !x.IsDeleted).Select(x => new CommonRow { Id = x.Id, Code = x.DeliveryNumber ?? x.DeliveryNo, Status = x.Status, At = x.DeliveryDate }), withDate: true);

        private static CommonRelatedGroup PurchaseOrders(Func<DatabaseContext, Guid, IQueryable<PurchaseOrder>> where) => G<PurchaseOrder>("PurchaseOrder", "Status",
            (db, id) => where(db, id).Where(x => !x.IsDeleted).Select(x => new CommonRow { Id = x.Id, Code = x.PurchaseOrderNumber ?? x.PONo, Status = x.Status, At = x.OrderDate }), withDate: true);

        private static CommonRelatedGroup GoodsReceipts(Func<DatabaseContext, Guid, IQueryable<GoodsReceipt>> where) => G<GoodsReceipt>("GoodsReceipt", "Status",
            (db, id) => where(db, id).Where(x => !x.IsDeleted).Select(x => new CommonRow { Id = x.Id, Code = x.ReceiptNumber ?? x.ReceiptNo, Status = x.Status, At = x.ReceiptDate }), withDate: true);

        private static CommonRelatedGroup SupplierIssues(Func<DatabaseContext, Guid, IQueryable<SupplierIssue>> where) => G<SupplierIssue>("SupplierIssue", "IssueStatus",
            (db, id) => where(db, id).Where(x => !x.IsDeleted).Select(x => new CommonRow { Id = x.Id, Code = x.IssueType, Name = x.Description, Status = x.IssueStatus, At = x.ReportedAt }), withDate: true);

        private static CommonRelatedGroup MaterialLots(Func<DatabaseContext, Guid, IQueryable<MaterialLot>> where) => G<MaterialLot>("MaterialLot", "Status",
            (db, id) => where(db, id).Where(x => !x.IsDeleted).Select(x => new CommonRow
            {
                Id = x.Id, Code = x.LotNumber ?? x.LotNo, Status = x.Status, At = x.ReceivedDate,
                Name = db.Ingredient.Where(i => i.Id == x.IngredientId).Select(i => i.Name).FirstOrDefault(),
            }));

        /// <summary>Nguyên liệu NCC cung cấp: từ NCC → tên nguyên liệu; từ nguyên liệu → tên NCC.</summary>
        private static CommonRelatedGroup SupplierMaterials(Func<DatabaseContext, Guid, IQueryable<SupplierMaterial>> where, bool showSupplier) => G<SupplierMaterial>("SupplierMaterial", "Status",
            showSupplier
                ? (db, id) => where(db, id).Where(x => !x.IsDeleted).Select(x => new CommonRow
                {
                    Id = x.Id, Code = x.SupplierMaterialCode, Status = x.Status, At = x.EffectiveFrom,
                    Name = db.Supplier.Where(s => s.Id == x.SupplierId).Select(s => s.Name).FirstOrDefault(),
                })
                : (db, id) => where(db, id).Where(x => !x.IsDeleted).Select(x => new CommonRow
                {
                    Id = x.Id, Code = x.SupplierMaterialCode, Status = x.Status, At = x.EffectiveFrom,
                    Name = db.Ingredient.Where(i => i.Id == x.IngredientId).Select(i => i.Name).FirstOrDefault(),
                }),
            title: "Nguyên liệu nhà cung cấp", screen: "scm-supplier-material");

        private static CommonRelatedGroup Recipes(Func<DatabaseContext, Guid, IQueryable<Recipe>> where) => G<Recipe>("Recipe", "Status",
            (db, id) => where(db, id).Where(x => !x.IsDeleted).Select(x => new CommonRow
            {
                Id = x.Id, Code = x.VersionCode, Status = x.Status, At = x.EffectiveFrom,
                Name = db.Dish.Where(d => d.Id == x.DishId).Select(d => d.Name).FirstOrDefault(),
            }));

        private static CommonRelatedGroup ProductionOrders(Func<DatabaseContext, Guid, IQueryable<ProductionOrder>> where) => G<ProductionOrder>("ProductionOrder", "Status",
            (db, id) => where(db, id).Where(x => !x.IsDeleted).Select(x => new CommonRow { Id = x.Id, Code = x.ProductionOrderNumber ?? x.ProductionOrderNo, Status = x.Status, At = x.PlannedStartDate }), withDate: true);

        private static CommonRelatedGroup ProductionBatches(Func<DatabaseContext, Guid, IQueryable<ProductionBatch>> where) => G<ProductionBatch>("ProductionBatch", "Status",
            (db, id) => where(db, id).Where(x => !x.IsDeleted).Select(x => new CommonRow
            {
                Id = x.Id, Code = x.BatchNumber ?? x.BatchNo, Status = x.Status, At = x.StartDate,
                Name = db.Dish.Where(d => d.Id == x.DishId).Select(d => d.Name).FirstOrDefault(),
            }));

        private static CommonRelatedGroup MaterialIssues(Func<DatabaseContext, Guid, IQueryable<MaterialIssue>> where) => G<MaterialIssue>("MaterialIssue", "Status",
            (db, id) => where(db, id).Where(x => !x.IsDeleted).Select(x => new CommonRow { Id = x.Id, Code = x.IssueNumber ?? x.IssueNo, Status = x.Status, At = x.IssueDate }), withDate: true);

        /// <summary>Quan hệ theo danh sách nguồn (khóa = tên danh sách của <see cref="CommonRegistry"/>). Thứ tự nhóm = thứ tự hiển thị tab.</summary>
        public static readonly IReadOnlyDictionary<string, IReadOnlyList<CommonRelatedGroup>> Related = new Dictionary<string, IReadOnlyList<CommonRelatedGroup>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Customer"] =
            [
                Contracts((db, id) => db.Contract.Where(x => x.CustomerId == id)),
                Orders((db, id) => db.Order.Where(x => x.CustomerId == id)),
                DeliveryOrders((db, id) => db.DeliveryOrder.Where(x => x.CustomerId == id)),
                Complaints((db, id) => db.CustomerComplaint.Where(x => x.CustomerId == id)),
            ],
            ["Contract"] =
            [
                Orders((db, id) => db.Order.Where(x => x.ContractId == id)),
            ],
            ["Order"] =
            [
                DeliveryOrders((db, id) => db.DeliveryOrder.Where(x => x.OrderId == id)),
                G<OrderReconciliation>("OrderReconciliation", "Status", (db, id) => db.OrderReconciliation.Where(x => !x.IsDeleted && x.OrderId == id)
                    .Select(x => new CommonRow { Id = x.Id, Code = "Đối soát", Status = x.Status, At = x.ReconciliationDate }), withDate: true),
                Complaints((db, id) => db.CustomerComplaint.Where(x => x.OrderId == id)),
                G<MenuChangeRequest>("MenuChangeRequest", "RequestStatus", (db, id) => db.MenuChangeRequest.Where(x => !x.IsDeleted && x.OrderId == id)
                    .Select(x => new CommonRow { Id = x.Id, Code = x.RequestNo, Name = x.Reason, Status = x.RequestStatus, At = x.MealDate })),
            ],
            ["Supplier"] =
            [
                PurchaseOrders((db, id) => db.PurchaseOrder.Where(x => x.SupplierId == id)),
                GoodsReceipts((db, id) => db.GoodsReceipt.Where(x => x.SupplierId == id)),
                SupplierMaterials((db, id) => db.SupplierMaterial.Where(x => x.SupplierId == id), showSupplier: false),
                MaterialLots((db, id) => db.MaterialLot.Where(x => x.SupplierId == id)),
                SupplierIssues((db, id) => db.SupplierIssue.Where(x => x.SupplierId == id)),
            ],
            ["Ingredient"] =
            [
                MaterialLots((db, id) => db.MaterialLot.Where(x => x.IngredientId == id)),
                SupplierMaterials((db, id) => db.SupplierMaterial.Where(x => x.IngredientId == id), showSupplier: true),
                Recipes((db, id) => db.Recipe.Where(r => db.RecipeLine.Any(l => l.RecipeId == r.Id && !l.IsDeleted && l.IngredientId == id))),
            ],
            ["PurchaseOrder"] =
            [
                GoodsReceipts((db, id) => db.GoodsReceipt.Where(x => x.PurchaseOrderId == id)),
                G<PurchaseRequest>("PurchaseRequest", "Status", (db, id) => db.PurchaseRequest.Where(x => !x.IsDeleted && db.PurchaseOrder.Any(p => p.Id == id && p.PurchaseRequestId == x.Id))
                    .Select(x => new CommonRow { Id = x.Id, Code = x.RequestNumber ?? x.RequestNo, Status = x.Status, At = x.RequestDate }), withDate: true),
                SupplierIssues((db, id) => db.SupplierIssue.Where(x => x.PurchaseOrderId == id)),
            ],
            ["MaterialLot"] =
            [
                GoodsReceipts((db, id) => db.GoodsReceipt.Where(g => db.GoodsReceiptLine.Any(l => l.GoodsReceiptId == g.Id && db.MaterialLot.Any(m => m.Id == id && m.GoodsReceiptLineId == l.Id)))),
                G<MaterialIssue>("MaterialIssue", "Status", (db, id) => db.MaterialIssue.Where(x => !x.IsDeleted
                        && db.MaterialIssueLine.Any(l => l.MaterialIssueId == x.Id && !l.IsDeleted && l.MaterialLotId == id))
                    .Select(x => new CommonRow { Id = x.Id, Code = x.IssueNumber ?? x.IssueNo, Status = x.Status, At = x.IssueDate }), withDate: true),
                // Lô → mẻ: phiếu xuất có dòng lấy lô này → lệnh sản xuất của phiếu → mẻ của lệnh (truy xuất nguồn gốc xuôi).
                G<ProductionBatch>("ProductionBatch", "Status", (db, id) => db.ProductionBatch.Where(b => !b.IsDeleted
                        && db.MaterialIssue.Any(i => !i.IsDeleted && i.ProductionOrderId == b.ProductionOrderId
                            && db.MaterialIssueLine.Any(l => l.MaterialIssueId == i.Id && !l.IsDeleted && l.MaterialLotId == id)))
                    .Select(x => new CommonRow
                    {
                        Id = x.Id, Code = x.BatchNumber ?? x.BatchNo, Status = x.Status, At = x.StartDate,
                        Name = db.Dish.Where(d => d.Id == x.DishId).Select(d => d.Name).FirstOrDefault(),
                    })),
            ],
            // UI-6 (D-80): tab "Chứng từ liên quan" của các trang chi tiết toàn màn – chỉ khóa ngoại có sẵn (không thêm cột).
            ["GoodsReceipt"] =
            [
                PurchaseOrders((db, id) => db.PurchaseOrder.Where(p => db.GoodsReceipt.Any(g => g.Id == id && g.PurchaseOrderId == p.Id))),
                MaterialLots((db, id) => db.MaterialLot.Where(m => db.GoodsReceiptLine.Any(l => l.Id == m.GoodsReceiptLineId && l.GoodsReceiptId == id && !l.IsDeleted))),
                SupplierIssues((db, id) => db.SupplierIssue.Where(x => x.GoodsReceiptId == id)),
            ],
            ["PurchaseRequest"] =
            [
                PurchaseOrders((db, id) => db.PurchaseOrder.Where(x => x.PurchaseRequestId == id)),
            ],
            ["CustomerComplaint"] =
            [
                Orders((db, id) => db.Order.Where(o => db.CustomerComplaint.Any(c => c.Id == id && c.OrderId == o.Id))),
                DeliveryOrders((db, id) => db.DeliveryOrder.Where(d => db.CustomerComplaint.Any(c => c.Id == id && c.DeliveryOrderId == d.Id))),
            ],
            ["ProductionPlan"] =
            [
                ProductionOrders((db, id) => db.ProductionOrder.Where(x => x.ProductionPlanId == id)),
                ProductionBatches((db, id) => db.ProductionBatch.Where(b => db.ProductionOrder.Any(o => o.Id == b.ProductionOrderId && o.ProductionPlanId == id && !o.IsDeleted))),
            ],
            ["ProductionOrder"] =
            [
                ProductionBatches((db, id) => db.ProductionBatch.Where(x => x.ProductionOrderId == id)),
                MaterialIssues((db, id) => db.MaterialIssue.Where(x => x.ProductionOrderId == id)),
            ],
            ["ProductionBatch"] =
            [
                ProductionOrders((db, id) => db.ProductionOrder.Where(o => db.ProductionBatch.Any(b => b.Id == id && b.ProductionOrderId == o.Id))),
                MaterialIssues((db, id) => db.MaterialIssue.Where(i => db.ProductionBatch.Any(b => b.Id == id && b.ProductionOrderId == i.ProductionOrderId))),
            ],
            ["CustomerDeliveryLocation"] =
            [
                DeliveryOrders((db, id) => db.DeliveryOrder.Where(x => x.CustomerDeliveryLocationId == id)),
            ],
            ["DeliveryRoute"] =
            [
                DeliveryOrders((db, id) => db.DeliveryOrder.Where(x => x.DeliveryRouteId == id)),
                Complaints((db, id) => db.CustomerComplaint.Where(x => x.DeliveryRouteId == id)),
            ],
            ["Dish"] =
            [
                Recipes((db, id) => db.Recipe.Where(x => x.DishId == id)),
                G<StandardCost>("StandardCost", "Status", (db, id) => db.StandardCost.Where(x => !x.IsDeleted && x.DishId == id)
                    .Select(x => new CommonRow { Id = x.Id, Status = x.Status, At = x.EffectiveFrom, Amount = x.CostPerServing }),
                    title: "Giá thành chuẩn", screen: "cost-meal",
                    label: r => string.Join(" – ", new[] { r.At?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), r.Amount?.ToString("#,0", CultureInfo.InvariantCulture) + " đ/suất" }.Where(s => !string.IsNullOrEmpty(s)))),
            ],
            ["Employee"] =
            [
                G<ShiftAssignment>("ShiftAssignment", "AssignmentStatus", (db, id) => db.ShiftAssignment.Where(x => !x.IsDeleted && x.EmployeeId == id)
                    .Select(x => new CommonRow
                    {
                        Id = x.Id, Status = x.AssignmentStatus, At = x.WorkDate,
                        Name = db.Shift.Where(s => s.Id == x.ShiftId).Select(s => s.Name ?? s.Code).FirstOrDefault(),
                    }), title: "Phân ca", screen: "hr-shift-assign", label: r => string.Join(" – ", new[] { r.At?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), r.Name }.Where(s => !string.IsNullOrEmpty(s)))),
                G<LeaveRequest>("LeaveRequest", "RequestStatus", (db, id) => db.LeaveRequest.Where(x => !x.IsDeleted && x.EmployeeId == id)
                    .Select(x => new CommonRow { Id = x.Id, Code = x.RequestNo, Name = x.LeaveTypeCode, Status = x.RequestStatus, At = x.FromDate }), withDate: true),
            ],
        };

        /// <summary>Danh sách nguồn có tab Liên quan.</summary>
        public static CommonResource? FindRelatedSource(string? resource)
            => CommonRegistry.Find(resource) is { } r && Related.ContainsKey(r.Key) ? r : null;

        /// <summary>Nhãn trạng thái theo STATUS_CATALOG (không có → chính mã).</summary>
        public static string? StatusLabel(Type entityType, string? column, string? code)
        {
            if (code == null || column == null)
            {
                return code;
            }
            string table = entityType.GetCustomAttribute<TableAttribute>()?.Name ?? entityType.Name;
            var set = StatusCatalogEffective.All.FirstOrDefault(s => s.Table.Equals(table, StringComparison.OrdinalIgnoreCase) && s.Column.Equals(column, StringComparison.OrdinalIgnoreCase));
            int i = set == null ? -1 : Array.FindIndex(set.Codes, c => c.Equals(code, StringComparison.OrdinalIgnoreCase));
            return i >= 0 && i < set!.Labels.Length ? set.Labels[i] : StatusLabels.Vi(code); // UI-6.10: từ điển chung thay vì mã tiếng Anh
        }

        /// <summary>Nhãn mặc định: mã – tên (– ngày) – rút gọn 120 ký tự.</summary>
        public static string? DefaultLabel(CommonRow row, bool withDate)
        {
            var parts = new List<string?> { row.Code, row.Name };
            if (withDate && row.At != null)
            {
                parts.Add(row.At.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
            }
            string label = string.Join(" – ", parts.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()));
            return label.Length == 0 ? null : label.Length > 120 ? label[..117] + "..." : label;
        }

        private static CommonSearchType S<T>(string key, Func<DatabaseContext, string, IQueryable<CommonRow>> query, bool withDate = false) where T : BaseEntity
        {
            var resource = CommonRegistry.Find(key) ?? throw new InvalidOperationException("Loại tìm kiếm phải có trong CommonRegistry: " + key);
            return new CommonSearchType(resource, typeof(T), query, withDate);
        }

        private static CommonRelatedGroup G<T>(string key, string? statusColumn, Func<DatabaseContext, Guid, IQueryable<CommonRow>> query, bool withDate = false,
            string? title = null, string? screen = null, Func<CommonRow, string?>? label = null) where T : BaseEntity
        {
            var resource = CommonRegistry.Find(key);
            return new CommonRelatedGroup(key, title ?? resource?.Title ?? throw new InvalidOperationException("Thiếu tiêu đề nhóm " + key),
                screen ?? resource!.ScreenId, typeof(T), statusColumn, query, label ?? (r => DefaultLabel(r, withDate)));
        }
    }

    /// <summary>Dòng trung gian (chiếu từ EF) – không ra ngoài API.</summary>
    public sealed class CommonRow
    {
        public Guid Id { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? Status { get; set; }
        public DateTime? At { get; set; }
        public decimal? Amount { get; set; }
    }

    /// <summary>1 loại bản ghi của tìm kiếm toàn cục (màn hình / tiêu đề lấy từ <see cref="CommonRegistry"/>).</summary>
    public sealed class CommonSearchType(CommonResource resource, Type entityType, Func<DatabaseContext, string, IQueryable<CommonRow>> query, bool withDate)
    {
        public CommonResource Resource { get; } = resource;
        public Type EntityType { get; } = entityType;
        public Func<DatabaseContext, string, IQueryable<CommonRow>> Query { get; } = query;
        public bool WithDate { get; } = withDate;
    }

    /// <summary>1 nhóm của tab Liên quan: khóa, tiêu đề màn hình, screen-id (quyền Xem + route), truy vấn theo Id nguồn.</summary>
    public sealed class CommonRelatedGroup(string key, string title, string screenId, Type entityType, string? statusColumn,
        Func<DatabaseContext, Guid, IQueryable<CommonRow>> query, Func<CommonRow, string?> label)
    {
        public string Key { get; } = key;
        public string Title { get; } = title;
        public string ScreenId { get; } = screenId;
        public Type EntityType { get; } = entityType;
        public string? StatusColumn { get; } = statusColumn;
        public Func<DatabaseContext, Guid, IQueryable<CommonRow>> Query { get; } = query;
        public Func<CommonRow, string?> Label { get; } = label;
    }
}
