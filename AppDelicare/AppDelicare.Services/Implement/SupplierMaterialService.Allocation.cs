using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request.Custom;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace AppDelicare.Services.Implement
{
    /// <summary>UI-6.51 (D-137 g, GAP-PUR-SPLIT): tỷ lệ chia đơn cho nhiều NCC trên bảng giá theo nguyên liệu.</summary>
    public partial class SupplierMaterialService
    {
        /// <summary>
        /// Lưu <c>AllocationPct</c> cho các dòng bảng giá của một nguyên liệu: mỗi giá trị 0 &lt; % ≤ 100 (trống / 0 = bỏ tỷ lệ); sau khi lưu, tổng các dòng đang hiệu lực
        /// có tỷ lệ phải = 100 (hoặc không dòng nào có tỷ lệ = chọn 1 NCC như cũ). Ghi lịch sử AuditChange.
        /// </summary>
        public async Task<IResult<bool>> SaveAllocation(SupplierMaterialAllocationRequest request)
        {
            var db = unitOfWork.DbContext;
            if (request.IngredientId == Guid.Empty)
            {
                return Result<bool>.Fail("Chọn nguyên liệu", false);
            }
            var rows = await db.SupplierMaterial.Where(m => m.IngredientId == request.IngredientId && !m.IsDeleted).ToListAsync();
            var unknown = request.Pct.Keys.Where(id => rows.All(r => r.Id != id)).ToList();
            if (unknown.Count > 0)
            {
                return Result<bool>.Fail("Dòng bảng giá không thuộc nguyên liệu này", false);
            }
            if (request.Pct.Values.Any(v => v is < 0 or > 100))
            {
                return Result<bool>.Fail("Tỷ lệ chia mỗi NCC phải từ 0 đến 100%", false);
            }
            DateTime today = clock.Today;
            decimal? After(SupplierMaterial r) => request.Pct.TryGetValue(r.Id, out var v) ? (v is > 0 ? v : null) : r.AllocationPct;
            var active = rows.Where(r => r.Status is null or "Active" && (r.EffectiveTo == null || r.EffectiveTo.Value.Date >= today)).ToList();
            decimal sum = active.Sum(r => After(r) ?? 0m);
            if (active.Any(r => After(r) != null) && Math.Abs(sum - 100m) > 0.01m)
            {
                return Result<bool>.Fail($"Tổng tỷ lệ chia các NCC phải bằng 100% (đang {sum:0.##}%)", false);
            }
            var changes = new List<object>();
            foreach (var r in rows.Where(r => request.Pct.ContainsKey(r.Id)))
            {
                decimal? v = After(r);
                if (v != r.AllocationPct)
                {
                    changes.Add(new { r.Id, r.SupplierId, from = r.AllocationPct, to = v });
                    r.AllocationPct = v;
                    r.ModifiedDate = clock.Now;
                }
            }
            if (changes.Count > 0)
            {
                db.AuditChange.Add(new AuditChange
                {
                    Id = Guid.NewGuid(), IdChange = request.IngredientId, EntityName = "SupplierMaterialAllocation", Action = "Update", Changes = JsonConvert.SerializeObject(changes),
                    CreatedDate = clock.Now, IsDeleted = false,
                });
                await db.SaveChangesAsync(AppDelicare.Model.Helper.ConstantHelper.TypeSave.Update);
            }
            return Result<bool>.Success(true, changes.Count == 0 ? "Không có thay đổi" : "Đã lưu tỷ lệ chia NCC");
        }
    }
}
