using AppDelicare.Model.Option;

namespace AppDelicare.Model.Helper
{
    /// <summary>
    /// Lỗi UAT 2026-11-21 (rà soát danh mục): cột lưu MÃ của danh mục tham chiếu <c>ReferenceLists</c> (appsettings – GAP-F-06) phải lấy từ danh mục,
    /// không nhận chữ tự do. MỘT nguồn cho cả máy chủ (kiểm khi Insert / Update – ReferenceListFilter + nghiệp vụ riêng) và giao diện
    /// (MetaData/GetReferenceLists trả kèm danh sách cột → form vẽ ô chọn).
    /// Khóa = "Thực thể.Cột" (tên class C#), giá trị = (tên danh mục, nhãn cột tiếng Việt).
    /// Cột trạng thái (lớp *Status.cs, MetaData/GetStatuses) không nằm ở đây.
    /// </summary>
    public static class ReferenceCatalogs
    {
        public static readonly IReadOnlyDictionary<string, (string List, string Label)> Columns = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["ContractMeal.MealType"] = ("MealType", "Loại suất"),
            ["Forecast.MealType"] = ("MealType", "Loại suất"),
            ["MenuLine.MealType"] = ("MealType", "Loại suất"),
            ["MenuAssignment.MealType"] = ("MealType", "Loại suất"),
            ["OrderLine.MealType"] = ("MealType", "Loại suất"),
            ["PricePolicy.MealType"] = ("MealType", "Loại suất"),
            ["LeaveRequest.LeaveTypeCode"] = ("LeaveType", "Loại nghỉ"),
            ["AttendanceEvent.SourceChannel"] = ("AttendanceSource", "Hình thức chấm công"),
            ["Warehouse.WarehouseType"] = ("WarehouseType", "Loại kho"),
            ["Ingredient.StorageType"] = ("StorageType", "Kiểu bảo quản"),
            ["Supplier.SupplierType"] = ("SupplierType", "Loại nhà cung cấp"),
            ["MealWasteRecord.WasteType"] = ("WasteType", "Loại hủy"),
            ["TemperatureLog.MeasurementType"] = ("TemperatureMeasurementType", "Loại đo nhiệt độ"),
            ["PackingResult.PackagingType"] = ("PackagingType", "Loại khay / hộp"),
            ["PackagingLedger.PackagingType"] = ("PackagingType", "Loại khay / hộp"),
            ["PackagingLedger.TransactionType"] = ("PackagingTransactionType", "Loại phát sinh bao bì"),
            ["CustomerComplaint.SourceChannel"] = ("ComplaintChannel", "Kênh tiếp nhận"),
            ["CustomerComplaint.CategoryCode"] = ("ComplaintCategory", "Phân loại phản ánh"),
            // ORD-OPT-1.2 (D-74 f): loại thực đơn – COMMON = thực đơn chung của tuần; trống / CUSTOMER = thực đơn riêng (gán qua Phân công thực đơn).
            ["Menu.MenuType"] = ("MenuType", "Loại thực đơn"),
        };

        /// <summary>MENU-4.6 (D-75 f2): tên tiếng Việt của danh mục – màn chỉ xem "Danh mục dùng chung" (adm-reference). Danh mục mới chưa có tên ở đây → lấy nhãn cột đầu tiên, rồi tới khóa.</summary>
        public static readonly IReadOnlyDictionary<string, string> ListTitles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["MealType"] = "Loại suất (bữa ăn)",
            ["LeaveType"] = "Loại nghỉ",
            ["AttendanceSource"] = "Hình thức chấm công",
            ["WarehouseType"] = "Loại kho",
            ["StorageType"] = "Kiểu bảo quản",
            ["WasteType"] = "Loại hủy suất ăn",
            ["TemperatureMeasurementType"] = "Loại đo nhiệt độ",
            ["PackagingType"] = "Loại khay / hộp",
            ["PackagingTransactionType"] = "Loại phát sinh khay / hộp",
            ["ReturnInspectionResult"] = "Kết quả kiểm tra khay hộp thu hồi",
            ["SupplierType"] = "Loại nhà cung cấp",
            ["ComplaintChannel"] = "Kênh tiếp nhận phản ánh",
            ["ComplaintCategory"] = "Phân loại phản ánh",
            ["MenuType"] = "Loại thực đơn",
            ["ApprovalOverdueGroups"] = "Nhóm nhận cảnh báo duyệt quá hạn",
        };

        public static string TitleOf(string list)
            => ListTitles.TryGetValue(list, out var t) ? t
                : Columns.Where(c => string.Equals(c.Value.List, list, StringComparison.OrdinalIgnoreCase)).Select(c => c.Value.Label).FirstOrDefault() ?? list;

        /// <summary>Các cột (Thực thể.Cột) dùng danh mục <paramref name="list"/>.</summary>
        public static List<string> ColumnsOf(string list)
            => [.. Columns.Where(c => string.Equals(c.Value.List, list, StringComparison.OrdinalIgnoreCase)).Select(c => c.Key).OrderBy(k => k)];

        public static IReadOnlyList<ReferenceItem> Items(ReferenceListsOptions? options, string list)
            => options?.Lists != null && options.Lists.TryGetValue(list, out var items) && items != null ? items : [];

        /// <summary>
        /// (Mã chuẩn, lỗi). Nhận mã hoặc nhãn, không phân biệt hoa thường → trả MÃ của danh mục. Trống → (null, null) – bắt buộc kiểm ở nơi gọi.
        /// Danh mục chưa cấu hình → không chặn. Giá trị cũ ngoài danh mục giữ được khi KHÔNG đổi (<paramref name="stored"/>).
        /// </summary>
        public static (string? Code, string? Error) Check(ReferenceListsOptions? options, string list, string label, string? value, string? stored = null)
        {
            string v = (value ?? string.Empty).Trim();
            if (v.Length == 0)
            {
                return (null, null);
            }
            var items = Items(options, list);
            if (items.Count == 0)
            {
                return (v, null);
            }
            var hit = items.FirstOrDefault(i => string.Equals(i.Code, v, StringComparison.OrdinalIgnoreCase))
                ?? items.FirstOrDefault(i => string.Equals(i.Label, v, StringComparison.OrdinalIgnoreCase));
            if (hit != null)
            {
                return (hit.Code, null);
            }
            if (stored != null && string.Equals(stored.Trim(), v, StringComparison.Ordinal))
            {
                return (stored.Trim(), null);
            }
            string allowed = string.Join(", ", items.Select(i => $"{i.Code} – {i.Label}"));
            return (null, string.Format(ConstantHelper.Message.ReferenceValueNotInCatalog, label, v, allowed));
        }

        /// <summary>Kiểm theo khóa cột trong <see cref="Columns"/> (vd. "ContractMeal.MealType").</summary>
        public static (string? Code, string? Error) CheckColumn(ReferenceListsOptions? options, string column, string? value, string? stored = null)
            => Columns.TryGetValue(column, out var c) ? Check(options, c.List, c.Label, value, stored) : (value?.Trim(), null);
    }
}
