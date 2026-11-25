using AppDelicare.Data.Connection;
using AppDelicare.Model.Entity;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6.46 (D-125 c): đầu phiếu in / Excel lấy pháp nhân theo chứng từ – công ty ghi trên chứng từ (vd Customer.CompanyId) → công ty của đơn vị
    /// (OrganizationUnit.CompanyId, UI-6.41 "Công ty &amp; đơn vị") → công ty đầu tiên (như trước). Công ty đã xóa bỏ qua.
    /// </summary>
    public static class PrintCompany
    {
        public static async Task<Company?> ResolveAsync(DatabaseContext db, Guid? companyId = null, Guid? unitId = null)
        {
            if (companyId == null && unitId is { } uid)
            {
                companyId = await db.OrganizationUnit.AsNoTracking().Where(u => u.Id == uid && !u.IsDeleted).Select(u => (Guid?)u.CompanyId).FirstOrDefaultAsync();
            }
            if (companyId is { } cid && await db.Company.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cid && !c.IsDeleted) is { } own)
            {
                return own;
            }
            return await db.Company.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.CreatedDate).FirstOrDefaultAsync();
        }
    }
}
