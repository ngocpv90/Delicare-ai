namespace AppDelicare.Model.Option
{
    /// <summary>
    /// T45B.5 (GAP-F-06, Gap Register sheet 08): danh mục tham chiếu chưa có bảng – section <c>ReferenceLists</c> của appsettings.
    /// Khóa = tên danh mục (MealType, WasteType…), giá trị = danh sách mã + nhãn. Đổi giá trị không cần sửa code. PROPOSED – BA/PO CONFIRM.
    /// </summary>
    public sealed class ReferenceListsOptions
    {
        public const string Section = "ReferenceLists";

        public Dictionary<string, List<ReferenceItem>> Lists { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class ReferenceItem
    {
        public string Code { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }
}
