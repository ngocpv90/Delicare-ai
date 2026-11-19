using System.Reflection;

namespace AppDelicare.Model.Helper
{
    /// <summary>
    /// UG-1 C-10: danh mục trạng thái HIỆU LỰC = theo code. Với mỗi cột có lớp trạng thái máy chủ (<c>*Status.cs</c>), mã + nhãn lấy từ chính lớp đó
    /// (hằng chuỗi có <c>Label(mã) ≠ mã</c>, theo thứ tự khai báo) – vì máy chủ chỉ ghi các mã này. Cột chưa có lớp giữ theo
    /// <see cref="StatusCatalog"/> (sinh từ docs/reference/STATUS_CATALOG.md). API MetaData/GetStatuses, thống kê trạng thái, tab Liên quan và
    /// giao diện (chế độ thật) đều đọc danh mục này → đổi trạng thái ở code là mọi nơi khớp theo.
    /// </summary>
    public static class StatusCatalogEffective
    {
        /// <summary>Lớp trạng thái → (Bảng, Cột). Thêm lớp trạng thái mới cho 1 cột → thêm 1 dòng ở đây.</summary>
        private static readonly (Type Type, string Table, string Column)[] Map =
        [
            (typeof(OrderStatus), "Order", "Status"),
            (typeof(ContractStatus), "Contract", "Status"),
            (typeof(ForecastStatus), "Forecast", "Status"),
            (typeof(MenuStatus), "Menu", "Status"),
            (typeof(MenuChangeStatus), "MenuChangeRequest", "RequestStatus"),
            (typeof(RecipeStatus), "Recipe", "Status"),
            (typeof(ReconciliationStatus), "OrderReconciliation", "Status"),
            (typeof(MaterialRequirementStatus), "MaterialRequirement", "Status"),
            (typeof(PurchaseRequestStatus), "PurchaseRequest", "Status"),
            (typeof(PurchaseOrderStatus), "PurchaseOrder", "Status"),
            (typeof(GoodsReceiptStatus), "GoodsReceipt", "Status"),
            (typeof(MaterialLotStatus), "MaterialLot", "Status"),
            (typeof(MaterialReservationStatus), "MaterialReservation", "Status"),
            (typeof(MaterialIssueStatus), "MaterialIssue", "Status"),
            (typeof(StockCountStatus), "StockCount", "Status"),
            (typeof(ProductionPlanStatus), "ProductionPlan", "Status"),
            (typeof(ProductionOrderStatus), "ProductionOrder", "Status"),
            (typeof(ProductionBatchStatus), "ProductionBatche", "Status"),
            (typeof(BatchQcStatus), "ProductionBatche", "QcStatus"),
            (typeof(PackingOrderStatus), "PackingOrder", "Status"),
            (typeof(DeliveryRouteStatus), "DeliveryRoute", "Status"),
            (typeof(DeliveryStopStatus), "DeliveryStop", "StopStatus"),
            (typeof(DeliveryOrderStatus), "DeliveryOrder", "Status"),
            (typeof(DeliveryLoadingStatus), "DeliveryLoading", "Status"),
            (typeof(DeliveryExecutionStatus), "DeliveryExecution", "Status"),
            (typeof(ContainerReturnStatus), "ContainerReturn", "Status"),
            (typeof(CleaningBatchStatus), "CleaningBatche", "Status"),
            (typeof(ComplaintStatus), "CustomerComplaint", "ComplaintStatus"),
            (typeof(SupplierIssueStatus), "SupplierIssue", "IssueStatus"),
            (typeof(ShiftAssignmentStatus), "ShiftAssignment", "AssignmentStatus"),
            (typeof(LeaveRequestStatus), "LeaveRequest", "RequestStatus"),
            (typeof(ShiftSwapStatus), "ShiftSwapRequest", "RequestStatus"),
            (typeof(EmployeeStatus), "Employee", "Status"),
        ];

        public const string CodeSource = "Theo code (*Status.cs)";

        public static IReadOnlyList<StatusCatalog.Item> All { get; } = Build();

        private static List<StatusCatalog.Item> Build()
        {
            var fromCode = new Dictionary<string, StatusCatalog.Item>(StringComparer.OrdinalIgnoreCase);
            foreach (var (type, table, column) in Map)
            {
                var label = type.GetMethod("Label", BindingFlags.Public | BindingFlags.Static, [typeof(string)]);
                if (label == null)
                {
                    continue;
                }
                var codes = new List<string>();
                var labels = new List<string>();
                foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral && f.FieldType == typeof(string)))
                {
                    string code = (string)f.GetRawConstantValue()!;
                    string text = label.Invoke(null, [code]) as string ?? code;
                    if (text != code && !codes.Contains(code))
                    {
                        codes.Add(code);
                        labels.Add(text);
                    }
                }
                if (codes.Count > 0)
                {
                    fromCode[table + "." + column] = new StatusCatalog.Item(table, column, [.. codes], [.. labels], CodeSource);
                }
            }
            var list = StatusCatalog.All.Select(x => fromCode.Remove(x.Key, out var code) ? code : x).ToList();
            list.AddRange(fromCode.Values);   // cột có lớp trạng thái nhưng STATUS_CATALOG.md chưa có (vd. DeliveryOrder, CleaningBatche)
            return list;
        }

        public static StatusCatalog.Item? Find(string table, string column)
            => All.FirstOrDefault(s => s.Table.Equals(table, StringComparison.OrdinalIgnoreCase) && s.Column.Equals(column, StringComparison.OrdinalIgnoreCase));
    }
}
