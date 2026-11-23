using AppDelicare.Data.Connection;
using AppDelicare.Model.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// MENU-4.7 (D-75 f3): lọc danh sách chứng từ (Đề nghị mua, Đơn mua, Phiếu nhập, Phiếu xuất) theo nguyên liệu – thay cho tra cứu ở bảng con đã ẩn.
    /// Lọc ở máy chủ: tham số <c>IngredientId</c> của action GetAll có sẵn; chỉ trả chứng từ có dòng chứa nguyên liệu, kèm tổng số lượng + ĐVT
    /// của nguyên liệu trên chứng từ. Không đổi stored procedure (DB thật không cần chạy script): khi có lọc, lấy hết kết quả của thủ tục rồi lọc / phân trang ở đây.
    /// </summary>
    public static class IngredientDocumentFilter
    {
        /// <summary>Số dòng tối đa đọc từ thủ tục khi có lọc nguyên liệu (chứng từ mua / kho – vài nghìn / năm).</summary>
        public const int AllRows = 100000;

        public readonly record struct LineQty(Guid DocumentId, decimal Quantity, Guid UnitId);

        public static async Task<Dictionary<Guid, (decimal Quantity, string? UnitName)>> SummarizeAsync(DatabaseContext db, List<LineQty> lines)
        {
            var unitIds = lines.Select(l => l.UnitId).Distinct().ToList();
            var names = await db.UnitMeasure.AsNoTracking().Where(u => unitIds.Contains(u.Id)).Select(u => new { u.Id, u.Name, u.Code }).ToListAsync();
            return lines.GroupBy(l => l.DocumentId).ToDictionary(g => g.Key, g =>
            {
                var unit = names.FirstOrDefault(n => n.Id == g.First().UnitId);
                return (g.Sum(x => x.Quantity), unit == null ? null : unit.Name ?? unit.Code);
            });
        }

        /// <summary>Giữ chứng từ có nguyên liệu, gán số lượng, phân trang lại (TotalRow = số chứng từ khớp).</summary>
        public static List<T> Apply<T>(List<T> all, Dictionary<Guid, (decimal Quantity, string? UnitName)> map, int page, int pageSize) where T : BaseViewModel, IIngredientQuantityRow
        {
            var hit = all.Where(x => map.ContainsKey(x.Id)).ToList();
            foreach (var x in hit)
            {
                (x.IngredientQuantity, x.IngredientUnitName) = map[x.Id];
                x.TotalRow = hit.Count;
            }
            int size = pageSize <= 0 ? hit.Count : pageSize;
            return [.. hit.Skip(Math.Max(0, page - 1) * size).Take(size)];
        }
    }
}
