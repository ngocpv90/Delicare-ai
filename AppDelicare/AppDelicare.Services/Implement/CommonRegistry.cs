#nullable enable
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using AppDelicare.Data.Connection;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Request.Custom;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// BE2.7 (spec 2.1 summary / export, 2.2 history, 2.4 files): DANH SÁCH TRẮNG các danh sách được dùng API chung.
    /// Không bao giờ nhận tên bảng / cột tùy ý từ client: tên danh sách tra trong bảng này, cột lấy từ đây (đã kiểm có thật trong entity – test
    /// <c>CommonRegistryTests</c>), truy vấn qua EF (tham số hóa). Màn hình (screen-id) khớp <c>ScreenMap.ByClass</c> (test kiểm).
    /// Cột không xuất: khóa / audit / JSON nội bộ / mật khẩu / tọa độ / cột deprecated (dùng cột mới – DEPRECATED_COLUMNS.md).
    /// </summary>
    public static class CommonRegistry
    {
        /// <summary>Tiêu đề cột dùng chung (cột không ghi tiêu đề riêng).</summary>
        private static readonly Dictionary<string, string> Headers = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Code"] = "Mã", ["Name"] = "Tên", ["Status"] = "Trạng thái", ["Note"] = "Ghi chú", ["Email"] = "Email", ["Address"] = "Địa chỉ",
            ["CustomerId"] = "Khách hàng", ["SupplierId"] = "Nhà cung cấp", ["WarehouseId"] = "Kho", ["IngredientId"] = "Nguyên liệu", ["ShiftId"] = "Ca",
            ["DishId"] = "Món", ["OrderId"] = "Đơn hàng", ["EmployeeId"] = "Nhân viên", ["VersionNo"] = "Phiên bản", ["EffectiveFrom"] = "Hiệu lực từ",
            ["EffectiveTo"] = "Hiệu lực đến", ["RequestStatus"] = "Trạng thái", ["Reason"] = "Lý do", ["Severity"] = "Mức độ", ["Resolution"] = "Kết quả xử lý",
            ["PlannedQty"] = "Số suất kế hoạch", ["DeliveredQuantity"] = "Suất giao", ["AcceptedQuantity"] = "Suất nhận", ["RejectedQuantity"] = "Suất từ chối",
            ["DeliveryDate"] = "Ngày giao", ["StartDate"] = "Bắt đầu", ["EndDate"] = "Kết thúc", ["FromDate"] = "Từ ngày", ["ToDate"] = "Đến ngày",
            ["Phone"] = "Điện thoại", ["MealDate"] = "Ngày ăn", ["Quantity"] = "Số lượng",
        };

        public static readonly IReadOnlyList<CommonResource> All =
        [
            R<Customer>("Khách hàng", "crm-customer", "Status", null, "Code,Name,PhoneNumber,TaxCode",
                "Code:Mã KH,Name:Tên khách hàng,CustomerType:Loại khách,PhoneNumber:Điện thoại,Email,Address,TaxCode:Mã số thuế,Status,Note", attachable: true),
            R<Contract>("Hợp đồng", "crm-contract", "Status", "EffectiveFrom", "ContractNumber,Name",
                "ContractNumber:Số hợp đồng,Name:Tên hợp đồng,CustomerId,SignedDate:Ngày ký,EffectiveFrom,EffectiveTo,ContractValue:Giá trị hợp đồng,Status,Note", attachable: true),
            // UI-6.4 (D-83): file phụ lục đã ký gắn theo dòng điều chỉnh giá – quyền theo màn Hợp đồng.
            R<PricePolicy>("Phụ lục điều chỉnh giá", "crm-contract", "Status", "EffectiveFrom", "Code,Name,MealType",
                "Code:Số phụ lục,MealType:Loại suất,UnitPrice:Đơn giá mới,EffectiveFrom:Áp dụng từ,EffectiveTo:Đến ngày,Status,Name:Lý do", attachable: true),
            R<Forecast>("Dự báo suất ăn", "crm-forecast", "Status", "ForecastDate", "MealType,Note",
                "ForecastDate:Ngày,CustomerId,ShiftId,MealType:Bữa,ForecastQuantity:Số suất dự báo,VersionNo,Status,Note"),
            R<Order>("Đơn đặt suất", "crm-order", "Status", "MealDate", "OrderNumber,Note",
                "OrderNumber:Số đơn,OrderDate:Ngày đặt,MealDate,CustomerId,ContractId:Hợp đồng,TotalQuantity:Tổng suất,TotalAmount:Thành tiền,Status,Note",
                attachable: true, children: [C<OrderLine>("OrderId")]),
            R<OrderReconciliation>("Đối soát suất ăn", "crm-reconciliation", "Status", "ReconciliationDate", "Note,Remark",
                "ReconciliationDate:Ngày đối soát,OrderId,OrderedQuantity:Suất đặt,ProducedQty:Suất sản xuất,PackedQty:Suất đóng gói,DeliveredQuantity,AcceptedQuantity,VariancePercent:% chênh lệch,FinalAmount:Giá trị chốt,Status,Note",
                attachable: true),
            R<Menu>("Thực đơn", "menu-menu", "Status", "EffectiveFrom", "Code,Name",
                "Code,Name:Tên thực đơn,MenuType:Loại thực đơn,VersionNo,EffectiveFrom,EffectiveTo,Status,Note", children: [C<MenuLine>("MenuId")]),
            R<MenuChangeRequest>("Yêu cầu đổi món", "menu-menu", "RequestStatus", "MealDate", "RequestNo,Reason",
                "RequestNo:Số phiếu,MealDate,OrderId,ShiftId,OriginalDishId:Món cũ,RequestedDishId:Món mới,Quantity:Số suất,Reason,RequestStatus"),
            R<Recipe>("Công thức", "menu-recipe", "Status", "EffectiveFrom", "VersionCode,Note",
                "DishId,VersionCode:Mã phiên bản,VersionNo,YieldQuantity:Sản lượng chuẩn,EffectiveFrom,EffectiveTo,Status,Note", children: [C<RecipeLine>("RecipeId")]),
            R<Dish>("Món ăn", "menu-dish", "Status", null, "Code,Name",
                "Code:Mã món,Name:Tên món,DishCategoryId:Nhóm món,MealCategory:Loại bữa,StandardServingSize:Định lượng chuẩn,StandardCost:Giá vốn chuẩn,Status,Note", attachable: true),
            R<Ingredient>("Nguyên liệu", "scm-ingredient", "Status", null, "Code,Name",
                "Code:Mã NL,Name:Tên nguyên liệu,IngredientCategoryId:Nhóm NL,BaseUnitId:ĐVT,StorageType:Bảo quản,ShelfLifeDays:Hạn dùng (ngày),SafetyStock:Tồn an toàn,Status,Note", attachable: true),
            R<Supplier>("Nhà cung cấp", "scm-supplier", "Status", null, "Code,Name,Phone,TaxCode",
                "Code:Mã NCC,Name:Tên nhà cung cấp,SupplierType:Loại NCC,TaxCode:Mã số thuế,Phone,Email,Address,ContactName:Người liên hệ,Status,Note", attachable: true),
            R<PurchaseRequest>("Đề nghị mua", "scm-pr", "Status", "RequestDate", "RequestNumber,Note",
                "RequestNumber:Số đề nghị,RequestDate:Ngày đề nghị,RequiredDate:Ngày cần hàng,RequestedByEmployeeId:Người đề nghị,Status,Note",
                attachable: true, children: [C<PurchaseRequestLine>("PurchaseRequestId")]),
            R<PurchaseOrder>("Đơn mua hàng", "scm-po", "Status", "OrderDate", "PurchaseOrderNumber,Note",
                "PurchaseOrderNumber:Số PO,OrderDate:Ngày đặt,SupplierId,ExpectedDeliveryDate:Ngày giao dự kiến,TotalAmount:Tổng tiền (chưa VAT),Status,Note",
                attachable: true, children: [C<PurchaseOrderLine>("PurchaseOrderId")]),
            R<GoodsReceipt>("Phiếu nhập kho", "scm-gr", "Status", "ReceiptDate", "ReceiptNumber,Note",
                "ReceiptNumber:Số phiếu nhập,ReceiptDate:Ngày nhập,SupplierId,PurchaseOrderId:Đơn mua,WarehouseId,Status,Note",
                attachable: true, children: [C<GoodsReceiptLine>("GoodsReceiptId")]),
            R<MaterialLot>("Lô nguyên liệu", "scm-lot", "Status", "ReceivedDate", "LotNumber,Note",
                "LotNumber:Số lô,IngredientId,SupplierId,ManufactureDate:NSX,ExpiryDate:HSD,ReceivedDate:Ngày nhận,QualityStatus:Chất lượng,Status,Note", attachable: true),
            R<MaterialIssue>("Phiếu xuất kho", "scm-issue", "Status", "IssueDate", "IssueNumber,Note",
                "IssueNumber:Số phiếu xuất,IssueDate:Ngày xuất,ProductionOrderId:Lệnh sản xuất,WarehouseId,Status,Note",
                attachable: true, children: [C<MaterialIssueLine>("MaterialIssueId")]),
            R<StockCount>("Kiểm kê kho", "scm-stockcount", "Status", "CountDate", "CountNumber,Note",
                "CountNumber:Số phiếu kiểm kê,CountDate:Ngày kiểm kê,WarehouseId,CountType:Loại kiểm kê,ResponsibleEmployeeId:Người phụ trách,Status,Note",
                attachable: true, children: [C<StockCountLine>("StockCountId")]),
            R<ProductionPlan>("Kế hoạch sản xuất", "prod-plan", "Status", "ProductionDate", "PlanNumber",
                "PlanNumber:Số kế hoạch,ProductionDate:Ngày sản xuất,ShiftId,OrganizationUnitId:Bếp,Status", attachable: true, children: [C<ProductionPlanLine>("ProductionPlanId")]),
            R<ProductionOrder>("Lệnh sản xuất", "prod-order", "Status", "PlannedStartDate", "ProductionOrderNumber,Note",
                "ProductionOrderNumber:Số lệnh,PlannedStartDate:Bắt đầu kế hoạch,DishId,ShiftId,PlannedQty,ActualQty:Số suất thực tế,Status,Note", attachable: true),
            R<ProductionBatch>("Mẻ sản xuất", "prod-batch", "Status", "StartDate", "BatchNumber",
                "BatchNumber:Số mẻ,StartDate,EndDate,DishId,PlannedQuantity:Phần kế hoạch,ActualQuantity:Phần thực tế,AcceptedQty:Phần đạt,RejectedQty:Phần loại,QualityStatus:Kết quả QC,Status",
                attachable: true),
            R<PackingOrder>("Lệnh đóng gói", "log-packing-order", "Status", "PackingDate", "PackingOrderNumber,Note",
                "PackingOrderNumber:Số lệnh đóng gói,PackingDate:Ngày đóng gói,ProductionBatchId:Mẻ,OrderId,PlannedQty,Status,Note"),
            R<DeliveryRoute>("Chuyến giao", "log-route", "Status", "DeliveryDate", "RouteNo,Code,Name",
                "RouteNo:Số chuyến,DeliveryDate,Name:Tên chuyến,VehicleId:Xe,DriverId:Tài xế,PlannedStart:Xuất phát kế hoạch,ActualStart:Xuất phát thực tế,Status",
                attachable: true),
            R<DeliveryOrder>("Đơn giao", "log-delivery-order", "Status", "DeliveryDate", "DeliveryNumber,Note",
                "DeliveryNumber:Số phiếu giao,DeliveryDate,CustomerId,CustomerDeliveryLocationId:Điểm giao,OrderId,DeliveryRouteId:Chuyến,PlannedDeliveryTime:Giờ giao dự kiến,ActualDeliveryTime:Giờ giao thực tế,Status,Note",
                attachable: true),
            R<ProofOfDelivery>("Xác nhận đã giao", "log-pod", null, "ConfirmedAt", "ReceiverName,Remark",
                "DeliveryOrderId:Phiếu giao,ReceivedAt:Thời điểm nhận,ReceiverName:Người nhận,ReceiverPhone:Điện thoại người nhận,DeliveredQuantity,AcceptedQuantity,RejectedQuantity,Remark:Ghi chú",
                attachable: true),
            R<CustomerComplaint>("Phản ánh khách hàng", "log-complaint", "ComplaintStatus", "ReceivedAt", "ComplaintNo,Description",
                "ComplaintNo:Số phản ánh,ReceivedAt:Ngày nhận,CustomerId,CategoryCode:Nhóm phản ánh,Severity,SlaDueAt:Hạn xử lý,ComplaintStatus:Trạng thái,ResolvedAt:Ngày xử lý xong,Resolution",
                attachable: true),
            R<SupplierIssue>("Sự cố nhà cung cấp", "qc-supplier-issue", "IssueStatus", "ReportedAt", "Description,Resolution",
                "ReportedAt:Ngày ghi nhận,SupplierId,GoodsReceiptId:Phiếu nhập,IssueType:Loại sự cố,Severity,DueAt:Hạn xử lý,IssueStatus:Trạng thái,Resolution",
                attachable: true),
            R<ContainerReturn>("Thu hồi hộp / khay", "log-container-return", "Status", "ReturnDate", "Note",
                "ReturnDate:Ngày thu hồi,CustomerId,DeliveryOrderId:Phiếu giao,ExpectedQuantity:Số phải thu,ReturnedQuantity:Số thu được,DamagedQuantity:Số hỏng,Status,Note"),
            R<CleaningBatch>("Mẻ vệ sinh khay", "qc-cleaning", "Status", "StartDate", "CleaningBatchNumber",
                "CleaningBatchNumber:Số mẻ vệ sinh,StartDate,EndDate,ContainerQuantity:Số khay,QcResult:Kết quả,Status"),
            R<ApprovalRequest>("Yêu cầu duyệt", "wf-inbox", "RequestStatus", "RequestedAt", "RequestNo,BusinessType",
                "RequestNo:Số yêu cầu,BusinessType:Loại nghiệp vụ,RequestedAt:Ngày gửi,RequestedBy:Người gửi,CurrentStepNo:Cấp hiện tại,DueAt:Hạn duyệt,RequestStatus,CompletedAt:Hoàn tất lúc"),
            R<LeaveRequest>("Đơn nghỉ", "hr-leave", "RequestStatus", "FromDate", "RequestNo,Reason",
                "RequestNo:Số đơn,EmployeeId,LeaveTypeCode:Loại nghỉ,FromDate,ToDate,Reason,RequestStatus,SubmittedAt:Ngày lập"),
            R<ShiftSwapRequest>("Đơn đổi ca", "hr-swap", "RequestStatus", "SubmittedAt", "RequestNo,Reason",
                "RequestNo:Số đơn,RequesterEmployeeId:Người đổi,TargetEmployeeId:Người nhận ca,Reason,RequestStatus,SubmittedAt:Ngày lập"),
            // UI-6 (D-80): trang chi tiết Điểm giao – có Lịch sử / Tài liệu / xuất Excel như các danh sách khác.
            R<CustomerDeliveryLocation>("Điểm giao", "crm-location", "Status", null, "Code,Name,Address,ContactName,ContactPhone",
                "Code:Mã điểm giao,Name:Tên điểm giao,CustomerId,Address:Địa chỉ,ContactName:Người liên hệ,ContactPhone:Điện thoại,DeliveryWindowFrom:Giao từ,DeliveryWindowTo:Giao đến,Status,DeliveryInstructions:Hướng dẫn giao", attachable: true),
            R<Employee>("Nhân viên", "adm-employee", "Status", null, "Code,FullName,Phone",
                "Code:Mã nhân viên,FullName:Họ tên,OrganizationUnitId:Đơn vị,Phone,Email,HireDate:Ngày vào làm,Status", attachable: true),
            R<Vehicle>("Xe giao hàng", "log-vehicle", "Status", null, "Code,LicensePlate",
                "Code:Mã xe,LicensePlate:Biển số,VehicleType:Loại xe,Capacity:Tải (suất),Status,Note"),
            R<Warehouse>("Kho", "scm-warehouse", "Status", null, "Code,Name",
                "Code:Mã kho,Name:Tên kho,WarehouseType:Loại kho,Address,Status,Note"),
        ];

        private static readonly Dictionary<string, CommonResource> ByKey = All.ToDictionary(r => r.Key, StringComparer.OrdinalIgnoreCase);

        /// <summary>Tra danh sách theo tên (tên controller / class); không có → null (client không thể tự chọn bảng ngoài danh sách).</summary>
        public static CommonResource? Find(string? resource)
            => string.IsNullOrWhiteSpace(resource) ? null : ByKey.GetValueOrDefault(resource.Trim());

        /// <summary>Cột khóa ngoại → nhãn hiển thị khi xuất (tên / mã của bản ghi được tham chiếu).</summary>
        public static readonly IReadOnlyDictionary<string, CommonLookup> Lookups = new Dictionary<string, CommonLookup>(StringComparer.OrdinalIgnoreCase)
        {
            ["CustomerId"] = L<Customer>(x => x.Name ?? x.Code),
            ["SupplierId"] = L<Supplier>(x => x.Name ?? x.Code),
            ["WarehouseId"] = L<Warehouse>(x => x.Name ?? x.Code),
            ["IngredientId"] = L<Ingredient>(x => string.Join(" – ", new[] { x.Code, x.Name }.Where(s => !string.IsNullOrEmpty(s)))),
            ["EmployeeId"] = L<Employee>(x => x.FullName ?? x.Code),
            ["RequestedByEmployeeId"] = L<Employee>(x => x.FullName ?? x.Code),
            ["ResponsibleEmployeeId"] = L<Employee>(x => x.FullName ?? x.Code),
            ["RequesterEmployeeId"] = L<Employee>(x => x.FullName ?? x.Code),
            ["TargetEmployeeId"] = L<Employee>(x => x.FullName ?? x.Code),
            ["DriverId"] = L<Employee>(x => x.FullName ?? x.Code),
            ["VehicleId"] = L<Vehicle>(x => x.Code ?? x.LicensePlate),
            ["ShiftId"] = L<Shift>(x => x.Name ?? x.Code),
            ["DishId"] = L<Dish>(x => x.Name ?? x.Code),
            ["OriginalDishId"] = L<Dish>(x => x.Name ?? x.Code),
            ["RequestedDishId"] = L<Dish>(x => x.Name ?? x.Code),
            ["DishCategoryId"] = L<DishCategory>(x => x.Name),
            ["IngredientCategoryId"] = L<IngredientCategory>(x => x.Name),
            ["BaseUnitId"] = L<UnitMeasure>(x => x.Name ?? x.Code),
            ["OrderId"] = L<Order>(x => x.OrderNumber ?? x.OrderNo),
            ["ContractId"] = L<Contract>(x => x.ContractNumber ?? x.ContractNo),
            ["PurchaseOrderId"] = L<PurchaseOrder>(x => x.PurchaseOrderNumber ?? x.PONo),
            ["GoodsReceiptId"] = L<GoodsReceipt>(x => x.ReceiptNumber ?? x.ReceiptNo),
            ["ProductionOrderId"] = L<ProductionOrder>(x => x.ProductionOrderNumber ?? x.ProductionOrderNo),
            ["ProductionBatchId"] = L<ProductionBatch>(x => x.BatchNumber ?? x.BatchNo),
            ["DeliveryOrderId"] = L<DeliveryOrder>(x => x.DeliveryNumber ?? x.DeliveryNo),
            ["DeliveryRouteId"] = L<DeliveryRoute>(x => x.RouteNo ?? x.Code),
            ["CustomerDeliveryLocationId"] = L<CustomerDeliveryLocation>(x => x.Name ?? x.Code),
            ["OrganizationUnitId"] = L<OrganizationUnit>(x => x.Name),
            ["RequestedBy"] = L<User>(x => x.UserName),
        };

        public static string HeaderOf(string column) => Headers.GetValueOrDefault(column, column);

        private static CommonResource R<T>(string title, string screen, string? status, string? date, string search, string columns,
            bool attachable = false, CommonChild[]? children = null) where T : BaseEntity
            => new CommonResource<T>(typeof(T).Name, title, screen, status, date,
                search.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                columns.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(c => c.Split(':', 2)).Select(p => new CommonColumn(p[0].Trim(), p.Length > 1 ? p[1].Trim() : HeaderOf(p[0].Trim()))).ToArray(),
                attachable, children ?? []);

        private static CommonChild C<T>(string foreignKey) where T : BaseEntity => new CommonChild<T>(foreignKey);

        private static CommonLookup L<T>(Func<T, string?> label) where T : BaseEntity => new CommonLookup<T>(label);
    }

    public sealed record CommonColumn(string Property, string Header);

    /// <summary>1 danh sách trong danh sách trắng – cột / truy vấn đều cố định trong code.</summary>
    public abstract class CommonResource(string key, string title, string screenId, string? statusColumn, string? dateColumn,
        string[] searchColumns, CommonColumn[] exportColumns, bool attachable, CommonChild[] children)
    {
        public string Key { get; } = key;
        public string Title { get; } = title;
        public string ScreenId { get; } = screenId;
        public string? StatusColumn { get; } = statusColumn;
        public string? DateColumn { get; } = dateColumn;
        public IReadOnlyList<string> SearchColumns { get; } = searchColumns;
        public IReadOnlyList<CommonColumn> ExportColumns { get; } = exportColumns;
        public bool Attachable { get; } = attachable;
        public IReadOnlyList<CommonChild> Children { get; } = children;
        public abstract Type EntityType { get; }
        /// <summary>Tên bảng SQL (thuộc tính [Table]) – khóa của StatusCatalog.</summary>
        public string TableName => EntityType.GetCustomAttribute<TableAttribute>()?.Name ?? EntityType.Name;

        public abstract Task<List<(string? Status, int Count)>> CountByStatusAsync(DatabaseContext db, CommonListRequest request);
        public abstract Task<int> CountAsync(DatabaseContext db, CommonListRequest request);
        public abstract Task<List<BaseEntity>> ListAsync(DatabaseContext db, CommonListRequest request, int take);
        public abstract Task<bool> ExistsAsync(DatabaseContext db, Guid id);

        /// <summary>Nhãn trạng thái theo STATUS_CATALOG (không có → chính mã).</summary>
        public string? StatusLabel(string? code)
        {
            if (code == null || StatusColumn == null)
            {
                return code;
            }
            var set = StatusCatalogEffective.All.FirstOrDefault(s => s.Table.Equals(TableName, StringComparison.OrdinalIgnoreCase) && s.Column.Equals(StatusColumn, StringComparison.OrdinalIgnoreCase));
            int i = set == null ? -1 : Array.FindIndex(set.Codes, c => c.Equals(code, StringComparison.OrdinalIgnoreCase));
            return i >= 0 && i < set!.Labels.Length ? set.Labels[i] : StatusLabels.Vi(code); // UI-6.10: từ điển chung thay vì mã tiếng Anh
        }

        public IReadOnlyList<string> CatalogStatuses()
            => StatusCatalogEffective.All.FirstOrDefault(s => StatusColumn != null && s.Table.Equals(TableName, StringComparison.OrdinalIgnoreCase)
                && s.Column.Equals(StatusColumn, StringComparison.OrdinalIgnoreCase))?.Codes ?? [];
    }

    public sealed class CommonResource<T>(string key, string title, string screenId, string? statusColumn, string? dateColumn,
        string[] searchColumns, CommonColumn[] exportColumns, bool attachable, CommonChild[] children)
        : CommonResource(key, title, screenId, statusColumn, dateColumn, searchColumns, exportColumns, attachable, children) where T : BaseEntity
    {
        public override Type EntityType => typeof(T);

        /// <summary>Bộ lọc chung: chưa xóa, tìm chữ trên cột tìm kiếm, khoảng ngày trên cột ngày, (tùy chọn) trạng thái. Giá trị đi qua biến → tham số SQL.</summary>
        private IQueryable<T> Filtered(DatabaseContext db, CommonListRequest request, bool withStatus)
        {
            IQueryable<T> query = db.Set<T>().AsNoTracking().Where(e => !e.IsDeleted);
            var p = Expression.Parameter(typeof(T), "e");
            string? text = request.SearchText?.Trim();
            if (!string.IsNullOrEmpty(text) && SearchColumns.Count > 0)
            {
                var contains = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
                Expression? any = null;
                foreach (var column in SearchColumns)
                {
                    var prop = Expression.Property(p, column);
                    var match = Expression.AndAlso(Expression.NotEqual(prop, Expression.Constant(null, typeof(string))),
                        Expression.Call(prop, contains, Holder(text)));
                    any = any == null ? match : Expression.OrElse(any, match);
                }
                query = query.Where(Expression.Lambda<Func<T, bool>>(any!, p));
            }
            if (DateColumn != null && (request.FromDate != null || request.ToDate != null))
            {
                Expression date = Expression.Convert(Expression.Property(p, DateColumn), typeof(DateTime?));
                if (request.FromDate is { } from)
                {
                    query = query.Where(Expression.Lambda<Func<T, bool>>(Expression.GreaterThanOrEqual(date, Holder<DateTime?>(from.Date)), p));
                }
                if (request.ToDate is { } to)
                {
                    query = query.Where(Expression.Lambda<Func<T, bool>>(Expression.LessThan(date, Holder<DateTime?>(to.Date.AddDays(1))), p));
                }
            }
            if (withStatus && StatusColumn != null && !string.IsNullOrWhiteSpace(request.Status))
            {
                query = query.Where(Expression.Lambda<Func<T, bool>>(Expression.Equal(Expression.Property(p, StatusColumn), Holder<string?>(request.Status.Trim())), p));
            }
            return query;
        }

        public override async Task<List<(string? Status, int Count)>> CountByStatusAsync(DatabaseContext db, CommonListRequest request)
        {
            var p = Expression.Parameter(typeof(T), "e");
            var selector = Expression.Lambda<Func<T, string?>>(Expression.Property(p, StatusColumn!), p);
            var rows = await Filtered(db, request, false).Select(selector).GroupBy(s => s).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
            return rows.Select(r => (r.Key, r.Count)).ToList();
        }

        public override Task<int> CountAsync(DatabaseContext db, CommonListRequest request) => Filtered(db, request, true).CountAsync();

        public override async Task<List<BaseEntity>> ListAsync(DatabaseContext db, CommonListRequest request, int take)
            => [.. await Filtered(db, request, true).OrderByDescending(e => e.CreatedDate).ThenBy(e => e.Id).Take(take).ToListAsync()];

        public override Task<bool> ExistsAsync(DatabaseContext db, Guid id) => db.Set<T>().AsNoTracking().AnyAsync(e => e.Id == id && !e.IsDeleted);

        /// <summary>Giá trị qua thuộc tính của đối tượng giữ chỗ → EF sinh tham số SQL (không nhúng chữ vào câu lệnh).</summary>
        private static MemberExpression Holder<TV>(TV value) => Expression.Property(Expression.Constant(new ValueHolder<TV>(value)), nameof(ValueHolder<TV>.Value));

        private sealed class ValueHolder<TV>(TV value)
        {
            public TV Value { get; } = value;
        }
    }

    /// <summary>Bảng dòng con của chứng từ (dòng đơn, dòng phiếu…) – lịch sử gộp cả thay đổi của dòng.</summary>
    public abstract class CommonChild(string foreignKey)
    {
        public string ForeignKey { get; } = foreignKey;
        public abstract Type EntityType { get; }
        public abstract Task<List<Guid>> IdsAsync(DatabaseContext db, Guid parentId);
    }

    public sealed class CommonChild<T>(string foreignKey) : CommonChild(foreignKey) where T : BaseEntity
    {
        public override Type EntityType => typeof(T);

        public override Task<List<Guid>> IdsAsync(DatabaseContext db, Guid parentId)
        {
            var p = Expression.Parameter(typeof(T), "e");
            var fk = Expression.Convert(Expression.Property(p, ForeignKey), typeof(Guid?));
            Guid? parent = parentId;
            Expression<Func<Guid?>> value = () => parent;
            var match = Expression.Lambda<Func<T, bool>>(Expression.Equal(fk, value.Body), p);
            return db.Set<T>().AsNoTracking().Where(match).Select(e => e.Id).ToListAsync();
        }
    }

    /// <summary>Nhãn của bản ghi được tham chiếu (cột khóa ngoại khi xuất Excel).</summary>
    public abstract class CommonLookup
    {
        public abstract Type EntityType { get; }
        public abstract Task<Dictionary<Guid, string>> LabelsAsync(DatabaseContext db, IReadOnlyCollection<Guid> ids);
    }

    public sealed class CommonLookup<T>(Func<T, string?> label) : CommonLookup where T : BaseEntity
    {
        public override Type EntityType => typeof(T);

        public override async Task<Dictionary<Guid, string>> LabelsAsync(DatabaseContext db, IReadOnlyCollection<Guid> ids)
        {
            var result = new Dictionary<Guid, string>();
            // Từng nhóm 500 Id – không phụ thuộc mức tương thích SQL Server của OPENJSON, không vượt 2 100 tham số.
            foreach (var chunk in ids.Distinct().Chunk(500))
            {
                var list = chunk.ToList();
                foreach (var e in await db.Set<T>().AsNoTracking().Where(x => list.Contains(x.Id)).ToListAsync())
                {
                    result[e.Id] = label(e) ?? string.Empty;
                }
            }
            return result;
        }
    }

    /// <summary>Định dạng giá trị ô khi xuất (ngày dd/MM/yyyy [HH:mm], giờ HH:mm, có / không).</summary>
    public static class CommonFormat
    {
        public static string Text(object? value) => value switch
        {
            null => string.Empty,
            DateTime d => d.TimeOfDay == TimeSpan.Zero ? d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : d.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
            TimeSpan t => t.ToString(@"hh\:mm", CultureInfo.InvariantCulture),
            bool b => b ? "Có" : "Không",
            decimal m => m.ToString("0.##", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
    }
}
