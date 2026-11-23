namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// BIZ-1.1 (D-77 a, anh chốt 2026-11-23 – thay chốt T14.1 "× (1 + hao hụt %)"): hao hụt 2 tầng, công thức CHIA như file vận hành thật (W3):
    /// <list type="bullet">
    /// <item>Định lượng tinh = <c>RecipeLine.Quantity</c> (sau chế biến).</item>
    /// <item>Sau sơ chế = tinh ÷ (1 − % hao hụt chế biến), % chế biến = <c>RecipeLine.ExpectedWasteRate</c> (theo dòng / phương pháp).</item>
    /// <item>Trước sơ chế (lượng mua) = sau sơ chế ÷ (1 − % hao hụt sơ chế), % sơ chế = <c>RecipeLine.WastePercent</c>, trống = <c>Ingredient.StandardWastePercent</c>.</item>
    /// </list>
    /// Kiểm chứng: Thịt hầm đậu đỏ 70 g, chế biến 35 %, sơ chế 5 % → sau sơ chế 107,7 g, lượng mua 113,4 g / suất.
    /// Một chỗ tính dùng chung: MRP, nguyên liệu của mẻ, giữ chỗ / phiếu xuất theo định mức, giá vốn công thức (giá thành chuẩn).
    /// % ngoài [0, 100) bị bỏ qua (coi 0) – nhập liệu đã chặn ở RecipeLine / Ingredient / nhập Excel.
    /// </summary>
    public static class WasteRules
    {
        private static decimal Pct(decimal? p) => p is { } v && v > 0m && v < 100m ? v : 0m;

        /// <summary>Lượng sau sơ chế (trước chế biến) từ định lượng tinh.</summary>
        public static decimal AfterPrep(decimal net, decimal? cookPct) => net / (1m - Pct(cookPct) / 100m);

        /// <summary>Lượng mua (trước sơ chế) từ định lượng tinh.</summary>
        public static decimal Gross(decimal net, decimal? prepPct, decimal? cookPct) => AfterPrep(net, cookPct) / (1m - Pct(prepPct) / 100m);
    }
}
