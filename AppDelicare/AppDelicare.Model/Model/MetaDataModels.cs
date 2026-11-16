namespace AppDelicare.Model.Model
{
    /// <summary>T45B.5: một bộ trạng thái (STATUS_CATALOG) – dùng vẽ badge và bộ lọc.</summary>
    public class StatusSetModel
    {
        /// <summary>"Bảng.Cột", vd. "Order.Status".</summary>
        public string Key { get; set; } = string.Empty;
        public string Table { get; set; } = string.Empty;
        public string Column { get; set; } = string.Empty;
        public List<StatusItemModel> ListItem { get; set; } = [];
        /// <summary>Nguồn: PROPOSED – BA/PO CONFIRM / Theo nguồn – không đổi…</summary>
        public string? Source { get; set; }
    }

    public class StatusItemModel
    {
        public string Code { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    /// <summary>T45B.5: một danh mục tham chiếu (MealType, WasteType…).</summary>
    public class ReferenceListModel
    {
        public string Name { get; set; } = string.Empty;
        public List<StatusItemModel> ListItem { get; set; } = [];
    }
}
