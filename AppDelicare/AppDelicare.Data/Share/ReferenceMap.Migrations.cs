namespace AppDelicare.Data.Share
{
    /// <summary>
    /// Quan hệ logic của cột THÊM BẰNG MIGRATION (không có trong DDL chuẩn docs/reference – CodeGen không sinh được). Sửa tay, CodeGen không ghi đè.
    /// UI-6.26 (D-107): DishCategorie.ProductionLineId → ProductionLine (dây chuyền đang gán cho nhóm món thì không xóa được).
    /// </summary>
    public static partial class ReferenceMap
    {
        public static readonly IReadOnlyDictionary<string, (string SourceTable, string Column)[]> IncomingFromMigrations = new Dictionary<string, (string, string)[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProductionLine"] = [("DishCategorie", "ProductionLineId")],
        };

        /// <summary>Nguồn tham chiếu của bảng = sinh từ DDL + cột thêm bằng migration.</summary>
        public static (string SourceTable, string Column)[] SourcesOf(string table)
            => [.. (Incoming.TryGetValue(table, out var a) ? a : []), .. (IncomingFromMigrations.TryGetValue(table, out var b) ? b : [])];
    }
}
